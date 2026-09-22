// <copyright file="GranteeResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Resolves a grantee a write names (<c>{kind, value}</c>) to its exact deployment-stamped <c>sys:</c> identity on the
/// server (ADR 0008): the client names a grantee and never supplies an identity, so the identity a grant stores is one
/// the deployment itself produced. The sources are consulted richest first, in the order the identity design gives
/// (§16.5.4): the pluggable directory (people, teams and roles, matched exactly on the value), then the observed-identity
/// store (the sightings this deployment has recorded, reach-filtered to the caller), then the policy's own mapping of the
/// kind to its dimension, whose completeness is the policy's whole-grain verdict.
/// </summary>
internal sealed class GranteeResolver
{
    // The exact match is looked for among the prefix matches of the whole value, so the page has to be wide enough to hold
    // every value the given one is a prefix of.
    private const int ExactMatchLimit = 64;

    // The kinds a principal directory resolves (the IPrincipalDirectory vocabulary). A workflow is a control-plane
    // concept and is never directory-resolved.
    private static readonly GranteeKind[] DirectoryResolvableKinds = [GranteeKind.Person, GranteeKind.Team, GranteeKind.Role];

    private readonly IObservedIdentityStore observed;
    private readonly IPrincipalDirectory? directory;
    private readonly ControlPlaneAccess access;

    /// <summary>Initializes a new instance of the <see cref="GranteeResolver"/> class.</summary>
    /// <param name="observed">The store-indexed observed identities.</param>
    /// <param name="directory">The external directory, or <see langword="null"/> when none is configured.</param>
    /// <param name="access">The caller's access and the policy's grantee mapping.</param>
    internal GranteeResolver(IObservedIdentityStore observed, IPrincipalDirectory? directory, ControlPlaneAccess access)
    {
        ArgumentNullException.ThrowIfNull(observed);
        ArgumentNullException.ThrowIfNull(access);
        this.observed = observed;
        this.directory = directory;
        this.access = access;
    }

    /// <summary>Resolves a grantee to its exact identity.</summary>
    /// <param name="kind">The grantee kind.</param>
    /// <param name="value">The grantee value (a subject id, a team or role name, or a workflow id), as its JSON value.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The resolution; its identity is empty when nothing the deployment stamps names the grantee.</returns>
    /// <exception cref="PrincipalDirectoryException">The directory resolves the kind and could not be reached.</exception>
    public async ValueTask<GranteeResolution> ResolveAsync(GranteeKind kind, JsonString value, CancellationToken cancellationToken)
    {
        if (this.directory is not null && Array.IndexOf(DirectoryResolvableKinds, kind) >= 0)
        {
            // The directory seam is string-typed (an LDAP filter, an HTTP query), so the value reifies at this genuine leaf.
            // A directory that cannot be reached propagates: an identity it would have resolved is never guessed instead.
            IReadOnlyList<ResolvedPrincipal> found = await this.directory.SearchAsync(kind, (string)value, ExactMatchLimit, cancellationToken).ConfigureAwait(false);
            foreach (ResolvedPrincipal principal in found)
            {
                if (principal.Kind == kind && value.ValueEquals(principal.ValueMemory.Span))
                {
                    return new GranteeResolution(principal.Identity, complete: true);
                }
            }
        }

        // A sighting the deployment recorded is an identity it produced, so it is exact; the search is reach-filtered to
        // the caller, the same AccessContext idiom the grantee search uses, and the value flows as its JSON value.
        using (ObservedIdentityPage page = await this.observed.SearchAsync(this.access.Current(), kind.ToObservedKind(), value, ExactMatchLimit, default, cancellationToken).ConfigureAwait(false))
        {
            foreach (ObservedIdentity identity in page.Identities)
            {
                using UnescapedUtf8JsonString seen = identity.SubjectValue.GetUtf8String();
                if (value.ValueEquals(seen.Span))
                {
                    // IdentityTagsValue copies the tags out of the pooled page, so the identity outlives the page.
                    return new GranteeResolution(identity.IdentityTagsValue, identity.CompleteValue);
                }
            }
        }

        // The policy's mapping of the kind to its dimension: exact for a whole-grain kind, a partial identity otherwise,
        // and empty (refused by the caller) where the deployment stamps nothing for the kind.
        return new GranteeResolution(this.access.ResolveGranteeIdentity(kind, (string)value), this.access.IsWholeGrainGrantee(kind));
    }
}

/// <summary>The outcome of resolving a grantee: its exact identity and whether that identity is the grantee's whole
/// stamped identity (§17.2) or a partial one a grant would match more broadly than intended.</summary>
/// <param name="identity">The resolved identity; empty when the grantee resolves to nothing.</param>
/// <param name="complete">Whether the identity is the grantee's whole stamped identity.</param>
internal readonly struct GranteeResolution(SecurityTagSet identity, bool complete)
{
    /// <summary>Gets the resolved identity.</summary>
    public SecurityTagSet Identity { get; } = identity;

    /// <summary>Gets a value indicating whether the identity is the grantee's whole stamped identity.</summary>
    public bool Complete { get; } = complete;
}