// <copyright file="AsyncApiRuntimeExpression.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// Represents a parsed AsyncAPI Runtime Expression.
/// </summary>
/// <remarks>
/// <para>
/// Runtime expressions in AsyncAPI 3.0 allow values to be extracted from message headers
/// or payloads at runtime. They are used in reply address location and correlation ID
/// location fields.
/// </para>
/// <para>
/// Supported forms:
/// <list type="bullet">
/// <item><c>$message.header#/pointer</c> — JSON Pointer into the message headers</item>
/// <item><c>$message.payload#/pointer</c> — JSON Pointer into the message payload</item>
/// <item>Literal values (anything not starting with <c>$</c>)</item>
/// </list>
/// </para>
/// </remarks>
public readonly struct AsyncApiRuntimeExpression
{
    private AsyncApiRuntimeExpression(AsyncApiRuntimeExpressionKind kind, string? jsonPointer, string? literalValue)
    {
        this.Kind = kind;
        this.JsonPointer = jsonPointer;
        this.LiteralValue = literalValue;
    }

    /// <summary>
    /// Gets the kind of runtime expression.
    /// </summary>
    public AsyncApiRuntimeExpressionKind Kind { get; }

    /// <summary>
    /// Gets the JSON Pointer fragment when the expression references a header or payload property.
    /// </summary>
    /// <remarks>
    /// Includes the leading <c>/</c>, e.g. <c>/correlationId</c> or <c>/replyTo</c>.
    /// </remarks>
    public string? JsonPointer { get; }

    /// <summary>
    /// Gets the literal value when <see cref="Kind"/> is <see cref="AsyncApiRuntimeExpressionKind.Literal"/>.
    /// </summary>
    public string? LiteralValue { get; }

    /// <summary>
    /// Parses an AsyncAPI runtime expression string.
    /// </summary>
    /// <param name="expression">The raw expression string from the spec (e.g. correlation ID location).</param>
    /// <returns>A parsed <see cref="AsyncApiRuntimeExpression"/>.</returns>
    public static AsyncApiRuntimeExpression Parse(string expression)
    {
        if (!expression.StartsWith('$'))
        {
            return new AsyncApiRuntimeExpression(AsyncApiRuntimeExpressionKind.Literal, null, expression);
        }

        if (expression.StartsWith("$message.header#", StringComparison.Ordinal))
        {
            string pointer = expression.Substring("$message.header#".Length);
            return new AsyncApiRuntimeExpression(AsyncApiRuntimeExpressionKind.MessageHeader, pointer, null);
        }

        if (expression.StartsWith("$message.payload#", StringComparison.Ordinal))
        {
            string pointer = expression.Substring("$message.payload#".Length);
            return new AsyncApiRuntimeExpression(AsyncApiRuntimeExpressionKind.MessagePayload, pointer, null);
        }

        // Unrecognized $ expression — treat as literal to be safe.
        return new AsyncApiRuntimeExpression(AsyncApiRuntimeExpressionKind.Literal, null, expression);
    }
}