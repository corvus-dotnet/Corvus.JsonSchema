// <copyright file="OpenApi31Walker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;
using PathStyles = Corvus.Text.Json.OpenApi31.OpenApiDocument.Parameter.SchemaEntity.StylesForPathEntity;
using QueryStyles = Corvus.Text.Json.OpenApi31.OpenApiDocument.Parameter.SchemaEntity.StylesForQueryEntity;

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
    /// <inheritdoc/>
    public IEnumerable<OperationEntry> EnumerateOperations(
        JsonElement specRoot,
        OperationFilter? filter = null,
        IOpenApiReferenceResolver? referenceResolver = null)
    {
        OpenApiDocument doc = specRoot;
        referenceResolver ??= new LocalReferenceResolver(specRoot);

        if (doc.PathsValue.IsUndefined())
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
            JsonProperty<JsonElement> path = pathProp.AsJsonElementProperty();

            foreach (OperationEntry entry in EnumeratePathItemOperations(
                path, pathItem, referenceResolver))
            {
                yield return entry;
            }
        }
    }

    private static IEnumerable<OperationEntry> EnumeratePathItemOperations(
        JsonProperty<JsonElement> path,
        OpenApiDocument.PathItem pathItem,
        IOpenApiReferenceResolver referenceResolver)
    {
        if (pathItem.Get.IsNotUndefined())
        {
            yield return CreateEntry(path, pathItem.Get, OperationMethod.Get, pathItem, referenceResolver);
        }

        if (pathItem.Put.IsNotUndefined())
        {
            yield return CreateEntry(path, pathItem.Put, OperationMethod.Put, pathItem, referenceResolver);
        }

        if (pathItem.Post.IsNotUndefined())
        {
            yield return CreateEntry(path, pathItem.Post, OperationMethod.Post, pathItem, referenceResolver);
        }

        if (pathItem.Delete.IsNotUndefined())
        {
            yield return CreateEntry(path, pathItem.Delete, OperationMethod.Delete, pathItem, referenceResolver);
        }

        if (pathItem.Options.IsNotUndefined())
        {
            yield return CreateEntry(path, pathItem.Options, OperationMethod.Options, pathItem, referenceResolver);
        }

        if (pathItem.Head.IsNotUndefined())
        {
            yield return CreateEntry(path, pathItem.Head, OperationMethod.Head, pathItem, referenceResolver);
        }

        if (pathItem.Patch.IsNotUndefined())
        {
            yield return CreateEntry(path, pathItem.Patch, OperationMethod.Patch, pathItem, referenceResolver);
        }

        if (pathItem.Trace.IsNotUndefined())
        {
            yield return CreateEntry(path, pathItem.Trace, OperationMethod.Trace, pathItem, referenceResolver);
        }
    }

    private static OperationEntry CreateEntry(
        JsonProperty<JsonElement> path,
        OpenApiDocument.Operation operation,
        OperationMethod method,
        OpenApiDocument.PathItem pathItem,
        IOpenApiReferenceResolver referenceResolver)
    {
        return new OperationEntry(
            path,
            (JsonElement)operation,
            method,
            ExtractParameters(operation, pathItem, referenceResolver),
            ExtractRequestBody(operation, referenceResolver),
            ExtractResponses(operation, referenceResolver));
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
            if (requestBodyOrRef.IsNotUndefined())
            {
                if (!TryResolveRequestBody(requestBodyOrRef, referenceResolver, out OpenApiDocument.RequestBody requestBody))
                {
                    throw new InvalidOperationException(
                        "Unable to resolve request body $ref.");
                }

                if (requestBody.ContentValue.IsNotUndefined())
                {
                    foreach (ExtractedSchema schema in EnumerateMediaTypeSchemas(
                        requestBody.ContentValue, SchemaRole.RequestBody))
                    {
                        yield return schema;
                    }
                }
            }

            // Response content schemas and headers
            if (operation.ResponsesValue.IsNotUndefined())
            {
                foreach (var responseProp in operation.ResponsesValue.EnumerateObject())
                {
                    if (responseProp.Value.IsUndefined())
                    {
                        continue;
                    }

                    if (!TryResolveResponse(responseProp.Value, referenceResolver, out OpenApiDocument.Response response))
                    {
                        throw new InvalidOperationException(
                            "Unable to resolve response $ref.");
                    }

                    if (response.ContentValue.IsNotUndefined())
                    {
                        foreach (ExtractedSchema schema in EnumerateMediaTypeSchemas(
                            response.ContentValue, SchemaRole.ResponseBody))
                        {
                            yield return schema;
                        }
                    }

                    // Response header schemas
                    if (response.Headers.IsNotUndefined())
                    {
                        foreach (var headerProp in response.Headers.EnumerateObject())
                        {
                            if (headerProp.Value.IsUndefined())
                            {
                                continue;
                            }

                            if (!TryResolveHeader(headerProp.Value, referenceResolver, out OpenApiDocument.Header header))
                            {
                                throw new InvalidOperationException(
                                    "Unable to resolve header $ref.");
                            }

                            if (header.SchemaValue.IsNotUndefined())
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
                if (parameter.SchemaValue.IsNotUndefined())
                {
                    yield return new ExtractedSchema(
                        (JsonElement)parameter.SchemaValue, SchemaRole.Parameter);
                }
            }
        }

        // Component schemas
        if (doc.ComponentsValue.IsNotUndefined())
        {
            OpenApiDocument.Components components = doc.ComponentsValue;
            if (components.Schemas.IsNotUndefined())
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
        bool hasOperationParams = operation.Parameters.IsNotUndefined();
        bool hasPathParams = pathItem.Parameters.IsNotUndefined();

        if (!hasOperationParams && !hasPathParams)
        {
            return [];
        }

        List<WalkedParameter> result = [];

        // Start with path-level parameters.
        if (hasPathParams)
        {
            int sourceIndex = 0;
            foreach (OpenApiDocument.ParameterOrReference paramOrRef in pathItem.Parameters.EnumerateArray())
            {
                if (paramOrRef.IsUndefined())
                {
                    sourceIndex++;
                    continue;
                }

                if (!TryResolveParameter(paramOrRef, referenceResolver, out OpenApiDocument.Parameter typed))
                {
                    throw new InvalidOperationException(
                        $"Unable to resolve parameter $ref at index {sourceIndex} in path-level parameters.");
                }

                (ParameterLocation location, ParameterStyle style, bool explode) = ParseParameterTraits(typed);
                bool required = typed.Required.ValueKind == JsonValueKind.True;
                bool hasSchema = typed.SchemaValue.IsNotUndefined();

                result.Add(new WalkedParameter(
                    (JsonElement)typed, location, required, style, explode, hasSchema,
                    isPathLevel: true, sourceIndex: sourceIndex));

                sourceIndex++;
            }
        }

        // Add operation-level parameters, replacing any path-level parameter
        // that shares the same name+in combination (operation wins per OpenAPI spec).
        if (hasOperationParams)
        {
            int sourceIndex = 0;
            foreach (OpenApiDocument.ParameterOrReference paramOrRef in operation.Parameters.EnumerateArray())
            {
                if (paramOrRef.IsUndefined())
                {
                    sourceIndex++;
                    continue;
                }

                if (!TryResolveParameter(paramOrRef, referenceResolver, out OpenApiDocument.Parameter typed))
                {
                    throw new InvalidOperationException(
                        $"Unable to resolve parameter $ref at index {sourceIndex} in operation-level parameters.");
                }

                (ParameterLocation location, ParameterStyle style, bool explode) = ParseParameterTraits(typed);
                bool required = typed.Required.ValueKind == JsonValueKind.True;
                bool hasSchema = typed.SchemaValue.IsNotUndefined();

                // Replace any path-level param with matching name+in.
                int existingIndex = FindParameterIndex(result, typed.Name, location);
                WalkedParameter walkedParam = new(
                    (JsonElement)typed, location, required, style, explode, hasSchema,
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

    private static bool TryResolveParameter(
        OpenApiDocument.ParameterOrReference paramOrRef,
        IOpenApiReferenceResolver referenceResolver,
        out OpenApiDocument.Parameter resolved)
    {
        OpenApiDocument.Parameter? result = paramOrRef.Match<IOpenApiReferenceResolver, OpenApiDocument.Parameter?>(
            in referenceResolver,
            static (in OpenApiDocument.Reference reference, in IOpenApiReferenceResolver solver) =>
            {
                using UnescapedUtf8JsonString refUtf8 = reference.Ref.GetUtf8String();
                if (solver.TryResolve<OpenApiDocument.Parameter>(refUtf8.Span, out var r))
                {
                    return r;
                }

                return null;
            },
            static (in OpenApiDocument.Parameter parameter, in IOpenApiReferenceResolver _) =>
                parameter);

        if (result is { } r2)
        {
            resolved = r2;
            return true;
        }

        resolved = default;
        return false;
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
        if (requestBodyOrRef.IsUndefined())
        {
            return null;
        }

        if (!TryResolveRequestBody(requestBodyOrRef, referenceResolver, out OpenApiDocument.RequestBody requestBody))
        {
            throw new InvalidOperationException(
                "Unable to resolve request body $ref.");
        }

        bool required = requestBody.Required.ValueKind == JsonValueKind.True;
        WalkedMediaTypeContent[] content = ExtractMediaTypeContent(requestBody.ContentValue);

        return new WalkedRequestBody((JsonElement)requestBody, required, content);
    }

    private static bool TryResolveRequestBody(
        OpenApiDocument.RequestBodyOrReference requestBodyOrRef,
        IOpenApiReferenceResolver referenceResolver,
        out OpenApiDocument.RequestBody resolved)
    {
        OpenApiDocument.RequestBody? result = requestBodyOrRef.Match<IOpenApiReferenceResolver, OpenApiDocument.RequestBody?>(
            in referenceResolver,
            static (in OpenApiDocument.Reference reference, in IOpenApiReferenceResolver solver) =>
            {
                using UnescapedUtf8JsonString refUtf8 = reference.Ref.GetUtf8String();
                if (solver.TryResolve<OpenApiDocument.RequestBody>(refUtf8.Span, out var r))
                {
                    return r;
                }

                return null;
            },
            static (in OpenApiDocument.RequestBody requestBody, in IOpenApiReferenceResolver _) =>
                requestBody);

        if (result is { } r2)
        {
            resolved = r2;
            return true;
        }

        resolved = default;
        return false;
    }

    private static WalkedResponse[] ExtractResponses(
        OpenApiDocument.Operation operation,
        IOpenApiReferenceResolver referenceResolver)
    {
        if (operation.ResponsesValue.IsUndefined())
        {
            return [];
        }

        List<WalkedResponse> result = [];

        foreach (var responseProp in operation.ResponsesValue.EnumerateObject())
        {
            if (responseProp.Value.IsUndefined())
            {
                continue;
            }

            if (!TryResolveResponse(responseProp.Value, referenceResolver, out OpenApiDocument.Response response))
            {
                throw new InvalidOperationException(
                    "Unable to resolve response $ref.");
            }

            WalkedMediaTypeContent[] content = ExtractMediaTypeContent(response.ContentValue);
            WalkedResponseHeader[] headers = ExtractResponseHeaders(response.Headers, referenceResolver);

            result.Add(new WalkedResponse(responseProp.AsJsonElementProperty(), content, headers));
        }

        return [.. result];
    }

    private static bool TryResolveResponse(
        OpenApiDocument.ResponseOrReference responseOrRef,
        IOpenApiReferenceResolver referenceResolver,
        out OpenApiDocument.Response resolved)
    {
        OpenApiDocument.Response? result = responseOrRef.Match<IOpenApiReferenceResolver, OpenApiDocument.Response?>(
            in referenceResolver,
            static (in OpenApiDocument.Reference reference, in IOpenApiReferenceResolver solver) =>
            {
                using UnescapedUtf8JsonString refUtf8 = reference.Ref.GetUtf8String();
                if (solver.TryResolve<OpenApiDocument.Response>(refUtf8.Span, out var r))
                {
                    return r;
                }

                return null;
            },
            static (in OpenApiDocument.Response response, in IOpenApiReferenceResolver _) =>
                response);

        if (result is { } r2)
        {
            resolved = r2;
            return true;
        }

        resolved = default;
        return false;
    }

    private static WalkedResponseHeader[] ExtractResponseHeaders(
        OpenApiDocument.Response.HeadersEntity headersMap,
        IOpenApiReferenceResolver referenceResolver)
    {
        if (headersMap.IsUndefined())
        {
            return [];
        }

        List<WalkedResponseHeader> result = [];

        foreach (var headerProp in headersMap.EnumerateObject())
        {
            if (headerProp.Value.IsUndefined())
            {
                continue;
            }

            OpenApiDocument.HeaderOrReference headerOrRef = headerProp.Value;

            if (!TryResolveHeader(headerOrRef, referenceResolver, out OpenApiDocument.Header header))
            {
                throw new InvalidOperationException(
                    "Unable to resolve header $ref.");
            }

            bool hasSchema = header.SchemaValue.IsNotUndefined();
            result.Add(new WalkedResponseHeader(headerProp.AsJsonElementProperty(), hasSchema));
        }

        return [.. result];
    }

    private static bool TryResolveHeader(
        OpenApiDocument.HeaderOrReference headerOrRef,
        IOpenApiReferenceResolver referenceResolver,
        out OpenApiDocument.Header resolved)
    {
        OpenApiDocument.Header? result = headerOrRef.Match<IOpenApiReferenceResolver, OpenApiDocument.Header?>(
            in referenceResolver,
            static (in OpenApiDocument.Reference reference, in IOpenApiReferenceResolver solver) =>
            {
                using UnescapedUtf8JsonString refUtf8 = reference.Ref.GetUtf8String();
                if (solver.TryResolve<OpenApiDocument.Header>(refUtf8.Span, out var r))
                {
                    return r;
                }

                return null;
            },
            static (in OpenApiDocument.Header header, in IOpenApiReferenceResolver _) =>
                header);

        if (result is { } r2)
        {
            resolved = r2;
            return true;
        }

        resolved = default;
        return false;
    }

    private static WalkedMediaTypeContent[] ExtractMediaTypeContent(OpenApiDocument.Content contentMap)
    {
        if (contentMap.IsUndefined())
        {
            return [];
        }

        List<WalkedMediaTypeContent> result = [];

        foreach (var mediaTypeProp in contentMap.EnumerateObject())
        {
            OpenApiDocument.MediaType mediaType = mediaTypeProp.Value;
            bool hasSchema = mediaType.SchemaValue.IsNotUndefined();
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

    private static (ParameterLocation Location, ParameterStyle Style, bool Explode) ParseParameterTraits(
        OpenApiDocument.Parameter typed)
    {
        ParameterLocation location = ParseLocation(typed.In);

        ParameterStyle style = location switch
        {
            ParameterLocation.Header => ParameterStyle.Simple,
            ParameterLocation.Cookie => ParameterStyle.Form,
            ParameterLocation.Path => ParsePathStyle((JsonElement)typed),
            ParameterLocation.Query => ParseQueryStyle((JsonElement)typed),
            _ => ParameterStyle.Form,
        };

        bool explode = typed.Explode.IsNotUndefined() ? (bool)typed.Explode : style == ParameterStyle.Form;
        return (location, style, explode);
    }

    private static ParameterStyle ParsePathStyle(PathStyles pathStyles)
    {
        return pathStyles.Match(
            static (in PathStyles.RequiredRequired rr) =>
                rr.Style.Match(
                    static () => ParameterStyle.Matrix,
                    static () => ParameterStyle.Label,
                    static () => ParameterStyle.Simple,
                    static () => ParameterStyle.Simple),
            static (in PathStyles _) =>
                ParameterStyle.Simple);
    }

    private static ParameterStyle ParseQueryStyle(QueryStyles queryStyles)
    {
        return queryStyles.Match(
            static (in QueryStyles.ThenEntity then) =>
                then.Style.Match(
                    static () => ParameterStyle.Form,
                    static () => ParameterStyle.SpaceDelimited,
                    static () => ParameterStyle.PipeDelimited,
                    static () => ParameterStyle.DeepObject,
                    static () => ParameterStyle.Form),
            static (in QueryStyles _) =>
                ParameterStyle.Form);
    }

    private static IEnumerable<ExtractedSchema> EnumerateMediaTypeSchemas(
        OpenApiDocument.Content contentMap, SchemaRole role)
    {
        foreach (var mediaTypeProp in contentMap.EnumerateObject())
        {
            if (mediaTypeProp.Value.SchemaValue.IsNotUndefined())
            {
                yield return new ExtractedSchema((JsonElement)mediaTypeProp.Value.SchemaValue, role);
            }
        }
    }
}