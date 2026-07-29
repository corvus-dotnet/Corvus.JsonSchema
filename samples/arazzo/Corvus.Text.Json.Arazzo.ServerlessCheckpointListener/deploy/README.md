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

With those set (plus `ARAZZO_AOT_LOCAL_FEED` and `ARAZZO_AOT_RUNTIME_VERSION` for the app build, and an `az login` session), two gates in `Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests` prove run-to-completion through the deployed listener:

- **`ServerlessRealCloudCheckpointListenerTests`** — the real Azure Functions runtime image, run locally, advances a seeded run to completion through the public listener and its shared store. A fast gate that needs no Function App provision.
- **`ArmFunctionAppLiveDeployTests.Runs_a_seeded_run_to_completion_on_a_live_flex_app_...`** — a real Flex Consumption Function App does the same, and tears down its own function-side resources.

Both seed a Pending run into the shared store, dispatch it with a run-scoped `CheckpointToken`, and assert the run reloads `Completed` with `callEcho`'s `{ "status": "ok" }`.

> The AOT app build restores the Corvus runtime from `ARAZZO_AOT_LOCAL_FEED`, **not** live source. After any change to the serverless runtime (for example the checkpoint-token wiring), rebuild the feed — `pwsh build-local-packages.ps1 -Version 5.0.0-local.<n>` — and point `ARAZZO_AOT_RUNTIME_VERSION` at the new version, or the deployed function will lag the source it is tested against.

## CI wiring

Run deploy before the gates and teardown in an `always()`/`finally` step, so a failed test still cleans up:

1. `az login` (the pipeline's Azure credentials — see `.github/workflows/build.yml`'s `testPhaseAzureCredentials` slot; nothing sensitive is committed).
2. `Deploy-CheckpointListener.ps1`, then export `ARAZZO_CHECKPOINT_LISTENER_URL` / `ARAZZO_CHECKPOINT_SECRET` / `ARAZZO_CHECKPOINT_STORAGE` from the state file (mask the secret).
3. Run the `[TestCategory("azure")]` gates.
4. `Remove-CheckpointListener.ps1` in an always-run step.
