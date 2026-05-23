// <copyright file="AsyncApiRuntimeExpressionKind.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// Identifies the kind of an AsyncAPI runtime expression.
/// </summary>
/// <remarks>
/// <para>
/// AsyncAPI 3.0 defines runtime expressions in the context of reply address
/// and correlation ID location fields. The supported forms are:
/// <list type="bullet">
/// <item><c>$message.header#/pointer</c> — a JSON Pointer into the message headers</item>
/// <item><c>$message.payload#/pointer</c> — a JSON Pointer into the message payload</item>
/// </list>
/// </para>
/// </remarks>
public enum AsyncApiRuntimeExpressionKind
{
    /// <summary>
    /// A literal value (not a runtime expression). The value is passed as-is.
    /// </summary>
    Literal,

    /// <summary>
    /// <c>$message.header#/pointer</c> — a JSON Pointer into the message headers.
    /// </summary>
    MessageHeader,

    /// <summary>
    /// <c>$message.payload#/pointer</c> — a JSON Pointer into the message payload.
    /// </summary>
    MessagePayload,
}