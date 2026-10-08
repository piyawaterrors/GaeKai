using System;
using System.Windows.Forms;

namespace GaeKai
{
    /// <summary>ช่องสำหรับกดคีย์ลัดที่ต้องการ แล้วแสดงผล เช่น "Ctrl + Shift + Space"</summary>
    internal sealed class HotkeyBox : TextBox
    {
        private Hotkey hotkey;

        public HotkeyBox()
        {
            ReadOnly = true;
            ShortcutsEnabled = false;
            BackColor = System.Drawing.SystemColors.Window;
            TextAlign = HorizontalAlignment.Center;
        }

        public event EventHandler HotkeyChanged;

        public Hotkey Hotkey
        {
            get { return hotkey; }
            set
            {
                hotkey = value;
                Text = value == null ? "" : value.ToString();
                EventHandler handler = HotkeyChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e)
        {
            // ให้ Enter, ลูกศร ฯลฯ ถูกส่งมาที่ช่องนี้ แต่ Tab / Esc เปล่าๆ ยังใช้เลื่อนโฟกัส / ปิดหน้าต่างได้
            bool plainNavigation = e.Modifiers == Keys.None && (e.KeyCode == Keys.Tab || e.KeyCode == Keys.Escape);
            e.IsInputKey = !plainNavigation;
            base.OnPreviewKeyDown(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            uint mods = CurrentModifiers(e.Modifiers);
            Keys key = e.KeyCode;

            if (Hotkey.IsModifierKey(key))
            {
                // กำลังกดแค่ปุ่มกดร่วม แสดงให้เห็นระหว่างกด
                Text = Hotkey.ModifiersToString(mods) + " + ...";
                return;
            }

            Hotkey candidate = new Hotkey(mods, key);
            if (candidate.IsValid) Hotkey = candidate;
            else Text = "ต้องกดร่วมกับ Ctrl / Alt / Shift / Win";
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            e.Handled = true;
            if (e.Modifiers == Keys.None && !IsWinDown())
            {
                Text = hotkey == null ? "" : hotkey.ToString();
            }
            base.OnKeyUp(e);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Text = hotkey == null ? "" : hotkey.ToString();
            base.OnLostFocus(e);
        }

        private static uint CurrentModifiers(Keys modifiers)
        {
            uint mods = 0;
            if ((modifiers & Keys.Control) != 0) mods |= NativeMethods.MOD_CONTROL;
            if ((modifiers & Keys.Alt) != 0) mods |= NativeMethods.MOD_ALT;
            if ((modifiers & Keys.Shift) != 0) mods |= NativeMethods.MOD_SHIFT;
            if (IsWinDown()) mods |= NativeMethods.MOD_WIN;
            return mods;
        }

        private static bool IsWinDown()
        {
            return (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0 ||
                   (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0;
        }
    }
}
