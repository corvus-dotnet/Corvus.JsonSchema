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

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that the
    /// response body for a given status code failed schema validation.
    /// </summary>
    /// <param name="statusCode">The HTTP status code of the response that failed validation.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowResponseBodyValidationFailed(int statusCode)
    {
        throw new InvalidOperationException(
            SR.Format(SR.ResponseBodyValidationFailed, statusCode));
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that the
    /// response body for a given status code failed schema validation, with detailed
    /// diagnostic information.
    /// </summary>
    /// <param name="statusCode">The HTTP status code of the response that failed validation.</param>
    /// <param name="detail">A JSON-formatted string containing validation diagnostics.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowResponseBodyValidationFailed(int statusCode, string detail)
    {
        throw new InvalidOperationException(
            SR.Format(SR.ResponseBodyValidationFailedWithDetail, statusCode, detail));
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that a
    /// form-urlencoded body value was not a JSON object.
    /// </summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowFormBodyMustBeObject()
    {
        throw new InvalidOperationException(SR.FormBodyMustBeObject);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that the
    /// multipart boundary could not be extracted from the Content-Type header.
    /// </summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowMultipartBoundaryNotFound()
    {
        throw new InvalidOperationException(SR.MultipartBoundaryNotFound);
    }

    /// <summary>
    /// Throws a <see cref="RequestBodyTooLargeException"/> indicating that the
    /// request body exceeded the configured maximum buffered size.
    /// </summary>
    /// <param name="maxBodyLength">The configured maximum body length in bytes.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowRequestBodyTooLarge(long maxBodyLength)
    {
        throw new RequestBodyTooLargeException(SR.Format(SR.RequestBodyTooLarge, maxBodyLength), maxBodyLength);
    }

    /// <summary>
    /// Throws a <see cref="MultipartOrderingException"/> indicating that a non-binary
    /// part arrived after a binary part under the RequireBinaryLast policy.
    /// </summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowMultipartOrderingViolation()
    {
        throw new MultipartOrderingException(SR.MultipartOrderingViolation);
    }

    /// <summary>
    /// Throws a <see cref="RequiredBinaryPartMissingException"/> for the named part.
    /// </summary>
    /// <param name="partName">The missing part's name.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowRequiredBinaryPartMissing(string partName)
    {
        throw new RequiredBinaryPartMissingException(SR.Format(SR.RequiredBinaryPartMissing, partName), partName);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that the named
    /// binary part has already been passed in wire order.
    /// </summary>
    /// <param name="partName">The part's name.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowBinaryPartAlreadyPassed(string partName)
    {
        throw new InvalidOperationException(SR.Format(SR.BinaryPartAlreadyPassed, partName));
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that the named
    /// binary part is not among the endpoint's declared binary parts.
    /// </summary>
    /// <param name="partName">The part's name.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUnknownBinaryPart(string partName)
    {
        throw new InvalidOperationException(SR.Format(SR.UnknownBinaryPart, partName));
    }
}