using System.Collections.Generic;
using System.Text;

namespace GaeKai
{
    internal enum ConversionDirection
    {
        None,
        EnglishToThai,
        ThaiToEnglish
    }

    /// <summary>
    /// แปลงข้อความที่พิมพ์ผิดแป้น ระหว่างแป้น US QWERTY กับแป้นไทยเกษมณี (Kedmanee)
    /// </summary>
    internal static class LayoutConverter
    {
        // แต่ละตำแหน่งของสองสตริงนี้คือปุ่มเดียวกันบนคีย์บอร์ด (47 ปุ่ม x ปกติ/Shift = 94 ตัว)
        private const string EnglishKeys =
            "`1234567890-=" + "qwertyuiop[]\\" + "asdfghjkl;'" + "zxcvbnm,./" +
            "~!@#$%^&*()_+" + "QWERTYUIOP{}|" + "ASDFGHJKL:\"" + "ZXCVBNM<>?";

        private const string ThaiKeys =
            "_ๅ/-ภถุึคตจขช" + "ๆไำพะัีรนยบลฃ" + "ฟหกดเ้่าสวง" + "ผปแอิืทมใฝ" +
            "%+๑๒๓๔ู฿๕๖๗๘๙" + "๐\"ฎฑธํ๊ณฯญฐ,ฅ" + "ฤฆฏโฌ็๋ษศซ." + "()ฉฮฺ์?ฒฬฦ";

        private static readonly Dictionary<char, char> EnToTh = new Dictionary<char, char>();
        private static readonly Dictionary<char, char> ThToEn = new Dictionary<char, char>();

        static LayoutConverter()
        {
            for (int i = 0; i < EnglishKeys.Length; i++)
            {
                EnToTh[EnglishKeys[i]] = ThaiKeys[i];
                ThToEn[ThaiKeys[i]] = EnglishKeys[i];
            }
        }

        internal static int KeyCount
        {
            get { return EnglishKeys.Length; }
        }

        internal static bool IsThai(char c)
        {
            return c >= 'ก' && c <= '๛';
        }

        private static bool IsLatinLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        /// <summary>
        /// เดาว่าข้อความถูกพิมพ์ด้วยแป้นไหน: ถ้ามีอักษรไทยมากกว่าอักษรอังกฤษ ให้แปลงไทย→อังกฤษ
        /// นอกนั้นให้แปลงอังกฤษ→ไทย
        /// </summary>
        internal static ConversionDirection DetectDirection(string text)
        {
            if (string.IsNullOrEmpty(text)) return ConversionDirection.None;

            int thai = 0, latin = 0, mappable = 0;
            foreach (char c in text)
            {
                if (IsThai(c)) thai++;
                else if (IsLatinLetter(c)) latin++;
                if (EnToTh.ContainsKey(c) || ThToEn.ContainsKey(c)) mappable++;
            }

            if (mappable == 0) return ConversionDirection.None;
            return thai > latin ? ConversionDirection.ThaiToEnglish : ConversionDirection.EnglishToThai;
        }

        internal static string Convert(string text, ConversionDirection direction)
        {
            if (string.IsNullOrEmpty(text) || direction == ConversionDirection.None) return text;

            Dictionary<char, char> map = direction == ConversionDirection.EnglishToThai ? EnToTh : ThToEn;
            StringBuilder sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                char mapped;
                sb.Append(map.TryGetValue(c, out mapped) ? mapped : c);
            }
            return sb.ToString();
        }

        internal static string Convert(string text, out ConversionDirection direction)
        {
            direction = DetectDirection(text);
            return Convert(text, direction);
        }
    }
}
