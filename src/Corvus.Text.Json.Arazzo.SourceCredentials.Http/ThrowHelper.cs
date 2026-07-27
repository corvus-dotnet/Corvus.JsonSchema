// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.SourceCredentials.Http;

/// <summary>
/// Centralized exception-throwing helpers for the HTTP source-credential runtime bridge.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw; helpers used from a catch or a value-producing expression (a switch arm / conditional) are <c>Get*Exception</c> factories the caller throws (so a local assigned in a <c>try</c> stays definitely assigned). All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    /// <summary>Throws when a workflow requires an API source that has no configured transport binding.</summary>
    /// <param name="workflowId">The workflow that requires the source.</param>
    /// <param name="source">The API source with no binding.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowApiSourceHasNoTransportBinding(string workflowId, string source)
        => throw new WorkflowTransportBindingException(SR.Format(SR.ApiSourceHasNoTransportBinding, workflowId, source));

    /// <summary>Throws when a workflow requires a message transport but the host configures no channel-transport cache.</summary>
    /// <param name="workflowId">The workflow that requires the transport.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNoChannelTransportCache(string workflowId)
        => throw new WorkflowTransportBindingException(SR.Format(SR.NoChannelTransportCache, workflowId));

    /// <summary>Throws when a channel source has no credential binding in the environment.</summary>
    /// <param name="sourceName">The channel source.</param>
    /// <param name="environment">The environment.</param>
    public static WorkflowTransportBindingException GetChannelSourceHasNoBindingException(string sourceName, string environment)
        => new(SR.Format(SR.ChannelSourceHasNoBinding, sourceName, environment));

    /// <summary>Throws when a channel credential carries no <c>serverUrl</c> config entry.</summary>
    /// <param name="sourceName">The channel source.</param>
    /// <param name="environment">The environment.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowChannelCredentialMissingServerUrl(string sourceName, string environment)
        => throw new WorkflowTransportBindingException(SR.Format(SR.ChannelCredentialMissingServerUrl, sourceName, environment));

    /// <summary>Throws when a channel source declares a protocol for which no transport factory is registered.</summary>
    /// <param name="sourceName">The channel source.</param>
    /// <param name="protocol">The declared protocol.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNoTransportFactoryForProtocol(string sourceName, string protocol)
        => throw new WorkflowTransportBindingException(SR.Format(SR.NoTransportFactoryForProtocol, sourceName, protocol));

    /// <summary>Creates the exception for a channel transport that failed to build, for the caller to throw.</summary>
    /// <param name="sourceName">The channel source.</param>
    /// <param name="environment">The environment.</param>
    /// <param name="protocol">The protocol.</param>
    /// <param name="inner">The build failure.</param>
    /// <returns>The exception to throw.</returns>
    public static WorkflowTransportBindingException GetChannelTransportBuildFailedException(string sourceName, string environment, string protocol, Exception inner)
        => new(SR.Format(SR.ChannelTransportBuildFailed, sourceName, environment, protocol, inner.Message));

    /// <summary>Throws when a source rejects its credential and the run must be faulted for rotation.</summary>
    /// <param name="sourceName">The source that rejected the credential.</param>
    /// <param name="statusCode">The rejecting HTTP status code.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSourceCredentialRejected(string sourceName, object? statusCode)
        => throw new SourceCredentialExpiredException(sourceName, SR.Format(SR.SourceCredentialRejected, sourceName, statusCode));

    /// <summary>Creates the exception for an unsupported source credential kind, for the caller to throw.</summary>
    /// <param name="kind">The unsupported credential kind.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetUnsupportedSourceCredentialKindException(object? kind)
        => new(SR.Format(SR.UnsupportedSourceCredentialKind, kind));

    /// <summary>Creates the exception for an unreadable mTLS client certificate, for the caller to throw.</summary>
    /// <param name="sourceName">The binding's source.</param>
    /// <param name="inner">The read failure.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetUnreadableClientCertificateException(string sourceName, Exception inner)
        => new(SR.Format(SR.UnreadableClientCertificate, sourceName, inner.Message), inner);

    /// <summary>Throws when an OAuth2 binding is created but no token-endpoint client was supplied.</summary>
    /// <param name="sourceName">The binding's source.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowOAuth2NoTokenClient(string sourceName)
        => throw new InvalidOperationException(SR.Format(SR.OAuth2NoTokenClient, sourceName));

    /// <summary>Throws when an OAuth2 binding names a non-https token endpoint and insecure endpoints are not allowed.</summary>
    /// <param name="sourceName">The binding's source.</param>
    /// <param name="scheme">The rejected URL scheme.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowOAuth2NonHttpsTokenUrl(string sourceName, string scheme)
        => throw new InvalidOperationException(SR.Format(SR.OAuth2NonHttpsTokenUrl, sourceName, scheme));

    /// <summary>Throws when a binding is missing a required secret reference.</summary>
    /// <param name="sourceName">The binding's source.</param>
    /// <param name="authKind">The binding's auth kind.</param>
    /// <param name="role">The missing secret role.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowMissingSecretReference(string sourceName, object? authKind, string role)
        => throw new InvalidOperationException(SR.Format(SR.MissingSecretReference, sourceName, authKind, role));

    /// <summary>Throws when a binding names an unknown api-key location.</summary>
    /// <param name="sourceName">The binding's source.</param>
    /// <param name="location">The unknown location.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUnknownApiKeyLocation(string sourceName, string? location)
        => throw new InvalidOperationException(SR.Format(SR.UnknownApiKeyLocation, sourceName, location));

    /// <summary>Creates the exception for a binding that is missing a required configuration value, for the caller to throw.</summary>
    /// <param name="sourceName">The binding's source.</param>
    /// <param name="authKind">The binding's auth kind.</param>
    /// <param name="key">The missing configuration key.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetMissingConfigValueException(string sourceName, object? authKind, string key)
        => new(SR.Format(SR.MissingConfigValue, sourceName, authKind, key));

    /// <summary>Throws when the OAuth2 token endpoint returns a non-success status.</summary>
    /// <param name="status">The returned status code.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowOAuth2TokenEndpointFailed(HttpStatusCode status)
        => throw new OAuth2TokenException(SR.Format(SR.OAuth2TokenEndpointFailed, (int)status, status));

    /// <summary>Throws when the OAuth2 token response is not a JSON object.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowOAuth2TokenResponseNotJsonObject()
        => throw new OAuth2TokenException(SR.OAuth2TokenResponseNotJsonObject);

    /// <summary>Throws when the OAuth2 token response contains no access token.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowOAuth2TokenResponseMissingAccessToken()
        => throw new OAuth2TokenException(SR.OAuth2TokenResponseMissingAccessToken);
}