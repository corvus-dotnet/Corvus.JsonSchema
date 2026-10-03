<#
.SYNOPSIS
Runs jsonschema-benchmark implementation images over the corpora and compares them with the first one.

.DESCRIPTION
Each image runs the benchmark protocol in a fresh container per corpus and run, printing cold,warm,compile,parse in
nanoseconds (and, through memory-wrapper.sh, the peak memory). The medians of the runs are compared with the first
image's: for each other image, the geometric mean of first / other over the corpora and how many corpora the first is
faster on.

.EXAMPLE
pwsh Compare-Images.ps1 -Schemas ~/src/jsonschema-benchmark/schemas -Images corvus-java,corvus-rs,corvus,blaze
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Schemas,
    [string[]] $Images = @("corvus-java", "corvus-rs", "corvus", "blaze"),
    [int] $Runs = 3,
    [string[]] $Only = @(),
    [string] $Engine = "podman",
    [string] $Csv = "",
    # Images that take the corpus directory instead of the schema and instances files.
    [string[]] $DirectoryImages = @("blaze"),
    # The CPUs the containers run on (for example 2-9, through taskset), so that runs are not moved between cores.
    [string] $CpuSet = ""
)

$ErrorActionPreference = "Stop"
# Lists arrive as one comma-separated string from `pwsh -File`.
$Images = @($Images | ForEach-Object { $_ -split "," } | Where-Object { $_ })
$Only = @($Only | ForEach-Object { $_ -split "," } | Where-Object { $_ })
$DirectoryImages = @($DirectoryImages | ForEach-Object { $_ -split "," } | Where-Object { $_ })
$Schemas = (Resolve-Path $Schemas).Path
$corpora = @(Get-ChildItem -Directory $Schemas | Sort-Object Name | ForEach-Object Name)
if ($Only.Count -gt 0) { $corpora = @($corpora | Where-Object { $Only -contains $_ }) }
$metrics = @("cold", "warm", "compile", "parse")

function Median([double[]] $values) {
    $sorted = @($values | Sort-Object)
    return $sorted[[int][math]::Floor($sorted.Count / 2)]
}

$rows = [System.Collections.Generic.List[object]]::new()
foreach ($corpus in $corpora) {
    foreach ($image in $Images) {
        $samples = @{}
        foreach ($m in $metrics) { $samples[$m] = [System.Collections.Generic.List[double]]::new() }
        $failed = $false
        for ($r = 0; $r -lt $Runs; $r++) {
            $arguments = @(if ($DirectoryImages -contains $image) { "/workspace/$corpus" } else {
                "/workspace/$corpus/schema-noformat.json"; "/workspace/$corpus/instances.jsonl" })
            # Rootless podman cannot set a container's CPUs: the engine runs under taskset, whose affinity the
            # container's processes inherit.
            $command = @(if ($CpuSet) { "taskset"; "-c"; $CpuSet; $Engine } else { $Engine })
            $out = & $command[0] @($command | Select-Object -Skip 1) run --rm -v "${Schemas}:/workspace" `
                "jsonschema-benchmark/$image" @arguments 2>$null
            if ($LASTEXITCODE -ne 0) { $failed = $true; break }
            $fields = @(($out | Select-Object -Last 1).Split(","))
            for ($i = 0; $i -lt 4; $i++) { $samples[$metrics[$i]].Add([double]$fields[$i]) }
        }
        $row = [ordered]@{ corpus = $corpus; image = $image; failed = $failed }
        foreach ($m in $metrics) { $row[$m] = if ($failed) { [double]::NaN } else { Median $samples[$m].ToArray() } }
        $rows.Add([pscustomobject]$row)
        Write-Host ("{0,-24} {1,-12} {2}" -f $corpus, $image, $(if ($failed) { "FAILED" } else {
            "warm {0,12:N1} us  cold {1,12:N1} us  compile {2,12:N1} us  parse {3,12:N1} us" -f `
                ($row.warm / 1000), ($row.cold / 1000), ($row.compile / 1000), ($row.parse / 1000) }))
    }
}
if ($Csv) { $rows | Export-Csv -NoTypeInformation $Csv }

$first = $Images[0]
foreach ($other in $Images | Select-Object -Skip 1) {
    foreach ($m in $metrics + @("parse+warm")) {
        $logs = 0.0; $n = 0; $faster = 0
        foreach ($corpus in $corpora) {
            $a = $rows | Where-Object { $_.corpus -eq $corpus -and $_.image -eq $first }
            $b = $rows | Where-Object { $_.corpus -eq $corpus -and $_.image -eq $other }
            if ($a.failed -or $b.failed) { continue }
            $x = if ($m -eq "parse+warm") { $a.parse + $a.warm } else { $a.$m }
            $y = if ($m -eq "parse+warm") { $b.parse + $b.warm } else { $b.$m }
            if ($y -le 0 -or $x -le 0) { continue }
            $logs += [math]::Log($x / $y); $n++
            if ($x -lt $y) { $faster++ }
        }
        if ($n -gt 0) {
            Write-Host ("{0} / {1} {2,-10}: geomean {3:N3}, faster on {4} of {5}" -f `
                $first, $other, $m, [math]::Exp($logs / $n), $faster, $n)
        }
    }
}
