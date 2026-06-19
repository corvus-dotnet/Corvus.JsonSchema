// <copyright file="DirectoryPrincipalProjector.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
    {
        if (this.spanMapper is not { } span)
        {
            return null;
        }

        var state = new ProjectionState(span, view, this.dimensions);
        if (!SecurityTagSet.TryBuild(in state, BuildIdentity, out SecurityTagSet identity))
        {
            return null;
        }

        return new ResolvedPrincipal(kind, value, label, hasLabel, identity);
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
}