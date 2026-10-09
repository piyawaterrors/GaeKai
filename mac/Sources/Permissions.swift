import AppKit
import ApplicationServices

/// สิทธิ์ Accessibility จำเป็นสำหรับการจำลองการกด Cmd+C / Cmd+V ให้โปรแกรมอื่น
enum Permissions {
    static var isTrusted: Bool {
        return AXIsProcessTrusted()
    }

    /// ให้ macOS ใส่ชื่อโปรแกรมไว้ในรายการ Accessibility และเปิดหน้า System Settings ที่ต้องไปเปิดสิทธิ์
    static func request() {
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
        _ = AXIsProcessTrustedWithOptions(options)
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") {
            NSWorkspace.shared.open(url)
        }
    }
}
