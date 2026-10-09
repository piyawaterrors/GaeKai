import ServiceManagement

/// เปิด/ปิดการเริ่มทำงานพร้อมเครื่อง ผ่าน Login Items ของ macOS (System Settings → General → Login Items)
enum LoginItem {
    static var isEnabled: Bool {
        return SMAppService.mainApp.status == .enabled
    }

    static func setEnabled(_ enabled: Bool) throws {
        if enabled == isEnabled { return }
        if enabled {
            try SMAppService.mainApp.register()
        } else {
            try SMAppService.mainApp.unregister()
        }
    }
}
