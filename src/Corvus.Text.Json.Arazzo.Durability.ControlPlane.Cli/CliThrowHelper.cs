// <copyright file="CliThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

/// <summary>
/// Centralized exception-throwing helpers for the control-plane CLI.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw; helpers used from an expression position (a <c>??</c>, where a value-producing path must terminate) are <c>Get*Exception</c> factories the caller throws. All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class CliThrowHelper
{
    /// <summary>Throws when the interactive (loopback) sign-in fails.</summary>
    /// <param name="error">The error the identity client reported.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSignInFailed(string? error)
        => throw new InvalidOperationException(SR.Format(SR.SignInFailed, error));

    /// <summary>Throws when OIDC discovery fails.</summary>
    /// <param name="error">The error discovery reported.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowOidcDiscoveryFailed(string? error)
        => throw new InvalidOperationException(SR.Format(SR.OidcDiscoveryFailed, error));

    /// <summary>Throws when the device authorization request fails.</summary>
    /// <param name="error">The error the device authorization endpoint reported.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowDeviceAuthorizationFailed(string? error)
        => throw new InvalidOperationException(SR.Format(SR.DeviceAuthorizationFailed, error));

    /// <summary>Throws when the device sign-in (token polling) fails.</summary>
    /// <param name="error">The error the token endpoint reported.</param>
    public static InvalidOperationException GetDeviceSignInFailedException(string? error)
        => new(SR.Format(SR.DeviceSignInFailed, error));

    /// <summary>Creates the exception for a token response that carried no access token, for the caller to throw.</summary>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetNoAccessTokenException()
        => new(SR.NoAccessToken);

    /// <summary>Throws when a named source cannot be resolved to a local file.</summary>
    /// <param name="name">The source name.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSourceNotResolved(string name)
        => throw new FileNotFoundException(SR.Format(SR.SourceNotResolved, name));

    /// <summary>Throws when a <c>--source</c> argument is not in the required <c>name=path</c> form.</summary>
    /// <param name="arg">The malformed argument.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSourceArgMalformed(string arg)
        => throw new ArgumentException(SR.Format(SR.SourceArgMalformed, arg));
}