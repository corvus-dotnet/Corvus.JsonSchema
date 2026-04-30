<#
.SYNOPSIS
    Updates the JSON-Schema-Test-Suite submodule and regenerates all test files.

.DESCRIPTION
    This script:
    1. Fetches and checks out the latest commit on the submodule's default branch
    2. Deletes old generated test files (generators only create, never delete)
    3. Regenerates V5 test classes (type, evaluator, annotation)
    4. Regenerates V4 spec feature files (JsonSchema + OpenApi30)

.PARAMETER Branch
    The branch to update the submodule to. Defaults to 'main'.

.EXAMPLE
    .\update-json-schema-test-suite.ps1
    .\update-json-schema-test-suite.ps1 -Branch main
#>
param(
    [string]$Branch = "main"
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot

# 1. Update the submodule
Write-Host "=== Updating JSON-Schema-Test-Suite submodule ===" -ForegroundColor Cyan
Push-Location "$repoRoot\JSON-Schema-Test-Suite"
try {
    git fetch origin
    $current = git rev-parse HEAD
    $latest = git rev-parse "origin/$Branch"

    if ($current -eq $latest) {
        Write-Host "Submodule is already up to date at $current" -ForegroundColor Green
    }
    else {
        $count = (git --no-pager log --oneline "$current..$latest").Count
        Write-Host "Updating submodule: $count new commits"
        git checkout $latest
        Write-Host "Updated to $latest" -ForegroundColor Green
    }
}
finally {
    Pop-Location
}

# 2. Regenerate V5 test classes
Write-Host ""
Write-Host "=== Regenerating V5 test classes ===" -ForegroundColor Cyan

$v5OutputDirs = @(
    "$repoRoot\tests\Corvus.Text.Json.Tests\JsonSchemaTestSuite",
    "$repoRoot\tests\Corvus.Text.Json.Tests\StandaloneEvaluatorTestSuite",
    "$repoRoot\tests\Corvus.Text.Json.Tests\AnnotationTestSuite"
)

foreach ($dir in $v5OutputDirs) {
    if (Test-Path $dir) {
        Get-ChildItem $dir -Filter "*.cs" -Recurse | Remove-Item -Force
        Write-Host "  Cleaned $dir"
    }
}

# Build and run V5 generator from its project directory so relative paths resolve correctly
Push-Location "$repoRoot\tests\Corvus.JsonSchemaTestSuite.CodeGenerator"
try {
    Write-Host "  Building V5 generator..."
    dotnet build -v q --no-restore
    if ($LASTEXITCODE -ne 0) { throw "V5 generator build failed" }

    Push-Location "bin\Debug\net10.0"
    try {
        Write-Host "  Running V5 generator..."
        & .\Corvus.JsonSchemaTestSuite.CodeGenerator.exe > $null
        if ($LASTEXITCODE -ne 0) { throw "V5 generator failed" }
    }
    finally {
        Pop-Location
    }
}
finally {
    Pop-Location
}

foreach ($dir in $v5OutputDirs) {
    $count = (Get-ChildItem $dir -Filter "*.cs" -Recurse | Measure-Object).Count
    Write-Host "  $([System.IO.Path]::GetFileName($dir)): $count files" -ForegroundColor Green
}

# 3. Regenerate V4 spec feature files
Write-Host ""
Write-Host "=== Regenerating V4 spec feature files ===" -ForegroundColor Cyan

$v4OutputDir = "$repoRoot\src-v4\Corvus.Json.Specs\Features\JsonSchema"

# Delete old feature files
if (Test-Path $v4OutputDir) {
    Get-ChildItem $v4OutputDir -Filter "*.feature" -Recurse | Remove-Item -Force
    Write-Host "  Cleaned $v4OutputDir"
}

# Build V4 generator
Push-Location "$repoRoot\src-v4\Corvus.JsonSchema.SpecGenerator"
try {
    Write-Host "  Building V4 generator..."
    dotnet build -c Release -v q --no-restore
    if ($LASTEXITCODE -ne 0) { throw "V4 generator build failed" }
}
finally {
    Pop-Location
}

$v4GeneratorExe = "$repoRoot\src-v4\Corvus.JsonSchema.SpecGenerator\bin\Release\net8.0\Corvus.JsonSchema.SpecGenerator.exe"
$v4GeneratorDir = "$repoRoot\src-v4\Corvus.JsonSchema.SpecGenerator"

# Run with JsonSchema selector (uses JSON-Schema-Test-Suite)
Write-Host "  Running V4 generator (JsonSchema)..."
& $v4GeneratorExe "$repoRoot\JSON-Schema-Test-Suite" $v4OutputDir "$v4GeneratorDir\JsonSchemaOrgTestSuiteSelector.jsonc" > $null
if ($LASTEXITCODE -ne 0) { throw "V4 JsonSchema generator failed" }

# Run with OpenApi selector (uses OpenApi-Test-Suite)
Write-Host "  Running V4 generator (OpenApi30)..."
& $v4GeneratorExe "$repoRoot\OpenApi-Test-Suite" $v4OutputDir "$v4GeneratorDir\OpenApiTestSuiteSelector.jsonc" > $null
if ($LASTEXITCODE -ne 0) { throw "V4 OpenApi generator failed" }

$jsonSchemaCount = (Get-ChildItem $v4OutputDir -Filter "*.feature" -Recurse -Exclude "OpenApi30" |
    Where-Object { $_.DirectoryName -notlike "*OpenApi30*" } | Measure-Object).Count
$openApiCount = (Get-ChildItem "$v4OutputDir\OpenApi30" -Filter "*.feature" | Measure-Object).Count
Write-Host "  JsonSchema: $jsonSchemaCount features, OpenApi30: $openApiCount features" -ForegroundColor Green

# Summary
Write-Host ""
Write-Host "=== Done ===" -ForegroundColor Cyan
Write-Host "Don't forget to build and run the tests before committing!"
