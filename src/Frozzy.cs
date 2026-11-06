// Frozzy: mascota animada (marioneta articulada) que vive en una esquina de la pantalla.
// La cabeza es la ilustración original; el cuerpo se dibuja por piezas (torso, brazos con
// codo y piernas con rodilla) y se posiciona con cinemática inversa, así puede hacer los
// ejercicios, tomar café, mirar el celular, saludar, saltar, caminar, etc.
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
    // Postura de la marioneta (unidades "u": la figura de pie mide 504 u)
    class Pose
    {
        public float Torso, Head, HeadDY, Shrug, Lean;
        public float LPx, LPy, RPx, RPy;   // patas (manos) relativas a cada hombro
        public float LFx, LFy, RFx, RFy;   // pies relativos a cada cadera
        public float LThumb, RThumb, Eyes = 1, ArmStretch = 1, ShoeTilt;

        public Pose Clone() { return (Pose)MemberwiseClone(); }

        public static void Toward(Pose c, Pose t, float k)
        {
            c.Torso += (t.Torso - c.Torso) * k; c.Head += (t.Head - c.Head) * k; c.HeadDY += (t.HeadDY - c.HeadDY) * k;
            c.Shrug += (t.Shrug - c.Shrug) * k; c.Lean += (t.Lean - c.Lean) * k;
            c.LPx += (t.LPx - c.LPx) * k; c.LPy += (t.LPy - c.LPy) * k; c.RPx += (t.RPx - c.RPx) * k; c.RPy += (t.RPy - c.RPy) * k;
            c.LFx += (t.LFx - c.LFx) * k; c.LFy += (t.LFy - c.LFy) * k; c.RFx += (t.RFx - c.RFx) * k; c.RFy += (t.RFy - c.RFy) * k;
            c.LThumb += (t.LThumb - c.LThumb) * k; c.RThumb += (t.RThumb - c.RThumb) * k;
            c.ArmStretch += (t.ArmStretch - c.ArmStretch) * k; c.ShoeTilt += (t.ShoeTilt - c.ShoeTilt) * k;
            c.Eyes = t.Eyes; // el parpadeo es instantáneo
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

        // ---- geometría de la ventana (unidades lógicas, se escalan por DPI)
        public const float LW = 244, LH = 252;
        const float K = 118f / 504f;                 // px lógicos por unidad de figura
        const float Ground = LH - 4;
        const float DeskW = 98, DeskH = 46;
        const float DeskLeft = LW - 6 - DeskW;
        const float DeskTop = Ground - DeskH;
        const float XDesk = DeskLeft + DeskW / 2 - 4;  // Frozzy sentado
        const float XStand = DeskLeft - 46;            // Frozzy de pie, a la izquierda
        const float RootStandY = Ground - 142 * K;      // cadera de pie (pies en el piso)
        const float RootSitY = DeskTop + 20 * K;        // cadera sentado (pecho a la altura del escritorio)

        // ---- esqueleto (u)
        const float TorsoLen = 140, ShX = 64, ShY = 36, UpperArm = 56, ForeArm = 52, PawR = 19;
        const float HipX = 30, HipY = 12, Thigh = 62, Shin = 56;
        const float HeadPivotX = 107, HeadPivotY = 218, HeadW = 209, HeadH = 225;

        static readonly Color Ink = Color.FromArgb(24, 26, 32);
        static readonly Color Fur = Color.White;
        static readonly Color Shirt = Color.FromArgb(252, 253, 255);
        static readonly Color ShirtShade = Color.FromArgb(226, 232, 241);
        static readonly Color Jeans = Color.FromArgb(44, 104, 206);
        static readonly Color JeansDark = Color.FromArgb(30, 76, 160);
        static readonly Color JeansCuff = Color.FromArgb(86, 140, 228);

        readonly float sc;
        readonly int DW, DH;
        readonly Bitmap head, frame;
        readonly Graphics fg;
        readonly System.Windows.Forms.Timer anim;
        readonly DateTime t0 = DateTime.Now;
        readonly Font f1, f2, fBig, fLogo, fLcd, fShirt;
        readonly Random rnd = new Random();

        St st = St.Desk;
        DateTime stateStart = DateTime.Now;
        string action = "idle";
        string callText, callSub;
        string sayL1, sayL2; DateTime sayUntil = DateTime.MinValue; bool sayBig;
        string phrase; DateTime phraseFrom, phraseUntil;
        string bubbleKey; DateTime bubbleStart;
        bool hover, down, dragged, busyHidden, userHidden;
        Point downPt, downLoc;

        // marioneta
        Pose cur;
        float rootX = XDesk, rootY = RootSitY;
        double lastT;
        string lProp, rProp;              // objeto en cada pata: mug, phone, bottle
        string idleAction = "teclear"; DateTime idleUntil = DateTime.MinValue, nextIdle = DateTime.Now.AddSeconds(12);
        DateTime nextBlink = DateTime.Now.AddSeconds(3); double blinkStart = -10;

        // reloj / próxima pausa
        string nextLabel = "";
        double nextRemaining = -1, nextFrac = 0;
        int nextMode; // 0 normal, 1 pendiente, 2 en pausa, 3 fuera de horario

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

        public FrozzyWindow()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual; Text = "Frozzy";
            sc = Brand.Scale;
            DW = (int)Math.Ceiling(LW * sc); DH = (int)Math.Ceiling(LH * sc);
            Size = new Size(DW, DH);
            Cursor = Cursors.Hand;

            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("frozzy_head.png"))
            using (Bitmap src = new Bitmap(s))
            {
                int hw = (int)Math.Round(HeadW * K * sc * 1.25f), hh = (int)Math.Round(HeadH * K * sc * 1.25f);
                head = new Bitmap(hw, hh, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(head))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(src, 0, 0, hw, hh);
                }
            }
            frame = new Bitmap(DW, DH, PixelFormat.Format32bppArgb);
            fg = Graphics.FromImage(frame);
            f1 = new Font("Segoe UI", 12.5f, FontStyle.Bold, GraphicsUnit.Pixel);
            f2 = new Font("Segoe UI", 10.5f, FontStyle.Regular, GraphicsUnit.Pixel);
            fBig = new Font("Segoe UI Black", 19f, FontStyle.Bold, GraphicsUnit.Pixel);
            fLogo = new Font("Segoe UI Black", 10.5f, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Pixel);
            fLcd = new Font("Consolas", 10f, FontStyle.Bold, GraphicsUnit.Pixel);
            fShirt = new Font("Segoe UI Black", 13f, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Pixel);

            cur = SitPose(0);
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
                return new Rectangle(Left + (int)(l * sc), Top + (int)((RootStandY - 100) * sc), (int)((LW - l) * sc), (int)((Ground - RootStandY + 100) * sc));
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
        public void Accompany() { Set(St.Active); action = "listo"; Say("¡Vamos, yo te acompaño!", null, 3, false); }
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

        public void SitDown() { callText = null; if (st != St.Desk) Set(St.Desk); }

        public void Say(string l1, string l2, double seconds, bool big)
        {
            sayL1 = l1; sayL2 = l2; sayBig = big; sayUntil = DateTime.Now.AddSeconds(seconds);
        }

        public void SetNext(string label, double remainingSec, double frac, int mode)
        {
            nextLabel = label; nextRemaining = remainingSec; nextFrac = frac; nextMode = mode;
        }

        public void SetBusyHidden(bool v) { busyHidden = v; UpdateVisibility(); }
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

        static Pose StandPose()
        {
            Pose p = new Pose();
            p.LPx = -8; p.LPy = 102; p.RPx = 8; p.RPy = 102;
            p.LFx = -4; p.LFy = 114; p.RFx = 4; p.RFy = 114;
            return p;
        }

        static Pose SitPose(double t)
        {
            Pose p = new Pose();
            p.LPx = 30; p.LPy = 80; p.RPx = -30; p.RPy = 80;
            p.LFx = -14; p.LFy = 58; p.RFx = 14; p.RFy = 58;
            return p;
        }

        // Postura objetivo según lo que esté haciendo
        Pose Target(double t, double el, bool seated)
        {
            Pose p = seated ? SitPose(t) : StandPose();
            string a;
            if (seated) a = hover && st == St.Desk ? "saludar_sentado" : idleAction;
            else if (st == St.Alert) a = "alerta";
            else if (st == St.Cheer) a = el < 2.2 ? "celebrar" : "pulgar";
            else if (st == St.Active) a = action;
            else a = "listo";
            lProp = null; rProp = null;

            switch (a)
            {
                // ---------------- de pie / generales
                case "listo":
                    p.LPy += S(t * 2) * 3; p.RPy += S(t * 2 + 1) * 3; p.Head = S(t * 0.9) * 3;
                    break;
                case "alerta":
                    {
                        double w = t * 9;
                        p.LPx = -36 + S(w) * 14; p.LPy = -54 + C(w) * 10;
                        p.RPx = 36 - S(w) * 14; p.RPy = -54 - C(w) * 10;
                        p.Head = S(t * 4.5) * 6;
                    }
                    break;
                case "celebrar":
                    p.LPx = -6; p.LPy = -8; p.LThumb = 1;
                    p.RPx = 40 + S(t * 10) * 12; p.RPy = -62;
                    p.Head = S(t * 3) * 5;
                    break;
                case "pulgar":
                    p.LPx = -6; p.LPy = -8; p.LThumb = 1;
                    p.RPy = 100; p.Head = 4;
                    break;
                case "saludar":
                    p.RPx = 42 + S(t * 9) * 14; p.RPy = -64; p.Head = 5;
                    break;
                case "postura":
                    p.Shrug = -3; p.LPx = -6; p.LPy = -8; p.LThumb = 1; p.RPx = 6; p.RPy = -8; p.RThumb = 1;
                    p.Head = S(t * 1.5) * 3;
                    break;

                // ---------------- vista 20-20-20: mira a lo lejos con la pata de visera
                case "mirar_lejos":
                    p.RPx = -46; p.RPy = -118; p.ArmStretch = 1.38f;
                    p.LPx = 8; p.LPy = 72;
                    p.Head = -4 + S(t * 0.7) * 7; p.Torso = S(t * 0.7) * 2;
                    break;

                // ---------------- agua
                case "tomar_agua":
                    {
                        rProp = "bottle";
                        double ph = t % 4.0;
                        bool drink = ph > 0.8 && ph < 3.0;
                        if (drink) { p.RPx = -48; p.RPy = -44; p.Head = -9; p.Eyes = 0.2f; }
                        else { p.RPx = 18; p.RPy = 70; }
                    }
                    break;

                // ---------------- estiramientos
                case "cuello_der": p.Head = 24; p.Torso = -2; p.LPx = 30; p.LPy = -58; p.ArmStretch = 1.25f; p.Eyes = 0; break;
                case "cuello_izq": p.Head = -24; p.Torso = 2; p.RPx = -30; p.RPy = -58; p.ArmStretch = 1.25f; p.Eyes = 0; break;
                case "menton": p.HeadDY = 12; p.Eyes = 0; p.LPx = 34; p.LPy = -40; p.RPx = -34; p.RPy = -40; p.ArmStretch = 1.2f; break;
                case "hombros_circ":
                    p.Shrug = S(t * 3.2) * 9; p.LPx = -8 + C(t * 3.2) * 6; p.RPx = 8 - C(t * 3.2) * 6;
                    p.LPy = 100 - S(t * 3.2) * 9; p.RPy = 100 - S(t * 3.2) * 9;
                    break;
                case "brazos_frente":
                    p.LPx = 62; p.LPy = 44; p.RPx = -62; p.RPy = 44; p.Head = 0; p.HeadDY = 4; p.Shrug = 3;
                    break;
                case "brazos_arriba":
                    p.LPx = -46; p.LPy = -112; p.RPx = 46; p.RPy = -112; p.ArmStretch = 1.42f;
                    p.Eyes = 0; p.Torso = S(t * 1.2) * 3; p.LFy = 117; p.RFy = 117; p.ShoeTilt = 8;
                    break;
                case "giro_der":
                    p.Torso = 9; p.Head = -6; p.LPx = 58; p.LPy = 62; p.RPx = 30; p.RPy = 78;
                    break;
                case "giro_izq":
                    p.Torso = -9; p.Head = 6; p.LPx = -30; p.LPy = 78; p.RPx = -58; p.RPy = 62;
                    break;
                case "munecas":
                    if ((t % 20) < 10) { p.LPx = 50; p.LPy = 28; p.RPx = -86; p.RPy = 26; }
                    else { p.LPx = 86; p.LPy = 26; p.RPx = -50; p.RPy = 28; }
                    p.HeadDY = 3;
                    break;

                // ---------------- pausa activa
                case "caminar":
                case "marchar":
                    {
                        bool high = a == "marchar";
                        double w = t * (high ? 6.0 : 7.5);
                        float lift = high ? 46 : 22;
                        p.LFy = 114 - Math.Max(0, S(w)) * lift; p.RFy = 114 - Math.Max(0, -S(w)) * lift;
                        p.LPx = -10 + S(w) * 16; p.LPy = 96 - Math.Max(0, -S(w)) * (high ? 40 : 18);
                        p.RPx = 10 + S(w) * 16; p.RPy = 96 - Math.Max(0, S(w)) * (high ? 40 : 18);
                        p.Head = S(w) * 3;
                    }
                    break;
                case "puntas":
                    {
                        float up = Math.Abs(S(t * 2.4));
                        p.LFy = 114 + up * 4; p.RFy = 114 + up * 4; p.ShoeTilt = up * 22;
                        p.LPy = 100 - up * 6; p.RPy = 100 - up * 6;
                    }
                    break;
                case "tobillos":
                    p.LFx = -10 + C(t * 5) * 10; p.LFy = 98 + S(t * 5) * 7;
                    p.LPx = -14 + S(t * 26) * 5; p.LPy = 80 + C(t * 26) * 4;
                    p.RPx = 14 - S(t * 26) * 5; p.RPy = 80 - C(t * 26) * 4;
                    p.Torso = 3;
                    break;
                case "cintura":
                    p.LPx = 0; p.LPy = 72; p.RPx = 0; p.RPy = 72; p.Shrug = -5; p.Head = -5; p.Lean = 1;
                    break;
                case "respirar":
                    {
                        float inh = (S(t * Math.PI * 2 / 5) + 1) / 2;
                        p.Eyes = 0; p.Shrug = inh * 6;
                        p.LPx = -8 - inh * 30; p.LPy = 100 - inh * 46; p.RPx = 8 + inh * 30; p.RPy = 100 - inh * 46;
                        p.Head = -inh * 4;
                    }
                    break;

                // ---------------- sentado (standby)
                case "teclear":
                    p.LPy += Math.Max(0, S(t * 13)) * -5; p.RPy += Math.Max(0, S(t * 13 + 1.7)) * -5;
                    p.Head = S(t * 0.6) * 2; p.HeadDY = 1;
                    break;
                case "cafe":
                    {
                        rProp = "mug";
                        double e2 = t % 5.0;
                        bool sip = e2 > 1.0 && e2 < 3.4;
                        if (sip) { p.RPx = -54; p.RPy = -48; p.Head = -8; p.Eyes = 0.2f; }
                        else { p.RPx = 22; p.RPy = 40; }
                    }
                    break;
                case "celular":
                    lProp = "phone";
                    p.LPx = 44; p.LPy = -14; p.HeadDY = 4; p.Head = 6 + S(t * 0.8) * 2;
                    p.RPx = -28; p.RPy = 78;
                    break;
                case "bostezo":
                    p.LPx = -44; p.LPy = -108; p.RPx = 44; p.RPy = -108; p.ArmStretch = 1.38f; p.Eyes = 0; p.Head = S(t * 1.5) * 4;
                    break;
                case "mirar_alrededor":
                    p.Head = S(t * 1.3) * 10; p.Torso = S(t * 1.3) * 2;
                    break;
                case "saludar_sentado":
                    p.RPx = 30 + S(t * 9) * 12; p.RPy = -60; p.Head = 5;
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

            // posición: de pie a la izquierda del escritorio o sentado en él
            bool wantSit = st == St.Desk;
            float tx = wantSit ? XDesk : XStand;
            bool atX = Math.Abs(rootX - tx) < 0.6f;
            bool walking = false;
            float targetY;
            if (wantSit)
            {
                if (!atX) { targetY = RootStandY; if (Math.Abs(rootY - RootStandY) < 1.5f) { walking = true; rootX += Math.Sign(tx - rootX) * (float)Math.Min(Math.Abs(tx - rootX), 70 * dt); } }
                else targetY = RootSitY;
            }
            else
            {
                targetY = RootStandY;
                if (!atX && Math.Abs(rootY - RootStandY) < 1.5f) { walking = true; rootX += Math.Sign(tx - rootX) * (float)Math.Min(Math.Abs(tx - rootX), 70 * dt); }
            }
            rootY += (targetY - rootY) * (float)Math.Min(1, dt * 9);
            bool seated = wantSit && atX && Math.Abs(rootY - RootSitY) < 3;

            // acciones de espera en el escritorio
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
            else idleAction = "teclear";

            Pose target;
            if (walking) { action = action ?? "listo"; target = Target(t, el, false); WalkLegs(target, t); }
            else target = Target(t, el, seated);

            // parpadeo
            if (now > nextBlink) { blinkStart = t; nextBlink = now.AddSeconds(2.5 + rnd.NextDouble() * 3.5); }
            if (t - blinkStart < 0.13) target.Eyes = 0;

            Pose.Toward(cur, target, (float)Math.Min(1, dt * 10));

            // saltos (alerta / celebración): aditivos sobre la postura
            float hop = 0, crouch = 0, tuck = 0;
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
                    if (p < 0.72) { double a = p / 0.72; hop = hgt * S(Math.PI * a); tuck = 14 * S(Math.PI * a); }
                    else { double q = (p - 0.72) / 0.28; crouch = 12 * S(Math.PI * q); }
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

            // Frozzy (detrás del escritorio; recortado al piso)
            GraphicsState gs = g.Save();
            g.SetClip(new RectangleF(0, 0, LW, Ground + 0.5f));
            g.TranslateTransform(rootX, rootY - hop + crouch * K);
            g.ScaleTransform(K, K);
            Pose drawPose = cur.Clone();
            drawPose.LFy -= tuck + crouch; drawPose.RFy -= tuck + crouch;
            DrawPuppet(g, drawPose, t);
            g.Restore(gs);

            DrawDesk(g, t, seated && rProp == "mug");

            // globo
            string l1 = null, l2 = null; bool big = false;
            if (now < sayUntil) { l1 = sayL1; l2 = sayL2; big = sayBig; }
            else if (st == St.Cheer && phrase != null && now >= phraseFrom && now < phraseUntil) { l1 = phrase; }
            else if (st == St.Alert && callText != null) { l1 = callText; l2 = callSub ?? "Haz clic en mí"; }
            else if (st == St.Desk && hover && HoverText != null) { l1 = HoverText(); l2 = "Arrástrame para moverme"; }
            if (l1 != null)
            {
                string key = l1 + "|" + l2;
                if (key != bubbleKey) { bubbleKey = key; bubbleStart = now; }
                double be = (now - bubbleStart).TotalSeconds;
                float s = 0.6f + 0.4f * Ease(be / 0.2);
                if (st == St.Alert && now >= sayUntil) s *= 1 + 0.03f * S(t * Math.PI * 2 / 0.9);
                float headTop = rootY - hop - (TorsoLen + HeadPivotY - 6) * K;
                DrawBubble(g, l1, l2, big, rootX, headTop, s);
            }
            else bubbleKey = null;

            Push();

            // hover: píxel opaco bajo el cursor
            Point c = Cursor.Position;
            bool h = false;
            if (Visible && Bounds.Contains(c))
            {
                int px = c.X - Left, py = c.Y - Top;
                if (px >= 0 && py >= 0 && px < DW && py < DH) h = frame.GetPixel(px, py).A > 20;
            }
            hover = h;
            if (anim.Interval != 33) anim.Interval = 33;
        }

        void WalkLegs(Pose p, double t)
        {
            double w = t * 11;
            p.LFy = 114 - Math.Max(0, S(w)) * 20; p.RFy = 114 - Math.Max(0, -S(w)) * 20;
            p.LPx = -8 - S(w) * 12; p.RPx = 8 - S(w) * 12;
        }

        // ------------------------------------------------- marioneta
        static PointF Rot(PointF v, float deg)
        {
            double r = deg * Math.PI / 180;
            return new PointF((float)(v.X * Math.Cos(r) - v.Y * Math.Sin(r)), (float)(v.X * Math.Sin(r) + v.Y * Math.Cos(r)));
        }
        static PointF Add(PointF a, PointF b) { return new PointF(a.X + b.X, a.Y + b.Y); }

        // Cinemática inversa de dos huesos. bend = +1/-1 elige hacia dónde dobla el codo/rodilla.
        static void IK(PointF s, PointF target, float l1, float l2, int bend, out PointF mid, out PointF end)
        {
            float dx = target.X - s.X, dy = target.Y - s.Y;
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            float max = l1 + l2 - 0.5f, min = Math.Abs(l1 - l2) + 0.5f;
            if (d < 0.001f) { dx = 0; dy = 1; d = min; }
            float dc = Math.Max(min, Math.Min(max, d));
            double a = Math.Atan2(dy, dx);
            double cosB = (l1 * l1 + dc * dc - l2 * l2) / (2 * l1 * dc);
            double b = Math.Acos(Math.Max(-1, Math.Min(1, cosB)));
            double ang = a + bend * b;
            mid = new PointF(s.X + l1 * (float)Math.Cos(ang), s.Y + l1 * (float)Math.Sin(ang));
            end = new PointF(s.X + dc * (float)Math.Cos(a), s.Y + dc * (float)Math.Sin(a));
        }

        void DrawPuppet(Graphics g, Pose p, double t)
        {
            PointF root = new PointF(0, 0);
            PointF neck = Rot(new PointF(0, -TorsoLen), p.Torso);
            PointF lSh = Add(neck, Rot(new PointF(-ShX, ShY - p.Shrug), p.Torso));
            PointF rSh = Add(neck, Rot(new PointF(ShX, ShY - p.Shrug), p.Torso));
            PointF lHip = new PointF(-HipX, HipY), rHip = new PointF(HipX, HipY);

            // piernas
            PointF lKnee, lAnk, rKnee, rAnk;
            IK(lHip, Add(lHip, new PointF(p.LFx, p.LFy)), Thigh, Shin, 1, out lKnee, out lAnk);
            IK(rHip, Add(rHip, new PointF(p.RFx, p.RFy)), Thigh, Shin, -1, out rKnee, out rAnk);
            DrawLeg(g, lHip, lKnee, lAnk, -1, p.ShoeTilt);
            DrawLeg(g, rHip, rKnee, rAnk, 1, p.ShoeTilt);

            // torso (camisa) + pelvis (jean) + correa
            GraphicsState gs = g.Save();
            g.RotateTransform(p.Torso);
            DrawTorso(g, p);
            g.Restore(gs);

            // cabeza
            gs = g.Save();
            g.TranslateTransform(neck.X, neck.Y + p.HeadDY);
            g.RotateTransform(p.Torso + p.Head);
            g.DrawImage(head, new RectangleF(-HeadPivotX, -HeadPivotY, HeadW, HeadH));
            if (p.Eyes < 0.5f) DrawClosedEyes(g);
            g.Restore(gs);

            // cuello de la camisa (tapa el corte de la cabeza)
            gs = g.Save();
            g.TranslateTransform(neck.X, neck.Y);
            g.RotateTransform(p.Torso);
            DrawCollar(g);
            g.Restore(gs);

            // brazos
            float la = UpperArm * p.ArmStretch, lf = ForeArm * p.ArmStretch;
            PointF lEl, lPaw, rEl, rPaw;
            IK(lSh, Add(lSh, Rot(new PointF(p.LPx, p.LPy), p.Torso)), la, lf, 1, out lEl, out lPaw);
            IK(rSh, Add(rSh, Rot(new PointF(p.RPx, p.RPy), p.Torso)), la, lf, -1, out rEl, out rPaw);
            DrawArm(g, lSh, lEl, lPaw, p.LThumb, lProp, -1);
            DrawArm(g, rSh, rEl, rPaw, p.RThumb, rProp, 1);
        }

        static void Capsule(Graphics g, PointF a, PointF b, float w, Color fill, bool outline)
        {
            using (Pen pn = new Pen(outline ? Ink : fill, outline ? w + 10 : w))
            { pn.StartCap = LineCap.Round; pn.EndCap = LineCap.Round; g.DrawLine(pn, a, b); }
        }

        void DrawLeg(Graphics g, PointF hip, PointF knee, PointF ank, int side, float tilt)
        {
            // jean
            Capsule(g, hip, knee, 50, Ink, true); Capsule(g, knee, ank, 46, Ink, true);
            Capsule(g, hip, knee, 50, Jeans, false); Capsule(g, knee, ank, 46, Jeans, false);
            // costura y dobladillo
            using (Pen seam = new Pen(JeansDark, 3)) g.DrawLine(seam, Lerp(hip, knee, 0.15f, side * 10), Lerp(knee, ank, 0.75f, side * 10));
            PointF c1 = Lerp(knee, ank, 0.74f, 0), c2 = Lerp(knee, ank, 0.9f, 0);
            Capsule(g, c1, c2, 48, Ink, true);
            Capsule(g, c1, c2, 48, JeansCuff, false);
            // zapato (encima del jean)
            GraphicsState gs = g.Save();
            g.TranslateTransform(ank.X, ank.Y);
            g.RotateTransform(-side * tilt);
            RectangleF shoe = new RectangleF(side < 0 ? -42 : -20, -2, 62, 24);
            using (GraphicsPath sp = Brand.Round(shoe, 11))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(34, 36, 42)))
            using (Pen pn = new Pen(Ink, 5))
            { g.FillPath(b, sp); g.DrawPath(pn, sp); }
            using (Pen hl = new Pen(Color.FromArgb(110, 255, 255, 255), 3)) g.DrawLine(hl, shoe.X + 12, shoe.Y + 6, shoe.Right - 14, shoe.Y + 6);
            g.Restore(gs);
        }

        static PointF Lerp(PointF a, PointF b, float t, float side)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y, len = (float)Math.Sqrt(dx * dx + dy * dy) + 0.001f;
            return new PointF(a.X + dx * t + (-dy / len) * side * -1, a.Y + dy * t + (dx / len) * side * -1);
        }

        void DrawTorso(Graphics g, Pose p)
        {
            float top = -TorsoLen;
            // pelvis (jean)
            RectangleF pel = new RectangleF(-60, -18, 120, 44);
            using (GraphicsPath pp = Brand.Round(pel, 16))
            using (SolidBrush b = new SolidBrush(Jeans))
            using (Pen pn = new Pen(Ink, 5)) { g.FillPath(b, pp); g.DrawPath(pn, pp); }
            using (Pen fly = new Pen(JeansDark, 3)) g.DrawLine(fly, 4, -4, 2, 22);

            // camisa
            PointF[] pts = {
                new PointF(-58, -14), new PointF(-61, -50), new PointF(-66 + p.Shrug * 0.3f, top + 50), new PointF(-60, top + 22 - p.Shrug),
                new PointF(-34, top + 6), new PointF(0, top + 2), new PointF(34, top + 6), new PointF(60, top + 22 - p.Shrug),
                new PointF(66 - p.Shrug * 0.3f, top + 50), new PointF(61, -50), new PointF(58, -14), new PointF(0, -10) };
            using (GraphicsPath sp = new GraphicsPath())
            {
                sp.AddClosedCurve(pts, 0.35f);
                using (SolidBrush b = new SolidBrush(Shirt)) g.FillPath(b, sp);
                GraphicsState gs = g.Save();
                g.SetClip(sp, CombineMode.Intersect);
                using (SolidBrush sh = new SolidBrush(ShirtShade)) { g.FillRectangle(sh, 40, top - 10, 40, TorsoLen + 10); g.FillRectangle(sh, -80, -40, 160, 30); }
                g.Restore(gs);
                using (Pen pn = new Pen(Ink, 5)) { pn.LineJoin = LineJoin.Round; g.DrawPath(pn, sp); }
            }
            using (Pen pl = new Pen(Color.FromArgb(150, 160, 175), 2.5f))
            {
                g.DrawLine(pl, 3, top + 16, 3, -22);
                // bolsillo
                g.DrawRectangle(pl, 18, top + 48, 30, 30);
                g.DrawLine(pl, 18, top + 56, 48, top + 56);
            }
            using (SolidBrush bt = new SolidBrush(Color.FromArgb(170, 180, 195)))
                for (int i = 0; i < 3; i++) g.FillEllipse(bt, 0, top + 36 + i * 30, 6, 6);
            using (SolidBrush lb = new SolidBrush(Brand.Blue))
                g.DrawString("FROZZ", fShirt, lb, 12, top + 28);

            // correa
            RectangleF belt = new RectangleF(-60, -24, 120, 16);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(58, 62, 72))) g.FillRectangle(b, belt);
            using (Pen pn = new Pen(Ink, 4)) g.DrawRectangle(pn, belt.X, belt.Y, belt.Width, belt.Height);
            RectangleF bk = new RectangleF(-14, -27, 26, 22);
            using (Pen gold = new Pen(Color.FromArgb(198, 168, 74), 5)) g.DrawRectangle(gold, bk.X, bk.Y, bk.Width, bk.Height);
            using (Pen pn = new Pen(Ink, 2)) g.DrawRectangle(pn, bk.X - 2.5f, bk.Y - 2.5f, bk.Width + 5, bk.Height + 5);
        }

        static void DrawCollar(Graphics g)
        {
            PointF[] l = { new PointF(-40, -4), new PointF(-4, 14), new PointF(-24, 32) };
            PointF[] r = { new PointF(40, -4), new PointF(4, 14), new PointF(24, 32) };
            using (SolidBrush b = new SolidBrush(Color.White))
            using (Pen pn = new Pen(Ink, 4.5f))
            {
                pn.LineJoin = LineJoin.Round;
                g.FillPolygon(b, l); g.DrawPolygon(pn, l);
                g.FillPolygon(b, r); g.DrawPolygon(pn, r);
            }
        }

        static void DrawClosedEyes(Graphics g)
        {
            using (SolidBrush w = new SolidBrush(Color.White))
            using (Pen pn = new Pen(Ink, 5))
            {
                pn.StartCap = LineCap.Round; pn.EndCap = LineCap.Round;
                PointF[] eyes = { new PointF(46 - HeadPivotX, 135 - HeadPivotY), new PointF(114 - HeadPivotX, 140 - HeadPivotY) };
                foreach (PointF e in eyes)
                {
                    g.FillEllipse(w, e.X - 12, e.Y - 15, 24, 30);
                    g.DrawArc(pn, e.X - 10, e.Y - 6, 20, 12, 10, 160);
                }
            }
        }

        void DrawArm(Graphics g, PointF sh, PointF el, PointF paw, float thumb, string prop, int side)
        {
            // manga (blanca) hasta la muñeca
            PointF wrist = Lerp(el, paw, 0.82f, 0);
            Capsule(g, sh, el, 36, Ink, true); Capsule(g, el, wrist, 32, Ink, true);
            Capsule(g, sh, el, 36, Shirt, false); Capsule(g, el, wrist, 32, Shirt, false);
            using (Pen cuff = new Pen(Color.FromArgb(150, 160, 175), 2.5f))
            {
                PointF a = Lerp(el, wrist, 0.8f, 15), b = Lerp(el, wrist, 0.8f, -15);
                g.DrawLine(cuff, a, b);
            }
            if (prop != null) DrawProp(g, prop, paw, side);
            // pata
            using (SolidBrush f = new SolidBrush(Fur))
            using (Pen pn = new Pen(Ink, 5))
            {
                g.FillEllipse(f, paw.X - PawR, paw.Y - PawR, PawR * 2, PawR * 2);
                g.DrawEllipse(pn, paw.X - PawR, paw.Y - PawR, PawR * 2, PawR * 2);
                if (thumb > 0.5f)
                {
                    RectangleF th = new RectangleF(paw.X - 7, paw.Y - PawR - 16, 14, 24);
                    using (GraphicsPath tp = Brand.Round(th, 7)) { g.FillPath(f, tp); g.DrawPath(pn, tp); }
                    g.FillEllipse(f, paw.X - PawR + 3, paw.Y - PawR + 6, PawR * 2 - 6, 10);
                }
            }
        }

        void DrawProp(Graphics g, string prop, PointF paw, int side)
        {
            using (Pen pn = new Pen(Ink, 4.5f))
            {
                pn.LineJoin = LineJoin.Round;
                if (prop == "mug")
                {
                    RectangleF m = new RectangleF(paw.X - side * 4 - 16, paw.Y - 34, 32, 40);
                    g.DrawArc(pn, side > 0 ? m.Right - 8 : m.X - 12, m.Y + 8, 20, 18, side > 0 ? -90 : 90, 180);
                    using (GraphicsPath mp = Brand.Round(m, 6)) { g.FillPath(Brushes.White, mp); g.DrawPath(pn, mp); }
                    using (SolidBrush b = new SolidBrush(Brand.Blue)) g.FillRectangle(b, m.X + 2.5f, m.Y + 14, m.Width - 5, 9);
                }
                else if (prop == "phone")
                {
                    RectangleF ph = new RectangleF(paw.X - 15, paw.Y - 50, 30, 52);
                    using (GraphicsPath pp = Brand.Round(ph, 6))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 44, 54))) { g.FillPath(b, pp); g.DrawPath(pn, pp); }
                    using (SolidBrush scr = new SolidBrush(Color.FromArgb(120, 190, 255))) g.FillRectangle(scr, ph.X + 4, ph.Y + 5, ph.Width - 8, ph.Height - 14);
                    using (SolidBrush bub = new SolidBrush(Color.White)) { g.FillRectangle(bub, ph.X + 7, ph.Y + 10, 14, 5); g.FillRectangle(bub, ph.X + 10, ph.Y + 20, 12, 5); }
                }
                else if (prop == "bottle")
                {
                    RectangleF b = new RectangleF(paw.X - 13, paw.Y - 66, 26, 74);
                    using (GraphicsPath bp = Brand.Round(b, 9))
                    using (SolidBrush fill = new SolidBrush(Color.FromArgb(150, 196, 236))) { g.FillPath(fill, bp); g.DrawPath(pn, bp); }
                    using (SolidBrush cap = new SolidBrush(Brand.Blue)) { g.FillRectangle(cap, b.X + 5, b.Y - 10, b.Width - 10, 12); g.DrawRectangle(pn, b.X + 5, b.Y - 10, b.Width - 10, 12); }
                    using (SolidBrush w = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillRectangle(w, b.X + 6, b.Y + 10, 5, b.Height - 24);
                }
            }
        }

        // ---------------------------------------------------- escritorio
        void DrawDesk(Graphics g, double t, bool mugInPaw)
        {
            using (Pen pen = new Pen(Ink, 1.8f))
            {
                pen.LineJoin = LineJoin.Round;

                // portátil (visto por detrás), frente al pecho de Frozzy
                RectangleF lid = new RectangleF(XDesk - 18, DeskTop - 21, 36, 21);
                using (GraphicsPath p = Brand.Round(lid, 3))
                using (LinearGradientBrush b = new LinearGradientBrush(lid, Color.FromArgb(222, 229, 238), Color.FromArgb(184, 196, 212), 90f))
                { g.FillPath(b, p); g.DrawPath(pen, p); }
                float lx = lid.X + lid.Width / 2, ly = lid.Y + lid.Height / 2 - 1;
                using (SolidBrush b = new SolidBrush(Brand.Blue)) g.FillEllipse(b, lx - 5, ly - 5, 10, 10);
                using (Pen w = new Pen(Color.White, 1.3f))
                    g.DrawLines(w, new PointF[] { new PointF(lx - 2.4f, ly - 2.2f), new PointF(lx + 2.4f, ly - 2.2f), new PointF(lx - 2.4f, ly + 2.2f), new PointF(lx + 2.4f, ly + 2.2f) });

                // taza con vapor (si no la tiene en la mano)
                float mx = XDesk + 24, my = DeskTop - 12;
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

                // planta pequeña
                float px = DeskLeft + 4, py = DeskTop - 9;
                using (SolidBrush leaf = new SolidBrush(Brand.Green))
                {
                    g.FillEllipse(leaf, px - 1, py - 9, 6, 10); g.FillEllipse(leaf, px + 4, py - 11, 6, 11); g.FillEllipse(leaf, px + 8, py - 8, 6, 9);
                }
                RectangleF pot = new RectangleF(px, py, 12, 9);
                using (SolidBrush b = new SolidBrush(Brand.Yellow)) { g.FillRectangle(b, pot); g.DrawRectangle(pen, pot.X, pot.Y, pot.Width, pot.Height); }

                // tablero del escritorio
                RectangleF slab = new RectangleF(DeskLeft - 3, DeskTop, DeskW + 6, 6);
                using (GraphicsPath p = Brand.Round(slab, 2.5f)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(244, 247, 251))) g.FillPath(b, p); g.DrawPath(pen, p); }
                RectangleF front = new RectangleF(DeskLeft + 2, DeskTop + 6, DeskW - 4, Ground - DeskTop - 6.9f);
                using (LinearGradientBrush b = new LinearGradientBrush(front, Brand.Blue, Brand.Navy, 90f)) g.FillRectangle(b, front);
                g.DrawRectangle(pen, front.X, front.Y, front.Width, front.Height);
                using (SolidBrush y = new SolidBrush(Brand.Yellow)) g.FillRectangle(y, front.X + 1, front.Y + 1, front.Width - 1, 2.5f);
                using (SolidBrush gr = new SolidBrush(Brand.Green)) g.FillRectangle(gr, front.X + 1, front.Y + 1, front.Width / 4, 2.5f);

                // logo FROZZ
                StringFormat cf = new StringFormat(); cf.Alignment = StringAlignment.Center;
                using (SolidBrush wb = new SolidBrush(Color.White))
                    g.DrawString("FROZZ", fLogo, wb, new RectangleF(front.X, front.Y + 4, front.Width, 14), cf);

                // reloj: cuánto falta para la próxima pausa
                RectangleF lcd = new RectangleF(front.X + front.Width / 2 - 25, front.Y + 18, 50, 13);
                using (GraphicsPath lp = Brand.Round(lcd, 2.5f))
                using (SolidBrush lb = new SolidBrush(Color.FromArgb(14, 26, 48)))
                using (Pen lpen = new Pen(Color.FromArgb(160, 255, 255, 255), 1)) { g.FillPath(lb, lp); g.DrawPath(lpen, lp); }
                string txt; Color tc = Color.FromArgb(120, 240, 140);
                if (nextMode == 2) { txt = "PAUSA"; tc = Brand.Yellow; }
                else if (nextMode == 3) { txt = "--:--"; tc = Color.FromArgb(150, 160, 180); }
                else if (nextMode == 1 || nextRemaining <= 0) { txt = (t % 1.0) < 0.6 ? "¡YA!" : ""; tc = Brand.Yellow; }
                else
                {
                    int s = (int)Math.Ceiling(nextRemaining);
                    txt = s >= 3600 ? (s / 3600) + "h" + ((s % 3600) / 60).ToString("00") : (s / 60) + ":" + (s % 60).ToString("00");
                }
                using (SolidBrush tb = new SolidBrush(tc))
                    g.DrawString(txt, fLcd, tb, new RectangleF(lcd.X, lcd.Y, lcd.Width, lcd.Height + 2), cf);

                // barra de progreso hacia la próxima pausa
                RectangleF bar = new RectangleF(front.X + 8, front.Bottom - 7, front.Width - 16, 3.2f);
                using (SolidBrush bb = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillRectangle(bb, bar);
                float fr = nextMode == 0 ? (float)Math.Max(0, Math.Min(1, nextFrac)) : (nextMode == 1 ? 1 : 0);
                using (SolidBrush fb = new SolidBrush(fr > 0.9f ? Brand.Yellow : Brand.Green)) g.FillRectangle(fb, bar.X, bar.Y, bar.Width * fr, bar.Height);
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
            if (disposing) { anim.Stop(); anim.Dispose(); fg.Dispose(); frame.Dispose(); head.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
