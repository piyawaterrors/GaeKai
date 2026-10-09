import Carbon

/// ลงทะเบียนคีย์ลัดที่ทำงานได้ทุกหน้าต่าง ด้วย RegisterEventHotKey ของ Carbon
/// (วิธีนี้ไม่ต้องขอสิทธิ์ Accessibility ต่างจากการดักปุ่มด้วย event tap)
final class HotkeyCenter {
    var onPress: (() -> Void)?

    private var hotkeyRef: EventHotKeyRef?
    private var handlerRef: EventHandlerRef?
    private static let signature: OSType = 0x4741_4B49 // "GAKI"

    init() {
        var eventType = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        InstallEventHandler(GetApplicationEventTarget(), { _, _, userData in
            guard let userData = userData else { return OSStatus(eventNotHandledErr) }
            let center = Unmanaged<HotkeyCenter>.fromOpaque(userData).takeUnretainedValue()
            DispatchQueue.main.async { center.onPress?() }
            return noErr
        }, 1, &eventType, Unmanaged.passUnretained(self).toOpaque(), &handlerRef)
    }

    deinit {
        unregister()
        if let handlerRef = handlerRef { RemoveEventHandler(handlerRef) }
    }

    var isRegistered: Bool {
        return hotkeyRef != nil
    }

    /// คืนค่า false ถ้าคีย์ลัดนี้ถูกโปรแกรมอื่นจองไว้แล้ว
    @discardableResult
    func register(_ hotkey: Hotkey) -> Bool {
        unregister()
        let id = EventHotKeyID(signature: HotkeyCenter.signature, id: 1)
        let status = RegisterEventHotKey(UInt32(hotkey.keyCode), hotkey.carbonModifiers, id,
                                         GetApplicationEventTarget(), 0, &hotkeyRef)
        if status != noErr { hotkeyRef = nil }
        return status == noErr
    }

    func unregister() {
        if let hotkeyRef = hotkeyRef { UnregisterEventHotKey(hotkeyRef) }
        hotkeyRef = nil
    }
}
