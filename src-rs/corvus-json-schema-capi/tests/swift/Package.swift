// swift-tools-version:5.9
// Links the XCFramework (xcframework.ps1, unpacked here by CI as CorvusJsonSchema.xcframework) the way a Swift package
// does, and calls the C library through its Clang module, CCorvusJsonSchema.
import PackageDescription

let package = Package(
    name: "CapiSwiftSmoke",
    platforms: [.macOS(.v10_15), .iOS(.v13)],
    products: [.library(name: "Smoke", targets: ["Smoke"])],
    targets: [
        .binaryTarget(name: "CCorvusJsonSchema", path: "CorvusJsonSchema.xcframework"),
        .target(name: "Smoke", dependencies: ["CCorvusJsonSchema"]),
        .testTarget(name: "SmokeTests", dependencies: ["Smoke"]),
    ]
)
