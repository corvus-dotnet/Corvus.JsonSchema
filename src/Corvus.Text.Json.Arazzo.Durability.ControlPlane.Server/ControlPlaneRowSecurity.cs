// <copyright file="ControlPlaneRowSecurity.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Security.Claims;
using System.Text;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.AspNetCore.Http;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// A deployment's row-security policy (design §14.2/§14.3): it turns the authenticated
/// <see cref="ClaimsPrincipal"/> into the caller's <see cref="AccessContext"/> — the row reach the principal has
/// for each <see cref="AccessVerb"/>, so read can be granted independently of write and purge. It also supplies
/// the internal tags stamped onto rows the principal creates (e.g. <c>sys:tenant=acme</c>) and validates
/// user-supplied tags. A deployment typically implements this over a <see cref="SecurityShell"/>: the shell owns
/// the mandated wrapper rule, the reserved prefix, and tag stripping, and this policy maps the principal's claims
/// onto it.
/// </summary>
/// <remarks>
/// Authentication and how a principal acquires claims are always the host's concern (§14.1). The control plane
/// depends only on this policy and a <see cref="ClaimsPrincipal"/> read through <see cref="IHttpContextAccessor"/>.
/// Pass an instance to <c>MapArazzoControlPlane(..., rowSecurity:)</c> (and call
/// <c>services.AddHttpContextAccessor()</c>) to switch enforcement on; omit it (the default) and the control plane
/// runs with <see cref="AccessContext.System"/> throughout — fully unrestricted, behaviour unchanged.
/// </remarks>
public abstract class ControlPlaneRowSecurityPolicy
{
    private byte[]? internalTagPrefixUtf8;
    private byte[]? ownerGroupTagKeyUtf8;

    // A shell with no mandated rules reserves only the prefix (its documented degenerate form), so the default
    // validation below refuses the reserved keyspace through the same validator, and reports the same message, that a
    // shell-backed deployment uses. Built once per policy instance from this policy's own configured prefix.
    private SecurityShell? reservedKeyspace;

    /// <summary>
    /// Resolves the caller's access grant from the authenticated principal. The grant's per-verb reach scopes
    /// every list/search and gates each single-row read/write/purge — there is no unscoped path. Return
    /// <see cref="AccessContext.System"/> (or a context with <see langword="null"/> reaches) for an unrestricted
    /// principal such as an operator.
    /// </summary>
    /// <param name="principal">The authenticated principal (<see langword="null"/> if the request is unauthenticated).</param>
    /// <returns>The caller's access grant; never <see langword="null"/>.</returns>
    public abstract AccessContext Resolve(ClaimsPrincipal? principal);

    /// <summary>
    /// Resolves the capability scopes (§14.1) this policy grants the principal directly — the Arazzo-plane
    /// per-principal grants that are <em>unioned</em> with the scopes the principal already carries in claims, so an
    /// access-request approval can grant run/admin capability (e.g. <c>runs:write</c>) without mutating the IdP. The
    /// deployment stamps these into the principal's effective scopes at authentication time (see the entitlement
    /// claims transformer). The default grants none — scopes come solely from claims.
    /// </summary>
    /// <param name="principal">The authenticated principal (<see langword="null"/> if unauthenticated).</param>
    /// <returns>The granted scopes (empty when none); the common path allocates nothing.</returns>
    public virtual IReadOnlyList<string> ResolveGrantedScopes(ClaimsPrincipal? principal) => [];

    /// <summary>
    /// Returns the deployment-internal security tags to stamp onto rows the principal creates (e.g. the
    /// principal's tenant as <c>sys:tenant=acme</c>), making the new row owned by the principal's slice of the
    /// shell. These carry the reserved internal prefix and are not user-editable. The default stamps nothing.
    /// </summary>
    /// <param name="principal">The authenticated principal creating the row.</param>
    /// <returns>The internal tags to stamp; empty to stamp none.</returns>
    public virtual IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal) => [];

    /// <summary>
    /// Validates user-supplied security tags before a row is created. The default refuses any tag whose key carries
    /// this policy's reserved internal prefix; override to add rules of your own.
    /// </summary>
    /// <param name="userTags">The security tags supplied through the user-facing API, as a (typically non-owning)
    /// <see cref="SecurityTagSet"/> over the request body — string-free so the default need not materialize a tag list
    /// (the offending key becomes a string only on the rejection path).</param>
    /// <exception cref="ArgumentException">A user tag is not permitted (e.g. it uses the reserved internal prefix).</exception>
    /// <remarks>
    /// This default used to accept everything, leaving the reserved keyspace guarded only by whatever the deployment's
    /// policy happened to do. That keyspace carries the deployment's own stamps — the identity the §7.7/§15
    /// administrator gate matches and, under ADR 0065, the owner group — so a client that can write into it can name
    /// itself into someone else's. Refusing it is the policy-independent floor; a deployment opts out deliberately by
    /// overriding, not by omission.
    /// </remarks>
    public virtual void ValidateUserTags(SecurityTagSet userTags)
        => (this.reservedKeyspace ??= new SecurityShell([], this.InternalTagPrefix)).ValidateUserTags(userTags);

    /// <summary>Whether a security-tag key (as unescaped UTF-8) is deployment-internal — its key carries the reserved
    /// prefix. The span-based check used to strip internal tags from client responses (§14.2), so it never materializes
    /// a managed string per tag.</summary>
    /// <param name="keyUtf8">The tag key as unescaped UTF-8.</param>
    /// <returns><see langword="true"/> if the key carries the reserved internal prefix.</returns>
    public bool IsInternalTag(ReadOnlySpan<byte> keyUtf8) => keyUtf8.StartsWith(this.InternalTagPrefixUtf8);

    /// <summary>Strips the reserved internal-tag prefix from a resolved identity dimension (as unescaped UTF-8),
    /// recovering the operator-facing claim a binding keys on (<c>sys:sub</c> → <c>sub</c>); a dimension without the
    /// prefix (a bare team/role) is returned unchanged. The span-based counterpart of <see cref="IsInternalTag"/>, over
    /// the cached UTF-8 prefix, so a caller matches the access-grants overview to runtime enforcement without a hardcoded
    /// literal prefix.</summary>
    /// <param name="dimensionUtf8">The resolved identity dimension key as unescaped UTF-8.</param>
    /// <returns>The dimension with the internal prefix removed if present; otherwise unchanged (a slice of the input).</returns>
    public ReadOnlySpan<byte> StripInternalPrefix(ReadOnlySpan<byte> dimensionUtf8)
        => dimensionUtf8.StartsWith(this.InternalTagPrefixUtf8) ? dimensionUtf8[this.InternalTagPrefixUtf8.Length..] : dimensionUtf8;

    /// <summary>Gets the reserved internal-tag key prefix (the public accessor of <see cref="InternalTagPrefix"/>) — the
    /// security wrapper reads it to preserve a version's internal tags across an admin re-tag (§14.2).</summary>
    public string InternalTagKeyPrefix => this.InternalTagPrefix;

    /// <summary>Gets the reserved internal tag prefix this policy stamps/maps to (default <see cref="SecurityShell.DefaultInternalPrefix"/>).</summary>
    protected virtual string InternalTagPrefix => SecurityShell.DefaultInternalPrefix;

    /// <summary>The reserved internal tag prefix as UTF-8 — computed once per instance from <see cref="InternalTagPrefix"/>
    /// and cached, so the span-based <see cref="TryDescribeUsageGrant"/> never re-encodes it per tag.</summary>
    protected ReadOnlySpan<byte> InternalTagPrefixUtf8 => this.internalTagPrefixUtf8 ??= Encoding.UTF8.GetBytes(this.InternalTagPrefix);

    /// <summary>Gets the internal tag key an owner group is stamped under (ADR 0065) as UTF-8 — this policy's reserved
    /// prefix followed by the <c>tenant</c> dimension, e.g. <c>sys:tenant</c>.</summary>
    /// <remarks>
    /// Derived from immutable configuration, so it is built once per policy instance rather than per request or per
    /// row: the tenancy scan compares it against every environment's tags, and re-encoding a fixed key on that path
    /// would allocate once per row to reproduce a value that never changes. Built without an intermediate concatenated
    /// string for the same reason.
    /// </remarks>
    public ReadOnlySpan<byte> OwnerGroupTagKeyUtf8 => this.ownerGroupTagKeyUtf8 ??= OwnerGroupTag.KeyFor(this.InternalTagPrefix);

    // The stack budget for a built internal-tag key (prefix + dimension); both are short identifiers, so this is never
    // exceeded in practice — a longer dimension falls back to a pooled rent.
    private const int StackKeyThreshold = 256;

    /// <summary>Maps operator-supplied source credential usage grants (§13) to the <strong>internal</strong> security
    /// tags a run must carry to use the binding — so a binding's usage scope is expressed in unforgeable, deployment-
    /// stamped identity terms rather than free-form labels. The default maps a grant <c>{dimension, value}</c> to the
    /// internal tag <c>{prefix+dimension = value}</c> (e.g. <c>{workflow, nightly-reconcile}</c> →
    /// <c>sys:workflow=nightly-reconcile</c>); a deployment may override to restrict which grants are permitted.</summary>
    /// <param name="grants">The operator-supplied usage grants.</param>
    /// <returns>The internal usage tags for the binding.</returns>
    public virtual IReadOnlyList<SecurityTag> ResolveUsageGrants(IReadOnlyList<CredentialUsageGrant> grants)
    {
        if (grants.Count == 0)
        {
            return [];
        }

        var tags = new List<SecurityTag>(grants.Count);
        foreach (CredentialUsageGrant grant in grants)
        {
            tags.Add(new SecurityTag(this.InternalTagPrefix + grant.Dimension, grant.Value));
        }

        return tags;
    }

    /// <summary>
    /// Resolves one operator-facing grant (<paramref name="dimension"/>, <paramref name="value"/>) — given as unescaped
    /// UTF-8 — to its internal identity tag, written <strong>bytes to bytes</strong> into <paramref name="builder"/>: the
    /// span counterpart of <see cref="ResolveUsageGrants"/>, so a grantee identity is built from a request without
    /// materializing a managed <see cref="string"/> per dimension/value. The default writes the single tag
    /// <c>{prefix+dimension = value}</c> (the same mapping as <see cref="ResolveUsageGrants"/>). A deployment that
    /// overrides <see cref="ResolveUsageGrants"/> to remap or restrict grants should override this too, to keep the two
    /// resolution paths consistent (write nothing to decline a grant).
    /// </summary>
    /// <param name="dimension">The grant dimension as unescaped UTF-8.</param>
    /// <param name="value">The grant value as unescaped UTF-8.</param>
    /// <param name="builder">The identity builder writing into a pooled buffer.</param>
    public virtual void ResolveUsageGrantInto(ReadOnlySpan<byte> dimension, ReadOnlySpan<byte> value, ref IdentityBuilder builder)
    {
        // The default mapping (mirroring ResolveUsageGrants): the internal tag key is the reserved prefix concatenated
        // with the dimension, built in a pooled/stack key buffer (both are short identifiers); the value is written
        // verbatim. No managed string. A deployment that maps grants differently overrides THIS method (in spans).
        string prefix = this.InternalTagPrefix;
        int prefixLength = Encoding.UTF8.GetByteCount(prefix);
        int keyLength = prefixLength + dimension.Length;
        byte[]? rented = null;
        Span<byte> key = keyLength <= StackKeyThreshold ? stackalloc byte[StackKeyThreshold] : (rented = ArrayPool<byte>.Shared.Rent(keyLength));
        try
        {
            Encoding.UTF8.GetBytes(prefix, key);
            dimension.CopyTo(key[prefixLength..]);
            builder.Add(key[..keyLength], value);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Describes a binding's stored internal usage tags back as operator-facing grants (the inverse of
    /// <see cref="ResolveUsageGrants"/>) for the management API response. The default strips the internal prefix from
    /// each tag's key; tags without the prefix are ignored.</summary>
    /// <param name="usageTags">The binding's internal usage tags.</param>
    /// <returns>The usage grants.</returns>
    /// <remarks>Takes the <see cref="SecurityTagSet"/> holder and drives the projection from its allocation-free
    /// enumerator — the grant key/value strings are the genuine response leaf, but the intermediate
    /// <see cref="List{T}"/> a <c>ToList()</c> would build is not, so this is iterated directly (it runs per row of the
    /// credentials/administrators/grantee list responses).</remarks>
    public virtual IReadOnlyList<CredentialUsageGrant> DescribeUsageScope(SecurityTagSet usageTags)
    {
        if (usageTags.IsEmpty)
        {
            return [];
        }

        var grants = new List<CredentialUsageGrant>();
        foreach (SecurityTag tag in usageTags)
        {
            if (tag.Key.StartsWith(this.InternalTagPrefix, StringComparison.Ordinal))
            {
                grants.Add(new CredentialUsageGrant(tag.Key[this.InternalTagPrefix.Length..], tag.Value));
            }
        }

        return grants;
    }

    /// <summary>The allocation-free, span-based counterpart of <see cref="DescribeUsageScope"/> (the inverse of
    /// <see cref="ResolveUsageGrantInto"/>): decides whether a stored internal tag key maps to an operator-facing grant
    /// and, if so, yields its dimension (the key with the reserved prefix stripped) as a slice of the input — no managed
    /// <see cref="string"/>, no list. The grant value passes through verbatim, so the caller writes
    /// <c>(dimension, value)</c>. A deployment that overrides <see cref="DescribeUsageScope"/> to remap the inverse
    /// projection should override this too, to keep the list and span paths consistent (return <see langword="false"/> to
    /// hide a tag).</summary>
    /// <param name="keyUtf8">The stored tag key as unescaped UTF-8.</param>
    /// <param name="dimensionUtf8">The grant dimension (a slice of <paramref name="keyUtf8"/>) when the tag is described.</param>
    /// <returns><see langword="true"/> if the tag maps to a grant; <see langword="false"/> to skip it.</returns>
    public virtual bool TryDescribeUsageGrant(ReadOnlySpan<byte> keyUtf8, out ReadOnlySpan<byte> dimensionUtf8)
    {
        ReadOnlySpan<byte> prefix = this.InternalTagPrefixUtf8;
        if (keyUtf8.StartsWith(prefix))
        {
            dimensionUtf8 = keyUtf8[prefix.Length..];
            return true;
        }

        dimensionUtf8 = default;
        return false;
    }

    /// <summary>
    /// The grantee kinds this policy can resolve to a <c>sys:</c> identity (design §16.5.4) — what the grantee picker
    /// may offer for view / operate / administer grants, so an operator never guesses a dimension. The default supports
    /// none; a deployment opts in to the kinds whose identities it actually stamps.
    /// </summary>
    public virtual IReadOnlyList<GranteeKind> SupportedGranteeKinds => [];

    /// <summary>
    /// Resolves a chosen grantee (<paramref name="kind"/>, <paramref name="value"/>) to its exact deployment-stamped
    /// <c>sys:</c> identity (design §16.5.4) — the identity an administrator entry or entitlement grant must name to
    /// match this principal (membership is exact set-equality). The default maps the kind to its dimension
    /// (<c>person→sub</c>, <c>team→tenant</c>, <c>role→role</c>, <c>workflow→workflow</c>) and produces the single tag
    /// <c>{prefix+dimension = value}</c> — best-effort for a free-typed value; a deployment overrides to resolve a
    /// multi-tag identity (e.g. a person to <c>{sys:tenant, sys:sub}</c>), typically via an <see cref="IPrincipalDirectory"/>.
    /// </summary>
    /// <param name="kind">The grantee kind.</param>
    /// <param name="value">The grantee value (a subject id, tenant name, role name, or workflow id).</param>
    /// <returns>The grantee's exact <c>sys:</c> identity.</returns>
    public virtual SecurityTagSet ResolveGranteeIdentity(GranteeKind kind, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        return SecurityTagSet.FromTags([new SecurityTag(this.InternalTagPrefix + GranteeDimension(kind), value)]);
    }

    /// <summary>
    /// Whether a single-tag resolution of <paramref name="kind"/> (the <see cref="ResolveGranteeIdentity"/> default) is the
    /// principal's <strong>whole</strong> stamped identity (§17.2) — so a grant naming it matches by exact set-equality —
    /// or a best-effort partial mapping. The default treats <see cref="GranteeKind.Person"/> as partial (a person is
    /// typically multi-tag, e.g. <c>{sys:tenant, sys:sub}</c>) and team/role/workflow as whole-grain; a deployment
    /// overrides to match its own stamping grain (e.g. one that stamps only <c>sys:sub</c> for people returns true here).
    /// </summary>
    /// <param name="kind">The grantee kind.</param>
    /// <returns><see langword="true"/> if a single-tag resolution of the kind is the whole identity.</returns>
    public virtual bool IsWholeGrainGrantee(GranteeKind kind) => kind != GranteeKind.Person;

    /// <summary>Maps a <see cref="GranteeKind"/> to the usage-grant dimension it resolves through (the part after the
    /// internal prefix, e.g. <see cref="GranteeKind.Team"/> → <c>tenant</c> → <c>sys:tenant</c>).</summary>
    /// <param name="kind">The grantee kind.</param>
    /// <returns>The dimension name.</returns>
    protected static string GranteeDimension(GranteeKind kind) => kind switch
    {
        GranteeKind.Person => "sub",
        GranteeKind.Team => "tenant",
        GranteeKind.Role => "role",
        GranteeKind.Workflow => "workflow",
        _ => throw ServerThrowHelper.GetUnknownGranteeKindException(kind),
    };
}

/// <summary>
/// Binds a <see cref="ControlPlaneRowSecurityPolicy"/> to the current request's principal (read through
/// <see cref="IHttpContextAccessor"/>) so the handlers can resolve the caller's <see cref="AccessContext"/>
/// without depending on ASP.NET Core themselves. When constructed without a policy it yields
/// <see cref="AccessContext.System"/> for every request — the unscoped default.
/// </summary>
internal sealed class ControlPlaneAccess
{
    // With no policy there is no configured prefix, so the reserved keyspace is the default one. Shared across
    // requests: a shell with no mandated rules is immutable and holds only the prefix.
    private static readonly SecurityShell DefaultReservedKeyspace = new([]);

    private readonly IHttpContextAccessor? httpContextAccessor;
    private readonly ControlPlaneRowSecurityPolicy? policy;

    /// <summary>Initializes an unscoped instance: every request resolves to <see cref="AccessContext.System"/>.</summary>
    public ControlPlaneAccess()
    {
    }

    /// <summary>
    /// Initializes an instance that has no row-security policy but can still see the authenticated principal, for the
    /// modes that authenticate without scoping reach (ADR 0016's <c>ScopesOnly</c>, and <c>Open</c> where there is no
    /// principal to see).
    /// </summary>
    /// <param name="httpContextAccessor">The accessor used to read the current request's principal.</param>
    /// <param name="ownerGroupClaimType">The claim type carrying the caller's owner group, or <see langword="null"/> to
    /// stamp no ownership.</param>
    /// <remarks>
    /// <para>Reach is unaffected: with no policy, <see cref="Current"/> still resolves to
    /// <see cref="AccessContext.System"/>. What this adds is that a handler can identify <em>who</em> is acting.
    /// Without it, an audit actor in <c>ScopesOnly</c> degrades to a constant, and ownership cannot be determined at
    /// all, which is what made ADR 0065's tenancy invariant unevaluable in that mode.</para>
    /// <para>Ownership and reach are different questions (ADR 0065), so <see cref="InternalTags"/> resolves the owner
    /// group from <paramref name="ownerGroupClaimType"/> here even though the deployment supplied no policy. A
    /// principal carrying no such claim stamps nothing: the deployment cannot tell owner groups apart, so it has one,
    /// and stamping the subject instead would make every individual user its own tenant.</para>
    /// <para>This is expressed as a real (internal) policy rather than as a special case beside a null one. Every
    /// member that reads the deployment's identity conventions — is this key internal, strip its prefix, describe it
    /// as a grant, resolve a grantee — short-circuits when there is no policy, on the premise that a deployment
    /// without one stamps no internal tags. Stamping an owner group falsifies that premise, and a stamp the response
    /// filter no longer recognises as internal is a stamp it stops stripping. A policy whose reach is
    /// <see cref="AccessContext.System"/> keeps every one of those members on its normal path.</para>
    /// </remarks>
    public ControlPlaneAccess(IHttpContextAccessor httpContextAccessor, string? ownerGroupClaimType)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        this.httpContextAccessor = httpContextAccessor;
        this.policy = string.IsNullOrEmpty(ownerGroupClaimType) ? null : new OwnerClaimPolicy(ownerGroupClaimType);
    }

    /// <summary>Initializes a scoped instance backed by a deployment policy.</summary>
    /// <param name="httpContextAccessor">The accessor used to read the current request's principal.</param>
    /// <param name="policy">The deployment's row-security policy.</param>
    public ControlPlaneAccess(IHttpContextAccessor httpContextAccessor, ControlPlaneRowSecurityPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        ArgumentNullException.ThrowIfNull(policy);
        this.httpContextAccessor = httpContextAccessor;
        this.policy = policy;
    }

    private ClaimsPrincipal? Principal => this.httpContextAccessor?.HttpContext?.User;

    /// <summary>Gets the current request's authenticated principal (e.g. for a handler that needs a specific claim such
    /// as the requesting subject); <see langword="null"/> when unscoped or unauthenticated.</summary>
    internal ClaimsPrincipal? CurrentPrincipal => this.Principal;

    /// <summary>Resolves the current request's access grant (<see cref="AccessContext.System"/> when unscoped).</summary>
    /// <returns>The caller's access grant.</returns>
    public AccessContext Current() => this.policy is null ? AccessContext.System : this.policy.Resolve(this.Principal);

    /// <summary>Returns the internal tags to stamp onto a row the current principal creates — the caller's deployment
    /// identity, which is also what the §7.7/§15 current-administrator gate matches against.</summary>
    /// <returns>The internal tags; empty when the deployment authenticates nobody, or when an authenticated principal
    /// carries no owner-group claim.</returns>
    /// <remarks>
    /// A deployment-supplied policy owns the answer outright (it may stamp several dimensions, and a deployment that
    /// has one has already said how identity is derived). Only in its absence does the owner-group claim supply one,
    /// through the internal policy the constructor builds, so the two paths cannot disagree about who owns a row.
    /// </remarks>
    public IReadOnlyList<SecurityTag> InternalTags() => this.policy?.GetInternalTags(this.Principal) ?? [];

    /// <summary>Validates user-supplied security tags for the current principal: the reserved internal keyspace is
    /// refused whatever the deployment's posture, then the policy applies any further rules of its own.</summary>
    /// <param name="userTags">The user-supplied security tags.</param>
    /// <exception cref="ArgumentException">A user tag is not permitted.</exception>
    /// <remarks>
    /// The prefix check cannot be left to the policy. With no policy this was a no-op, so a client could supply
    /// <c>sys:tenant=…</c> in a create body and have it stamped verbatim — naming itself into another owner group. In
    /// the reach-enforcing modes the reach check caught that as a side effect (a tag outside your own reach is refused),
    /// but <c>ScopesOnly</c> grants unrestricted reach, so nothing caught it there. The keyspace belongs to the
    /// deployment in every mode, so the refusal belongs here rather than in an optional collaborator.
    /// </remarks>
    public void ValidateUserTags(SecurityTagSet userTags)
    {
        if (this.policy is { } p)
        {
            p.ValidateUserTags(userTags);
            return;
        }

        DefaultReservedKeyspace.ValidateUserTags(userTags);
    }

    /// <summary>Whether a security-tag key (as unescaped UTF-8) is deployment-internal and must be stripped from client
    /// responses (§14.2). An unscoped deployment stamps no internal tags, so nothing is internal.</summary>
    /// <param name="keyUtf8">The tag key as unescaped UTF-8.</param>
    /// <returns><see langword="true"/> if the key carries the reserved internal prefix.</returns>
    public bool IsInternalTag(ReadOnlySpan<byte> keyUtf8) => this.policy?.IsInternalTag(keyUtf8) ?? false;

    /// <summary>Strips the deployment-configured internal-tag prefix from a resolved identity dimension (UTF-8),
    /// recovering the operator-facing claim a binding keys on (<c>sys:sub</c> → <c>sub</c>); a dimension without the
    /// prefix, or an unscoped deployment (no policy), returns it unchanged. The access-grants overview matches bindings
    /// to a grantee through this, so it uses the same prefix runtime enforcement does rather than a hardcoded literal.</summary>
    /// <param name="dimensionUtf8">The resolved identity dimension key as unescaped UTF-8.</param>
    /// <returns>The dimension with the internal prefix removed if present; otherwise unchanged.</returns>
    public ReadOnlySpan<byte> StripInternalPrefix(ReadOnlySpan<byte> dimensionUtf8)
        => this.policy is { } p ? p.StripInternalPrefix(dimensionUtf8) : dimensionUtf8;

    /// <summary>Gets the reserved internal-tag key prefix for the current deployment — the security wrapper uses it to
    /// preserve a version's internal tags across an admin re-tag (§14.2). Falls back to the default when unscoped (no
    /// internal tags are stamped then, so the prefix is not exercised).</summary>
    public string InternalTagPrefix => this.policy?.InternalTagKeyPrefix ?? SecurityShell.DefaultInternalPrefix;

    /// <summary>Gets the internal tag key an owner group is stamped under (ADR 0065) as UTF-8, e.g. <c>sys:tenant</c> —
    /// the key the tenancy scan matches each environment's tags against. Cached by the policy, or the default keyspace's
    /// when the deployment stamps nothing, so the scan never re-encodes it.</summary>
    public ReadOnlySpan<byte> OwnerGroupTagKeyUtf8 => this.policy is { } p ? p.OwnerGroupTagKeyUtf8 : OwnerGroupTag.DefaultKeyUtf8;

    /// <summary>Maps source credential usage grants to internal usage tags (empty when unscoped).</summary>
    /// <param name="grants">The operator-supplied usage grants.</param>
    /// <returns>The internal usage tags.</returns>
    public IReadOnlyList<SecurityTag> ResolveUsageGrants(IReadOnlyList<CredentialUsageGrant> grants) => this.policy?.ResolveUsageGrants(grants) ?? [];

    /// <summary>Resolves one grant (UTF-8 dimension/value) into <paramref name="builder"/> bytes-to-bytes (writes nothing when unscoped).</summary>
    /// <param name="dimension">The grant dimension as unescaped UTF-8.</param>
    /// <param name="value">The grant value as unescaped UTF-8.</param>
    /// <param name="builder">The identity builder writing into a pooled buffer.</param>
    public void ResolveUsageGrantInto(ReadOnlySpan<byte> dimension, ReadOnlySpan<byte> value, ref IdentityBuilder builder)
        => this.policy?.ResolveUsageGrantInto(dimension, value, ref builder);

    /// <summary>Describes internal usage tags back as operator-facing grants (empty when unscoped).</summary>
    /// <param name="usageTags">The binding's internal usage tags.</param>
    /// <returns>The usage grants.</returns>
    public IReadOnlyList<CredentialUsageGrant> DescribeUsageScope(SecurityTagSet usageTags) => this.policy?.DescribeUsageScope(usageTags) ?? [];

    /// <summary>The span-based, allocation-free counterpart of <see cref="DescribeUsageScope"/> (see
    /// <see cref="ControlPlaneRowSecurityPolicy.TryDescribeUsageGrant"/>): an unscoped deployment (no policy) describes
    /// nothing, so every tag is skipped.</summary>
    /// <param name="keyUtf8">The stored tag key as unescaped UTF-8.</param>
    /// <param name="dimensionUtf8">The grant dimension (a slice of <paramref name="keyUtf8"/>) when described.</param>
    /// <returns><see langword="true"/> if the tag maps to a grant.</returns>
    public bool TryDescribeUsageGrant(ReadOnlySpan<byte> keyUtf8, out ReadOnlySpan<byte> dimensionUtf8)
    {
        if (this.policy is null)
        {
            dimensionUtf8 = default;
            return false;
        }

        return this.policy.TryDescribeUsageGrant(keyUtf8, out dimensionUtf8);
    }

    /// <summary>Gets the grantee kinds the deployment can resolve (empty when unscoped).</summary>
    /// <returns>The supported grantee kinds.</returns>
    public IReadOnlyList<GranteeKind> SupportedGranteeKinds() => this.policy?.SupportedGranteeKinds ?? [];

    /// <summary>Resolves a grantee to its exact <c>sys:</c> identity (empty when unscoped).</summary>
    /// <param name="kind">The grantee kind.</param>
    /// <param name="value">The grantee value.</param>
    /// <returns>The grantee's identity.</returns>
    public SecurityTagSet ResolveGranteeIdentity(GranteeKind kind, string value) => this.policy?.ResolveGranteeIdentity(kind, value) ?? SecurityTagSet.Empty;

    /// <summary>Whether a single-tag resolution of the kind is the whole stamped identity (§17.2); conservatively <see langword="false"/> when unscoped.</summary>
    /// <param name="kind">The grantee kind.</param>
    /// <returns><see langword="true"/> if a single-tag resolution of the kind is complete.</returns>
    public bool IsWholeGrainGrantee(GranteeKind kind) => this.policy?.IsWholeGrainGrantee(kind) ?? false;

    /// <summary>Describes the current caller's own stamped identity as operator-facing grants — the "whoami" identity
    /// (empty when unscoped). Composes <see cref="InternalTags"/> through <see cref="DescribeUsageScope"/>.</summary>
    /// <returns>The caller's identity as <c>{dimension, value}</c> grants.</returns>
    // The whoami path resolves the caller's internal tags as a list once per request (not per row), so wrapping them into
    // the holder here pays a single FromTags off the hot list paths — which now take the SecurityTagSet directly.
    public IReadOnlyList<CredentialUsageGrant> CallerIdentityGrants() => this.DescribeUsageScope(SecurityTagSet.FromTags(this.InternalTags()));

    // Returns a client view of the version whose deployment-internal security tags (the reserved prefix, §14.2) are
    // stripped — the persisted document keeps them for row authorization; only the response drops them. When the
    // version carries no internal tags the original element is returned unchanged (zero-copy, no rewrite). A rewritten
    // view is a pooled document owned by the workspace, so its element stays valid through the deferred body
    // serialization. Lives on the ACCESS policy (not one handler) so catalog add and workspace publish share it.
    internal CatalogVersion PublicView(CatalogVersion version, JsonWorkspace workspace)
    {
        SecurityTagSet full = version.SecurityTagsValue;
        if (full.IsEmpty || !this.HasInternalTag(full))
        {
            return version;
        }

        SecurityTagSet visible = this.BuildVisibleTags(full);
        var stripped = ParsedJsonDocument<CatalogVersion>.Parse(CatalogVersion.CreateWithSecurityTags(version, visible));
        workspace.TakeOwnership(stripped);
        return stripped.RootElement;
    }

    /// <summary>
    /// The identity conventions a deployment gets when it authenticates but supplies no row-security policy: reach
    /// stays unrestricted, and the caller's owner group is read from a declared claim (ADR 0065). Everything else —
    /// the reserved prefix, tag validation, grant description, grantee resolution — is the base policy's default, so
    /// the owner stamp is treated as internal everywhere a deployment-supplied policy's stamp would be.
    /// </summary>
    /// <param name="claimType">The claim type carrying the caller's owner group.</param>
    private sealed class OwnerClaimPolicy(string claimType) : ControlPlaneRowSecurityPolicy
    {
        // The dimension an owner group is stamped under. Fixed rather than derived from the claim type, so an owner
        // group read from a claim named 'tid' or 'org' still lands on the house 'tenant' dimension a §16.5.4 team
        // grant names; otherwise a grant and the stamp it is meant to match would key on different dimensions. Both
        // operands are compile-time constants, so this folds to the literal "sys:tenant".
        private const string OwnerGroupTagKey = SecurityShell.DefaultInternalPrefix + OwnerGroupTag.Dimension;

        /// <inheritdoc/>
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        /// <inheritdoc/>
        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal)
        {
            string? owner = principal?.FindFirst(claimType)?.Value;
            return string.IsNullOrEmpty(owner) ? [] : [new SecurityTag(OwnerGroupTagKey, owner)];
        }
    }

    // Whether the set carries any deployment-internal tag (string-free span scan).
    private bool HasInternalTag(SecurityTagSet set)
    {
        SecurityTagSet.Utf8Enumerator e = set.EnumerateUtf8();
        try
        {
            while (e.MoveNext())
            {
                if (this.IsInternalTag(e.CurrentKey))
                {
                    return true;
                }
            }
        }
        finally
        {
            e.Dispose();
        }

        return false;
    }

    // Builds the non-internal (client-visible) subset of a tag set bytes-native — each retained tag is appended from
    // its unescaped UTF-8 key/value, so no per-tag managed string is created.
    private SecurityTagSet BuildVisibleTags(SecurityTagSet full)
        => SecurityTagSet.Build(
            (Full: full, Access: this),
            static (ref IdentityBuilder builder, in (SecurityTagSet Full, ControlPlaneAccess Access) s) =>
            {
                SecurityTagSet.Utf8Enumerator e = s.Full.EnumerateUtf8();
                try
                {
                    while (e.MoveNext())
                    {
                        if (!s.Access.IsInternalTag(e.CurrentKey))
                        {
                            builder.Add(e.CurrentKey, e.CurrentValue);
                        }
                    }
                }
                finally
                {
                    e.Dispose();
                }
            });
}