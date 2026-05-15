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
    public IEnumerable<OperationEntry> EnumerateOperations(
        JsonElement specRoot,
        OperationFilter? filter = null,
        IOpenApiReferenceResolver? referenceResolver = null)
    {
        OpenApiDocument doc = specRoot;
        referenceResolver ??= new LocalReferenceResolver(specRoot);

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
                        ExtractParameters(typed, pathItem, referenceResolver),
                        ExtractRequestBody(typed, referenceResolver),
                        ExtractResponses(typed, referenceResolver));
                }
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerable<ExtractedSchema> ExtractSchemas(
        JsonElement specRoot,
        OperationFilter? filter = null,
        IOpenApiReferenceResolver? referenceResolver = null)
    {
        OpenApiDocument doc = specRoot;
        referenceResolver ??= new LocalReferenceResolver(specRoot);

        foreach (OperationEntry entry in this.EnumerateOperations(specRoot, filter, referenceResolver))
        {
            OpenApiDocument.Operation operation = entry.Operation;

            // Request body content schemas (resolve $ref)
            JsonElement requestBody = (JsonElement)operation.RequestBody;
            if (requestBody.ValueKind == JsonValueKind.Object)
            {
                requestBody = ResolveRef(requestBody, referenceResolver);

                if (requestBody.TryGetProperty("content"u8, out JsonElement requestContent)
                    && requestContent.ValueKind == JsonValueKind.Object)
                {
                    foreach (ExtractedSchema schema in EnumerateMediaTypeSchemas(
                        requestContent, SchemaRole.RequestBody))
                    {
                        yield return schema;
                    }
                }
            }

            // Response content schemas and headers (resolve $ref)
            if (operation.ResponsesValue.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty<JsonElement> responseProp in
                    ((JsonElement)operation.ResponsesValue).EnumerateObject())
                {
                    JsonElement responseElement = responseProp.Value;
                    if (responseElement.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    responseElement = ResolveRef(responseElement, referenceResolver);
                    OpenApiDocument.Response response = responseElement;

                    if (response.Content.ValueKind == JsonValueKind.Object)
                    {
                        foreach (ExtractedSchema schema in EnumerateMediaTypeSchemas(
                            (JsonElement)response.Content, SchemaRole.ResponseBody))
                        {
                            yield return schema;
                        }
                    }

                    // Response header schemas (resolve $ref on each header)
                    if (response.Headers.ValueKind == JsonValueKind.Object)
                    {
                        foreach (JsonProperty<JsonElement> headerProp in
                            ((JsonElement)response.Headers).EnumerateObject())
                        {
                            JsonElement headerElement = ResolveRef(headerProp.Value, referenceResolver);

                            if (headerElement.TryGetProperty("schema"u8, out JsonElement headerSchema)
                                && headerSchema.ValueKind == JsonValueKind.Object)
                            {
                                yield return new ExtractedSchema(headerSchema, SchemaRole.Header);
                            }
                        }
                    }
                }
            }

            // Parameter schemas (merged from path-level and operation-level, already resolved)
            foreach (WalkedParameter walkedParam in entry.Parameters)
            {
                if (walkedParam.Element.TryGetProperty("schema"u8, out JsonElement paramSchema)
                    && paramSchema.ValueKind == JsonValueKind.Object)
                {
                    yield return new ExtractedSchema(paramSchema, SchemaRole.Parameter);
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

    /// <summary>
    /// Resolves a <c>$ref</c> on a raw <see cref="JsonElement"/> for OpenAPI 3.0
    /// types that do not have union-type discrimination (i.e. non-<c>OrReference</c> types).
    /// </summary>
    private static JsonElement ResolveRef(
        JsonElement element,
        IOpenApiReferenceResolver referenceResolver)
    {
        if (element.TryGetProperty("$ref"u8, out JsonElement refProp)
            && refProp.ValueKind == JsonValueKind.String
            && referenceResolver.TryResolve(refProp.GetString()!, out JsonElement resolved))
        {
            return resolved;
        }

        return element;
    }

    private static WalkedParameter[] ExtractParameters(
        OpenApiDocument.Operation operation,
        OpenApiDocument.PathItem pathItem,
        IOpenApiReferenceResolver referenceResolver)
    {
        bool hasOperationParams = operation.Parameters.ValueKind == JsonValueKind.Array;
        bool hasPathParams = pathItem.Parameters.ValueKind == JsonValueKind.Array;

        if (!hasOperationParams && !hasPathParams)
        {
            return [];
        }

        List<WalkedParameter> result = [];

        // Start with path-level parameters.
        if (hasPathParams)
        {
            int sourceIndex = 0;
            foreach (JsonElement param in ((JsonElement)pathItem.Parameters).EnumerateArray())
            {
                if (param.ValueKind != JsonValueKind.Object)
                {
                    sourceIndex++;
                    continue;
                }

                JsonElement resolved = ResolveRef(param, referenceResolver);
                OpenApiDocument.Parameter typed = resolved;

                ParameterLocation location = ParseLocation((JsonElement)typed.In);
                bool required = typed.Required.ValueKind == JsonValueKind.True;
                bool hasSchema = typed.Schema.ValueKind == JsonValueKind.Object;

                (ParameterStyle style, bool explode) = ParseStyleAndExplode(
                    (JsonElement)typed.Style, (JsonElement)typed.Explode, location);

                result.Add(new WalkedParameter(
                    resolved, location, required, style, explode, hasSchema,
                    hasSchema ? SchemaClassifier.Classify((JsonElement)typed.Schema) : ParameterSerializationKind.String,
                    isPathLevel: true, sourceIndex: sourceIndex));
                sourceIndex++;
            }
        }

        // Add operation-level parameters, replacing any path-level parameter
        // that shares the same name+in combination (operation wins per OpenAPI spec).
        if (hasOperationParams)
        {
            int sourceIndex = 0;
            foreach (JsonElement param in ((JsonElement)operation.Parameters).EnumerateArray())
            {
                if (param.ValueKind != JsonValueKind.Object)
                {
                    sourceIndex++;
                    continue;
                }

                OpenApiDocument.Parameter typed = ResolveRef(param, referenceResolver);

                ParameterLocation location = ParseLocation((JsonElement)typed.In);
                bool required = typed.Required.ValueKind == JsonValueKind.True;
                bool hasSchema = typed.Schema.ValueKind == JsonValueKind.Object;

                (ParameterStyle style, bool explode) = ParseStyleAndExplode(
                    (JsonElement)typed.Style, (JsonElement)typed.Explode, location);

                // Replace any path-level param with matching name+in.
                int existingIndex = FindParameterIndex(result, (JsonElement)typed.Name, location);
                WalkedParameter walkedParam = new(
                    (JsonElement)typed, location, required, style, explode, hasSchema,
                    hasSchema ? SchemaClassifier.Classify((JsonElement)typed.Schema) : ParameterSerializationKind.String,
                    isPathLevel: false, sourceIndex: sourceIndex);

                if (existingIndex >= 0)
                {
                    result[existingIndex] = walkedParam;
                }
                else
                {
                    result.Add(walkedParam);
                }

                sourceIndex++;
            }
        }

        return [.. result];
    }

    private static int FindParameterIndex(
        List<WalkedParameter> parameters,
        JsonElement name,
        ParameterLocation location)
    {
        if (name.ValueKind != JsonValueKind.String)
        {
            return -1;
        }

        for (int i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].Location != location)
            {
                continue;
            }

            OpenApiDocument.Parameter existing = parameters[i].Element;
            if (((JsonElement)existing.Name).ValueKind == JsonValueKind.String
                && name.Equals((JsonElement)existing.Name))
            {
                return i;
            }
        }

        return -1;
    }

    private static WalkedRequestBody? ExtractRequestBody(
        OpenApiDocument.Operation operation,
        IOpenApiReferenceResolver referenceResolver)
    {
        JsonElement requestBody = (JsonElement)operation.RequestBody;

        if (requestBody.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Resolve $ref if present
        requestBody = ResolveRef(requestBody, referenceResolver);

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

    private static WalkedResponse[] ExtractResponses(
        OpenApiDocument.Operation operation,
        IOpenApiReferenceResolver referenceResolver)
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

            // Resolve $ref on the response
            JsonElement responseElement = ResolveRef(responseProp.Value, referenceResolver);
            OpenApiDocument.Response response = responseElement;

            WalkedMediaTypeContent[] content = response.Content.ValueKind == JsonValueKind.Object
                ? ExtractMediaTypeContent((JsonElement)response.Content)
                : [];

            WalkedResponseHeader[] headers = response.Headers.ValueKind == JsonValueKind.Object
                ? ExtractResponseHeaders((JsonElement)response.Headers, referenceResolver)
                : [];

            result.Add(new WalkedResponse(responseProp, content, headers));
        }

        return [.. result];
    }

    private static WalkedResponseHeader[] ExtractResponseHeaders(
        JsonElement headersMap,
        IOpenApiReferenceResolver referenceResolver)
    {
        List<WalkedResponseHeader> result = [];

        foreach (JsonProperty<JsonElement> headerProp in headersMap.EnumerateObject())
        {
            // Resolve $ref on the header
            JsonElement headerElement = ResolveRef(headerProp.Value, referenceResolver);

            bool hasSchema = headerElement.TryGetProperty("schema"u8, out JsonElement schema)
                && schema.ValueKind == JsonValueKind.Object;
            result.Add(new WalkedResponseHeader(headerProp, hasSchema));
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

        if (inValue.ValueEquals(ParameterInUtf8.Path))
        {
            return ParameterLocation.Path;
        }

        if (inValue.ValueEquals(ParameterInUtf8.Header))
        {
            return ParameterLocation.Header;
        }

        if (inValue.ValueEquals(ParameterInUtf8.Cookie))
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
            if (styleValue.ValueEquals(ParameterStyleUtf8.Form))
            {
                return ParameterStyle.Form;
            }

            if (styleValue.ValueEquals(ParameterStyleUtf8.Simple))
            {
                return ParameterStyle.Simple;
            }

            if (styleValue.ValueEquals(ParameterStyleUtf8.Label))
            {
                return ParameterStyle.Label;
            }

            if (styleValue.ValueEquals(ParameterStyleUtf8.Matrix))
            {
                return ParameterStyle.Matrix;
            }

            if (styleValue.ValueEquals(ParameterStyleUtf8.SpaceDelimited))
            {
                return ParameterStyle.SpaceDelimited;
            }

            if (styleValue.ValueEquals(ParameterStyleUtf8.PipeDelimited))
            {
                return ParameterStyle.PipeDelimited;
            }

            if (styleValue.ValueEquals(ParameterStyleUtf8.DeepObject))
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

    /// <summary>
    /// UTF-8 constants for the OpenAPI <c>in</c> property values.
    /// </summary>
    /// <remarks>
    /// The OpenAPI 3.0 schema's <c>in</c> property is typed as <see cref="OpenApi30.JsonString"/>
    /// without enum values accessible from a single generated type, so we define the constants here.
    /// </remarks>
    private static class ParameterInUtf8
    {
        public static ReadOnlySpan<byte> Path => "path"u8;

        public static ReadOnlySpan<byte> Query => "query"u8;

        public static ReadOnlySpan<byte> Header => "header"u8;

        public static ReadOnlySpan<byte> Cookie => "cookie"u8;
    }

    /// <summary>
    /// UTF-8 constants for the OpenAPI <c>style</c> property values.
    /// </summary>
    /// <remarks>
    /// The OpenAPI 3.0 schema does not expose style values as simple enum constants,
    /// so we define them here.
    /// </remarks>
    private static class ParameterStyleUtf8
    {
        public static ReadOnlySpan<byte> Form => "form"u8;

        public static ReadOnlySpan<byte> Simple => "simple"u8;

        public static ReadOnlySpan<byte> Label => "label"u8;

        public static ReadOnlySpan<byte> Matrix => "matrix"u8;

        public static ReadOnlySpan<byte> SpaceDelimited => "spaceDelimited"u8;

        public static ReadOnlySpan<byte> PipeDelimited => "pipeDelimited"u8;

        public static ReadOnlySpan<byte> DeepObject => "deepObject"u8;
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