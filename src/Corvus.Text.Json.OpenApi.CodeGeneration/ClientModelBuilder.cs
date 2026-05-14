// <copyright file="ClientModelBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Builds a <see cref="ClientModel"/> from the output of an <see cref="ISpecWalker"/>.
/// </summary>
/// <remarks>
/// <para>
/// The walkers extract typed metadata (parameters, request bodies, responses) using
/// the strongly-typed OpenAPI schema models. The builder copies <see cref="JsonElement"/>
/// and <see cref="JsonProperty{TValue}"/> references from the walked types into the
/// client model types — no strings are allocated for operation metadata.
/// </para>
/// <para>
/// Schema pointers are constructed efficiently in a UTF-8 byte buffer and transcoded
/// once to <see langword="string"/> at the end. These are the only string allocations
/// performed by the builder.
/// </para>
/// </remarks>
public static class ClientModelBuilder
{
    /// <summary>
    /// Builds a <see cref="ClientModel"/> from a specification document.
    /// </summary>
    /// <param name="specRoot">The root element of the parsed spec document.</param>
    /// <param name="walker">The spec walker to use.</param>
    /// <param name="filter">Optional operation filter.</param>
    /// <returns>The built <see cref="ClientModel"/>.</returns>
    /// <remarks>
    /// <para>
    /// Schema pointers are collected from operation parameters, request bodies, and
    /// responses. Component schemas and transitive <c>$ref</c> targets do not need
    /// to be gathered separately — <c>{ "$ref": "..." }</c> is a valid JSON Schema,
    /// so the code generator follows references automatically.
    /// </para>
    /// </remarks>
    public static ClientModel Build(
        JsonElement specRoot,
        ISpecWalker walker,
        OperationFilter? filter = null)
    {
        List<ClientOperation> operations = [];
        List<string> schemaPointers = [];

        foreach (OperationEntry entry in walker.EnumerateOperations(specRoot, filter))
        {
            ClientOperation op = BuildOperation(entry, schemaPointers);
            operations.Add(op);
        }

        return new ClientModel(
            specRoot,
            [.. operations],
            [.. schemaPointers]);
    }

    private static ClientOperation BuildOperation(
        OperationEntry entry,
        List<string> schemaPointers)
    {
        ClientParameter[] parameters = BuildParameters(entry, schemaPointers);
        ClientRequestBody? requestBody = BuildRequestBody(entry, schemaPointers);
        ClientResponse[] responses = BuildResponses(entry, schemaPointers);

        return new ClientOperation(
            entry.Path,
            entry.Operation,
            entry.Method,
            parameters,
            requestBody,
            responses);
    }

    private static ClientParameter[] BuildParameters(
        OperationEntry entry,
        List<string> schemaPointers)
    {
        if (entry.Parameters.Length == 0)
        {
            return [];
        }

        ClientParameter[] result = new ClientParameter[entry.Parameters.Length];

        for (int i = 0; i < entry.Parameters.Length; i++)
        {
            WalkedParameter walked = entry.Parameters[i];

            string? schemaPointer = null;
            if (walked.HasSchema)
            {
                schemaPointer = BuildParameterSchemaPointer(entry.Path, entry.Method, i);
                schemaPointers.Add(schemaPointer);
            }

            result[i] = new ClientParameter(
                walked.Element,
                walked.Location,
                walked.IsRequired,
                schemaPointer,
                walked.Style,
                walked.Explode);
        }

        return result;
    }

    private static ClientRequestBody? BuildRequestBody(
        OperationEntry entry,
        List<string> schemaPointers)
    {
        if (entry.RequestBody is not { } walked)
        {
            return null;
        }

        ClientMediaTypeContent[] content = BuildContent(
            walked.Content,
            entry.Path,
            entry.Method,
            "/requestBody"u8,
            schemaPointers);

        return new ClientRequestBody(walked.Element, walked.IsRequired, content);
    }

    private static ClientResponse[] BuildResponses(
        OperationEntry entry,
        List<string> schemaPointers)
    {
        if (entry.Responses.Length == 0)
        {
            return [];
        }

        ClientResponse[] result = new ClientResponse[entry.Responses.Length];

        for (int i = 0; i < entry.Responses.Length; i++)
        {
            WalkedResponse walked = entry.Responses[i];

            ClientMediaTypeContent[] content = BuildResponseContent(
                walked,
                entry.Path,
                entry.Method,
                schemaPointers);

            result[i] = new ClientResponse(walked.Property, content);
        }

        return result;
    }

    private static ClientMediaTypeContent[] BuildContent(
        WalkedMediaTypeContent[] walkedContent,
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        ReadOnlySpan<byte> parentSegmentUtf8,
        List<string> schemaPointers)
    {
        if (walkedContent.Length == 0)
        {
            return [];
        }

        ClientMediaTypeContent[] result = new ClientMediaTypeContent[walkedContent.Length];

        for (int i = 0; i < walkedContent.Length; i++)
        {
            WalkedMediaTypeContent walked = walkedContent[i];

            string? schemaPointer = null;
            if (walked.HasSchema)
            {
                schemaPointer = BuildContentSchemaPointer(
                    pathProperty, method, parentSegmentUtf8, walked.Property);
                schemaPointers.Add(schemaPointer);
            }

            result[i] = new ClientMediaTypeContent(walked.Property, schemaPointer);
        }

        return result;
    }

    private static ClientMediaTypeContent[] BuildResponseContent(
        WalkedResponse walked,
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        List<string> schemaPointers)
    {
        if (walked.Content.Length == 0)
        {
            return [];
        }

        ClientMediaTypeContent[] result = new ClientMediaTypeContent[walked.Content.Length];

        for (int i = 0; i < walked.Content.Length; i++)
        {
            WalkedMediaTypeContent walkedContent = walked.Content[i];

            string? schemaPointer = null;
            if (walkedContent.HasSchema)
            {
                schemaPointer = BuildResponseContentSchemaPointer(
                    pathProperty, method, walked.Property, walkedContent.Property);
                schemaPointers.Add(schemaPointer);
            }

            result[i] = new ClientMediaTypeContent(walkedContent.Property, schemaPointer);
        }

        return result;
    }

    // Schema pointer construction -------------------------------------------
    // All build JSON Pointer strings using RFC 6901 escaping (~ → ~0, / → ~1).
    // We construct in UTF-8 with a stack-allocated buffer, then transcode once.

    /// <summary>
    /// Builds: #/paths/&lt;path&gt;/&lt;method&gt;/parameters/&lt;index&gt;/schema.
    /// </summary>
    private static string BuildParameterSchemaPointer(
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        int index)
    {
        byte[]? rented = null;
        Span<byte> buffer = stackalloc byte[256];
        int pos = 0;
        using UnescapedUtf8JsonString pathName = pathProperty.Utf8NameSpan;

        try
        {
            Append(ref buffer, ref rented, ref pos, "#/paths/"u8);
            AppendEncodedSegment(ref buffer, ref rented, ref pos, pathName.Span);
            Append(ref buffer, ref rented, ref pos, "/"u8);
            AppendMethodUtf8(ref buffer, ref rented, ref pos, method);
            Append(ref buffer, ref rented, ref pos, "/parameters/"u8);
            AppendInt(ref buffer, ref rented, ref pos, index);
            Append(ref buffer, ref rented, ref pos, "/schema"u8);

            return Encoding.UTF8.GetString(buffer[..pos]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Builds: #/paths/&lt;path&gt;/&lt;method&gt;/&lt;parentSegment&gt;/content/&lt;mediaType&gt;/schema.
    /// </summary>
    private static string BuildContentSchemaPointer(
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        ReadOnlySpan<byte> parentSegmentUtf8,
        JsonProperty<JsonElement> mediaTypeProperty)
    {
        byte[]? rented = null;
        Span<byte> buffer = stackalloc byte[256];
        int pos = 0;
        using UnescapedUtf8JsonString pathName = pathProperty.Utf8NameSpan;
        using UnescapedUtf8JsonString mediaTypeName = mediaTypeProperty.Utf8NameSpan;

        try
        {
            Append(ref buffer, ref rented, ref pos, "#/paths/"u8);
            AppendEncodedSegment(ref buffer, ref rented, ref pos, pathName.Span);
            Append(ref buffer, ref rented, ref pos, "/"u8);
            AppendMethodUtf8(ref buffer, ref rented, ref pos, method);
            Append(ref buffer, ref rented, ref pos, parentSegmentUtf8);
            Append(ref buffer, ref rented, ref pos, "/content/"u8);
            AppendEncodedSegment(ref buffer, ref rented, ref pos, mediaTypeName.Span);
            Append(ref buffer, ref rented, ref pos, "/schema"u8);

            return Encoding.UTF8.GetString(buffer[..pos]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Builds: #/paths/&lt;path&gt;/&lt;method&gt;/responses/&lt;statusCode&gt;/content/&lt;mediaType&gt;/schema.
    /// </summary>
    private static string BuildResponseContentSchemaPointer(
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        JsonProperty<JsonElement> responseProperty,
        JsonProperty<JsonElement> mediaTypeProperty)
    {
        byte[]? rented = null;
        Span<byte> buffer = stackalloc byte[256];
        int pos = 0;
        using UnescapedUtf8JsonString pathName = pathProperty.Utf8NameSpan;
        using UnescapedUtf8JsonString statusCode = responseProperty.Utf8NameSpan;
        using UnescapedUtf8JsonString mediaTypeName = mediaTypeProperty.Utf8NameSpan;

        try
        {
            Append(ref buffer, ref rented, ref pos, "#/paths/"u8);
            AppendEncodedSegment(ref buffer, ref rented, ref pos, pathName.Span);
            Append(ref buffer, ref rented, ref pos, "/"u8);
            AppendMethodUtf8(ref buffer, ref rented, ref pos, method);
            Append(ref buffer, ref rented, ref pos, "/responses/"u8);
            AppendEncodedSegment(ref buffer, ref rented, ref pos, statusCode.Span);
            Append(ref buffer, ref rented, ref pos, "/content/"u8);
            AppendEncodedSegment(ref buffer, ref rented, ref pos, mediaTypeName.Span);
            Append(ref buffer, ref rented, ref pos, "/schema"u8);

            return Encoding.UTF8.GetString(buffer[..pos]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Appends a UTF-8 segment with RFC 6901 JSON Pointer escaping (~ → ~0, / → ~1).
    /// </summary>
    private static void AppendEncodedSegment(
        ref Span<byte> buffer,
        ref byte[]? rented,
        ref int pos,
        ReadOnlySpan<byte> segment)
    {
        while (segment.Length > 0)
        {
            int specialIndex = segment.IndexOfAny((byte)'~', (byte)'/');
            if (specialIndex < 0)
            {
                Append(ref buffer, ref rented, ref pos, segment);
                break;
            }

            if (specialIndex > 0)
            {
                Append(ref buffer, ref rented, ref pos, segment[..specialIndex]);
            }

            if (segment[specialIndex] == (byte)'~')
            {
                Append(ref buffer, ref rented, ref pos, "~0"u8);
            }
            else
            {
                Append(ref buffer, ref rented, ref pos, "~1"u8);
            }

            segment = segment[(specialIndex + 1)..];
        }
    }

    private static void AppendMethodUtf8(
        ref Span<byte> buffer,
        ref byte[]? rented,
        ref int pos,
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

        Append(ref buffer, ref rented, ref pos, methodSpan);
    }

    private static void AppendInt(
        ref Span<byte> buffer,
        ref byte[]? rented,
        ref int pos,
        int value)
    {
        // Max int digits is 11 characters (-2147483648)
        // Format directly into the target buffer after ensuring capacity.
        EnsureCapacity(ref buffer, ref rented, pos + 11);
        if (System.Buffers.Text.Utf8Formatter.TryFormat(value, buffer[pos..], out int bytesWritten))
        {
            pos += bytesWritten;
        }
    }

    private static void EnsureCapacity(ref Span<byte> buffer, ref byte[]? rented, int required)
    {
        if (required > buffer.Length)
        {
            Grow(ref buffer, ref rented, required);
        }
    }

    private static void Append(
        ref Span<byte> buffer,
        ref byte[]? rented,
        ref int pos,
        ReadOnlySpan<byte> data)
    {
        if (pos + data.Length > buffer.Length)
        {
            Grow(ref buffer, ref rented, pos + data.Length);
        }

        data.CopyTo(buffer[pos..]);
        pos += data.Length;
    }

    private static void Grow(ref Span<byte> buffer, ref byte[]? rented, int required)
    {
        int newSize = Math.Max(buffer.Length * 2, required);
        byte[] newArray = ArrayPool<byte>.Shared.Rent(newSize);
        buffer[..].CopyTo(newArray);

        if (rented is not null)
        {
            ArrayPool<byte>.Shared.Return(rented);
        }

        rented = newArray;
        buffer = newArray;
    }
}