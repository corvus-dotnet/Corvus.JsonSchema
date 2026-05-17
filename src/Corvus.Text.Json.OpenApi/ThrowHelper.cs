// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Centralized exception-throwing helpers for the OpenAPI support library
/// and for use by generated client code.
/// </summary>
/// <remarks>
/// <para>
/// Methods are marked <see cref="DoesNotReturnAttribute"/> so the JIT can
/// optimize call-site code that follows a throw path. All exception messages
/// come from the embedded <c>Resources/Strings.resx</c> resource file via
/// <c>SR</c>.
/// </para>
/// </remarks>
public static class ThrowHelper
{
    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that the
    /// operation has no path parameters.
    /// </summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNoPathParameters()
    {
        throw new InvalidOperationException(SR.NoPathParameters);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that the
    /// operation has no query parameters.
    /// </summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNoQueryParameters()
    {
        throw new InvalidOperationException(SR.NoQueryParameters);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that the
    /// operation has no header parameters.
    /// </summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNoHeaderParameters()
    {
        throw new InvalidOperationException(SR.NoHeaderParameters);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that the
    /// operation has no cookie parameters.
    /// </summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNoCookieParameters()
    {
        throw new InvalidOperationException(SR.NoCookieParameters);
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> indicating that a request
    /// parameter failed schema validation.
    /// </summary>
    /// <param name="parameterName">The name of the parameter that failed validation.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowRequestParameterValidationFailed(string parameterName)
    {
        throw new ArgumentException(
            SR.Format(SR.RequestParameterValidationFailed, parameterName),
            parameterName);
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> indicating that a request
    /// parameter failed schema validation, with detailed diagnostic information.
    /// </summary>
    /// <param name="parameterName">The name of the parameter that failed validation.</param>
    /// <param name="detail">A JSON-formatted string containing validation diagnostics.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowRequestParameterValidationFailed(string parameterName, string detail)
    {
        throw new ArgumentException(
            SR.Format(SR.RequestParameterValidationFailedWithDetail, parameterName, detail),
            parameterName);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that the
    /// request body failed schema validation.
    /// </summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowRequestBodyValidationFailed()
    {
        throw new InvalidOperationException(SR.RequestBodyValidationFailed);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that the
    /// request body failed schema validation, with detailed diagnostic information.
    /// </summary>
    /// <param name="detail">A JSON-formatted string containing validation diagnostics.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowRequestBodyValidationFailed(string detail)
    {
        throw new InvalidOperationException(
            SR.Format(SR.RequestBodyValidationFailedWithDetail, detail));
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that a
    /// request body <c>$ref</c> could not be resolved during code generation.
    /// </summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUnableToResolveRequestBodyRef()
    {
        throw new InvalidOperationException(SR.UnableToResolveRequestBodyRef);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that a
    /// response <c>$ref</c> could not be resolved during code generation.
    /// </summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUnableToResolveResponseRef()
    {
        throw new InvalidOperationException(SR.UnableToResolveResponseRef);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that a
    /// header <c>$ref</c> could not be resolved during code generation.
    /// </summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUnableToResolveHeaderRef()
    {
        throw new InvalidOperationException(SR.UnableToResolveHeaderRef);
    }
}