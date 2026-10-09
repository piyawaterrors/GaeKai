import AppKit

enum App {
    static let name = "GaeKai"
    static let displayName = "GaeKai แก้ไข"
    /// โปรแกรมตัวที่เปิดซ้ำจะส่งข้อความนี้ไปบอกตัวที่ทำงานอยู่ให้เปิดหน้าตั้งค่า
    static let showSettingsNotification = Notification.Name("io.github.piyawaterrors.gaekai.show-settings")

    static var version: String {
        return Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "dev"
    }
}

/// ตัวโปรแกรมหลัก: ไอคอนที่แถบเมนู + เมนู + รับคีย์ลัด
@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate {
    private var settings = AppSettings()
    private let hotkeys = HotkeyCenter()
    private var statusItem: NSStatusItem!
    private let hotkeyItem = NSMenuItem()
    private let permissionItem = NSMenuItem(title: "⚠️ อนุญาตสิทธิ์ Accessibility...", action: nil, keyEquivalent: "")
    private let pauseItem = NSMenuItem(title: "หยุดชั่วคราว", action: nil, keyEquivalent: "")
    private let startupItem = NSMenuItem(title: "เปิดอัตโนมัติเมื่อเปิดเครื่อง", action: nil, keyEquivalent: "")
    private var settingsWindow: SettingsWindow?
    private var permissionTimer: Timer?
    private var busy = false

    func applicationDidFinishLaunching(_ notification: Notification) {
        let firstRun = !AppSettings.exists
        settings = AppSettings.load()

        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        statusItem.button?.image = AppIcon.statusBarImage()
        statusItem.menu = buildMenu()

        hotkeys.onPress = { [weak self] in self?.onHotkeyPressed() }
        DistributedNotificationCenter.default().addObserver(
            forName: App.showSettingsNotification, object: nil, queue: .main
        ) { [weak self] _ in
            MainActor.assumeIsolated { self?.showSettings() }
        }

        if firstRun {
            // ครั้งแรกที่เปิด: ตั้งให้เปิดอัตโนมัติพร้อมเครื่องเลย (ยกเว้นยังเปิดจากไฟล์ .dmg อยู่)
            if !isRunningFromDiskImage { try? LoginItem.setEnabled(true) }
            trySaveSettings()
        }

        applyHotkey()
        if isRunningFromDiskImage {
            showMoveToApplications()
        } else if !Permissions.isTrusted {
            showPermissionGuide()
        } else if firstRun && !settings.paused {
            showAlert("โปรแกรมทำงานอยู่ที่แถบเมนูด้านบน (ไอคอน ก)",
                      "คลุมข้อความที่พิมพ์ผิดภาษา แล้วกด \(settings.hotkey.symbols)")
        }
    }

    /// ผู้ใช้เปิดโปรแกรมซ้ำ (เช่น ดับเบิลคลิกใน Applications) — เปิดหน้าตั้งค่าให้ เผื่อหาไอคอนในแถบเมนูไม่เจอ
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        showSettings()
        return false
    }

    // MARK: - เมนู

    private func buildMenu() -> NSMenu {
        let menu = NSMenu()
        menu.delegate = self
        hotkeyItem.isEnabled = false
        permissionItem.action = #selector(requestPermission)
        let settingsMenuItem = NSMenuItem(title: "ตั้งค่า / เปลี่ยนคีย์ลัด...", action: #selector(showSettings), keyEquivalent: ",")
        pauseItem.action = #selector(togglePause)
        startupItem.action = #selector(toggleStartup)

        menu.addItem(hotkeyItem)
        menu.addItem(permissionItem)
        menu.addItem(.separator())
        menu.addItem(settingsMenuItem)
        menu.addItem(pauseItem)
        menu.addItem(startupItem)
        menu.addItem(.separator())
        menu.addItem(NSMenuItem(title: "วิธีใช้ / เกี่ยวกับ", action: #selector(showAbout), keyEquivalent: ""))
        menu.addItem(NSMenuItem(title: "ออกจากโปรแกรม", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q"))
        for item in menu.items where item.action != #selector(NSApplication.terminate(_:)) {
            item.target = self
        }
        return menu
    }

    func menuWillOpen(_ menu: NSMenu) {
        startupItem.state = LoginItem.isEnabled ? .on : .off
        updateUi()
    }

    private func updateUi() {
        let trusted = Permissions.isTrusted
        let active = hotkeys.isRegistered
        let state = settings.paused ? " (หยุดชั่วคราว)" : !active ? " (ใช้งานไม่ได้)" : !trusted ? " (ยังไม่ได้รับสิทธิ์)" : ""
        hotkeyItem.title = "คีย์ลัด: " + settings.hotkey.symbols + state
        permissionItem.isHidden = trusted
        pauseItem.state = settings.paused ? .on : .off
        statusItem.button?.appearsDisabled = settings.paused || !active || !trusted
        statusItem.button?.toolTip = App.displayName + " — " + settings.hotkey.symbols + state
    }

    // MARK: - คีย์ลัด

    private func onHotkeyPressed() {
        if busy || settings.paused { return }
        guard Permissions.isTrusted else {
            showPermissionGuide()
            return
        }
        busy = true
        Task { @MainActor in
            _ = await TextSwapper.convertSelection(settings: settings)
            busy = false
        }
    }

    /// ลงทะเบียนคีย์ลัดตามการตั้งค่า แจ้งเตือนถ้าคีย์ลัดชนกับโปรแกรมอื่น
    @discardableResult
    private func applyHotkey() -> Bool {
        var ok = true
        if settings.paused || settingsWindow != nil {
            hotkeys.unregister()
        } else {
            ok = hotkeys.register(settings.hotkey)
            if !ok {
                showAlert("ใช้คีย์ลัด \(settings.hotkey.symbols) ไม่ได้ เพราะมีโปรแกรมอื่นใช้อยู่",
                          "กรุณาเปลี่ยนคีย์ลัดในหน้าตั้งค่า")
            }
        }
        updateUi()
        return ok
    }

    // MARK: - คำสั่งในเมนู

    @objc private func showSettings() {
        if let window = settingsWindow {
            window.present()
            return
        }

        let window = SettingsWindow(settings: settings, startAtLogin: LoginItem.isEnabled) { [weak self] hotkey in
            guard let self = self else { return false }
            let ok = self.hotkeys.register(hotkey)
            self.hotkeys.unregister()
            return ok
        }
        // ปิดคีย์ลัดไว้ระหว่างตั้งค่า เพื่อให้กดคีย์ลัดเดิมลงในช่องได้
        hotkeys.unregister()
        window.onClose = { [weak self] result in
            MainActor.assumeIsolated { self?.onSettingsClosed(result) }
        }
        settingsWindow = window
        window.present()
    }

    private func onSettingsClosed(_ result: SettingsWindow.Result?) {
        settingsWindow = nil
        if let result = result {
            settings.hotkey = result.hotkey
            settings.switchKeyboardLayout = result.switchKeyboardLayout
            settings.restoreClipboard = result.restoreClipboard
            setStartup(result.startAtLogin)
            trySaveSettings()
        }
        applyHotkey()
    }

    @objc private func togglePause() {
        settings.paused.toggle()
        trySaveSettings()
        applyHotkey()
    }

    @objc private func toggleStartup() {
        setStartup(!LoginItem.isEnabled)
    }

    @objc private func requestPermission() {
        Permissions.request()
        watchPermission()
    }

    @objc private func showAbout() {
        showAlert(App.displayName + " เวอร์ชัน " + App.version, """
            แก้ข้อความที่พิมพ์ผิดภาษา ไทย ⇄ อังกฤษ

            วิธีใช้:
            1. คลุม (เลือก) ข้อความที่พิมพ์ผิดภาษา
            2. กด \(settings.hotkey.symbols)

            ตัวอย่าง:
                l;ylfu  →  สวัสดี
                เนนก  →  good

            ไฟล์ตั้งค่า: \(AppSettings.fileURL.path)
            """)
    }

    // MARK: - สิทธิ์และการติดตั้ง

    private func showPermissionGuide() {
        let alert = NSAlert()
        alert.messageText = "GaeKai ต้องได้รับสิทธิ์ Accessibility ก่อนใช้งาน"
        alert.informativeText = """
            macOS ต้องให้อนุญาตก่อน โปรแกรมจึงจะกด ⌘C / ⌘V แทนคุณได้

            1. กด "เปิด System Settings"
            2. ไปที่ Privacy & Security → Accessibility
            3. เปิดสวิตช์ที่ GaeKai

            GaeKai ไม่ได้อ่านหรือเก็บข้อมูลอื่นใดนอกจากข้อความที่คุณคลุมแล้วกดคีย์ลัด
            """
        alert.addButton(withTitle: "เปิด System Settings")
        alert.addButton(withTitle: "ไว้ทีหลัง")
        NSApp.activate(ignoringOtherApps: true)
        if alert.runModal() == .alertFirstButtonReturn {
            Permissions.request()
        }
        watchPermission()
    }

    /// คอยดูว่าผู้ใช้อนุญาตสิทธิ์แล้วหรือยัง เพื่ออัปเดตไอคอนให้เป็นสีปกติ
    private func watchPermission() {
        if permissionTimer != nil || Permissions.isTrusted { return }
        permissionTimer = Timer.scheduledTimer(withTimeInterval: 1.5, repeats: true) { [weak self] timer in
            MainActor.assumeIsolated {
                guard Permissions.isTrusted else { return }
                timer.invalidate()
                self?.permissionTimer = nil
                self?.updateUi()
            }
        }
    }

    private var isRunningFromDiskImage: Bool {
        let path = Bundle.main.bundlePath
        return path.hasPrefix("/Volumes/") || path.contains("/AppTranslocation/")
    }

    private func showMoveToApplications() {
        showAlert("กรุณาลาก GaeKai ไปไว้ในโฟลเดอร์ Applications ก่อน",
                  "แล้วเปิดจาก Applications อีกครั้ง\n" +
                  "ถ้าเปิดจากไฟล์ .dmg โดยตรง macOS จะจำสิทธิ์และการเปิดอัตโนมัติเมื่อเปิดเครื่องไม่ได้")
    }

    // MARK: - ตัวช่วย

    private func setStartup(_ enabled: Bool) {
        do {
            try LoginItem.setEnabled(enabled)
        } catch {
            showAlert("ตั้งค่าการเปิดอัตโนมัติไม่สำเร็จ", error.localizedDescription)
        }
    }

    private func trySaveSettings() {
        do {
            try settings.save()
        } catch {
            showAlert("บันทึกการตั้งค่าไม่สำเร็จ", error.localizedDescription)
        }
    }

    private func showAlert(_ message: String, _ detail: String) {
        let alert = NSAlert()
        alert.messageText = message
        alert.informativeText = detail
        NSApp.activate(ignoringOtherApps: true)
        alert.runModal()
    }
}
