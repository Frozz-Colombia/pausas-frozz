// Instalador integrado: el mismo PausasFrozz.exe se instala solo cuando se ejecuta
// desde cualquier carpeta distinta a %LOCALAPPDATA%\PausasFrozz (p. ej. Descargas).
// Instala solo para el usuario actual (sin permisos de administrador): copia el .exe,
// crea el acceso directo del menú Inicio y lo registra en "Aplicaciones instaladas".
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PausasFrozz
{
    static class Instalacion
    {
        const string UninstKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\PausasFrozz";

        public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PausasFrozz"); } }
        public static string Exe { get { return Path.Combine(Dir, "PausasFrozz.exe"); } }
        static string Shortcut { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Pausas Frozz.lnk"); } }

        public static bool RunningInstalled
        {
            get { return string.Equals(Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(Exe), StringComparison.OrdinalIgnoreCase); }
        }

        public static string InstalledVersion
        {
            get
            {
                try { return File.Exists(Exe) ? FileVersionInfo.GetVersionInfo(Exe).FileVersion : null; }
                catch { return null; }
            }
        }

        static void StopOthers()
        {
            int me = Process.GetCurrentProcess().Id;
            foreach (Process p in Process.GetProcessesByName("PausasFrozz"))
            {
                if (p.Id == me) continue;
                try { p.Kill(); p.WaitForExit(5000); } catch { }
            }
        }

        public static void Install()
        {
            StopOthers();
            Directory.CreateDirectory(Dir);
            string tmp = Exe + ".new";
            File.Copy(Application.ExecutablePath, tmp, true);
            if (File.Exists(Exe)) File.Delete(Exe);
            File.Move(tmp, Exe);
            CreateShortcut();
            Register();
            Inicio.ApplyPath(true, Exe);
        }

        public static void Register()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstKey))
                {
                    k.SetValue("DisplayName", "Pausas Frozz");
                    k.SetValue("DisplayVersion", AppInfo.Version);
                    k.SetValue("Publisher", "Frozz Colombia");
                    k.SetValue("DisplayIcon", Exe);
                    k.SetValue("InstallLocation", Dir);
                    k.SetValue("UninstallString", "\"" + Exe + "\" --uninstall");
                    k.SetValue("URLInfoAbout", "https://github.com/" + AppInfo.Repo);
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    try { k.SetValue("EstimatedSize", (int)(new FileInfo(Exe).Length / 1024), RegistryValueKind.DWord); } catch { }
                }
            }
            catch { }
        }

        static void CreateShortcut()
        {
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                object sh = Activator.CreateInstance(t);
                object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { Shortcut });
                Type lt = lnk.GetType();
                lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { Exe });
                lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { Dir });
                lt.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { "Pausas activas, descanso visual e hidratación - SST Frozz" });
                lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
            }
            catch { }
        }

        public static void Uninstall()
        {
            if (MessageBox.Show("¿Desinstalar Pausas Frozz?\n\nTu historial de pausas se conserva en:\n" + Config.Dir,
                    "Pausas Frozz", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            StopOthers();
            Inicio.ApplyPath(false, Exe);
            try { if (File.Exists(Shortcut)) File.Delete(Shortcut); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstKey, false); } catch { }
            // el .exe en uso no se puede borrar a sí mismo: se borra la carpeta un instante después
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"" + Dir + "\"");
                psi.CreateNoWindow = true; psi.UseShellExecute = false; psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            catch { }
            MessageBox.Show("Pausas Frozz se desinstaló. ¡Gracias por cuidarte!", "Pausas Frozz", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    class InstallerForm : Form
    {
        Bitmap bear;
        Label lblMsg;
        Button btnOk, btnCancel;
        bool done;
        readonly Font fTitle = new Font("Segoe UI Semibold", 17f);
        readonly Font fSub = new Font("Segoe UI", 9f);

        public InstallerForm(Icon icon)
        {
            Text = "Instalar Pausas Frozz";
            Icon = icon;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.White; Font = new Font("Segoe UI", 10f);
            DoubleBuffered = true;

            int W = Brand.S(580), H = Brand.S(400);
            ClientSize = new Size(W, H);
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("frozzy.png"))
                using (Bitmap src = new Bitmap(s))
                {
                    int bh = Brand.S(230), bw = bh * src.Width / src.Height;
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

            int x = Brand.S(190), cw = W - x - Brand.S(28);
            string inst = Instalacion.InstalledVersion;
            string intro =
                "Frozzy te recordará descansar la vista, tomar agua, estirarte y hacer pausas activas, " +
                "según el instructivo de Ergonomía de SST.\n\n" +
                "• Se instala solo para tu usuario, sin permisos de administrador.\n" +
                "• Arranca con Windows y se actualiza solo.\n" +
                "• Lo puedes desinstalar desde Configuración › Aplicaciones.";
            if (inst != null) intro += "\n\nYa tienes instalada la versión " + inst + "; se reemplazará por la " + AppInfo.Version + ".";
            lblMsg = new Label();
            lblMsg.Text = intro; lblMsg.ForeColor = Brand.Text;
            lblMsg.Location = new Point(x, Brand.S(86)); lblMsg.Size = new Size(cw, H - Brand.S(86) - Brand.S(76));
            Controls.Add(lblMsg);

            btnOk = new Button();
            btnOk.Text = inst != null ? "Actualizar" : "Instalar";
            btnOk.Font = new Font("Segoe UI Semibold", 10.5f);
            btnOk.Size = new Size(Brand.S(130), Brand.S(38));
            btnOk.Location = new Point(W - Brand.S(28) - btnOk.Width, H - Brand.S(62));
            btnOk.BackColor = Brand.Navy; btnOk.ForeColor = Color.White;
            btnOk.FlatStyle = FlatStyle.Flat; btnOk.FlatAppearance.BorderSize = 0;
            btnOk.FlatAppearance.MouseOverBackColor = Brand.Blue; btnOk.Cursor = Cursors.Hand;
            btnOk.Click += delegate { OnOk(); };
            Controls.Add(btnOk);

            btnCancel = new Button();
            btnCancel.Text = "Cancelar";
            btnCancel.Size = new Size(Brand.S(110), Brand.S(38));
            btnCancel.Location = new Point(btnOk.Left - Brand.S(10) - btnCancel.Width, btnOk.Top);
            btnCancel.FlatStyle = FlatStyle.Flat; btnCancel.FlatAppearance.BorderColor = Brand.Line;
            btnCancel.ForeColor = Brand.Navy; btnCancel.BackColor = Color.White; btnCancel.Cursor = Cursors.Hand;
            btnCancel.Click += delegate { Close(); };
            Controls.Add(btnCancel);

            AcceptButton = btnOk; CancelButton = btnCancel;
        }

        void OnOk()
        {
            if (done) { Close(); return; }
            btnOk.Enabled = false; btnCancel.Enabled = false;
            UseWaitCursor = true;
            try
            {
                Instalacion.Install();
                Process.Start(Instalacion.Exe);
            }
            catch (Exception ex)
            {
                UseWaitCursor = false;
                MessageBox.Show(this, "No se pudo instalar Pausas Frozz.\n\n" + ex.Message, "Pausas Frozz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                btnOk.Enabled = true; btnCancel.Enabled = true;
                return;
            }
            UseWaitCursor = false;
            done = true;
            lblMsg.Text = "¡Listo! Pausas Frozz quedó instalado.\n\n" +
                "Busca a Frozzy en la esquina inferior derecha de tu pantalla, trabajando en su escritorio. " +
                "Cuando sea hora de una pausa se pondrá de pie para avisarte.\n\n" +
                "Ya puedes borrar este instalador.";
            btnOk.Text = "Cerrar"; btnOk.Enabled = true;
            btnCancel.Visible = false;
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
            int x = Brand.S(190);
            TextRenderer.DrawText(g, "Pausas Frozz", fTitle, new Point(x, Brand.S(24)), Brand.Navy, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "Versión " + AppInfo.Version + "  ·  SST Frozz Colombia", fSub, new Point(x + Brand.S(2), Brand.S(60)), Brand.Muted, TextFormatFlags.NoPadding);
            int sb = Brand.S(6);
            using (SolidBrush y = new SolidBrush(Brand.Yellow)) g.FillRectangle(y, 0, H - sb, W, sb);
            using (SolidBrush gr = new SolidBrush(Brand.Green)) g.FillRectangle(gr, 0, H - sb, W / 4, sb);
        }
    }
}
