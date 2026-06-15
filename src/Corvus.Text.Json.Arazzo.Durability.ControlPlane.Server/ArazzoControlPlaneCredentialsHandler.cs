// <copyright file="ArazzoControlPlaneCredentialsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiCredentialsHandler"/> over an <see cref="ISourceCredentialStore"/> — the
/// control-plane surface that manages source credential bindings (design §13). The endpoints are gated by the
/// <c>credentials:read</c>/<c>credentials:write</c> capability scopes.
/// </summary>
/// <remarks>
/// <para><strong>Trust boundary (§13).</strong> This handler manages <em>references</em> only. It holds no
/// <see cref="ISecretResolver"/> and never resolves, returns, or logs secret material; request and response bodies
/// carry a <see cref="SecretRef"/> plus non-secret metadata. A value that is not a well-formed <see cref="SecretRef"/>
/// is rejected at the boundary (400) by the same <see cref="SourceCredentialBinding.ValidateDefinition"/> the store
/// uses, so secret material cannot be smuggled inline. Rotation is by changing the reference.</para>
/// <para>The (sourceName, environment) pair is the binding key; create conflicts (409) if one already exists, and
/// update/delete on a missing binding is a 404. Identity and created-* audit fields are immutable across updates.</para>
/// </remarks>
public sealed class ArazzoControlPlaneCredentialsHandler : IApiCredentialsHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    private readonly ISourceCredentialStore store;
    private readonly ControlPlaneAccess access;
    private readonly string actor;

    /// <summary>Initializes a new, unscoped instance (every request runs with <see cref="AccessContext.System"/> — no
    /// row security).</summary>
    /// <param name="store">The persistent source credential store the endpoints delegate to.</param>
    /// <param name="actor">The audit actor recorded on writes (a deployment may resolve this from the principal).</param>
    public ArazzoControlPlaneCredentialsHandler(ISourceCredentialStore store, string actor = "control-plane")
        : this(store, new ControlPlaneAccess(), actor)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneCredentialsHandler"/> class.</summary>
    /// <param name="store">The persistent source credential store the endpoints delegate to.</param>
    /// <param name="access">Resolves the caller's <see cref="AccessContext"/> per request and the internal tenant tags
    /// stamped onto created bindings (§14.2). Unscoped (<see cref="AccessContext.System"/>) when no row security is configured.</param>
    /// <param name="actor">The audit actor recorded on writes (a deployment may resolve this from the principal).</param>
    internal ArazzoControlPlaneCredentialsHandler(ISourceCredentialStore store, ControlPlaneAccess access, string actor = "control-plane")
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(actor);
        this.store = store;
        this.access = access;
        this.actor = actor;
    }

    /// <inheritdoc/>
    public async ValueTask<ListCredentialsResult> HandleListCredentialsAsync(ListCredentialsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        using PooledDocumentList<SourceCredentialBinding> bindings = await this.store.ListAsync(this.access.Current(), cancellationToken).ConfigureAwait(false);
        return ListCredentialsResult.Ok(ToList(bindings), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<CreateCredentialResult> HandleCreateCredentialAsync(CreateCredentialParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        SourceCredentialDefinition definition;
        SecurityTagSet managementTags;
        try
        {
            // managementTags = the principal's deployment-internal tenant tag (always stamped, so the owner keeps
            // management) PLUS any operator-supplied management labels (validated against the reserved internal prefix).
            IReadOnlyList<SecurityTag> userManagement = ReadTags(parameters.Body.ManagementTags);
            this.access.ValidateUserTags(userManagement);
            var management = new List<SecurityTag>(this.access.InternalTags());
            management.AddRange(userManagement);
            managementTags = SecurityTagSet.FromTags(management);

            // usageTags are derived from the operator's usage GRANTS, which the deployment maps to UNFORGEABLE internal
            // identity tags (e.g. sys:workflow=nightly-reconcile) — never free-form labels, so usage cannot be
            // self-granted by a workflow author. With no grants, usage defaults to the creating principal's own identity
            // (the owner's runs). The two scopes are independent (§13/§14.2).
            IReadOnlyList<CredentialUsageGrant> grants = ReadGrants(parameters.Body.UsageGrants);
            IReadOnlyList<SecurityTag> usage = grants.Count > 0 ? this.access.ResolveUsageGrants(grants) : this.access.InternalTags();
            SecurityTagSet usageTags = SecurityTagSet.FromTags(usage);

            definition = ReadWrite(parameters.Body) with { ManagementTags = managementTags, UsageTags = usageTags };
        }
        catch (ArgumentException ex)
        {
            return CreateCredentialResult.BadRequest(Problem("invalid-credential", "Invalid credential binding", 400, ex.Message), workspace);
        }

        // Guard against privilege escalation: a principal may not create a binding it could not itself manage.
        if (!this.access.Current().Admits(AccessVerb.Write, managementTags))
        {
            return CreateCredentialResult.BadRequest(
                Problem("management-out-of-reach", "Management scope out of reach", 400, "The binding's management tags are outside your own management reach."), workspace);
        }

        try
        {
            using ParsedJsonDocument<SourceCredentialBinding> created = await this.store.AddAsync(definition, this.actor, cancellationToken).ConfigureAwait(false);
            return CreateCredentialResult.Created(ToSummary(created.RootElement), workspace);
        }
        catch (ArgumentException ex)
        {
            return CreateCredentialResult.BadRequest(Problem("invalid-credential", "Invalid credential binding", 400, ex.Message), workspace);
        }
        catch (InvalidOperationException ex)
        {
            return CreateCredentialResult.Conflict(Problem("credential-exists", "Credential already exists", 409, ex.Message), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<GetCredentialResult> HandleGetCredentialAsync(GetCredentialParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string sourceName = (string)parameters.SourceName;
        string environment = (string)parameters.Environment;
        using ParsedJsonDocument<SourceCredentialBinding>? binding = await this.store.GetAsync(sourceName, environment, this.access.Current(), cancellationToken).ConfigureAwait(false);
        return binding is { } b
            ? GetCredentialResult.Ok(ToSummary(b.RootElement), workspace)
            : GetCredentialResult.NotFound(NotFoundProblem(sourceName, environment), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<UpdateCredentialResult> HandleUpdateCredentialAsync(UpdateCredentialParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string sourceName = (string)parameters.SourceName;
        string environment = (string)parameters.Environment;
        SourceCredentialDefinition definition;
        try
        {
            definition = ReadUpdate(sourceName, environment, parameters.Body);
        }
        catch (ArgumentException ex)
        {
            return UpdateCredentialResult.BadRequest(Problem("invalid-credential", "Invalid credential binding", 400, ex.Message), workspace);
        }

        try
        {
            using ParsedJsonDocument<SourceCredentialBinding>? updated = await this.store.UpdateAsync(sourceName, environment, definition, WorkflowEtag.None, this.actor, this.access.Current(), cancellationToken).ConfigureAwait(false);
            return updated is { } b
                ? UpdateCredentialResult.Ok(ToSummary(b.RootElement), workspace)
                : UpdateCredentialResult.NotFound(NotFoundProblem(sourceName, environment), workspace);
        }
        catch (ArgumentException ex)
        {
            return UpdateCredentialResult.BadRequest(Problem("invalid-credential", "Invalid credential binding", 400, ex.Message), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<DeleteCredentialResult> HandleDeleteCredentialAsync(DeleteCredentialParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string sourceName = (string)parameters.SourceName;
        string environment = (string)parameters.Environment;
        bool deleted = await this.store.DeleteAsync(sourceName, environment, WorkflowEtag.None, this.access.Current(), cancellationToken).ConfigureAwait(false);
        return deleted
            ? DeleteCredentialResult.NoContent()
            : DeleteCredentialResult.NotFound(NotFoundProblem(sourceName, environment), workspace);
    }

    private static SourceCredentialDefinition ReadWrite(Models.CredentialBindingWrite body)
    {
        string sourceName = body.SourceName.IsNotUndefined() ? (string)body.SourceName : throw new ArgumentException("A 'sourceName' is required.");
        string environment = body.Environment.IsNotUndefined() ? (string)body.Environment : throw new ArgumentException("An 'environment' is required.");
        return new SourceCredentialDefinition(
            sourceName,
            environment,
            ReadAuthKind(body.AuthKind),
            ReadSecretRefs(body.SecretRefs),
            ReadConfig(body.Config),
            OptionalString(body.Description));
    }

    private static SourceCredentialDefinition ReadUpdate(string sourceName, string environment, Models.CredentialBindingUpdate body)
        => new(
            sourceName,
            environment,
            ReadAuthKind(body.AuthKind),
            ReadSecretRefs(body.SecretRefs),
            ReadConfig(body.Config),
            OptionalString(body.Description));

    private static SourceCredentialKind ReadAuthKind(Models.SourceCredentialKind authKind)
        => authKind.IsNotUndefined()
            ? SourceCredentialKindExtensions.Parse((string)authKind)
            : throw new ArgumentException("An 'authKind' is required.");

    private static List<SecretReferenceDefinition> ReadSecretRefs(Models.CredentialBindingWrite.SecretReferenceArray secretRefs)
    {
        var list = new List<SecretReferenceDefinition>();
        if (secretRefs.IsNotUndefined())
        {
            foreach (Models.SecretReference reference in secretRefs.EnumerateArray())
            {
                list.Add(new SecretReferenceDefinition((string)reference.Name, (string)reference.Ref));
            }
        }

        return list;
    }

    private static List<SecretReferenceDefinition> ReadSecretRefs(Models.CredentialBindingUpdate.SecretReferenceArray secretRefs)
    {
        var list = new List<SecretReferenceDefinition>();
        if (secretRefs.IsNotUndefined())
        {
            foreach (Models.SecretReference reference in secretRefs.EnumerateArray())
            {
                list.Add(new SecretReferenceDefinition((string)reference.Name, (string)reference.Ref));
            }
        }

        return list;
    }

    private static List<CredentialConfigDefinition>? ReadConfig(Models.CredentialBindingWrite.CredentialConfigEntryArray config)
    {
        if (!config.IsNotUndefined() || config.GetArrayLength() == 0)
        {
            return null;
        }

        var list = new List<CredentialConfigDefinition>();
        foreach (Models.CredentialConfigEntry entry in config.EnumerateArray())
        {
            list.Add(new CredentialConfigDefinition((string)entry.Key, (string)entry.Value));
        }

        return list;
    }

    private static List<CredentialConfigDefinition>? ReadConfig(Models.CredentialBindingUpdate.CredentialConfigEntryArray config)
    {
        if (!config.IsNotUndefined() || config.GetArrayLength() == 0)
        {
            return null;
        }

        var list = new List<CredentialConfigDefinition>();
        foreach (Models.CredentialConfigEntry entry in config.EnumerateArray())
        {
            list.Add(new CredentialConfigDefinition((string)entry.Key, (string)entry.Value));
        }

        return list;
    }

    private static string? OptionalString(Models.JsonString value) => value.IsNotUndefined() ? (string)value : null;

    private static List<SecurityTag> ReadTags(Models.CredentialBindingWrite.CredentialSecurityTagArray tags)
    {
        var list = new List<SecurityTag>();
        if (tags.IsNotUndefined())
        {
            foreach (Models.CredentialSecurityTag tag in tags.EnumerateArray())
            {
                list.Add(new SecurityTag((string)tag.Key, (string)tag.Value));
            }
        }

        return list;
    }

    private static List<CredentialUsageGrant> ReadGrants(Models.CredentialBindingWrite.CredentialUsageGrantArray grants)
    {
        var list = new List<CredentialUsageGrant>();
        if (grants.IsNotUndefined())
        {
            foreach (Models.CredentialUsageGrant grant in grants.EnumerateArray())
            {
                list.Add(new CredentialUsageGrant((string)grant.DimensionValue, (string)grant.Value));
            }
        }

        return list;
    }

    private static Models.CredentialSecurityTag.Source ToSecurityTag(SecurityTag tag)
        => new((ref Models.CredentialSecurityTag.Builder b) => b.Create(tag.Key, tag.Value));

    private static Models.CredentialUsageGrant.Source ToUsageGrant(CredentialUsageGrant grant)
        => new((ref Models.CredentialUsageGrant.Builder b) => b.Create(grant.Dimension, grant.Value));

    private Models.CredentialBindingSummary.Source ToSummary(SourceCredentialBinding binding)
        => new((ref Models.CredentialBindingSummary.Builder b) =>
        {
            Models.JsonString.Source description = default;
            if (binding.DescriptionOrNull is { } d)
            {
                description = d;
            }

            Models.JsonString.Source lastUpdatedBy = default;
            if (binding.LastUpdatedByOrNull is { } u)
            {
                lastUpdatedBy = u;
            }

            Models.JsonDateTime.Source lastUpdatedAt = default;
            if (binding.LastUpdatedAtValue is { } ua)
            {
                lastUpdatedAt = ua;
            }

            Models.CredentialBindingSummary.CredentialConfigEntryArray.Source config = default;
            if (binding.Config.IsNotUndefined() && binding.Config.GetArrayLength() > 0)
            {
                config = new Models.CredentialBindingSummary.CredentialConfigEntryArray.Source((ref Models.CredentialBindingSummary.CredentialConfigEntryArray.Builder ab) =>
                {
                    foreach (SourceCredentialBinding.CredentialConfigEntry entry in binding.Config.EnumerateArray())
                    {
                        ab.AddItem(ToConfigEntry((string)entry.Key, (string)entry.Value));
                    }
                });
            }

            Models.CredentialBindingSummary.CredentialSecurityTagArray.Source managementTags = default;
            if (!binding.ManagementTagsValue.IsEmpty)
            {
                managementTags = new Models.CredentialBindingSummary.CredentialSecurityTagArray.Source((ref Models.CredentialBindingSummary.CredentialSecurityTagArray.Builder ab) =>
                {
                    foreach (SecurityTag tag in binding.ManagementTagsValue.ToList())
                    {
                        ab.AddItem(ToSecurityTag(tag));
                    }
                });
            }

            // The stored usage scope is described back as operator-facing identity grants (the inverse of the create
            // mapping) — internal tags are not exposed raw.
            IReadOnlyList<CredentialUsageGrant> describedGrants = this.access.DescribeUsageScope(binding.UsageTagsValue.ToList());
            Models.CredentialBindingSummary.CredentialUsageGrantArray.Source usageGrants = default;
            if (describedGrants.Count > 0)
            {
                usageGrants = new Models.CredentialBindingSummary.CredentialUsageGrantArray.Source((ref Models.CredentialBindingSummary.CredentialUsageGrantArray.Builder ab) =>
                {
                    foreach (CredentialUsageGrant grant in describedGrants)
                    {
                        ab.AddItem(ToUsageGrant(grant));
                    }
                });
            }

            b.Create(
                authKind: binding.AuthKindValue.ToJsonToken(),
                createdAt: binding.CreatedAtValue,
                createdBy: binding.CreatedByValue,
                environment: binding.EnvironmentValue,
                etag: binding.EtagValue.Value ?? string.Empty,
                id: binding.IdValue,
                secretRefs: ToSecretRefs(binding),
                sourceName: binding.SourceNameValue,
                config: config,
                description: description,
                lastUpdatedAt: lastUpdatedAt,
                lastUpdatedBy: lastUpdatedBy,
                managementTags: managementTags,
                usageGrants: usageGrants);
        });

    private static Models.CredentialBindingSummary.SecretReferenceArray.Source ToSecretRefs(SourceCredentialBinding binding)
        => new((ref Models.CredentialBindingSummary.SecretReferenceArray.Builder ab) =>
        {
            foreach (SourceCredentialBinding.SecretReference reference in binding.SecretRefs.EnumerateArray())
            {
                string name = (string)reference.Name;
                string referenceValue = (string)reference.Ref;
                ab.AddItem(new Models.SecretReference.Source((ref Models.SecretReference.Builder sb) => sb.Create(name, referenceValue)));
            }
        });

    private static Models.CredentialConfigEntry.Source ToConfigEntry(string key, string value)
        => new((ref Models.CredentialConfigEntry.Builder b) => b.Create(key, value));

    private Models.CredentialBindingList.Source ToList(IReadOnlyList<SourceCredentialBinding> bindings)
        => new((ref Models.CredentialBindingList.Builder b) => b.Create(
            credentials: new Models.CredentialBindingList.CredentialBindingSummaryArray.Source((ref Models.CredentialBindingList.CredentialBindingSummaryArray.Builder ab) =>
            {
                foreach (SourceCredentialBinding binding in bindings)
                {
                    ab.AddItem(this.ToSummary(binding));
                }
            })));

    private static Models.ProblemDetails.Source NotFoundProblem(string sourceName, string environment)
        => Problem("credential-not-found", "Credential not found", 404, $"No source credential binding for '{sourceName}@{environment}' exists.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));
}