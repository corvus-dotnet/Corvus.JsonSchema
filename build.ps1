#Requires -Version 7
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
    The path to import the ZeroFailed module from. This is useful when testing pre-release
    versions of ZeroFailed that are not yet available in the PowerShell Gallery.
.PARAMETER ZfModuleVersion
    The version of the ZeroFailed module to import. This is useful when testing pre-release
    versions of ZeroFailed that are not yet available in the PowerShell Gallery.
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
    [string] $ZfModuleVersion = "1.1.0-preview0001",

    [Parameter()]
    [version] $InvokeBuildModuleVersion = "5.14.22"
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $PSCommandPath

#region InvokeBuild setup
# This handles calling the build engine when this file is run like a normal PowerShell script
# (i.e. avoids the need to have another script to setup the InvokeBuild environment and issue the 'Invoke-Build' command )
if ($MyInvocation.ScriptName -notlike '*Invoke-Build.ps1') {
    Install-PSResource InvokeBuild -Version $InvokeBuildModuleVersion -Scope CurrentUser -TrustRepository -Verbose:$false | Out-Null
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
Import-Module Microsoft.PowerShell.PSResourceGet
if (!($ZfModulePath)) {
    Install-PSResource ZeroFailed -Version $ZfModuleVersion -Scope CurrentUser -TrustRepository | Out-Null
    $ZfModulePath = "ZeroFailed"
    $splat.Add("RequiredVersion", ($ZfModuleVersion -split '-')[0])
}
else {
    Write-Host "ZfModulePath: $ZfModulePath"
}
$splat.Add("Name", $ZfModulePath)
# Ensure only 1 version of the module is loaded
Get-Module ZeroFailed | Remove-Module
Import-Module @splat
$ver = "{0} {1}" -f (Get-Module ZeroFailed).Version, (Get-Module ZeroFailed).PrivateData.PsData.PreRelease
Write-Host "Using ZeroFailed module version: $ver"
#endregion

$PSModuleAutoloadingPreference = 'none'

# Load the build configuration
. $here/.zf/config.ps1