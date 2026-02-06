using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TaleSoft.UzakMasaustuAraci.Win.UI.ServerApp
{
    public partial class Form1 : Form
    {
        TcpListener server;
        Thread dinlemeThread;
        NetworkStream anaStream;

        // HIZ ÖLÇÜM DEĞİŞKENLERİ
        int kareSayisi = 0;       // O saniyedeki FPS
        long gelenVeriBoyutu = 0; // O saniyedeki KB

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            dinlemeThread = new Thread(SunucuyuBaslat);
            dinlemeThread.IsBackground = true;
            dinlemeThread.Start();
        }

        private void SunucuyuBaslat()
        {
            try
            {
                server = new TcpListener(IPAddress.Parse("127.0.0.1"), 5000);
                server.Start();

                while (true)
                {
                    try
                    {
                        TcpClient client = server.AcceptTcpClient();
                        client.NoDelay = true;
                        anaStream = client.GetStream();

                        // --- ADIM 1: ÖNCE KİMLİK BİLGİSİNİ OKU ---
                        // Client ilk bağlandığında "INFO|..." diye bir yazı atıyor.
                        // Bunu okumak için StreamReader kullanalım ama Stream'i kapatmasın diye dikkat edelim.
                        // Basitçe byte olarak okuyup string'e çevirelim.

                        byte[] buffer = new byte[1024];
                        int okunan = anaStream.Read(buffer, 0, buffer.Length);
                        string ilkMesaj = Encoding.UTF8.GetString(buffer, 0, okunan);

                        // Mesajın içinden bilgileri çek (INFO|PC|USER|OS)
                        if (ilkMesaj.StartsWith("INFO"))
                        {
                            string[] parcalar = ilkMesaj.Split('|');
                            // parcalar[0] = INFO
                            // parcalar[1] = PC Adı
                            // parcalar[2] = Kullanıcı
                            // parcalar[3] = OS (veya fazlası)

                            string bilgiMetni = $"Bağlı PC: {parcalar[1]} / Kullanıcı: {parcalar[2]}";

                            // Label'a yaz
                            lblPCBilgi.Invoke((MethodInvoker)delegate {
                                lblPCBilgi.Text = bilgiMetni;
                                lblPCBilgi.ForeColor = Color.Blue;
                            });
                        }
                        // ------------------------------------------

                        // Görüntü Döngüsü Başlıyor
                        while (true)
                        {
                            // 1. Header Oku
                            byte[] boyutBuffer = new byte[4];
                            int hOkunan = 0;
                            while (hOkunan < 4)
                            {
                                int k = anaStream.Read(boyutBuffer, hOkunan, 4 - hOkunan);
                                if (k == 0) throw new Exception("Koptu");
                                hOkunan += k;
                            }
                            int boyut = BitConverter.ToInt32(boyutBuffer, 0);

                            if (boyut < 0 || boyut > 10000000) continue;

                            // 2. Resim Oku
                            byte[] resimBuffer = new byte[boyut];
                            int tOkunan = 0;
                            while (tOkunan < boyut)
                            {
                                int k = anaStream.Read(resimBuffer, tOkunan, boyut - tOkunan);
                                if (k == 0) throw new Exception("Koptu");
                                tOkunan += k;
                            }

                            // --- İSTATİSTİK GÜNCELLEME ---
                            kareSayisi++;               // 1 kare daha geldi
                            gelenVeriBoyutu += boyut;   // Şu kadar byte geldi
                            // -----------------------------

                            using (MemoryStream ms = new MemoryStream(resimBuffer))
                            {
                                Image img = Image.FromStream(ms);
                                pictureBox1.Invoke((MethodInvoker)delegate {
                                    if (pictureBox1.Image != null) pictureBox1.Image.Dispose();
                                    pictureBox1.Image = new Bitmap(img);
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lblPCBilgi.Invoke((MethodInvoker)delegate {
                            lblPCBilgi.Text = "Bağlantı Bekleniyor...";
                            lblPCBilgi.ForeColor = Color.Black;
                        });
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Server Hatası: " + ex.Message); }
        }

        // --- TIMER: HER SANİYE HIZI EKRANA YAZAR ---

        int sunucuTarafiKalite = 30;
        private void timer1_Tick(object sender, EventArgs e)
        {
            // 1. Hızı Ekrana Yaz
            double kbHiz = gelenVeriBoyutu / 1024.0;
            lblHiz.Text = $"FPS: {kareSayisi} - Hız: {kbHiz:0.0} KB/s - Kalite: %{sunucuTarafiKalite}";

            // 2. OTOMATİK KALİTE AYARI (ADAPTIVE QUALITY)
            // Eğer bağlantı varsa ve veri akıyorsa kararı ver
            if (anaStream != null && kareSayisi > 0)
            {
                bool degisiklikGerekli = false;

                // Durum: Çok Yavaş (Kare sayısı 8'in altındaysa kalite düşür)
                if (kareSayisi < 8 && sunucuTarafiKalite > 10)
                {
                    sunucuTarafiKalite -= 10; // Kaliteyi 10 puan düşür
                    degisiklikGerekli = true;
                }
                // Durum: Çok Hızlı (Kare sayısı 20'nin üstündeyse kalite arttır)
                else if (kareSayisi > 20 && sunucuTarafiKalite < 80)
                {
                    sunucuTarafiKalite += 10; // Kaliteyi 10 puan arttır
                    degisiklikGerekli = true;
                }

                // Eğer karar değiştiyse Müşteriye bildir
                if (degisiklikGerekli)
                {
                    try
                    {
                        // Komut Formatı: KALITE|20
                        string komut = $"KALITE|{sunucuTarafiKalite}\n";
                        byte[] veri = Encoding.UTF8.GetBytes(komut);
                        lock (anaStream) { anaStream.Write(veri, 0, veri.Length); }
                    }
                    catch { }
                }
            }

            // Sayaçları sıfırla
            kareSayisi = 0;
            gelenVeriBoyutu = 0;
        }

        // --- FARE VE KLAVYE EVENTLERİ (Aynen Kalıyor) ---
        private void KomutGonder(string tip, int x, int y)
        {
            if (anaStream != null)
            {
                try
                {
                    float xOran = (float)x / pictureBox1.Width;
                    float yOran = (float)y / pictureBox1.Height;
                    string komut = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}|{1}|{2}\n", tip, xOran, yOran);
                    byte[] veri = Encoding.UTF8.GetBytes(komut);
                    lock (anaStream) { anaStream.Write(veri, 0, veri.Length); }
                }
                catch { }
            }
        }
        private void KomutGonder(string tip, int tus)
        {
            if (anaStream != null)
            {
                try
                {
                    string komut = $"{tip}|{tus}\n";
                    byte[] veri = Encoding.UTF8.GetBytes(komut);
                    lock (anaStream) { anaStream.Write(veri, 0, veri.Length); }
                }
                catch { }
            }
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) KomutGonder("MOUSE_DOWN", e.X, e.Y);
            else if (e.Button == MouseButtons.Right) KomutGonder("MOUSE_R_DOWN", e.X, e.Y);
        }
        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) KomutGonder("MOUSE_UP", e.X, e.Y);
            else if (e.Button == MouseButtons.Right) KomutGonder("MOUSE_R_UP", e.X, e.Y);
        }
        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) KomutGonder("MOUSE_MOVE", e.X, e.Y);
        }
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            KomutGonder("KEY_DOWN", e.KeyValue);
            e.SuppressKeyPress = true;
        }
        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            KomutGonder("KEY_UP", e.KeyValue);
        }
    }
}