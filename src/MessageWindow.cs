using System;
using System.Windows.Forms;

namespace GaeKai
{
    /// <summary>
    /// หน้าต่างที่มองไม่เห็น ใช้รับคีย์ลัด (WM_HOTKEY) และข้อความจากโปรแกรมตัวที่สองที่ถูกเปิดซ้ำ
    /// </summary>
    internal sealed class MessageWindow : NativeWindow, IDisposable
    {
        private const int HotkeyId = 0x4B41; // "KA"

        internal static readonly int ShowSettingsMessage =
            NativeMethods.RegisterWindowMessage(InstallLayout.ShowSettingsMessageName);

        // ตัวติดตั้ง/ตัวถอนการติดตั้งส่งมาเพื่อขอให้ปิดโปรแกรม
        private static readonly int ExitMessage =
            NativeMethods.RegisterWindowMessage(InstallLayout.ExitMessageName);

        private bool registered;

        public event EventHandler HotkeyPressed;
        public event EventHandler ShowSettingsRequested;
        public event EventHandler ExitRequested;

        public MessageWindow()
        {
            CreateParams cp = new CreateParams();
            cp.Caption = Program.AppName;
            CreateHandle(cp);

            // ถ้าโปรแกรมนี้ถูกเปิดด้วยสิทธิ์ Administrator ต้องอนุญาตให้ตัวติดตั้ง/ตัวที่เปิดซ้ำ
            // (ซึ่งมีสิทธิ์ปกติ) ส่งข้อความมาได้ ไม่อย่างนั้น Windows จะบล็อกไว้
            if (ShowSettingsMessage != 0)
                NativeMethods.ChangeWindowMessageFilterEx(Handle, ShowSettingsMessage, NativeMethods.MSGFLT_ALLOW, IntPtr.Zero);
            if (ExitMessage != 0)
                NativeMethods.ChangeWindowMessageFilterEx(Handle, ExitMessage, NativeMethods.MSGFLT_ALLOW, IntPtr.Zero);
        }

        public bool RegisterHotkey(Hotkey hotkey)
        {
            UnregisterHotkey();
            if (hotkey == null || !hotkey.IsValid) return false;

            registered = NativeMethods.RegisterHotKey(
                Handle, HotkeyId, hotkey.Modifiers | NativeMethods.MOD_NOREPEAT, (uint)hotkey.Key);
            return registered;
        }

        public void UnregisterHotkey()
        {
            if (!registered) return;
            NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            registered = false;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                EventHandler handler = HotkeyPressed;
                if (handler != null) handler(this, EventArgs.Empty);
                return;
            }
            if (ShowSettingsMessage != 0 && m.Msg == ShowSettingsMessage)
            {
                EventHandler handler = ShowSettingsRequested;
                if (handler != null) handler(this, EventArgs.Empty);
                return;
            }
            if (ExitMessage != 0 && m.Msg == ExitMessage)
            {
                EventHandler handler = ExitRequested;
                if (handler != null) handler(this, EventArgs.Empty);
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            UnregisterHotkey();
            DestroyHandle();
        }
    }
}
