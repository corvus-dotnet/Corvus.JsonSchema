<#
.SYNOPSIS
    Generated-code analyzer configuration maintenance
.DESCRIPTION
    Writes (or, with -Check, verifies) the two configuration pieces that stop analyzers running on generated code:

      analyzers/generated-code.globalconfig   sets every diagnostic of each analyzer that analyses generated code
                                              (GeneratedCodeAnalysisFlags.Analyze, the compiler's default for an
                                              analyzer that never calls ConfigureGeneratedCodeAnalysis) to none. The
                                              analyzer driver skips an analyzer on every syntax tree whose per-tree
                                              severities suppress all of its diagnostics, and a global severity stands
                                              in for trees that have no per-tree entry, which in csc is every source
                                              generator tree. Analyzers that opt out of generated code need nothing:
                                              the driver already skips them on trees it judges generated.
      .editorconfig (generated region)        restores those diagnostics for files on disk, so hand-written code keeps
                                              exactly today's analysis, and marks the checked-in generator output
                                              folders with every analyzer diagnostic hidden.

    The diagnostic IDs come from the analyzers actually in use: the SDK's NetAnalyzers (the SDK that global.json
    selects) and every analyzer package in the restore graph of a project that hosts generator output (a project that
    references the Corvus.Text.Json source generator as an analyzer, or that owns a checked-in generated folder).
    analyzers/AnalyzerProbe.cs runs each analyzer's Initialize and reports its generated-code flags and descriptors.
    The restored severity is "default" (the rule's own effective severity) unless the SDK's analysis-level
    configuration sets the rule, in which case that value is restored.

    Run this after changing the SDK in global.json or an analyzer package. CI runs -Check and fails when the files are
    stale, so a new analyzer, or a new diagnostic in one, cannot silently start running on generated code.
.PARAMETER Check
    Verify the files match what the analyzers in use require. Exits with code 1 if stale.
.EXAMPLE
    .\update-generated-code-analyzer-config.ps1
    Rewrite analyzers/generated-code.globalconfig and the generated region of .editorconfig.
.EXAMPLE
    .\update-generated-code-analyzer-config.ps1 -Check
    Verify both are current (the pre-commit gate).
#>
[CmdletBinding()]
param(
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$RepoRoot = $PSScriptRoot
$ProbePath = Join-Path $RepoRoot 'analyzers' 'AnalyzerProbe.cs'
$GlobalConfigPath = Join-Path $RepoRoot 'analyzers' 'generated-code.globalconfig'
$EditorConfigPath = Join-Path $RepoRoot '.editorconfig'
$SolutionPath = Join-Path $RepoRoot 'Corvus.Text.Json.slnx'
$BeginMarker = '# BEGIN generated-code analyzers (written by update-generated-code-analyzer-config.ps1; do not edit by hand)'
$EndMarker = '# END generated-code analyzers'

# Checked-in generator output inside otherwise hand-written projects, as .editorconfig globs relative to the repository
# root. Every directory named Generated, B, C or GeneratedCoreTypes that holds C# files must match one of these; the
# script fails otherwise so that a new folder is added here deliberately.
$GeneratedFolderGlobs = @(
    'docs/ExampleRecipes/*/Generated',
    'benchmarks/*/B',
    'benchmarks/*/C',
    'benchmarks/*/Generated',
    'src/*/Generated',
    'tests/*/Generated',
    'src-v4/Corvus.Json.ExtendedTypes/Corvus.Json/GeneratedCoreTypes',
    'src-v4/Corvus.JsonPatch.Benchmarking/Benchmarks/Generated'
)

$GeneratedFolderNames = @('Generated', 'B', 'C', 'GeneratedCoreTypes')
$ExcludedTopLevelDirectories = @('.git', 'node_modules', 'bin', 'obj')

function Get-RelativePath([string]$Path) {
    return [System.IO.Path]::GetRelativePath($RepoRoot, $Path).Replace('\', '/')
}

function Test-GlobMatch([string]$RelativePath, [string]$Glob) {
    $pattern = '^' + [regex]::Escape($Glob).Replace('\*', '[^/]*') + '$'
    return $RelativePath -match $pattern
}

function Test-ExcludedPath([string]$Path) {
    foreach ($segment in ((Get-RelativePath $Path) -split '/')) {
        if ($segment -in $ExcludedTopLevelDirectories -or $segment -like '.*-scratch') { return $true }
    }

    return $false
}

function Get-ProjectFiles {
    return Get-ChildItem -Path $RepoRoot -Filter *.csproj -Recurse -File | Where-Object { -not (Test-ExcludedPath $_.FullName) }
}

function Find-OwningProject([string]$Directory) {
    $current = $Directory
    while ($current -and $current.Length -ge $RepoRoot.Length) {
        $projects = @(Get-ChildItem -Path $current -Filter *.csproj -File -ErrorAction SilentlyContinue)
        if ($projects.Count -gt 0) { return $projects[0].FullName }
        $current = Split-Path $current -Parent
    }

    return $null
}

# 1. Projects that host generated code.
$projectFiles = @(Get-ProjectFiles)
$hosting = [System.Collections.Generic.SortedDictionary[string, string]]::new([System.StringComparer]::Ordinal)
foreach ($project in $projectFiles) {
    $text = [System.IO.File]::ReadAllText($project.FullName)
    if ($text -match 'Corvus\.Text\.Json\.SourceGenerator\.csproj"[^>]*OutputItemType="Analyzer"' -or
        $text -match 'OutputItemType="Analyzer"[^>]*Corvus\.Text\.Json\.SourceGenerator\.csproj"' -or
        $text -match '<PackageReference\s+Include="Corvus\.Text\.Json\.SourceGenerator"') {
        $hosting[(Get-RelativePath $project.FullName)] = 'source generator'
    }
}

$generatedFolders = @(Get-ChildItem -Path $RepoRoot -Directory -Recurse |
    Where-Object { $_.Name -in $GeneratedFolderNames } |
    Where-Object { -not (Test-ExcludedPath $_.FullName) } |
    Where-Object { @(Get-ChildItem -Path $_.FullName -Filter *.cs -File -Recurse | Select-Object -First 1).Count -gt 0 })
$unmatched = [System.Collections.Generic.List[string]]::new()
foreach ($folder in $generatedFolders) {
    $rel = Get-RelativePath $folder.FullName
    $matched = $false
    foreach ($glob in $GeneratedFolderGlobs) { if (Test-GlobMatch $rel $glob) { $matched = $true; break } }
    if (-not $matched) { $unmatched.Add($rel); continue }
    $owner = Find-OwningProject $folder.FullName
    if ($owner) {
        $key = Get-RelativePath $owner
        if (-not $hosting.ContainsKey($key)) { $hosting[$key] = 'checked-in generated folder' }
    }
}

if ($unmatched.Count -gt 0) {
    Write-Host "Generated-code folders that match none of the configured globs (add them to `$GeneratedFolderGlobs in $($MyInvocation.MyCommand.Name)):"
    $unmatched | ForEach-Object { Write-Host "  $_" }
    exit 1
}

# 2. Analyzer assemblies: the SDK's NetAnalyzers, plus the analyzer packages in the hosting projects' restore graphs.
Push-Location $RepoRoot
try {
    $sdkVersion = (& dotnet --version).Trim()
    $sdkLine = & dotnet --list-sdks | Where-Object { $_.StartsWith("$sdkVersion ") } | Select-Object -First 1
}
finally {
    Pop-Location
}

if (-not $sdkLine) { throw "SDK $sdkVersion not found in 'dotnet --list-sdks'." }
$sdkDirectory = Join-Path ($sdkLine.Substring($sdkVersion.Length).Trim().TrimStart('[').TrimEnd(']')) $sdkVersion
$sdkAnalyzers = Join-Path $sdkDirectory 'Sdks' 'Microsoft.NET.Sdk' 'analyzers'
$sdkConfigs = Join-Path $sdkAnalyzers 'build' 'config'
$assemblies = [System.Collections.Generic.List[string]]::new()
$assemblySources = @{}
foreach ($name in @('Microsoft.CodeAnalysis.NetAnalyzers.dll', 'Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll')) {
    $path = Join-Path $sdkAnalyzers $name
    if (-not (Test-Path $path)) { throw "SDK analyzer not found: $path" }
    $assemblies.Add($path)
    $assemblySources[$name] = "SDK $sdkVersion"
}

$solutionProjects = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($match in [regex]::Matches([System.IO.File]::ReadAllText($SolutionPath), '<Project Path="([^"]+)"')) { [void]$solutionProjects.Add($match.Groups[1].Value.Replace('\', '/')) }
$notRestored = [System.Collections.Generic.List[string]]::new()
$analyzerPattern = '^analyzers/dotnet/(?:cs|roslyn(\d+)\.(\d+)/cs)/[^/]+\.dll$'
$packageUse = @{}
foreach ($entry in $hosting.GetEnumerator()) {
    $projectPath = Join-Path $RepoRoot $entry.Key
    $assetsPath = Join-Path (Split-Path $projectPath -Parent) 'obj' 'project.assets.json'
    if (-not (Test-Path $assetsPath)) {
        if ($solutionProjects.Contains($entry.Key)) { throw "No restore output for $($entry.Key) (expected $assetsPath). Run 'dotnet restore Corvus.Text.Json.slnx' first." }
        $notRestored.Add($entry.Key)
        continue
    }

    $projectText = [System.IO.File]::ReadAllText($projectPath)
    $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json -AsHashtable
    $packagesPath = $assets.project.restore.packagesPath
    foreach ($library in $assets.libraries.GetEnumerator()) {
        if ($library.Value.type -ne 'package') { continue }
        $packageId = $library.Key.Split('/')[0]
        if ($projectText -match "<PackageReference\s+Include=`"$([regex]::Escape($packageId))`"[^>]*ExcludeAssets=`"[^`"]*\b(?:all|analyzers)\b") { continue }
        # The root Directory.Build.targets removes the Roslyn meta-analyzers from every project that is not a Roslyn
        # component (target RemoveRoslynMetaAnalyzers); the same rule applies here.
        if ($packageId -eq 'Microsoft.CodeAnalysis.Analyzers' -and $projectText -notmatch '<IsRoslynComponent>\s*true\s*</IsRoslynComponent>' -and $projectText -notmatch '<CorvusKeepRoslynMetaAnalyzers>\s*true\s*</CorvusKeepRoslynMetaAnalyzers>') { continue }
        $candidates = @($library.Value.files | Where-Object { $_ -match $analyzerPattern })
        if ($candidates.Count -eq 0) { continue }
        # Packages that ship one build per Roslyn version get the newest one, as the SDK's compiler would pick.
        $best = $candidates | Sort-Object -Property @{ Expression = { if ($_ -match $analyzerPattern -and $Matches[1]) { [int]$Matches[1] * 1000 + [int]$Matches[2] } else { 0 } } } -Descending | Select-Object -First 1
        $folder = [System.IO.Path]::GetDirectoryName($best).Replace('\', '/')
        foreach ($file in ($candidates | Where-Object { [System.IO.Path]::GetDirectoryName($_).Replace('\', '/') -eq $folder })) {
            $full = Join-Path $packagesPath $library.Value.path $file
            if (-not $assemblies.Contains($full)) {
                $assemblies.Add($full)
                $assemblySources[[System.IO.Path]::GetFileName($full)] = $library.Key
            }
        }

        if (-not $packageUse.ContainsKey($library.Key)) { $packageUse[$library.Key] = [System.Collections.Generic.List[string]]::new() }
        $packageUse[$library.Key].Add($entry.Key)
    }
}

Write-Host "Analyzer packages in the hosting projects' restore graphs:"
foreach ($package in ($packageUse.Keys | Sort-Object)) { Write-Host "  $package ($($packageUse[$package].Count) projects, e.g. $($packageUse[$package][0]))" }

# 3. Probe the analyzers.
$probeOutput = & dotnet run --file $ProbePath -- @assemblies 2>&1
if ($LASTEXITCODE -ne 0) {
    $probeOutput | ForEach-Object { Write-Host $_ }
    throw "The analyzer probe failed (exit code $LASTEXITCODE)."
}

$json = @($probeOutput | Where-Object { $_ -is [string] -and $_.StartsWith('[') } | Select-Object -Last 1)
if ($json.Count -ne 1) { $probeOutput | ForEach-Object { Write-Host $_ }; throw 'The analyzer probe printed no report.' }
$reports = @($json[0] | ConvertFrom-Json)

# 4. The SDK's analysis-level configuration for the default analysis mode (this repository sets no AnalysisMode). A
# project's level is its .NET target framework version (netX.0 targets X, older frameworks the SDK's latest level), and
# each level's file lists the rules whose severity differs from the descriptor at that level (rules newer than the
# level are none). Every level in use must give a rule the same effective severity for one repository-wide restore.
$sdkSeverities = @{}
$availableLevels = @(Get-ChildItem -Path $sdkConfigs -Filter 'analysislevel_*_default.globalconfig' -File | ForEach-Object { [int]$_.BaseName.Split('_')[1] } | Sort-Object)
foreach ($level in $availableLevels) {
    foreach ($line in Get-Content -LiteralPath (Join-Path $sdkConfigs "analysislevel_$($level)_default.globalconfig")) {
        if ($line -match '^dotnet_diagnostic\.([A-Za-z0-9]+)\.severity\s*=\s*(\S+)') {
            if (-not $sdkSeverities.ContainsKey($Matches[1])) { $sdkSeverities[$Matches[1]] = @{} }
            $sdkSeverities[$Matches[1]][$level] = $Matches[2].ToLowerInvariant()
        }
    }
}

$levelsInUse = [System.Collections.Generic.SortedSet[int]]::new()
foreach ($project in $projectFiles) {
    foreach ($match in [regex]::Matches([System.IO.File]::ReadAllText($project.FullName), '<TargetFrameworks?>([^<]*)</TargetFrameworks?>')) {
        foreach ($tfm in ($match.Groups[1].Value -split ';')) {
            if ($tfm -match '^net(\d+)\.\d+') { [void]$levelsInUse.Add([int]$Matches[1]) }
            elseif ($tfm.Trim()) { [void]$levelsInUse.Add($availableLevels[-1]) }
        }
    }
}

function Get-EffectiveSeverity([object]$Descriptor) {
    # The restored value must reproduce csc's decision exactly. A build runs no analyzer whose diagnostics are all
    # hidden or suggestion severity, and a per-tree "default" would count as enabling such a rule, so the rule's
    # effective severity is written out; $null means the rule is off and needs no restore.
    $descriptorValue = if (-not $Descriptor.IsEnabledByDefault) { $null } else { switch ($Descriptor.DefaultSeverity) { 'Error' { 'error' } 'Warning' { 'warning' } 'Info' { 'suggestion' } 'Hidden' { 'silent' } default { throw "Unknown severity $($Descriptor.DefaultSeverity) for $($Descriptor.Id)." } } }
    $values = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($level in $levelsInUse) {
        $value = $descriptorValue
        if ($sdkSeverities.ContainsKey($Descriptor.Id)) {
            $levelValue = $sdkSeverities[$Descriptor.Id][$level]
            if ($null -ne $levelValue) { $value = if ($levelValue -eq 'none') { $null } else { $levelValue } }
        }

        [void]$values.Add([string]$value)
    }

    if ($values.Count -ne 1) { throw "$($Descriptor.Id) has different effective severities across the analysis levels in use ($($levelsInUse -join ', ')): $($values -join ', '). A single repository-wide restore cannot be exact; decide by hand." }
    $single = @($values)[0]
    return $(if ($single -eq '') { $null } else { $single })
}

# 5. Decide the configuration.
# An analyzer that registers no action at all (a placeholder for a rule that is not implemented) does nothing on any
# tree and is left out. An analyzer whose Initialize failed in the probe is taken as analysing generated code.
$optIn = @($reports | Where-Object { $_.GeneratedCodeFlags -match 'Analyze' -and ($_.RegistersActions -or $_.InitializeError) } | Sort-Object -Property Assembly, Type)
$notes = [System.Collections.Generic.List[string]]::new()
$suppress = [System.Collections.Generic.List[object]]::new()
foreach ($analyzer in $optIn) {
    if ($analyzer.InitializeError) { $notes.Add("$($analyzer.Type): Initialize failed ($($analyzer.InitializeError)); its flags are taken as the default, Analyze | ReportDiagnostics.") }
    $active = @($analyzer.Descriptors | Where-Object { $null -ne (Get-EffectiveSeverity $_) }).Count -gt 0
    if ($analyzer.RegistersSymbolStart -and $active) { $notes.Add("$($analyzer.Type) registers symbol-start actions; the per-tree skip does not apply to those, so it still runs on generator trees.") }
    foreach ($descriptor in $analyzer.Descriptors) {
        if ($descriptor.NotConfigurable -and $active) { $notes.Add("$($analyzer.Type): $($descriptor.Id) is not configurable, so the analyzer cannot be skipped per tree and keeps running on generator trees.") }
        $restore = Get-EffectiveSeverity $descriptor

        $suppress.Add([pscustomobject]@{ Analyzer = $analyzer; Id = $descriptor.Id; Restore = $restore })
    }
}

# The same analyzer can arrive more than once (the SDK's NetAnalyzers and the NetAnalyzers package, or two versions of
# one package in different projects), and several analyzers can share a diagnostic ID (the trimming analyzers do).
# The configuration is per ID; every analyzer type that reports it is named in the comment.
$byId = [System.Collections.Generic.SortedDictionary[string, object]]::new([System.StringComparer]::Ordinal)
foreach ($item in $suppress) {
    if (-not $byId.ContainsKey($item.Id)) { $byId[$item.Id] = [pscustomobject]@{ Id = $item.Id; Restore = $item.Restore; Analyzers = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::Ordinal) } }
    $entry = $byId[$item.Id]
    if ($entry.Restore -ne $item.Restore) { throw "$($item.Id) would be restored to '$($entry.Restore)' for one analyzer and '$($item.Restore)' for another ($($item.Analyzer.Type)). Decide by hand." }
    [void]$entry.Analyzers.Add("$($item.Analyzer.Type) ($($item.Analyzer.Assembly) $($item.Analyzer.AssemblyVersion), $($assemblySources[$item.Analyzer.Assembly]))")
}

$ids = @($byId.Keys)

# A rule that the repository configures by hand elsewhere, or suppresses through NoWarn, would interact with the
# restore section in ways this script does not model; stop and let a person decide.
$handConfigured = [System.Collections.Generic.List[string]]::new()
$noWarn = [System.Collections.Generic.List[string]]::new()
$configFiles = @(Get-ChildItem -Path $RepoRoot -Recurse -File -Include '.editorconfig', '*.globalconfig', '*.csproj', '*.props', '*.targets' |
    Where-Object { -not (Test-ExcludedPath $_.FullName) -and $_.FullName -ne $GlobalConfigPath })
foreach ($file in $configFiles) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    if ($file.FullName -eq $EditorConfigPath) {
        $begin = $text.IndexOf($BeginMarker, [System.StringComparison]::Ordinal)
        $end = $text.IndexOf($EndMarker, [System.StringComparison]::Ordinal)
        if ($begin -ge 0 -and $end -gt $begin) { $text = $text.Substring(0, $begin) + $text.Substring($end + $EndMarker.Length) }
    }

    foreach ($match in [regex]::Matches($text, 'dotnet_diagnostic\.([A-Za-z0-9]+)\.severity')) { if ($ids -ccontains $match.Groups[1].Value) { $handConfigured.Add("$(Get-RelativePath $file.FullName): $($match.Groups[1].Value)") } }
    foreach ($match in [regex]::Matches($text, '<NoWarn>([^<]*)</NoWarn>')) { foreach ($id in ($match.Groups[1].Value -split '[;,\s]+')) { if ($ids -ccontains $id) { $noWarn.Add("$(Get-RelativePath $file.FullName): $id") } } }
}

if ($handConfigured.Count -gt 0) {
    Write-Host 'These diagnostics of analyzers that analyse generated code are configured by hand; the generated configuration cannot be exact for them. Resolve by hand:'
    $handConfigured | Sort-Object -Unique | ForEach-Object { Write-Host "  $_" }
    exit 1
}

# NoWarn suppresses a diagnostic compilation-wide, which today also stops its analyzer executing; the restored per-tree
# severity re-enables the execution on files on disk (the diagnostic stays suppressed). Cheap, but not identical.
if ($noWarn.Count -gt 0) { Write-Warning "NoWarn covers a restored diagnostic, so its analyzer runs (reporting nothing) on the files of: $(($noWarn | Sort-Object -Unique) -join '; ')" }
$nl = "`n"
$global = [System.Text.StringBuilder]::new()
[void]$global.Append("# Written by update-generated-code-analyzer-config.ps1; do not edit by hand. Run the script after changing the SDK in$nl")
[void]$global.Append("# global.json or an analyzer package; CI runs it with -Check.$nl#$nl")
[void]$global.Append("# The analyzers listed here analyse generated code, so they execute on every source generator tree, where an$nl")
[void]$global.Append("# .editorconfig section cannot reach them (csc gives generator trees no per-tree options). Setting each of their$nl")
[void]$global.Append("# diagnostics to none here makes the analyzer driver skip the analyzer on every tree that has no per-tree severity of$nl")
[void]$global.Append("# its own, which is exactly the generator trees; the generated region of the root .editorconfig restores the$nl")
[void]$global.Append("# severities for files on disk. Analyzers that opt out of generated code are already skipped there and need nothing.$nl")
[void]$global.Append("is_global = true$nl")
foreach ($entry in $byId.Values) {
    [void]$global.Append($nl)
    foreach ($analyzer in $entry.Analyzers) { [void]$global.Append("# $analyzer$nl") }
    [void]$global.Append("dotnet_diagnostic.$($entry.Id).severity = none$nl")
}

$region = [System.Text.StringBuilder]::new()
[void]$region.Append("$BeginMarker$nl")
[void]$region.Append("# analyzers/generated-code.globalconfig turns the diagnostics of every analyzer that analyses generated code off, so$nl")
[void]$region.Append("# that the analyzer driver skips those analyzers on source generator trees. This section restores them for files on$nl")
[void]$region.Append("# disk at each rule's effective severity (the SDK's analysis-level value where it sets one, else the rule's own default),$nl")
[void]$region.Append("# which keeps a build's decision to run or skip each analyzer on hand-written code exactly as it was.$nl")
[void]$region.Append("[*.cs]$nl")
foreach ($entry in ($byId.Values | Where-Object Restore)) {
    [void]$region.Append("dotnet_diagnostic.$($entry.Id).severity = $($entry.Restore)$nl")
}

[void]$region.Append("$nl# Checked-in generator output inside hand-written projects: no analyzer diagnostic reported, and the analyzers above$nl")
[void]$region.Append("# skipped per tree. Neither key touches compiler diagnostics. The files are classified as generated by their header;$nl")
[void]$region.Append("# generated_code = true is not set here because it also switches off the project's nullable context for a file$nl")
[void]$region.Append("# without the header (CS8669 in the hand-maintained V4 core types).$nl")
[void]$region.Append("[{$($GeneratedFolderGlobs -join ',')}/**.cs]$nl")
[void]$region.Append("dotnet_analyzer_diagnostic.severity = none$nl")
foreach ($id in $ids) { [void]$region.Append("dotnet_diagnostic.$id.severity = none$nl") }
[void]$region.Append($EndMarker)

$globalText = $global.ToString()
$editorText = if (Test-Path $EditorConfigPath) { [System.IO.File]::ReadAllText($EditorConfigPath) } else { '' }
$editorNl = if ($editorText.Contains("`r`n")) { "`r`n" } else { "`n" }
$regionText = $region.ToString().Replace("`n", $editorNl)
$beginIndex = $editorText.IndexOf($BeginMarker, [System.StringComparison]::Ordinal)
$endIndex = $editorText.IndexOf($EndMarker, [System.StringComparison]::Ordinal)
if ($beginIndex -ge 0 -and $endIndex -gt $beginIndex) {
    $newEditorText = $editorText.Substring(0, $beginIndex) + $regionText + $editorText.Substring($endIndex + $EndMarker.Length)
}
else {
    $newEditorText = $editorText.TrimEnd("`r", "`n") + $editorNl + $editorNl + $regionText + $editorNl
}

Write-Host "Hosting projects: $($hosting.Count) ($(@($hosting.Values | Where-Object { $_ -eq 'source generator' }).Count) reference the source generator, $(@($hosting.Values | Where-Object { $_ -ne 'source generator' }).Count) own generated folders); generated folders: $($generatedFolders.Count)"
if ($notRestored.Count -gt 0) { Write-Host "Not restored, so not probed (outside the main solution): $($notRestored.Count) projects ($(($notRestored | Select-Object -First 3) -join ', ')$(if ($notRestored.Count -gt 3) { ', ...' }))" }
Write-Host "Analysis levels in use: $($levelsInUse -join ', ') (SDK files for $($availableLevels -join ', '))"
Write-Host "Analyzer assemblies probed: $($assemblies.Count); analyzers: $($reports.Count); analysing generated code: $($optIn.Count) ($($ids.Count) diagnostics)"
foreach ($analyzer in $optIn) { Write-Host "  $($analyzer.Type): $(($analyzer.Descriptors | ForEach-Object Id) -join ', ') [$($analyzer.GeneratedCodeFlags)$(if ($analyzer.FlagsAreDefault) { ', not configured' })]" }
foreach ($note in $notes) { Write-Warning $note }

if ($Check) {
    $stale = [System.Collections.Generic.List[string]]::new()
    if (-not (Test-Path $GlobalConfigPath) -or [System.IO.File]::ReadAllText($GlobalConfigPath) -cne $globalText) { $stale.Add((Get-RelativePath $GlobalConfigPath)) }
    if ($editorText -cne $newEditorText) { $stale.Add((Get-RelativePath $EditorConfigPath)) }
    if ($stale.Count -gt 0) {
        Write-Host "STALE: $($stale -join ', ') do not match the analyzers in use. Run update-generated-code-analyzer-config.ps1 and commit the result."
        exit 1
    }

    Write-Host 'Generated-code analyzer configuration is current.'
    exit 0
}

New-Item -ItemType Directory -Path (Split-Path $GlobalConfigPath -Parent) -Force | Out-Null
[System.IO.File]::WriteAllText($GlobalConfigPath, $globalText, [System.Text.UTF8Encoding]::new($false))
[System.IO.File]::WriteAllText($EditorConfigPath, $newEditorText, [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $(Get-RelativePath $GlobalConfigPath) and the generated region of .editorconfig."
