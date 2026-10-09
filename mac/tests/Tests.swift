// ชุดทดสอบเล็กๆ ไม่ต้องพึ่ง framework ภายนอก — สั่งรัน: mac/build.sh test
import Foundation

@main
enum Tests {
    static var passed = 0
    static var failed = 0

    static func main() {
        // ---- แปลงอังกฤษ → ไทย ----
        expectConvert("l;ylfu;yoouh;yo0yomiN", "สวัสดีวันนี้วันจันทร์", .englishToThai)
        expectConvert("8iy[", "ครับ", .englishToThai)
        expectConvert("-v[86I", "ขอบคุณ", .englishToThai)
        expectConvert("123", "ๅ/_", .englishToThai) // แป้นเกษมณีของ macOS: ปุ่ม 3 ได้ "_"
        expectConvert("wmp\r\nl;ylfu", "ไทย\r\nสวัสดี", .englishToThai)

        // ---- แปลงไทย → อังกฤษ ----
        expectConvert("เนนก", "good", .thaiToEnglish)
        expectConvert("เนนก ทนพืรืเ", "good morning", .thaiToEnglish)
        expectConvert("้ำสสน ไนพสก", "hello world", .thaiToEnglish)
        expectConvert("ๅ/_", "123", .thaiToEnglish)
        expectConvert("ฉันชอบ", "Cyo=v[", .thaiToEnglish)
        expectConvert("กิ่ง", "dbj'", .thaiToEnglish) // สระ/วรรณยุกต์ต้องแปลงแยกตัว ไม่ใช่ทั้งกลุ่ม

        // ---- ไม่มีอะไรต้องแปลง ----
        expectConvert("", "", .none)
        expectConvert("   \r\n\t", "   \r\n\t", .none)

        // ---- ตารางแป้นต้องจับคู่กันครบ 1:1 และแปลงไป-กลับแล้วได้ค่าเดิม ----
        let standard = LayoutConverter.standard
        expect(standard.keyCount == 94, "keyboard table has 94 entries")
        let ascii = String(String.UnicodeScalarView((33...126).compactMap(Unicode.Scalar.init)))
        let thai = standard.convert(ascii, .englishToThai)
        expect(standard.detectDirection(thai) == .thaiToEnglish, "detects Thai side of full table")
        expect(standard.convert(thai, .thaiToEnglish) == ascii, "round trip of every printable ASCII char")
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

    static func expectConvert(_ input: String, _ expected: String, _ expectedDirection: ConversionDirection) {
        let (actual, direction) = LayoutConverter.standard.convert(input)
        expect(actual == expected && direction == expectedDirection,
               "convert \"\(escape(input))\" -> \"\(escape(expected))\" (\(expectedDirection)), " +
               "got \"\(escape(actual))\" (\(direction))")
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
