// <copyright file="ISecretResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Dereferences a <see cref="SecretRef"/> to live <see cref="SecretMaterial"/> (design §13). This is the
/// <strong>runner-side</strong> trust boundary: only a resolver reads secret material, and only at transport-bind time.
/// The control plane and the durability stores never hold a resolver, so they never see a secret.
/// </summary>
/// <remarks>
/// <para>Implementations are scheme-specific (one per <see cref="SecretScheme"/>); a
/// <see cref="CompositeSecretResolver"/> dispatches a reference to the resolver registered for its scheme. A resolver
/// reads the current version of the secret, or the version pinned by <see cref="SecretRef.Version"/> when present.</para>
/// <para>Per the §13.4 performance posture, the runner caches the <em>derived</em> auth artifact (built from the
/// resolved material) with a fairly short TTL; the resolve itself happens on a cache miss, so a resolver should be
/// correct and clear rather than micro-optimized.</para>
/// </remarks>
public interface ISecretResolver
{
    /// <summary>Gets a value indicating whether this resolver handles the given scheme.</summary>
    /// <param name="scheme">The secret store scheme.</param>
    /// <returns><see langword="true"/> if this resolver can dereference references of that scheme.</returns>
    bool CanResolve(SecretScheme scheme);

    /// <summary>Dereferences a reference to live secret material the caller owns and must dispose (scrub).</summary>
    /// <param name="reference">The reference to resolve.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The resolved secret material.</returns>
    /// <exception cref="SecretResolutionException">The reference could not be resolved.</exception>
    ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken);
}