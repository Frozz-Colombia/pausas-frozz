// Herramienta de desarrollo: corta src/frozzy.png (diseño original) en piezas para animar
// a Frozzy como marioneta de recortes. Cada pieza se guarda en un lienzo del mismo tamaño
// que el original (264x504) para que todas compartan coordenadas.
// Uso: piezas.exe <frozzy.png> <carpeta_salida>
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

static class Piezas
{
    static Bitmap src;
    static readonly Color Ink = Color.FromArgb(10, 10, 12);

    static PointF[] P(params float[] v)
    {
        PointF[] r = new PointF[v.Length / 2];
        for (int i = 0; i < r.Length; i++) r[i] = new PointF(v[2 * i], v[2 * i + 1]);
        return r;
    }

    // copia los píxeles del original que caen dentro del polígono
    static Bitmap Cut(PointF[] poly)
    {
        Bitmap b = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b))
        using (GraphicsPath gp = new GraphicsPath())
        {
            gp.AddPolygon(poly);
            g.SetClip(gp);
            g.DrawImage(src, 0, 0, src.Width, src.Height);
        }
        return b;
    }

    static void Save(Bitmap b, string dir, string name) { b.Save(Path.Combine(dir, name), ImageFormat.Png); }

    // Extrae el logo FROZZ COLOMBIA del instructivo SST quitando el fondo azul oscuro
    static void Logo(string jpg, string outPng)
    {
        using (Bitmap s = new Bitmap(jpg))
        {
            Rectangle r = new Rectangle(1198, 920, 240, 84);
            Color bg = Color.FromArgb(1, 41, 101);
            Bitmap o = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
            for (int y = 0; y < r.Height; y++)
                for (int x = 0; x < r.Width; x++)
                {
                    Color c = s.GetPixel(r.X + x, r.Y + y);
                    int d = Math.Max(Math.Abs(c.R - bg.R), Math.Max(Math.Abs(c.G - bg.G), Math.Abs(c.B - bg.B)));
                    double al = Math.Max(0, Math.Min(1, (d - 22) / 120.0));
                    if (al <= 0) { o.SetPixel(x, y, Color.Transparent); continue; }
                    Func<int, int, int> un = (v, b0) => (int)Math.Max(0, Math.Min(255, (v - (1 - al) * b0) / al));
                    o.SetPixel(x, y, Color.FromArgb((int)(al * 255), un(c.R, bg.R), un(c.G, bg.G), un(c.B, bg.B)));
                }
            o.Save(outPng, ImageFormat.Png);
        }
    }

    static void Main(string[] a)
    {
        if (a[0] == "logo") { Logo(a[1], a[2]); Console.WriteLine("logo listo"); return; }
        src = new Bitmap(a[0]);
        string dir = a[1];
        Directory.CreateDirectory(dir);

        // curva del corte bajo la barbilla (cabeza / cuerpo)
        float[] chin = { 0, 196, 45, 198, 80, 211, 120, 221, 160, 225, 200, 215, 235, 195, 264, 188 };

        // ---- cabeza
        Save(Cut(P(0, 0, 264, 0, 264, 188, 235, 195, 200, 215, 160, 225, 120, 221, 80, 211, 45, 198, 0, 196)), dir, "p_cabeza.png");

        // ---- torso + correa + parte alta del jean (sin brazos)
        PointF[] torso = P(78, 212, 80, 211, 120, 221, 160, 225, 200, 215, 192, 232, 200, 260, 207, 290, 213, 320, 217, 347,
                           221, 374, 224, 412, 36, 412, 40, 374, 44, 347, 47, 320, 53, 290, 60, 260, 68, 232);
        Bitmap t = Cut(torso);
        using (Graphics g = Graphics.FromImage(t))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath gpt = new GraphicsPath()) { gpt.AddPolygon(torso); g.SetClip(gpt); }
            Brush white = Brushes.White;
            using (SolidBrush belt = new SolidBrush(Color.FromArgb(73, 82, 94)))
            using (SolidBrush jean = new SolidBrush(Color.FromArgb(23, 97, 173)))
            using (Pen ink = new Pen(Ink, 5f))
            using (Pen ink4 = new Pen(Ink, 4f))
            {
                ink.LineJoin = LineJoin.Round; ink.StartCap = LineCap.Round; ink.EndCap = LineCap.Round;
                // lado derecho: quitar puño y pata, reconstruir camisa, correa y jean
                g.FillPolygon(white, P(194, 300, 216, 300, 219, 346, 194, 346));
                g.FillRectangle(belt, 188, 348, 36, 17);
                g.DrawLine(ink4, 188, 347, 222, 347);
                g.DrawLine(ink4, 186, 365.5f, 223, 365.5f);
                g.FillPolygon(jean, P(186, 367.5f, 222, 367.5f, 225, 414, 186, 414));
                g.DrawLines(ink, P(207, 290, 213, 320, 218, 347, 220, 366, 223, 412));
                // lado izquierdo: quitar el brazo del pulgar, reconstruir camisa
                g.FillPolygon(white, P(70, 226, 76, 244, 106, 244, 106, 347, 42, 347, 45, 320, 51, 290, 58, 260, 66, 236));
                g.DrawLines(ink, P(69, 232, 60, 260, 53, 290, 47, 320, 43, 347));
            }
        }
        Save(t, dir, "p_torso.png");

        // ---- manga derecha (hombro a puño) y pata derecha
        // La manga termina redondeada en la articulación del hombro (210,250): lo que queda
        // "detrás" de la articulación lo dibuja la máscara del hombro en el programa.
        Bitmap manga = Cut(P(189, 217, 200, 215, 235, 195, 264, 188, 264, 350, 205, 350, 211, 320, 204, 290, 197, 260, 189, 232));
        {
            float px = 210, py = 250, dx = 23, dy = 88, len = (float)Math.Sqrt(dx * dx + dy * dy), R = 34;
            dx /= len; dy /= len;
            for (int y = 0; y < manga.Height; y++)
                for (int x = 0; x < manga.Width; x++)
                {
                    float vx = x - px, vy = y - py;
                    float dot = vx * dx + vy * dy;
                    if (dot < 0 && vx * vx + vy * vy > R * R && manga.GetPixel(x, y).A > 0) manga.SetPixel(x, y, Color.Transparent);
                }
        }
        Save(manga, dir, "p_manga.png");
        Save(Cut(P(203, 338, 262, 336, 262, 400, 250, 412, 225, 414, 213, 401, 207, 376, 202, 350)), dir, "p_pata.png");

        // ---- brazo original con el pulgar arriba (pose de celebración)
        Save(Cut(P(0, 196, 45, 198, 80, 211, 106, 219, 106, 350, 0, 350)), dir, "p_pulgar.png");

        // ---- piernas (desde la entrepierna hacia abajo, con zapato)
        Save(Cut(P(30, 400, 129, 400, 129, 504, 30, 504)), dir, "p_pierna_izq.png");
        Bitmap pd = Cut(P(129, 400, 240, 400, 240, 504, 129, 504));
        using (Graphics g = Graphics.FromImage(pd))
        {
            // quitar los restos de la pata original y rehacer el borde del jean
            g.CompositingMode = CompositingMode.SourceCopy;
            using (SolidBrush clear = new SolidBrush(Color.Transparent)) g.FillPolygon(clear, P(221, 398, 264, 398, 264, 419, 224, 419));
            g.CompositingMode = CompositingMode.SourceOver;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush jean = new SolidBrush(Color.FromArgb(23, 97, 173))) g.FillPolygon(jean, P(204, 398, 222, 398, 225, 419, 204, 419));
            using (Pen ink = new Pen(Ink, 5f)) g.DrawLine(ink, 222, 398, 225.5f, 420);
        }
        Save(pd, dir, "p_pierna_der.png");

        // ---- vista previa del ensamblado en reposo
        Bitmap prev = new Bitmap(src.Width * 2 + 20, src.Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(prev))
        {
            g.Clear(Color.FromArgb(120, 140, 170));
            string[] order = { "p_pierna_izq.png", "p_pierna_der.png", "p_pata.png", "p_manga.png", "p_torso.png", "p_cabeza.png" };
            foreach (string n in order) using (Bitmap b = new Bitmap(Path.Combine(dir, n))) g.DrawImage(b, 0, 0, b.Width, b.Height);
            // espejo del brazo derecho como brazo izquierdo
            g.TranslateTransform(src.Width + 20 + 260, 0); g.ScaleTransform(-1, 1);
            using (Bitmap b = new Bitmap(Path.Combine(dir, "p_pata.png"))) g.DrawImage(b, 0, 0, b.Width, b.Height);
            using (Bitmap b = new Bitmap(Path.Combine(dir, "p_manga.png"))) g.DrawImage(b, 0, 0, b.Width, b.Height);
            g.ResetTransform(); g.TranslateTransform(src.Width + 20, 0);
            foreach (string n in new string[] { "p_pierna_izq.png", "p_pierna_der.png", "p_pata.png", "p_manga.png", "p_torso.png", "p_cabeza.png" })
                using (Bitmap b = new Bitmap(Path.Combine(dir, n))) g.DrawImage(b, 0, 0, b.Width, b.Height);
        }
        prev.Save(Path.Combine(dir, "preview.png"), ImageFormat.Png);
        Console.WriteLine("piezas listas");
    }
}
