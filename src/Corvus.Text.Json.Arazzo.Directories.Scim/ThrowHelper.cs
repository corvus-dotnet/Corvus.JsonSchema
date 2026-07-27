// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Corvus.Text.Json.Arazzo.Directories.Scim;

/// <summary>
/// Centralized exception-throwing helpers for the SCIM directory adapter.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw; the helper used from a switch-expression arm is a <c>Get*Exception</c> factory the caller throws. All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    /// <summary>Throws when a SCIM service-provider search returns a non-success status.</summary>
    /// <param name="status">The returned status code.</param>
    /// <param name="resource">The resource type that was searched.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSearchFailed(HttpStatusCode status, object? resource)
        => throw new ScimDirectoryException(SR.Format(SR.SearchFailed, (int)status, status, resource));

    /// <summary>Creates the exception for an unsupported SCIM authentication, for the caller to throw.</summary>
    /// <param name="authenticationTypeName">The unsupported authentication type name.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetUnsupportedAuthenticationException(string authenticationTypeName)
        => new(SR.Format(SR.UnsupportedAuthentication, authenticationTypeName));
}