// <copyright file="AwsSecretsManagerSecretResolverBuilderExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Amazon.SecretsManager;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.AwsSecretsManager;

/// <summary>
/// Registers the AWS Secrets Manager (<c>awssm://</c>) resolver on a <see cref="SecretResolverBuilder"/>. This lives in
/// the AWS Secrets Manager resolver assembly because only it can reference the AWS SDK; the core builder cannot.
/// </summary>
public static class AwsSecretsManagerSecretResolverBuilderExtensions
{
    /// <summary>Registers an <see cref="AwsSecretsManagerSecretResolver"/> for the <c>awssm://</c> scheme.</summary>
    /// <param name="builder">The resolver builder.</param>
    /// <param name="client">The AWS Secrets Manager client (caller-configured region + a least-privileged,
    /// <c>secretsmanager:GetSecretValue</c>-only credential).</param>
    /// <returns>The builder, for chaining.</returns>
    public static SecretResolverBuilder AddAwsSecretsManager(this SecretResolverBuilder builder, IAmazonSecretsManager client)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(client);
        return builder.Add(new AwsSecretsManagerSecretResolver(client));
    }
}