// Pausas Frozz - recordatorios de pausas activas, descanso visual, hidratación,
// estiramientos y postura. Basado en el instructivo SST "Ergonomía en puestos
// administrativos y uso de pantallas" de Frozz Colombia.
// Compila con el csc.exe de .NET Framework 4 (C# 5): ver build.ps1
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Pausas Frozz")]
[assembly: AssemblyDescription("Recordatorios de pausas activas, descanso visual e hidratación - SST Frozz Colombia")]
[assembly: AssemblyCompany("Frozz Colombia")]
[assembly: AssemblyProduct("Pausas Frozz")]
[assembly: AssemblyVersion(PausasFrozz.AppInfo.Version)]
[assembly: AssemblyFileVersion(PausasFrozz.AppInfo.Version)]

namespace PausasFrozz
{
    static class AppInfo
    {
        // Para publicar una actualización: subir este número y hacer push a main.
        public const string Version = "1.1.0";
        public const string Repo = "smedina-cpu/pausas-frozz";
    }

    // ------------------------------------------------------- actualizador
    // Lee el último Release público de GitHub (sin cuenta ni token):
    //   https://github.com/<repo>/releases/latest/download/version.txt
    //   https://github.com/<repo>/releases/latest/download/PausasFrozz.exe
    static class Updater
    {
        public class Info { public Version Version; public string Sha256; public string Notes; }

        static string BaseUrl { get { return "https://github.com/" + AppInfo.Repo + "/releases/latest/download/"; } }

        public static Version Norm(Version v) { return new Version(v.Major, v.Minor, Math.Max(0, v.Build)); }
        public static Version Current { get { return Norm(Assembly.GetExecutingAssembly().GetName().Version); } }

        static WebClient Client()
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
            WebClient wc = new WebClient();
            wc.Encoding = Encoding.UTF8;
            wc.Headers[HttpRequestHeader.UserAgent] = "PausasFrozz/" + AppInfo.Version;
            wc.Headers[HttpRequestHeader.CacheControl] = "no-cache";
            return wc;
        }

        public static Info Check()
        {
            string txt;
            using (WebClient wc = Client()) txt = wc.DownloadString(BaseUrl + "version.txt");
            Info i = new Info();
            foreach (string raw in txt.Split('\n'))
            {
                string line = raw.Trim();
                int p = line.IndexOf('=');
                if (p <= 0) continue;
                string k = line.Substring(0, p).Trim().ToLowerInvariant(), v = line.Substring(p + 1).Trim();
                if (k == "version") i.Version = Norm(new Version(v));
                else if (k == "sha256") i.Sha256 = v.ToLowerInvariant();
                else if (k == "notas") i.Notes = v;
            }
            if (i.Version == null) throw new Exception("version.txt sin versión");
            return i;
        }

        static string ExePath { get { return Application.ExecutablePath; } }

        // Descarga junto al .exe actual y verifica la huella SHA-256
        public static string Download(Info info)
        {
            string tmp = ExePath + ".new";
            if (File.Exists(tmp)) File.Delete(tmp);
            using (WebClient wc = Client()) wc.DownloadFile(BaseUrl + "PausasFrozz.exe", tmp);
            string hash;
            using (FileStream fs = File.OpenRead(tmp))
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
            if (!string.IsNullOrEmpty(info.Sha256) && hash != info.Sha256)
            {
                File.Delete(tmp);
                throw new Exception("La descarga está incompleta o dañada (la huella no coincide).");
            }
            return tmp;
        }

        // Windows permite renombrar un .exe en ejecución: se aparta el actual y se pone el nuevo.
        public static void Apply(string newFile)
        {
            string exe = ExePath, old = exe + ".old";
            if (File.Exists(old)) File.Delete(old);
            File.Move(exe, old);
            try { File.Move(newFile, exe); }
            catch { if (!File.Exists(exe)) File.Move(old, exe); throw; }
            Process.Start(exe, "--updated");
        }

        public static void Cleanup()
        {
            foreach (string s in new string[] { ".old", ".new" })
                try { if (File.Exists(ExePath + s)) File.Delete(ExePath + s); } catch { }
        }
    }
    // ------------------------------------------------------------------ marca
    static class Brand
    {
        public static readonly Color Navy = Color.FromArgb(13, 42, 107);
        public static readonly Color Blue = Color.FromArgb(31, 92, 184);
        public static readonly Color Green = Color.FromArgb(110, 184, 52);
        public static readonly Color GreenDark = Color.FromArgb(52, 130, 40);
        public static readonly Color Yellow = Color.FromArgb(247, 210, 0);
        public static readonly Color Light = Color.FromArgb(237, 243, 251);
        public static readonly Color Line = Color.FromArgb(205, 214, 228);
        public static readonly Color Text = Color.FromArgb(28, 36, 54);
        public static readonly Color Muted = Color.FromArgb(98, 110, 132);
        public static float Scale = 1f;
        public static int S(float v) { return (int)Math.Round(v * Scale); }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // ------------------------------------------------------------- Win32
    static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
        [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);

        public static TimeSpan IdleTime()
        {
            LASTINPUTINFO l = new LASTINPUTINFO();
            l.cbSize = (uint)Marshal.SizeOf(l);
            if (!GetLastInputInfo(ref l)) return TimeSpan.Zero;
            uint now = unchecked((uint)Environment.TickCount);
            return TimeSpan.FromMilliseconds(unchecked(now - l.dwTime));
        }

        // true si hay una app en pantalla completa, presentación o juego
        public static bool UserBusy()
        {
            try
            {
                int s;
                if (SHQueryUserNotificationState(out s) != 0) return false;
                return s == 2 || s == 3 || s == 4;
            }
            catch { return false; }
        }

        public static void RoundCorners(IntPtr h)
        {
            try { int v = 2; DwmSetWindowAttribute(h, 33, ref v, 4); } catch { }
        }
    }

    static class Sonido
    {
        public static void Chime()
        {
            ThreadPool.QueueUserWorkItem(delegate { try { Console.Beep(880, 110); Console.Beep(1175, 170); } catch { } });
        }
        public static void Tick()
        {
            ThreadPool.QueueUserWorkItem(delegate { try { Console.Beep(988, 90); } catch { } });
        }
        public static void Done()
        {
            ThreadPool.QueueUserWorkItem(delegate { try { Console.Beep(784, 120); Console.Beep(988, 120); Console.Beep(1319, 220); } catch { } });
        }
    }

    // ------------------------------------------------------- recordatorios
    class Step
    {
        public string Text; public int Seconds;
        public Step(string t, int s) { Text = t; Seconds = s; }
    }

    class ReminderDef
    {
        public string Key, Name, Short, Plural, Glyph, Title, Intro, Tip, DoneText;
        public int DefaultMinutes;
        public bool ResetOnAway, Checklist, ResetsVista;
        public List<Step> Steps = new List<Step>();
        public int TotalSeconds { get { return Checklist ? 0 : Steps.Sum(s => s.Seconds); } }
        public bool Timed { get { return TotalSeconds > 0; } }
    }

    static class Catalogo
    {
        public static List<ReminderDef> Todos()
        {
            List<ReminderDef> l = new List<ReminderDef>();
            ReminderDef d;

            d = new ReminderDef();
            d.Key = "vista"; d.Name = "Descanso visual (regla 20-20-20)"; d.Short = "Vista"; d.Plural = "Descansos visuales";
            d.Glyph = "eye"; d.DefaultMinutes = 20; d.ResetOnAway = true;
            d.Title = "Descansa la vista";
            d.Intro = "Regla 20-20-20: cada 20 minutos mira algo a 20 pies (6 m) de distancia durante 20 segundos.";
            d.Steps.Add(new Step("Aparta la mirada de la pantalla y enfoca un objeto lejano, a unos 6 metros: una ventana, el fondo de la oficina.", 20));
            d.Tip = "Parpadea varias veces: frente a la pantalla parpadeamos menos y los ojos se resecan.";
            d.DoneText = "Listo";
            l.Add(d);

            d = new ReminderDef();
            d.Key = "agua"; d.Name = "Hidratación"; d.Short = "Agua"; d.Plural = "Vasos de agua";
            d.Glyph = "drop"; d.DefaultMinutes = 60; d.ResetOnAway = false;
            d.Title = "Hora de hidratarte";
            d.Intro = "Toma un vaso de agua. Mantenerte hidratado ayuda a la concentración y reduce la fatiga.";
            d.Tip = "Ten tu termo o botella a la mano en el puesto de trabajo.";
            d.DoneText = "Ya tomé agua";
            l.Add(d);

            d = new ReminderDef();
            d.Key = "estiramiento"; d.Name = "Estiramientos de cuello, hombros y espalda"; d.Short = "Estiramientos"; d.Plural = "Rutinas de estiramiento";
            d.Glyph = "stretch"; d.DefaultMinutes = 60; d.ResetOnAway = true; d.ResetsVista = true;
            d.Title = "Estira cuello, hombros y espalda";
            d.Intro = "Unos 2 minutos de estiramientos suaves. Muévete despacio, sin rebotes y sin llegar al dolor.";
            d.Steps.Add(new Step("Cuello: inclina la cabeza llevando la oreja hacia el hombro derecho. Sostén.", 15));
            d.Steps.Add(new Step("Cuello: ahora hacia el hombro izquierdo. Sostén.", 15));
            d.Steps.Add(new Step("Cuello: baja el mentón hacia el pecho y siente el estiramiento en la nuca.", 15));
            d.Steps.Add(new Step("Hombros: haz círculos lentos con los hombros hacia atrás.", 15));
            d.Steps.Add(new Step("Hombros: entrelaza los dedos y estira los brazos al frente, redondeando la espalda alta.", 15));
            d.Steps.Add(new Step("Espalda: brazos arriba, dedos entrelazados, estírate hacia el techo.", 15));
            d.Steps.Add(new Step("Espalda: sentado, gira el tronco hacia la derecha apoyándote en el espaldar. Sostén.", 15));
            d.Steps.Add(new Step("Espalda: ahora gira el tronco hacia la izquierda. Sostén.", 15));
            d.Steps.Add(new Step("Muñecas: brazo al frente, palma hacia afuera; con la otra mano lleva los dedos hacia ti. Cambia de mano a la mitad.", 20));
            d.Tip = "Si una molestia persiste, repórtala a SST.";
            d.DoneText = "Terminé";
            l.Add(d);

            d = new ReminderDef();
            d.Key = "pausa"; d.Name = "Pausa activa"; d.Short = "Pausa activa"; d.Plural = "Pausas activas";
            d.Glyph = "walk"; d.DefaultMinutes = 90; d.ResetOnAway = true; d.ResetsVista = true;
            d.Title = "Pausa activa: levántate";
            d.Intro = "Llevas un buen rato en la silla. Ponte de pie y activa la circulación durante unos 3 minutos.";
            d.Steps.Add(new Step("Ponte de pie y camina: ve por agua, sube y baja escaleras o da una vuelta por la oficina.", 60));
            d.Steps.Add(new Step("Marcha en tu sitio levantando las rodillas.", 30));
            d.Steps.Add(new Step("Ponte en puntas de pie y baja despacio. Repite unas 10 veces.", 30));
            d.Steps.Add(new Step("Haz círculos con los tobillos y sacude suavemente manos y brazos.", 20));
            d.Steps.Add(new Step("Manos en la cintura: abre el pecho y lleva los hombros hacia atrás.", 20));
            d.Steps.Add(new Step("Respira profundo 3 veces: inhala por la nariz y exhala lento por la boca.", 20));
            d.Tip = "Al volver, alterna tareas y evita giros repetidos del cuello.";
            d.DoneText = "Terminé";
            l.Add(d);

            d = new ReminderDef();
            d.Key = "postura"; d.Name = "Revisión de postura"; d.Short = "Postura"; d.Plural = "Revisiones de postura";
            d.Glyph = "posture"; d.DefaultMinutes = 120; d.ResetOnAway = false; d.Checklist = true;
            d.Title = "Revisa tu postura";
            d.Intro = "Un puesto bien ajustado también es una medida de prevención. Verifica:";
            d.Steps.Add(new Step("Espalda recta y bien apoyada en el espaldar.", 0));
            d.Steps.Add(new Step("Pantalla a la altura de los ojos, a 50–70 cm.", 0));
            d.Steps.Add(new Step("Teclado y mouse al alcance, brazos en ángulo de 90°.", 0));
            d.Steps.Add(new Step("Pies apoyados en el suelo.", 0));
            d.Steps.Add(new Step("Elementos de uso frecuente al alcance, sin giros repetidos del cuello.", 0));
            d.Tip = "Reporta a SST las molestias persistentes.";
            d.DoneText = "Mi postura está bien";
            l.Add(d);

            return l;
        }
    }

    // ------------------------------------------------------------ config
    class Config
    {
        public Dictionary<string, bool> On = new Dictionary<string, bool>();
        public Dictionary<string, int> Min = new Dictionary<string, int>();
        public bool UseSchedule = true;
        public TimeSpan Start = new TimeSpan(7, 0, 0);
        public TimeSpan End = new TimeSpan(17, 30, 0);
        public bool[] Days = { false, true, true, true, true, true, false }; // índice = DayOfWeek (0 = domingo)
        public int IdleMinutes = 5;
        public int SnoozeMinutes = 5;
        public bool Sound = true;
        public bool Startup = true;
        public bool FirstRun = true;

        public static string Dir
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("PAUSASFROZZ_DATA");
                if (!string.IsNullOrEmpty(env)) return env;
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PausasFrozz");
            }
        }
        static string FilePath { get { return Path.Combine(Dir, "config.txt"); } }

        public static Config Load(List<ReminderDef> defs)
        {
            Config c = new Config();
            foreach (ReminderDef d in defs) { c.On[d.Key] = true; c.Min[d.Key] = d.DefaultMinutes; }
            try
            {
                if (!File.Exists(FilePath)) return c;
                foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int i = raw.IndexOf('=');
                    if (i <= 0) continue;
                    string k = raw.Substring(0, i).Trim(), v = raw.Substring(i + 1).Trim();
                    int n;
                    if (k.EndsWith(".on")) c.On[k.Substring(0, k.Length - 3)] = v == "1";
                    else if (k.EndsWith(".min") && int.TryParse(v, out n)) c.Min[k.Substring(0, k.Length - 4)] = Math.Max(1, Math.Min(480, n));
                    else if (k == "horario") c.UseSchedule = v == "1";
                    else if (k == "inicio") c.Start = ParseHM(v, c.Start);
                    else if (k == "fin") c.End = ParseHM(v, c.End);
                    else if (k == "dias")
                    {
                        for (int j = 0; j < 7; j++) c.Days[j] = false;
                        foreach (string p in v.Split(',')) { int dd; if (int.TryParse(p, out dd) && dd >= 0 && dd < 7) c.Days[dd] = true; }
                    }
                    else if (k == "ausente" && int.TryParse(v, out n)) c.IdleMinutes = Math.Max(1, Math.Min(60, n));
                    else if (k == "posponer" && int.TryParse(v, out n)) c.SnoozeMinutes = Math.Max(1, Math.Min(60, n));
                    else if (k == "sonido") c.Sound = v == "1";
                    else if (k == "inicio_windows") c.Startup = v == "1";
                    else if (k == "primera_vez") c.FirstRun = v == "1";
                }
            }
            catch { }
            return c;
        }

        static TimeSpan ParseHM(string v, TimeSpan def)
        {
            TimeSpan t;
            return TimeSpan.TryParseExact(v, @"hh\:mm", CultureInfo.InvariantCulture, out t) ? t : def;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# Pausas Frozz - configuración");
                foreach (KeyValuePair<string, bool> kv in On) sb.AppendLine(kv.Key + ".on=" + (kv.Value ? "1" : "0"));
                foreach (KeyValuePair<string, int> kv in Min) sb.AppendLine(kv.Key + ".min=" + kv.Value);
                sb.AppendLine("horario=" + (UseSchedule ? "1" : "0"));
                sb.AppendLine("inicio=" + Start.ToString(@"hh\:mm"));
                sb.AppendLine("fin=" + End.ToString(@"hh\:mm"));
                List<string> ds = new List<string>();
                for (int j = 0; j < 7; j++) if (Days[j]) ds.Add(j.ToString());
                sb.AppendLine("dias=" + string.Join(",", ds));
                sb.AppendLine("ausente=" + IdleMinutes);
                sb.AppendLine("posponer=" + SnoozeMinutes);
                sb.AppendLine("sonido=" + (Sound ? "1" : "0"));
                sb.AppendLine("inicio_windows=" + (Startup ? "1" : "0"));
                sb.AppendLine("primera_vez=" + (FirstRun ? "1" : "0"));
                File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public bool InSchedule(DateTime t)
        {
            if (!UseSchedule) return true;
            if (!Days[(int)t.DayOfWeek]) return false;
            return t.TimeOfDay >= Start && t.TimeOfDay < End;
        }

        public string ScheduleText()
        {
            string[] n = { "D", "L", "M", "X", "J", "V", "S" };
            int[] order = { 1, 2, 3, 4, 5, 6, 0 };
            List<string> ds = new List<string>();
            foreach (int i in order) if (Days[i]) ds.Add(n[i]);
            return string.Join(" ", ds) + "  " + Start.ToString(@"hh\:mm") + "–" + End.ToString(@"hh\:mm");
        }
    }

    // --------------------------------------------------------- historial
    static class Historial
    {
        public static string FilePath { get { return Path.Combine(Config.Dir, "historial.csv"); } }
        static Dictionary<string, int> hoy = new Dictionary<string, int>();
        static DateTime dia = DateTime.Today;

        public static void Load()
        {
            hoy.Clear(); dia = DateTime.Today;
            try
            {
                if (!File.Exists(FilePath)) return;
                string prefix = dia.ToString("yyyy-MM-dd") + ",";
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    if (!line.StartsWith(prefix)) continue;
                    string[] p = line.Split(',');
                    if (p.Length >= 4 && p[3] == "hecho") Inc(p[2]);
                }
            }
            catch { }
        }

        static void Inc(string k) { int n; hoy.TryGetValue(k, out n); hoy[k] = n + 1; }

        public static int Hoy(string k)
        {
            if (DateTime.Today != dia) { hoy.Clear(); dia = DateTime.Today; }
            int n; hoy.TryGetValue(k, out n); return n;
        }

        public static void Add(string key, string accion)
        {
            if (DateTime.Today != dia) { hoy.Clear(); dia = DateTime.Today; }
            if (accion == "hecho") Inc(key);
            try
            {
                Directory.CreateDirectory(Config.Dir);
                bool nuevo = !File.Exists(FilePath);
                using (StreamWriter w = new StreamWriter(FilePath, true, Encoding.UTF8))
                {
                    if (nuevo) w.WriteLine("fecha,hora,recordatorio,accion");
                    DateTime n = DateTime.Now;
                    w.WriteLine(n.ToString("yyyy-MM-dd") + "," + n.ToString("HH:mm:ss") + "," + key + "," + accion);
                }
            }
            catch { }
        }
    }

    static class Inicio
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValName = "PausasFrozz";
        public static void Apply(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue(ValName, "\"" + Application.ExecutablePath + "\" --startup");
                    else if (k.GetValue(ValName) != null) k.DeleteValue(ValName, false);
                }
            }
            catch { }
        }
    }

    // ------------------------------------------------------------ iconos
    static class Icons
    {
        public static void DrawGlyph(Graphics g, string kind, RectangleF r, Color c)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = Math.Min(r.Width, r.Height);
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            using (Pen p = new Pen(c, Math.Max(1.2f, s * 0.085f)))
            using (SolidBrush b = new SolidBrush(c))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                if (kind == "eye")
                {
                    float w = s * 0.92f, h = s * 0.42f;
                    PointF L = new PointF(cx - w / 2, cy), R = new PointF(cx + w / 2, cy);
                    using (GraphicsPath gp = new GraphicsPath())
                    {
                        gp.AddBezier(L, new PointF(cx - w / 4, cy - h), new PointF(cx + w / 4, cy - h), R);
                        gp.AddBezier(R, new PointF(cx + w / 4, cy + h), new PointF(cx - w / 4, cy + h), L);
                        gp.CloseFigure();
                        g.DrawPath(p, gp);
                    }
                    float ir = s * 0.17f;
                    g.FillEllipse(b, cx - ir, cy - ir, ir * 2, ir * 2);
                }
                else if (kind == "drop")
                {
                    float R = s * 0.27f, ccy = cy + s * 0.14f, top = cy - s * 0.42f;
                    float d = ccy - top;
                    double a = Math.Asin(R / d);
                    float tx = (float)(R * Math.Cos(a)), ty = (float)(R * Math.Sin(a));
                    g.FillPolygon(b, new PointF[] { new PointF(cx, top), new PointF(cx + tx, ccy - ty), new PointF(cx - tx, ccy - ty) });
                    g.FillEllipse(b, cx - R, ccy - R, R * 2, R * 2);
                }
                else if (kind == "stretch")
                {
                    float hr = s * 0.11f;
                    float hy = cy - s * 0.22f;
                    g.FillEllipse(b, cx - hr, hy - hr, hr * 2, hr * 2);
                    PointF sh = new PointF(cx, hy + hr * 1.9f), hip = new PointF(cx, cy + s * 0.16f);
                    g.DrawLine(p, sh, hip);
                    g.DrawLines(p, new PointF[] { sh, new PointF(cx - s * 0.2f, cy - s * 0.08f), new PointF(cx - s * 0.3f, cy - s * 0.46f) });
                    g.DrawLines(p, new PointF[] { sh, new PointF(cx + s * 0.2f, cy - s * 0.08f), new PointF(cx + s * 0.3f, cy - s * 0.46f) });
                    g.DrawLine(p, hip, new PointF(cx - s * 0.14f, cy + s * 0.46f));
                    g.DrawLine(p, hip, new PointF(cx + s * 0.14f, cy + s * 0.46f));
                }
                else if (kind == "walk")
                {
                    float hr = s * 0.09f;
                    float hx = cx + s * 0.04f, hy = cy - s * 0.34f;
                    g.FillEllipse(b, hx - hr, hy - hr, hr * 2, hr * 2);
                    PointF sh = new PointF(cx + s * 0.01f, hy + hr * 1.7f), hip = new PointF(cx - s * 0.03f, cy + s * 0.1f);
                    g.DrawLine(p, sh, hip);
                    g.DrawLines(p, new PointF[] { sh, new PointF(cx + s * 0.13f, cy - s * 0.04f), new PointF(cx + s * 0.22f, cy + s * 0.06f) });
                    g.DrawLines(p, new PointF[] { sh, new PointF(cx - s * 0.12f, cy - s * 0.02f), new PointF(cx - s * 0.2f, cy + s * 0.08f) });
                    g.DrawLines(p, new PointF[] { hip, new PointF(cx + s * 0.08f, cy + s * 0.27f), new PointF(cx + s * 0.17f, cy + s * 0.44f) });
                    g.DrawLines(p, new PointF[] { hip, new PointF(cx - s * 0.1f, cy + s * 0.27f), new PointF(cx - s * 0.24f, cy + s * 0.4f) });
                }
                else if (kind == "posture")
                {
                    RectangleF m = new RectangleF(cx - s * 0.42f, cy - s * 0.38f, s * 0.84f, s * 0.56f);
                    using (GraphicsPath gp = Brand.Round(m, s * 0.06f)) g.DrawPath(p, gp);
                    g.DrawLine(p, cx, m.Bottom, cx, cy + s * 0.36f);
                    g.DrawLine(p, cx - s * 0.2f, cy + s * 0.38f, cx + s * 0.2f, cy + s * 0.38f);
                    g.DrawLines(p, new PointF[] { new PointF(cx - s * 0.17f, cy - s * 0.1f), new PointF(cx - s * 0.04f, cy + s * 0.03f), new PointF(cx + s * 0.19f, cy - s * 0.21f) });
                }
            }
        }

        public static Bitmap AppBitmap(int size)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float pad = size >= 32 ? size * 0.04f : 0;
                RectangleF r = new RectangleF(pad, pad, size - pad * 2 - 0.5f, size - pad * 2 - 0.5f);
                using (GraphicsPath gp = Brand.Round(r, size * 0.22f))
                using (LinearGradientBrush lb = new LinearGradientBrush(r, Brand.Blue, Brand.Navy, 60f))
                    g.FillPath(lb, gp);
                float inset = size * 0.16f;
                DrawGlyph(g, "eye", new RectangleF(inset, inset - size * 0.05f, size - inset * 2, size - inset * 2), Color.White);
                using (Pen gp2 = new Pen(Brand.Green, Math.Max(1.5f, size * 0.09f)))
                {
                    gp2.StartCap = LineCap.Round; gp2.EndCap = LineCap.Round;
                    g.DrawLine(gp2, size * 0.3f, size * 0.78f, size * 0.7f, size * 0.78f);
                }
            }
            return bmp;
        }

        public static Icon AppIcon(int size)
        {
            using (Bitmap b = AppBitmap(size))
            {
                IntPtr h = b.GetHicon();
                Icon tmp = Icon.FromHandle(h);
                Icon ic = (Icon)tmp.Clone();
                Native.DestroyIcon(h);
                return ic;
            }
        }

        // ICO multi-tamaño con PNG embebidos (para el ícono del .exe)
        public static void WriteIco(string path)
        {
            int[] sizes = { 256, 64, 48, 32, 24, 16 };
            List<byte[]> pngs = new List<byte[]>();
            foreach (int s in sizes)
                using (Bitmap b = AppBitmap(s))
                using (MemoryStream ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
            using (FileStream fs = File.Create(path))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write((short)0); bw.Write((short)1); bw.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    byte d = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                    bw.Write(d); bw.Write(d); bw.Write((byte)0); bw.Write((byte)0);
                    bw.Write((short)1); bw.Write((short)32);
                    bw.Write(pngs[i].Length); bw.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (byte[] p in pngs) bw.Write(p);
            }
        }
    }

    // ---------------------------------------------------- barra progreso
    class Bar : Control
    {
        float val;
        public float Value { get { return val; } set { val = Math.Max(0, Math.Min(1, value)); Invalidate(); } }
        public Bar() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = new RectangleF(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Brand.Round(r, Height / 2f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(212, 222, 238))) g.FillPath(b, p);
            float w = (Width - 1) * val;
            if (w > 0)
                using (GraphicsPath p = Brand.Round(new RectangleF(0, 0, Math.Max(w, Height), Height - 1), Height / 2f))
                using (SolidBrush b = new SolidBrush(Brand.Green)) g.FillPath(b, p);
        }
    }

    // ------------------------------------------------------- ventana aviso
    class ReminderPopup : Form
    {
        public readonly ReminderDef Def;
        public event Action<ReminderPopup, string> Result;
        public bool Running { get { return running; } }

        readonly bool sound;
        readonly int hh;
        Label lblStepNum, lblCount, lblStep;
        Bar bar;
        Button btnMain, btnLater, btnSkip;
        System.Windows.Forms.Timer tick, fade, autoClose;
        int stepIdx, remaining, elapsed, total;
        bool running, done, closedFlag;
        Font fTitle, fSub;

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00000008; // NOACTIVATE | TOOLWINDOW | TOPMOST
                cp.ClassStyle |= 0x00020000; // sombra
                return cp;
            }
        }

        public ReminderPopup(ReminderDef def, bool sound, int snoozeMin)
        {
            Def = def; this.sound = sound;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual; BackColor = Color.White;
            AutoScaleMode = AutoScaleMode.None;
            Text = "Pausas Frozz";
            DoubleBuffered = true;

            fTitle = new Font("Segoe UI Semibold", 14f);
            fSub = new Font("Segoe UI", 8f, FontStyle.Bold);
            Font fBody = new Font("Segoe UI", 10f);
            Font fStep = new Font("Segoe UI Semibold", 11f);
            Font fSmall = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            Font fTip = new Font("Segoe UI", 9f, FontStyle.Italic);
            Font fCount = new Font("Segoe UI", 20f, FontStyle.Bold);
            Font fBtn = new Font("Segoe UI Semibold", 9.5f);

            int W = Brand.S(450), pad = Brand.S(20), cw = W - 2 * pad;
            hh = Brand.S(78);
            int y = hh + Brand.S(16);

            AddText(this, def.Intro, fBody, Brand.Text, pad, ref y, cw, Brand.S(12));
            total = def.TotalSeconds;

            if (def.Checklist)
            {
                foreach (Step st in def.Steps)
                {
                    Label ck = new Label();
                    ck.Text = "✔"; ck.Font = new Font("Segoe UI Symbol", 10.5f, FontStyle.Bold); ck.ForeColor = Brand.GreenDark;
                    ck.Location = new Point(pad, y); ck.Size = new Size(Brand.S(24), Brand.S(24)); ck.BackColor = Color.Transparent;
                    Controls.Add(ck);
                    int yy = y + Brand.S(2);
                    AddText(this, st.Text, fBody, Brand.Text, pad + Brand.S(26), ref yy, cw - Brand.S(26), 0);
                    y = Math.Max(yy, y + Brand.S(24)) + Brand.S(4);
                }
                y += Brand.S(6);
            }
            else if (def.Timed)
            {
                Panel card = new Panel();
                card.BackColor = Brand.Light; card.Location = new Point(pad, y); card.Width = cw;
                int ix = Brand.S(14), iw = cw - 2 * ix, cy = Brand.S(8);

                lblStepNum = new Label();
                lblStepNum.Font = fSmall; lblStepNum.ForeColor = Brand.Muted; lblStepNum.BackColor = Color.Transparent;
                lblStepNum.Location = new Point(ix, cy); lblStepNum.Size = new Size(iw - Brand.S(110), Brand.S(42));
                lblStepNum.TextAlign = ContentAlignment.MiddleLeft;
                card.Controls.Add(lblStepNum);

                lblCount = new Label();
                lblCount.Font = fCount; lblCount.ForeColor = Brand.Navy; lblCount.BackColor = Color.Transparent;
                lblCount.Location = new Point(ix + iw - Brand.S(110), cy); lblCount.Size = new Size(Brand.S(110), Brand.S(42));
                lblCount.TextAlign = ContentAlignment.MiddleRight;
                card.Controls.Add(lblCount);
                cy += Brand.S(44);

                int maxH = 0;
                foreach (Step st in def.Steps)
                    maxH = Math.Max(maxH, TextRenderer.MeasureText(st.Text, fStep, new Size(iw, 0), TextFormatFlags.WordBreak).Height);
                lblStep = new Label();
                lblStep.Font = fStep; lblStep.ForeColor = Brand.Text; lblStep.BackColor = Color.Transparent;
                lblStep.Location = new Point(ix, cy); lblStep.Size = new Size(iw, maxH + Brand.S(4));
                card.Controls.Add(lblStep);
                cy += lblStep.Height + Brand.S(10);

                bar = new Bar();
                bar.BackColor = Brand.Light; bar.Location = new Point(ix, cy); bar.Size = new Size(iw, Brand.S(8));
                card.Controls.Add(bar);
                cy += bar.Height + Brand.S(14);
                card.Height = cy;
                Controls.Add(card);
                card.Paint += delegate(object s, PaintEventArgs e) { };
                y += card.Height + Brand.S(12);

                stepIdx = 0; remaining = def.Steps[0].Seconds;
                ShowStep(true);
            }

            if (!string.IsNullOrEmpty(def.Tip))
                AddText(this, def.Tip, fTip, Brand.Muted, pad, ref y, cw, Brand.S(14));

            // botones
            int bh = Brand.S(36);
            string mainText = def.Timed ? "Empezar (" + Dur(total) + ")" : def.DoneText;
            btnMain = MakeButton(mainText, fBtn, true);
            btnLater = MakeButton("En " + snoozeMin + " min", fBtn, false);
            btnSkip = MakeButton("Omitir", fBtn, false);
            int x = W - pad;
            foreach (Button b in new Button[] { btnMain, btnLater, btnSkip })
            {
                int bw = Math.Max(Brand.S(b == btnMain ? 130 : 86), TextRenderer.MeasureText(b.Text, fBtn).Width + Brand.S(28));
                if (b == btnMain && def.Timed) bw = Math.Max(bw, TextRenderer.MeasureText("Terminar", fBtn).Width + Brand.S(28));
                b.Size = new Size(bw, bh);
                x -= bw;
                b.Location = new Point(x, y);
                x -= Brand.S(8);
                Controls.Add(b);
            }
            btnMain.Click += delegate { OnMain(); };
            btnLater.Click += delegate { CloseWith("pospuesto"); };
            btnSkip.Click += delegate { CloseWith("omitido"); };

            y += bh + pad + Brand.S(5);
            ClientSize = new Size(W, y);

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - Brand.S(16), wa.Bottom - Height - Brand.S(16));

            tick = new System.Windows.Forms.Timer(); tick.Interval = 1000; tick.Tick += delegate { OnTick(); };
            fade = new System.Windows.Forms.Timer(); fade.Interval = 15;
            fade.Tick += delegate { Opacity = Math.Min(1.0, Opacity + 0.1); if (Opacity >= 1.0) fade.Stop(); };
            Opacity = 0;
        }

        static string Dur(int secs)
        {
            if (secs < 60) return secs + " s";
            return "~" + (int)Math.Round(secs / 60.0) + " min";
        }

        static string Clock(int secs) { return (secs / 60) + ":" + (secs % 60).ToString("00"); }

        static void AddText(Control parent, string text, Font f, Color c, int x, ref int y, int w, int gap)
        {
            Size sz = TextRenderer.MeasureText(text, f, new Size(w, 0), TextFormatFlags.WordBreak);
            Label l = new Label();
            l.Text = text; l.Font = f; l.ForeColor = c; l.BackColor = Color.Transparent;
            l.Location = new Point(x, y); l.Size = new Size(w, sz.Height + Brand.S(2));
            parent.Controls.Add(l);
            y += l.Height + gap;
        }

        Button MakeButton(string text, Font f, bool primary)
        {
            Button b = new Button();
            b.Text = text; b.Font = f; b.FlatStyle = FlatStyle.Flat; b.TabStop = false; b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            if (primary)
            {
                b.BackColor = Brand.Navy; b.ForeColor = Color.White;
                b.FlatAppearance.BorderSize = 0;
                b.FlatAppearance.MouseOverBackColor = Brand.Blue;
            }
            else
            {
                b.BackColor = Color.White; b.ForeColor = Brand.Navy;
                b.FlatAppearance.BorderColor = Brand.Line; b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.MouseOverBackColor = Brand.Light;
            }
            return b;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Native.RoundCorners(Handle);
            fade.Start();
            if (sound) Sonido.Chime();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int W = ClientSize.Width, pad = Brand.S(20);
            using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(0, 0, W, hh), Brand.Navy, Brand.Blue, 0f))
                g.FillRectangle(lb, 0, 0, W, hh);
            int cs = Brand.S(48);
            Rectangle circ = new Rectangle(pad, (hh - cs) / 2, cs, cs);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 255, 255, 255))) g.FillEllipse(b, circ);
            int ins = Def.Glyph == "walk" || Def.Glyph == "stretch" ? Brand.S(6) : Brand.S(10);
            Icons.DrawGlyph(g, Def.Glyph, new RectangleF(circ.X + ins, circ.Y + ins, cs - 2 * ins, cs - 2 * ins), Color.White);
            int tx = pad + cs + Brand.S(14);
            TextRenderer.DrawText(g, "PAUSAS FROZZ  ·  SST", fSub, new Point(tx, Brand.S(15)), Brand.Yellow, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, Def.Title, fTitle, new Point(tx, Brand.S(33)), Color.White, TextFormatFlags.NoPadding);
            int sb = Brand.S(5);
            using (SolidBrush y = new SolidBrush(Brand.Yellow)) g.FillRectangle(y, 0, ClientSize.Height - sb, W, sb);
            using (SolidBrush gr = new SolidBrush(Brand.Green)) g.FillRectangle(gr, 0, ClientSize.Height - sb, W / 4, sb);
            using (Pen p = new Pen(Brand.Line)) g.DrawRectangle(p, 0, 0, W - 1, ClientSize.Height - 1);
        }

        void ShowStep(bool preview)
        {
            Step st = Def.Steps[stepIdx];
            lblStepNum.Text = Def.Steps.Count > 1 ? "PASO " + (stepIdx + 1) + " DE " + Def.Steps.Count : "QUÉ HACER";
            lblStep.Text = st.Text;
            lblCount.Text = Clock(remaining);
            lblCount.ForeColor = preview ? Brand.Muted : Brand.Navy;
            bar.Value = total > 0 ? (float)elapsed / total : 0;
        }

        void OnMain()
        {
            if (Def.Timed && !running && !done)
            {
                running = true;
                btnMain.Text = "Terminar";
                btnLater.Visible = false;
                ShowStep(false);
                tick.Start();
                return;
            }
            CloseWith("hecho");
        }

        void OnTick()
        {
            remaining--; elapsed++;
            if (remaining <= 0)
            {
                stepIdx++;
                if (stepIdx >= Def.Steps.Count) { Finish(); return; }
                remaining = Def.Steps[stepIdx].Seconds;
                if (sound) Sonido.Tick();
            }
            ShowStep(false);
        }

        void Finish()
        {
            tick.Stop(); running = false; done = true;
            lblStepNum.Text = "COMPLETADO";
            lblCount.Text = "✓"; lblCount.ForeColor = Brand.GreenDark;
            lblStep.Text = "¡Bien hecho! Cuidarte también es parte del trabajo.";
            bar.Value = 1;
            btnSkip.Visible = false;
            btnMain.Text = "Cerrar";
            if (sound) Sonido.Done();
            autoClose = new System.Windows.Forms.Timer(); autoClose.Interval = 5000;
            autoClose.Tick += delegate { CloseWith("hecho"); };
            autoClose.Start();
        }

        public void CloseWith(string action)
        {
            if (closedFlag) return;
            closedFlag = true;
            tick.Stop(); fade.Stop(); if (autoClose != null) autoClose.Stop();
            if (Result != null) Result(this, action);
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (!closedFlag) { closedFlag = true; if (Result != null) Result(this, "omitido"); }
            base.OnFormClosed(e);
        }
    }

    // ------------------------------------------------------ configuración
    class SettingsForm : Form
    {
        readonly Config cfg;
        readonly List<ReminderDef> defs;
        Dictionary<string, CheckBox> chk = new Dictionary<string, CheckBox>();
        Dictionary<string, NumericUpDown> num = new Dictionary<string, NumericUpDown>();
        CheckBox chkSched, chkSound, chkStartup;
        DateTimePicker dtStart, dtEnd;
        CheckBox[] days = new CheckBox[7];
        NumericUpDown numIdle, numSnooze;

        public SettingsForm(Config c, List<ReminderDef> d, Icon icon)
        {
            cfg = c; defs = d;
            Text = "Pausas Frozz — Configuración";
            Icon = icon;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Segoe UI", 9.5f); BackColor = Color.White;

            int x = Brand.S(22), y = Brand.S(18), W = Brand.S(520);
            Font fH = new Font("Segoe UI Semibold", 11f);

            Section("Recordatorios", fH, x, ref y);
            foreach (ReminderDef def in defs)
            {
                CheckBox cb = new CheckBox();
                cb.Text = def.Name; cb.Checked = cfg.On[def.Key];
                cb.Location = new Point(x, y); cb.Size = new Size(Brand.S(330), Brand.S(26));
                Controls.Add(cb); chk[def.Key] = cb;
                Lbl("cada", x + Brand.S(340), y + Brand.S(3));
                NumericUpDown n = Num(5, 480, cfg.Min[def.Key], x + Brand.S(380), y);
                num[def.Key] = n;
                Lbl("min", x + Brand.S(452), y + Brand.S(3));
                y += Brand.S(32);
            }
            y += Brand.S(10);

            Section("Horario de trabajo", fH, x, ref y);
            chkSched = new CheckBox();
            chkSched.Text = "Recordar solo en este horario:"; chkSched.Checked = cfg.UseSchedule;
            chkSched.Location = new Point(x, y); chkSched.Size = new Size(Brand.S(240), Brand.S(26));
            Controls.Add(chkSched);
            Lbl("de", x + Brand.S(246), y + Brand.S(3));
            dtStart = Time(cfg.Start, x + Brand.S(272), y);
            Lbl("a", x + Brand.S(356), y + Brand.S(3));
            dtEnd = Time(cfg.End, x + Brand.S(374), y);
            y += Brand.S(34);
            string[] names = { "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" };
            int[] map = { 1, 2, 3, 4, 5, 6, 0 };
            for (int i = 0; i < 7; i++)
            {
                CheckBox cb = new CheckBox();
                cb.Text = names[i]; cb.Checked = cfg.Days[map[i]];
                cb.Location = new Point(x + Brand.S(20) + i * Brand.S(66), y); cb.Size = new Size(Brand.S(62), Brand.S(26));
                Controls.Add(cb); days[map[i]] = cb;
            }
            y += Brand.S(42);

            Section("General", fH, x, ref y);
            Lbl("Si no uso el equipo por", x, y + Brand.S(3));
            numIdle = Num(1, 60, cfg.IdleMinutes, x + Brand.S(175), y);
            Lbl("min, considerar que estoy en pausa (reinicia contadores)", x + Brand.S(245), y + Brand.S(3), Brand.S(260));
            y += Brand.S(46);
            Lbl("Al posponer un aviso, repetirlo en", x, y + Brand.S(3));
            numSnooze = Num(1, 60, cfg.SnoozeMinutes, x + Brand.S(250), y);
            Lbl("min", x + Brand.S(322), y + Brand.S(3));
            y += Brand.S(34);
            chkSound = new CheckBox();
            chkSound.Text = "Sonido al mostrar los avisos"; chkSound.Checked = cfg.Sound;
            chkSound.Location = new Point(x, y); chkSound.Size = new Size(Brand.S(400), Brand.S(26));
            Controls.Add(chkSound);
            y += Brand.S(30);
            chkStartup = new CheckBox();
            chkStartup.Text = "Iniciar automáticamente con Windows"; chkStartup.Checked = cfg.Startup;
            chkStartup.Location = new Point(x, y); chkStartup.Size = new Size(Brand.S(400), Brand.S(26));
            Controls.Add(chkStartup);
            y += Brand.S(44);

            Button ok = new Button();
            ok.Text = "Guardar"; ok.Size = new Size(Brand.S(110), Brand.S(34));
            ok.Location = new Point(W - x - Brand.S(110) * 2 - Brand.S(10), y);
            ok.BackColor = Brand.Navy; ok.ForeColor = Color.White; ok.FlatStyle = FlatStyle.Flat; ok.FlatAppearance.BorderSize = 0;
            ok.Click += delegate { Apply(); DialogResult = DialogResult.OK; Close(); };
            Button cancel = new Button();
            cancel.Text = "Cancelar"; cancel.Size = new Size(Brand.S(110), Brand.S(34));
            cancel.Location = new Point(W - x - Brand.S(110), y);
            cancel.FlatStyle = FlatStyle.Flat; cancel.FlatAppearance.BorderColor = Brand.Line; cancel.ForeColor = Brand.Navy;
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(ok); Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
            y += Brand.S(34) + Brand.S(20);

            Label foot = new Label();
            foot.Text = "Basado en el instructivo SST «Ergonomía en puestos administrativos y uso de pantallas» — Frozz Colombia.";
            foot.ForeColor = Brand.Muted; foot.Font = new Font("Segoe UI", 8f);
            foot.Location = new Point(x, y); foot.Size = new Size(W - 2 * x, Brand.S(34));
            Controls.Add(foot);
            y += Brand.S(40);

            ClientSize = new Size(W, y);
        }

        void Section(string text, Font f, int x, ref int y)
        {
            Label l = new Label();
            l.Text = text; l.Font = f; l.ForeColor = Brand.Navy;
            l.Location = new Point(x, y); l.AutoSize = true;
            Controls.Add(l);
            y += Brand.S(30);
        }

        void Lbl(string text, int x, int y) { Lbl(text, x, y, 0); }
        void Lbl(string text, int x, int y, int w)
        {
            Label l = new Label();
            l.Text = text; l.ForeColor = Brand.Text; l.Location = new Point(x, y);
            if (w > 0) { l.Size = new Size(w, Brand.S(40)); } else l.AutoSize = true;
            Controls.Add(l);
        }

        NumericUpDown Num(int min, int max, int val, int x, int y)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = min; n.Maximum = max; n.Value = Math.Max(min, Math.Min(max, val));
            n.Location = new Point(x, y); n.Width = Brand.S(64);
            Controls.Add(n);
            return n;
        }

        DateTimePicker Time(TimeSpan t, int x, int y)
        {
            DateTimePicker p = new DateTimePicker();
            p.Format = DateTimePickerFormat.Custom; p.CustomFormat = "HH:mm"; p.ShowUpDown = true;
            p.Value = DateTime.Today.Add(t);
            p.Location = new Point(x, y); p.Width = Brand.S(78);
            Controls.Add(p);
            return p;
        }

        void Apply()
        {
            foreach (ReminderDef d in defs) { cfg.On[d.Key] = chk[d.Key].Checked; cfg.Min[d.Key] = (int)num[d.Key].Value; }
            cfg.UseSchedule = chkSched.Checked;
            cfg.Start = new TimeSpan(dtStart.Value.Hour, dtStart.Value.Minute, 0);
            cfg.End = new TimeSpan(dtEnd.Value.Hour, dtEnd.Value.Minute, 0);
            for (int i = 0; i < 7; i++) cfg.Days[i] = days[i].Checked;
            cfg.IdleMinutes = (int)numIdle.Value;
            cfg.SnoozeMinutes = (int)numSnooze.Value;
            cfg.Sound = chkSound.Checked;
            cfg.Startup = chkStartup.Checked;
            cfg.Save();
        }
    }

    // ---------------------------------------------------------- instructivo
    class InstructivoForm : Form
    {
        public InstructivoForm(Icon icon)
        {
            Text = "Ergonomía en puestos administrativos y uso de pantallas — SST Frozz";
            Icon = icon; BackColor = Color.White; StartPosition = FormStartPosition.CenterScreen;
            PictureBox pb = new PictureBox();
            pb.Dock = DockStyle.Fill; pb.SizeMode = PictureBoxSizeMode.Zoom;
            Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("instructivo.jpg");
            if (s != null) pb.Image = Image.FromStream(s);
            Controls.Add(pb);
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int w = (int)(wa.Width * 0.8), h = (int)(w / 1.5);
            if (h > wa.Height * 0.9) { h = (int)(wa.Height * 0.9); w = (int)(h * 1.5); }
            ClientSize = new Size(w, h);
        }
    }

    // ------------------------------------------------------------ bandeja
    class TrayApp : ApplicationContext
    {
        readonly List<ReminderDef> defs = Catalogo.Todos();
        readonly Dictionary<string, DateTime> due = new Dictionary<string, DateTime>();
        readonly Config cfg;
        readonly NotifyIcon tray;
        readonly ContextMenuStrip menu;
        readonly System.Windows.Forms.Timer timer;
        readonly Icon appIcon, formIcon;
        readonly bool demo;
        ReminderPopup popup;
        SettingsForm settings;
        InstructivoForm instr;
        DateTime pausedUntil = DateTime.MinValue, awaySince, noPopupBefore = DateTime.MinValue;
        bool wasPaused, away, locked, inSchedKnown, inSched;
        int ticks;

        Control invoker;
        System.Windows.Forms.Timer updTimer, updFirst;
        bool checkingUpd;
        string declinedVersion;
        ToolStripMenuItem miStatus, miResume, miPauseMenu;
        List<ToolStripMenuItem> statusItems = new List<ToolStripMenuItem>();

        public TrayApp(bool startup, string demoKey, bool updated)
        {
            demo = demoKey != null;
            cfg = Config.Load(defs);
            Historial.Load();
            appIcon = Icons.AppIcon(SystemInformation.SmallIconSize.Width);
            formIcon = Icons.AppIcon(32);

            menu = new ContextMenuStrip();
            menu.Font = new Font("Segoe UI", 9.5f);
            BuildMenu();

            tray = new NotifyIcon();
            tray.Icon = appIcon; tray.Text = "Pausas Frozz"; tray.ContextMenuStrip = menu; tray.Visible = true;
            tray.DoubleClick += delegate { OpenSettings(); };

            ResetAll(DateTime.Now);
            if (!demo) Inicio.Apply(cfg.Startup);

            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.PowerModeChanged += OnPower;

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 1000; timer.Tick += delegate { Tick(); };
            timer.Start();

            invoker = new Control();
            invoker.CreateControl();
            if (!demo)
            {
                updFirst = new System.Windows.Forms.Timer(); updFirst.Interval = 45000;
                updFirst.Tick += delegate { updFirst.Stop(); CheckUpdates(false); };
                updFirst.Start();
                updTimer = new System.Windows.Forms.Timer(); updTimer.Interval = 6 * 3600 * 1000;
                updTimer.Tick += delegate { CheckUpdates(false); };
                updTimer.Start();
            }
            if (updated)
                tray.ShowBalloonTip(6000, "Pausas Frozz actualizado", "Ya tienes la versión " + AppInfo.Version + ".", ToolTipIcon.Info);

            if (cfg.FirstRun && !demo)
            {
                tray.ShowBalloonTip(10000, "Pausas Frozz está activo",
                    "Te recordaré descansar la vista, hidratarte, estirarte y hacer pausas activas. Estoy junto al reloj: clic derecho para opciones.",
                    ToolTipIcon.Info);
                cfg.FirstRun = false; cfg.Save();
            }

            if (demo)
            {
                ReminderDef d = defs.FirstOrDefault(r => r.Key == demoKey);
                if (d != null) ShowReminder(d);
                else if (demoKey == "config") OpenSettings();
            }
        }

        void BuildMenu()
        {
            ToolStripMenuItem title = new ToolStripMenuItem("Pausas Frozz  v" + AppInfo.Version);
            title.Font = new Font(menu.Font, FontStyle.Bold); title.Enabled = false;
            menu.Items.Add(title);
            miStatus = new ToolStripMenuItem(""); miStatus.Enabled = false;
            menu.Items.Add(miStatus);
            foreach (ReminderDef d in defs)
            {
                ToolStripMenuItem it = new ToolStripMenuItem(d.Short); it.Enabled = false; it.Tag = d;
                statusItems.Add(it); menu.Items.Add(it);
            }
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem now = new ToolStripMenuItem("Hacer ahora");
            foreach (ReminderDef d in defs)
            {
                ReminderDef dd = d;
                now.DropDownItems.Add(d.Name, null, delegate { ShowReminder(dd); });
            }
            menu.Items.Add(now);

            miPauseMenu = new ToolStripMenuItem("Pausar recordatorios");
            miPauseMenu.DropDownItems.Add("30 minutos", null, delegate { PauseFor(TimeSpan.FromMinutes(30)); });
            miPauseMenu.DropDownItems.Add("1 hora", null, delegate { PauseFor(TimeSpan.FromHours(1)); });
            miPauseMenu.DropDownItems.Add("2 horas", null, delegate { PauseFor(TimeSpan.FromHours(2)); });
            miPauseMenu.DropDownItems.Add("Hasta mañana", null, delegate { pausedUntil = DateTime.Today.AddDays(1); wasPaused = true; UpdateTooltip(DateTime.Now); });
            menu.Items.Add(miPauseMenu);
            miResume = new ToolStripMenuItem("Reanudar recordatorios", null, delegate { pausedUntil = DateTime.MinValue; });
            menu.Items.Add(miResume);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Resumen de hoy", null, delegate { ShowSummary(); });
            menu.Items.Add("Ver instructivo de ergonomía", null, delegate { ShowInstructivo(); });
            menu.Items.Add("Configuración…", null, delegate { OpenSettings(); });
            menu.Items.Add("Buscar actualizaciones", null, delegate { CheckUpdates(true); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Salir", null, delegate { ExitApp(); });

            menu.Opening += delegate { RefreshMenu(); };
        }

        void RefreshMenu()
        {
            DateTime now = DateTime.Now;
            bool paused = now < pausedUntil;
            miResume.Visible = paused;
            if (paused) miStatus.Text = "En pausa hasta " + (pausedUntil.Date > now.Date ? "mañana" : pausedUntil.ToString("HH:mm"));
            else if (!cfg.InSchedule(now)) miStatus.Text = "Fuera de horario (" + cfg.ScheduleText() + ")";
            else if (away) miStatus.Text = "Estás en pausa (sin actividad)";
            else miStatus.Text = "Próximos avisos:";
            foreach (ToolStripMenuItem it in statusItems)
            {
                ReminderDef d = (ReminderDef)it.Tag;
                string hoy = "   (hoy: " + Historial.Hoy(d.Key) + ")";
                if (!cfg.On[d.Key]) { it.Text = "   " + d.Short + ": desactivado"; continue; }
                int m = (int)Math.Ceiling((due[d.Key] - now).TotalMinutes);
                it.Text = "   " + d.Short + ": " + (m <= 0 ? "pendiente" : "en " + m + " min") + hoy;
            }
        }

        void PauseFor(TimeSpan t)
        {
            pausedUntil = DateTime.Now.Add(t); wasPaused = true;
            if (popup != null) popup.CloseWith("pausado");
            UpdateTooltip(DateTime.Now);
        }

        void ResetAll(DateTime now)
        {
            foreach (ReminderDef d in defs) due[d.Key] = now.AddMinutes(cfg.Min[d.Key]);
        }

        void Tick()
        {
            DateTime now = DateTime.Now;
            bool sched = cfg.InSchedule(now);
            if (inSchedKnown && sched && !inSched) ResetAll(now);
            inSched = sched; inSchedKnown = true;

            bool paused = now < pausedUntil;
            if (wasPaused && !paused) ResetAll(now);
            wasPaused = paused;

            TimeSpan idle = Native.IdleTime();
            bool isAway = locked || idle.TotalMinutes >= cfg.IdleMinutes;
            if (isAway && !away) { away = true; awaySince = locked ? now : now - idle; }
            else if (!isAway && away) { away = false; OnReturn(now); }

            if (++ticks % 5 == 1) UpdateTooltip(now);

            if (demo) return;
            if (!sched || paused || away || popup != null || now < noPopupBefore) return;
            if (Native.UserBusy()) return;

            ReminderDef next = null;
            foreach (ReminderDef d in defs)
                if (cfg.On[d.Key] && due[d.Key] <= now && (next == null || cfg.Min[d.Key] > cfg.Min[next.Key])) next = d;
            if (next != null) ShowReminder(next);
        }

        void OnReturn(DateTime now)
        {
            if ((now - awaySince).TotalMinutes < cfg.IdleMinutes) return;
            if (popup != null && !popup.Running) popup.CloseWith("ausente");
            foreach (ReminderDef d in defs)
            {
                if (d.ResetOnAway) due[d.Key] = now.AddMinutes(cfg.Min[d.Key]);
                else if (due[d.Key] < now.AddMinutes(1)) due[d.Key] = now.AddMinutes(1);
            }
            noPopupBefore = now.AddSeconds(60);
        }

        void OnSessionSwitch(object s, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.ConsoleDisconnect || e.Reason == SessionSwitchReason.RemoteDisconnect)
            {
                locked = true;
                if (!away) { away = true; awaySince = DateTime.Now; }
            }
            else if (e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.ConsoleConnect || e.Reason == SessionSwitchReason.RemoteConnect)
                locked = false;
        }

        void OnPower(object s, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume) { ResetAll(DateTime.Now); noPopupBefore = DateTime.Now.AddSeconds(60); }
        }

        void ShowReminder(ReminderDef d)
        {
            if (popup != null) popup.CloseWith("reemplazado");
            popup = new ReminderPopup(d, cfg.Sound, cfg.SnoozeMinutes);
            popup.Result += OnPopupResult;
            popup.Show();
        }

        void OnPopupResult(ReminderPopup p, string action)
        {
            if (popup == p) popup = null;
            DateTime now = DateTime.Now;
            string k = p.Def.Key;
            if (action == "hecho" || action == "omitido" || action == "pospuesto") Historial.Add(k, action);
            if (action == "hecho" || action == "omitido") due[k] = now.AddMinutes(cfg.Min[k]);
            else if (action == "pospuesto") due[k] = now.AddMinutes(cfg.SnoozeMinutes);
            if (action == "hecho" && p.Def.ResetsVista) due["vista"] = now.AddMinutes(cfg.Min["vista"]);
            noPopupBefore = now.AddSeconds(90);
            UpdateTooltip(now);
            if (demo && popup == null) ExitApp();
        }

        void UpdateTooltip(DateTime now)
        {
            string t;
            if (now < pausedUntil) t = "Pausas Frozz · en pausa";
            else if (!cfg.InSchedule(now)) t = "Pausas Frozz · fuera de horario";
            else
            {
                ReminderDef next = null;
                foreach (ReminderDef d in defs) if (cfg.On[d.Key] && (next == null || due[d.Key] < due[next.Key])) next = d;
                if (next == null) t = "Pausas Frozz · sin recordatorios activos";
                else
                {
                    int m = Math.Max(0, (int)Math.Ceiling((due[next.Key] - now).TotalMinutes));
                    t = "Pausas Frozz · " + next.Short + " en " + m + " min";
                }
            }
            if (t.Length > 63) t = t.Substring(0, 63);
            try { tray.Text = t; } catch { }
        }

        void ShowSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Hoy has completado:");
            sb.AppendLine();
            foreach (ReminderDef d in defs) sb.AppendLine("•  " + d.Plural + ": " + Historial.Hoy(d.Key));
            sb.AppendLine();
            sb.AppendLine("El historial completo queda en:");
            sb.AppendLine(Historial.FilePath);
            sb.AppendLine();
            sb.Append("¿Abrir el historial?");
            if (MessageBox.Show(sb.ToString(), "Pausas Frozz — Resumen de hoy", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                try { if (File.Exists(Historial.FilePath)) Process.Start(Historial.FilePath); else Process.Start(Config.Dir); } catch { }
            }
        }

        void ShowInstructivo()
        {
            if (instr != null && !instr.IsDisposed) { instr.Activate(); return; }
            instr = new InstructivoForm(formIcon);
            instr.Show();
        }

        void OpenSettings()
        {
            if (settings != null && !settings.IsDisposed) { settings.Activate(); return; }
            settings = new SettingsForm(cfg, defs, formIcon);
            settings.FormClosed += delegate
            {
                if (settings.DialogResult == DialogResult.OK)
                {
                    ResetAll(DateTime.Now);
                    if (!demo) Inicio.Apply(cfg.Startup);
                    UpdateTooltip(DateTime.Now);
                }
                settings = null;
            };
            settings.Show();
            settings.Activate();
        }

        static DialogResult Ask(string text, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            using (Form owner = new Form())
            {
                owner.TopMost = true; owner.ShowInTaskbar = false; owner.StartPosition = FormStartPosition.Manual;
                owner.Location = new Point(-2000, -2000); owner.Size = new Size(1, 1);
                owner.Show(); owner.Activate();
                return MessageBox.Show(owner, text, "Pausas Frozz", buttons, icon);
            }
        }

        void OnUi(MethodInvoker m) { try { invoker.BeginInvoke(m); } catch { } }

        void CheckUpdates(bool manual)
        {
            if (checkingUpd) return;
            checkingUpd = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Updater.Info info = null; Exception err = null;
                try { info = Updater.Check(); } catch (Exception ex) { err = ex; }
                OnUi(delegate { checkingUpd = false; OnCheckResult(info, err, manual); });
            });
        }

        void OnCheckResult(Updater.Info info, Exception err, bool manual)
        {
            if (err != null)
            {
                if (manual) Ask("No se pudo comprobar si hay actualizaciones.\nRevisa la conexión a internet.\n\n" + err.Message, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (info.Version <= Updater.Current)
            {
                if (manual) Ask("Tienes la última versión (" + AppInfo.Version + ").", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string v = info.Version.ToString();
            if (!manual && v == declinedVersion) return;
            string msg = "Hay una nueva versión de Pausas Frozz: " + v + "\n(tienes la " + AppInfo.Version + ")";
            if (!string.IsNullOrEmpty(info.Notes)) msg += "\n\nNovedades: " + info.Notes;
            msg += "\n\n¿Actualizar ahora? Toma unos segundos.";
            if (Ask(msg, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) { declinedVersion = v; return; }
            checkingUpd = true;
            tray.ShowBalloonTip(4000, "Pausas Frozz", "Descargando la versión " + v + "…", ToolTipIcon.Info);
            ThreadPool.QueueUserWorkItem(delegate
            {
                string file = null; Exception e2 = null;
                try { file = Updater.Download(info); } catch (Exception ex) { e2 = ex; }
                OnUi(delegate
                {
                    checkingUpd = false;
                    if (e2 != null) { Ask("No se pudo descargar la actualización.\n\n" + e2.Message, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                    try { Updater.Apply(file); }
                    catch (Exception ex)
                    {
                        Ask("No se pudo instalar la actualización.\n\n" + ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    ExitApp();
                });
            });
        }

        void ExitApp()
        {
            timer.Stop();
            if (updTimer != null) updTimer.Stop();
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPower;
            tray.Visible = false;
            tray.Dispose();
            ExitThread();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--export-icon") { Icons.WriteIco(args[1]); return; }
            try { Native.SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string demo = null;
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "--demo") demo = args[i + 1];

            bool updated = args.Contains("--updated");
            using (Mutex m = new Mutex(false, demo != null ? "Local\\PausasFrozz_SST_demo" : "Local\\PausasFrozz_SST"))
            {
                bool got;
                try { got = m.WaitOne(updated ? 30000 : 0); } catch (AbandonedMutexException) { got = true; }
                if (!got)
                {
                    MessageBox.Show("Pausas Frozz ya está en ejecución.\nBúscalo en la bandeja del sistema, junto al reloj.",
                        "Pausas Frozz", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try
                {
                    Updater.Cleanup();
                    using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) Brand.Scale = g.DpiX / 96f;
                    Application.Run(new TrayApp(args.Contains("--startup"), demo, updated));
                }
                finally { m.ReleaseMutex(); }
            }
        }
    }
}
