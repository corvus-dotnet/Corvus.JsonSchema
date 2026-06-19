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
/// <remarks>
/// <para>
/// <strong>Confinement (§17.5/F6).</strong> When constructed with a <c>secretRoot</c>, every locator is resolved
/// <em>relative to that root</em> and the canonicalised path must stay within it — an absolute locator or a
/// <c>..</c> traversal that would escape the root is rejected. A deployment that exposes the <c>file://</c> scheme to
/// less-trusted binding authors configures a root so a reference cannot read arbitrary host files. When no root is
/// configured the locator is the exact file path (the original behaviour, for the trusted-operator k8s-projection case
/// where the locator <em>is</em> the mount path).
/// </para>
/// </remarks>
public sealed class FileSecretResolver : ISecretResolver
{
    private readonly string? secretRoot;

    /// <summary>Initializes a new instance of the <see cref="FileSecretResolver"/> class.</summary>
    /// <param name="secretRoot">
    /// An optional confinement root (§17.5/F6). When supplied, locators are resolved relative to it and may not escape
    /// it (absolute paths and <c>..</c> traversal are rejected). When <see langword="null"/> the locator is the exact
    /// file path (trusted-operator behaviour).
    /// </param>
    public FileSecretResolver(string? secretRoot = null)
    {
        this.secretRoot = secretRoot is null ? null : Path.GetFullPath(secretRoot);
    }

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

        string path = this.ConfinePath(reference);

        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            return new SecretMaterial(bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            throw new SecretResolutionException(reference, $"could not read secret file '{reference.Locator}'.", ex);
        }
    }

    // Resolves the reference's locator to a concrete file path. With no configured root the locator is used verbatim
    // (trusted operator). With a root, the locator is resolved relative to it and the canonical result must remain
    // within the root — an absolute locator or a `..` escape is rejected before any I/O (§17.5/F6).
    private string ConfinePath(SecretRef reference)
    {
        if (this.secretRoot is null)
        {
            return reference.Locator;
        }

        // Combine relative to the root: an absolute locator makes Path.Combine return it unchanged, so the containment
        // check below catches the escape. Canonicalise (resolving any `..`) before comparing.
        string combined = Path.GetFullPath(Path.Combine(this.secretRoot, reference.Locator));
        string rootWithSeparator = this.secretRoot.EndsWith(Path.DirectorySeparatorChar) ? this.secretRoot : this.secretRoot + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(rootWithSeparator, StringComparison.Ordinal) && !string.Equals(combined, this.secretRoot, StringComparison.Ordinal))
        {
            throw new SecretResolutionException(reference, $"the locator escapes the configured secret root '{this.secretRoot}'; file references may not use absolute paths or '..' traversal.");
        }

        return combined;
    }
}