using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace GaeKai
{
    internal enum ConversionDirection
    {
        None,
        EnglishToThai,
        ThaiToEnglish
    }

    internal enum TextLanguage
    {
        None,
        Thai,
        English
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

        // ลักษณะที่พบในข้อความภาษาไทยที่ถูกพิมพ์ด้วยแป้นอังกฤษ แต่แทบไม่พบในภาษาอังกฤษจริง
        // เช่น "l;ylfu" (สวัสดี), "py',u" (ยังมี), "vp^j" (อยู่), "8iy[" (ครับ)
        private static readonly Regex EnglishGarbage = new Regex(
            @"[a-z][\[\]\\;^=~`|\{\}<>]|[\[\]\\;^=~`|\{\}<>][a-z]" + // สัญลักษณ์ติดกับตัวอักษร
            @"|[a-z],[a-z0-9]" +                                     // จุลภาคกลางคำ
            @"|(^|[^a-z0-9.])[.,][a-z]" +                            // คำที่ขึ้นต้นด้วย . หรือ ,
            @"|[0-9](?!(st|nd|rd|th)\b)[a-z]" +                      // ตัวเลขตามด้วยตัวอักษร (ยกเว้น 1st 2nd 3rd 4th)
            @"|[a-rt-z]'(?![a-z])" +                                 // ' ท้ายคำ (ยกเว้น students')
            @"|[a-z]'(?!(s|t|d|m|re|ll|ve)\b)[a-z]",                 // ' กลางคำที่ไม่ใช่ don't, it's, we're ...
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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

        // ---- แปลงทีละช่วง ----

        private enum CharKind
        {
            Separator, // ช่องว่าง ขึ้นบรรทัด และตัวอักษรที่ไม่อยู่บนแป้นพิมพ์
            English,   // มีบนแป้นอังกฤษเท่านั้น เช่น a-z 0-9 ; [ '
            Thai,      // อักษรไทย
            Either     // มีบนทั้งสองแป้น เช่น / - . , ( ) — ตามช่วงที่อยู่ติดกัน
        }

        private static CharKind KindOf(char c)
        {
            bool english = EnToTh.ContainsKey(c);
            bool thai = ThToEn.ContainsKey(c) || IsThai(c);
            if (english && thai) return CharKind.Either;
            if (english) return CharKind.English;
            if (thai) return CharKind.Thai;
            return CharKind.Separator;
        }

        private struct Segment
        {
            public int Start;
            public int Length;
            public CharKind Kind;
        }

        /// <summary>
        /// แก้ข้อความที่พิมพ์ผิดแป้น โดยแบ่งเป็นช่วงตามชนิดตัวอักษร แล้วแปลงแต่ละช่วงไปอีกภาษา
        /// จึงแก้ข้อความที่ผิดสลับกันได้ เช่น "py',u ฺีเ vp^j" → "ยังมี Bug อยู่"
        /// ช่วงที่อ่านได้ถูกต้องอยู่แล้ว และจะอ่านไม่ออกถ้าแปลง จะถูกเก็บไว้ตามเดิม
        /// ถ้าไม่มีช่วงไหนต้องแปลงเลย จะแปลงทั้งหมด (ผู้ใช้กดคีย์ลัดซ้ำเพื่อสลับกลับได้)
        /// </summary>
        internal static string Fix(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            List<Segment> segments = Split(text);
            string[] originals = new string[segments.Count];
            string[] flipped = new string[segments.Count];
            bool[] flip = new bool[segments.Count];
            bool anyFlip = false;

            for (int i = 0; i < segments.Count; i++)
            {
                Segment s = segments[i];
                originals[i] = text.Substring(s.Start, s.Length);
                flipped[i] = originals[i];
                if (s.Kind != CharKind.English && s.Kind != CharKind.Thai) continue;

                bool toThai = s.Kind == CharKind.English;
                flipped[i] = Convert(originals[i], toThai ? ConversionDirection.EnglishToThai : ConversionDirection.ThaiToEnglish);
                bool looksRight = toThai ? IsPlausibleEnglish(originals[i]) : IsPlausibleThai(originals[i]);
                bool wouldLookWrong = !(toThai ? IsPlausibleThai(flipped[i]) : IsPlausibleEnglish(flipped[i]));
                flip[i] = !(looksRight && wouldLookWrong);
                anyFlip |= flip[i];
            }

            StringBuilder sb = new StringBuilder(text.Length);
            for (int i = 0; i < segments.Count; i++)
            {
                sb.Append(flip[i] || !anyFlip ? flipped[i] : originals[i]);
            }
            return sb.ToString();
        }

        /// <summary>แบ่งข้อความเป็นช่วงของตัวอักษรชนิดเดียวกัน คั่นด้วยช่องว่าง หรือจุดที่เปลี่ยนจากไทยเป็นอังกฤษ</summary>
        private static List<Segment> Split(string text)
        {
            List<Segment> segments = new List<Segment>();
            int i = 0;
            while (i < text.Length)
            {
                Segment s = new Segment();
                s.Start = i;
                if (KindOf(text[i]) == CharKind.Separator)
                {
                    while (i < text.Length && KindOf(text[i]) == CharKind.Separator) i++;
                    s.Kind = CharKind.Separator;
                }
                else
                {
                    // ตัวที่อยู่บนทั้งสองแป้น (เช่น , . -) อยู่ช่วงเดียวกับตัวก่อนหน้า หรือตัวถัดไปถ้าอยู่ต้นช่วง
                    s.Kind = CharKind.Either;
                    while (i < text.Length)
                    {
                        CharKind kind = KindOf(text[i]);
                        if (kind == CharKind.Separator) break;
                        if (kind != CharKind.Either)
                        {
                            if (s.Kind == CharKind.Either) s.Kind = kind;
                            else if (kind != s.Kind) break;
                        }
                        i++;
                    }
                }
                s.Length = i - s.Start;
                segments.Add(s);
            }
            return segments;
        }

        /// <summary>ภาษาของตัวอักษรตัวท้ายสุด ใช้เลือกแป้นพิมพ์ให้พิมพ์ต่อได้ทันที</summary>
        internal static TextLanguage LanguageAtEnd(string text)
        {
            if (text == null) return TextLanguage.None;
            for (int i = text.Length - 1; i >= 0; i--)
            {
                if (IsThai(text[i])) return TextLanguage.Thai;
                if (IsLatinLetter(text[i])) return TextLanguage.English;
            }
            return TextLanguage.None;
        }

        /// <summary>ไม่มีลักษณะของภาษาไทยที่ถูกพิมพ์ด้วยแป้นอังกฤษ (ตัดสินแบบผ่อนปรน)</summary>
        internal static bool IsPlausibleEnglish(string text)
        {
            return !EnglishGarbage.IsMatch(text);
        }

        /// <summary>
        /// เขียนถูกตามหลักการวางสระและวรรณยุกต์ไทยหรือไม่ เช่น สระบน/ล่างและวรรณยุกต์ต้องตามหลังพยัญชนะ
        /// สระหน้าต้องตามด้วยพยัญชนะ ข้อความอังกฤษที่ถูกพิมพ์ด้วยแป้นไทยมักผิดหลักนี้ เช่น "ฺีเ" (Bug), "้ำสสน" (hello)
        /// </summary>
        internal static bool IsPlausibleThai(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                char p = i > 0 ? text[i - 1] : '\0';
                switch (c)
                {
                    case 'เ': case 'แ': case 'โ': case 'ใ': case 'ไ':
                        if (i + 1 >= text.Length || !IsThaiConsonant(text[i + 1])) return false;
                        break;
                    case 'ั': // ั
                    case 'ิ': case 'ี': case 'ึ': case 'ื': // ิ ี ึ ื
                    case 'ุ': case 'ู': case 'ฺ': // ุ ู ฺ
                    case '็': case 'ํ': case '๎': // ็ ํ ๎
                        if (!IsThaiConsonant(p)) return false;
                        break;
                    case '่': case '้': case '๊': case '๋': // ่ ้ ๊ ๋
                        if (!IsThaiConsonant(p) && p != 'ั' && !(p >= 'ิ' && p <= 'ู')) return false;
                        break;
                    case '์': // ์
                        if (!IsThaiConsonant(p) && p != 'ิ' && p != 'ุ') return false;
                        break;
                    case 'ะ':
                        if (!IsThaiConsonant(p) && !IsThaiTone(p) && p != 'า') return false;
                        break;
                    case 'า': case 'ำ':
                        if (!IsThaiConsonant(p) && !IsThaiTone(p) && p != 'ํ') return false;
                        break;
                    case 'ๅ':
                        if (p != 'ฤ' && p != 'ฦ') return false;
                        break;
                }
            }
            return true;
        }

        private static bool IsThaiConsonant(char c)
        {
            return c >= 'ก' && c <= 'ฮ';
        }

        private static bool IsThaiTone(char c)
        {
            return c >= '่' && c <= '๋';
        }
    }
}
