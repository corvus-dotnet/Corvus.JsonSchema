<#
.SYNOPSIS
    Documentation Code Sample Catalog Maintenance
.DESCRIPTION
    Maintains docs/code-sample-catalog.yaml — the authoritative inventory of every
    fenced code block across documentation, skills, and instruction files.
.PARAMETER Check
    Verify catalog matches current files. Exits with code 1 if stale.
.PARAMETER Generate
    Generate a fresh catalog, discarding all existing annotations.
.PARAMETER UpdateFile
    Re-scan a single file and update its catalog entries, preserving annotations
    for blocks whose line ranges have not changed.
.PARAMETER Stats
    Print summary statistics from the current catalog.
.EXAMPLE
    .\docs\update-code-sample-catalog.ps1
    Full update — re-scan all files, preserve existing annotations.
.EXAMPLE
    .\docs\update-code-sample-catalog.ps1 -UpdateFile docs\JsonPath.md
    Re-scan a single file after editing it.
.EXAMPLE
    .\docs\update-code-sample-catalog.ps1 -Check
    Verify the catalog is in sync with the files on disk.
#>
[CmdletBinding()]
param(
    [switch]$Check,
    [switch]$Generate,
    [string]$UpdateFile,
    [switch]$Stats
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$CatalogPath = Join-Path $RepoRoot 'docs' 'code-sample-catalog.yaml'

# Section definitions — order determines catalog layout
$SectionDefinitions = @(
    @{ Name = 'example-recipes';       Pattern = 'docs\ExampleRecipes\*\README.md' }
    @{ Name = 'main-docs';             Pattern = 'docs\*.md' }
    @{ Name = 'v4-docs';               Pattern = 'docs\V4\*.md' }
    @{ Name = 'copilot-docs';          Pattern = 'docs\copilot\*.md' }
    @{ Name = 'copilot-instructions';  Pattern = '.github\copilot-instructions.md' }
    @{ Name = 'skills';                Pattern = '.github\skills\*\SKILL.md' }
)

$YamlHeader = @"
# Documentation Code Sample Catalog
#
# Authoritative inventory of every fenced code block in documentation,
# skills, and instruction files. This catalog is the source of truth
# for documentation code sample verification.
#
# Maintenance:
#   Full update:     .\docs\update-code-sample-catalog.ps1
#   Single file:     .\docs\update-code-sample-catalog.ps1 -UpdateFile <path>
#   Verify current:  .\docs\update-code-sample-catalog.ps1 -Check
#   Fresh generate:  .\docs\update-code-sample-catalog.ps1 -Generate
#   Statistics:      .\docs\update-code-sample-catalog.ps1 -Stats
#
# When editing documentation files, also update this catalog:
#   - If you insert/remove lines, adjust the 'lines' ranges of affected blocks
#   - If you add/remove fenced code blocks, run -UpdateFile <path> to refresh
#   - After verifying a C# sample compiles, set its verified field to true
#
# C# block categories:
#   compilable  - Should compile against the current codebase
#   fragment    - Partial code, not intended to compile standalone
#   v4-before   - V4 migration 'before' example (intentionally old API)
#   bad-pattern - Analyzer/migration 'bad' example (intentionally wrong)
#
# Non-C# blocks (json, yaml, bash, xml, etc.) have no category or verified fields.

"@

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Test-IsCSharp([string]$Language) {
    $Language.ToLower() -in @('csharp', 'cs', 'c#')
}

function Get-CodeBlocks([string]$FilePath) {
    $lines = [System.IO.File]::ReadAllLines($FilePath, [System.Text.Encoding]::UTF8)
    $blocks = [System.Collections.ArrayList]::new()
    $inBlock = $false
    $blockStart = 0
    $blockLang = ''
    $blockIndex = 0
    $fenceLen = 0

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $lineNum = $i + 1
        $stripped = $lines[$i].Trim()

        if (-not $inBlock) {
            if ($stripped -match '^(`{3,})\s*(\S*)') {
                $inBlock = $true
                $fenceLen = $Matches[1].Length
                $blockStart = $lineNum
                $blockLang = $Matches[2]
            }
        }
        else {
            if ($stripped -match '^(`{3,})\s*$' -and $Matches[1].Length -ge $fenceLen) {
                [void]$blocks.Add(@{
                    Index    = $blockIndex
                    Language = $blockLang
                    Lines    = @($blockStart, $lineNum)
                })
                $blockIndex++
                $inBlock = $false
            }
        }
    }

    return ,$blocks
}

function Get-CompanionProject([string]$ReadmePath) {
    $parent = Split-Path -Parent $ReadmePath
    $csprojs = Get-ChildItem -Path $parent -Filter '*.csproj' -ErrorAction SilentlyContinue
    if ($csprojs) {
        return [System.IO.Path]::GetRelativePath($RepoRoot, $parent).Replace('\', '/') + '/'
    }
    return $null
}

function Get-SectionForPath([string]$RelPath) {
    if ($RelPath -match '^docs/ExampleRecipes/')       { return 'example-recipes' }
    if ($RelPath -match '^docs/V4/')                   { return 'v4-docs' }
    if ($RelPath -match '^docs/copilot/')               { return 'copilot-docs' }
    if ($RelPath -eq '.github/copilot-instructions.md') { return 'copilot-instructions' }
    if ($RelPath -match '^\.github/skills/')            { return 'skills' }
    if ($RelPath -match '^docs/[^/]+\.md$')             { return 'main-docs' }
    return $null
}

# ---------------------------------------------------------------------------
# Scan all documentation directories
# ---------------------------------------------------------------------------

function Get-AllSections {
    $sections = [ordered]@{}

    foreach ($def in $SectionDefinitions) {
        $pattern = Join-Path $RepoRoot $def.Pattern
        $files = @(Get-Item -Path $pattern -ErrorAction SilentlyContinue | Sort-Object FullName)
        $entries = [System.Collections.ArrayList]::new()

        foreach ($file in $files) {
            $blocks = Get-CodeBlocks $file.FullName
            if ($blocks.Count -eq 0) { continue }

            $relPath = [System.IO.Path]::GetRelativePath($RepoRoot, $file.FullName).Replace('\', '/')
            $entry = @{
                Path   = $relPath
                Blocks = $blocks
            }

            if ($def.Name -eq 'example-recipes') {
                $companion = Get-CompanionProject $file.FullName
                if ($companion) { $entry.CompanionProject = $companion }
            }

            [void]$entries.Add($entry)
        }

        if ($entries.Count -gt 0) {
            $sections[$def.Name] = $entries
        }
    }

    return $sections
}

# ---------------------------------------------------------------------------
# Read existing catalog
# ---------------------------------------------------------------------------

function Read-FullCatalog([string]$Path) {
    $sections = [ordered]@{}
    if (-not (Test-Path $Path)) { return $sections }

    $currentSection = $null
    $currentEntry = $null

    foreach ($line in [System.IO.File]::ReadAllLines($Path, [System.Text.Encoding]::UTF8)) {
        # Skip comments and blank lines
        if ($line -match '^\s*#' -or $line -match '^\s*$') { continue }

        # Section header — no leading whitespace, ends with colon
        if ($line -match '^([\w-]+):\s*$') {
            $currentSection = $Matches[1]
            $sections[$currentSection] = [System.Collections.ArrayList]::new()
            $currentEntry = $null
            continue
        }

        $trimmed = $line.Trim()

        # File entry
        if ($trimmed -match '^- path:\s*"([^"]+)"') {
            $currentEntry = @{
                Path             = $Matches[1]
                CompanionProject = $null
                Blocks           = [System.Collections.ArrayList]::new()
            }
            if ($currentSection -and $sections.Contains($currentSection)) {
                [void]$sections[$currentSection].Add($currentEntry)
            }
            continue
        }

        # Companion project
        if ($trimmed -match '^companion_project:\s*"([^"]+)"' -and $currentEntry) {
            $currentEntry.CompanionProject = $Matches[1]
            continue
        }

        # Skip 'blocks:' header line
        if ($trimmed -eq 'blocks:') { continue }

        # Block entry in flow mapping style
        if ($trimmed -match '^- \{(.+)\}$' -and $currentEntry) {
            $c = $Matches[1]
            $block = @{
                Index    = if ($c -match 'index:\s*(\d+)')               { [int]$Matches[1] } else { 0 }
                Language = if ($c -match 'language:\s*(\S+?)(?:,|$)')    { $Matches[1] }      else { '' }
                Lines    = @(
                    $(if ($c -match 'lines:\s*\[(\d+),')                { [int]$Matches[1] } else { 0 }),
                    $(if ($c -match 'lines:\s*\[\d+,\s*(\d+)\]')       { [int]$Matches[1] } else { 0 })
                )
            }
            if ($c -match 'category:\s*([\w-]+)')    { $block.Category = $Matches[1] }
            if ($c -match 'verified:\s*(true|false)') { $block.Verified = ($Matches[1] -eq 'true') }
            [void]$currentEntry.Blocks.Add($block)
        }
    }

    return $sections
}

# ---------------------------------------------------------------------------
# Merge annotations from an existing catalog into freshly scanned sections
# ---------------------------------------------------------------------------

function Merge-Annotations($Scanned, $Existing) {
    # Build lookup: "path|index" -> block
    $lookup = @{}
    foreach ($sectionName in $Existing.Keys) {
        foreach ($entry in $Existing[$sectionName]) {
            foreach ($block in $entry.Blocks) {
                $lookup["$($entry.Path)|$($block.Index)"] = $block
            }
        }
    }

    foreach ($sectionName in $Scanned.Keys) {
        foreach ($entry in $Scanned[$sectionName]) {
            foreach ($block in $entry.Blocks) {
                if (-not (Test-IsCSharp $block.Language)) { continue }

                $key = "$($entry.Path)|$($block.Index)"
                if ($lookup.ContainsKey($key)) {
                    $old = $lookup[$key]
                    if ($old.Language -eq $block.Language) {
                        $block.Category = if ($old.Category) { $old.Category } else { 'compilable' }
                        if ($old.Verified -and
                            $old.Lines[0] -eq $block.Lines[0] -and
                            $old.Lines[1] -eq $block.Lines[1]) {
                            $block.Verified = $true
                        }
                        else {
                            $block.Verified = $false
                        }
                        continue
                    }
                }
                # New block or language mismatch — defaults
                $block.Category = 'compilable'
                $block.Verified = $false
            }
        }
    }
}

# ---------------------------------------------------------------------------
# Set defaults for any C# blocks that lack category/verified
# ---------------------------------------------------------------------------

function Set-Defaults($Sections) {
    foreach ($sectionName in $Sections.Keys) {
        foreach ($entry in $Sections[$sectionName]) {
            foreach ($block in $entry.Blocks) {
                if (Test-IsCSharp $block.Language) {
                    if (-not $block.ContainsKey('Category') -or -not $block.Category) {
                        $block.Category = 'compilable'
                    }
                    if (-not $block.ContainsKey('Verified')) {
                        $block.Verified = $false
                    }
                }
            }
        }
    }
}

# ---------------------------------------------------------------------------
# Write catalog YAML
# ---------------------------------------------------------------------------

function Write-Catalog($Sections, [string]$Path) {
    $sb = [System.Text.StringBuilder]::new(64000)
    [void]$sb.Append($YamlHeader)

    $first = $true
    foreach ($sectionName in $Sections.Keys) {
        if (-not $first) { [void]$sb.Append("`n") }
        $first = $false

        [void]$sb.Append("${sectionName}:`n")

        foreach ($entry in $Sections[$sectionName]) {
            [void]$sb.Append("  - path: `"$($entry.Path)`"`n")
            if ($entry.CompanionProject) {
                [void]$sb.Append("    companion_project: `"$($entry.CompanionProject)`"`n")
            }
            [void]$sb.Append("    blocks:`n")

            foreach ($block in $entry.Blocks) {
                $parts = [System.Collections.ArrayList]::new()
                [void]$parts.Add("index: $($block.Index)")
                [void]$parts.Add("language: $($block.Language)")
                [void]$parts.Add("lines: [$($block.Lines[0]), $($block.Lines[1])]")

                if (Test-IsCSharp $block.Language) {
                    $cat = if ($block.Category) { $block.Category } else { 'compilable' }
                    $ver = if ($block.Verified) { 'true' } else { 'false' }
                    [void]$parts.Add("category: $cat")
                    [void]$parts.Add("verified: $ver")
                }

                [void]$sb.Append("      - {$($parts -join ', ')}`n")
            }
        }
    }

    [System.IO.File]::WriteAllText($Path, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
}

# ---------------------------------------------------------------------------
# Check mode — compare scanned blocks against existing catalog
# ---------------------------------------------------------------------------

function Test-CatalogCurrent($Scanned, [string]$CatPath) {
    if (-not (Test-Path $CatPath)) {
        Write-Host 'Catalog file does not exist.'
        return $false
    }

    $existing = Read-FullCatalog $CatPath
    $issues = [System.Collections.ArrayList]::new()

    # Build lookup of existing blocks
    $existingBlocks = @{}
    foreach ($sectionName in $existing.Keys) {
        foreach ($entry in $existing[$sectionName]) {
            foreach ($block in $entry.Blocks) {
                $existingBlocks["$($entry.Path)|$($block.Index)"] = $block
            }
        }
    }

    # Existing file paths
    $existingPaths = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($sectionName in $existing.Keys) {
        foreach ($entry in $existing[$sectionName]) {
            [void]$existingPaths.Add($entry.Path)
        }
    }

    $scannedKeys = [System.Collections.Generic.HashSet[string]]::new()
    $scannedPaths = [System.Collections.Generic.HashSet[string]]::new()

    foreach ($sectionName in $Scanned.Keys) {
        foreach ($entry in $Scanned[$sectionName]) {
            [void]$scannedPaths.Add($entry.Path)
            if (-not $existingPaths.Contains($entry.Path)) {
                [void]$issues.Add("NEW FILE: $($entry.Path)")
            }

            foreach ($block in $entry.Blocks) {
                $key = "$($entry.Path)|$($block.Index)"
                [void]$scannedKeys.Add($key)

                if (-not $existingBlocks.ContainsKey($key)) {
                    [void]$issues.Add(
                        "NEW BLOCK: $($entry.Path) #$($block.Index) ($($block.Language) lines $($block.Lines[0])-$($block.Lines[1]))")
                }
                else {
                    $old = $existingBlocks[$key]
                    if ($old.Lines[0] -ne $block.Lines[0] -or $old.Lines[1] -ne $block.Lines[1]) {
                        [void]$issues.Add(
                            "MOVED: $($entry.Path) #$($block.Index) [$($old.Lines[0]),$($old.Lines[1])] -> [$($block.Lines[0]),$($block.Lines[1])]")
                    }
                    if ($old.Language -ne $block.Language) {
                        [void]$issues.Add(
                            "LANGUAGE: $($entry.Path) #$($block.Index) $($old.Language) -> $($block.Language)")
                    }
                }
            }
        }
    }

    # Removed files/blocks
    foreach ($path in $existingPaths) {
        if (-not $scannedPaths.Contains($path)) {
            [void]$issues.Add("REMOVED FILE: $path")
        }
    }
    foreach ($key in $existingBlocks.Keys) {
        if (-not $scannedKeys.Contains($key)) {
            [void]$issues.Add("REMOVED BLOCK: $key")
        }
    }

    if ($issues.Count -gt 0) {
        Write-Host "Catalog is STALE - $($issues.Count) issue(s):"
        foreach ($issue in $issues) { Write-Host "  $issue" }
        return $false
    }

    Write-Host 'Catalog is up to date.'
    return $true
}

# ---------------------------------------------------------------------------
# Single-file update
# ---------------------------------------------------------------------------

function Update-SingleFile([string]$FilePath, [string]$CatPath) {
    $fullPath = (Resolve-Path $FilePath -ErrorAction Stop).Path
    $relPath = [System.IO.Path]::GetRelativePath($RepoRoot, $fullPath).Replace('\', '/')

    if (-not (Test-Path $CatPath)) {
        Write-Warning 'Catalog does not exist. Run a full update first.'
        return
    }

    $sections = Read-FullCatalog $CatPath
    $newBlocks = Get-CodeBlocks $fullPath

    # Find and update existing entry
    $found = $false
    foreach ($sectionName in @($sections.Keys)) {
        for ($i = 0; $i -lt $sections[$sectionName].Count; $i++) {
            $entry = $sections[$sectionName][$i]
            if ($entry.Path -ne $relPath) { continue }

            # If the file now has zero blocks, remove the entry
            if ($newBlocks.Count -eq 0) {
                $sections[$sectionName].RemoveAt($i)
                if ($sections[$sectionName].Count -eq 0) {
                    $sections.Remove($sectionName)
                }
                Write-Catalog $sections $CatPath
                Write-Host "Removed catalog entry (no blocks): $relPath"
                return
            }

            # Build old-block lookup by index
            $oldLookup = @{}
            foreach ($old in $entry.Blocks) { $oldLookup["$($old.Index)"] = $old }

            # Apply annotations to new blocks
            foreach ($block in $newBlocks) {
                if (-not (Test-IsCSharp $block.Language)) { continue }
                $key = "$($block.Index)"
                if ($oldLookup.ContainsKey($key) -and $oldLookup[$key].Language -eq $block.Language) {
                    $old = $oldLookup[$key]
                    $block.Category = if ($old.Category) { $old.Category } else { 'compilable' }
                    if ($old.Verified -and
                        $old.Lines[0] -eq $block.Lines[0] -and
                        $old.Lines[1] -eq $block.Lines[1]) {
                        $block.Verified = $true
                    }
                    else { $block.Verified = $false }
                }
                else {
                    $block.Category = 'compilable'
                    $block.Verified = $false
                }
            }

            $entry.Blocks = $newBlocks
            $found = $true
            break
        }
        if ($found) { break }
    }

    if ($newBlocks.Count -eq 0) {
        Write-Host "No code blocks found in: $relPath (not in catalog)"
        return
    }

    # New file — add to the appropriate section
    if (-not $found) {
        $sectionName = Get-SectionForPath $relPath
        if (-not $sectionName) {
            Write-Warning "Cannot determine catalog section for: $relPath"
            return
        }

        foreach ($block in $newBlocks) {
            if (Test-IsCSharp $block.Language) {
                $block.Category = 'compilable'
                $block.Verified = $false
            }
        }

        $newEntry = @{
            Path   = $relPath
            Blocks = $newBlocks
        }

        if ($sectionName -eq 'example-recipes') {
            $companion = Get-CompanionProject $fullPath
            if ($companion) { $newEntry.CompanionProject = $companion }
        }

        if (-not $sections.Contains($sectionName)) {
            $sections[$sectionName] = [System.Collections.ArrayList]::new()
        }
        [void]$sections[$sectionName].Add($newEntry)

        # Re-sort by path
        $sorted = @($sections[$sectionName] | Sort-Object { $_.Path })
        $sections[$sectionName] = [System.Collections.ArrayList]::new($sorted)
    }

    Write-Catalog $sections $CatPath
    Write-Host "Updated catalog for: $relPath"
}

# ---------------------------------------------------------------------------
# Statistics
# ---------------------------------------------------------------------------

function Write-Stats($Sections) {
    $totalF = 0; $totalB = 0; $totalCs = 0; $totalVer = 0

    Write-Host 'Code Sample Catalog Statistics:'
    foreach ($name in $Sections.Keys) {
        $sf = 0; $sb = 0; $sc = 0; $sv = 0
        foreach ($entry in $Sections[$name]) {
            $sf++
            foreach ($block in $entry.Blocks) {
                $sb++
                if (Test-IsCSharp $block.Language) {
                    $sc++
                    if ($block.Verified -eq $true) { $sv++ }
                }
            }
        }
        $totalF += $sf; $totalB += $sb; $totalCs += $sc; $totalVer += $sv
        Write-Host ("  {0,-25}  {1,3} files  {2,4} blocks  {3,4} C#  {4,4} verified" -f
            $name, $sf, $sb, $sc, $sv)
    }
    Write-Host ("  {0,-25}  {1,3} files  {2,4} blocks  {3,4} C#  {4,4} verified" -f
        'TOTAL', $totalF, $totalB, $totalCs, $totalVer)
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

if ($Check) {
    $scanned = Get-AllSections
    $ok = Test-CatalogCurrent $scanned $CatalogPath
    exit $(if ($ok) { 0 } else { 1 })
}

if ($Stats) {
    if (Test-Path $CatalogPath) {
        $sections = Read-FullCatalog $CatalogPath
    }
    else {
        $sections = Get-AllSections
        Set-Defaults $sections
    }
    Write-Stats $sections
    exit 0
}

if ($UpdateFile) {
    Update-SingleFile $UpdateFile $CatalogPath
    exit 0
}

# Default: full update
$scanned = Get-AllSections

if (-not $Generate -and (Test-Path $CatalogPath)) {
    $existing = Read-FullCatalog $CatalogPath
    Merge-Annotations $scanned $existing
}

Set-Defaults $scanned
Write-Catalog $scanned $CatalogPath
Write-Stats $scanned
Write-Host "`nCatalog written to: $([System.IO.Path]::GetRelativePath($RepoRoot, $CatalogPath))"
