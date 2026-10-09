import Foundation

enum ConversionDirection {
    case none
    case englishToThai
    case thaiToEnglish
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

    /// เดาว่าข้อความถูกพิมพ์ด้วยแป้นไหน: ถ้ามีอักษรไทยมากกว่าอักษรอังกฤษ ให้แปลงไทย→อังกฤษ
    /// นอกนั้นให้แปลงอังกฤษ→ไทย
    func detectDirection(_ text: String) -> ConversionDirection {
        var thai = 0, latin = 0, mappable = 0
        for c in text.unicodeScalars {
            if LayoutConverter.isThai(c) { thai += 1 }
            else if LayoutConverter.isLatinLetter(c) { latin += 1 }
            if enToTh[c] != nil || thToEn[c] != nil { mappable += 1 }
        }

        if mappable == 0 { return .none }
        return thai > latin ? .thaiToEnglish : .englishToThai
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

    func convert(_ text: String) -> (text: String, direction: ConversionDirection) {
        let direction = detectDirection(text)
        return (convert(text, direction), direction)
    }
}
