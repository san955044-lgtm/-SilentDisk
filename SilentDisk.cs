using System;
using System.Drawing;
using System.IO;
using System.Management;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SilentDisk
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SilentDiskContext());
        }
    }

    public class SilentDiskContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;
        private Timer checkTimer;
        private MenuItem startupMenuItem;
        private string logFilePath;

        public SilentDiskContext()
        {
            logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SilentDisk.log");

            // Setup Context Menu
            trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("Drive & Health Details", OnShowDetails);
            trayMenu.MenuItems.Add("Check Now", OnCheckNow);
            trayMenu.MenuItems.Add("-");
            trayMenu.MenuItems.Add("Open Disk Management", OnOpenDiskMgmt);
            trayMenu.MenuItems.Add("View Log", OnViewLog);
            trayMenu.MenuItems.Add("-");

            startupMenuItem = new MenuItem("Start with Windows", OnToggleStartup);
            startupMenuItem.Checked = IsRunOnStartupEnabled();
            trayMenu.MenuItems.Add(startupMenuItem);

            trayMenu.MenuItems.Add("-");
            trayMenu.MenuItems.Add("Exit", OnExit);

            // Setup Tray Icon
            trayIcon = new NotifyIcon();
            trayIcon.ContextMenu = trayMenu;
            trayIcon.Visible = true;
            trayIcon.Text = "SilentDisk 2.5 - Initializing...";
            trayIcon.Icon = CreateStatusIcon(Color.LimeGreen);
            trayIcon.DoubleClick += OnShowDetails;

            // Timer (checks every 30 minutes = 1800000 ms)
            checkTimer = new Timer();
            checkTimer.Interval = 30 * 60 * 1000;
            checkTimer.Tick += new EventHandler(OnTimerTick);
            checkTimer.Start();

            LogMessage("SilentDisk 2.5 started.");
            PerformHealthCheck(false);
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            PerformHealthCheck(false);
        }

        private void OnCheckNow(object sender, EventArgs e)
        {
            PerformHealthCheck(true);
        }

        private void OnShowDetails(object sender, EventArgs e)
        {
            ShowDetailsForm();
        }

        private void ShowDetailsForm()
        {
            Form form = new Form();
            form.Text = "SilentDisk - Drive & Health Details";
            form.Size = new Size(520, 420);
            form.StartPosition = FormStartPosition.CenterScreen;
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.MaximizeBox = false;
            form.MinimizeBox = false;
            form.ShowIcon = true;
            form.Icon = trayIcon.Icon;

            TextBox txt = new TextBox();
            txt.Multiline = true;
            txt.ReadOnly = true;
            txt.ScrollBars = ScrollBars.Vertical;
            txt.Dock = DockStyle.Fill;
            txt.Font = new Font("Consolas", 9.5f, FontStyle.Regular);
            txt.BackColor = Color.FromArgb(245, 245, 245);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("==================================================");
            sb.AppendLine("           PHYSICAL DISK HEALTH (SMART/WMI)        ");
            sb.AppendLine("==================================================");

            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Model, InterfaceType, Size, Status FROM Win32_DiskDrive");
                int index = 0;
                foreach (ManagementObject drive in searcher.Get())
                {
                    string model = drive["Model"] != null ? drive["Model"].ToString().Trim() : "Unknown";
                    string iface = drive["InterfaceType"] != null ? drive["InterfaceType"].ToString().Trim() : "Unknown";
                    string status = drive["Status"] != null ? drive["Status"].ToString().Trim() : "Unknown";
                    long sizeBytes = drive["Size"] != null ? Convert.ToInt64(drive["Size"]) : 0;
                    long sizeGB = sizeBytes / (1024 * 1024 * 1024);

                    sb.AppendLine(string.Format("Disk #{0}: {1}", index++, model));
                    sb.AppendLine(string.Format("  - Interface : {0}", iface));
                    sb.AppendLine(string.Format("  - Capacity  : {0} GB", sizeGB));
                    sb.AppendLine(string.Format("  - Status    : {0} {1}", status, (status.Equals("OK", StringComparison.InvariantCultureIgnoreCase) ? "[HEALTHY]" : "[WARNING/FAIL]")));
                    sb.AppendLine();
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("Error reading physical drives: " + ex.Message);
            }

            sb.AppendLine("==================================================");
            sb.AppendLine("          LOGICAL PARTITIONS & STORAGE USAGE      ");
            sb.AppendLine("==================================================");

            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                foreach (DriveInfo d in drives)
                {
                    if (d.IsReady && (d.DriveType == DriveType.Fixed))
                    {
                        double totalGB = (double)d.TotalSize / (1024 * 1024 * 1024);
                        double freeGB = (double)d.AvailableFreeSpace / (1024 * 1024 * 1024);
                        double usedGB = totalGB - freeGB;
                        double freePercent = (freeGB / totalGB) * 100.0;
                        double usedPercent = 100.0 - freePercent;

                        sb.AppendLine(string.Format("Partition {0} (Volume: {1})", d.Name, string.IsNullOrEmpty(d.VolumeLabel) ? "Local Disk" : d.VolumeLabel));
                        sb.AppendLine(string.Format("  - Total Space: {0:0.0} GB", totalGB));
                        sb.AppendLine(string.Format("  - Used Space : {0:0.0} GB ({1:0.0}%)", usedGB, usedPercent));
                        sb.AppendLine(string.Format("  - Free Space : {0:0.0} GB ({1:0.0}%)", freeGB, freePercent));

                        if (freePercent < 10.0 || freeGB < 5.0)
                        {
                            sb.AppendLine("  -> ALERT: LOW DISK SPACE!");
                        }
                        sb.AppendLine();
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("Error reading partitions: " + ex.Message);
            }

            txt.Text = sb.ToString();
            txt.Select(0, 0);

            Panel pnlBottom = new Panel();
            pnlBottom.Height = 45;
            pnlBottom.Dock = DockStyle.Bottom;

            Button btnClose = new Button();
            btnClose.Text = "Close";
            btnClose.Size = new Size(85, 30);
            btnClose.Location = new Point(form.ClientSize.Width - 100, 8);
            btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Click += delegate { form.Close(); };

            Button btnRefresh = new Button();
            btnRefresh.Text = "Refresh";
            btnRefresh.Size = new Size(85, 30);
            btnRefresh.Location = new Point(form.ClientSize.Width - 195, 8);
            btnRefresh.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnRefresh.Click += delegate {
                form.Close();
                ShowDetailsForm();
            };

            pnlBottom.Controls.Add(btnRefresh);
            pnlBottom.Controls.Add(btnClose);

            form.Controls.Add(txt);
            form.Controls.Add(pnlBottom);

            form.ShowDialog();
        }

        private void PerformHealthCheck(bool manualTrigger)
        {
            bool allHardwareOk = true;
            string hardwareReport = "";
            int driveCount = 0;

            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Model, Status FROM Win32_DiskDrive");
                foreach (ManagementObject drive in searcher.Get())
                {
                    driveCount++;
                    string model = drive["Model"] != null ? drive["Model"].ToString().Trim() : "Unknown Drive";
                    string status = drive["Status"] != null ? drive["Status"].ToString().Trim() : "Unknown";

                    hardwareReport += model + " [" + status + "]\n";

                    if (!status.Equals("OK", StringComparison.InvariantCultureIgnoreCase))
                    {
                        allHardwareOk = false;
                    }
                }
            }
            catch (Exception ex)
            {
                allHardwareOk = false;
                hardwareReport = "WMI Error: " + ex.Message;
                LogMessage("ERROR checking WMI: " + ex.Message);
            }

            bool spaceWarning = false;
            string spaceReport = "";
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                foreach (DriveInfo d in drives)
                {
                    if (d.IsReady && (d.DriveType == DriveType.Fixed))
                    {
                        double freePercent = ((double)d.AvailableFreeSpace / d.TotalSize) * 100.0;
                        long freeGB = d.AvailableFreeSpace / (1024 * 1024 * 1024);

                        if (freePercent < 10.0 || freeGB < 5)
                        {
                            spaceWarning = true;
                            spaceReport += d.Name + " has low space (" + freePercent.ToString("0.0") + "% free, " + freeGB + " GB)!\n";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage("ERROR checking partition space: " + ex.Message);
            }

            if (!allHardwareOk)
            {
                trayIcon.Icon = CreateStatusIcon(Color.Red);
                trayIcon.Text = "SilentDisk: Critical Hardware Alert!";
                trayIcon.ShowBalloonTip(4000, "SilentDisk Alert", "Drive hardware warning/error!\n" + hardwareReport, ToolTipIcon.Error);
                LogMessage("ALERT: Hardware failure -> " + hardwareReport.Replace("\n", " | "));
            }
            else if (spaceWarning)
            {
                trayIcon.Icon = CreateStatusIcon(Color.Orange);
                trayIcon.Text = "SilentDisk: Low Disk Space Warning";
                trayIcon.ShowBalloonTip(3000, "SilentDisk Warning", "Low free disk space:\n" + spaceReport, ToolTipIcon.Warning);
                LogMessage("WARNING: " + spaceReport.Replace("\n", " | "));
            }
            else
            {
                trayIcon.Icon = CreateStatusIcon(Color.LimeGreen);
                trayIcon.Text = "SilentDisk: All " + driveCount + " drive(s) healthy";
                LogMessage("Health check passed. " + driveCount + " drive(s) healthy.");

                if (manualTrigger)
                {
                    trayIcon.ShowBalloonTip(2000, "SilentDisk", "All drives healthy & sufficient space.\n" + hardwareReport, ToolTipIcon.Info);
                }
            }
        }

        private Icon CreateStatusIcon(Color color)
        {
            using (Bitmap bmp = new Bitmap(16, 16))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (Brush b = new SolidBrush(color))
                    {
                        g.FillEllipse(b, 2, 2, 12, 12);
                    }
                    using (Pen p = new Pen(Color.FromArgb(40, 40, 40), 1.5f))
                    {
                        g.DrawEllipse(p, 2, 2, 12, 12);
                    }
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        private void OnOpenDiskMgmt(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("diskmgmt.msc");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not launch Disk Management: " + ex.Message, "SilentDisk", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnViewLog(object sender, EventArgs e)
        {
            try
            {
                if (!File.Exists(logFilePath))
                {
                    File.WriteAllText(logFilePath, "=== SilentDisk Log Started ===\r\n");
                }
                System.Diagnostics.Process.Start("notepad.exe", logFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open log: " + ex.Message, "SilentDisk", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnToggleStartup(object sender, EventArgs e)
        {
            bool newState = !startupMenuItem.Checked;
            SetRunOnStartup(newState);
            startupMenuItem.Checked = newState;
        }

        private bool IsRunOnStartupEnabled()
        {
            try
            {
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return rk != null && rk.GetValue("SilentDisk") != null;
                }
            }
            catch { return false; }
        }

        private void SetRunOnStartup(bool enable)
        {
            try
            {
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (rk != null)
                    {
                        if (enable)
                            rk.SetValue("SilentDisk", Application.ExecutablePath);
                        else
                            rk.DeleteValue("SilentDisk", false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to change startup settings: " + ex.Message, "SilentDisk", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LogMessage(string msg)
        {
            try
            {
                string line = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + msg + Environment.NewLine;
                File.AppendAllText(logFilePath, line);
            }
            catch { }
        }

        private void OnExit(object sender, EventArgs e)
        {
            LogMessage("SilentDisk stopped by user.");
            trayIcon.Visible = false;
            checkTimer.Stop();
            Application.Exit();
        }
    }
}
