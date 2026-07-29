<#
.SYNOPSIS
    Tears down everything Deploy-CheckpointListener.ps1 created, so there is zero cost between runs.

.DESCRIPTION
    Reads the deployment state JSON and deletes the Container App, its managed environment, the Log Analytics
    workspace, the container registry, and the storage account. Every delete is best-effort and independent, so a
    resource that is already gone (or a transient failure) does not stop the rest — run it from a CI always()/finally
    step. Deleting the resources removes the deployed run store; that is intended (a fresh store per run).

.PARAMETER StateFile
    The deployment state JSON written by Deploy-CheckpointListener.ps1. Defaults to '.listener-deploy-state.json'
    beside this script.

.EXAMPLE
    ./Remove-CheckpointListener.ps1
    Tears down the deployment described by the default state file.
#>
[CmdletBinding()]
param(
    [string]$StateFile = (Join-Path $PSScriptRoot '.listener-deploy-state.json')
)

$ErrorActionPreference = 'Continue'

if (-not (Test-Path $StateFile)) {
    Write-Warning "No state file at $StateFile; nothing to tear down."
    return
}

$state = Get-Content -Raw -Path $StateFile | ConvertFrom-Json

# Best-effort az: never throw, so one failed delete does not abandon the rest.
function Remove-AzResource {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Args)
    try { & az @Args 2>&1 | Out-Null } catch { Write-Warning "az $($Args -join ' ') failed: $_" }
}

az account set --subscription $state.subscriptionId 2>&1 | Out-Null

$rg = $state.resourceGroup
Write-Host "Tearing down checkpoint listener (suffix $($state.suffix)) in $rg" -ForegroundColor Cyan

Remove-AzResource containerapp delete -n $state.app -g $rg --yes
Remove-AzResource containerapp env delete -n $state.environment -g $rg --yes
Remove-AzResource monitor log-analytics workspace delete -g $rg -n $state.workspace --yes --force true
Remove-AzResource acr delete -n $state.registry -g $rg --yes
Remove-AzResource storage account delete -n $state.storageAccount -g $rg --yes

Remove-Item -Path $StateFile -ErrorAction SilentlyContinue
Write-Host 'Teardown complete.' -ForegroundColor Green
