<#
.SYNOPSIS
    Writes lib/JSON/Schema/Corvus/Uts46Data.pm, the UTS #46 mapping that the idn-hostname and idn-email formats
    apply to a name before they check it.

.DESCRIPTION
    UTS #46 (Unicode IDNA Compatibility Processing) maps a name before it is checked: some code points are removed
    ("ignored", such as U+200B ZERO WIDTH SPACE) and some are replaced ("mapped", such as the fullwidth letters, which
    become their ASCII forms). The JSON-Schema-Test-Suite expects the idn-hostname format to apply this mapping.

    The script reads IdnaMappingTable.txt of a Unicode version and writes the ignored and mapped entries for the code
    points outside ASCII. (The mapping of ASCII only lowercases the letters, which does not change whether a name is
    valid.) Every other status is left as it is: "valid" and "deviation" code points are kept (nontransitional
    processing), and "disallowed" ones are kept for the IDNA2008 checks that follow to refuse.

    The .NET engine's script (src/Corvus.Text.Json/gen-uts46-data.ps1) writes the same table for C#, and the Rust
    crate's (src-rs/corvus-json-schema/gen-uts46-data.ps1) for Rust; this script is
    that one with another last step.

    The table is a run of 8-byte entries in code point order:

        start (3 bytes, big-endian) | count - 1 (2 bytes) | offset into the pool (2 bytes) | length in the pool (1 byte)

    An ignored entry has the offset 0xFFFF and the length 0. The pool holds the UTF-8 text of every distinct mapping.

.PARAMETER MappingTable
    The path of IdnaMappingTable.txt. Download it from https://www.unicode.org/Public/<version>/idna/IdnaMappingTable.txt.

.PARAMETER UnicodeVersion
    The Unicode version of the table, which the generated file records.

.PARAMETER Output
    The file to write. The default is lib/JSON/Schema/Corvus/Uts46Data.pm of the distribution.

.EXAMPLE
    pwsh gen-uts46-data.ps1 -MappingTable ./IdnaMappingTable.txt
#>
param(
    [Parameter(Mandatory = $true)][string]$MappingTable,
    [string]$UnicodeVersion = '17.0.0',
    [string]$Output = (Join-Path $PSScriptRoot '../lib/JSON/Schema/Corvus/Uts46Data.pm')
)

$ErrorActionPreference = 'Stop'

$entries = [System.Collections.Generic.List[object]]::new()
$pool = [System.Collections.Generic.List[byte]]::new()
$poolOffsets = @{}

foreach ($line in [System.IO.File]::ReadAllLines((Resolve-Path $MappingTable))) {
    $content = $line.Split('#')[0].Trim()
    if ($content.Length -eq 0) {
        continue
    }

    $fields = @($content.Split(';') | ForEach-Object { $_.Trim() })
    $status = $fields[1]
    if ($status -ne 'mapped' -and $status -ne 'ignored') {
        continue
    }

    $range = $fields[0].Split('..')
    $start = [Convert]::ToInt32($range[0], 16)
    $end = if ($range.Length -gt 1) { [Convert]::ToInt32($range[-1], 16) } else { $start }
    if ($end -lt 0x80) {
        continue
    }

    if ($start -lt 0x80) {
        $start = 0x80
    }

    if ($end - $start -gt 0xFFFF) {
        throw "The range $($fields[0]) is too long for an entry."
    }

    $offset = 0xFFFF
    $length = 0
    if ($status -eq 'mapped') {
        $text = -join @($fields[2].Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { [char]::ConvertFromUtf32([Convert]::ToInt32($_, 16)) })
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
        if (-not $poolOffsets.ContainsKey($text)) {
            $poolOffsets[$text] = $pool.Count
            $pool.AddRange($bytes)
        }

        $offset = $poolOffsets[$text]
        $length = $bytes.Length
        if ($offset -ge 0xFFFF -or $length -gt 0xFF) {
            throw "The pool is too large for the entry layout."
        }
    }

    $entries.Add([pscustomobject]@{ Start = $start; Count = $end - $start; Offset = $offset; Length = $length })
}

function Format-Hex([System.Collections.Generic.IEnumerable[byte]]$bytes) {
    $all = @($bytes)
    $lines = [System.Collections.Generic.List[string]]::new()
    for ($i = 0; $i -lt $all.Length; $i += 56) {
        $chunk = $all[$i..([Math]::Min($i + 55, $all.Length - 1))]
        $lines.Add('  ' + (($chunk | ForEach-Object { '{0:x2}' -f $_ }) -join ''))
    }

    return $lines -join "`n"
}

$table = [System.Collections.Generic.List[byte]]::new()
foreach ($e in $entries) {
    $table.Add([byte](($e.Start -shr 16) -band 0xFF))
    $table.Add([byte](($e.Start -shr 8) -band 0xFF))
    $table.Add([byte]($e.Start -band 0xFF))
    $table.Add([byte](($e.Count -shr 8) -band 0xFF))
    $table.Add([byte]($e.Count -band 0xFF))
    $table.Add([byte](($e.Offset -shr 8) -band 0xFF))
    $table.Add([byte]($e.Offset -band 0xFF))
    $table.Add([byte]$e.Length)
}

$text = @"
package JSON::Schema::Corvus::Uts46Data;

# Written by tools/gen-uts46-data.ps1 from IdnaMappingTable.txt. Do not edit.

use strict;
use warnings;

# The Unicode version of the UTS #46 mapping.
our `$UNICODE_VERSION = '$UnicodeVersion';

# The ignored and mapped entries outside ASCII, 8 bytes each, in code point order: the first code point (3 bytes,
# big-endian), the number of code points less one (2 bytes), the offset of the mapping in `$POOL (2 bytes, 0xFFFF for
# an ignored entry) and its length (1 byte).
our `$ENTRIES = pack 'H*', join '', qw(
$(Format-Hex $table)
);

# The UTF-8 text of every distinct mapping.
our `$POOL = pack 'H*', join '', qw(
$(Format-Hex $pool)
);

1;
"@

$text = $text.Replace("`r`n", "`n")
[System.IO.File]::WriteAllText($Output, $text.TrimEnd("`n") + "`n", [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $($entries.Count) entries and $($pool.Count) pool bytes to $Output"
