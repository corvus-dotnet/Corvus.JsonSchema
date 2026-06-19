// <copyright file="IPrincipalDirectory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Directories;

/// <summary>
/// A principal resolved from an external directory: a real grantee (person / team / role) and its <strong>exact</strong>
/// deployment-stamped <c>sys:</c> identity (design §16.5.4), so naming it as a grantee is never an inert or over-broad
/// hand-typed rule (administrator/entitlement membership is exact set-equality).
/// </summary>
/// <remarks>
/// The grantee <see cref="Value"/> and display <see cref="Label"/> are both held as owned, unescaped <strong>UTF-8</strong>
/// so a resolved principal flows <strong>bytes to bytes</strong> into the grantee-picker response: the server writes them
/// straight into the generated model without a managed-string round-trip (design §16.5.4). Even a <em>constructed</em>
/// display label (e.g. <c>first + " " + last</c>, which has no single source span) is assembled by the adapter into a
/// pooled UTF-8 buffer and handed in as a span — never a managed string. Two constructors mirror the
/// <see cref="IdentityBuilder.Add(System.ReadOnlySpan{byte}, System.ReadOnlySpan{byte})"/> /
/// <see cref="IdentityBuilder.Add(System.ReadOnlySpan{byte}, string)"/> pattern: the
/// <see cref="ResolvedPrincipal(GranteeKind, System.ReadOnlySpan{byte}, System.ReadOnlySpan{byte}, bool, SecurityTagSet)"/>
/// span constructor is the built-in adapters' fast path; the
/// <see cref="ResolvedPrincipal(GranteeKind, string, string?, SecurityTagSet)"/> string constructor is the ergonomic path a
/// deployment-authored <see cref="IDirectoryIdentityMapper"/> uses (and LDAP, whose Novell client only yields strings — its
/// genuine leaf). <see cref="Value"/>/<see cref="Label"/> decode on demand for string consumers (a CLI, the conformance
/// tests); the server uses <see cref="ValueMemory"/>/<see cref="LabelMemory"/>.
/// </remarks>
public readonly struct ResolvedPrincipal
{
    private readonly ReadOnlyMemory<byte> value;
    private readonly ReadOnlyMemory<byte> label;
    private readonly bool hasLabel;

    /// <summary>Initializes a new instance of the <see cref="ResolvedPrincipal"/> struct from managed strings — the ergonomic path for a deployment mapper (and the string-typed LDAP adapter); the value/label are encoded to owned UTF-8 once.</summary>
    /// <param name="kind">The grantee kind.</param>
    /// <param name="value">The grantee value (the directory's stable identifier, prefix-searchable).</param>
    /// <param name="label">An optional human-friendly display label.</param>
    /// <param name="identity">The principal's exact <c>sys:</c> identity.</param>
    public ResolvedPrincipal(GranteeKind kind, string value, string? label, SecurityTagSet identity)
    {
        ArgumentNullException.ThrowIfNull(value);
        this.Kind = kind;
        this.value = Encoding.UTF8.GetBytes(value);
        this.hasLabel = label is not null;
        this.label = label is null ? default : Encoding.UTF8.GetBytes(label);
        this.Identity = identity;
    }

    /// <summary>Initializes a new instance of the <see cref="ResolvedPrincipal"/> struct from unescaped UTF-8 spans — the built-in adapters' bytes-to-bytes fast path; the (transient) source spans (including an adapter-assembled display label) are copied to owned UTF-8 so the principal can cross the search await.</summary>
    /// <param name="kind">The grantee kind.</param>
    /// <param name="value">The grantee value as unescaped UTF-8.</param>
    /// <param name="label">The display label as unescaped UTF-8 (ignored when <paramref name="hasLabel"/> is <see langword="false"/>).</param>
    /// <param name="hasLabel">Whether a label is present (distinguishes an absent label from an empty one).</param>
    /// <param name="identity">The principal's exact <c>sys:</c> identity.</param>
    public ResolvedPrincipal(GranteeKind kind, ReadOnlySpan<byte> value, ReadOnlySpan<byte> label, bool hasLabel, SecurityTagSet identity)
    {
        this.Kind = kind;
        this.value = value.ToArray();
        this.hasLabel = hasLabel;
        this.label = hasLabel ? label.ToArray() : default;
        this.Identity = identity;
    }

    private ResolvedPrincipal(GranteeKind kind, ReadOnlyMemory<byte> value, ReadOnlyMemory<byte> label, bool hasLabel, SecurityTagSet identity)
    {
        this.Kind = kind;
        this.value = value;
        this.label = label;
        this.hasLabel = hasLabel;
        this.Identity = identity;
    }

    /// <summary>Gets the grantee kind.</summary>
    public GranteeKind Kind { get; }

    /// <summary>Gets the principal's exact <c>sys:</c> identity.</summary>
    public SecurityTagSet Identity { get; }

    /// <summary>Gets a value indicating whether a display label is present.</summary>
    public bool HasLabel => this.hasLabel;

    /// <summary>Gets the grantee value as owned, unescaped UTF-8 (the bytes-to-bytes path into the response).</summary>
    public ReadOnlyMemory<byte> ValueMemory => this.value;

    /// <summary>Gets the display label as owned, unescaped UTF-8 (empty when <see cref="HasLabel"/> is <see langword="false"/>).</summary>
    public ReadOnlyMemory<byte> LabelMemory => this.label;

    /// <summary>Gets the grantee value — the directory's stable identifier for the principal (prefix-searchable) — decoded for string consumers (a CLI, the conformance tests).</summary>
    public string Value => Encoding.UTF8.GetString(this.value.Span);

    /// <summary>Gets an optional human-friendly display label (e.g. a person's display name) — decoded for string consumers, or <see langword="null"/> when absent.</summary>
    public string? Label => this.hasLabel ? Encoding.UTF8.GetString(this.label.Span) : null;

    /// <summary>Returns a copy of this principal with its <see cref="Identity"/> replaced (the governed-dimension restamp); the owned value/label UTF-8 is reused.</summary>
    /// <param name="identity">The replacement identity.</param>
    /// <returns>The restamped principal.</returns>
    public ResolvedPrincipal WithIdentity(SecurityTagSet identity) => new(this.Kind, this.value, this.label, this.hasLabel, identity);
}

/// <summary>
/// A pluggable directory / IdP search seam (design §16.5.4): resolves real people / teams / roles to their exact
/// deployment-stamped <c>sys:</c> identity for the grantee picker, so an operator names a real principal instead of
/// hand-assembling a <c>{dimension, value}</c> tuple. Adapters wrap the popular protocols (LDAP/AD, Keycloak, SCIM 2.0,
/// Microsoft Entra (Graph), Okta, Google Workspace) and ship as separate packages.
/// </summary>
/// <remarks>
/// <para>Read-only and never mutating: the directory owns identity and coarse org/team membership (§16.5.4), so a search
/// resolves a grantee to its <c>sys:</c> identity without ever writing to the IdP. An adapter fetches raw records and
/// projects each to a <see cref="ResolvedPrincipal"/> through the deployment's <see cref="IDirectoryIdentityMapper"/>;
/// it authenticates to the directory with a credential resolved from a <c>SecretRef</c> via the deployment's
/// <c>ISecretResolver</c> (no new secret store — the §13 boundary), never stored or returned.</para>
/// <para>Optional: when no directory is configured the control plane still offers the store-indexed observed-identity
/// typeahead (<c>IObservedIdentityStore</c>) and a validated well-known subject id. The directory does its own indexing;
/// the control plane keeps no shadow copy of it.</para>
/// </remarks>
public interface IPrincipalDirectory
{
    /// <summary>
    /// Searches the directory for principals of <paramref name="kind"/> matching <paramref name="query"/> (typically a
    /// name / id prefix), resolving each to its exact <c>sys:</c> identity.
    /// </summary>
    /// <param name="kind">The grantee kind to search (<see cref="GranteeKind.Person"/> / <see cref="GranteeKind.Team"/> / <see cref="GranteeKind.Role"/>).</param>
    /// <param name="query">The search query (e.g. a name or id prefix).</param>
    /// <param name="limit">The maximum number of principals to return.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The matching principals (empty when none).</returns>
    ValueTask<IReadOnlyList<ResolvedPrincipal>> SearchAsync(GranteeKind kind, string query, int limit, CancellationToken cancellationToken);
}