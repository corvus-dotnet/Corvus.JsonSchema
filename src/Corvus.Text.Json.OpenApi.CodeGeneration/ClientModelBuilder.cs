// <copyright file="ClientModelBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Builds a <see cref="ClientModel"/> from the output of an <see cref="ISpecWalker"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the materialization boundary: the walker returns zero-allocation
/// <see cref="JsonElement"/>-backed values, and the builder converts them into
/// the string-based <see cref="ClientModel"/> needed by code emitters.
/// </para>
/// </remarks>
public static class ClientModelBuilder
{
    private static readonly byte[] OperationIdUtf8 = "operationId"u8.ToArray();
    private static readonly byte[] SummaryUtf8 = "summary"u8.ToArray();
    private static readonly byte[] DescriptionUtf8 = "description"u8.ToArray();
    private static readonly byte[] TagsUtf8 = "tags"u8.ToArray();
    private static readonly byte[] ParametersUtf8 = "parameters"u8.ToArray();
    private static readonly byte[] RequestBodyUtf8 = "requestBody"u8.ToArray();
    private static readonly byte[] ResponsesUtf8 = "responses"u8.ToArray();
    private static readonly byte[] ContentUtf8 = "content"u8.ToArray();
    private static readonly byte[] SchemaUtf8 = "schema"u8.ToArray();
    private static readonly byte[] NameUtf8 = "name"u8.ToArray();
    private static readonly byte[] InUtf8 = "in"u8.ToArray();
    private static readonly byte[] RequiredUtf8 = "required"u8.ToArray();
    private static readonly byte[] InfoUtf8 = "info"u8.ToArray();
    private static readonly byte[] TitleUtf8 = "title"u8.ToArray();
    private static readonly byte[] VersionUtf8 = "version"u8.ToArray();
    private static readonly byte[] RefUtf8 = "$ref"u8.ToArray();
    private static readonly byte[] PathsUtf8 = "paths"u8.ToArray();

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
        (string? title, string? version, string? description) = ExtractInfo(specRoot);

        List<ClientOperation> operations = [];
        HashSet<string> schemaPointers = new(StringComparer.Ordinal);

        foreach (OperationEntry entry in walker.EnumerateOperations(specRoot, filter))
        {
            string path = entry.Path.Name;
            ClientOperation op = BuildOperation(entry, path, schemaPointers);
            operations.Add(op);
        }

        // Also collect component schemas
        foreach (ExtractedSchema extracted in walker.ExtractSchemas(specRoot, filter))
        {
            string? pointer = GetSchemaPointer(extracted.Schema);
            if (pointer is not null)
            {
                schemaPointers.Add(pointer);
            }
        }

        return new ClientModel(
            title,
            version,
            description,
            operations,
            [.. schemaPointers]);
    }

    private static (string? Title, string? Version, string? Description) ExtractInfo(
        JsonElement specRoot)
    {
        if (!specRoot.TryGetProperty(InfoUtf8, out JsonElement info)
            || info.ValueKind != JsonValueKind.Object)
        {
            return (null, null, null);
        }

        string? title = info.TryGetProperty(TitleUtf8, out JsonElement t)
            && t.ValueKind == JsonValueKind.String
            ? t.GetString() : null;

        string? version = info.TryGetProperty(VersionUtf8, out JsonElement v)
            && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

        string? description = info.TryGetProperty(DescriptionUtf8, out JsonElement d)
            && d.ValueKind == JsonValueKind.String
            ? d.GetString() : null;

        return (title, version, description);
    }

    private static ClientOperation BuildOperation(
        OperationEntry entry,
        string path,
        HashSet<string> schemaPointers)
    {
        JsonElement op = entry.Operation;

        string? operationId = TryGetString(op, OperationIdUtf8);
        string? summary = TryGetString(op, SummaryUtf8);
        string? description = TryGetString(op, DescriptionUtf8);
        IReadOnlyList<string> tags = ExtractTags(op);
        IReadOnlyList<ClientParameter> parameters = ExtractParameters(op, path, entry.Method, schemaPointers);
        ClientRequestBody? requestBody = ExtractRequestBody(op, path, entry.Method, schemaPointers);
        IReadOnlyList<ClientResponse> responses = ExtractResponses(op, path, entry.Method, schemaPointers);

        return new ClientOperation(
            operationId,
            path,
            entry.Method,
            summary,
            description,
            tags,
            parameters,
            requestBody,
            responses);
    }

    private static IReadOnlyList<string> ExtractTags(JsonElement operation)
    {
        if (!operation.TryGetProperty(TagsUtf8, out JsonElement tags)
            || tags.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<string> result = [];
        foreach (JsonElement tag in tags.EnumerateArray())
        {
            if (tag.ValueKind == JsonValueKind.String)
            {
                result.Add(tag.GetString()!);
            }
        }

        return result;
    }

    private static IReadOnlyList<ClientParameter> ExtractParameters(
        JsonElement operation,
        string path,
        OperationMethod method,
        HashSet<string> schemaPointers)
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

            // Resolve $ref if present
            JsonElement resolved = ResolveRef(param, operation);

            string? name = TryGetString(resolved, NameUtf8);
            string? inValue = TryGetString(resolved, InUtf8);
            bool required = resolved.TryGetProperty(RequiredUtf8, out JsonElement req)
                && req.ValueKind == JsonValueKind.True;
            string? description = TryGetString(resolved, DescriptionUtf8);

            ParameterLocation location = ParseLocation(inValue);

            string? schemaPointer = null;
            if (resolved.TryGetProperty(SchemaUtf8, out JsonElement schema)
                && schema.ValueKind == JsonValueKind.Object)
            {
                schemaPointer = BuildPointer(
                    "#/paths", EscapeJsonPointer(path), method.ToString().ToLowerInvariant(),
                    "parameters", index.ToString(), "schema");

                schemaPointers.Add(schemaPointer);
            }

            result.Add(new ClientParameter(
                name ?? $"param{index}",
                location,
                required,
                schemaPointer,
                description));

            index++;
        }

        return result;
    }

    private static ClientRequestBody? ExtractRequestBody(
        JsonElement operation,
        string path,
        OperationMethod method,
        HashSet<string> schemaPointers)
    {
        if (!operation.TryGetProperty(RequestBodyUtf8, out JsonElement requestBody)
            || requestBody.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        JsonElement resolved = ResolveRef(requestBody, operation);

        bool required = resolved.TryGetProperty(RequiredUtf8, out JsonElement req)
            && req.ValueKind == JsonValueKind.True;
        string? description = TryGetString(resolved, DescriptionUtf8);

        List<ClientMediaTypeContent> content = ExtractContent(
            resolved,
            path,
            method,
            "requestBody",
            schemaPointers);

        return new ClientRequestBody(required, content, description);
    }

    private static IReadOnlyList<ClientResponse> ExtractResponses(
        JsonElement operation,
        string path,
        OperationMethod method,
        HashSet<string> schemaPointers)
    {
        if (!operation.TryGetProperty(ResponsesUtf8, out JsonElement responses)
            || responses.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        List<ClientResponse> result = [];

        foreach (JsonProperty<JsonElement> responseProp in responses.EnumerateObject())
        {
            string statusCode = responseProp.Name;
            JsonElement response = responseProp.Value;

            if (response.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            JsonElement resolved = ResolveRef(response, operation);

            string? description = TryGetString(resolved, DescriptionUtf8);

            List<ClientMediaTypeContent> content = ExtractContent(
                resolved,
                path,
                method,
                $"responses/{EscapeJsonPointer(statusCode)}",
                schemaPointers);

            result.Add(new ClientResponse(statusCode, content, description));
        }

        return result;
    }

    private static List<ClientMediaTypeContent> ExtractContent(
        JsonElement parent,
        string path,
        OperationMethod method,
        string parentSegment,
        HashSet<string> schemaPointers)
    {
        List<ClientMediaTypeContent> content = [];

        if (!parent.TryGetProperty(ContentUtf8, out JsonElement contentMap)
            || contentMap.ValueKind != JsonValueKind.Object)
        {
            return content;
        }

        foreach (JsonProperty<JsonElement> mediaTypeProp in contentMap.EnumerateObject())
        {
            string mediaType = mediaTypeProp.Name;
            JsonElement mediaTypeObj = mediaTypeProp.Value;

            string? schemaPointer = null;
            if (mediaTypeObj.TryGetProperty(SchemaUtf8, out JsonElement schema)
                && schema.ValueKind == JsonValueKind.Object)
            {
                schemaPointer = BuildPointer(
                    "#/paths",
                    EscapeJsonPointer(path),
                    method.ToString().ToLowerInvariant(),
                    parentSegment,
                    "content",
                    EscapeJsonPointer(mediaType),
                    "schema");

                schemaPointers.Add(schemaPointer);
            }

            content.Add(new ClientMediaTypeContent(mediaType, schemaPointer));
        }

        return content;
    }

    private static JsonElement ResolveRef(JsonElement element, JsonElement root)
    {
        // For now, we don't fully resolve $ref — we just detect it.
        // Full $ref resolution is handled by the V5 codegen when processing schemas.
        // The builder just needs the inline properties.
        if (element.TryGetProperty(RefUtf8, out JsonElement refValue)
            && refValue.ValueKind == JsonValueKind.String)
        {
            // If this is a $ref, the element itself may still have inline properties
            // (OpenAPI 3.1 supports $ref with sibling keywords). For building the
            // client model we use the element as-is; schema resolution happens later.
        }

        return element;
    }

    private static ParameterLocation ParseLocation(string? value) =>
        value switch
        {
            "path" => ParameterLocation.Path,
            "query" => ParameterLocation.Query,
            "header" => ParameterLocation.Header,
            "cookie" => ParameterLocation.Cookie,
            _ => ParameterLocation.Query,
        };

    private static string? TryGetString(JsonElement element, byte[] propertyName)
    {
        if (element.TryGetProperty(propertyName, out JsonElement value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private static string EscapeJsonPointer(string segment) =>
        segment.Replace("~", "~0", StringComparison.Ordinal)
               .Replace("/", "~1", StringComparison.Ordinal);

    private static string BuildPointer(params ReadOnlySpan<string> segments)
    {
        StringBuilder sb = new();
        foreach (string segment in segments)
        {
            if (sb.Length > 0 && sb[sb.Length - 1] != '/')
            {
                sb.Append('/');
            }

            sb.Append(segment);
        }

        return sb.ToString();
    }

    private static string GetSchemaPointer(JsonElement schema)
    {
        // If the schema has a $ref, that IS the pointer
        if (schema.TryGetProperty(RefUtf8, out JsonElement refValue)
            && refValue.ValueKind == JsonValueKind.String)
        {
            return refValue.GetString()!;
        }

        // For inline schemas, we can't easily determine the pointer without
        // tracking it during walking. Return a placeholder that the emitter
        // can handle by generating an anonymous type.
        return $"#/anonymous/{Guid.NewGuid():N}";
    }
}