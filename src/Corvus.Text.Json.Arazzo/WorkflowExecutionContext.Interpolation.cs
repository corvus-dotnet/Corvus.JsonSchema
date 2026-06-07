// <copyright file="WorkflowExecutionContext.Interpolation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using System.Text.Json;
using Corvus.Text;

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// Scalar-source storage and embedded (<c>{...}</c>) string interpolation for
/// <see cref="WorkflowExecutionContext"/>.
/// </summary>
public sealed partial class WorkflowExecutionContext
{
    // Scalar values are stored as UTF-8 (converted once when set) so that interpolation appends them
    // directly to the builder and criterion comparands compare them as UTF-8 spans — no managed
    // string is materialized on the hot path.
    private Dictionary<string, byte[]>? requestHeaders;
    private Dictionary<string, byte[]>? requestQuery;
    private Dictionary<string, byte[]>? requestPath;
    private Dictionary<string, byte[]>? responseHeaders;
    private Dictionary<string, byte[]>? messageHeaders;
    private byte[]? url;
    private byte[]? method;
    private int? statusCode;

    /// <summary>
    /// Sets the request URL and HTTP method (resolved by <c>$url</c> and <c>$method</c>).
    /// </summary>
    /// <param name="requestUrl">The full request URL.</param>
    /// <param name="httpMethod">The HTTP method.</param>
    public void SetRequest(string requestUrl, string httpMethod)
    {
        ArgumentNullException.ThrowIfNull(requestUrl);
        ArgumentNullException.ThrowIfNull(httpMethod);
        this.url = Encoding.UTF8.GetBytes(requestUrl);
        this.method = Encoding.UTF8.GetBytes(httpMethod);
    }

    /// <summary>
    /// Sets the response status code (resolved by <c>$statusCode</c>).
    /// </summary>
    /// <param name="code">The HTTP status code.</param>
    public void SetResponseStatusCode(int code) => this.statusCode = code;

    /// <summary>
    /// Sets a request header value (resolved by <c>$request.header.&lt;name&gt;</c>).
    /// </summary>
    /// <param name="name">The header name (case-insensitive).</param>
    /// <param name="value">The header value.</param>
    public void SetRequestHeader(string name, string value)
        => Add(ref this.requestHeaders, StringComparer.OrdinalIgnoreCase, name, value);

    /// <summary>
    /// Sets a request query parameter value (resolved by <c>$request.query.&lt;name&gt;</c>).
    /// </summary>
    /// <param name="name">The query parameter name (case-sensitive).</param>
    /// <param name="value">The parameter value.</param>
    public void SetRequestQueryParameter(string name, string value)
        => Add(ref this.requestQuery, StringComparer.Ordinal, name, value);

    /// <summary>
    /// Sets a request path parameter value (resolved by <c>$request.path.&lt;name&gt;</c>).
    /// </summary>
    /// <param name="name">The path parameter name (case-sensitive).</param>
    /// <param name="value">The parameter value.</param>
    public void SetRequestPathParameter(string name, string value)
        => Add(ref this.requestPath, StringComparer.Ordinal, name, value);

    /// <summary>
    /// Sets a response header value (resolved by <c>$response.header.&lt;name&gt;</c>).
    /// </summary>
    /// <param name="name">The header name (case-insensitive).</param>
    /// <param name="value">The header value.</param>
    public void SetResponseHeader(string name, string value)
        => Add(ref this.responseHeaders, StringComparer.OrdinalIgnoreCase, name, value);

    /// <summary>
    /// Sets an AsyncAPI message header value (resolved by <c>$message.header.&lt;name&gt;</c>).
    /// </summary>
    /// <param name="name">The header name (case-insensitive).</param>
    /// <param name="value">The header value.</param>
    public void SetMessageHeader(string name, string value)
        => Add(ref this.messageHeaders, StringComparer.OrdinalIgnoreCase, name, value);

    /// <summary>
    /// Interpolates a template, substituting each embedded <c>{expression}</c> with its resolved
    /// value (scalars as text; objects and arrays as RFC 8259 JSON), and returns the result string.
    /// </summary>
    /// <param name="template">The template (for example <c>"Bearer {$steps.login.outputs.token}"</c>).</param>
    /// <param name="result">When this method returns <see langword="true"/>, the interpolated string.</param>
    /// <returns>
    /// <see langword="true"/> if every embedded expression resolved; otherwise <see langword="false"/>.
    /// </returns>
    public bool TryInterpolate(string template, out string result)
    {
        ArgumentNullException.ThrowIfNull(template);

        var builder = new Utf8ValueStringBuilder(template.Length + 32);
        try
        {
            if (!this.TryInterpolateCore(template, ref builder))
            {
                result = string.Empty;
                return false;
            }

            result = Encoding.UTF8.GetString(builder.AsSpan());
            return true;
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>
    /// Interpolates a template directly to a UTF-8 buffer, substituting each embedded
    /// <c>{expression}</c> with its resolved value.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="output">The buffer to receive the UTF-8 result.</param>
    /// <returns>
    /// <see langword="true"/> if every embedded expression resolved (and the result was written);
    /// otherwise <see langword="false"/> (nothing is written).
    /// </returns>
    public bool TryInterpolate(string template, IBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(output);

        var builder = new Utf8ValueStringBuilder(template.Length + 32);
        try
        {
            if (!this.TryInterpolateCore(template, ref builder))
            {
                return false;
            }

            output.Write(builder.AsSpan());
            return true;
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static void Add(ref Dictionary<string, byte[]>? map, StringComparer comparer, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        (map ??= new Dictionary<string, byte[]>(comparer))[name] = Encoding.UTF8.GetBytes(value);
    }

    private static void AppendText(ref Utf8ValueStringBuilder builder, ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return;
        }

        Span<byte> destination = builder.AppendSpan(Encoding.UTF8.GetByteCount(text));
        Encoding.UTF8.GetBytes(text, destination);
    }

    private bool TryInterpolateCore(string template, ref Utf8ValueStringBuilder builder)
    {
        ReadOnlySpan<char> span = template;
        int i = 0;

        while (i < span.Length)
        {
            // An embedded expression begins with "{$" (per spec, the braces wrap a runtime
            // expression). A bare '{' — e.g. a regex quantifier like a{2,3} — is literal text.
            if (span[i] == '{' && i + 1 < span.Length && span[i + 1] == '$')
            {
                int relativeClose = span[i..].IndexOf('}');
                if (relativeClose < 0)
                {
                    AppendText(ref builder, span[i..]);
                    return true;
                }

                int closeIndex = i + relativeClose;
                ReadOnlySpan<char> inner = span[(i + 1)..closeIndex];
                ArazzoExpression expression = ArazzoExpression.Parse(inner.ToString());
                if (!this.TryAppendEmbedded(expression, ref builder))
                {
                    return false;
                }

                i = closeIndex + 1;
                continue;
            }

            int next = IndexOfEmbeddedStart(span, i);
            if (next < 0)
            {
                AppendText(ref builder, span[i..]);
                break;
            }

            AppendText(ref builder, span[i..next]);
            i = next;
        }

        return true;
    }

    private static int IndexOfEmbeddedStart(ReadOnlySpan<char> span, int from)
    {
        for (int j = from; j < span.Length - 1; j++)
        {
            if (span[j] == '{' && span[j + 1] == '$')
            {
                return j;
            }
        }

        return -1;
    }

    private bool TryAppendEmbedded(in ArazzoExpression expression, ref Utf8ValueStringBuilder builder)
    {
        switch (expression.Source)
        {
            case ArazzoExpressionSource.Literal:
                AppendText(ref builder, expression.LiteralValue);
                return true;

            case ArazzoExpressionSource.Url:
                return TryAppendScalar(ref builder, this.url);

            case ArazzoExpressionSource.Method:
                return TryAppendScalar(ref builder, this.method);

            case ArazzoExpressionSource.StatusCode:
                if (this.statusCode is int code)
                {
                    builder.Append(code);
                    return true;
                }

                return false;

            case ArazzoExpressionSource.RequestHeader:
                return TryAppendFromMap(ref builder, this.requestHeaders, expression.Name);

            case ArazzoExpressionSource.RequestQuery:
                return TryAppendFromMap(ref builder, this.requestQuery, expression.Name);

            case ArazzoExpressionSource.RequestPath:
                return TryAppendFromMap(ref builder, this.requestPath, expression.Name);

            case ArazzoExpressionSource.ResponseHeader:
                return TryAppendFromMap(ref builder, this.responseHeaders, expression.Name);

            case ArazzoExpressionSource.MessageHeader:
                return TryAppendFromMap(ref builder, this.messageHeaders, expression.Name);

            default:
                if (this.TryResolveValue(expression, out JsonElement value))
                {
                    AppendJsonEmbedded(ref builder, value);
                    return true;
                }

                return false;
        }
    }

    private static bool TryAppendScalar(ref Utf8ValueStringBuilder builder, byte[]? value)
    {
        if (value is null)
        {
            return false;
        }

        builder.Append(value);
        return true;
    }

    private static bool TryAppendFromMap(ref Utf8ValueStringBuilder builder, Dictionary<string, byte[]>? map, string? name)
    {
        if (map is null || name is null || !map.TryGetValue(name, out byte[]? value))
        {
            return false;
        }

        builder.Append(value);
        return true;
    }

    private static void AppendJsonEmbedded(ref Utf8ValueStringBuilder builder, in JsonElement value)
    {
        // Per the Arazzo spec, embedded scalar values convert to (unquoted) strings, while objects
        // and arrays are serialized as JSON (RFC 8259). Strings are appended as UTF-8 without
        // materializing a managed string.
        if (value.ValueKind == JsonValueKind.String)
        {
            using UnescapedUtf8JsonString unescaped = value.GetUtf8String();
            builder.Append(unescaped.Span);
            return;
        }

        var bufferWriter = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(bufferWriter))
        {
            value.WriteTo(writer);
        }

        builder.Append(bufferWriter.WrittenSpan);
    }
}