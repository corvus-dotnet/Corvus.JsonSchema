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
$SkipPublish = $true

$SolutionToBuild = (Resolve-Path (Join-Path $here ".\Corvus.Text.Json.slnx")).Path
$ProjectsToPublish = @()
$NugetPublishSource = property ZF_NUGET_PUBLISH_SOURCE "$here/_local-nuget-feed"
$IncludeAssembliesInCodeCoverage = @(
    'Corvus.Json.CodeGeneration'
    'Corvus.Json.CodeGeneration.4'
    'Corvus.Json.CodeGeneration.6'
    'Corvus.Json.CodeGeneration.7'
    'Corvus.Json.CodeGeneration.201909'
    'Corvus.Json.CodeGeneration.202012'
    'Corvus.Json.CodeGeneration.CorvusVocabulary'
    'Corvus.Json.CodeGeneration.CSharp'
    'Corvus.Json.CodeGeneration.CSharp.QuickStart'
    'Corvus.Json.CodeGeneration.HttpClientDocumentResolver'
    'Corvus.Json.CodeGeneration.OpenApi30'
    'Corvus.Json.CodeGeneration.OpenApi31'
    'Corvus.Json.CodeGeneration.YamlPreProcessor'
    'Corvus.Json.ExtendedTypes'
    'Corvus.Json.JsonReference'
    'Corvus.Json.JsonSchema.Draft4'
    'Corvus.Json.JsonSchema.Draft6'
    'Corvus.Json.JsonSchema.Draft7'
    'Corvus.Json.JsonSchema.Draft201909'
    'Corvus.Json.JsonSchema.Draft202012'
    'Corvus.Json.JsonSchema.OpenApi30'
    'Corvus.Json.JsonSchema.OpenApi31'
    'Corvus.Json.Patch'
    'Corvus.Json.SourceGenerator'
    'Corvus.Json.SourceGeneratorTools'
    'Corvus.Json.SourceGeneratorTools.ForLanguageProvider'
    'Corvus.Json.Validator'
)
$ExcludeAssembliesInCodeCoverage = @()
$ExcludeFilesInCodeCoverage = @('*.g.cs')

# When running GHA honour the TFM that the matrix build passes via an environment variable
$TargetFrameworkMoniker = property BUILDVAR_TargetFrameworkMoniker ''

# Exclude 'outerloop' (memory stress tests) and 'failing' (known failures) categories
# which are too resource-intensive for CI runners.
# NOTE: MSTest uses 'TestCategory' as the trait name (xUnit used 'category').
# NOTE: '-m:1' and '-p:' are MSBuild-style args that are incompatible with the
# Microsoft Testing Platform (MTP) used by MSTest. Removed to fix CI test execution.
$AdditionalTestArgs = @(
    "--filter", 'TestCategory!=failing&TestCategory!=outerloop'
)
$StripOutputFromLargeTrxFiles = $true
$TruncateOversizedCoverageReport = $true
$UseGitHubFlavour = $true

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

    Write-Host "Checking documentation code sample catalog is up to date"
    exec { & pwsh -File (Join-Path $here "docs\update-code-sample-catalog.ps1") -Check }
}
# Synopsis: Optionally builds the documentation website
task PostBuild BuildWebsite
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
