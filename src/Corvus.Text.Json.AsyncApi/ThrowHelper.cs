// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Centralized exception-throwing helpers for the AsyncAPI support library
/// and for use by generated producer/consumer code.
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
    /// Throws an <see cref="ArgumentException"/> indicating that the
    /// message payload failed schema validation.
    /// </summary>
    /// <param name="parameterName">The name of the parameter that failed validation.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowMessagePayloadValidationFailed(string parameterName)
    {
        throw new ArgumentException(
            SR.MessagePayloadValidationFailed,
            parameterName);
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> indicating that the
    /// message payload failed schema validation, with detailed diagnostic information.
    /// </summary>
    /// <param name="parameterName">The name of the parameter that failed validation.</param>
    /// <param name="detail">A JSON-formatted string containing validation diagnostics.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowMessagePayloadValidationFailed(string parameterName, string detail)
    {
        throw new ArgumentException(
            SR.Format(SR.MessagePayloadValidationFailedWithDetail, detail),
            parameterName);
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> indicating that the
    /// message headers failed schema validation.
    /// </summary>
    /// <param name="parameterName">The name of the parameter that failed validation.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowMessageHeadersValidationFailed(string parameterName)
    {
        throw new ArgumentException(
            SR.MessageHeadersValidationFailed,
            parameterName);
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> indicating that the
    /// message headers failed schema validation, with detailed diagnostic information.
    /// </summary>
    /// <param name="parameterName">The name of the parameter that failed validation.</param>
    /// <param name="detail">A JSON-formatted string containing validation diagnostics.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowMessageHeadersValidationFailed(string parameterName, string detail)
    {
        throw new ArgumentException(
            SR.Format(SR.MessageHeadersValidationFailedWithDetail, detail),
            parameterName);
    }

    /// <summary>
    /// Throws a <see cref="NotSupportedException"/> indicating that the
    /// message has an unsupported content type.
    /// </summary>
    /// <param name="messageName">The name of the message.</param>
    /// <param name="contentType">The unsupported content type value.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUnsupportedContentType(string messageName, string contentType)
    {
        throw new NotSupportedException(
            SR.Format(SR.UnsupportedContentType, messageName, contentType));
    }

    /// <summary>
    /// Throws a <see cref="NotSupportedException"/> indicating that the
    /// message uses an unsupported schema format.
    /// </summary>
    /// <param name="messageName">The name of the message.</param>
    /// <param name="schemaFormat">The unsupported schema format value.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUnsupportedSchemaFormat(string messageName, string schemaFormat)
    {
        throw new NotSupportedException(
            SR.Format(SR.UnsupportedSchemaFormat, messageName, schemaFormat));
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that the
    /// consumer has not been started.
    /// </summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowConsumerNotStarted()
    {
        throw new InvalidOperationException(SR.ConsumerNotStarted);
    }

    /// <summary>
    /// Throws a <see cref="NotSupportedException"/> indicating that the
    /// channel has bindings in an unrecognized format.
    /// </summary>
    /// <param name="channelName">The name of the channel.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUnsupportedBindingsFormat(string channelName)
    {
        throw new NotSupportedException(
            SR.Format(SR.UnsupportedBindingsFormat, channelName));
    }
}