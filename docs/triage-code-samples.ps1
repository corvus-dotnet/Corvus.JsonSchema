<#
.SYNOPSIS
    Automatically triage C# code blocks in the documentation catalog.
.DESCRIPTION
    Reads docs/code-sample-catalog.yaml, examines each C# block's content,
    and assigns categories (compilable, fragment, v4-before, bad-pattern)
    based on content analysis. For example-recipes, cross-references with
    companion .cs files and sets verified: true for blocks that compile.
.PARAMETER DryRun
    Show what would change without writing the catalog.
.PARAMETER Section
    Only process blocks in the named section (e.g. 'main-docs', 'example-recipes').
.PARAMETER File
    Only process blocks from a specific file path (relative).
#>
[CmdletBinding()]
param(
    [switch]$DryRun,
    [string]$Section,
    [string]$File
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$CatalogPath = Join-Path $RepoRoot 'docs' 'code-sample-catalog.yaml'

# ---------------------------------------------------------------------------
# Content-based classification rules
# ---------------------------------------------------------------------------

# V4 namespace/type markers — if ANY of these appear, the block is V4 code
$V4Markers = @(
    'Corvus\.Json\.'                # V4 namespace (not Corvus.Text.Json)
    'using Corvus\.Json;'
    'ParsedValue<'
    'JsonDocument\.Parse'           # System.Text.Json direct usage (V4 pattern)
    '\.AsString\b'                  # V4 conversion
    '\.AsNumber\b'
    '\.Serialize\(\)'               # V4 serialization
    'JsonAny\b'                     # V4 type
    'JsonNotAny\b'
    'IJsonValue\b'                  # V4 interface
    '\.WithAge\('                   # V4 mutation pattern (With*)
    '\.WithName\('
    '\.WithItem\('
    'Corvus\.Json\.Patch\b'         # V4 Patch namespace
    '\.As<'                         # V4 .As<Type>() conversion
    'ValidationContext\b'           # V4 validation type
    'IJsonDocument<'                # V4 interface (note: V5 is IJsonDocument without generic)
    'SchemaVocabulary\.'            # V4 vocabulary static class
    '\.IsValid\(\)'                 # V4 validation (no args)
)

# V5 markers — used to distinguish V5 from V4 in mixed content
$V5Markers = @(
    'Corvus\.Text\.Json'
    'ParsedJsonDocument<'
    'JsonWorkspace'
    'CreateBuilder\('
    'EvaluateSchema\('
    'JsonDocumentBuilder<'
    'IJsonElement<'
    '\.Mutable\b'
    'SchemaEvaluator'
    'JsonElement\.Parse'            # V5 parse pattern
)

# Fragment indicators — code that's too short or syntactically incomplete to compile
function Test-IsFragment([string[]]$Lines) {
    $code = ($Lines -join "`n").Trim()

    # Very short (1-2 lines of actual code, excluding blank lines)
    $codeLines = @($Lines | Where-Object { $_.Trim() -ne '' -and $_.Trim() -notmatch '^\s*//' })
    if ($codeLines.Count -le 1 -and $code -notmatch 'Console\.Write') { return $true }

    # Ellipsis (placeholder code)
    if ($code -match '\.\.\.') { return $true }

    # Single property/method signature without body
    if ($codeLines.Count -le 2 -and $code -match '^\s*(public|private|internal|protected)' -and $code -notmatch '\{') { return $true }

    # Attribute-only block
    if ($codeLines.Count -le 2 -and $code -match '^\s*\[' -and $code -notmatch '\{' -and $code -notmatch ';') { return $true }

    # Comment-only block
    $nonComment = @($codeLines | Where-Object { $_.Trim() -notmatch '^\s*//' })
    if ($nonComment.Count -eq 0) { return $true }

    return $false
}

function Test-IsBadPattern([string[]]$Lines, [string]$FilePath) {
    $code = ($Lines -join "`n").Trim()

    # Analyzer docs: blocks preceded by "❌" or "Noncompliant" or "Bad" in surrounding context
    # We detect this by looking at the file context above the block
    # For now, check if the code itself has common "bad" markers
    if ($FilePath -match 'Analyzers\.md|MigrationAnalyzers\.md') {
        # In analyzer docs, blocks that show BOTH bad and good code (before/after) are fragments
        # Blocks with explicit "// Noncompliant" or similar comments are bad-pattern
        if ($code -match '// Noncompliant|// Bad|// Wrong|// Don''t do this') { return $true }
    }

    return $false
}

function Test-IsV4Code([string[]]$Lines) {
    $code = ($Lines -join "`n")

    $hasV4 = $false
    foreach ($marker in $V4Markers) {
        if ($code -match $marker) { $hasV4 = $true; break }
    }
    if (-not $hasV4) { return $false }

    # If it also has V5 markers, it's mixed (migration before/after → fragment)
    foreach ($marker in $V5Markers) {
        if ($code -match $marker) { return $false }  # Mixed → will be classified separately
    }

    return $true
}

function Test-IsMixedV4V5([string[]]$Lines) {
    $code = ($Lines -join "`n")
    $hasV4 = $false; $hasV5 = $false
    foreach ($marker in $V4Markers) {
        if ($code -match $marker) { $hasV4 = $true; break }
    }
    foreach ($marker in $V5Markers) {
        if ($code -match $marker) { $hasV5 = $true; break }
    }
    return ($hasV4 -and $hasV5)
}

# ---------------------------------------------------------------------------
# Read the catalog
# ---------------------------------------------------------------------------

function Read-FullCatalog([string]$Path) {
    $sections = [ordered]@{}
    if (-not (Test-Path $Path)) { Write-Error "Catalog not found: $Path"; return $sections }

    $currentSection = $null
    $currentEntry = $null

    foreach ($line in [System.IO.File]::ReadAllLines($Path, [System.Text.Encoding]::UTF8)) {
        if ($line -match '^\s*#' -or $line -match '^\s*$') { continue }
        if ($line -match '^([\w-]+):\s*$') {
            $currentSection = $Matches[1]
            $sections[$currentSection] = [System.Collections.ArrayList]::new()
            $currentEntry = $null
            continue
        }

        $trimmed = $line.Trim()

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

        if ($trimmed -match '^companion_project:\s*"([^"]+)"' -and $currentEntry) {
            $currentEntry.CompanionProject = $Matches[1]
            continue
        }

        if ($trimmed -eq 'blocks:') { continue }

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
# Write catalog YAML (copied from main script for self-contained use)
# ---------------------------------------------------------------------------

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

function Test-IsCSharp([string]$Language) {
    $Language.ToLower() -in @('csharp', 'cs', 'c#')
}

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
# Read block content from source files
# ---------------------------------------------------------------------------

function Get-BlockContent([string]$FilePath, [int]$StartLine, [int]$EndLine) {
    $fullPath = Join-Path $RepoRoot $FilePath
    if (-not (Test-Path $fullPath)) { return @() }
    $allLines = [System.IO.File]::ReadAllLines($fullPath, [System.Text.Encoding]::UTF8)
    # StartLine/EndLine are 1-based and include the fence lines
    # Return only the content lines (between fences)
    $start = $StartLine  # skip opening fence (0-indexed: StartLine-1 is fence, so StartLine is first content)
    $end = $EndLine - 2  # skip closing fence
    if ($start -gt $end -or $start -ge $allLines.Count) { return @() }
    return $allLines[$start..$end]
}

# ---------------------------------------------------------------------------
# Cross-reference ExampleRecipes with companion .cs files
# ---------------------------------------------------------------------------

function Get-CompanionCsContent([string]$CompanionProject) {
    $dir = Join-Path $RepoRoot $CompanionProject
    if (-not (Test-Path $dir)) { return $null }
    $csFiles = Get-ChildItem -Path $dir -Filter '*.cs' -ErrorAction SilentlyContinue
    if (-not $csFiles) { return $null }
    $allContent = @()
    foreach ($f in $csFiles) {
        $allContent += [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
    }
    return ($allContent -join "`n")
}

function Test-BlockInCompanion([string[]]$BlockLines, [string]$CompanionContent) {
    if (-not $CompanionContent -or $BlockLines.Count -eq 0) { return $false }
    # Check if the significant code lines exist in the companion files
    $significantLines = @($BlockLines | Where-Object {
        $t = $_.Trim()
        $t -ne '' -and $t -notmatch '^\s*//' -and $t -ne '{' -and $t -ne '}'
    })
    if ($significantLines.Count -eq 0) { return $false }

    # Check first and last significant lines are in the companion
    $firstLine = $significantLines[0].Trim()
    $lastLine = $significantLines[-1].Trim()
    return ($CompanionContent.Contains($firstLine) -and $CompanionContent.Contains($lastLine))
}

# ---------------------------------------------------------------------------
# Main triage logic
# ---------------------------------------------------------------------------

$catalog = Read-FullCatalog $CatalogPath
$changes = 0
$stats = @{ compilable = 0; fragment = 0; 'v4-before' = 0; 'bad-pattern' = 0; verified = 0; skipped = 0 }

foreach ($sectionName in @($catalog.Keys)) {
    if ($Section -and $sectionName -ne $Section) { continue }

    foreach ($entry in $catalog[$sectionName]) {
        if ($File -and $entry.Path -ne $File) { continue }

        $companionContent = $null
        if ($entry.CompanionProject) {
            $companionContent = Get-CompanionCsContent $entry.CompanionProject
        }

        foreach ($block in $entry.Blocks) {
            if (-not (Test-IsCSharp $block.Language)) { $stats.skipped++; continue }

            $content = Get-BlockContent $entry.Path $block.Lines[0] $block.Lines[1]
            if ($content.Count -eq 0) { $stats.skipped++; continue }

            $oldCat = if ($block.Category) { $block.Category } else { 'compilable' }
            $oldVer = if ($block.Verified) { $block.Verified } else { $false }
            $newCat = 'compilable'
            $newVer = $false

            # 1. V4-only file detection (docs/V4/*.md)
            if ($entry.Path -match '^docs/V4/') {
                $newCat = 'v4-before'
            }
            # 2. Content-based V4 detection
            elseif (Test-IsV4Code $content) {
                $newCat = 'v4-before'
            }
            # 3. Mixed V4/V5 (migration before/after pairs)
            elseif (Test-IsMixedV4V5 $content) {
                $newCat = 'fragment'
            }
            # 4. Bad-pattern detection
            elseif (Test-IsBadPattern $content $entry.Path) {
                $newCat = 'bad-pattern'
            }
            # 5. Fragment detection
            elseif (Test-IsFragment $content) {
                $newCat = 'fragment'
            }
            # 6. ExampleRecipes: cross-reference with companion project
            elseif ($companionContent) {
                if (Test-BlockInCompanion $content $companionContent) {
                    $newVer = $true
                }
            }

            # MigrationAnalyzers.md: all blocks are before/after pairs → fragment
            if ($entry.Path -eq 'docs/MigrationAnalyzers.md') {
                # Check for mixed content (before + after in same block)
                $code = ($content -join "`n")
                if ($code -match '// Before' -or $code -match '// After' -or
                    (Test-IsMixedV4V5 $content)) {
                    $newCat = 'fragment'
                }
                # Single analyzer examples that are just a few lines of either V4 or V5
                elseif ($content.Count -le 5) {
                    $newCat = 'fragment'
                }
            }

            # CopilotMigrationInstructions.md: many blocks are V4 before/after pairs
            if ($entry.Path -match 'CopilotMigrationInstructions\.md') {
                if (Test-IsV4Code $content) {
                    $newCat = 'v4-before'
                }
                elseif (Test-IsMixedV4V5 $content) {
                    $newCat = 'fragment'
                }
            }

            if ($newCat -ne $oldCat -or $newVer -ne $oldVer) {
                if ($DryRun) {
                    $catChange = if ($newCat -ne $oldCat) { "cat: $oldCat -> $newCat" } else { '' }
                    $verChange = if ($newVer -ne $oldVer) { "ver: $oldVer -> $newVer" } else { '' }
                    Write-Host "  $($entry.Path) #$($block.Index): $catChange $verChange"
                }
                $block.Category = $newCat
                $block.Verified = $newVer
                $changes++
            }

            $stats[$newCat]++
            if ($newVer) { $stats.verified++ }
        }
    }
}

Write-Host ""
Write-Host "Triage results:"
Write-Host "  compilable:  $($stats.compilable)"
Write-Host "  fragment:    $($stats.fragment)"
Write-Host "  v4-before:   $($stats['v4-before'])"
Write-Host "  bad-pattern: $($stats['bad-pattern'])"
Write-Host "  verified:    $($stats.verified)"
Write-Host "  skipped:     $($stats.skipped) (non-C#)"
Write-Host "  changes:     $changes"

if (-not $DryRun -and $changes -gt 0) {
    Write-Catalog $catalog $CatalogPath
    Write-Host "`nCatalog updated with $changes changes."
}
elseif ($DryRun -and $changes -gt 0) {
    Write-Host "`nDry run — no changes written. Run without -DryRun to apply."
}
else {
    Write-Host "`nNo changes needed."
}
