using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace GaeKai
{
    /// <summary>
    /// จัดการคลิปบอร์ดด้วย Win32 API โดยตรง เพื่อสำรอง/คืนค่าข้อมูลเดิมของผู้ใช้ได้ครบทุกรูปแบบ
    /// (ข้อความ, HTML, RTF, รูปภาพ, ไฟล์ ฯลฯ) ตามลำดับเดิม
    /// </summary>
    internal static class ClipboardHelper
    {
        // รูปแบบที่ข้อมูลไม่ใช่ HGLOBAL (เป็น GDI handle) คัดลอกเป็นไบต์ไม่ได้ จึงข้ามไป
        // รูปภาพยังคืนค่าได้ผ่าน CF_DIB / CF_DIBV5 / PNG ที่มักมีมาด้วยเสมอ
        private static readonly HashSet<uint> UncopyableFormats = new HashSet<uint>
        {
            2,    // CF_BITMAP
            3,    // CF_METAFILEPICT
            9,    // CF_PALETTE
            14,   // CF_ENHMETAFILE
            0x80, // CF_OWNERDISPLAY
            0x82, // CF_DSPBITMAP
            0x83, // CF_DSPMETAFILEPICT
            0x8E, // CF_DSPENHMETAFILE
        };

        private static readonly uint CanIncludeInClipboardHistory =
            NativeMethods.RegisterClipboardFormat("CanIncludeInClipboardHistory");

        private static readonly uint CanUploadToCloudClipboard =
            NativeMethods.RegisterClipboardFormat("CanUploadToCloudClipboard");

        internal sealed class Snapshot
        {
            internal readonly List<KeyValuePair<uint, byte[]>> Items = new List<KeyValuePair<uint, byte[]>>();
        }

        /// <summary>เก็บสำเนาคลิปบอร์ดปัจจุบัน คืนค่า null ถ้าเปิดคลิปบอร์ดไม่ได้</summary>
        public static Snapshot Capture(IntPtr owner)
        {
            if (!Open(owner)) return null;
            try
            {
                Snapshot snapshot = new Snapshot();
                uint format = 0;
                while ((format = NativeMethods.EnumClipboardFormats(format)) != 0)
                {
                    if (UncopyableFormats.Contains(format)) continue;
                    if (format >= 0x200 && format <= 0x3FF) continue; // CF_PRIVATEFIRST..CF_GDIOBJLAST

                    byte[] data = ReadGlobal(NativeMethods.GetClipboardData(format));
                    if (data != null) snapshot.Items.Add(new KeyValuePair<uint, byte[]>(format, data));
                }
                return snapshot;
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }

        /// <summary>คืนค่าคลิปบอร์ดจากสำเนา (ไม่ให้ไปซ้ำใน Clipboard History ของ Windows)</summary>
        public static bool Restore(IntPtr owner, Snapshot snapshot)
        {
            if (snapshot == null) return false;
            if (!Open(owner)) return false;
            try
            {
                NativeMethods.EmptyClipboard();
                if (snapshot.Items.Count == 0) return true;

                foreach (KeyValuePair<uint, byte[]> item in snapshot.Items)
                {
                    SetGlobal(item.Key, item.Value);
                }
                ExcludeFromHistory();
                return true;
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }

        public static string GetText(IntPtr owner)
        {
            if (!Open(owner)) return null;
            try
            {
                IntPtr handle = NativeMethods.GetClipboardData(NativeMethods.CF_UNICODETEXT);
                if (handle == IntPtr.Zero) return string.Empty;

                IntPtr ptr = NativeMethods.GlobalLock(handle);
                if (ptr == IntPtr.Zero) return string.Empty;
                try
                {
                    int maxChars = (int)(NativeMethods.GlobalSize(handle).ToUInt64() / 2);
                    string text = Marshal.PtrToStringUni(ptr, maxChars);
                    int nul = text.IndexOf('\0');
                    return nul >= 0 ? text.Substring(0, nul) : text;
                }
                finally
                {
                    NativeMethods.GlobalUnlock(handle);
                }
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }

        // Visual Studio ใส่รูปแบบเหล่านี้มาเมื่อคัดลอกทั้งบรรทัด
        private static readonly string[] LineCopyFormats =
        {
            "MSDEVLineSelect", "VisualStudioEditorOperationsLineCutCopyClipboardTag"
        };

        // VS Code (และโปรแกรมที่สร้างจาก VS Code) เก็บ metadata เป็น JSON ไว้ในรูปแบบเหล่านี้
        private static readonly string[] EditorMetadataFormats =
        {
            "Chromium Web Custom MIME Data Format", "vscode-editor-data"
        };

        private const string EmptySelectionMarker = "\"isFromEmptySelection\":true";

        /// <summary>
        /// โปรแกรมแก้โค้ดบางตัว (VS Code, Visual Studio) จะคัดลอกทั้งบรรทัดเมื่อกด Ctrl+C โดยไม่ได้คลุมข้อความ
        /// ถ้าเป็นกรณีนี้ต้องไม่แปลง ไม่อย่างนั้นจะได้บรรทัดซ้ำเพิ่มเข้ามา
        /// </summary>
        public static bool IsWholeLineCopy(IntPtr owner)
        {
            foreach (string name in LineCopyFormats)
            {
                uint format = NativeMethods.RegisterClipboardFormat(name);
                if (format != 0 && NativeMethods.IsClipboardFormatAvailable(format)) return true;
            }

            if (!Open(owner)) return false;
            try
            {
                foreach (string name in EditorMetadataFormats)
                {
                    uint format = NativeMethods.RegisterClipboardFormat(name);
                    if (format == 0 || !NativeMethods.IsClipboardFormatAvailable(format)) continue;

                    byte[] data = ReadGlobal(NativeMethods.GetClipboardData(format));
                    if (data == null) continue;
                    if (System.Text.Encoding.Unicode.GetString(data).Contains(EmptySelectionMarker) ||
                        System.Text.Encoding.UTF8.GetString(data).Contains(EmptySelectionMarker))
                    {
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }

        /// <summary>ใส่ข้อความลงคลิปบอร์ด โดยไม่บันทึกลง Clipboard History (Win+V)</summary>
        public static bool SetText(IntPtr owner, string text)
        {
            if (!Open(owner)) return false;
            try
            {
                NativeMethods.EmptyClipboard();
                byte[] bytes = new byte[(text.Length + 1) * 2];
                System.Text.Encoding.Unicode.GetBytes(text, 0, text.Length, bytes, 0);
                bool ok = SetGlobal(NativeMethods.CF_UNICODETEXT, bytes);
                ExcludeFromHistory();
                return ok;
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }

        private static void ExcludeFromHistory()
        {
            byte[] zero = new byte[4];
            if (CanIncludeInClipboardHistory != 0) SetGlobal(CanIncludeInClipboardHistory, zero);
            if (CanUploadToCloudClipboard != 0) SetGlobal(CanUploadToCloudClipboard, zero);
        }

        private static bool Open(IntPtr owner)
        {
            // โปรแกรมอื่นอาจกำลังเปิดคลิปบอร์ดอยู่ชั่วครู่ จึงลองซ้ำ
            for (int attempt = 0; attempt < 20; attempt++)
            {
                if (NativeMethods.OpenClipboard(owner)) return true;
                Thread.Sleep(15);
            }
            return false;
        }

        private static byte[] ReadGlobal(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return null;

            ulong size = NativeMethods.GlobalSize(handle).ToUInt64();
            if (size == 0 || size > int.MaxValue) return null;

            IntPtr ptr = NativeMethods.GlobalLock(handle);
            if (ptr == IntPtr.Zero) return null;
            try
            {
                byte[] data = new byte[(int)size];
                Marshal.Copy(ptr, data, 0, data.Length);
                return data;
            }
            finally
            {
                NativeMethods.GlobalUnlock(handle);
            }
        }

        private static bool SetGlobal(uint format, byte[] data)
        {
            IntPtr handle = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, new UIntPtr((uint)Math.Max(data.Length, 1)));
            if (handle == IntPtr.Zero) return false;

            IntPtr ptr = NativeMethods.GlobalLock(handle);
            if (ptr == IntPtr.Zero)
            {
                NativeMethods.GlobalFree(handle);
                return false;
            }
            Marshal.Copy(data, 0, ptr, data.Length);
            NativeMethods.GlobalUnlock(handle);

            // เมื่อสำเร็จ ระบบจะเป็นเจ้าของหน่วยความจำนี้ ห้าม Free เอง
            if (NativeMethods.SetClipboardData(format, handle) == IntPtr.Zero)
            {
                NativeMethods.GlobalFree(handle);
                return false;
            }
            return true;
        }
    }
}
