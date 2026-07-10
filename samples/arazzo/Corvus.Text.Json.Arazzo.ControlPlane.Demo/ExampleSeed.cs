// <copyright file="ExampleSeed.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;

using CpEnvironment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.ControlPlane.Demo;

/// <summary>
/// The connected durable stores an <see cref="IExampleSeed"/> writes its demo content into. The seam carries the
/// stores as their backend-agnostic interfaces (the sample happens to bind Postgres implementations), so the seed
/// itself is decoupled from the chosen store backend.
/// </summary>
/// <param name="Catalog">The secured workflow catalog the demo's catalogued versions are added to.</param>
/// <param name="SourceCredentials">The §13 source-credential store the demo's credential references are added to.</param>
/// <param name="EnvironmentStore">The governed environment store the demo's developer sandbox is added to.</param>
/// <param name="SpecsDir">The directory holding the demo's Arazzo + OpenAPI specification packages.</param>
public sealed record ExampleSeedContext(
    SecuredWorkflowCatalog Catalog,
    ISourceCredentialStore SourceCredentials,
    IEnvironmentStore EnvironmentStore,
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
    /// references (references only — never secret material), and the developer sandbox environment the debug-run
    /// readiness gate needs.
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

        // Source-credential bindings — references only (the §13 invariant: never secret material). Each points at the
        // Vault path the AppHost's provisioner seeds (vault://secret/arazzo/<source>#api-key); the runner resolves it
        // with its read-only token. This populates /credentials (and the CLI + web UI) out of the box.
        foreach (string environment in new[] { "production", "development" })
        {
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
                        [new SecretReferenceDefinition("value", $"vault://secret/arazzo/{source}#api-key")]),
                    "demo",
                    cancellationToken);
            }
        }

        // §18: seed a development-class environment that ALLOWS working-copy drafts to run as debug runs (default off
        // everywhere else — crossing that line is an explicit per-environment decision by its administrators). Its two
        // sources are credentialed above, so the run dialog's readiness gate passes and a debug run executes against the
        // real external source services. The environment store is the governed, reach-scoped one the debug-run seam reads.
        using ParsedJsonDocument<CpEnvironment> developmentEnvironment = ParsedJsonDocument<CpEnvironment>.Parse(
            """{"name":"development","displayName":"Development","description":"Developer sandbox — working-copy drafts may run here as debug runs (§18).","allowsDraftRuns":true}"""u8.ToArray());
        (await context.EnvironmentStore.AddAsync(developmentEnvironment.RootElement, "demo", cancellationToken)).Dispose();
    }

    /// <inheritdoc/>
    public ValueTask RunLiveSampleAsync(IWorkflowStateStore stateStore, WorkflowResumer resumer, Action<string>? log = null)
        => DemoData.RunLiveOnboardingAsync(stateStore, resumer, log);
}