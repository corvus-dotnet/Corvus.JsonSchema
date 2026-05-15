// <copyright file="ClientModelBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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

            ClientResponseHeader[] headers = BuildResponseHeaders(
                walked,
                entry.Path,
                entry.Method,
                schemaPointers);

            result[i] = new ClientResponse(walked.Property, content, headers);
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

    private static ClientResponseHeader[] BuildResponseHeaders(
        WalkedResponse walked,
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        List<string> schemaPointers)
    {
        if (walked.Headers.Length == 0)
        {
            return [];
        }

        ClientResponseHeader[] result = new ClientResponseHeader[walked.Headers.Length];

        for (int i = 0; i < walked.Headers.Length; i++)
        {
            WalkedResponseHeader walkedHeader = walked.Headers[i];

            string? schemaPointer = null;
            if (walkedHeader.HasSchema)
            {
                schemaPointer = BuildResponseHeaderSchemaPointer(
                    pathProperty, method, walked.Property, walkedHeader.Property);
                schemaPointers.Add(schemaPointer);
            }

            result[i] = new ClientResponseHeader(walkedHeader.Property, schemaPointer);
        }

        return result;
    }

    // Schema pointer construction -------------------------------------------
    // All build JSON Pointer strings using RFC 6901 escaping (~ → ~0, / → ~1).
    // We construct in UTF-8 with Utf8ValueStringBuilder, then transcode once.

    /// <summary>
    /// Builds: #/paths/&lt;path&gt;/&lt;method&gt;/parameters/&lt;index&gt;/schema.
    /// </summary>
    private static string BuildParameterSchemaPointer(
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        int index)
    {
        Span<byte> initialBuffer = stackalloc byte[256];
        Utf8ValueStringBuilder sb = new(initialBuffer);
        using UnescapedUtf8JsonString pathName = pathProperty.Utf8NameSpan;

        try
        {
            sb.Append("#/paths/"u8);
            AppendEncodedSegment(ref sb, pathName.Span);
            sb.Append((byte)'/');
            AppendMethodUtf8(ref sb, method);
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
    /// Builds: #/paths/&lt;path&gt;/&lt;method&gt;/&lt;parentSegment&gt;/content/&lt;mediaType&gt;/schema.
    /// </summary>
    private static string BuildContentSchemaPointer(
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
    /// Builds: #/paths/&lt;path&gt;/&lt;method&gt;/responses/&lt;statusCode&gt;/content/&lt;mediaType&gt;/schema.
    /// </summary>
    private static string BuildResponseContentSchemaPointer(
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
    /// Builds: #/paths/&lt;path&gt;/&lt;method&gt;/responses/&lt;statusCode&gt;/headers/&lt;headerName&gt;/schema.
    /// </summary>
    private static string BuildResponseHeaderSchemaPointer(
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
    private static void AppendEncodedSegment(
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

    private static void AppendMethodUtf8(
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