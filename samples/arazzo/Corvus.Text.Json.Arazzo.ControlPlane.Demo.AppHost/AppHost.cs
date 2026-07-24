// <copyright file="AppHost.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// The Aspire composition root for the Arazzo control-plane demo. This is the *real* host wiring: each piece is
// a first-class resource so the Aspire dashboard becomes the OpenTelemetry viewer (traces/logs/metrics) and the
// single launch point. It composes the two-process topology (control plane + runner) plus a locally-runnable
// HashiCorp Vault for source credentials (design §13), a SEPARATE HashiCorp Vault holding the control plane's
// executor-signing key (#879, the custody split), and Keycloak for OIDC.
IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// The single AppHost-level switch for the *example* seeding — the infrastructure counterpart to the runtime
// IExampleSeed / seedExampleData split (W4). When true (the demo default) the provisioner seeds the demo source
// secrets; a real deployment sets SeedExampleData=false and provisions only the real AppRole trust, then supplies its
// own source secrets from its secret-management. Read via the plain indexer (the config Binder's GetValue<T> extension
// is not in the AppHost's assembly set — same constraint as the GitHub-App read below): default true unless "false".
bool seedExampleData = !string.Equals(builder.Configuration["SeedExampleData"], "false", StringComparison.OrdinalIgnoreCase);

// Checkpoint protection key (§14 at rest, backlog #861): generated fresh per composition boot and handed to the
// control plane as configuration. The demo resets its data every run, so an ephemeral key is exactly right — a
// durable deployment sources this from its KMS/secret store instead (the same secure-introduction posture as the
// Vault AppRole trust above).
string checkpointProtectionKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

// Optional local, UNCOMMITTED GitHub App credentials for the designer's Git integration (workflow-designer §4.7 / D3).
// Copy github-app.local.json.example → github-app.local.json and fill in your own App (see the README); absent means
// the control plane brokers no App and the Git panel stays off. The client id is public; the secret never enters git.
// Read directly (not via Configuration.AddJsonFile — that extension is not in the AppHost's assembly set) into two
// locals the controlplane resource injects below as GitHubOAuth__ClientId (config) + GITHUB_OAUTH_CLIENT_SECRET (env).
string? githubClientId = null;
string? githubClientSecret = null;
string githubOAuthConfigPath = Path.Combine(builder.AppHostDirectory, "github-oauth.local.json");
if (File.Exists(githubOAuthConfigPath))
{
    using System.Text.Json.JsonDocument githubOAuthConfig = System.Text.Json.JsonDocument.Parse(File.ReadAllText(githubOAuthConfigPath));
    if (githubOAuthConfig.RootElement.TryGetProperty("GitHubOAuth", out System.Text.Json.JsonElement githubOAuthSection))
    {
        githubClientId = githubOAuthSection.TryGetProperty("ClientId", out System.Text.Json.JsonElement idElement) ? idElement.GetString() : null;
        githubClientSecret = githubOAuthSection.TryGetProperty("ClientSecret", out System.Text.Json.JsonElement secretElement) ? secretElement.GetString() : null;
    }
}

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

// The control-plane system runner is a SECOND runner process, so it needs its OWN response-wrapped SecretID: the
// wrapping token is single-use (unwrapping consumes it), so two runners cannot share one. Both bind to the same
// read-only AppRole (secret/arazzo/* covers secret/arazzo/controlplane, the credential the system runner resolves).
string vaultSystemWrapTokenPath = Path.Combine(vaultHandoffDir, "secretid-system.wrap");

// Executor-package signing (#879). A SEPARATE HashiCorp Vault — the control-plane SIGNING vault — holds the private key
// that signs each compiled executor's manifest at catalog-add. It is deliberately NOT the runner's credential vault
// above: the custody split is the whole point (a runner that verifies with only the public key can never forge a
// package). The control plane reaches this vault to sign; the runner never does. Dev mode, in-memory, fresh each run.
const string signingVaultRootToken = "arazzo-signing-dev-root-token";

// The Transit key that signs, and the id both sides agree on: the control plane stamps it into each signature, the
// runner selects the trusted public key by it. Same value keeps the demo simple.
const string signingKeyName = "arazzo-executor-signing";

// The control plane's SIGN-ONLY token: a fixed id the provisioner (root) mints against a policy allowing only
// transit/sign on the key — least privilege, so the signer cannot administer the vault. Injected to the control plane.
const string signingControlPlaneToken = "arazzo-cp-signing-token";

// The public-key handoff: the provisioner exports the Transit key's PUBLIC half to a file here, and the runner reads it
// into its trust store at startup. This is the demo's stand-in for how a real runner gets its trust anchor — deployment
// configuration the platform provisions (a mounted ConfigMap / a file IaC drops), obtained out-of-band from the signing
// authority, NEVER by the runner contacting the signing vault. The public key is not secret, so no wrapping/handoff
// ceremony (unlike the AppRole SecretID above) — just a config file. World-writable dir for the rootless-podman writer.
string signingHandoffDir = Path.Combine(Path.GetTempPath(), "arazzo-signing-" + Guid.NewGuid().ToString("N")[..12]);
Directory.CreateDirectory(signingHandoffDir);
if (!OperatingSystem.IsWindows())
{
    File.SetUnixFileMode(
        signingHandoffDir,
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
}
string signingPublicKeyPath = Path.Combine(signingHandoffDir, "executor-signing.pub");

// HashiCorp Vault, dev mode: unsealed, in-memory (fresh each run), KV v2 mounted at secret/. The secret *store*.
var vault = builder.AddContainer("vault", "hashicorp/vault", "1.18")
    .WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", vaultRootToken)
    .WithEnvironment("VAULT_DEV_LISTEN_ADDRESS", "0.0.0.0:8200")
    .WithArgs("server", "-dev")
    .WithHttpEndpoint(targetPort: 8200, name: "http");

// The control-plane SIGNING vault (#879) — a second, independent dev-mode Vault. Its Transit engine (enabled by the
// signing-vault-init provisioner below) holds the executor-signing key; the control plane signs against it, the runner
// never touches it. Kept separate from the runner's credential `vault` above so the custody split is real infrastructure.
var signingVault = builder.AddContainer("signing-vault", "hashicorp/vault", "1.18")
    .WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", signingVaultRootToken)
    .WithEnvironment("VAULT_DEV_LISTEN_ADDRESS", "0.0.0.0:8200")
    .WithArgs("server", "-dev")
    .WithHttpEndpoint(targetPort: 8200, name: "http");

// The application-owned message bus — NATS with JetStream (durable, file-backed) so a message published before its
// consumer subscribes is not lost. It carries the async KYC exchange (design §8): the onboard-customer-async workflow
// publishes a review request to kyc.requests and suspends awaiting a verdict on kyc.verdict; the KYC service is the
// manual-recovery inbox (consumes requests) + verdict publisher, and the runner subscribes to kyc.verdict to resume
// the suspended run. Owned by the application, NOT the control plane. Ephemeral (no volume), like the rest of the demo.
// Broker authentication is ON (ADR 0051): every connection presents the deployment token in its CONNECT
// handshake — the hosts get it as Nats__Token config, and the runners' channel credentials reference the same
// value through Vault (secret/arazzo/<source>#token), so the §13 channel bindings are real, not theater.
const string natsToken = "demo-nats-token";
var nats = builder.AddContainer("nats", "nats", "2.10")
    .WithArgs("-js", "--auth", natsToken)
    .WithEndpoint(targetPort: 4222, scheme: "nats", name: "nats");

// The provisioner: a one-shot Vault-CLI container — the *only* write-capable identity (the "CI/IaC provisioning
// step" stand-in, design §13.5.1). It writes a read-only, path-scoped policy, mints the runner's read-only token
// bound to it, then exits. The runner never has these privileges. The script splits along the same seam as the
// runtime IExampleSeed (W4): the REAL AppRole-trust provisioning every deployment performs, then — only for the
// example deployment (seedExampleData) — the demo source secrets. A real provisioner runs the trust half and seeds
// its OWN real secrets from its secret-management, so the two halves are kept separate and assembled below.

// REAL (every deployment): wait for Vault, write the read-only, path-scoped policy, enable AppRole, (re)create the
// runner's role bound to that policy with a short-TTL renewable token, pin the non-secret RoleID, and hand off a
// response-wrapped single-use SecretID via the shared dir. The runner never gets write privileges.
const string approleTrustScript =
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
    // A SECOND response-wrapped SecretID for the control-plane system runner (its wrapping token is its own — single-use).
    "vault write -f -wrap-ttl=1800s -field=wrapping_token auth/approle/role/arazzo-runner/secret-id > /shared/secretid-system.wrap; " +
    "chmod 644 /shared/secretid-system.wrap; ";

// EXAMPLE-ONLY (seedExampleData): dev-dummy API keys for the sample's source services, at the Vault paths the seeded
// credential *references* point at (vault://secret/arazzo/<source>#api-key). A real deployment omits this and provisions
// its own real secrets. 'kyc-notifications' is a NATS channel source: its secret is the broker connection token,
// seeded below with the other example secrets (ADR 0051).
const string exampleSecretSeedScript =
    "vault kv put secret/arazzo/onboarding api-key=demo-onboarding-key; " +
    "vault kv put secret/arazzo/ledger api-key=demo-ledger-key; " +
    "vault kv put secret/arazzo/kyc api-key=demo-kyc-key; " +
    // The kyc-notifications CHANNEL credential's broker token (ADR 0051): the runner resolves it and presents
    // it in the CONNECT handshake; the value is the broker's deployment token above.
    $"vault kv put secret/arazzo/kyc-notifications token={natsToken}; ";

// NOT example-only: the control plane's system approval workflow (design §16.5.1) is a product feature, so its runner's
// OAuth2 client secret is seeded unconditionally at the Vault path the installed 'controlplane' credential references
// (vault://secret/arazzo/controlplane#client-secret). It equals the realm's arazzo-access-approval client secret; the
// system runner resolves it as its read-only Vault identity to fetch an accessRequests:grant token for grantAccessRequest.
const string systemWorkflowSecretSeedScript =
    "vault kv put secret/arazzo/controlplane client-secret=arazzo-access-approval-dev-secret; " +
    // The access-notifications CHANNEL credential's broker token (ADR 0051) — the system workflow's own
    // notification channels, distinct from the application's kyc-notifications source.
    $"vault kv put secret/arazzo/access-notifications token={natsToken}; ";

// Completion tail (infra): announce done, then linger briefly so the orchestrator observes the container reach
// 'Running' before it exits. A sub-second exit is read as a start failure (FailedToStart) and retried; with this the
// container goes Running -> exit 0 -> Finished, and WithHiddenOnCompletion(0) below then hides it as a completed step.
const string provisionCompletionScript = "echo provisioning-complete; sleep 15";

// Assemble: real trust always; the system-workflow secret always (product feature); the demo secrets only for the
// example deployment.
string provisionScript = approleTrustScript
    + systemWorkflowSecretSeedScript
    + (seedExampleData ? exampleSecretSeedScript : string.Empty)
    + provisionCompletionScript;

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

// The signing-vault provisioner (#879) — the control plane's key-custody equivalent of the AppRole provisioner above:
// enable the Transit engine, generate the ECDSA P-256 signing key IN the vault (its private half never leaves), grant a
// SIGN-ONLY policy + a fixed-id token to the control plane (least privilege — it can sign, not administer), and export
// the key's PUBLIC half to the shared handoff file for the runner's trust store. The public key is not secret, so it is
// exported plainly (no response-wrapping). Idempotent on retry: transit enable is guarded, the key/policy upsert, and
// the token create tolerates a duplicate id. The PEM is extracted from the JSON key read with grep/sed/printf (busybox
// tools in the image — no jq): strip the field wrapper, then %b-expand the JSON \n escapes into a real PEM. A single
// concatenated line (like the AppRole script above) — NOT a multi-line literal, whose CRLF line endings break /bin/sh.
const string signingProvisionScript =
    "set -e; " +
    "until vault status >/dev/null 2>&1; do echo 'waiting for signing vault'; sleep 1; done; " +
    "vault secrets enable transit 2>/dev/null || true; " +
    "vault write -f transit/keys/arazzo-executor-signing type=ecdsa-p256; " +
    // VaultSharp signs at transit/sign/<key>/<hash-algo> (the hash is a PATH segment), so the sign-only policy must grant
    // the /* sub-path, not just the bare key path — otherwise the sign is permission-denied.
    "echo 'path \"transit/sign/arazzo-executor-signing\" { capabilities = [\"update\"] } path \"transit/sign/arazzo-executor-signing/*\" { capabilities = [\"update\"] }' | vault policy write arazzo-signer -; " +
    "vault token create -id=arazzo-cp-signing-token -policy=arazzo-signer -period=768h >/dev/null 2>&1 || true; " +
    "KEY=$(vault read -format=json transit/keys/arazzo-executor-signing | grep -o '\"public_key\": *\"[^\"]*\"' | head -1 | sed -e 's/^\"public_key\": *\"//' -e 's/\"$//'); " +
    "printf '%b' \"$KEY\" > /shared/executor-signing.pub; " +
    "chmod 644 /shared/executor-signing.pub; " +
    "echo signing-provisioning-complete; sleep 15";

var signingVaultInit = builder.AddContainer("signing-vault-init", "hashicorp/vault", "1.18")
    .WithEnvironment("VAULT_ADDR", signingVault.GetEndpoint("http"))
    .WithEnvironment("VAULT_TOKEN", signingVaultRootToken)
    // The public-key export lands here; the runner (a host process) reads it from the same host dir (its trust anchor).
    .WithBindMount(signingHandoffDir, "/shared")
    .WithEntrypoint("/bin/sh")
    .WithArgs("-c", signingProvisionScript)
    .WaitFor(signingVault)
    .WithHiddenOnCompletion(0);

// Keycloak — the identity provider (design §16). Bootstrapped declaratively along the real/example seam (§W4). The base
// realm import (`arazzo-realm.json` + `arazzo-users-0.json`) ALWAYS seeds the `arazzo` realm, its `arazzo-admins` group
// (→ the first system admin, §16.2), the UI (auth-code+PKCE) / CLI (device-flow) OIDC clients with a `groups` mapper, and
// the grantee-directory service account (real infra the §16.5.4 resolver needs). The demo personas (arazzo-admin, alice)
// live in `arazzo-users-1.json` and import only when SeedExampleData is on — so one switch governs all example fiction.
// Ephemeral (no data volume): the realm is re-imported fresh on each run, matching the demo's reset-each-run theme (the
// Postgres containers are ephemeral, Vault dev is in-memory) — predictable state, no volume drift.
var keycloak = builder.AddKeycloak("keycloak")
    .WithRealmImport("realms/arazzo-realm.json")
    .WithRealmImport("realms/arazzo-users-0.json");
if (seedExampleData)
{
    keycloak = keycloak.WithRealmImport("realms/arazzo-users-1.json");
}

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
    .WithEnvironment("Nats__Token", natsToken)
    .WaitFor(nats)
    .WithHttpEndpoint()
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

// The ASP.NET control-plane host: the real server surface (catalog, runs, credentials, administrators, security)
// plus the build-free web UI. It stores credential *references* only (never binds to
// Vault — the §13 invariant), so it needs no Vault token. It references Keycloak as the OIDC authority for token
// validation (§16.3) and waits for the realm import. Externally reachable; OTel flows to the dashboard.
var controlplane = builder.AddProject<Projects.Corvus_Text_Json_Arazzo_ControlPlane_Demo>("controlplane")
    .WithReference(workflowstore)
    .WaitFor(workflowstore)
    // §18 multi-process: a SEPARATE runner process hosts $draft debug runs, so the control plane must NOT run its own
    // in-process draft pump (else both would claim the same runs). The control plane only MARKS runs claimable.
    .WithEnvironment("ControlPlane__HostDraftRunnerInProcess", "false")
    // W4 seeding split: the AppHost's single SeedExampleData switch drives the control plane's example seed too, so one
    // flag governs all demo fiction end to end (this control-plane seed + the Vault demo secrets + the Keycloak personas).
    .WithEnvironment("ControlPlane__SeedExampleData", seedExampleData ? "true" : "false")
    // §14.1/§16: ENFORCE authentication + row security. This activates the whole (already-built) auth stack — Keycloak
    // JWT-bearer + BFF cookie/OIDC + dev-API-key, ControlPlaneSecurityMode.Scoped, and the per-row reach policy — so the
    // API is no longer anonymous. The first administrator is bootstrapped declaratively (§16.2): the Keycloak realm
    // import seeds the arazzo-admins group + seed admin, and the control plane's tier-3 deployment-policy grant maps that
    // group to the service operator. Everyone else earns reach through the §16.5 access-request -> approval flow.
    .WithEnvironment("ControlPlane__RequireAuthorization", "true")
    // §14 at rest: the per-boot checkpoint-encryption key (see its generation above) — checkpoints, step
    // outputs included, are AES-GCM-wrapped by the application before Postgres sees them.
    .WithEnvironment("ControlPlane__CheckpointProtectionKey", checkpointProtectionKey)
    // #879 executor-package signing: the control plane signs each compiled executor's manifest against the signing
    // vault's Transit key at catalog-add, using its sign-only token. The runner verifies with the public key alone.
    .WithEnvironment("ControlPlane__SigningVault__Address", signingVault.GetEndpoint("http"))
    .WithEnvironment("SIGNING_VAULT_TOKEN", signingControlPlaneToken)
    .WithEnvironment("ControlPlane__SigningVault__KeyName", signingKeyName)
    .WithEnvironment("ControlPlane__SigningVault__KeyId", signingKeyName)
    .WithEnvironment("ControlPlane__SigningVault__MountPoint", "transit")
    .WithEnvironment("ControlPlane__SigningVault__Algorithm", "ecdsa-p256-sha256")
    // Wait for the signing key + sign token to exist before the control plane seeds (seeding builds + signs executors).
    .WaitForCompletion(signingVaultInit)
    .WithReference(keycloak)
    .WaitFor(keycloak)
    // Grantee-directory (§16.5.4): the control plane resolves real Keycloak users/groups/roles for the grant pickers
    // through the arazzo-directory service-account client (realm import gives it realm-management view-users/query-groups).
    // The Keycloak base URL is otherwise only in service-discovery config, so surface it explicitly; the client id is
    // public config; the secret is injected as an env var the directory resolves via env://ARAZZO_DIRECTORY_CLIENT_SECRET
    // (must equal the realm import's client secret).
    .WithEnvironment("ControlPlane__Keycloak__BaseUrl", keycloak.GetEndpoint("http"))
    .WithEnvironment("ControlPlane__Directory__ClientId", "arazzo-directory")
    .WithEnvironment("ARAZZO_DIRECTORY_CLIENT_SECRET", "arazzo-directory-dev-secret")
    // The connected-provider demo (ADR 0052): the realm import registers the arazzo-portal OAuth client, and the
    // control plane folds this Keycloak in as a connected provider over it — the fetch pane's interactive sign-in is
    // then live-testable with the seeded realm users, no external account. The secret must equal the realm import's.
    .WithEnvironment("ARAZZO_PORTAL_CLIENT_SECRET", "arazzo-portal-dev-secret")
    // Onboarding, ledger, and kyc are real external sources: inject their endpoints so the control plane's live-
    // execution transports route those sources there (its startup live runs call them), and wait for them to be healthy.
    .WithEnvironment("ControlPlane__Sources__Onboarding", onboarding.GetEndpoint("http"))
    .WithEnvironment("ControlPlane__Sources__Ledger", ledger.GetEndpoint("http"))
    .WithEnvironment("ControlPlane__Sources__Kyc", kyc.GetEndpoint("http"))
    // The control plane's live resumer executes the seeded async run, whose send step publishes to the bus.
    .WithEnvironment("Nats__Url", nats.GetEndpoint("nats"))
    .WithEnvironment("Nats__Token", natsToken)
    .WaitFor(onboarding)
    .WaitFor(ledger)
    .WaitFor(kyc)
    .WaitFor(nats)
    // PINNED external port (8090), isProxied:false so the app binds :8090 DIRECTLY (no DCP proxy on a random port) —
    // the browser-facing control-plane URL is then stable across runs. Two things need a fixed URL: the GitHub App's
    // OAuth callback (http://localhost:8090/arazzo/v1/github/auth/callback is registered on the App, and a dynamic
    // port would change it every run), and the designer/app links a user keeps open. The runner's WithReference/
    // GetEndpoint and the health check resolve to :8090.
    .WithHttpEndpoint(port: 8090, isProxied: false)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

// Aspire's DCP hands each child an SSL_CERT_DIR holding only the dev certificate, which on Linux REPLACES the
// system CA store — outbound public TLS (the GitHub token exchange) then fails PartialChain. Restore the system
// bundle through the orthogonal SSL_CERT_FILE channel the DCP does not touch. This must happen at LAUNCH: the
// process's root store is read before any in-process env repair can land (verified empirically — an in-process
// append had no effect; a launch-level value works).
if (File.Exists("/etc/ssl/certs/ca-certificates.crt"))
{
    controlplane.WithEnvironment("SSL_CERT_FILE", "/etc/ssl/certs/ca-certificates.crt");
}

// Enable the designer's GitHub OAuth broker (§4.7 / D3) when local credentials are present (read at the top). The
// client id is public (it rides the authorize URL) so it goes in as config; the secret is injected as an env var the
// control plane resolves through env://GITHUB_OAUTH_CLIENT_SECRET — never committed. Absent → "brokers no OAuth App"
// (Git panel off).
if (!string.IsNullOrWhiteSpace(githubClientId) && !string.IsNullOrWhiteSpace(githubClientSecret))
{
    controlplane
        .WithEnvironment("GitHubOAuth__ClientId", githubClientId)
        .WithEnvironment("GITHUB_OAUTH_CLIENT_SECRET", githubClientSecret);
}

// The runner ("execution-host") — the second process in the topology. It shares the store, registers in the
// runner registry, and claims/resumes runs (design §5/§7). It is the §13 secret *consumer*: it holds ONLY the
// read-only Vault token (least privilege), waits for provisioning to finish, and resolves credentials at bind
// time. It waits for the control plane to seed the store, then claims and executes runs against the real source services (below).
builder.AddProject<Projects.Corvus_Text_Json_Arazzo_Runner_Demo>("runner")
    .WithReference(workflowstore)
    .WaitFor(workflowstore)
    // §14 at rest: the SAME per-boot checkpoint key the control plane holds — every process touching the
    // shared state store must wrap it identically.
    .WithEnvironment("Runner__CheckpointProtectionKey", checkpointProtectionKey)
    .WithEnvironment("VAULT_ADDR", vault.GetEndpoint("http"))
    // Secure introduction: NOT a pre-minted token. The runner gets its non-secret AppRole RoleID as plain config and
    // the PATH to the single-use wrapping token the provisioner left behind; it unwraps that to obtain the SecretID and
    // authenticates via AppRole for a dynamically-issued, short-TTL token. (In production the runtime host's service
    // principal / platform attestation supplies this; the sample simulates it — see the samples README.)
    .WithEnvironment("Runner__Vault__RoleId", runnerRoleId)
    .WithEnvironment("Runner__Vault__WrapTokenFile", vaultWrapTokenPath)
    // #879 executor-package trust: the runner's trust anchor — the signing key's PUBLIC half, exported by the signing
    // provisioner to this host file (its stand-in for a platform-provisioned config artifact). The runner reads it into
    // its trust store and verifies every catalogued executor before loading it; it never reaches the signing vault.
    .WithEnvironment("Runner__ExecutorTrust__PublicKeyFile", signingPublicKeyPath)
    .WithEnvironment("Runner__ExecutorTrust__KeyId", signingKeyName)
    // §18 multi-process: this runner hosts the development-environment $draft debug runs the control plane marks,
    // executing each against the real source services (below).
    .WithEnvironment("Runner__Environment", "development")
    // All source backends are real external services (their own processes + databases); the runner routes each at its
    // endpoint and waits for them (its executed runs call them). There is no /svc mock backend to point at any more.
    .WithEnvironment("Runner__Sources__Onboarding", onboarding.GetEndpoint("http"))
    .WithEnvironment("Runner__Sources__Ledger", ledger.GetEndpoint("http"))
    .WithEnvironment("Runner__Sources__Kyc", kyc.GetEndpoint("http"))
    // The runner subscribes to kyc.verdict to resume suspended async runs, and publishes review requests when it
    // executes an async run's send step.
    .WithEnvironment("Nats__Url", nats.GetEndpoint("nats"))
    .WithEnvironment("Nats__Token", natsToken)
    .WithReference(controlplane)
    .WaitFor(controlplane)
    // Authenticated registration (design §5.5/§16.4): the runner registers through the control plane's authenticated HTTP
    // endpoint as its own machine principal — the arazzo-runner Keycloak client (client-credentials) — rather than
    // self-asserting a Pending row into the shared store. It needs the control-plane base URL, the Keycloak base URL (for the
    // token endpoint), and its client-credentials (the client id is its azp -> the bound principal; the secret must equal the
    // realm import's). The control plane derives the trusted principal from the presented token and binds the authorization.
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithEnvironment("Runner__ControlPlane__BaseUrl", controlplane.GetEndpoint("http"))
    .WithEnvironment("Runner__Keycloak__BaseUrl", keycloak.GetEndpoint("http"))
    .WithEnvironment("Runner__Keycloak__ClientId", "arazzo-runner")
    .WithEnvironment("Runner__Keycloak__ClientSecret", "arazzo-runner-dev-secret")
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
    // #879: also gate on the signing provisioner so the exported public-key file exists before the runner reads it.
    .WaitForCompletion(signingVaultInit)
    // Aspire-managed HTTP endpoint (no hardcoded port). The runner is an internal worker, so Aspire proxies this
    // endpoint; letting Aspire assign the port (rather than the old launchSettings applicationUrl=5280) is what stops
    // the app from binding the same port as the DCP proxy — the collision that was crashing the runner on startup.
    .WithHttpEndpoint()
    .WithHttpHealthCheck("/health");

// The control-plane SYSTEM RUNNER (design §16.5.1) — a dedicated execution host for the control plane's own internal
// workflows, separate from the application runner above. It serves the internal "system" environment, claims the
// bootstrapped access-approval runs the control plane starts, and hosts the access.decision consumer. It registers as
// the arazzo-access-approval machine principal, and resolves the 'controlplane' OAuth2 credential (accessRequests:grant)
// as its own read-only Vault identity to call grantAccessRequest on the control-plane API.
builder.AddProject<Projects.Corvus_Text_Json_Arazzo_ControlPlane_SystemRunner>("system-runner")
    .WithReference(workflowstore)
    .WaitFor(workflowstore)
    // Opt-in diagnostics for the headless system runner: when the host is launched with RUNNER_DIAG_LOG set, the runner
    // tees its console + any unhandled exception to that file, so a start-up or resume failure of this critical component
    // is legible from outside the DCP-managed console (which otherwise streams only to the Aspire dashboard). Unset by
    // default (empty env), so it costs nothing unless a developer explicitly turns it on to investigate the runner.
    .WithEnvironment("RUNNER_DIAG_LOG", System.Environment.GetEnvironmentVariable("RUNNER_DIAG_LOG") ?? string.Empty)
    .WithEnvironment("Runner__CheckpointProtectionKey", checkpointProtectionKey)
    .WithEnvironment("VAULT_ADDR", vault.GetEndpoint("http"))
    .WithEnvironment("Runner__Vault__RoleId", runnerRoleId)
    // Its OWN wrapping token (single-use) — a second SecretID the provisioner wrapped for this second runner process.
    .WithEnvironment("Runner__Vault__WrapTokenFile", vaultSystemWrapTokenPath)
    .WithEnvironment("Runner__ExecutorTrust__PublicKeyFile", signingPublicKeyPath)
    .WithEnvironment("Runner__ExecutorTrust__KeyId", signingKeyName)
    // The internal environment the bootstrapped approval workflow is made available in (design §16.5.1).
    .WithEnvironment("Runner__Environment", "system")
    // The message bus: the approval run's notify SEND publishes to access.notify; the decision consumer subscribes to
    // access.decision (the approver's decision, published by the control plane).
    .WithEnvironment("Nats__Url", nats.GetEndpoint("nats"))
    .WithEnvironment("Nats__Token", natsToken)
    // The control-plane API the approval workflow calls (grantAccessRequest), and the runner registers through.
    .WithReference(controlplane)
    .WaitFor(controlplane)
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithEnvironment("Runner__ControlPlane__BaseUrl", controlplane.GetEndpoint("http"))
    .WithEnvironment("Runner__Keycloak__BaseUrl", keycloak.GetEndpoint("http"))
    // Authenticated registration + the OAuth2 credential identity: the arazzo-access-approval client (client-credentials),
    // whose token carries both runners:register (to register) and accessRequests:grant (to call grantAccessRequest).
    .WithEnvironment("Runner__Keycloak__ClientId", "arazzo-access-approval")
    .WithEnvironment("Runner__Keycloak__ClientSecret", "arazzo-access-approval-dev-secret")
    .WaitFor(nats)
    // Gate on the provisioner finishing so the wrapping token + signing public key exist before the runner reads them.
    .WaitForCompletion(vaultInit)
    .WaitForCompletion(signingVaultInit)
    .WithHttpEndpoint()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
