// <copyright file="CertificateAuthenticationProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography.X509Certificates;

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Authentication provider for X.509 client certificate security schemes.
/// </summary>
/// <remarks>
/// <para>
/// Populates the <see cref="MessageAuthenticationContext.Credentials"/> dictionary
/// with <c>certificate</c> (base64-encoded PFX) and optionally <c>password</c> entries.
/// The transport reads these to configure TLS client authentication.
/// </para>
/// </remarks>
public sealed class CertificateAuthenticationProvider : IMessageAuthenticationProvider
{
    private readonly X509Certificate2 certificate;

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificateAuthenticationProvider"/> class.
    /// </summary>
    /// <param name="certificate">The X.509 client certificate.</param>
    public CertificateAuthenticationProvider(X509Certificate2 certificate)
    {
        this.certificate = certificate;
    }

    /// <inheritdoc/>
    public ValueTask AuthenticateAsync(
        MessageAuthenticationContext context,
        CancellationToken cancellationToken = default)
    {
        byte[] rawCert = this.certificate.Export(X509ContentType.Pfx);
        context.Credentials["certificate"] = Convert.ToBase64String(rawCert);
        return ValueTask.CompletedTask;
    }
}