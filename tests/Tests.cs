// ชุดทดสอบเล็กๆ ไม่ต้องพึ่ง framework ภายนอก — สั่งรัน: build.bat test
using System;
using System.Windows.Forms;

namespace GaeKai.Tests
{
    internal static class Tests
    {
        private static int passed, failed;

        private static int Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // ---- แปลงอังกฤษ → ไทย ----
            ExpectFix("l;ylfu;yoouh;yo0yomiN", "สวัสดีวันนี้วันจันทร์");
            ExpectFix("8iy[", "ครับ");
            ExpectFix("-v[86I", "ขอบคุณ");
            ExpectFix("123", "ๅ/-");
            ExpectFix("wmp\r\nl;ylfu", "ไทย\r\nสวัสดี");

            // ---- แปลงไทย → อังกฤษ ----
            ExpectFix("เนนก", "good");
            ExpectFix("เนนก ทนพืรืเ", "good morning");
            ExpectFix("้ำสสน ไนพสก", "hello world");
            ExpectFix("ๅ/-", "123");
            ExpectFix("ฉันชอบ", "Cyo=v[");

            // ---- ผิดสลับกันในข้อความเดียว: แปลงแต่ละช่วงไปคนละทาง ----
            ExpectFix("py',u ฺีเ vp^j =j;p9i;0lv[.shsojvp", "ยังมี Bug อยู่ ช่วยตรวจสอบให้หน่อย");
            ExpectFix("ฺีเvp^j", "Bugอยู่");
            ExpectFix("l;ylfu ้ำสสน", "สวัสดี hello");

            // ---- คลุมเกินมาถึงส่วนที่พิมพ์ถูกแล้ว: ส่วนที่ถูกอยู่แล้วไม่ถูกแปลง ----
            ExpectFix("l;ylfu;yoouh ครับ", "สวัสดีวันนี้ ครับ");
            ExpectFix("Hello l;ylfu", "Hello สวัสดี");
            ExpectFix("สวัสดีครับ ้ำสสน", "สวัสดีครับ hello");

            // ---- ไม่มีอะไรต้องแปลง ----
            ExpectFix("", "");
            ExpectFix("   \r\n\t", "   \r\n\t");

            // ---- ตรวจรูปแบบภาษา ----
            foreach (string word in new[] { "สวัสดี", "ครับ", "ค่ะ", "น้ำ", "ข้าว", "เกาะ", "จ๊ะ", "เก็บ", "สิทธิ์", "ฤๅษี",
                                            "ไม่", "ใช่", "เป็น", "กิ่ง", "แล้ว", "ทั้งนี้", "เดี๋ยว", "เกี๊ยะ", "อำนาจ", "ทํา",
                                            "จันทร์", "ดีๆ", "ฯลฯ", "พ.ศ.", "๑๒๓", "คน" })
            {
                Expect(LayoutConverter.IsPlausibleThai(word), "plausible Thai: " + word);
            }
            foreach (string garbage in new[] { "ฺีเ", "้ำสสน", "ทนพืรืเ", "ๅ/-", "ะำหะ", "บสรืาล" })
            {
                Expect(!LayoutConverter.IsPlausibleThai(garbage), "implausible Thai: " + garbage);
            }
            foreach (string word in new[] { "Bug", "hello", "don't", "it's", "students'", "e.g.", "3rd", "C++", "v1.0", "a/b",
                                            "well-known", "user@example.com", "(note)", "Hello," })
            {
                Expect(LayoutConverter.IsPlausibleEnglish(word), "plausible English: " + word);
            }
            foreach (string garbage in new[] { "l;ylfu", "py',u", "vp^j", "8iy[", ".sh", "w,j", "8o", "py'", "=j;p9i" })
            {
                Expect(!LayoutConverter.IsPlausibleEnglish(garbage), "implausible English: " + garbage);
            }

            // ---- ภาษาท้ายข้อความ (ใช้เลือกแป้นพิมพ์หลังแปลง) ----
            Expect(LayoutConverter.LanguageAtEnd("ยังมี Bug อยู่") == TextLanguage.Thai, "language at end: Thai");
            Expect(LayoutConverter.LanguageAtEnd("อยู่ Bug!") == TextLanguage.English, "language at end: English");
            Expect(LayoutConverter.LanguageAtEnd("123 ...") == TextLanguage.None, "language at end: none");

            // ---- ตารางแป้นต้องจับคู่กันครบ 1:1 และแปลงไป-กลับแล้วได้ค่าเดิม ----
            Expect(LayoutConverter.KeyCount == 94, "keyboard table has 94 entries");
            string ascii = "";
            for (char c = '!'; c <= '~'; c++) ascii += c;
            string thai = LayoutConverter.Convert(ascii, ConversionDirection.EnglishToThai);
            Expect(LayoutConverter.Convert(thai, ConversionDirection.ThaiToEnglish) == ascii, "round trip of every printable ASCII char");
            Expect(LayoutConverter.Fix(thai) == ascii && LayoutConverter.Fix(ascii) == thai, "Fix flips the full table both ways");
            Expect(LayoutConverter.Convert("The quick brown fox!", ConversionDirection.EnglishToThai) != "The quick brown fox!",
                "English sentence changes");

            // ---- คีย์ลัด ----
            ExpectHotkey("Ctrl+Shift+Space", "Ctrl + Shift + Space");
            ExpectHotkey("ctrl + alt + k", "Ctrl + Alt + K");
            ExpectHotkey("Win+Shift+;", "Shift + Win + ;");
            ExpectHotkey("Alt+1", "Alt + 1");
            ExpectHotkey("Ctrl+Num5", "Ctrl + Num5");
            ExpectHotkey("Ctrl+=", "Ctrl + =");
            ExpectHotkey("Ctrl+Shift+Enter", "Ctrl + Shift + Enter");
            ExpectHotkey("F9", "F9");
            ExpectHotkey("Pause", "Pause");
            ExpectHotkey("A", null);              // ต้องมีปุ่มกดร่วม
            ExpectHotkey("Ctrl+Shift", null);     // ไม่มีปุ่มหลัก
            ExpectHotkey("Ctrl+Foo", null);
            ExpectHotkey("Hyper+K", null);
            ExpectHotkey("", null);
            Expect(Hotkey.Parse(Hotkey.Default.ToString()).ToString() == Hotkey.Default.ToString(), "default hotkey round trip");
            Expect(new Hotkey(NativeMethods.MOD_CONTROL, Keys.ShiftKey).IsValid == false, "modifier-only key is invalid");

            Console.WriteLine();
            Console.WriteLine("{0} passed, {1} failed", passed, failed);
            return failed == 0 ? 0 : 1;
        }

        private static void ExpectFix(string input, string expected)
        {
            string actual = LayoutConverter.Fix(input);
            Expect(actual == expected,
                string.Format("fix \"{0}\" -> \"{1}\", got \"{2}\"", Escape(input), Escape(expected), Escape(actual)));
        }

        private static void ExpectHotkey(string text, string expected)
        {
            Hotkey hotkey = Hotkey.Parse(text);
            string actual = hotkey == null ? null : hotkey.ToString();
            Expect(actual == expected,
                string.Format("hotkey \"{0}\" -> \"{1}\", got \"{2}\"", text, expected ?? "(invalid)", actual ?? "(invalid)"));
        }

        private static void Expect(bool condition, string description)
        {
            if (condition)
            {
                passed++;
                Console.WriteLine("  ok    " + description);
            }
            else
            {
                failed++;
                Console.WriteLine("  FAIL  " + description);
            }
        }

        private static string Escape(string s)
        {
            return s.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }
    }
}
