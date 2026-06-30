// <copyright file="OpenApi30Walker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using EncodingInfo = Corvus.Text.Json.OpenApi.CodeGeneration.EncodingInfo;

namespace Corvus.Text.Json.OpenApi30;

/// <summary>
/// Walks the strongly-typed OpenAPI 3.0 <see cref="OpenApiDocument"/> model into the shared,
/// language-neutral intermediate representation consumed by <see cref="OpenApi30CodeGenerator"/>.
/// </summary>
internal sealed class OpenApi30Walker : OpenApiWalkerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApi30Walker"/> class.
    /// </summary>
    /// <param name="clientNamePrefix">Optional prefix for client type names.</param>
    /// <param name="ignoreEmptyFormUrlEncodedBody">
    /// When <see langword="true"/>, form-urlencoded request bodies whose schema defines no
    /// properties are treated as if the body were absent.
    /// </param>
    public OpenApi30Walker(string? clientNamePrefix, bool ignoreEmptyFormUrlEncodedBody)
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
    // PathProp is JsonProperty<JsonElement> because PathsValue.EnumerateObject()
    // yields untyped properties in the 3.0 generated model.
    // In 3.0, HTTP methods on PathItem are pattern properties (not named props)
    // so we discover them via TryGetProperty with UTF-8 method names.
    internal readonly record struct OperationRef(
        JsonProperty<JsonElement> PathProp,
        OpenApiDocument.Operation Operation,
        OperationMethod Method,
        OpenApiDocument.PathItem PathItem);

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

    // ═══════════════════════════════════════════════════════════════════
    // Schema pointer collection — walks typed model directly
    // ═══════════════════════════════════════════════════════════════════
    internal static void CollectPathItemPointers(
        JsonProperty<JsonElement> pathProp,
        List<SchemaReference> pointers,
        Dictionary<string, string> parameterNames,
        IOpenApiReferenceResolver referenceResolver,
        ReadOnlySpan<byte> rootSegmentUtf8,
        string? callbackPathItemRef = null)
    {
        OpenApiDocument.PathItem pathItem = OpenApiDocument.PathItem.From(pathProp.Value);

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
            foreach ((ReadOnlyMemory<byte> methodName, OperationMethod method) in HttpMethods)
            {
                if (resolved.TryGetProperty(methodName.Span, out JsonElement operationElement)
                    && operationElement.ValueKind == JsonValueKind.Object)
                {
                    OpenApiDocument.Operation operation = operationElement;
                    CollectOperationPointers(pathProp, operation, method, resolved, pointers, parameterNames, referenceResolver, rootSegmentUtf8, pathItemRefValue);
                }
            }
        }
    }

    private static void CollectOperationPointers(
        JsonProperty<JsonElement> pathProp,
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
            if (param.Schema.IsNotUndefined())
            {
                string positionalPointer = SchemaPointerBuilder.BuildParameterSchemaPointer(
                    rootSegmentUtf8, pathName.Span, method, sourceIndex, isPathLevel);

                string resolvablePointer = refValue is not null
                    ? SchemaPointerBuilder.BuildRefBasedPointer(refValue, "/schema")
                    : pathItemRefValue is not null
                        ? SchemaPointerBuilder.BuildRefBasedPointer(pathItemRefValue, SchemaPointerBuilder.BuildParameterSubPath(method, sourceIndex, isPathLevel))
                        : positionalPointer;

                pointers.Add(new SchemaReference(positionalPointer, resolvablePointer));

                // Record the parameter name so the naming heuristic can use it.
                // Key is the fragment (pointer without leading '#') for direct lookup.
                if (param.Name.IsNotUndefined())
                {
                    string? name = param.Name.GetString();
                    if (name is not null)
                    {
                        parameterNames[positionalPointer.AsSpan(1).ToString()] = name;
                    }
                }
            }
        }

        // Request body content schema pointers
        if (operation.RequestBody.IsNotUndefined()
            && TryResolveRequestBody(operation.RequestBody, referenceResolver, out OpenApiDocument.RequestBody requestBody, out IDisposable rbScope, out string? rbRefValue)
            && requestBody.Content.IsNotUndefined())
        {
            using (rbScope)
            {
                foreach (var mediaTypeProp in requestBody.Content.EnumerateObject())
                {
                    // Skip raw stream content types — no JSON Schema type needed.
                    if (CodeEmitHelpers.IsRawStreamMediaType(mediaTypeProp.Name))
                    {
                        continue;
                    }

                    if (mediaTypeProp.Value.Schema.IsNotUndefined())
                    {
                        using UnescapedUtf8JsonString mediaTypeName = mediaTypeProp.Utf8NameSpan;

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

                if (!TryResolveResponse(
                    OpenApiDocument.Responses.DefaultEntity.From(responseProp.Value),
                    referenceResolver,
                    out OpenApiDocument.Response response,
                    out IDisposable responseScope,
                    out string? responseRefValue))
                {
                    continue;
                }

                using (responseScope)
                {
                    using UnescapedUtf8JsonString statusCode = responseProp.Utf8NameSpan;

                    if (response.Content.IsNotUndefined())
                    {
                        foreach (var mediaTypeProp in response.Content.EnumerateObject())
                        {
                            // Skip raw stream content types — no JSON Schema type needed.
                            if (CodeEmitHelpers.IsRawStreamMediaType(mediaTypeProp.Name))
                            {
                                continue;
                            }

                            if (mediaTypeProp.Value.Schema.IsNotUndefined())
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
                                if (header.Schema.IsNotUndefined())
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

        foreach (JsonProperty<JsonElement> pathProp in doc.PathsValue.EnumerateObject())
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
        JsonProperty<JsonElement> pathProp,
        IOpenApiReferenceResolver referenceResolver)
    {
        OpenApiDocument.PathItem pathItem = OpenApiDocument.PathItem.From(pathProp.Value);

        if (!TryResolvePathItem(pathItem, referenceResolver, out OpenApiDocument.PathItem resolved, out IDisposable pathItemScope))
        {
            yield break;
        }

        using (pathItemScope)
        {
            foreach ((ReadOnlyMemory<byte> methodName, OperationMethod method) in HttpMethods)
            {
                if (resolved.TryGetProperty(methodName.Span, out JsonElement operationElement)
                    && operationElement.ValueKind == JsonValueKind.Object)
                {
                    OpenApiDocument.Operation operation = operationElement;
                    yield return new OperationRef(pathProp, operation, method, resolved);
                }
            }
        }
    }

    internal static IEnumerable<OperationRef> WalkCallbackOperationRefs(
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
        foreach (JsonProperty<JsonElement> pathProp in doc.PathsValue.EnumerateObject())
        {
            OpenApiDocument.PathItem pathItem = OpenApiDocument.PathItem.From(pathProp.Value);

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

                    foreach (JsonProperty<OpenApiDocument.Operation.CallbacksEntity.AdditionalPropertiesEntity> callbackProp in callbacks.EnumerateObject())
                    {
                        // Resolve the callback-or-reference union
                        OpenApiDocument.Callback? resolvedCallback = ResolveCallbackOrReference(callbackProp.Value, referenceResolver);
                        if (resolvedCallback is null)
                        {
                            continue;
                        }

                        // Each property in the Callback object is a runtime-expression → path-item
                        foreach (JsonProperty<OpenApiDocument.PathItem> callbackPathProp in resolvedCallback.Value.EnumerateObject())
                        {
                            if (filter is not null)
                            {
                                using UnescapedUtf16JsonString name = callbackPathProp.Utf16NameSpan;
                                if (!filter.Matches(name.Span))
                                {
                                    continue;
                                }
                            }

                            foreach (OperationRef entry in WalkPathItemRefs(callbackPathProp.AsJsonElementProperty(), referenceResolver))
                            {
                                yield return entry;
                            }
                        }
                    }
                }
            }
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
    }

    internal static OpenApiDocument.Callback? ResolveCallbackOrReference(
        OpenApiDocument.Operation.CallbacksEntity.AdditionalPropertiesEntity callbackOrRef,
        IOpenApiReferenceResolver referenceResolver)
    {
        // Check if it's a $ref
        if (callbackOrRef.TryGetAsReference(out OpenApiDocument.Reference reference))
        {
            if (reference.Ref.IsNotUndefined())
            {
                string refStr = reference.Ref.GetString()!;
                if (referenceResolver.TryResolve(refStr, out JsonElement element))
                {
                    return OpenApiDocument.Callback.From(element);
                }
            }

            return null;
        }

        // It's an inline callback object
        if (callbackOrRef.TryGetAsCallback(out OpenApiDocument.Callback callback))
        {
            return callback;
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
        bool hasOperationParams = operation.Parameters.IsNotUndefined();
        bool hasPathParams = pathItem.Parameters.IsNotUndefined();

        if (!hasOperationParams && !hasPathParams)
        {
            return [];
        }

        List<(OpenApiDocument.Parameter Parameter, int SourceIndex, bool IsPathLevel, string? RefValue)> result = [];

        if (hasPathParams)
        {
            int sourceIndex = 0;
            foreach (OpenApiDocument.PathItem.ParametersEntityArray.ParametersEntity paramOrRef in pathItem.Parameters.EnumerateArray())
            {
                if (paramOrRef.IsUndefined())
                {
                    sourceIndex++;
                    continue;
                }

                if (TryResolvePathItemParameter(paramOrRef, referenceResolver, out OpenApiDocument.Parameter typed, out IDisposable paramScope, out string? refValue))
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
            foreach (OpenApiDocument.Operation.ParametersEntityArray.ParametersEntity paramOrRef in operation.Parameters.EnumerateArray())
            {
                if (paramOrRef.IsUndefined())
                {
                    sourceIndex++;
                    continue;
                }

                if (TryResolveOperationParameter(paramOrRef, referenceResolver, out OpenApiDocument.Parameter typed, out IDisposable paramScope, out string? refValue))
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

    private static bool TryResolvePathItemParameter(
        OpenApiDocument.PathItem.ParametersEntityArray.ParametersEntity paramOrRef,
        IOpenApiReferenceResolver referenceResolver,
        out OpenApiDocument.Parameter resolved,
        out IDisposable baseScope,
        out string? refValue)
    {
        OpenApiDocument.Reference asRef = OpenApiDocument.Reference.From(paramOrRef);
        if (asRef.Ref.IsNotUndefined())
        {
            string refStr = asRef.Ref.GetString()!;
            if (referenceResolver.TryResolve(refStr, out JsonElement pathItemParamElement))
            {
                resolved = OpenApiDocument.Parameter.From(pathItemParamElement);
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

    private static bool TryResolveOperationParameter(
        OpenApiDocument.Operation.ParametersEntityArray.ParametersEntity paramOrRef,
        IOpenApiReferenceResolver referenceResolver,
        out OpenApiDocument.Parameter resolved,
        out IDisposable baseScope,
        out string? refValue)
    {
        OpenApiDocument.Reference asRef = OpenApiDocument.Reference.From(paramOrRef);
        if (asRef.Ref.IsNotUndefined())
        {
            string refStr = asRef.Ref.GetString()!;
            if (referenceResolver.TryResolve(refStr, out JsonElement opParamElement))
            {
                resolved = OpenApiDocument.Parameter.From(opParamElement);
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
        OpenApiDocument.Operation.RequestBodyEntity requestBodyOrRef,
        IOpenApiReferenceResolver referenceResolver,
        out OpenApiDocument.RequestBody resolved,
        out IDisposable baseScope,
        out string? refValue)
    {
        OpenApiDocument.Reference asRef = OpenApiDocument.Reference.From(requestBodyOrRef);
        if (asRef.Ref.IsNotUndefined())
        {
            string refStr = asRef.Ref.GetString()!;
            if (referenceResolver.TryResolve(refStr, out JsonElement rbElement))
            {
                resolved = OpenApiDocument.RequestBody.From(rbElement);
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
        OpenApiDocument.Responses.DefaultEntity responseOrRef,
        IOpenApiReferenceResolver referenceResolver,
        out OpenApiDocument.Response resolved,
        out IDisposable baseScope,
        out string? refValue)
    {
        // Avoid the Match discriminator — the 3.0 typed model's schema validation
        // can reject valid Response Objects that contain optional sub-objects with
        // missing optional properties (e.g. a header with only "description" and
        // no "schema"). Instead, check for $ref directly and fall back to From().
        OpenApiDocument.Reference asRef = OpenApiDocument.Reference.From(responseOrRef);
        if (asRef.Ref.IsNotUndefined())
        {
            string refStr = asRef.Ref.GetString()!;
            if (referenceResolver.TryResolve(refStr, out JsonElement respElement))
            {
                resolved = OpenApiDocument.Response.From(respElement);
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
        OpenApiDocument.Response.HeadersEntity.AdditionalPropertiesEntity headerOrRef,
        IOpenApiReferenceResolver referenceResolver,
        out OpenApiDocument.Header resolved,
        out IDisposable baseScope,
        out string? refValue)
    {
        // Same approach as TryResolveResponse — avoid Match discriminator to
        // handle headers that have optional properties missing (e.g. no "schema").
        OpenApiDocument.Reference asRef = OpenApiDocument.Reference.From(headerOrRef);
        if (asRef.Ref.IsNotUndefined())
        {
            string refStr = asRef.Ref.GetString()!;
            if (referenceResolver.TryResolve(refStr, out JsonElement hdrElement))
            {
                resolved = OpenApiDocument.Header.From(hdrElement);
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

    // ── Parameter trait parsing ──────────────────────────────────────────
    // In OpenAPI 3.0, the "in" and "style" properties are plain JSON strings
    // without const-string enum Match — we use ValueEquals with UTF-8 literals.
    private static (ParameterLocation Location, ParameterStyle Style, bool Explode) ParseParameterTraits(
        OpenApiDocument.Parameter typed)
    {
        ParameterLocation location;
        ParameterStyle style;

        if (typed.In.ValueEquals("query"u8))
        {
            location = ParameterLocation.Query;
            style = ParseQueryStyle(typed);
        }
        else if (typed.In.ValueEquals("header"u8))
        {
            location = ParameterLocation.Header;
            style = ParameterStyle.Simple;
        }
        else if (typed.In.ValueEquals("path"u8))
        {
            location = ParameterLocation.Path;
            style = ParsePathStyle(typed);
        }
        else if (typed.In.ValueEquals("cookie"u8))
        {
            location = ParameterLocation.Cookie;
            style = ParameterStyle.Form;
        }
        else
        {
            location = ParameterLocation.Query;
            style = ParseQueryStyle(typed);
        }

        bool explode = typed.Explode.IsNotUndefined() ? (bool)typed.Explode : style == ParameterStyle.Form;
        return (location, style, explode);
    }

    private static ParameterStyle ParsePathStyle(OpenApiDocument.Parameter typed)
    {
        if (typed.Style.IsUndefined())
        {
            return ParameterStyle.Simple;
        }

        if (typed.Style.ValueEquals("matrix"u8))
        {
            return ParameterStyle.Matrix;
        }

        if (typed.Style.ValueEquals("label"u8))
        {
            return ParameterStyle.Label;
        }

        if (typed.Style.ValueEquals("simple"u8))
        {
            return ParameterStyle.Simple;
        }

        return ParameterStyle.Simple;
    }

    private static ParameterStyle ParseQueryStyle(OpenApiDocument.Parameter typed)
    {
        if (typed.Style.IsUndefined())
        {
            return ParameterStyle.Form;
        }

        if (typed.Style.ValueEquals("form"u8))
        {
            return ParameterStyle.Form;
        }

        if (typed.Style.ValueEquals("spaceDelimited"u8))
        {
            return ParameterStyle.SpaceDelimited;
        }

        if (typed.Style.ValueEquals("pipeDelimited"u8))
        {
            return ParameterStyle.PipeDelimited;
        }

        if (typed.Style.ValueEquals("deepObject"u8))
        {
            return ParameterStyle.DeepObject;
        }

        return ParameterStyle.Form;
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
        string methodName = GetMethodName(operationId, opRef.Method, pathTemplate);

        ParameterInfo[] parameters = PrepareParameters(
            opRef.Operation, opRef.PathItem, pathNameUtf8, opRef.Method, referenceResolver);
        RequestBodyInfo? requestBody = PrepareRequestBody(
            opRef.Operation, pathNameUtf8, opRef.Method, referenceResolver, this.IgnoreEmptyFormUrlEncodedBody);
        ResponseInfo[] responses = PrepareResponses(
            opRef.Operation, pathNameUtf8, opRef.Method, referenceResolver);

        ServerInfo? effectiveServer = ResolveEffectiveServer(
            opRef.Operation, opRef.PathItem, rootServer);

        OperationSecurityRequirementSet[]? securityRequirements = specRoot.ValueKind != JsonValueKind.Undefined
            ? ExtractSecurityRequirements(opRef.Operation, specRoot)
            : null;

        return new OperationInfo(
            pathTemplate,
            opRef.Method,
            methodName,
            operationId,
            summary,
            description,
            isDeprecated,
            tags,
            parameters,
            requestBody,
            responses,
            effectiveServer,
            securityRequirements);
    }

    private static OperationSecurityRequirementSet[]? ExtractSecurityRequirements(
        OpenApiDocument.Operation operation,
        JsonElement specRoot)
    {
        OpenApiDocument doc = specRoot;
        Dictionary<string, string> schemeTypes = BuildSecuritySchemeTypeLookup(specRoot);

        // Operation-level security overrides document-level.
        if (operation.Security.IsNotUndefined())
        {
            return ParseSecurityRequirements(operation.Security, schemeTypes);
        }

        // Fall back to document-level security.
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

    private static ParameterInfo[] PrepareParameters(
        OpenApiDocument.Operation operation,
        OpenApiDocument.PathItem pathItem,
        ReadOnlySpan<byte> pathNameUtf8,
        OperationMethod method,
        IOpenApiReferenceResolver referenceResolver)
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

            (ParameterLocation location, ParameterStyle style, bool explode) = ParseParameterTraits(param);
            bool required = param.Required;
            bool hasSchema = param.Schema.IsNotUndefined();
            JsonElement schemaElement = hasSchema ? JsonElement.From(param.Schema) : default;
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

            SchemaRef? schemaPointer = hasSchema
                ? new SchemaRef(SchemaPointerBuilder.BuildParameterSchemaPointer(
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
            string name = param.Name.GetString()!;

            result[i] = new ParameterInfo(
                name, location, required, style, explode,
                serializationKind, elementKind, schemaPointer, deepNesting, defaultValueJson, defaultValueKind);
        }

        return result;
    }

    private static RequestBodyInfo? PrepareRequestBody(
        OpenApiDocument.Operation operation,
        ReadOnlySpan<byte> pathNameUtf8,
        OperationMethod method,
        IOpenApiReferenceResolver referenceResolver,
        bool ignoreEmptyFormUrlEncodedBody = false)
    {
        OpenApiDocument.Operation.RequestBodyEntity requestBodyOrRef = operation.RequestBody;
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
            if (ignoreEmptyFormUrlEncodedBody && IsEmptyFormUrlEncodedBody(requestBody.Content))
            {
                return null;
            }

            bool required = requestBody.Required;
            string? description = requestBody.Description.IsNotUndefined()
                ? requestBody.Description.GetString()
                : null;

            ContentInfo[] content = PrepareContentEntries(
                requestBody.Content, pathNameUtf8, method, "/requestBody"u8);

            BinaryPropertyInfo[] binaryProperties = DetectBinaryProperties(requestBody.Content);

            return new RequestBodyInfo(description, required, content, binaryProperties);
        }
    }

    /// <summary>
    /// Detects properties with <c>format: binary</c> in the multipart content schema.
    /// These properties represent file uploads and should be hoisted to separate
    /// <see cref="BinaryPartData"/> parameters in the generated client method.
    /// </summary>
    private static BinaryPropertyInfo[] DetectBinaryProperties(
        OpenApiDocument.RequestBody.ContentEntity contentMap)
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

            OpenApiDocument.MediaType mediaType = mediaTypeProp.Value;
            if (mediaType.Schema.IsUndefined())
            {
                return [];
            }

            JsonElement schema = JsonElement.From(mediaType.Schema);
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
        IOpenApiReferenceResolver referenceResolver)
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

            if (!TryResolveResponse(
                OpenApiDocument.Responses.DefaultEntity.From(responseProp.Value),
                referenceResolver,
                out OpenApiDocument.Response response,
                out IDisposable responseScope,
                out string? _))
            {
                ThrowHelper.ThrowUnableToResolveResponseRef();
            }

            using (responseScope)
            {
                // Extract status code at the emit boundary
                string statusCode = responseProp.Name;

                using UnescapedUtf8JsonString statusCodeUtf8 = responseProp.Utf8NameSpan;

                ContentInfo[] content = PrepareResponseContentEntries(
                    response.Content, pathNameUtf8, method, statusCodeUtf8.Span);

                HeaderInfo[] headers = PrepareResponseHeaders(
                    response.Headers, pathNameUtf8, method, statusCodeUtf8.Span, referenceResolver);

                LinkInfo[] links = PrepareLinks(response.Links, referenceResolver, statusCode);

                result.Add(new ResponseInfo(statusCode, content, headers, links));
            }
        }

        return [.. result];
    }

    private static ContentInfo[] PrepareContentEntries(
        OpenApiDocument.RequestBody.ContentEntity contentMap,
        ReadOnlySpan<byte> pathNameUtf8,
        OperationMethod method,
        ReadOnlySpan<byte> parentSegmentUtf8)
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
                && !CodeEmitHelpers.IsRawStreamMediaType(mediaType);

            SchemaRef? schemaPointer = null;
            if (hasSchema)
            {
                using UnescapedUtf8JsonString mediaTypeName = mediaTypeProp.Utf8NameSpan;
                schemaPointer = new SchemaRef(SchemaPointerBuilder.BuildContentSchemaPointer(
                    "paths"u8, pathNameUtf8, method, parentSegmentUtf8, mediaTypeName.Span));
            }

            result.Add(new ContentInfo(mediaType, schemaPointer, ReadEncodings(mediaTypeProp.Value)));
        }

        return [.. result];
    }

    private static ContentInfo[] PrepareResponseContentEntries(
        OpenApiDocument.Response.ContentEntity contentMap,
        ReadOnlySpan<byte> pathNameUtf8,
        OperationMethod method,
        ReadOnlySpan<byte> statusCodeUtf8)
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
                && !CodeEmitHelpers.IsRawStreamMediaType(mediaType);

            SchemaRef? schemaPointer = null;
            if (hasSchema)
            {
                using UnescapedUtf8JsonString mediaTypeName = mediaTypeProp.Utf8NameSpan;
                schemaPointer = new SchemaRef(SchemaPointerBuilder.BuildResponseContentSchemaPointer(
                    "paths"u8, pathNameUtf8, method, statusCodeUtf8, mediaTypeName.Span));
            }

            result.Add(new ContentInfo(mediaType, schemaPointer, null));
        }

        return [.. result];
    }

    private static IReadOnlyDictionary<string, EncodingInfo>? ReadEncodings(
        OpenApiDocument.MediaType mediaType)
    {
        OpenApiDocument.MediaType.EncodingEntity encodingMap = mediaType.Encoding;
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
        IOpenApiReferenceResolver referenceResolver)
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

            if (!TryResolveHeader(headerProp.Value, referenceResolver, out OpenApiDocument.Header header, out IDisposable headerScope, out string? _))
            {
                ThrowHelper.ThrowUnableToResolveHeaderRef();
            }

            using (headerScope)
            {
                bool hasSchema = header.Schema.IsNotUndefined();

                SchemaRef? schemaPointer = null;
                if (hasSchema)
                {
                    using UnescapedUtf8JsonString headerName = headerProp.Utf8NameSpan;
                    schemaPointer = new SchemaRef(SchemaPointerBuilder.BuildResponseHeaderSchemaPointer(
                        "paths"u8, pathNameUtf8, method, statusCodeUtf8, headerName.Span));
                }

                // Extract explode and serialization kind for response header deserialization.
                // Headers always use style: simple. Explode defaults to false.
                bool explode = false;

                if (hasSchema)
                {
                    explode = header.Explode.ValueKind == JsonValueKind.True;
                }

                JsonElement schemaEl = hasSchema ? JsonElement.From(header.Schema) : default;
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

                        // In 3.0, parameters additionalProperties is untyped (any).
                        // The value should be a string (runtime expression) or a literal.
                        string expression = paramProp.Value.ValueKind == JsonValueKind.String
                            ? paramProp.Value.GetString() ?? string.Empty
                            : paramProp.Value.ToString();
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
        OpenApiDocument.Response.LinksEntity.AdditionalPropertiesEntity linkOrRef,
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
    private static bool IsEmptyFormUrlEncodedBody(OpenApiDocument.RequestBody.ContentEntity contentMap)
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

            OpenApiDocument.MediaType mediaType = mediaTypeProp.Value;
            if (mediaType.Schema.IsUndefined())
            {
                return true;
            }

            JsonElement schema = JsonElement.From(mediaType.Schema);
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
}