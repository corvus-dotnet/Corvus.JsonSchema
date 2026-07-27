// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Corvus.Text.Json.Arazzo.Directories.Keycloak;

/// <summary>
/// Centralized exception-throwing helpers for the Keycloak directory adapter.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw; the helper used from a switch-expression arm is a <c>Get*Exception</c> factory the caller throws. All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    /// <summary>Throws when a Keycloak Admin API search returns a non-success status.</summary>
    /// <param name="status">The returned status code.</param>
    /// <param name="resource">The resource that was searched.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSearchFailed(HttpStatusCode status, object? resource)
        => throw new KeycloakDirectoryException(SR.Format(SR.SearchFailed, (int)status, status, resource));

    /// <summary>Creates the exception for an unsupported Keycloak resource, for the caller to throw.</summary>
    /// <param name="resource">The unsupported resource.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetUnsupportedResourceException(object? resource)
        => new(SR.Format(SR.UnsupportedResource, resource));

    /// <summary>Throws when a Keycloak Admin API group-membership fetch returns a non-success status.</summary>
    /// <param name="status">The returned status code.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowGroupMembershipsFailed(HttpStatusCode status)
        => throw new KeycloakDirectoryException(SR.Format(SR.GroupMembershipsFailed, (int)status, status));

    /// <summary>Throws when the token response is not a JSON object.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowTokenResponseNotJsonObject()
        => throw new KeycloakDirectoryException(SR.TokenResponseNotJsonObject);

    /// <summary>Throws when the token response contains no access token.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowTokenResponseMissingAccessToken()
        => throw new KeycloakDirectoryException(SR.TokenResponseMissingAccessToken);

    /// <summary>Throws when the configured authentication is not supported.</summary>
    /// <param name="authenticationTypeName">The unsupported authentication type name.</param>
    public static InvalidOperationException GetUnsupportedAuthenticationException(string authenticationTypeName)
        => new(SR.Format(SR.UnsupportedAuthentication, authenticationTypeName));

    /// <summary>Throws when the Keycloak token endpoint returns a non-success status.</summary>
    /// <param name="status">The returned status code.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowTokenEndpointFailed(HttpStatusCode status)
        => throw new KeycloakDirectoryException(SR.Format(SR.TokenEndpointFailed, (int)status, status));
}