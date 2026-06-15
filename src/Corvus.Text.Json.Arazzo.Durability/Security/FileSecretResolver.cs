// <copyright file="FileSecretResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// An <see cref="ISecretResolver"/> for the <c>file://</c> scheme (design §13): the secret is the exact byte content of
/// the file at the reference's <see cref="SecretRef.Locator"/> — e.g. a Kubernetes secret projected into the runner's
/// pod as a mounted file. The file content is read verbatim (no trimming), so the operator controls exactly what bytes
/// the secret holds. Files are unversioned here, so a <see cref="SecretRef.Version"/> is rejected.
/// </summary>
public sealed class FileSecretResolver : ISecretResolver
{
    /// <inheritdoc/>
    public bool CanResolve(SecretScheme scheme) => scheme == SecretScheme.File;

    /// <inheritdoc/>
    public async ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
    {
        if (reference.Scheme != SecretScheme.File)
        {
            throw new SecretResolutionException(reference, "this resolver only handles the file:// scheme.");
        }

        if (reference.Version is not null)
        {
            throw new SecretResolutionException(reference, "file secrets are unversioned; remove the #version.");
        }

        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(reference.Locator, cancellationToken).ConfigureAwait(false);
            return new SecretMaterial(bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            throw new SecretResolutionException(reference, $"could not read secret file '{reference.Locator}'.", ex);
        }
    }
}