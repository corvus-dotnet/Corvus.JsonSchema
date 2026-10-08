<#
.SYNOPSIS
    Writes tables_gen.go, the Unicode data of the module.

.DESCRIPTION
    The module takes every Unicode property it reads from the tables this script writes, and nothing from the unicode
    package of the Go toolchain, so that its answers do not depend on the toolchain that built it.

    The data comes from unicodetables.rs of the regress crate, which the Rust port of the evaluator depends on and
    which is generated from the Unicode Character Database. The script copies the General_Category values, the
    binary properties, the Script values, the Script_Extensions ranges of the scripts whose extensions differ from
    their Script ranges, the simple case folding and the simple uppercase mapping.

    A table is written as inclusive ranges, the ones up to U+FFFF as uint16 and the rest as uint32. Tables with the
    same ranges are written once. The values that follow from others are not written at all, and ucd.go builds them
    on first use. They are the General_Category groups (L, LC, M, N, P, S, Z and C), Assigned (the complement of Cn)
    and the Unknown script (Cn, Co and Cs together). The script checks each of those against the source and stops
    if one differs.

    The output is formatted as gofmt formats it.

.PARAMETER RegressTables
    The path of src/unicodetables.rs in a checkout of the regress crate, for example under
    ~/.cargo/registry/src/*/regress-0.12.0.

.PARAMETER UnicodeVersion
    The version of Unicode the source was generated from. regress 0.12.0 carries Unicode 17.0.0.

.PARAMETER Output
    The file to write. The default is tables_gen.go next to this script.

.EXAMPLE
    pwsh gen_tables.ps1 -RegressTables ~/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f/regress-0.12.0/src/unicodetables.rs
#>
param(
    [Parameter(Mandatory = $true)][string]$RegressTables,
    [string]$UnicodeVersion = '17.0.0',
    [string]$Output = (Join-Path $PSScriptRoot 'tables_gen.go')
)

$ErrorActionPreference = 'Stop'
$source = [System.IO.File]::ReadAllText((Resolve-Path $RegressTables))

# Every range table of the source, by its constant name, as a flat list of inclusive bounds.
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

function Get-Table([string]$constant) {
    $bounds = $tables[$constant]
    if ($null -eq $bounds) { throw "The source has no table $constant." }
    return , $bounds
}

# The union of tables, as sorted ranges that neither overlap nor touch.
function Get-Union([string[]]$constants) {
    $pairs = [System.Collections.Generic.List[long]]::new()
    foreach ($constant in $constants) {
        $bounds = Get-Table $constant
        for ($k = 0; $k -lt $bounds.Count; $k += 2) { $pairs.Add(([long]$bounds[$k] -shl 32) -bor [long]$bounds[$k + 1]) }
    }
    $pairs.Sort()
    $merged = [System.Collections.Generic.List[int]]::new()
    foreach ($pair in $pairs) {
        $lo = [int]($pair -shr 32)
        $hi = [int]($pair -band 0xFFFFFFFFL)
        $n = $merged.Count
        if ($n -gt 0 -and $lo -le $merged[$n - 1] + 1) {
            if ($hi -gt $merged[$n - 1]) { $merged[$n - 1] = $hi }
            continue
        }
        $merged.Add($lo)
        $merged.Add($hi)
    }
    return , $merged
}

function Get-Complement($bounds) {
    $inverse = [System.Collections.Generic.List[int]]::new()
    $next = 0
    for ($k = 0; $k -lt $bounds.Count; $k += 2) {
        if ($bounds[$k] -gt $next) { $inverse.Add($next); $inverse.Add($bounds[$k] - 1) }
        $next = $bounds[$k + 1] + 1
    }
    if ($next -le 0x10FFFF) { $inverse.Add($next); $inverse.Add(0x10FFFF) }
    return , $inverse
}

function Assert-Same([string]$what, $expected, $actual) {
    if (($expected -join ',') -ne ($actual -join ',')) {
        throw "$what does not hold for this source. ucd.go builds that value from the others, so it needs a table of its own."
    }
}

function Get-PascalName([string]$name) {
    $parts = @($name.ToLowerInvariant().Split('_') | ForEach-Object { $_.Substring(0, 1).ToUpperInvariant() + $_.Substring(1) })
    return $parts -join ''
}

# The General_Category values that have a table, by short name. The groups are unions of these.
$categories = [ordered]@{
    Lu = 'UPPERCASE_LETTER'; Ll = 'LOWERCASE_LETTER'; Lt = 'TITLECASE_LETTER'; Lm = 'MODIFIER_LETTER'; Lo = 'OTHER_LETTER'
    Mn = 'NONSPACING_MARK'; Mc = 'SPACING_MARK'; Me = 'ENCLOSING_MARK'
    Nd = 'DECIMAL_NUMBER'; Nl = 'LETTER_NUMBER'; No = 'OTHER_NUMBER'
    Pc = 'CONNECTOR_PUNCTUATION'; Pd = 'DASH_PUNCTUATION'; Ps = 'OPEN_PUNCTUATION'; Pe = 'CLOSE_PUNCTUATION'
    Pi = 'INITIAL_PUNCTUATION'; Pf = 'FINAL_PUNCTUATION'; Po = 'OTHER_PUNCTUATION'
    Sm = 'MATH_SYMBOL'; Sc = 'CURRENCY_SYMBOL'; Sk = 'MODIFIER_SYMBOL'; So = 'OTHER_SYMBOL'
    Zs = 'SPACE_SEPARATOR'; Zl = 'LINE_SEPARATOR'; Zp = 'PARAGRAPH_SEPARATOR'
    Cc = 'CONTROL'; Cf = 'FORMAT'; Cs = 'SURROGATE'; Co = 'PRIVATE_USE'; Cn = 'UNASSIGNED'
}
$groups = [ordered]@{
    LETTER = @('Lu', 'Ll', 'Lt', 'Lm', 'Lo'); CASED_LETTER = @('Lu', 'Ll', 'Lt'); MARK = @('Mn', 'Mc', 'Me')
    NUMBER = @('Nd', 'Nl', 'No'); PUNCTUATION = @('Pc', 'Pd', 'Ps', 'Pe', 'Pi', 'Pf', 'Po')
    SYMBOL = @('Sm', 'Sc', 'Sk', 'So'); SEPARATOR = @('Zs', 'Zl', 'Zp'); OTHER = @('Cc', 'Cf', 'Cs', 'Co', 'Cn')
}
foreach ($group in $groups.Keys) {
    $members = @($groups[$group] | ForEach-Object { $categories[$_] })
    Assert-Same "$group = $($groups[$group] -join ' + ')" (Get-Table $group) (Get-Union $members)
}
Assert-Same 'Assigned = the complement of Cn' (Get-Table 'ASSIGNED') (Get-Complement (Get-Table 'UNASSIGNED'))
Assert-Same 'Script=Unknown = Cn + Co + Cs' (Get-Table 'UNKNOWN') (Get-Union @('UNASSIGNED', 'PRIVATE_USE', 'SURROGATE'))
Assert-Same 'Script_Extensions=Unknown = Script=Unknown' (Get-Table 'UNKNOWN_EXTENSIONS') (Get-Table 'UNKNOWN')

# The binary properties, by canonical name, which is the last name of each arm of the match. ASCII, Any and Assigned
# need no table.
$body = [regex]::Match($source, 'fn unicode_property_binary_from_str\(.*?match s \{(.*?)_ => None', 'Singleline').Groups[1].Value
$binary = @([regex]::Matches($body, '((?:"\w+"\s*\|?\s*)+)=>') |
        ForEach-Object { @([regex]::Matches($_.Groups[1].Value, '"(\w+)"'))[-1].Groups[1].Value } |
        Where-Object { $_ -notin @('ASCII', 'Any', 'Assigned') } | Sort-Object -CaseSensitive)

# The names of each script, the long name first, and the tables of its two properties.
$body = [regex]::Match($source, 'fn unicode_property_value_script_from_str\(.*?match s \{(.*?)_ => None', 'Singleline').Groups[1].Value
$scriptNames = [ordered]@{}
foreach ($m in [regex]::Matches($body, '((?:"\w+"\s*\|?\s*)+)=>\s*Some\((\w+)\)')) {
    $scriptNames[$m.Groups[2].Value] = @([regex]::Matches($m.Groups[1].Value, '"(\w+)"') | ForEach-Object { $_.Groups[1].Value })
}
$scriptTables = @{}
$body = [regex]::Match($source, 'fn script_value_ranges\(.*?match value \{(.*?)\n    \}', 'Singleline').Groups[1].Value
foreach ($m in [regex]::Matches($body, '(\w+) => &(\w+),')) { $scriptTables[$m.Groups[1].Value] = $m.Groups[2].Value }
$extensionTables = @{}
$body = [regex]::Match($source, 'fn script_extensions_value_ranges\(.*?match value \{(.*?)\n    \}', 'Singleline').Groups[1].Value
foreach ($m in [regex]::Matches($body, '(\w+) => &(\w+),')) { $extensionTables[$m.Groups[1].Value] = $m.Groups[2].Value }
$scripts = @($scriptNames.Keys | Where-Object { $_ -ne 'Unknown' })
foreach ($script in $scriptNames.Keys) {
    if (-not $scriptTables.ContainsKey($script) -or -not $extensionTables.ContainsKey($script)) {
        throw "The source names the script $script and has no ranges for it."
    }
}

$out = [System.Text.StringBuilder]::new()
function Add-Line([string]$line = '') { [void]$out.Append($line).Append("`n") }

function Add-Values([string]$type, $values) {
    Add-Line "`t$type{"
    for ($k = 0; $k -lt $values.Count; $k += 12) {
        $last = [Math]::Min($k + 12, $values.Count) - 1
        $row = @($values[$k..$last] | ForEach-Object { '0x{0:X}' -f $_ })
        Add-Line ("`t`t" + ($row -join ', ') + ',')
    }
    Add-Line "`t},"
}

# Writes a table under a Go name, unless a table with the same ranges is already written. It returns the name of the
# variable that holds the ranges.
$written = @{}
$shared = 0
function Add-Table([string]$goName, [string]$constant) {
    $bounds = Get-Table $constant
    $key = $bounds -join ','
    if ($written.ContainsKey($key)) {
        $script:shared++
        return $written[$key]
    }
    $written[$key] = $goName
    $low = [System.Collections.Generic.List[int]]::new()
    $high = [System.Collections.Generic.List[int]]::new()
    for ($k = 0; $k -lt $bounds.Count; $k += 2) {
        $lo = $bounds[$k]
        $hi = $bounds[$k + 1]
        if ($hi -le 0xFFFF) { $low.Add($lo); $low.Add($hi) }
        elseif ($lo -gt 0xFFFF) { $high.Add($lo); $high.Add($hi) }
        else { $low.Add($lo); $low.Add(0xFFFF); $high.Add(0x10000); $high.Add($hi) }
    }
    Add-Line "var $goName = Table{"
    if ($low.Count -gt 0) { Add-Values 'r16: []uint16' $low }
    if ($high.Count -gt 0) { Add-Values 'r32: []uint32' $high }
    Add-Line '}'
    Add-Line
    return $goName
}

$body = [System.Text.StringBuilder]::new()
$header = $out
$out = $body

$categoryVars = [ordered]@{}
foreach ($short in $categories.Keys) { $categoryVars[$short] = Add-Table "gc$short" $categories[$short] }
$binaryVars = [ordered]@{}
foreach ($name in $binary) { $binaryVars[$name] = Add-Table "bin$(Get-PascalName $name)" $name.ToUpperInvariant() }
$scriptVars = [ordered]@{}
foreach ($script in $scripts) { $scriptVars[$script] = Add-Table "sc$script" $scriptTables[$script] }
$extensionVars = [ordered]@{}
foreach ($script in $scripts) { $extensionVars[$script] = Add-Table "scx$script" $extensionTables[$script] }

function Add-CaseTable([string]$goName, [string]$constant) {
    $m = [regex]::Match($source, "const ${constant}: \[FoldRange; (\d+)\] = \[(.*?)\];", 'Singleline')
    if (-not $m.Success) { throw "The source has no table $constant." }
    $rows = @([regex]::Matches($m.Groups[2].Value, 'FoldRange::from\((0x[0-9A-Fa-f]+), (\d+), (-?\d+), (\d+)\)'))
    if ($rows.Count -ne [int]$m.Groups[1].Value) {
        throw "Table $constant has $($rows.Count) ranges, and its declaration says $($m.Groups[1].Value)."
    }
    Add-Line "var $goName = []caseRange{"
    foreach ($row in $rows) {
        $first = [Convert]::ToInt32($row.Groups[1].Value, 16)
        $step = [int]$row.Groups[4].Value
        Add-Line ("`t{{0x{0:X}, {1}, {2}, {3}}}," -f $first, $row.Groups[3].Value, $row.Groups[2].Value, ($step - 1))
    }
    Add-Line '}'
    Add-Line
}

Add-Line '// caseFolds is the simple case folding (the C and S rows of CaseFolding.txt). Each row is the first code point of a'
Add-Line '// range, the amount to add, the length of the range, and a mask. The amount applies to the code points whose offset'
Add-Line '// in the range has none of the bits of the mask.'
Add-CaseTable 'caseFolds' 'FOLDS'
Add-Line '// upperCases is the simple uppercase mapping, in the form of caseFolds.'
Add-CaseTable 'upperCases' 'TO_UPPERCASE'

function Add-Lookup([string]$function, [string]$comment, $entries) {
    foreach ($line in $comment -split "`n") { Add-Line "// $line" }
    Add-Line "func $function(name string) *Table {"
    Add-Line "`tswitch name {"
    foreach ($entry in $entries) {
        Add-Line ("`tcase " + (($entry.Names | ForEach-Object { "`"$_`"" }) -join ', ') + ':')
        Add-Line "`t`treturn &$($entry.Variable)"
    }
    Add-Line "`t}"
    Add-Line "`treturn nil"
    Add-Line '}'
    Add-Line
}

$out = $header
Add-Line "// Code generated by gen_tables.ps1 from the Unicode $UnicodeVersion property tables of the regress crate (gen-unicode). DO NOT EDIT."
Add-Line
Add-Line 'package ucd'
Add-Line
Add-Line '// Version is the version of Unicode the tables hold.'
Add-Line "const Version = `"$UnicodeVersion`""
Add-Line
Add-Lookup 'categoryTable' 'categoryTable returns the table of a General_Category value that is not a group, by its short name.' @(
    $categoryVars.Keys | ForEach-Object { @{ Names = @($_); Variable = $categoryVars[$_] } })
Add-Lookup 'binaryTable' 'binaryTable returns the table of a binary property, by its canonical name.' @(
    $binaryVars.Keys | ForEach-Object { @{ Names = @($_); Variable = $binaryVars[$_] } })
Add-Lookup 'scriptTable' "scriptTable returns the table of a Script value other than Unknown, by its long name or by an alias (the ISO`n15924 code, for example)." @(
    $scriptVars.Keys | ForEach-Object { @{ Names = $scriptNames[$_]; Variable = $scriptVars[$_] } })
Add-Lookup 'scriptExtensionsTable' "scriptExtensionsTable returns the table of a Script_Extensions value other than Unknown. It is the table of the`nScript value when the two have the same ranges." @(
    $extensionVars.Keys | ForEach-Object { @{ Names = $scriptNames[$_]; Variable = $extensionVars[$_] } })

Add-Line '// scriptNames lists every name of every script, the long name of each followed by its aliases.'
Add-Line 'var scriptNames = [][]string{'
foreach ($script in $scriptNames.Keys) { Add-Line ("`t{" + (($scriptNames[$script] | ForEach-Object { "`"$_`"" }) -join ', ') + '},') }
Add-Line '}'
Add-Line
Add-Line '// binaryNames lists the canonical name of every binary property that has a table.'
Add-Line 'var binaryNames = []string{'
foreach ($name in $binary) { Add-Line "`t`"$name`"," }
Add-Line '}'
Add-Line
[void]$out.Append($body.ToString())

[System.IO.File]::WriteAllText($Output, $out.ToString().TrimEnd("`n") + "`n", [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $Output with $($written.Count) tables. $shared more share the ranges of one of them."
