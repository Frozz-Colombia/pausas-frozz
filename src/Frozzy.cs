// Frozzy: mascota animada que vive en una esquina de la pantalla.
// Marioneta de recortes hecha con piezas del diseño original (cabeza, torso, manga, pata,
// piernas y el brazo del pulgar arriba). Cada pieza gira sobre su articulación; el brazo
// izquierdo es el derecho reflejado, así ambos lados se mueven igual.
// Ventana "layered" con transparencia por píxel: nunca roba el foco.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PausasFrozz
{
    // Postura (ángulos en grados; positivo = sentido horario en pantalla).
    // Brazos: 0 = colgando; negativo = hacia afuera/arriba; positivo = hacia el cuerpo (por delante).
    class Pose
    {
        public float Lean, Head, HeadDY, ShDY, BodyDY;
        public float RA, RP, LA, LP;            // hombro y codo (manga/pata) de cada brazo
        public float RFront, LFront, Thumb;     // 1 = brazo por delante del torso / brazo del pulgar
        public float LegLDY, LegRDY, LegLA, LegRA;
        public float Eyes = 1;

        public Pose Clone() { return (Pose)MemberwiseClone(); }

        public static void Toward(Pose c, Pose t, float k)
        {
            c.Lean += (t.Lean - c.Lean) * k; c.Head += (t.Head - c.Head) * k; c.HeadDY += (t.HeadDY - c.HeadDY) * k;
            c.ShDY += (t.ShDY - c.ShDY) * k; c.BodyDY += (t.BodyDY - c.BodyDY) * k;
            c.RA += (t.RA - c.RA) * k; c.RP += (t.RP - c.RP) * k; c.LA += (t.LA - c.LA) * k; c.LP += (t.LP - c.LP) * k;
            c.LegLDY += (t.LegLDY - c.LegLDY) * k; c.LegRDY += (t.LegRDY - c.LegRDY) * k;
            c.LegLA += (t.LegLA - c.LegLA) * k; c.LegRA += (t.LegRA - c.LegRA) * k;
            c.RFront = t.RFront; c.LFront = t.LFront; c.Thumb = t.Thumb; c.Eyes = t.Eyes;
        }
    }

    class FrozzyWindow : Form
    {
        [StructLayout(LayoutKind.Sequential)] struct PT { public int x, y; public PT(int a, int b) { x = a; y = b; } }
        [StructLayout(LayoutKind.Sequential)] struct SZ { public int cx, cy; public SZ(int a, int b) { cx = a; cy = b; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLEND { public byte op, flags, alpha, fmt; }
        [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref PT pptDst, ref SZ psize, IntPtr hdcSrc, ref PT pptSrc, int crKey, ref BLEND pblend, int dwFlags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);

        public enum St { Desk, Alert, Active, Cheer }

        // ---- ventana (unidades lógicas, se escalan por DPI)
        public const float LW = 244, LH = 252;
        const float SW = 264, SH = 504;              // lienzo de las piezas (diseño original)
        const float K = 128f / SH;                   // px lógicos por unidad del diseño
        const int SS = 2;                            // supersampling del personaje
        const float Ground = LH - 4;
        const float DeskW = 98, DeskH = 46;
        const float DeskLeft = LW - 6 - DeskW;
        const float DeskTop = Ground - DeskH;
        const float XDesk = DeskLeft + DeskW / 2 - 4;  // Frozzy detrás del escritorio
        const float XStand = DeskLeft - 46;            // Frozzy de pie, a la izquierda
        const float RootSitY = Ground + 3;             // pies (ocultos) detrás del escritorio
        const float LaptopH = 18;

        // ---- articulaciones (coordenadas del diseño original)
        const float FeetX = 130, FeetY = 504;
        const float HipPivX = 130, HipPivY = 410;
        const float NeckX = 148, NeckY = 218;
        const float ShX = 210, ShY = 250;              // hombro derecho
        const float ElX = 233, ElY = 338;              // puño (codo de la marioneta)
        const float PawCX = 233, PawCY = 378;          // centro de la pata
        const float MirrorX = 260;                     // reflejo del brazo derecho → izquierdo (eje x = 130)
        const float LegLX = 85, LegRX = 178, LegY = 405;

        static readonly Color Ink = Color.FromArgb(18, 20, 26);

        readonly float sc;
        readonly int DW, DH;
        readonly Bitmap pzCabeza, pzTorso, pzManga, pzPata, pzPulgar, pzPiernaI, pzPiernaD, logo;
        readonly Bitmap frame, pup;
        readonly Graphics pg;
        bool lowPower;
        readonly Graphics fg;
        readonly System.Windows.Forms.Timer anim;
        readonly DateTime t0 = DateTime.Now;
        readonly Font f1, f2, fBig, fLcd;
        readonly Random rnd = new Random();

        St st = St.Desk;
        DateTime stateStart = DateTime.Now;
        string action = "listo";
        string callText, callSub;
        string sayL1, sayL2; DateTime sayUntil = DateTime.MinValue; bool sayBig;
        string phrase; DateTime phraseFrom, phraseUntil;
        string bubbleKey; DateTime bubbleStart;
        bool hover, down, dragged, busyHidden, userHidden;
        Point downPt, downLoc;

        Pose cur = new Pose();
        readonly List<DateTime> hoverEnters = new List<DateTime>();
        DateTime annoyedUntil = DateTime.MinValue;
        string annoyedText;
        static readonly string[] Molesto = {
            "¡Ey! ¡Déjame trabajar!", "¡Ya, ya, ya! ¡Me haces cosquillas!", "¡Oye! ¡Estoy ocupado!",
            "¡Quieto con ese mouse!", "¡Uy, qué intenso!", "¡Respeta mi espacio personal, parce!",
            "¡Me vas a hacer regar el café!", "¡Aaay, ya pues!" };
        static readonly string dumpDir = Environment.GetEnvironmentVariable("PAUSASFROZZ_DUMP");
        static int dumpN;
        float rootX = XDesk, rootY = RootSitY;
        double lastT;
        string lProp, rProp;
        string idleAction = "teclear"; DateTime idleUntil = DateTime.MinValue, nextIdle = DateTime.Now.AddSeconds(12);
        DateTime nextBlink = DateTime.Now.AddSeconds(3); double blinkStart = -10;

        double nextRemaining = -1, nextFrac;
        int nextMode;   // 0 normal, 1 pendiente, 2 en pausa, 3 fuera de horario

        public event EventHandler Clicked, RightClicked, Moved;
        public Func<string> HoverText;
        public St State { get { return st; } }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00080000 | 0x00000080 | 0x00000008 | 0x08000000; // LAYERED | TOOLWINDOW | TOPMOST | NOACTIVATE
                return cp;
            }
        }

        Bitmap Pieza(string name, float w, float h, float q, float sharpen)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using (Bitmap src = new Bitmap(s))
            {
                int bw = Math.Max(1, (int)Math.Round(w * sc * q)), bh = Math.Max(1, (int)Math.Round(h * sc * q));
                Bitmap b = new Bitmap(bw, bh, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(src, 0, 0, bw, bh);
                }
                if (sharpen > 0) Sharpen(b, sharpen);
                return b;
            }
        }

        // Realce de detalle (unsharp mask 3x3) sobre ARGB premultiplicado
        static void Sharpen(Bitmap b, float amount)
        {
            int w = b.Width, h = b.Height, n = w * h;
            BitmapData d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
            int[] src = new int[n], dst = new int[n];
            Marshal.Copy(d.Scan0, src, 0, n);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x, c = src[i], a = (c >> 24) & 255;
                    if (a == 0) { dst[i] = c; continue; }
                    int sr = 0, sg = 0, sb = 0, cnt = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int yy = y + dy; if (yy < 0 || yy >= h) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx; if (xx < 0 || xx >= w) continue;
                            int q = src[yy * w + xx];
                            sr += (q >> 16) & 255; sg += (q >> 8) & 255; sb += q & 255; cnt++;
                        }
                    }
                    int r0 = (c >> 16) & 255, g0 = (c >> 8) & 255, b0 = c & 255;
                    int r1 = (int)(r0 + amount * (r0 - sr / (float)cnt)), g1 = (int)(g0 + amount * (g0 - sg / (float)cnt)), b1 = (int)(b0 + amount * (b0 - sb / (float)cnt));
                    r1 = Math.Max(0, Math.Min(a, r1)); g1 = Math.Max(0, Math.Min(a, g1)); b1 = Math.Max(0, Math.Min(a, b1));
                    dst[i] = (a << 24) | (r1 << 16) | (g1 << 8) | b1;
                }
            Marshal.Copy(dst, 0, d.Scan0, n);
            b.UnlockBits(d);
        }

        public FrozzyWindow()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual; Text = "Frozzy";
            sc = Brand.Scale;
            DW = (int)Math.Ceiling(LW * sc); DH = (int)Math.Ceiling(LH * sc);
            Size = new Size(DW, DH);
            Cursor = Cursors.Hand;

            // Las piezas se preparan exactamente al doble de la resolución de pantalla: el personaje
            // completo se dibuja en un búfer 2x y se reduce con filtro bicúbico (todo igual de nítido).
            const float sh = 0.45f;
            pzCabeza = Pieza("fz_cabeza.png", SW * K, SH * K, SS, sh);
            pzTorso = Pieza("fz_torso.png", SW * K, SH * K, SS, sh);
            pzManga = Pieza("fz_manga.png", SW * K, SH * K, SS, sh);
            pzPata = Pieza("fz_pata.png", SW * K, SH * K, SS, sh);
            pzPulgar = Pieza("fz_pulgar.png", SW * K, SH * K, SS, sh);
            pzPiernaI = Pieza("fz_pierna_izq.png", SW * K, SH * K, SS, sh);
            pzPiernaD = Pieza("fz_pierna_der.png", SW * K, SH * K, SS, sh);
            logo = Pieza("fz_logo.png", 66, 23, 2f, 0);
            pup = new Bitmap(DW * SS, DH * SS, PixelFormat.Format32bppPArgb);
            pg = Graphics.FromImage(pup);

            frame = new Bitmap(DW, DH, PixelFormat.Format32bppArgb);
            fg = Graphics.FromImage(frame);
            f1 = new Font("Segoe UI", 12.5f, FontStyle.Bold, GraphicsUnit.Pixel);
            f2 = new Font("Segoe UI", 10.5f, FontStyle.Regular, GraphicsUnit.Pixel);
            fBig = new Font("Segoe UI Black", 19f, FontStyle.Bold, GraphicsUnit.Pixel);
            fLcd = new Font("Consolas", 10f, FontStyle.Bold, GraphicsUnit.Pixel);

            anim = new System.Windows.Forms.Timer();
            anim.Interval = 33;
            anim.Tick += delegate { Render(); };
        }

        public static Point DefaultLocation()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            return new Point(wa.Right - (int)Math.Ceiling(LW * Brand.Scale) - Brand.S(4), wa.Bottom - (int)Math.Ceiling(LH * Brand.Scale));
        }

        // Zona de pantalla que ocupa Frozzy de pie (para ubicar el aviso a su lado)
        public Rectangle StandingScreenRect
        {
            get
            {
                float l = XStand - 45;
                return new Rectangle(Left + (int)(l * sc), Top + (int)((Ground - 140) * sc), (int)((LW - l) * sc), (int)(140 * sc));
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Render();
            anim.Start();
        }

        // ------------------------------------------------------- API
        public void Alert(string call, string sub) { callText = call; callSub = sub; Set(St.Alert); phrase = null; }
        public void Accompany() { Set(St.Active); action = "listo"; }
        public void SetAction(string a) { action = string.IsNullOrEmpty(a) ? "listo" : a; if (st != St.Active) Set(St.Active); }

        public void Cheer(string motivational)
        {
            Set(St.Cheer);
            Say("¡Firme!", "¡Bien hecho!", 2.2, true);
            phrase = motivational;
            phraseFrom = DateTime.Now.AddSeconds(2.2);
            phraseUntil = phraseFrom.AddSeconds(string.IsNullOrEmpty(motivational) ? 0 : Math.Min(9, 3.5 + motivational.Length * 0.06));
        }

        public void ForceIdle(string a, double secs) { idleAction = a; idleUntil = DateTime.Now.AddSeconds(secs); nextIdle = idleUntil.AddSeconds(30); }

        public void Annoy() { annoyedUntil = DateTime.Now.AddSeconds(30); annoyedText = Molesto[rnd.Next(Molesto.Length)]; }

        public void SitDown() { callText = null; if (st != St.Desk) Set(St.Desk); }

        public void Say(string l1, string l2, double seconds, bool big)
        {
            sayL1 = l1; sayL2 = l2; sayBig = big; sayUntil = DateTime.Now.AddSeconds(seconds);
        }

        public void SetNext(string label, double remainingSec, double frac, int mode)
        {
            nextRemaining = remainingSec; nextFrac = frac; nextMode = mode;
        }

        public void SetBusyHidden(bool v) { busyHidden = v; UpdateVisibility(); }
        public void SetAway(bool v) { lowPower = v; }
        public void SetUserHidden(bool v) { userHidden = v; UpdateVisibility(); }

        void UpdateVisibility()
        {
            bool show = !busyHidden && !userHidden;
            if (show && !Visible) { Show(); Render(); anim.Start(); }
            else if (!show && Visible) { anim.Stop(); Hide(); }
        }

        void Set(St s) { st = s; stateStart = DateTime.Now; }

        // ------------------------------------------------------ mouse
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { down = true; dragged = false; downPt = Cursor.Position; downLoc = Location; }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (down)
            {
                Point c = Cursor.Position;
                int dx = c.X - downPt.X, dy = c.Y - downPt.Y;
                if (!dragged && Math.Abs(dx) + Math.Abs(dy) > Brand.S(5)) dragged = true;
                if (dragged) { Location = new Point(downLoc.X + dx, downLoc.Y + dy); Render(); }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && down)
            {
                down = false;
                if (dragged) { if (Moved != null) Moved(this, EventArgs.Empty); }
                else if (Clicked != null) Clicked(this, EventArgs.Empty);
            }
            else if (e.Button == MouseButtons.Right && RightClicked != null) RightClicked(this, EventArgs.Empty);
            base.OnMouseUp(e);
        }

        // ---------------------------------------------------- posturas
        static float S(double x) { return (float)Math.Sin(x); }
        static float C(double x) { return (float)Math.Cos(x); }
        static float Ease(double a) { a = Math.Max(0, Math.Min(1, a)); return (float)(1 - Math.Pow(1 - a, 3)); }

        Pose Target(double t, double el, bool seated)
        {
            Pose p = new Pose();
            string a;
            if (st == St.Desk && DateTime.Now < annoyedUntil) a = "molesto";
            else if (seated) a = hover && st == St.Desk ? "saludar" : idleAction;
            else if (st == St.Alert) a = "alerta";
            else if (st == St.Cheer) a = el < 2.2 ? "celebrar" : "pulgar";
            else if (st == St.Active) a = action;
            else a = "listo";
            lProp = null; rProp = null;

            switch (a)
            {
                case "listo":
                    p.RA = S(t * 1.6) * 3; p.LA = S(t * 1.6 + 1) * 3; p.Head = S(t * 0.9) * 2.5f;
                    break;
                case "alerta":
                    {
                        double w = t * 8;
                        p.RA = -138 + S(w) * 16; p.RP = S(w) * 28;
                        p.LA = -138 - S(w) * 16; p.LP = -S(w) * 28;
                        p.Head = S(t * 4) * 5;
                    }
                    break;
                case "celebrar":
                    p.Thumb = 1; p.RA = -142 + S(t * 9) * 10; p.RP = S(t * 9) * 26; p.Head = S(t * 3) * 4;
                    break;
                case "pulgar":
                    p.Thumb = 1; p.Head = 3 + S(t * 1.5) * 2;
                    break;
                case "molesto":
                    p.RA = -125 + S(t * 23) * 38; p.RP = S(t * 27) * 45;
                    p.LA = -125 + C(t * 21) * 38; p.LP = C(t * 25) * 45;
                    p.Head = S(t * 17) * 9; p.BodyDY = -Math.Abs(S(t * 11)) * 3;
                    break;
                case "saludar":
                    p.RA = -140; p.RP = S(t * 9) * 32; p.Head = 4;
                    break;
                case "postura":
                    p.Thumb = 1; p.RA = -8; p.Head = S(t * 1.6) * 3; p.ShDY = 3;
                    break;

                // vista 20-20-20: pata de visera, mirando a lo lejos
                case "mirar_lejos":
                    p.RFront = 1; p.RA = (t % 6) < 3 ? -150 : -158; p.RP = (t % 6) < 3 ? 100 : 118; p.LA = -12; p.LP = 60;
                    p.Head = -3 + S(t * 0.7) * 6; p.Lean = S(t * 0.7) * 1.5f;
                    break;

                // agua
                case "tomar_agua":
                    {
                        rProp = "bottle"; p.RFront = 1;
                        double ph = t % 4.0;
                        if (ph > 0.8 && ph < 3.0) { p.RA = 130; p.RP = -130; p.Head = -8; p.Eyes = 0.2f; }
                        else { p.RA = 25; p.RP = -10; }
                    }
                    break;

                // estiramientos
                case "cuello_der": p.Head = 22; p.Lean = -2; p.Eyes = 0; break;
                case "cuello_izq": p.Head = -22; p.Lean = 2; p.Eyes = 0; break;
                case "menton": p.HeadDY = 10; p.Head = 0; p.Eyes = 0; break;
                case "hombros_circ":
                    p.ShDY = S(t * 3.2) * 8; p.RA = C(t * 3.2) * 6; p.LA = C(t * 3.2) * 6;
                    break;
                case "brazos_frente":
                    p.RFront = 1; p.LFront = 1; p.RA = 50; p.RP = 10; p.LA = 50; p.LP = 10; p.HeadDY = 3;
                    break;
                case "brazos_arriba":
                    p.RA = -132 + S(t * 1.2) * 4; p.RP = -12; p.LA = -132 + S(t * 1.2) * 4; p.LP = -12; p.Eyes = 0; p.BodyDY = -3;
                    break;
                case "giro_der":
                    p.Lean = 7; p.Head = -4; p.RA = -22; p.LFront = 1; p.LA = 38; p.LP = 12;
                    break;
                case "giro_izq":
                    p.Lean = -7; p.Head = 4; p.LA = -22; p.RFront = 1; p.RA = 38; p.RP = 12;
                    break;
                case "munecas":
                    if ((t % 20) < 10) { p.RA = -78; p.RP = -70; p.LFront = 1; p.LA = 55; p.LP = -10; }
                    else { p.LA = -78; p.LP = -70; p.RFront = 1; p.RA = 55; p.RP = -10; }
                    p.HeadDY = 2;
                    break;

                // pausa activa
                case "caminar":
                case "marchar":
                    {
                        bool high = a == "marchar";
                        double w = t * (high ? 6.0 : 7.5);
                        float lift = high ? 34 : 14;
                        p.LegLDY = -Math.Max(0, S(w)) * lift; p.LegRDY = -Math.Max(0, -S(w)) * lift;
                        p.LegLA = Math.Max(0, S(w)) * (high ? 6 : 3); p.LegRA = -Math.Max(0, -S(w)) * (high ? 6 : 3);
                        p.RA = S(w) * (high ? 26 : 16); p.LA = -S(w) * (high ? 26 : 16);
                        p.BodyDY = -Math.Abs(S(w)) * 3; p.Head = S(w) * 2;
                    }
                    break;
                case "puntas":
                    p.BodyDY = -Math.Abs(S(t * 2.4)) * 11; p.RA = -6; p.LA = -6;
                    break;
                case "tobillos":
                    p.LegRDY = -12; p.LegRA = S(t * 5) * 12;
                    p.RA = -12; p.LA = -12; p.RP = S(t * 24) * 16; p.LP = -S(t * 24) * 16;
                    break;
                case "cintura":
                    p.RA = -28; p.RP = 75; p.LA = -28; p.LP = 75; p.ShDY = -3; p.Head = -4;
                    break;
                case "respirar":
                    {
                        float inh = (S(t * Math.PI * 2 / 5) + 1) / 2;
                        p.Eyes = 0; p.RA = -inh * 65; p.LA = -inh * 65; p.ShDY = inh * 4; p.BodyDY = -inh * 2; p.Head = -inh * 3;
                    }
                    break;

                // en el escritorio
                case "teclear":
                    p.RFront = 1; p.LFront = 1; p.RA = 28 + S(t * 13) * 2; p.RP = S(t * 13) * 6; p.LA = 28 + S(t * 13 + 1.7f) * 2; p.LP = S(t * 13 + 1.7f) * 6;
                    p.Head = S(t * 0.6) * 2;
                    break;
                case "cafe":
                    {
                        rProp = "mug"; p.RFront = 1; p.LFront = 1; p.LA = 28;
                        double e2 = t % 5.0;
                        if (e2 > 1.0 && e2 < 3.4) { p.RA = 130; p.RP = -130; p.Head = -7; p.Eyes = 0.2f; }
                        else { p.RA = 60; p.RP = -60; }
                    }
                    break;
                case "celular":
                    lProp = "phone"; p.LFront = 1; p.RFront = 1;
                    p.LA = 140; p.LP = -140; p.RA = 28; p.HeadDY = 3; p.Head = 5 + S(t * 0.8) * 2;
                    break;
                case "bostezo":
                    p.RA = -135; p.RP = -12; p.LA = -135; p.LP = -12; p.Eyes = 0; p.Head = S(t * 1.5) * 3;
                    break;
                case "mirar_alrededor":
                    p.RFront = 1; p.LFront = 1; p.RA = 28; p.LA = 28; p.Head = S(t * 1.3) * 10;
                    break;
            }
            return p;
        }

        // ---------------------------------------------------- render
        void Render()
        {
            if (!IsHandleCreated) return;
            DateTime now = DateTime.Now;
            double t = (now - t0).TotalSeconds;
            double dt = Math.Min(0.1, Math.Max(0.001, t - lastT)); lastT = t;
            double el = (now - stateStart).TotalSeconds;

            if (st == St.Cheer && now > phraseUntil && el > 2.2) SitDown();

            // desplazamiento entre el escritorio y el lado izquierdo
            bool wantSit = st == St.Desk;
            float tx = wantSit ? XDesk : XStand;
            bool walking = Math.Abs(rootX - tx) > 0.6f;
            if (walking) rootX += Math.Sign(tx - rootX) * (float)Math.Min(Math.Abs(tx - rootX), 75 * dt);
            float ty = wantSit && !walking ? RootSitY : Ground;
            rootY += (ty - rootY) * (float)Math.Min(1, dt * 9);
            bool seated = wantSit && !walking;

            if (seated && st == St.Desk)
            {
                if (now > idleUntil && idleAction != "teclear") idleAction = "teclear";
                if (now > nextIdle && !hover)
                {
                    string[] opts = { "cafe", "cafe", "celular", "bostezo", "mirar_alrededor" };
                    idleAction = opts[rnd.Next(opts.Length)];
                    double dur = idleAction == "cafe" ? 5 : idleAction == "celular" ? 6 : 3.2;
                    idleUntil = now.AddSeconds(dur);
                    nextIdle = now.AddSeconds(dur + 18 + rnd.Next(30));
                }
            }
            else if (!seated) idleAction = "teclear";

            Pose target;
            if (walking)
            {
                target = new Pose();
                double w = t * 11;
                target.LegLDY = -Math.Max(0, S(w)) * 16; target.LegRDY = -Math.Max(0, -S(w)) * 16;
                target.RA = S(w) * 18; target.LA = -S(w) * 18; target.BodyDY = -Math.Abs(S(w)) * 4;
                lProp = null; rProp = null;
            }
            else target = Target(t, el, seated);

            if (now > nextBlink) { blinkStart = t; nextBlink = now.AddSeconds(2.5 + rnd.NextDouble() * 3.5); }
            if (t - blinkStart < 0.13) target.Eyes = 0;

            Pose.Toward(cur, target, (float)Math.Min(1, dt * 10));

            // saltos de alerta / celebración
            float hop = 0, crouch = 0;
            if (!seated && !walking && (st == St.Alert || (st == St.Cheer && el < 1.3)))
            {
                const double T = 0.62;
                bool calm = st == St.Alert && el > 45;
                double cyc = calm ? 3 * T + 3.0 : T;
                double ph = el % cyc;
                if (!(calm && ph >= 3 * T))
                {
                    double p = (ph % T) / T;
                    float hgt = st == St.Alert ? 26 : 16;
                    if (p < 0.72) hop = hgt * S(Math.PI * p / 0.72);
                    else crouch = 14 * S(Math.PI * (p - 0.72) / 0.28);
                }
            }

            Graphics g = fg;
            g.ResetTransform(); g.ResetClip();
            g.Clear(Color.Transparent);
            g.ScaleTransform(sc, sc);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            // Frozzy al doble de resolución (solo la zona que ocupa) y luego reducido 2:1 con bicúbico
            int x1 = Math.Max(0, (int)Math.Floor((rootX - 82) * sc)), x2 = Math.Min(DW, (int)Math.Ceiling((rootX + 82) * sc));
            int y2 = Math.Min(DH, (int)Math.Ceiling((Ground + 1) * sc));
            Rectangle dstR = new Rectangle(x1, 0, Math.Max(1, x2 - x1), y2);
            Rectangle srcR = new Rectangle(dstR.X * SS, 0, dstR.Width * SS, dstR.Height * SS);
            pg.ResetTransform(); pg.ResetClip();
            pg.CompositingMode = CompositingMode.SourceCopy;
            using (SolidBrush clr = new SolidBrush(Color.Transparent)) pg.FillRectangle(clr, srcR);
            pg.CompositingMode = CompositingMode.SourceOver;
            pg.SetClip(srcR);
            pg.ScaleTransform(sc * SS, sc * SS);
            pg.SetClip(new RectangleF(0, 0, LW, Ground + 0.5f), CombineMode.Intersect);
            pg.SmoothingMode = SmoothingMode.AntiAlias;
            pg.InterpolationMode = InterpolationMode.HighQualityBilinear;
            pg.PixelOffsetMode = PixelOffsetMode.None;
            pg.TranslateTransform(rootX, rootY - hop);
            pg.ScaleTransform(K, K);
            pg.TranslateTransform(-FeetX, -FeetY);
            DrawPuppet(pg, cur, crouch);
            GraphicsState gs = g.Save();
            g.ResetTransform();
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(pup, dstR, srcR, GraphicsUnit.Pixel);
            g.Restore(gs);

            DrawDesk(g, t, seated && rProp == "mug");

            // globo
            string l1 = null, l2 = null; bool big = false;
            if (st == St.Desk && now < annoyedUntil) { l1 = annoyedText; }
            else if (now < sayUntil) { l1 = sayL1; l2 = sayL2; big = sayBig; }
            else if (st == St.Cheer && phrase != null && now >= phraseFrom && now < phraseUntil) l1 = phrase;
            else if (st == St.Alert && callText != null) { l1 = callText; l2 = callSub ?? "Haz clic en mí"; }
            else if (st == St.Desk && hover && HoverText != null) { l1 = HoverText(); l2 = "Arrástrame para moverme"; }
            if (l1 != null)
            {
                string key = l1 + "|" + l2;
                if (key != bubbleKey) { bubbleKey = key; bubbleStart = now; }
                double be = (now - bubbleStart).TotalSeconds;
                float s = 0.6f + 0.4f * Ease(be / 0.2);
                if (st == St.Alert && now >= sayUntil) s *= 1 + 0.03f * S(t * Math.PI * 2 / 0.9);
                float headTop = rootY - hop - SH * K + 4;
                DrawBubble(g, l1, l2, big, rootX, headTop, s);
            }
            else bubbleKey = null;

            Push();
            if (dumpDir != null && dumpN < 320) { try { frame.Save(Path.Combine(dumpDir, "f" + (dumpN++).ToString("000") + ".png")); } catch { } }

            Point c = Cursor.Position;
            bool h = false;
            if (Visible && Bounds.Contains(c))
            {
                int px = c.X - Left, py = c.Y - Top;
                if (px >= 0 && py >= 0 && px < DW && py < DH) h = frame.GetPixel(px, py).A > 20;
            }
            if (h && !hover && st == St.Desk)
            {
                hoverEnters.Add(now);
                hoverEnters.RemoveAll(x => (now - x).TotalSeconds > 6);
                if (hoverEnters.Count >= 4 && now > annoyedUntil)
                {
                    annoyedUntil = now.AddSeconds(3.5);
                    annoyedText = Molesto[rnd.Next(Molesto.Length)];
                    hoverEnters.Clear();
                }
            }
            hover = h;
            int iv = lowPower ? 250 : (seated && st == St.Desk && !hover && l1 == null && idleAction == "teclear" ? 50 : 33);
            if (anim.Interval != iv) anim.Interval = iv;
        }

        // ------------------------------------------------- marioneta
        static void RotateAt(Graphics g, float deg, float x, float y)
        {
            if (deg == 0) return;
            g.TranslateTransform(x, y); g.RotateTransform(deg); g.TranslateTransform(-x, -y);
        }

        void Piece(Graphics g, Bitmap b) { g.DrawImage(b, new RectangleF(0, 0, SW, SH)); }

        void DrawPuppet(Graphics g, Pose p, float crouch)
        {
            // piernas (quedan detrás del torso)
            DrawLeg(g, pzPiernaI, LegLX, p.BodyDY, p.LegLDY, p.LegLA);
            DrawLeg(g, pzPiernaD, LegRX, p.BodyDY, p.LegRDY, p.LegRA);

            // parte de arriba
            GraphicsState up = g.Save();
            g.TranslateTransform(0, p.BodyDY + crouch);
            RotateAt(g, p.Lean, HipPivX, HipPivY);

            bool thumb = p.Thumb > 0.5f;
            if (p.RFront < 0.5f) DrawArm(g, false, p.RA, p.RP, p.ShDY, rProp);
            if (p.LFront < 0.5f && !thumb) DrawArm(g, true, p.LA, p.LP, p.ShDY, lProp);

            Piece(g, pzTorso);

            GraphicsState hs = g.Save();
            g.TranslateTransform(0, p.HeadDY);
            RotateAt(g, p.Head, NeckX, NeckY);
            Piece(g, pzCabeza);
            if (p.Eyes < 0.5f) DrawClosedEyes(g);
            g.Restore(hs);

            if (thumb) Piece(g, pzPulgar);
            if (p.RFront >= 0.5f) DrawArm(g, false, p.RA, p.RP, p.ShDY, rProp);
            if (p.LFront >= 0.5f && !thumb) DrawArm(g, true, p.LA, p.LP, p.ShDY, lProp);
            g.Restore(up);
        }

        // Levantar la rodilla visto de frente = la pierna se ve más corta (anclada a la cadera)
        void DrawLeg(Graphics g, Bitmap piece, float hipX, float bodyDY, float lift, float angle)
        {
            GraphicsState s = g.Save();
            g.TranslateTransform(0, bodyDY);
            RotateAt(g, angle, hipX, LegY);
            float k = Math.Max(0.6f, 1 + lift / 110f);
            if (k < 0.999f) { g.TranslateTransform(hipX, LegY); g.ScaleTransform(1 + (1 - k) * 0.15f, k); g.TranslateTransform(-hipX, -LegY); }
            Piece(g, piece);
            g.Restore(s);
        }

        void DrawArm(Graphics g, bool left, float a, float pa, float shDY, string prop)
        {
            GraphicsState s = g.Save();
            if (left) { g.TranslateTransform(MirrorX, 0); g.ScaleTransform(-1, 1); }
            g.TranslateTransform(0, -shDY);
            RotateAt(g, a, ShX, ShY);
            GraphicsState ps = g.Save();
            RotateAt(g, pa, ElX, ElY);
            Piece(g, pzPata);
            g.Restore(ps);
            Piece(g, pzManga);
            if (prop != null)
            {
                RotateAt(g, pa, ElX, ElY);
                // contrarresta la rotación para que el objeto quede derecho
                RotateAt(g, -(a + pa), PawCX, PawCY);
                if (left) { g.TranslateTransform(PawCX, PawCY); g.ScaleTransform(-1, 1); g.TranslateTransform(-PawCX, -PawCY); }
                DrawProp(g, prop);
            }
            g.Restore(s);
        }

        static void DrawClosedEyes(Graphics g)
        {
            using (SolidBrush w = new SolidBrush(Color.White))
            using (Pen pn = new Pen(Ink, 5))
            {
                pn.StartCap = LineCap.Round; pn.EndCap = LineCap.Round;
                PointF[] eyes = { new PointF(87, 135), new PointF(155, 140) };
                foreach (PointF e in eyes)
                {
                    g.FillEllipse(w, e.X - 12, e.Y - 15, 24, 30);
                    g.DrawArc(pn, e.X - 10, e.Y - 6, 20, 12, 10, 160);
                }
            }
        }

        static void DrawProp(Graphics g, string prop)
        {
            float x = PawCX, y = PawCY;
            using (Pen pn = new Pen(Ink, 5f))
            {
                pn.LineJoin = LineJoin.Round;
                if (prop == "mug")
                {
                    RectangleF m = new RectangleF(x - 22, y - 62, 44, 52);
                    pn.Width = 6; g.DrawArc(pn, m.Right - 10, m.Y + 10, 24, 22, -90, 180); pn.Width = 5f;
                    using (GraphicsPath mp = Brand.Round(m, 8)) { g.FillPath(Brushes.White, mp); g.DrawPath(pn, mp); }
                    using (SolidBrush b = new SolidBrush(Brand.Blue)) g.FillRectangle(b, m.X + 3, m.Y + 18, m.Width - 6, 12);
                }
                else if (prop == "phone")
                {
                    RectangleF ph = new RectangleF(x - 20, y - 74, 40, 66);
                    using (GraphicsPath pp = Brand.Round(ph, 8))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 44, 54))) { g.FillPath(b, pp); g.DrawPath(pn, pp); }
                    using (SolidBrush scr = new SolidBrush(Color.FromArgb(120, 190, 255))) g.FillRectangle(scr, ph.X + 5, ph.Y + 6, ph.Width - 10, ph.Height - 16);
                    using (SolidBrush bub = new SolidBrush(Color.White)) { g.FillRectangle(bub, ph.X + 9, ph.Y + 13, 18, 6); g.FillRectangle(bub, ph.X + 13, ph.Y + 25, 16, 6); }
                }
                else if (prop == "bottle")
                {
                    RectangleF b = new RectangleF(x - 17, y - 98, 34, 96);
                    using (GraphicsPath bp = Brand.Round(b, 12))
                    using (SolidBrush fill = new SolidBrush(Color.FromArgb(150, 196, 236))) { g.FillPath(fill, bp); g.DrawPath(pn, bp); }
                    using (SolidBrush cap = new SolidBrush(Brand.Blue)) { g.FillRectangle(cap, b.X + 6, b.Y - 13, b.Width - 12, 15); g.DrawRectangle(pn, b.X + 6, b.Y - 13, b.Width - 12, 15); }
                    using (SolidBrush w = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillRectangle(w, b.X + 7, b.Y + 12, 6, b.Height - 30);
                }
            }
        }

        // ---------------------------------------------------- escritorio
        void DrawDesk(Graphics g, double t, bool mugInPaw)
        {
            using (Pen pen = new Pen(Ink, 1.8f))
            {
                pen.LineJoin = LineJoin.Round;

                // portátil (visto por detrás)
                RectangleF lid = new RectangleF(XDesk - 18, DeskTop - LaptopH, 36, LaptopH);
                using (GraphicsPath p = Brand.Round(lid, 3))
                using (LinearGradientBrush b = new LinearGradientBrush(lid, Color.FromArgb(222, 229, 238), Color.FromArgb(184, 196, 212), 90f))
                { g.FillPath(b, p); g.DrawPath(pen, p); }
                float lx = lid.X + lid.Width / 2, ly = lid.Y + lid.Height / 2;
                using (SolidBrush b = new SolidBrush(Brand.Blue)) g.FillEllipse(b, lx - 4.5f, ly - 4.5f, 9, 9);
                using (Pen w = new Pen(Color.White, 1.2f))
                    g.DrawLines(w, new PointF[] { new PointF(lx - 2.2f, ly - 2f), new PointF(lx + 2.2f, ly - 2f), new PointF(lx - 2.2f, ly + 2f), new PointF(lx + 2.2f, ly + 2f) });

                // taza con vapor
                float mx = XDesk + 26, my = DeskTop - 12;
                if (!mugInPaw)
                {
                    using (Pen hp = new Pen(Ink, 1.6f)) g.DrawArc(hp, mx + 7, my + 2.5f, 6, 6, -90, 180);
                    RectangleF mug = new RectangleF(mx, my, 10, 12);
                    using (GraphicsPath p = Brand.Round(mug, 2)) { g.FillPath(Brushes.White, p); g.DrawPath(pen, p); }
                    using (SolidBrush b = new SolidBrush(Brand.Blue)) g.FillRectangle(b, mx + 0.9f, my + 4.5f, 8.2f, 3f);
                    for (int i = 0; i < 2; i++)
                    {
                        double ph = (t * 0.7 + i * 0.5) % 1.0;
                        int a = (int)(150 * Math.Sin(Math.PI * ph));
                        float sx0 = mx + 3 + i * 3.5f, sy0 = my - 2 - (float)ph * 9;
                        using (Pen sp = new Pen(Color.FromArgb(a, 150, 165, 185), 1.3f))
                            g.DrawBezier(sp, sx0, sy0, sx0 - 2.5f, sy0 - 2.5f, sx0 + 2.5f, sy0 - 4.5f, sx0, sy0 - 7);
                    }
                }

                // planta
                float px = DeskLeft + 4, py = DeskTop - 9;
                using (SolidBrush leaf = new SolidBrush(Brand.Green))
                { g.FillEllipse(leaf, px - 1, py - 9, 6, 10); g.FillEllipse(leaf, px + 4, py - 11, 6, 11); g.FillEllipse(leaf, px + 8, py - 8, 6, 9); }
                RectangleF pot = new RectangleF(px, py, 12, 9);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(236, 240, 246))) { g.FillRectangle(b, pot); g.DrawRectangle(pen, pot.X, pot.Y, pot.Width, pot.Height); }

                // escritorio
                RectangleF slab = new RectangleF(DeskLeft - 3, DeskTop, DeskW + 6, 6);
                using (GraphicsPath p = Brand.Round(slab, 2.5f)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(244, 247, 251))) g.FillPath(b, p); g.DrawPath(pen, p); }
                RectangleF front = new RectangleF(DeskLeft + 2, DeskTop + 6, DeskW - 4, Ground - DeskTop - 6.9f);
                using (LinearGradientBrush b = new LinearGradientBrush(front, Color.FromArgb(14, 70, 150), Color.FromArgb(1, 41, 101), 90f)) g.FillRectangle(b, front);
                g.DrawRectangle(pen, front.X, front.Y, front.Width, front.Height);

                // logo oficial
                g.DrawImage(logo, new RectangleF(front.X + (front.Width - 66) / 2, front.Y + 2, 66, 23));

                // reloj: cuánto falta para la próxima pausa
                RectangleF lcd = new RectangleF(front.X + front.Width / 2 - 25, front.Y + 25, 50, 13);
                using (GraphicsPath lp = Brand.Round(lcd, 2.5f))
                using (SolidBrush lb = new SolidBrush(Color.FromArgb(8, 18, 36)))
                using (Pen lpen = new Pen(Color.FromArgb(110, 255, 255, 255), 1)) { g.FillPath(lb, lp); g.DrawPath(lpen, lp); }
                string txt; Color tc = Color.FromArgb(120, 225, 255);
                if (nextMode == 2) { txt = "PAUSA"; tc = Color.FromArgb(255, 214, 90); }
                else if (nextMode == 3) { txt = "--:--"; tc = Color.FromArgb(150, 160, 180); }
                else if (nextMode == 1 || nextRemaining <= 0) { txt = (t % 1.0) < 0.6 ? "¡YA!" : ""; tc = Color.FromArgb(255, 214, 90); }
                else
                {
                    int s = (int)Math.Ceiling(nextRemaining);
                    txt = s >= 3600 ? (s / 3600) + "h" + ((s % 3600) / 60).ToString("00") : (s / 60) + ":" + (s % 60).ToString("00");
                }
                StringFormat cf = new StringFormat(); cf.Alignment = StringAlignment.Center;
                using (SolidBrush tb = new SolidBrush(tc))
                    g.DrawString(txt, fLcd, tb, new RectangleF(lcd.X, lcd.Y - 0.5f, lcd.Width, lcd.Height + 2), cf);
                // barrita de progreso dentro del reloj
                if (nextMode == 0)
                {
                    float fr = (float)Math.Max(0, Math.Min(1, nextFrac));
                    using (SolidBrush pb = new SolidBrush(Color.FromArgb(120, 225, 255)))
                        g.FillRectangle(pb, lcd.X + 3, lcd.Bottom - 2.2f, (lcd.Width - 6) * fr, 1.2f);
                }
            }
        }

        void DrawBubble(Graphics g, string l1, string l2, bool big, float tx, float ty, float s)
        {
            Font a = big ? fBig : f1;
            float maxW = LW - 30;
            StringFormat sf = new StringFormat(); sf.Alignment = StringAlignment.Center;
            SizeF m1 = g.MeasureString(l1, a, (int)maxW, sf);
            SizeF m2 = l2 == null ? SizeF.Empty : g.MeasureString(l2, f2, (int)maxW, sf);
            float w = Math.Max(m1.Width, m2.Width) + 22, h = m1.Height + (l2 == null ? 0 : m2.Height) + 12;
            float bx = Math.Max(4, Math.Min(LW - 4 - w, tx - w * 0.5f));
            float by = Math.Max(2, ty - 10 - h);
            float tailX = Math.Max(bx + 14, Math.Min(bx + w - 14, tx));
            ty = Math.Max(ty, by + h + 6);

            GraphicsState gs = g.Save();
            g.TranslateTransform(tx, ty); g.ScaleTransform(s, s); g.TranslateTransform(-tx, -ty);
            RectangleF r = new RectangleF(bx, by, w, h);
            PointF[] tail = { new PointF(tailX - 7, r.Bottom - 2), new PointF(tailX + 6, r.Bottom - 2), new PointF(tx + 1, ty) };
            using (GraphicsPath p = Brand.Round(r, 10))
            using (Pen pen = new Pen(Ink, 3.6f))
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
            {
                pen.LineJoin = LineJoin.Round;
                g.TranslateTransform(1.5f, 2f); g.FillPath(shadow, p); g.TranslateTransform(-1.5f, -2f);
                g.DrawPath(pen, p); g.DrawPolygon(pen, tail);
                g.FillPath(Brushes.White, p); g.FillPolygon(Brushes.White, tail);
            }
            using (SolidBrush nb = new SolidBrush(Brand.Navy))
                g.DrawString(l1, a, nb, new RectangleF(bx + 4, by + 6, w - 8, m1.Height + 2), sf);
            if (l2 != null)
                using (SolidBrush mb = new SolidBrush(Brand.Muted))
                    g.DrawString(l2, f2, mb, new RectangleF(bx + 4, by + 6 + m1.Height, w - 8, m2.Height + 2), sf);
            g.Restore(gs);
        }

        void Push()
        {
            IntPtr screen = GetDC(IntPtr.Zero), mem = CreateCompatibleDC(screen);
            IntPtr hb = frame.GetHbitmap(Color.FromArgb(0)), old = SelectObject(mem, hb);
            PT dst = new PT(Left, Top), src = new PT(0, 0);
            SZ size = new SZ(DW, DH);
            BLEND bl; bl.op = 0; bl.flags = 0; bl.alpha = 255; bl.fmt = 1;
            UpdateLayeredWindow(Handle, screen, ref dst, ref size, mem, ref src, 0, ref bl, 2);
            SelectObject(mem, old); DeleteObject(hb); DeleteDC(mem); ReleaseDC(IntPtr.Zero, screen);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                anim.Stop(); anim.Dispose(); fg.Dispose(); frame.Dispose(); pg.Dispose(); pup.Dispose();
                foreach (Bitmap b in new Bitmap[] { pzCabeza, pzTorso, pzManga, pzPata, pzPulgar, pzPiernaI, pzPiernaD, logo }) b.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
