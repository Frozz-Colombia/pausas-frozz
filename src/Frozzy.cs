// Frozzy: mascota animada que vive en una esquina de la pantalla.
// Trabaja en su escritorio; cuando toca una pausa se pone de pie, salta y avisa
// hasta que le das clic. Ventana "layered" con transparencia por píxel: las zonas
// transparentes dejan pasar el mouse y nunca roba el foco.
using System;
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

        public enum St { Desk, Rising, Alert, Active, Cheer, Sitting }

        // ---- geometría en unidades lógicas (se escalan por DPI)
        public const float LW = 236, LH = 250;
        const float BH = 118;                       // alto de Frozzy de pie
        const float SRC_W = 264, SRC_H = 504;      // tamaño de frozzy.png
        const float K = BH / SRC_H;
        const float BW = SRC_W * K;
        const float Ground = LH - 4;
        const float DeskW = 80, DeskH = 40;
        const float DeskLeft = LW - 6 - DeskW;
        const float DeskTop = Ground - DeskH;
        const float XDesk = DeskLeft + DeskW / 2 + 2;   // centro de Frozzy sentado
        const float SitTop = DeskTop - 290 * K;          // pecho a la altura del escritorio
        const float XStand = DeskLeft - BW / 2 - 8;     // de pie, a la izquierda del escritorio
        const float StandTop = Ground - BH;
        const double RiseDur = 0.75;

        readonly float sc;
        readonly int DW, DH;
        readonly Bitmap bear, frame;
        readonly Graphics fg;
        readonly System.Windows.Forms.Timer anim;
        readonly DateTime t0 = DateTime.Now;
        readonly Font f1, f2, fBig;

        St st = St.Desk, riseTarget = St.Alert;
        DateTime stateStart = DateTime.Now;
        string callText;
        string sayL1, sayL2; DateTime sayUntil = DateTime.MinValue; bool sayBig;
        string bubbleKey; DateTime bubbleStart;
        bool hover, down, dragged, busyHidden, userHidden;
        Point downPt, downLoc;

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

            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("frozzy.png"))
            using (Bitmap src = new Bitmap(s))
            {
                int bh = (int)Math.Round(BH * sc), bw = (int)Math.Round(bh * src.Width / (float)src.Height);
                bear = new Bitmap(bw, bh, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(bear))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(src, 0, 0, bw, bh);
                }
            }
            frame = new Bitmap(DW, DH, PixelFormat.Format32bppArgb);
            fg = Graphics.FromImage(frame);
            f1 = new Font("Segoe UI", 12.5f, FontStyle.Bold, GraphicsUnit.Pixel);
            f2 = new Font("Segoe UI", 10.5f, FontStyle.Regular, GraphicsUnit.Pixel);
            fBig = new Font("Segoe UI Black", 19f, FontStyle.Bold, GraphicsUnit.Pixel);

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
                return new Rectangle(Left + (int)((XStand - BW / 2) * sc), Top + (int)((StandTop - 30) * sc),
                    (int)((LW - (XStand - BW / 2)) * sc), (int)((BH + 30) * sc));
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Render();
            anim.Start();
        }

        // ------------------------------------------------------- API
        public void Alert(string call) { callText = call; StandUp(St.Alert); }
        public void Accompany() { StandUp(St.Active); Say("¡Vamos, yo te acompaño!", null, 3, false); }
        public void Cheer() { StandUp(St.Cheer); Say("¡Firme!", "¡Bien hecho!", 3.2, true); }

        public void SitDown()
        {
            callText = null;
            if (st == St.Desk || st == St.Sitting) return;
            if (st == St.Rising) { double el = (DateTime.Now - stateStart).TotalSeconds; Set(St.Sitting); stateStart = DateTime.Now.AddSeconds(-(RiseDur - el)); return; }
            Set(St.Sitting);
        }

        public void Say(string l1, string l2, double seconds, bool big)
        {
            sayL1 = l1; sayL2 = l2; sayBig = big; sayUntil = DateTime.Now.AddSeconds(seconds);
        }

        public void SetBusyHidden(bool v) { busyHidden = v; UpdateVisibility(); }
        public void SetUserHidden(bool v) { userHidden = v; UpdateVisibility(); }

        void UpdateVisibility()
        {
            bool show = !busyHidden && !userHidden;
            if (show && !Visible) { Show(); Render(); anim.Start(); }
            else if (!show && Visible) { anim.Stop(); Hide(); }
        }

        void StandUp(St target)
        {
            if (st == St.Desk || st == St.Sitting)
            {
                // si venía sentándose, se levanta desde donde iba
                double back = st == St.Sitting ? Math.Max(0, RiseDur - (DateTime.Now - stateStart).TotalSeconds) : 0;
                riseTarget = target; Set(St.Rising);
                stateStart = DateTime.Now.AddSeconds(-back);
            }
            else if (st == St.Rising) riseTarget = target;
            else Set(target);
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

        // ----------------------------------------------------- dibujo
        static float Ease(double a) { a = Math.Max(0, Math.Min(1, a)); return (float)(1 - Math.Pow(1 - a, 3)); }
        static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        void Pose(double p, out float cx, out float top)
        {
            const float riseH = 24;
            p = Math.Max(0, Math.Min(1, p));
            if (p < 0.45) { cx = XDesk; top = SitTop - riseH * Ease(p / 0.45); }
            else
            {
                double q = (p - 0.45) / 0.55;
                float e = Ease(q);
                cx = Lerp(XDesk, XStand, e);
                top = Lerp(SitTop - riseH, StandTop, e) - (float)Math.Sin(Math.PI * q) * 16;
            }
        }

        void Hop(double el, double height, out float dy, out float rot, out float sx, out float sy)
        {
            const double T = 0.62;
            dy = 0; rot = 0; sx = 1; sy = 1;
            bool calm = st == St.Alert && el > 45;       // si lo ignoran, salta en ráfagas
            double cyc = calm ? 3 * T + 3.0 : T;
            double ph = el % cyc;
            if (calm && ph >= 3 * T)
            {
                rot = (float)Math.Sin((ph - 3 * T) * Math.PI * 2 / 1.5) * 4;
                return;
            }
            int n = (int)((calm ? ph : el) / T);
            double p = (ph % T) / T;
            if (p < 0.75)
            {
                double a = p / 0.75;
                dy = -(float)(height * Math.Sin(Math.PI * a));
                rot = (n % 2 == 0 ? 1 : -1) * 6 * (float)Math.Sin(Math.PI * a);
            }
            else
            {
                double q = (p - 0.75) / 0.25;
                sy = 1 - 0.1f * (float)Math.Sin(Math.PI * q);
                sx = 1 + 0.07f * (float)Math.Sin(Math.PI * q);
            }
        }

        void Render()
        {
            if (!IsHandleCreated) return;
            DateTime now = DateTime.Now;
            double t = (now - t0).TotalSeconds;
            double el = (now - stateStart).TotalSeconds;
            if (st == St.Rising && el >= RiseDur) { Set(riseTarget); el = 0; }
            else if (st == St.Sitting && el >= RiseDur) { Set(St.Desk); el = 0; }
            else if (st == St.Cheer && el >= 3.2) { SitDown(); el = 0; }

            float cx = XDesk, top = SitTop, rot = 0, sx = 1, sy = 1, dy = 0;
            switch (st)
            {
                case St.Desk:
                    top = SitTop + (float)Math.Sin(t * Math.PI * 2 / 2.2) * 0.8f;
                    rot = (float)Math.Sin(t * Math.PI * 2 / 3.3) * 1.2f;
                    break;
                case St.Rising: Pose(el / RiseDur, out cx, out top); break;
                case St.Sitting: Pose(1 - el / RiseDur, out cx, out top); break;
                case St.Alert:
                    cx = XStand; top = StandTop;
                    Hop(el, 24, out dy, out rot, out sx, out sy);
                    break;
                case St.Active:
                    cx = XStand; top = StandTop;
                    rot = (float)Math.Sin(t * Math.PI * 2 / 1.6) * 3;
                    break;
                case St.Cheer:
                    cx = XStand; top = StandTop;
                    if (el < 1.24) Hop(el, 14, out dy, out rot, out sx, out sy);
                    break;
            }
            top += dy;

            Graphics g = fg;
            g.ResetTransform(); g.ResetClip();
            g.Clear(Color.Transparent);
            g.ScaleTransform(sc, sc);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            // Frozzy (siempre detrás del escritorio; recortado al piso)
            GraphicsState gs = g.Save();
            g.SetClip(new RectangleF(0, 0, LW, Ground));
            g.TranslateTransform(cx, top + BH);
            g.RotateTransform(rot);
            g.ScaleTransform(sx, sy);
            g.DrawImage(bear, new RectangleF(-BW / 2, -BH, BW, BH));
            g.Restore(gs);

            DrawDesk(g, t);

            // globo
            string l1 = null, l2 = null; bool big = false;
            if (now < sayUntil) { l1 = sayL1; l2 = sayL2; big = sayBig; }
            else if (st == St.Alert && callText != null) { l1 = callText; l2 = "Haz clic en mí"; }
            else if (st == St.Desk && hover && HoverText != null) { l1 = HoverText(); l2 = "Arrástrame para moverme"; }
            if (l1 != null)
            {
                string key = l1 + "|" + l2;
                if (key != bubbleKey) { bubbleKey = key; bubbleStart = now; }
                double be = (now - bubbleStart).TotalSeconds;
                float s = 0.6f + 0.4f * Ease(be / 0.2);
                if (st == St.Alert && now >= sayUntil) s *= 1 + 0.03f * (float)Math.Sin(t * Math.PI * 2 / 0.9);
                DrawBubble(g, l1, l2, big, cx, top - 2, s);
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

            bool fast = st != St.Desk || hover || l1 != null;
            int iv = fast ? 33 : 66;
            if (anim.Interval != iv) anim.Interval = iv;
        }

        void DrawDesk(Graphics g, double t)
        {
            Color ol = Color.FromArgb(27, 35, 51);
            using (Pen pen = new Pen(ol, 1.8f))
            {
                pen.LineJoin = LineJoin.Round;

                // portátil (visto por detrás)
                RectangleF lid = new RectangleF(XDesk - 31, DeskTop - 25, 34, 25);
                using (GraphicsPath p = Brand.Round(lid, 3))
                using (LinearGradientBrush b = new LinearGradientBrush(lid, Color.FromArgb(222, 229, 238), Color.FromArgb(184, 196, 212), 90f))
                { g.FillPath(b, p); g.DrawPath(pen, p); }
                float lx = lid.X + lid.Width / 2, ly = lid.Y + lid.Height / 2 - 1;
                using (SolidBrush b = new SolidBrush(Brand.Blue)) g.FillEllipse(b, lx - 5, ly - 5, 10, 10);
                using (Pen w = new Pen(Color.White, 1.3f))
                    g.DrawLines(w, new PointF[] { new PointF(lx - 2.4f, ly - 2.2f), new PointF(lx + 2.4f, ly - 2.2f), new PointF(lx - 2.4f, ly + 2.2f), new PointF(lx + 2.4f, ly + 2.2f) });

                // taza con vapor
                float mx = XDesk + 15, my = DeskTop - 12;
                using (Pen hp = new Pen(ol, 1.6f)) g.DrawArc(hp, mx + 7, my + 2.5f, 6, 6, -90, 180);
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

                // escritorio
                RectangleF slab = new RectangleF(DeskLeft - 3, DeskTop, DeskW + 6, 6);
                using (GraphicsPath p = Brand.Round(slab, 2.5f)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(244, 247, 251))) g.FillPath(b, p); g.DrawPath(pen, p); }
                RectangleF front = new RectangleF(DeskLeft + 2, DeskTop + 6, DeskW - 4, Ground - DeskTop - 6.9f);
                using (LinearGradientBrush b = new LinearGradientBrush(front, Brand.Blue, Brand.Navy, 90f)) g.FillRectangle(b, front);
                g.DrawRectangle(pen, front.X, front.Y, front.Width, front.Height);
                using (SolidBrush y = new SolidBrush(Brand.Yellow)) g.FillRectangle(y, front.X + 1, front.Y + 1, front.Width - 1, 2.5f);
                using (SolidBrush gr = new SolidBrush(Brand.Green)) g.FillRectangle(gr, front.X + 1, front.Y + 1, front.Width / 4, 2.5f);
                using (Pen w = new Pen(Color.FromArgb(120, 255, 255, 255), 1.2f))
                {
                    RectangleF dr = new RectangleF(front.X + front.Width / 2 - 13, front.Y + 11, 26, 13);
                    g.DrawRectangle(w, dr.X, dr.Y, dr.Width, dr.Height);
                    g.DrawLine(w, dr.X + 9, dr.Y + 6.5f, dr.Right - 9, dr.Y + 6.5f);
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
            float bx = Math.Max(4, Math.Min(LW - 4 - w, tx - w * 0.55f));
            float by = Math.Max(2, ty - 10 - h);
            float tailX = Math.Max(bx + 14, Math.Min(bx + w - 14, tx));

            GraphicsState gs = g.Save();
            g.TranslateTransform(tx, ty); g.ScaleTransform(s, s); g.TranslateTransform(-tx, -ty);
            RectangleF r = new RectangleF(bx, by, w, h);
            PointF[] tail = { new PointF(tailX - 7, r.Bottom - 2), new PointF(tailX + 6, r.Bottom - 2), new PointF(tx + 1, ty) };
            using (GraphicsPath p = Brand.Round(r, 10))
            using (Pen pen = new Pen(Color.FromArgb(27, 35, 51), 3.6f))
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
            if (disposing) { anim.Stop(); anim.Dispose(); fg.Dispose(); frame.Dispose(); bear.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
