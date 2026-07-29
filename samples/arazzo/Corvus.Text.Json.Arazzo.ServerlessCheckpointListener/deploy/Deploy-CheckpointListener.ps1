<#
.SYNOPSIS
    Deploys the Arazzo public checkpoint listener to Azure Container Apps and writes a state file for teardown.

.DESCRIPTION
    Stands up the token-authenticated checkpoint listener (ADR 0062) as a scale-to-zero Azure Container App,
    backed by a fresh Azure Storage run store, so a real serverless function (Azure Functions or AWS Lambda)
    can reach its checkpoint surface over public HTTPS. It:

      1. Registers the Microsoft.App and Microsoft.OperationalInsights providers (Container Apps needs them, and
         Microsoft.App is not registered by default).
      2. Creates an Azure Storage account (the shared run store the listener terminates checkpoints into).
      3. Publishes the listener and builds + pushes its image with `az acr build` (no local Docker needed).
      4. Creates a Log Analytics workspace (explicit, so teardown is deterministic) and a Container Apps
         environment and app: external ingress on 8080, scale-to-zero, with the storage connection string and a
         freshly generated checkpoint secret supplied as Container App secrets (never plaintext env values).

    It emits a JSON state file (resource names, the public URL, the base64 checkpoint secret, and the storage
    connection string) that Remove-CheckpointListener.ps1 consumes to tear everything down at zero cost. In CI,
    read the URL/secret/storage from the state file into the test's environment variables and mask the secret.

    No subscription, secret, or storage identifier is baked into source: identifiers come from parameters or the
    ARAZZO_AZURE_* environment variables, and the secret is generated here.

.PARAMETER SubscriptionId
    The Azure subscription. Defaults to $env:ARAZZO_AZURE_SUBSCRIPTION_ID.

.PARAMETER ResourceGroup
    The resource group to deploy into (must exist). Defaults to $env:ARAZZO_AZURE_RESOURCE_GROUP.

.PARAMETER Location
    The Azure region. Defaults to $env:ARAZZO_AZURE_LOCATION, then 'uksouth'.

.PARAMETER Suffix
    A short unique suffix for globally-unique resource names. Defaults to a random 8-hex-char value.

.PARAMETER StateFile
    Where to write the deployment state JSON. Defaults to '.listener-deploy-state.json' beside this script.

.EXAMPLE
    ./Deploy-CheckpointListener.ps1
    Deploys using the ARAZZO_AZURE_* environment variables and a random suffix.
#>
[CmdletBinding()]
param(
    [string]$SubscriptionId = $env:ARAZZO_AZURE_SUBSCRIPTION_ID,
    [string]$ResourceGroup = $env:ARAZZO_AZURE_RESOURCE_GROUP,
    [string]$Location = $(if ($env:ARAZZO_AZURE_LOCATION) { $env:ARAZZO_AZURE_LOCATION } else { 'uksouth' }),
    [string]$Suffix = ([guid]::NewGuid().ToString('n').Substring(0, 8)),
    [string]$StateFile = (Join-Path $PSScriptRoot '.listener-deploy-state.json')
)

$ErrorActionPreference = 'Stop'

if (-not $SubscriptionId -or -not $ResourceGroup) {
    throw 'Set ARAZZO_AZURE_SUBSCRIPTION_ID and ARAZZO_AZURE_RESOURCE_GROUP (or pass -SubscriptionId/-ResourceGroup).'
}

# Only stdout is a reliable channel for captured values: the Azure CLI can emit a Python dependency warning on
# stderr. This helper runs az, throws on failure, and returns stdout trimmed.
function Invoke-Az {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Args)
    $out = & az @Args
    if ($LASTEXITCODE -ne 0) { throw "az $($Args -join ' ') failed (exit $LASTEXITCODE): $out" }
    return ($out | Out-String).Trim()
}

$project = Join-Path $PSScriptRoot '..' 'Corvus.Text.Json.Arazzo.ServerlessCheckpointListener.csproj'
$dockerfile = Join-Path $PSScriptRoot '..' 'Dockerfile'
$publishDir = Join-Path ([System.IO.Path]::GetTempPath()) "arazzo-listener-pub-$Suffix"

$storageAccount = "arazzock$Suffix"
$registry = "arazzoacr$Suffix"
$workspace = "arazzolaw$Suffix"
$environment = "arazzo-cae-$Suffix"
$app = "arazzo-listener-$Suffix"
$imageTag = 'v1'

Write-Host "Deploying checkpoint listener (suffix $Suffix) to $ResourceGroup / $Location" -ForegroundColor Cyan

Invoke-Az account set --subscription $SubscriptionId | Out-Null

# 1. Providers (Container Apps needs Microsoft.App + Microsoft.OperationalInsights). Registering a provider is a
#    subscription-scoped operation an operator performs once, and it persists. This is best-effort so a least-privileged,
#    resource-group-scoped CI identity that cannot read or register providers still proceeds: if one is genuinely
#    unregistered the later resource creation fails with a clear error, and an operator registers it once.
foreach ($provider in @('Microsoft.App', 'Microsoft.OperationalInsights')) {
    try {
        if ((& az provider show -n $provider --query registrationState -o tsv 2>$null) -ne 'Registered') {
            & az provider register -n $provider 2>&1 | Out-Null
            for ($i = 0; $i -lt 30 -and (& az provider show -n $provider --query registrationState -o tsv 2>$null) -ne 'Registered'; $i++) {
                Start-Sleep -Seconds 10
            }
        }
    }
    catch {
        Write-Warning "Could not verify or register ${provider} (an operator may need to register it once): $_"
    }
}

# 2. The shared run store.
Invoke-Az storage account create -n $storageAccount -g $ResourceGroup -l $Location `
    --sku Standard_LRS --kind StorageV2 --min-tls-version TLS1_2 --allow-blob-public-access false -o none | Out-Null
$storageConnection = Invoke-Az storage account show-connection-string -n $storageAccount -g $ResourceGroup --query connectionString -o tsv

# 3. Publish + build + push the image. The Dockerfile stages a pre-published app, so az acr build's context is the
#    publish directory (it uploads that + the Dockerfile and builds in ACR, needing no local Docker).
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
dotnet publish $project -c Release -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)." }
Copy-Item $dockerfile (Join-Path $publishDir 'Dockerfile') -Force
Invoke-Az acr create -n $registry -g $ResourceGroup -l $Location --sku Basic --admin-enabled true -o none | Out-Null
$image = "$(Invoke-Az acr show -n $registry -g $ResourceGroup --query loginServer -o tsv)/arazzo-checkpoint-listener:$imageTag"
Invoke-Az acr build --registry $registry --image "arazzo-checkpoint-listener:$imageTag" --file (Join-Path $publishDir 'Dockerfile') $publishDir | Out-Null

# 4. Log Analytics (explicit name for deterministic teardown), the Container Apps environment, and the app. A
#    fresh 256-bit checkpoint secret; the storage connection string and secret are Container App secrets.
$secretBytes = [byte[]]::new(32)
[System.Security.Cryptography.RandomNumberGenerator]::Fill($secretBytes)
$checkpointSecret = [Convert]::ToBase64String($secretBytes)

Invoke-Az monitor log-analytics workspace create -g $ResourceGroup -n $workspace -l $Location -o none | Out-Null
$workspaceId = Invoke-Az monitor log-analytics workspace show -g $ResourceGroup -n $workspace --query customerId -o tsv
$workspaceKey = Invoke-Az monitor log-analytics workspace get-shared-keys -g $ResourceGroup -n $workspace --query primarySharedKey -o tsv
Invoke-Az containerapp env create -n $environment -g $ResourceGroup -l $Location `
    --logs-workspace-id $workspaceId --logs-workspace-key $workspaceKey -o none | Out-Null

$registryUser = Invoke-Az acr credential show -n $registry -g $ResourceGroup --query username -o tsv
$registryPassword = Invoke-Az acr credential show -n $registry -g $ResourceGroup --query 'passwords[0].value' -o tsv
$loginServer = Invoke-Az acr show -n $registry -g $ResourceGroup --query loginServer -o tsv

$fqdn = Invoke-Az containerapp create -n $app -g $ResourceGroup --environment $environment --image $image `
    --registry-server $loginServer --registry-username $registryUser --registry-password $registryPassword `
    --target-port 8080 --ingress external --min-replicas 0 --max-replicas 1 --cpu 0.5 --memory 1.0Gi `
    --secrets "storage-conn=$storageConnection" "checkpoint-secret=$checkpointSecret" `
    --env-vars 'ARAZZO_CHECKPOINT_STORAGE=secretref:storage-conn' 'ARAZZO_CHECKPOINT_SECRET=secretref:checkpoint-secret' `
    --query 'properties.configuration.ingress.fqdn' -o tsv

$state = [ordered]@{
    suffix            = $Suffix
    subscriptionId    = $SubscriptionId
    resourceGroup     = $ResourceGroup
    location          = $Location
    storageAccount    = $storageAccount
    registry          = $registry
    workspace         = $workspace
    environment       = $environment
    app               = $app
    listenerUrl       = "https://$fqdn"
    checkpointSecret  = $checkpointSecret
    storageConnection = $storageConnection
}
$state | ConvertTo-Json | Set-Content -Path $StateFile

Write-Host "Listener deployed: https://$fqdn" -ForegroundColor Green
Write-Host "State written to $StateFile (contains the checkpoint secret + storage connection string; keep it out of logs)." -ForegroundColor Gray
