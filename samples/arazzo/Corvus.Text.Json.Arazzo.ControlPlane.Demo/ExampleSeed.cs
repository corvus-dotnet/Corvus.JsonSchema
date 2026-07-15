// <copyright file="ExampleSeed.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;

using CpEnvironment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.ControlPlane.Demo;

/// <summary>
/// The connected durable stores an <see cref="IExampleSeed"/> writes its demo content into. The seam carries the
/// stores as their backend-agnostic interfaces (the sample happens to bind Postgres implementations), so the seed
/// itself is decoupled from the chosen store backend.
/// </summary>
/// <param name="Catalog">The secured workflow catalog the demo's catalogued versions are added to.</param>
/// <param name="SourceCredentials">The §13 source-credential store the demo's credential references are added to.</param>
/// <param name="EnvironmentStore">The governed environment store the demo's environments are added to.</param>
/// <param name="EnvironmentAdministrators">The per-environment administrator store (§7.7) the demo grants admin on.</param>
/// <param name="Sources">The source registry (§7.8) the demo's referenced source specs are registered in.</param>
/// <param name="Availability">The availability store (§7.6) the demo marks catalogued versions "Available in".</param>
/// <param name="AccessRequests">The access-request store (§16.5) the demo seeds a pending request into.</param>
/// <param name="AvailabilityRequests">The availability (promotion) request store (§7.6) the demo seeds a pending request into.</param>
/// <param name="SpecsDir">The directory holding the demo's Arazzo + OpenAPI specification packages.</param>
public sealed record ExampleSeedContext(
    SecuredWorkflowCatalog Catalog,
    ISourceCredentialStore SourceCredentials,
    IEnvironmentStore EnvironmentStore,
    IEnvironmentAdministratorStore EnvironmentAdministrators,
    ISourceStore Sources,
    IAvailabilityStore Availability,
    IAccessRequestStore AccessRequests,
    IAvailabilityRequestStore AvailabilityRequests,
    string SpecsDir);

/// <summary>
/// Seeds the sample's <em>example</em> content — the demo fiction: catalogued workflow versions, source-credential
/// references, a developer sandbox environment, and a live-executed run. This is the counterpart to
/// <c>IDeploymentBootstrap</c> (the real, config-driven bootstrapping every deployment performs): a production
/// deployment runs only the bootstrap, whereas the sample additionally runs this seed when <c>seedExampleData</c>
/// is set. Keeping the two apart is what lets the real bootstrap graduate into a configurable deployment capability
/// without dragging the demo fiction along.
/// </summary>
public interface IExampleSeed
{
    /// <summary>
    /// Seeds the demo's example content into the connected stores: catalogued workflow versions, source-credential
    /// references (references only — never secret material), the governed environments (development / staging /
    /// production) with the arazzo-admins group as their administrator, the registered source specs, an availability
    /// entry, and a pending access + promotion request — so every governance surface has content out of the box.
    /// </summary>
    /// <param name="context">The connected stores + specification directory to seed into.</param>
    /// <param name="cancellationToken">Cancels the seed.</param>
    /// <returns>A task that completes when the demo content has been written.</returns>
    ValueTask SeedAsync(ExampleSeedContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes one fresh onboarding run live once the host is listening, so the demo shows a genuinely-executed run
    /// rather than only hand-seeded states.
    /// </summary>
    /// <param name="stateStore">The run store the live run is written to.</param>
    /// <param name="resumer">The live resumer that re-enters the run's compiled executor against the demo services.</param>
    /// <param name="log">An optional progress sink.</param>
    /// <returns>A task that completes when the live run has advanced to its first pause (or completion).</returns>
    ValueTask RunLiveSampleAsync(IWorkflowStateStore stateStore, WorkflowResumer resumer, Action<string>? log = null);
}

/// <summary>
/// The Arazzo control-plane sample's <see cref="IExampleSeed"/>: the demo fiction that makes the browsable sample
/// self-contained. It delegates the catalogued workflow content to <see cref="DemoData"/> and adds the
/// source-credential references + developer sandbox environment out of the box.
/// </summary>
public sealed class ArazzoExampleSeed : IExampleSeed
{
    /// <inheritdoc/>
    public async ValueTask SeedAsync(ExampleSeedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Catalogued workflow versions from real packages (an Arazzo workflow + its OpenAPI sources), so the browsable
        // demo exercises the genuine metadata generator and JSON Schema validator.
        await DemoData.SeedAsync(context.Catalog, context.SpecsDir);

        // The arazzo-admins group's resolved identity (sys:group + the pinned sys:iss issuer) — the identity the
        // governance seeds below key on: production credential usage, environment administration.
        SecurityTagSet adminGroup = DemoData.GroupIdentity("arazzo-admins");

        // Source-credential bindings — references only (the §13 invariant: never secret material). Each points at the
        // Vault path the AppHost's provisioner seeds (vault://secret/arazzo/<source>#api-key); the runner resolves it
        // with its read-only token. This populates /credentials (and the CLI + web UI) out of the box. The PRODUCTION
        // bindings are usage-scoped to the admin group (§13: only runs carrying the admin identity are entitled), so
        // the access overview shows the admin team's credential rights; development stays shared (empty usage tags) so
        // any debug run resolves its credentials.
        foreach (string environment in new[] { "production", "development" })
        {
            SecurityTagSet usage = environment == "production" ? adminGroup : SecurityTagSet.Empty;

            // 'notifications' is the AsyncAPI (NATS) source onboard-customer-async binds (kyc.requests / kyc.verdict). Its
            // transport is the message binder, not a per-source API key, but the debug-run readiness gate requires a binding
            // for EVERY referenced source — so it is seeded here too (its Vault #api-key is provisioned but never resolved
            // for the channel). Without it, a debug run of the async workflow fails 409 "No credential bound for: notifications".
            foreach (string source in new[] { "onboarding", "ledger", "kyc", "notifications" })
            {
                // AddAsync returns the persisted binding as a pooled document — dispose it (the seed doesn't read it back).
                using ParsedJsonDocument<SourceCredentialBinding> seeded = await context.SourceCredentials.AddAsync(
                    new SourceCredentialDefinition(
                        source,
                        environment,
                        SourceCredentialKind.ApiKey,
                        // The ApiKey provider resolves the key from the secret reference named "value" (default header
                        // X-API-Key); the reference target is the Vault path's #api-key field the provisioner seeds.
                        [new SecretReferenceDefinition("value", $"vault://secret/arazzo/{source}#api-key")],
                        UsageTags: usage),
                    "demo",
                    cancellationToken);
            }
        }

        // Governed environments (§7.7): the developer sandbox (working-copy drafts may run here as §18 debug runs — a
        // deliberate per-environment decision, off everywhere else) plus the two promoted environments a real
        // deployment ships with. development's two sources are credentialed above, so the run dialog's readiness gate
        // passes and a debug run executes against the real external source services.
        foreach ((string name, string displayName, string description, bool allowsDraftRuns) in new[]
        {
            ("development", "Development", "Developer sandbox — working-copy drafts may run here as debug runs (§18).", true),
            ("staging", "Staging", "Pre-production verification environment.", false),
            ("production", "Production", "Live production environment.", false),
        })
        {
            // The generated Create() builds the draft in one pooled pass (and, unlike the interpolated-string parse it
            // replaces, cannot produce invalid JSON from a value containing a quote). The server-stamped fields are
            // omitted via default Sources for the store to stamp.
            using ParsedJsonDocument<CpEnvironment> environment = CpEnvironment.Create(
                createdAt: default,
                createdBy: default,
                etag: default,
                name: name,
                allowsDraftRuns: allowsDraftRuns,
                description: description,
                displayName: displayName);
            (await context.EnvironmentStore.AddAsync(environment.RootElement, "demo", cancellationToken)).Dispose();
        }

        // Environment administrators (§7.7): the arazzo-admins group administers every environment — the key unblock so
        // an administrator can manage grants, promotions, and availability per environment (without this, no interactive
        // administrator exists for the governance surfaces). The identity is the resolved sys:group=arazzo-admins tag.
        var administration = new SecuredEnvironmentAdministration(context.EnvironmentAdministrators, "demo");
        foreach (string environmentName in new[] { "development", "staging", "production" })
        {
            await administration.EstablishAsync(environmentName, adminGroup, default, false, default, false, cancellationToken);
        }

        // Source registry (§7.8): register the four referenced sources from their real specs, so the sources panel and
        // the workflow authoring surface resolve them. onboarding/ledger/kyc are OpenAPI; notifications is the AsyncAPI
        // (NATS) channel. Management tags are empty (a real deployment would tag by reach).
        foreach ((string name, string file, string type, string displayName, string description) in new[]
        {
            ("onboarding", "onboarding.openapi.json", "openapi", "Onboarding API", "Customer onboarding service."),
            ("ledger", "ledger.openapi.json", "openapi", "Ledger API", "Reconciliation and ledger service."),
            ("kyc", "kyc.openapi.json", "openapi", "KYC API", "Identity-verification service."),
            ("notifications", "notifications.asyncapi.json", "asyncapi", "Notifications", "KYC review request/verdict channel (NATS)."),
        })
        {
            ReadOnlyMemory<byte> documentUtf8 = File.ReadAllBytes(Path.Combine(context.SpecsDir, file));
            using ParsedJsonDocument<RegisteredSource> source = RegisteredSource.Draft(
                name, type, documentUtf8, displayName, description, SecurityTagSet.Empty);
            (await context.Sources.AddAsync(source.RootElement, "demo", cancellationToken)).Dispose();
        }

        // Availability (§7.6): the first catalogued onboarding version is "Available in" production, so the runs surface
        // can start it there and the availability panel is not empty out of the box.
        (await context.Availability.MakeAvailableAsync("onboard-customer", 1, "production", "demo", cancellationToken)).Entry.Dispose();

        // Inbox content for the admin governance surfaces: a pending access request (§16.5, a user asking for run scopes)
        // and a pending promotion request (§7.6, asking to make a catalogued version available in production).
        using (ParsedJsonDocument<AccessRequest> access = AccessRequest.Draft(
            "onboard-customer", ["runs:write", "runs:read"], "sub", "alice", "Alice (Payments)", "On-call incident response.", 3600))
        {
            (await context.AccessRequests.CreateAsync(access.RootElement, "alice", cancellationToken)).Dispose();
        }

        using (ParsedJsonDocument<AvailabilityRequest> promotion = AvailabilityRequest.Draft(
            "onboard-customer", 2, "production", "Please promote v2 to production."))
        {
            (await context.AvailabilityRequests.CreateAsync(promotion.RootElement, "alice", cancellationToken)).Dispose();
        }
    }

    /// <inheritdoc/>
    public ValueTask RunLiveSampleAsync(IWorkflowStateStore stateStore, WorkflowResumer resumer, Action<string>? log = null)
        => DemoData.RunLiveOnboardingAsync(stateStore, resumer, log);
}