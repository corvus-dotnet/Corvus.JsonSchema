<#
.SYNOPSIS
    Writes tables_gen.go, the Unicode data that Go's unicode package does not carry.

.DESCRIPTION
    The data comes from unicodetables.rs of the regress crate, which the Rust port of the evaluator depends on and
    which is generated from the Unicode Character Database. The script copies the binary properties that the unicode
    package lacks, the short names of the scripts, and the Script_Extensions ranges of the scripts whose extensions
    differ from their Script ranges. Run gofmt on the result.

.PARAMETER RegressTables
    The path of src/unicodetables.rs in a checkout of the regress crate, for example under
    ~/.cargo/registry/src/*/regress-0.12.0.

.PARAMETER Output
    The file to write. The default is tables_gen.go next to this script.

.EXAMPLE
    pwsh gen_tables.ps1 -RegressTables ~/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f/regress-0.12.0/src/unicodetables.rs
#>
param(
    [Parameter(Mandatory = $true)][string]$RegressTables,
    [string]$Output = (Join-Path $PSScriptRoot 'tables_gen.go')
)

$ErrorActionPreference = 'Stop'
$source = [System.IO.File]::ReadAllText((Resolve-Path $RegressTables))

# Every table of the source, by its constant name, as a flat list of inclusive bounds.
$tables = @{}
$tablePattern = [regex]::new('const ([A-Z0-9_]+): \[Interval; (\d+)\] =\s*\[(.*?)\];', 'Singleline')
$intervalPattern = [regex]::new('Interval::new\((\d+), (\d+)\)')
foreach ($m in $tablePattern.Matches($source)) {
    $bounds = [System.Collections.Generic.List[int]]::new()
    foreach ($i in $intervalPattern.Matches($m.Groups[3].Value)) {
        $bounds.Add([int]$i.Groups[1].Value)
        $bounds.Add([int]$i.Groups[2].Value)
    }
    if ($bounds.Count -ne 2 * [int]$m.Groups[2].Value) {
        throw "Table $($m.Groups[1].Value) has $($bounds.Count / 2) intervals, and its declaration says $($m.Groups[2].Value)."
    }
    $tables[$m.Groups[1].Value] = $bounds
}

# The binary properties ECMA-262 lists that the unicode package has no table for.
$binary = @(
    'Alphabetic', 'Case_Ignorable', 'Cased', 'Changes_When_Casefolded', 'Changes_When_Casemapped',
    'Changes_When_Lowercased', 'Changes_When_Titlecased', 'Changes_When_Uppercased', 'Default_Ignorable_Code_Point',
    'Grapheme_Base', 'Grapheme_Extend', 'ID_Continue', 'ID_Start', 'Math', 'XID_Continue', 'XID_Start', 'Lowercase',
    'Uppercase', 'Emoji', 'Emoji_Component', 'Emoji_Modifier', 'Emoji_Modifier_Base', 'Emoji_Presentation',
    'Extended_Pictographic', 'Changes_When_NFKC_Casefolded', 'Bidi_Mirrored'
)

function Get-GoName([string]$constant) {
    $parts = @($constant.ToLowerInvariant().Split('_') | ForEach-Object { $_.Substring(0, 1).ToUpperInvariant() + $_.Substring(1) })
    return 'tab' + ($parts -join '')
}

$out = [System.Text.StringBuilder]::new()
function Add-Line([string]$line = '') { [void]$out.Append($line).Append("`n") }

function Add-Table([string]$constant) {
    $bounds = $tables[$constant]
    if ($null -eq $bounds) { throw "The source has no table $constant." }
    Add-Line "var $(Get-GoName $constant) = []rune{"
    for ($k = 0; $k -lt $bounds.Count; $k += 12) {
        $last = [Math]::Min($k + 12, $bounds.Count) - 1
        $row = @($bounds[$k..$last] | ForEach-Object { '0x{0:X}' -f $_ })
        Add-Line ("`t" + ($row -join ', ') + ',')
    }
    Add-Line '}'
    Add-Line
}

Add-Line '// Code generated from the Unicode 17.0.0 property tables of the regress crate (gen-unicode). DO NOT EDIT.'
Add-Line
Add-Line 'package ecmaregex'
Add-Line
Add-Line "// The tables below hold the Unicode data the standard library's unicode package does not carry. Each table is a"
Add-Line '// sorted list of inclusive ranges, written as pairs of code points.'
Add-Line
Add-Line '// binaryTables maps the binary properties that the unicode package lacks to their ranges.'
Add-Line 'var binaryTables = map[string][]rune{'
foreach ($name in $binary) { Add-Line "`t`"$name`": $(Get-GoName $name.ToUpperInvariant())," }
Add-Line '}'
Add-Line
foreach ($name in $binary) { Add-Table $name.ToUpperInvariant() }

# The names of each script. The first of each arm of the match is the name the unicode package uses.
$body = [regex]::Match($source, 'fn unicode_property_value_script_from_str\(.*?match s \{(.*?)_ => None', 'Singleline').Groups[1].Value
$scriptNames = [ordered]@{}
foreach ($m in [regex]::Matches($body, '((?:"\w+"\s*\|?\s*)+)=>\s*Some\((\w+)\)')) {
    $scriptNames[$m.Groups[2].Value] = @([regex]::Matches($m.Groups[1].Value, '"(\w+)"') | ForEach-Object { $_.Groups[1].Value })
}
Add-Line '// scriptAliases maps the short (ISO 15924) and alternative names of a script to the name the unicode package uses.'
Add-Line 'var scriptAliases = map[string]string{'
foreach ($names in $scriptNames.Values) {
    for ($k = 1; $k -lt $names.Count; $k++) { Add-Line "`t`"$($names[$k])`": `"$($names[0])`"," }
}
Add-Line '}'
Add-Line

$body = [regex]::Match($source, 'fn script_extensions_value_ranges\(.*?match value \{(.*?)\n    \}', 'Singleline').Groups[1].Value
$extensions = @([regex]::Matches($body, '(\w+) => &(\w+),') | Where-Object { $_.Groups[2].Value.EndsWith('_EXTENSIONS') })
Add-Line '// scriptExtensionTables maps a script to the ranges of its Script_Extensions property, when they differ from the'
Add-Line '// ranges of its Script property.'
Add-Line 'var scriptExtensionTables = map[string][]rune{'
foreach ($m in $extensions) { Add-Line "`t`"$($scriptNames[$m.Groups[1].Value][0])`": $(Get-GoName $m.Groups[2].Value)," }
Add-Line '}'
Add-Line
foreach ($m in $extensions) { Add-Table $m.Groups[2].Value }

[System.IO.File]::WriteAllText($Output, $out.ToString().TrimEnd("`n") + "`n", [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $Output. Run gofmt -w on it."
