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
    private Dictionary<string, string>? requestHeaders;
    private Dictionary<string, string>? requestQuery;
    private Dictionary<string, string>? requestPath;
    private Dictionary<string, string>? responseHeaders;
    private Dictionary<string, string>? messageHeaders;
    private string? url;
    private string? method;
    private int? statusCode;

    /// <summary>
    /// Sets the request URL and HTTP method (resolved by <c>$url</c> and <c>$method</c>).
    /// </summary>
    /// <param name="requestUrl">The full request URL.</param>
    /// <param name="httpMethod">The HTTP method.</param>
    public void SetRequest(string requestUrl, string httpMethod)
    {
        this.url = requestUrl;
        this.method = httpMethod;
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

    private static void Add(ref Dictionary<string, string>? map, StringComparer comparer, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        (map ??= new Dictionary<string, string>(comparer))[name] = value;
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
            if (span[i] == '{')
            {
                int relativeClose = span[i..].IndexOf('}');
                if (relativeClose < 0)
                {
                    // No closing brace — emit the remainder literally.
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

            int relativeOpen = span[i..].IndexOf('{');
            if (relativeOpen < 0)
            {
                AppendText(ref builder, span[i..]);
                break;
            }

            AppendText(ref builder, span.Slice(i, relativeOpen));
            i += relativeOpen;
        }

        return true;
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

    private static bool TryAppendScalar(ref Utf8ValueStringBuilder builder, string? value)
    {
        if (value is null)
        {
            return false;
        }

        AppendText(ref builder, value);
        return true;
    }

    private static bool TryAppendFromMap(ref Utf8ValueStringBuilder builder, Dictionary<string, string>? map, string? name)
    {
        if (map is null || name is null || !map.TryGetValue(name, out string? value))
        {
            return false;
        }

        AppendText(ref builder, value);
        return true;
    }

    private static void AppendJsonEmbedded(ref Utf8ValueStringBuilder builder, in JsonElement value)
    {
        // Per the Arazzo spec, embedded scalar values convert to (unquoted) strings, while objects
        // and arrays are serialized as JSON (RFC 8259).
        if (value.ValueKind == JsonValueKind.String)
        {
            AppendText(ref builder, value.GetString());
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