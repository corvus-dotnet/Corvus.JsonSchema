<#
.SYNOPSIS
Builds the corvus_json_schema PHP extension for the PHP on the PATH and packages it as PIE expects.

.DESCRIPTION
Builds the extension (cargo, release profile, for the host) against the PHP that `php` and `php-config` on the PATH
name (its version, and whether it is thread-safe, decide the build), and writes to OutDir the archive PIE looks for in
a release's assets:

  Linux and macOS  php_corvus_json_schema-<version>_php<X.Y>-<arch>-<os>-<libc>[-debug][-zts].zip, holding
                   corvus_json_schema.so
  Windows          php_corvus_json_schema-<version>-<X.Y>-<ts|nts>-<compiler>-<arch>.zip, holding a DLL of the same
                   name

and prints the archive's path.

On Windows, ext-php-rs needs a nightly Rust (for the vectorcall calling convention PHP uses) and the PHP SDK's
development package, which its build script downloads for the PHP on the PATH.

.PARAMETER Glibc
On Linux (glibc), the oldest glibc the extension may need (such as 2.17, the manylinux2014 baseline): the extension is
linked by zig (which must be on the PATH) against that version's symbols, so it loads on any distribution with that
glibc or newer. The PHP headers are read, and the C wrapper compiled, by the system's tools as usual.

.PARAMETER Version
The version in the archive's name (default: the version in Cargo.toml). It must be the release's tag.

.PARAMETER OutDir
Where to write the archive (default: dist, next to this script).

.EXAMPLE
pwsh package.ps1 -Glibc 2.17
#>
[CmdletBinding()]
param(
    [string] $Glibc,
    [string] $Version,
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
    if (-not $Version) {
        $Version = ((Get-Content Cargo.toml) | Where-Object { $_ -match '^version = "(.+)"$' } | Select-Object -First 1) -replace '^version = "(.+)"$', '$1'
    }
    $target = ((Invoke-Native "rustc" @("-vV")) | Where-Object { $_ -like "host: *" }) -replace "^host: ", ""
    $php = @((Invoke-Native "php" @("-n", "-r", 'echo PHP_MAJOR_VERSION . "." . PHP_MINOR_VERSION, "\n", PHP_ZTS, "\n", PHP_DEBUG, "\n", PHP_OS_FAMILY, "\n", php_uname("m");')) -split "`n" | ForEach-Object { $_.Trim() })
    $phpVersion = $php[0]
    $zts = $php[1] -eq "1"
    $debug = $php[2] -eq "1"
    $os = $php[3]
    $arch = switch -Regex ($php[4]) {
        "^(x86_64|x64|AMD64)$" { "x86_64" }
        "^(arm64|aarch64|ARM64)$" { "arm64" }
        default { throw "Unsupported architecture $($php[4])" }
    }
    Write-Host "Building corvus_json_schema $Version for PHP $phpVersion ($(if ($zts) { 'ZTS' } else { 'NTS' })$(if ($debug) { ', debug' })) on $target"

    if ($target -like "*-musl*") {
        # A musl shared library links the C runtime dynamically (the musl targets link it statically by default, which
        # a shared library cannot).
        $env:RUSTFLAGS = "$env:RUSTFLAGS -C target-feature=-crt-static".Trim()
    }
    if ($Glibc) {
        if ($target -notlike "*-linux-gnu") {
            throw "-Glibc applies to Linux glibc targets only, not $target"
        }
        # zig as the linker only: cargo-zigbuild would also hand bindgen zig's clang flags, which an older libclang
        # rejects. The wrapper drops -Wl,-O1, which zig ignores with a warning, and the Cortex-A53 erratum fix rustc
        # asks for on aarch64, which zig's linker rejects.
        $zigTarget = "$($target.Split('-')[0])-linux-gnu.$Glibc"
        $linker = Join-Path $PSScriptRoot "target" "zig-cc-$zigTarget.sh"
        New-Item -ItemType Directory -Force (Split-Path $linker) | Out-Null
        Set-Content -Path $linker -Value @(
            "#!/bin/sh",
            "for a in `"`$@`"; do shift; case `"`$a`" in -Wl,-O1|-Wl,--fix-cortex-a53-843419) ;; *) set -- `"`$@`" `"`$a`" ;; esac; done",
            "exec zig cc -target $zigTarget `"`$@`""
        )
        Invoke-Native "chmod" @("+x", $linker) | Out-Null
        Set-Item "env:CARGO_TARGET_$($target.ToUpperInvariant().Replace('-', '_'))_LINKER" $linker
    }
    Invoke-Native "cargo" @("build", "--release", "--locked", "--target", $target) | Out-Null
    $built = Join-Path "target" $target "release"

    New-Item -ItemType Directory -Force $OutDir | Out-Null
    $stage = Join-Path ([System.IO.Path]::GetTempPath()) "corvus-json-schema-php-$([guid]::NewGuid())"
    New-Item -ItemType Directory $stage | Out-Null
    try {
        if ($os -eq "Windows") {
            # The compiler PHP was built with, as PIE names it: "PHP Extension Build => API20220829,NTS,VS16".
            $build = (Invoke-Native "php" @("-n", "-i")) | Where-Object { $_ -like "PHP Extension Build*" } | Select-Object -First 1
            if ($build -notmatch ",(VS\d+|VC\d+)\s*$") {
                throw "No compiler in '$build'"
            }
            $compiler = $Matches[1].ToLowerInvariant()
            $name = "php_corvus_json_schema-$Version-$phpVersion-$(if ($zts) { 'ts' } else { 'nts' })-$compiler-$arch"
            Copy-Item (Join-Path $built "corvus_json_schema.dll") (Join-Path $stage "$name.dll")
        } else {
            $osName = switch ($os) {
                "Linux" { "linux" }
                "Darwin" { "darwin" }
                default { throw "Unsupported operating system $os" }
            }
            $libc = if ($osName -eq "darwin") {
                "bsdlibc"
            } elseif ($target -like "*-musl*") {
                "musl"
            } else {
                "glibc"
            }
            $name = "php_corvus_json_schema-${Version}_php$phpVersion-$arch-$osName-$libc$(if ($debug) { '-debug' })$(if ($zts) { '-zts' })"
            $library = if ($osName -eq "darwin") { "libcorvus_json_schema.dylib" } else { "libcorvus_json_schema.so" }
            Copy-Item (Join-Path $built $library) (Join-Path $stage "corvus_json_schema.so")
        }
        $archive = Join-Path (Resolve-Path $OutDir) "$name.zip"
        Remove-Item $archive -ErrorAction SilentlyContinue
        Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $archive
        Write-Output $archive
    } finally {
        Remove-Item -Recurse -Force $stage
    }
} finally {
    Pop-Location
}
