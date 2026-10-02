<#
.SYNOPSIS
Builds the corvus-json-schema C library as an XCFramework, for Swift packages on Apple platforms.

.DESCRIPTION
On macOS, builds the static library (cargo, release profile) for macOS (arm64 and x86_64), iOS (arm64) and the iOS
simulator (arm64 and x86_64), combines each platform's architectures with lipo, and writes to OutDir:

  CorvusJsonSchema.xcframework.zip         the XCFramework: per platform, libcorvus_json_schema.a and the headers,
                                           with a module map declaring the Clang module CCorvusJsonSchema
  CorvusJsonSchema.xcframework.zip.sha256  its SwiftPM checksum (swift package compute-checksum), which a
                                           Package.swift binaryTarget names with the archive's URL

and prints the archive's path. The Rust targets must be installed (rustup target add).

.PARAMETER MacOS
The oldest macOS the library supports (MACOSX_DEPLOYMENT_TARGET).

.PARAMETER IOS
The oldest iOS the library supports (IPHONEOS_DEPLOYMENT_TARGET).

.PARAMETER OutDir
Where to write the archive (default: dist, next to this script).
#>
[CmdletBinding()]
param(
    [string] $MacOS = "10.15",
    [string] $IOS = "13.0",
    [string] $OutDir = (Join-Path $PSScriptRoot "dist")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-Native([string] $Command, [string[]] $Arguments) {
    $output = & $Command @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        $output | Write-Host
        throw "$Command $($Arguments -join ' ') failed (exit $LASTEXITCODE)"
    }
    return $output
}

Push-Location $PSScriptRoot
try {
    $env:MACOSX_DEPLOYMENT_TARGET = $MacOS
    $env:IPHONEOS_DEPLOYMENT_TARGET = $IOS
    # Each slice of the XCFramework, and the Rust targets lipo combines into it.
    $slices = [ordered]@{
        "macos"         = @("aarch64-apple-darwin", "x86_64-apple-darwin")
        "ios"           = @("aarch64-apple-ios")
        "ios-simulator" = @("aarch64-apple-ios-sim", "x86_64-apple-ios")
    }
    $work = Join-Path "target" "xcframework"
    if (Test-Path $work) { Remove-Item $work -Recurse -Force }
    $headers = Join-Path $work "include"
    New-Item -ItemType Directory -Force $headers | Out-Null
    Copy-Item include/corvus_json_schema.h $headers
    Set-Content -Path (Join-Path $headers "module.modulemap") -Value @(
        "module CCorvusJsonSchema {",
        "    header `"corvus_json_schema.h`"",
        "    export *",
        "}"
    )

    $arguments = [System.Collections.Generic.List[string]]::new()
    foreach ($slice in $slices.Keys) {
        $libraries = foreach ($t in $slices[$slice]) {
            Write-Host "Building the static library for $t"
            Invoke-Native "cargo" @("rustc", "--lib", "--crate-type", "staticlib", "--release", "--locked", "--target", $t) | Out-Null
            Join-Path "target" $t "release" "libcorvus_json_schema.a"
        }
        $library = Join-Path $work $slice "libcorvus_json_schema.a"
        New-Item -ItemType Directory -Force (Split-Path $library) | Out-Null
        Invoke-Native "lipo" (@("-create", "-output", $library) + @($libraries)) | Out-Null
        $arguments.AddRange([string[]]@("-library", $library, "-headers", $headers))
    }

    $framework = Join-Path $work "CorvusJsonSchema.xcframework"
    Invoke-Native "xcodebuild" (@("-create-xcframework") + $arguments + @("-output", $framework)) | Out-Null

    New-Item -ItemType Directory -Force $OutDir | Out-Null
    $archive = Join-Path (Resolve-Path $OutDir) "CorvusJsonSchema.xcframework.zip"
    Remove-Item $archive -ErrorAction SilentlyContinue
    # ditto keeps the framework's layout (symbolic links, if any) as SwiftPM expects; the archive holds the
    # .xcframework directory at its root.
    Invoke-Native "ditto" @("-c", "-k", "--sequesterRsrc", "--keepParent", $framework, $archive) | Out-Null
    $checksum = ((Invoke-Native "swift" @("package", "compute-checksum", $archive)) | Select-Object -Last 1).Trim()
    Set-Content -Path "$archive.sha256" -Value $checksum -NoNewline
    Write-Host "Packaged $archive (checksum $checksum)"
    Write-Output $archive
} finally {
    Pop-Location
}
