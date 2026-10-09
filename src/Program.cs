using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("GaeKai")]
[assembly: AssemblyDescription("Fix text typed with the wrong keyboard layout (Thai <-> English)")]
[assembly: AssemblyProduct("GaeKai")]
[assembly: AssemblyCopyright("MIT License")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]
[assembly: ComVisible(false)]

namespace GaeKai
{
    internal static class Program
    {
        internal const string AppName = InstallLayout.AppName;
        internal const string DisplayName = InstallLayout.DisplayName;

        internal static string Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(3); }
        }

        [STAThread]
        private static int Main(string[] args)
        {
            if (HasArg(args, InstallLayout.UninstallArg))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                return Uninstaller.Run(HasArg(args, InstallLayout.QuietArg));
            }

            bool autostarted = HasArg(args, InstallLayout.AutostartArg);

            bool createdNew;
            Mutex mutex;
            try
            {
                mutex = new Mutex(true, @"Local\" + AppName + ".SingleInstance", out createdNew);
            }
            catch (UnauthorizedAccessException)
            {
                // มีอีกตัวที่เปิดด้วยสิทธิ์ Administrator อยู่แล้ว ตัวที่มีสิทธิ์ปกติจึงเปิด mutex ของมันไม่ได้
                mutex = null;
                createdNew = false;
            }

            using (mutex)
            {
                if (!createdNew)
                {
                    // เปิดซ้ำ: บอกตัวที่ทำงานอยู่ให้เปิดหน้าตั้งค่าแทน (เผื่อหาไอคอนในถาดไม่เจอ)
                    if (!autostarted)
                    {
                        NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
                        NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST,
                            MessageWindow.ShowSettingsMessage, IntPtr.Zero, IntPtr.Zero);
                    }
                    return 0;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    MessageBox.Show("เกิดข้อผิดพลาด: " + e.Exception.Message, DisplayName,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                };

                using (TrayApp app = new TrayApp(autostarted))
                {
                    Application.Run(app);
                }
                GC.KeepAlive(mutex);
            }
            return 0;
        }

        private static bool HasArg(string[] args, string name)
        {
            return Array.Exists(args, delegate(string a)
            {
                return string.Equals(a, name, StringComparison.OrdinalIgnoreCase);
            });
        }
    }
}
