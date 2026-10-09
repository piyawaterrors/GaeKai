// ชุดทดสอบเล็กๆ ไม่ต้องพึ่ง framework ภายนอก — สั่งรัน: mac/build.sh test
import Foundation

@main
enum Tests {
    static var passed = 0
    static var failed = 0

    static func main() {
        // ---- แปลงอังกฤษ → ไทย ----
        expectFix("l;ylfu;yoouh;yo0yomiN", "สวัสดีวันนี้วันจันทร์")
        expectFix("8iy[", "ครับ")
        expectFix("-v[86I", "ขอบคุณ")
        expectFix("123", "ๅ/_") // แป้นเกษมณีของ macOS: ปุ่ม 3 ได้ "_"
        expectFix("wmp\r\nl;ylfu", "ไทย\r\nสวัสดี")

        // ---- แปลงไทย → อังกฤษ ----
        expectFix("เนนก", "good")
        expectFix("เนนก ทนพืรืเ", "good morning")
        expectFix("้ำสสน ไนพสก", "hello world")
        expectFix("ๅ/_", "123")
        expectFix("ฉันชอบ", "Cyo=v[")
        expectFix("กิ่ง", "dbj'") // สระ/วรรณยุกต์ต้องแปลงแยกตัว ไม่ใช่ทั้งกลุ่ม

        // ---- ผิดสลับกันในข้อความเดียว: แปลงแต่ละช่วงไปคนละทาง ----
        expectFix("py',u ฺีเ vp^j =j;p9i;0lv[.shsojvp", "ยังมี Bug อยู่ ช่วยตรวจสอบให้หน่อย")
        expectFix("ฺีเvp^j", "Bugอยู่")
        expectFix("l;ylfu ้ำสสน", "สวัสดี hello")

        // ---- คลุมเกินมาถึงส่วนที่พิมพ์ถูกแล้ว: ส่วนที่ถูกอยู่แล้วไม่ถูกแปลง ----
        expectFix("l;ylfu;yoouh ครับ", "สวัสดีวันนี้ ครับ")
        expectFix("Hello l;ylfu", "Hello สวัสดี")
        expectFix("สวัสดีครับ ้ำสสน", "สวัสดีครับ hello")

        // ---- ไม่มีอะไรต้องแปลง ----
        expectFix("", "")
        expectFix("   \r\n\t", "   \r\n\t")

        // ---- ตรวจรูปแบบภาษา ----
        for word in ["สวัสดี", "ครับ", "ค่ะ", "น้ำ", "ข้าว", "เกาะ", "จ๊ะ", "เก็บ", "สิทธิ์", "ฤๅษี", "ไม่", "ใช่", "เป็น",
                     "กิ่ง", "แล้ว", "ทั้งนี้", "เดี๋ยว", "เกี๊ยะ", "อำนาจ", "ทํา", "จันทร์", "ดีๆ", "ฯลฯ", "พ.ศ.", "๑๒๓", "คน"] {
            expect(LayoutConverter.isPlausibleThai(word), "plausible Thai: " + word)
        }
        for garbage in ["ฺีเ", "้ำสสน", "ทนพืรืเ", "ๅ/-", "ะำหะ", "บสรืาล"] {
            expect(!LayoutConverter.isPlausibleThai(garbage), "implausible Thai: " + garbage)
        }
        for word in ["Bug", "hello", "don't", "it's", "students'", "e.g.", "3rd", "C++", "v1.0", "a/b",
                     "well-known", "user@example.com", "(note)", "Hello,"] {
            expect(LayoutConverter.isPlausibleEnglish(word), "plausible English: " + word)
        }
        for garbage in ["l;ylfu", "py',u", "vp^j", "8iy[", ".sh", "w,j", "8o", "py'", "=j;p9i"] {
            expect(!LayoutConverter.isPlausibleEnglish(garbage), "implausible English: " + garbage)
        }

        // ---- ภาษาท้ายข้อความ (ใช้เลือกแป้นพิมพ์หลังแปลง) ----
        expect(LayoutConverter.languageAtEnd("ยังมี Bug อยู่") == .thai, "language at end: Thai")
        expect(LayoutConverter.languageAtEnd("อยู่ Bug!") == .english, "language at end: English")
        expect(LayoutConverter.languageAtEnd("123 ...") == TextLanguage.none, "language at end: none")

        // ---- ตารางแป้นต้องจับคู่กันครบ 1:1 และแปลงไป-กลับแล้วได้ค่าเดิม ----
        let standard = LayoutConverter.standard
        expect(standard.keyCount == 94, "keyboard table has 94 entries")
        let ascii = String(String.UnicodeScalarView((33...126).compactMap(Unicode.Scalar.init)))
        let thai = standard.convert(ascii, .englishToThai)
        expect(standard.convert(thai, .thaiToEnglish) == ascii, "round trip of every printable ASCII char")
        expect(standard.fix(thai) == ascii && standard.fix(ascii) == thai, "fix flips the full table both ways")
        expect(standard.convert("The quick brown fox!", .englishToThai) != "The quick brown fox!", "English sentence changes")

        // ---- ตารางที่อ่านจากแป้นของ macOS เอง ต้องตรงกับตารางในโค้ด ----
        if let us = KeyboardLayouts.installedSource(id: "com.apple.keylayout.US"),
           let kedmanee = KeyboardLayouts.installedSource(id: "com.apple.keylayout.Thai"),
           let usKeys = KeyboardLayouts.characters(of: us),
           let thaiKeys = KeyboardLayouts.characters(of: kedmanee) {
            let fromSystem = String(String.UnicodeScalarView(thaiKeys.compactMap { $0 }))
            expect(usKeys.compactMap({ $0 }).count == 94 && String(String.UnicodeScalarView(usKeys.compactMap { $0 })) ==
                   LayoutConverter.englishKeys, "macOS US layout matches built-in table")
            expect(fromSystem == LayoutConverter.kedmaneeKeys, "macOS Thai layout matches built-in Kedmanee table")
        } else {
            expect(false, "US and Thai layouts are installed")
        }

        if let us = KeyboardLayouts.installedSource(id: "com.apple.keylayout.US"),
           let pattachote = KeyboardLayouts.installedSource(id: "com.apple.keylayout.Thai-PattaChote"),
           let usKeys = KeyboardLayouts.characters(of: us),
           let thaiKeys = KeyboardLayouts.characters(of: pattachote) {
            var pairs: [(Unicode.Scalar, Unicode.Scalar)] = []
            for (e, t) in zip(usKeys, thaiKeys) {
                if let e = e, let t = t { pairs.append((e, t)) }
            }
            let converter = LayoutConverter(pairs: pairs)
            let typed = converter.convert("hello", .englishToThai)
            expect(converter.keyCount >= 60, "Pattachote table has enough keys (\(converter.keyCount))")
            expect(typed != "hello" && converter.convert(typed, .thaiToEnglish) == "hello",
                   "Pattachote round trip: hello -> \(typed)")
        }

        // ---- คีย์ลัด ----
        expectHotkey("Ctrl+Shift+Space", "Ctrl + Shift + Space")
        expectHotkey("ctrl + alt + k", "Ctrl + Option + K")
        expectHotkey("Cmd+Shift+;", "Shift + Cmd + ;")
        expectHotkey("⌃⌥ + K", nil) // ต้องคั่นด้วย +
        expectHotkey("⌃ + ⌥ + K", "Ctrl + Option + K")
        expectHotkey("Option+1", "Option + 1")
        expectHotkey("Ctrl+Num5", "Ctrl + Num5")
        expectHotkey("Ctrl+=", "Ctrl + =")
        expectHotkey("Ctrl+Shift+Return", "Ctrl + Shift + Return")
        expectHotkey("F9", "F9")
        expectHotkey("A", nil)            // ต้องมีปุ่มกดร่วม
        expectHotkey("Ctrl+Shift", nil)   // ไม่มีปุ่มหลัก
        expectHotkey("Ctrl+Foo", nil)
        expectHotkey("Hyper+K", nil)
        expectHotkey("", nil)
        expect(Hotkey.parse(Hotkey.default.description) == Hotkey.default, "default hotkey round trip")
        expect(Hotkey.default.symbols == "⌃⇧Space", "default hotkey symbols")

        print("")
        print("\(passed) passed, \(failed) failed")
        exit(failed == 0 ? 0 : 1)
    }

    static func expectFix(_ input: String, _ expected: String) {
        let actual = LayoutConverter.standard.fix(input)
        expect(actual == expected, "fix \"\(escape(input))\" -> \"\(escape(expected))\", got \"\(escape(actual))\"")
    }

    static func expectHotkey(_ text: String, _ expected: String?) {
        let actual = Hotkey.parse(text)?.description
        expect(actual == expected, "hotkey \"\(text)\" -> \"\(expected ?? "(invalid)")\", got \"\(actual ?? "(invalid)")\"")
    }

    static func expect(_ condition: Bool, _ description: String) {
        if condition {
            passed += 1
            print("  ok    " + description)
        } else {
            failed += 1
            print("  FAIL  " + description)
        }
    }

    static func escape(_ s: String) -> String {
        return s.replacingOccurrences(of: "\r", with: "\\r").replacingOccurrences(of: "\n", with: "\\n")
            .replacingOccurrences(of: "\t", with: "\\t")
    }
}
