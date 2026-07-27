// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Corvus.Text.Json.Arazzo.Directories.Google;

/// <summary>
/// Centralized exception-throwing helpers for the Google Workspace directory adapter.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw; the parse-failure helper used from a catch is a <c>Get*Exception</c> factory the caller throws. All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    /// <summary>Throws when a Directory API search returns a non-success status.</summary>
    /// <param name="status">The returned status code.</param>
    /// <param name="resource">The resource that was searched.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSearchFailed(HttpStatusCode status, object? resource)
        => throw new GoogleDirectoryException(SR.Format(SR.SearchFailed, (int)status, status, resource));

    /// <summary>Throws when a Directory API group-membership fetch returns a non-success status.</summary>
    /// <param name="status">The returned status code.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowGroupMembershipsFailed(HttpStatusCode status)
        => throw new GoogleDirectoryException(SR.Format(SR.GroupMembershipsFailed, (int)status, status));

    /// <summary>Throws when the token response is not a JSON object.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowTokenResponseNotJsonObject()
        => throw new GoogleDirectoryException(SR.TokenResponseNotJsonObject);

    /// <summary>Throws when the token response contains no access token.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowTokenResponseMissingAccessToken()
        => throw new GoogleDirectoryException(SR.TokenResponseMissingAccessToken);

    /// <summary>Throws when the configured authentication is not supported.</summary>
    /// <param name="authenticationTypeName">The unsupported authentication type name.</param>
    public static InvalidOperationException GetUnsupportedAuthenticationException(string authenticationTypeName)
        => new(SR.Format(SR.UnsupportedAuthentication, authenticationTypeName));

    /// <summary>Throws when the token endpoint returns a non-success status.</summary>
    /// <param name="status">The returned status code.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowTokenEndpointFailed(HttpStatusCode status)
        => throw new GoogleDirectoryException(SR.Format(SR.TokenEndpointFailed, (int)status, status));

    /// <summary>Creates the exception for a service-account private key that could not be imported, for the caller to throw.</summary>
    /// <param name="inner">The import failure.</param>
    /// <returns>The exception to throw.</returns>
    public static GoogleDirectoryException GetPrivateKeyImportFailedException(Exception inner)
        => new(SR.PrivateKeyImportFailed, inner);
}