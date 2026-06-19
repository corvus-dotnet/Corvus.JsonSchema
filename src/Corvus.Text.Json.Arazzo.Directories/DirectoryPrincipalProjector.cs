// <copyright file="DirectoryPrincipalProjector.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Directories;

/// <summary>
/// The single projection every <see cref="IPrincipalDirectory"/> adapter funnels a raw <see cref="DirectoryRecord"/>
/// through (design §16.5.4): it applies the deployment's <see cref="IDirectoryIdentityMapper"/> and then stamps the
/// adapter's configured issuer via <see cref="DirectoryIssuer.Stamp"/>. Routing every adapter through here makes the
/// issuer dimension <strong>correct by construction</strong> — an adapter cannot return a principal whose identity is
/// missing (or carries a forged) <c>sys:iss</c>.
/// </summary>
public sealed class DirectoryPrincipalProjector
{
    private readonly IDirectoryIdentityMapper mapper;
    private readonly string issuer;

    /// <summary>Initializes a new instance of the <see cref="DirectoryPrincipalProjector"/> class.</summary>
    /// <param name="mapper">The deployment's record→identity projection.</param>
    /// <param name="issuer">The adapter's configured issuer id (stamped onto every resolved principal, mapper-immutable).</param>
    public DirectoryPrincipalProjector(IDirectoryIdentityMapper mapper, string issuer)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentException.ThrowIfNullOrEmpty(issuer);
        this.mapper = mapper;
        this.issuer = issuer;
    }

    /// <summary>Projects a raw record to a resolved principal carrying the adapter's issuer, or <see langword="null"/> if the mapper drops it.</summary>
    /// <param name="record">The raw directory record.</param>
    /// <returns>The resolved principal with its <c>sys:iss</c> stamped, or <see langword="null"/>.</returns>
    public ResolvedPrincipal? Project(DirectoryRecord record)
    {
        if (this.mapper.Map(record) is not { } principal)
        {
            return null;
        }

        return principal with { Identity = DirectoryIssuer.Stamp(principal.Identity, this.issuer) };
    }
}