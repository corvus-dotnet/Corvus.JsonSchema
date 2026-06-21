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
/// is rejected at the boundary (400) by the same <see cref="SourceCredentialBinding.ValidateDraft"/> the store
/// uses, so secret material cannot be smuggled inline. Rotation is by changing the reference.</para>
/// <para>The (sourceName, environment) pair is the binding key; create conflicts (409) if one already exists, and
/// update/delete on a missing binding is a 404. Identity and created-* audit fields are immutable across updates.</para>
/// </remarks>
public sealed class ArazzoControlPlaneCredentialsHandler : IApiCredentialsHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    private static readonly TimeSpan DefaultExpiringWindow = TimeSpan.FromDays(7);

    private readonly ISourceCredentialStore store;
    private readonly ControlPlaneAccess access;
    private readonly string actor;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan expiringWindow;

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
    /// <param name="timeProvider">The clock used to derive each binding's <see cref="CredentialStatus"/> on read
    /// (defaults to <see cref="TimeProvider.System"/>).</param>
    /// <param name="expiringWindow">How far ahead of expiry a still-valid credential is reported as
    /// <see cref="CredentialStatus.ExpiringSoon"/> (defaults to 7 days).</param>
    internal ArazzoControlPlaneCredentialsHandler(ISourceCredentialStore store, ControlPlaneAccess access, string actor = "control-plane", TimeProvider? timeProvider = null, TimeSpan? expiringWindow = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(actor);
        this.store = store;
        this.access = access;
        this.actor = actor;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.expiringWindow = expiringWindow ?? DefaultExpiringWindow;
    }

    /// <inheritdoc/>
    public async ValueTask<ListCredentialsResult> HandleListCredentialsAsync(ListCredentialsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 100;

        // The opaque page token flows to the store as its JSON value (From() rewraps parameters.PageToken — free, no
        // reify, no managed string; an undefined token rewraps to an undefined JsonString); the store decodes it
        // bytes-native from the request UTF-8.
        JsonString pageToken = JsonString.From(parameters.PageToken);
        using SourceCredentialPage page = await this.store.ListAsync(this.access.Current(), limit, pageToken, cancellationToken).ConfigureAwait(false);
        return ListCredentialsResult.Ok(this.ToList(page.Bindings, page.NextPageToken), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<CreateCredentialResult> HandleCreateCredentialAsync(CreateCredentialParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Models.CredentialBindingWrite body = parameters.Body;
        SecurityTagSet managementTags;
        SecurityTagSet usageTags;
        try
        {
            // managementTags = the principal's deployment-internal tenant tag (always stamped, so the owner keeps
            // management) PLUS any operator-supplied management labels (validated against the reserved internal prefix).
            IReadOnlyList<SecurityTag> userManagement = ReadTags(body.ManagementTags);
            this.access.ValidateUserTags(userManagement);
            var management = new List<SecurityTag>(this.access.InternalTags());
            management.AddRange(userManagement);
            managementTags = SecurityTagSet.FromTags(management);

            // usageTags are derived from the operator's usage GRANTS, which the deployment maps to UNFORGEABLE internal
            // identity tags (e.g. sys:workflow=nightly-reconcile) — never free-form labels, so usage cannot be
            // self-granted by a workflow author. The grants are resolved BYTES-TO-BYTES through the span seam (the same
            // ResolveUsageGrantInto the administrators handler uses): each grant's {dimension, value} is read as UTF-8
            // and the resolved tag written straight into the pooled buffer — no managed string or grant list. With no
            // grants, usage defaults to the creating principal's own identity (the owner's runs). The two scopes are
            // independent (§13/§14.2).
            if (body.UsageGrants.IsNotUndefined() && body.UsageGrants.GetArrayLength() > 0)
            {
                var grantsState = new UsageGrantsState(this.access, body.UsageGrants);
                usageTags = SecurityTagSet.Build(in grantsState, BuildUsageGrants);
            }
            else
            {
                usageTags = SecurityTagSet.FromTags(this.access.InternalTags());
            }

            // The required request fields are validated up front; their JSON values are then carried into the draft
            // bytes-to-bytes (no per-field strings, no list) — see the draft build below.
            if (!body.SourceName.IsNotUndefined())
            {
                throw new ArgumentException("A 'sourceName' is required.");
            }

            if (!body.Environment.IsNotUndefined())
            {
                throw new ArgumentException("An 'environment' is required.");
            }

            _ = ReadAuthKind(body.AuthKind);
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
            // The persisted binding carries the request body's reference/metadata JSON values bytes-to-bytes (no
            // per-field strings, no list) plus the resolved management/usage tags; the store stamps id/createdBy/createdAt/etag.
            using ParsedJsonDocument<SourceCredentialBinding> draft = SourceCredentialBinding.Draft(
                sourceName: (JsonElement)body.SourceName,
                environment: (JsonElement)body.Environment,
                authKind: (JsonElement)body.AuthKind,
                secretRefs: (JsonElement)body.SecretRefs,
                config: (JsonElement)body.Config,
                description: (JsonElement)body.Description,
                expiresAt: (JsonElement)body.ExpiresAt,
                rotatedAt: (JsonElement)body.RotatedAt,
                managementTags: managementTags,
                usageTags: usageTags);
            using ParsedJsonDocument<SourceCredentialBinding> created = await this.store.AddAsync(draft.RootElement, this.actor, cancellationToken).ConfigureAwait(false);
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
        Models.CredentialBindingUpdate body = parameters.Body;
        try
        {
            _ = ReadAuthKind(body.AuthKind);
        }
        catch (ArgumentException ex)
        {
            return UpdateCredentialResult.BadRequest(Problem("invalid-credential", "Invalid credential binding", 400, ex.Message), workspace);
        }

        try
        {
            // Only the mutable content is carried bytes-to-bytes; the immutable identity (sourceName, environment) and the
            // security tags are carried forward from the stored binding by the store, so the draft omits them.
            using ParsedJsonDocument<SourceCredentialBinding> draft = SourceCredentialBinding.Draft(
                sourceName: default,
                environment: default,
                authKind: (JsonElement)body.AuthKind,
                secretRefs: (JsonElement)body.SecretRefs,
                config: (JsonElement)body.Config,
                description: (JsonElement)body.Description,
                expiresAt: (JsonElement)body.ExpiresAt,
                rotatedAt: (JsonElement)body.RotatedAt,
                managementTags: default,
                usageTags: default);
            using ParsedJsonDocument<SourceCredentialBinding>? updated = await this.store.UpdateAsync(sourceName, environment, draft.RootElement, WorkflowEtag.None, this.actor, this.access.Current(), cancellationToken).ConfigureAwait(false);
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

    private static SourceCredentialKind ReadAuthKind(Models.SourceCredentialKind authKind)
        => authKind.IsNotUndefined()
            ? SourceCredentialKindExtensions.Parse((string)authKind)
            : throw new ArgumentException("An 'authKind' is required.");

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

    // Builds the binding's usage-identity tags from the operator's usage grants, reading each grant's {dimension, value}
    // as UTF-8 and writing the deployment's resolved internal tag straight into the pooled buffer (the bytes-to-bytes
    // counterpart of the string ResolveUsageGrants — a deployment that remaps grants overrides ResolveUsageGrantInto too).
    private static void BuildUsageGrants(ref IdentityBuilder builder, in UsageGrantsState state)
    {
        foreach (Models.CredentialUsageGrant grant in state.Grants.EnumerateArray())
        {
            using UnescapedUtf8JsonString dimension = grant.DimensionValue.GetUtf8String();
            using UnescapedUtf8JsonString value = grant.Value.GetUtf8String();
            state.Access.ResolveUsageGrantInto(dimension.Span, value.Span, ref builder);
        }
    }

    private readonly ref struct UsageGrantsState(ControlPlaneAccess access, Models.CredentialBindingWrite.CredentialUsageGrantArray grants)
    {
        public ControlPlaneAccess Access { get; } = access;

        public Models.CredentialBindingWrite.CredentialUsageGrantArray Grants { get; } = grants;
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

            Models.JsonDateTime.Source expiresAt = default;
            if (binding.ExpiresAtOrNull is { } ea)
            {
                expiresAt = ea;
            }

            Models.JsonDateTime.Source rotatedAt = default;
            if (binding.RotatedAtOrNull is { } ra)
            {
                rotatedAt = ra;
            }

            // credentialStatus is derived from expiresAt against the current time (§13.2) — never persisted, so it
            // cannot go stale.
            CredentialStatus status = binding.DeriveStatus(this.timeProvider.GetUtcNow(), this.expiringWindow);

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
                    foreach (SecurityTag tag in binding.ManagementTagsValue)
                    {
                        ab.AddItem(ToSecurityTag(tag));
                    }
                });
            }

            // The stored usage scope is described back as operator-facing identity grants (the inverse of the create
            // mapping) — internal tags are not exposed raw.
            IReadOnlyList<CredentialUsageGrant> describedGrants = this.access.DescribeUsageScope(binding.UsageTagsValue);
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
                credentialStatus: ToStatusToken(status),
                environment: binding.EnvironmentValue,
                etag: binding.EtagValue.Value ?? string.Empty,
                id: binding.IdValue,
                secretRefs: ToSecretRefs(binding),
                sourceName: binding.SourceNameValue,
                config: config,
                description: description,
                expiresAt: expiresAt,
                lastUpdatedAt: lastUpdatedAt,
                lastUpdatedBy: lastUpdatedBy,
                managementTags: managementTags,
                rotatedAt: rotatedAt,
                usageGrants: usageGrants);
        });

    private static string ToStatusToken(CredentialStatus status) => status switch
    {
        CredentialStatus.Valid => "valid",
        CredentialStatus.ExpiringSoon => "expiringSoon",
        CredentialStatus.Expired => "expired",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown credential status."),
    };

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

    private Models.CredentialBindingList.Source ToList(IReadOnlyList<SourceCredentialBinding> bindings, ReadOnlyMemory<byte> nextPageToken)
        => new((ref Models.CredentialBindingList.Builder b) => b.Create(
            credentials: new Models.CredentialBindingList.CredentialBindingSummaryArray.Source((ref Models.CredentialBindingList.CredentialBindingSummaryArray.Builder ab) =>
            {
                foreach (SourceCredentialBinding binding in bindings)
                {
                    ab.AddItem(this.ToSummary(binding));
                }
            }),
            nextPageToken: nextPageToken.IsEmpty ? default : (Models.JsonString.Source)nextPageToken.Span));

    private static Models.ProblemDetails.Source NotFoundProblem(string sourceName, string environment)
        => Problem("credential-not-found", "Credential not found", 404, $"No source credential binding for '{sourceName}@{environment}' exists.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));
}