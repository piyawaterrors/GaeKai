using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GaeKai
{
    /// <summary>เปิด/ปิดการเริ่มทำงานพร้อม Windows ผ่าน HKCU\...\Run (ไม่ต้องใช้สิทธิ์ Admin)</summary>
    internal static class StartupManager
    {
        public static bool IsEnabled
        {
            get { return InstallLayout.IsAutostartEnabled; }
        }

        public static void SetEnabled(bool enabled)
        {
            InstallLayout.SetAutostart(Application.ExecutablePath, enabled);
        }

        /// <summary>ถ้าผู้ใช้ย้ายไฟล์ .exe ไปที่อื่น ให้ชี้ Registry ไปยังตำแหน่งใหม่</summary>
        public static void RefreshPathIfEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(InstallLayout.RunKeyPath, true))
                {
                    if (key == null) return;
                    string current = key.GetValue(InstallLayout.AppName) as string;
                    string expected = InstallLayout.AutostartCommand(Application.ExecutablePath);
                    if (current != null && !string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        key.SetValue(InstallLayout.AppName, expected);
                    }
                }
            }
            catch (Exception)
            {
                // ไม่สำคัญพอที่จะต้องแจ้งผู้ใช้
            }
        }
    }
}
