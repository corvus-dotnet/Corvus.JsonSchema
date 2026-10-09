<#
.SYNOPSIS
Builds every test program of the Object Pascal port with Free Pascal and runs them all.

.DESCRIPTION
Each program of the tests directory is built into build/ (units in build/units) with the compiler given, and run
from the package directory, which is where the programs look for the JSON-Schema-Test-Suite submodule and the Go
module from (../../JSON-Schema-Test-Suite and ../../src-go/corvus-json-schema: set JSON_SCHEMA_TEST_SUITE or
CORVUS_GO_MODULE to use others).

The exit code is 0 when every program built and passed, and 1 otherwise. A warning of the compiler is printed and
does not fail the run.

TestDifferential needs a checkout of https://github.com/sourcemeta-research/jsonschema-benchmark, named by
JSONSCHEMA_BENCHMARK, and reports that it was skipped without one. TestDoubleCases and TestStringCases read cases
that a reference implementation wrote: they are built always, and run only when their file is given.

TestReadme runs the samples of README.md and of ../../docs/JsonSchemaForPascal.md, and fails when a sample in either
document is not in the program.

.EXAMPLE
pwsh tests/run-tests.ps1
pwsh tests/run-tests.ps1 -Fpc ~/sdk/fpc-3.2.2/bin/fpc
pwsh tests/run-tests.ps1 -Only TestSuite, TestResults
pwsh tests/run-tests.ps1 -HeapTrace -BuildDirectory build/heap
#>
[CmdletBinding()]
param(
    # The Free Pascal compiler. The default is the fpc on the PATH.
    [string] $Fpc = "fpc",

    # The programs to build and run. The default is all of them.
    [string[]] $Only = @(),

    # The directory the programs are built into (its units directory holds the compiled units). The default is the
    # package's build directory.
    [string] $BuildDirectory = "",

    # More options for the compiler, in place of -O3.
    [string[]] $Options = @("-O3"),

    # Build with the heap trace unit (-gh) and fail a program that leaves memory unfreed.
    [switch] $HeapTrace,

    # The file of cases for TestDoubleCases (number text and the bits of its double).
    [string] $DoubleCases = "",

    # The file of cases for TestStringCases (JSON strings and what a reference reads).
    [string] $StringCases = ""
)

$ErrorActionPreference = "Stop"
$package = Split-Path -Parent $PSScriptRoot
# (A PowerShell variable's name is not case sensitive, so the directory in use has a name of its own.)
$target = Join-Path $package "build"
if ($BuildDirectory -ne "") {
    $target = $BuildDirectory
}
New-Item -ItemType Directory -Force -Path $target | Out-Null
$target = (Resolve-Path $target).Path
$units = Join-Path $target "units"
New-Item -ItemType Directory -Force -Path $units | Out-Null

# The programs, the quick ones first. Each value is the arguments the program is run with, or $null for a program
# that is built and not run.
$programs = [ordered]@{
    TestChecked      = @()
    TestDocument     = @()
    TestUcd          = @()
    TestEcmaRegex    = @()
    TestUri          = @()
    TestFormats      = @()
    TestSchemaSide   = @()
    TestCompileSuite = @()
    TestSuite        = @()
    TestResults      = @()
    TestApi          = @()
    TestAllocations  = @()
    TestReadme       = @()
    TestDifferential = @()
    TestDoubleCases  = $null
    TestStringCases  = $null
}
if ($DoubleCases -ne "") {
    $programs.TestDoubleCases = @((Resolve-Path $DoubleCases).Path)
}
if ($StringCases -ne "") {
    $programs.TestStringCases = @((Resolve-Path $StringCases).Path)
}

$names = @($programs.Keys)
# Started with pwsh -File, a list arrives as one string with commas in it.
$Only = @($Only | ForEach-Object { $_ -split "," } | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" })
if ($Only.Count -gt 0) {
    $unknown = @($Only | Where-Object { $names -notcontains $_ })
    if ($unknown.Count -gt 0) {
        throw "Unknown test program: $($unknown -join ', ')"
    }
    $names = @($names | Where-Object { $Only -contains $_ })
}

$compilerOptions = @($Options)
if ($HeapTrace) {
    $compilerOptions += @("-gh", "-gl")
}
# A program that uses threads links with the C library, whose start files Free Pascal looks for in its library
# path: the C compiler says where they are.
$cc = @(Get-Command gcc, cc -ErrorAction SilentlyContinue | Select-Object -First 1)
if ($cc.Count -gt 0) {
    $startFile = & $cc[0].Source "-print-file-name=crtbegin.o"
    if ($LASTEXITCODE -eq 0 -and $startFile -and (Test-Path $startFile)) {
        $compilerOptions += "-Fl$(Split-Path -Parent $startFile)"
    }
}

$failed = @()
Push-Location $package
try {
    foreach ($name in $names) {
        Write-Host "== $name"
        $arguments = @($compilerOptions) + @("-Fusrc", "-Fisrc", "-FU$units", "-FE$target", (Join-Path "tests" "$name.pas"))
        $output = @(& $Fpc @arguments 2>&1)
        if ($LASTEXITCODE -ne 0) {
            $output | ForEach-Object { Write-Host $_ }
            Write-Host "FAILED: $name did not build"
            $failed += $name
            continue
        }
        @($output | Where-Object { "$_" -match "Warning:|Error:|Fatal:" }) | ForEach-Object { Write-Host $_ }

        $run = $programs[$name]
        if ($null -eq $run) {
            Write-Host "built (not run: it reads a file of cases, see -DoubleCases and -StringCases)"
            continue
        }
        $executable = Join-Path $target $name
        if ($IsWindows) {
            $executable += ".exe"
        }
        # The heap trace unit writes its report to the file HEAPTRC names.
        $trace = Join-Path $target "$name.heaptrc"
        if ($HeapTrace) {
            Remove-Item -Force -ErrorAction SilentlyContinue $trace
            $env:HEAPTRC = "log=$trace"
        }
        $lines = @(& $executable @run 2>&1)
        $exitCode = $LASTEXITCODE
        # A long run prints a line for each area. The failures and the last lines say what happened.
        $shown = @($lines | Where-Object { "$_" -match "^FAILED|expected .*, got " })
        $shown += @($lines | Select-Object -Last 4)
        $shown | Select-Object -Unique | ForEach-Object { Write-Host $_ }
        if ($exitCode -ne 0) {
            Write-Host "FAILED: $name exited with code $exitCode"
            $failed += $name
            continue
        }
        if ($HeapTrace) {
            $report = @()
            if (Test-Path $trace) {
                $report = @(Get-Content $trace)
            }
            $unfreed = @($report | Where-Object { "$_" -match "unfreed memory blocks" })
            $unfreed | ForEach-Object { Write-Host $_ }
            if (@($unfreed | Where-Object { "$_" -match "^0 unfreed memory blocks" }).Count -eq 0) {
                Write-Host "FAILED: $name left memory unfreed (or wrote no heap trace)"
                $failed += $name
            }
        }
    }
}
finally {
    if ($HeapTrace) {
        Remove-Item Env:HEAPTRC -ErrorAction SilentlyContinue
    }
    Pop-Location
}

if ($failed.Count -gt 0) {
    Write-Host "$($failed.Count) of $($names.Count) test programs failed: $($failed -join ', ')"
    exit 1
}
Write-Host "All $($names.Count) test programs passed."
exit 0
