# ADR 0060. Local serverless deploy is emulated with LocalStack, and one Lambda deployer serves local development and production

Date: 2026-07-27. Status: **Accepted**. Scope: how the serverless deploy and invoke are emulated locally so the deploy path is proven without a cloud account, and why the runner uses a single deployer against both the local emulator and real AWS. Builds on the serverless deploy design ([ADR 0055](0055-serverless-backend-aot-from-signed-executor.md)) and the deploy security model ([ADR 0059](0059-serverless-deploy-runs-on-the-runner-as-the-secure-boundary.md)).

## Context

The serverless deploy (ADR 0055, ADR 0059) creates a function from a version's signed native binary in the user's cloud account and invokes it. Proving that path needs a local emulation a developer or CI can run without a cloud account.

Two local emulators were considered:

- The AWS Lambda **Runtime Interface Emulator (RIE)** emulates only a function's runtime contract (the Lambda Runtime API) and a raw invoke endpoint, one function per instance. It has **no control plane**: no `CreateFunction`, no `UpdateFunctionCode`, no Function URLs, no IAM. Deploying to RIE is running the one function's container, not calling a deploy API. A RIE-only path would exercise runtime code the production deployer never runs, and none of the control-plane deploy code the production deployer does run, so it would not prove the deploy.
- **LocalStack** emulates the Lambda control plane and data plane through the real AWS API surface (`CreateFunction`, `UpdateFunctionCode`, Function URLs, `Invoke`), executing the function with Lambda's own runtime under the hood.

## Decision

**1. Local serverless deploy is emulated with LocalStack, not RIE.** LocalStack presents the real AWS Lambda API, so the deploy and invoke a developer or CI runs against it are the same calls the runner makes against real AWS. RIE, which cannot present `CreateFunction`, is rejected for the deploy path (it emulates only invocation).

**2. One deployer serves both.** The runner's deployer is a single `IServerlessDeployer` over the AWS SDK's `IAmazonLambda`. It targets LocalStack when a local endpoint is configured and real AWS otherwise, with no code change, because the AWS SDK client's endpoint is the only difference. There is no separate local deployer, so the code proven locally is the code that runs in production.

**3. LocalStack is an Aspire resource in the dev composition and a Testcontainers module in the automated test.** In the demo composition the AppHost adds LocalStack as a resource (via the LocalStack Aspire hosting integration), so Aspire owns the container's lifecycle and there are no orphaned containers or networks. The automated integration test spins a LocalStack container directly through the Testcontainers LocalStack module, so a test needs no AppHost. The runner's `IAmazonLambda` is registered so it auto-targets LocalStack when enabled and falls back to the real AWS SDK configuration otherwise.

## Consequences

- The deploy path is exercised by the same code locally and in production, and only the endpoint differs. This is the realism the RIE-only path could not give, since RIE has no deploy API.
- LocalStack Community does not enforce IAM, so the local proof covers the deploy and invoke **mechanics** but not the SigV4 / Function-URL `AWS_IAM` invoke auth (ADR 0059 decision 4). That auth is proven against real AWS, as ADR 0059 already states.
- Executing a custom `provided.al2023` native-AOT bootstrap under LocalStack's Lambda executor is **verified, not assumed**: the deployed function runs a workflow to completion and checkpoints back (proven 2026-07-28 via the demo composition, and gated by the automated invoke-and-run integration test). This supersedes the "Lambda-RIE local harness" the plan once named for the runtime-contract proof: RIE is rejected here (decision 1), and LocalStack executes the same bootstrap under Lambda's own runtime, so the one LocalStack gate proves both the deploy and the execution.
- **The execution proof needs a runtime environment the deploy proof does not.** LocalStack must itself reach a container runtime to spawn the exec container — under rootless **podman** it mounts the podman socket at the docker.sock path (`DOCKER_HOST`) — and its runtime code-copy (the Docker-API `put_archive` streaming the native binary into the exec container) is unreliable there ("passing bulk input to subprocess: write |1: broken pipe"). Pin a **4.x Community** image (the demo AppHost and the deploy integration test both pin **4.9.2**, the latest token-free release — keep them identical) and set **`LAMBDA_PREBUILD_IMAGES=1`**, which bakes the code into a per-function image via a podman *build* (a filesystem build context, unaffected) and skips the copy. LocalStack 3.0 deploys but cannot execute (its prebuild path has a separate bug), and every 2026.x CalVer / `:stable` / `:latest` tag is the licensed image that quits without a `LOCALSTACK_AUTH_TOKEN`, so a 4.x pin is required. Give the AOT cold-start room with `LAMBDA_RUNTIME_ENVIRONMENT_TIMEOUT`.
- The demo composition gains a LocalStack resource and the runner's serverless deploy, so the serverless path becomes demonstrable end to end on a developer box.
- The plan collapses the earlier separate "local RIE deployer" and "AWS Lambda deployer" into one `IAmazonLambda` deployer, because LocalStack is the local AWS.
