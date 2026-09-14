<#
.SYNOPSIS
    Regenerate-and-diff gate for the V5 code generator. It proves that a generator change leaves the generated output
    byte-identical.

.DESCRIPTION
    Run 'snapshot' with the generator BEFORE the change. It regenerates everything and keeps the output.
    Run 'check' with the generator AFTER the change. It regenerates again and compares the output with the snapshot.

    What is regenerated:
    - with the CLI (dotnet src/Corvus.Json.Cli/bin/<Configuration>/net10.0/Corvus.Json.Cli.dll jsonschema ...):
      src/Corvus.Text.Json.AsyncApi30/AsyncApi30.json, src/Corvus.Text.Json.OpenApi31/OpenApi31.json and the
      tests/Corvus.Text.Json.Tests.MigrationSchemas/*.json models (the recipes from the AsyncApi30 README and
      docs/RunningTests.md);
    - with the source generator: the obj/<Configuration>/net10.0/generated output of the in-repo consumers listed in
      $Consumers (they set EmitCompilerGeneratedFiles), by building each project. Each consumer's
      obj/<Configuration>/net10.0 folder is cleared first. Roslyn never deletes emitted files, so an emitted-files folder
      can keep files from an older generator, and without its intermediate assembly the build cannot skip compilation.
    The CLI, the generator and the consumers are built unless -NoBuild is given.

    Known differences that do not fail the check:
    - A difference confined to a CorvusJsonSchemaProgram file is reported, but it does not fail the check. Generators
      before the schema registry's virtual resources keyed rebased islands and synthetic $ref roots under
      Guid.NewGuid() (corvus-schema:///<guid>/Schema in the program), so the program differed between two builds of the
      same generator, and it differs between a snapshot taken with such a generator and a check with a later one
      (whose locations are deterministic, 00000001.virtual/Schema). None of the corpora below rebase an island, which
      would also change that island's SchemaDocument constants.
    - The committed src/Corvus.Text.Json.AsyncApi30/Generated and tests/Corvus.Text.Json.Tests.MigrationModels.V5 files
      are regenerated on release and may lag the generator. The check therefore compares generator-before with
      generator-after, and only reports how far the committed files are from fresh output.

    Take the snapshot and the check in the same worktree. The generator's raw string literals (the emitted
    JsonSchemaTypeGeneratorAttribute, for one) take the line endings of its source files on disk, so a worktree whose .cs
    files are not checked out with CRLF (.gitattributes) builds a generator that emits different line endings.
    Use a work directory path of the same length for the snapshot and the check. The CLI shortens long file names to
    fit the output path, so a longer path produces "Only in" differences that are not generator changes.

.PARAMETER Mode
    snapshot (with the generator before the change) or check (with the generator after the change).

.PARAMETER NoBuild
    Use the CLI and the consumers' generated output as they are, without building.

.PARAMETER Configuration
    The build configuration. Defaults to $env:CONFIGURATION, or Debug.

.PARAMETER SnapshotPath
    Where the snapshot is kept. Defaults to $env:CORVUS_REGEN_SNAPSHOT, or corvus-regenerate-and-diff in the
    temporary directory.

.PARAMETER WorkPath
    Where the CLI output and the build logs are written. Defaults to corvus-regenerate-and-diff-work under $env:TMPDIR,
    or under the temporary directory.

.PARAMETER MSBuildArgs
    Extra MSBuild switches for every build, separated by spaces. Defaults to $env:MSBUILD_ARGS.
    When the script runs through 'pwsh -File' (as 'pwsh ./regenerate-and-diff.ps1' does), a separate value that starts
    with '-' is read as a parameter name, so pass the switches with the colon form ('-MSBuildArgs:-m:4 -nr:false') or
    in MSBUILD_ARGS. A call from a PowerShell prompt ('./regenerate-and-diff.ps1 ... -MSBuildArgs "-m:4"') takes the
    value as it is.

.EXAMPLE
    ./regenerate-and-diff.ps1 snapshot -MSBuildArgs '-m:4 -nr:false -p:UseSharedCompilation=false'
    # From a PowerShell prompt, with the generator before the change.

.EXAMPLE
    pwsh ./regenerate-and-diff.ps1 check '-MSBuildArgs:-m:4 -nr:false -p:UseSharedCompilation=false'
    # From another shell, with the generator after the change. Prints REGENERATE_AND_DIFF_OK (check) when the output is
    # unchanged.
#>

param(
    [Parameter(Position = 0)]
    [ValidateSet('snapshot', 'check')]
    [string]$Mode = 'check',
    [switch]$NoBuild,
    [string]$Configuration = $(if ($env:CONFIGURATION) { $env:CONFIGURATION } else { 'Debug' }),
    [string]$SnapshotPath = $(if ($env:CORVUS_REGEN_SNAPSHOT) { $env:CORVUS_REGEN_SNAPSHOT } else { Join-Path ([System.IO.Path]::GetTempPath()) 'corvus-regenerate-and-diff' }),
    [string]$WorkPath = $(Join-Path $(if ($env:TMPDIR) { $env:TMPDIR } else { [System.IO.Path]::GetTempPath() }) 'corvus-regenerate-and-diff-work'),
    [string]$MSBuildArgs = $env:MSBUILD_ARGS
)

$ErrorActionPreference = 'Stop'
$env:MSBUILDDISABLENODEREUSE = '1'

$Root = $PSScriptRoot
$Cli = Join-Path $Root "src/Corvus.Json.Cli/bin/$Configuration/net10.0/Corvus.Json.Cli.dll"
$MSBuildSwitches = @(if ($MSBuildArgs) { $MSBuildArgs -split '\s+' | Where-Object { $_ } })
$Consumers = @(
    'src/Corvus.Text.Json.OpenApi20'
    'src/Corvus.Text.Json.OpenApi30'
    'src/Corvus.Text.Json.OpenApi31'
    'src/Corvus.Text.Json.OpenApi32'
    'src/Corvus.Text.Json.Patch'
    'tests/Corvus.Text.Json.Tests.GeneratedModels'
    'tests/Corvus.Text.Json.Tests.GeneratedModels.NativeEnums'
    'tests/Corvus.Text.Json.Tests.GeneratedModels.NativeEnums.Disabled'
    'tests/Corvus.Text.Json.Tests.GeneratedModels.NullOrUndefinedExceptNonNullDefaulted'
    'tests/Corvus.Text.Json.Tests.GeneratedModels.OptionalAsNullable'
)
$MigrationSchemas = [ordered]@{
    'person' = 'MigrationPerson'
    'nested' = 'MigrationNested'
    'composite' = 'MigrationComposite'
    'item-array' = 'MigrationItemArray'
    'int-vector' = 'MigrationIntVector'
    'status-enum' = 'MigrationStatusEnum'
    'tuple' = 'MigrationTuple'
    'union' = 'MigrationUnion'
    'pattern-union' = 'MigrationPatternUnion'
    'with-defaults' = 'MigrationWithDefaults'
}
$script:Failed = $false

function Invoke-Build([string]$Project) {
    Write-Host "--- build $Project $(Get-Date -Format HH:mm:ss)"
    $log = Join-Path $WorkPath "build-$(Split-Path $Project -Leaf).log"
    & dotnet build (Join-Path $Root $Project) -c $Configuration @MSBuildSwitches *> $log
    if ($LASTEXITCODE -ne 0) {
        Write-Host "BUILD FAILED: $Project"
        Select-String -LiteralPath $log -Pattern ' error ' |
            ForEach-Object { $_.Line.Trim() } |
            Sort-Object -Unique |
            Select-Object -First 5 |
            ForEach-Object { Write-Host $_ }
        $script:Failed = $true
    }
}

function Invoke-Cli([string]$Schema, [string]$RootNamespace, [string]$RootTypeName, [string]$Label) {
    $log = Join-Path $WorkPath "cli-$Label.log"
    $outputPath = Join-Path $WorkPath "cli/$Label"
    & dotnet $Cli jsonschema (Join-Path $Root $Schema) --rootNamespace $RootNamespace --outputRootTypeName $RootTypeName --outputPath $outputPath *>> $log
    if ($LASTEXITCODE -ne 0) {
        Write-Host "CLI FAILED: $Schema"
        Get-Content -LiteralPath $log -Tail 3 | ForEach-Object { Write-Host $_ }
        $script:Failed = $true
    }
}

function Get-RelativeFiles([string]$Directory) {
    $set = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($file in Get-ChildItem -LiteralPath $Directory -Recurse -File -Force) {
        [void]$set.Add([System.IO.Path]::GetRelativePath($Directory, $file.FullName))
    }

    return , $set
}

function Test-SameContent([string]$Left, [string]$Right) {
    if ((Get-Item -LiteralPath $Left).Length -ne (Get-Item -LiteralPath $Right).Length) {
        return $false
    }

    [byte[]]$leftBytes = [System.IO.File]::ReadAllBytes($Left)
    [byte[]]$rightBytes = [System.IO.File]::ReadAllBytes($Right)
    return [System.Linq.Enumerable]::SequenceEqual($leftBytes, $rightBytes)
}

# The differences between two directory trees, one line per file, in the form diff -rq reports them.
function Get-DirectoryDifferences([string]$Expected, [string]$Actual) {
    $differences = [System.Collections.Generic.List[string]]::new()
    foreach ($directory in @($Expected, $Actual)) {
        if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
            $differences.Add("Missing directory: $directory")
        }
    }

    if ($differences.Count -gt 0) {
        return , $differences
    }

    $expectedFiles = Get-RelativeFiles $Expected
    $actualFiles = Get-RelativeFiles $Actual
    foreach ($relative in ($expectedFiles | Sort-Object)) {
        if (-not $actualFiles.Contains($relative)) {
            $differences.Add("Only in ${Expected}: $relative")
        }
        elseif (-not (Test-SameContent (Join-Path $Expected $relative) (Join-Path $Actual $relative))) {
            $differences.Add("Files $(Join-Path $Expected $relative) and $(Join-Path $Actual $relative) differ")
        }
    }

    foreach ($relative in ($actualFiles | Sort-Object)) {
        if (-not $expectedFiles.Contains($relative)) {
            $differences.Add("Only in ${Actual}: $relative")
        }
    }

    return , $differences
}

function Compare-Output([string]$SnapshotDirectory, [string]$FreshDirectory, [string]$Label) {
    $differences = Get-DirectoryDifferences $SnapshotDirectory $FreshDirectory
    $programDifferences = @($differences | Where-Object { $_.Contains('CorvusJsonSchemaProgram') })
    $otherDifferences = @($differences | Where-Object { -not $_.Contains('CorvusJsonSchemaProgram') })
    if ($otherDifferences.Count -eq 0) {
        $fileCount = @(Get-ChildItem -LiteralPath $FreshDirectory -Recurse -File -Force).Count
        $note = if ($programDifferences.Count -gt 0) { '; a program file differs, which does not fail the check (synthetic root locations: see the help)' } else { '' }
        Write-Host "${Label}: identical ($fileCount files$note)"
    }
    else {
        Write-Host "${Label}: DIFFERENT"
        $otherDifferences | Select-Object -First 10 | ForEach-Object { Write-Host $_ }
        $script:Failed = $true
    }
}

if (Test-Path -LiteralPath $WorkPath) {
    Remove-Item -LiteralPath $WorkPath -Recurse -Force
}

foreach ($label in @('asyncapi30', 'openapi31', 'migration')) {
    New-Item -ItemType Directory -Path (Join-Path $WorkPath "cli/$label") -Force | Out-Null
}

if (-not $NoBuild) {
    Invoke-Build 'src/Corvus.Json.Cli/Corvus.Json.Cli.csproj'
}

Write-Host "--- CLI regeneration $(Get-Date -Format HH:mm:ss)"
Invoke-Cli 'src/Corvus.Text.Json.AsyncApi30/AsyncApi30.json' 'Corvus.Text.Json.AsyncApi30' 'AsyncApiDocument' 'asyncapi30'
Invoke-Cli 'src/Corvus.Text.Json.OpenApi31/OpenApi31.json' 'Corvus.Text.Json.OpenApi31' 'OpenApiDocument' 'openapi31'
foreach ($entry in $MigrationSchemas.GetEnumerator()) {
    Invoke-Cli "tests/Corvus.Text.Json.Tests.MigrationSchemas/migration-$($entry.Key).json" 'Corvus.Text.Json.Tests.MigrationModels.V5' $entry.Value 'migration'
}

if (-not $NoBuild) {
    foreach ($consumer in $Consumers) {
        $intermediate = Join-Path $Root "$consumer/obj/$Configuration/net10.0"
        if (Test-Path -LiteralPath $intermediate) {
            Remove-Item -LiteralPath $intermediate -Recurse -Force
        }

        Invoke-Build $consumer
    }
}

switch ($Mode) {
    'snapshot' {
        if (Test-Path -LiteralPath $SnapshotPath) {
            Remove-Item -LiteralPath $SnapshotPath -Recurse -Force
        }

        New-Item -ItemType Directory -Path (Join-Path $SnapshotPath 'obj') -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $WorkPath 'cli') -Destination (Join-Path $SnapshotPath 'cli') -Recurse
        foreach ($consumer in $Consumers) {
            $generated = Join-Path $Root "$consumer/obj/$Configuration/net10.0/generated"
            if (Test-Path -LiteralPath $generated -PathType Container) {
                Copy-Item -LiteralPath $generated -Destination (Join-Path $SnapshotPath "obj/$(Split-Path $consumer -Leaf)") -Recurse
            }
            else {
                Write-Host "no generated output for $consumer (build it first)"
                $script:Failed = $true
            }
        }

        Write-Host "snapshot: $(@(Get-ChildItem -LiteralPath $SnapshotPath -Recurse -File -Force).Count) files in $SnapshotPath"
    }

    'check' {
        if (-not (Test-Path -LiteralPath (Join-Path $SnapshotPath 'cli') -PathType Container)) {
            Write-Host "no snapshot in ${SnapshotPath}: run '$PSCommandPath snapshot' with the generator before the change"
            exit 2
        }

        foreach ($label in @('asyncapi30', 'openapi31', 'migration')) {
            Compare-Output (Join-Path $SnapshotPath "cli/$label") (Join-Path $WorkPath "cli/$label") "CLI $label"
        }

        foreach ($consumer in $Consumers) {
            Compare-Output (Join-Path $SnapshotPath "obj/$(Split-Path $consumer -Leaf)") (Join-Path $Root "$consumer/obj/$Configuration/net10.0/generated") (Split-Path $consumer -Leaf)
        }
    }
}

$committedDifferences = Get-DirectoryDifferences (Join-Path $Root 'src/Corvus.Text.Json.AsyncApi30/Generated') (Join-Path $WorkPath 'cli/asyncapi30')
Write-Host "committed AsyncApi30/Generated vs fresh CLI output (informational): $($committedDifferences.Count) files differ"

if ($script:Failed) {
    Write-Host "REGENERATE_AND_DIFF_FAILED ($Mode)"
    exit 1
}

Write-Host "REGENERATE_AND_DIFF_OK ($Mode)"
