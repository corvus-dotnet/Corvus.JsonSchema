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

            // Request body content schemas
            OpenApiDocument.RequestBodyOrReference requestBodyOrRef = operation.RequestBody;
            if (requestBodyOrRef.ValueKind == JsonValueKind.Object)
            {
                OpenApiDocument.RequestBodyOrReference? resolvedBody = ResolveRequestBody(requestBodyOrRef, referenceResolver);
                if (resolvedBody is { } requestBody
                    && requestBody.Content.ValueKind == JsonValueKind.Object)
                {
                    foreach (ExtractedSchema schema in EnumerateMediaTypeSchemas(
                        requestBody.Content, SchemaRole.RequestBody))
                    {
                        yield return schema;
                    }
                }
            }

            // Response content schemas and headers
            if (operation.ResponsesValue.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty<JsonElement> responseProp in
                    ((JsonElement)operation.ResponsesValue).EnumerateObject())
                {
                    if (responseProp.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    OpenApiDocument.ResponseOrReference responseOrRef = responseProp.Value;
                    OpenApiDocument.ResponseOrReference? resolvedResponse = ResolveResponse(responseOrRef, referenceResolver);
                    if (resolvedResponse is not { } response)
                    {
                        continue;
                    }

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
                            if (headerProp.Value.ValueKind != JsonValueKind.Object)
                            {
                                continue;
                            }

                            OpenApiDocument.HeaderOrReference headerOrRef = headerProp.Value;
                            OpenApiDocument.HeaderOrReference? resolvedHeader = ResolveHeader(headerOrRef, referenceResolver);
                            if (resolvedHeader is { } header
                                && header.Schema.ValueKind == JsonValueKind.Object)
                            {
                                yield return new ExtractedSchema(
                                    (JsonElement)header.Schema, SchemaRole.Header);
                            }
                        }
                    }
                }
            }

            // Parameter schemas (merged from path-level and operation-level, already resolved)
            foreach (WalkedParameter walkedParam in entry.Parameters)
            {
                OpenApiDocument.ParameterOrReference parameter = walkedParam.Element;
                if (parameter.Schema.ValueKind == JsonValueKind.Object)
                {
                    yield return new ExtractedSchema(
                        (JsonElement)parameter.Schema, SchemaRole.Parameter);
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

                OpenApiDocument.ParameterOrReference paramOrRef = param;
                (JsonElement element, OpenApiDocument.ParameterOrReference typed)? resolved =
                    ResolveParameter(paramOrRef, referenceResolver);

                if (resolved is not null)
                {
                    var (element, typed) = resolved.Value;
                    ParameterLocation location = ParseLocation(typed.In);
                    bool required = typed.Required.ValueKind == JsonValueKind.True;
                    bool hasSchema = typed.Schema.ValueKind == JsonValueKind.Object;

                    (ParameterStyle style, bool explode) = ParseStyleAndExplode(typed.Style, typed.Explode, location);

                    result.Add(new WalkedParameter(
                        element, location, required, style, explode, hasSchema,
                        isPathLevel: true, sourceIndex: sourceIndex));
                }

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

                OpenApiDocument.ParameterOrReference paramOrRef = param;
                (JsonElement element, OpenApiDocument.ParameterOrReference typed)? resolved =
                    ResolveParameter(paramOrRef, referenceResolver);

                if (resolved is not null)
                {
                    var (element, typed) = resolved.Value;
                    ParameterLocation location = ParseLocation(typed.In);
                    bool required = typed.Required.ValueKind == JsonValueKind.True;
                    bool hasSchema = typed.Schema.ValueKind == JsonValueKind.Object;

                    (ParameterStyle style, bool explode) = ParseStyleAndExplode(typed.Style, typed.Explode, location);

                    // Replace any path-level param with matching name+in.
                    int existingIndex = FindParameterIndex(result, typed.Name, location);
                    WalkedParameter walkedParam = new(
                        element, location, required, style, explode, hasSchema,
                        isPathLevel: false, sourceIndex: sourceIndex);

                    if (existingIndex >= 0)
                    {
                        result[existingIndex] = walkedParam;
                    }
                    else
                    {
                        result.Add(walkedParam);
                    }
                }

                sourceIndex++;
            }
        }

        return [.. result];
    }

    private static (JsonElement Element, OpenApiDocument.ParameterOrReference Typed)? ResolveParameter(
        OpenApiDocument.ParameterOrReference paramOrRef,
        IOpenApiReferenceResolver referenceResolver)
    {
        return paramOrRef.Match<IOpenApiReferenceResolver, (JsonElement, OpenApiDocument.ParameterOrReference)?>(
            in referenceResolver,
            static (in OpenApiDocument.Reference reference, in IOpenApiReferenceResolver resolver) =>
            {
                string? refString = reference.Ref.GetString();
                if (refString is not null
                    && resolver.TryResolve<OpenApiDocument.ParameterOrReference>(refString, out var resolved))
                {
                    return ((JsonElement)resolved, resolved);
                }

                return null;
            },
            static (in OpenApiDocument.Parameter parameter, in IOpenApiReferenceResolver _) =>
                ((JsonElement)parameter, (OpenApiDocument.ParameterOrReference)(JsonElement)parameter));
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

            OpenApiDocument.ParameterOrReference existing = parameters[i].Element;
            if (existing.Name.ValueKind == JsonValueKind.String
                && name.Equals(existing.Name))
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
        OpenApiDocument.RequestBodyOrReference requestBodyOrRef = operation.RequestBody;
        if (requestBodyOrRef.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        OpenApiDocument.RequestBodyOrReference? resolved = ResolveRequestBody(requestBodyOrRef, referenceResolver);
        if (resolved is not { } requestBody)
        {
            return null;
        }

        bool required = requestBody.Required.ValueKind == JsonValueKind.True;
        WalkedMediaTypeContent[] content = ExtractMediaTypeContent(requestBody.Content);

        return new WalkedRequestBody((JsonElement)requestBody, required, content);
    }

    private static OpenApiDocument.RequestBodyOrReference? ResolveRequestBody(
        OpenApiDocument.RequestBodyOrReference requestBodyOrRef,
        IOpenApiReferenceResolver referenceResolver)
    {
        return requestBodyOrRef.Match<IOpenApiReferenceResolver, OpenApiDocument.RequestBodyOrReference?>(
            in referenceResolver,
            static (in OpenApiDocument.Reference reference, in IOpenApiReferenceResolver resolver) =>
            {
                string? refString = reference.Ref.GetString();
                if (refString is not null
                    && resolver.TryResolve<OpenApiDocument.RequestBodyOrReference>(refString, out var resolved))
                {
                    return resolved;
                }

                return null;
            },
            static (in OpenApiDocument.RequestBody requestBody, in IOpenApiReferenceResolver _) =>
                (OpenApiDocument.RequestBodyOrReference)(JsonElement)requestBody);
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

            OpenApiDocument.ResponseOrReference responseOrRef = responseProp.Value;
            OpenApiDocument.ResponseOrReference? resolved = ResolveResponse(responseOrRef, referenceResolver);
            if (resolved is not { } response)
            {
                continue;
            }

            WalkedMediaTypeContent[] content = ExtractMediaTypeContent(response.Content);
            WalkedResponseHeader[] headers = ExtractResponseHeaders(response.Headers, referenceResolver);

            result.Add(new WalkedResponse(responseProp, content, headers));
        }

        return [.. result];
    }

    private static OpenApiDocument.ResponseOrReference? ResolveResponse(
        OpenApiDocument.ResponseOrReference responseOrRef,
        IOpenApiReferenceResolver referenceResolver)
    {
        return responseOrRef.Match<IOpenApiReferenceResolver, OpenApiDocument.ResponseOrReference?>(
            in referenceResolver,
            static (in OpenApiDocument.Reference reference, in IOpenApiReferenceResolver resolver) =>
            {
                string? refString = reference.Ref.GetString();
                if (refString is not null
                    && resolver.TryResolve<OpenApiDocument.ResponseOrReference>(refString, out var resolved))
                {
                    return resolved;
                }

                return null;
            },
            static (in OpenApiDocument.Response response, in IOpenApiReferenceResolver _) =>
                (OpenApiDocument.ResponseOrReference)(JsonElement)response);
    }

    private static WalkedResponseHeader[] ExtractResponseHeaders(
        JsonElement headersMap,
        IOpenApiReferenceResolver referenceResolver)
    {
        if (headersMap.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        List<WalkedResponseHeader> result = [];

        foreach (JsonProperty<JsonElement> headerProp in headersMap.EnumerateObject())
        {
            if (headerProp.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            OpenApiDocument.HeaderOrReference headerOrRef = headerProp.Value;
            OpenApiDocument.HeaderOrReference? resolved = ResolveHeader(headerOrRef, referenceResolver);
            if (resolved is not { } header)
            {
                continue;
            }

            bool hasSchema = header.Schema.ValueKind == JsonValueKind.Object;
            result.Add(new WalkedResponseHeader(headerProp, hasSchema));
        }

        return [.. result];
    }

    private static OpenApiDocument.HeaderOrReference? ResolveHeader(
        OpenApiDocument.HeaderOrReference headerOrRef,
        IOpenApiReferenceResolver referenceResolver)
    {
        return headerOrRef.Match<IOpenApiReferenceResolver, OpenApiDocument.HeaderOrReference?>(
            in referenceResolver,
            static (in OpenApiDocument.Reference reference, in IOpenApiReferenceResolver resolver) =>
            {
                string? refString = reference.Ref.GetString();
                if (refString is not null
                    && resolver.TryResolve<OpenApiDocument.HeaderOrReference>(refString, out var resolved))
                {
                    return resolved;
                }

                return null;
            },
            static (in OpenApiDocument.Header header, in IOpenApiReferenceResolver _) =>
                (OpenApiDocument.HeaderOrReference)(JsonElement)header);
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

        if (inValue.ValueEquals(OpenApiDocument.Parameter.InEntity.EnumValues.PathUtf8))
        {
            return ParameterLocation.Path;
        }

        if (inValue.ValueEquals(OpenApiDocument.Parameter.InEntity.EnumValues.HeaderUtf8))
        {
            return ParameterLocation.Header;
        }

        if (inValue.ValueEquals(OpenApiDocument.Parameter.InEntity.EnumValues.CookieUtf8))
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
    /// UTF-8 constants for the OpenAPI <c>style</c> property values.
    /// </summary>
    /// <remarks>
    /// The OpenAPI 3.1 schema does not expose style values as simple enum constants
    /// (they are nested in a complex <c>oneOf</c> structure), so we define them here.
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
            if (mediaType.SchemaValue.ValueKind == JsonValueKind.Object)
            {
                yield return new ExtractedSchema((JsonElement)mediaType.SchemaValue, role);
            }
        }
    }
}