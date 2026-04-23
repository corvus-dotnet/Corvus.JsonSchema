<#
This example demonstrates a software build process using the 'ZeroFailed.Build.DotNet' extension
to provide the features needed when building a .NET solutions.
#>

$zerofailedExtensions = @(
    @{
        # References the extension from its GitHub repository. If not already installed, use latest version from 'main' will be downloaded.
        Name = "ZeroFailed.DevOps.Common"
        GitRepository = "https://github.com/zerofailed/ZeroFailed.DevOps.Common"
        GitRef = "feature/update-enter-exit-action-support"
    }
    @{
        # References the extension from its GitHub repository. If not already installed, use latest version from 'main' will be downloaded.
        Name = "ZeroFailed.Build.DotNet"
        GitRepository = "https://github.com/zerofailed/ZeroFailed.Build.DotNet"
        GitRef = "feature/refactor-test-report"
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
$SkipPublish = $true

$SolutionToBuild = (Resolve-Path (Join-Path $here ".\Corvus.Text.Json.slnx")).Path
$ProjectsToPublish = @()
$NugetPublishSource = property ZF_NUGET_PUBLISH_SOURCE "$here/_local-nuget-feed"
$IncludeAssembliesInCodeCoverage = "Corvus*"
$ExcludeAssembliesInCodeCoverage = "Corvus*.Tests*"

# Run test assemblies sequentially to avoid OOM on CI runners (7 GB).
# The solution has 7 test assemblies; running them all in parallel exhausts memory.
# Exclude 'outerloop' (memory stress tests) and 'failing' (known failures) categories
# which are too resource-intensive for CI runners.
# Disable SourceLink queries during test — the JSON-Schema-Test-Suite submodule isn't
# checked out in the test phase, and LocateRepository emits a warning per project.
$AdditionalTestArgs = @(
    "-m:1",
    "--filter", 'category!=failing&category!=outerloop',
    '-p:EnableSourceControlManagerQueries=false'
)
$StripOutputFromLargeTrxFiles = $true
$TruncateOversizedCoverageReport = $true

# Collect code coverage only for the core library assemblies.
# $IncludeFilesInCodeCoverage = "Corvus.Json.CodeGeneration.dll;Corvus.Json.CodeGeneration.CSharp.dll;Corvus.Json.ExtendedTypes.dll;Corvus.Json.JsonReference.dll;Corvus.Text.Json.dll;Corvus.Text.Json.Validator.dll;Corvus.Text.Json.CodeGeneration.dll"

# When running in GHA create a GitHub Release when running a release build
$CreateGitHubRelease = $env:GITHUB_ACTIONS ? $true : $false
$PublishNuGetPackagesAsGitHubReleaseArtefacts = $true

# Allow build script parameters to be overridden via environment variables
$BuildWebsite = [Convert]::ToBoolean((property BUILDVAR_BuildWebsite $Website.ToBool()))
$IsPreviewDeployment = [Convert]::ToBoolean((property BUILDVAR_IsPreviewDeployment $false))
$BasePathPrefix = property BUILDVAR_BasePathPrefix $BasePathPrefix

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
}
# Synopsis: Optionally builds the documentation website
task PostBuild BuildWebsite
task PreTest {
    # Turn down logging when running Specs to suppress ReqnRoll Given/When/Then output
    $script:LogLevelBackup = $LogLevel
    $script:LogLevel = "quiet"

    # Three test projects target net10.0 only (analyzer + codegen tests).
    # When dotnet test runs for net8.0/net481 it can't find their DLLs and returns
    # exit code 1, which aborts the InvokeBuild pipeline (skipping PostTest etc.).
    # Switch to a test-specific solution that excludes those projects.
    $script:SolutionToTestBackup = $SolutionToBuild
    if ($TargetFrameworkMoniker -ne "net10.0") {
        $testSlnx = (Resolve-Path (Join-Path $here ".\Corvus.Text.Json.Test.slnx")).Path
        $script:SolutionToBuild = $testSlnx
        Write-Build Yellow "PreTest: Using $testSlnx for $TargetFrameworkMoniker (excludes net10.0-only test projects)"
    }
}
task PostTest {
    # Revert solution and logging level
    $script:SolutionToBuild = $SolutionToTestBackup
    $script:LogLevel = $LogLevelBackup
}
# task PreTestReport {}
# task PostTestReport {}
# task PreAnalysis {}
# task PostAnalysis {}
# task PrePackage {}
# task PostPackage {}
# task PrePublish {}
# task PostPublish {}
# task RunLast {}

# Custom tasks

task BuildWebsite -If { $BuildWebsite } {
    $websiteDir = Join-Path $here "docs\website"

    Write-Information "Building documentation website..."

    # Ensure Node dependencies are installed
    Set-Location $websiteDir
    if (!(Test-Path (Join-Path $websiteDir "node_modules"))) {
        Write-Information "Installing Node dependencies..."
        exec { & npm ci --prefix $websiteDir }
    }

    $websiteBuildArgs = @("-SkipDotNetBuild")
    if ($BasePathPrefix) {
        $websiteBuildArgs += "-BasePathPrefix", $BasePathPrefix
    }
    if ($IsPreviewDeployment) {
        $websiteBuildArgs += "-IsPreviewDeployment"
    }

    exec { & pwsh -File (Join-Path $websiteDir "build.ps1") @websiteBuildArgs }
}
