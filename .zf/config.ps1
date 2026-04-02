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

# Collect code coverage only for the core library assemblies.
# $IncludeFilesInCodeCoverage = "Corvus.Json.CodeGeneration.dll;Corvus.Json.CodeGeneration.CSharp.dll;Corvus.Json.ExtendedTypes.dll;Corvus.Json.JsonReference.dll;Corvus.Text.Json.dll;Corvus.Text.Json.Validator.dll;Corvus.Text.Json.CodeGeneration.dll"

# When running in GHA create a GitHub Release when running a release build
$CreateGitHubRelease = $env:GITHUB_ACTIONS ? $true : $false
$PublishNuGetPackagesAsGitHubReleaseArtefacts = $true

# Allow build script parameters to be overridden via environment variables
$BuildWebsite = [Convert]::ToBoolean((property BUILDVAR_BuildWebsite $Website))
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
task PostTest ShrinkV4SpecsTrxFile,TruncateOversizedCoverageReport,{
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
task ShrinkV4SpecsTrxFile {
    # The V4 Specs TRX file (~105 MB, 19K tests) exceeds lxml's text-node size limit
    # in the publish-unit-test-result-action (xmlSAX2Characters error).
    # Strip per-test <Output> elements (captured Reqnroll step text) to shrink the
    # file while keeping pass/fail results visible in the CI test report.
    $v4Path = Join-Path $SourcesDir "src-v4"
    Get-ChildItem -Path $v4Path -Filter "test-results_*.trx" -Recurse -ErrorAction SilentlyContinue |
        ForEach-Object {
            $sizeMB = [math]::Round($_.Length / 1MB, 1)
            Write-Build Yellow "PostTest: stripping test output from $($_.FullName) ($sizeMB MB)"
            try {
                $content = [System.IO.File]::ReadAllText($_.FullName)
                $content = [regex]::Replace(
                    $content,
                    '<Output>\s*<StdOut>.*?</StdOut>\s*</Output>',
                    '',
                    [System.Text.RegularExpressions.RegexOptions]::Singleline)
                [System.IO.File]::WriteAllText($_.FullName, $content)
                $newSizeMB = [math]::Round((Get-Item $_.FullName).Length / 1MB, 1)
                Write-Build Yellow "  Reduced to $newSizeMB MB"
            }
            catch {
                Write-Build Yellow "  Strip failed, removing file: $_"
                Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
            }
        }
}
task TruncateOversizedCoverageReport {
    # GitHub PR comments have a 65536 character limit. The coverage summary for this
    # solution often exceeds that. Truncate and append a note when too large.
    # The SummaryGithub.md is generated inside RunTestsWithDotNetCoverage (before
    # PostTest), so it exists at this point.
    $summaryPath = Join-Path $CoverageDir "SummaryGithub.md"
    $maxChars = 60000  # leave headroom for sticky-comment wrapper
    if (Test-Path $summaryPath) {
        $content = Get-Content -Raw -Path $summaryPath
        if ($content.Length -gt $maxChars) {
            $originalLen = $content.Length
            $truncated = $content.Substring(0, $maxChars)
            # Cut at last newline to avoid splitting a table row
            $lastNl = $truncated.LastIndexOf("`n")
            if ($lastNl -gt 0) { $truncated = $truncated.Substring(0, $lastNl) }
            $truncated += "`n`n---`n> **Note:** Coverage summary truncated from $originalLen to $($truncated.Length) characters. Full report is in the build artifacts.`n"
            Set-Content -Path $summaryPath -Value $truncated -Encoding UTF8 -NoNewline
            Write-Build Yellow "PostTest: truncated $summaryPath from $originalLen to $($truncated.Length) chars"
        }
    }
}
