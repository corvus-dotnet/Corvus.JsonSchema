<#
.SYNOPSIS
    Writes EcmaUnicodeData.java, the Unicode data behind the \p{...} escapes and the case-insensitive matching of
    ECMA-262 patterns.

.DESCRIPTION
    java.util.regex takes its Unicode data from the JDK it runs on, and Java 17, 21 and 25 each ship a different
    Unicode version. ECMA-262 follows the latest one. So the library carries its own tables, and a pattern means the
    same on every JDK.

    The data comes from unicodetables.rs of the regress crate, which the Rust port of the evaluator depends on and
    which is generated from the Unicode Character Database. The Go port's tables come from the same file. The script
    copies:

    - the thirty General_Category values that are not unions of others, and the names of all of them;
    - the binary properties ECMA-262 lists, with their names;
    - the Script and Script_Extensions ranges of each script, with its names;
    - the simple case folding and the single character uppercase mapping, which case-insensitive matching uses.

    Each table is written as a string of printable ASCII characters. EcmaUnicode.java reads a table the first time a
    pattern names its property.

    When the data was generated for Unicode 17.0.0, the set of every property expression (1,714 of them, counting
    aliases) was compared with what V8 answers for it under Unicode 17 and with the files of the Unicode Character
    Database (DerivedGeneralCategory.txt, Scripts.txt, ScriptExtensions.txt, PropList.txt, DerivedCoreProperties.txt,
    DerivedBinaryProperties.txt, DerivedNormalizationProps.txt, emoji-data.txt), and the two case mappings with
    CaseFolding.txt, UnicodeData.txt and SpecialCasing.txt. All agreed. Those comparisons are not part of the build.
    Repeat them when the data moves to a later Unicode version. EcmaUnicodeTest checks what can be checked without
    them.

.PARAMETER RegressTables
    The path of src/unicodetables.rs in a checkout of the regress crate, for example under
    ~/.cargo/registry/src/*/regress-0.12.0.

.PARAMETER UnicodeVersion
    The Unicode version of the tables, which the generated file records.

.PARAMETER Output
    The file to write. The default is EcmaUnicodeData.java in the library's source tree.

.EXAMPLE
    pwsh gen-ecma-unicode-data.ps1 -RegressTables ~/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f/regress-0.12.0/src/unicodetables.rs
#>
param(
    [Parameter(Mandatory = $true)][string]$RegressTables,
    [string]$UnicodeVersion = '17.0.0',
    [string]$Output = (Join-Path $PSScriptRoot 'src/main/java/io/github/corvusdotnet/jsonschema/EcmaUnicodeData.java')
)

$ErrorActionPreference = 'Stop'
$source = [System.IO.File]::ReadAllText((Resolve-Path $RegressTables))

# Every interval table of the source, by its constant name, as a flat list of inclusive bounds.
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

# The body of the match expression of a function of the source.
function Get-MatchBody([string]$function) {
    $m = [regex]::Match($source, "fn $function\(.*?match \w+ \{(.*?)\n    \}", 'Singleline')
    if (-not $m.Success) { throw "The source has no function $function." }
    return $m.Groups[1].Value
}

# The arms of a from_str function, as an ordered map from the enum member to its names.
function Get-Names([string]$function) {
    $names = [ordered]@{}
    foreach ($m in [regex]::Matches((Get-MatchBody $function), '((?:"\w+"\s*\|?\s*)+)=>\s*Some\((\w+)\)')) {
        $names[$m.Groups[2].Value] = @([regex]::Matches($m.Groups[1].Value, '"(\w+)"') | ForEach-Object { $_.Groups[1].Value })
    }
    return $names
}

# Numbers are written five bits to a character, lowest bits first. A character from the second half of the alphabet
# says another follows.
$alphabet = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz+/'
function Add-Number([System.Text.StringBuilder]$sb, [long]$value) {
    if ($value -lt 0) { throw "Cannot encode $value." }
    while ($value -ge 32) {
        [void]$sb.Append($alphabet[32 + ($value -band 31)])
        $value = $value -shr 5
    }
    [void]$sb.Append($alphabet[$value])
}

# A table is written as, for each range, the gap from the end of the range before it and its length less one.
function ConvertTo-Text($bounds) {
    $sb = [System.Text.StringBuilder]::new()
    $next = 0
    for ($k = 0; $k -lt $bounds.Count; $k += 2) {
        if ($bounds[$k] -lt $next -or $bounds[$k + 1] -lt $bounds[$k]) { throw 'A table is not sorted.' }
        Add-Number $sb ($bounds[$k] - $next)
        Add-Number $sb ($bounds[$k + 1] - $bounds[$k])
        $next = $bounds[$k + 1] + 1
    }
    return $sb.ToString()
}

# A case mapping is written as, for each run, the gap from the end of the run before it, its length less one, its
# delta (twice its size, plus one when negative) and its stride less one.
function ConvertTo-FoldText([string]$constant) {
    $m = [regex]::Match($source, "const ${constant}: \[FoldRange; (\d+)\] = \[(.*?)\];", 'Singleline')
    if (-not $m.Success) { throw "The source has no table $constant." }
    $runs = @([regex]::Matches($m.Groups[2].Value, 'FoldRange::from\((0x[0-9A-Fa-f]+), (\d+), (-?\d+), (\d+)\)'))
    if ($runs.Count -ne [int]$m.Groups[1].Value) { throw "Table $constant has $($runs.Count) runs." }
    $sb = [System.Text.StringBuilder]::new()
    $next = 0
    foreach ($run in $runs) {
        $start = [Convert]::ToInt32($run.Groups[1].Value, 16)
        $length = [int]$run.Groups[2].Value
        $delta = [int]$run.Groups[3].Value
        $stride = [int]$run.Groups[4].Value
        if ($start -lt $next -or $length -lt 1 -or $stride -lt 1) { throw "Table $constant is not sorted." }
        Add-Number $sb ($start - $next)
        Add-Number $sb ($length - 1)
        Add-Number $sb ($(if ($delta -lt 0) { -2 * $delta + 1 } else { 2 * $delta }))
        Add-Number $sb ($stride - 1)
        $next = $start + $length
    }
    return $sb.ToString()
}

$out = [System.Text.StringBuilder]::new()
function Add-Line([string]$line = '') { [void]$out.Append($line).Append("`n") }

# A string constant, split so that no line of the file is long.
function Add-Text([string]$declaration, [string]$text) {
    $width = 103
    if ($text.Length -le $width - $declaration.Length) {
        Add-Line "    $declaration `"$text`";"
        return
    }
    Add-Line "    $declaration"
    for ($k = 0; $k -lt $text.Length; $k += $width) {
        $piece = $text.Substring($k, [Math]::Min($width, $text.Length - $k))
        $end = if ($k + $width -ge $text.Length) { ';' } else { '' }
        $lead = if ($k -eq 0) { '  ' } else { '+ ' }
        Add-Line "            $lead`"$piece`"$end"
    }
}

function Add-Array([string]$declaration, $items, [scriptblock]$format) {
    Add-Line "    $declaration = {"
    $line = '       '
    foreach ($item in $items) {
        $text = (& $format $item) + ','
        if ($line.Length + 1 + $text.Length -gt 118) {
            Add-Line $line
            $line = '       '
        }
        $line += ' ' + $text
    }
    if ($line.Trim().Length -gt 0) { Add-Line $line }
    Add-Line '    };'
}

$quote = { param($s) "`"$s`"" }
$plain = { param($n) "$n" }

# The tables, in the order of their numbers.
$tableTexts = [System.Collections.Generic.List[string]]::new()
$tableNotes = [System.Collections.Generic.List[string]]::new()
$tableNumbers = @{}
function Add-TableNumber([string]$constant, [string]$note) {
    if ($tableNumbers.ContainsKey($constant)) { return $tableNumbers[$constant] }
    $tableNumbers[$constant] = $tableTexts.Count
    $tableTexts.Add((ConvertTo-Text (Get-Table $constant)))
    $tableNotes.Add($note)
    return $tableNumbers[$constant]
}

function Get-Union($constants) {
    $member = [System.Collections.BitArray]::new(0x110000)
    foreach ($constant in $constants) {
        $bounds = Get-Table $constant
        for ($k = 0; $k -lt $bounds.Count; $k += 2) {
            for ($c = $bounds[$k]; $c -le $bounds[$k + 1]; $c++) { $member[$c] = $true }
        }
    }
    return , $member
}

# General_Category. The values that are not unions come first, so that a union is a set of bits.
$leaves = [ordered]@{
    UppercaseLetter = 'UPPERCASE_LETTER'; LowercaseLetter = 'LOWERCASE_LETTER'; TitlecaseLetter = 'TITLECASE_LETTER'
    ModifierLetter = 'MODIFIER_LETTER'; OtherLetter = 'OTHER_LETTER'; NonspacingMark = 'NONSPACING_MARK'
    SpacingMark = 'SPACING_MARK'; EnclosingMark = 'ENCLOSING_MARK'; DecimalNumber = 'DECIMAL_NUMBER'
    LetterNumber = 'LETTER_NUMBER'; OtherNumber = 'OTHER_NUMBER'; ConnectorPunctuation = 'CONNECTOR_PUNCTUATION'
    DashPunctuation = 'DASH_PUNCTUATION'; OpenPunctuation = 'OPEN_PUNCTUATION'; ClosePunctuation = 'CLOSE_PUNCTUATION'
    InitialPunctuation = 'INITIAL_PUNCTUATION'; FinalPunctuation = 'FINAL_PUNCTUATION'
    OtherPunctuation = 'OTHER_PUNCTUATION'; MathSymbol = 'MATH_SYMBOL'; CurrencySymbol = 'CURRENCY_SYMBOL'
    ModifierSymbol = 'MODIFIER_SYMBOL'; OtherSymbol = 'OTHER_SYMBOL'; SpaceSeparator = 'SPACE_SEPARATOR'
    LineSeparator = 'LINE_SEPARATOR'; ParagraphSeparator = 'PARAGRAPH_SEPARATOR'; Control = 'CONTROL'
    Format = 'FORMAT'; Surrogate = 'SURROGATE'; PrivateUse = 'PRIVATE_USE'; Unassigned = 'UNASSIGNED'
}
$unions = [ordered]@{
    CasedLetter = @('UppercaseLetter', 'LowercaseLetter', 'TitlecaseLetter')
    Letter      = @('UppercaseLetter', 'LowercaseLetter', 'TitlecaseLetter', 'ModifierLetter', 'OtherLetter')
    Mark        = @('NonspacingMark', 'SpacingMark', 'EnclosingMark')
    Number      = @('DecimalNumber', 'LetterNumber', 'OtherNumber')
    Punctuation = @('ConnectorPunctuation', 'DashPunctuation', 'OpenPunctuation', 'ClosePunctuation',
        'InitialPunctuation', 'FinalPunctuation', 'OtherPunctuation')
    Symbol      = @('MathSymbol', 'CurrencySymbol', 'ModifierSymbol', 'OtherSymbol')
    Separator   = @('SpaceSeparator', 'LineSeparator', 'ParagraphSeparator')
    Other       = @('Control', 'Format', 'Surrogate', 'PrivateUse', 'Unassigned')
}
$unionTables = @{
    CasedLetter = 'CASED_LETTER'; Letter = 'LETTER'; Mark = 'MARK'; Number = 'NUMBER'; Punctuation = 'PUNCTUATION'
    Symbol = 'SYMBOL'; Separator = 'SEPARATOR'; Other = 'OTHER'
}
$leafBits = @{}
foreach ($leaf in $leaves.Keys) {
    $leafBits[$leaf] = 1 -shl (Add-TableNumber $leaves[$leaf] "General_Category=$leaf")
}
if ($tableTexts.Count -ne 30) { throw 'There are thirty General_Category values that are not unions.' }

# The thirty values must cover every code point once, and each union must be what the source says it is.
$all = Get-Union @($leaves.Values)
$total = 0
foreach ($constant in $leaves.Values) {
    $bounds = Get-Table $constant
    for ($k = 0; $k -lt $bounds.Count; $k += 2) { $total += $bounds[$k + 1] - $bounds[$k] + 1 }
}
if ($total -ne 0x110000) { throw "The General_Category values cover $total code points." }
for ($c = 0; $c -lt 0x110000; $c++) { if (-not $all[$c]) { throw "No General_Category value has U+$('{0:X4}' -f $c)." } }
foreach ($union in $unions.Keys) {
    $expected = Get-Union @($unionTables[$union])
    $actual = Get-Union @($unions[$union] | ForEach-Object { $leaves[$_] })
    for ($c = 0; $c -lt 0x110000; $c++) {
        if ($expected[$c] -ne $actual[$c]) { throw "General_Category=$union differs from its parts at U+$('{0:X4}' -f $c)." }
    }
}

$categoryNames = [System.Collections.Generic.List[string]]::new()
$categoryBits = [System.Collections.Generic.List[int]]::new()
$sourceCategories = Get-Names 'unicode_property_value_general_category_from_str'
if ($sourceCategories.Count -ne 38) { throw "The source names $($sourceCategories.Count) General_Category values." }
foreach ($member in $sourceCategories.Keys) {
    $bits = 0
    if ($leafBits.ContainsKey($member)) {
        $bits = $leafBits[$member]
    } elseif ($unions.Contains($member)) {
        foreach ($part in $unions[$member]) { $bits = $bits -bor $leafBits[$part] }
    } else {
        throw "The General_Category value $member is not known to this script."
    }
    foreach ($name in $sourceCategories[$member]) {
        $categoryNames.Add($name)
        $categoryBits.Add($bits)
    }
}

# The binary properties.
$binaryTableOf = @{}
foreach ($m in [regex]::Matches((Get-MatchBody 'binary_property_ranges'), '(\w+) => (\w+)_ranges\(\),')) {
    $binaryTableOf[$m.Groups[1].Value] = $m.Groups[2].Value.ToUpperInvariant()
}
$binaryNames = [System.Collections.Generic.List[string]]::new()
$binaryTables = [System.Collections.Generic.List[int]]::new()
$sourceBinaries = Get-Names 'unicode_property_binary_from_str'
if ($sourceBinaries.Count -ne 53) { throw "The source names $($sourceBinaries.Count) binary properties, and ECMA-262 lists 53." }
foreach ($member in $sourceBinaries.Keys) {
    $number = Add-TableNumber $binaryTableOf[$member] $sourceBinaries[$member][-1]
    foreach ($name in $sourceBinaries[$member]) {
        $binaryNames.Add($name)
        $binaryTables.Add($number)
    }
}

# The scripts. Script_Extensions has a table of its own only where it differs from Script.
$scriptTableOf = @{}
foreach ($m in [regex]::Matches((Get-MatchBody 'script_value_ranges'), '(\w+) => &(\w+),')) {
    $scriptTableOf[$m.Groups[1].Value] = $m.Groups[2].Value
}
$extensionTableOf = @{}
foreach ($m in [regex]::Matches((Get-MatchBody 'script_extensions_value_ranges'), '(\w+) => &(\w+),')) {
    $extensionTableOf[$m.Groups[1].Value] = $m.Groups[2].Value
}
$scriptNames = [System.Collections.Generic.List[string]]::new()
$scriptTables = [System.Collections.Generic.List[int]]::new()
$extensionTables = [System.Collections.Generic.List[int]]::new()
$sourceScripts = Get-Names 'unicode_property_value_script_from_str'
foreach ($member in $sourceScripts.Keys) {
    $long = $sourceScripts[$member][0]
    $script = Add-TableNumber $scriptTableOf[$member] "Script=$long"
    $extensions = Add-TableNumber $extensionTableOf[$member] "Script_Extensions=$long"
    foreach ($name in $sourceScripts[$member]) {
        $scriptNames.Add($name)
        $scriptTables.Add($script)
        $extensionTables.Add($extensions)
    }
}

Add-Line "// Generated by gen-ecma-unicode-data.ps1 from the Unicode $UnicodeVersion property tables of the regress crate."
Add-Line '// DO NOT EDIT.'
Add-Line
Add-Line 'package io.github.corvusdotnet.jsonschema;'
Add-Line
Add-Line '/**'
Add-Line ' * The Unicode data behind the property escapes and the case-insensitive matching of ECMA-262 patterns, which'
Add-Line ' * {@link EcmaUnicode} reads. The library carries it so that a pattern means the same on every JDK.'
Add-Line ' *'
Add-Line ' * <p>A number is written five bits to a character, lowest bits first, in the alphabet of base 64. A character from'
Add-Line ' * the second half of the alphabet says another follows. A table of code points is, for each range, the gap from the'
Add-Line ' * end of the range before it and the length of the range less one. A case mapping is, for each run, the gap from the'
Add-Line ' * end of the run before it, its length less one, its delta (twice its size, plus one when negative) and its stride'
Add-Line ' * less one. The delta applies to every code point of the run that is a whole number of strides from its start.'
Add-Line ' */'
Add-Line 'final class EcmaUnicodeData {'
Add-Line '    private EcmaUnicodeData() {'
Add-Line '    }'
Add-Line
Add-Line '    /** The Unicode version of the data. */'
Add-Line "    static final String UNICODE_VERSION = `"$UnicodeVersion`";"
Add-Line
Add-Line '    /** The number of tables. */'
Add-Line "    static final int TABLE_COUNT = $($tableTexts.Count);"
Add-Line
Add-Line '    /** The names of the General_Category values. */'
Add-Array 'static final String[] CATEGORY_NAMES' $categoryNames $quote
Add-Line
Add-Line '    /** For each name of {@link #CATEGORY_NAMES}, the tables its value is the union of, one bit to a table. */'
Add-Array 'static final int[] CATEGORY_TABLES' $categoryBits { param($n) '0x{0:x}' -f $n }
Add-Line
Add-Line '    /** The names of the binary properties. */'
Add-Array 'static final String[] BINARY_NAMES' $binaryNames $quote
Add-Line
Add-Line '    /** For each name of {@link #BINARY_NAMES}, the number of its table. */'
Add-Array 'static final int[] BINARY_TABLES' $binaryTables $plain
Add-Line
Add-Line '    /** The names of the scripts. */'
Add-Array 'static final String[] SCRIPT_NAMES' $scriptNames $quote
Add-Line
Add-Line '    /** For each name of {@link #SCRIPT_NAMES}, the number of the table of its Script property. */'
Add-Array 'static final int[] SCRIPT_TABLES' $scriptTables $plain
Add-Line
Add-Line '    /** For each name of {@link #SCRIPT_NAMES}, the number of the table of its Script_Extensions property. */'
Add-Array 'static final int[] SCRIPT_EXTENSION_TABLES' $extensionTables $plain
Add-Line
Add-Line '    /** Simple case folding, which is how a pattern read with the u flag grammar ignores case. */'
Add-Text 'static final String SIMPLE_FOLDING =' (ConvertTo-FoldText 'FOLDS')
Add-Line
Add-Line '    /** The uppercase mapping of one character, which is how a pattern read with no flag ignores case. */'
Add-Text 'static final String UPPERCASE =' (ConvertTo-FoldText 'TO_UPPERCASE')
Add-Line
Add-Line '    /** The text of a table. */'
Add-Line '    static String table(int number) {'
Add-Line '        switch (number) {'
for ($k = 0; $k -lt $tableTexts.Count; $k++) {
    Add-Line "            case ${k}:"
    Add-Line "                return T$k;"
}
Add-Line '            default:'
Add-Line '                throw new IllegalArgumentException();'
Add-Line '        }'
Add-Line '    }'
for ($k = 0; $k -lt $tableTexts.Count; $k++) {
    Add-Line
    Add-Line "    /** $($tableNotes[$k]). */"
    Add-Text "private static final String T$k =" $tableTexts[$k]
}
Add-Line '}'

[System.IO.File]::WriteAllText($Output, $out.ToString().TrimEnd("`n") + "`n", [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $Output ($($tableTexts.Count) tables)."
