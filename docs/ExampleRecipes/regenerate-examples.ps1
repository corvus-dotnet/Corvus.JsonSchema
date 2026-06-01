<#
.SYNOPSIS
    Regenerates all CLI-generated code in the ExampleRecipes projects.

.DESCRIPTION
    This script regenerates the Generated/ directories for all OpenAPI and AsyncAPI
    ExampleRecipes projects. Run this after making changes to the code generators
    (OpenApi30/31/32CodeGenerator, AsyncApi30CodeGenerator, etc.) to ensure the
    examples stay in sync.

    Prerequisites — build the CLI tool first:
        dotnet build src\Corvus.Json.Cli -f net10.0 -c Release

.PARAMETER Force
    Always regenerate, even if the lock file indicates no changes.

.PARAMETER Project
    Regenerate only the specified project(s) by number prefix (e.g., "032", "031,032").
    If not specified, regenerates all projects.

.EXAMPLE
    .\regenerate-examples.ps1
    # Regenerates all OpenAPI and AsyncAPI example projects

.EXAMPLE
    .\regenerate-examples.ps1 -Project 032
    # Regenerates only 032-OpenApiAdvancedServer

.EXAMPLE
    .\regenerate-examples.ps1 -Force
    # Forces regeneration even if specs haven't changed
#>

param(
    [switch]$Force,
    [string]$Project
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path "$PSScriptRoot\..\.."
$recipesDir = $PSScriptRoot
$cliProject = Join-Path $repoRoot 'src\Corvus.Json.Cli'

function Invoke-CliTool {
    param(
        [string]$Command,
        [string]$SpecFile,
        [string]$RootNamespace,
        [string]$OutputPath,
        [string]$WorkingDir
    )

    $toolArgs = @($Command, $SpecFile, "--rootNamespace", $RootNamespace, "--outputPath", $OutputPath)
    if ($Force) { $toolArgs += "--force" }

    Write-Host "  corvusjson $Command $SpecFile --rootNamespace $RootNamespace --outputPath $OutputPath$(if ($Force) {' --force'})" -ForegroundColor DarkGray

    Push-Location $WorkingDir
    try {
        & dotnet run --project $cliProject -f net10.0 -c Release --no-build -- @toolArgs
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Code generation failed for $WorkingDir"
            exit 1
        }
    }
    finally {
        Pop-Location
    }
}

# Define all generation targets
$targets = @(
    @{
        Number = "029"
        Name = "029-OpenApiClient"
        Commands = @(
            @{ Command = "openapi-client"; Spec = "petstore.json"; Namespace = "Petstore.Client"; Output = "Generated" }
        )
    },
    @{
        Number = "030"
        Name = "030-OpenApiServer"
        Commands = @(
            @{ Command = "openapi-server"; Spec = "petstore.json"; Namespace = "Petstore.Server"; Output = "Generated" }
        )
    },
    @{
        Number = "031"
        Name = "031-OpenApiAdvancedClient"
        Commands = @(
            @{ Command = "openapi-client"; Spec = "petstore-extended.json"; Namespace = "Petstore.Extended"; Output = "Generated" }
        )
    },
    @{
        Number = "032"
        Name = "032-OpenApiAdvancedServer"
        Commands = @(
            @{ Command = "openapi-server"; Spec = "petstore-extended.json"; Namespace = "Petstore.Extended.Server"; Output = "Generated" }
        )
    },
    @{
        Number = "033"
        Name = "033-OpenApiEndToEnd"
        Commands = @(
            @{ Command = "openapi-client"; Spec = "petstore-extended.json"; Namespace = "Petstore.EndToEnd.Client"; Output = "Generated/Client" },
            @{ Command = "openapi-server"; Spec = "petstore-extended.json"; Namespace = "Petstore.EndToEnd.Server"; Output = "Generated/Server" }
        )
    },
    @{
        Number = "034"
        Name = "034-OpenApiCallbackServer"
        Commands = @(
            @{ Command = "openapi-callback-server"; Spec = "event-api.json"; Namespace = "EventSubscription.CallbackServer"; Output = "Generated" }
        )
    },
    @{
        Number = "035"
        Name = "035-OpenApiCallbackClient"
        Commands = @(
            @{ Command = "openapi-callback-client"; Spec = "event-api.json"; Namespace = "EventSubscription.CallbackClient"; Output = "Generated" }
        )
    },
    @{
        Number = "036"
        Name = "036-AsyncApiProducer"
        Commands = @(
            @{ Command = "asyncapi-generate"; Spec = "streetlights.json"; Namespace = "Streetlights.Client"; Output = "Generated" }
        )
    },
    @{
        Number = "037"
        Name = "037-AsyncApiConsumer"
        Commands = @(
            @{ Command = "asyncapi-generate"; Spec = "streetlights.json"; Namespace = "Streetlights.Client"; Output = "Generated" }
        )
    },
    @{
        Number = "038"
        Name = "038-AsyncApiEndToEnd"
        Commands = @(
            @{ Command = "asyncapi-generate"; Spec = "streetlights.json"; Namespace = "Streetlights.Client"; Output = "Generated" }
        )
    },
    @{
        Number = "039"
        Name = "039-AsyncApiAuthentication"
        Commands = @(
            @{ Command = "asyncapi-generate"; Spec = "streetlights.json"; Namespace = "Streetlights.Client"; Output = "Generated" }
        )
    }
)

# Filter targets if -Project specified
if ($Project) {
    $prefixes = $Project -split ',' | ForEach-Object { $_.Trim() }
    $targets = $targets | Where-Object { $prefixes -contains $_.Number }
    if ($targets.Count -eq 0) {
        Write-Error "No matching projects found for: $Project"
        exit 1
    }
}

$totalCommands = ($targets | ForEach-Object { $_.Commands.Count } | Measure-Object -Sum).Sum
$current = 0

foreach ($target in $targets) {
    $projectDir = Join-Path $recipesDir $target.Name

    if (-not (Test-Path $projectDir)) {
        Write-Warning "Directory not found, skipping: $($target.Name)"
        continue
    }

    Write-Host "`n[$($target.Name)]" -ForegroundColor Green

    foreach ($cmd in $target.Commands) {
        $current++
        Write-Host "  ($current/$totalCommands) Generating $($cmd.Namespace)..." -ForegroundColor White

        Invoke-CliTool `
            -Command $cmd.Command `
            -SpecFile $cmd.Spec `
            -RootNamespace $cmd.Namespace `
            -OutputPath $cmd.Output `
            -WorkingDir $projectDir
    }
}

Write-Host "`n$([char]0x2713) All $totalCommands generation steps completed successfully." -ForegroundColor Green