<#
.SYNOPSIS
    Parses BenchmarkDotNet JSON results, compares against a previous baseline,
    and writes a summary table to the GitHub Actions step summary.

.PARAMETER ResultsDir
    Path to the BenchmarkDotNet.Artifacts/results directory.

.PARAMETER BaselineDir
    Path to the previous run's results directory (optional).

.PARAMETER OutputPath
    Path to write the markdown summary. Defaults to $env:GITHUB_STEP_SUMMARY.

.PARAMETER RegressionThreshold
    Percentage increase in Mean that is considered a regression. Default: 5.
#>
param(
    [Parameter(Mandatory)]
    [string] $ResultsDir,

    [Parameter()]
    [string] $BaselineDir,

    [Parameter()]
    [string] $OutputPath,

    [Parameter()]
    [double] $RegressionThreshold = 5.0
)

$ErrorActionPreference = 'Stop'

function Format-Duration([double]$ns) {
    if ($ns -ge 1e9)     { return "{0:N2} s"  -f ($ns / 1e9) }
    if ($ns -ge 1e6)     { return "{0:N2} ms" -f ($ns / 1e6) }
    if ($ns -ge 1e3)     { return "{0:N2} μs" -f ($ns / 1e3) }
    return "{0:N2} ns" -f $ns
}

function Format-Bytes([double]$bytes) {
    if ($bytes -eq 0)    { return "-" }
    if ($bytes -ge 1MB)  { return "{0:N2} MB" -f ($bytes / 1MB) }
    if ($bytes -ge 1KB)  { return "{0:N2} KB" -f ($bytes / 1KB) }
    return "{0:N0} B" -f $bytes
}

# Collect all JSON result files
$jsonFiles = Get-ChildItem $ResultsDir -Filter "*-report-full.json" -ErrorAction SilentlyContinue
if (-not $jsonFiles) {
    Write-Warning "No JSON result files found in $ResultsDir"
    exit 0
}

# Parse current results into a flat list
$currentResults = @{}
foreach ($file in $jsonFiles) {
    $report = Get-Content $file.FullName -Raw | ConvertFrom-Json
    foreach ($benchmark in $report.Benchmarks) {
        $key = $benchmark.FullName
        $mean = $benchmark.Statistics.Mean
        $allocated = 0
        $allocMetric = $benchmark.Metrics | Where-Object { $_.Descriptor.Id -eq "Allocated Memory" }
        if ($allocMetric) {
            $allocated = $allocMetric.Value
        }
        # Also check Memory property
        if ($benchmark.Memory -and $benchmark.Memory.BytesAllocatedPerOperation) {
            $allocated = $benchmark.Memory.BytesAllocatedPerOperation
        }
        $currentResults[$key] = @{
            Method    = $benchmark.Method
            Type      = $benchmark.Type
            Mean      = $mean
            Error     = $benchmark.Statistics.StandardError
            Allocated = $allocated
        }
    }
}

# Parse baseline results if available
$baselineResults = @{}
if ($BaselineDir -and (Test-Path $BaselineDir)) {
    $baselineFiles = Get-ChildItem $BaselineDir -Filter "*-report-full.json" -ErrorAction SilentlyContinue
    foreach ($file in $baselineFiles) {
        $report = Get-Content $file.FullName -Raw | ConvertFrom-Json
        foreach ($benchmark in $report.Benchmarks) {
            $key = $benchmark.FullName
            $mean = $benchmark.Statistics.Mean
            $allocated = 0
            $allocMetric = $benchmark.Metrics | Where-Object { $_.Descriptor.Id -eq "Allocated Memory" }
            if ($allocMetric) {
                $allocated = $allocMetric.Value
            }
            if ($benchmark.Memory -and $benchmark.Memory.BytesAllocatedPerOperation) {
                $allocated = $benchmark.Memory.BytesAllocatedPerOperation
            }
            $baselineResults[$key] = @{
                Mean      = $mean
                Allocated = $allocated
            }
        }
    }
}

$hasBaseline = $baselineResults.Count -gt 0

# Group by benchmark class
$grouped = $currentResults.GetEnumerator() | Group-Object { $_.Value.Type } | Sort-Object Name

# Build markdown
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("## Benchmark Results")
[void]$sb.AppendLine()

if ($hasBaseline) {
    [void]$sb.AppendLine("> Compared against previous run on this branch. Regressions (>{0}%) marked with :warning:" -f $RegressionThreshold)
} else {
    [void]$sb.AppendLine("> No previous baseline found for comparison.")
}
[void]$sb.AppendLine()

$regressionCount = 0

foreach ($group in $grouped) {
    [void]$sb.AppendLine("### $($group.Name)")
    [void]$sb.AppendLine()

    if ($hasBaseline) {
        [void]$sb.AppendLine("| Method | Mean | Allocated | Δ Mean | Status |")
        [void]$sb.AppendLine("|--------|-----:|----------:|-------:|:------:|")
    } else {
        [void]$sb.AppendLine("| Method | Mean | Allocated |")
        [void]$sb.AppendLine("|--------|-----:|----------:|")
    }

    $sorted = $group.Group | Sort-Object { $_.Value.Method }
    foreach ($entry in $sorted) {
        $r = $entry.Value
        $meanStr = Format-Duration $r.Mean
        $allocStr = Format-Bytes $r.Allocated

        if ($hasBaseline -and $baselineResults.ContainsKey($entry.Key)) {
            $prev = $baselineResults[$entry.Key]
            $deltaPercent = if ($prev.Mean -gt 0) { (($r.Mean - $prev.Mean) / $prev.Mean) * 100 } else { 0 }
            $deltaStr = "{0:+0.0;-0.0;0.0}%" -f $deltaPercent

            $status = ":white_check_mark:"
            if ($deltaPercent -gt $RegressionThreshold) {
                $status = ":warning:"
                $regressionCount++
            } elseif ($deltaPercent -lt -$RegressionThreshold) {
                $status = ":rocket:"
            }

            [void]$sb.AppendLine("| $($r.Method) | $meanStr | $allocStr | $deltaStr | $status |")
        } elseif ($hasBaseline) {
            [void]$sb.AppendLine("| $($r.Method) | $meanStr | $allocStr | _new_ | :sparkles: |")
        } else {
            [void]$sb.AppendLine("| $($r.Method) | $meanStr | $allocStr |")
        }
    }

    [void]$sb.AppendLine()
}

# Summary footer
if ($hasBaseline -and $regressionCount -gt 0) {
    [void]$sb.AppendLine(":warning: **$regressionCount regression(s) detected** (>{0}% slower)" -f $RegressionThreshold)
} elseif ($hasBaseline) {
    [void]$sb.AppendLine(":white_check_mark: **No regressions detected**")
}

$markdown = $sb.ToString()

# Write output
if ($OutputPath) {
    [System.IO.File]::AppendAllText($OutputPath, $markdown)
    Write-Host "Summary written to $OutputPath"
} elseif ($env:GITHUB_STEP_SUMMARY) {
    [System.IO.File]::AppendAllText($env:GITHUB_STEP_SUMMARY, $markdown)
    Write-Host "Summary written to GITHUB_STEP_SUMMARY"
} else {
    Write-Host $markdown
}