using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("GaeKai Setup")]
[assembly: AssemblyDescription("Installer for GaeKai")]
[assembly: AssemblyProduct("GaeKai")]
[assembly: AssemblyCopyright("MIT License")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]
[assembly: ComVisible(false)]

namespace GaeKai.Setup
{
    internal sealed class InstallOptions
    {
        public bool StartMenuShortcut = true;
        public bool DesktopShortcut;
        public bool StartWithWindows = true;
        public bool LaunchAfterInstall = true;

        /// <summary>ค่าเริ่มต้นของตัวเลือก: ถ้าเป็นการอัปเดต ให้คงตัวเลือกเดิมที่ผู้ใช้เลือกไว้</summary>
        public static InstallOptions ForThisComputer()
        {
            InstallOptions options = new InstallOptions();
            if (InstallLayout.IsInstalled)
            {
                options.StartMenuShortcut = File.Exists(InstallLayout.StartMenuShortcutPath);
                options.DesktopShortcut = File.Exists(InstallLayout.DesktopShortcutPath);
                options.StartWithWindows = InstallLayout.IsAutostartEnabled;
            }
            return options;
        }
    }

    internal static class SetupProgram
    {
        /// <summary>
        /// GaeKai-Setup.exe        เปิดหน้าติดตั้ง
        /// GaeKai-Setup.exe /S     ติดตั้งแบบเงียบ (Start Menu + เปิดพร้อม Windows, ไม่เปิดโปรแกรม)
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            byte[] payload = Installer.ReadPayload();
            if (payload == null)
            {
                MessageBox.Show("ไฟล์ติดตั้งเสียหาย กรุณาดาวน์โหลดใหม่", InstallLayout.DisplayName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            bool silent = Array.Exists(args, delegate(string a)
            {
                return a == "/S" || a == "/s" || string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase);
            });
            if (silent)
            {
                InstallOptions options = InstallOptions.ForThisComputer();
                options.LaunchAfterInstall = false;
                try
                {
                    Installer.Install(payload, options);
                    return 0;
                }
                catch (Exception)
                {
                    return 1;
                }
            }

            Application.Run(new SetupForm(payload));
            return 0;
        }
    }

    internal static class Installer
    {
        public static byte[] ReadPayload()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(InstallLayout.ExeName))
            {
                if (stream == null) return null;
                using (MemoryStream ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        public static Version PayloadVersion(byte[] payload)
        {
            try
            {
                return Assembly.ReflectionOnlyLoad(payload).GetName().Version;
            }
            catch (Exception)
            {
                return new Version(0, 0, 0);
            }
        }

        public static void Install(byte[] payload, InstallOptions options)
        {
            string exe = InstallLayout.InstalledExePath;

            // อัปเดต: ปิดตัวเก่าที่กำลังทำงานก่อน ไม่อย่างนั้นเขียนทับไฟล์ไม่ได้
            // และตัวใหม่จะเปิดไม่ขึ้นเพราะตัวเก่ายังจองไว้
            if (!InstallLayout.CloseRunningInstances(5000))
            {
                throw new InvalidOperationException(InstallLayout.CloseManuallyMessage);
            }

            Directory.CreateDirectory(InstallLayout.InstallDir);
            WriteWithRetry(exe, payload);

            SetShortcut(InstallLayout.StartMenuShortcutPath, exe, options.StartMenuShortcut);
            SetShortcut(InstallLayout.DesktopShortcutPath, exe, options.DesktopShortcut);
            InstallLayout.SetAutostart(exe, options.StartWithWindows);
            RegisterUninstaller(exe, PayloadVersion(payload), payload.Length);

            if (options.LaunchAfterInstall)
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe);
                psi.WorkingDirectory = InstallLayout.InstallDir;
                Process.Start(psi);
            }
        }

        private static void WriteWithRetry(string path, byte[] data)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    File.WriteAllBytes(path, data);
                    return;
                }
                catch (IOException)
                {
                    // โปรแกรมเก่าเพิ่งปิด ไฟล์อาจยังถูกล็อกอยู่ชั่วครู่
                    if (attempt >= 20) throw;
                    Thread.Sleep(250);
                }
            }
        }

        private static void SetShortcut(string shortcutPath, string exe, bool wanted)
        {
            if (wanted)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath));
                ShellLink.Create(shortcutPath, exe, InstallLayout.InstallDir,
                    "แก้ข้อความที่พิมพ์ผิดภาษา ไทย ⇄ อังกฤษ");
            }
            else if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }
        }

        /// <summary>ลงทะเบียนให้แสดงใน Settings → Apps และถอนการติดตั้งได้จากที่นั่น</summary>
        private static void RegisterUninstaller(string exe, Version version, int sizeBytes)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(InstallLayout.UninstallKeyPath))
            {
                key.SetValue("DisplayName", InstallLayout.DisplayName);
                key.SetValue("DisplayVersion", version.ToString(3));
                key.SetValue("Publisher", InstallLayout.Publisher);
                key.SetValue("DisplayIcon", exe + ",0");
                key.SetValue("InstallLocation", InstallLayout.InstallDir);
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                key.SetValue("UninstallString", "\"" + exe + "\" " + InstallLayout.UninstallArg);
                key.SetValue("QuietUninstallString",
                    "\"" + exe + "\" " + InstallLayout.UninstallArg + " " + InstallLayout.QuietArg);
                key.SetValue("EstimatedSize", (sizeBytes + 1023) / 1024, RegistryValueKind.DWord);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
    }
}
