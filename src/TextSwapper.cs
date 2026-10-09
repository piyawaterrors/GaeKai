using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GaeKai
{
    /// <summary>
    /// ขั้นตอนหลัก: คัดลอกข้อความที่คลุมไว้ (Ctrl+C) → แปลงภาษา → วางทับ (Ctrl+V) → สลับแป้นพิมพ์ → คืนค่าคลิปบอร์ด
    /// </summary>
    internal static class TextSwapper
    {
        private const int KeyReleaseTimeoutMs = 2000;
        private const int CopyTimeoutMs = 700;
        private const int PasteSettleMs = 400;

        /// <returns>true ถ้าแปลงและวางทับแล้ว</returns>
        public static async Task<bool> ConvertSelectionAsync(AppSettings settings, IntPtr owner)
        {
            // ต้องรอให้ผู้ใช้ปล่อยปุ่มคีย์ลัดก่อน ไม่อย่างนั้น Ctrl+C จะกลายเป็น Ctrl+Shift+C
            await WaitForKeysReleasedAsync(settings.Hotkey);

            ClipboardHelper.Snapshot backup = settings.RestoreClipboard ? ClipboardHelper.Capture(owner) : null;

            uint sequence = NativeMethods.GetClipboardSequenceNumber();
            SendShortcut(Keys.C);

            string selected = await WaitForCopiedTextAsync(sequence, owner);
            if (selected == null) return false; // ไม่มีข้อความที่คลุมไว้ คลิปบอร์ดไม่ถูกแตะ

            if (ClipboardHelper.IsWholeLineCopy(owner))
            {
                ClipboardHelper.Restore(owner, backup);
                return false;
            }

            string converted = LayoutConverter.Fix(selected);
            if (converted == selected)
            {
                ClipboardHelper.Restore(owner, backup);
                return false;
            }

            if (!ClipboardHelper.SetText(owner, converted)) return false;
            SendShortcut(Keys.V);

            if (settings.SwitchKeyboardLayout)
            {
                // เคอร์เซอร์อยู่ท้ายข้อความที่วาง จึงสลับเป็นภาษาของคำท้ายสุด ให้พิมพ์ต่อได้ทันที
                switch (LayoutConverter.LanguageAtEnd(converted))
                {
                    case TextLanguage.Thai:
                        KeyboardLayouts.SwitchForegroundWindow(KeyboardLayouts.LangThai);
                        break;
                    case TextLanguage.English:
                        KeyboardLayouts.SwitchForegroundWindow(KeyboardLayouts.LangEnglish);
                        break;
                }
            }

            if (settings.RestoreClipboard)
            {
                // โปรแกรมปลายทางอ่านคลิปบอร์ดแบบไม่พร้อมกัน ต้องรอให้วางเสร็จก่อนคืนค่า
                await Task.Delay(PasteSettleMs);
                ClipboardHelper.Restore(owner, backup);
            }
            return true;
        }

        private static async Task WaitForKeysReleasedAsync(Hotkey hotkey)
        {
            List<int> modifiers = new List<int>();
            if (hotkey.HasModifier(NativeMethods.MOD_CONTROL)) modifiers.Add(NativeMethods.VK_CONTROL);
            if (hotkey.HasModifier(NativeMethods.MOD_SHIFT)) modifiers.Add(NativeMethods.VK_SHIFT);
            if (hotkey.HasModifier(NativeMethods.MOD_ALT)) modifiers.Add(NativeMethods.VK_MENU);
            if (hotkey.HasModifier(NativeMethods.MOD_WIN))
            {
                modifiers.Add(NativeMethods.VK_LWIN);
                modifiers.Add(NativeMethods.VK_RWIN);
            }

            DateTime deadline = DateTime.UtcNow.AddMilliseconds(KeyReleaseTimeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (!IsDown((int)hotkey.Key) && !modifiers.Exists(IsDown)) return;
                await Task.Delay(15);
            }

            // ผู้ใช้ยังกดค้างอยู่ — ปล่อยปุ่มกดร่วมแทน เพื่อไม่ให้ไปปนกับ Ctrl+C / Ctrl+V
            List<NativeMethods.INPUT> inputs = new List<NativeMethods.INPUT>();
            foreach (int vk in modifiers)
            {
                if (IsDown(vk)) inputs.Add(KeyInput((ushort)vk, true));
            }
            Send(inputs.ToArray());
        }

        private static async Task<string> WaitForCopiedTextAsync(uint sequenceBefore, IntPtr owner)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(CopyTimeoutMs);
            while (NativeMethods.GetClipboardSequenceNumber() == sequenceBefore)
            {
                if (DateTime.UtcNow >= deadline) return null;
                await Task.Delay(15);
            }

            // โปรแกรมต้นทางอาจยังเขียนข้อมูลรูปแบบอื่นลงคลิปบอร์ดไม่เสร็จ
            await Task.Delay(40);
            return ClipboardHelper.GetText(owner) ?? string.Empty;
        }

        private static bool IsDown(int vk)
        {
            return (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        private static void SendShortcut(Keys key)
        {
            Send(new[]
            {
                KeyInput(NativeMethods.VK_CONTROL, false),
                KeyInput((ushort)key, false),
                KeyInput((ushort)key, true),
                KeyInput(NativeMethods.VK_CONTROL, true),
            });
        }

        private static NativeMethods.INPUT KeyInput(ushort vk, bool keyUp)
        {
            uint flags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0;
            if (vk == NativeMethods.VK_LWIN || vk == NativeMethods.VK_RWIN) flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;

            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_KEYBOARD;
            input.U.ki.wVk = vk;
            input.U.ki.wScan = (ushort)NativeMethods.MapVirtualKey(vk, NativeMethods.MAPVK_VK_TO_VSC);
            input.U.ki.dwFlags = flags;
            return input;
        }

        private static void Send(NativeMethods.INPUT[] inputs)
        {
            if (inputs.Length == 0) return;
            NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
        }
    }

    /// <summary>สลับภาษาแป้นพิมพ์ของหน้าต่างที่ใช้งานอยู่</summary>
    internal static class KeyboardLayouts
    {
        internal const int LangThai = 0x1E;
        internal const int LangEnglish = 0x09;

        public static void SwitchForegroundWindow(int primaryLanguage)
        {
            IntPtr layout = Find(primaryLanguage);
            if (layout == IntPtr.Zero) return;

            // ส่งไปที่ช่องที่กำลังพิมพ์อยู่ด้วย ไม่ใช่แค่หน้าต่างหลัก เพราะบางโปรแกรม (เช่นแอปแบบ UWP)
            // ช่องพิมพ์อยู่คนละ thread กับกรอบหน้าต่าง ส่งไปที่กรอบอย่างเดียวจะไม่เปลี่ยนภาษา
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            NativeMethods.GUITHREADINFO info = new NativeMethods.GUITHREADINFO();
            info.cbSize = Marshal.SizeOf(typeof(NativeMethods.GUITHREADINFO));
            IntPtr focus = NativeMethods.GetGUIThreadInfo(0, ref info) ? info.hwndFocus : IntPtr.Zero;

            if (focus != IntPtr.Zero)
                NativeMethods.PostMessage(focus, NativeMethods.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, layout);
            if (foreground != IntPtr.Zero && foreground != focus)
                NativeMethods.PostMessage(foreground, NativeMethods.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, layout);
        }

        private static IntPtr Find(int primaryLanguage)
        {
            int count = NativeMethods.GetKeyboardLayoutList(0, null);
            if (count <= 0) return IntPtr.Zero;

            IntPtr[] layouts = new IntPtr[count];
            NativeMethods.GetKeyboardLayoutList(count, layouts);
            foreach (IntPtr layout in layouts)
            {
                int langId = (int)(layout.ToInt64() & 0xFFFF);
                if ((langId & 0x3FF) == primaryLanguage) return layout;
            }
            return IntPtr.Zero;
        }
    }
}
