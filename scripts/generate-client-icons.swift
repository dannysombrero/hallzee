// macOS maintainer utility; normal builds use the checked-in ICO and ICNS.
// Run from the repository root: swift scripts/generate-client-icons.swift
import AppKit
guard CommandLine.arguments.count <= 2 else {
    fatalError("Usage: swift scripts/generate-client-icons.swift [assets-directory]")
}
let assets = URL(fileURLWithPath: CommandLine.arguments.count == 2 ? CommandLine.arguments[1] : "receiver/universal/Assets")
let source = NSImage(contentsOf: assets.appendingPathComponent("hallzee-logo.png"))!
let temporary = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
let iconset = temporary.appendingPathComponent("hallzee.iconset")
try FileManager.default.createDirectory(at: iconset, withIntermediateDirectories: true)
defer { try? FileManager.default.removeItem(at: temporary) }
func png(_ size: Int) -> Data {
    let bitmap = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: size, pixelsHigh: size, bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: bitmap)
    NSGraphicsContext.current!.imageInterpolation = .high
    let scale = CGFloat(size) / max(source.size.width, source.size.height)
    let w = source.size.width * scale, h = source.size.height * scale
    source.draw(in: NSRect(x: (CGFloat(size)-w)/2, y: (CGFloat(size)-h)/2, width: w, height: h), from: .zero, operation: .copy, fraction: 1)
    NSGraphicsContext.restoreGraphicsState()
    return bitmap.representation(using: .png, properties: [:])!
}
for size in [16,32,128,256,512] {
    try png(size).write(to: iconset.appendingPathComponent("icon_\(size)x\(size).png"))
    try png(size*2).write(to: iconset.appendingPathComponent("icon_\(size)x\(size)@2x.png"))
}
let process = Process()
process.executableURL = URL(fileURLWithPath: "/usr/bin/iconutil")
process.arguments = ["-c", "icns", iconset.path, "-o", assets.appendingPathComponent("hallzee.icns").path]
try process.run(); process.waitUntilExit()
precondition(process.terminationStatus == 0)
let sizes = [16,20,24,32,40,48,64,128,256]
let images = sizes.map { png($0) }
var ico = Data()
func u16(_ value: Int) { var v = UInt16(value).littleEndian; withUnsafeBytes(of: &v) { ico.append(contentsOf: $0) } }
func u32(_ value: Int) { var v = UInt32(value).littleEndian; withUnsafeBytes(of: &v) { ico.append(contentsOf: $0) } }
u16(0); u16(1); u16(sizes.count)
var offset = 6 + sizes.count * 16
for (size, data) in zip(sizes, images) {
    ico.append(contentsOf: [UInt8(size % 256), UInt8(size % 256), 0, 0])
    u16(1); u16(32); u32(data.count); u32(offset)
    offset += data.count
}
for data in images { ico.append(data) }
try ico.write(to: assets.appendingPathComponent("hallzee.ico"))
