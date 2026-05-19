// <copyright file="SchemaPointerBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
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
    /// <param name="pathNameUtf8">The UTF-8 path name from the paths map.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="index">The parameter index.</param>
    /// <param name="isPathLevel">Whether the parameter is path-level.</param>
    /// <returns>The JSON Pointer string (including leading <c>#</c>).</returns>
    public static string BuildParameterSchemaPointer(
        ReadOnlySpan<byte> pathNameUtf8,
        OperationMethod method,
        int index,
        bool isPathLevel)
    {
        Span<byte> initialBuffer = stackalloc byte[256];
        Utf8ValueStringBuilder sb = new(initialBuffer);

        try
        {
            sb.Append("#/paths/"u8);
            AppendEncodedSegment(ref sb, pathNameUtf8);

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
    /// Builds: <c>#/paths/&lt;path&gt;/&lt;method&gt;/parameters/&lt;idx&gt;/content/&lt;mediaType&gt;/schema</c>.
    /// </summary>
    /// <param name="pathNameUtf8">The UTF-8 path name from the paths map.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="index">The zero-based parameter index.</param>
    /// <param name="isPathLevel">Whether the parameter is at path-item level rather than operation level.</param>
    /// <param name="mediaTypeNameUtf8">The UTF-8 media type name from the content map.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string BuildParameterContentSchemaPointer(
        ReadOnlySpan<byte> pathNameUtf8,
        OperationMethod method,
        int index,
        bool isPathLevel,
        ReadOnlySpan<byte> mediaTypeNameUtf8)
    {
        Span<byte> initialBuffer = stackalloc byte[256];
        Utf8ValueStringBuilder sb = new(initialBuffer);

        try
        {
            sb.Append("#/paths/"u8);
            AppendEncodedSegment(ref sb, pathNameUtf8);

            if (!isPathLevel)
            {
                sb.Append((byte)'/');
                AppendMethodUtf8(ref sb, method);
            }

            sb.Append("/parameters/"u8);
            sb.Append(index);
            sb.Append("/content/"u8);
            AppendEncodedSegment(ref sb, mediaTypeNameUtf8);
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
    /// <param name="pathNameUtf8">The UTF-8 path name from the paths map.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="parentSegmentUtf8">The parent segment (e.g. <c>/requestBody</c>) as UTF-8.</param>
    /// <param name="mediaTypeNameUtf8">The UTF-8 media type name from the content map.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string BuildContentSchemaPointer(
        ReadOnlySpan<byte> pathNameUtf8,
        OperationMethod method,
        ReadOnlySpan<byte> parentSegmentUtf8,
        ReadOnlySpan<byte> mediaTypeNameUtf8)
    {
        Span<byte> initialBuffer = stackalloc byte[256];
        Utf8ValueStringBuilder sb = new(initialBuffer);

        try
        {
            sb.Append("#/paths/"u8);
            AppendEncodedSegment(ref sb, pathNameUtf8);
            sb.Append((byte)'/');
            AppendMethodUtf8(ref sb, method);
            sb.Append(parentSegmentUtf8);
            sb.Append("/content/"u8);
            AppendEncodedSegment(ref sb, mediaTypeNameUtf8);
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
    /// <param name="pathNameUtf8">The UTF-8 path name from the paths map.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="statusCodeUtf8">The UTF-8 status code from the responses map.</param>
    /// <param name="mediaTypeNameUtf8">The UTF-8 media type name from the content map.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string BuildResponseContentSchemaPointer(
        ReadOnlySpan<byte> pathNameUtf8,
        OperationMethod method,
        ReadOnlySpan<byte> statusCodeUtf8,
        ReadOnlySpan<byte> mediaTypeNameUtf8)
    {
        Span<byte> initialBuffer = stackalloc byte[256];
        Utf8ValueStringBuilder sb = new(initialBuffer);

        try
        {
            sb.Append("#/paths/"u8);
            AppendEncodedSegment(ref sb, pathNameUtf8);
            sb.Append((byte)'/');
            AppendMethodUtf8(ref sb, method);
            sb.Append("/responses/"u8);
            AppendEncodedSegment(ref sb, statusCodeUtf8);
            sb.Append("/content/"u8);
            AppendEncodedSegment(ref sb, mediaTypeNameUtf8);
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
    /// <param name="pathNameUtf8">The UTF-8 path name from the paths map.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="statusCodeUtf8">The UTF-8 status code from the responses map.</param>
    /// <param name="headerNameUtf8">The UTF-8 header name from the response headers map.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string BuildResponseHeaderSchemaPointer(
        ReadOnlySpan<byte> pathNameUtf8,
        OperationMethod method,
        ReadOnlySpan<byte> statusCodeUtf8,
        ReadOnlySpan<byte> headerNameUtf8)
    {
        Span<byte> initialBuffer = stackalloc byte[256];
        Utf8ValueStringBuilder sb = new(initialBuffer);

        try
        {
            sb.Append("#/paths/"u8);
            AppendEncodedSegment(ref sb, pathNameUtf8);
            sb.Append((byte)'/');
            AppendMethodUtf8(ref sb, method);
            sb.Append("/responses/"u8);
            AppendEncodedSegment(ref sb, statusCodeUtf8);
            sb.Append("/headers/"u8);
            AppendEncodedSegment(ref sb, headerNameUtf8);
            sb.Append("/schema"u8);

            return Encoding.UTF8.GetString(sb.AsSpan());
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Builds a resolvable schema pointer from a <c>$ref</c> value by appending a sub-path
    /// to the reference's fragment. When the component is reached via <c>$ref</c>, the type
    /// builder cannot navigate the positional pointer (because the positional location
    /// contains a Reference Object, not the actual component). Instead, we use the
    /// <c>$ref</c> target as the base and append the schema sub-path to its fragment.
    /// </summary>
    /// <param name="refValue">
    /// The resolved <c>$ref</c> value (e.g. <c>#/components/parameters/PetId</c> or
    /// <c>./common/types.json#/components/parameters/OrderId</c>).
    /// </param>
    /// <param name="subPath">
    /// The sub-path to append after the ref's fragment (e.g. <c>/schema</c> or
    /// <c>/content/application~1json/schema</c>).
    /// </param>
    /// <returns>
    /// A resolvable reference string with the sub-path appended to the fragment.
    /// </returns>
    public static string BuildRefBasedPointer(string refValue, string subPath)
    {
        // The refValue may be:
        // - Fragment-only: "#/components/parameters/PetId"
        //   → result: "#/components/parameters/PetId/schema"
        // - External with fragment: "./common.json#/components/parameters/OrderId"
        //   → result: "./common.json#/components/parameters/OrderId/schema"
        // - External without fragment: "./common.json"
        //   → result: "./common.json#/schema" (unlikely but handled)
        int hashIndex = refValue.IndexOf('#');
        if (hashIndex >= 0)
        {
            return string.Concat(refValue, subPath);
        }

        return string.Concat(refValue, "#", subPath);
    }

    /// <summary>
    /// Builds a content schema sub-path: <c>/content/&lt;mediaType&gt;/schema</c> with
    /// RFC 6901 escaping applied to the media type segment.
    /// </summary>
    /// <param name="mediaTypeNameUtf8">The UTF-8 media type name.</param>
    /// <returns>The sub-path string (e.g. <c>/content/application~1json/schema</c>).</returns>
    public static string BuildContentSubPath(ReadOnlySpan<byte> mediaTypeNameUtf8)
    {
        Span<byte> initialBuffer = stackalloc byte[128];
        Utf8ValueStringBuilder sb = new(initialBuffer);

        try
        {
            sb.Append("/content/"u8);
            AppendEncodedSegment(ref sb, mediaTypeNameUtf8);
            sb.Append("/schema"u8);

            return Encoding.UTF8.GetString(sb.AsSpan());
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Builds a header schema sub-path: <c>/headers/&lt;headerName&gt;/schema</c> with
    /// RFC 6901 escaping applied to the header name segment.
    /// </summary>
    /// <param name="headerNameUtf8">The UTF-8 header name.</param>
    /// <returns>The sub-path string (e.g. <c>/headers/x-rate-limit/schema</c>).</returns>
    public static string BuildHeaderSubPath(ReadOnlySpan<byte> headerNameUtf8)
    {
        Span<byte> initialBuffer = stackalloc byte[128];
        Utf8ValueStringBuilder sb = new(initialBuffer);

        try
        {
            sb.Append("/headers/"u8);
            AppendEncodedSegment(ref sb, headerNameUtf8);
            sb.Append("/schema"u8);

            return Encoding.UTF8.GetString(sb.AsSpan());
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Builds a parameter schema sub-path relative to a path item:
    /// <c>/&lt;method&gt;/parameters/&lt;index&gt;/schema</c> or
    /// <c>/parameters/&lt;index&gt;/schema</c> (path-level).
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="index">The parameter index.</param>
    /// <param name="isPathLevel">Whether the parameter is path-level.</param>
    /// <returns>The sub-path string.</returns>
    public static string BuildParameterSubPath(
        OperationMethod method,
        int index,
        bool isPathLevel)
    {
        Span<byte> initialBuffer = stackalloc byte[64];
        Utf8ValueStringBuilder sb = new(initialBuffer);

        try
        {
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
    /// Builds a request body content schema sub-path relative to a path item:
    /// <c>/&lt;method&gt;/requestBody/content/&lt;mediaType&gt;/schema</c>.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="mediaTypeNameUtf8">The UTF-8 media type name.</param>
    /// <returns>The sub-path string.</returns>
    public static string BuildRequestBodyContentSubPath(
        OperationMethod method,
        ReadOnlySpan<byte> mediaTypeNameUtf8)
    {
        Span<byte> initialBuffer = stackalloc byte[128];
        Utf8ValueStringBuilder sb = new(initialBuffer);

        try
        {
            sb.Append((byte)'/');
            AppendMethodUtf8(ref sb, method);
            sb.Append("/requestBody/content/"u8);
            AppendEncodedSegment(ref sb, mediaTypeNameUtf8);
            sb.Append("/schema"u8);

            return Encoding.UTF8.GetString(sb.AsSpan());
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Builds a response content schema sub-path relative to a path item:
    /// <c>/&lt;method&gt;/responses/&lt;statusCode&gt;/content/&lt;mediaType&gt;/schema</c>.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="statusCodeUtf8">The UTF-8 status code.</param>
    /// <param name="mediaTypeNameUtf8">The UTF-8 media type name.</param>
    /// <returns>The sub-path string.</returns>
    public static string BuildResponseContentSubPath(
        OperationMethod method,
        ReadOnlySpan<byte> statusCodeUtf8,
        ReadOnlySpan<byte> mediaTypeNameUtf8)
    {
        Span<byte> initialBuffer = stackalloc byte[128];
        Utf8ValueStringBuilder sb = new(initialBuffer);

        try
        {
            sb.Append((byte)'/');
            AppendMethodUtf8(ref sb, method);
            sb.Append("/responses/"u8);
            AppendEncodedSegment(ref sb, statusCodeUtf8);
            sb.Append("/content/"u8);
            AppendEncodedSegment(ref sb, mediaTypeNameUtf8);
            sb.Append("/schema"u8);

            return Encoding.UTF8.GetString(sb.AsSpan());
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Builds a response header schema sub-path relative to a path item:
    /// <c>/&lt;method&gt;/responses/&lt;statusCode&gt;/headers/&lt;headerName&gt;/schema</c>.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="statusCodeUtf8">The UTF-8 status code.</param>
    /// <param name="headerNameUtf8">The UTF-8 header name.</param>
    /// <returns>The sub-path string.</returns>
    public static string BuildResponseHeaderSubPath(
        OperationMethod method,
        ReadOnlySpan<byte> statusCodeUtf8,
        ReadOnlySpan<byte> headerNameUtf8)
    {
        Span<byte> initialBuffer = stackalloc byte[128];
        Utf8ValueStringBuilder sb = new(initialBuffer);

        try
        {
            sb.Append((byte)'/');
            AppendMethodUtf8(ref sb, method);
            sb.Append("/responses/"u8);
            AppendEncodedSegment(ref sb, statusCodeUtf8);
            sb.Append("/headers/"u8);
            AppendEncodedSegment(ref sb, headerNameUtf8);
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
            OperationMethod.Query => "query"u8,
            OperationMethod.Publish => "publish"u8,
            OperationMethod.Subscribe => "subscribe"u8,
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };

        sb.Append(methodSpan);
    }
}