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

        foreach (var pathProp in doc.PathsValue.EnumerateObject())
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
                        pathProp.AsJsonElementProperty(),
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

        foreach (var entry in this.EnumerateOperations(specRoot, filter, referenceResolver))
        {
            OpenApiDocument.Operation operation = entry.Operation;

            // Request body content schemas
            OpenApiDocument.RequestBodyOrReference requestBodyOrRef = operation.RequestBody;
            if (requestBodyOrRef.ValueKind == JsonValueKind.Object)
            {
                OpenApiDocument.RequestBody? resolvedBody = ResolveRequestBody(requestBodyOrRef, referenceResolver);
                if (resolvedBody is { } requestBody
                    && requestBody.ContentValue.ValueKind == JsonValueKind.Object)
                {
                    foreach (ExtractedSchema schema in EnumerateMediaTypeSchemas(
                        requestBody.ContentValue, SchemaRole.RequestBody))
                    {
                        yield return schema;
                    }
                }
            }

            // Response content schemas and headers
            if (operation.ResponsesValue.ValueKind == JsonValueKind.Object)
            {
                foreach (var responseProp in operation.ResponsesValue.EnumerateObject())
                {
                    if (responseProp.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    OpenApiDocument.Response? resolvedResponse = ResolveResponse(responseProp.Value, referenceResolver);
                    if (resolvedResponse is not { } response)
                    {
                        continue;
                    }

                    if (response.ContentValue.ValueKind == JsonValueKind.Object)
                    {
                        foreach (ExtractedSchema schema in EnumerateMediaTypeSchemas(
                            response.ContentValue, SchemaRole.ResponseBody))
                        {
                            yield return schema;
                        }
                    }

                    // Response header schemas
                    if (response.Headers.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var headerProp in response.Headers.EnumerateObject())
                        {
                            if (headerProp.Value.ValueKind != JsonValueKind.Object)
                            {
                                continue;
                            }

                            OpenApiDocument.Header? resolvedHeader = ResolveHeader(headerProp.Value, referenceResolver);
                            if (resolvedHeader is { } header
                                && header.SchemaValue.ValueKind == JsonValueKind.Object)
                            {
                                yield return new ExtractedSchema(
                                    (JsonElement)header.SchemaValue, SchemaRole.Header);
                            }
                        }
                    }
                }
            }

            // Parameter schemas (merged from path-level and operation-level, already resolved)
            foreach (WalkedParameter walkedParam in entry.Parameters)
            {
                OpenApiDocument.Parameter parameter = walkedParam.Element;
                if (parameter.SchemaValue.ValueKind == JsonValueKind.Object)
                {
                    yield return new ExtractedSchema(
                        (JsonElement)parameter.SchemaValue, SchemaRole.Parameter);
                }
            }
        }

        // Component schemas
        if (doc.ComponentsValue.ValueKind == JsonValueKind.Object)
        {
            OpenApiDocument.Components components = doc.ComponentsValue;
            if (components.Schemas.ValueKind == JsonValueKind.Object)
            {
                foreach (var schemaProp in components.Schemas.EnumerateObject())
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
            foreach (JsonElement param in pathItem.Parameters.EnumerateArray())
            {
                if (param.ValueKind != JsonValueKind.Object)
                {
                    sourceIndex++;
                    continue;
                }

                OpenApiDocument.ParameterOrReference paramOrRef = param;
                (JsonElement element, OpenApiDocument.Parameter typed)? resolved =
                    ResolveParameter(paramOrRef, referenceResolver);

                if (resolved is not null)
                {
                    var (element, typed) = resolved.Value;
                    ParameterLocation location = ParseLocation(typed.In);
                    bool required = typed.Required.ValueKind == JsonValueKind.True;
                    bool hasSchema = typed.SchemaValue.ValueKind == JsonValueKind.Object;

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
            foreach (JsonElement param in operation.Parameters.EnumerateArray())
            {
                if (param.ValueKind != JsonValueKind.Object)
                {
                    sourceIndex++;
                    continue;
                }

                OpenApiDocument.ParameterOrReference paramOrRef = param;
                (JsonElement element, OpenApiDocument.Parameter typed)? resolved =
                    ResolveParameter(paramOrRef, referenceResolver);

                if (resolved is not null)
                {
                    var (element, typed) = resolved.Value;
                    ParameterLocation location = ParseLocation(typed.In);
                    bool required = typed.Required.ValueKind == JsonValueKind.True;
                    bool hasSchema = typed.SchemaValue.ValueKind == JsonValueKind.Object;

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

    private static (JsonElement Element, OpenApiDocument.Parameter Typed)? ResolveParameter(
        OpenApiDocument.ParameterOrReference paramOrRef,
        IOpenApiReferenceResolver referenceResolver)
    {
        return paramOrRef.Match<IOpenApiReferenceResolver, (JsonElement, OpenApiDocument.Parameter)?>(
            in referenceResolver,
            static (in OpenApiDocument.Reference reference, in IOpenApiReferenceResolver resolver) =>
            {
                if (reference.Ref.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                using UnescapedUtf8JsonString refUtf8 = reference.Ref.GetUtf8String();
                if (resolver.TryResolve<OpenApiDocument.Parameter>(refUtf8.Span, out var resolved))
                {
                    return ((JsonElement)resolved, resolved);
                }

                return null;
            },
            static (in OpenApiDocument.Parameter parameter, in IOpenApiReferenceResolver _) =>
                ((JsonElement)parameter, parameter));
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

        OpenApiDocument.RequestBody? resolved = ResolveRequestBody(requestBodyOrRef, referenceResolver);
        if (resolved is not { } requestBody)
        {
            return null;
        }

        bool required = requestBody.Required.ValueKind == JsonValueKind.True;
        WalkedMediaTypeContent[] content = ExtractMediaTypeContent(requestBody.ContentValue);

        return new WalkedRequestBody((JsonElement)requestBody, required, content);
    }

    private static OpenApiDocument.RequestBody? ResolveRequestBody(
        OpenApiDocument.RequestBodyOrReference requestBodyOrRef,
        IOpenApiReferenceResolver referenceResolver)
    {
        return requestBodyOrRef.Match<IOpenApiReferenceResolver, OpenApiDocument.RequestBody?>(
            in referenceResolver,
            static (in OpenApiDocument.Reference reference, in IOpenApiReferenceResolver resolver) =>
            {
                if (reference.Ref.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                using UnescapedUtf8JsonString refUtf8 = reference.Ref.GetUtf8String();
                if (resolver.TryResolve<OpenApiDocument.RequestBody>(refUtf8.Span, out var resolved))
                {
                    return resolved;
                }

                return null;
            },
            static (in OpenApiDocument.RequestBody requestBody, in IOpenApiReferenceResolver _) =>
                requestBody);
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

        foreach (var responseProp in operation.ResponsesValue.EnumerateObject())
        {
            if (responseProp.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            OpenApiDocument.Response? resolved = ResolveResponse(responseProp.Value, referenceResolver);
            if (resolved is not { } response)
            {
                continue;
            }

            WalkedMediaTypeContent[] content = ExtractMediaTypeContent(response.ContentValue);
            WalkedResponseHeader[] headers = ExtractResponseHeaders(response.Headers, referenceResolver);

            result.Add(new WalkedResponse(responseProp.AsJsonElementProperty(), content, headers));
        }

        return [.. result];
    }

    private static OpenApiDocument.Response? ResolveResponse(
        OpenApiDocument.ResponseOrReference responseOrRef,
        IOpenApiReferenceResolver referenceResolver)
    {
        return responseOrRef.Match<IOpenApiReferenceResolver, OpenApiDocument.Response?>(
            in referenceResolver,
            static (in OpenApiDocument.Reference reference, in IOpenApiReferenceResolver resolver) =>
            {
                if (reference.Ref.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                using UnescapedUtf8JsonString refUtf8 = reference.Ref.GetUtf8String();
                if (resolver.TryResolve<OpenApiDocument.Response>(refUtf8.Span, out var resolved))
                {
                    return resolved;
                }

                return null;
            },
            static (in OpenApiDocument.Response response, in IOpenApiReferenceResolver _) =>
                response);
    }

    private static WalkedResponseHeader[] ExtractResponseHeaders(
        OpenApiDocument.Response.HeadersEntity headersMap,
        IOpenApiReferenceResolver referenceResolver)
    {
        if (headersMap.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        List<WalkedResponseHeader> result = [];

        foreach (var headerProp in headersMap.EnumerateObject())
        {
            if (headerProp.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            OpenApiDocument.HeaderOrReference headerOrRef = headerProp.Value;
            OpenApiDocument.Header? resolved = ResolveHeader(headerOrRef, referenceResolver);
            if (resolved is not { } header)
            {
                continue;
            }

            bool hasSchema = header.SchemaValue.ValueKind == JsonValueKind.Object;
            result.Add(new WalkedResponseHeader(headerProp.AsJsonElementProperty(), hasSchema));
        }

        return [.. result];
    }

    private static OpenApiDocument.Header? ResolveHeader(
        OpenApiDocument.HeaderOrReference headerOrRef,
        IOpenApiReferenceResolver referenceResolver)
    {
        return headerOrRef.Match<IOpenApiReferenceResolver, OpenApiDocument.Header?>(
            in referenceResolver,
            static (in OpenApiDocument.Reference reference, in IOpenApiReferenceResolver resolver) =>
            {
                if (reference.Ref.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                using UnescapedUtf8JsonString refUtf8 = reference.Ref.GetUtf8String();
                if (resolver.TryResolve<OpenApiDocument.Header>(refUtf8.Span, out var resolved))
                {
                    return resolved;
                }

                return null;
            },
            static (in OpenApiDocument.Header header, in IOpenApiReferenceResolver _) =>
                header);
    }

    private static WalkedMediaTypeContent[] ExtractMediaTypeContent(OpenApiDocument.Content contentMap)
    {
        if (contentMap.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        List<WalkedMediaTypeContent> result = [];

        foreach (var mediaTypeProp in contentMap.EnumerateObject())
        {
            OpenApiDocument.MediaType mediaType = mediaTypeProp.Value;
            bool hasSchema = mediaType.SchemaValue.ValueKind == JsonValueKind.Object;
            result.Add(new WalkedMediaTypeContent(mediaTypeProp.AsJsonElementProperty(), hasSchema));
        }

        return [.. result];
    }

    private static ParameterLocation ParseLocation(OpenApiDocument.Parameter.InEntity inValue)
    {
        return inValue.Match(
            static () => ParameterLocation.Query,
            static () => ParameterLocation.Header,
            static () => ParameterLocation.Path,
            static () => ParameterLocation.Cookie,
            static () => ParameterLocation.Query);
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
        OpenApiDocument.Content contentMap, SchemaRole role)
    {
        foreach (var mediaTypeProp in contentMap.EnumerateObject())
        {
            if (mediaTypeProp.Value.SchemaValue.ValueKind == JsonValueKind.Object)
            {
                yield return new ExtractedSchema((JsonElement)mediaTypeProp.Value.SchemaValue, role);
            }
        }
    }
}