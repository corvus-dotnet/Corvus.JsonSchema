// <copyright file="DirectoryPrincipalProjector.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Directories;

/// <summary>
/// The single projection every <see cref="IPrincipalDirectory"/> adapter funnels a fetched record through (design
/// §16.5.4): it applies the deployment's mapper and then stamps the adapter's configured issuer. Routing every adapter
/// through here makes the issuer dimension <strong>correct by construction</strong> — an adapter cannot return a principal
/// whose identity is missing (or carries a forged) <c>sys:iss</c>.
/// </summary>
/// <remarks>
/// Two paths, depending on the deployment mapper. The string path (<see cref="Project"/>) applies an
/// <see cref="IDirectoryIdentityMapper"/> to a materialized <see cref="DirectoryRecord"/> and stamps the issuer via
/// <see cref="DirectoryIssuer.Stamp"/> — used by string-sourced adapters (LDAP) and any deployment supplying a string
/// mapper. The bytes-to-bytes path (<see cref="TryProjectIdentity"/>) applies an <see cref="IDirectoryIdentitySpanMapper"/>
/// to a <see cref="DirectoryRecordView"/>, writing the identity straight into a pooled buffer and appending the issuer
/// span — used by UTF-8-sourced (HTTP/JSON) adapters when the supplied mapper implements the span interface, so a resolved
/// identity is built without materializing a managed <see cref="string"/> per tag.
/// </remarks>
public sealed class DirectoryPrincipalProjector
{
    private readonly IDirectoryIdentityMapper mapper;
    private readonly IDirectoryIdentitySpanMapper? spanMapper;
    private readonly string issuer;
    private readonly byte[] issuerUtf8;

    /// <summary>Initializes a new instance of the <see cref="DirectoryPrincipalProjector"/> class.</summary>
    /// <param name="mapper">The deployment's record→identity projection (optionally also an <see cref="IDirectoryIdentitySpanMapper"/> for the bytes-to-bytes path).</param>
    /// <param name="issuer">The adapter's configured issuer id (stamped onto every resolved principal, mapper-immutable).</param>
    public DirectoryPrincipalProjector(IDirectoryIdentityMapper mapper, string issuer)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentException.ThrowIfNullOrEmpty(issuer);
        this.mapper = mapper;
        this.spanMapper = mapper as IDirectoryIdentitySpanMapper;
        this.issuer = issuer;
        this.issuerUtf8 = Encoding.UTF8.GetBytes(issuer);
    }

    /// <summary>
    /// Gets the provider attributes the deployment mapper reads (see <see cref="IDirectoryIdentityMapper.RequiredAttributes"/>)
    /// — empty when the mapper declares none (the adapter then surfaces every attribute). An adapter requests exactly these
    /// (plus its own value/label attributes) where its provider supports projection.
    /// </summary>
    public IReadOnlyCollection<string> RequiredAttributes => this.spanMapper?.RequiredAttributes ?? this.mapper.RequiredAttributes;

    /// <summary>Gets a value indicating whether the deployment mapper supports the bytes-to-bytes path (<see cref="TryProjectIdentity"/>); when <see langword="false"/> an adapter uses the string <see cref="Project"/> path.</summary>
    public bool SupportsSpanProjection => this.spanMapper is not null;

    /// <summary>Projects a materialized record to a resolved principal carrying the adapter's issuer, or <see langword="null"/> if the mapper drops it (the string path).</summary>
    /// <param name="record">The materialized directory record.</param>
    /// <returns>The resolved principal with its <c>sys:iss</c> stamped, or <see langword="null"/>.</returns>
    public ResolvedPrincipal? Project(DirectoryRecord record)
    {
        if (this.mapper.Map(record) is not { } principal)
        {
            return null;
        }

        return principal with { Identity = DirectoryIssuer.Stamp(principal.Identity, this.issuer) };
    }

    /// <summary>
    /// Projects a captured record view to a resolved principal whose <c>sys:</c> identity is built bytes-to-bytes (the span
    /// path) — the span mapper writes its tags from the view's UTF-8 spans, then the configured issuer is appended, with no
    /// managed string per tag. Returns <see langword="null"/> if the mapper drops the record. Only valid when
    /// <see cref="SupportsSpanProjection"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="kind">The grantee kind (owned by the adapter).</param>
    /// <param name="value">The grantee value (owned by the adapter).</param>
    /// <param name="label">The grantee display label (owned by the adapter).</param>
    /// <param name="view">The captured record view the mapper reads as spans.</param>
    /// <returns>The resolved principal, or <see langword="null"/> if dropped.</returns>
    public ResolvedPrincipal? TryProjectIdentity(GranteeKind kind, string value, string? label, DirectoryRecordView view)
    {
        if (this.spanMapper is not { } span)
        {
            return null;
        }

        var state = new ProjectionState(span, view, this.issuerUtf8);
        if (!SecurityTagSet.TryBuild(in state, BuildIdentity, out SecurityTagSet identity))
        {
            return null;
        }

        return new ResolvedPrincipal(kind, value, label, identity);
    }

    // The span mapper contributes its sys: tags, then the adapter's configured issuer is appended (mapper-immutable: the
    // contract forbids the mapper emitting sys:iss; the conformance suite enforces exactly one).
    private static bool BuildIdentity(ref IdentityBuilder builder, in ProjectionState state)
    {
        if (!state.Mapper.TryMapIdentity(state.View, ref builder))
        {
            return false;
        }

        builder.Add(DirectoryIssuer.IssuerTagKeyUtf8, state.Issuer);
        return true;
    }

    private readonly ref struct ProjectionState
    {
        public ProjectionState(IDirectoryIdentitySpanMapper mapper, DirectoryRecordView view, byte[] issuer)
        {
            this.Mapper = mapper;
            this.View = view;
            this.Issuer = issuer;
        }

        public IDirectoryIdentitySpanMapper Mapper { get; }

        public DirectoryRecordView View { get; }

        public byte[] Issuer { get; }
    }
}