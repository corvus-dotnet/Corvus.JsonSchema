// <copyright file="OpenApi31Walker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi31;

/// <summary>
/// Walks an OpenAPI 3.1 specification document using the strongly-typed
/// <see cref="OpenApiDocument"/> model.
/// </summary>
/// <remarks>
/// <para>
/// All returned <see cref="OperationEntry"/> and <see cref="ExtractedSchema"/>
/// values hold element references into the parsed document. The caller must keep
/// the <see cref="ParsedJsonDocument{T}"/> alive while consuming results.
/// </para>
/// </remarks>
public sealed class OpenApi31Walker : ISpecWalker
{
    private static readonly (ReadOnlyMemory<byte> Name, OperationMethod Method)[] HttpMethods =
    [
        ("get"u8.ToArray(), OperationMethod.Get),
        ("put"u8.ToArray(), OperationMethod.Put),
        ("post"u8.ToArray(), OperationMethod.Post),
        ("delete"u8.ToArray(), OperationMethod.Delete),
        ("options"u8.ToArray(), OperationMethod.Options),
        ("head"u8.ToArray(), OperationMethod.Head),
        ("patch"u8.ToArray(), OperationMethod.Patch),
        ("trace"u8.ToArray(), OperationMethod.Trace),
    ];

    /// <inheritdoc/>
    public IEnumerable<OperationEntry> EnumerateOperations(JsonElement specRoot, OperationFilter? filter = null)
    {
        OpenApiDocument doc = specRoot;

        if (doc.PathsValue.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (JsonProperty<JsonElement> pathProp in ((JsonElement)doc.PathsValue).EnumerateObject())
        {
            if (filter is not null)
            {
                using UnescapedUtf16JsonString name = pathProp.Utf16NameSpan;
                if (!filter.Matches(name.Span))
                {
                    continue;
                }
            }

            OpenApiDocument.PathItem pathItem = pathProp.Value;

            foreach ((ReadOnlyMemory<byte> methodName, OperationMethod method) in HttpMethods)
            {
                if (pathItem.TryGetProperty(methodName.Span, out JsonElement operation)
                    && operation.ValueKind == JsonValueKind.Object)
                {
                    OpenApiDocument.Operation typed = operation;

                    yield return new OperationEntry(
                        pathProp,
                        operation,
                        method,
                        GetOptionalString(typed.OperationId),
                        GetOptionalString(typed.Summary),
                        GetOptionalString(typed.Description),
                        ExtractParameters(typed),
                        ExtractRequestBody(typed),
                        ExtractResponses(typed),
                        ExtractTags(typed));
                }
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerable<ExtractedSchema> ExtractSchemas(JsonElement specRoot, OperationFilter? filter = null)
    {
        OpenApiDocument doc = specRoot;

        foreach (OperationEntry entry in this.EnumerateOperations(specRoot, filter))
        {
            OpenApiDocument.Operation operation = entry.Operation;

            // Request body content schemas
            OpenApiDocument.RequestBodyOrReference requestBody = operation.RequestBody;
            if (requestBody.ValueKind == JsonValueKind.Object
                && requestBody.Content.ValueKind == JsonValueKind.Object)
            {
                foreach (ExtractedSchema schema in EnumerateMediaTypeSchemas(
                    requestBody.Content, SchemaRole.RequestBody))
                {
                    yield return schema;
                }
            }

            // Response content schemas and headers
            if (operation.ResponsesValue.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty<JsonElement> responseProp in
                    ((JsonElement)operation.ResponsesValue).EnumerateObject())
                {
                    OpenApiDocument.ResponseOrReference response = responseProp.Value;

                    if (response.Content.ValueKind == JsonValueKind.Object)
                    {
                        foreach (ExtractedSchema schema in EnumerateMediaTypeSchemas(
                            response.Content, SchemaRole.ResponseBody))
                        {
                            yield return schema;
                        }
                    }

                    // Response header schemas
                    if (response.Headers.ValueKind == JsonValueKind.Object)
                    {
                        foreach (JsonProperty<JsonElement> headerProp in
                            ((JsonElement)response.Headers).EnumerateObject())
                        {
                            OpenApiDocument.HeaderOrReference header = headerProp.Value;
                            if (header.Schema.ValueKind == JsonValueKind.Object)
                            {
                                yield return new ExtractedSchema(
                                    (JsonElement)header.Schema, SchemaRole.Header);
                            }
                        }
                    }
                }
            }

            // Parameter schemas
            if (operation.Parameters.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement param in ((JsonElement)operation.Parameters).EnumerateArray())
                {
                    OpenApiDocument.ParameterOrReference parameter = param;
                    if (parameter.Schema.ValueKind == JsonValueKind.Object)
                    {
                        yield return new ExtractedSchema(
                            (JsonElement)parameter.Schema, SchemaRole.Parameter);
                    }
                }
            }
        }

        // Component schemas
        if (doc.ComponentsValue.ValueKind == JsonValueKind.Object)
        {
            OpenApiDocument.Components components = doc.ComponentsValue;
            if (components.Schemas.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty<JsonElement> schemaProp in
                    ((JsonElement)components.Schemas).EnumerateObject())
                {
                    yield return new ExtractedSchema(schemaProp.Value, SchemaRole.ComponentSchema);
                }
            }
        }
    }

    private static string[] ExtractTags(OpenApiDocument.Operation operation)
    {
        if (operation.Tags.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<string> result = [];

        foreach (JsonElement tag in ((JsonElement)operation.Tags).EnumerateArray())
        {
            if (tag.ValueKind == JsonValueKind.String)
            {
                result.Add(tag.GetString()!);
            }
        }

        return [.. result];
    }

    private static WalkedParameter[] ExtractParameters(OpenApiDocument.Operation operation)
    {
        if (operation.Parameters.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<WalkedParameter> result = [];

        foreach (JsonElement param in ((JsonElement)operation.Parameters).EnumerateArray())
        {
            if (param.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            OpenApiDocument.ParameterOrReference typed = param;

            string name = GetOptionalString(typed.Name) ?? "unknown";
            ParameterLocation location = ParseLocation(typed.In);
            bool required = typed.Required.ValueKind == JsonValueKind.True;
            bool hasSchema = typed.Schema.ValueKind == JsonValueKind.Object;

            (ParameterStyle style, bool explode) = ParseStyleAndExplode(typed.Style, typed.Explode, location);

            result.Add(new WalkedParameter(name, location, required, style, explode, hasSchema));
        }

        return [.. result];
    }

    private static WalkedRequestBody? ExtractRequestBody(OpenApiDocument.Operation operation)
    {
        OpenApiDocument.RequestBodyOrReference requestBody = operation.RequestBody;

        if (requestBody.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        bool required = requestBody.Required.ValueKind == JsonValueKind.True;
        WalkedMediaTypeContent[] content = ExtractMediaTypeContent(requestBody.Content);
        string? description = GetOptionalString(requestBody.Description);

        return new WalkedRequestBody(required, content, description);
    }

    private static WalkedResponse[] ExtractResponses(OpenApiDocument.Operation operation)
    {
        if (operation.ResponsesValue.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        List<WalkedResponse> result = [];

        foreach (JsonProperty<JsonElement> responseProp in
            ((JsonElement)operation.ResponsesValue).EnumerateObject())
        {
            if (responseProp.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            OpenApiDocument.ResponseOrReference response = responseProp.Value;

            string statusCode = responseProp.Name;
            string? description = GetOptionalString(response.Description);
            WalkedMediaTypeContent[] content = ExtractMediaTypeContent(response.Content);

            result.Add(new WalkedResponse(statusCode, description, content));
        }

        return [.. result];
    }

    private static WalkedMediaTypeContent[] ExtractMediaTypeContent(JsonElement contentMap)
    {
        if (contentMap.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        List<WalkedMediaTypeContent> result = [];

        foreach (JsonProperty<JsonElement> mediaTypeProp in contentMap.EnumerateObject())
        {
            OpenApiDocument.MediaType mediaType = mediaTypeProp.Value;
            bool hasSchema = mediaType.SchemaValue.ValueKind == JsonValueKind.Object;
            result.Add(new WalkedMediaTypeContent(mediaTypeProp.Name, hasSchema));
        }

        return [.. result];
    }

    private static ParameterLocation ParseLocation(JsonElement inValue)
    {
        if (inValue.ValueKind != JsonValueKind.String)
        {
            return ParameterLocation.Query;
        }

        if (inValue.ValueEquals("path"u8))
        {
            return ParameterLocation.Path;
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

    private static (ParameterStyle Style, bool Explode) ParseStyleAndExplode(
        JsonElement styleValue,
        JsonElement explodeValue,
        ParameterLocation location)
    {
        ParameterStyle style = ParseStyle(styleValue, location);

        bool explode;
        if (explodeValue.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            explode = explodeValue.ValueKind == JsonValueKind.True;
        }
        else
        {
            explode = style == ParameterStyle.Form;
        }

        return (style, explode);
    }

    private static ParameterStyle ParseStyle(JsonElement styleValue, ParameterLocation location)
    {
        if (styleValue.ValueKind == JsonValueKind.String)
        {
            if (styleValue.ValueEquals("form"u8))
            {
                return ParameterStyle.Form;
            }

            if (styleValue.ValueEquals("simple"u8))
            {
                return ParameterStyle.Simple;
            }

            if (styleValue.ValueEquals("label"u8))
            {
                return ParameterStyle.Label;
            }

            if (styleValue.ValueEquals("matrix"u8))
            {
                return ParameterStyle.Matrix;
            }

            if (styleValue.ValueEquals("spaceDelimited"u8))
            {
                return ParameterStyle.SpaceDelimited;
            }

            if (styleValue.ValueEquals("pipeDelimited"u8))
            {
                return ParameterStyle.PipeDelimited;
            }

            if (styleValue.ValueEquals("deepObject"u8))
            {
                return ParameterStyle.DeepObject;
            }
        }

        return location switch
        {
            ParameterLocation.Query or ParameterLocation.Cookie => ParameterStyle.Form,
            _ => ParameterStyle.Simple,
        };
    }

    private static string? GetOptionalString(JsonElement element) =>
        element.ValueKind == JsonValueKind.String ? element.GetString() : null;

    private static IEnumerable<ExtractedSchema> EnumerateMediaTypeSchemas(
        JsonElement contentMap, SchemaRole role)
    {
        foreach (JsonProperty<JsonElement> mediaTypeProp in contentMap.EnumerateObject())
        {
            OpenApiDocument.MediaType mediaType = mediaTypeProp.Value;
            if (mediaType.SchemaValue.ValueKind == JsonValueKind.Object)
            {
                yield return new ExtractedSchema((JsonElement)mediaType.SchemaValue, role);
            }
        }
    }
}