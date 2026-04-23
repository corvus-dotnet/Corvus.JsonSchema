<#
.SYNOPSIS
    Configures a local Bowtie checkout to build dotnet-corvus-jsonschema
    against locally-built Corvus.Json packages.

.DESCRIPTION
    Patches the Bowtie dotnet-corvus-jsonschema implementation so that its
    Docker build uses NuGet packages from a local feed instead of nuget.org.
    This lets you test work-in-progress library changes against the JSON
    Schema Test Suite via Bowtie without publishing packages.

    The script modifies files inside the Bowtie implementation directory
    (Dockerfile, .csproj, .dockerignore) and copies packages and a
    NuGet.config into it.  These changes are for local development only
    and should not be committed.

    Use -Teardown to revert all changes.

.PARAMETER BowtiePath
    Path to the root of a local Bowtie repository checkout.

.PARAMETER Version
    The local package version to use. Defaults to 5.0.0-local.1.
    Must match the version used when running build-local-packages.ps1.

.PARAMETER Teardown
    Reverts all changes made by a previous setup run: restores the
    original Dockerfile, .csproj, and .dockerignore from backups, and
    deletes the copied local-packages directory, NuGet.config, and
    backup directory.

.EXAMPLE
    .\configure-bowtie-for-local-development.ps1 -BowtiePath D:\source\bowtie
    Builds local packages and configures the Bowtie implementation to use them.

.EXAMPLE
    .\configure-bowtie-for-local-development.ps1 -BowtiePath D:\source\bowtie -Version 5.0.0-local.3
    Uses a specific package version (must match build-local-packages.ps1 -Version).

.EXAMPLE
    .\configure-bowtie-for-local-development.ps1 -BowtiePath D:\source\bowtie -Teardown
    Reverts all changes and restores the implementation to its original state.
#>
param(
    [Parameter(Mandatory)]
    [string]$BowtiePath,

    [string]$Version = "5.0.0-local.1",

    [switch]$Teardown
)

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$implDir = Join-Path $BowtiePath "implementations" "dotnet-corvus-jsonschema"
$localPkgDir = Join-Path $implDir "local-packages"
$nugetConfig = Join-Path $implDir "NuGet.config"
$dockerfile = Join-Path $implDir "Dockerfile"
$csproj = Join-Path $implDir "bowtie_corvus_jsonschema.csproj"
$dockerignore = Join-Path $implDir ".dockerignore"
$backupDir = Join-Path $implDir ".local-dev-backup"

# Validate the implementation directory exists
if (-not (Test-Path $implDir)) {
    Write-Error "Bowtie implementation not found at $implDir.  Check -BowtiePath."
    exit 1
}

# ---------------------------------------------------------------------------
#  Teardown
# ---------------------------------------------------------------------------
if ($Teardown) {
    Write-Host "Tearing down local development configuration..." -ForegroundColor Cyan

    # Restore from backups (safer than git checkout — only reverts our changes)
    if (Test-Path $backupDir) {
        foreach ($name in @("Dockerfile", "bowtie_corvus_jsonschema.csproj", ".dockerignore")) {
            $backup = Join-Path $backupDir $name
            $target = Join-Path $implDir $name
            if (Test-Path $backup) {
                Copy-Item $backup -Destination $target -Force
                Write-Host "  Restored $name" -ForegroundColor Gray
            }
        }
        Remove-Item -Recurse -Force $backupDir
        Write-Host "  Removed backup directory" -ForegroundColor Gray
    }
    else {
        Write-Warning "No backup directory found at $backupDir.  Falling back to git checkout."
        Push-Location $implDir
        try {
            git checkout -- Dockerfile bowtie_corvus_jsonschema.csproj .dockerignore
            if ($LASTEXITCODE -ne 0) {
                Write-Error "git checkout failed (exit code $LASTEXITCODE)."
                exit 1
            }
        }
        finally {
            Pop-Location
        }
    }

    # Remove copied artefacts
    if (Test-Path $localPkgDir) {
        Remove-Item -Recurse -Force $localPkgDir
        Write-Host "  Removed $localPkgDir" -ForegroundColor Gray
    }
    if (Test-Path $nugetConfig) {
        Remove-Item -Force $nugetConfig
        Write-Host "  Removed $nugetConfig" -ForegroundColor Gray
    }

    Write-Host "Teardown complete." -ForegroundColor Green
    exit 0
}

# ---------------------------------------------------------------------------
#  Setup
# ---------------------------------------------------------------------------
Write-Host "Configuring Bowtie for local development..." -ForegroundColor Cyan
Write-Host "  Bowtie path: $BowtiePath" -ForegroundColor Gray
Write-Host "  Version:     $Version" -ForegroundColor Gray
Write-Host ""

# Back up original files before any modifications
if (-not (Test-Path $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    foreach ($name in @("Dockerfile", "bowtie_corvus_jsonschema.csproj", ".dockerignore")) {
        Copy-Item (Join-Path $implDir $name) -Destination $backupDir -Force
    }
    Write-Host "  Backed up original files to $backupDir" -ForegroundColor Gray
}

# 1. Build local packages if needed
$localPackagesSource = Join-Path $repoRoot "local-packages"
$existingPkgs = @(Get-ChildItem $localPackagesSource -Filter "*$Version.nupkg" -ErrorAction SilentlyContinue)
if ($existingPkgs.Count -eq 0) {
    Write-Host "Building local packages at version $Version..." -ForegroundColor Yellow
    & (Join-Path $repoRoot "build-local-packages.ps1") -Version $Version
    if ($LASTEXITCODE -ne 0) {
        Write-Error "build-local-packages.ps1 failed."
        exit 1
    }
}
else {
    Write-Host "Using existing local packages ($($existingPkgs.Count) found at version $Version)." -ForegroundColor Gray
}

# 2. Copy V4 packages into the implementation directory
#    The bowtie implementation currently uses V4 (Corvus.Json.*) packages.
New-Item -ItemType Directory -Path $localPkgDir -Force | Out-Null
$v4Packages = Get-ChildItem $localPackagesSource -Filter "Corvus.Json.*$Version.nupkg"
$copied = 0
foreach ($pkg in $v4Packages) {
    Copy-Item $pkg.FullName -Destination $localPkgDir -Force
    $copied++
}
Write-Host "  Copied $copied V4 package(s) to $localPkgDir" -ForegroundColor Gray

# Validate the required package was copied
$requiredPkg = Join-Path $localPkgDir "Corvus.Json.Validator.$Version.nupkg"
if (-not (Test-Path $requiredPkg)) {
    Write-Error "Required package Corvus.Json.Validator.$Version.nupkg was not found.  Build may have failed or version mismatch."
    exit 1
}

# 3. Create NuGet.config with local source (higher priority)
$nugetConfigContent = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="local-packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@
Set-Content -Path $nugetConfig -Value $nugetConfigContent -Encoding utf8NoBOM
Write-Host "  Created $nugetConfig" -ForegroundColor Gray

# 4. Patch .csproj to reference local package version
$csprojContent = Get-Content $csproj -Raw
$patched = $csprojContent -replace `
    'Include="Corvus\.Json\.Validator"\s+Version="[^"]*"', `
    "Include=""Corvus.Json.Validator"" Version=""$Version"""
if ($patched -eq $csprojContent) {
    Write-Warning "Could not find Corvus.Json.Validator PackageReference in .csproj to patch."
}
else {
    Set-Content -Path $csproj -Value $patched -NoNewline -Encoding utf8NoBOM
    Write-Host "  Patched .csproj to version $Version" -ForegroundColor Gray
}

# 5. Patch Dockerfile to copy NuGet.config and local-packages before restore
$dockerfileContent = Get-Content $dockerfile -Raw
if ($dockerfileContent -notmatch "COPY NuGet\.config") {
    $original = $dockerfileContent
    $dockerfileContent = $dockerfileContent -replace `
        '(COPY \*\.csproj \.)', `
        "COPY NuGet.config .`nCOPY local-packages/ local-packages/`n`$1"
    if ($dockerfileContent -eq $original) {
        Write-Error "Failed to patch Dockerfile — could not find 'COPY *.csproj .' line."
        exit 1
    }
    Set-Content -Path $dockerfile -Value $dockerfileContent -NoNewline -Encoding utf8NoBOM
    Write-Host "  Patched Dockerfile to copy local packages" -ForegroundColor Gray
}
else {
    Write-Host "  Dockerfile already patched." -ForegroundColor Gray
}

# 6. Update .dockerignore to allow NuGet.config and local-packages through
$dockerignoreContent = Get-Content $dockerignore -Raw
$additions = @()
if ($dockerignoreContent -notmatch "!NuGet\.config") {
    $additions += "!NuGet.config"
}
if ($dockerignoreContent -notmatch "!local-packages") {
    $additions += "!local-packages/"
}
if ($additions.Count -gt 0) {
    $newContent = $dockerignoreContent.TrimEnd() + "`n" + ($additions -join "`n") + "`n"
    Set-Content -Path $dockerignore -Value $newContent -NoNewline -Encoding utf8NoBOM
    Write-Host "  Updated .dockerignore" -ForegroundColor Gray
}

# 7. Build the container image locally
#    Bowtie never builds images itself — it pulls from the registry or uses
#    an existing local image.  We build and tag as localhost/... so that
#    bowtie finds it without attempting a pull.
$localImageName = "localhost/dotnet-corvus-jsonschema"
$containerCmd = if (Get-Command podman -ErrorAction SilentlyContinue) { "podman" }
               elseif (Get-Command docker -ErrorAction SilentlyContinue) { "docker" }
               else { $null }
if ($containerCmd) {
    Write-Host "  Building container image $localImageName ..." -ForegroundColor Gray
    & $containerCmd build --quiet -f $dockerfile -t $localImageName $implDir
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Container image build failed."
        exit 1
    }
    Write-Host "  Built $localImageName ($containerCmd)" -ForegroundColor Gray
}
else {
    Write-Warning "Neither podman nor docker found. Cannot build container image."
}

Write-Host ""
Write-Host "Setup complete. You can now test with:" -ForegroundColor Green
Write-Host "  cd $BowtiePath" -ForegroundColor White
Write-Host "  bowtie suite -i $localImageName 2020-12" -ForegroundColor White
Write-Host ""
Write-Host "To revert:" -ForegroundColor Yellow
Write-Host "  .\configure-bowtie-for-local-development.ps1 -BowtiePath $BowtiePath -Teardown" -ForegroundColor White
