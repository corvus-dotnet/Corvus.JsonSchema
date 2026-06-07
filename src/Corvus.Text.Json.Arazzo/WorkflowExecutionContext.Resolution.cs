// <copyright file="WorkflowExecutionContext.Resolution.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
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
    /// Resolves a runtime expression to a typed <see cref="Comparand"/> for <c>simple</c> criteria.
    /// </summary>
    /// <param name="expression">The parsed expression.</param>
    /// <returns>The resolved comparand, or <see cref="Comparand.Undefined"/> if it could not be resolved.</returns>
    internal Comparand ResolveComparand(in ArazzoExpression expression)
    {
        switch (expression.Source)
        {
            case ArazzoExpressionSource.Url:
                return this.url is null ? Comparand.Undefined : Comparand.FromString(this.url);

            case ArazzoExpressionSource.Method:
                return this.method is null ? Comparand.Undefined : Comparand.FromString(this.method);

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
                return this.TryResolveValue(expression, out JsonElement element)
                    ? ComparandFromJson(element)
                    : Comparand.Undefined;
        }
    }

    private static Comparand ComparandFromJson(in JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => Comparand.FromString(element.GetString()!),
            JsonValueKind.Number => Comparand.FromNumber(element.GetDouble()),
            JsonValueKind.True => Comparand.FromBoolean(true),
            JsonValueKind.False => Comparand.FromBoolean(false),
            JsonValueKind.Null => Comparand.Null,
            JsonValueKind.Object or JsonValueKind.Array => Comparand.FromJson(element.GetRawText()),
            _ => Comparand.Undefined,
        };

    private static Comparand ComparandFromMap(Dictionary<string, string>? map, string? name)
        => map is not null && name is not null && map.TryGetValue(name, out string? value)
            ? Comparand.FromString(value)
            : Comparand.Undefined;

    private static bool TryReturn(string? candidate, out string value)
    {
        value = candidate ?? string.Empty;
        return candidate is not null;
    }

    private static bool TryReturnFromMap(Dictionary<string, string>? map, string? name, out string value)
    {
        if (map is not null && name is not null && map.TryGetValue(name, out string? found))
        {
            value = found;
            return true;
        }

        value = string.Empty;
        return false;
    }
}