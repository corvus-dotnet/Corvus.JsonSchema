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

## Wiring status

The build service produces, signs, and attaches the native binary, and the deploy-time verification of the attestation
exists (`WorkflowAotBuildService.VerifyNativeArtifact`). Publishing is now wired to the build. Promoting a version into
an environment that requires Isolated execution queues its build for the environment's runtime target (deploy-on-publish),
and the start gate holds a run pinned to that environment until the build reaches ready (the dispatch-ready gate,
`INativeBuildJobStore.IsTargetReadyAsync`). Dispatch also matches the environment's required isolation against a runner's
advertised model (Phase 2, ADR 0058).

One step remains to reach a live serverless deploy: the deploy step itself, which calls `VerifyNativeArtifact` and hands
the verified binary to the function platform (AWS Lambda, then Azure Functions). Until it lands, the sample runners run
in-process, this build path is exercised by the container integration proof rather than by a live serverless deploy, and
an Isolated environment's runs stay held at the build-ready gate because there is no deployed function to dispatch to.
