// <copyright file="RuntimeExpression.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents a parsed OpenAPI Runtime Expression (OAS §4.8.20.4).
/// </summary>
/// <remarks>
/// <para>
/// Runtime expressions allow values to be extracted from the request or response context
/// at runtime. They are used in Link Object parameter bindings and request body expressions.
/// </para>
/// <para>
/// Supported forms:
/// <list type="bullet">
/// <item><c>$response.body#/pointer</c> — JSON Pointer into the response body</item>
/// <item><c>$response.header.name</c> — response header value</item>
/// <item><c>$request.path.name</c> — path parameter from the request</item>
/// <item><c>$request.query.name</c> — query parameter from the request</item>
/// <item><c>$request.header.name</c> — request header value</item>
/// <item><c>$request.body#/pointer</c> — JSON Pointer into the request body</item>
/// <item><c>$url</c> — the full request URL</item>
/// <item><c>$method</c> — the HTTP method</item>
/// <item>Literal values (anything not starting with <c>$</c>)</item>
/// </list>
/// </para>
/// </remarks>
public readonly struct RuntimeExpression
{
    private RuntimeExpression(RuntimeExpressionKind kind, string? name, string? jsonPointer, string? literalValue)
    {
        this.Kind = kind;
        this.Name = name;
        this.JsonPointer = jsonPointer;
        this.LiteralValue = literalValue;
    }

    /// <summary>
    /// Gets the kind of runtime expression.
    /// </summary>
    public RuntimeExpressionKind Kind { get; }

    /// <summary>
    /// Gets the name component (header name, parameter name) when applicable.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the JSON Pointer fragment when the expression references a body property.
    /// </summary>
    public string? JsonPointer { get; }

    /// <summary>
    /// Gets the literal value when <see cref="Kind"/> is <see cref="RuntimeExpressionKind.Literal"/>.
    /// </summary>
    public string? LiteralValue { get; }

    /// <summary>
    /// Parses an OpenAPI runtime expression string.
    /// </summary>
    /// <param name="expression">The raw expression string from the Link Object.</param>
    /// <returns>A parsed <see cref="RuntimeExpression"/>.</returns>
    public static RuntimeExpression Parse(string expression)
    {
        if (!expression.StartsWith('$'))
        {
            return new RuntimeExpression(RuntimeExpressionKind.Literal, null, null, expression);
        }

        if (expression == "$url")
        {
            return new RuntimeExpression(RuntimeExpressionKind.Url, null, null, null);
        }

        if (expression == "$method")
        {
            return new RuntimeExpression(RuntimeExpressionKind.Method, null, null, null);
        }

        if (expression.StartsWith("$response.body#", StringComparison.Ordinal))
        {
            string pointer = expression.Substring("$response.body#".Length);
            return new RuntimeExpression(RuntimeExpressionKind.ResponseBody, null, pointer, null);
        }

        if (expression.StartsWith("$response.header.", StringComparison.Ordinal))
        {
            string headerName = expression.Substring("$response.header.".Length);
            return new RuntimeExpression(RuntimeExpressionKind.ResponseHeader, headerName, null, null);
        }

        if (expression.StartsWith("$request.body#", StringComparison.Ordinal))
        {
            string pointer = expression.Substring("$request.body#".Length);
            return new RuntimeExpression(RuntimeExpressionKind.RequestBody, null, pointer, null);
        }

        if (expression.StartsWith("$request.path.", StringComparison.Ordinal))
        {
            string paramName = expression.Substring("$request.path.".Length);
            return new RuntimeExpression(RuntimeExpressionKind.RequestPath, paramName, null, null);
        }

        if (expression.StartsWith("$request.query.", StringComparison.Ordinal))
        {
            string paramName = expression.Substring("$request.query.".Length);
            return new RuntimeExpression(RuntimeExpressionKind.RequestQuery, paramName, null, null);
        }

        if (expression.StartsWith("$request.header.", StringComparison.Ordinal))
        {
            string headerName = expression.Substring("$request.header.".Length);
            return new RuntimeExpression(RuntimeExpressionKind.RequestHeader, headerName, null, null);
        }

        // Unrecognized $ expression — treat as literal to be safe.
        return new RuntimeExpression(RuntimeExpressionKind.Literal, null, null, expression);
    }
}