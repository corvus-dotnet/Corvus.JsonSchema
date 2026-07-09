// <copyright file="AppHost.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// The Aspire composition root for the Arazzo control-plane demo. This is the *real* host wiring: each piece is
// a first-class resource so the Aspire dashboard becomes the OpenTelemetry viewer (traces/logs/metrics) and the
// single launch point. It composes the two-process topology (control plane + runner) plus a locally-runnable
// HashiCorp Vault for source credentials (design §13); Keycloak for OIDC and optionally Postgres join here next.
IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// The shared durability store. SQLite is the local stand-in for the production shared store (becomes an
// AddPostgres resource later); injecting one connection string into both processes is how they share it — the
// exact shape that generalizes to a real database. The control plane owns reset + seed; the runner reads + claims.
string sharedStore = $"Data Source={Path.Combine(Path.GetTempPath(), "arazzo-demo-shared.db")}";

// Dev-only fixed Vault tokens — the locally-runnable stand-in for production secret-store auth (design §13.5.1).
// In prod the runner's identity comes from platform attestation / AppRole (never a token baked into the workload),
// and provisioning runs as a CI/IaC identity. Here the orchestrator delivers a fixed dev root token to the
// write-capable provisioner and a separate fixed read-only token to the runner. The security-critical split —
// provisioner writes, runner reads (path-scoped) — is preserved exactly.
const string vaultRootToken = "arazzo-dev-root-token";
const string vaultRunnerReadOnlyToken = "arazzo-runner-ro-token";

// HashiCorp Vault, dev mode: unsealed, in-memory (fresh each run), KV v2 mounted at secret/. The secret *store*.
var vault = builder.AddContainer("vault", "hashicorp/vault", "1.18")
    .WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", vaultRootToken)
    .WithEnvironment("VAULT_DEV_LISTEN_ADDRESS", "0.0.0.0:8200")
    .WithArgs("server", "-dev")
    .WithHttpEndpoint(targetPort: 8200, name: "http");

// The provisioner: a one-shot Vault-CLI container — the *only* write-capable identity (the "CI/IaC provisioning
// step" stand-in, design §13.5.1). It writes a read-only, path-scoped policy, mints the runner's read-only token
// bound to it, seeds the demo secret values (dev dummies), then exits. The runner never has these privileges.
const string provisionScript =
    "set -e; " +
    "until vault status >/dev/null 2>&1; do echo 'waiting for vault'; sleep 1; done; " +
    "echo 'path \"secret/data/arazzo/*\" { capabilities = [\"read\"] }' | vault policy write arazzo-runner-ro -; " +
    // Idempotent: the orchestrator may run this one-shot provisioner more than once (a retry), and a fixed token id
    // cannot be created twice. Revoke any prior token first (a no-op on the first run) so the create always succeeds
    // with the current policy; the runner, which starts only after this completes, always gets a valid token.
    $"vault token revoke {vaultRunnerReadOnlyToken} 2>/dev/null || true; " +
    $"vault token create -id={vaultRunnerReadOnlyToken} -policy=arazzo-runner-ro -period=24h; " +
    "vault kv put secret/arazzo/onboarding api-key=demo-onboarding-key; " +
    "vault kv put secret/arazzo/ledger api-key=demo-ledger-key; " +
    "echo provisioning-complete; " +
    // Linger briefly after the work is done so the orchestrator observes the container reach 'Running' before it
    // exits. A sub-second exit is read as a start failure (FailedToStart) and retried; with this the container goes
    // Running -> exit 0 -> Finished, and WithHiddenOnCompletion(0) below then hides it as a completed init step.
    "sleep 15";

var vaultInit = builder.AddContainer("vault-init", "hashicorp/vault", "1.18")
    .WithEnvironment("VAULT_ADDR", vault.GetEndpoint("http"))
    .WithEnvironment("VAULT_TOKEN", vaultRootToken)
    .WithEntrypoint("/bin/sh")
    .WithArgs("-c", provisionScript)
    .WaitFor(vault)
    // This is a run-to-completion provisioner: it does its work and exits 0. Tell the orchestrator that exit 0 is a
    // valid terminal completion (not an unexpected stop to restart, nor a start failure) and hide it from the
    // dashboard once done — the standard init-container pattern. Without this the DCP treats the quick exit as
    // "failed to start" and retries it (harmless because the script is idempotent, but noisy and misleading).
    .WithHiddenOnCompletion(0);

// Keycloak — the identity provider (design §16). It is bootstrapped declaratively: the realm import seeds the
// `arazzo` realm with an `arazzo-admins` group (→ the first system admin, §16.2), demo domain groups, a seed
// admin + demo user, and the UI (auth-code+PKCE) and CLI (device-flow) OIDC clients with a `groups` claim mapper.
// Ephemeral (no data volume): the realm is re-imported fresh on each run, matching the demo's reset-each-run
// theme (the control plane wipes SQLite, Vault dev is in-memory) — predictable state, no volume drift.
var keycloak = builder.AddKeycloak("keycloak")
    .WithRealmImport("realms");

// The ASP.NET control-plane host: the real server surface (catalog, runs, credentials, administrators, security)
// plus the build-free web UI and the demo /svc backends. It stores credential *references* only (never binds to
// Vault — the §13 invariant), so it needs no Vault token. It references Keycloak as the OIDC authority for token
// validation (§16.3) and waits for the realm import. Externally reachable; OTel flows to the dashboard.
var controlplane = builder.AddProject<Projects.Corvus_Text_Json_Arazzo_ControlPlane_Demo>("controlplane")
    .WithEnvironment("ConnectionStrings__workflowstore", sharedStore)
    // §18 multi-process: a SEPARATE runner process hosts $draft debug runs, so the control plane must NOT run its own
    // in-process draft pump (else both would claim the same runs). The control plane only MARKS runs claimable.
    .WithEnvironment("ControlPlane__HostDraftRunnerInProcess", "false")
    .WithReference(keycloak)
    .WaitFor(keycloak)
    // Aspire-managed HTTP endpoint (no hardcoded port): Aspire assigns the port, proxies it, and injects
    // ASPNETCORE_URLS so the app binds what Aspire chose — this is what makes the health check and the runner's
    // WithReference/GetEndpoint resolve to the real address without launchSettings pinning a fixed port.
    .WithHttpEndpoint()
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

// The runner ("execution-host") — the second process in the topology. It shares the store, registers in the
// runner registry, and claims/resumes runs (design §5/§7). It is the §13 secret *consumer*: it holds ONLY the
// read-only Vault token (least privilege), waits for provisioning to finish, and resolves credentials at bind
// time. It waits for the control plane to seed the store + holds a reference for the future /svc executor calls.
builder.AddProject<Projects.Corvus_Text_Json_Arazzo_Runner_Demo>("runner")
    .WithEnvironment("ConnectionStrings__workflowstore", sharedStore)
    .WithEnvironment("VAULT_ADDR", vault.GetEndpoint("http"))
    .WithEnvironment("VAULT_TOKEN", vaultRunnerReadOnlyToken)
    // §18 multi-process: this runner hosts the development-environment $draft debug runs the control plane marks,
    // executing each against the control plane's own /svc source backends (the demo stand-in for real endpoints).
    .WithEnvironment("Runner__Environment", "development")
    .WithEnvironment("Runner__SourcesBaseUrl", controlplane.GetEndpoint("http"))
    .WithReference(controlplane)
    .WaitFor(controlplane)
    // Depend on Vault being reachable, not on the DCP detecting the one-shot provisioner's exit. The runner resolves
    // credentials resiliently at bind time (and its startup self-check tolerates a not-yet-provisioned token), so it
    // does not need to hard-block on vault-init's completion — which is also more robust than coupling a long-running
    // worker to a run-once container's terminal signal. (vault-init still runs and provisions; it just isn't a gate.)
    .WaitFor(vault)
    // Aspire-managed HTTP endpoint (no hardcoded port). The runner is an internal worker, so Aspire proxies this
    // endpoint; letting Aspire assign the port (rather than the old launchSettings applicationUrl=5280) is what stops
    // the app from binding the same port as the DCP proxy — the collision that was crashing the runner on startup.
    .WithHttpEndpoint()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
