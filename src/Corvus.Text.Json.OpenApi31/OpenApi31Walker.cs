// <copyright file="OpenApi31Walker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using EncodingInfo = Corvus.Text.Json.OpenApi.CodeGeneration.EncodingInfo;

namespace Corvus.Text.Json.OpenApi31;

/// <summary>
/// Walks the strongly-typed OpenAPI 3.1 <see cref="OpenApiDocument"/> model into the shared,
/// language-neutral intermediate representation consumed by <see cref="OpenApi31CodeGenerator"/>.
/// </summary>
internal sealed class OpenApi31Walker : OpenApiWalkerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApi31Walker"/> class.
    /// </summary>
    /// <param name="clientNamePrefix">Optional prefix for client type names.</param>
    /// <param name="ignoreEmptyFormUrlEncodedBody">
    /// When <see langword="true"/>, form-urlencoded request bodies whose schema defines no
    /// properties are treated as if the body were absent.
    /// </param>
    public OpenApi31Walker(string? clientNamePrefix, bool ignoreEmptyFormUrlEncodedBody)
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
        OpenApiDocument.PathItem PathItem);

    // ═══════════════════════════════════════════════════════════════════
    // Schema pointer collection — walks typed model directly
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
                    if (mediaTypeProp.Value.SchemaValue.IsNotUndefined())
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
                            if (mediaTypeProp.Value.SchemaValue.IsNotUndefined())
                            {
                                // Skip raw stream content types — no JSON Schema type needed.
                                if (CodeEmitHelpers.IsRawStreamMediaType(mediaTypeProp.Name))
                                {
                                    continue;
                                }

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
            foreach (OpenApiDocument.ParameterOrReference paramOrRef in pathItem.Parameters.EnumerateArray())
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
            foreach (OpenApiDocument.ParameterOrReference paramOrRef in operation.Parameters.EnumerateArray())
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
    private static (ParameterLocation Location, ParameterStyle Style, bool Explode) ParseParameterTraits(
        OpenApiDocument.Parameter typed)
    {
        (ParameterLocation location, ParameterStyle style) = typed.In.Match<OpenApiDocument.Parameter, (ParameterLocation, ParameterStyle)>(
            in typed,
            static (OpenApiDocument.Parameter p) => (ParameterLocation.Query, ParseQueryStyle(OpenApiDocument.Parameter.SchemaEntity.StylesForQueryEntity.From(p))),
            static (OpenApiDocument.Parameter _) => (ParameterLocation.Header, ParameterStyle.Simple),
            static (OpenApiDocument.Parameter p) => (ParameterLocation.Path, ParsePathStyle(OpenApiDocument.Parameter.SchemaEntity.StylesForPathEntity.From(p))),
            static (OpenApiDocument.Parameter _) => (ParameterLocation.Cookie, ParameterStyle.Form),
            static (OpenApiDocument.Parameter p) => (ParameterLocation.Query, ParseQueryStyle(OpenApiDocument.Parameter.SchemaEntity.StylesForQueryEntity.From(p))));

        bool explode = typed.Explode.IsNotUndefined() ? (bool)typed.Explode : style == ParameterStyle.Form;
        return (location, style, explode);
    }

    private static ParameterStyle ParsePathStyle(OpenApiDocument.Parameter.SchemaEntity.StylesForPathEntity pathStyles)
    {
        return pathStyles.Match(
            static (in OpenApiDocument.Parameter.SchemaEntity.StylesForPathEntity.RequiredRequired rr) =>
                rr.Style.Match(
                    static () => ParameterStyle.Matrix,
                    static () => ParameterStyle.Label,
                    static () => ParameterStyle.Simple,
                    static () => ParameterStyle.Simple),
            static (in OpenApiDocument.Parameter.SchemaEntity.StylesForPathEntity _) =>
                ParameterStyle.Simple);
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
            if (ignoreEmptyFormUrlEncodedBody && IsEmptyFormUrlEncodedBody(requestBody.ContentValue))
            {
                return null;
            }

            bool required = requestBody.Required;
            string? description = requestBody.Description.IsNotUndefined()
                ? requestBody.Description.GetString()
                : null;

            ContentInfo[] content = PrepareContentEntries(
                requestBody.ContentValue, pathNameUtf8, method, "/requestBody"u8);

            BinaryPropertyInfo[] binaryProperties = DetectBinaryProperties(requestBody.ContentValue);

            return new RequestBodyInfo(description, required, content, binaryProperties);
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

            OpenApiDocument.MediaType mediaType = mediaTypeProp.Value;
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

            if (!TryResolveResponse(responseProp.Value, referenceResolver, out OpenApiDocument.Response response, out IDisposable responseScope, out string? _))
            {
                ThrowHelper.ThrowUnableToResolveResponseRef();
            }

            using (responseScope)
            {
                // Extract status code at the emit boundary
                string statusCode = responseProp.Name;

                using UnescapedUtf8JsonString statusCodeUtf8 = responseProp.Utf8NameSpan;

                ContentInfo[] content = PrepareResponseContentEntries(
                    response.ContentValue, pathNameUtf8, method, statusCodeUtf8.Span);

                HeaderInfo[] headers = PrepareResponseHeaders(
                    response.Headers, pathNameUtf8, method, statusCodeUtf8.Span, referenceResolver);

                LinkInfo[] links = PrepareLinks(response.Links, referenceResolver, statusCode);

                result.Add(new ResponseInfo(statusCode, content, headers, links));
            }
        }

        return [.. result];
    }

    private static ContentInfo[] PrepareContentEntries(
        OpenApiDocument.Content contentMap,
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
            bool hasSchema = mediaTypeProp.Value.SchemaValue.IsNotUndefined()
                && !CodeEmitHelpers.IsOctetStreamMediaType(mediaType)
                && !CodeEmitHelpers.IsTextPlainMediaType(mediaType);

            SchemaRef? schemaPointer = null;
            if (hasSchema)
            {
                using UnescapedUtf8JsonString mediaTypeName = mediaTypeProp.Utf8NameSpan;
                schemaPointer = new SchemaRef(SchemaPointerBuilder.BuildContentSchemaPointer(
                    "paths"u8, pathNameUtf8, method, parentSegmentUtf8, mediaTypeName.Span));
            }

            IReadOnlyDictionary<string, EncodingInfo>? encodings = ReadEncodings(mediaTypeProp.Value);

            result.Add(new ContentInfo(mediaType, schemaPointer, encodings));
        }

        return [.. result];
    }

    private static ContentInfo[] PrepareResponseContentEntries(
        OpenApiDocument.Content contentMap,
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
            bool hasSchema = mediaTypeProp.Value.SchemaValue.IsNotUndefined()
                && !CodeEmitHelpers.IsOctetStreamMediaType(mediaType)
                && !CodeEmitHelpers.IsTextPlainMediaType(mediaType);

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
}