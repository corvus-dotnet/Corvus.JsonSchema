<#
.SYNOPSIS
    Configures a local Bowtie checkout to build dotnet-corvus-jsonschema
    implementations against locally-built Corvus.Json packages.

.DESCRIPTION
    Patches the Bowtie dotnet-corvus-jsonschema-v4engine and
    dotnet-corvus-jsonschema-v5engine implementations so that their
    Docker builds use NuGet packages from a local feed instead of
    nuget.org.  This lets you test work-in-progress library changes
    against the JSON Schema Test Suite via Bowtie without publishing
    packages.

    The script modifies files inside each Bowtie implementation
    directory (Dockerfile, .csproj, .dockerignore) and copies packages
    and a NuGet.config into them.  These changes are for local
    development only and should not be committed.

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
    Builds local packages and configures both Bowtie implementations.

.EXAMPLE
    .\configure-bowtie-for-local-development.ps1 -BowtiePath D:\source\bowtie -Version 5.0.0-local.3
    Uses a specific package version (must match build-local-packages.ps1 -Version).

.EXAMPLE
    .\configure-bowtie-for-local-development.ps1 -BowtiePath D:\source\bowtie -Teardown
    Reverts all changes and restores both implementations to their original state.
#>
param(
    [Parameter(Mandatory)]
    [string]$BowtiePath,

    [string]$Version = "5.0.0-local.1",

    [switch]$Teardown
)

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot

# Define both implementations
$implementations = @(
    @{
        DirName         = "dotnet-corvus-jsonschema-v4engine"
        CsprojName      = "bowtie_corvus_jsonschema_v4engine.csproj"
        PackageFilter   = "Corvus.Json.*"
        RequiredPackage = "Corvus.Json.Validator"
        PatchRegex      = 'Include="Corvus\.Json\.Validator"\s+Version="[^"]*"'
        PatchReplace    = 'Include="Corvus.Json.Validator" Version="{0}"'
    },
    @{
        DirName         = "dotnet-corvus-jsonschema-v5engine"
        CsprojName      = "bowtie_corvus_jsonschema_v5engine.csproj"
        PackageFilter   = "Corvus.*"
        RequiredPackage = "Corvus.Text.Json.Validator"
        PatchRegex      = 'Include="Corvus\.Text\.Json\.Validator"\s+Version="[^"]*"'
        PatchReplace    = 'Include="Corvus.Text.Json.Validator" Version="{0}"'
    }
)

# Validate at least one implementation directory exists
$validImpls = @()
foreach ($impl in $implementations) {
    $implDir = Join-Path $BowtiePath "implementations" $impl.DirName
    if (Test-Path $implDir) {
        $validImpls += $impl
    }
    else {
        Write-Warning "Implementation not found: $implDir — skipping."
    }
}
if ($validImpls.Count -eq 0) {
    Write-Error "No Bowtie implementations found.  Check -BowtiePath."
    exit 1
}

# ---------------------------------------------------------------------------
#  Helper: process one implementation (setup or teardown)
# ---------------------------------------------------------------------------
function Invoke-TeardownImpl($impl) {
    $implDir = Join-Path $BowtiePath "implementations" $impl.DirName
    $backupDir = Join-Path $implDir ".local-dev-backup"
    $localPkgDir = Join-Path $implDir "local-packages"
    $nugetConfig = Join-Path $implDir "NuGet.config"

    Write-Host "  [$($impl.DirName)]" -ForegroundColor Yellow

    if (Test-Path $backupDir) {
        foreach ($name in @("Dockerfile", $impl.CsprojName, ".dockerignore")) {
            $backup = Join-Path $backupDir $name
            $target = Join-Path $implDir $name
            if (Test-Path $backup) {
                Copy-Item $backup -Destination $target -Force
                Write-Host "    Restored $name" -ForegroundColor Gray
            }
        }
        Remove-Item -Recurse -Force $backupDir
        Write-Host "    Removed backup directory" -ForegroundColor Gray
    }
    else {
        Write-Warning "    No backup directory found — falling back to git checkout."
        Push-Location $implDir
        try {
            git checkout -- Dockerfile $impl.CsprojName .dockerignore
            if ($LASTEXITCODE -ne 0) {
                Write-Error "git checkout failed (exit code $LASTEXITCODE)."
                exit 1
            }
        }
        finally {
            Pop-Location
        }
    }

    if (Test-Path $localPkgDir) {
        Remove-Item -Recurse -Force $localPkgDir
        Write-Host "    Removed local-packages" -ForegroundColor Gray
    }
    if (Test-Path $nugetConfig) {
        Remove-Item -Force $nugetConfig
        Write-Host "    Removed NuGet.config" -ForegroundColor Gray
    }
}

function Invoke-SetupImpl($impl, $localPackagesSource) {
    $implDir = Join-Path $BowtiePath "implementations" $impl.DirName
    $backupDir = Join-Path $implDir ".local-dev-backup"
    $localPkgDir = Join-Path $implDir "local-packages"
    $nugetConfig = Join-Path $implDir "NuGet.config"
    $dockerfile = Join-Path $implDir "Dockerfile"
    $csproj = Join-Path $implDir $impl.CsprojName
    $dockerignore = Join-Path $implDir ".dockerignore"

    Write-Host "  [$($impl.DirName)]" -ForegroundColor Yellow

    # Back up original files
    if (-not (Test-Path $backupDir)) {
        New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
        foreach ($name in @("Dockerfile", $impl.CsprojName, ".dockerignore")) {
            Copy-Item (Join-Path $implDir $name) -Destination $backupDir -Force
        }
        Write-Host "    Backed up original files" -ForegroundColor Gray
    }

    # Copy packages
    New-Item -ItemType Directory -Path $localPkgDir -Force | Out-Null
    $packages = Get-ChildItem $localPackagesSource -Filter "$($impl.PackageFilter)$Version.nupkg"
    $copied = 0
    foreach ($pkg in $packages) {
        Copy-Item $pkg.FullName -Destination $localPkgDir -Force
        $copied++
    }
    Write-Host "    Copied $copied package(s)" -ForegroundColor Gray

    # Validate required package
    $requiredPkg = Join-Path $localPkgDir "$($impl.RequiredPackage).$Version.nupkg"
    if (-not (Test-Path $requiredPkg)) {
        Write-Error "Required package $($impl.RequiredPackage).$Version.nupkg not found."
        exit 1
    }

    # Create NuGet.config
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
    Write-Host "    Created NuGet.config" -ForegroundColor Gray

    # Patch .csproj
    $csprojContent = Get-Content $csproj -Raw
    $replacement = $impl.PatchReplace -f $Version
    $patched = $csprojContent -replace $impl.PatchRegex, $replacement
    if ($patched -eq $csprojContent) {
        Write-Warning "    Could not find PackageReference to patch in .csproj."
    }
    else {
        Set-Content -Path $csproj -Value $patched -NoNewline -Encoding utf8NoBOM
        Write-Host "    Patched .csproj to version $Version" -ForegroundColor Gray
    }

    # Patch Dockerfile
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
        Write-Host "    Patched Dockerfile" -ForegroundColor Gray
    }
    else {
        Write-Host "    Dockerfile already patched." -ForegroundColor Gray
    }

    # Update .dockerignore
    $dockerignoreContent = Get-Content $dockerignore -Raw
    $additions = @()
    if ($dockerignoreContent -notmatch "!NuGet\.config") { $additions += "!NuGet.config" }
    if ($dockerignoreContent -notmatch "!local-packages") { $additions += "!local-packages/" }
    if ($additions.Count -gt 0) {
        $newContent = $dockerignoreContent.TrimEnd() + "`n" + ($additions -join "`n") + "`n"
        Set-Content -Path $dockerignore -Value $newContent -NoNewline -Encoding utf8NoBOM
        Write-Host "    Updated .dockerignore" -ForegroundColor Gray
    }

    # Build container image
    $localImageName = "localhost/$($impl.DirName)"
    $containerCmd = if (Get-Command podman -ErrorAction SilentlyContinue) { "podman" }
                   elseif (Get-Command docker -ErrorAction SilentlyContinue) { "docker" }
                   else { $null }
    if ($containerCmd) {
        Write-Host "    Building container image $localImageName ..." -ForegroundColor Gray
        & $containerCmd build --no-cache --quiet -f $dockerfile -t $localImageName $implDir
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Container image build failed for $($impl.DirName)."
            exit 1
        }
        Write-Host "    Built $localImageName ($containerCmd)" -ForegroundColor Gray
    }
    else {
        Write-Warning "    Neither podman nor docker found. Cannot build container image."
    }
}

# ---------------------------------------------------------------------------
#  Teardown
# ---------------------------------------------------------------------------
if ($Teardown) {
    Write-Host "Tearing down local development configuration..." -ForegroundColor Cyan

    foreach ($impl in $validImpls) {
        Invoke-TeardownImpl $impl
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

# 2. Configure each implementation
foreach ($impl in $validImpls) {
    Invoke-SetupImpl $impl $localPackagesSource
}

Write-Host ""
Write-Host "Setup complete. You can now test with:" -ForegroundColor Green
$bowtieExe = "`$bowtie"
foreach ($impl in $validImpls) {
    Write-Host "  & $bowtieExe suite -i localhost/$($impl.DirName) 2020-12 | & $bowtieExe summary --show failures" -ForegroundColor White
}
Write-Host ""
Write-Host "To revert:" -ForegroundColor Yellow
Write-Host "  .\configure-bowtie-for-local-development.ps1 -BowtiePath $BowtiePath -Teardown" -ForegroundColor White
