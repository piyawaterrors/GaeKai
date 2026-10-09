import AppKit

/// ไอคอน "ก" — วาดด้วยโค้ด ไม่ต้องมีไฟล์รูปแยก
enum AppIcon {
    private static func thaiFont(size: CGFloat) -> NSFont {
        return NSFont(name: "Thonburi-Bold", size: size) ?? NSFont.boldSystemFont(ofSize: size)
    }

    /// ไอคอนในแถบเมนู: กรอบปุ่มคีย์บอร์ดที่เจาะเป็นตัว "ก" (template image จึงเปลี่ยนสีตามธีมสว่าง/มืดเอง)
    static func statusBarImage() -> NSImage {
        let image = NSImage(size: NSSize(width: 18, height: 18), flipped: false) { rect in
            let box = rect.insetBy(dx: 1, dy: 1)
            NSColor.black.setFill()
            NSBezierPath(roundedRect: box, xRadius: 4, yRadius: 4).fill()

            NSGraphicsContext.current?.compositingOperation = .destinationOut
            drawGlyph(in: box, heightRatio: 0.62, color: .black)
            return true
        }
        image.isTemplate = true
        image.accessibilityDescription = "GaeKai"
        return image
    }

    /// ไอคอนโปรแกรม: สี่เหลี่ยมมุมโค้งสีน้ำเงิน + "ก" สีขาว ตามขนาดกริดไอคอนของ macOS
    static func appIconImage(pixels: Int) -> NSBitmapImageRep {
        let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: pixels, pixelsHigh: pixels,
                                   bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
                                   colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
        NSGraphicsContext.saveGraphicsState()
        NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)

        let size = CGFloat(pixels)
        let box = NSRect(x: size * 0.1, y: size * 0.1, width: size * 0.8, height: size * 0.8)
        let path = NSBezierPath(roundedRect: box, xRadius: size * 0.18, yRadius: size * 0.18)

        NSGraphicsContext.saveGraphicsState()
        let shadow = NSShadow()
        shadow.shadowColor = NSColor.black.withAlphaComponent(0.3)
        shadow.shadowOffset = NSSize(width: 0, height: -size * 0.01)
        shadow.shadowBlurRadius = size * 0.02
        shadow.set()
        NSColor(srgbRed: 29 / 255, green: 78 / 255, blue: 216 / 255, alpha: 1).setFill()
        path.fill()
        NSGraphicsContext.restoreGraphicsState()

        let gradient = NSGradient(starting: NSColor(srgbRed: 59 / 255, green: 130 / 255, blue: 246 / 255, alpha: 1),
                                  ending: NSColor(srgbRed: 29 / 255, green: 78 / 255, blue: 216 / 255, alpha: 1))
        gradient?.draw(in: path, angle: -45)
        drawGlyph(in: box, heightRatio: 0.56, color: .white)

        NSGraphicsContext.restoreGraphicsState()
        return rep
    }

    /// วาด "ก" ให้อยู่กลางกรอบพอดี โดยวัดจากรูปร่างตัวอักษรจริง (ไม่ใช่ความสูงบรรทัด ซึ่งเผื่อที่ให้สระบน-ล่าง)
    private static func drawGlyph(in box: NSRect, heightRatio: CGFloat, color: NSColor) {
        let font = thaiFont(size: 100)
        let text = NSAttributedString(string: "ก", attributes: [.font: font, .foregroundColor: color])
        let line = CTLineCreateWithAttributedString(text)
        let bounds = CTLineGetBoundsWithOptions(line, .useGlyphPathBounds)
        guard bounds.height > 0, let context = NSGraphicsContext.current?.cgContext else { return }

        let scale = box.height * heightRatio / bounds.height
        context.saveGState()
        context.translateBy(x: box.midX - bounds.midX * scale, y: box.midY - bounds.midY * scale)
        context.scaleBy(x: scale, y: scale)
        context.textPosition = .zero
        CTLineDraw(line, context)
        context.restoreGState()
    }
}
