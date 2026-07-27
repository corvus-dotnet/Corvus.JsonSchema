// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Corvus.Text.Json.Arazzo.Directories.EntraId;

/// <summary>
/// Centralized exception-throwing helpers for the Entra ID directory adapter.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw. All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    /// <summary>Throws when a Graph search returns a non-success status.</summary>
    /// <param name="status">The returned status code.</param>
    /// <param name="resource">The resource that was searched.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSearchFailed(HttpStatusCode status, object? resource)
        => throw new EntraIdDirectoryException(SR.Format(SR.SearchFailed, (int)status, status, resource));

    /// <summary>Throws when a Graph group-membership fetch returns a non-success status.</summary>
    /// <param name="status">The returned status code.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowGroupMembershipsFailed(HttpStatusCode status)
        => throw new EntraIdDirectoryException(SR.Format(SR.GroupMembershipsFailed, (int)status, status));

    /// <summary>Throws when the token response is not a JSON object.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowTokenResponseNotJsonObject()
        => throw new EntraIdDirectoryException(SR.TokenResponseNotJsonObject);

    /// <summary>Throws when the token response contains no access token.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowTokenResponseMissingAccessToken()
        => throw new EntraIdDirectoryException(SR.TokenResponseMissingAccessToken);

    /// <summary>Throws when the configured authentication is not supported.</summary>
    /// <param name="authenticationTypeName">The unsupported authentication type name.</param>
    public static InvalidOperationException GetUnsupportedAuthenticationException(string authenticationTypeName)
        => new(SR.Format(SR.UnsupportedAuthentication, authenticationTypeName));

    /// <summary>Throws when the identity platform token endpoint returns a non-success status.</summary>
    /// <param name="status">The returned status code.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowTokenEndpointFailed(HttpStatusCode status)
        => throw new EntraIdDirectoryException(SR.Format(SR.TokenEndpointFailed, (int)status, status));
}