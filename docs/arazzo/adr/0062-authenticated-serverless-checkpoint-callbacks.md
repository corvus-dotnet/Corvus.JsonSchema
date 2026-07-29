# ADR 0062. Authenticated serverless checkpoint callbacks use a run-scoped bearer token

Date: 2026-07-29. Status: **Accepted**. Scope: how a deployed serverless function authenticates its checkpoint callback to the runner's checkpoint surface, so that surface can be exposed beyond a private network (for example to a real cloud function) without being an open write endpoint. Builds on the serverless backend design ([ADR 0055](0055-serverless-backend-aot-from-signed-executor.md)) and the runner-as-deploy-boundary model ([ADR 0059](0059-serverless-deploy-runs-on-the-runner-as-the-secure-boundary.md)).

## Context

A serverless run advances out of process: the runner invokes the baked function, which restores the run from its checkpoint, advances it, and checkpoints it back over HTTP to the runner's checkpoint surface — `GET`/`POST /runs/{runId}/checkpoint` (`WorkflowCheckpointEndpoints`), which the function-side `HttpWorkflowStateStore` speaks. The invocation carries `{ runId, environment, checkpointUrl }`.

Today that round-trip is **unauthenticated at the credential level**. The checkpoint surface can require an *ambient authenticated principal* (`MapWorkflowCheckpointEndpoints`'s `requireAuthorization` calls `.RequireAuthorization()` with the host's auth scheme), but:

- the function-side `HttpWorkflowStateStore` sends **no** credential on any request, and
- the invocation carries **no** token.

So the surface only works unauthenticated (as the local container gates run it) or would reject the function outright if ambient auth were required. For the checkpoint surface to be **publicly reachable** — which a real cloud function's callback requires, and which the run-to-completion-in-CI test harness needs — it must be authenticated, but the caller is not a user with an OIDC session. It is a **machine, acting for one specific run**, that needs a credential scoped to that run and no more.

Options considered: (a) a cloud-native identity — the function presents its managed identity / IAM role and the surface validates it against each cloud's IdP; rejected as vendor-specific (Azure AD vs AWS SigV4), coupling the surface to every cloud. (b) A single shared API key; rejected because it is not run-scoped — a leak compromises every callback. (c) A run-scoped, short-lived token the runner mints and the surface validates.

## Decision

**The function authenticates its checkpoint callback with a run-scoped, short-lived bearer token (`CheckpointToken`) the runner mints and the checkpoint surface validates.**

- **Shape.** The token is `{expiryUnixSeconds}.{base64url(HMAC-SHA256(secret, "runId:expiry"))}` — a symmetric HMAC over the run id and an expiry. The run id is *bound by the signature but not transmitted*, because the checkpoint endpoint already knows it from the request URL. It needs no cloud identity provider, and it is **opaque to the function**, which never interprets it — it only carries it.
- **Mint.** The runner's `ServerlessRunExecutionBackend` mints one per dispatch (an optional `checkpointTokenIssuer`) and writes it into the invocation as `checkpointToken`. When no issuer is configured, no token is carried and the surface is not token-authenticated (the existing behaviour).
- **Present.** `ServerlessInvocationHandler` reads `checkpointToken` from the invocation and sets it as `Authorization: Bearer` on the per-invocation checkpoint client, so it rides every load and save. The token is optional, so its absence is not an error.
- **Validate.** `MapWorkflowCheckpointEndpoints` takes an optional `authenticateCheckpointToken` delegate: `(runId, token) => CheckpointToken.TryValidate(secret, token, runId, now)`. When supplied, a request without a valid token is a `401`; validation checks the HMAC (in constant time) against the **URL's** run id and the token's expiry, so a token minted for another run does not validate on this one. It composes with — and is independent of — the ambient `requireAuthorization`.

The token binds the run via the URL rather than transmitting a claim, so it is the minimal credential: a valid token proves only "the runner authorised checkpoints for *this* run, until *this* time".

## Consequences

- **Blast radius is one run, briefly.** A leaked token authenticates only its own run and only until it expires. It cannot be replayed against a different run (the signature is over the run id) nor after expiry.
- **Vendor-neutral and self-contained.** Symmetric HMAC means no dependency on a cloud IdP or a JWT library, and the token validates in-endpoint with no auth-middleware registration, so the same mechanism works for any serverless vendor and for a purpose-built listener host.
- **The function stays a dumb carrier.** It holds no signing key and interprets nothing — it copies an opaque string from the invocation to a header. Only the runner (mint) and the checkpoint surface (validate) know the secret, and in the common topology they are the same host, so the secret need not travel.
- **Additive and backward-compatible.** Every new parameter is optional; the existing call sites (`MapArazzoControlPlane`, the demo host, the local gates) pass nothing and behave exactly as before. Turning the feature on is a host-wiring choice: configure the issuer on the backend and the authenticator on the surface with a shared secret.
- **It is the prerequisite for a public checkpoint listener.** With this, the checkpoint surface can be exposed to a real cloud function (or a scale-to-zero listener that stands in for the runner in a CI run-to-completion test) without being an open write endpoint. Extending the token to a run-scoped OIDC/JWT credential, or moving the shared secret into the environment's key custody (as the executor signing keys are, ADR 0059), is a later refinement this shape does not preclude.
