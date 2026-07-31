# Serverless AOT: building and deploying a version's native binary

How a catalogued workflow version becomes a native binary for a serverless runner, and the invariants a deployment
and its CI must hold so that every build is reproducible and every live version stays dispatchable.

The serverless backend runs a per-(environment, version) native binary, Native-AOT compiled from the version's
already-signed executor IL. The decision is [ADR 0055](../adr/0055-serverless-backend-aot-from-signed-executor.md)
(serverless AOT from the signed executor), which supersedes [ADR 0028](../adr/superseded/0028-pluggable-execution-backends.md).
It builds on the compile-at-add path in the [execution host guide](execution-host.md), the content-hashed package in
the [catalog guide](catalog.md), and the integrity binding in
[ADR 0025](../adr/0025-integrity-binding-optional-signature.md). Operating a runner is the
[running a runner guide](running-a-runner.md).

## The pipeline

A version's executor IL is compiled and signed once, at catalog-add. The native binary is produced later, on demand,
per runtime target, and is provably derived from that signed IL. `WorkflowAotBuildService.BuildAndAttachAsync` runs the
whole path for one (version, target):

1. Read the signed `executor.dll`, its manifest, and the detached signature from the version's package.
2. Verify the trust chain, exactly as a runner verifies at load time. The manifest parses, the assembly's SHA-256
   digest matches the manifest it is signed under, the package is signed, and the signature verifies against a trusted
   key. Any failure refuses the build.
3. Assemble a thin serverless host-app around the signed IL (`AotHostAppAssembler`). The host-app references the signed
   `executor.dll` by its assembly identity and pins the runtime packages at the version's engine version.
4. Native-AOT compile the host-app in the build container (`ContainerWorkflowAotBuilder` runs the Amazon Linux 2023
   image for the Linux targets).
5. Sign the binary and attach it with its attestation. The binary goes to `metadata/native/<rid>`, the signed
   attestation to `metadata/native-attestation/<rid>.json` and `.sig`.

A Native-AOT compile failure (an AOT-cleanliness gap) is a non-successful outcome carrying the build log. A missing,
malformed, or unverified executor is a refusal, because it is input the control plane must not have produced.

## Why build from the signed IL, not from source

The executor is generated and compiled to IL once, at catalog-add, and its manifest is signed by the control plane
(the private key never leaves the signing vault). Native AOT (ILC) compiles IL, so the build service compiles the
already-signed `executor.dll` directly rather than re-generating and re-compiling source. That keeps the trust chain
short. The native binary derives from the exact IL a runner would otherwise verify and load, and the build service
checks the same digest binding and signature before it starts ILC. There is no separately-signed source to trust, which
is the correction [ADR 0055](../adr/0055-serverless-backend-aot-from-signed-executor.md) makes over ADR 0028.

## Signing the native binary

Verifying the signed IL proves the native binary was derived from trusted IL at the moment it was built. It does not
protect the binary afterward. The binary travels through storage, transport, and deploy before it runs, and the
in-process backend has no such gap only because it verifies the IL at every load. So the control plane signs the native
binary inside the build operation, the moment it is produced, with the same key and signer that sign the executor
manifest. The attestation is a small manifest binding the binary to its version (the content hash), its runtime target,
its engine version, and its own digest, with a detached signature, carried alongside the binary under
`metadata/native-attestation/<rid>`.

A deploy verifies that attestation against a trusted key before it hands the binary to the function platform, so a
binary swapped in storage or transit is caught, and a validly-signed binary for one version or target cannot be replayed
as another. `WorkflowAotBuildService.VerifyNativeArtifact` is that check. The function platform's own code-signing (for
example AWS Signer) is the complementary control that enforces integrity at and after deploy, the continuous check the
in-process backend gets from verify-at-load. Signing the output is not optional for the serverless path: a version
reaches a serverless environment only through a fully signed chain, so a deployment that runs unsigned executors uses the
in-process backend.

## Invariant 1: the engine-version pin

Each version's executor manifest records an `engineVersion`, the Corvus runtime version its IL was compiled against
(manifest format 2 and later). The assembler references the runtime graph as packages at that version, and refuses a
manifest that records no engine version, because a native binary linked against a mismatched runtime is not a safe
artifact. The native binary's runtime therefore matches the IL's runtime exactly.

## Invariant 2: feed retention (the rule that stops drift)

The package feed the build container restores from must retain every engine version that any live workflow version was
built against. A version can be rebuilt at any time, for a new runtime target, a redeploy, or a lost artifact, and the
rebuild must resolve the same engine version its IL was compiled against. Pruning a runtime package version from the
feed while a live workflow version still pins it breaks that version's rebuild.

The retention rule is: keep a runtime package version in the feed for as long as any non-retired workflow version's
manifest names it as its `engineVersion`. A deployment's CI enforces this whenever it prunes the feed. This is the
single invariant most likely to drift silently, because a version built months ago against an older runtime keeps
pinning that runtime, and nothing else in the system references it.

## Invariant 3: multi-target, RID-keyed artifacts

A version carries at most one native binary per runtime target, stored under `metadata/native/<rid>` (for example
`metadata/native/linux-x64`). Targets are built on demand, so a version might carry a Linux binary first and a Windows
binary later. Native AOT does not cross-compile across operating systems, so a Linux target builds on a Linux host and a
Windows target needs a Windows build host. `IWorkflowAotBuilder` is the per-target seam a builder for each target
implements. A runner deploys the binary that matches its platform.

The content hash covers only the workflow and its sources, so these native entries, like every `metadata/*` entry, are
excluded from it. Building a new target, or rebuilding one, never changes the version's identity.

## Building the AOT builder image

A deployment builds the image once, and again on a runtime upgrade. The script drives podman or docker and is
cross-platform PowerShell.

```pwsh
# From src/Corvus.Text.Json.Arazzo.Durability.Aot
./build-aot-builder-image.ps1                      # arazzo-aot-builder:net10, via podman
./build-aot-builder-image.ps1 -ContainerCli docker # or docker
```

Only the capability to build the image is in the repository (the Dockerfile and this script). The image itself and the
native binaries it produces are never committed.

## Building a version's native binary

The control plane invokes the build service with the container builder and the feed the runtime packages resolve from.
`RuntimePackageVersion` is the version's engine version, which invariant 2 guarantees the feed still carries.

```csharp
var builder = new ContainerWorkflowAotBuilder(new ContainerAotBuilderOptions
{
    ContainerImage = "arazzo-aot-builder:net10",
    ReadOnlyMounts = [(feedPath, "/work/local-packages")],
});

var service = new WorkflowAotBuildService(
    verifier,   // the control plane's own trust store: build only from a verified, signed executor
    builder,
    new AotHostAppOptions
    {
        RuntimePackageVersion = manifestEngineVersion, // the version's engineVersion (invariant 1)
        FeedSources = [("local", "/work/local-packages"), ("nuget.org", "https://api.nuget.org/v3/index.json")],
    });

WorkflowAotBuildOutcome outcome = await service.BuildAndAttachAsync(package, "linux-x64", cancellationToken);
if (outcome.Succeeded)
{
    // outcome.Package carries the native binary as metadata/native/linux-x64; the content hash is unchanged.
}
```

In development the feed is the local package feed `build-local-packages.ps1` produces (it packs the whole solution to
`local-packages/` at a `5.0.0-local.N` version, newer than nuget.org). In production the same mount points at the
internal feed that resolves the runtime package version. The container stays hermetic either way.

## Feed-retention check

The shape of the check a deployment's CI runs before it prunes the feed. It compares the set of engine versions live
workflow versions still pin against the set the feed carries, and fails if any pinned version would be removed.

```pwsh
# Illustrative. $liveEngineVersions is read from the manifests of every non-retired version;
# $feedVersions is the runtime package versions present in the feed.
$missing = $liveEngineVersions | Where-Object { $_ -notin $feedVersions }
if ($missing) {
    throw "The feed is missing runtime versions still pinned by live workflow versions: $($missing -join ', ')."
}
```

## From build to a live run

The path from a signed executor to a running serverless function is now wired end to end. Promoting a version into an
environment that requires Isolated execution queues its build for the environment's runtime target (deploy-on-publish),
and the start gate holds a run pinned to that environment until the build reaches ready (the dispatch-ready gate,
`INativeBuildJobStore.IsTargetReadyAsync`). Dispatch also matches the environment's required isolation against a runner's
advertised model (Phase 2, ADR 0058).

The deploy step then hands the built binary to the function platform. `WorkflowDeployWorker` drives a per-target
deployment through `Queued -> Deploying -> Deployed | Failed`, calling `WorkflowDeployService.DeployAsync`, which first
verifies the attestation (`WorkflowAotBuildService.VerifyNativeArtifact`) and then passes the verified binary to an
`IServerlessDeployer`. That interface is the per-platform seam: `LambdaServerlessDeployer` is the AWS Lambda
implementation (it packages the `bootstrap` binary, creates the `provided.al2023` function, and exposes a Function URL).
Each deployed function is baked for one environment, so the deployer stamps the environment's source base URLs as
`ARAZZO_SOURCE__<name>` function environment variables, which the baked `Bind()` in the host-app reads into one
`HttpClientTransport` per source. A run is dispatched by `ServerlessRunExecutionBackend`, which POSTs
`{ runId, environment, checkpointUrl }` to the resolved Function URL (`DeployedFunctionUrlResolver`); the function
restores the run, binds its transports, advances it, and checkpoints back over HTTP through `HttpWorkflowStateStore`.

### Verifying the deploy path

The deploy-and-run path is exercised live against **LocalStack** as the local analogy for AWS Lambda: a version
promoted into an Isolated environment builds, deploys as a Lambda, and a run dispatched to it executes an OpenAPI step
and completes, with the outcome checkpointed back. Under rootless **podman**, pin a **4.x Community** LocalStack (the
demo AppHost and the deploy integration test both pin **4.9.2**) and set **`LAMBDA_PREBUILD_IMAGES=1`**: LocalStack's
runtime code-copy (the Docker-API `put_archive` that streams the native binary into the exec container) is unreliable
there ("passing bulk input to subprocess: write |1: broken pipe"), and prebuilding bakes the code into a per-function
image via a podman *build* (a filesystem build context, which is unaffected), skipping that copy. The 3.0 image's
prebuild path has a separate bug and every 2026.x CalVer / `:stable` tag is the licensed image that needs a token, so a
4.x pin is required. See `samples/arazzo/Corvus.Text.Json.Arazzo.ControlPlane.Demo.AppHost`.

The same path is a **repository gate**, not only a manual sample: `ServerlessLiveExecutionLocalStackTests` (opt-in,
`[integration][docker]`) compiles a real echo-workflow bootstrap in the AOT builder container, deploys it with the
production `LambdaServerlessDeployer` to LocalStack 4.9.2, and asserts execution two ways over one deployed function. The
first invokes a no-`runId` probe and checks the bootstrap faults *inside* the Arazzo invocation handler (its exception
message survives native-AOT symbol stripping, where a stack-frame name would not). Its **run-to-completion companion**
hosts the runner's real checkpoint surface (`MapWorkflowCheckpointEndpoints`) and the workflow's `echo` source on Kestrel
bound to `0.0.0.0`, seeds a Pending run, dispatches the function a real `{ runId, environment, checkpointUrl }`, and
asserts the run reaches `Completed` with the `callEcho` step output `{ "status": "ok" }` — proving the function loaded
its checkpoint, called its source at `host.containers.internal`, and saved the advance back. The gate skips unless
`ARAZZO_AOT_LOCAL_FEED` and `ARAZZO_AOT_RUNTIME_VERSION` are set (and, under podman, `ARAZZO_LOCALSTACK_DOCKER_SOCK`).

The **Azure Functions** target has the counterpart gate `ServerlessLiveExecutionAzureFunctionsTests` (opt-in,
`[integration][docker]`). Azure has no management-plane emulator (Azurite emulates Storage only), so it proves
**execution**, not the deploy (ADR 0061): it compiles the *same* `serverless-check` workflow into a framework-dependent
ReadyToRun isolated-worker app in the AOT builder container, drops the published app into the real Azure Functions
runtime image (`mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0`) at `/home/site/wwwroot`
(HTTP-only, so no Azure Storage is needed), and dispatches the same seeded Pending run to its `[Function("invoke")]` HTTP
trigger. The run reaches `Completed` with `callEcho` `{ "status": "ok" }`, the isolated worker having reached this test's
Kestrel checkpoint surface and `echo` source through `host.containers.internal` (a `--add-host …:host-gateway` route).
Both gates compile the identical workflow from one shared fixture, so the two vendors' execution is proven over the same
run.

The Azure deploy path itself is `AzureFunctionsServerlessDeployer` (the `...AzureFunctions.Deploy` package). It deploys a
version's app package by **run-from-package**: it uploads the zip to a blob container over the injected
`BlobContainerClient` (Azurite locally, real Azure Storage in production, only the endpoint differing, the same way the
Lambda deployer uses `IAmazonLambda`), then points the `dotnet-isolated` Function App at it with `WEBSITE_RUN_FROM_PACKAGE`.
The management-plane app configuration, which sets that value and the source app settings over ARM, has no local emulator,
so it is a separate injected seam, `IFunctionAppConfigurator`, real ARM in production and a recording fake in tests. The
storage mechanism is proven live against Azurite by `AzureFunctionsServerlessDeployerAzuriteTests`, which checks the
run-from-package URL the deployer mints fetches back the byte-identical package it uploaded. The whole path is proven end
to end by `AzureFunctionsRunFromPackageDeployTests`, which uploads a real app to Azurite, fetches the package from that URL
exactly as the App Service platform would, and runs it to completion under the real Functions host. The standalone
`dotnet-isolated` runtime image does not itself honour a `WEBSITE_RUN_FROM_PACKAGE` URL (that download is an App Service
platform feature), so the gate performs the platform's fetch and mounts the package in its place.

On **Flex Consumption** — Microsoft's recommended serverless plan, and the one that boots the deployed isolated worker
where Linux Consumption does not — the deploy mechanism is different: Flex's only deployment technology is *One Deploy*,
not `WEBSITE_RUN_FROM_PACKAGE`. So `AzureFunctionsFlexDeployer` (the `…AzureFunctions.Deploy.Arm` package) posts the app
package to the app's One Deploy endpoint (`{scm}/api/publish`) with an Azure Resource Manager AAD bearer token (Flex
disables SCM basic auth) and waits on the deployment. This is proven end to end against **real Azure** by
`ArmFunctionAppLiveDeployTests`: it provisions a real Flex Consumption dotnet-isolated (.NET 10) Function App, runs the
production deployer against it, checks the platform loaded our function (its `invoke` route stops being a 404), and tears
down every resource it created (zero cost between runs). It skips unless `ARAZZO_AZURE_SUBSCRIPTION_ID` and
`ARAZZO_AZURE_RESOURCE_GROUP` are set (with the Azure CLI authenticated); no subscription identifiers are in source.

## The operator surface

The control and visibility over this pipeline, all landed:

- **REST.** A version's builds are the `nativeBuilds` operations; its deployments are the read-only `deployments`
  operations (`GET .../versions/{n}/deployments`, `/count`, `/{environment}/{runtimeIdentifier}`), reach-gated to the
  version, keyset-paged, status-filterable. Deployments are read-only on the control plane by design — the deploy runs
  on the runner, which holds the environment's cloud credentials (ADR 0059); the control plane records and reports the
  state the deploy worker drives.
- **CLI.** `arazzo-runs runners list` (the roster with advertised isolation and liveness), `builds list/get/enqueue`,
  and `deployments list/get` (status + function invoke URL).
- **Console.** The catalog version detail's "Serverless — builds & deployments" section (build/deploy rows per target,
  queue-a-build, failure reasons, invoke URLs), and the runners panel's isolation-posture strip: per environment
  requiring `Isolated`, whether a live, authorized runner advertising `Isolated` serves it.
- **Host wiring.** Deployer selection is a host-wiring concern (ADR 0061): the serverless runner picks its platform
  from `Runner:Serverless:Platform` — `lambda` (default) or `azure-flex` (the One Deploy path over the runner's ambient
  Azure identity) — with the same deploy worker, verification, and queue driving either.

Execution-host **isolation hardening** also landed: register-time, authorize-time, and environment-update admission
checks plus the runner's advertise-vs-wire startup fence (ADR 0058).
