<#
.SYNOPSIS
Writes src/metaschema_files.ml: the standard metaschemas as strings, which the Metaschemas module serves by URI.

.DESCRIPTION
The Go module embeds the files of its metaschemas directory (src-go/corvus-json-schema/metaschemas). OCaml has no
embedding without a preprocessor, so this script writes each file as a quoted string literal, exactly as the file is,
in an array of pairs of the file's name (relative, with forward slashes, in ordinal order) and its text.

A metaschema is never copied by hand. Run this script again when the Go module's files change.

.EXAMPLE
pwsh tools/gen-metaschemas.ps1
pwsh tools/gen-metaschemas.ps1 -Check
#>
[CmdletBinding()]
param(
    # Compare the file with what would be written, and write nothing. Exit code 1 when it is stale.
    [switch] $Check
)

$ErrorActionPreference = "Stop"
$package = Split-Path -Parent $PSScriptRoot
$source = Join-Path $package ".." ".." "src-go" "corvus-json-schema" "metaschemas" | Resolve-Path
$target = Join-Path $package "src" "metaschema_files.ml"

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
$utf8 = [System.Text.UTF8Encoding]::new($false, $true)
$out = [System.Text.StringBuilder]::new()
[void] $out.Append("(* The standard metaschemas, from src-go/corvus-json-schema/metaschemas. Written by$nl")
[void] $out.Append("   tools/gen-metaschemas.ps1: do not edit. *)$nl$nl")
[void] $out.Append("[@@@ocamlformat `"disable`"]$nl$nl")
[void] $out.Append("(** The files, by their path within the metaschemas directory, in ordinal order, each with its text. *)$nl")
[void] $out.Append("let files =$nl  [|$nl")
foreach ($name in $names) {
    $text = $utf8.GetString([System.IO.File]::ReadAllBytes($byName[$name]))
    if ($text.Length -eq 0) { throw "$name is empty" }
    if ($text.Contains("|metaschema}")) { throw "$name contains the delimiter of the quoted string" }
    if ($text.Contains("`r")) { throw "$name has a carriage return: the files are written with line feeds alone" }
    [void] $out.Append("    (`"$name`", {metaschema|$text|metaschema});$nl")
}
[void] $out.Append("  |]$nl")

$result = $out.ToString()
if ($Check) {
    $current = if (Test-Path $target) { [System.IO.File]::ReadAllText($target) } else { "" }
    if ($current -ne $result) {
        Write-Error "$target is stale: run pwsh tools/gen-metaschemas.ps1"
        exit 1
    }
    Write-Host "$target is current ($($names.Length) metaschemas)"
    exit 0
}
[System.IO.File]::WriteAllText($target, $result, [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $target ($($names.Length) metaschemas)"
