#Requires -Version 7.1
<#
.SYNOPSIS
    Runs a ZeroFailed-based build process.
.DESCRIPTION
    This script was scaffolded using a template from the ZeroFailed project.
    It uses the InvokeBuild module to orchestrate an opinionated software build process.
.EXAMPLE
    PS C:\> ./build.ps1
    Downloads any missing module dependencies (ZeroFailed & InvokeBuild) and executes
    the build process.
.PARAMETER Tasks
    Optionally override the default task executed as the entry-point of the build.
.PARAMETER Configuration
    The build configuration, defaults to 'Release'.
.PARAMETER SourcesDir
    The path where the source code to be built is located, defaults to the current working directory.
.PARAMETER PackagesDir
    The output path for any packages produced as part of the build.
.PARAMETER LogLevel
    The logging verbosity.
.PARAMETER Clean
    When true, the .NET solution will be cleaned and all output/intermediate folders deleted.
.PARAMETER Website
    When true, builds the documentation website after the .NET build completes.
    The website build reuses the already-compiled binaries (skips .NET compilation).
    Also set when the BUILDVAR_BuildWebsite environment variable is 'true' (for CI).
.PARAMETER BasePathPrefix
    Base URL path prefix for the documentation website (e.g. '/Corvus.Text.Json' for
    GitHub Pages subpath hosting). Only used when -Website is set.
    Falls back to the BUILDVAR_BasePathPrefix environment variable if not specified.
.PARAMETER ZfModulePath
    The path to import the ZeroFailed module from, instead of installing it. This is useful when testing a local
    copy of ZeroFailed.
.PARAMETER ZfModuleVersion
    The version of the ZeroFailed module to install and import.
.PARAMETER ZfModuleRepository
    The PowerShell repository to install the ZeroFailed module from: a registered repository name, or a URL. For a
    URL, a repository that is already registered with that URL (and any credentials it has) is used. Otherwise, the
    URL is registered as a new repository. The default is the ZeroFailed GitHub Packages feed. GitHub Packages needs
    a token, even for public packages: when the GITHUB_TOKEN or GH_TOKEN environment variable is set, it is used as
    the credential. If the module cannot be installed from this repository, it is installed from PSGallery.
.PARAMETER ZfExtensionsLockMode
    How the ZeroFailed extensions lock file is used. 'Write' resolves the extensions and updates the lock file.
    'Frozen' installs the versions in the lock file, and fails if the extensions differ from it. 'Off' does not
    use the lock file. Defaults to 'Frozen' in GitHub Actions, and to 'Write' elsewhere. The
    ZF_EXTENSIONS_LOCK_MODE environment variable takes precedence over this parameter.
.PARAMETER InvokeBuildModuleVersion
    The version of the InvokeBuild module to be used.
#>
[CmdletBinding()]
param (
    [Parameter(Position=0)]
    [string[]] $Tasks = @("."),

    [Parameter()]
    [string] $Configuration = "Release",

    [Parameter()]
    [string] $SourcesDir = $PWD,

    [Parameter()]
    [string] $PackagesDir = "_packages",

    [Parameter()]
    [ValidateSet("quiet","minimal","normal","detailed")]
    [string] $LogLevel = "minimal",

    [Parameter()]
    [switch] $Clean,

    [Parameter()]
    [switch] $Website,

    [Parameter()]
    [string] $BasePathPrefix = "",

    [Parameter()]
    [string] $ZfModulePath,

    [Parameter()]
    [string] $ZfModuleVersion = "2.0.0",

    [Parameter()]
    [string] $ZfModuleRepository = "https://nuget.pkg.github.com/zerofailed/index.json",

    [Parameter()]
    [ValidateSet("Write","Frozen","Off")]
    [string] $ZfExtensionsLockMode = ($env:GITHUB_ACTIONS -eq "true" ? "Frozen" : "Write"),

    [Parameter()]
    [version] $InvokeBuildModuleVersion = "5.14.23"
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $PSCommandPath

#region InvokeBuild setup
# This handles calling the build engine when this file is run like a normal PowerShell script
# (i.e. avoids the need to have another script to setup the InvokeBuild environment and issue the 'Invoke-Build' command )
if ($MyInvocation.ScriptName -notlike '*Invoke-Build.ps1') {
    if (!(Get-InstalledPSResource InvokeBuild -Version $InvokeBuildModuleVersion -ErrorAction Ignore)) {
        # InvokeBuild is only published to PSGallery, which sometimes fails, so retry the install
        for ($attempt = 1; ; $attempt++) {
            try {
                Install-PSResource InvokeBuild -Version $InvokeBuildModuleVersion -Scope CurrentUser -TrustRepository -Repository PSGallery
                break
            }
            catch {
                if ($attempt -ge 3) { throw }
                Write-Warning "Could not install InvokeBuild $InvokeBuildModuleVersion (attempt $attempt of 3), so retrying: $($_.Exception.Message)"
                Start-Sleep -Seconds (5 * $attempt)
            }
        }
    }
    try {
        Invoke-Build $Tasks $MyInvocation.MyCommand.Path @PSBoundParameters
    }
    catch {
        Write-Host -f Yellow "`n`n***`n*** Build Failure Summary - check previous logs for more details`n***"
        Write-Host -f Yellow $_.Exception.Message
        Write-Host -f Yellow $_.ScriptStackTrace
        exit 1
    }
    return
}
#endregion

#region Initialise build framework
$splat = @{ Force = $true; Verbose = $false}
if (!($ZfModulePath)) {
    if (!(Get-InstalledPSResource ZeroFailed -Version $ZfModuleVersion -ErrorAction Ignore)) {
        # Support a repository URL, re-using any existing registration for it (and its credentials)
        if ($ZfModuleRepository -match '://') {
            $uri = $ZfModuleRepository
            $ZfModuleRepository = (Get-PSResourceRepository | ? Uri -eq $uri | Select-Object -First 1).Name ?? ($uri -replace '\W','_')
            $null = Get-PSResourceRepository $ZfModuleRepository -ErrorAction Ignore || Register-PSResourceRepository $ZfModuleRepository -Uri $uri
        }
        $installSplat = @{
            Name = 'ZeroFailed'
            Version = $ZfModuleVersion
            Repository = $ZfModuleRepository
            Scope = 'CurrentUser'
            TrustRepository = $true
        }
        # GitHub Packages needs a token to install any package, even a public one. In GitHub Actions, the
        # workflow's token is in GITHUB_TOKEN or GH_TOKEN, depending on the action that runs this script.
        $gitHubToken = $env:GITHUB_TOKEN ? $env:GITHUB_TOKEN : $env:GH_TOKEN
        if ($gitHubToken -and (Get-PSResourceRepository $ZfModuleRepository).Uri.Host -eq 'nuget.pkg.github.com') {
            $installSplat.Credential = [pscredential]::new('token', (ConvertTo-SecureString $gitHubToken -AsPlainText -Force))
        }
        try {
            Install-PSResource @installSplat
        }
        catch {
            if ($ZfModuleRepository -eq 'PSGallery') { throw }
            Write-Warning "Could not install ZeroFailed $ZfModuleVersion from '$ZfModuleRepository', so trying PSGallery: $($_.Exception.Message)"
            $installSplat.Remove('Credential')
            $installSplat.Repository = 'PSGallery'
            Install-PSResource @installSplat
        }
    }
    # Import the module's manifest by its path. A package from GitHub Packages is installed in a lower-case
    # 'zerofailed' folder, which 'Import-Module ZeroFailed' does not find on a case-sensitive file system (Linux).
    $installed = Get-InstalledPSResource ZeroFailed -Version $ZfModuleVersion | Select-Object -First 1
    $ZfModulePath = Join-Path $installed.InstalledLocation $installed.Name ($ZfModuleVersion -split '-')[0] 'ZeroFailed.psd1'
    if (!(Test-Path $ZfModulePath)) {
        throw "The ZeroFailed module manifest is not in the expected location: $ZfModulePath"
    }
}
else {
    Write-Host "ZfModulePath: $ZfModulePath"
}
$splat.Add("Name", $ZfModulePath)
# Ensure only 1 version of the module is loaded
Get-Module ZeroFailed | Remove-Module
Import-Module @splat
$ver = ("{0} {1}" -f (Get-Module ZeroFailed).Version, (Get-Module ZeroFailed).PrivateData.PsData.PreRelease).Trim()
Write-Host "Using ZeroFailed module version: $ver from $ZfModulePath"
#endregion

$PSModuleAutoloadingPreference = 'none'

# ZeroFailed reads the extensions lock mode from this variable, unless ZF_EXTENSIONS_LOCK_MODE is set
$zerofailedExtensionsLockMode = $ZfExtensionsLockMode

# Load the build configuration
. $here/.zf/config.ps1