# Deploying the checkpoint listener

`Deploy-CheckpointListener.ps1` and `Remove-CheckpointListener.ps1` stand up and tear down the public checkpoint listener (the host in this sample) on Azure Container Apps, so a real serverless function can reach a token-authenticated checkpoint surface over public HTTPS. The design is [ADR 0062](../../../../docs/arazzo/adr/0062-authenticated-serverless-checkpoint-callbacks.md); the "Deployment" section there records the hard-won specifics these scripts encode.

## What deploy creates

A scale-to-zero Container App running the listener, plus the resources it needs, all named with a shared `-Suffix`:

- an Azure Storage account (the shared run store the listener terminates checkpoints into),
- a container registry (the listener image, built with `az acr build` from a locally published app),
- a Log Analytics workspace and a Container Apps environment,
- the Container App itself: external ingress on 8080, `--min-replicas 0`, with the storage connection string and a freshly generated 256-bit checkpoint secret supplied as Container App secrets.

It writes `.listener-deploy-state.json` (resource names, the public URL, the checkpoint secret, and the storage connection string). `Remove-CheckpointListener.ps1` reads that file and deletes every resource, best-effort, so there is zero cost between runs.

## Usage

```pwsh
# Identifiers come from parameters or the ARAZZO_AZURE_* environment variables; the secret is generated.
$env:ARAZZO_AZURE_SUBSCRIPTION_ID = '<subscription>'
$env:ARAZZO_AZURE_RESOURCE_GROUP = '<resource-group>'   # must exist
$env:ARAZZO_AZURE_LOCATION = 'uksouth'

./Deploy-CheckpointListener.ps1
# ... run the tests against the deployed listener (see below) ...
./Remove-CheckpointListener.ps1
```

## Running the real-cloud tests against it

The state file feeds three environment variables that the opt-in `[TestCategory("azure")]` gates read:

| Environment variable | Value from state file |
| --- | --- |
| `ARAZZO_CHECKPOINT_LISTENER_URL` | `listenerUrl` |
| `ARAZZO_CHECKPOINT_SECRET` | `checkpointSecret` |
| `ARAZZO_CHECKPOINT_STORAGE` | `storageConnection` |

With those set (plus `ARAZZO_AOT_LOCAL_FEED` and `ARAZZO_AOT_RUNTIME_VERSION` for the app build, and an `az login` session), three gates in `Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests` prove run-to-completion through the deployed listener:

- **`ServerlessRealCloudCheckpointListenerTests`** — the real Azure Functions runtime image, run locally, advances a seeded run to completion through the public listener and its shared store. A fast gate that needs no Function App provision.
- **`ArmFunctionAppLiveDeployTests.Runs_a_seeded_run_to_completion_on_a_live_flex_app_...`** — a real Flex Consumption Function App does the same, and tears down its own function-side resources.
- **`ServerlessRealCloudCheckpointListenerLambdaTests`** — an AWS Lambda (under LocalStack) does the same, proving the listener is vendor-neutral: one checkpoint surface for both clouds.

All three seed a Pending run into the shared store, dispatch it with a run-scoped `CheckpointToken`, and assert the run reloads `Completed` with `callEcho`'s `{ "status": "ok" }`.

> The AOT app build restores the Corvus runtime from `ARAZZO_AOT_LOCAL_FEED`, **not** live source. After any change to the serverless runtime (for example the checkpoint-token wiring), rebuild the feed — `pwsh build-local-packages.ps1 -Version 5.0.0-local.<n>` — and point `ARAZZO_AOT_RUNTIME_VERSION` at the new version, or the deployed function will lag the source it is tested against.

## CI wiring

The `azure-e2e` job in [`.github/workflows/build.yml`](../../../../.github/workflows/build.yml) runs these gates in CI. It is **dormant by default** — gated on the `RUN_AZURE_E2E` repository variable being `'true'` — so it never breaks a release build before its Azure identity exists. When enabled it logs in via OIDC, builds the AOT feed and builder image, runs `Deploy-CheckpointListener.ps1`, exports `ARAZZO_CHECKPOINT_LISTENER_URL` / `ARAZZO_CHECKPOINT_SECRET` / `ARAZZO_CHECKPOINT_STORAGE` from the state file (masking the secret), runs the `TestCategory=azure` gates, and runs `Remove-CheckpointListener.ps1` in an `always()` step. The `azure` category is excluded from the Docker `integration-tests` job (as `cosmos` is) so these gates do not silently skip there.

Because a full CI run happens on the pre-release pull request, a single `pull_request` federated credential and Contributor on the test resource group is all the job needs.

### Enabling it (one-time)

All commands are cross-platform PowerShell (`pwsh`). You need an `az login` session that can create an app registration and assign roles on the resource group, and `gh` authenticated with admin on the repository. No subscription or resource-group identifier is committed, so fill in your own:

```pwsh
$Sub  = '<subscription-id>'
$Rg   = '<resource-group>'
$Repo = 'corvus-dotnet/Corvus.JsonSchema'
```

1. **Create the service principal.**
   ```pwsh
   $AppId = az ad app create --display-name arazzo-tests-e2e --query appId -o tsv
   az ad sp create --id $AppId
   $SpObjectId = az ad sp show --id $AppId --query id -o tsv
   ```
2. **Add the GitHub OIDC federated credential** (the pre-release PR build). Add a second one with subject `repo:$Repo:ref:refs/heads/main` if push-to-`main` builds should run it too.
   ```pwsh
   $FederatedCredential = @{
       name      = 'github-pr'
       issuer    = 'https://token.actions.githubusercontent.com'
       subject   = "repo:$Repo:pull_request"
       audiences = @('api://AzureADTokenExchange')
   } | ConvertTo-Json
   az ad app federated-credential create --id $AppId --parameters $FederatedCredential
   ```
3. **Grant Contributor on the test resource group only.** If it returns `PrincipalNotFound`, wait about 30 seconds for the service principal to replicate and re-run.
   ```pwsh
   az role assignment create `
     --assignee-object-id $SpObjectId `
     --assignee-principal-type ServicePrincipal `
     --role Contributor `
     --scope "/subscriptions/$Sub/resourceGroups/$Rg"
   ```
4. **Set the secrets and the enable switch.**
   ```pwsh
   $TenantId = az account show --query tenantId -o tsv
   gh secret   set AZURE_TESTS_E2E_CLIENT_ID       --repo $Repo --body $AppId
   gh secret   set AZURE_TESTS_E2E_TENANT_ID       --repo $Repo --body $TenantId
   gh secret   set AZURE_TESTS_E2E_SUBSCRIPTION_ID --repo $Repo --body $Sub
   gh secret   set ARAZZO_AZURE_RESOURCE_GROUP     --repo $Repo --body $Rg
   gh variable set RUN_AZURE_E2E                    --repo $Repo --body 'true'
   ```
5. **Verify.**
   ```pwsh
   gh secret list --repo $Repo
   gh variable list --repo $Repo
   az ad app federated-credential list --id $AppId -o table
   az role assignment list --assignee $AppId --scope "/subscriptions/$Sub/resourceGroups/$Rg" -o table
   ```

Then open a pull request from a **same-repo** branch (a fork PR does not receive the secrets or the OIDC token) and watch the **Azure E2E (Checkpoint Listener)** job. To pause it again, set `RUN_AZURE_E2E` to `false`.

### What to expect, and first-run snags

The job is heavy on first run: the feed build alone is about 40 minutes, then the deploy and the three gates (local Azure Functions runtime, real Flex, LocalStack Lambda), then teardown. On success the resource group is left empty. The parts only a live run can settle, each isolated to this one job:

- **`azure/login` reports no matching federated identity** — the OIDC subject must be exactly `repo:corvus-dotnet/Corvus.JsonSchema:pull_request` for a same-repo PR.
- **The job has no secrets** — the PR came from a fork; use a same-repo branch.
- **The podman socket step fails** — the AOT builder and the LocalStack/Functions containers run under rootless podman; if the runner trips here, switch those to Docker or install podman.
- **A resource-creation `AuthorizationFailed`** — the service principal is missing Contributor on the resource group, or a provider is genuinely unregistered (the deploy warns rather than failing at that point, so it surfaces at the `containerapp`/`functionapp` create).
