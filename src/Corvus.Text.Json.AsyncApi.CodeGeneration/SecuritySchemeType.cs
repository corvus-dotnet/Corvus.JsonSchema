// <copyright file="SecuritySchemeType.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// The type of security scheme defined in the AsyncAPI specification.
/// </summary>
/// <remarks>
/// See <see href="https://www.asyncapi.com/docs/reference/specification/v3.0.0#securitySchemeObject"/>.
/// </remarks>
public enum SecuritySchemeType
{
    /// <summary>
    /// Username and password authentication.
    /// </summary>
    UserPassword,

    /// <summary>
    /// API key (header, query, or cookie).
    /// </summary>
    ApiKey,

    /// <summary>
    /// X.509 client certificate.
    /// </summary>
    X509,

    /// <summary>
    /// Symmetric encryption key.
    /// </summary>
    SymmetricEncryption,

    /// <summary>
    /// Asymmetric encryption key.
    /// </summary>
    AsymmetricEncryption,

    /// <summary>
    /// HTTP API key (similar to HTTP-level API key schemes).
    /// </summary>
    HttpApiKey,

    /// <summary>
    /// HTTP authentication (basic, bearer, etc.).
    /// </summary>
    Http,

    /// <summary>
    /// OAuth 2.0.
    /// </summary>
    OAuth2,

    /// <summary>
    /// OpenID Connect Discovery.
    /// </summary>
    OpenIdConnect,

    /// <summary>
    /// SASL Plain.
    /// </summary>
    Plain,

    /// <summary>
    /// SASL SCRAM-SHA-256.
    /// </summary>
    ScramSha256,

    /// <summary>
    /// SASL SCRAM-SHA-512.
    /// </summary>
    ScramSha512,

    /// <summary>
    /// SASL GSSAPI.
    /// </summary>
    Gssapi,
}