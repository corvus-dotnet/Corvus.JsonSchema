// <copyright file="AppHost.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// The Aspire composition root for the Arazzo control-plane demo. This is the *real* host wiring: each piece is
// a first-class resource so the Aspire dashboard becomes the OpenTelemetry viewer (traces/logs/metrics) and the
// single launch point. It composes the two-process topology (control plane + runner) plus a locally-runnable
// HashiCorp Vault for source credentials (design §13); Keycloak for OIDC and optionally Postgres join here next.
IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// The shared durability store — a real Postgres database. AddPostgres stands up a Postgres server container;
// AddDatabase declares the shared 'workflowstore' database both processes open (its name IS the connection-string
// name the apps look up, so no app-side config-key change is needed). Ephemeral (no data volume): a fresh empty
// database each run — the reset-each-run theme without a wipe. The control plane owns the schema (each store's
// PrepareAsync) + seed; the runner reads + claims. WithReference injects ConnectionStrings__workflowstore (the Npgsql
// connection string) into each process, and WaitFor gates each on the database being healthy.
var postgres = builder.AddPostgres("postgres");
var workflowstore = postgres.AddDatabase("workflowstore");

// The onboarding service's OWN database — a dedicated Postgres instance (the microservice-owns-its-data pattern: the
// onboarding backend owns its schema + data independently of the control plane's shared workflowstore). Ephemeral,
// like the workflowstore. The onboarding service provisions its schema (its own PrepareAsync) on startup.
var onboardingPostgres = builder.AddPostgres("onboarding-postgres");
var onboardingDb = onboardingPostgres.AddDatabase("onboardingdb");

// The ledger service's OWN database — likewise a dedicated Postgres instance. The ledger service provisions its schema
// and seeds its account book (its own PrepareAsync) on startup.
var ledgerPostgres = builder.AddPostgres("ledger-postgres");
var ledgerDb = ledgerPostgres.AddDatabase("ledgerdb");

// The KYC service's OWN database — likewise a dedicated Postgres instance. The KYC service owns identity-verification
// records (the synchronous verifyIdentity step persists a record here; the manual-recovery verdict updates it). It
// provisions its schema (its own PrepareAsync) on startup.
var kycPostgres = builder.AddPostgres("kyc-postgres");
var kycDb = kycPostgres.AddDatabase("kycdb");

// Vault secure introduction (design §13.5.1). Provisioning runs as a CI/IaC identity (here, a fixed dev root token on
// the one-shot provisioner). The runner does NOT get a pre-minted token: it authenticates via Vault AppRole and
// receives a dynamically-issued, short-TTL token. In production the runner's identity (its AppRole SecretID, or a
// cloud/Kubernetes workload identity) is supplied by the runtime host's service principal / platform attestation;
// THIS SAMPLE SIMULATES that by having the trusted orchestrator provision an AppRole and hand the runner a
// response-wrapped SecretID (see web/../samples README). The security-critical split — provisioner writes, runner
// reads (path-scoped) — is preserved exactly.
const string vaultRootToken = "arazzo-dev-root-token";

// The runner's AppRole RoleID — non-secret (like a username), so the orchestrator delivers it as plain config. The
// SecretID is NOT here: the provisioner generates a response-wrapped SecretID at provision time and writes the
// single-use wrapping token to a shared handoff dir; the runner unwraps it (never seeing the SecretID in plaintext).
const string runnerRoleId = "arazzo-runner-approle";

// The handoff directory for the response-wrapped SecretID: the provisioner (vault-init) writes the single-use wrapping
// token here and the runner reads + unwraps it. It stands in for the runtime host delivering the workload's identity.
// World-writable so the rootless-podman container user can write into it (the bind-mounted host dir is owned by the
// host user); the provisioner chmod 644s the file so the host-side runner process can read it. Ephemeral per run.
string vaultHandoffDir = Path.Combine(Path.GetTempPath(), "arazzo-vault-approle-" + Guid.NewGuid().ToString("N")[..12]);
Directory.CreateDirectory(vaultHandoffDir);
if (!OperatingSystem.IsWindows())
{
    // POSIX permissions only apply to the rootless-podman (Linux) host this sample targets; a no-op elsewhere.
    File.SetUnixFileMode(
        vaultHandoffDir,
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
}
string vaultWrapTokenPath = Path.Combine(vaultHandoffDir, "secretid.wrap");

// HashiCorp Vault, dev mode: unsealed, in-memory (fresh each run), KV v2 mounted at secret/. The secret *store*.
var vault = builder.AddContainer("vault", "hashicorp/vault", "1.18")
    .WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", vaultRootToken)
    .WithEnvironment("VAULT_DEV_LISTEN_ADDRESS", "0.0.0.0:8200")
    .WithArgs("server", "-dev")
    .WithHttpEndpoint(targetPort: 8200, name: "http");

// The application-owned message bus — NATS with JetStream (durable, file-backed) so a message published before its
// consumer subscribes is not lost. It carries the async KYC exchange (design §8): the onboard-customer-async workflow
// publishes a review request to kyc.requests and suspends awaiting a verdict on kyc.verdict; the KYC service is the
// manual-recovery inbox (consumes requests) + verdict publisher, and the runner subscribes to kyc.verdict to resume
// the suspended run. Owned by the application, NOT the control plane. Ephemeral (no volume), like the rest of the demo.
var nats = builder.AddContainer("nats", "nats", "2.10")
    .WithArgs("-js")
    .WithEndpoint(targetPort: 4222, scheme: "nats", name: "nats");

// The provisioner: a one-shot Vault-CLI container — the *only* write-capable identity (the "CI/IaC provisioning
// step" stand-in, design §13.5.1). It writes a read-only, path-scoped policy, mints the runner's read-only token
// bound to it, seeds the demo secret values (dev dummies), then exits. The runner never has these privileges.
const string provisionScript =
    "set -e; " +
    "until vault status >/dev/null 2>&1; do echo 'waiting for vault'; sleep 1; done; " +
    "echo 'path \"secret/data/arazzo/*\" { capabilities = [\"read\"] }' | vault policy write arazzo-runner-ro -; " +
    // Enable the AppRole auth method (idempotent) and (re)create the runner's role bound to the read-only policy. The
    // TOKEN is short-TTL + renewable (the short-lived-credential property, design §13.5.1). The SecretID is left
    // session-lived (ttl/num_uses unlimited) so VaultSharp can transparently re-login when the token expires over the
    // demo's lifetime; in production the platform would rotate the SecretID.
    "vault auth enable approle 2>/dev/null || true; " +
    "vault write auth/approle/role/arazzo-runner token_policies=arazzo-runner-ro token_ttl=20m token_max_ttl=2h secret_id_ttl=0 secret_id_num_uses=0; " +
    // Pin a fixed, non-secret RoleID (like a username) the orchestrator injects to the runner as plain config.
    $"vault write auth/approle/role/arazzo-runner/role-id role_id={runnerRoleId}; " +
    // Generate a RESPONSE-WRAPPED SecretID: the SecretID never leaves Vault in plaintext — only a single-use wrapping
    // token does. Write it to the shared handoff dir for the runner to unwrap. The wrap-ttl must comfortably exceed the
    // gap until the runner reads it: the runner starts only after the control plane is healthy (Keycloak realm import,
    // ~5 min), so a short wrap-ttl would expire first. Idempotent on a retry (upsert role; the file is overwritten).
    "vault write -f -wrap-ttl=1800s -field=wrapping_token auth/approle/role/arazzo-runner/secret-id > /shared/secretid.wrap; " +
    "chmod 644 /shared/secretid.wrap; " +
    "vault kv put secret/arazzo/onboarding api-key=demo-onboarding-key; " +
    "vault kv put secret/arazzo/ledger api-key=demo-ledger-key; " +
    "vault kv put secret/arazzo/kyc api-key=demo-kyc-key; " +
    "echo provisioning-complete; " +
    // Linger briefly after the work is done so the orchestrator observes the container reach 'Running' before it
    // exits. A sub-second exit is read as a start failure (FailedToStart) and retried; with this the container goes
    // Running -> exit 0 -> Finished, and WithHiddenOnCompletion(0) below then hides it as a completed init step.
    "sleep 15";

var vaultInit = builder.AddContainer("vault-init", "hashicorp/vault", "1.18")
    .WithEnvironment("VAULT_ADDR", vault.GetEndpoint("http"))
    .WithEnvironment("VAULT_TOKEN", vaultRootToken)
    // The response-wrapped SecretID handoff: the provisioner writes the single-use wrapping token to /shared, which is
    // the host dir the runner reads (its stand-in for a runtime host delivering the workload's identity).
    .WithBindMount(vaultHandoffDir, "/shared")
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

// The onboarding domain service — a real external source: its own process + its own database (above). It replaces the
// former inline /svc/onboarding mock; the control plane's startup live runs and the runner's executed runs both call
// it over the network. It provisions its own schema on startup and serves the generated onboarding API (the workflow's
// create/verify/provision/welcome ops, plus list/get for the onboarding console). Externally reachable for the console.
var onboarding = builder.AddProject<Projects.Corvus_Text_Json_Arazzo_Samples_Onboarding_Host>("onboarding")
    .WithReference(onboardingDb)
    .WaitFor(onboardingDb)
    .WithHttpEndpoint()
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

// The ledger/reconciliation domain service — the second real external source (its own process + its own database). It
// replaces the former inline /svc/ledger mock; the control plane's startup live runs and the runner's executed runs
// both call it over the network. It seeds its account book on startup and serves the generated ledger API (the
// nightly-reconcile workflow's ops, plus reconciliation/account reads). Externally reachable for a future console.
var ledger = builder.AddProject<Projects.Corvus_Text_Json_Arazzo_Samples_Ledger_Host>("ledger")
    .WithReference(ledgerDb)
    .WaitFor(ledgerDb)
    .WithHttpEndpoint()
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

// The KYC domain service — the third real external source (its own process + its own database). It owns ALL identity
// verification: the workflow's synchronous verifyIdentity step (createAccount@onboarding -> verifyIdentity@kyc ->
// provision@onboarding), and later the manual-recovery verdict published onto the bus (design §8). It provisions its
// own schema on startup and serves the generated KYC API (verifyIdentity plus list/get for the manual-recovery
// console). Externally reachable for that console.
var kyc = builder.AddProject<Projects.Corvus_Text_Json_Arazzo_Samples_Kyc_Host>("kyc")
    .WithReference(kycDb)
    .WaitFor(kycDb)
    // The KYC service is both sides of the async exchange: it consumes review requests (kyc.requests) and publishes
    // verdicts (kyc.verdict) on the application bus.
    .WithEnvironment("Nats__Url", nats.GetEndpoint("nats"))
    .WaitFor(nats)
    .WithHttpEndpoint()
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

// The ASP.NET control-plane host: the real server surface (catalog, runs, credentials, administrators, security)
// plus the build-free web UI and the demo /svc backends. It stores credential *references* only (never binds to
// Vault — the §13 invariant), so it needs no Vault token. It references Keycloak as the OIDC authority for token
// validation (§16.3) and waits for the realm import. Externally reachable; OTel flows to the dashboard.
var controlplane = builder.AddProject<Projects.Corvus_Text_Json_Arazzo_ControlPlane_Demo>("controlplane")
    .WithReference(workflowstore)
    .WaitFor(workflowstore)
    // §18 multi-process: a SEPARATE runner process hosts $draft debug runs, so the control plane must NOT run its own
    // in-process draft pump (else both would claim the same runs). The control plane only MARKS runs claimable.
    .WithEnvironment("ControlPlane__HostDraftRunnerInProcess", "false")
    // §14.1/§16: ENFORCE authentication + row security. This activates the whole (already-built) auth stack — Keycloak
    // JWT-bearer + BFF cookie/OIDC + dev-API-key, ControlPlaneSecurityMode.Scoped, and the per-row reach policy — so the
    // API is no longer anonymous. The first administrator is bootstrapped declaratively (§16.2): the Keycloak realm
    // import seeds the arazzo-admins group + seed admin, and the control plane's tier-3 deployment-policy grant maps that
    // group to the service operator. Everyone else earns reach through the §16.5 access-request -> approval flow.
    .WithEnvironment("ControlPlane__RequireAuthorization", "true")
    .WithReference(keycloak)
    .WaitFor(keycloak)
    // Onboarding, ledger, and kyc are real external sources: inject their endpoints so the control plane's live-
    // execution transports route those sources there (its startup live runs call them), and wait for them to be healthy.
    .WithEnvironment("ControlPlane__Sources__Onboarding", onboarding.GetEndpoint("http"))
    .WithEnvironment("ControlPlane__Sources__Ledger", ledger.GetEndpoint("http"))
    .WithEnvironment("ControlPlane__Sources__Kyc", kyc.GetEndpoint("http"))
    // The control plane's live resumer executes the seeded async run, whose send step publishes to the bus.
    .WithEnvironment("Nats__Url", nats.GetEndpoint("nats"))
    .WaitFor(onboarding)
    .WaitFor(ledger)
    .WaitFor(kyc)
    .WaitFor(nats)
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
    .WithReference(workflowstore)
    .WaitFor(workflowstore)
    .WithEnvironment("VAULT_ADDR", vault.GetEndpoint("http"))
    // Secure introduction: NOT a pre-minted token. The runner gets its non-secret AppRole RoleID as plain config and
    // the PATH to the single-use wrapping token the provisioner left behind; it unwraps that to obtain the SecretID and
    // authenticates via AppRole for a dynamically-issued, short-TTL token. (In production the runtime host's service
    // principal / platform attestation supplies this; the sample simulates it — see the samples README.)
    .WithEnvironment("Runner__Vault__RoleId", runnerRoleId)
    .WithEnvironment("Runner__Vault__WrapTokenFile", vaultWrapTokenPath)
    // §18 multi-process: this runner hosts the development-environment $draft debug runs the control plane marks,
    // executing each against the control plane's own /svc source backends (the demo stand-in for real endpoints).
    .WithEnvironment("Runner__Environment", "development")
    // All source backends are real external services (their own processes + databases); the runner routes each at its
    // endpoint and waits for them (its executed runs call them). There is no /svc mock backend to point at any more.
    .WithEnvironment("Runner__Sources__Onboarding", onboarding.GetEndpoint("http"))
    .WithEnvironment("Runner__Sources__Ledger", ledger.GetEndpoint("http"))
    .WithEnvironment("Runner__Sources__Kyc", kyc.GetEndpoint("http"))
    // The runner subscribes to kyc.verdict to resume suspended async runs, and publishes review requests when it
    // executes an async run's send step.
    .WithEnvironment("Nats__Url", nats.GetEndpoint("nats"))
    .WithReference(controlplane)
    .WaitFor(controlplane)
    .WaitFor(onboarding)
    .WaitFor(ledger)
    .WaitFor(kyc)
    .WaitFor(nats)
    // Wait for the provisioner to FINISH, not just for Vault to be reachable. vault-init writes the read-only policy
    // and mints the runner's token; if the runner starts the moment Vault is up, its startup credential self-check can
    // beat that provisioning and get a 403 from Vault (a transient-but-ugly error trace). This is now safe to gate on:
    // vault-init reliably reaches Finished (it lingers briefly so the DCP observes Running, then WithHiddenOnCompletion
    // hides it) — the earlier reason we couldn't WaitForCompletion here (the one-shot never being seen as completed).
    .WaitForCompletion(vaultInit)
    // Aspire-managed HTTP endpoint (no hardcoded port). The runner is an internal worker, so Aspire proxies this
    // endpoint; letting Aspire assign the port (rather than the old launchSettings applicationUrl=5280) is what stops
    // the app from binding the same port as the DCP proxy — the collision that was crashing the runner on startup.
    .WithHttpEndpoint()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
