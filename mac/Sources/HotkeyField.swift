import AppKit

/// ช่องสำหรับกดคีย์ลัดที่ต้องการ แล้วแสดงผล เช่น "⌃⇧Space"
final class HotkeyField: NSView {
    var onChange: (() -> Void)?

    var hotkey: Hotkey? {
        didSet {
            text = hotkey?.symbols ?? ""
            onChange?()
        }
    }

    private var text = "" {
        didSet { needsDisplay = true }
    }

    override var intrinsicContentSize: NSSize {
        return NSSize(width: 220, height: 26)
    }

    override var acceptsFirstResponder: Bool {
        return true
    }

    override func becomeFirstResponder() -> Bool {
        needsDisplay = true
        return super.becomeFirstResponder()
    }

    override func resignFirstResponder() -> Bool {
        text = hotkey?.symbols ?? ""
        return super.resignFirstResponder()
    }

    override func mouseDown(with event: NSEvent) {
        window?.makeFirstResponder(self)
    }

    override func keyDown(with event: NSEvent) {
        let modifiers = HotkeyField.modifiers(from: event.modifierFlags)
        // Tab / Esc เปล่าๆ ยังใช้เลื่อนโฟกัส / ปิดหน้าต่างได้
        if modifiers.isEmpty && (event.keyCode == 48 || event.keyCode == 53) {
            super.keyDown(with: event)
            return
        }

        let candidate = Hotkey(modifiers: modifiers, keyCode: Int(event.keyCode))
        if candidate.isValid {
            hotkey = candidate
        } else {
            text = "ต้องกดร่วมกับ ⌃ / ⌥ / ⇧ / ⌘"
        }
    }

    /// ปุ่มที่กดร่วมกับปุ่มกดร่วม (เช่น Cmd+K) จะถูกส่งมาที่นี่ก่อน keyDown ต้องรับไว้ก่อนเมนูหรือปุ่มอื่นจะเอาไป
    /// ส่วน Return / Esc เปล่าๆ ปล่อยให้ปุ่มบันทึก / ยกเลิกทำงานตามปกติ
    override func performKeyEquivalent(with event: NSEvent) -> Bool {
        guard window?.firstResponder === self, event.type == .keyDown,
              !HotkeyField.modifiers(from: event.modifierFlags).isEmpty else { return false }
        keyDown(with: event)
        return true
    }

    override func flagsChanged(with event: NSEvent) {
        // กำลังกดแค่ปุ่มกดร่วม แสดงให้เห็นระหว่างกด
        let modifiers = HotkeyField.modifiers(from: event.modifierFlags)
        text = modifiers.isEmpty ? hotkey?.symbols ?? "" : Hotkey.modifierSymbols(modifiers) + "…"
    }

    private static func modifiers(from flags: NSEvent.ModifierFlags) -> Hotkey.Modifiers {
        var result: Hotkey.Modifiers = []
        if flags.contains(.control) { result.insert(.control) }
        if flags.contains(.option) { result.insert(.option) }
        if flags.contains(.shift) { result.insert(.shift) }
        if flags.contains(.command) { result.insert(.command) }
        return result
    }

    override func draw(_ dirtyRect: NSRect) {
        let box = bounds.insetBy(dx: 0.5, dy: 0.5)
        let path = NSBezierPath(roundedRect: box, xRadius: 6, yRadius: 6)
        NSColor.textBackgroundColor.setFill()
        path.fill()
        NSColor.separatorColor.setStroke()
        path.stroke()

        let focused = window?.firstResponder === self
        let shown = text.isEmpty && focused ? "กดคีย์ลัดที่ต้องการ" : text
        let attributes: [NSAttributedString.Key: Any] = [
            .font: NSFont.systemFont(ofSize: NSFont.systemFontSize + 1, weight: .medium),
            .foregroundColor: text.isEmpty ? NSColor.placeholderTextColor : NSColor.labelColor,
        ]
        let size = shown.size(withAttributes: attributes)
        shown.draw(at: NSPoint(x: bounds.midX - size.width / 2, y: bounds.midY - size.height / 2),
                   withAttributes: attributes)
    }

    override func drawFocusRingMask() {
        NSBezierPath(roundedRect: bounds, xRadius: 6, yRadius: 6).fill()
    }

    override var focusRingMaskBounds: NSRect {
        return bounds
    }
}
