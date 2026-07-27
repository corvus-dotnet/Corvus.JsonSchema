// <copyright file="HashiCorpVaultSecretResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using VaultSharp;
using VaultSharp.Core;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines.KeyValue.V2;

namespace Corvus.Text.Json.Arazzo.Durability.Vault;

/// <summary>
/// An <see cref="ISecretResolver"/> for the <c>vault://</c> scheme (design §13): dereferences a reference to a field of
/// a secret held in a HashiCorp Vault <strong>KV v2</strong> secrets engine. A reference is
/// <c>vault://&lt;mount&gt;/&lt;path&gt;#&lt;field&gt;</c> — the first locator segment is the engine mount point, the
/// rest is the secret path, and the fragment names which key of the secret's data map to read (a KV secret is a
/// key→value map, so the field is required). The current secret version is read.
/// </summary>
/// <remarks>
/// The resolver holds a single caller-supplied <see cref="IVaultClient"/> (configured with the Vault address and a
/// least-privileged auth method with read access to the path). As a §13 runner-side resolver it only ever <em>reads</em>
/// a secret, and only at transport-bind time; the resolved value is returned as scrubable <see cref="SecretMaterial"/>.
/// </remarks>
public sealed class HashiCorpVaultSecretResolver : ISecretResolver
{
    private readonly IVaultClient client;

    /// <summary>Initializes a new instance of the <see cref="HashiCorpVaultSecretResolver"/> class.</summary>
    /// <param name="client">The Vault client (caller-configured address + least-privileged auth).</param>
    public HashiCorpVaultSecretResolver(IVaultClient client)
        => this.client = client ?? throw new ArgumentNullException(nameof(client));

    /// <inheritdoc/>
    public bool CanResolve(SecretScheme scheme) => scheme == SecretScheme.HashiCorpVault;

    /// <inheritdoc/>
    public async ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
    {
        if (reference.Scheme != SecretScheme.HashiCorpVault)
        {
            ThrowHelper.ThrowSchemeMismatch(reference);
        }

        int slash = reference.Locator.IndexOf('/', StringComparison.Ordinal);
        if (slash <= 0 || slash == reference.Locator.Length - 1)
        {
            ThrowHelper.ThrowReferenceMalformed(reference);
        }

        if (reference.Version is not { Length: > 0 } field)
        {
            throw ThrowHelper.GetFieldRequiredException(reference);
        }

        string mount = reference.Locator[..slash];
        string path = reference.Locator[(slash + 1)..];

        Secret<SecretData> secret;
        try
        {
            secret = await this.client.V1.Secrets.KeyValue.V2.ReadSecretAsync(path, mountPoint: mount).ConfigureAwait(false);
        }
        catch (VaultApiException ex)
        {
            throw ThrowHelper.GetReadFailedException(reference, mount, path, ex);
        }

        if (!secret.Data.Data.TryGetValue(field, out object? value))
        {
            ThrowHelper.ThrowFieldMissing(reference, mount, path, field);
        }

        return SecretMaterial.FromString(value?.ToString() ?? string.Empty);
    }
}