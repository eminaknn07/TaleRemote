using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TaleSoft.UzakMasaustuAraci.Win.UI.ClientApp
{
    public partial class Form1 : Form
    {
        // --- ROBOT ELİ ---
        [DllImport("user32.dll")]
        static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);
        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int X, int Y);
        private const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const int MOUSEEVENTF_LEFTUP = 0x0004;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x0008; // Sağ Tık Bas
        private const int MOUSEEVENTF_RIGHTUP = 0x0010;   // Sağ Tık Bırak
        private long aktifKalite = 30L;

        // Klavye Sabitleri
        private const int KEYEVENTF_KEYUP = 0x0002;

        TcpClient client;
        NetworkStream stream;
        Thread anaIslemThread;

        // TRAFİK POLİSİ (Senkronizasyon)
        // Başlangıçta true yapıyoruz ki ilk resmi hemen yollasın
        AutoResetEvent onayBekleyicisi = new AutoResetEvent(true);

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            lblDurum.Text = "Hazırlanıyor...";
            anaIslemThread = new Thread(Baslat);
            anaIslemThread.IsBackground = true;
            anaIslemThread.Start();
        }

        private void Baslat()
        {
            while (true)
            {
                try
                {
                    DurumGuncelle("Sunucu aranıyor...", Color.OrangeRed);

                    // Ngrok Ayarları
                    client = new TcpClient("2.tcp.eu.ngrok.io", 13492);
                    client.NoDelay = true;
                    stream = client.GetStream();

                    DurumGuncelle("Bağlandı! Kimlik gönderiliyor...", Color.Yellow);

                    // --- YENİ BÖLÜM: BİLGİSAYAR BİLGİLERİNİ GÖNDER ---
                    // Format: PCAdi|KullaniciAdi|OS
                    string pcAdi = Environment.MachineName;
                    string kullanici = Environment.UserName;
                    string os = Environment.OSVersion.ToString(); // Basit OS bilgisi

                    string kimlikBilgisi = $"INFO|{pcAdi}|{kullanici}|{os}\n"; // Başına INFO koyduk, sonuna \n
                    byte[] kimlikBytes = Encoding.UTF8.GetBytes(kimlikBilgisi);

                    stream.Write(kimlikBytes, 0, kimlikBytes.Length);
                    stream.Flush();
                    // --------------------------------------------------

                    DurumGuncelle("Bağlandı! Görüntü gidiyor.", Color.Green);

                    // Dinleme ve Görüntü gönderme işlemleri devam ediyor...
                    Thread dinleme = new Thread(KomutlariVeOnayiDinle);
                    dinleme.IsBackground = true;
                    dinleme.Start();

                    EkranGonder();
                }
                catch
                {
                    DurumGuncelle("Bağlantı koptu. 3 sn sonra tekrar denenecek.", Color.Red);
                    Thread.Sleep(3000);
                }
            }
        }

        private void EkranGonder()
        {
            ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);
            System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;
            EncoderParameters myEncoderParameters = new EncoderParameters(1);

            while (client.Connected)
            {
                try
                {
                    // --- GÜNCELLEME: Kalite artık dinamik ---
                    // 'aktifKalite' değişkeni IsleKomut tarafından değiştirilecek
                    myEncoderParameters.Param[0] = new EncoderParameter(myEncoder, aktifKalite);

                    int genislik = Screen.PrimaryScreen.Bounds.Width;
                    int yukseklik = Screen.PrimaryScreen.Bounds.Height;

                    using (Bitmap ekran = new Bitmap(genislik, yukseklik))
                    {
                        using (Graphics g = Graphics.FromImage(ekran))
                        {
                            g.CopyFromScreen(0, 0, 0, 0, ekran.Size);
                        }

                        // Yarı yarıya küçültmeye devam (Performans için)
                        using (Bitmap kucukResim = new Bitmap(ekran, new Size(genislik / 2, yukseklik / 2)))
                        {
                            byte[] resimBytes;
                            using (MemoryStream ms = new MemoryStream())
                            {
                                // Dinamik kalite ile kaydet
                                kucukResim.Save(ms, jpgEncoder, myEncoderParameters);
                                resimBytes = ms.ToArray();
                            }

                            byte[] boyut = BitConverter.GetBytes(resimBytes.Length);

                            lock (stream)
                            {
                                stream.Write(boyut, 0, boyut.Length);
                                stream.Write(resimBytes, 0, resimBytes.Length);
                                stream.Flush();
                            }
                        }
                    }

                    // Bekleme süresi (FPS ayarı)
                    Thread.Sleep(50);
                }
                catch { break; }
            }
        }
        private void IsleKomut(string komutSatiri)
        {
            try
            {
                string[] parcalar = komutSatiri.Split('|');
                string komutTipi = parcalar[0];

                // --- GÜNCELLEME: KALİTE AYARI ---
                if (komutTipi == "KALITE")
                {
                    long yeniKalite = long.Parse(parcalar[1]);

                    // Sınırlandırma (Çok bozulmasın veya şişmesin)
                    if (yeniKalite < 5) yeniKalite = 5;
                    if (yeniKalite > 80) yeniKalite = 80;

                    aktifKalite = yeniKalite; // Değişkeni güncelle
                }
                // --- MOUSE İŞLEMLERİ ---
                else if (komutTipi.StartsWith("MOUSE"))
                {
                    var culture = System.Globalization.CultureInfo.InvariantCulture;
                    float xYuzde = float.Parse(parcalar[1], culture);
                    float yYuzde = float.Parse(parcalar[2], culture);

                    int hedefX = (int)(Screen.PrimaryScreen.Bounds.Width * xYuzde);
                    int hedefY = (int)(Screen.PrimaryScreen.Bounds.Height * yYuzde);

                    SetCursorPos(hedefX, hedefY);

                    switch (komutTipi)
                    {
                        case "MOUSE_DOWN":
                            mouse_event(MOUSEEVENTF_LEFTDOWN, hedefX, hedefY, 0, 0); break;
                        case "MOUSE_UP":
                            mouse_event(MOUSEEVENTF_LEFTUP, hedefX, hedefY, 0, 0); break;
                        case "MOUSE_R_DOWN":
                            mouse_event(MOUSEEVENTF_RIGHTDOWN, hedefX, hedefY, 0, 0); break;
                        case "MOUSE_R_UP":
                            mouse_event(MOUSEEVENTF_RIGHTUP, hedefX, hedefY, 0, 0); break;
                        case "MOUSE_MOVE": break; // Zaten SetCursorPos ile gitti
                    }
                }
                // --- KLAVYE İŞLEMLERİ ---
                else if (komutTipi == "KEY_DOWN")
                {
                    byte tusKodu = byte.Parse(parcalar[1]);
                    keybd_event(tusKodu, 0, 0, 0);
                }
                else if (komutTipi == "KEY_UP")
                {
                    byte tusKodu = byte.Parse(parcalar[1]);
                    keybd_event(tusKodu, 0, KEYEVENTF_KEYUP, 0);
                }
            }
            catch { }
        }
        private void KomutlariVeOnayiDinle()
        {
            try
            {
                StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                while (client.Connected)
                {
                    string satir = reader.ReadLine();
                    if (string.IsNullOrEmpty(satir)) continue;

                    if (satir == "ACK")
                    {
                        onayBekleyicisi.Set();
                    }
                    else
                    {
                        // DÜZELTME BURADA:
                        // Eskiden sadece "TIKLA" varsa işliyorduk.
                        // Şimdi ne gelirse gelsin (MOUSE, KEY vs.) işleyiciye gönderiyoruz.
                        IsleKomut(satir);
                    }
                }
            }
            catch { }
        }

     

        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid) return codec;
            }
            return null;
        }

        private void DurumGuncelle(string mesaj, Color renk)
        {
            if (lblDurum.InvokeRequired)
            {
                lblDurum.Invoke((MethodInvoker)delegate {
                    lblDurum.Text = mesaj;
                    lblDurum.ForeColor = renk;
                });
            }
        }
    }
}