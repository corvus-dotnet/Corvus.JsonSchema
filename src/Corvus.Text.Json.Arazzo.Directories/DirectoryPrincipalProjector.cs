// <copyright file="DirectoryPrincipalProjector.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Directories;

/// <summary>
/// The single projection every <see cref="IPrincipalDirectory"/> adapter funnels a fetched record through (design
/// §16.5.4): it applies the deployment's mapper and then stamps the deployment-controlled <strong>governed
/// dimensions</strong> — the adapter's issuer (<c>sys:iss</c>) plus any request-context ambient dimensions (§16.5.5, e.g.
/// <c>sys:tenant</c> from a vanity host). Routing every adapter through here makes those dimensions <strong>correct by
/// construction</strong> — an adapter cannot return a principal whose identity is missing (or carries a forged)
/// <c>sys:iss</c>, nor one missing the ambient tenant the runtime caller will carry.
/// </summary>
/// <remarks>
/// The issuer is itself a (static) governed dimension, so it is stamped through the <em>same</em> mechanism as any
/// ambient dimension — the projector has no issuer-specific path. Two paths, depending on the deployment mapper. The
/// string path (<see cref="Project"/>) applies an <see cref="IDirectoryIdentityMapper"/> to a materialized
/// <see cref="DirectoryRecord"/> and strip-and-restamps every governed dimension in one pass via
/// <see cref="AmbientIdentityStamp"/> — used by string-sourced adapters (LDAP) and any deployment supplying a string
/// mapper. The bytes-to-bytes path (<see cref="TryProjectIdentity"/>) applies an <see cref="IDirectoryIdentitySpanMapper"/>
/// to a <see cref="DirectoryRecordView"/>, writing the identity straight into a pooled buffer and appending each governed
/// dimension's pre-encoded UTF-8 — used by UTF-8-sourced (HTTP/JSON) adapters when the supplied mapper implements the span
/// interface, so a resolved identity is built without materializing a managed <see cref="string"/> per tag.
/// </remarks>
public sealed class DirectoryPrincipalProjector
{
    // A synthetic membership grantee carries no directory attributes — only its name (the grantee value). Shared so the
    // string projection path allocates no per-membership attribute map.
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptyAttributes = new Dictionary<string, IReadOnlyList<string>>();

    private readonly IDirectoryIdentityMapper mapper;
    private readonly IDirectoryIdentitySpanMapper? spanMapper;
    private readonly IAmbientIdentityDimensions[] dimensions;

    /// <summary>Initializes a new instance of the <see cref="DirectoryPrincipalProjector"/> class.</summary>
    /// <param name="mapper">The deployment's record→identity projection (optionally also an <see cref="IDirectoryIdentitySpanMapper"/> for the bytes-to-bytes path).</param>
    /// <param name="issuer">The adapter's configured issuer id (stamped onto every resolved principal as the <c>sys:iss</c> governed dimension, mapper-immutable).</param>
    /// <param name="ambient">
    /// An optional request-context ambient-dimension provider (§16.5.5). When supplied, every resolved principal also
    /// carries the dimensions it resolves (e.g. <c>sys:tenant</c> derived from the request host), stamped mapper-immutably
    /// from the <em>same</em> provider the runtime caller is stamped from, so a grant authored in a tenant context matches
    /// exactly the caller in that context. <see langword="null"/> (the default) leaves behaviour unchanged.
    /// </param>
    public DirectoryPrincipalProjector(IDirectoryIdentityMapper mapper, string issuer, IAmbientIdentityDimensions? ambient = null)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentException.ThrowIfNullOrEmpty(issuer);
        this.mapper = mapper;
        this.spanMapper = mapper as IDirectoryIdentitySpanMapper;

        // The issuer is a (static) governed dimension — sys:iss — funnelled through the same uniform stamp as any
        // request-context ambient dimension, so the projector has no issuer-specific path. The set is fixed at
        // construction (the issuer first, then the ambient provider when present).
        var issuerDimension = new StaticAmbientIdentityDimensions([new SecurityTag(DirectoryIssuer.IssuerTagKey, issuer)]);
        this.dimensions = ambient is null ? [issuerDimension] : [issuerDimension, ambient];
    }

    /// <summary>
    /// Gets the provider attributes the deployment mapper reads (see <see cref="IDirectoryIdentityMapper.RequiredAttributes"/>)
    /// — empty when the mapper declares none (the adapter then surfaces every attribute). An adapter requests exactly these
    /// (plus its own value/label attributes) where its provider supports projection.
    /// </summary>
    public IReadOnlyCollection<string> RequiredAttributes => this.spanMapper?.RequiredAttributes ?? this.mapper.RequiredAttributes;

    /// <summary>Gets a value indicating whether the deployment mapper supports the bytes-to-bytes path (<see cref="TryProjectIdentity"/>); when <see langword="false"/> an adapter uses the string <see cref="Project"/> path.</summary>
    public bool SupportsSpanProjection => this.spanMapper is not null;

    /// <summary>Projects a materialized record to a resolved principal carrying the governed dimensions (issuer + any ambient), or <see langword="null"/> if the mapper drops it (the string path).</summary>
    /// <param name="record">The materialized directory record.</param>
    /// <returns>The resolved principal with its governed dimensions stamped, or <see langword="null"/>.</returns>
    public ResolvedPrincipal? Project(DirectoryRecord record)
    {
        if (this.mapper.Map(record) is not { } principal)
        {
            return null;
        }

        // One strip-and-restamp pass over every governed dimension (the issuer + any ambient), mapper-immutable.
        return principal.WithIdentity(AmbientIdentityStamp.Apply(this.dimensions, principal.Identity));
    }

    /// <summary>
    /// Projects a captured record view to a resolved principal whose <c>sys:</c> identity is built bytes-to-bytes (the span
    /// path) — the span mapper writes its tags from the view's UTF-8 spans, then each governed dimension (issuer + any
    /// ambient) is appended, with no managed string per tag. Returns <see langword="null"/> if the mapper drops the record.
    /// Only valid when <see cref="SupportsSpanProjection"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="kind">The grantee kind (owned by the adapter).</param>
    /// <param name="value">The grantee value as unescaped UTF-8 (the adapter's parse span; copied into the principal).</param>
    /// <param name="label">The display label as unescaped UTF-8 the adapter assembled (a single attribute span or a pooled <c>first + " " + last</c> buffer); ignored when <paramref name="hasLabel"/> is <see langword="false"/>.</param>
    /// <param name="hasLabel">Whether a display label is present.</param>
    /// <param name="view">The captured record view the mapper reads as spans.</param>
    /// <returns>The resolved principal, or <see langword="null"/> if dropped.</returns>
    public ResolvedPrincipal? TryProjectIdentity(GranteeKind kind, ReadOnlySpan<byte> value, ReadOnlySpan<byte> label, bool hasLabel, DirectoryRecordView view)
        => this.TryBuildIdentity(view, out SecurityTagSet identity)
            ? new ResolvedPrincipal(kind, value, label, hasLabel, identity)
            : null;

    /// <summary>
    /// Returns <paramref name="person"/> enriched with the identity of every group and role it belongs to — the person's
    /// <strong>full membership-expanded identity</strong> (design §16.5.4). Each membership name is projected as a synthetic
    /// team / role through the <em>same</em> deployment mapper and issuer, and its resolved tags are unioned into the
    /// person's identity (deduplicated by key and value). This is what lets a person with no explicit grant surface the
    /// grants it inherits through its groups / roles: administration, reach, and the effective-access lookup all compare by
    /// membership (a target's named identity ⊆ the principal's stamped identity — §16.5.4), so a person whose identity now
    /// contains <c>sys:group=arazzo-admins</c> matches every grant a bare <c>arazzo-admins</c> team matches. It also closes
    /// the §16.5.5 drift where the login resolver stamps a member's groups but a directory search did not — both now resolve
    /// the same fully-expanded identity. An adapter calls this after resolving a person, having fetched the person's group /
    /// role memberships from the directory; a person with no memberships (or one whose memberships add no new tag) is returned
    /// unchanged, allocation-free.
    /// </summary>
    /// <param name="person">The resolved person to expand (its issuer / ambient governed dimensions are already stamped).</param>
    /// <param name="groupNames">The directory names of the groups the person belongs to (each projected as <see cref="GranteeKind.Team"/>), or empty.</param>
    /// <param name="roleNames">The directory names of the roles the person holds (each projected as <see cref="GranteeKind.Role"/>), or empty.</param>
    /// <returns>The person with its memberships' identity tags unioned in, or the same principal when there is nothing to add.</returns>
    /// <remarks>
    /// The union is built <strong>bytes to bytes</strong>: each membership resolves to a <see cref="SecurityTagSet"/> whose
    /// UTF-8 tags are written straight into a pooled buffer, deduplicated against the person and prior memberships by
    /// <see cref="SecurityTagSet.Contains(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/> on the unescaped spans — no managed
    /// <see cref="SecurityTag"/> list, no per-tag string, and (on the span path) no throwaway <see cref="ResolvedPrincipal"/>.
    /// The only owned allocation is the one <see cref="byte"/> array the expanded set carries; the membership-projection
    /// scratch and the projections table are pooled.
    /// </remarks>
    public ResolvedPrincipal EnrichWithMemberships(ResolvedPrincipal person, IReadOnlyList<string>? groupNames, IReadOnlyList<string>? roleNames)
    {
        int groupCount = groupNames?.Count ?? 0;
        int roleCount = roleNames?.Count ?? 0;
        int total = groupCount + roleCount;
        if (total == 0)
        {
            return person;
        }

        // Resolve each membership to its deployment-stamped identity once, into a pooled table the union walks for the
        // cross-membership dedup. A dropped or empty membership is skipped; if every one drops, there is nothing to expand.
        SecurityTagSet[] projections = ArrayPool<SecurityTagSet>.Shared.Rent(total);
        try
        {
            int count = 0;
            for (int i = 0; i < groupCount; i++)
            {
                if (this.TryProjectMembership(GranteeKind.Team, groupNames![i], out SecurityTagSet identity) && !identity.IsEmpty)
                {
                    projections[count++] = identity;
                }
            }

            for (int i = 0; i < roleCount; i++)
            {
                if (this.TryProjectMembership(GranteeKind.Role, roleNames![i], out SecurityTagSet identity) && !identity.IsEmpty)
                {
                    projections[count++] = identity;
                }
            }

            if (count == 0)
            {
                return person;
            }

            var state = new MembershipUnionState(person.Identity, projections, count);
            SecurityTagSet expanded = SecurityTagSet.Build(in state, BuildUnion);

            // The person's tags are always written first, so `expanded` is a superset of the person's identity; when it is no
            // larger, every membership tag was already present and the original principal (its owned value/label reused) stands.
            return expanded.Count == person.Identity.Count ? person : person.WithIdentity(expanded);
        }
        finally
        {
            // clearArray so the pooled slots do not pin the projected identities' byte arrays past this call.
            ArrayPool<SecurityTagSet>.Shared.Return(projections, clearArray: true);
        }
    }

    // Projects one membership name as a synthetic grantee of `kind` through the same mapper, returning only its identity (no
    // ResolvedPrincipal). A span mapper's string Map throws, so it MUST take the bytes-to-bytes path with a synthetic view
    // carrying just the membership name as the grantee value (no attributes) — the name is encoded into a pooled scratch, so
    // the only owned allocation is the identity itself. A string mapper projects a synthetic record. Returns false (identity
    // = empty) if the mapper drops the synthetic grantee.
    private bool TryProjectMembership(GranteeKind kind, string name, out SecurityTagSet identity)
    {
        if (string.IsNullOrEmpty(name))
        {
            identity = default;
            return false;
        }

        if (this.spanMapper is not null)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(name.Length));
            try
            {
                int written = Encoding.UTF8.GetBytes(name, buffer);
                ReadOnlySpan<byte> value = buffer.AsSpan(0, written);
                return this.TryBuildIdentity(new DirectoryRecordView(kind, value, default, default), out identity);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        if (this.Project(new DirectoryRecord(kind, name, name, EmptyAttributes, [])) is { } resolved)
        {
            identity = resolved.Identity;
            return true;
        }

        identity = default;
        return false;
    }

    // Writes the membership-expanded identity into the pooled builder: the person's own tags first (authoritative — a
    // membership re-emitting a governed dimension such as the shared sys:iss or an ambient sys:tenant is deduped against
    // them), then each membership's tags that neither the person nor an earlier membership already carries. All dedup is a
    // string-free span comparison; no managed set is built.
    private static void BuildUnion(ref IdentityBuilder builder, in MembershipUnionState state)
    {
        SecurityTagSet.Utf8Enumerator own = state.Person.EnumerateUtf8();
        try
        {
            while (own.MoveNext())
            {
                builder.Add(own.CurrentKey, own.CurrentValue);
            }
        }
        finally
        {
            own.Dispose();
        }

        for (int i = 0; i < state.Count; i++)
        {
            SecurityTagSet.Utf8Enumerator e = state.Projections[i].EnumerateUtf8();
            try
            {
                while (e.MoveNext())
                {
                    if (state.Person.Contains(e.CurrentKey, e.CurrentValue) || ContainedEarlier(state.Projections, i, e.CurrentKey, e.CurrentValue))
                    {
                        continue;
                    }

                    builder.Add(e.CurrentKey, e.CurrentValue);
                }
            }
            finally
            {
                e.Dispose();
            }
        }
    }

    // Whether an earlier membership projection (0..index-1) already carries this tag — the cross-membership dedup that
    // absorbs a directory returning the same group twice, or two names the mapper collapses to one tag.
    private static bool ContainedEarlier(SecurityTagSet[] projections, int index, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        for (int j = 0; j < index; j++)
        {
            if (projections[j].Contains(key, value))
            {
                return true;
            }
        }

        return false;
    }

    // Builds a record view's sys: identity (mapper tags + governed dimensions) into a SecurityTagSet, or false to drop it —
    // the shared core of TryProjectIdentity and the span-path membership projection. Only valid for a span mapper.
    private bool TryBuildIdentity(DirectoryRecordView view, out SecurityTagSet identity)
    {
        if (this.spanMapper is not { } span)
        {
            identity = default;
            return false;
        }

        var state = new ProjectionState(span, view, this.dimensions);
        return SecurityTagSet.TryBuild(in state, BuildIdentity, out identity);
    }

    // The span mapper contributes its sys: tags, then each governed dimension (the issuer, then any ambient) is appended
    // from its cached, pre-encoded set — bytes to bytes, no per-record allocation. Mapper-immutable: the contract forbids
    // the mapper emitting a governed key; the conformance suite enforces exactly one issuer.
    private static bool BuildIdentity(ref IdentityBuilder builder, in ProjectionState state)
    {
        if (!state.Mapper.TryMapIdentity(state.View, ref builder))
        {
            return false;
        }

        IAmbientIdentityDimensions[] dimensions = state.Dimensions;
        for (int i = 0; i < dimensions.Length; i++)
        {
            dimensions[i].Resolve().WriteTo(ref builder);
        }

        return true;
    }

    private readonly ref struct ProjectionState
    {
        public ProjectionState(IDirectoryIdentitySpanMapper mapper, DirectoryRecordView view, IAmbientIdentityDimensions[] dimensions)
        {
            this.Mapper = mapper;
            this.View = view;
            this.Dimensions = dimensions;
        }

        public IDirectoryIdentitySpanMapper Mapper { get; }

        public DirectoryRecordView View { get; }

        public IAmbientIdentityDimensions[] Dimensions { get; }
    }

    // The state the membership union writes from: the person's own identity, plus the resolved membership identities the
    // union deduplicates against. A plain (non-ref) struct — held by the pooled projections array — so it threads through
    // SecurityTagSet.Build as a static callback with no capture.
    private readonly struct MembershipUnionState
    {
        public MembershipUnionState(SecurityTagSet person, SecurityTagSet[] projections, int count)
        {
            this.Person = person;
            this.Projections = projections;
            this.Count = count;
        }

        public SecurityTagSet Person { get; }

        public SecurityTagSet[] Projections { get; }

        public int Count { get; }
    }
}