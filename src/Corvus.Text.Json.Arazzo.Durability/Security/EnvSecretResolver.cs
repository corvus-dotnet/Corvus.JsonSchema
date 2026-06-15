// <copyright file="EnvSecretResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// An <see cref="ISecretResolver"/> for the <c>env://</c> scheme (design §13): the secret is the value of an
/// environment variable named by the reference's <see cref="SecretRef.Locator"/> — e.g. a value injected into the
/// runner's process by the orchestrator. Environment variables are unversioned, so a <see cref="SecretRef.Version"/> is
/// rejected.
/// </summary>
public sealed class EnvSecretResolver : ISecretResolver
{
    /// <inheritdoc/>
    public bool CanResolve(SecretScheme scheme) => scheme == SecretScheme.Environment;

    /// <inheritdoc/>
    public ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
    {
        if (reference.Scheme != SecretScheme.Environment)
        {
            throw new SecretResolutionException(reference, "this resolver only handles the env:// scheme.");
        }

        if (reference.Version is not null)
        {
            throw new SecretResolutionException(reference, "environment-variable secrets are unversioned; remove the #version.");
        }

        string? value = Environment.GetEnvironmentVariable(reference.Locator);
        if (value is null)
        {
            throw new SecretResolutionException(reference, $"environment variable '{reference.Locator}' is not set.");
        }

        return new ValueTask<SecretMaterial>(SecretMaterial.FromString(value));
    }
}