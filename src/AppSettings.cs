using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GaeKai
{
    /// <summary>
    /// การตั้งค่า เก็บเป็นไฟล์ข้อความแบบ key=value ที่ %APPDATA%\GaeKai\settings.ini
    /// (ส่วน "เริ่มทำงานพร้อม Windows" เก็บใน Registry ดู <see cref="StartupManager"/>)
    /// </summary>
    internal sealed class AppSettings
    {
        public Hotkey Hotkey = Hotkey.Default;
        public bool SwitchKeyboardLayout = true;
        public bool RestoreClipboard = true;
        public bool Paused;

        public static string FolderPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Program.AppName);
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(FolderPath, "settings.ini"); }
        }

        public static bool Exists
        {
            get { return File.Exists(FilePath); }
        }

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            if (!Exists) return settings;

            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed[0] == '#' || trimmed[0] == ';') continue;
                    int eq = trimmed.IndexOf('=');
                    if (eq <= 0) continue;
                    values[trimmed.Substring(0, eq).Trim()] = trimmed.Substring(eq + 1).Trim();
                }
            }
            catch (IOException)
            {
                return settings;
            }
            catch (UnauthorizedAccessException)
            {
                return settings;
            }

            string value;
            if (values.TryGetValue("Hotkey", out value))
            {
                Hotkey hotkey = Hotkey.Parse(value);
                if (hotkey != null) settings.Hotkey = hotkey;
            }
            settings.SwitchKeyboardLayout = ReadBool(values, "SwitchKeyboardLayout", settings.SwitchKeyboardLayout);
            settings.RestoreClipboard = ReadBool(values, "RestoreClipboard", settings.RestoreClipboard);
            settings.Paused = ReadBool(values, "Paused", settings.Paused);
            return settings;
        }

        public void Save()
        {
            Directory.CreateDirectory(FolderPath);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# GaeKai settings");
            sb.AppendLine("Hotkey=" + Hotkey);
            sb.AppendLine("SwitchKeyboardLayout=" + SwitchKeyboardLayout.ToString().ToLowerInvariant());
            sb.AppendLine("RestoreClipboard=" + RestoreClipboard.ToString().ToLowerInvariant());
            sb.AppendLine("Paused=" + Paused.ToString().ToLowerInvariant());
            File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
        }

        private static bool ReadBool(Dictionary<string, string> values, string key, bool fallback)
        {
            string value;
            bool result;
            if (values.TryGetValue(key, out value) && bool.TryParse(value, out result)) return result;
            return fallback;
        }
    }
}
