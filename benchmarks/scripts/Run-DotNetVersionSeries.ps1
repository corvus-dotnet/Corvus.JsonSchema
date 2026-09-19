#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs the .NET version-over-version benchmark series (V4 and V5) pinned to a set of CPUs, with host health probes.
    By default it rotates short single-runtime processes (paired A/B). It can run BenchmarkDotNet instead.

.DESCRIPTION
    The series compares the same code on successive .NET runtimes, so the differences it looks for are a few percent.
    That needs a quiet machine and, on a hybrid CPU, a run that stays on the performance cores. This script:

      * builds the two harnesses in Release;
      * waits for the machine to settle after the build;
      * runs a health probe (Stopwatch-pair overhead and a fixed CPU-bound loop) before and after each series;
      * runs each series with its CPU affinity set to -CpuList;
      * with -Method Rotation (the default), runs each harness's 'ab' mode once per runtime per round, in turn, and
        reports the ratio between adjacent runtimes for each round (the paired ratio). Machine drift affects both
        sides of a pair alike, so the median paired ratio is the figure to publish;
      * with -Method BenchmarkDotNet, runs the full BenchmarkDotNet jobs. BenchmarkDotNet runs its jobs in order,
        directly after it builds them, so the first runtime is measured on a machine still hot from the build. Its
        figures for adjacent runtimes disagreed from run to run on the machine this was written on;
      * samples how many cores are busy while BenchmarkDotNet is measuring, and aborts the series if other
        work keeps more than -MaxBusyCores busy for three consecutive samples;
      * copies each BenchmarkDotNet log, and a series.log with the probes and samples, to -ResultsDirectory.

    Compare the probe lines between runs. If the overhead figure drifts upwards (by 20% or more) the host has slowed
    down, and that run's absolute figures are not comparable with a healthy run's.

    Under WSL2 the guest's CPU numbers have no fixed relation to the host's cores, so -CpuList alone does not keep
    the run on the performance cores. Set the affinity of the vmmemWSL process on the Windows host first, from an
    elevated PowerShell, and re-apply it after every 'wsl --shutdown'. For a CPU whose performance cores are logical
    processors 0 to 11:

        (Get-Process vmmemWSL).ProcessorAffinity = 0xFFF

    Keep the host's display awake for the duration. Some laptops drop to a slower mode when it sleeps.

.PARAMETER Series
    Which series to run. V4 is src-v4/Corvus.Json.Benchmarking (.NET 8.0 onwards). V5 is
    benchmarks/Corvus.Text.Json.DotNetVersions.Benchmarks (.NET 10.0 onwards).

.PARAMETER Method
    Rotation (paired A/B, the default) or BenchmarkDotNet.

.PARAMETER Rounds
    The number of rotation rounds. Each round runs every runtime once.

.PARAMETER MeasureSeconds
    How long each rotation process measures for, after its warm-up.

.PARAMETER CpuList
    The logical CPUs to run on, as a comma-separated list of numbers and ranges. The default, 0-11, is the performance
    cores of an Intel Core i7-13800H.

.PARAMETER SettleSeconds
    How long to wait after the build before measuring.

.PARAMETER MaxBusyCores
    The number of busy cores, sampled over five seconds while a benchmark is measuring, above which a sample counts
    against the run. The benchmark itself keeps about one core busy.

.PARAMETER ResultsDirectory
    Where to write the logs. Defaults to a timestamped directory under BenchmarkDotNet.Artifacts/series at the repo root.

.PARAMETER MaxCpuCount
    Limits every build to this many MSBuild nodes (-m:N), including the builds BenchmarkDotNet runs for each runtime
    (the build arguments are written to a Directory.Build.rsp in the harness output directory).

.PARAMETER NoNodeReuse
    Turns MSBuild node reuse off (-nr:false) for every build, in the same way.

.PARAMETER SkipBuild
    Do not build the harnesses first.

.EXAMPLE
    pwsh benchmarks/scripts/Run-DotNetVersionSeries.ps1

.EXAMPLE
    pwsh benchmarks/scripts/Run-DotNetVersionSeries.ps1 -Series V5 -MaxCpuCount 4 -NoNodeReuse

.NOTES
    Location: benchmarks/scripts/Run-DotNetVersionSeries.ps1
#>
[CmdletBinding()]
param(
    [ValidateSet('V4', 'V5')]
    [string[]]$Series = @('V4', 'V5'),

    [ValidateSet('Rotation', 'BenchmarkDotNet')]
    [string]$Method = 'Rotation',

    [int]$Rounds = 10,

    [int]$MeasureSeconds = 5,

    [string]$CpuList = '0-11',

    [int]$SettleSeconds = 120,

    [double]$MaxBusyCores = 3.0,

    [string]$ResultsDirectory,

    [int]$MaxCpuCount,

    [switch]$NoNodeReuse,

    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path

$BuildArguments = @()
if ($MaxCpuCount -gt 0) { $BuildArguments += "-m:$MaxCpuCount" }
if ($NoNodeReuse) { $BuildArguments += '-nr:false' }

$harnesses = @{
    V4 = @{
        Project  = Join-Path $repoRoot 'src-v4' 'Corvus.Json.Benchmarking' 'Corvus.Json.Benchmarking.csproj'
        Assembly = 'Corvus.Json.Benchmarking.dll'
        Filter   = '*ValidateLargeDocumentCorvusOnly.ValidateLargeArrayCorvusV4'
        Runtimes = @(8, 9, 10, 11)
        Cases    = @('V4')
    }
    V5 = @{
        Project  = Join-Path $repoRoot 'benchmarks' 'Corvus.Text.Json.DotNetVersions.Benchmarks' 'Corvus.Text.Json.DotNetVersions.Benchmarks.csproj'
        Assembly = 'Corvus.Text.Json.DotNetVersions.Benchmarks.dll'
        Filter   = '*'
        Runtimes = @(10, 11)
        Cases    = @('GeneratedTypes', 'StandaloneEvaluator', 'DynamicValidator')
    }
}

if (-not $ResultsDirectory) {
    $ResultsDirectory = Join-Path $repoRoot 'BenchmarkDotNet.Artifacts' 'series' (Get-Date -Format 'yyyyMMdd-HHmmss')
}

New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null
$seriesLog = Join-Path $ResultsDirectory 'series.log'

function Write-SeriesLog([string]$message) {
    $line = "$(Get-Date -Format 'HH:mm:ss') $message"
    Add-Content -Path $seriesLog -Value $line
    Write-Host $line
}

# Expands '0-3,8' to 0,1,2,3,8.
function Expand-CpuList([string]$list) {
    foreach ($part in $list.Split(',')) {
        $bounds = $part.Trim().Split('-')
        ([int]$bounds[0])..([int]$bounds[-1])
    }
}

function Set-Affinity([System.Diagnostics.Process]$process) {
    [long]$mask = 0
    foreach ($cpu in Expand-CpuList $CpuList) {
        $mask = $mask -bor ([long]1 -shl $cpu)
    }

    $process.ProcessorAffinity = [IntPtr]$mask
}

# Starts dotnet with its affinity already set (taskset) where we can, so that the first instructions and every
# child process are pinned too. On Windows we set it immediately after the start. Children inherit it.
function Start-Pinned([string[]]$dotnetArguments, [string]$workingDirectory, [string]$outputPath) {
    $startArguments = @{
        WorkingDirectory       = $workingDirectory
        RedirectStandardOutput = $outputPath
        RedirectStandardError  = "$outputPath.err"
        PassThru               = $true
        NoNewWindow            = $true
    }

    if ($IsLinux) {
        return Start-Process -FilePath 'taskset' -ArgumentList (@('-c', $CpuList, 'dotnet') + $dotnetArguments) @startArguments
    }

    $process = Start-Process -FilePath 'dotnet' -ArgumentList $dotnetArguments @startArguments
    Set-Affinity $process
    return $process
}

# The number of busy cores over a five second window. This is instantaneous, unlike the load average, which is
# still decaying from BenchmarkDotNet's own builds when the first benchmark starts to measure.
function Get-BusyCores {
    if ($IsLinux) {
        $first = (Get-Content /proc/stat -TotalCount 1) -split '\s+' | Select-Object -Skip 1 -First 5 | ForEach-Object { [long]$_ }
        Start-Sleep -Seconds 5
        $second = (Get-Content /proc/stat -TotalCount 1) -split '\s+' | Select-Object -Skip 1 -First 5 | ForEach-Object { [long]$_ }
        $total = ($second | Measure-Object -Sum).Sum - ($first | Measure-Object -Sum).Sum
        $idle = ($second[3] + $second[4]) - ($first[3] + $first[4])
        return [Math]::Round(($total - $idle) / $total * [Environment]::ProcessorCount, 1)
    }

    $sample = Get-Counter '\Processor(_Total)\% Processor Time' -SampleInterval 5 -MaxSamples 1
    return [Math]::Round($sample.CounterSamples[0].CookedValue / 100 * [Environment]::ProcessorCount, 1)
}

$probeSource = @'
using System.Diagnostics;

static double Overhead()
{
    const int N = 20_000_000;
    long acc = 0;
    long start = Stopwatch.GetTimestamp();
    for (int i = 0; i < N; i++)
    {
        long a = Stopwatch.GetTimestamp();
        long b = Stopwatch.GetTimestamp();
        acc += b - a;
    }

    long end = Stopwatch.GetTimestamp();
    GC.KeepAlive(acc);
    return (end - start) * 1e9 / Stopwatch.Frequency / N;
}

static double Spin()
{
    long start = Stopwatch.GetTimestamp();
    ulong x = 88172645463325252UL;
    for (int i = 0; i < 300_000_000; i++)
    {
        x ^= x << 13;
        x ^= x >> 7;
        x ^= x << 17;
    }

    GC.KeepAlive(x);
    return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
}

Overhead();
Spin();
Console.WriteLine($"overhead={Overhead():F1}ns spin={Spin():F0}ms");
'@

$probeProject = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <TieredPGO>false</TieredPGO>
  </PropertyGroup>
</Project>
'@

# The probe is built outside the repo so that the repo's build props and analyzers do not apply to it.
function Build-Probe {
    $probeDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "corvus-series-probe-$PID"
    New-Item -ItemType Directory -Force -Path $probeDirectory | Out-Null
    Set-Content -Path (Join-Path $probeDirectory 'Program.cs') -Value $probeSource
    Set-Content -Path (Join-Path $probeDirectory 'probe.csproj') -Value $probeProject
    $output = Join-Path $probeDirectory 'out'
    & dotnet build (Join-Path $probeDirectory 'probe.csproj') -c Release -o $output @BuildArguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The health probe failed to build.' }
    return Join-Path $output 'probe.dll'
}

function Invoke-Probe([string]$label) {
    $probeOutput = Join-Path $ResultsDirectory 'probe.out'
    $process = Start-Pinned -dotnetArguments @($probeAssembly) -workingDirectory $ResultsDirectory -outputPath $probeOutput
    $process.WaitForExit()
    Write-SeriesLog "probe[$label] $((Get-Content $probeOutput -Raw).Trim())"
    Remove-Item $probeOutput, "$probeOutput.err" -ErrorAction SilentlyContinue
}

function Invoke-Series([string]$name) {
    $harness = $harnesses[$name]
    $outputDirectory = Join-Path (Split-Path $harness.Project) 'bin' 'Release' 'net10.0'
    $log = Join-Path $ResultsDirectory "$($name.ToLowerInvariant()).log"

    # Stale Job-* directories make BenchmarkDotNet drop benchmarks silently.
    Get-ChildItem $outputDirectory -Directory -Filter 'Job-*' -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
    Remove-Item (Join-Path $outputDirectory 'BenchmarkDotNet.Artifacts') -Recurse -Force -ErrorAction SilentlyContinue

    $responseFile = Join-Path $outputDirectory 'Directory.Build.rsp'
    if ($BuildArguments.Count -gt 0) {
        Set-Content -Path $responseFile -Value $BuildArguments
    }
    else {
        Remove-Item $responseFile -ErrorAction SilentlyContinue
    }

    Invoke-Probe "before $name"
    $process = Start-Pinned -dotnetArguments @($harness.Assembly, '--filter', $harness.Filter) -workingDirectory $outputDirectory -outputPath $log
    $busySamples = 0

    while (-not $process.HasExited) {
        Start-Sleep -Seconds 30

        # Only judge the machine while a benchmark is measuring, not while BenchmarkDotNet is building.
        $lastLine = Get-Content $log -Tail 1 -ErrorAction SilentlyContinue
        if ($lastLine -match '^(Workload|Overhead)') {
            $busy = Get-BusyCores
            Write-SeriesLog "$name busycores=$busy"
            $busySamples = $busy -gt $MaxBusyCores ? $busySamples + 1 : 0
            if ($busySamples -ge 3) {
                Write-SeriesLog "ABORT $name busycores=$busy"
                $process.Kill($true)
                throw "The $name series was aborted. Other work kept more than $MaxBusyCores cores busy."
            }
        }
    }

    Invoke-Probe "after $name"
    Remove-Item "$log.err" -ErrorAction SilentlyContinue

    Write-Host ''
    Get-Content $log | Where-Object { $_ -match '^\| ' -or $_ -match '^\s+(\[Host\]|\.NET|Job-)' } | Write-Host
    Write-Host ''
}

# The newest installed Microsoft.NETCore.App version for a major version, for dotnet exec --fx-version.
function Get-RuntimeVersion([int]$major) {
    $versions = & dotnet --list-runtimes |
        Where-Object { $_ -match "^Microsoft\.NETCore\.App $major\.\S+" } |
        ForEach-Object { ($_ -split ' ')[1] }
    if (-not $versions) { throw "No .NET $major runtime is installed." }
    return $versions | Sort-Object { [version]($_ -replace '-.*$', '') } | Select-Object -Last 1
}

function Get-Median([double[]]$values) {
    $sorted = $values | Sort-Object
    return $sorted[[int][Math]::Floor($sorted.Count / 2)]
}

# Runs the harness's 'ab' mode once per runtime per round. A runtime uses the build for its own TFM where the
# harness has one, and otherwise the newest build before it (so .NET 11 runs the net10.0 binaries unchanged).
function Invoke-Rotation([string]$name) {
    $harness = $harnesses[$name]
    $binDirectory = Join-Path (Split-Path $harness.Project) 'bin' 'Release'
    $log = Join-Path $ResultsDirectory "$($name.ToLowerInvariant())-rotation.log"
    $output = Join-Path $ResultsDirectory 'rotation.out'

    $runtimes = foreach ($major in $harness.Runtimes) {
        $tfmMajor = $major
        while ($tfmMajor -gt 0 -and -not (Test-Path (Join-Path $binDirectory "net$tfmMajor.0" $harness.Assembly))) { $tfmMajor-- }
        if ($tfmMajor -eq 0) { throw "No build of $($harness.Assembly) can run on .NET $major." }
        @{ Major = $major; Version = Get-RuntimeVersion $major; Directory = Join-Path $binDirectory "net$tfmMajor.0" }
    }

    Invoke-Probe "before $name"

    foreach ($case in $harness.Cases) {
        $label = $case -eq $name ? $name : "$name $case"
        $medians = @{}
        foreach ($runtime in $runtimes) { $medians[$runtime.Major] = [System.Collections.Generic.List[double]]::new() }
        foreach ($round in 1..$Rounds) {
            foreach ($runtime in $runtimes) {
                $caseArguments = $case -eq 'V4' ? @() : @($case)
                $arguments = @('exec', '--fx-version', $runtime.Version, $harness.Assembly, 'ab') + $caseArguments + @("$MeasureSeconds")
                $process = Start-Pinned -dotnetArguments $arguments -workingDirectory $runtime.Directory -outputPath $output
                $process.WaitForExit()
                $line = (Get-Content $output -Raw).Trim()
                Add-Content -Path $log -Value $line
                if ($line -notmatch 'median=([\d.]+)') { throw "The $name harness failed on .NET $($runtime.Major): $line" }
                $medians[$runtime.Major].Add([double]$Matches[1])
            }

            Write-SeriesLog "$label round $round of $Rounds"
        }

        Write-Host ''
        foreach ($runtime in $runtimes) {
            Write-SeriesLog ('{0} .NET {1}: median of medians {2:F4} ms' -f $label, $runtime.Major, (Get-Median $medians[$runtime.Major]))
        }

        for ($i = 1; $i -lt $runtimes.Count; $i++) {
            $from = $runtimes[$i - 1].Major
            $to = $runtimes[$i].Major
            $ratios = 0..($Rounds - 1) | ForEach-Object { $medians[$to][$_] / $medians[$from][$_] }
            $ratio = Get-Median $ratios
            Write-SeriesLog ('{0} .NET {1} -> {2}: paired ratio {3:F3} (range {4:F3} to {5:F3}), {6:F1}% {7}' -f $label, $from, $to, $ratio, ($ratios | Measure-Object -Minimum).Minimum, ($ratios | Measure-Object -Maximum).Maximum, ([Math]::Abs(1 - $ratio) * 100), ($ratio -lt 1 ? 'faster' : 'slower'))
        }

        Write-Host ''
    }

    Remove-Item $output, "$output.err" -ErrorAction SilentlyContinue
    Invoke-Probe "after $name"
}

if (-not $SkipBuild) {
    foreach ($name in $Series) {
        # The rotation runs each runtime on the build for its own TFM, so it needs them all.
        $frameworkArguments = $Method -eq 'Rotation' ? @() : @('-f', 'net10.0')
        & dotnet build $harnesses[$name].Project -c Release @frameworkArguments @BuildArguments
        if ($LASTEXITCODE -ne 0) { throw "The $name harness failed to build." }
    }
}

$probeAssembly = Build-Probe

Write-SeriesLog "settling for $SettleSeconds s (cpus $CpuList)"
Start-Sleep -Seconds $SettleSeconds

foreach ($name in $Series) {
    if ($Method -eq 'Rotation') { Invoke-Rotation $name } else { Invoke-Series $name }
}

Write-SeriesLog 'SERIES_DONE'
Write-Host "Results: $ResultsDirectory"
