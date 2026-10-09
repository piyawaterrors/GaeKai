import Carbon

/// อ่านและสลับแป้นพิมพ์ที่ผู้ใช้เปิดไว้ใน System Settings → Keyboard → Input Sources
enum KeyboardLayouts {
    enum Language {
        case thai
        case english
    }

    // 47 ปุ่มตามลำดับเดียวกับ LayoutConverter.englishKeys (รหัสปุ่มเป็นตำแหน่งบนคีย์บอร์ด ไม่ขึ้นกับภาษา)
    static let keyCodes: [Int] = [
        kVK_ANSI_Grave, kVK_ANSI_1, kVK_ANSI_2, kVK_ANSI_3, kVK_ANSI_4, kVK_ANSI_5, kVK_ANSI_6,
        kVK_ANSI_7, kVK_ANSI_8, kVK_ANSI_9, kVK_ANSI_0, kVK_ANSI_Minus, kVK_ANSI_Equal,
        kVK_ANSI_Q, kVK_ANSI_W, kVK_ANSI_E, kVK_ANSI_R, kVK_ANSI_T, kVK_ANSI_Y, kVK_ANSI_U,
        kVK_ANSI_I, kVK_ANSI_O, kVK_ANSI_P, kVK_ANSI_LeftBracket, kVK_ANSI_RightBracket, kVK_ANSI_Backslash,
        kVK_ANSI_A, kVK_ANSI_S, kVK_ANSI_D, kVK_ANSI_F, kVK_ANSI_G, kVK_ANSI_H, kVK_ANSI_J,
        kVK_ANSI_K, kVK_ANSI_L, kVK_ANSI_Semicolon, kVK_ANSI_Quote,
        kVK_ANSI_Z, kVK_ANSI_X, kVK_ANSI_C, kVK_ANSI_V, kVK_ANSI_B, kVK_ANSI_N, kVK_ANSI_M,
        kVK_ANSI_Comma, kVK_ANSI_Period, kVK_ANSI_Slash,
    ]

    /// ตารางแปลงจากแป้นไทยและแป้นอังกฤษที่ผู้ใช้เปิดไว้จริง (รองรับปัตตะโชติ, Dvorak ฯลฯ)
    /// ถ้าไม่ได้เปิดแป้นไทยไว้ หรืออ่านไม่ได้ จะใช้ตาราง US ⇄ เกษมณี
    static func converterForEnabledLayouts() -> LayoutConverter {
        guard let thai = enabledSource(.thai), let english = enabledSource(.english),
              let thaiKeys = characters(of: thai), let englishKeys = characters(of: english)
        else { return LayoutConverter.standard }

        var pairs: [(Unicode.Scalar, Unicode.Scalar)] = []
        for (e, t) in zip(englishKeys, thaiKeys) {
            if let e = e, let t = t { pairs.append((e, t)) }
        }
        let converter = LayoutConverter(pairs: pairs)
        return converter.keyCount >= 60 ? converter : LayoutConverter.standard
    }

    /// สลับไปใช้แป้นภาษาที่ต้องการ (ถ้าผู้ใช้เปิดแป้นนั้นไว้)
    static func select(_ language: Language) {
        if let source = enabledSource(language) {
            TISSelectInputSource(source)
        }
    }

    static func enabledSource(_ language: Language) -> TISInputSource? {
        let filter = [kTISPropertyInputSourceType as String: kTISTypeKeyboardLayout as String] as CFDictionary
        guard let list = TISCreateInputSourceList(filter, false)?.takeRetainedValue() as? [TISInputSource] else {
            return nil
        }
        switch language {
        case .thai:
            return list.first { languages(of: $0).first == "th" }
        case .english:
            let ascii = list.filter { boolProperty($0, kTISPropertyInputSourceIsASCIICapable) }
            return ascii.first { languages(of: $0).first == "en" } ?? ascii.first
        }
    }

    /// หาแป้นจากรหัส เช่น "com.apple.keylayout.Thai" รวมถึงแป้นที่ติดตั้งไว้แต่ไม่ได้เปิดใช้
    static func installedSource(id: String) -> TISInputSource? {
        let filter = [kTISPropertyInputSourceID as String: id] as CFDictionary
        let list = TISCreateInputSourceList(filter, true)?.takeRetainedValue() as? [TISInputSource]
        return list?.first
    }

    /// ตัวอักษรที่ได้จากแต่ละปุ่ม (ปกติ 47 ตัว ตามด้วยกด Shift 47 ตัว)
    /// ปุ่มที่ไม่ได้ตัวอักษรเดียวพอดี (dead key, ได้หลายตัว) จะเป็น nil
    static func characters(of source: TISInputSource) -> [Unicode.Scalar?]? {
        guard let pointer = TISGetInputSourceProperty(source, kTISPropertyUnicodeKeyLayoutData) else { return nil }
        let data = Unmanaged<CFData>.fromOpaque(pointer).takeUnretainedValue() as Data
        let keyboardType = UInt32(LMGetKbdType())

        return data.withUnsafeBytes { raw -> [Unicode.Scalar?]? in
            guard let layout = raw.bindMemory(to: UCKeyboardLayout.self).baseAddress else { return nil }
            var result: [Unicode.Scalar?] = []
            for shifted in [false, true] {
                let modifiers = shifted ? UInt32(shiftKey >> 8) & 0xFF : 0
                for code in keyCodes {
                    var deadKeyState: UInt32 = 0
                    var length = 0
                    var chars = [UniChar](repeating: 0, count: 4)
                    let status = UCKeyTranslate(layout, UInt16(code), UInt16(kUCKeyActionDown), modifiers,
                                                keyboardType, OptionBits(kUCKeyTranslateNoDeadKeysBit),
                                                &deadKeyState, chars.count, &length, &chars)
                    let scalars = String(utf16CodeUnits: chars, count: length).unicodeScalars
                    result.append(status == noErr && scalars.count == 1 ? scalars.first : nil)
                }
            }
            return result
        }
    }

    private static func languages(of source: TISInputSource) -> [String] {
        guard let pointer = TISGetInputSourceProperty(source, kTISPropertyInputSourceLanguages) else { return [] }
        return Unmanaged<CFArray>.fromOpaque(pointer).takeUnretainedValue() as? [String] ?? []
    }

    private static func boolProperty(_ source: TISInputSource, _ key: CFString) -> Bool {
        guard let pointer = TISGetInputSourceProperty(source, key) else { return false }
        return CFBooleanGetValue(Unmanaged<CFBoolean>.fromOpaque(pointer).takeUnretainedValue())
    }
}
