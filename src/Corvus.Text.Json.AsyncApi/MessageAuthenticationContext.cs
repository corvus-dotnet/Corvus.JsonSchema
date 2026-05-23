// <copyright file="MessageAuthenticationContext.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Provides context for message transport authentication.
/// </summary>
/// <remarks>
/// <para>
/// This context is passed to <see cref="IMessageAuthenticationProvider.AuthenticateAsync"/>
/// and contains the security scheme type, name, and a dictionary for credentials.
/// The transport reads credentials from <see cref="Credentials"/> after the provider
/// has been called.
/// </para>
/// </remarks>
public sealed class MessageAuthenticationContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageAuthenticationContext"/> class.
    /// </summary>
    /// <param name="schemeType">The security scheme type.</param>
    /// <param name="schemeName">The security scheme name from the spec.</param>
    public MessageAuthenticationContext(SecuritySchemeType schemeType, string schemeName)
    {
        this.SchemeType = schemeType;
        this.SchemeName = schemeName;
        this.Credentials = new Dictionary<string, string>();
    }

    /// <summary>
    /// Gets the security scheme type required by the server or operation.
    /// </summary>
    public SecuritySchemeType SchemeType { get; }

    /// <summary>
    /// Gets the name of the security scheme from the AsyncAPI specification.
    /// </summary>
    public string SchemeName { get; }

    /// <summary>
    /// Gets the credentials dictionary. Providers populate this with the
    /// key/value pairs needed by the transport (e.g., "username"/"password",
    /// "token", "apiKey").
    /// </summary>
    public Dictionary<string, string> Credentials { get; }
}