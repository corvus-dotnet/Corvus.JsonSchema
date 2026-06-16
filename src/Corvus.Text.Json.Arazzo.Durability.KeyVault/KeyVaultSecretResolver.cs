// <copyright file="KeyVaultSecretResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using Azure;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.KeyVault;

/// <summary>
/// An <see cref="ISecretResolver"/> for the <c>keyvault://</c> scheme (design §13): dereferences a reference to a
/// secret held in Azure Key Vault via its <em>secrets</em> data plane (distinct from the
/// <see cref="KeyVaultCheckpointProtector"/>, which uses Key Vault <em>keys</em> for envelope encryption). A reference
/// is <c>keyvault://&lt;vault-host&gt;/&lt;secret-name&gt;[#&lt;version&gt;]</c>: the host names the vault (a bare name
/// such as <c>myvault</c> is expanded to <c>myvault.vault.azure.net</c>; a value containing a dot is used verbatim),
/// the path segment names the secret, and an optional <c>#version</c> pins a specific secret version (otherwise the
/// current version is read).
/// </summary>
/// <remarks>
/// <para>The resolver holds a single caller-supplied <see cref="TokenCredential"/> (so the runner runs under a
/// least-privileged, secrets-read-only identity) and builds — and caches, per vault — a <see cref="SecretClient"/> for
/// each distinct vault it sees. As a §13 runner-side resolver, it only ever <em>reads</em> a secret, and only at
/// transport-bind time; the resolved value is returned as scrubable <see cref="SecretMaterial"/>.</para>
/// </remarks>
public sealed class KeyVaultSecretResolver : ISecretResolver
{
    private readonly Func<Uri, SecretClient> clientFactory;
    private readonly ConcurrentDictionary<string, SecretClient> clients = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initializes a new instance of the <see cref="KeyVaultSecretResolver"/> class.</summary>
    /// <param name="credential">The credential the per-vault <see cref="SecretClient"/>s authenticate with — supply a
    /// least-privileged, secrets-<em>get</em>-only identity (e.g. a managed identity / <see cref="TokenCredential"/>).</param>
    public KeyVaultSecretResolver(TokenCredential credential)
        : this(uri => new SecretClient(uri, credential ?? throw new ArgumentNullException(nameof(credential))))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KeyVaultSecretResolver"/> class over a caller-supplied
    /// client factory (the seam used by tests to inject a stub <see cref="SecretClient"/>).</summary>
    /// <param name="clientFactory">Builds a <see cref="SecretClient"/> for a vault URI; invoked once per distinct vault.</param>
    public KeyVaultSecretResolver(Func<Uri, SecretClient> clientFactory)
        => this.clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));

    /// <inheritdoc/>
    public bool CanResolve(SecretScheme scheme) => scheme == SecretScheme.KeyVault;

    /// <inheritdoc/>
    public async ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
    {
        if (reference.Scheme != SecretScheme.KeyVault)
        {
            throw new SecretResolutionException(reference, "this resolver only handles the keyvault:// scheme.");
        }

        int slash = reference.Locator.IndexOf('/', StringComparison.Ordinal);
        if (slash <= 0 || slash == reference.Locator.Length - 1)
        {
            throw new SecretResolutionException(reference, "a keyvault:// reference must be keyvault://<vault-host>/<secret-name>[#version].");
        }

        string host = reference.Locator[..slash];
        string secretName = reference.Locator[(slash + 1)..];
        Uri vaultUri = new($"https://{(host.Contains('.', StringComparison.Ordinal) ? host : $"{host}.vault.azure.net")}");
        SecretClient client = this.clients.GetOrAdd(vaultUri.AbsoluteUri, _ => this.clientFactory(vaultUri));

        try
        {
            Response<KeyVaultSecret> response = await client.GetSecretAsync(secretName, reference.Version, cancellationToken).ConfigureAwait(false);
            return SecretMaterial.FromString(response.Value.Value);
        }
        catch (RequestFailedException ex)
        {
            throw new SecretResolutionException(reference, $"Key Vault returned {ex.Status} reading secret '{secretName}' from '{vaultUri}'.", ex);
        }
    }
}