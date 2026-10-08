using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GaeKai
{
    /// <summary>ตัวโปรแกรมหลัก: ไอคอนที่ถาดไอคอน + เมนู + รับคีย์ลัด</summary>
    internal sealed class TrayApp : ApplicationContext
    {
        private readonly AppSettings settings;
        private readonly MessageWindow window;
        private readonly NotifyIcon tray;
        private readonly ToolStripMenuItem hotkeyItem;
        private readonly ToolStripMenuItem pauseItem;
        private readonly ToolStripMenuItem startupItem;
        private SettingsForm settingsForm;
        private bool busy;
        private bool balloonOpensSettings;

        public TrayApp(bool autostarted)
        {
            bool firstRun = !AppSettings.Exists;
            settings = AppSettings.Load();
            if (firstRun)
            {
                // ครั้งแรกที่เปิดแบบไม่ผ่านตัวติดตั้ง: ตั้งให้เปิดอัตโนมัติพร้อม Windows เลย
                // (ถ้าติดตั้งผ่าน GaeKai-Setup.exe ผู้ใช้เลือกเรื่องนี้ไว้ในตัวติดตั้งแล้ว)
                if (!InstallLayout.IsInstalled)
                {
                    try { StartupManager.SetEnabled(true); }
                    catch (Exception) { }
                }
                TrySaveSettings();
            }
            else
            {
                StartupManager.RefreshPathIfEnabled();
            }

            window = new MessageWindow();
            window.HotkeyPressed += OnHotkeyPressed;
            window.ShowSettingsRequested += delegate { ShowSettings(); };
            window.ExitRequested += delegate { ExitThread(); };

            ContextMenuStrip menu = new ContextMenuStrip();
            hotkeyItem = new ToolStripMenuItem();
            hotkeyItem.Enabled = false;
            ToolStripMenuItem settingsItem = new ToolStripMenuItem("ตั้งค่า / เปลี่ยนคีย์ลัด...", null, delegate { ShowSettings(); });
            settingsItem.Font = new Font(settingsItem.Font, FontStyle.Bold);
            pauseItem = new ToolStripMenuItem("หยุดชั่วคราว", null, delegate { TogglePause(); });
            startupItem = new ToolStripMenuItem("เปิดอัตโนมัติเมื่อเปิดเครื่อง", null, delegate { ToggleStartup(); });
            menu.Items.Add(hotkeyItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(settingsItem);
            menu.Items.Add(pauseItem);
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("วิธีใช้ / เกี่ยวกับ", null, delegate { ShowAbout(); }));
            menu.Items.Add(new ToolStripMenuItem("ออกจากโปรแกรม", null, delegate { ExitThread(); }));
            menu.Opening += delegate { startupItem.Checked = StartupManager.IsEnabled; };

            tray = new NotifyIcon();
            tray.ContextMenuStrip = menu;
            tray.MouseDoubleClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) ShowSettings();
            };
            tray.BalloonTipClicked += delegate
            {
                if (balloonOpensSettings) ShowSettings();
            };
            tray.Icon = AppIcon.Small;
            tray.Visible = true;

            if (ApplyHotkey() && !autostarted && !settings.Paused)
            {
                ShowBalloon("โปรแกรมทำงานอยู่ที่ถาดไอคอนมุมขวาล่าง\nคลุมข้อความที่พิมพ์ผิดภาษา แล้วกด " + settings.Hotkey,
                    ToolTipIcon.Info, false);
            }
        }

        private async void OnHotkeyPressed(object sender, EventArgs e)
        {
            if (busy || settings.Paused) return;
            busy = true;
            try
            {
                await TextSwapper.ConvertSelectionAsync(settings, window.Handle);
            }
            catch (Exception ex)
            {
                ShowBalloon("แปลงข้อความไม่สำเร็จ: " + ex.Message, ToolTipIcon.Error, false);
            }
            finally
            {
                busy = false;
            }
        }

        /// <summary>ลงทะเบียนคีย์ลัดตามการตั้งค่า แจ้งเตือนถ้าคีย์ลัดชนกับโปรแกรมอื่น</summary>
        private bool ApplyHotkey()
        {
            bool ok = true;
            if (settings.Paused || settingsForm != null)
            {
                window.UnregisterHotkey();
            }
            else
            {
                ok = window.RegisterHotkey(settings.Hotkey);
                if (!ok)
                {
                    ShowBalloon("ใช้คีย์ลัด " + settings.Hotkey + " ไม่ได้ เพราะมีโปรแกรมอื่นใช้อยู่\nคลิกที่นี่เพื่อเปลี่ยนคีย์ลัด",
                        ToolTipIcon.Warning, true);
                }
            }
            UpdateUi(ok);
            return ok;
        }

        private void UpdateUi(bool hotkeyActive)
        {
            string state = settings.Paused ? " (หยุดชั่วคราว)" : hotkeyActive ? "" : " (ใช้งานไม่ได้)";
            hotkeyItem.Text = "คีย์ลัด: " + settings.Hotkey + state;
            pauseItem.Checked = settings.Paused;
            tray.Icon = settings.Paused || !hotkeyActive ? AppIcon.Paused : AppIcon.Small;

            string tip = Program.AppName + " — " + settings.Hotkey + state;
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip; // NotifyIcon รับได้สูงสุด 63 ตัวอักษร
        }

        private void ShowSettings()
        {
            if (settingsForm != null)
            {
                settingsForm.WindowState = FormWindowState.Normal;
                settingsForm.Activate();
                return;
            }

            settingsForm = new SettingsForm(settings, StartupManager.IsEnabled, window.RegisterHotkey);
            // ปิดคีย์ลัดไว้ระหว่างตั้งค่า เพื่อให้กดคีย์ลัดเดิมลงในช่องได้
            window.UnregisterHotkey();
            settingsForm.FormClosed += OnSettingsClosed;
            settingsForm.Show();
        }

        private void OnSettingsClosed(object sender, FormClosedEventArgs e)
        {
            SettingsForm form = settingsForm;
            settingsForm = null;

            if (form.DialogResult == DialogResult.OK)
            {
                settings.Hotkey = form.SelectedHotkey;
                settings.SwitchKeyboardLayout = form.SwitchKeyboardLayout;
                settings.RestoreClipboard = form.RestoreClipboard;
                SetStartup(form.StartWithWindows);
                TrySaveSettings();
            }
            form.Dispose();
            ApplyHotkey();
        }

        private void TogglePause()
        {
            settings.Paused = !settings.Paused;
            TrySaveSettings();
            ApplyHotkey();
        }

        private void ToggleStartup()
        {
            SetStartup(!StartupManager.IsEnabled);
        }

        private void SetStartup(bool enabled)
        {
            try
            {
                StartupManager.SetEnabled(enabled);
            }
            catch (Exception ex)
            {
                ShowBalloon("ตั้งค่าการเปิดอัตโนมัติไม่สำเร็จ: " + ex.Message, ToolTipIcon.Error, false);
            }
        }

        private void TrySaveSettings()
        {
            try
            {
                settings.Save();
            }
            catch (IOException ex)
            {
                ShowBalloon("บันทึกการตั้งค่าไม่สำเร็จ: " + ex.Message, ToolTipIcon.Error, false);
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowBalloon("บันทึกการตั้งค่าไม่สำเร็จ: " + ex.Message, ToolTipIcon.Error, false);
            }
        }

        private void ShowAbout()
        {
            MessageBox.Show(
                Program.DisplayName + " เวอร์ชัน " + Program.Version + "\n" +
                "แก้ข้อความที่พิมพ์ผิดภาษา ไทย ⇄ อังกฤษ (แป้นเกษมณี)\n\n" +
                "วิธีใช้:\n" +
                "1. คลุม (เลือก) ข้อความที่พิมพ์ผิดภาษา\n" +
                "2. กด " + settings.Hotkey + "\n\n" +
                "ตัวอย่าง:\n" +
                "    l;ylfu  →  สวัสดี\n" +
                "    เนนก  →  good\n\n" +
                "ไฟล์ตั้งค่า: " + AppSettings.FilePath,
                Program.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowBalloon(string text, ToolTipIcon icon, bool opensSettings)
        {
            if (tray == null) return;
            balloonOpensSettings = opensSettings;
            tray.ShowBalloonTip(5000, Program.DisplayName, text, icon);
        }

        protected override void ExitThreadCore()
        {
            if (settingsForm != null) settingsForm.Close();
            tray.Visible = false;
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tray.Dispose();
                window.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
