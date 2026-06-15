// <copyright file="SecretScheme.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The external secret store a <see cref="SecretRef"/> points into (design §13). The runner's <c>ISecretResolver</c>
/// dispatches on this to dereference the secret; the control plane and the Arazzo database never read it.
/// </summary>
public enum SecretScheme
{
    /// <summary>Azure Key Vault (<c>keyvault://</c>).</summary>
    KeyVault,

    /// <summary>AWS Secrets Manager (<c>awssm://</c>).</summary>
    AwsSecretsManager,

    /// <summary>HashiCorp Vault (<c>vault://</c>).</summary>
    HashiCorpVault,

    /// <summary>An environment variable (<c>env://</c>) — e.g. a value injected by the orchestrator.</summary>
    Environment,

    /// <summary>A file path (<c>file://</c>) — e.g. a mounted Kubernetes secret.</summary>
    File,
}