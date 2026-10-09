import Foundation

/// การตั้งค่า เก็บเป็นไฟล์ข้อความแบบ key=value ที่ ~/Library/Application Support/GaeKai/settings.ini
/// (ส่วน "เปิดอัตโนมัติเมื่อเปิดเครื่อง" เก็บใน Login Items ของ macOS ดู LoginItem)
final class AppSettings {
    var hotkey = Hotkey.default
    var switchKeyboardLayout = true
    var restoreClipboard = true
    var paused = false

    static var folderURL: URL {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        return base.appendingPathComponent(App.name, isDirectory: true)
    }

    static var fileURL: URL {
        return folderURL.appendingPathComponent("settings.ini")
    }

    static var exists: Bool {
        return FileManager.default.fileExists(atPath: fileURL.path)
    }

    static func load() -> AppSettings {
        let settings = AppSettings()
        guard let content = try? String(contentsOf: fileURL, encoding: .utf8) else { return settings }

        var values: [String: String] = [:]
        for line in content.components(separatedBy: .newlines) {
            let trimmed = line.trimmingCharacters(in: .whitespaces)
            if trimmed.isEmpty || trimmed.hasPrefix("#") || trimmed.hasPrefix(";") { continue }
            guard let eq = trimmed.firstIndex(of: "="), eq != trimmed.startIndex else { continue }
            let key = trimmed[..<eq].trimmingCharacters(in: .whitespaces).lowercased()
            values[key] = trimmed[trimmed.index(after: eq)...].trimmingCharacters(in: .whitespaces)
        }

        if let value = values["hotkey"], let hotkey = Hotkey.parse(value) { settings.hotkey = hotkey }
        settings.switchKeyboardLayout = readBool(values, "switchkeyboardlayout", settings.switchKeyboardLayout)
        settings.restoreClipboard = readBool(values, "restoreclipboard", settings.restoreClipboard)
        settings.paused = readBool(values, "paused", settings.paused)
        return settings
    }

    func save() throws {
        try FileManager.default.createDirectory(at: AppSettings.folderURL, withIntermediateDirectories: true)
        let content = """
            # GaeKai settings
            Hotkey=\(hotkey)
            SwitchKeyboardLayout=\(switchKeyboardLayout)
            RestoreClipboard=\(restoreClipboard)
            Paused=\(paused)

            """
        try content.write(to: AppSettings.fileURL, atomically: true, encoding: .utf8)
    }

    private static func readBool(_ values: [String: String], _ key: String, _ fallback: Bool) -> Bool {
        switch values[key]?.lowercased() {
        case "true": return true
        case "false": return false
        default: return fallback
        }
    }
}
