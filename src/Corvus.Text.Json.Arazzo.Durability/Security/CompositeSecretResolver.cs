// <copyright file="CompositeSecretResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// An <see cref="ISecretResolver"/> that dispatches a reference to the registered resolver for its
/// <see cref="SecretScheme"/> (design §13). This is how the runner is configured with exactly the secret stores its
/// deployment uses: register a <see cref="EnvSecretResolver"/>/<see cref="FileSecretResolver"/> (and, in later phases,
/// Key Vault / AWS Secrets Manager / HashiCorp Vault resolvers). A reference whose scheme has no registered resolver
/// fails closed with a <see cref="SecretResolutionException"/> rather than silently returning nothing.
/// </summary>
public sealed class CompositeSecretResolver : ISecretResolver
{
    private readonly ISecretResolver[] resolvers;

    /// <summary>Initializes a new instance of the <see cref="CompositeSecretResolver"/> class.</summary>
    /// <param name="resolvers">The scheme-specific resolvers to dispatch to (the first that
    /// <see cref="ISecretResolver.CanResolve"/>s a scheme wins).</param>
    public CompositeSecretResolver(params ISecretResolver[] resolvers)
    {
        ArgumentNullException.ThrowIfNull(resolvers);
        this.resolvers = resolvers;
    }

    /// <inheritdoc/>
    public bool CanResolve(SecretScheme scheme)
    {
        foreach (ISecretResolver resolver in this.resolvers)
        {
            if (resolver.CanResolve(scheme))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
    {
        foreach (ISecretResolver resolver in this.resolvers)
        {
            if (resolver.CanResolve(reference.Scheme))
            {
                return resolver.ResolveAsync(reference, cancellationToken);
            }
        }

        throw new SecretResolutionException(reference, $"no resolver is registered for the '{reference.Scheme}' scheme.");
    }
}