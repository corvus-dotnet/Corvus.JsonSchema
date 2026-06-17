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
    $"vault token create -id={vaultRunnerReadOnlyToken} -policy=arazzo-runner-ro -period=24h; " +
    "vault kv put secret/arazzo/onboarding api-key=demo-onboarding-key; " +
    "vault kv put secret/arazzo/ledger api-key=demo-ledger-key; " +
    "echo provisioning-complete";

var vaultInit = builder.AddContainer("vault-init", "hashicorp/vault", "1.18")
    .WithEnvironment("VAULT_ADDR", vault.GetEndpoint("http"))
    .WithEnvironment("VAULT_TOKEN", vaultRootToken)
    .WithEntrypoint("/bin/sh")
    .WithArgs("-c", provisionScript)
    .WaitFor(vault);

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
    .WithReference(keycloak)
    .WaitFor(keycloak)
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
    .WithReference(controlplane)
    .WaitFor(controlplane)
    .WaitForCompletion(vaultInit)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
