// <copyright file="WorkflowExecutionContext.Resolution.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// Scalar/typed resolution of runtime expressions for criterion evaluation.
/// </summary>
public sealed partial class WorkflowExecutionContext
{
    /// <summary>
    /// Resolves a runtime expression to a string value (used as the context for <c>regex</c> criteria).
    /// </summary>
    /// <param name="expression">The parsed expression.</param>
    /// <param name="value">When this method returns <see langword="true"/>, the resolved string.</param>
    /// <returns><see langword="true"/> if the expression resolved to a value.</returns>
    public bool TryResolveString(in ArazzoExpression expression, out string value)
    {
        switch (expression.Source)
        {
            case ArazzoExpressionSource.Literal:
                value = expression.LiteralValue ?? string.Empty;
                return expression.LiteralValue is not null;

            case ArazzoExpressionSource.Url:
                return TryReturn(this.url, out value);

            case ArazzoExpressionSource.Method:
                return TryReturn(this.method, out value);

            case ArazzoExpressionSource.StatusCode:
                if (this.statusCode is int code)
                {
                    value = code.ToString(CultureInfo.InvariantCulture);
                    return true;
                }

                value = string.Empty;
                return false;

            case ArazzoExpressionSource.RequestHeader:
                return TryReturnFromMap(this.requestHeaders, expression.Name, out value);

            case ArazzoExpressionSource.RequestQuery:
                return TryReturnFromMap(this.requestQuery, expression.Name, out value);

            case ArazzoExpressionSource.RequestPath:
                return TryReturnFromMap(this.requestPath, expression.Name, out value);

            case ArazzoExpressionSource.ResponseHeader:
                return TryReturnFromMap(this.responseHeaders, expression.Name, out value);

            case ArazzoExpressionSource.MessageHeader:
                return TryReturnFromMap(this.messageHeaders, expression.Name, out value);

            default:
                if (this.TryResolveValue(expression, out JsonElement element))
                {
                    value = element.ValueKind == JsonValueKind.String
                        ? element.GetString()!
                        : element.GetRawText();
                    return true;
                }

                value = string.Empty;
                return false;
        }
    }

    /// <summary>
    /// Resolves a runtime expression to a typed <see cref="Comparand"/> for <c>simple</c> criteria,
    /// optionally navigating into the resolved value with a JSON Pointer (for the <c>.</c>/<c>[]</c>
    /// operand operators).
    /// </summary>
    /// <param name="expression">The parsed expression.</param>
    /// <param name="navigationPointer">An optional JSON Pointer to navigate into a JSON-valued result.</param>
    /// <returns>The resolved comparand, or <see cref="Comparand.Undefined"/> if it could not be resolved.</returns>
    internal Comparand ResolveComparand(in ArazzoExpression expression, string? navigationPointer = null)
    {
        // Navigation (.property / [index]) only applies to JSON-valued sources, not scalars.
        if (navigationPointer is not null && IsScalarSource(expression.Source))
        {
            return Comparand.Undefined;
        }

        switch (expression.Source)
        {
            case ArazzoExpressionSource.Url:
                return this.url is null ? Comparand.Undefined : Comparand.FromUtf8String(this.url);

            case ArazzoExpressionSource.Method:
                return this.method is null ? Comparand.Undefined : Comparand.FromUtf8String(this.method);

            case ArazzoExpressionSource.StatusCode:
                return this.statusCode is int code ? Comparand.FromNumber(code) : Comparand.Undefined;

            case ArazzoExpressionSource.RequestHeader:
                return ComparandFromMap(this.requestHeaders, expression.Name);

            case ArazzoExpressionSource.RequestQuery:
                return ComparandFromMap(this.requestQuery, expression.Name);

            case ArazzoExpressionSource.RequestPath:
                return ComparandFromMap(this.requestPath, expression.Name);

            case ArazzoExpressionSource.ResponseHeader:
                return ComparandFromMap(this.responseHeaders, expression.Name);

            case ArazzoExpressionSource.MessageHeader:
                return ComparandFromMap(this.messageHeaders, expression.Name);

            default:
                if (!this.TryResolveValue(expression, out JsonElement element))
                {
                    return Comparand.Undefined;
                }

                if (navigationPointer is not null && !element.TryResolvePointer(navigationPointer, out element))
                {
                    return Comparand.Undefined;
                }

                return ComparandFromJson(element);
        }
    }

    private static bool IsScalarSource(ArazzoExpressionSource source)
        => source is ArazzoExpressionSource.Url
            or ArazzoExpressionSource.Method
            or ArazzoExpressionSource.StatusCode
            or ArazzoExpressionSource.RequestHeader
            or ArazzoExpressionSource.RequestQuery
            or ArazzoExpressionSource.RequestPath
            or ArazzoExpressionSource.ResponseHeader
            or ArazzoExpressionSource.MessageHeader;

    private static Comparand ComparandFromJson(in JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => Comparand.FromJsonString(element),
            JsonValueKind.Number => Comparand.FromNumber(element.GetDouble()),
            JsonValueKind.True => Comparand.FromBoolean(true),
            JsonValueKind.False => Comparand.FromBoolean(false),
            JsonValueKind.Null => Comparand.Null,
            JsonValueKind.Object or JsonValueKind.Array => Comparand.FromJson(element),
            _ => Comparand.Undefined,
        };

    private static Comparand ComparandFromMap(Dictionary<string, byte[]>? map, string? name)
        => map is not null && name is not null && map.TryGetValue(name, out byte[]? value)
            ? Comparand.FromUtf8String(value)
            : Comparand.Undefined;

    private static bool TryReturn(byte[]? candidate, out string value)
    {
        value = candidate is null ? string.Empty : Encoding.UTF8.GetString(candidate);
        return candidate is not null;
    }

    private static bool TryReturnFromMap(Dictionary<string, byte[]>? map, string? name, out string value)
    {
        if (map is not null && name is not null && map.TryGetValue(name, out byte[]? found))
        {
            value = Encoding.UTF8.GetString(found);
            return true;
        }

        value = string.Empty;
        return false;
    }
}