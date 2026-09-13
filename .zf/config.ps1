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
# The native AOT profile the Corvus.Text.Json package carries (profiles/Corvus.Text.Json.mibc) is recorded from the
# code being built: on CI in the compile phase, before the package phase packs it; locally on request
# (BUILDVAR_GenerateAotProfile=true, or ./build.ps1 -Tasks GenerateAotProfile). Linux only: the trace needs the
# instrumented JIT of a framework-dependent run and dotnet-pgo, which the dotnet-eng feed publishes as a .NET 11
# tool only, so a .NET 11 runtime must be present: the pipeline installs the 11 SDK (additionalNetSdkVersion in
# build.yml); locally the task takes the runtime from `dotnet`, then from ~/.dotnet, and only otherwise installs
# one under .zf/aot-profile. The result goes to src/Corvus.Text.Json/obj/profiles, which the package prefers to
# the checked-in file when present.
$GenerateAotProfile = [Convert]::ToBoolean((property BUILDVAR_GenerateAotProfile ($env:GITHUB_ACTIONS ? $true : $false)))
$DotnetPgoVersion = "11.0.0-preview.6.26310.106"
$DotnetPgoFeed = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/flat2"
$DotnetPgoRuntimeMajor = 11

task GenerateAotProfile -If { $GenerateAotProfile -and $IsLinux } {
    $profileDir = Join-Path $here ".zf/aot-profile"
    $toolsDir = Join-Path $profileDir "tools"
    New-Item -ItemType Directory -Path $toolsDir -Force | Out-Null

    # The dotnet that runs dotnet-pgo: one whose root has a .NET $DotnetPgoRuntimeMajor runtime.
    function Test-HasRuntime([string] $dotnetExe) {
        return (Test-Path $dotnetExe) -and ((& $dotnetExe --list-runtimes 2>$null) -match "^Microsoft\.NETCore\.App $DotnetPgoRuntimeMajor\.")
    }
    $pgoDotnet = "dotnet"
    $pgoDotnetRoot = $null
    if (-not (Test-HasRuntime (Get-Command dotnet).Source)) {
        $userDotnet = Join-Path $HOME ".dotnet/dotnet"
        $localDotnet = Join-Path $profileDir "dotnet11/dotnet"
        if (Test-HasRuntime $userDotnet) {
            $pgoDotnet = $userDotnet
        }
        elseif (Test-HasRuntime $localDotnet) {
            $pgoDotnet = $localDotnet
        }
        else {
            Write-Host "No .NET $DotnetPgoRuntimeMajor runtime found for dotnet-pgo; installing one under $profileDir"
            $installScript = Join-Path $profileDir "dotnet-install.sh"
            Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.sh" -OutFile $installScript
            exec { & bash $installScript --channel "$DotnetPgoRuntimeMajor.0" --quality preview --runtime dotnet --install-dir (Join-Path $profileDir "dotnet11") }
            $pgoDotnet = $localDotnet
        }
        $pgoDotnetRoot = Split-Path $pgoDotnet -Parent
    }
    Write-Host "dotnet-pgo runs on $pgoDotnet"

    $pgoDir = Join-Path $toolsDir "dotnet-pgo/$DotnetPgoVersion"
    $pgoDll = Join-Path $pgoDir "tools/net11.0/any/dotnet-pgo.dll"
    if (-not (Test-Path $pgoDll)) {
        Write-Host "Downloading dotnet-pgo $DotnetPgoVersion"
        New-Item -ItemType Directory -Path $pgoDir -Force | Out-Null
        $nupkg = Join-Path $pgoDir "dotnet-pgo.zip"
        Invoke-WebRequest -Uri "$DotnetPgoFeed/dotnet-pgo/$DotnetPgoVersion/dotnet-pgo.$DotnetPgoVersion.nupkg" -OutFile $nupkg
        Expand-Archive -Path $nupkg -DestinationPath $pgoDir -Force
    }

    $trace = Join-Path $toolsDir "dotnet-trace"
    if (-not (Test-Path $trace)) {
        exec { & dotnet tool install dotnet-trace --tool-path $toolsDir }
    }

    Write-Host "Collecting the corpora"
    $corpusDir = Join-Path $profileDir "sourcemeta"
    New-Item -ItemType Directory -Path $corpusDir -Force | Out-Null
    Get-ChildItem (Join-Path $here "benchmarks") -Directory -Filter "Corvus.Text.Json.*BenchmarkModels" | ForEach-Object {
        Get-ChildItem $_.FullName -File | Where-Object { $_.Name -like "*-instances.jsonl" -or $_.Name -like "*-schema.json" } | Copy-Item -Destination $corpusDir -Force
    }
    $corpora = Get-ChildItem $corpusDir -Filter "*-instances.jsonl" | ForEach-Object { $_.Name -replace "-instances\.jsonl$", "" } | Sort-Object

    # Publishing the runner rebuilds Corvus.Text.Json (a project reference) into the same bin/obj the package phase
    # packs from, and a plain `dotnet publish` knows nothing of the version the build gave the library: 5.6.0
    # shipped a Corvus.Text.Json.dll with AssemblyVersion 1.0.0.0 that way, which no other package could bind
    # to. So the identity of the assembly the build produced is read back and handed to the publish, and the
    # task fails if the assembly's identity is not the same afterwards.
    $libraryDll = Join-Path $here "src/Corvus.Text.Json/bin/$Configuration/net10.0/Corvus.Text.Json.dll"
    if (-not (Test-Path $libraryDll)) {
        throw "GenerateAotProfile must run after the build: $libraryDll is missing"
    }
    function Get-AssemblyIdentity([string] $path) {
        $name = [Reflection.AssemblyName]::GetAssemblyName($path)
        $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($path)
        return [pscustomobject]@{ AssemblyVersion = $name.Version.ToString(); FileVersion = $info.FileVersion; InformationalVersion = $info.ProductVersion }
    }
    $identity = Get-AssemblyIdentity $libraryDll
    if ($identity.AssemblyVersion -eq "1.0.0.0") {
        throw "The built Corvus.Text.Json.dll has the default assembly version 1.0.0.0; the build did not version it"
    }
    # The SDK appends "+<commit>" to InformationalVersion itself (SourceLink), so pass it without the suffix.
    $versionWithoutMetadata = $identity.InformationalVersion -replace '\+.*$', ''
    $versionProperties = @(
        "-p:Version=$versionWithoutMetadata",
        "-p:AssemblyVersion=$($identity.AssemblyVersion)",
        "-p:FileVersion=$($identity.FileVersion)",
        "-p:InformationalVersion=$versionWithoutMetadata"
    )
    Write-Host "Publishing the cold runner (framework-dependent) as $($identity.InformationalVersion)"
    $runnerDir = Join-Path $profileDir "runner"
    exec { & dotnet publish (Join-Path $here "benchmarks/Corvus.Text.Json.RuntimeEvaluator.ColdRunner/Corvus.Text.Json.RuntimeEvaluator.ColdRunner.csproj") -c $Configuration -r linux-x64 --self-contained false -o $runnerDir --nologo -v:minimal @versionProperties }
    $after = Get-AssemblyIdentity $libraryDll
    if (($after.AssemblyVersion -ne $identity.AssemblyVersion) -or ($after.FileVersion -ne $identity.FileVersion) -or ($after.InformationalVersion -ne $identity.InformationalVersion)) {
        throw "Publishing the cold runner changed Corvus.Text.Json.dll from $($identity.InformationalVersion) ($($identity.AssemblyVersion)) to $($after.InformationalVersion) ($($after.AssemblyVersion))"
    }
    $published = Get-AssemblyIdentity (Join-Path $runnerDir "Corvus.Text.Json.dll")
    if ($published.AssemblyVersion -ne $identity.AssemblyVersion) {
        throw "The cold runner was published against Corvus.Text.Json $($published.AssemblyVersion), not $($identity.AssemblyVersion)"
    }

    Write-Host "Tracing the instrumented warm run over $($corpora.Count) corpora"
    $nettrace = Join-Path $profileDir "profile.nettrace"
    $env:COLD_ROOT = $corpusDir
    $env:DOTNET_TieredPGO = "1"
    $env:DOTNET_TC_QuickJitForLoops = "1"
    $env:DOTNET_TC_CallCountThreshold = "10000"
    $env:DOTNET_ReadyToRun = "0"
    try {
        exec { & $trace collect --providers "Microsoft-Windows-DotNETRuntime:0x1E000080018:5" -o $nettrace -- dotnet (Join-Path $runnerDir "Corvus.Text.Json.RuntimeEvaluator.ColdRunner.dll") warm 50 @corpora }
    }
    finally {
        Remove-Item Env:\COLD_ROOT, Env:\DOTNET_TieredPGO, Env:\DOTNET_TC_QuickJitForLoops, Env:\DOTNET_TC_CallCountThreshold, Env:\DOTNET_ReadyToRun -ErrorAction SilentlyContinue
    }

    Write-Host "Writing the profile"
    $outDir = Join-Path $here "src/Corvus.Text.Json/obj/profiles"
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    $mibc = Join-Path $outDir "Corvus.Text.Json.mibc"
    if ($pgoDotnetRoot) { $env:DOTNET_ROOT = $pgoDotnetRoot }
    try {
        exec { & $pgoDotnet $pgoDll create-mibc --trace $nettrace --output $mibc }
        $dump = Join-Path $profileDir "profile-dump.txt"
        exec { & $pgoDotnet $pgoDll dump -i $mibc -o $dump | Out-Null }
    }
    finally {
        if ($pgoDotnetRoot) { Remove-Item Env:\DOTNET_ROOT -ErrorAction SilentlyContinue }
    }
    $methods = (Select-String -Path $dump -Pattern "Corvus\.Text\.Json" | Measure-Object).Count
    if ((Get-Item $mibc).Length -lt 20000 -or $methods -lt 1000) {
        throw "The AOT profile looks wrong: $((Get-Item $mibc).Length) bytes, $methods Corvus.Text.Json methods"
    }
    Write-Host "Profile written to $mibc ($((Get-Item $mibc).Length) bytes, $methods Corvus.Text.Json methods); the package takes it from there"
}

task PostBuild GenerateAotProfile, BuildWebSiteLocal
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