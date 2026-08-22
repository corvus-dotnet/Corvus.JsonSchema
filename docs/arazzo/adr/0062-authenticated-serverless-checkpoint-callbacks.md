# ADR 0062. Authenticated serverless checkpoint callbacks use a run-scoped bearer token

Date: 2026-07-29. Status: **Accepted**. Scope: how a deployed serverless function authenticates its checkpoint callback to the runner's checkpoint surface, so that surface can be exposed beyond a private network (for example to a real cloud function) without being an open write endpoint. Builds on the serverless backend design ([ADR 0055](0055-serverless-backend-aot-from-signed-executor.md)) and the runner-as-deploy-boundary model ([ADR 0059](0059-serverless-deploy-runs-on-the-runner-as-the-secure-boundary.md)).

## Context

A serverless run advances out of process: the runner invokes the baked function, which restores the run from its checkpoint, advances it, and checkpoints it back over HTTP to the runner's checkpoint surface — `GET`/`POST /environments/{environment}/runs/{runId}/checkpoint` (`WorkflowCheckpointEndpoints`; the route gained the `{environment}` segment under ADR 0065 decision 9, see the 2026-08-22 amendment), which the function-side `HttpWorkflowStateStore` speaks. The invocation carries `{ runId, environment, checkpointUrl }`.

Today that round-trip is **unauthenticated at the credential level**. The checkpoint surface can require an *ambient authenticated principal* (`MapWorkflowCheckpointEndpoints`'s `requireAuthorization` calls `.RequireAuthorization()` with the host's auth scheme), but:

- the function-side `HttpWorkflowStateStore` sends **no** credential on any request, and
- the invocation carries **no** token.

So the surface only works unauthenticated (as the local container gates run it) or would reject the function outright if ambient auth were required. For the checkpoint surface to be **publicly reachable** — which a real cloud function's callback requires, and which the run-to-completion-in-CI test harness needs — it must be authenticated, but the caller is not a user with an OIDC session. It is a **machine, acting for one specific run**, that needs a credential scoped to that run and no more.

Options considered: (a) a cloud-native identity — the function presents its managed identity / IAM role and the surface validates it against each cloud's IdP; rejected as vendor-specific (Azure AD vs AWS SigV4), coupling the surface to every cloud. (b) A single shared API key; rejected because it is not run-scoped — a leak compromises every callback. (c) A run-scoped, short-lived token the runner mints and the surface validates.

## Decision

**The function authenticates its checkpoint callback with a run-scoped, short-lived bearer token (`CheckpointToken`) the runner mints and the checkpoint surface validates.**

- **Shape.** The token is `{expiryUnixSeconds}.{base64url(HMAC-SHA256(secret, "environment:runId:expiry"))}` — a symmetric HMAC over the run's full `(environment, runId)` address and an expiry. (Originally the signed message was `"runId:expiry"`; the environment field was added under ADR 0065 decision 9, see the 2026-08-22 amendment, so a token minted for one environment's run does not validate for the same run id at another.) The address is *bound by the signature but not transmitted*, because the checkpoint endpoint already knows both halves from the request URL. It needs no cloud identity provider, and it is **opaque to the function**, which never interprets it — it only carries it.
- **Mint.** The runner's `ServerlessRunExecutionBackend` mints one per dispatch (an optional `checkpointTokenIssuer`) and writes it into the invocation as `checkpointToken`. When no issuer is configured, no token is carried and the surface is not token-authenticated (the existing behaviour).
- **Present.** `ServerlessInvocationHandler` reads `checkpointToken` from the invocation and sets it as `Authorization: Bearer` on the per-invocation checkpoint client, so it rides every load and save. The token is optional, so its absence is not an error.
- **Validate.** `MapWorkflowCheckpointEndpoints` takes an `authenticateCheckpointToken` delegate: `(address, token) => CheckpointToken.TryValidate(secret, token, address, now)`, where `address` is the `WorkflowRunAddress` built from the route's `{environment}` and `{runId}` (see the 2026-08-22 amendment). A request without a valid token is a `401`; validation checks the HMAC (in constant time) against the **URL's** address and the token's expiry, so a token minted for another run, or for the same run id at another environment, does not validate on this one. It composes with — and is independent of — the ambient `requireAuthorization`. (This delegate was optional as originally shipped, which is what left the mechanism inert; it is required as of the 2026-08-07 amendment below.)

The token binds the run via the URL rather than transmitting a claim, so it is the minimal credential: a valid token proves only "the runner authorised checkpoints for *this* run, until *this* time".

## Consequences

- **Blast radius is one run, briefly.** A leaked token authenticates only its own run and only until it expires. It cannot be replayed against a different run, nor against the same run id at another environment (the signature is over the full `(environment, runId)` address), nor after expiry.
- **Vendor-neutral and self-contained.** Symmetric HMAC means no dependency on a cloud IdP or a JWT library, and the token validates in-endpoint with no auth-middleware registration, so the same mechanism works for any serverless vendor and for a purpose-built listener host.
- **The function stays a dumb carrier.** It holds no signing key and interprets nothing — it copies an opaque string from the invocation to a header. Only the runner (mint) and the checkpoint surface (validate) know the secret, and in the common topology they are the same host, so the secret need not travel.
- **Turning it on is a host-wiring choice.** Configure the issuer on the backend and the authenticator on the surface with a shared secret. The authenticator was optional when this was introduced, which is what left the mechanism inert; the amendment below makes it required.
- **It is the prerequisite for a public checkpoint listener.** With this, the checkpoint surface can be exposed to a real cloud function (or a scale-to-zero listener that stands in for the runner in a CI run-to-completion test) without being an open write endpoint. Extending the token to a run-scoped OIDC/JWT credential, or moving the shared secret into the environment's key custody (as the executor signing keys are, ADR 0059), is a later refinement this shape does not preclude.

## Security review (2026-07-29)

An adversarial review confirmed the token primitive is sound: without the secret it could not be forged, cross-run-reused, timing-attacked, or made malleable. (Comparing the canonical base64url signature *text* rather than decoded bytes neutralises base64 malleability; HMAC is not length-extendable; and the signed message has a unique decomposition because the validator supplies every field independently and the expiry is a canonical decimal. This review was of the original `{runId}:{expiry}` message; under the composite address the message is `{environment}:{runId}:{expiry}` (see the 2026-08-22 amendment), and its decomposition stays unique because the environment-name grammar admits no colon, so the first colon ends the environment, the run-id grammar admits no colon, and the canonical-decimal expiry is the suffix after the final colon.) It surfaced the following, which shape how the mechanism must be used.

- **The mechanism must be wired to have any effect.** As introduced it was capability-only: the token issuer and authenticator were optional, and the shipped call sites (the production `MapArazzoControlPlane`, the demo runner, the local gates) passed neither, so the checkpoint surface was either Open (development only, `ControlPlaneSecurityMode.Open`) or required an ambient OIDC principal that a machine callback cannot present. The token authenticated nothing anywhere but the public listener. **This is now closed** — see the amendment below.
- **Hardening applied.** `Issue`/`TryValidate` reject a secret shorter than `MinimumSecretBytes` (256 bits), so a weak key is caught at mint; the expiry must be canonical (no sign, whitespace, or leading zeros), so exactly one token string authenticates a run; and the function refuses to send a token over a non-HTTPS checkpoint URL (loopback exempt), so a bearer credential never crosses the internet in cleartext.
- **Deployment obligations and residual risks.** The token is a bearer credential: it rides the invocation payload (keep it out of the cloud platform's invocation logs) and the callback channel (HTTPS, now enforced), and within its lifetime it can be replayed — mitigated by a short lifetime (which must still exceed a single invocation's duration, or checkpoints mid-run start failing) and, for saves, by the monotonic write-sequence that drops a replayed or stale checkpoint. The shared secret's custody, entropy source, and rotation (a key id enabling old+new during a roll) are the deploying host's responsibility; a validator-side maximum-lifetime ceiling and moving the secret into the environment's key custody (ADR 0059) are refinements this shape does not preclude.

## Amendment (2026-08-22): the token binds the full `(environment, runId)` address

ADR 0065 decision 9 made the run's primary key the composite `(environment, runId)` at every ingress and in every backend (see the H18 closure in the threat model). The checkpoint token and surface were re-cut to match, so this ADR's original run-id-only shape is superseded as follows:

- The signed message is `"environment:runId:expiry"` (was `"runId:expiry"`), so a token minted for one environment's run does not validate for the same run id at another environment.
- The checkpoint surface route is `/environments/{environment}/runs/{runId}/checkpoint` (was `/runs/{runId}/checkpoint`), and both halves are grammar-validated before the token is checked.
- `CheckpointToken.Issue`/`TryValidate` take an `in WorkflowRunAddress`, and the `authenticateCheckpointToken` delegate is `(address, token) => CheckpointToken.TryValidate(secret, token, address, now)`.

Everything else about the mechanism (symmetric HMAC, bearer semantics, blast radius, HTTPS enforcement, the required-authenticator amendment below) is unchanged; the address simply replaces the run id everywhere the run id was bound.

## Deployment: the public listener on Azure Container Apps (2026-07-29)

The public checkpoint listener (`Corvus.Text.Json.Arazzo.ServerlessCheckpointListener`) is the host that wires this mechanism: it maps `MapWorkflowCheckpointEndpoints` with a `CheckpointToken.TryValidate` authenticator over a shared Azure Storage run store, plus the workflow's `echo` source. It is proven live on **Azure Container Apps** (the reachability chosen over a dev tunnel: a real, scale-to-zero public HTTPS surface). The hard-won specifics of that deploy, so a run-to-completion gate can stand it up and tear it down at zero cost between runs:

- **Provider registration.** Container Apps needs `Microsoft.App` **and** `Microsoft.OperationalInsights` registered on the subscription (`az provider register -n Microsoft.App`). `Microsoft.App` is not registered by default; registration is asynchronous (a couple of minutes) and must complete before `containerapp env create`.
- **The image is a pre-published context, not a source build.** The listener `Dockerfile` stages a framework-dependent published app onto `mcr.microsoft.com/dotnet/aspnet:10.0` (`COPY . ./` over the publish directory) — it runs no `dotnet publish` itself. So the build context is the publish output: `dotnet publish` locally, then `podman build -f <Dockerfile> <publishDir>` and push to ACR (admin-enabled, `podman login` with `az acr credential show`), or point `az acr build` at the publish directory. A source-directory build will not contain the app.
- **Container App configuration.** External ingress on target port **8080** (`ASPNETCORE_URLS=http://+:8080`), `--min-replicas 0` (scale-to-zero, so idle cost is ~0), and the sensitive configuration — the storage connection string and the base64 checkpoint secret — supplied as Container App **secrets** referenced by env (`ARAZZO_CHECKPOINT_STORAGE=secretref:storage-conn`, `ARAZZO_CHECKPOINT_SECRET=secretref:checkpoint-secret`), never as plaintext env values. No endpoint, secret, or storage identifier is baked into source; they arrive only through the environment.
- **Teardown must include the auto-created workspace.** `containerapp env create` auto-creates a Log Analytics workspace alongside the managed environment; deleting the environment can leave it behind (the same class of trap as `functionapp create` leaving its plan and Application Insights). A zero-cost teardown deletes the Container App, the managed environment, that Log Analytics workspace, the ACR, and the storage account.
- **Cold start is expected.** Scale-to-zero means the first request cold-starts (a few seconds); a reachability or invoke poll must tolerate the initial non-`200`.
- **Reachability proof.** Over public HTTPS the deployed listener answers `GET /health` `200`, `GET /demo/echo` `{"status":"ok"}`, a no-token checkpoint request `401`, a valid-token request `404` (the token validated and the store was queried — no checkpoint yet), and a token minted for a *different* run `401` (run-scoping holds over the internet). The valid-token `404` proves the deployed container reached the real Azure Storage account, so the surface is a genuine token-authenticated public endpoint, not an open write endpoint.

The run-to-completion gates then share that listener's store: each seeds a Pending run into the same Azure Storage account, dispatches a real function with a run-scoped token and the listener as `checkpointUrl` and `echo` source, and reads the run back `Completed` from that store — the deployed worker having authenticated every checkpoint callback to the public listener, which terminated them into the shared store. Three gates prove the mechanism is vendor-neutral over the one listener: `ServerlessRealCloudCheckpointListenerTests` (a local Azure Functions runtime image), `ArmFunctionAppLiveDeployTests` (a real Flex Consumption app that also tears itself down), and `ServerlessRealCloudCheckpointListenerLambdaTests` (an AWS Lambda under LocalStack). All three were verified live against the deployed listener.


## Amendment: the token is required, not optional (2026-08-07)

A security audit against the [threat model](../reference/threat-model.md) recorded finding **H1**: this
mechanism was implemented, sound, and applied on exactly one of five call sites. `MapArazzoControlPlane`,
the serverless runner demo, and both local execution gates all mapped the checkpoint surface with no
authenticator, so it fell through to the host's ambient authorization. That admits any authenticated
caller to every run in the deployment, and in `ControlPlaneSecurityMode.Open` admits everyone. The one
correctly wired site was the public listener, which is the pattern the audit calls a mitigation applied
to one of two sibling paths.

The capability-only shape is what allowed it. So the shape changes.

- **`authenticateCheckpointToken` is a required parameter of `MapWorkflowCheckpointEndpoints`.** A host
  cannot map this surface without deciding how a callback proves which run it may touch. That
  forecloses the whole class rather than the four instances of it.
- **`MapArazzoControlPlane` takes a `checkpointSecret`, and maps no checkpoint surface without one.**
  Absent rather than open, which is the [ADR 0016](0016-control-plane-security-mode.md) posture. A
  deployment that dispatches no out-of-process runs needs no such surface, and one that does must say
  so by configuring the secret. A secret below `CheckpointToken.MinimumSecretBytes` fails the mapping
  rather than the first callback.
- **The token is this surface's reach gate, and no reach or lease check is added alongside it.** The
  audit proposed gating on reach and lease as the runner API twin does. That does not fit: the caller
  is a dispatched function holding no principal to derive reach from and no lease, which is the exact
  case this ADR exists to answer. Requiring a lease here would refuse every legitimate callback. What
  bounds the caller is that the token names one run and expires.
- **Ambient authorization still composes with it.** The two answer different questions: the principal
  says a caller belongs to the deployment, the token says which run it may read and overwrite.

The residual risk is unchanged and is the one this shape accepts: a bearer token is replayable within
its lifetime, bounded to the run it names.

Separately, [ADR 0065](0065-control-plane-owns-store-runners-encrypt-payload.md) decision 6 requires the
per-run single-flight interlock to be **per run, not per component**. The coordinator holds that
interlock in memory, and a host mapping both the checkpoint surface and the runner API built one each,
which is precisely a lock held per component. Both map methods now take the host's coordinator.