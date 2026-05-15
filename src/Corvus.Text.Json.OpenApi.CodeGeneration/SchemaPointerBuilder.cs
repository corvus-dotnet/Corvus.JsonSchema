// <copyright file="SchemaPointerBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Builds JSON Pointer strings for schema references within OpenAPI specifications.
/// </summary>
/// <remarks>
/// <para>
/// All pointers use RFC 6901 escaping (<c>~</c> → <c>~0</c>, <c>/</c> → <c>~1</c>).
/// Pointers are constructed in a UTF-8 <see cref="Utf8ValueStringBuilder"/> for
/// efficiency and transcoded once to <see langword="string"/> at the end.
/// </para>
/// </remarks>
public static class SchemaPointerBuilder
{
    /// <summary>
    /// Builds: <c>#/paths/&lt;path&gt;/&lt;method&gt;/parameters/&lt;index&gt;/schema</c>.
    /// </summary>
    /// <param name="pathProperty">The path property from the paths map.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="index">The parameter index.</param>
    /// <param name="isPathLevel">Whether the parameter is path-level.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string BuildParameterSchemaPointer(
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        int index,
        bool isPathLevel)
    {
        Span<byte> initialBuffer = stackalloc byte[256];
        Utf8ValueStringBuilder sb = new(initialBuffer);
        using UnescapedUtf8JsonString pathName = pathProperty.Utf8NameSpan;

        try
        {
            sb.Append("#/paths/"u8);
            AppendEncodedSegment(ref sb, pathName.Span);

            if (!isPathLevel)
            {
                sb.Append((byte)'/');
                AppendMethodUtf8(ref sb, method);
            }

            sb.Append("/parameters/"u8);
            sb.Append(index);
            sb.Append("/schema"u8);

            return Encoding.UTF8.GetString(sb.AsSpan());
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Builds: <c>#/paths/&lt;path&gt;/&lt;method&gt;/&lt;parentSegment&gt;/content/&lt;mediaType&gt;/schema</c>.
    /// </summary>
    /// <param name="pathProperty">The path property from the paths map.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="parentSegmentUtf8">The parent segment (e.g. <c>/requestBody</c>) as UTF-8.</param>
    /// <param name="mediaTypeProperty">The media type property from the content map.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string BuildContentSchemaPointer(
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        ReadOnlySpan<byte> parentSegmentUtf8,
        JsonProperty<JsonElement> mediaTypeProperty)
    {
        Span<byte> initialBuffer = stackalloc byte[256];
        Utf8ValueStringBuilder sb = new(initialBuffer);
        using UnescapedUtf8JsonString pathName = pathProperty.Utf8NameSpan;
        using UnescapedUtf8JsonString mediaTypeName = mediaTypeProperty.Utf8NameSpan;

        try
        {
            sb.Append("#/paths/"u8);
            AppendEncodedSegment(ref sb, pathName.Span);
            sb.Append((byte)'/');
            AppendMethodUtf8(ref sb, method);
            sb.Append(parentSegmentUtf8);
            sb.Append("/content/"u8);
            AppendEncodedSegment(ref sb, mediaTypeName.Span);
            sb.Append("/schema"u8);

            return Encoding.UTF8.GetString(sb.AsSpan());
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Builds: <c>#/paths/&lt;path&gt;/&lt;method&gt;/responses/&lt;statusCode&gt;/content/&lt;mediaType&gt;/schema</c>.
    /// </summary>
    /// <param name="pathProperty">The path property from the paths map.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="responseProperty">The response property from the responses map.</param>
    /// <param name="mediaTypeProperty">The media type property from the content map.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string BuildResponseContentSchemaPointer(
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        JsonProperty<JsonElement> responseProperty,
        JsonProperty<JsonElement> mediaTypeProperty)
    {
        Span<byte> initialBuffer = stackalloc byte[256];
        Utf8ValueStringBuilder sb = new(initialBuffer);
        using UnescapedUtf8JsonString pathName = pathProperty.Utf8NameSpan;
        using UnescapedUtf8JsonString statusCode = responseProperty.Utf8NameSpan;
        using UnescapedUtf8JsonString mediaTypeName = mediaTypeProperty.Utf8NameSpan;

        try
        {
            sb.Append("#/paths/"u8);
            AppendEncodedSegment(ref sb, pathName.Span);
            sb.Append((byte)'/');
            AppendMethodUtf8(ref sb, method);
            sb.Append("/responses/"u8);
            AppendEncodedSegment(ref sb, statusCode.Span);
            sb.Append("/content/"u8);
            AppendEncodedSegment(ref sb, mediaTypeName.Span);
            sb.Append("/schema"u8);

            return Encoding.UTF8.GetString(sb.AsSpan());
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Builds: <c>#/paths/&lt;path&gt;/&lt;method&gt;/responses/&lt;statusCode&gt;/headers/&lt;headerName&gt;/schema</c>.
    /// </summary>
    /// <param name="pathProperty">The path property from the paths map.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="responseProperty">The response property from the responses map.</param>
    /// <param name="headerProperty">The header property from the response headers map.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string BuildResponseHeaderSchemaPointer(
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        JsonProperty<JsonElement> responseProperty,
        JsonProperty<JsonElement> headerProperty)
    {
        Span<byte> initialBuffer = stackalloc byte[256];
        Utf8ValueStringBuilder sb = new(initialBuffer);
        using UnescapedUtf8JsonString pathName = pathProperty.Utf8NameSpan;
        using UnescapedUtf8JsonString statusCode = responseProperty.Utf8NameSpan;
        using UnescapedUtf8JsonString headerName = headerProperty.Utf8NameSpan;

        try
        {
            sb.Append("#/paths/"u8);
            AppendEncodedSegment(ref sb, pathName.Span);
            sb.Append((byte)'/');
            AppendMethodUtf8(ref sb, method);
            sb.Append("/responses/"u8);
            AppendEncodedSegment(ref sb, statusCode.Span);
            sb.Append("/headers/"u8);
            AppendEncodedSegment(ref sb, headerName.Span);
            sb.Append("/schema"u8);

            return Encoding.UTF8.GetString(sb.AsSpan());
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Appends a UTF-8 segment with RFC 6901 JSON Pointer escaping (~ → ~0, / → ~1).
    /// </summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="segment">The raw UTF-8 segment to encode.</param>
    internal static void AppendEncodedSegment(
        ref Utf8ValueStringBuilder sb,
        ReadOnlySpan<byte> segment)
    {
        while (segment.Length > 0)
        {
            int specialIndex = segment.IndexOfAny((byte)'~', (byte)'/');
            if (specialIndex < 0)
            {
                sb.Append(segment);
                break;
            }

            if (specialIndex > 0)
            {
                sb.Append(segment[..specialIndex]);
            }

            if (segment[specialIndex] == (byte)'~')
            {
                sb.Append("~0"u8);
            }
            else
            {
                sb.Append("~1"u8);
            }

            segment = segment[(specialIndex + 1)..];
        }
    }

    /// <summary>
    /// Appends the UTF-8 representation of an <see cref="OperationMethod"/>.
    /// </summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="method">The operation method.</param>
    internal static void AppendMethodUtf8(
        ref Utf8ValueStringBuilder sb,
        OperationMethod method)
    {
        ReadOnlySpan<byte> methodSpan = method switch
        {
            OperationMethod.Get => "get"u8,
            OperationMethod.Put => "put"u8,
            OperationMethod.Post => "post"u8,
            OperationMethod.Delete => "delete"u8,
            OperationMethod.Options => "options"u8,
            OperationMethod.Head => "head"u8,
            OperationMethod.Patch => "patch"u8,
            OperationMethod.Trace => "trace"u8,
            OperationMethod.Publish => "publish"u8,
            OperationMethod.Subscribe => "subscribe"u8,
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };

        sb.Append(methodSpan);
    }
}