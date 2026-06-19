// <copyright file="DirectoryIssuer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.Directories;

/// <summary>
/// Stamps the reserved <c>sys:iss</c> issuer tag onto a resolved principal's identity so identities from different
/// directories are <strong>structurally disjoint</strong> (design §16.5.4): two providers can each resolve a principal
/// named <c>alice</c>, but <c>{sys:iss=corp-ldap, sys:sub=alice}</c> can never set-equal <c>{sys:iss=keycloak,
/// sys:sub=alice}</c>, so a grant to one never silently admits the other.
/// </summary>
/// <remarks>
/// The issuer is adapter-controlled and <strong>mapper-immutable</strong>: <see cref="Stamp"/> strips any <c>sys:iss</c>
/// the deployment mapper may have produced and writes the adapter's configured issuer, so a mapper can neither omit nor
/// forge it. <strong>Runtime contract:</strong> this guarantees disjointness on the authoring side only; for a
/// directory-resolved grant to actually match a caller at runtime, the deployment's runtime identity stamper (a
/// row-security policy's internal-tag resolver) must emit the <em>same</em> <c>sys:iss</c> for that issuer. An issuer
/// present on one side but not the other fails closed (the grant matches no one) rather than over-granting.
/// </remarks>
public static class DirectoryIssuer
{
    /// <summary>The reserved security-tag key carrying the resolving directory's issuer id.</summary>
    public const string IssuerTagKey = "sys:iss";

    /// <summary>
    /// Returns <paramref name="identity"/> with the reserved <see cref="IssuerTagKey"/> set to <paramref name="issuer"/>,
    /// replacing any issuer tag already present (so the adapter's issuer is authoritative and a mapper cannot forge it).
    /// </summary>
    /// <param name="identity">The mapper-projected identity.</param>
    /// <param name="issuer">The adapter's configured issuer id (a stable, deployment-unique provider identifier).</param>
    /// <returns>The identity carrying exactly one <see cref="IssuerTagKey"/> tag for <paramref name="issuer"/>.</returns>
    public static SecurityTagSet Stamp(SecurityTagSet identity, string issuer)
    {
        ArgumentException.ThrowIfNullOrEmpty(issuer);
        List<SecurityTag> tags = identity.ToList();
        tags.RemoveAll(static t => string.Equals(t.Key, IssuerTagKey, StringComparison.Ordinal));
        tags.Add(new SecurityTag(IssuerTagKey, issuer));
        return SecurityTagSet.FromTags(tags);
    }
}