<#
.SYNOPSIS
Builds the corvus-json-schema C library and lays it out as a release package.

.DESCRIPTION
Builds the shared and static libraries (cargo, release profile) for a Rust target, and writes
<OutDir>/corvus-json-schema-<version>-<target>/ with:

  include/                       corvus_json_schema.h and the C++ wrapper corvus_json_schema.hpp
  lib/ (and bin/ on Windows)     the shared library (with its import library on Windows) and the static library; for
                                 MSVC also corvus_json_schema_mt.lib, the static library for the static C runtime (/MT)
  lib/cmake/corvus_json_schema/  the CMake package: corvus_json_schema::corvus_json_schema (shared) and
                                 corvus_json_schema::corvus_json_schema_static, with a version file
  lib/pkgconfig/                 corvus-json-schema.pc
  LICENSE, DESIGN.md

The static library needs the system libraries the Rust standard library uses; they are asked of the compiler
(--print native-static-libs) and written into the CMake package and the pkg-config file.

.PARAMETER Target
The Rust target triple (default: the host's), or universal2-apple-darwin for one macOS package holding both
architectures (built on macOS: both targets, combined with lipo).

.PARAMETER Zig
Build with cargo-zigbuild (zig as the linker), to cross-build Linux targets such as the musl ones from a glibc host.

.PARAMETER Glibc
With -Zig, the oldest glibc the shared library may need (such as 2.17, the manylinux2014 baseline): it links against
that version's symbols, so it loads on any distribution with that glibc or newer.

.PARAMETER OutDir
Where to write the package (default: dist, next to this script).

.EXAMPLE
pwsh package.ps1
pwsh package.ps1 -Target aarch64-unknown-linux-gnu -OutDir /tmp/packages
#>
[CmdletBinding()]
param(
    [string] $Target,
    [string] $OutDir = (Join-Path $PSScriptRoot "dist"),
    [switch] $Zig,
    [string] $Glibc
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
    if (-not $Target) {
        $Target = ((Invoke-Native "rustc" @("-vV")) | Where-Object { $_ -like "host: *" }) -replace "^host: ", ""
    }
    $version = ((Get-Content Cargo.toml) | Where-Object { $_ -match '^version = "(.+)"$' } | Select-Object -First 1) -replace '^version = "(.+)"$', '$1'
    $targetIsWindows = $Target -like "*-windows-*"
    $targetIsMac = $Target -like "*-apple-*"
    $targetIsMsvc = $Target -like "*-msvc"

    # A musl shared library links the C runtime dynamically (the musl targets link it statically by default, which a
    # shared library cannot); the static library is unaffected.
    if ($Target -like "*-musl*") {
        $env:RUSTFLAGS = "$env:RUSTFLAGS -C target-feature=-crt-static".Trim()
    }
    $build = if ($Zig) { "zigbuild" } else { "build" }
    $targets = @(if ($Target -eq "universal2-apple-darwin") { "aarch64-apple-darwin", "x86_64-apple-darwin" } else { $Target })
    foreach ($t in $targets) {
        Write-Host "Building corvus_json_schema $version for $t"
        # cargo-zigbuild takes the glibc version as a suffix on the target (the output directory is the target's).
        $buildTarget = if ($Glibc) { "$t.$Glibc" } else { $t }
        Invoke-Native "cargo" @($build, "--lib", "--release", "--locked", "--target", $buildTarget) | Out-Null
    }
    # For MSVC, a second static library linked against the static C runtime (/MT), built apart so that the first is
    # untouched: the default one matches only the dynamic runtime (/MD).
    $staticMt = ""
    if ($targetIsMsvc) {
        $saved = $env:RUSTFLAGS
        $env:RUSTFLAGS = "$saved -C target-feature=+crt-static".Trim()
        $env:CARGO_TARGET_DIR = Join-Path "target" "crt-static"
        try {
            Invoke-Native "cargo" @("rustc", "--lib", "--crate-type", "staticlib", "--release", "--locked", "--target", $Target) | Out-Null
        } finally {
            $env:RUSTFLAGS = $saved
            Remove-Item Env:CARGO_TARGET_DIR
        }
        $staticMt = "lib/corvus_json_schema_mt.lib"
    }
    # The system libraries a program linking the static library needs (building a static library links nothing, so
    # this needs no cross linker).
    $cargoArgs = @("--release", "--locked", "--target", $targets[0])
    $printed = Invoke-Native "cargo" (@("rustc", "--lib", "--crate-type", "staticlib") + $cargoArgs + @("--", "--print", "native-static-libs"))
    $line = $printed | Where-Object { "$_" -match "native-static-libs: " } | Select-Object -Last 1
    if (-not $line) { throw "rustc did not print the native static libraries" }
    # An array (@(...)): a single library would otherwise be a string, indexed by character below.
    $nativeLibs = @(("$line" -replace "^.*native-static-libs: ", "").Trim() -split "\s+" | Where-Object { $_ })

    # CMake wants library names (and frameworks as "-framework X"); pkg-config wants linker flags.
    $cmakeLibs = [System.Collections.Generic.List[string]]::new()
    $pcFlags = [System.Collections.Generic.List[string]]::new()
    for ($i = 0; $i -lt $nativeLibs.Count; $i++) {
        $lib = $nativeLibs[$i]
        if ($lib -eq "-framework") {
            $cmakeLibs.Add("-framework $($nativeLibs[$i + 1])")
            $pcFlags.Add("-framework $($nativeLibs[$i + 1])")
            $i++
        } elseif ($lib -like "-l*") {
            $cmakeLibs.Add($lib.Substring(2))
            $pcFlags.Add($lib)
        } elseif ($lib -like "/defaultlib:*") {
            # The C runtime: CMake's and MSVC's default (the dynamic one) is the one Rust links against.
            continue
        } elseif ($lib -like "*.lib") {
            $cmakeLibs.Add($lib)
            $pcFlags.Add($lib)
        } else {
            $cmakeLibs.Add($lib)
            $pcFlags.Add($lib)
        }
    }

    $built = Join-Path "target" $Target "release"
    if ($targets.Count -gt 1) {
        New-Item -ItemType Directory -Force $built | Out-Null
        foreach ($file in @("libcorvus_json_schema.dylib", "libcorvus_json_schema.a")) {
            $inputs = $targets | ForEach-Object { Join-Path "target" $_ "release" $file }
            Invoke-Native "lipo" (@("-create", "-output", (Join-Path $built $file)) + $inputs) | Out-Null
        }
    }
    $name = "corvus-json-schema-$version-$Target"
    $root = Join-Path $OutDir $name
    if (Test-Path $root) { Remove-Item $root -Recurse -Force }
    $cmakeDir = Join-Path $root "lib" "cmake" "corvus_json_schema"
    $pcDir = Join-Path $root "lib" "pkgconfig"
    foreach ($d in @("include", "lib")) { New-Item -ItemType Directory -Force (Join-Path $root $d) | Out-Null }
    New-Item -ItemType Directory -Force $cmakeDir, $pcDir | Out-Null

    Copy-Item include/corvus_json_schema.h, include/corvus_json_schema.hpp (Join-Path $root "include")
    Copy-Item LICENSE, DESIGN.md $root

    $importLibrary = ""
    if ($targetIsWindows) {
        New-Item -ItemType Directory -Force (Join-Path $root "bin") | Out-Null
        Copy-Item (Join-Path $built "corvus_json_schema.dll") (Join-Path $root "bin")
        if ($targetIsMsvc) {
            Copy-Item (Join-Path $built "corvus_json_schema.dll.lib") (Join-Path $root "lib")
            Copy-Item (Join-Path $built "corvus_json_schema.lib") (Join-Path $root "lib")
            Copy-Item (Join-Path "target" "crt-static" $Target "release" "corvus_json_schema.lib") (Join-Path $root $staticMt)
            $importLibrary = "lib/corvus_json_schema.dll.lib"
            $static = "lib/corvus_json_schema.lib"
        } else {
            Copy-Item (Join-Path $built "libcorvus_json_schema.dll.a") (Join-Path $root "lib")
            Copy-Item (Join-Path $built "libcorvus_json_schema.a") (Join-Path $root "lib")
            $importLibrary = "lib/libcorvus_json_schema.dll.a"
            $static = "lib/libcorvus_json_schema.a"
        }
        $shared = "bin/corvus_json_schema.dll"
    } else {
        $sharedName = if ($targetIsMac) { "libcorvus_json_schema.dylib" } else { "libcorvus_json_schema.so" }
        Copy-Item (Join-Path $built $sharedName) (Join-Path $root "lib")
        Copy-Item (Join-Path $built "libcorvus_json_schema.a") (Join-Path $root "lib")
        $shared = "lib/$sharedName"
        $static = "lib/libcorvus_json_schema.a"
    }

    $values = @{
        "@PLATFORM@"              = $Target
        "@VERSION@"               = $version
        "@SHARED_LOCATION@"       = $shared
        "@IMPORT_LIBRARY@"        = $importLibrary
        "@STATIC_LOCATION@"       = $static
        "@STATIC_MT_LOCATION@"    = $staticMt
        "@STATIC_LINK_LIBRARIES@" = ($cmakeLibs -join ";")
        "@STATIC_LINK_FLAGS@"     = ($pcFlags -join " ")
    }
    foreach ($t in @(
            @("cmake/corvus_json_schemaConfig.cmake.in", (Join-Path $cmakeDir "corvus_json_schemaConfig.cmake")),
            @("cmake/corvus_json_schemaConfigVersion.cmake.in", (Join-Path $cmakeDir "corvus_json_schemaConfigVersion.cmake")),
            @("cmake/corvus-json-schema.pc.in", (Join-Path $pcDir "corvus-json-schema.pc")))) {
        $text = Get-Content $t[0] -Raw
        foreach ($k in $values.Keys) { $text = $text.Replace($k, $values[$k]) }
        Set-Content -Path $t[1] -Value $text -NoNewline
    }

    Write-Host "Packaged $root"
    Write-Output $root
} finally {
    Pop-Location
}
