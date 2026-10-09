import Carbon

/// คีย์ลัด = ปุ่มกดร่วม (Ctrl/Option/Shift/Cmd) + ปุ่มหลัก 1 ปุ่ม
/// ปุ่มหลักเก็บเป็นรหัสตำแหน่งปุ่ม จึงใช้ได้เหมือนกันไม่ว่าตอนนั้นจะเป็นแป้นไทยหรืออังกฤษ
struct Hotkey: Equatable, CustomStringConvertible {
    struct Modifiers: OptionSet {
        let rawValue: Int
        static let control = Modifiers(rawValue: 1)
        static let option = Modifiers(rawValue: 2)
        static let shift = Modifiers(rawValue: 4)
        static let command = Modifiers(rawValue: 8)
    }

    static let `default` = Hotkey(modifiers: [.control, .shift], keyCode: kVK_Space)

    // ลำดับตามแบบ macOS: ⌃ ⌥ ⇧ ⌘
    private static let modifierNames: [(Modifiers, String, String)] = [
        (.control, "Ctrl", "⌃"), (.option, "Option", "⌥"), (.shift, "Shift", "⇧"), (.command, "Cmd", "⌘"),
    ]

    private static let modifierAliases: [String: Modifiers] = [
        "ctrl": .control, "control": .control, "⌃": .control,
        "option": .option, "opt": .option, "alt": .option, "⌥": .option,
        "shift": .shift, "⇧": .shift,
        "cmd": .command, "command": .command, "⌘": .command,
    ]

    private static let keyNames: [Int: String] = {
        var names: [Int: String] = [
            kVK_Space: "Space", kVK_Return: "Return", kVK_Tab: "Tab", kVK_Delete: "Delete",
            kVK_ForwardDelete: "ForwardDelete", kVK_Escape: "Esc", kVK_Home: "Home", kVK_End: "End",
            kVK_PageUp: "PageUp", kVK_PageDown: "PageDown", kVK_LeftArrow: "Left", kVK_RightArrow: "Right",
            kVK_UpArrow: "Up", kVK_DownArrow: "Down",
            kVK_ANSI_Grave: "`", kVK_ANSI_Minus: "-", kVK_ANSI_Equal: "=", kVK_ANSI_LeftBracket: "[",
            kVK_ANSI_RightBracket: "]", kVK_ANSI_Backslash: "\\", kVK_ANSI_Semicolon: ";", kVK_ANSI_Quote: "'",
            kVK_ANSI_Comma: ",", kVK_ANSI_Period: ".", kVK_ANSI_Slash: "/",
        ]
        let letters = [kVK_ANSI_A, kVK_ANSI_B, kVK_ANSI_C, kVK_ANSI_D, kVK_ANSI_E, kVK_ANSI_F, kVK_ANSI_G,
                       kVK_ANSI_H, kVK_ANSI_I, kVK_ANSI_J, kVK_ANSI_K, kVK_ANSI_L, kVK_ANSI_M, kVK_ANSI_N,
                       kVK_ANSI_O, kVK_ANSI_P, kVK_ANSI_Q, kVK_ANSI_R, kVK_ANSI_S, kVK_ANSI_T, kVK_ANSI_U,
                       kVK_ANSI_V, kVK_ANSI_W, kVK_ANSI_X, kVK_ANSI_Y, kVK_ANSI_Z]
        for (i, code) in letters.enumerated() {
            names[code] = String(UnicodeScalar(UInt8(ascii: "A") + UInt8(i)))
        }
        let digits = [kVK_ANSI_0, kVK_ANSI_1, kVK_ANSI_2, kVK_ANSI_3, kVK_ANSI_4,
                      kVK_ANSI_5, kVK_ANSI_6, kVK_ANSI_7, kVK_ANSI_8, kVK_ANSI_9]
        let keypad = [kVK_ANSI_Keypad0, kVK_ANSI_Keypad1, kVK_ANSI_Keypad2, kVK_ANSI_Keypad3, kVK_ANSI_Keypad4,
                      kVK_ANSI_Keypad5, kVK_ANSI_Keypad6, kVK_ANSI_Keypad7, kVK_ANSI_Keypad8, kVK_ANSI_Keypad9]
        for i in 0..<10 {
            names[digits[i]] = String(i)
            names[keypad[i]] = "Num" + String(i)
        }
        for (i, code) in functionKeys.enumerated() {
            names[code] = "F" + String(i + 1)
        }
        return names
    }()

    private static let functionKeys = [
        kVK_F1, kVK_F2, kVK_F3, kVK_F4, kVK_F5, kVK_F6, kVK_F7, kVK_F8, kVK_F9, kVK_F10,
        kVK_F11, kVK_F12, kVK_F13, kVK_F14, kVK_F15, kVK_F16, kVK_F17, kVK_F18, kVK_F19, kVK_F20,
    ]

    let modifiers: Modifiers
    let keyCode: Int

    init(modifiers: Modifiers, keyCode: Int) {
        self.modifiers = modifiers
        self.keyCode = keyCode
    }

    /// ต้องเป็นปุ่มที่รู้จัก และต้องมีปุ่มกดร่วมอย่างน้อย 1 ปุ่ม (ยกเว้น F1–F20)
    var isValid: Bool {
        guard Hotkey.keyNames[keyCode] != nil else { return false }
        return !modifiers.isEmpty || Hotkey.functionKeys.contains(keyCode)
    }

    var carbonModifiers: UInt32 {
        var result = 0
        if modifiers.contains(.control) { result |= controlKey }
        if modifiers.contains(.option) { result |= optionKey }
        if modifiers.contains(.shift) { result |= shiftKey }
        if modifiers.contains(.command) { result |= cmdKey }
        return UInt32(result)
    }

    var keyName: String {
        return Hotkey.keyNames[keyCode] ?? "?"
    }

    /// รูปแบบที่ใช้ในไฟล์ตั้งค่า เช่น "Ctrl + Shift + Space"
    var description: String {
        let parts = Hotkey.modifierNames.filter { modifiers.contains($0.0) }.map { $0.1 }
        return (parts + [keyName]).joined(separator: " + ")
    }

    /// รูปแบบที่แสดงบนหน้าจอแบบ macOS เช่น "⌃⇧Space"
    var symbols: String {
        return Hotkey.modifierSymbols(modifiers) + keyName
    }

    static func modifierSymbols(_ modifiers: Modifiers) -> String {
        return modifierNames.filter { modifiers.contains($0.0) }.map { $0.2 }.joined()
    }

    static func parse(_ text: String) -> Hotkey? {
        let parts = text.split(separator: "+", omittingEmptySubsequences: false)
            .map { $0.trimmingCharacters(in: .whitespaces) }
        guard let last = parts.last, !last.isEmpty else { return nil }

        var modifiers: Modifiers = []
        for part in parts.dropLast() {
            guard let modifier = modifierAliases[part.lowercased()] else { return nil }
            modifiers.insert(modifier)
        }

        guard let code = keyNames.first(where: { $0.value.caseInsensitiveCompare(last) == .orderedSame })?.key
        else { return nil }

        let hotkey = Hotkey(modifiers: modifiers, keyCode: code)
        return hotkey.isValid ? hotkey : nil
    }
}
