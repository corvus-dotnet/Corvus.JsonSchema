// <copyright file="OpenApi30Walker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi30;

/// <summary>
/// Walks an OpenAPI 3.0 specification document using the strongly-typed
/// <see cref="OpenApiDocument"/> model.
/// </summary>
/// <remarks>
/// <para>
/// All returned <see cref="OperationEntry"/> and <see cref="ExtractedSchema"/>
/// values hold element references into the parsed document. The caller must keep
/// the <see cref="ParsedJsonDocument{T}"/> alive while consuming results.
/// </para>
/// </remarks>
public sealed class OpenApi30Walker : ISpecWalker
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
                        ExtractParameters(typed),
                        ExtractRequestBody(typed),
                        ExtractResponses(typed));
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
            if (operation.RequestBody.ValueKind == JsonValueKind.Object
                && operation.RequestBody.TryGetProperty("content"u8, out JsonElement requestContent)
                && requestContent.ValueKind == JsonValueKind.Object)
            {
                foreach (ExtractedSchema schema in EnumerateMediaTypeSchemas(
                    requestContent, SchemaRole.RequestBody))
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
                    OpenApiDocument.Response response = responseProp.Value;

                    if (response.Content.ValueKind == JsonValueKind.Object)
                    {
                        foreach (ExtractedSchema schema in EnumerateMediaTypeSchemas(
                            (JsonElement)response.Content, SchemaRole.ResponseBody))
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
                            if (headerProp.Value.TryGetProperty("schema"u8, out JsonElement headerSchema)
                                && headerSchema.ValueKind == JsonValueKind.Object)
                            {
                                yield return new ExtractedSchema(headerSchema, SchemaRole.Header);
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
                    if (param.TryGetProperty("schema"u8, out JsonElement paramSchema)
                        && paramSchema.ValueKind == JsonValueKind.Object)
                    {
                        yield return new ExtractedSchema(paramSchema, SchemaRole.Parameter);
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

            OpenApiDocument.Parameter typed = param;

            ParameterLocation location = ParseLocation((JsonElement)typed.In);
            bool required = typed.Required.ValueKind == JsonValueKind.True;
            bool hasSchema = typed.Schema.ValueKind == JsonValueKind.Object;

            (ParameterStyle style, bool explode) = ParseStyleAndExplode(
                (JsonElement)typed.Style, (JsonElement)typed.Explode, location);

            result.Add(new WalkedParameter(param, location, required, style, explode, hasSchema));
        }

        return [.. result];
    }

    private static WalkedRequestBody? ExtractRequestBody(OpenApiDocument.Operation operation)
    {
        JsonElement requestBody = (JsonElement)operation.RequestBody;

        if (requestBody.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        bool required = requestBody.TryGetProperty("required"u8, out JsonElement req)
            && req.ValueKind == JsonValueKind.True;

        WalkedMediaTypeContent[] content = [];
        if (requestBody.TryGetProperty("content"u8, out JsonElement contentMap)
            && contentMap.ValueKind == JsonValueKind.Object)
        {
            content = ExtractMediaTypeContent(contentMap);
        }

        return new WalkedRequestBody(requestBody, required, content);
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

            OpenApiDocument.Response response = responseProp.Value;

            WalkedMediaTypeContent[] content = response.Content.ValueKind == JsonValueKind.Object
                ? ExtractMediaTypeContent((JsonElement)response.Content)
                : [];

            result.Add(new WalkedResponse(responseProp, content));
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
            bool hasSchema = mediaType.Schema.ValueKind == JsonValueKind.Object;
            result.Add(new WalkedMediaTypeContent(mediaTypeProp, hasSchema));
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

    private static IEnumerable<ExtractedSchema> EnumerateMediaTypeSchemas(
        JsonElement contentMap, SchemaRole role)
    {
        foreach (JsonProperty<JsonElement> mediaTypeProp in contentMap.EnumerateObject())
        {
            OpenApiDocument.MediaType mediaType = mediaTypeProp.Value;
            if (mediaType.Schema.ValueKind == JsonValueKind.Object)
            {
                yield return new ExtractedSchema((JsonElement)mediaType.Schema, role);
            }
        }
    }
}