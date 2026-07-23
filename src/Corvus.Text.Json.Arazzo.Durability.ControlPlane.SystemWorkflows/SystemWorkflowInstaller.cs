// <copyright file="SystemWorkflowInstaller.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;

// Environment (the durability type) collides with System.Environment; alias it so the store type stays unambiguous.
using CpEnvironment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.SystemWorkflows;

/// <summary>
/// Installs the bootstrapped access-approval system workflow (design §16.5.1) into a control-plane deployment: creates
/// the internal environment, provisions the system runner's credential, catalogues the workflow package (its Arazzo
/// document plus its embedded AsyncAPI and OpenAPI sources), and makes the version available in the environment.
/// </summary>
/// <remarks>
/// <para>
/// Every step is idempotent — a re-install over an already-provisioned deployment is a no-op — so a deployment may call
/// <see cref="InstallAsync"/> unconditionally on start-up.
/// </para>
/// <para>
/// Ordering is significant: the runner credential is provisioned BEFORE the version is catalogued. The catalog-time
/// credential gate (§13) resolves the <c>controlplane</c> source's credential against the version's effective tags;
/// scoping that credential's USAGE to the workflow's own identity (<c>sys:workflow=access-approval</c>) both secures it
/// (only this workflow's runs may present it) and admits the version through the gate.
/// </para>
/// </remarks>
public sealed class SystemWorkflowInstaller
{
    /// <summary>The catalog base id of the access-approval workflow (its Arazzo <c>workflowId</c>).</summary>
    private const string BaseWorkflowId = "access-approval";

    /// <summary>The catalogued version installed (there is one bootstrapped version).</summary>
    private const int VersionNumber = 1;

    /// <summary>The Arazzo source name — and hence the credential's source name — of the control-plane API the grant
    /// step calls. Matches the <c>sourceDescriptions</c> entry in the embedded workflow document.</summary>
    private const string ControlPlaneSourceName = "controlplane";

    /// <summary>The OAuth2 scope the runner's issued token must carry to call <c>grantAccessRequest</c>. Mirrors
    /// <c>ControlPlaneScopes.AccessRequestsGrant</c> in the server project, which this project cannot reference without a
    /// dependency cycle; kept in sync by the live-verify pass.</summary>
    private const string GrantScope = "accessRequests:grant";

    private readonly ISecuredWorkflowCatalog catalog;
    private readonly IAvailabilityStore availability;
    private readonly ISourceCredentialStore credentials;
    private readonly IEnvironmentStore environments;
    private readonly IEnvironmentAdministratorStore environmentAdministrators;
    private readonly IWorkflowExecutorProvider? executorProvider;

    /// <summary>Initializes a new instance of the <see cref="SystemWorkflowInstaller"/> class.</summary>
    /// <param name="catalog">The catalog the approval version is published to. Must be configured with the same credential
    /// store as <paramref name="credentials"/> so the catalog-time credential gate resolves the runner credential.</param>
    /// <param name="availability">The availability store the version is made available in.</param>
    /// <param name="credentials">The credential store the runner's OAuth2 client-credentials identity is provisioned in.</param>
    /// <param name="environments">The environment store the internal environment is created in.</param>
    /// <param name="environmentAdministrators">The environment-administrator store, so the internal environment is granted
    /// its administration (the genesis administrator) just as a normally-created environment is (§7.7).</param>
    /// <param name="executorProvider">When supplied, the install PROBES the executor bake and throws if the
    /// workflow cannot be generated and compiled. A null build is the provider's degraded mode for USER
    /// workflows (catalogued, just not runnable — a diagnosable state); a non-runnable SYSTEM workflow
    /// instead crash-loops its runner with no visible cause, so the deployment must refuse to come up.</param>
    public SystemWorkflowInstaller(
        ISecuredWorkflowCatalog catalog,
        IAvailabilityStore availability,
        ISourceCredentialStore credentials,
        IEnvironmentStore environments,
        IEnvironmentAdministratorStore environmentAdministrators,
        IWorkflowExecutorProvider? executorProvider = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(environments);
        ArgumentNullException.ThrowIfNull(environmentAdministrators);

        this.catalog = catalog;
        this.availability = availability;
        this.credentials = credentials;
        this.environments = environments;
        this.environmentAdministrators = environmentAdministrators;
        this.executorProvider = executorProvider;
    }

    /// <summary>Installs the access-approval workflow and its supporting environment and credential, idempotently.</summary>
    /// <param name="options">The deployment-supplied install parameters.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the workflow is catalogued and available.</returns>
    public async ValueTask InstallAsync(SystemWorkflowInstallOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        // The management/reach tags default to the administrator identity, so the system surfaces are administrator-scoped
        // rather than visible to everyone when the deployment does not scope them explicitly.
        SecurityTagSet management = options.ManagementTags.IsEmpty ? options.AdministratorIdentity : options.ManagementTags;

        this.ProbeExecutorBake();

        await this.EnsureEnvironmentAsync(options, management, cancellationToken).ConfigureAwait(false);
        await this.EnsureCredentialAsync(options, management, cancellationToken).ConfigureAwait(false);
        await this.EnsureCatalogVersionAsync(options, cancellationToken).ConfigureAwait(false);
        await this.EnsureAvailabilityAsync(options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads an embedded system-workflow spec by file name, robust to MSBuild resource-name mangling.</summary>
    private static byte[] ReadSpec(string fileName)
    {
        Assembly assembly = typeof(SystemWorkflowInstaller).Assembly;
        string suffix = ".specs." + fileName;
        string resourceName = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith(suffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"The embedded system-workflow spec '{fileName}' was not found in assembly '{assembly.GetName().Name}'.");

        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The embedded system-workflow spec resource '{resourceName}' could not be opened.");
        byte[] buffer = new byte[stream.Length];
        stream.ReadExactly(buffer);
        return buffer;
    }

    /// <summary>Builds the deterministic workflow package from the three embedded specs.</summary>
    private static ReadOnlyMemory<byte> BuildPackage()
        => WorkflowPackage.Pack(ReadSpec("access-approval.arazzo.json"), ReadSources());

    /// <summary>The workflow's referenced source documents, keyed by their <c>sourceDescriptions</c> names.</summary>
    private static KeyValuePair<string, byte[]>[] ReadSources()
        =>
        [
            new KeyValuePair<string, byte[]>("notifications", ReadSpec("access-approval.asyncapi.json")),
            new KeyValuePair<string, byte[]>(ControlPlaneSourceName, ReadSpec("access-approval.controlplane.openapi.json")),
        ];

    /// <summary>
    /// Fails the install loudly when the SYSTEM workflow cannot bake (the deferred half of "surface
    /// system-workflow bake failures"): a deployment must refuse to come up with a non-runnable
    /// critical workflow rather than catalogue it and let its runner crash-loop causelessly.
    /// </summary>
    /// <exception cref="InvalidOperationException">The executor could not be generated and compiled.</exception>
    private void ProbeExecutorBake()
    {
        if (this.executorProvider is null)
        {
            return;
        }

        if (this.executorProvider.BuildExecutor(ReadSpec("access-approval.arazzo.json"), ReadSources(), "system-install-probe") is null)
        {
            throw new InvalidOperationException(
                $"The '{BaseWorkflowId}' system workflow failed to bake: its executor could not be generated and compiled, and the deployment refuses to come up with a non-runnable critical workflow. The executor build log (\"Executor build skipped: …\") carries the generation/compile diagnostics.");
        }
    }

    private async ValueTask EnsureEnvironmentAsync(SystemWorkflowInstallOptions options, SecurityTagSet management, CancellationToken cancellationToken)
    {
        ParsedJsonDocument<CpEnvironment>? existing =
            await this.environments.GetAsync(options.Environment, AccessContext.System, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            existing.Dispose();
        }
        else
        {
            using ParsedJsonDocument<CpEnvironment> draft = CpEnvironment.Draft(
                options.Environment,
                options.EnvironmentDisplayName,
                options.EnvironmentDescription,
                management);
            (await this.environments.AddAsync(draft.RootElement, options.Actor, cancellationToken).ConfigureAwait(false)).Dispose();
        }

        // Grant the genesis administrator administration of the internal environment, exactly as creating an environment
        // through the API does ("creating one grants the creator administration", §7.7). Without this the environment is
        // created and reachable but carries no administrators record, so listing its administrators 404s even for a caller
        // in reach. Run it unconditionally, not only on first create: EstablishAsync is idempotent (a conflict is a
        // no-op), so it also repairs a deployment whose system environment predates this fix.
        var administration = new SecuredEnvironmentAdministration(this.environmentAdministrators, options.Actor);
        await administration.EstablishAsync(options.Environment, options.AdministratorIdentity, default, hasKind: false, default, hasLabel: false, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask EnsureCredentialAsync(SystemWorkflowInstallOptions options, SecurityTagSet management, CancellationToken cancellationToken)
    {
        ParsedJsonDocument<SourceCredentialBinding>? existing =
            await this.credentials.GetAsync(ControlPlaneSourceName, options.Environment, AccessContext.System, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            existing.Dispose();
            return;
        }

        // Usage-scope the credential to the approval workflow's own identity: only runs of access-approval (which carry
        // sys:workflow=access-approval) may resolve it, and that same scoping admits the version through the catalog gate.
        SecurityTagSet usageTags = SecurityTagSet.FromTags([new SecurityTag(WorkflowIdentity.WorkflowTagKey, BaseWorkflowId)]);

        var definition = new SourceCredentialDefinition(
            ControlPlaneSourceName,
            options.Environment,
            SourceCredentialKind.OAuth2ClientCredentials,
            [new SecretReferenceDefinition("clientSecret", options.CredentialClientSecretRef)],
            Config:
            [
                new CredentialConfigDefinition("tokenUrl", options.CredentialTokenUrl),
                new CredentialConfigDefinition("clientId", options.CredentialClientId),
                new CredentialConfigDefinition("scope", GrantScope),
            ],
            Description: "The control-plane system runner's OAuth2 client-credentials identity for calling grantAccessRequest (design §16.5.1).",
            ManagementTags: management,
            UsageTags: usageTags);

        (await this.credentials.AddAsync(definition, options.Actor, cancellationToken).ConfigureAwait(false)).Dispose();
    }

    private async ValueTask EnsureCatalogVersionAsync(SystemWorkflowInstallOptions options, CancellationToken cancellationToken)
    {
        ParsedJsonDocument<CatalogVersion>? existing =
            await this.catalog.GetAsync(BaseWorkflowId, VersionNumber, AccessContext.System, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            existing.Dispose();
            return;
        }

        (await this.catalog.AddAsync(
            BuildPackage(),
            options.Owner,
            TagSet.FromTags(options.WorkflowTags),
            options.AdministratorIdentity,
            cancellationToken).ConfigureAwait(false)).Dispose();
    }

    private async ValueTask EnsureAvailabilityAsync(SystemWorkflowInstallOptions options, CancellationToken cancellationToken)
    {
        // MakeAvailableAsync is additive and idempotent (Created is false when the entry already exists), so no read-back
        // guard is needed; dispose the returned pooled entry document either way.
        (await this.availability.MakeAvailableAsync(BaseWorkflowId, VersionNumber, options.Environment, options.Actor, cancellationToken).ConfigureAwait(false)).Entry.Dispose();
    }
}