// สร้างไฟล์ไอคอน mac/Resources/GaeKai.icns (สั่งรัน: mac/build.sh icon)
import AppKit

@main
enum IconGen {
    static func main() throws {
        let output = CommandLine.arguments.count > 1 ? CommandLine.arguments[1] : "GaeKai.iconset"
        try? FileManager.default.removeItem(atPath: output)
        try FileManager.default.createDirectory(atPath: output, withIntermediateDirectories: true)

        for points in [16, 32, 128, 256, 512] {
            for scale in [1, 2] {
                let name = scale == 1 ? "icon_\(points)x\(points).png" : "icon_\(points)x\(points)@2x.png"
                let png = AppIcon.appIconImage(pixels: points * scale).representation(using: .png, properties: [:])!
                try png.write(to: URL(fileURLWithPath: output).appendingPathComponent(name))
            }
        }
        print("Wrote \(output)")
    }
}
