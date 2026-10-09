<#
.SYNOPSIS
Writes src/Corvus.JsonSchema.MetaschemaFiles.inc: the standard metaschemas as constants, which
Corvus.JsonSchema.Metaschemas serves by URI.

.DESCRIPTION
The Go module embeds the files of its metaschemas directory (src-go/corvus-json-schema/metaschemas). Pascal has no
embedding that Free Pascal and Delphi share, so this script turns each file into an array of bytes, exactly as the
file is, and writes the list of file names (relative, with forward slashes, in ordinal order) and a function that
gives the bytes of the file at an index.

A metaschema is never copied by hand. Run this script again when the Go module's files change.

.EXAMPLE
pwsh tools/gen-metaschemas.ps1
pwsh tools/gen-metaschemas.ps1 -Check
#>
[CmdletBinding()]
param(
    # Compare the include file with what would be written, and write nothing. Exit code 1 when it is stale.
    [switch] $Check
)

$ErrorActionPreference = "Stop"
$package = Split-Path -Parent $PSScriptRoot
$source = Join-Path $package ".." ".." "src-go" "corvus-json-schema" "metaschemas" | Resolve-Path
$target = Join-Path $package "src" "Corvus.JsonSchema.MetaschemaFiles.inc"

$files = @(Get-ChildItem -Path $source -Recurse -File -Filter "*.json" | ForEach-Object {
        [pscustomobject]@{
            Name = [System.IO.Path]::GetRelativePath($source, $_.FullName).Replace("\", "/")
            Path = $_.FullName
        }
    })
$names = [string[]]@($files | ForEach-Object { $_.Name })
[System.Array]::Sort($names, [System.StringComparer]::Ordinal)
$byName = @{}
foreach ($file in $files) { $byName[$file.Name] = $file.Path }

$nl = "`n"
$out = [System.Text.StringBuilder]::new()
[void] $out.Append("{ The standard metaschemas, from src-go/corvus-json-schema/metaschemas. Written by$nl")
[void] $out.Append("  tools/gen-metaschemas.ps1: do not edit. }$nl$nl")
[void] $out.Append("const$nl")
[void] $out.Append("  MetaschemaFileCount = $($names.Length);$nl$nl")
[void] $out.Append("  { The files, by their path within the metaschemas directory, in ordinal order. }$nl")
[void] $out.Append("  MetaschemaFileNames: array[0..$($names.Length - 1)] of UTF8String = ($nl")
for ($i = 0; $i -lt $names.Length; $i++) {
    $separator = if ($i -lt $names.Length - 1) { "," } else { ");" }
    [void] $out.Append("    '$($names[$i])'$separator$nl")
}

for ($i = 0; $i -lt $names.Length; $i++) {
    $bytes = [System.IO.File]::ReadAllBytes($byName[$names[$i]])
    if ($bytes.Length -eq 0) { throw "$($names[$i]) is empty" }
    [void] $out.Append("$nl  { $($names[$i]) }$nl")
    [void] $out.Append("  MetaschemaFile$($i): array[0..$($bytes.Length - 1)] of Byte = ($nl")
    $line = [System.Text.StringBuilder]::new("   ")
    for ($j = 0; $j -lt $bytes.Length; $j++) {
        $item = " $($bytes[$j])" + $(if ($j -lt $bytes.Length - 1) { "," } else { ");" })
        if ($line.Length + $item.Length -gt 120) {
            [void] $out.Append($line.ToString()).Append($nl)
            $line = [System.Text.StringBuilder]::new("   ")
        }
        [void] $line.Append($item)
    }
    [void] $out.Append($line.ToString()).Append($nl)
}

[void] $out.Append("$nl{ MetaschemaFileText is a copy of the bytes of the file at an index of ")
[void] $out.Append("MetaschemaFileNames. }$nl")
[void] $out.Append("function MetaschemaFileText(Index: Int32): TBytes;$nl")
[void] $out.Append("begin$nl")
[void] $out.Append("  Result := nil;$nl")
[void] $out.Append("  case Index of$nl")
for ($i = 0; $i -lt $names.Length; $i++) {
    [void] $out.Append("    $($i): begin$nl")
    [void] $out.Append("      SetLength(Result, SizeOf(MetaschemaFile$($i)));$nl")
    [void] $out.Append("      Move(MetaschemaFile$($i), Result[0], SizeOf(MetaschemaFile$($i)));$nl")
    [void] $out.Append("    end;$nl")
}
[void] $out.Append("  end;$nl")
[void] $out.Append("end;$nl")

$text = $out.ToString()
if ($Check) {
    $current = if (Test-Path $target) { [System.IO.File]::ReadAllText($target) } else { "" }
    if ($current -ne $text) {
        Write-Error "$target is stale: run pwsh tools/gen-metaschemas.ps1"
        exit 1
    }
    Write-Host "$target is current ($($names.Length) metaschemas)"
    exit 0
}
[System.IO.File]::WriteAllText($target, $text, [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $target ($($names.Length) metaschemas)"
