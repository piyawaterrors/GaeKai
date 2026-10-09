import Foundation

enum ConversionDirection {
    case none
    case englishToThai
    case thaiToEnglish
}

enum TextLanguage {
    case none
    case thai
    case english
}

/// แปลงข้อความที่พิมพ์ผิดแป้น ระหว่างแป้นอังกฤษกับแป้นไทย
///
/// ทำงานทีละ Unicode scalar ไม่ใช่ทีละ Character เพราะ Swift รวมสระ/วรรณยุกต์ไทยเข้ากับพยัญชนะ
/// (เช่น "กิ่" เป็น Character เดียว) และรวม "\r\n" เป็นตัวเดียว ซึ่งจะทำให้แปลงผิด
struct LayoutConverter {
    // แต่ละตำแหน่งของสองสตริงนี้คือปุ่มเดียวกันบนคีย์บอร์ด (47 ปุ่ม x ปกติ/Shift = 94 ตัว)
    static let englishKeys =
        "`1234567890-=" + "qwertyuiop[]\\" + "asdfghjkl;'" + "zxcvbnm,./" +
        "~!@#$%^&*()_+" + "QWERTYUIOP{}|" + "ASDFGHJKL:\"" + "ZXCVBNM<>?"

    // แป้นเกษมณีของ macOS ต่างจากของ Windows 2 ปุ่ม: ปุ่ม ` ได้ "-" และปุ่ม 3 ได้ "_" (Windows กลับกัน)
    static let kedmaneeKeys =
        "-ๅ/_ภถุึคตจขช" + "ๆไำพะัีรนยบลฃ" + "ฟหกดเ้่าสวง" + "ผปแอิืทมใฝ" +
        "%+๑๒๓๔ู฿๕๖๗๘๙" + "๐\"ฎฑธํ๊ณฯญฐ,ฅ" + "ฤฆฏโฌ็๋ษศซ." + "()ฉฮฺ์?ฒฬฦ"

    /// ตารางสำรอง US ⇄ เกษมณี ใช้เมื่ออ่านแป้นพิมพ์ที่ผู้ใช้เปิดไว้ไม่ได้
    static let standard = LayoutConverter(pairs: Array(zip(englishKeys.unicodeScalars, kedmaneeKeys.unicodeScalars)))

    private var enToTh: [Unicode.Scalar: Unicode.Scalar] = [:]
    private var thToEn: [Unicode.Scalar: Unicode.Scalar] = [:]

    /// สร้างตารางจากคู่ (ตัวอักษรแป้นอังกฤษ, ตัวอักษรแป้นไทย) ของปุ่มเดียวกัน
    /// ข้ามปุ่มที่ให้ตัวอักษรซ้ำกับปุ่มอื่น เพื่อให้แปลงไป-กลับแล้วได้ค่าเดิมเสมอ
    init(pairs: [(Unicode.Scalar, Unicode.Scalar)]) {
        for (english, thai) in pairs where english != thai {
            if enToTh[english] != nil || thToEn[thai] != nil { continue }
            enToTh[english] = thai
            thToEn[thai] = english
        }
    }

    var keyCount: Int {
        return enToTh.count
    }

    static func isThai(_ c: Unicode.Scalar) -> Bool {
        return c.value >= 0x0E01 && c.value <= 0x0E5B
    }

    private static func isLatinLetter(_ c: Unicode.Scalar) -> Bool {
        return (c >= "a" && c <= "z") || (c >= "A" && c <= "Z")
    }

    func convert(_ text: String, _ direction: ConversionDirection) -> String {
        let map: [Unicode.Scalar: Unicode.Scalar]
        switch direction {
        case .none: return text
        case .englishToThai: map = enToTh
        case .thaiToEnglish: map = thToEn
        }

        var result = String.UnicodeScalarView()
        for c in text.unicodeScalars {
            result.append(map[c] ?? c)
        }
        return String(result)
    }

    // ---- แปลงทีละช่วง (ตรรกะเดียวกับ src/LayoutConverter.cs ของ Windows) ----

    private enum CharKind {
        case separator // ช่องว่าง ขึ้นบรรทัด และตัวอักษรที่ไม่อยู่บนแป้นพิมพ์
        case english   // มีบนแป้นอังกฤษเท่านั้น เช่น a-z 0-9 ; [ '
        case thai      // อักษรไทย
        case either    // มีบนทั้งสองแป้น เช่น / - . , ( ) — ตามช่วงที่อยู่ติดกัน
    }

    private func kind(of c: Unicode.Scalar) -> CharKind {
        let english = enToTh[c] != nil
        let thai = thToEn[c] != nil || LayoutConverter.isThai(c)
        if english && thai { return .either }
        if english { return .english }
        if thai { return .thai }
        return .separator
    }

    /// แก้ข้อความที่พิมพ์ผิดแป้น โดยแบ่งเป็นช่วงตามชนิดตัวอักษร แล้วแปลงแต่ละช่วงไปอีกภาษา
    /// จึงแก้ข้อความที่ผิดสลับกันได้ เช่น "py',u ฺีเ vp^j" → "ยังมี Bug อยู่"
    /// ช่วงที่อ่านได้ถูกต้องอยู่แล้ว และจะอ่านไม่ออกถ้าแปลง จะถูกเก็บไว้ตามเดิม
    /// ถ้าไม่มีช่วงไหนต้องแปลงเลย จะแปลงทั้งหมด (ผู้ใช้กดคีย์ลัดซ้ำเพื่อสลับกลับได้)
    func fix(_ text: String) -> String {
        let segments = split(text)
        var flips: [Bool] = []
        var flippedTexts: [String] = []

        for segment in segments {
            guard segment.kind == .english || segment.kind == .thai else {
                flips.append(false)
                flippedTexts.append(segment.text)
                continue
            }
            let toThai = segment.kind == .english
            let flipped = convert(segment.text, toThai ? .englishToThai : .thaiToEnglish)
            let looksRight = toThai ? LayoutConverter.isPlausibleEnglish(segment.text)
                                    : LayoutConverter.isPlausibleThai(segment.text)
            let wouldLookWrong = !(toThai ? LayoutConverter.isPlausibleThai(flipped)
                                          : LayoutConverter.isPlausibleEnglish(flipped))
            flips.append(!(looksRight && wouldLookWrong))
            flippedTexts.append(flipped)
        }

        let anyFlip = flips.contains(true)
        var result = ""
        for (i, segment) in segments.enumerated() {
            result += (flips[i] || !anyFlip) ? flippedTexts[i] : segment.text
        }
        return result
    }

    /// แบ่งข้อความเป็นช่วงของตัวอักษรชนิดเดียวกัน คั่นด้วยช่องว่าง หรือจุดที่เปลี่ยนจากไทยเป็นอังกฤษ
    private func split(_ text: String) -> [(text: String, kind: CharKind)] {
        let scalars = Array(text.unicodeScalars)
        var segments: [(text: String, kind: CharKind)] = []
        var i = 0
        while i < scalars.count {
            let start = i
            var segmentKind = CharKind.separator
            if kind(of: scalars[i]) == .separator {
                while i < scalars.count && kind(of: scalars[i]) == .separator { i += 1 }
            } else {
                // ตัวที่อยู่บนทั้งสองแป้น (เช่น , . -) อยู่ช่วงเดียวกับตัวก่อนหน้า หรือตัวถัดไปถ้าอยู่ต้นช่วง
                segmentKind = .either
                while i < scalars.count {
                    let k = kind(of: scalars[i])
                    if k == .separator { break }
                    if k != .either {
                        if segmentKind == .either {
                            segmentKind = k
                        } else if k != segmentKind {
                            break
                        }
                    }
                    i += 1
                }
            }
            segments.append((text: String(String.UnicodeScalarView(scalars[start..<i])), kind: segmentKind))
        }
        return segments
    }

    /// ภาษาของตัวอักษรตัวท้ายสุด ใช้เลือกแป้นพิมพ์ให้พิมพ์ต่อได้ทันที
    static func languageAtEnd(_ text: String) -> TextLanguage {
        for c in text.unicodeScalars.reversed() {
            if isThai(c) { return .thai }
            if isLatinLetter(c) { return .english }
        }
        return .none
    }

    // ลักษณะที่พบในข้อความภาษาไทยที่ถูกพิมพ์ด้วยแป้นอังกฤษ แต่แทบไม่พบในภาษาอังกฤษจริง
    // เช่น "l;ylfu" (สวัสดี), "py',u" (ยังมี), "vp^j" (อยู่), "8iy[" (ครับ)
    private static let englishGarbage = try! NSRegularExpression(
        pattern: #"[a-z][\[\]\\;^=~`|\{\}<>]|[\[\]\\;^=~`|\{\}<>][a-z]"# // สัญลักษณ์ติดกับตัวอักษร
            + #"|[a-z],[a-z0-9]"#                                        // จุลภาคกลางคำ
            + #"|(^|[^a-z0-9.])[.,][a-z]"#                               // คำที่ขึ้นต้นด้วย . หรือ ,
            + #"|[0-9](?!(st|nd|rd|th)\b)[a-z]"#                         // ตัวเลขตามด้วยตัวอักษร (ยกเว้น 1st 2nd 3rd 4th)
            + #"|[a-rt-z]'(?![a-z])"#                                    // ' ท้ายคำ (ยกเว้น students')
            + #"|[a-z]'(?!(s|t|d|m|re|ll|ve)\b)[a-z]"#,                  // ' กลางคำที่ไม่ใช่ don't, it's, we're ...
        options: [.caseInsensitive])

    /// ไม่มีลักษณะของภาษาไทยที่ถูกพิมพ์ด้วยแป้นอังกฤษ (ตัดสินแบบผ่อนปรน)
    static func isPlausibleEnglish(_ text: String) -> Bool {
        let range = NSRange(text.startIndex..., in: text)
        return englishGarbage.firstMatch(in: text, options: [], range: range) == nil
    }

    /// เขียนถูกตามหลักการวางสระและวรรณยุกต์ไทยหรือไม่ เช่น สระบน/ล่างและวรรณยุกต์ต้องตามหลังพยัญชนะ
    /// สระหน้าต้องตามด้วยพยัญชนะ ข้อความอังกฤษที่ถูกพิมพ์ด้วยแป้นไทยมักผิดหลักนี้ เช่น "ฺีเ" (Bug), "้ำสสน" (hello)
    static func isPlausibleThai(_ text: String) -> Bool {
        let s = text.unicodeScalars.map { $0.value }
        for i in s.indices {
            let p: UInt32 = i > 0 ? s[i - 1] : 0
            switch s[i] {
            case 0x0E40...0x0E44: // สระหน้า เ แ โ ใ ไ
                if i + 1 >= s.count || !isThaiConsonant(s[i + 1]) { return false }
            case 0x0E31, 0x0E34...0x0E3A, 0x0E47, 0x0E4D, 0x0E4E: // ั ิ ี ึ ื ุ ู ฺ ็ ํ ๎
                if !isThaiConsonant(p) { return false }
            case 0x0E48...0x0E4B: // ่ ้ ๊ ๋
                if !isThaiConsonant(p) && p != 0x0E31 && !(0x0E34...0x0E39).contains(p) { return false }
            case 0x0E4C: // ์
                if !isThaiConsonant(p) && p != 0x0E34 && p != 0x0E38 { return false }
            case 0x0E30: // ะ
                if !isThaiConsonant(p) && !isThaiTone(p) && p != 0x0E32 { return false }
            case 0x0E32, 0x0E33: // า ำ
                if !isThaiConsonant(p) && !isThaiTone(p) && p != 0x0E4D { return false }
            case 0x0E45: // ๅ
                if p != 0x0E24 && p != 0x0E26 { return false }
            default:
                break
            }
        }
        return true
    }

    private static func isThaiConsonant(_ v: UInt32) -> Bool {
        return v >= 0x0E01 && v <= 0x0E2E
    }

    private static func isThaiTone(_ v: UInt32) -> Bool {
        return v >= 0x0E48 && v <= 0x0E4B
    }
}
