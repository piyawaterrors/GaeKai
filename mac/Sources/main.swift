import AppKit

// ถ้ามีตัวที่ทำงานอยู่แล้ว ให้บอกตัวนั้นเปิดหน้าตั้งค่าแทน แล้วปิดตัวเอง
let bundleID = Bundle.main.bundleIdentifier ?? App.name
let others = NSRunningApplication.runningApplications(withBundleIdentifier: bundleID)
    .filter { $0.processIdentifier != ProcessInfo.processInfo.processIdentifier }
if !others.isEmpty {
    DistributedNotificationCenter.default().postNotificationName(
        App.showSettingsNotification, object: nil, userInfo: nil, deliverImmediately: true)
    exit(0)
}

MainActor.assumeIsolated {
    let app = NSApplication.shared
    let delegate = AppDelegate()
    app.delegate = delegate
    app.setActivationPolicy(.accessory)
    app.run()
}
