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
            ExpectConvert("l;ylfu;yoouh;yo0yomiN", "สวัสดีวันนี้วันจันทร์", ConversionDirection.EnglishToThai);
            ExpectConvert("8iy[", "ครับ", ConversionDirection.EnglishToThai);
            ExpectConvert("-v[86I", "ขอบคุณ", ConversionDirection.EnglishToThai);
            ExpectConvert("123", "ๅ/-", ConversionDirection.EnglishToThai);
            ExpectConvert("wmp\r\nl;ylfu", "ไทย\r\nสวัสดี", ConversionDirection.EnglishToThai);

            // ---- แปลงไทย → อังกฤษ ----
            ExpectConvert("เนนก", "good", ConversionDirection.ThaiToEnglish);
            ExpectConvert("เนนก ทนพืรืเ", "good morning", ConversionDirection.ThaiToEnglish);
            ExpectConvert("้ำสสน ไนพสก", "hello world", ConversionDirection.ThaiToEnglish);
            ExpectConvert("ๅ/-", "123", ConversionDirection.ThaiToEnglish);
            ExpectConvert("ฉันชอบ", "Cyo=v[", ConversionDirection.ThaiToEnglish);

            // ---- ไม่มีอะไรต้องแปลง ----
            ExpectConvert("", "", ConversionDirection.None);
            ExpectConvert("   \r\n\t", "   \r\n\t", ConversionDirection.None);

            // ---- ตารางแป้นต้องจับคู่กันครบ 1:1 และแปลงไป-กลับแล้วได้ค่าเดิม ----
            Expect(LayoutConverter.KeyCount == 94, "keyboard table has 94 entries");
            string ascii = "";
            for (char c = '!'; c <= '~'; c++) ascii += c;
            string thai = LayoutConverter.Convert(ascii, ConversionDirection.EnglishToThai);
            Expect(LayoutConverter.DetectDirection(thai) == ConversionDirection.ThaiToEnglish, "detects Thai side of full table");
            Expect(LayoutConverter.Convert(thai, ConversionDirection.ThaiToEnglish) == ascii, "round trip of every printable ASCII char");
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

        private static void ExpectConvert(string input, string expected, ConversionDirection expectedDirection)
        {
            ConversionDirection direction;
            string actual = LayoutConverter.Convert(input, out direction);
            Expect(actual == expected && direction == expectedDirection,
                string.Format("convert \"{0}\" -> \"{1}\" ({2}), got \"{3}\" ({4})",
                    Escape(input), Escape(expected), expectedDirection, Escape(actual), direction));
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
