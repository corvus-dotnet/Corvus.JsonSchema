// <copyright file="AwsSecretsManagerSecretResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.AwsSecretsManager;

/// <summary>
/// An <see cref="ISecretResolver"/> for the <c>awssm://</c> scheme (design §13): dereferences a reference to a secret
/// held in AWS Secrets Manager. A reference is <c>awssm://&lt;secret-id-or-arn&gt;[#&lt;version-id&gt;]</c> — the
/// locator is the secret name or full ARN, and an optional <c>#version-id</c> pins a specific <c>VersionId</c>
/// (otherwise the current version, <c>AWSCURRENT</c>, is read).
/// </summary>
/// <remarks>
/// The resolver holds a single caller-supplied <see cref="IAmazonSecretsManager"/> client (configured with the region
/// and a least-privileged, <c>secretsmanager:GetSecretValue</c>-only credential). As a §13 runner-side resolver it only
/// ever <em>reads</em> a secret, and only at transport-bind time; the resolved value is returned as scrubable
/// <see cref="SecretMaterial"/>. A secret stored as a string is returned verbatim; a binary secret is returned as its
/// raw bytes.
/// </remarks>
public sealed class AwsSecretsManagerSecretResolver : ISecretResolver
{
    private readonly IAmazonSecretsManager client;

    /// <summary>Initializes a new instance of the <see cref="AwsSecretsManagerSecretResolver"/> class.</summary>
    /// <param name="client">The AWS Secrets Manager client (caller-configured region + least-privileged credential).</param>
    public AwsSecretsManagerSecretResolver(IAmazonSecretsManager client)
        => this.client = client ?? throw new ArgumentNullException(nameof(client));

    /// <inheritdoc/>
    public bool CanResolve(SecretScheme scheme) => scheme == SecretScheme.AwsSecretsManager;

    /// <inheritdoc/>
    public async ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
    {
        if (reference.Scheme != SecretScheme.AwsSecretsManager)
        {
            throw new SecretResolutionException(reference, "this resolver only handles the awssm:// scheme.");
        }

        var request = new GetSecretValueRequest { SecretId = reference.Locator };
        if (reference.Version is not null)
        {
            request.VersionId = reference.Version;
        }

        GetSecretValueResponse response;
        try
        {
            response = await this.client.GetSecretValueAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonSecretsManagerException ex)
        {
            throw new SecretResolutionException(reference, $"AWS Secrets Manager could not read secret '{reference.Locator}': {ex.Message}", ex);
        }

        if (response.SecretString is { } secretString)
        {
            return SecretMaterial.FromString(secretString);
        }

        if (response.SecretBinary is { } binary)
        {
            return new SecretMaterial(binary.ToArray());
        }

        throw new SecretResolutionException(reference, $"AWS Secrets Manager secret '{reference.Locator}' has neither a string nor a binary value.");
    }
}