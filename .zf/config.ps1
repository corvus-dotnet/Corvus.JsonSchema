<#
This example demonstrates a software build process using the 'ZeroFailed.Build.DotNet' extension
to provide the features needed when building a .NET solutions.
#>

$zerofailedExtensions = @(
    @{
        # References the extension from its GitHub repository. If not already installed, use latest version from 'main' will be downloaded.
        Name = "ZeroFailed.Build.DotNet"
        GitRepository = "https://github.com/zerofailed/ZeroFailed.Build.DotNet"
        GitRef = "main"
    }
    @{
        # References the extension from its GitHub repository. If not already installed, use latest version from 'main' will be downloaded.
        Name = "ZeroFailed.Build.GitHub"
        GitRepository = "https://github.com/zerofailed/ZeroFailed.Build.GitHub"
        GitRef = "main"
    }
)

# Load the tasks and process
. ZeroFailed.tasks -ZfPath $here/.zf

#
# Build process configuration
#
#
# Build process control options
#
$SkipInit = $false
$SkipVersion = $false
$SkipBuild = $false
$CleanBuild = $Clean
$SkipTest = $false
$SkipTestReport = $false
$SkipAnalysis = $false
$SkipPackage = $false
$SkipPublish = $false

$SolutionToBuild = (Resolve-Path (Join-Path $here ".\Corvus.Text.Json.slnx")).Path
$ProjectsToPublish = @()
# npm package(s) the PublishNpmPackages task pushes to npmjs.org on release-tag builds (see PostPublish).
# Both are scoped to the @endjin org (publishConfig.access: public); the task versions each to the release tag.
$NpmPackagesToPublish = @("packages/corvus-json-runtime", "packages/corvus-json-client-runtime")
$NugetPublishSource = property ZF_NUGET_PUBLISH_SOURCE "$here/_local-nuget-feed"
$IncludeAssembliesInCodeCoverage = @()
$ExcludeAssembliesInCodeCoverage = @()
$ExcludeFilesInCodeCoverage = @('*.g.cs')

# Pass the dotnet-coverage settings file to control which assemblies are instrumented.
# This filters coverage collection at instrumentation time (not just at report time).
$DotNetCoverageSettingsFile = (Resolve-Path (Join-Path $here "dotnet-coverage.settings.xml")).Path

# When running GHA honour the TFM that the matrix build passes via an environment variable
$TargetFrameworkMoniker = property BUILDVAR_TargetFrameworkMoniker ''

# Exclude 'outerloop' (memory stress tests), 'failing' (known failures), and
# 'integration' (Docker-dependent integration tests) categories.
# NOTE: MSTest uses 'TestCategory' as the trait name (xUnit used 'category').
# NOTE: '--ignore-exit-code 8' suppresses MTP exit code 8 ("zero tests ran") which
# occurs for CodeGenerator.Tests on net481 (empty assembly — CLI tool is net10.0 only).
# NOTE: The '&' character in the filter expression can be misinterpreted by
# process-spawning layers (e.g. dotnet-coverage launching dotnet test).
# Integration tests run in a separate CI job with Docker available.
$AdditionalTestArgs = @(
    "--filter", 'TestCategory!=outerloop&TestCategory!=failing&TestCategory!=integration'
    "--ignore-exit-code", "8"
)
$StripOutputFromLargeTrxFiles = $true
$TruncateOversizedCoverageReport = $true
$UseGitHubFlavour = $true

# When running in GHA create a GitHub Release when running a release build
$CreateGitHubRelease = $env:GITHUB_ACTIONS ? $true : $false
$PublishNuGetPackagesAsGitHubReleaseArtefacts = $true

# Allow build script parameters to be overridden via environment variables
$BuildWebsite = [Convert]::ToBoolean((property BUILDVAR_BuildWebsite $Website.ToBool()))
$IsPreviewDeployment = [Convert]::ToBoolean((property BUILDVAR_IsPreviewDeployment $false))
$BasePathPrefix = property BUILDVAR_BasePathPrefix $BasePathPrefix
$VellumDownloadToken = property VELLUM_DOWNLOAD_TOKEN ''

task . FullBuild

#
# Build Process Extensibility Points - uncomment and implement as required
#

# task RunFirst {}
# task PreInit {}
# task PostInit {}
# task PreVersion {}
# task PostVersion {}
task PreBuild {
    Write-Host "Initialising submodule"
    exec { & git submodule init }
    exec { & git submodule update }

    Write-Host "Checking documentation code sample catalog is up to date"
    exec { & pwsh -File (Join-Path $here "docs\update-code-sample-catalog.ps1") -Check }
}
task PostBuild BuildWebSiteLocal
task PreTest {
    # Turn down logging when running Specs to suppress ReqnRoll Given/When/Then output
    $script:LogLevelBackup = $LogLevel
    $script:LogLevel = "quiet"
}
task PostTest {
    # Revert logging level
    $script:LogLevel = $LogLevelBackup
}
# task PreTestReport {}
# task PostTestReport {}
# task PreAnalysis {}
# task PostAnalysis {}
# task PrePackage {}
# task PostPackage {}
# task PrePublish {}
task PostPublish PublishNpmPackages
# task RunLast {}

# Custom tasks

# Synopsis: Standalone task to build the documentation static web app.
task BuildWebsite {
    $websiteDir = Join-Path $here "docs\website"

    $websiteBuildArgs = @{ SkipDotNetBuild = $true }

    if ($VellumDownloadToken) {
        $websiteBuildArgs += @{ VellumDownloadToken = (ConvertTo-SecureString $VellumDownloadToken -AsPlainText) }
    }

    $basePathPrefix = $env:BUILDVAR_BasePathPrefix
    if ($basePathPrefix) {
        $websiteBuildArgs += @{ BasePathPrefix = $basePathPrefix }
    }

    if ($env:BUILDVAR_IsPreviewDeployment -ieq "true") {
        $websiteBuildArgs += @{ IsPreviewDeployment = $true }
    }

    Write-Host "Building documentation website..."
    Write-Host "  BasePathPrefix: $basePathPrefix"
    Write-Host "  IsPreviewDeployment: $($env:BUILDVAR_IsPreviewDeployment)"
    Write-Host "  Args: $websiteBuildArgs"

    & (Join-Path $websiteDir "build.ps1") @websiteBuildArgs
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
# Synopsis: Wrapper task to enable running the standalone BuildWebsite task for local builds
task BuildWebSiteLocal -If { $BuildWebsite } BuildWebsite

# Synopsis: Publishes the npm runtime package(s) to npmjs.org. Mirrors the NuGet publish gate -- runs only
# from a release-tag build (refs/tags/X.Y.Z), versions each package to that tag, is idempotent (skips a
# version already on the registry), and authenticates via NPM_AUTH_TOKEN (wired from the NPM_TOKEN repo
# secret in build.yml). Uses the repo's direct '& npm' invocation pattern (as the docs-website build does).
task PublishNpmPackages -If { $NpmPackagesToPublish -and ($env:GITHUB_REF -like 'refs/tags/*') } {
    if (-not $env:NPM_AUTH_TOKEN) { throw "NPM_AUTH_TOKEN is required to publish npm packages on a release build." }
    $version = $env:GITHUB_REF_NAME
    $env:NODE_AUTH_TOKEN = $env:NPM_AUTH_TOKEN
    try {
        foreach ($relativePath in $NpmPackagesToPublish) {
            $packageDir = (Resolve-Path (Join-Path $here $relativePath)).Path
            $packageName = (Get-Content (Join-Path $packageDir "package.json") -Raw | ConvertFrom-Json).name
            Write-Build White "Publishing $packageName@$version (from $relativePath)"
            Push-Location $packageDir
            try {
                # npm reads the token from $env:NODE_AUTH_TOKEN at publish time; it is never written to disk.
                '//registry.npmjs.org/:_authToken=${NODE_AUTH_TOKEN}' | Set-Content -Path ".npmrc" -NoNewline
                if ((& npm view "$packageName@$version" version 2>$null) -eq $version) {
                    Write-Build Yellow "  $packageName@$version is already on npm - skipping."
                }
                else {
                    exec { & npm version $version --no-git-tag-version --allow-same-version | Out-Null }
                    exec { & npm ci --no-audit --no-fund }
                    exec { & npm run build }
                    exec { & npm test }
                    exec { & npm publish --provenance }
                }
            }
            finally {
                Remove-Item ".npmrc" -ErrorAction SilentlyContinue
                Pop-Location
            }
        }
    }
    finally {
        $env:NODE_AUTH_TOKEN = $null
    }
}