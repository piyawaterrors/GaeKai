import AppKit
import Carbon

/// ขั้นตอนหลัก: คัดลอกข้อความที่คลุมไว้ (Cmd+C) → แปลงภาษา → วางทับ (Cmd+V) → สลับแป้นพิมพ์ → คืนค่าคลิปบอร์ด
@MainActor
enum TextSwapper {
    private static let keyReleaseTimeout: TimeInterval = 2.0
    private static let copyTimeout: TimeInterval = 0.7
    private static let pasteSettleNanoseconds: UInt64 = 400_000_000

    static func convertSelection(settings: AppSettings) async -> ConversionDirection {
        // รอให้ผู้ใช้ปล่อยปุ่มคีย์ลัดก่อน ไม่อย่างนั้นโปรแกรมปลายทางอาจเห็นเป็น Cmd+Shift+C
        await waitForKeysReleased(settings.hotkey)

        let backup = settings.restoreClipboard ? Clipboard.capture() : nil

        let changeCount = Clipboard.changeCount
        postShortcut(kVK_ANSI_C)

        guard let selected = await waitForCopiedText(since: changeCount) else {
            return .none // ไม่มีข้อความที่คลุมไว้ คลิปบอร์ดไม่ถูกแตะ
        }

        if Clipboard.isWholeLineCopy() {
            Clipboard.restore(backup)
            return .none
        }

        let (converted, direction) = KeyboardLayouts.converterForEnabledLayouts().convert(selected)
        if direction == .none || converted == selected {
            Clipboard.restore(backup)
            return .none
        }

        guard Clipboard.setText(converted) else { return .none }
        postShortcut(kVK_ANSI_V)

        if settings.switchKeyboardLayout {
            KeyboardLayouts.select(direction == .englishToThai ? .thai : .english)
        }

        if settings.restoreClipboard {
            // โปรแกรมปลายทางอ่านคลิปบอร์ดแบบไม่พร้อมกัน ต้องรอให้วางเสร็จก่อนคืนค่า
            try? await Task.sleep(nanoseconds: pasteSettleNanoseconds)
            Clipboard.restore(backup)
        }
        return direction
    }

    private static func waitForKeysReleased(_ hotkey: Hotkey) async {
        var held: CGEventFlags = []
        if hotkey.modifiers.contains(.control) { held.insert(.maskControl) }
        if hotkey.modifiers.contains(.option) { held.insert(.maskAlternate) }
        if hotkey.modifiers.contains(.shift) { held.insert(.maskShift) }
        if hotkey.modifiers.contains(.command) { held.insert(.maskCommand) }

        let deadline = Date().addingTimeInterval(keyReleaseTimeout)
        while Date() < deadline {
            let flags = CGEventSource.flagsState(.combinedSessionState)
            let keyDown = CGEventSource.keyState(.combinedSessionState, key: CGKeyCode(hotkey.keyCode))
            if flags.intersection(held).isEmpty && !keyDown { return }
            try? await Task.sleep(nanoseconds: 15_000_000)
        }
        // ผู้ใช้ยังกดค้างอยู่ ก็ทำต่อได้ เพราะ postShortcut กำหนดปุ่มกดร่วมของ Cmd+C / Cmd+V เองทุกครั้ง
    }

    private static func waitForCopiedText(since changeCount: Int) async -> String? {
        let deadline = Date().addingTimeInterval(copyTimeout)
        while Clipboard.changeCount == changeCount {
            if Date() >= deadline { return nil }
            try? await Task.sleep(nanoseconds: 15_000_000)
        }

        // โปรแกรมต้นทางอาจยังเขียนข้อมูลรูปแบบอื่นลงคลิปบอร์ดไม่เสร็จ
        try? await Task.sleep(nanoseconds: 40_000_000)
        return Clipboard.text() ?? ""
    }

    /// จำลองการกด Cmd + ปุ่ม (ต้องได้สิทธิ์ Accessibility ก่อน ไม่อย่างนั้นระบบจะทิ้ง event นี้ไปเฉยๆ)
    private static func postShortcut(_ keyCode: Int) {
        let source = CGEventSource(stateID: .hidSystemState)
        let command = CGKeyCode(kVK_Command)
        let events = [
            CGEvent(keyboardEventSource: source, virtualKey: command, keyDown: true),
            CGEvent(keyboardEventSource: source, virtualKey: CGKeyCode(keyCode), keyDown: true),
            CGEvent(keyboardEventSource: source, virtualKey: CGKeyCode(keyCode), keyDown: false),
            CGEvent(keyboardEventSource: source, virtualKey: command, keyDown: false),
        ]
        for (i, event) in events.enumerated() {
            // กำหนดปุ่มกดร่วมเองทุกครั้ง ไม่ให้ปุ่มที่ผู้ใช้ยังกดค้าง (เช่น Shift) ปนเข้าไป
            event?.flags = i < 3 ? .maskCommand : []
            event?.post(tap: .cghidEventTap)
        }
    }
}
