// <copyright file="ClientModelBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Builds a <see cref="ClientModel"/> from the output of an <see cref="ISpecWalker"/>.
/// </summary>
/// <remarks>
/// <para>
/// The builder navigates the spec document's <see cref="JsonElement"/> tree to
/// pre-locate sub-elements (parameters, request bodies, responses) and build
/// UTF-8 JSON pointer references to schemas. No strings are extracted from the
/// document — all <see cref="JsonElement"/> references are stored directly in
/// the model types, and UTF-16 conversion happens at the emitter boundary.
/// </para>
/// </remarks>
public static class ClientModelBuilder
{
    private static ReadOnlySpan<byte> ParametersUtf8 => "parameters"u8;

    private static ReadOnlySpan<byte> RequestBodyUtf8 => "requestBody"u8;

    private static ReadOnlySpan<byte> ResponsesUtf8 => "responses"u8;

    private static ReadOnlySpan<byte> ContentUtf8 => "content"u8;

    private static ReadOnlySpan<byte> SchemaUtf8 => "schema"u8;

    private static ReadOnlySpan<byte> InUtf8 => "in"u8;

    private static ReadOnlySpan<byte> RequiredUtf8 => "required"u8;

    private static ReadOnlySpan<byte> RefUtf8 => "$ref"u8;

    /// <summary>
    /// Builds a <see cref="ClientModel"/> from a specification document.
    /// </summary>
    /// <param name="specRoot">The root element of the parsed spec document.</param>
    /// <param name="walker">The spec walker to use.</param>
    /// <param name="filter">Optional operation filter.</param>
    /// <returns>The built <see cref="ClientModel"/>.</returns>
    public static ClientModel Build(
        JsonElement specRoot,
        ISpecWalker walker,
        OperationFilter? filter = null)
    {
        List<ClientOperation> operations = [];
        List<byte[]> schemaPointers = [];

        foreach (OperationEntry entry in walker.EnumerateOperations(specRoot, filter))
        {
            ClientOperation op = BuildOperation(entry, schemaPointers);
            operations.Add(op);
        }

        // Also collect component schemas
        foreach (ExtractedSchema extracted in walker.ExtractSchemas(specRoot, filter))
        {
            byte[]? pointer = GetSchemaPointer(extracted.Schema);
            if (pointer is not null)
            {
                schemaPointers.Add(pointer);
            }
        }

        return new ClientModel(
            specRoot,
            [.. operations],
            [.. schemaPointers]);
    }

    private static ClientOperation BuildOperation(
        OperationEntry entry,
        List<byte[]> schemaPointers)
    {
        JsonElement op = entry.Operation;

        ClientParameter[] parameters = ExtractParameters(op, entry.Path, entry.Method, schemaPointers);
        ClientRequestBody? requestBody = ExtractRequestBody(op, entry.Path, entry.Method, schemaPointers);
        ClientResponse[] responses = ExtractResponses(op, entry.Path, entry.Method, schemaPointers);

        return new ClientOperation(
            entry.Path,
            entry.Method,
            op,
            parameters,
            requestBody,
            responses);
    }

    private static ClientParameter[] ExtractParameters(
        JsonElement operation,
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        List<byte[]> schemaPointers)
    {
        if (!operation.TryGetProperty(ParametersUtf8, out JsonElement parameters)
            || parameters.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<ClientParameter> result = [];
        int index = 0;

        foreach (JsonElement param in parameters.EnumerateArray())
        {
            if (param.ValueKind != JsonValueKind.Object)
            {
                index++;
                continue;
            }

            // Resolve $ref if present (currently a no-op; full resolution is done by the V5 codegen)
            JsonElement resolved = ResolveRef(param);

            ParameterLocation location = ParseLocation(resolved);
            bool required = resolved.TryGetProperty(RequiredUtf8, out JsonElement req)
                && req.ValueKind == JsonValueKind.True;

            byte[]? schemaPointer = null;
            if (resolved.TryGetProperty(SchemaUtf8, out JsonElement schema)
                && schema.ValueKind == JsonValueKind.Object)
            {
                schemaPointer = BuildParameterSchemaPointer(pathProperty, method, index);
                schemaPointers.Add(schemaPointer);
            }

            result.Add(new ClientParameter(resolved, location, required, schemaPointer));
            index++;
        }

        return [.. result];
    }

    private static ClientRequestBody? ExtractRequestBody(
        JsonElement operation,
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        List<byte[]> schemaPointers)
    {
        if (!operation.TryGetProperty(RequestBodyUtf8, out JsonElement requestBody)
            || requestBody.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        JsonElement resolved = ResolveRef(requestBody);

        bool required = resolved.TryGetProperty(RequiredUtf8, out JsonElement req)
            && req.ValueKind == JsonValueKind.True;

        ClientMediaTypeContent[] content = ExtractContent(
            resolved,
            pathProperty,
            method,
            "requestBody"u8,
            schemaPointers);

        return new ClientRequestBody(resolved, required, content);
    }

    private static ClientResponse[] ExtractResponses(
        JsonElement operation,
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        List<byte[]> schemaPointers)
    {
        if (!operation.TryGetProperty(ResponsesUtf8, out JsonElement responses)
            || responses.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        List<ClientResponse> result = [];

        foreach (JsonProperty<JsonElement> responseProp in responses.EnumerateObject())
        {
            JsonElement response = responseProp.Value;

            if (response.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            JsonElement resolved = ResolveRef(response);

            ClientMediaTypeContent[] content = ExtractResponseContent(
                resolved,
                pathProperty,
                method,
                responseProp,
                schemaPointers);

            result.Add(new ClientResponse(responseProp, resolved, content));
        }

        return [.. result];
    }

    private static ClientMediaTypeContent[] ExtractContent(
        JsonElement parent,
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        ReadOnlySpan<byte> parentSegment,
        List<byte[]> schemaPointers)
    {
        if (!parent.TryGetProperty(ContentUtf8, out JsonElement contentMap)
            || contentMap.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        List<ClientMediaTypeContent> content = [];

        foreach (JsonProperty<JsonElement> mediaTypeProp in contentMap.EnumerateObject())
        {
            byte[]? schemaPointer = null;
            if (mediaTypeProp.Value.TryGetProperty(SchemaUtf8, out JsonElement schema)
                && schema.ValueKind == JsonValueKind.Object)
            {
                schemaPointer = BuildContentSchemaPointer(
                    pathProperty, method, parentSegment, mediaTypeProp);
                schemaPointers.Add(schemaPointer);
            }

            content.Add(new ClientMediaTypeContent(mediaTypeProp, schemaPointer));
        }

        return [.. content];
    }

    private static ClientMediaTypeContent[] ExtractResponseContent(
        JsonElement resolved,
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        JsonProperty<JsonElement> responseProp,
        List<byte[]> schemaPointers)
    {
        if (!resolved.TryGetProperty(ContentUtf8, out JsonElement contentMap)
            || contentMap.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        List<ClientMediaTypeContent> content = [];

        foreach (JsonProperty<JsonElement> mediaTypeProp in contentMap.EnumerateObject())
        {
            byte[]? schemaPointer = null;
            if (mediaTypeProp.Value.TryGetProperty(SchemaUtf8, out JsonElement schema)
                && schema.ValueKind == JsonValueKind.Object)
            {
                schemaPointer = BuildResponseContentSchemaPointer(
                    pathProperty, method, responseProp, mediaTypeProp);
                schemaPointers.Add(schemaPointer);
            }

            content.Add(new ClientMediaTypeContent(mediaTypeProp, schemaPointer));
        }

        return [.. content];
    }

    private static JsonElement ResolveRef(JsonElement element)
    {
        // For now, we don't fully resolve $ref — we just detect it.
        // Full $ref resolution is handled by the V5 codegen when processing schemas.
        // The builder just needs the inline properties.
        return element;
    }

    private static ParameterLocation ParseLocation(JsonElement param)
    {
        if (!param.TryGetProperty(InUtf8, out JsonElement inValue)
            || inValue.ValueKind != JsonValueKind.String)
        {
            return ParameterLocation.Query;
        }

        if (inValue.ValueEquals("path"u8))
        {
            return ParameterLocation.Path;
        }

        if (inValue.ValueEquals("query"u8))
        {
            return ParameterLocation.Query;
        }

        if (inValue.ValueEquals("header"u8))
        {
            return ParameterLocation.Header;
        }

        if (inValue.ValueEquals("cookie"u8))
        {
            return ParameterLocation.Cookie;
        }

        return ParameterLocation.Query;
    }

    private static byte[]? GetSchemaPointer(JsonElement schema)
    {
        // If the schema has a $ref, convert that ref string to UTF-8 bytes
        if (schema.TryGetProperty(RefUtf8, out JsonElement refValue)
            && refValue.ValueKind == JsonValueKind.String)
        {
            using UnescapedUtf8JsonString refUtf8 = refValue.GetUtf8String();
            return refUtf8.Span.ToArray();
        }

        return null;
    }

    /// <summary>
    /// Gets the UTF-8 bytes for an <see cref="OperationMethod"/>.
    /// </summary>
    private static ReadOnlySpan<byte> MethodToUtf8(OperationMethod method) =>
        method switch
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

    // Schema pointer builders -----------------------------------------------
    // All build UTF-8 pointers using Utf8JsonPointer.TryEncodeSegment for
    // segments that may contain ~ or / (path templates, media types, status codes).
    // The format is: #/paths/<encoded-path>/<method>/<suffix>

    /// <summary>
    /// Builds: #/paths/&lt;path&gt;/&lt;method&gt;/parameters/&lt;index&gt;/schema.
    /// </summary>
    private static byte[] BuildParameterSchemaPointer(
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        int index)
    {
        Span<byte> buffer = stackalloc byte[512];
        int pos = WritePointerPrefix(pathProperty, method, buffer);

        // /parameters/
        "/parameters/"u8.CopyTo(buffer[pos..]);
        pos += "/parameters/"u8.Length;

        // index
        Utf8Formatter.TryFormat(index, buffer[pos..], out int indexWritten);
        pos += indexWritten;

        // /schema
        "/schema"u8.CopyTo(buffer[pos..]);
        pos += "/schema"u8.Length;

        return buffer[..pos].ToArray();
    }

    /// <summary>
    /// Builds: #/paths/&lt;path&gt;/&lt;method&gt;/&lt;parentSegment&gt;/content/&lt;mediaType&gt;/schema.
    /// </summary>
    private static byte[] BuildContentSchemaPointer(
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        ReadOnlySpan<byte> parentSegment,
        JsonProperty<JsonElement> mediaTypeProp)
    {
        Span<byte> buffer = stackalloc byte[512];
        int pos = WritePointerPrefix(pathProperty, method, buffer);

        // /<parentSegment> (e.g. /requestBody)
        buffer[pos++] = (byte)'/';
        parentSegment.CopyTo(buffer[pos..]);
        pos += parentSegment.Length;

        // /content/
        "/content/"u8.CopyTo(buffer[pos..]);
        pos += "/content/"u8.Length;

        // Encoded media type (e.g. application/json → application~1json)
        pos += EncodePropertyNameSegment(mediaTypeProp, buffer[pos..]);

        // /schema
        "/schema"u8.CopyTo(buffer[pos..]);
        pos += "/schema"u8.Length;

        return buffer[..pos].ToArray();
    }

    /// <summary>
    /// Builds: #/paths/&lt;path&gt;/&lt;method&gt;/responses/&lt;statusCode&gt;/content/&lt;mediaType&gt;/schema.
    /// </summary>
    private static byte[] BuildResponseContentSchemaPointer(
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        JsonProperty<JsonElement> responseProp,
        JsonProperty<JsonElement> mediaTypeProp)
    {
        Span<byte> buffer = stackalloc byte[512];
        int pos = WritePointerPrefix(pathProperty, method, buffer);

        // /responses/
        "/responses/"u8.CopyTo(buffer[pos..]);
        pos += "/responses/"u8.Length;

        // Encoded status code (e.g. "200", "default", "2XX" — usually safe but encode anyway)
        pos += EncodePropertyNameSegment(responseProp, buffer[pos..]);

        // /content/
        "/content/"u8.CopyTo(buffer[pos..]);
        pos += "/content/"u8.Length;

        // Encoded media type
        pos += EncodePropertyNameSegment(mediaTypeProp, buffer[pos..]);

        // /schema
        "/schema"u8.CopyTo(buffer[pos..]);
        pos += "/schema"u8.Length;

        return buffer[..pos].ToArray();
    }

    /// <summary>
    /// Writes the common pointer prefix: #/paths/&lt;encoded-path&gt;/&lt;method&gt;.
    /// </summary>
    private static int WritePointerPrefix(
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        Span<byte> buffer)
    {
        int pos = 0;

        // #/paths/
        "#/paths/"u8.CopyTo(buffer[pos..]);
        pos += "#/paths/"u8.Length;

        // Encoded path template (e.g. /pets/{petId} → ~1pets~1{petId})
        pos += EncodePropertyNameSegment(pathProperty, buffer[pos..]);

        // /<method>
        buffer[pos++] = (byte)'/';
        ReadOnlySpan<byte> methodUtf8 = MethodToUtf8(method);
        methodUtf8.CopyTo(buffer[pos..]);
        pos += methodUtf8.Length;

        return pos;
    }

    /// <summary>
    /// Gets the UTF-8 property name and encodes it as a JSON pointer segment.
    /// </summary>
    private static int EncodePropertyNameSegment(
        JsonProperty<JsonElement> property,
        Span<byte> destination)
    {
        using UnescapedUtf8JsonString nameUtf8 = property.Utf8NameSpan;
        Utf8JsonPointer.TryEncodeSegment(nameUtf8.Span, destination, out int written);
        return written;
    }
}