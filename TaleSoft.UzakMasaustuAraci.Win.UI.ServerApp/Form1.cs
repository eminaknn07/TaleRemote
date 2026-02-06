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

        int kareSayisi = 0;      
        long gelenVeriBoyutu = 0; 

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

                        byte[] buffer = new byte[1024];
                        int okunan = anaStream.Read(buffer, 0, buffer.Length);
                        string ilkMesaj = Encoding.UTF8.GetString(buffer, 0, okunan);

                        if (ilkMesaj.StartsWith("INFO"))
                        {
                            string[] parcalar = ilkMesaj.Split('|');

                            string bilgiMetni = $"Bağlı PC: {parcalar[1]} / Kullanıcı: {parcalar[2]}";

                            lblPCBilgi.Invoke((MethodInvoker)delegate {
                                lblPCBilgi.Text = bilgiMetni;
                                lblPCBilgi.ForeColor = Color.Blue;
                            });
                        }
                        while (true)
                        {
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

                            byte[] resimBuffer = new byte[boyut];
                            int tOkunan = 0;
                            while (tOkunan < boyut)
                            {
                                int k = anaStream.Read(resimBuffer, tOkunan, boyut - tOkunan);
                                if (k == 0) throw new Exception("Koptu");
                                tOkunan += k;
                            }

                            kareSayisi++;              
                            gelenVeriBoyutu += boyut;   

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


        int sunucuTarafiKalite = 30;
        private void timer1_Tick(object sender, EventArgs e)
        {
            double kbHiz = gelenVeriBoyutu / 1024.0;
            lblHiz.Text = $"FPS: {kareSayisi} - Hız: {kbHiz:0.0} KB/s - Kalite: %{sunucuTarafiKalite}";

            if (anaStream != null && kareSayisi > 0)
            {
                bool degisiklikGerekli = false;

                if (kareSayisi < 8 && sunucuTarafiKalite > 10)
                {
                    sunucuTarafiKalite -= 10; 
                    degisiklikGerekli = true;
                }
                else if (kareSayisi > 20 && sunucuTarafiKalite < 80)
                {
                    sunucuTarafiKalite += 10;
                    degisiklikGerekli = true;
                }

                if (degisiklikGerekli)
                {
                    try
                    {
                        string komut = $"KALITE|{sunucuTarafiKalite}\n";
                        byte[] veri = Encoding.UTF8.GetBytes(komut);
                        lock (anaStream) { anaStream.Write(veri, 0, veri.Length); }
                    }
                    catch { }
                }
            }

            kareSayisi = 0;
            gelenVeriBoyutu = 0;
        }

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