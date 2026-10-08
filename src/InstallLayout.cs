using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace GaeKai
{
    /// <summary>
    /// ชื่อ ตำแหน่งไฟล์ และ Registry ที่ตัวโปรแกรมและตัวติดตั้ง (GaeKai-Setup.exe) ใช้ร่วมกัน
    /// ติดตั้งแบบรายผู้ใช้ทั้งหมด จึงไม่ต้องใช้สิทธิ์ Admin
    /// </summary>
    internal static class InstallLayout
    {
        internal const string AppName = "GaeKai";
        internal const string DisplayName = "GaeKai แก้ไข";
        internal const string Publisher = "GaeKai contributors";
        internal const string ExeName = AppName + ".exe";

        internal const string AutostartArg = "--autostart";
        internal const string UninstallArg = "--uninstall";
        internal const string QuietArg = "--quiet";

        internal const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        internal const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName;

        internal const string ShowSettingsMessageName = AppName + ".ShowSettings";
        internal const string ExitMessageName = AppName + ".Exit";

        public static string InstallDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName);
            }
        }

        public static string InstalledExePath
        {
            get { return Path.Combine(InstallDir, ExeName); }
        }

        public static string StartMenuShortcutPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), DisplayName + ".lnk"); }
        }

        public static string DesktopShortcutPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), DisplayName + ".lnk"); }
        }

        /// <summary>ติดตั้งผ่านตัวติดตั้งแล้วหรือยัง (มีรายการใน Settings → Apps)</summary>
        public static bool IsInstalled
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(UninstallKeyPath, false))
                {
                    return key != null;
                }
            }
        }

        public static string AutostartCommand(string exePath)
        {
            return "\"" + exePath + "\" " + AutostartArg;
        }

        public static bool IsAutostartEnabled
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    return key != null && key.GetValue(AppName) != null;
                }
            }
        }

        public static void SetAutostart(string exePath, bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (enabled) key.SetValue(AppName, AutostartCommand(exePath));
                else if (key.GetValue(AppName) != null) key.DeleteValue(AppName, false);
            }
        }

        internal const string CloseManuallyMessage =
            "GaeKai ที่เปิดอยู่ในตอนนี้ปิดเองไม่ได้ (อาจเปิดด้วยสิทธิ์ Administrator)\n" +
            "กรุณาคลิกขวาที่ไอคอน ก ในถาดไอคอนมุมขวาล่าง แล้วเลือก \"ออกจากโปรแกรม\" ก่อน แล้วลองอีกครั้ง";

        /// <summary>
        /// ปิด GaeKai ที่กำลังทำงานอยู่ (ยกเว้นตัวเอง) ก่อนอัปเดตหรือถอนการติดตั้ง
        /// ขอให้ปิดเองก่อน ถ้าไม่ปิดภายในเวลาที่กำหนดจึงบังคับปิด
        /// </summary>
        /// <returns>false ถ้ายังมีตัวที่ปิดไม่ได้เหลืออยู่</returns>
        public static bool CloseRunningInstances(int timeoutMs)
        {
            bool allClosed = true;
            int self = Process.GetCurrentProcess().Id;
            int session = Process.GetCurrentProcess().SessionId;

            int exitMessage = NativeMethods.RegisterWindowMessage(ExitMessageName);
            if (exitMessage != 0)
            {
                NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, exitMessage, IntPtr.Zero, IntPtr.Zero);
            }

            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            foreach (Process process in Process.GetProcessesByName(AppName))
            {
                using (process)
                {
                    if (process.Id == self || process.SessionId != session) continue;
                    try
                    {
                        int remaining = Math.Max(0, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
                        if (!process.WaitForExit(remaining))
                        {
                            process.Kill();
                            if (!process.WaitForExit(2000)) allClosed = false;
                        }
                    }
                    catch (InvalidOperationException) { } // ปิดไปแล้ว
                    catch (System.ComponentModel.Win32Exception)
                    {
                        allClosed = false; // ไม่มีสิทธิ์ปิด (เช่น เปิดด้วยสิทธิ์ Admin)
                    }
                }
            }
            return allClosed;
        }
    }
}
