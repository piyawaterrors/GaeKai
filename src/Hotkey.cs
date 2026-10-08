using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace GaeKai
{
    /// <summary>คีย์ลัด = ปุ่มกดร่วม (Ctrl/Alt/Shift/Win) + ปุ่มหลัก 1 ปุ่ม</summary>
    internal sealed class Hotkey
    {
        public static readonly Hotkey Default =
            new Hotkey(NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT, Keys.Space);

        private static readonly Dictionary<Keys, string> FriendlyNames = new Dictionary<Keys, string>
        {
            { Keys.Oemtilde, "`" }, { Keys.OemMinus, "-" }, { Keys.Oemplus, "=" },
            { Keys.OemOpenBrackets, "[" }, { Keys.OemCloseBrackets, "]" }, { Keys.OemPipe, "\\" },
            { Keys.OemSemicolon, ";" }, { Keys.OemQuotes, "'" }, { Keys.Oemcomma, "," },
            { Keys.OemPeriod, "." }, { Keys.OemQuestion, "/" },
            { Keys.Enter, "Enter" }, { Keys.Back, "Backspace" }, { Keys.Capital, "CapsLock" },
            { Keys.PageUp, "PageUp" }, { Keys.PageDown, "PageDown" }, { Keys.Escape, "Esc" },
            { Keys.Scroll, "ScrollLock" }, { Keys.Pause, "Pause" }, { Keys.Insert, "Insert" },
            { Keys.Delete, "Delete" }, { Keys.Home, "Home" }, { Keys.End, "End" },
            { Keys.PrintScreen, "PrintScreen" },
        };

        public readonly uint Modifiers;
        public readonly Keys Key;

        public Hotkey(uint modifiers, Keys key)
        {
            Modifiers = modifiers & (NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT |
                                     NativeMethods.MOD_SHIFT | NativeMethods.MOD_WIN);
            Key = key & Keys.KeyCode;
        }

        public bool HasModifier(uint mod)
        {
            return (Modifiers & mod) != 0;
        }

        internal static bool IsModifierKey(Keys key)
        {
            switch (key)
            {
                case Keys.ControlKey: case Keys.LControlKey: case Keys.RControlKey:
                case Keys.ShiftKey: case Keys.LShiftKey: case Keys.RShiftKey:
                case Keys.Menu: case Keys.LMenu: case Keys.RMenu:
                case Keys.LWin: case Keys.RWin:
                    return true;
            }
            return false;
        }

        private bool IsStandaloneKey
        {
            get { return (Key >= Keys.F1 && Key <= Keys.F24) || Key == Keys.Pause || Key == Keys.Scroll; }
        }

        /// <summary>ต้องมีปุ่มหลัก และต้องมีปุ่มกดร่วมอย่างน้อย 1 ปุ่ม (ยกเว้น F1–F24, Pause, ScrollLock)</summary>
        public bool IsValid
        {
            get
            {
                if (Key == Keys.None || IsModifierKey(Key)) return false;
                return Modifiers != 0 || IsStandaloneKey;
            }
        }

        public static string ModifiersToString(uint modifiers)
        {
            List<string> parts = new List<string>();
            if ((modifiers & NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((modifiers & NativeMethods.MOD_ALT) != 0) parts.Add("Alt");
            if ((modifiers & NativeMethods.MOD_SHIFT) != 0) parts.Add("Shift");
            if ((modifiers & NativeMethods.MOD_WIN) != 0) parts.Add("Win");
            return string.Join(" + ", parts);
        }

        public static string KeyToString(Keys key)
        {
            string name;
            if (FriendlyNames.TryGetValue(key, out name)) return name;
            if (key >= Keys.D0 && key <= Keys.D9) return ((char)('0' + (key - Keys.D0))).ToString();
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return "Num" + (key - Keys.NumPad0);
            return key.ToString();
        }

        public override string ToString()
        {
            string mods = ModifiersToString(Modifiers);
            string key = KeyToString(Key);
            return mods.Length == 0 ? key : mods + " + " + key;
        }

        public static Hotkey Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            string[] parts = text.Split('+');
            uint mods = 0;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                switch (parts[i].Trim().ToLowerInvariant())
                {
                    case "ctrl": case "control": mods |= NativeMethods.MOD_CONTROL; break;
                    case "alt": mods |= NativeMethods.MOD_ALT; break;
                    case "shift": mods |= NativeMethods.MOD_SHIFT; break;
                    case "win": case "windows": mods |= NativeMethods.MOD_WIN; break;
                    default: return null;
                }
            }

            Keys key = ParseKey(parts[parts.Length - 1].Trim());
            if (key == Keys.None) return null;

            Hotkey hotkey = new Hotkey(mods, key);
            return hotkey.IsValid ? hotkey : null;
        }

        private static Keys ParseKey(string name)
        {
            if (name.Length == 0) return Keys.None;

            foreach (KeyValuePair<Keys, string> pair in FriendlyNames)
            {
                if (string.Equals(pair.Value, name, StringComparison.OrdinalIgnoreCase)) return pair.Key;
            }
            if (name.Length == 1 && name[0] >= '0' && name[0] <= '9') return Keys.D0 + (name[0] - '0');
            if (name.StartsWith("Num", StringComparison.OrdinalIgnoreCase) && name.Length == 4 &&
                name[3] >= '0' && name[3] <= '9')
            {
                return Keys.NumPad0 + (name[3] - '0');
            }

            Keys key;
            if (Enum.TryParse(name, true, out key) && (key & ~Keys.KeyCode) == 0) return key;
            return Keys.None;
        }
    }
}
