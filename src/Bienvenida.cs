// Bienvenida: Frozzy se presenta y pregunta cómo llamar a la persona (opcional).
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace PausasFrozz
{
    class WelcomeForm : Form
    {
        readonly Config cfg;
        readonly TextBox txtName;
        readonly CheckBox chkName, chkVoice;
        Bitmap bear;
        readonly Font fTitle = new Font("Segoe UI Semibold", 17f);

        public WelcomeForm(Config c, Icon icon)
        {
            cfg = c;
            Text = "¡Hola! Soy Frozzy";
            Icon = icon;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.White; Font = new Font("Segoe UI", 10f);
            DoubleBuffered = true; TopMost = true;

            int W = Brand.S(580), H = Brand.S(390);
            ClientSize = new Size(W, H);
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("frozzy.png"))
                using (Bitmap src = new Bitmap(s))
                {
                    int bh = Brand.S(240), bw = bh * src.Width / src.Height;
                    bear = new Bitmap(bw, bh);
                    using (Graphics g = Graphics.FromImage(bear))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(src, 0, 0, bw, bh);
                    }
                }
            }
            catch { }

            int x = Brand.S(190), cw = W - x - Brand.S(28), y = Brand.S(72);
            Label intro = new Label();
            intro.Text = "Voy a estar en la esquina de tu pantalla trabajando en mi escritorio. " +
                         "Cuando sea hora de descansar la vista, tomar agua o estirarte, me pongo de pie y te aviso.\n\n" +
                         "¿Cómo te llamas?";
            intro.ForeColor = Brand.Text; intro.Location = new Point(x, y); intro.Size = new Size(cw, Brand.S(100));
            Controls.Add(intro);
            y += Brand.S(104);

            txtName = new TextBox();
            txtName.Location = new Point(x, y); txtName.Width = Brand.S(240); txtName.MaxLength = 30;
            txtName.Text = cfg.UserName ?? "";
            Controls.Add(txtName);
            y += Brand.S(36);

            chkName = new CheckBox();
            chkName.Text = "Sí, quiero que Frozzy me llame por mi nombre";
            chkName.Checked = cfg.UseName;
            chkName.Location = new Point(x, y); chkName.Size = new Size(cw, Brand.S(26));
            Controls.Add(chkName);
            txtName.TextChanged += delegate { if (txtName.Text.Trim().Length > 0 && !chkName.Checked) chkName.Checked = true; if (txtName.Text.Trim().Length == 0) chkName.Checked = false; };
            y += Brand.S(28);

            chkVoice = new CheckBox();
            bool voz = Sonido.VozDisponible;
            chkVoice.Text = voz ? "Leer en voz alta los pasos de cada pausa" : "Leer los pasos en voz alta (no hay voz en español instalada)";
            chkVoice.Enabled = voz; chkVoice.Checked = voz && cfg.Voice;
            chkVoice.Location = new Point(x, y); chkVoice.Size = new Size(cw, Brand.S(26));
            Controls.Add(chkVoice);
            y += Brand.S(30);

            Label foot = new Label();
            foot.Text = "Puedes cambiar esto cuando quieras en Configuración.";
            foot.ForeColor = Brand.Muted; foot.Font = new Font("Segoe UI", 8.5f);
            foot.Location = new Point(x, y); foot.Size = new Size(cw, Brand.S(22));
            Controls.Add(foot);

            Button ok = new Button();
            ok.Text = "¡Empecemos!";
            ok.Font = new Font("Segoe UI Semibold", 10.5f);
            ok.Size = new Size(Brand.S(150), Brand.S(38));
            ok.Location = new Point(W - Brand.S(28) - ok.Width, H - Brand.S(60));
            ok.BackColor = Brand.Navy; ok.ForeColor = Color.White;
            ok.FlatStyle = FlatStyle.Flat; ok.FlatAppearance.BorderSize = 0;
            ok.FlatAppearance.MouseOverBackColor = Brand.Blue; ok.Cursor = Cursors.Hand;
            ok.Click += delegate { Save(); Close(); };
            Controls.Add(ok);
            AcceptButton = ok;
        }

        void Save()
        {
            cfg.UserName = txtName.Text.Trim();
            cfg.UseName = chkName.Checked && cfg.UserName.Length > 0;
            cfg.Voice = chkVoice.Checked;
            cfg.WelcomeDone = true;
            cfg.Save();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int W = ClientSize.Width, H = ClientSize.Height;
            Rectangle left = new Rectangle(0, 0, Brand.S(165), H);
            using (LinearGradientBrush lb = new LinearGradientBrush(left, Brand.Blue, Brand.Navy, 90f)) g.FillRectangle(lb, left);
            if (bear != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(bear, (left.Width - bear.Width) / 2 + Brand.S(4), H - bear.Height - Brand.S(30), bear.Width, bear.Height);
            }
            TextRenderer.DrawText(g, "¡Hola! Soy Frozzy", fTitle, new Point(Brand.S(190), Brand.S(24)), Brand.Navy, TextFormatFlags.NoPadding);
            int sb = Brand.S(6);
            using (SolidBrush y = new SolidBrush(Brand.Yellow)) g.FillRectangle(y, 0, H - sb, W, sb);
            using (SolidBrush gr = new SolidBrush(Brand.Green)) g.FillRectangle(gr, 0, H - sb, W / 4, sb);
        }
    }
}
