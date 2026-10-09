import AppKit

final class SettingsWindow: NSWindowController, NSWindowDelegate {
    struct Result {
        let hotkey: Hotkey
        let switchKeyboardLayout: Bool
        let restoreClipboard: Bool
        let startAtLogin: Bool
    }

    /// เรียกเมื่อปิดหน้าต่าง ได้ nil ถ้าผู้ใช้กดยกเลิก
    var onClose: ((Result?) -> Void)?

    private let hotkeyField = HotkeyField()
    private let switchLayoutBox = NSButton(checkboxWithTitle: "สลับภาษาแป้นพิมพ์ให้อัตโนมัติหลังแปลง (พิมพ์ต่อได้ทันที)",
                                           target: nil, action: nil)
    private let restoreClipboardBox = NSButton(checkboxWithTitle: "คืนค่าคลิปบอร์ดเดิมหลังแปลง", target: nil, action: nil)
    private let startupBox = NSButton(checkboxWithTitle: "เปิดโปรแกรมอัตโนมัติเมื่อเปิดเครื่อง", target: nil, action: nil)
    private let tryRegisterHotkey: (Hotkey) -> Bool
    private var result: Result?

    /// - Parameter tryRegisterHotkey: ใช้ทดสอบว่าคีย์ลัดใหม่ไม่ชนกับโปรแกรมอื่นก่อนบันทึก
    init(settings: AppSettings, startAtLogin: Bool, tryRegisterHotkey: @escaping (Hotkey) -> Bool) {
        self.tryRegisterHotkey = tryRegisterHotkey
        let window = NSWindow(contentRect: .zero, styleMask: [.titled, .closable], backing: .buffered, defer: false)
        window.title = App.displayName + " — ตั้งค่า"
        window.isReleasedWhenClosed = false // หน้าต่างนี้มี NSWindowController เป็นเจ้าของ
        super.init(window: window)
        window.delegate = self

        hotkeyField.hotkey = settings.hotkey
        switchLayoutBox.state = settings.switchKeyboardLayout ? .on : .off
        restoreClipboardBox.state = settings.restoreClipboard ? .on : .off
        startupBox.state = startAtLogin ? .on : .off

        let title = NSTextField(labelWithString: "คีย์ลัดสำหรับแปลงข้อความที่คลุมไว้")
        title.font = NSFont.boldSystemFont(ofSize: NSFont.systemFontSize)

        let defaultButton = NSButton(title: "ค่าเริ่มต้น", target: self, action: #selector(resetHotkey))
        let hotkeyRow = NSStackView(views: [hotkeyField, defaultButton])
        hotkeyRow.spacing = 8

        let hint = NSTextField(labelWithString: "คลิกที่ช่องด้านบน แล้วกดคีย์ลัดใหม่ที่ต้องการ เช่น ⌃⌥K")
        hint.textColor = .secondaryLabelColor
        hint.font = NSFont.systemFont(ofSize: NSFont.smallSystemFontSize)

        let cancelButton = NSButton(title: "ยกเลิก", target: self, action: #selector(cancel))
        cancelButton.keyEquivalent = "\u{1b}"
        let saveButton = NSButton(title: "บันทึก", target: self, action: #selector(save))
        saveButton.keyEquivalent = "\r"
        let buttons = NSStackView()
        buttons.setViews([cancelButton, saveButton], in: .trailing)
        buttons.spacing = 8

        let root = NSStackView(views: [title, hotkeyRow, hint, switchLayoutBox, restoreClipboardBox, startupBox, buttons])
        root.orientation = .vertical
        root.alignment = .leading
        root.spacing = 8
        root.edgeInsets = NSEdgeInsets(top: 18, left: 20, bottom: 18, right: 20)
        root.setCustomSpacing(4, after: hotkeyRow)
        root.setCustomSpacing(16, after: hint)
        root.setCustomSpacing(20, after: startupBox)
        buttons.trailingAnchor.constraint(equalTo: root.trailingAnchor, constant: -20).isActive = true

        window.contentView = root
        window.setContentSize(root.fittingSize)
        window.initialFirstResponder = nil
        window.center()
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    func present() {
        NSApp.activate(ignoringOtherApps: true)
        showWindow(nil)
        window?.makeKeyAndOrderFront(nil)
        window?.makeFirstResponder(nil)
    }

    @objc private func resetHotkey() {
        hotkeyField.hotkey = Hotkey.default
    }

    @objc private func cancel() {
        window?.close()
    }

    @objc private func save() {
        guard let window = window else { return }
        guard let hotkey = hotkeyField.hotkey, hotkey.isValid else {
            showWarning("กรุณากดคีย์ลัดที่ต้องการในช่องคีย์ลัด")
            return
        }
        guard tryRegisterHotkey(hotkey) else {
            showWarning("คีย์ลัด \(hotkey.symbols) ถูกใช้งานโดยโปรแกรมอื่นอยู่แล้ว\nกรุณาเลือกคีย์ลัดอื่น")
            return
        }
        result = Result(hotkey: hotkey,
                        switchKeyboardLayout: switchLayoutBox.state == .on,
                        restoreClipboard: restoreClipboardBox.state == .on,
                        startAtLogin: startupBox.state == .on)
        window.close()
    }

    private func showWarning(_ message: String) {
        guard let window = window else { return }
        let alert = NSAlert()
        alert.alertStyle = .warning
        alert.messageText = message
        alert.beginSheetModal(for: window) { _ in
            window.makeFirstResponder(self.hotkeyField)
        }
    }

    func windowWillClose(_ notification: Notification) {
        onClose?(result)
        onClose = nil
    }
}
