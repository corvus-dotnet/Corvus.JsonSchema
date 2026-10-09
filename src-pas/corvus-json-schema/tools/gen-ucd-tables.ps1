<#
.SYNOPSIS
    Writes Corvus.JsonSchema.UcdTables.inc, the Unicode data of the package.

.DESCRIPTION
    The package takes every Unicode property it reads from the tables this script writes, and nothing from the
    run-time library of the compiler, so that its answers do not depend on the compiler that built it.

    This is the generator of the Go module (src-go/corvus-json-schema/internal/ucd/gen_tables.ps1) with the part that
    writes Go replaced by a part that writes Pascal. It reads the same source and makes the same choices, so the two
    sets of tables hold the same data.

    The data comes from unicodetables.rs of the regress crate, which the Rust port of the evaluator depends on and
    which is generated from the Unicode Character Database. The script copies the General_Category values, the
    binary properties, the Script values, the Script_Extensions ranges of the scripts whose extensions differ from
    their Script ranges, the simple case folding and the simple uppercase mapping.

    A table is written as inclusive ranges, the ones up to U+FFFF as UInt16 and the rest as UInt32. Tables with the
    same ranges are written once. The values that follow from others are not written at all, and
    Corvus.JsonSchema.Ucd.pas builds them. They are the General_Category groups (L, LC, M, N, P, S, Z and C),
    Assigned (the complement of Cn) and the Unknown script (Cn, Co and Cs together). The script checks each of those
    against the source and stops if one differs.

    Pascal has no case statement over strings that Delphi also compiles, so each lookup of a table by name is written
    as two arrays, the names in ordinal order and the number of the table of each, which the unit searches.

.PARAMETER RegressTables
    The path of src/unicodetables.rs in a checkout of the regress crate, for example under
    ~/.cargo/registry/src/*/regress-0.12.0.

.PARAMETER UnicodeVersion
    The version of Unicode the source was generated from. regress 0.12.0 carries Unicode 17.0.0.

.PARAMETER Output
    The file to write. The default is src/Corvus.JsonSchema.UcdTables.inc of the package this script is in.

.EXAMPLE
    pwsh gen-ucd-tables.ps1 -RegressTables ~/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f/regress-0.12.0/src/unicodetables.rs
#>
param(
    [Parameter(Mandatory = $true)][string]$RegressTables,
    [string]$UnicodeVersion = '17.0.0',
    [string]$Output = (Join-Path (Join-Path (Split-Path $PSScriptRoot -Parent) 'src') 'Corvus.JsonSchema.UcdTables.inc')
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
        throw "$what does not hold for this source. Corvus.JsonSchema.Ucd.pas builds that value from the others, so it needs a table of its own."
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

# Writes items separated by commas, in rows no longer than a line of the package may be.
function Add-Wrapped([string]$indent, [string[]]$items) {
    $row = [System.Text.StringBuilder]::new($indent)
    for ($k = 0; $k -lt $items.Count; $k++) {
        $item = $items[$k]
        if ($k -lt $items.Count - 1) { $item += ',' }
        if ($row.Length -gt $indent.Length -and $row.Length + 1 + $item.Length -gt 118) {
            Add-Line $row.ToString()
            $row = [System.Text.StringBuilder]::new($indent)
        }
        if ($row.Length -gt $indent.Length) { [void]$row.Append(' ') }
        [void]$row.Append($item)
    }
    if ($row.Length -gt $indent.Length) { Add-Line $row.ToString() }
}

function Add-Values([string]$name, [string]$type, $values) {
    Add-Line "  $($name): array[0..$($values.Count - 1)] of $type = ("
    for ($k = 0; $k -lt $values.Count; $k += 12) {
        $last = [Math]::Min($k + 12, $values.Count) - 1
        $row = @($values[$k..$last] | ForEach-Object { '${0:X}' -f $_ })
        $end = if ($last -eq $values.Count - 1) { '' } else { ',' }
        Add-Line ('    ' + ($row -join ', ') + $end)
    }
    Add-Line '  );'
}

# Writes a table under a Pascal name, unless a table with the same ranges is already written. It returns the name of
# the constant that numbers the table. The ranges are in the arrays of that name with R16 and R32 after it.
$written = @{}
$order = [System.Collections.Generic.List[object]]::new()
$identifiers = @{}
$shared = 0
function Add-Table([string]$pascalName, [string]$constant) {
    $bounds = Get-Table $constant
    $key = $bounds -join ','
    if ($written.ContainsKey($key)) {
        $script:shared++
        return $written[$key]
    }
    # Pascal does not tell names apart by case.
    if ($identifiers.ContainsKey($pascalName.ToLowerInvariant())) { throw "Two tables would be named $pascalName." }
    $identifiers[$pascalName.ToLowerInvariant()] = $true
    $written[$key] = $pascalName
    $low = [System.Collections.Generic.List[int]]::new()
    $high = [System.Collections.Generic.List[int]]::new()
    for ($k = 0; $k -lt $bounds.Count; $k += 2) {
        $lo = $bounds[$k]
        $hi = $bounds[$k + 1]
        if ($hi -le 0xFFFF) { $low.Add($lo); $low.Add($hi) }
        elseif ($lo -gt 0xFFFF) { $high.Add($lo); $high.Add($hi) }
        else { $low.Add($lo); $low.Add(0xFFFF); $high.Add(0x10000); $high.Add($hi) }
    }
    $order.Add(@{ Name = $pascalName; Low = ($low.Count -gt 0); High = ($high.Count -gt 0) })
    if ($low.Count -gt 0) { Add-Values "${pascalName}R16" 'UInt16' $low }
    if ($high.Count -gt 0) { Add-Values "${pascalName}R32" 'UInt32' $high }
    Add-Line
    return $pascalName
}

$body = [System.Text.StringBuilder]::new()
$header = $out
$out = $body

$categoryVars = [ordered]@{}
foreach ($short in $categories.Keys) { $categoryVars[$short] = Add-Table "Gc$short" $categories[$short] }
$binaryVars = [ordered]@{}
foreach ($name in $binary) { $binaryVars[$name] = Add-Table "Bin$(Get-PascalName $name)" $name.ToUpperInvariant() }
$scriptVars = [ordered]@{}
foreach ($script in $scripts) { $scriptVars[$script] = Add-Table "Sc$script" $scriptTables[$script] }
$extensionVars = [ordered]@{}
foreach ($script in $scripts) { $extensionVars[$script] = Add-Table "Scx$script" $extensionTables[$script] }

function Add-CaseTable([string]$pascalName, [string]$constant) {
    $m = [regex]::Match($source, "const ${constant}: \[FoldRange; (\d+)\] = \[(.*?)\];", 'Singleline')
    if (-not $m.Success) { throw "The source has no table $constant." }
    $rows = @([regex]::Matches($m.Groups[2].Value, 'FoldRange::from\((0x[0-9A-Fa-f]+), (\d+), (-?\d+), (\d+)\)'))
    if ($rows.Count -ne [int]$m.Groups[1].Value) {
        throw "Table $constant has $($rows.Count) ranges, and its declaration says $($m.Groups[1].Value)."
    }
    Add-Line "  $($pascalName): array[0..$($rows.Count - 1)] of TUcdCaseRange = ("
    for ($k = 0; $k -lt $rows.Count; $k++) {
        $row = $rows[$k]
        $first = [Convert]::ToInt32($row.Groups[1].Value, 16)
        $step = [int]$row.Groups[4].Value
        $end = if ($k -eq $rows.Count - 1) { '' } else { ',' }
        Add-Line (('    (First: ${0:X}; Delta: {1}; Length: {2}; Mask: {3})' -f $first, $row.Groups[3].Value, $row.Groups[2].Value, ($step - 1)) + $end)
    }
    Add-Line '  );'
    Add-Line
}

Add-Line '  { CaseFolds is the simple case folding (the C and S rows of CaseFolding.txt). Each row is the first code point of a'
Add-Line '    range, the amount to add, the length of the range, and a mask. The amount applies to the code points whose offset'
Add-Line '    in the range has none of the bits of the mask. }'
Add-CaseTable 'CaseFolds' 'FOLDS'
Add-Line '  { UpperCases is the simple uppercase mapping, in the form of CaseFolds. }'
Add-CaseTable 'UpperCases' 'TO_UPPERCASE'

# Writes the lookup of a table by name: the names in ordinal order, and the number of the table of each.
function Add-Lookup([string]$prefix, [string]$comment, $entries) {
    $pairs = [System.Collections.Generic.SortedDictionary[string, string]]::new([System.StringComparer]::Ordinal)
    foreach ($entry in $entries) {
        foreach ($name in $entry.Names) { $pairs.Add($name, $entry.Variable) }
    }
    $lines = @($comment -split "`n")
    for ($k = 0; $k -lt $lines.Count; $k++) {
        $open = if ($k -eq 0) { '  { ' } else { '    ' }
        $close = if ($k -eq $lines.Count - 1) { ' }' } else { '' }
        Add-Line ($open + $lines[$k] + $close)
    }
    Add-Line "  $($prefix)Names: array[0..$($pairs.Count - 1)] of UTF8String = ("
    Add-Wrapped '    ' @($pairs.Keys | ForEach-Object { "'$_'" })
    Add-Line '  );'
    Add-Line "  $($prefix)Tables: array[0..$($pairs.Count - 1)] of Int32 = ("
    Add-Wrapped '    ' @($pairs.Values)
    Add-Line '  );'
    Add-Line
}

$out = $header
Add-Line "{ Code generated by tools/gen-ucd-tables.ps1 from the Unicode $UnicodeVersion property tables of the regress crate"
Add-Line '  (gen-unicode). DO NOT EDIT. }'
Add-Line
Add-Line 'const'
Add-Line '  { TablesVersion is the version of Unicode the tables hold. }'
Add-Line "  TablesVersion = '$UnicodeVersion';"
Add-Line
Add-Line '  { TableCount is the number of tables. Each constant below numbers one of them. }'
Add-Line "  TableCount = $($order.Count);"
for ($k = 0; $k -lt $order.Count; $k++) { Add-Line "  $($order[$k].Name) = $k;" }
Add-Line
Add-Lookup 'CategoryTable' 'CategoryTable holds the table of each General_Category value that is not a group, by its short name.' @(
    $categoryVars.Keys | ForEach-Object { @{ Names = @($_); Variable = $categoryVars[$_] } })
Add-Lookup 'BinaryTable' 'BinaryTable holds the table of each binary property, by its canonical name.' @(
    $binaryVars.Keys | ForEach-Object { @{ Names = @($_); Variable = $binaryVars[$_] } })
Add-Lookup 'ScriptTable' "ScriptTable holds the table of each Script value other than Unknown, by its long name and by each alias (the ISO`n15924 code, for example)." @(
    $scriptVars.Keys | ForEach-Object { @{ Names = $scriptNames[$_]; Variable = $scriptVars[$_] } })
Add-Lookup 'ScriptExtensionsTable' "ScriptExtensionsTable holds the table of each Script_Extensions value other than Unknown. It is the table of the`nScript value when the two have the same ranges." @(
    $extensionVars.Keys | ForEach-Object { @{ Names = $scriptNames[$_]; Variable = $extensionVars[$_] } })

$flat = [System.Collections.Generic.List[string]]::new()
$firsts = [System.Collections.Generic.List[string]]::new()
foreach ($script in $scriptNames.Keys) {
    $firsts.Add([string]$flat.Count)
    foreach ($name in $scriptNames[$script]) { $flat.Add("'$name'") }
}
$firsts.Add([string]$flat.Count)
Add-Line '  { ScriptNameList lists every name of every script, the long name of each followed by its aliases. The names of'
Add-Line '    script K are the elements ScriptNameFirst[K] to ScriptNameFirst[K + 1] - 1. }'
Add-Line "  ScriptCount = $($scriptNames.Count);"
Add-Line "  ScriptNameList: array[0..$($flat.Count - 1)] of UTF8String = ("
Add-Wrapped '    ' $flat.ToArray()
Add-Line '  );'
Add-Line "  ScriptNameFirst: array[0..$($scriptNames.Count)] of Int32 = ("
Add-Wrapped '    ' $firsts.ToArray()
Add-Line '  );'
Add-Line
Add-Line '  { BinaryNameList lists the canonical name of every binary property that has a table. }'
Add-Line "  BinaryNameList: array[0..$($binary.Count - 1)] of UTF8String = ("
Add-Wrapped '    ' @($binary | ForEach-Object { "'$_'" })
Add-Line '  );'
Add-Line
[void]$out.Append($body.ToString())

Add-Line '{ LoadTables copies the ranges of every table into Tables, where the functions of the unit read them. }'
Add-Line 'procedure LoadTables;'
Add-Line 'begin'
foreach ($table in $order) {
    if ($table.Low) { Add-Line "  Load16($($table.Name), $($table.Name)R16);" }
    if ($table.High) { Add-Line "  Load32($($table.Name), $($table.Name)R32);" }
}
Add-Line 'end;'

$text = $out.ToString().TrimEnd("`n")
foreach ($line in $text -split "`n") {
    if ($line.Length -gt 120) { throw "A line of the output is longer than 120 characters: $line" }
}
# The file ends without a line break, as every file of the package does.
[System.IO.File]::WriteAllText($Output, $text, [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $Output with $($written.Count) tables. $shared more share the ranges of one of them."