using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GaeKai
{
    /// <summary>
    /// ถอนการติดตั้ง (เรียกจาก Settings → Apps ด้วย "GaeKai.exe --uninstall")
    /// </summary>
    internal static class Uninstaller
    {
        public static int Run(bool quiet)
        {
            if (!quiet && MessageBox.Show(
                    "ต้องการถอนการติดตั้ง " + InstallLayout.DisplayName + " ออกจากเครื่องใช่ไหม?",
                    InstallLayout.DisplayName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return 1;
            }

            if (!InstallLayout.CloseRunningInstances(5000))
            {
                if (!quiet)
                {
                    MessageBox.Show(InstallLayout.CloseManuallyMessage, InstallLayout.DisplayName,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return 1;
            }

            Try(delegate { InstallLayout.SetAutostart(Application.ExecutablePath, false); });
            Try(delegate { DeleteFile(InstallLayout.StartMenuShortcutPath); });
            Try(delegate { DeleteFile(InstallLayout.DesktopShortcutPath); });
            Try(delegate { Registry.CurrentUser.DeleteSubKeyTree(InstallLayout.UninstallKeyPath, false); });
            Try(delegate
            {
                if (Directory.Exists(AppSettings.FolderPath)) Directory.Delete(AppSettings.FolderPath, true);
            });

            if (!quiet)
            {
                MessageBox.Show("ถอนการติดตั้ง " + InstallLayout.DisplayName + " เรียบร้อยแล้ว",
                    InstallLayout.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            DeleteProgramFiles();
            return 0;
        }

        /// <summary>
        /// ไฟล์ .exe ที่กำลังทำงานลบตัวเองไม่ได้ จึงให้ cmd รอ 2 วินาทีจนโปรแกรมปิดแล้วค่อยลบ
        /// ลบเฉพาะ GaeKai.exe และโฟลเดอร์ที่ว่างแล้วเท่านั้น (rmdir ไม่ใช้ /s)
        /// </summary>
        private static void DeleteProgramFiles()
        {
            string exe = InstallLayout.InstalledExePath;
            string dir = InstallLayout.InstallDir;
            bool runningFromInstallDir = string.Equals(
                Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase);

            if (!runningFromInstallDir)
            {
                Try(delegate { DeleteFile(exe); });
                Try(delegate { if (Directory.Exists(dir)) Directory.Delete(dir, false); });
                return;
            }

            ProcessStartInfo psi = new ProcessStartInfo(
                Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                "/c ping 127.0.0.1 -n 3 > nul & del /f /q \"" + exe + "\" & rmdir \"" + dir + "\"");
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.WorkingDirectory = Path.GetTempPath();
            Try(delegate { Process.Start(psi); });
        }

        private static void DeleteFile(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        private static void Try(Action action)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
                // ทำขั้นตอนอื่นต่อ ถึงขั้นตอนนี้จะไม่สำเร็จก็ตาม
            }
        }
    }
}
