// <copyright file="IPrincipalDirectory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Directories;

/// <summary>
/// A principal resolved from an external directory: a real grantee (person / team / role) and its <strong>exact</strong>
/// deployment-stamped <c>sys:</c> identity (design §16.5.4), so naming it as a grantee is never an inert or over-broad
/// hand-typed rule (administrator/entitlement membership is exact set-equality).
/// </summary>
/// <param name="Kind">The grantee kind.</param>
/// <param name="Value">The grantee value — the directory's stable identifier for the principal (prefix-searchable).</param>
/// <param name="Label">An optional human-friendly display label (e.g. a person's display name).</param>
/// <param name="Identity">The principal's exact <c>sys:</c> identity.</param>
public readonly record struct ResolvedPrincipal(GranteeKind Kind, string Value, string? Label, SecurityTagSet Identity);

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