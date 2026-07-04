// <copyright file="OpenApi32Walker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using EncodingInfo = Corvus.Text.Json.OpenApi.CodeGeneration.EncodingInfo;

namespace Corvus.Text.Json.OpenApi32;

/// <summary>
/// Walks the strongly-typed OpenAPI 3.2 <see cref="OpenApiDocument"/> model into the shared,
/// language-neutral intermediate representation consumed by <see cref="OpenApi32CodeGenerator"/>.
/// </summary>
internal sealed class OpenApi32Walker : OpenApiWalkerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApi32Walker"/> class.
    /// </summary>
    /// <param name="clientNamePrefix">Optional prefix for client type names.</param>
    /// <param name="ignoreEmptyFormUrlEncodedBody">
    /// When <see langword="true"/>, form-urlencoded request bodies whose schema defines no
    /// properties are treated as if the body were absent.
    /// </param>
    public OpenApi32Walker(string? clientNamePrefix, bool ignoreEmptyFormUrlEncodedBody)
        : base(clientNamePrefix, ignoreEmptyFormUrlEncodedBody)
    {
    }

    /// <inheritdoc/>
    protected override IReadOnlyList<OperationInfo> WalkOperations(
        JsonElement specRoot,
        OperationFilter? filter,
        IOpenApiReferenceResolver referenceResolver)
    {
        List<OperationInfo> operations = [];
        ServerInfo? rootServer = GetDefaultServerInfo(specRoot);

        foreach (OperationRef opRef in WalkOperationRefs(specRoot, filter, referenceResolver))
        {
            operations.Add(this.PrepareOperation(opRef, referenceResolver, rootServer, specRoot));
        }

        return operations;
    }

    // ── Walk-phase reference (typed model objects, no strings extracted) ──
    internal readonly record struct OperationRef(
        JsonProperty<OpenApiDocument.PathItem> PathProp,
        OpenApiDocument.Operation Operation,
        OperationMethod Method,
        OpenApiDocument.PathItem PathItem,
        string? CustomMethodName = null);

    // ═══════════════════════════════════════════════════════════════════
    internal static void CollectPathItemPointers(
        JsonProperty<OpenApiDocument.PathItem> pathProp,
        List<SchemaReference> pointers,
        Dictionary<string, string> parameterNames,
        IOpenApiReferenceResolver referenceResolver,
        ReadOnlySpan<byte> rootSegmentUtf8,
        string? callbackPathItemRef = null)
    {
        OpenApiDocument.PathItem pathItem = pathProp.Value;

        // Determine if this path item is a $ref and compute its absolute reference
        string? pathItemRefValue = callbackPathItemRef;
        if (pathItemRefValue is null)
        {
            OpenApiDocument.Reference asRef = OpenApiDocument.Reference.From(pathItem);
            if (asRef.Ref.IsNotUndefined())
            {
                string refStr = asRef.Ref.GetString()!;
                pathItemRefValue = referenceResolver.ResolveToAbsolute(refStr);
            }
        }

        if (!TryResolvePathItem(pathItem, referenceResolver, out OpenApiDocument.PathItem resolved, out IDisposable pathItemScope))
        {
            return;
        }

        using (pathItemScope)
        {
            if (resolved.Get.IsNotUndefined())
            {
                CollectOperationPointers(pathProp, resolved.Get, OperationMethod.Get, resolved, pointers, parameterNames, referenceResolver, rootSegmentUtf8, pathItemRefValue);
            }

            if (resolved.Put.IsNotUndefined())
            {
                CollectOperationPointers(pathProp, resolved.Put, OperationMethod.Put, resolved, pointers, parameterNames, referenceResolver, rootSegmentUtf8, pathItemRefValue);
            }

            if (resolved.Post.IsNotUndefined())
            {
                CollectOperationPointers(pathProp, resolved.Post, OperationMethod.Post, resolved, pointers, parameterNames, referenceResolver, rootSegmentUtf8, pathItemRefValue);
            }

            if (resolved.Delete.IsNotUndefined())
            {
                CollectOperationPointers(pathProp, resolved.Delete, OperationMethod.Delete, resolved, pointers, parameterNames, referenceResolver, rootSegmentUtf8, pathItemRefValue);
            }

            if (resolved.Options.IsNotUndefined())
            {
                CollectOperationPointers(pathProp, resolved.Options, OperationMethod.Options, resolved, pointers, parameterNames, referenceResolver, rootSegmentUtf8, pathItemRefValue);
            }

            if (resolved.Head.IsNotUndefined())
            {
                CollectOperationPointers(pathProp, resolved.Head, OperationMethod.Head, resolved, pointers, parameterNames, referenceResolver, rootSegmentUtf8, pathItemRefValue);
            }

            if (resolved.Patch.IsNotUndefined())
            {
                CollectOperationPointers(pathProp, resolved.Patch, OperationMethod.Patch, resolved, pointers, parameterNames, referenceResolver, rootSegmentUtf8, pathItemRefValue);
            }

            if (resolved.Trace.IsNotUndefined())
            {
                CollectOperationPointers(pathProp, resolved.Trace, OperationMethod.Trace, resolved, pointers, parameterNames, referenceResolver, rootSegmentUtf8, pathItemRefValue);
            }

            if (resolved.Query.IsNotUndefined())
            {
                CollectOperationPointers(pathProp, resolved.Query, OperationMethod.Query, resolved, pointers, parameterNames, referenceResolver, rootSegmentUtf8, pathItemRefValue);
            }

            // Walk additionalOperations — custom HTTP methods
            if (resolved.AdditionalOperations.IsNotUndefined())
            {
                foreach (var additionalOp in resolved.AdditionalOperations.EnumerateObject())
                {
                    if (additionalOp.Value.IsNotUndefined())
                    {
                        CollectAdditionalOperationPointers(pathProp, additionalOp.Value, additionalOp.Name, resolved, pointers, parameterNames, referenceResolver, rootSegmentUtf8, pathItemRefValue);
                    }
                }
            }
        }
    }

    private static void CollectOperationPointers(
        JsonProperty<OpenApiDocument.PathItem> pathProp,
        OpenApiDocument.Operation operation,
        OperationMethod method,
        OpenApiDocument.PathItem pathItem,
        List<SchemaReference> pointers,
        Dictionary<string, string> parameterNames,
        IOpenApiReferenceResolver referenceResolver,
        ReadOnlySpan<byte> rootSegmentUtf8,
        string? pathItemRefValue = null)
    {
        using UnescapedUtf8JsonString pathName = pathProp.Utf8NameSpan;

        // Parameter schema pointers (with path/operation merge)
        foreach ((OpenApiDocument.Parameter param, int sourceIndex, bool isPathLevel, string? refValue) in
            MergeParameters(operation, pathItem, referenceResolver))
        {
            if (param.SchemaValue.IsNotUndefined())
            {
                string positionalPointer = SchemaPointerBuilder.BuildParameterSchemaPointer(
                    rootSegmentUtf8, pathName.Span, method, sourceIndex, isPathLevel);

                string resolvablePointer = refValue is not null
                    ? SchemaPointerBuilder.BuildRefBasedPointer(refValue, "/schema")
                    : pathItemRefValue is not null
                        ? SchemaPointerBuilder.BuildRefBasedPointer(pathItemRefValue, SchemaPointerBuilder.BuildParameterSubPath(method, sourceIndex, isPathLevel))
                        : positionalPointer;

                pointers.Add(new SchemaReference(positionalPointer, resolvablePointer));

                // Record the parameter name keyed by fragment (pointer without '#')
                // so the heuristic can look it up from reference.Fragment without allocating.
                if (param.Name.IsNotUndefined())
                {
                    string? name = param.Name.GetString();
                    if (name is not null)
                    {
                        parameterNames[positionalPointer.AsSpan(1).ToString()] = name;
                    }
                }
            }
            else if (param.Content.IsNotUndefined())
            {
                // Querystring parameters use 'content' instead of 'schema'.
                foreach (var mediaTypeProp in param.Content.EnumerateObject())
                {
                    using UnescapedUtf8JsonString mediaTypeName = mediaTypeProp.Utf8NameSpan;

                    string positionalPointer = SchemaPointerBuilder.BuildParameterContentSchemaPointer(
                        rootSegmentUtf8, pathName.Span, method, sourceIndex, isPathLevel, mediaTypeName.Span);

                    string resolvablePointer = refValue is not null
                        ? SchemaPointerBuilder.BuildRefBasedPointer(
                            refValue,
                            SchemaPointerBuilder.BuildContentSubPath(mediaTypeName.Span))
                        : positionalPointer;

                    pointers.Add(new SchemaReference(positionalPointer, resolvablePointer));

                    if (param.Name.IsNotUndefined())
                    {
                        string? name = param.Name.GetString();
                        if (name is not null)
                        {
                            parameterNames[positionalPointer.AsSpan(1).ToString()] = name;
                        }
                    }

                    break; // Only take the first content entry
                }
            }
        }

        // Request body content schema pointers
        if (operation.RequestBody.IsNotUndefined()
            && TryResolveRequestBody(operation.RequestBody, referenceResolver, out OpenApiDocument.RequestBody requestBody, out IDisposable rbScope, out string? rbRefValue)
            && requestBody.ContentValue.IsNotUndefined())
        {
            using (rbScope)
            {
                foreach (var mediaTypeProp in requestBody.ContentValue.EnumerateObject())
                {
                    if (mediaTypeProp.Value.Schema.IsNotUndefined())
                    {
                        using UnescapedUtf8JsonString mediaTypeName = mediaTypeProp.Utf8NameSpan;

                        // Skip raw stream content types — no JSON Schema type needed.
                        if (CodeEmitHelpers.IsRawStreamMediaType(mediaTypeProp.Name))
                        {
                            continue;
                        }

                        string positionalPointer = SchemaPointerBuilder.BuildContentSchemaPointer(
                            rootSegmentUtf8, pathName.Span, method, "/requestBody"u8, mediaTypeName.Span);

                        string resolvablePointer = rbRefValue is not null
                            ? SchemaPointerBuilder.BuildRefBasedPointer(
                                rbRefValue, SchemaPointerBuilder.BuildContentSubPath(mediaTypeName.Span))
                            : pathItemRefValue is not null
                                ? SchemaPointerBuilder.BuildRefBasedPointer(
                                    pathItemRefValue, SchemaPointerBuilder.BuildRequestBodyContentSubPath(method, mediaTypeName.Span))
                                : positionalPointer;

                        pointers.Add(new SchemaReference(positionalPointer, resolvablePointer));
                    }

                    // Collect prefixItems and items schema pointers for multipart/mixed.
                    if (CodeEmitHelpers.IsMultipartMixedMediaType(mediaTypeProp.Name))
                    {
                        OpenApiDocument.MediaType mediaType = OpenApiDocument.MediaType.From(mediaTypeProp.Value);

                        if (mediaType.PrefixEncoding.IsNotUndefined())
                        {
                            using UnescapedUtf8JsonString mtName = mediaTypeProp.Utf8NameSpan;
                            int prefixIndex = 0;
                            foreach (OpenApiDocument.Encoding encoding in mediaType.PrefixEncoding.EnumerateArray())
                            {
                                string contentType = encoding.ContentType.IsNotUndefined()
                                    ? encoding.ContentType.GetString()!
                                    : "application/json";

                                if (!CodeEmitHelpers.IsRawStreamMediaType(contentType))
                                {
                                    string pointer = BuildPrefixItemSchemaPointer(
                                        rootSegmentUtf8, pathName.Span, method, mtName.Span, prefixIndex, null);
                                    pointers.Add(new SchemaReference(pointer, pointer));
                                }

                                prefixIndex++;
                            }
                        }
                        else if (mediaType.ItemEncoding.IsNotUndefined())
                        {
                            string contentType = mediaType.ItemEncoding.ContentType.IsNotUndefined()
                                ? mediaType.ItemEncoding.ContentType.GetString()!
                                : "application/json";

                            if (!CodeEmitHelpers.IsRawStreamMediaType(contentType))
                            {
                                using UnescapedUtf8JsonString mtName = mediaTypeProp.Utf8NameSpan;
                                string pointer = BuildItemsSchemaPointer(
                                    rootSegmentUtf8, pathName.Span, method, mtName.Span, null);
                                pointers.Add(new SchemaReference(pointer, pointer));
                            }
                        }
                    }
                }
            }
        }

        // Response content and header schema pointers
        if (operation.ResponsesValue.IsNotUndefined())
        {
            foreach (var responseProp in operation.ResponsesValue.EnumerateObject())
            {
                if (responseProp.Value.IsUndefined())
                {
                    continue;
                }

                if (!TryResolveResponse(responseProp.Value, referenceResolver, out OpenApiDocument.Response response, out IDisposable responseScope, out string? responseRefValue))
                {
                    continue;
                }

                using (responseScope)
                {
                    using UnescapedUtf8JsonString statusCode = responseProp.Utf8NameSpan;

                    if (response.ContentValue.IsNotUndefined())
                    {
                        foreach (var mediaTypeProp in response.ContentValue.EnumerateObject())
                        {
                            if (mediaTypeProp.Value.Schema.IsNotUndefined()
                                && !CodeEmitHelpers.IsRawStreamMediaType(mediaTypeProp.Name))
                            {
                                using UnescapedUtf8JsonString mediaTypeName = mediaTypeProp.Utf8NameSpan;

                                string positionalPointer = SchemaPointerBuilder.BuildResponseContentSchemaPointer(
                                    rootSegmentUtf8, pathName.Span, method, statusCode.Span, mediaTypeName.Span);

                                string resolvablePointer = responseRefValue is not null
                                    ? SchemaPointerBuilder.BuildRefBasedPointer(
                                        responseRefValue, SchemaPointerBuilder.BuildContentSubPath(mediaTypeName.Span))
                                    : pathItemRefValue is not null
                                        ? SchemaPointerBuilder.BuildRefBasedPointer(
                                            pathItemRefValue, SchemaPointerBuilder.BuildResponseContentSubPath(method, statusCode.Span, mediaTypeName.Span))
                                        : positionalPointer;

                                pointers.Add(new SchemaReference(positionalPointer, resolvablePointer));
                            }

                            if (mediaTypeProp.Value.ItemSchema.IsNotUndefined())
                            {
                                using UnescapedUtf8JsonString mediaTypeName = mediaTypeProp.Utf8NameSpan;

                                string positionalPointer = SchemaPointerBuilder.BuildResponseContentItemSchemaPointer(
                                    rootSegmentUtf8, pathName.Span, method, statusCode.Span, mediaTypeName.Span);

                                pointers.Add(new SchemaReference(positionalPointer, positionalPointer));
                            }
                        }
                    }

                    if (response.Headers.IsNotUndefined())
                    {
                        foreach (var headerProp in response.Headers.EnumerateObject())
                        {
                            if (headerProp.Value.IsUndefined())
                            {
                                continue;
                            }

                            if (!TryResolveHeader(headerProp.Value, referenceResolver, out OpenApiDocument.Header header, out IDisposable headerScope, out string? headerRefValue))
                            {
                                continue;
                            }

                            using (headerScope)
                            {
                                if (header.SchemaValue.IsNotUndefined())
                                {
                                    using UnescapedUtf8JsonString headerName = headerProp.Utf8NameSpan;

                                    string positionalPointer = SchemaPointerBuilder.BuildResponseHeaderSchemaPointer(
                                        rootSegmentUtf8, pathName.Span, method, statusCode.Span, headerName.Span);

                                    string resolvablePointer = headerRefValue is not null
                                        ? SchemaPointerBuilder.BuildRefBasedPointer(headerRefValue, "/schema")
                                        : responseRefValue is not null
                                            ? SchemaPointerBuilder.BuildRefBasedPointer(
                                                responseRefValue, SchemaPointerBuilder.BuildHeaderSubPath(headerName.Span))
                                            : pathItemRefValue is not null
                                                ? SchemaPointerBuilder.BuildRefBasedPointer(
                                                    pathItemRefValue, SchemaPointerBuilder.BuildResponseHeaderSubPath(method, statusCode.Span, headerName.Span))
                                                : positionalPointer;

                                    pointers.Add(new SchemaReference(positionalPointer, resolvablePointer));
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private static void CollectAdditionalOperationPointers(
        JsonProperty<OpenApiDocument.PathItem> pathProp,
        OpenApiDocument.Operation operation,
        string customMethodName,
        OpenApiDocument.PathItem pathItem,
        List<SchemaReference> pointers,
        Dictionary<string, string> parameterNames,
        IOpenApiReferenceResolver referenceResolver,
        ReadOnlySpan<byte> rootSegmentUtf8,
        string? pathItemRefValue = null)
    {
        using UnescapedUtf8JsonString pathName = pathProp.Utf8NameSpan;
        byte[] customMethodUtf8 = System.Text.Encoding.UTF8.GetBytes(customMethodName);

        // Parameter schema pointers (with path/operation merge)
        foreach ((OpenApiDocument.Parameter param, int sourceIndex, bool isPathLevel, string? refValue) in
            MergeParameters(operation, pathItem, referenceResolver))
        {
            if (param.SchemaValue.IsNotUndefined())
            {
                string positionalPointer = BuildAdditionalOpParameterPointer(
                    rootSegmentUtf8, pathName.Span, customMethodUtf8, sourceIndex, isPathLevel);

                string resolvablePointer = refValue is not null
                    ? SchemaPointerBuilder.BuildRefBasedPointer(refValue, "/schema")
                    : positionalPointer;

                pointers.Add(new SchemaReference(positionalPointer, resolvablePointer));

                if (param.Name.IsNotUndefined())
                {
                    string? name = param.Name.GetString();
                    if (name is not null)
                    {
                        parameterNames[positionalPointer.AsSpan(1).ToString()] = name;
                    }
                }
            }
            else if (param.Content.IsNotUndefined())
            {
                // Querystring parameters use 'content' instead of 'schema'.
                foreach (var mediaTypeProp in param.Content.EnumerateObject())
                {
                    using UnescapedUtf8JsonString mediaTypeName = mediaTypeProp.Utf8NameSpan;

                    string positionalPointer = BuildAdditionalOpParameterContentSchemaPointer(
                        rootSegmentUtf8, pathName.Span, customMethodName, sourceIndex, mediaTypeName.Span);

                    string resolvablePointer = refValue is not null
                        ? SchemaPointerBuilder.BuildRefBasedPointer(
                            refValue,
                            SchemaPointerBuilder.BuildContentSubPath(mediaTypeName.Span))
                        : positionalPointer;

                    pointers.Add(new SchemaReference(positionalPointer, resolvablePointer));

                    if (param.Name.IsNotUndefined())
                    {
                        string? name = param.Name.GetString();
                        if (name is not null)
                        {
                            parameterNames[positionalPointer.AsSpan(1).ToString()] = name;
                        }
                    }

                    break; // Only take the first content entry
                }
            }
        }

        // Request body content schema pointers
        if (operation.RequestBody.IsNotUndefined()
            && TryResolveRequestBody(operation.RequestBody, referenceResolver, out OpenApiDocument.RequestBody requestBody, out IDisposable rbScope, out string? rbRefValue)
            && requestBody.ContentValue.IsNotUndefined())
        {
            using (rbScope)
            {
                foreach (var mediaTypeProp in requestBody.ContentValue.EnumerateObject())
                {
                    if (mediaTypeProp.Value.Schema.IsNotUndefined())
                    {
                        using UnescapedUtf8JsonString mediaTypeName = mediaTypeProp.Utf8NameSpan;

                        if (CodeEmitHelpers.IsRawStreamMediaType(mediaTypeProp.Name))
                        {
                            continue;
                        }

                        string positionalPointer = BuildAdditionalOpContentPointer(
                            rootSegmentUtf8, pathName.Span, customMethodUtf8, "/requestBody"u8, mediaTypeName.Span);

                        string resolvablePointer = rbRefValue is not null
                            ? SchemaPointerBuilder.BuildRefBasedPointer(
                                rbRefValue, SchemaPointerBuilder.BuildContentSubPath(mediaTypeName.Span))
                            : positionalPointer;

                        pointers.Add(new SchemaReference(positionalPointer, resolvablePointer));
                    }

                    // Collect prefixItems and items schema pointers for multipart/mixed.
                    if (CodeEmitHelpers.IsMultipartMixedMediaType(mediaTypeProp.Name))
                    {
                        OpenApiDocument.MediaType mediaType = OpenApiDocument.MediaType.From(mediaTypeProp.Value);

                        if (mediaType.PrefixEncoding.IsNotUndefined())
                        {
                            using UnescapedUtf8JsonString mtName = mediaTypeProp.Utf8NameSpan;
                            int prefixIndex = 0;
                            foreach (OpenApiDocument.Encoding encoding in mediaType.PrefixEncoding.EnumerateArray())
                            {
                                string contentType = encoding.ContentType.IsNotUndefined()
                                    ? encoding.ContentType.GetString()!
                                    : "application/json";

                                if (!CodeEmitHelpers.IsRawStreamMediaType(contentType))
                                {
                                    string pointer = BuildPrefixItemSchemaPointer(
                                        rootSegmentUtf8, pathName.Span, OperationMethod.Get, mtName.Span, prefixIndex, customMethodName);
                                    pointers.Add(new SchemaReference(pointer, pointer));
                                }

                                prefixIndex++;
                            }
                        }
                        else if (mediaType.ItemEncoding.IsNotUndefined())
                        {
                            string contentType = mediaType.ItemEncoding.ContentType.IsNotUndefined()
                                ? mediaType.ItemEncoding.ContentType.GetString()!
                                : "application/json";

                            if (!CodeEmitHelpers.IsRawStreamMediaType(contentType))
                            {
                                using UnescapedUtf8JsonString mtName = mediaTypeProp.Utf8NameSpan;
                                string pointer = BuildItemsSchemaPointer(
                                    rootSegmentUtf8, pathName.Span, OperationMethod.Get, mtName.Span, customMethodName);
                                pointers.Add(new SchemaReference(pointer, pointer));
                            }
                        }
                    }
                }
            }
        }

        // Response content and header schema pointers
        if (operation.ResponsesValue.IsNotUndefined())
        {
            foreach (var responseProp in operation.ResponsesValue.EnumerateObject())
            {
                if (responseProp.Value.IsUndefined())
                {
                    continue;
                }

                if (!TryResolveResponse(responseProp.Value, referenceResolver, out OpenApiDocument.Response response, out IDisposable responseScope, out string? responseRefValue))
                {
                    continue;
                }

                using (responseScope)
                {
                    using UnescapedUtf8JsonString statusCode = responseProp.Utf8NameSpan;

                    if (response.ContentValue.IsNotUndefined())
                    {
                        foreach (var mediaTypeProp in response.ContentValue.EnumerateObject())
                        {
                            if (mediaTypeProp.Value.Schema.IsNotUndefined())
                            {
                                if (CodeEmitHelpers.IsRawStreamMediaType(mediaTypeProp.Name))
                                {
                                    continue;
                                }

                                using UnescapedUtf8JsonString mediaTypeName = mediaTypeProp.Utf8NameSpan;

                                string positionalPointer = BuildAdditionalOpResponseContentPointer(
                                    rootSegmentUtf8, pathName.Span, customMethodUtf8, statusCode.Span, mediaTypeName.Span);

                                string resolvablePointer = responseRefValue is not null
                                    ? SchemaPointerBuilder.BuildRefBasedPointer(
                                        responseRefValue, SchemaPointerBuilder.BuildContentSubPath(mediaTypeName.Span))
                                    : positionalPointer;

                                pointers.Add(new SchemaReference(positionalPointer, resolvablePointer));
                            }

                            if (mediaTypeProp.Value.ItemSchema.IsNotUndefined())
                            {
                                using UnescapedUtf8JsonString mediaTypeName = mediaTypeProp.Utf8NameSpan;

                                string positionalPointer = BuildAdditionalOpResponseContentItemSchemaPointer(
                                    rootSegmentUtf8, pathName.Span, customMethodName, statusCode.Span, mediaTypeName.Span);

                                pointers.Add(new SchemaReference(positionalPointer, positionalPointer));
                            }
                        }
                    }

                    if (response.Headers.IsNotUndefined())
                    {
                        foreach (var headerProp in response.Headers.EnumerateObject())
                        {
                            if (!TryResolveHeader(headerProp.Value, referenceResolver, out OpenApiDocument.Header header, out IDisposable headerScope, out string? headerRefValue))
                            {
                                continue;
                            }

                            using (headerScope)
                            {
                                if (header.SchemaValue.IsNotUndefined())
                                {
                                    using UnescapedUtf8JsonString headerName = headerProp.Utf8NameSpan;

                                    string positionalPointer = BuildAdditionalOpResponseHeaderPointer(
                                        rootSegmentUtf8, pathName.Span, customMethodUtf8, statusCode.Span, headerName.Span);

                                    string resolvablePointer = headerRefValue is not null
                                        ? SchemaPointerBuilder.BuildRefBasedPointer(headerRefValue, "/schema")
                                        : positionalPointer;

                                    pointers.Add(new SchemaReference(positionalPointer, resolvablePointer));
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // ── Pointer builders for additionalOperations ────────────────────────
    // Format: #/paths/<path>/additionalOperations/<METHOD>/...
    // Uses string concatenation since this is codegen-time only (not runtime hot path).
    private static string BuildAdditionalOpParameterPointer(
        ReadOnlySpan<byte> rootSegmentUtf8, ReadOnlySpan<byte> pathNameUtf8, ReadOnlySpan<byte> customMethodUtf8, int index, bool isPathLevel)
    {
        string rootSegment = System.Text.Encoding.UTF8.GetString(rootSegmentUtf8);
        string pathSegment = EncodeJsonPointerSegment(System.Text.Encoding.UTF8.GetString(pathNameUtf8));
        string methodName = System.Text.Encoding.UTF8.GetString(customMethodUtf8);

        return isPathLevel
            ? $"#/{rootSegment}/{pathSegment}/parameters/{index}/schema"
            : $"#/{rootSegment}/{pathSegment}/additionalOperations/{methodName}/parameters/{index}/schema";
    }

    private static string BuildAdditionalOpContentPointer(
        ReadOnlySpan<byte> rootSegmentUtf8, ReadOnlySpan<byte> pathNameUtf8, ReadOnlySpan<byte> customMethodUtf8, ReadOnlySpan<byte> parentSegmentUtf8, ReadOnlySpan<byte> mediaTypeNameUtf8)
    {
        string rootSegment = System.Text.Encoding.UTF8.GetString(rootSegmentUtf8);
        string pathSegment = EncodeJsonPointerSegment(System.Text.Encoding.UTF8.GetString(pathNameUtf8));
        string methodName = System.Text.Encoding.UTF8.GetString(customMethodUtf8);
        string parentSegment = System.Text.Encoding.UTF8.GetString(parentSegmentUtf8);
        string mediaTypeSegment = EncodeJsonPointerSegment(System.Text.Encoding.UTF8.GetString(mediaTypeNameUtf8));

        return $"#/{rootSegment}/{pathSegment}/additionalOperations/{methodName}{parentSegment}/content/{mediaTypeSegment}/schema";
    }

    private static string BuildAdditionalOpResponseContentPointer(
        ReadOnlySpan<byte> rootSegmentUtf8, ReadOnlySpan<byte> pathNameUtf8, ReadOnlySpan<byte> customMethodUtf8, ReadOnlySpan<byte> statusCodeUtf8, ReadOnlySpan<byte> mediaTypeNameUtf8)
    {
        string rootSegment = System.Text.Encoding.UTF8.GetString(rootSegmentUtf8);
        string pathSegment = EncodeJsonPointerSegment(System.Text.Encoding.UTF8.GetString(pathNameUtf8));
        string methodName = System.Text.Encoding.UTF8.GetString(customMethodUtf8);
        string statusCode = System.Text.Encoding.UTF8.GetString(statusCodeUtf8);
        string mediaTypeSegment = EncodeJsonPointerSegment(System.Text.Encoding.UTF8.GetString(mediaTypeNameUtf8));

        return $"#/{rootSegment}/{pathSegment}/additionalOperations/{methodName}/responses/{statusCode}/content/{mediaTypeSegment}/schema";
    }

    private static string BuildAdditionalOpResponseHeaderPointer(
        ReadOnlySpan<byte> rootSegmentUtf8, ReadOnlySpan<byte> pathNameUtf8, ReadOnlySpan<byte> customMethodUtf8, ReadOnlySpan<byte> statusCodeUtf8, ReadOnlySpan<byte> headerNameUtf8)
    {
        string rootSegment = System.Text.Encoding.UTF8.GetString(rootSegmentUtf8);
        string pathSegment = EncodeJsonPointerSegment(System.Text.Encoding.UTF8.GetString(pathNameUtf8));
        string methodName = System.Text.Encoding.UTF8.GetString(customMethodUtf8);
        string statusCode = System.Text.Encoding.UTF8.GetString(statusCodeUtf8);
        string headerSegment = EncodeJsonPointerSegment(System.Text.Encoding.UTF8.GetString(headerNameUtf8));

        return $"#/{rootSegment}/{pathSegment}/additionalOperations/{methodName}/responses/{statusCode}/headers/{headerSegment}/schema";
    }

    private static string EncodeJsonPointerSegment(string segment) =>
        segment.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    // String-based overloads for use during PrepareOperation (where customMethodName is a string)
    private static string BuildAdditionalOpParameterSchemaPointer(
        ReadOnlySpan<byte> pathNameUtf8, string customMethodName, int index)
    {
        string pathSegment = EncodeJsonPointerSegment(System.Text.Encoding.UTF8.GetString(pathNameUtf8));
        return $"#/paths/{pathSegment}/additionalOperations/{customMethodName}/parameters/{index}/schema";
    }

    private static string BuildAdditionalOpContentSchemaPointer(
        ReadOnlySpan<byte> pathNameUtf8, string customMethodName, ReadOnlySpan<byte> parentSegmentUtf8, ReadOnlySpan<byte> mediaTypeNameUtf8)
    {
        string pathSegment = EncodeJsonPointerSegment(System.Text.Encoding.UTF8.GetString(pathNameUtf8));
        string parentSegment = System.Text.Encoding.UTF8.GetString(parentSegmentUtf8);
        string mediaTypeSegment = EncodeJsonPointerSegment(System.Text.Encoding.UTF8.GetString(mediaTypeNameUtf8));
        return $"#/paths/{pathSegment}/additionalOperations/{customMethodName}{parentSegment}/content/{mediaTypeSegment}/schema";
    }

    private static string BuildAdditionalOpResponseContentSchemaPointer(
        ReadOnlySpan<byte> pathNameUtf8, string customMethodName, ReadOnlySpan<byte> statusCodeUtf8, ReadOnlySpan<byte> mediaTypeNameUtf8)
    {
        string pathSegment = EncodeJsonPointerSegment(System.Text.Encoding.UTF8.GetString(pathNameUtf8));
        string statusCode = System.Text.Encoding.UTF8.GetString(statusCodeUtf8);
        string mediaTypeSegment = EncodeJsonPointerSegment(System.Text.Encoding.UTF8.GetString(mediaTypeNameUtf8));
        return $"#/paths/{pathSegment}/additionalOperations/{customMethodName}/responses/{statusCode}/content/{mediaTypeSegment}/schema";
    }

    private static string BuildAdditionalOpResponseContentItemSchemaPointer(
        ReadOnlySpan<byte> rootSegmentUtf8,
        ReadOnlySpan<byte> pathNameUtf8,
        string customMethodName,
        ReadOnlySpan<byte> statusCodeUtf8,
        ReadOnlySpan<byte> mediaTypeNameUtf8)
    {
        string rootSegment = System.Text.Encoding.UTF8.GetString(rootSegmentUtf8);
        string pathSegment = EncodeJsonPointerSegment(System.Text.Encoding.UTF8.GetString(pathNameUtf8));
        string statusCode = System.Text.Encoding.UTF8.GetString(statusCodeUtf8);
        string mediaTypeSegment = EncodeJsonPointerSegment(System.Text.Encoding.UTF8.GetString(mediaTypeNameUtf8));
        return $"#/{rootSegment}/{pathSegment}/additionalOperations/{customMethodName}/responses/{statusCode}/content/{mediaTypeSegment}/itemSchema";
    }

    private static string BuildAdditionalOpResponseHeaderSchemaPointer(
        ReadOnlySpan<byte> pathNameUtf8, string customMethodName, ReadOnlySpan<byte> statusCodeUtf8, ReadOnlySpan<byte> headerNameUtf8)
    {
        string pathSegment = EncodeJsonPointerSegment(System.Text.Encoding.UTF8.GetString(pathNameUtf8));
        string statusCode = System.Text.Encoding.UTF8.GetString(statusCodeUtf8);
        string headerSegment = EncodeJsonPointerSegment(System.Text.Encoding.UTF8.GetString(headerNameUtf8));
        return $"#/paths/{pathSegment}/additionalOperations/{customMethodName}/responses/{statusCode}/headers/{headerSegment}/schema";
    }

    // ═══════════════════════════════════════════════════════════════════
    // Walk logic — yields typed model references
    // ═══════════════════════════════════════════════════════════════════
    internal static IEnumerable<OperationRef> WalkOperationRefs(
        JsonElement specRoot,
        OperationFilter? filter,
        IOpenApiReferenceResolver? referenceResolver = null)
    {
        OpenApiDocument doc = specRoot;

        if (doc.PathsValue.IsUndefined())
        {
            yield break;
        }

        referenceResolver ??= new LocalReferenceResolver(specRoot);

        foreach (JsonProperty<OpenApiDocument.PathItem> pathProp in doc.PathsValue.EnumerateObject())
        {
            if (filter is not null)
            {
                using UnescapedUtf16JsonString name = pathProp.Utf16NameSpan;
                if (!filter.Matches(name.Span))
                {
                    continue;
                }
            }

            foreach (OperationRef entry in WalkPathItemRefs(pathProp, referenceResolver))
            {
                yield return entry;
            }
        }
    }

    private static IEnumerable<OperationRef> WalkPathItemRefs(
        JsonProperty<OpenApiDocument.PathItem> pathProp,
        IOpenApiReferenceResolver referenceResolver)
    {
        OpenApiDocument.PathItem pathItem = pathProp.Value;

        if (!TryResolvePathItem(pathItem, referenceResolver, out OpenApiDocument.PathItem resolved, out IDisposable pathItemScope))
        {
            yield break;
        }

        using (pathItemScope)
        {
            if (resolved.Get.IsNotUndefined())
            {
                yield return new OperationRef(pathProp, resolved.Get, OperationMethod.Get, resolved);
            }

            if (resolved.Put.IsNotUndefined())
            {
                yield return new OperationRef(pathProp, resolved.Put, OperationMethod.Put, resolved);
            }

            if (resolved.Post.IsNotUndefined())
            {
                yield return new OperationRef(pathProp, resolved.Post, OperationMethod.Post, resolved);
            }

            if (resolved.Delete.IsNotUndefined())
            {
                yield return new OperationRef(pathProp, resolved.Delete, OperationMethod.Delete, resolved);
            }

            if (resolved.Options.IsNotUndefined())
            {
                yield return new OperationRef(pathProp, resolved.Options, OperationMethod.Options, resolved);
            }

            if (resolved.Head.IsNotUndefined())
            {
                yield return new OperationRef(pathProp, resolved.Head, OperationMethod.Head, resolved);
            }

            if (resolved.Patch.IsNotUndefined())
            {
                yield return new OperationRef(pathProp, resolved.Patch, OperationMethod.Patch, resolved);
            }

            if (resolved.Trace.IsNotUndefined())
            {
                yield return new OperationRef(pathProp, resolved.Trace, OperationMethod.Trace, resolved);
            }

            if (resolved.Query.IsNotUndefined())
            {
                yield return new OperationRef(pathProp, resolved.Query, OperationMethod.Query, resolved);
            }

            // Walk additionalOperations — custom HTTP methods (e.g. COPY, PURGE)
            if (resolved.AdditionalOperations.IsNotUndefined())
            {
                foreach (var additionalOp in resolved.AdditionalOperations.EnumerateObject())
                {
                    if (additionalOp.Value.IsNotUndefined())
                    {
                        yield return new OperationRef(pathProp, additionalOp.Value, OperationMethod.Custom, resolved, additionalOp.Name);
                    }
                }
            }
        }
    }

    private static IEnumerable<OperationRef> WalkWebhookOperationRefs(
        JsonElement specRoot,
        OperationFilter? filter,
        IOpenApiReferenceResolver? referenceResolver = null)
    {
        OpenApiDocument doc = specRoot;

        if (doc.Webhooks.IsUndefined())
        {
            yield break;
        }

        referenceResolver ??= new LocalReferenceResolver(specRoot);

        foreach (JsonProperty<OpenApiDocument.PathItem> webhookProp in doc.Webhooks.EnumerateObject())
        {
            if (filter is not null)
            {
                using UnescapedUtf16JsonString name = webhookProp.Utf16NameSpan;
                if (!filter.Matches(name.Span))
                {
                    continue;
                }
            }

            foreach (OperationRef entry in WalkPathItemRefs(webhookProp, referenceResolver))
            {
                yield return entry;
            }
        }
    }

    private static IEnumerable<OperationRef> WalkCallbackOperationRefs(
        JsonElement specRoot,
        OperationFilter? filter,
        IOpenApiReferenceResolver? referenceResolver = null)
    {
        OpenApiDocument doc = specRoot;

        if (doc.PathsValue.IsUndefined())
        {
            yield break;
        }

        referenceResolver ??= new LocalReferenceResolver(specRoot);

        // Walk each operation in paths and check for callbacks
        foreach (JsonProperty<OpenApiDocument.PathItem> pathProp in doc.PathsValue.EnumerateObject())
        {
            OpenApiDocument.PathItem pathItem = pathProp.Value;

            if (!TryResolvePathItem(pathItem, referenceResolver, out OpenApiDocument.PathItem resolved, out IDisposable pathItemScope))
            {
                continue;
            }

            using (pathItemScope)
            {
                foreach (OpenApiDocument.Operation operation in EnumerateOperationsInPathItem(resolved))
                {
                    OpenApiDocument.Operation.CallbacksEntity callbacks = operation.Callbacks;
                    if (callbacks.IsUndefined())
                    {
                        continue;
                    }

                    foreach (JsonProperty<OpenApiDocument.CallbacksOrReference> callbackProp in callbacks.EnumerateObject())
                    {
                        // Resolve the callbacks-or-reference
                        OpenApiDocument.Callbacks? resolvedCallbacks = ResolveCallbacksOrReference(callbackProp.Value, referenceResolver);
                        if (resolvedCallbacks is null)
                        {
                            continue;
                        }

                        // Each property in the Callbacks object is a runtime-expression → path-item
                        foreach (JsonProperty<OpenApiDocument.PathItem> callbackPathProp in resolvedCallbacks.Value.EnumerateObject())
                        {
                            if (filter is not null)
                            {
                                using UnescapedUtf16JsonString name = callbackPathProp.Utf16NameSpan;
                                if (!filter.Matches(name.Span))
                                {
                                    continue;
                                }
                            }

                            foreach (OperationRef entry in WalkPathItemRefs(callbackPathProp, referenceResolver))
                            {
                                yield return entry;
                            }
                        }
                    }
                }
            }
        }
    }

    internal static IEnumerable<OperationRef> WalkWebhookAndCallbackOperationRefs(
        JsonElement specRoot,
        OperationFilter? filter,
        IOpenApiReferenceResolver? referenceResolver = null)
    {
        referenceResolver ??= new LocalReferenceResolver(specRoot);

        foreach (OperationRef opRef in WalkWebhookOperationRefs(specRoot, filter, referenceResolver))
        {
            yield return opRef;
        }

        foreach (OperationRef opRef in WalkCallbackOperationRefs(specRoot, filter, referenceResolver))
        {
            yield return opRef;
        }
    }

    /// <summary>
    /// Enumerates all operations in a path item (yielding just the Operation).
    /// </summary>
    private static IEnumerable<OpenApiDocument.Operation> EnumerateOperationsInPathItem(OpenApiDocument.PathItem resolved)
    {
        if (resolved.Get.IsNotUndefined())
        {
            yield return resolved.Get;
        }

        if (resolved.Put.IsNotUndefined())
        {
            yield return resolved.Put;
        }

        if (resolved.Post.IsNotUndefined())
        {
            yield return resolved.Post;
        }

        if (resolved.Delete.IsNotUndefined())
        {
            yield return resolved.Delete;
        }

        if (resolved.Options.IsNotUndefined())
        {
            yield return resolved.Options;
        }

        if (resolved.Head.IsNotUndefined())
        {
            yield return resolved.Head;
        }

        if (resolved.Patch.IsNotUndefined())
        {
            yield return resolved.Patch;
        }

        if (resolved.Trace.IsNotUndefined())
        {
            yield return resolved.Trace;
        }

        if (resolved.Query.IsNotUndefined())
        {
            yield return resolved.Query;
        }
    }

    internal static IEnumerable<(OpenApiDocument.Operation Operation, OperationMethod Method)> EnumerateOperationsWithMethod(OpenApiDocument.PathItem resolved)
    {
        if (resolved.Get.IsNotUndefined())
        {
            yield return (resolved.Get, OperationMethod.Get);
        }

        if (resolved.Put.IsNotUndefined())
        {
            yield return (resolved.Put, OperationMethod.Put);
        }

        if (resolved.Post.IsNotUndefined())
        {
            yield return (resolved.Post, OperationMethod.Post);
        }

        if (resolved.Delete.IsNotUndefined())
        {
            yield return (resolved.Delete, OperationMethod.Delete);
        }

        if (resolved.Options.IsNotUndefined())
        {
            yield return (resolved.Options, OperationMethod.Options);
        }

        if (resolved.Head.IsNotUndefined())
        {
            yield return (resolved.Head, OperationMethod.Head);
        }

        if (resolved.Patch.IsNotUndefined())
        {
            yield return (resolved.Patch, OperationMethod.Patch);
        }

        if (resolved.Trace.IsNotUndefined())
        {
            yield return (resolved.Trace, OperationMethod.Trace);
        }

        if (resolved.Query.IsNotUndefined())
        {
            yield return (resolved.Query, OperationMethod.Query);
        }
    }

    internal static OpenApiDocument.Callbacks? ResolveCallbacksOrReference(
        OpenApiDocument.CallbacksOrReference callbacksOrRef,
        IOpenApiReferenceResolver referenceResolver)
    {
        // Check if it's a $ref
        if (callbacksOrRef.TryGetAsRequiredRef(out OpenApiDocument.CallbacksOrReference.RequiredRef requiredRef))
        {
            // It's a reference — resolve it
            OpenApiDocument.Reference asRef = OpenApiDocument.Reference.From(requiredRef);
            if (asRef.Ref.IsNotUndefined())
            {
                string refStr = asRef.Ref.GetString()!;
                if (referenceResolver.TryResolve(refStr, out JsonElement element))
                {
                    return OpenApiDocument.Callbacks.From(element);
                }
            }

            return null;
        }

        // It's an inline callbacks object
        if (callbacksOrRef.TryGetAsCallbacks(out OpenApiDocument.Callbacks callbacks))
        {
            return callbacks;
        }

        return null;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Parameter merging — typed dedup via JsonString equality
    // ═══════════════════════════════════════════════════════════════════
    private static List<(OpenApiDocument.Parameter Parameter, int SourceIndex, bool IsPathLevel, string? RefValue)> MergeParameters(
        OpenApiDocument.Operation operation,
        OpenApiDocument.PathItem pathItem,
        IOpenApiReferenceResolver referenceResolver)
    {
        bool hasOperationParams = operation.ParametersValue.IsNotUndefined();
        bool hasPathParams = pathItem.ParametersValue.IsNotUndefined();

        if (!hasOperationParams && !hasPathParams)
        {
            return [];
        }

        List<(OpenApiDocument.Parameter Parameter, int SourceIndex, bool IsPathLevel, string? RefValue)> result = [];

        if (hasPathParams)
        {
            int sourceIndex = 0;
            foreach (OpenApiDocument.ParameterOrReference paramOrRef in pathItem.ParametersValue.EnumerateArray())
            {
                if (paramOrRef.IsUndefined())
                {
                    sourceIndex++;
                    continue;
                }

                if (TryResolveParameter(paramOrRef, referenceResolver, out OpenApiDocument.Parameter typed, out IDisposable paramScope, out string? refValue))
                {
                    paramScope.Dispose();
                    result.Add((typed, sourceIndex, true, refValue));
                }

                sourceIndex++;
            }
        }

        if (hasOperationParams)
        {
            int sourceIndex = 0;
            foreach (OpenApiDocument.ParameterOrReference paramOrRef in operation.ParametersValue.EnumerateArray())
            {
                if (paramOrRef.IsUndefined())
                {
                    sourceIndex++;
                    continue;
                }

                if (TryResolveParameter(paramOrRef, referenceResolver, out OpenApiDocument.Parameter typed, out IDisposable paramScope, out string? refValue))
                {
                    paramScope.Dispose();
                    int existingIndex = FindParameterIndex(result, typed);

                    if (existingIndex >= 0)
                    {
                        result[existingIndex] = (typed, sourceIndex, false, refValue);
                    }
                    else
                    {
                        result.Add((typed, sourceIndex, false, refValue));
                    }
                }

                sourceIndex++;
            }
        }

        return result;
    }

    private static int FindParameterIndex(
        List<(OpenApiDocument.Parameter Parameter, int SourceIndex, bool IsPathLevel, string? RefValue)> parameters,
        OpenApiDocument.Parameter newParam)
    {
        for (int i = 0; i < parameters.Count; i++)
        {
            if (newParam.In.Equals(parameters[i].Parameter.In)
                && newParam.Name.Equals(parameters[i].Parameter.Name))
            {
                return i;
            }
        }

        return -1;
    }

    // ── $ref resolution ─────────────────────────────────────────────────
    private static bool TryResolveParameter(
        OpenApiDocument.ParameterOrReference paramOrRef,
        IOpenApiReferenceResolver referenceResolver,
        out OpenApiDocument.Parameter resolved,
        out IDisposable baseScope,
        out string? refValue)
    {
        OpenApiDocument.Reference asRef = OpenApiDocument.Reference.From(paramOrRef);
        if (asRef.Ref.IsNotUndefined())
        {
            string refStr = asRef.Ref.GetString()!;
            if (referenceResolver.TryResolve(refStr, out JsonElement element))
            {
                resolved = OpenApiDocument.Parameter.From(element);
                refValue = referenceResolver.ResolveToAbsolute(refStr);
                baseScope = referenceResolver.PushResolvedBase(refStr);
                return true;
            }

            resolved = default;
            baseScope = EmptyScope.Instance;
            refValue = null;
            return false;
        }

        resolved = OpenApiDocument.Parameter.From(paramOrRef);
        baseScope = EmptyScope.Instance;
        refValue = null;
        return true;
    }

    private static bool TryResolveRequestBody(
        OpenApiDocument.RequestBodyOrReference requestBodyOrRef,
        IOpenApiReferenceResolver referenceResolver,
        out OpenApiDocument.RequestBody resolved,
        out IDisposable baseScope,
        out string? refValue)
    {
        OpenApiDocument.Reference asRef = OpenApiDocument.Reference.From(requestBodyOrRef);
        if (asRef.Ref.IsNotUndefined())
        {
            string refStr = asRef.Ref.GetString()!;
            if (referenceResolver.TryResolve(refStr, out JsonElement element))
            {
                resolved = OpenApiDocument.RequestBody.From(element);
                refValue = referenceResolver.ResolveToAbsolute(refStr);
                baseScope = referenceResolver.PushResolvedBase(refStr);
                return true;
            }

            resolved = default;
            baseScope = EmptyScope.Instance;
            refValue = null;
            return false;
        }

        resolved = OpenApiDocument.RequestBody.From(requestBodyOrRef);
        baseScope = EmptyScope.Instance;
        refValue = null;
        return true;
    }

    private static bool TryResolveResponse(
        OpenApiDocument.ResponseOrReference responseOrRef,
        IOpenApiReferenceResolver referenceResolver,
        out OpenApiDocument.Response resolved,
        out IDisposable baseScope,
        out string? refValue)
    {
        OpenApiDocument.Reference asRef = OpenApiDocument.Reference.From(responseOrRef);
        if (asRef.Ref.IsNotUndefined())
        {
            string refStr = asRef.Ref.GetString()!;
            if (referenceResolver.TryResolve(refStr, out JsonElement element))
            {
                resolved = OpenApiDocument.Response.From(element);
                refValue = referenceResolver.ResolveToAbsolute(refStr);
                baseScope = referenceResolver.PushResolvedBase(refStr);
                return true;
            }

            resolved = default;
            baseScope = EmptyScope.Instance;
            refValue = null;
            return false;
        }

        resolved = OpenApiDocument.Response.From(responseOrRef);
        baseScope = EmptyScope.Instance;
        refValue = null;
        return true;
    }

    private static bool TryResolveHeader(
        OpenApiDocument.HeaderOrReference headerOrRef,
        IOpenApiReferenceResolver referenceResolver,
        out OpenApiDocument.Header resolved,
        out IDisposable baseScope,
        out string? refValue)
    {
        OpenApiDocument.Reference asRef = OpenApiDocument.Reference.From(headerOrRef);
        if (asRef.Ref.IsNotUndefined())
        {
            string refStr = asRef.Ref.GetString()!;
            if (referenceResolver.TryResolve(refStr, out JsonElement element))
            {
                resolved = OpenApiDocument.Header.From(element);
                refValue = referenceResolver.ResolveToAbsolute(refStr);
                baseScope = referenceResolver.PushResolvedBase(refStr);
                return true;
            }

            resolved = default;
            baseScope = EmptyScope.Instance;
            refValue = null;
            return false;
        }

        resolved = OpenApiDocument.Header.From(headerOrRef);
        baseScope = EmptyScope.Instance;
        refValue = null;
        return true;
    }

    internal static bool TryResolvePathItem(
        OpenApiDocument.PathItem pathItem,
        IOpenApiReferenceResolver referenceResolver,
        out OpenApiDocument.PathItem resolved,
        out IDisposable baseScope)
    {
        OpenApiDocument.Reference asRef = OpenApiDocument.Reference.From(pathItem);
        if (asRef.Ref.IsNotUndefined())
        {
            string refStr = asRef.Ref.GetString()!;

            // Use the non-generic TryResolve to avoid expensive EvaluateSchema() on complex PathItem objects.
            if (referenceResolver.TryResolve(refStr, out JsonElement element))
            {
                resolved = OpenApiDocument.PathItem.From(element);
                baseScope = referenceResolver.PushResolvedBase(refStr);
                return true;
            }

            resolved = default;
            baseScope = EmptyScope.Instance;
            return false;
        }

        resolved = pathItem;
        baseScope = EmptyScope.Instance;
        return true;
    }

    // ── Parameter trait parsing ──────────────────────────────────────────
    private static (ParameterLocation Location, ParameterStyle Style, bool Explode, bool AllowReserved) ParseParameterTraits(
        OpenApiDocument.Parameter typed)
    {
        (ParameterLocation location, ParameterStyle style, bool allowReserved) = typed.In.Match<OpenApiDocument.Parameter, (ParameterLocation, ParameterStyle, bool)>(
            in typed,
            static (OpenApiDocument.Parameter p) => (ParameterLocation.Query, ParseQueryStyle(OpenApiDocument.Parameter.SchemaEntity.StylesForQueryEntity.From(p)), ParseQueryAllowReserved(OpenApiDocument.Parameter.SchemaEntity.StylesForQueryEntity.From(p))),
            static (OpenApiDocument.Parameter _) => (ParameterLocation.Querystring, ParameterStyle.Form, false),
            static (OpenApiDocument.Parameter _) => (ParameterLocation.Header, ParameterStyle.Simple, false),
            static (OpenApiDocument.Parameter p) => ParsePathTraits(OpenApiDocument.Parameter.SchemaEntity.StylesForPathEntity.From(p)),
            static (OpenApiDocument.Parameter p) => ParseCookieTraits(OpenApiDocument.Parameter.SchemaEntity.StylesForCookieEntity.From(p)),
            static (OpenApiDocument.Parameter p) => (ParameterLocation.Query, ParseQueryStyle(OpenApiDocument.Parameter.SchemaEntity.StylesForQueryEntity.From(p)), ParseQueryAllowReserved(OpenApiDocument.Parameter.SchemaEntity.StylesForQueryEntity.From(p))));

        bool explode = typed.Explode.IsNotUndefined() ? (bool)typed.Explode : style == ParameterStyle.Form;
        return (location, style, explode, allowReserved);
    }

    private static (ParameterLocation, ParameterStyle, bool AllowReserved) ParsePathTraits(
        OpenApiDocument.Parameter.SchemaEntity.StylesForPathEntity pathStyles)
    {
        ParameterStyle style = pathStyles.Match(
            static (in OpenApiDocument.Parameter.SchemaEntity.StylesForPathEntity.RequiredRequired rr) =>
                rr.Style.Match(
                    static () => ParameterStyle.Matrix,
                    static () => ParameterStyle.Label,
                    static () => ParameterStyle.Simple,
                    static () => ParameterStyle.Simple),
            static (in OpenApiDocument.Parameter.SchemaEntity.StylesForPathEntity _) =>
                ParameterStyle.Simple);

        bool allowReserved = pathStyles.AllowReserved.IsNotUndefined() && (bool)pathStyles.AllowReserved;
        return (ParameterLocation.Path, style, allowReserved);
    }

    private static (ParameterLocation, ParameterStyle, bool AllowReserved) ParseCookieTraits(
        OpenApiDocument.Parameter.SchemaEntity.StylesForCookieEntity cookieStyles)
    {
        return cookieStyles.Match(
            static (in OpenApiDocument.Parameter.SchemaEntity.StylesForCookieEntity.ThenEntity then) =>
            {
                ParameterStyle style = then.Style.Match(
                    static () => ParameterStyle.Form,
                    static () => ParameterStyle.Cookie,
                    static () => ParameterStyle.Form);

                // allowReserved is only valid when style=form
                bool allowReserved = style == ParameterStyle.Form &&
                    then.AllowReserved.IsNotUndefined() &&
                    (bool)then.AllowReserved;

                return (ParameterLocation.Cookie, style, allowReserved);
            },
            static (in OpenApiDocument.Parameter.SchemaEntity.StylesForCookieEntity _) =>
                (ParameterLocation.Cookie, ParameterStyle.Form, false));
    }

    private static ParameterStyle ParseQueryStyle(OpenApiDocument.Parameter.SchemaEntity.StylesForQueryEntity queryStyles)
    {
        return queryStyles.Match(
            static (in OpenApiDocument.Parameter.SchemaEntity.StylesForQueryEntity.ThenEntity then) =>
                then.Style.Match(
                    static () => ParameterStyle.Form,
                    static () => ParameterStyle.SpaceDelimited,
                    static () => ParameterStyle.PipeDelimited,
                    static () => ParameterStyle.DeepObject,
                    static () => ParameterStyle.Form),
            static (in OpenApiDocument.Parameter.SchemaEntity.StylesForQueryEntity _) =>
                ParameterStyle.Form);
    }

    private static bool ParseQueryAllowReserved(OpenApiDocument.Parameter.SchemaEntity.StylesForQueryEntity queryStyles)
    {
        return queryStyles.AllowReserved.IsNotUndefined() && (bool)queryStyles.AllowReserved;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Preparation — converts typed model to emit-boundary records
    // ═══════════════════════════════════════════════════════════════════
    internal OperationInfo PrepareOperation(
        OperationRef opRef,
        IOpenApiReferenceResolver referenceResolver,
        ServerInfo? rootServer,
        JsonElement specRoot = default)
    {
        using UnescapedUtf8JsonString pathName = opRef.PathProp.Utf8NameSpan;
        ReadOnlySpan<byte> pathNameUtf8 = pathName.Span;

        // Extract strings at the emit boundary
        string pathTemplate = opRef.PathProp.Name;
        string? operationId = opRef.Operation.OperationId.IsNotUndefined()
            ? opRef.Operation.OperationId.GetString()
            : null;
        string? summary = opRef.Operation.Summary.IsNotUndefined()
            ? opRef.Operation.Summary.GetString()
            : null;
        string? description = opRef.Operation.Description.IsNotUndefined()
            ? opRef.Operation.Description.GetString()
            : null;
        bool isDeprecated = opRef.Operation.Deprecated.ValueKind == JsonValueKind.True;
        string[] tags = ExtractTags(opRef.Operation);
        string methodName = GetMethodName(operationId, opRef.Method, pathTemplate, opRef.CustomMethodName);

        ParameterInfo[] parameters = PrepareParameters(
            opRef.Operation, opRef.PathItem, pathNameUtf8, opRef.Method, referenceResolver, opRef.CustomMethodName);
        RequestBodyInfo? requestBody = PrepareRequestBody(
            opRef.Operation, pathNameUtf8, opRef.Method, referenceResolver, opRef.CustomMethodName, this.IgnoreEmptyFormUrlEncodedBody);
        ResponseInfo[] responses = PrepareResponses(
            opRef.Operation, pathNameUtf8, opRef.Method, referenceResolver, opRef.CustomMethodName);

        ServerInfo? effectiveServer = ResolveEffectiveServer(
            opRef.Operation, opRef.PathItem, rootServer);

        OperationSecurityRequirementSet[]? securityRequirements = specRoot.ValueKind != JsonValueKind.Undefined
            ? ExtractSecurityRequirements(opRef.Operation, specRoot)
            : null;

        return new OperationInfo(
            PathTemplate: pathTemplate,
            Method: opRef.Method,
            MethodName: methodName,
            OperationId: operationId,
            Summary: summary,
            Description: description,
            IsDeprecated: isDeprecated,
            Tags: tags,
            Parameters: parameters,
            RequestBody: requestBody,
            Responses: responses,
            EffectiveServer: effectiveServer,
            SecurityRequirements: securityRequirements,
            CustomMethodName: opRef.CustomMethodName);
    }

    private static ParameterInfo[] PrepareParameters(
        OpenApiDocument.Operation operation,
        OpenApiDocument.PathItem pathItem,
        ReadOnlySpan<byte> pathNameUtf8,
        OperationMethod method,
        IOpenApiReferenceResolver referenceResolver,
        string? customMethodName = null)
    {
        var merged = MergeParameters(operation, pathItem, referenceResolver);

        if (merged.Count == 0)
        {
            return [];
        }

        ParameterInfo[] result = new ParameterInfo[merged.Count];

        for (int i = 0; i < merged.Count; i++)
        {
            (OpenApiDocument.Parameter param, int sourceIndex, bool isPathLevel, string? _) = merged[i];

            (ParameterLocation location, ParameterStyle style, bool explode, bool allowReserved) = ParseParameterTraits(param);
            bool required = param.Required;

            // Querystring parameters use 'content' instead of 'schema'.
            if (location == ParameterLocation.Querystring)
            {
                string? schemaPointerString = BuildQuerystringContentSchemaPointer(
                    param, pathNameUtf8, method, sourceIndex, isPathLevel, customMethodName);
                SchemaRef? schemaPointer = schemaPointerString is not null
                    ? new SchemaRef(schemaPointerString)
                    : null;

                string name = param.Name.IsNotUndefined() ? param.Name.GetString()! : "__querystring";

                result[i] = new ParameterInfo(
                    Name: name,
                    Location: location,
                    IsRequired: required,
                    Style: style,
                    Explode: explode,
                    SerializationKind: ParameterSerializationKind.Object,
                    ElementSerializationKind: ParameterSerializationKind.String,
                    SchemaPointer: schemaPointer,
                    HasDeepNesting: false,
                    DefaultValueJson: null,
                    DefaultValueKind: JsonValueKind.Undefined,
                    AllowReserved: allowReserved);
                continue;
            }

            bool hasSchema = param.SchemaValue.IsNotUndefined();
            JsonElement schemaElement = hasSchema ? JsonElement.From(param.SchemaValue) : default;
            ParameterSerializationKind serializationKind = hasSchema
                ? SchemaClassifier.Classify(schemaElement)
                : ParameterSerializationKind.String;

            ParameterSerializationKind elementKind = serializationKind switch
            {
                ParameterSerializationKind.Array => SchemaClassifier.ClassifyArrayElement(schemaElement),
                ParameterSerializationKind.Object => SchemaClassifier.ClassifyObjectValue(schemaElement),
                _ => ParameterSerializationKind.String,
            };

            bool deepNesting = hasSchema
                && serializationKind is ParameterSerializationKind.Object or ParameterSerializationKind.Array
                && SchemaClassifier.HasDeepNesting(schemaElement);

            SchemaRef? schemaPointerRegular = hasSchema
                ? new SchemaRef(customMethodName is not null
                    ? BuildAdditionalOpParameterSchemaPointer(pathNameUtf8, customMethodName, sourceIndex)
                    : SchemaPointerBuilder.BuildParameterSchemaPointer(
                        "paths"u8, pathNameUtf8, method, sourceIndex, isPathLevel))
                : null;

            // Extract schema default value (if any) for optional parameters.
            string? defaultValueJson = null;
            JsonValueKind defaultValueKind = JsonValueKind.Undefined;
            if (hasSchema && !required
                && schemaElement.TryGetProperty("default"u8, out JsonElement defaultEl)
                && defaultEl.ValueKind != JsonValueKind.Undefined)
            {
                defaultValueJson = defaultEl.GetRawText();
                defaultValueKind = defaultEl.ValueKind;
            }

            // Extract name at the emit boundary
            string regularName = param.Name.GetString()!;

            result[i] = new ParameterInfo(
                Name: regularName,
                Location: location,
                IsRequired: required,
                Style: style,
                Explode: explode,
                SerializationKind: serializationKind,
                ElementSerializationKind: elementKind,
                SchemaPointer: schemaPointerRegular,
                HasDeepNesting: deepNesting,
                DefaultValueJson: defaultValueJson,
                DefaultValueKind: defaultValueKind,
                AllowReserved: allowReserved);
        }

        return result;
    }

    private static string? BuildQuerystringContentSchemaPointer(
        OpenApiDocument.Parameter param,
        ReadOnlySpan<byte> pathNameUtf8,
        OperationMethod method,
        int sourceIndex,
        bool isPathLevel,
        string? customMethodName)
    {
        // Get the first media type from the parameter's content map.
        if (param.Content.IsUndefined())
        {
            return null;
        }

        foreach (var mediaTypeProp in param.Content.EnumerateObject())
        {
            using UnescapedUtf8JsonString mediaTypeName = mediaTypeProp.Utf8NameSpan;

            if (customMethodName is not null)
            {
                // Build pointer for additionalOperations parameters
                return BuildAdditionalOpParameterContentSchemaPointer(
                    "paths"u8, pathNameUtf8, customMethodName, sourceIndex, mediaTypeName.Span);
            }

            return SchemaPointerBuilder.BuildParameterContentSchemaPointer(
                "paths"u8, pathNameUtf8, method, sourceIndex, isPathLevel, mediaTypeName.Span);
        }

        return null;
    }

    private static string BuildAdditionalOpParameterContentSchemaPointer(
        ReadOnlySpan<byte> rootSegmentUtf8,
        ReadOnlySpan<byte> pathNameUtf8,
        string customMethodName,
        int index,
        ReadOnlySpan<byte> mediaTypeNameUtf8)
    {
        string rootSegment = System.Text.Encoding.UTF8.GetString(rootSegmentUtf8);
        string pathSegment = EncodeJsonPointerSegment(System.Text.Encoding.UTF8.GetString(pathNameUtf8));
        string mediaTypeSegment = EncodeJsonPointerSegment(System.Text.Encoding.UTF8.GetString(mediaTypeNameUtf8));
        return $"#/{rootSegment}/{pathSegment}/additionalOperations/{customMethodName}/parameters/{index}/content/{mediaTypeSegment}/schema";
    }

    private static RequestBodyInfo? PrepareRequestBody(
        OpenApiDocument.Operation operation,
        ReadOnlySpan<byte> pathNameUtf8,
        OperationMethod method,
        IOpenApiReferenceResolver referenceResolver,
        string? customMethodName = null,
        bool ignoreEmptyFormUrlEncodedBody = false)
    {
        OpenApiDocument.RequestBodyOrReference requestBodyOrRef = operation.RequestBody;
        if (requestBodyOrRef.IsUndefined())
        {
            return null;
        }

        if (!TryResolveRequestBody(requestBodyOrRef, referenceResolver, out OpenApiDocument.RequestBody requestBody, out IDisposable rbScope, out string? _))
        {
            ThrowHelper.ThrowUnableToResolveRequestBodyRef();
        }

        using (rbScope)
        {
            // When --ignoreEmptyFormUrlEncodedBody is set, treat form-urlencoded bodies
            // whose schema defines no properties as absent. This handles real-world APIs
            // (e.g. Stripe) that emit empty body definitions for operations with no body.
            if (ignoreEmptyFormUrlEncodedBody && IsEmptyFormUrlEncodedBody(requestBody.ContentValue))
            {
                return null;
            }

            bool required = requestBody.Required;
            string? description = requestBody.Description.IsNotUndefined()
                ? requestBody.Description.GetString()
                : null;

            ContentInfo[] content = PrepareContentEntries(
                requestBody.ContentValue, pathNameUtf8, method, "/requestBody"u8, customMethodName);

            BinaryPropertyInfo[] binaryProperties = DetectBinaryProperties(requestBody.ContentValue);

            // Detect multipart/mixed prefixEncoding or itemEncoding.
            MixedPartInfo[]? prefixParts = null;
            MixedPartInfo? itemPart = null;
            DetectMultipartMixedParts(
                requestBody.ContentValue, pathNameUtf8, method, customMethodName,
                out prefixParts, out itemPart);

            return new RequestBodyInfo(description, required, content, binaryProperties, prefixParts, itemPart);
        }
    }

    /// <summary>
    /// Detects properties with <c>format: binary</c> in the multipart content schema.
    /// These properties represent file uploads and should be hoisted to separate
    /// <see cref="BinaryPartData"/> parameters in the generated client method.
    /// </summary>
    private static BinaryPropertyInfo[] DetectBinaryProperties(
        OpenApiDocument.Content contentMap)
    {
        if (contentMap.IsUndefined())
        {
            return [];
        }

        foreach (var mediaTypeProp in contentMap.EnumerateObject())
        {
            if (!CodeEmitHelpers.IsMultipartMediaType(mediaTypeProp.Name))
            {
                continue;
            }

            OpenApiDocument.MediaType mediaType = OpenApiDocument.MediaType.From(mediaTypeProp.Value);
            if (mediaType.SchemaValue.IsUndefined())
            {
                return [];
            }

            JsonElement schema = JsonElement.From(mediaType.SchemaValue);
            if (!schema.TryGetProperty("properties"u8, out JsonElement properties)
                || properties.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            // Read per-property encoding overrides (may specify contentType for binary fields).
            IReadOnlyDictionary<string, EncodingInfo>? encodings = ReadEncodings(mediaType);

            List<BinaryPropertyInfo> result = [];

            foreach (JsonProperty<JsonElement> prop in properties.EnumerateObject())
            {
                JsonElement propSchema = prop.Value;
                if (propSchema.TryGetProperty("format"u8, out JsonElement formatEl)
                    && formatEl.ValueKind == JsonValueKind.String
                    && formatEl.ValueEquals("binary"u8))
                {
                    string propName = prop.Name;
                    string paramName = CodeEmitHelpers.SanitizeParameterName(propName);

                    // Use contentType from encoding object if specified.
                    string? contentType = null;
                    if (encodings is not null
                        && encodings.TryGetValue(propName, out EncodingInfo enc))
                    {
                        contentType = enc.ContentType;
                    }

                    result.Add(new BinaryPropertyInfo(propName, paramName, contentType));
                }
            }

            return [.. result];
        }

        return [];
    }

    private static ResponseInfo[] PrepareResponses(
        OpenApiDocument.Operation operation,
        ReadOnlySpan<byte> pathNameUtf8,
        OperationMethod method,
        IOpenApiReferenceResolver referenceResolver,
        string? customMethodName = null)
    {
        if (operation.ResponsesValue.IsUndefined())
        {
            return [];
        }

        List<ResponseInfo> result = [];

        foreach (var responseProp in operation.ResponsesValue.EnumerateObject())
        {
            if (responseProp.Value.IsUndefined())
            {
                continue;
            }

            if (!TryResolveResponse(responseProp.Value, referenceResolver, out OpenApiDocument.Response response, out IDisposable responseScope, out string? _))
            {
                ThrowHelper.ThrowUnableToResolveResponseRef();
            }

            using (responseScope)
            {
                // Extract status code at the emit boundary
                string statusCode = responseProp.Name;

                using UnescapedUtf8JsonString statusCodeUtf8 = responseProp.Utf8NameSpan;

                string? responseSummary = response.Summary.IsNotUndefined()
                    ? response.Summary.GetString()
                    : null;
                string? responseDescription = response.Description.IsNotUndefined()
                    ? response.Description.GetString()
                    : null;

                ContentInfo[] content = PrepareResponseContentEntries(
                    response.ContentValue, pathNameUtf8, method, statusCodeUtf8.Span, customMethodName);

                HeaderInfo[] headers = PrepareResponseHeaders(
                    response.Headers, pathNameUtf8, method, statusCodeUtf8.Span, referenceResolver, customMethodName);

                LinkInfo[] links = PrepareLinks(response.Links, referenceResolver, statusCode);

                result.Add(new ResponseInfo(
                    StatusCode: statusCode,
                    Content: content,
                    Headers: headers,
                    Links: links,
                    Summary: responseSummary,
                    Description: responseDescription));
            }
        }

        return [.. result];
    }

    private static ContentInfo[] PrepareContentEntries(
        OpenApiDocument.Content contentMap,
        ReadOnlySpan<byte> pathNameUtf8,
        OperationMethod method,
        ReadOnlySpan<byte> parentSegmentUtf8,
        string? customMethodName = null)
    {
        if (contentMap.IsUndefined())
        {
            return [];
        }

        List<ContentInfo> result = [];

        foreach (var mediaTypeProp in contentMap.EnumerateObject())
        {
            string mediaType = mediaTypeProp.Name;
            bool hasSchema = mediaTypeProp.Value.Schema.IsNotUndefined()
                && !CodeEmitHelpers.IsOctetStreamMediaType(mediaType)
                && !CodeEmitHelpers.IsTextPlainMediaType(mediaType);

            SchemaRef? schemaPointer = null;
            if (hasSchema)
            {
                using UnescapedUtf8JsonString mediaTypeName = mediaTypeProp.Utf8NameSpan;
                schemaPointer = new SchemaRef(customMethodName is not null
                    ? BuildAdditionalOpContentSchemaPointer(pathNameUtf8, customMethodName, parentSegmentUtf8, mediaTypeName.Span)
                    : SchemaPointerBuilder.BuildContentSchemaPointer(
                        "paths"u8, pathNameUtf8, method, parentSegmentUtf8, mediaTypeName.Span));
            }

            IReadOnlyDictionary<string, EncodingInfo>? encodings = ReadEncodings(OpenApiDocument.MediaType.From(mediaTypeProp.Value));

            result.Add(new ContentInfo(mediaType, schemaPointer, encodings));
        }

        return [.. result];
    }

    private static ContentInfo[] PrepareResponseContentEntries(
        OpenApiDocument.Content contentMap,
        ReadOnlySpan<byte> pathNameUtf8,
        OperationMethod method,
        ReadOnlySpan<byte> statusCodeUtf8,
        string? customMethodName = null)
    {
        if (contentMap.IsUndefined())
        {
            return [];
        }

        List<ContentInfo> result = [];

        foreach (var mediaTypeProp in contentMap.EnumerateObject())
        {
            string mediaType = mediaTypeProp.Name;
            bool hasSchema = mediaTypeProp.Value.Schema.IsNotUndefined()
                && !CodeEmitHelpers.IsOctetStreamMediaType(mediaType)
                && !CodeEmitHelpers.IsTextPlainMediaType(mediaType);

            SchemaRef? schemaPointer = null;
            if (hasSchema)
            {
                using UnescapedUtf8JsonString mediaTypeName = mediaTypeProp.Utf8NameSpan;
                schemaPointer = new SchemaRef(customMethodName is not null
                    ? BuildAdditionalOpResponseContentSchemaPointer(pathNameUtf8, customMethodName, statusCodeUtf8, mediaTypeName.Span)
                    : SchemaPointerBuilder.BuildResponseContentSchemaPointer(
                        "paths"u8, pathNameUtf8, method, statusCodeUtf8, mediaTypeName.Span));
            }

            SchemaRef? itemSchemaPointer = null;
            if (mediaTypeProp.Value.ItemSchema.IsNotUndefined())
            {
                using UnescapedUtf8JsonString mediaTypeName = mediaTypeProp.Utf8NameSpan;
                itemSchemaPointer = new SchemaRef(customMethodName is not null
                    ? BuildAdditionalOpResponseContentItemSchemaPointer("paths"u8, pathNameUtf8, customMethodName, statusCodeUtf8, mediaTypeName.Span)
                    : SchemaPointerBuilder.BuildResponseContentItemSchemaPointer(
                        "paths"u8, pathNameUtf8, method, statusCodeUtf8, mediaTypeName.Span));
            }

            result.Add(new ContentInfo(mediaType, schemaPointer, null, itemSchemaPointer));
        }

        return [.. result];
    }

    private static IReadOnlyDictionary<string, EncodingInfo>? ReadEncodings(
        OpenApiDocument.MediaType mediaType)
    {
        OpenApiDocument.MediaType.MediaTypeEncodingEntity encodingMap = mediaType.EncodingValue;
        if (encodingMap.IsUndefined())
        {
            return null;
        }

        Dictionary<string, EncodingInfo> result = new(StringComparer.Ordinal);

        foreach (var prop in encodingMap.EnumerateObject())
        {
            string propertyName = prop.Name;
            OpenApiDocument.Encoding encoding = prop.Value;

            string? style = encoding.Style.IsNotUndefined()
                ? encoding.Style.GetString()
                : null;

            bool? explode = encoding.Explode.IsNotUndefined()
                ? (bool)encoding.Explode
                : null;

            bool allowReserved = encoding.AllowReserved.IsNotUndefined()
                && (bool)encoding.AllowReserved;

            string? contentType = encoding.ContentType.IsNotUndefined()
                ? encoding.ContentType.GetString()
                : null;

            result[propertyName] = new EncodingInfo(style, explode, allowReserved, contentType);
        }

        return result;
    }

    private static HeaderInfo[] PrepareResponseHeaders(
        OpenApiDocument.Response.HeadersEntity headersMap,
        ReadOnlySpan<byte> pathNameUtf8,
        OperationMethod method,
        ReadOnlySpan<byte> statusCodeUtf8,
        IOpenApiReferenceResolver referenceResolver,
        string? customMethodName = null)
    {
        if (headersMap.IsUndefined())
        {
            return [];
        }

        List<HeaderInfo> result = [];

        foreach (var headerProp in headersMap.EnumerateObject())
        {
            if (headerProp.Value.IsUndefined())
            {
                continue;
            }

            OpenApiDocument.HeaderOrReference headerOrRef = headerProp.Value;

            if (!TryResolveHeader(headerOrRef, referenceResolver, out OpenApiDocument.Header header, out IDisposable headerScope, out string? _))
            {
                ThrowHelper.ThrowUnableToResolveHeaderRef();
            }

            using (headerScope)
            {
                bool hasSchema = header.SchemaValue.IsNotUndefined();

                SchemaRef? schemaPointer = null;
                if (hasSchema)
                {
                    using UnescapedUtf8JsonString headerName = headerProp.Utf8NameSpan;
                    schemaPointer = new SchemaRef(customMethodName is not null
                        ? BuildAdditionalOpResponseHeaderSchemaPointer(pathNameUtf8, customMethodName, statusCodeUtf8, headerName.Span)
                        : SchemaPointerBuilder.BuildResponseHeaderSchemaPointer(
                            "paths"u8, pathNameUtf8, method, statusCodeUtf8, headerName.Span));
                }

                // Extract explode and serialization kind for response header deserialization.
                // Headers always use style: simple. Explode defaults to false.
                bool explode = false;

                if (hasSchema)
                {
                    explode = header.Explode.ValueKind == JsonValueKind.True;
                }

                JsonElement schemaEl = hasSchema ? JsonElement.From(header.SchemaValue) : default;
                ParameterSerializationKind serializationKind = hasSchema
                    ? SchemaClassifier.Classify(schemaEl)
                    : ParameterSerializationKind.String;

                ParameterSerializationKind elementKind = serializationKind switch
                {
                    ParameterSerializationKind.Array => SchemaClassifier.ClassifyArrayElement(schemaEl),
                    ParameterSerializationKind.Object => SchemaClassifier.ClassifyObjectValue(schemaEl),
                    _ => ParameterSerializationKind.String,
                };

                bool deepNesting = hasSchema
                    && serializationKind is ParameterSerializationKind.Object or ParameterSerializationKind.Array
                    && SchemaClassifier.HasDeepNesting(schemaEl);

                // Extract header name at the emit boundary
                string name = headerProp.Name;
                result.Add(new HeaderInfo(name, schemaPointer, explode, serializationKind, elementKind, deepNesting));
            }
        }

        return [.. result];
    }

    private static LinkInfo[] PrepareLinks(
        OpenApiDocument.Response.LinksEntity linksMap,
        IOpenApiReferenceResolver referenceResolver,
        string sourceStatusCode)
    {
        if (linksMap.IsUndefined())
        {
            return [];
        }

        List<LinkInfo> result = [];

        foreach (var linkProp in linksMap.EnumerateObject())
        {
            if (linkProp.Value.IsUndefined())
            {
                continue;
            }

            if (!TryResolveLink(linkProp.Value, referenceResolver, out OpenApiDocument.Link link, out IDisposable linkScope))
            {
                // Skip unresolvable link references rather than failing the whole generation.
                continue;
            }

            using (linkScope)
            {
                string linkName = linkProp.Name;

                // We only support operationId-based links for now.
                string? targetOperationId = link.OperationId.IsNotUndefined()
                    ? link.OperationId.GetString()
                    : null;

                if (targetOperationId is null)
                {
                    // operationRef links are deferred — skip.
                    continue;
                }

                // Extract parameter bindings (paramName → runtime expression string)
                List<LinkParameterBinding> bindings = [];
                if (link.Parameters.IsNotUndefined())
                {
                    foreach (var paramProp in link.Parameters.EnumerateObject())
                    {
                        string paramName = paramProp.Name;
                        string expression = paramProp.Value.GetString() ?? string.Empty;
                        bindings.Add(new LinkParameterBinding(paramName, expression));
                    }
                }

                // Extract requestBody expression if present
                string? requestBodyExpr = null;
                if (link.RequestBody.IsNotUndefined() && link.RequestBody.ValueKind == JsonValueKind.String)
                {
                    requestBodyExpr = link.RequestBody.GetString();
                }

                string? description = link.Description.IsNotUndefined()
                    ? link.Description.GetString()
                    : null;

                result.Add(new LinkInfo(linkName, targetOperationId, [.. bindings], requestBodyExpr, description, sourceStatusCode));
            }
        }

        return [.. result];
    }

    private static bool TryResolveLink(
        OpenApiDocument.LinkOrReference linkOrRef,
        IOpenApiReferenceResolver referenceResolver,
        out OpenApiDocument.Link resolved,
        out IDisposable baseScope)
    {
        OpenApiDocument.Reference asRef = OpenApiDocument.Reference.From(linkOrRef);
        if (asRef.Ref.IsNotUndefined())
        {
            string refStr = asRef.Ref.GetString()!;
            if (referenceResolver.TryResolve(refStr, out JsonElement element))
            {
                resolved = OpenApiDocument.Link.From(element);
                baseScope = referenceResolver.PushResolvedBase(refStr);
                return true;
            }

            resolved = default;
            baseScope = EmptyScope.Instance;
            return false;
        }

        resolved = OpenApiDocument.Link.From(linkOrRef);
        baseScope = EmptyScope.Instance;
        return true;
    }

    // ── String helpers ──────────────────────────────────────────────────
    private static string[] ExtractTags(OpenApiDocument.Operation operation)
    {
        if (operation.Tags.IsUndefined())
        {
            return [];
        }

        List<string> result = [];

        foreach (var tag in operation.Tags.EnumerateArray())
        {
            if (tag.ValueKind == JsonValueKind.String)
            {
                result.Add(tag.GetString()!);
            }
        }

        return [.. result];
    }

    // ── Server info extraction ───────────────────────────────────────────
    internal static ServerInfo? GetDefaultServerInfo(JsonElement specRoot)
    {
        OpenApiDocument doc = specRoot;

        if (doc.Servers.IsNotUndefined())
        {
            foreach (var server in doc.Servers.EnumerateArray())
            {
                return ExtractServerInfo(server);
            }
        }

        return null;
    }

    private static OperationSecurityRequirementSet[]? ExtractSecurityRequirements(
        OpenApiDocument.Operation operation,
        JsonElement specRoot)
    {
        OpenApiDocument doc = specRoot;
        Dictionary<string, string> schemeTypes = BuildSecuritySchemeTypeLookup(specRoot);

        // Operation-level security overrides document-level
        if (operation.Security.IsNotUndefined())
        {
            return ParseSecurityRequirements(operation.Security, schemeTypes);
        }

        // Fall back to document-level security
        if (doc.Security.IsNotUndefined())
        {
            return ParseSecurityRequirements(doc.Security, schemeTypes);
        }

        return null;
    }

    private static OperationSecurityRequirementSet[]? ParseSecurityRequirements(
        OpenApiDocument.Operation.SecurityRequirementArray securityArray,
        Dictionary<string, string> schemeTypes)
    {
        List<OperationSecurityRequirementSet> sets = [];

        // Each element of the array is an alternative (OR); the schemes within it are AND'd.
        foreach (OpenApiDocument.SecurityRequirement requirement in securityArray.EnumerateArray())
        {
            sets.Add(ParseSecurityRequirementSet(requirement, schemeTypes));
        }

        return sets.Count > 0 ? [.. sets] : null;
    }

    private static OperationSecurityRequirementSet[]? ParseSecurityRequirements(
        OpenApiDocument.SecurityRequirementArray securityArray,
        Dictionary<string, string> schemeTypes)
    {
        List<OperationSecurityRequirementSet> sets = [];

        foreach (OpenApiDocument.SecurityRequirement requirement in securityArray.EnumerateArray())
        {
            sets.Add(ParseSecurityRequirementSet(requirement, schemeTypes));
        }

        return sets.Count > 0 ? [.. sets] : null;
    }

    private static OperationSecurityRequirementSet ParseSecurityRequirementSet(
        OpenApiDocument.SecurityRequirement requirement,
        Dictionary<string, string> schemeTypes)
    {
        List<OperationSecurityRequirement> requirements = [];

        // Each property is one scheme: { "schemeName": ["scope1", "scope2"] }. An empty object marks
        // anonymous access as an accepted alternative.
        foreach (var schemeProp in requirement.EnumerateObject())
        {
            string schemeName = schemeProp.Name;
            List<string> scopes = [];

            foreach (var scope in schemeProp.Value.EnumerateArray())
            {
                scopes.Add(scope.GetString()!);
            }

            requirements.Add(new OperationSecurityRequirement(
                schemeName,
                [.. scopes],
                schemeTypes.TryGetValue(schemeName, out string? schemeType) ? schemeType : null));
        }

        return new OperationSecurityRequirementSet([.. requirements], requirements.Count == 0);
    }

    private static ServerInfo? ExtractServerInfo(OpenApiDocument.Server server)
    {
        if (server.Url.IsUndefined())
        {
            return null;
        }

        string urlTemplate = server.Url.GetString()!;
        List<ServerVariableInfo> variables = [];

        if (server.Variables.IsNotUndefined())
        {
            foreach (var kvp in server.Variables.EnumerateObject())
            {
                string varName = kvp.Name;
                OpenApiDocument.ServerVariable variable = kvp.Value;
                string defaultValue = variable.Default.IsNotUndefined()
                    ? (string)variable.Default
                    : string.Empty;

                string[]? allowedValues = null;
                if (variable.Enum.IsNotUndefined())
                {
                    List<string> values = [];
                    foreach (var item in variable.Enum.EnumerateArray())
                    {
                        values.Add((string)item);
                    }

                    allowedValues = [.. values];
                }

                variables.Add(new ServerVariableInfo(varName, defaultValue, allowedValues));
            }
        }

        return new ServerInfo(urlTemplate, [.. variables]);
    }

    private static ServerInfo? ResolveEffectiveServer(
        OpenApiDocument.Operation operation,
        OpenApiDocument.PathItem pathItem,
        ServerInfo? rootServer)
    {
        // OAS precedence: operation > path > root
        if (operation.Servers.IsNotUndefined())
        {
            foreach (var server in operation.Servers.EnumerateArray())
            {
                return ExtractServerInfo(server);
            }
        }

        if (pathItem.Servers.IsNotUndefined())
        {
            foreach (var server in pathItem.Servers.EnumerateArray())
            {
                return ExtractServerInfo(server);
            }
        }

        return rootServer;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the content map contains only a form-urlencoded
    /// media type whose schema defines no properties (i.e. the body is empty/redundant).
    /// </summary>
    private static bool IsEmptyFormUrlEncodedBody(OpenApiDocument.Content contentMap)
    {
        if (contentMap.IsUndefined())
        {
            return false;
        }

        foreach (var mediaTypeProp in contentMap.EnumerateObject())
        {
            if (!CodeEmitHelpers.IsFormUrlEncodedMediaType(mediaTypeProp.Name))
            {
                return false;
            }

            OpenApiDocument.MediaType mediaType = OpenApiDocument.MediaType.From(mediaTypeProp.Value);
            if (mediaType.SchemaValue.IsUndefined())
            {
                return true;
            }

            JsonElement schema = JsonElement.From(mediaType.SchemaValue);
            if (!schema.TryGetProperty("properties"u8, out JsonElement properties)
                || properties.ValueKind != JsonValueKind.Object)
            {
                return true;
            }

            var enumerator = properties.EnumerateObject();
            return !enumerator.MoveNext();
        }

        return false;
    }

    /// <summary>
    /// Detects <c>multipart/mixed</c> content with <c>prefixEncoding</c> or <c>itemEncoding</c>.
    /// </summary>
    private static void DetectMultipartMixedParts(
        OpenApiDocument.Content contentMap,
        ReadOnlySpan<byte> pathNameUtf8,
        OperationMethod method,
        string? customMethodName,
        out MixedPartInfo[]? prefixParts,
        out MixedPartInfo? itemPart)
    {
        prefixParts = null;
        itemPart = null;

        if (contentMap.IsUndefined())
        {
            return;
        }

        foreach (var mediaTypeProp in contentMap.EnumerateObject())
        {
            if (!CodeEmitHelpers.IsMultipartMixedMediaType(mediaTypeProp.Name))
            {
                continue;
            }

            OpenApiDocument.MediaType mediaType = OpenApiDocument.MediaType.From(mediaTypeProp.Value);

            using UnescapedUtf8JsonString mediaTypeName = mediaTypeProp.Utf8NameSpan;

            // Check prefixEncoding (array of encodings — positional parts).
            if (mediaType.PrefixEncoding.IsNotUndefined())
            {
                prefixParts = ReadPrefixParts(mediaType, pathNameUtf8, mediaTypeName.Span, method, customMethodName);
                return;
            }

            // Check itemEncoding (single encoding — homogeneous parts).
            if (mediaType.ItemEncoding.IsNotUndefined())
            {
                itemPart = ReadItemPart(mediaType, pathNameUtf8, mediaTypeName.Span, method, customMethodName);
                return;
            }
        }
    }

    /// <summary>
    /// Reads prefixEncoding positions and pairs them with prefixItems schema pointers.
    /// </summary>
    private static MixedPartInfo[] ReadPrefixParts(
        OpenApiDocument.MediaType mediaType,
        ReadOnlySpan<byte> pathNameUtf8,
        ReadOnlySpan<byte> mediaTypeNameUtf8,
        OperationMethod method,
        string? customMethodName)
    {
        List<MixedPartInfo> parts = [];

        // Read prefixEncoding content types.
        int index = 0;
        foreach (OpenApiDocument.Encoding encoding in mediaType.PrefixEncoding.EnumerateArray())
        {
            string contentType = encoding.ContentType.IsNotUndefined()
                ? encoding.ContentType.GetString()!
                : "application/json";

            bool isBinary = CodeEmitHelpers.IsOctetStreamMediaType(contentType)
                || contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                || contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
                || contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

            // Build schema pointer for this prefixItem position.
            string schemaPointer = BuildPrefixItemSchemaPointer(
                "paths"u8, pathNameUtf8, method, mediaTypeNameUtf8, index, customMethodName);

            parts.Add(new MixedPartInfo(new SchemaRef(schemaPointer), contentType, isBinary));
            index++;
        }

        return [.. parts];
    }

    /// <summary>
    /// Reads itemEncoding and pairs with the items schema pointer.
    /// </summary>
    private static MixedPartInfo ReadItemPart(
        OpenApiDocument.MediaType mediaType,
        ReadOnlySpan<byte> pathNameUtf8,
        ReadOnlySpan<byte> mediaTypeNameUtf8,
        OperationMethod method,
        string? customMethodName)
    {
        string contentType = mediaType.ItemEncoding.ContentType.IsNotUndefined()
            ? mediaType.ItemEncoding.ContentType.GetString()!
            : "application/json";

        bool isBinary = CodeEmitHelpers.IsOctetStreamMediaType(contentType)
            || contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

        string schemaPointer = BuildItemsSchemaPointer(
            "paths"u8, pathNameUtf8, method, mediaTypeNameUtf8, customMethodName);

        return new MixedPartInfo(new SchemaRef(schemaPointer), contentType, isBinary);
    }

    private static string BuildPrefixItemSchemaPointer(
        ReadOnlySpan<byte> rootSegmentUtf8,
        ReadOnlySpan<byte> pathNameUtf8,
        OperationMethod method,
        ReadOnlySpan<byte> mediaTypeNameUtf8,
        int index,
        string? customMethodName)
    {
        string rootSegment = Encoding.UTF8.GetString(rootSegmentUtf8);
        string pathSegment = EscapeJsonPointerSegment(Encoding.UTF8.GetString(pathNameUtf8));
        string mediaTypeSegment = EscapeJsonPointerSegment(Encoding.UTF8.GetString(mediaTypeNameUtf8));
        string methodStr = method.ToString().ToLowerInvariant();

        if (customMethodName is not null)
        {
            return $"#/{rootSegment}/{pathSegment}/additionalOperations/{customMethodName}/requestBody/content/{mediaTypeSegment}/schema/prefixItems/{index}";
        }

        return $"#/{rootSegment}/{pathSegment}/{methodStr}/requestBody/content/{mediaTypeSegment}/schema/prefixItems/{index}";
    }

    private static string BuildItemsSchemaPointer(
        ReadOnlySpan<byte> rootSegmentUtf8,
        ReadOnlySpan<byte> pathNameUtf8,
        OperationMethod method,
        ReadOnlySpan<byte> mediaTypeNameUtf8,
        string? customMethodName)
    {
        string rootSegment = Encoding.UTF8.GetString(rootSegmentUtf8);
        string pathSegment = EscapeJsonPointerSegment(Encoding.UTF8.GetString(pathNameUtf8));
        string mediaTypeSegment = EscapeJsonPointerSegment(Encoding.UTF8.GetString(mediaTypeNameUtf8));
        string methodStr = method.ToString().ToLowerInvariant();

        if (customMethodName is not null)
        {
            return $"#/{rootSegment}/{pathSegment}/additionalOperations/{customMethodName}/requestBody/content/{mediaTypeSegment}/schema/items";
        }

        return $"#/{rootSegment}/{pathSegment}/{methodStr}/requestBody/content/{mediaTypeSegment}/schema/items";
    }

    /// <summary>
    /// Escapes a JSON Pointer segment per RFC 6901: <c>~</c> → <c>~0</c>, <c>/</c> → <c>~1</c>.
    /// </summary>
    private static string EscapeJsonPointerSegment(string segment)
    {
        return segment.Replace("~", "~0").Replace("/", "~1");
    }
}