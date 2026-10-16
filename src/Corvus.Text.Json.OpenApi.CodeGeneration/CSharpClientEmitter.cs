// <copyright file="CSharpClientEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text;
using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi.CodeGeneration.Operations;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The C# client emitter: one emitter for every OpenAPI version, over the prepared operation model. Consolidated
/// from the four spec-version generators' client emission (the 3.2 generator's code is the base, being the feature
/// superset); the version-conditional branches it still carries are the drift items of the drift catalogue, each
/// removed in its own change.
/// </summary>
public sealed class CSharpClientEmitter : IOpenApiClientEmitter
{
    private readonly string rootNamespace;
    private readonly string? clientNamePrefix;
    private readonly IModelTypeMap typeMap;
    private string specVersion = "3.2";

    /// <summary>
    /// Initializes a new instance of the <see cref="CSharpClientEmitter"/> class.
    /// </summary>
    /// <param name="rootNamespace">The root namespace for generated types.</param>
    /// <param name="typeMap">The generated model types the client refers to, by schema pointer.</param>
    /// <param name="clientNamePrefix">The prefix for generated client type names, or <see langword="null"/> to use <c>Api</c>.</param>
    public CSharpClientEmitter(string rootNamespace, IModelTypeMap typeMap, string? clientNamePrefix = null)
    {
        this.rootNamespace = rootNamespace;
        this.typeMap = typeMap;
        this.clientNamePrefix = clientNamePrefix;
    }

    /// <inheritdoc/>
    public string LanguageId => LanguageIds.CSharp;

    /// <inheritdoc/>
    public IReadOnlyList<GeneratedFile> EmitClient(in OpenApiClientModel model, IModelTypeMap types, ClientEmitOptions options)
        => new CSharpClientEmitter(options.Namespace, types, options.ClientName).EmitClientFiles(in model);

    /// <inheritdoc/>
    public IReadOnlyList<GeneratedFile> EmitCallbackClient(in OpenApiClientModel model, IModelTypeMap types, ClientEmitOptions options)
        => new CSharpClientEmitter(options.Namespace, types, options.ClientName).EmitClientFiles(in model);

    private IReadOnlyList<GeneratedFile> EmitClientFiles(in OpenApiClientModel model)
    {
        List<OperationInfo> operations = [.. model.Operations];
        string? documentSelf = model.DocumentSelf;
        this.specVersion = model.SpecVersion;

        // Drift D1 (see the drift catalogue): only the 3.2 generator wrote the SecuritySchemes constants.
        SecuritySchemeInfo[] securitySchemes = model.SpecVersion == "3.2" ? model.SecuritySchemes : [];
        ServerInfo? rootServer = model.RootServer;

        List<GeneratedFile> files = [];
        Dictionary<string, List<OperationInfo>> groups = GroupOperationsByTag(operations);

        foreach (OperationInfo op in operations)
        {
            files.Add(this.EmitRequestStruct(op));
            files.Add(this.EmitResponseStruct(op, operations));
        }

        foreach ((string tag, List<OperationInfo> tagOps) in groups)
        {
            string clientName = this.GetClientName(tag);
            files.Add(this.EmitInterface(clientName, tagOps, rootServer, documentSelf, securitySchemes));
            files.Add(this.EmitImplementation(clientName, tagOps));
        }

        return files;
    }

    private static Dictionary<string, List<OperationInfo>> GroupOperationsByTag(
        List<OperationInfo> operations)
    {
        Dictionary<string, List<OperationInfo>> groups = new(StringComparer.Ordinal);

        foreach (OperationInfo op in operations)
        {
            if (op.Tags.Length == 0)
            {
                AddToGroup(groups, "default", op);
            }
            else
            {
                foreach (string tag in op.Tags)
                {
                    AddToGroup(groups, tag, op);
                }
            }
        }

        return groups;

        static void AddToGroup(
            Dictionary<string, List<OperationInfo>> groups,
            string tag,
            OperationInfo op)
        {
            if (!groups.TryGetValue(tag, out List<OperationInfo>? list))
            {
                list = [];
                groups[tag] = list;
            }

            list.Add(op);
        }
    }

    private string GetClientName(string tag)
    {
        string prefix = this.clientNamePrefix ?? "Api";
        string sanitized = CodeEmitHelpers.SanitizeIdentifier(tag);
        return $"{prefix}{sanitized}";
    }

    private string GetParameterTypeName(ParameterInfo param)
    {
        string resolved = this.ResolveSchemaTypeName(param.SchemaPointer);
        return resolved;
    }

    private string GetParameterSourceTypeName(ParameterInfo param)
    {
        string resolved = this.ResolveSchemaTypeName(param.SchemaPointer);
        return $"{resolved}.Source";
    }

    private string ResolveSchemaTypeName(string? schemaPointer)
    {
        if (schemaPointer is not null
            && this.typeMap.TryGetTypeReference(schemaPointer, out string? typeName))
        {
            return typeName;
        }

        return this.typeMap.FallbackTypeReference;
    }

    // The request body's schema pointer (the key into the type map's builder set), picked by the same rule that picks its
    // type name so the two always describe the same content entry.
    private static string? ResolveRequestBodySchemaPointer(RequestBodyInfo requestBody)
    {
        foreach (ContentInfo content in requestBody.Content)
        {
            if (CodeEmitHelpers.IsJsonMediaType(content.MediaType)
                || CodeEmitHelpers.IsFormUrlEncodedMediaType(content.MediaType)
                || CodeEmitHelpers.IsMultipartMediaType(content.MediaType))
            {
                return content.SchemaPointer;
            }
        }

        return null;
    }

    // Whether the operation can also be offered as a closure-free, single-materialisation generic overload: it must
    // have a body that is materialised from a Source (not a raw stream, not multipart/mixed, whose parts are
    // materialised individually), and that body's type must be one the model generator gives a Source<TContext>.
    private bool HasContextThreadedBody(OperationInfo op)
    {
        if (op.RequestBody is not { } requestBody
            || IsRawStreamRequestBody(requestBody)
            || IsMultipartMixedRequestBody(requestBody))
        {
            return false;
        }

        return ResolveRequestBodySchemaPointer(requestBody) is { } pointer
            && this.typeMap.HasBuilder(pointer);
    }

    private string ResolveRequestBodyTypeName(RequestBodyInfo requestBody)
    {
        foreach (ContentInfo content in requestBody.Content)
        {
            if (CodeEmitHelpers.IsJsonMediaType(content.MediaType)
                || CodeEmitHelpers.IsFormUrlEncodedMediaType(content.MediaType)
                || CodeEmitHelpers.IsMultipartMediaType(content.MediaType))
            {
                return this.ResolveSchemaTypeName(content.SchemaPointer);
            }
        }

        return this.typeMap.FallbackTypeReference;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the request body's primary content type
    /// is a raw stream type (octet-stream, text/plain, or any other non-JSON type).
    /// </summary>
    private static bool IsRawStreamRequestBody(RequestBodyInfo requestBody)
    {
        foreach (ContentInfo content in requestBody.Content)
        {
            if (CodeEmitHelpers.IsRawStreamMediaType(content.MediaType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the content map contains only a form-urlencoded
    /// media type whose schema defines no properties (i.e. the body is empty/redundant).
    /// </summary>
    private static bool IsFormUrlEncodedRequestBody(RequestBodyInfo requestBody)
    {
        foreach (ContentInfo content in requestBody.Content)
        {
            if (CodeEmitHelpers.IsFormUrlEncodedMediaType(content.MediaType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the request body's primary content type
    /// is <c>multipart/form-data</c>.
    /// </summary>
    private static bool IsMultipartRequestBody(RequestBodyInfo requestBody)
    {
        foreach (ContentInfo content in requestBody.Content)
        {
            if (CodeEmitHelpers.IsMultipartMediaType(content.MediaType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the request body's primary content type
    /// is <c>multipart/mixed</c> with either <c>prefixEncoding</c> or <c>itemEncoding</c>.
    /// </summary>
    private static bool IsMultipartMixedRequestBody(RequestBodyInfo requestBody)
    {
        return requestBody.PrefixParts is not null || requestBody.ItemPart is not null;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the multipart/mixed request body carries at least
    /// one binary part (a binary prefix part or binary repeating items).
    /// </summary>
    private static bool HasMixedBinaryParts(RequestBodyInfo requestBody)
    {
        if (requestBody.ItemPart is { IsBinary: true })
        {
            return true;
        }

        if (requestBody.PrefixParts is { } prefixParts)
        {
            foreach (MixedPartInfo part in prefixParts)
            {
                if (part.IsBinary)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the mixed body's binary parts all trail its
    /// non-binary parts in schema position. Mixed bodies are positional (the wire order
    /// is the schema order), so this is the shape live streaming requires; other shapes
    /// keep the buffered path.
    /// </summary>
    private static bool MixedBinaryPartsAreTailShaped(RequestBodyInfo requestBody)
    {
        bool seenBinary = false;
        if (requestBody.PrefixParts is { } prefixParts)
        {
            foreach (MixedPartInfo part in prefixParts)
            {
                if (part.IsBinary)
                {
                    seenBinary = true;
                }
                else if (seenBinary)
                {
                    return false;
                }
            }
        }

        // Non-binary repeating items after binary prefix parts also interleave.
        return requestBody.ItemPart is not { IsBinary: false } || !seenBinary;
    }

    /// <summary>
    /// Detects <c>multipart/mixed</c> content with <c>prefixEncoding</c> or <c>itemEncoding</c>.
    /// </summary>
    private static IReadOnlyDictionary<string, ContentEncodingInfo>? GetRequestBodyEncodings(
        RequestBodyInfo requestBody,
        Func<string, bool> mediaTypePredicate)
    {
        foreach (ContentInfo content in requestBody.Content)
        {
            if (mediaTypePredicate(content.MediaType))
            {
                return content.Encodings;
            }
        }

        return null;
    }

    private string? ResolveResponseTypeName(ResponseInfo resp)
    {
        foreach (ContentInfo content in resp.Content)
        {
            if (CodeEmitHelpers.IsJsonMediaType(content.MediaType))
            {
                return this.ResolveSchemaTypeName(content.SchemaPointer);
            }
        }

        return null;
    }

    // The multipart/form-data response body's schema type: the typed body whose non-binary
    // fields the multipart serializer writes alongside the BinaryPartData parts.
    private string? ResolveMultipartResponseTypeName(ResponseInfo resp)
    {
        foreach (ContentInfo content in resp.Content)
        {
            if (CodeEmitHelpers.IsMultipartMediaType(content.MediaType))
            {
                return this.ResolveSchemaTypeName(content.SchemaPointer);
            }
        }

        return null;
    }

    // The JSON response body's schema pointer (the key into the type map's builder set) — used to decide whether the body
    // type carries a Source<TContext>, and so whether a closure-free Ok<TContext> overload can be emitted for it.
    private string? ResolveResponseBodySchemaPointer(ResponseInfo resp)
    {
        foreach (ContentInfo content in resp.Content)
        {
            if (CodeEmitHelpers.IsJsonMediaType(content.MediaType))
            {
                return content.SchemaPointer;
            }
        }

        return null;
    }

    // The JSON-ish media type the response declares for its body, picked by the same rule that picked the body's
    // schema so the two always describe the same content entry.
    private static string DeclaredJsonMediaType(ResponseInfo resp)
    {
        foreach (ContentInfo content in resp.Content)
        {
            if (CodeEmitHelpers.IsJsonMediaType(content.MediaType))
            {
                return content.MediaType;
            }
        }

        return "application/json";
    }

    private string? ResolveItemSchemaTypeName(ResponseInfo resp)
    {
        foreach (ContentInfo content in resp.Content)
        {
            if (content.ItemSchemaPointer is not null)
            {
                return this.ResolveSchemaTypeName(content.ItemSchemaPointer);
            }
        }

        return null;
    }

    private static List<ContentInfo> GetStreamingContent(ResponseInfo response)
    {
        List<ContentInfo> streamingContent = [];

        foreach (ContentInfo content in response.Content)
        {
            if (content.ItemSchemaPointer is not null)
            {
                streamingContent.Add(content);
            }
        }

        return streamingContent;
    }

    private static string GetStreamingFactoryName(string baseName, string mediaType, bool includeMediaTypeSuffix)
    {
        if (!includeMediaTypeSuffix)
        {
            return baseName;
        }

        if (mediaType.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseName}Sse";
        }

        if (mediaType.StartsWith("application/x-ndjson", StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseName}Ndjson";
        }

        return $"{baseName}{CodeEmitHelpers.SanitizeIdentifier(mediaType)}";
    }

    private static string GetSimpleTypeIdentifier(string typeName)
    {
        int dotIndex = typeName.LastIndexOf('.');
        string simpleName = dotIndex >= 0 ? typeName[(dotIndex + 1)..] : typeName;
        int genericIndex = simpleName.IndexOf('<');

        return genericIndex >= 0 ? simpleName[..genericIndex] : simpleName;
    }

    /// <summary>
    /// Returns the distinct content categories present in a response's content entries.
    /// </summary>
    private static ContentCategory[] GetDistinctContentCategories(ResponseInfo resp)
    {
        return resp.Content
            .Select(c => CodeEmitHelpers.ClassifyMediaType(c.MediaType))
            .Distinct()
            .ToArray();
    }

    /// <summary>
    /// Determines whether a response is classified as a raw <c>application/octet-stream</c> body
    /// (the same classification the client uses to expose a response <see cref="System.IO.Stream"/>).
    /// </summary>
    private static bool IsOctetStreamResponse(ResponseInfo resp)
    {
        return Array.IndexOf(GetDistinctContentCategories(resp), ContentCategory.OctetStream) >= 0;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the response's content is text/plain with no JSON
    /// alternative, in which case the server result carries the text through the binary
    /// writer machinery with a text content type.
    /// </summary>
    private static bool IsTextOnlyResponse(ResponseInfo resp)
    {
        // Streaming responses (SSE, NDJSON) classify into the text/binary buckets by media
        // type but are handled by the streaming machinery, never the binary writer.
        foreach (ContentInfo content in resp.Content)
        {
            if (content.MediaType.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase)
                || string.Equals(content.MediaType, "application/x-ndjson", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        ContentCategory[] categories = GetDistinctContentCategories(resp);
        return Array.IndexOf(categories, ContentCategory.TextPlain) >= 0
            && Array.IndexOf(categories, ContentCategory.Json) < 0;
    }

    /// <summary>
    /// Returns <see langword="true"/> if any 2xx or default response of the operation carries a
    /// raw binary (octet-stream) or text-only body, in which case the server result/endpoint emit
    /// the binary response path mirroring the client's <see cref="System.IO.Stream"/> accessor.
    /// </summary>
    private static bool HasBinaryStyleResponse(OperationInfo op)
    {
        foreach (ResponseInfo resp in op.Responses)
        {
            bool eligible = resp.StatusCode == "default"
                || (resp.StatusCode.Length > 0 && resp.StatusCode[0] == '2');
            if (eligible && (IsOctetStreamResponse(resp) || IsTextOnlyResponse(resp)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the response declares <c>multipart/form-data</c>
    /// content with at least one <c>format: binary</c> property, in which case the server
    /// result carries a typed body plus <see cref="BinaryPartData"/> parts and the endpoint
    /// writes them with the multipart serializer.
    /// </summary>
    private static bool IsMultipartResponseWithBinaryParts(ResponseInfo resp)
        => resp.BinaryParts.Length > 0
            && Array.IndexOf(GetDistinctContentCategories(resp), ContentCategory.Multipart) >= 0;

    /// <summary>
    /// Returns <see langword="true"/> if any 2xx or default response of the operation carries
    /// a <c>multipart/form-data</c> body with binary parts.
    /// </summary>
    private static bool HasMultipartResponse(OperationInfo op)
    {
        foreach (ResponseInfo resp in op.Responses)
        {
            bool eligible = resp.StatusCode == "default"
                || (resp.StatusCode.Length > 0 && resp.StatusCode[0] == '2');
            if (eligible && IsMultipartResponseWithBinaryParts(resp))
            {
                return true;
            }
        }

        return false;
    }

    // ── Request struct emission ─────────────────────────────────────────
    private GeneratedFile EmitRequestStruct(OperationInfo op)
    {
        string structName = $"{op.CSharpMethodName()}{GeneratedClientTypeNaming.RequestSuffix}";
        IndentedWriter w = new();

        CodeEmitHelpers.EmitHeader(w);
        w.WriteLine($"namespace {this.rootNamespace};");
        w.WriteLine();

        // OpenAPI 2.0 only: a collectionFormat with no wire mapping at its location degraded to csv, reported so it is never silent.
        foreach (ParameterInfo degraded in op.Parameters.Where(p => p.DegradedCollectionFormat))
        {
            w.WriteLine($"#warning OpenAPI 2.0 collectionFormat on the '{degraded.Name}' {(degraded.Location == ParameterLocation.Path ? "path" : "header")} parameter has no wire mapping outside query parameters; comma-separated (csv) semantics are used.");
        }

        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Request type for the {op.CSharpMethodName()} operation.");
        w.WriteLine("/// </summary>");

        // Drift D2 (see the drift catalogue): only the 3.2 generator documented a request with its description.
        string? remarks = this.specVersion == "3.2" ? op.Description ?? op.Summary : op.Summary;
        if (remarks is not null)
        {
            w.WriteLine($"/// <remarks>{CodeEmitHelpers.EscapeXml(remarks)}</remarks>");
        }

        w.WriteLine($"public readonly struct {structName} : IApiRequest<{structName}>");
        w.OpenBrace();

        ParameterInfo[] pathParams = op.Parameters
            .Where(p => p.Location == ParameterLocation.Path).ToArray();
        ParameterInfo[] queryParams = op.Parameters
            .Where(p => p.Location == ParameterLocation.Query).ToArray();
        ParameterInfo[] querystringParams = op.Parameters
            .Where(p => p.Location == ParameterLocation.Querystring).ToArray();
        ParameterInfo[] headerParams = op.Parameters
            .Where(p => p.Location == ParameterLocation.Header).ToArray();
        ParameterInfo[] cookieParams = op.Parameters
            .Where(p => p.Location == ParameterLocation.Cookie).ToArray();
        bool hasQueryOrQuerystring = queryParams.Length > 0 || querystringParams.Length > 0;

        // Collect distinct response media types for the Accept header.
        string[] acceptMediaTypes = CodeEmitHelpers.GetAcceptMediaTypes(
            op.Responses
                .SelectMany(r => r.Content)
                .Select(c => (c.MediaType, c.SchemaPointer)));

        this.EmitRequestFields(w, op.Parameters);
        this.EmitRequestConstructor(w, structName, op.Parameters);

        w.WriteLine();
        w.WriteLine("/// <inheritdoc/>");
        w.WriteLine($"public static ReadOnlySpan<byte> PathTemplateUtf8 => \"{op.PathTemplate}\"u8;");
        w.WriteLine();
        w.WriteLine("/// <inheritdoc/>");
        w.WriteLine($"public static OperationMethod Method => {CodeEmitHelpers.OperationMethodExpression(op.Method)};");

        if (op.Method == OperationMethod.Custom && op.CustomMethodName is not null)
        {
            w.WriteLine();
            w.WriteLine("/// <inheritdoc/>");
            w.WriteLine($"public static ReadOnlySpan<byte> CustomMethodNameUtf8 => \"{op.CustomMethodName.ToUpperInvariant()}\"u8;");
        }

        w.WriteLine();
        w.WriteLine("/// <inheritdoc/>");
        w.WriteLine($"public static bool HasPathParameters => {(pathParams.Length > 0 ? "true" : "false")};");
        w.WriteLine();
        w.WriteLine("/// <inheritdoc/>");
        w.WriteLine($"public static bool HasQueryParameters => {(hasQueryOrQuerystring ? "true" : "false")};");
        w.WriteLine();
        bool hasAcceptHeader = acceptMediaTypes.Length > 0;

        w.WriteLine("/// <inheritdoc/>");
        w.WriteLine($"public static bool HasHeaderParameters => {(headerParams.Length > 0 || hasAcceptHeader ? "true" : "false")};");
        w.WriteLine();
        w.WriteLine("/// <inheritdoc/>");
        w.WriteLine($"public static bool HasCookieParameters => {(cookieParams.Length > 0 ? "true" : "false")};");

        w.WriteLine();
        this.EmitWriteResolvedPath(w, op.PathTemplate, pathParams);
        w.WriteLine();
        this.EmitWriteQueryString(w, queryParams, querystringParams);
        w.WriteLine();
        this.EmitWriteHeaders(w, headerParams, acceptMediaTypes);
        w.WriteLine();
        this.EmitWriteCookies(w, cookieParams);
        w.WriteLine();
        this.EmitValidate(w, op.Parameters);

        if (op.EffectiveServer is { } effectiveServer)
        {
            w.WriteLine();
            EmitCreateServerUri(w, effectiveServer);
        }

        w.CloseBrace();

        return new GeneratedFile($"{structName}.cs", w.ToString());
    }

    private void EmitRequestFields(IndentedWriter w, ParameterInfo[] allParams)
    {
        foreach (ParameterInfo param in allParams)
        {
            string fieldName = CodeEmitHelpers.SanitizeIdentifier(param.Name);
            string typeName = this.GetParameterTypeName(param);

            w.WriteLine();
            w.WriteLine("/// <summary>");
            w.WriteLine($"/// Gets the {param.Name} parameter.");
            w.WriteLine("/// </summary>");
            w.WriteLine($"public {typeName} {fieldName} {{ get; init; }}");
        }
    }

    private void EmitRequestConstructor(
        IndentedWriter w,
        string structName,
        ParameterInfo[] allParams)
    {
        ParameterInfo[] requiredParams = allParams.Where(p => p.IsRequired).ToArray();

        if (requiredParams.Length == 0)
        {
            return;
        }

        w.WriteLine();
        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Initializes a new instance of the <see cref=\"{structName}\"/> struct.");
        w.WriteLine("/// </summary>");

        List<string> paramParts = [];

        foreach (ParameterInfo param in requiredParams)
        {
            string sanitizedParam = CodeEmitHelpers.SanitizeParameterName(param.Name);
            w.WriteLine($"/// <param name=\"{sanitizedParam}\">The {param.Name} parameter.</param>");
            string typeName = this.GetParameterTypeName(param);
            paramParts.Add($"{typeName} {CodeEmitHelpers.EscapeCSharpKeyword(sanitizedParam)}");
        }

        w.WriteLine($"public {structName}({string.Join(", ", paramParts)})");
        w.OpenBrace();

        foreach (ParameterInfo param in requiredParams)
        {
            string fieldName = CodeEmitHelpers.SanitizeIdentifier(param.Name);
            string paramIdentifier = CodeEmitHelpers.EscapeCSharpKeyword(
                CodeEmitHelpers.SanitizeParameterName(param.Name));
            w.WriteLine($"this.{fieldName} = {paramIdentifier};");
        }

        w.CloseBrace();
    }

    private void EmitWriteResolvedPath(
        IndentedWriter w,
        string pathTemplate,
        ParameterInfo[] pathParams)
    {
        w.WriteLine("/// <inheritdoc/>");
        w.WriteLine("public void WriteResolvedPath(IBufferWriter<byte> writer)");
        w.OpenBrace();

        if (pathParams.Length == 0)
        {
            w.WriteLine("ThrowHelper.ThrowNoPathParameters();");
        }
        else
        {
            this.EmitPathSegmentWrites(w, pathTemplate, pathParams);
        }

        w.CloseBrace();
    }

    private void EmitPathSegmentWrites(
        IndentedWriter w,
        string pathTemplate,
        ParameterInfo[] pathParams)
    {
        ReadOnlySpan<char> remaining = pathTemplate;

        while (remaining.Length > 0)
        {
            int openBrace = remaining.IndexOf('{');

            if (openBrace < 0)
            {
                w.WriteLine($"writer.Write(\"{remaining.ToString()}\"u8);");
                break;
            }

            if (openBrace > 0)
            {
                w.WriteLine($"writer.Write(\"{remaining[..openBrace].ToString()}\"u8);");
            }

            int closeBrace = remaining[(openBrace + 1)..].IndexOf('}');

            if (closeBrace < 0)
            {
                w.WriteLine($"writer.Write(\"{remaining[openBrace..].ToString()}\"u8);");
                break;
            }

            string paramName = remaining[(openBrace + 1)..(openBrace + 1 + closeBrace)].ToString();
            string fieldName = CodeEmitHelpers.SanitizeIdentifier(paramName);

            ParameterInfo? matchingParam = null;

            foreach (ParameterInfo p in pathParams)
            {
                if (p.Name == paramName)
                {
                    matchingParam = p;
                    break;
                }
            }

            ParameterSerializationKind kind = matchingParam?.SerializationKind
                ?? ParameterSerializationKind.String;
            ParameterStyle style = matchingParam?.Style ?? ParameterStyle.Simple;
            bool explode = matchingParam?.Explode ?? false;
            ParameterSerializationKind? pathElementKind = matchingParam?.ElementSerializationKind;
            bool allowReserved = matchingParam?.AllowReserved ?? false;

            CodeEmitHelpers.EmitPathParamWrite(w, paramName, $"this.{fieldName}", fieldName, kind, style, explode, allowReserved, elementKind: pathElementKind);
            remaining = remaining[(openBrace + 1 + closeBrace + 1)..];
        }
    }

    private void EmitWriteQueryString(IndentedWriter w, ParameterInfo[] queryParams, ParameterInfo[] querystringParams)
    {
        w.WriteLine("/// <inheritdoc/>");
        w.WriteLine("public int WriteQueryString(IBufferWriter<byte> writer)");
        w.OpenBrace();

        if (queryParams.Length == 0 && querystringParams.Length == 0)
        {
            w.WriteLine("ThrowHelper.ThrowNoQueryParameters();");
            w.WriteLine("return default;");
            w.CloseBrace();
            return;
        }

        // Querystring parameter: serialize the entire content object as form-urlencoded query.
        if (querystringParams.Length > 0)
        {
            ParameterInfo qsParam = querystringParams[0];
            string fieldName = CodeEmitHelpers.SanitizeIdentifier(qsParam.Name);

            if (!qsParam.IsRequired)
            {
                w.WriteLine($"if (this.{fieldName}.IsNotUndefined())");
                w.OpenBrace();
                w.WriteLine($"return FormUrlEncodedQueryStringWriter.Write(this.{fieldName}, writer);");
                w.CloseBrace();
                w.WriteLine();
                w.WriteLine("return 0;");
            }
            else
            {
                w.WriteLine($"return FormUrlEncodedQueryStringWriter.Write(this.{fieldName}, writer);");
            }

            w.CloseBrace();
            return;
        }

        // Regular query parameters
        w.WriteLine("int totalWritten = 0;");
        w.WriteLine("bool first = true;");
        w.WriteLine();

        foreach (ParameterInfo param in queryParams)
        {
            string fieldName = CodeEmitHelpers.SanitizeIdentifier(param.Name);
            ParameterSerializationKind kind = param.SerializationKind;

            if (!param.IsRequired)
            {
                w.WriteLine($"if (this.{fieldName}.IsNotUndefined())");
                w.OpenBrace();
                CodeEmitHelpers.EmitQueryParamWrite(w, param.Name, $"this.{fieldName}", fieldName, kind, param.Style, param.Explode, elementKind: param.ElementSerializationKind);
                w.CloseBrace();
                w.WriteLine();
            }
            else
            {
                CodeEmitHelpers.EmitQueryParamWrite(w, param.Name, $"this.{fieldName}", fieldName, kind, param.Style, param.Explode, elementKind: param.ElementSerializationKind);
                w.WriteLine();
            }
        }

        w.WriteLine("return totalWritten;");
        w.CloseBrace();
    }

    private void EmitWriteHeaders(IndentedWriter w, ParameterInfo[] headerParams, string[] acceptMediaTypes)
    {
        w.WriteLine("/// <inheritdoc/>");
        w.WriteLine("public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state)");
        w.OpenBrace();

        bool hasAccept = acceptMediaTypes.Length > 0;

        if (headerParams.Length == 0 && !hasAccept)
        {
            w.WriteLine("ThrowHelper.ThrowNoHeaderParameters();");
            w.CloseBrace();
            return;
        }

        if (hasAccept)
        {
            CodeEmitHelpers.EmitAcceptHeader(w, acceptMediaTypes);
            w.WriteLine();
        }

        foreach (ParameterInfo param in headerParams)
        {
            string fieldName = CodeEmitHelpers.SanitizeIdentifier(param.Name);
            ParameterSerializationKind kind = param.SerializationKind;

            w.WriteLine($"ReadOnlySpan<byte> nameUtf8{fieldName} = \"{param.Name}\"u8;");

            if (!param.IsRequired)
            {
                w.WriteLine($"if (this.{fieldName}.IsNotUndefined())");
                w.OpenBrace();
                CodeEmitHelpers.EmitHeaderParamWrite(w, $"this.{fieldName}", fieldName, kind, param.Style, param.Explode, elementKind: param.ElementSerializationKind);
                w.CloseBrace();
                w.WriteLine();
            }
            else
            {
                CodeEmitHelpers.EmitHeaderParamWrite(w, $"this.{fieldName}", fieldName, kind, param.Style, param.Explode, elementKind: param.ElementSerializationKind);
                w.WriteLine();
            }
        }

        w.CloseBrace();
    }

    private void EmitWriteCookies(IndentedWriter w, ParameterInfo[] cookieParams)
    {
        w.WriteLine("/// <inheritdoc/>");
        w.WriteLine("public int WriteCookies(IBufferWriter<byte> writer)");
        w.OpenBrace();

        if (cookieParams.Length == 0)
        {
            w.WriteLine("ThrowHelper.ThrowNoCookieParameters();");
            w.WriteLine("return default;");
            w.CloseBrace();
            return;
        }

        w.WriteLine("int totalWritten = 0;");
        w.WriteLine("bool first = true;");
        w.WriteLine();

        foreach (ParameterInfo param in cookieParams)
        {
            string fieldName = CodeEmitHelpers.SanitizeIdentifier(param.Name);
            ParameterSerializationKind kind = param.SerializationKind;

            // For cookie style, allowReserved is implied (no percent-encoding).
            // For form style, respect the allowReserved flag from the spec.
            bool effectiveAllowReserved = param.Style == ParameterStyle.Cookie || param.AllowReserved;

            if (!param.IsRequired)
            {
                w.WriteLine($"if (this.{fieldName}.IsNotUndefined())");
                w.OpenBrace();
                CodeEmitHelpers.EmitCookieParamWrite(w, param.Name, $"this.{fieldName}", fieldName, kind, param.Style, param.Explode, effectiveAllowReserved, elementKind: param.ElementSerializationKind);
                w.CloseBrace();
                w.WriteLine();
            }
            else
            {
                CodeEmitHelpers.EmitCookieParamWrite(w, param.Name, $"this.{fieldName}", fieldName, kind, param.Style, param.Explode, effectiveAllowReserved, elementKind: param.ElementSerializationKind);
                w.WriteLine();
            }
        }

        w.WriteLine("return totalWritten;");
        w.CloseBrace();
    }

    private void EmitValidate(IndentedWriter w, ParameterInfo[] allParams)
    {
        // Filter to params that have a schema (and thus a typed property).
        ParameterInfo[] validatable = allParams.Where(p => p.SchemaPointer is not null).ToArray();

        w.WriteLine("/// <inheritdoc/>");
        w.WriteLine("public void Validate(ValidationMode mode = ValidationMode.Basic)");
        w.OpenBrace();

        if (validatable.Length == 0)
        {
            w.CloseBrace();
            return;
        }

        w.WriteLine("if (mode == ValidationMode.None)");
        w.OpenBrace();
        w.WriteLine("return;");
        w.CloseBrace();
        w.WriteLine("else if (mode == ValidationMode.Detailed)");
        w.OpenBrace();

        foreach (ParameterInfo param in validatable)
        {
            string fieldName = CodeEmitHelpers.SanitizeIdentifier(param.Name);

            if (param.IsRequired)
            {
                w.WriteLine($"using JsonSchemaResultsCollector collector{fieldName} = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);");
                w.WriteLine($"if (!this.{fieldName}.EvaluateSchema(collector{fieldName}))");
                w.OpenBrace();
                w.WriteLine($"ThrowHelper.ThrowRequestParameterValidationFailed(\"{param.Name}\", SchemaValidationDetail.FormatResults(collector{fieldName}));");
                w.CloseBrace();
                w.WriteLine();
            }
            else
            {
                w.WriteLine($"if (this.{fieldName}.IsNotUndefined())");
                w.OpenBrace();
                w.WriteLine($"using JsonSchemaResultsCollector collector{fieldName} = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);");
                w.WriteLine($"if (!this.{fieldName}.EvaluateSchema(collector{fieldName}))");
                w.OpenBrace();
                w.WriteLine($"ThrowHelper.ThrowRequestParameterValidationFailed(\"{param.Name}\", SchemaValidationDetail.FormatResults(collector{fieldName}));");
                w.CloseBrace();
                w.CloseBrace();
                w.WriteLine();
            }
        }

        w.CloseBrace();
        w.WriteLine("else");
        w.OpenBrace();

        foreach (ParameterInfo param in validatable)
        {
            string fieldName = CodeEmitHelpers.SanitizeIdentifier(param.Name);

            if (param.IsRequired)
            {
                w.WriteLine($"if (!this.{fieldName}.EvaluateSchema())");
                w.OpenBrace();
                w.WriteLine($"ThrowHelper.ThrowRequestParameterValidationFailed(\"{param.Name}\");");
                w.CloseBrace();
                w.WriteLine();
            }
            else
            {
                w.WriteLine($"if (this.{fieldName}.IsNotUndefined() && !this.{fieldName}.EvaluateSchema())");
                w.OpenBrace();
                w.WriteLine($"ThrowHelper.ThrowRequestParameterValidationFailed(\"{param.Name}\");");
                w.CloseBrace();
                w.WriteLine();
            }
        }

        w.CloseBrace();
        w.CloseBrace();
    }

    private void EmitResponseValidate(IndentedWriter w, ResponseInfo[] responses)
    {
        // Collect responses that have a JSON body with a resolved type name.
        List<(string StatusCode, string AccessorName)> validatable = [];

        foreach (ResponseInfo resp in responses)
        {
            ContentCategory[] categories = GetDistinctContentCategories(resp);
            if (categories.Contains(ContentCategory.Json))
            {
                // Skip streaming responses — they can't be validated eagerly.
                if (this.ResolveItemSchemaTypeName(resp) is not null)
                {
                    continue;
                }

                string? typeName = this.ResolveResponseTypeName(resp);
                if (typeName is not null)
                {
                    validatable.Add((resp.StatusCode, CodeEmitHelpers.StatusCodeToName(resp.StatusCode)));
                }
            }
        }

        w.WriteLine("/// <inheritdoc/>");
        w.WriteLine("public void Validate(ValidationMode mode = ValidationMode.Basic)");
        w.OpenBrace();

        if (validatable.Count == 0)
        {
            w.CloseBrace();
            return;
        }

        w.WriteLine("if (mode == ValidationMode.None)");
        w.OpenBrace();
        w.WriteLine("return;");
        w.CloseBrace();
        w.WriteLine("else if (mode == ValidationMode.Detailed)");
        w.OpenBrace();
        EmitResponseValidateBodies(w, validatable, detailed: true);
        w.CloseBrace();
        w.WriteLine("else");
        w.OpenBrace();
        EmitResponseValidateBodies(w, validatable, detailed: false);
        w.CloseBrace();

        w.CloseBrace();
    }

    private static void EmitResponseValidateBodies(
        IndentedWriter w,
        List<(string StatusCode, string AccessorName)> validatable,
        bool detailed)
    {
        // Separate named status codes from the default fallback.
        List<(string StatusCode, string AccessorName)> named = [];
        (string StatusCode, string AccessorName)? defaultResp = null;

        foreach ((string statusCode, string accessorName) in validatable)
        {
            if (statusCode == "default")
            {
                defaultResp = (statusCode, accessorName);
            }
            else
            {
                named.Add((statusCode, accessorName));
            }
        }

        // Emit if / else if chain for named status codes.
        for (int i = 0; i < named.Count; i++)
        {
            (string statusCode, string accessorName) = named[i];
            string prefix = i == 0 ? "if" : "else if";
            w.WriteLine($"{prefix} (this.StatusCode == {statusCode})");
            w.OpenBrace();
            EmitBodyValidation(w, accessorName, statusCode, detailed);
            w.CloseBrace();
        }

        // Emit the default as else (or standalone if no named codes).
        if (defaultResp is { } def)
        {
            if (named.Count > 0)
            {
                w.WriteLine("else");
                w.OpenBrace();
                EmitBodyValidation(w, def.AccessorName, "this.StatusCode", detailed);
                w.CloseBrace();
            }
            else
            {
                EmitBodyValidation(w, def.AccessorName, "this.StatusCode", detailed);
            }
        }
    }

    private static void EmitBodyValidation(
        IndentedWriter w,
        string accessorName,
        string statusCodeExpr,
        bool detailed)
    {
        if (detailed)
        {
            w.WriteLine($"using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);");
            w.WriteLine($"if (!this.{accessorName}Body.EvaluateSchema(collector))");
            w.OpenBrace();
            w.WriteLine($"ThrowHelper.ThrowResponseBodyValidationFailed({statusCodeExpr}, SchemaValidationDetail.FormatResults(collector));");
            w.CloseBrace();
        }
        else
        {
            w.WriteLine($"if (!this.{accessorName}Body.EvaluateSchema())");
            w.OpenBrace();
            w.WriteLine($"ThrowHelper.ThrowResponseBodyValidationFailed({statusCodeExpr});");
            w.CloseBrace();
        }
    }

    // ── Response struct emission ────────────────────────────────────────
    private GeneratedFile EmitResponseStruct(OperationInfo op, List<OperationInfo> allOperations)
    {
        string structName = $"{op.CSharpMethodName()}{GeneratedClientTypeNaming.ResponseSuffix}";
        IndentedWriter w = new();

        CodeEmitHelpers.EmitHeader(w);
        w.WriteLine($"namespace {this.rootNamespace};");
        w.WriteLine();

        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Response type for the {op.CSharpMethodName()} operation.");
        w.WriteLine("/// </summary>");
        w.WriteLine($"public struct {structName} : IApiResponse<{structName}>");
        w.OpenBrace();

        w.WriteLine("private IAsyncDisposable? owner;");

        // Only emit parsedDocument field when at least one response has a JSON body (excluding streaming responses).
        bool hasJsonBody = op.Responses.Any(r =>
            r.Content.Any(c => c.SchemaPointer is not null && CodeEmitHelpers.IsJsonMediaType(c.MediaType))
            && !r.Content.Any(c => c.ItemSchemaPointer is not null));
        bool hasTextBody = op.Responses.Any(r =>
            r.Content.Any(c => CodeEmitHelpers.IsTextPlainMediaType(c.MediaType))
            && !r.Content.Any(c => c.ItemSchemaPointer is not null));
        if (hasJsonBody)
        {
            w.WriteLine("private IDisposable? parsedDocument;");
        }

        // Determine if this operation has any resolvable links.
        bool hasLinks = op.Responses.Any(r => r.Links.Length > 0);
        if (hasLinks)
        {
            w.WriteLine("private IApiTransport? transport;");
        }

        // Determine if any link uses $request.* expressions, requiring the source request.
        bool hasRequestExprLinks = hasLinks && HasRequestBasedExpressions(op);
        bool hasRequestBodyExprLinks = hasRequestExprLinks && HasRequestBodyExpressions(op);
        if (hasRequestExprLinks)
        {
            w.WriteLine($"internal {op.CSharpMethodName()}Request sourceRequest;");
            w.WriteLine("internal JsonWorkspace sourceWorkspace;");

            if (hasRequestBodyExprLinks && op.RequestBody is not null)
            {
                string bodyTypeName = this.ResolveRequestBodyTypeName(op.RequestBody.Value);
                w.WriteLine($"internal {bodyTypeName} sourceBody;");
            }
        }

        // Emit private backing fields for text/plain responses (excluding streaming responses with itemSchema).
        foreach (ResponseInfo resp in op.Responses)
        {
            // If this response has itemSchema, it's a streaming response — skip text/plain fields.
            if (resp.Content.Any(c => c.ItemSchemaPointer is not null))
            {
                continue;
            }

            ContentCategory[] categories = GetDistinctContentCategories(resp);
            if (categories.Contains(ContentCategory.TextPlain))
            {
                string accessorName = CodeEmitHelpers.StatusCodeToName(resp.StatusCode);
                CodeEmitHelpers.EmitTextPlainFields(w, accessorName);
            }
        }

        // Only emit the responseHeaders field if any response has typed headers.
        bool hasTypedHeaders = op.Responses.Any(
            r => r.Headers.Any(h => h.SchemaPointer is not null));
        if (hasTypedHeaders)
        {
            w.WriteLine("private IResponseHeaders? responseHeaders;");
            w.WriteLine("private JsonWorkspace workspace;");
        }

        // Detect streaming responses with itemSchema.
        bool hasItemSchema = op.Responses.Any(r =>
            r.Content.Any(c => c.ItemSchemaPointer is not null));
        if (hasItemSchema)
        {
            w.WriteLine("private Stream? itemStream;");
        }

        w.WriteLine();
        w.WriteLine("/// <inheritdoc/>");
        w.WriteLine("public int StatusCode { get; private set; }");
        w.WriteLine();
        w.WriteLine("/// <inheritdoc/>");
        w.WriteLine("public bool IsSuccess => this.StatusCode >= 200 && this.StatusCode < 300;");

        foreach (ResponseInfo resp in op.Responses)
        {
            string accessorName = CodeEmitHelpers.StatusCodeToName(resp.StatusCode);
            ContentCategory[] categories = GetDistinctContentCategories(resp);
            string bodyDocSummary = resp.Summary ?? $"Gets the {resp.StatusCode} response body.";

            // Skip all body properties for streaming responses — use EnumerateXxxItems instead.
            bool isStreaming = resp.Content.Any(c => c.ItemSchemaPointer is not null);

            foreach (ContentCategory cat in categories)
            {
                if (cat == ContentCategory.OctetStream)
                {
                    if (isStreaming)
                    {
                        continue;
                    }

                    w.WriteLine();
                    w.WriteLine("/// <summary>");
                    w.WriteLine($"/// {CodeEmitHelpers.EscapeXml(resp.Summary ?? $"Gets the {resp.StatusCode} response stream.")}");
                    w.WriteLine("/// </summary>");
                    w.WriteLine($"public Stream? {accessorName}Stream {{ get; private set; }}");
                }
                else if (cat == ContentCategory.TextPlain)
                {
                    if (isStreaming)
                    {
                        continue;
                    }

                    CodeEmitHelpers.EmitTextPlainProperties(w, accessorName);
                }
                else if (cat == ContentCategory.Multipart)
                {
                    if (isStreaming || !IsMultipartResponseWithBinaryParts(resp))
                    {
                        continue;
                    }

                    w.WriteLine();
                    w.WriteLine("/// <summary>");
                    w.WriteLine($"/// Gets the holder for the {resp.StatusCode} multipart response body. Read it with <see cref=\"Get{accessorName}MultipartAsync\"/>; it streams and can be read once.");
                    w.WriteLine("/// </summary>");
                    w.WriteLine($"public MultipartResponseBody? {accessorName}MultipartBody {{ get; private set; }}");
                }
                else if (cat == ContentCategory.Json)
                {
                    if (isStreaming)
                    {
                        continue;
                    }

                    string? typeName = this.ResolveResponseTypeName(resp);
                    if (typeName is not null)
                    {
                        w.WriteLine();
                        w.WriteLine("/// <summary>");
                        w.WriteLine($"/// {CodeEmitHelpers.EscapeXml(bodyDocSummary)}");
                        w.WriteLine("/// </summary>");
                        w.WriteLine($"public {typeName} {accessorName}Body {{ get; private set; }}");
                    }
                }
            }
        }

        this.EmitResponseHeaderProperties(w, op.Responses);

        // Emit streaming enumeration methods for responses with itemSchema.
        if (hasItemSchema)
        {
            this.EmitStreamingItemMethods(w, op.Responses);
        }

        w.WriteLine();
        this.EmitCreateAsync(w, structName, op.Responses, hasLinks);

        // Collect the specific (non-default) status codes for TryGetDefault.
        List<string> specificStatusCodes = [];

        foreach (ResponseInfo resp in op.Responses)
        {
            if (resp.StatusCode != "default")
            {
                ContentCategory[] cats = GetDistinctContentCategories(resp);
                bool hasContent = cats.Length > 0
                    && !(cats.Length == 1 && cats[0] == ContentCategory.Json
                         && this.ResolveResponseTypeName(resp) is null);
                if (hasContent)
                {
                    specificStatusCodes.Add(resp.StatusCode);
                }
            }
        }

        foreach (ResponseInfo resp in op.Responses)
        {
            string accessorName = CodeEmitHelpers.StatusCodeToName(resp.StatusCode);
            ContentCategory[] categories = GetDistinctContentCategories(resp);
            bool isStreaming = resp.Content.Any(c => c.ItemSchemaPointer is not null);

            foreach (ContentCategory cat in categories)
            {
                // Skip all TryGet methods for streaming responses — use EnumerateXxxItems instead.
                if (isStreaming && (cat == ContentCategory.OctetStream || cat == ContentCategory.TextPlain || cat == ContentCategory.Json))
                {
                    continue;
                }

                if (cat == ContentCategory.OctetStream)
                {
                    EmitTryGetMethod(
                        w, resp, accessorName, "Stream", "Stream?",
                        $"this.{accessorName}Stream!", "response stream",
                        specificStatusCodes, useNotNullWhen: true);
                }

                if (cat == ContentCategory.TextPlain)
                {
                    EmitTryGetMethod(
                        w, resp, accessorName, "String", "string?",
                        $"this.{accessorName}Text!", "response text",
                        specificStatusCodes, useNotNullWhen: true);
                }

                if (cat == ContentCategory.Json)
                {
                    string? typeName = this.ResolveResponseTypeName(resp);
                    if (typeName is not null)
                    {
                        EmitTryGetMethod(
                            w, resp, accessorName, string.Empty, typeName,
                            $"this.{accessorName}Body", "typed response body",
                            specificStatusCodes, useNotNullWhen: false);
                    }
                }
            }
        }

        this.EmitMultipartResponseAccessors(w, op);

        this.EmitMatchResult(w, structName, op.Responses, includeContext: false);
        this.EmitMatchResult(w, structName, op.Responses, includeContext: true);

        w.WriteLine();
        this.EmitResponseValidate(w, op.Responses);

        w.WriteLine();
        w.WriteLine("/// <inheritdoc/>");
        w.WriteLine("public async ValueTask DisposeAsync()");
        w.OpenBrace();

        if (hasTypedHeaders)
        {
            w.WriteLine("this.workspace.Dispose();");
        }

        if (hasJsonBody)
        {
            w.WriteLine("this.parsedDocument?.Dispose();");
        }

        if (hasRequestExprLinks)
        {
            w.WriteLine("this.sourceWorkspace?.Dispose();");
        }

        // Return rented text/plain buffers (excluding streaming responses with itemSchema).
        foreach (ResponseInfo resp in op.Responses)
        {
            if (resp.Content.Any(c => c.ItemSchemaPointer is not null))
            {
                continue;
            }

            ContentCategory[] cats = GetDistinctContentCategories(resp);
            if (cats.Contains(ContentCategory.TextPlain))
            {
                string accessorName = CodeEmitHelpers.StatusCodeToName(resp.StatusCode);
                CodeEmitHelpers.EmitTextPlainBufferReturn(w, accessorName);
            }
        }

        foreach (ResponseInfo resp in op.Responses)
        {
            if (resp.Content.Any(c => c.ItemSchemaPointer is not null) || !IsMultipartResponseWithBinaryParts(resp))
            {
                continue;
            }

            string accessorName = CodeEmitHelpers.StatusCodeToName(resp.StatusCode);
            string camelName = CodeEmitHelpers.ToCamelCase(accessorName);
            w.WriteLine($"if (this.{accessorName}MultipartBody is {{ }} {camelName}MultipartBody)");
            w.OpenBrace();
            w.WriteLine($"await {camelName}MultipartBody.DisposeAsync().ConfigureAwait(false);");
            w.CloseBrace();
        }

        w.WriteLine("if (this.owner is not null)");
        w.OpenBrace();
        w.WriteLine("await this.owner.DisposeAsync().ConfigureAwait(false);");
        w.CloseBrace();
        w.CloseBrace();

        // Emit link accessor struct and property when the operation has links.
        if (hasLinks)
        {
            this.EmitLinksAccessor(w, structName, op, allOperations);
        }

        w.CloseBrace();

        this.EmitMultipartViewStructs(w, op);

        return new GeneratedFile($"{structName}.cs", w.ToString());
    }

    /// <summary>
    /// Emits the streaming accessor for each 2xx multipart/form-data response with
    /// binary parts: it begins the driver over the held body, parses the typed body
    /// from the non-binary parts, and hands back wire-order part handles.
    /// </summary>
    private void EmitMultipartResponseAccessors(IndentedWriter w, OperationInfo op)
    {
        foreach (ResponseInfo resp in op.Responses)
        {
            if (resp.StatusCode == "default"
                || resp.Content.Any(c => c.ItemSchemaPointer is not null)
                || !IsMultipartResponseWithBinaryParts(resp)
                || this.ResolveMultipartResponseTypeName(resp) is not { } bodyTypeName)
            {
                continue;
            }

            string accessorName = CodeEmitHelpers.StatusCodeToName(resp.StatusCode);
            string viewName = $"{op.CSharpMethodName()}{accessorName}Multipart";

            w.WriteLine();
            string namesLiteral = string.Join(", ", resp.BinaryParts.Select(p => CodeEmitHelpers.FormatStringLiteral(p.PropertyName)));
            w.WriteLine($"private static readonly string[] {accessorName}MultipartPartNames = [{namesLiteral}];");
            w.WriteLine();
            w.WriteLine("/// <summary>");
            w.WriteLine($"/// Reads the {resp.StatusCode} multipart response body. The typed body parses from");
            w.WriteLine("/// the non-binary parts, and each binary part arrives as a handle that streams its");
            w.WriteLine("/// bytes from the live response, in wire order. The body can be read once; handles");
            w.WriteLine("/// are valid until the response is disposed.");
            w.WriteLine("/// </summary>");
            w.WriteLine("/// <param name=\"maxNonBinaryPartsLength\">The maximum total bytes of non-binary parts accumulated for the typed body.</param>");
            w.WriteLine("/// <param name=\"cancellationToken\">A cancellation token.</param>");
            w.WriteLine("/// <returns>The typed body and the binary part handles.</returns>");
            w.WriteLine($"public async ValueTask<{viewName}> Get{accessorName}MultipartAsync(long maxNonBinaryPartsLength = ApiServerOptions.DefaultMaxNonBinaryPartsLength, CancellationToken cancellationToken = default)");
            w.OpenBrace();
            w.WriteLine($"if (this.{accessorName}MultipartBody is not {{ }} multipartBody)");
            w.OpenBrace();
            w.WriteLine($"throw new InvalidOperationException(\"The response does not carry a {resp.StatusCode} multipart body.\");");
            w.CloseBrace();
            w.WriteLine();
            w.WriteLine($"MultipartStreamingDriver driver = await multipartBody.BeginAsync({accessorName}MultipartPartNames, maxNonBinaryPartsLength, cancellationToken).ConfigureAwait(false);");
            w.WriteLine($"ParsedJsonDocument<{bodyTypeName}> bodyDocument = ParsedJsonDocument<{bodyTypeName}>.Parse(driver.ProjectionUtf8Json);");
            w.WriteLine("multipartBody.TakeBodyDocument(bodyDocument);");
            w.WriteLine("return new()");
            w.OpenBrace();
            w.WriteLine("Body = bodyDocument.RootElement,");
            foreach (BinaryPropertyInfo part in resp.BinaryParts)
            {
                string propName = CodeEmitHelpers.ToPascalCase(part.PropertyName);
                string nameLiteral = CodeEmitHelpers.FormatStringLiteral(part.PropertyName);
                string requiredLiteral = part.IsRequired ? "true" : "false";
                w.WriteLine($"{propName} = driver.GetHandle({nameLiteral}, required: {requiredLiteral}),");
            }

            w.CloseBraceNoNewline().Write(";");
            w.WriteLine();
            w.CloseBrace();
        }
    }

    /// <summary>
    /// Emits the per-response view struct returned by the multipart accessor: the
    /// typed body plus a wire-order handle per binary part.
    /// </summary>
    private void EmitMultipartViewStructs(IndentedWriter w, OperationInfo op)
    {
        foreach (ResponseInfo resp in op.Responses)
        {
            if (resp.StatusCode == "default"
                || resp.Content.Any(c => c.ItemSchemaPointer is not null)
                || !IsMultipartResponseWithBinaryParts(resp)
                || this.ResolveMultipartResponseTypeName(resp) is not { } bodyTypeName)
            {
                continue;
            }

            string accessorName = CodeEmitHelpers.StatusCodeToName(resp.StatusCode);
            string viewName = $"{op.CSharpMethodName()}{accessorName}Multipart";

            w.WriteLine();
            w.WriteLine("/// <summary>");
            w.WriteLine($"/// The {resp.StatusCode} multipart body of the {op.CSharpMethodName()} operation: the typed body");
            w.WriteLine("/// plus a streaming handle per binary part. Open handles in wire order and consume");
            w.WriteLine("/// each before opening the next; they are valid until the response is disposed.");
            w.WriteLine("/// </summary>");
            w.WriteLine($"public readonly struct {viewName}");
            w.OpenBrace();
            w.WriteLine("/// <summary>Gets the typed body parsed from the non-binary parts.</summary>");
            w.WriteLine($"public {bodyTypeName} Body {{ get; init; }}");
            foreach (BinaryPropertyInfo part in resp.BinaryParts)
            {
                string propName = CodeEmitHelpers.ToPascalCase(part.PropertyName);
                w.WriteLine();
                w.WriteLine("/// <summary>");
                w.WriteLine($"/// Gets the streaming handle for the '{part.PropertyName}' part. Open it to consume the part's bytes from the wire, in wire order.");
                w.WriteLine("/// </summary>");
                w.WriteLine($"public BinaryPartHandle {propName} {{ get; init; }}");
            }

            w.CloseBrace();
        }
    }

    private void EmitLinksAccessor(
        IndentedWriter w,
        string responseStructName,
        OperationInfo op,
        List<OperationInfo> allOperations)
    {
        // Collect all links across all responses for this operation.
        List<LinkInfo> allLinks = [];
        foreach (ResponseInfo resp in op.Responses)
        {
            allLinks.AddRange(resp.Links);
        }

        if (allLinks.Count == 0)
        {
            return;
        }

        string accessorName = $"{responseStructName}LinksAccessor";

        // Emit the Links property on the response struct.
        w.WriteLine();
        w.WriteLine("/// <summary>");
        w.WriteLine("/// Gets the links accessor for navigating to related operations.");
        w.WriteLine("/// </summary>");
        w.WriteLine($"public {accessorName} Links => new(this);");

        // Emit the nested LinksAccessor readonly struct.
        w.WriteLine();
        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Provides navigable link methods for the <see cref=\"{responseStructName}\"/>.");
        w.WriteLine("/// </summary>");
        w.WriteLine($"public readonly struct {accessorName}");
        w.OpenBrace();

        w.WriteLine($"private readonly {responseStructName} response;");
        w.WriteLine();

        w.WriteLine($"internal {accessorName}({responseStructName} response)");
        w.OpenBrace();
        w.WriteLine("this.response = response;");
        w.CloseBrace();

        // Emit one method per link.
        foreach (LinkInfo link in allLinks)
        {
            this.EmitLinkMethod(w, link, op, allOperations);
        }

        w.CloseBrace();
    }

    private void EmitLinkMethod(
        IndentedWriter w,
        LinkInfo link,
        OperationInfo sourceOp,
        List<OperationInfo> allOperations)
    {
        // Resolve the target operation by operationId.
        OperationInfo? targetOp = null;
        foreach (OperationInfo candidate in allOperations)
        {
            if (string.Equals(candidate.OperationId, link.TargetOperationId, StringComparison.Ordinal))
            {
                targetOp = candidate;
                break;
            }
        }

        if (targetOp is null)
        {
            // Target operation not found (may have been excluded by filters).
            w.WriteLine();
            w.WriteLine($"// Link '{link.LinkName}' targets operationId '{link.TargetOperationId}' which is not available.");
            return;
        }

        OperationInfo target = targetOp.Value;
        string targetResponseType = $"{target.CSharpMethodName()}{GeneratedClientTypeNaming.ResponseSuffix}";
        string targetRequestType = $"{target.CSharpMethodName()}{GeneratedClientTypeNaming.RequestSuffix}";

        // Determine which target parameters are NOT satisfied by link bindings.
        HashSet<string> boundParams = new(StringComparer.OrdinalIgnoreCase);
        foreach (LinkParameterBinding binding in link.ParameterBindings)
        {
            boundParams.Add(binding.ParameterName);
        }

        // Build the method signature with unsatisfied required parameters.
        List<ParameterInfo> unsatisfiedParams = [];
        foreach (ParameterInfo param in target.Parameters)
        {
            if (!boundParams.Contains(param.Name))
            {
                unsatisfiedParams.Add(param);
            }
        }

        w.WriteLine();
        if (link.Description is not null)
        {
            w.WriteLine("/// <summary>");
            w.WriteLine($"/// {CodeEmitHelpers.EscapeXml(link.Description)}");
            w.WriteLine("/// </summary>");
        }

        // Build parameter list for the method.
        string methodName = $"{CodeEmitHelpers.SanitizeIdentifier(link.LinkName)}Async";
        w.Write($"public ValueTask<{targetResponseType}> {methodName}(");

        bool first = true;
        foreach (ParameterInfo param in unsatisfiedParams)
        {
            if (!first)
            {
                w.Write(", ");
            }

            string paramSourceTypeName = this.GetParameterSourceTypeName(param);
            string paramName = CodeEmitHelpers.SanitizeParameterName(param.Name);

            if (param.IsRequired)
            {
                w.Write($"{paramSourceTypeName} {paramName}");
            }
            else
            {
                w.Write($"{paramSourceTypeName} {paramName} = default");
            }

            first = false;
        }

        if (!first)
        {
            w.Write(", ");
        }

        w.Write("CancellationToken cancellationToken = default)");
        w.WriteLine();
        w.OpenBrace();

        // Emit the body: build the request, set bound parameters from runtime expressions.
        bool hasUnsatisfiedParams = unsatisfiedParams.Count > 0;
        if (hasUnsatisfiedParams)
        {
            w.WriteLine("JsonWorkspace workspace = JsonWorkspace.CreateUnrented();");
        }

        w.WriteLine($"{targetRequestType} request = new()");
        w.OpenBrace();

        // Set parameters from link bindings.
        foreach (LinkParameterBinding binding in link.ParameterBindings)
        {
            // Find the target parameter.
            ParameterInfo? targetParam = null;
            foreach (ParameterInfo p in target.Parameters)
            {
                if (string.Equals(p.Name, binding.ParameterName, StringComparison.OrdinalIgnoreCase))
                {
                    targetParam = p;
                    break;
                }
            }

            if (targetParam is null)
            {
                continue;
            }

            string paramTypeName = this.GetParameterTypeName(targetParam.Value);
            string propertyName = CodeEmitHelpers.SanitizeIdentifier(binding.ParameterName);

            RuntimeExpression expr = RuntimeExpression.Parse(binding.Expression);
            string valueExpression = EmitRuntimeExpressionValue(expr, paramTypeName, sourceOp, link);

            w.WriteLine($"{propertyName} = {valueExpression},");
        }

        // Set unsatisfied parameters from method arguments.
        foreach (ParameterInfo param in unsatisfiedParams)
        {
            string propertyName = CodeEmitHelpers.SanitizeIdentifier(param.Name);
            string paramName = CodeEmitHelpers.SanitizeParameterName(param.Name);
            string typeName = this.GetParameterTypeName(param);

            if (param.IsRequired)
            {
                w.WriteLine($"{propertyName} = ({typeName}){typeName}.CreateBuilder(workspace, {paramName}, 30).RootElement,");
            }
            else
            {
                string undefinedFallback = CodeEmitHelpers.FormatDefaultValueExpression(
                    typeName, param.DefaultValueJson, param.DefaultValueKind);
                w.WriteLine(
                    $"{propertyName} = {paramName}.IsUndefined ? {undefinedFallback} : " +
                    $"({typeName}){typeName}.CreateBuilder(workspace, {paramName}, 30).RootElement,");
            }
        }

        w.CloseBraceWithSemicolon();
        w.WriteLine();

        // Invoke the transport.
        w.WriteLine($"return this.response.transport!.SendAsync<{targetRequestType}, {targetResponseType}>(in request, cancellationToken);");

        w.CloseBrace();
    }

    private static string EmitRuntimeExpressionValue(
        RuntimeExpression expr,
        string targetTypeName,
        OperationInfo sourceOp,
        LinkInfo link)
    {
        switch (expr.Kind)
        {
            case RuntimeExpressionKind.ResponseBody:
                // Navigate the response body via typed property accessors.
                if (expr.JsonPointer?.StartsWith('/') == true)
                {
                    string bodyAccessorName = CodeEmitHelpers.StatusCodeToName(link.SourceStatusCode);
                    string access = $"this.response.{bodyAccessorName}Body";
                    access = AppendJsonPointerNavigation(access, expr.JsonPointer);
                    return $"{targetTypeName}.From({access})";
                }

                return $"default({targetTypeName})";

            case RuntimeExpressionKind.ResponseHeader:
                return $"{targetTypeName}.ParseValue(\"{expr.Name}\")";

            case RuntimeExpressionKind.RequestPath:
            case RuntimeExpressionKind.RequestQuery:
            case RuntimeExpressionKind.RequestHeader:
                // Access the stored source request property.
                string reqPropertyName = CodeEmitHelpers.SanitizeIdentifier(expr.Name!);
                return $"{targetTypeName}.From(this.response.sourceRequest.{reqPropertyName})";

            case RuntimeExpressionKind.RequestBody:
                // Navigate the stored source body via typed property accessors.
                if (expr.JsonPointer?.StartsWith('/') == true)
                {
                    string bodyAccess = "this.response.sourceBody";
                    bodyAccess = AppendJsonPointerNavigation(bodyAccess, expr.JsonPointer);
                    return $"{targetTypeName}.From({bodyAccess})";
                }

                return $"{targetTypeName}.From(this.response.sourceBody)";

            case RuntimeExpressionKind.Literal:
                return $"{targetTypeName}.ParseValue(\"\"\"{expr.LiteralValue}\"\"\")";

            default:
                // $url, $method require additional infrastructure.
                return $"default({targetTypeName}) /* unsupported: {expr.Kind} expression */";
        }
    }

    /// <summary>
    /// Appends property/indexer access expressions for each segment of a JSON Pointer.
    /// Numeric segments produce array indexer syntax (e.g., [0]), non-numeric segments
    /// produce PascalCase property access (e.g., .PropertyName).
    /// If the pointer references a path that doesn't exist on the generated type, the
    /// emitted code will fail to compile — this is intentional, as it surfaces spec
    /// authoring errors at build time.
    /// </summary>
    private static string AppendJsonPointerNavigation(string baseExpression, string jsonPointer)
    {
        string pointerPath = jsonPointer.Substring(1); // Remove leading '/'
        string[] segments = pointerPath.Split('/');

        string access = baseExpression;
        foreach (string rawSegment in segments)
        {
            // JSON Pointer escaping: ~1 → /, ~0 → ~
            string segment = rawSegment.Replace("~1", "/").Replace("~0", "~");

            if (int.TryParse(segment, out int index))
            {
                // Numeric segment → array indexer
                access += $"[{index}]";
            }
            else
            {
                // Named segment → typed property accessor
                access += $".{CodeEmitHelpers.SanitizeIdentifier(segment)}";
            }
        }

        return access;
    }

    private static bool HasRequestBasedExpressions(OperationInfo op)
    {
        foreach (ResponseInfo resp in op.Responses)
        {
            foreach (LinkInfo link in resp.Links)
            {
                foreach (LinkParameterBinding binding in link.ParameterBindings)
                {
                    RuntimeExpression expr = RuntimeExpression.Parse(binding.Expression);
                    if (expr.Kind is RuntimeExpressionKind.RequestPath
                        or RuntimeExpressionKind.RequestQuery
                        or RuntimeExpressionKind.RequestHeader
                        or RuntimeExpressionKind.RequestBody)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool HasRequestBodyExpressions(OperationInfo op)
    {
        foreach (ResponseInfo resp in op.Responses)
        {
            foreach (LinkInfo link in resp.Links)
            {
                foreach (LinkParameterBinding binding in link.ParameterBindings)
                {
                    RuntimeExpression expr = RuntimeExpression.Parse(binding.Expression);
                    if (expr.Kind is RuntimeExpressionKind.RequestBody)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void EmitCreateAsync(
        IndentedWriter w,
        string structName,
        ResponseInfo[] responses,
        bool hasLinks)
    {
        // Determine if any response needs async processing (JSON parsing is async; streams and text are not).
        // Streaming responses (itemSchema present) store the stream, so don't need async parsing.
        bool hasAnyJsonBody = responses.Any(
            r => r.Content.Any(c => CodeEmitHelpers.IsJsonMediaType(c.MediaType))
                 && this.ResolveResponseTypeName(r) is not null
                 && this.ResolveItemSchemaTypeName(r) is null);

        w.WriteLine("/// <inheritdoc/>");

        // If the only bodies are raw streams (no JSON parsing), we don't need async.
        if (hasAnyJsonBody)
        {
            w.WriteLine($"public static async ValueTask<{structName}> CreateAsync(");
        }
        else
        {
            w.WriteLine($"public static ValueTask<{structName}> CreateAsync(");
        }

        w.PushIndent();
        w.WriteLine("int statusCode,");
        w.WriteLine("Stream contentStream,");
        w.WriteLine("string? contentType = null,");
        w.WriteLine("IResponseHeaders? responseHeaders = null,");
        w.WriteLine("IAsyncDisposable? owner = null,");
        w.WriteLine("IApiTransport? transport = null,");
        w.WriteLine("CancellationToken cancellationToken = default)");
        w.PopIndent();
        w.OpenBrace();

        w.WriteLine($"{structName} response = default;");
        w.WriteLine("response.StatusCode = statusCode;");
        w.WriteLine("response.owner = owner;");

        if (hasLinks)
        {
            w.WriteLine("response.transport = transport;");
        }

        w.WriteLine();

        this.EmitReadResponseHeaders(w, responses);

        foreach (ResponseInfo resp in responses)
        {
            string accessorName = CodeEmitHelpers.StatusCodeToName(resp.StatusCode);

            if (resp.StatusCode == "default")
            {
                continue;
            }

            ContentCategory[] categories = GetDistinctContentCategories(resp);

            // Skip if the only category is JSON with no resolved type.
            if (categories.Length == 1 && categories[0] == ContentCategory.Json
                && this.ResolveResponseTypeName(resp) is null)
            {
                continue;
            }

            w.WriteLine($"if (statusCode == {resp.StatusCode})");
            w.OpenBrace();

            // If this response has itemSchema, store the stream for streaming enumeration.
            if (this.ResolveItemSchemaTypeName(resp) is not null)
            {
                w.WriteLine("response.itemStream = contentStream;");
            }
            else if (categories.Length == 1)
            {
                // Single content category — no Content-Type branching needed.
                this.EmitResponseBodyForCategory(w, categories[0], resp, accessorName);
            }
            else
            {
                // Multiple content categories — branch on Content-Type.
                this.EmitContentTypeBranching(w, resp, accessorName, categories);
            }

            if (hasAnyJsonBody)
            {
                w.WriteLine("return response;");
            }
            else
            {
                w.WriteLine("return ValueTask.FromResult(response);");
            }

            w.CloseBrace();
            w.WriteLine();
        }

        ResponseInfo? defaultResp = null;

        foreach (ResponseInfo resp in responses)
        {
            if (resp.StatusCode == "default")
            {
                defaultResp = resp;
                break;
            }
        }

        if (defaultResp is not null)
        {
            string accessorName = CodeEmitHelpers.StatusCodeToName("default");
            ContentCategory[] defaultCategories = GetDistinctContentCategories(defaultResp.Value);

            if (this.ResolveItemSchemaTypeName(defaultResp.Value) is not null)
            {
                w.WriteLine("response.itemStream = contentStream;");
            }
            else if (defaultCategories.Length == 1)
            {
                this.EmitResponseBodyForCategory(w, defaultCategories[0], defaultResp.Value, accessorName);
            }
            else if (defaultCategories.Length > 1)
            {
                this.EmitContentTypeBranching(w, defaultResp.Value, accessorName, defaultCategories);
            }
        }

        if (hasAnyJsonBody)
        {
            w.WriteLine("return response;");
        }
        else
        {
            w.WriteLine("return ValueTask.FromResult(response);");
        }

        w.CloseBrace();
    }

    private void EmitResponseBodyForCategory(
        IndentedWriter w,
        ContentCategory category,
        ResponseInfo resp,
        string accessorName)
    {
        switch (category)
        {
            case ContentCategory.TextPlain:
                CodeEmitHelpers.EmitTextPlainResponseBody(w, accessorName);
                break;
            case ContentCategory.OctetStream:
                CodeEmitHelpers.EmitStreamResponseBody(w, accessorName);
                break;
            case ContentCategory.Multipart:
                if (IsMultipartResponseWithBinaryParts(resp))
                {
                    // The body streams: hold it (with the headers that carry the
                    // boundary) until the accessor reads it.
                    w.WriteLine($"response.{accessorName}MultipartBody = new MultipartResponseBody(contentStream, responseHeaders);");
                }

                break;
            case ContentCategory.Json:
                string? typeName = this.ResolveResponseTypeName(resp);
                if (typeName is not null)
                {
                    CodeEmitHelpers.EmitParseResponseBody(w, typeName, accessorName);
                }

                break;
        }
    }

    private void EmitContentTypeBranching(
        IndentedWriter w,
        ResponseInfo resp,
        string accessorName,
        ContentCategory[] categories)
    {
        bool first = true;

        foreach (ContentCategory category in categories)
        {
            // Skip JSON if we can't resolve the type.
            if (category == ContentCategory.Json && this.ResolveResponseTypeName(resp) is null)
            {
                continue;
            }

            string condition = CodeEmitHelpers.ContentTypeCondition(category);

            if (first)
            {
                w.WriteLine($"if ({condition})");
                first = false;
            }
            else
            {
                w.WriteLine($"else if ({condition})");
            }

            w.OpenBrace();
            this.EmitResponseBodyForCategory(w, category, resp, accessorName);
            w.CloseBrace();
        }

        w.WriteLine();
    }

    private void EmitResponseHeaderProperties(
        IndentedWriter w,
        ResponseInfo[] responses)
    {
        HashSet<string> emittedHeaders = [];

        foreach (ResponseInfo resp in responses)
        {
            foreach (HeaderInfo header in resp.Headers)
            {
                string propertyName = CodeEmitHelpers.HeaderNameToPropertyName(header.HeaderName);

                if (!emittedHeaders.Add(propertyName))
                {
                    continue;
                }

                string? typeName = header.SchemaPointer is not null
                    ? this.ResolveSchemaTypeName(header.SchemaPointer)
                    : null;

                w.WriteLine();
                w.WriteLine("/// <summary>");
                w.WriteLine($"/// Gets the value of the <c>{header.HeaderName}</c> response header,");
                w.WriteLine("/// or <see langword=\"null\"/> if the header was not present.");
                w.WriteLine("/// </summary>");

                if (typeName is not null)
                {
                    string fieldName = CodeEmitHelpers.ToCamelCase(propertyName);

                    // Resolve the element/value type for deeply nested headers.
                    string? elementTypeName = null;
                    if (header.HasDeepNesting && header.SchemaPointer is not null)
                    {
                        string elementPointer = header.SerializationKind is ParameterSerializationKind.Array
                            ? header.SchemaPointer + "/items"
                            : header.SchemaPointer + "/additionalProperties";
                        elementTypeName = this.ResolveSchemaTypeName(elementPointer);
                    }

                    CodeEmitHelpers.EmitResponseHeaderLazyProperty(
                        w,
                        propertyName,
                        fieldName,
                        header.HeaderName,
                        typeName,
                        header.Explode,
                        header.SerializationKind,
                        header.ElementSerializationKind,
                        header.HasDeepNesting,
                        elementTypeName);
                }
                else
                {
                    w.WriteLine($"public string? {propertyName}Header {{ get; private set; }}");
                }
            }
        }
    }

    private void EmitStreamingItemMethods(IndentedWriter w, ResponseInfo[] responses)
    {
        foreach (ResponseInfo resp in responses)
        {
            string? itemTypeName = this.ResolveItemSchemaTypeName(resp);
            if (itemTypeName is null)
            {
                continue;
            }

            string accessorName = CodeEmitHelpers.StatusCodeToName(resp.StatusCode);

            // Raw items method (NDJSON/SSE — strips metadata, yields documents).
            w.WriteLine();
            w.WriteLine("/// <summary>");
            w.WriteLine($"/// Enumerates the streaming items from the {resp.StatusCode} response.");
            w.WriteLine("/// </summary>");
            w.WriteLine("/// <param name=\"cancellationToken\">A cancellation token.</param>");
            w.WriteLine($"/// <returns>An async enumerable of parsed documents containing <see cref=\"{itemTypeName}\"/> items. Each document must be disposed by the caller.</returns>");
            w.WriteLine("/// <remarks>");
            w.WriteLine("/// <para>The response stream is read line-by-line. Supports NDJSON and SSE formats.</para>");
            w.WriteLine($"/// <para>For SSE streams, use <see cref=\"Enumerate{accessorName}SseItems\"/> to access event metadata.</para>");
            w.WriteLine("/// <para>The response must not be disposed until enumeration is complete.");
            w.WriteLine("/// Each yielded document must be disposed when no longer needed.</para>");
            w.WriteLine("/// </remarks>");
            w.WriteLine($"public IAsyncEnumerable<ParsedJsonDocument<{itemTypeName}>> Enumerate{accessorName}Items(CancellationToken cancellationToken = default)");
            w.OpenBrace();
            w.WriteLine("if (this.itemStream is null)");
            w.OpenBrace();
            w.WriteLine("throw new InvalidOperationException(\"No streaming content is available.\");");
            w.CloseBrace();
            w.WriteLine();
            w.WriteLine($"return JsonStreamReader.ReadItemsAsync<{itemTypeName}>(this.itemStream, cancellationToken: cancellationToken);");
            w.CloseBrace();

            // SSE items method (preserves event metadata).
            w.WriteLine();
            w.WriteLine("/// <summary>");
            w.WriteLine($"/// Enumerates the streaming SSE events from the {resp.StatusCode} response,");
            w.WriteLine("/// including event metadata (event type, id, retry).");
            w.WriteLine("/// </summary>");
            w.WriteLine("/// <param name=\"cancellationToken\">A cancellation token.</param>");
            w.WriteLine($"/// <returns>An async enumerable of SSE events wrapping <see cref=\"{itemTypeName}\"/> items. Each event must be disposed by the caller.</returns>");
            w.WriteLine("/// <remarks>");
            w.WriteLine("/// <para>Use this method when consuming Server-Sent Events and you need access to");
            w.WriteLine("/// the event type, id, or retry metadata fields.</para>");
            w.WriteLine("/// <para>The response must not be disposed until enumeration is complete.");
            w.WriteLine("/// Each yielded event must be disposed when no longer needed.</para>");
            w.WriteLine("/// </remarks>");
            w.WriteLine($"public IAsyncEnumerable<SseEvent<{itemTypeName}>> Enumerate{accessorName}SseItems(CancellationToken cancellationToken = default)");
            w.OpenBrace();
            w.WriteLine("if (this.itemStream is null)");
            w.OpenBrace();
            w.WriteLine("throw new InvalidOperationException(\"No streaming content is available.\");");
            w.CloseBrace();
            w.WriteLine();
            w.WriteLine($"return JsonStreamReader.ReadSseItemsAsync<{itemTypeName}>(this.itemStream, cancellationToken: cancellationToken);");
            w.CloseBrace();
        }
    }

    private void EmitReadResponseHeaders(
        IndentedWriter w,
        ResponseInfo[] responses)
    {
        // Check if any response has headers at all.
        bool hasHeaders = false;

        foreach (ResponseInfo resp in responses)
        {
            if (resp.Headers.Length > 0)
            {
                hasHeaders = true;
                break;
            }
        }

        if (!hasHeaders)
        {
            return;
        }

        // Store the reference for lazy typed header parsing.
        bool hasTypedHeaders = responses.Any(
            r => r.Headers.Any(h => h.SchemaPointer is not null));
        if (hasTypedHeaders)
        {
            w.WriteLine("response.responseHeaders = responseHeaders;");
            w.WriteLine("response.workspace = JsonWorkspace.CreateUnrented();");
            w.WriteLine();
        }

        // For untyped headers (no schema), eagerly read the raw string value.
        HashSet<string> emittedHeaders = [];

        foreach (ResponseInfo resp in responses)
        {
            foreach (HeaderInfo header in resp.Headers)
            {
                string propertyName = CodeEmitHelpers.HeaderNameToPropertyName(header.HeaderName);

                if (!emittedHeaders.Add(propertyName))
                {
                    continue;
                }

                // Only eagerly read headers that have no schema (raw string properties).
                if (header.SchemaPointer is null)
                {
                    string camelName = CodeEmitHelpers.ToCamelCase(propertyName);
                    w.WriteLine("if (responseHeaders is not null)");
                    w.OpenBrace();
                    w.WriteLine($"if (responseHeaders.TryGetValue(\"{header.HeaderName}\", out string? {camelName}Value))");
                    w.OpenBrace();
                    w.WriteLine($"response.{propertyName}Header = {camelName}Value;");
                    w.CloseBrace();
                    w.CloseBrace();
                    w.WriteLine();
                }
            }
        }
    }

    private static void EmitTryGetMethod(
        IndentedWriter w,
        ResponseInfo resp,
        string accessorName,
        string methodSuffix,
        string typeName,
        string memberAccess,
        string paramDescription,
        List<string> specificStatusCodes,
        bool useNotNullWhen)
    {
        w.WriteLine();
        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Tries to get the {resp.StatusCode} {paramDescription}.");
        w.WriteLine("/// </summary>");
        w.WriteLine(
            $"/// <param name=\"result\">The {paramDescription} if the status matches.</param>");

        if (resp.StatusCode == "default")
        {
            w.WriteLine(
                "/// <returns><see langword=\"true\"/> if the status code does not match any specific response.</returns>");
        }
        else
        {
            w.WriteLine(
                $"/// <returns><see langword=\"true\"/> if the status code is {resp.StatusCode}.</returns>");
        }

        string notNullAttr = useNotNullWhen ? "[NotNullWhen(true)] " : string.Empty;
        w.WriteLine(
            $"public bool TryGet{accessorName}{methodSuffix}({notNullAttr}out {typeName} result)");
        w.OpenBrace();

        if (resp.StatusCode == "default")
        {
            if (specificStatusCodes.Count > 0)
            {
                string condition = string.Join(
                    " && ",
                    specificStatusCodes.Select(sc => $"this.StatusCode != {sc}"));
                w.WriteLine($"if ({condition})");
                w.OpenBrace();
                w.WriteLine($"result = {memberAccess};");
                w.WriteLine("return true;");
                w.CloseBrace();
                w.WriteLine();
                w.WriteLine("result = default;");
                w.WriteLine("return false;");
            }
            else
            {
                w.WriteLine($"result = {memberAccess};");
                w.WriteLine("return true;");
            }
        }
        else
        {
            w.WriteLine($"if (this.StatusCode == {resp.StatusCode})");
            w.OpenBrace();
            w.WriteLine($"result = {memberAccess};");
            w.WriteLine("return true;");
            w.CloseBrace();
            w.WriteLine();
            w.WriteLine("result = default;");
            w.WriteLine("return false;");
        }

        w.CloseBrace();
    }

    private void EmitMatchResult(
        IndentedWriter w,
        string structName,
        ResponseInfo[] responses,
        bool includeContext)
    {
        // Build the full matrix of (status code × content category) entries.
        List<(string statusCode, string paramName, string typeName, ContentCategory category)> typed = [];
        List<(string paramName, string typeName, ContentCategory category)> defaultEntries = [];

        foreach (ResponseInfo resp in responses)
        {
            string accessorName = CodeEmitHelpers.StatusCodeToName(resp.StatusCode);
            ContentCategory[] categories = GetDistinctContentCategories(resp);

            foreach (ContentCategory category in categories)
            {
                // Skip streaming responses in MatchResult — they use EnumerateXxxItems.
                if (resp.Content.Any(c => c.ItemSchemaPointer is not null))
                {
                    if (category == ContentCategory.Json || category == ContentCategory.TextPlain || category == ContentCategory.OctetStream)
                    {
                        continue;
                    }
                }

                string? jsonTypeName = category == ContentCategory.Json
                    ? this.ResolveResponseTypeName(resp)
                    : null;

                string typeName = CodeEmitHelpers.GetMatchTypeName(category, jsonTypeName);

                if (string.IsNullOrEmpty(typeName))
                {
                    continue;
                }

                string suffix = CodeEmitHelpers.MatchParamSuffix(category);
                string paramName = $"{accessorName}{suffix}";

                if (resp.StatusCode == "default")
                {
                    defaultEntries.Add((paramName, typeName, category));
                }
                else
                {
                    typed.Add((resp.StatusCode, paramName, typeName, category));
                }
            }
        }

        if (typed.Count == 0 && defaultEntries.Count == 0)
        {
            return;
        }

        w.WriteLine();
        w.WriteLine("/// <summary>");
        w.WriteLine("/// Matches the response against each status code and content type,");
        w.WriteLine("/// and calls the corresponding handler.");
        w.WriteLine("/// </summary>");

        if (includeContext)
        {
            w.WriteLine(
                "/// <typeparam name=\"TContext\">The type of the context to pass to the handler.</typeparam>");
        }

        w.WriteLine(
            "/// <typeparam name=\"TResult\">The type of the result returned by the handler.</typeparam>");

        if (includeContext)
        {
            w.WriteLine(
                "/// <param name=\"context\">The context to pass to the handler.</param>");
        }

        foreach (var (statusCode, paramName, _, _) in typed)
        {
            w.WriteLine(
                $"/// <param name=\"match{paramName}\">Handler for the {statusCode} response.</param>");
        }

        foreach (var (paramName, _, _) in defaultEntries)
        {
            w.WriteLine(
                $"/// <param name=\"match{paramName}\">Handler for a default response.</param>");
        }

        if (defaultEntries.Count == 0)
        {
            w.WriteLine(
                "/// <param name=\"matchDefault\">Handler for any unmatched status code.</param>");
        }

        w.WriteLine("/// <returns>The result of calling the matched handler.</returns>");

        if (includeContext)
        {
            w.Write($"public TResult MatchResult<TContext, TResult>(");
        }
        else
        {
            w.Write($"public TResult MatchResult<TResult>(");
        }

        w.WriteLine();
        w.PushIndent();

        if (includeContext)
        {
            w.WriteLine("in TContext context,");
        }

        foreach (var (_, paramName, typeName, _) in typed)
        {
            if (includeContext)
            {
                w.WriteLine($"ResponseMatcher<{typeName}, TContext, TResult> match{paramName},");
            }
            else
            {
                w.WriteLine($"ResponseMatcher<{typeName}, TResult> match{paramName},");
            }
        }

        // Default entries (from a spec-level default response).
        for (int i = 0; i < defaultEntries.Count; i++)
        {
            var (paramName, typeName, _) = defaultEntries[i];
            bool isLast = i == defaultEntries.Count - 1;
            string trailing = isLast ? ")" : ",";

            if (includeContext)
            {
                w.WriteLine($"ResponseMatcher<{typeName}, TContext, TResult> match{paramName}{trailing}");
            }
            else
            {
                w.WriteLine($"ResponseMatcher<{typeName}, TResult> match{paramName}{trailing}");
            }
        }

        // The unmatched fallback (when no spec default response covers it).
        if (defaultEntries.Count == 0)
        {
            if (includeContext)
            {
                w.WriteLine($"ResponseMatcher<int, TContext, TResult> matchDefault)");
            }
            else
            {
                w.WriteLine($"ResponseMatcher<int, TResult> matchDefault)");
            }
        }

        w.PopIndent();

        if (includeContext)
        {
            w.WriteLine("where TContext : allows ref struct");
        }

        w.OpenBrace();

        // Group typed entries by status code for the dispatch body.
        foreach (var group in typed.GroupBy(t => t.statusCode))
        {
            string statusCode = group.Key;
            var entries = group.ToList();

            w.WriteLine($"if (this.StatusCode == {statusCode})");
            w.OpenBrace();

            if (entries.Count == 1)
            {
                // Single content category — direct dispatch.
                EmitMatchCall(w, entries[0].paramName, entries[0].category,
                    CodeEmitHelpers.StatusCodeToName(statusCode), includeContext);
            }
            else
            {
                // Multiple content categories — branch on populated fields.
                EmitMultiCategoryMatchDispatch(w, entries, statusCode, includeContext);
            }

            w.CloseBrace();
            w.WriteLine();
        }

        if (defaultEntries.Count > 0)
        {
            if (defaultEntries.Count == 1)
            {
                EmitMatchCall(w, defaultEntries[0].paramName, defaultEntries[0].category,
                    "Default", includeContext);
            }
            else
            {
                EmitMultiCategoryDefaultMatchDispatch(w, defaultEntries, includeContext);
            }
        }
        else
        {
            if (includeContext)
            {
                w.WriteLine("return matchDefault(this.StatusCode, context);");
            }
            else
            {
                w.WriteLine("return matchDefault(this.StatusCode);");
            }
        }

        w.CloseBrace();
    }

    private static void EmitMatchCall(
        IndentedWriter w,
        string paramName,
        ContentCategory category,
        string accessorName,
        bool includeContext)
    {
        string member = CodeEmitHelpers.GetMemberNameForMatchResult(accessorName, category);

        if (includeContext)
        {
            w.WriteLine($"return match{paramName}(this.{member}, context);");
        }
        else
        {
            w.WriteLine($"return match{paramName}(this.{member});");
        }
    }

    private static void EmitMultiCategoryMatchDispatch(
        IndentedWriter w,
        List<(string statusCode, string paramName, string typeName, ContentCategory category)> entries,
        string statusCode,
        bool includeContext)
    {
        string accessorName = CodeEmitHelpers.StatusCodeToName(statusCode);

        // Emit if/else if chain for non-fallback categories, with JSON as fallback.
        ContentCategory? fallback = null;
        string? fallbackParamName = null;

        foreach (var entry in entries)
        {
            string? condition = CodeEmitHelpers.ContentCategoryDetectionCondition(
                accessorName, entry.category);

            if (condition is null)
            {
                // This is the fallback (JSON) — emit last.
                fallback = entry.category;
                fallbackParamName = entry.paramName;
                continue;
            }

            w.WriteLine($"if ({condition})");
            w.OpenBrace();
            EmitMatchCall(w, entry.paramName, entry.category, accessorName, includeContext);
            w.CloseBrace();
            w.WriteLine();
        }

        if (fallback is not null)
        {
            EmitMatchCall(w, fallbackParamName!, fallback.Value, accessorName, includeContext);
        }
    }

    private static void EmitMultiCategoryDefaultMatchDispatch(
        IndentedWriter w,
        List<(string paramName, string typeName, ContentCategory category)> entries,
        bool includeContext)
    {
        ContentCategory? fallback = null;
        string? fallbackParamName = null;

        foreach (var entry in entries)
        {
            string? condition = CodeEmitHelpers.ContentCategoryDetectionCondition(
                "Default", entry.category);

            if (condition is null)
            {
                fallback = entry.category;
                fallbackParamName = entry.paramName;
                continue;
            }

            w.WriteLine($"if ({condition})");
            w.OpenBrace();
            EmitMatchCall(w, entry.paramName, entry.category, "Default", includeContext);
            w.CloseBrace();
            w.WriteLine();
        }

        if (fallback is not null)
        {
            EmitMatchCall(w, fallbackParamName!, fallback.Value, "Default", includeContext);
        }
    }

    // ── Interface emission ──────────────────────────────────────────────
    private GeneratedFile EmitInterface(
        string clientName,
        IReadOnlyList<OperationInfo> operations,
        ServerInfo? serverInfo,
        string? documentSelf = null,
        SecuritySchemeInfo[]? securitySchemes = null)
    {
        IndentedWriter w = new();

        CodeEmitHelpers.EmitHeader(w);
        w.WriteLine($"namespace {this.rootNamespace};");
        w.WriteLine();

        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Client interface for the {clientName} API operations.");
        w.WriteLine("/// </summary>");
        w.WriteLine($"public interface I{clientName}Client : IAsyncDisposable");
        w.OpenBrace();

        if (documentSelf is not null)
        {
            w.WriteLine("/// <summary>");
            w.WriteLine("/// Gets the document identity URI (<c>$self</c>).");
            w.WriteLine("/// </summary>");
            w.WriteLine(
                $"static string DocumentIdentityUri => {CodeEmitHelpers.FormatStringLiteral(documentSelf)};");
            w.WriteLine();
        }

        if (serverInfo is { } si)
        {
            EmitCreateServerUri(w, si);
        }

        if (securitySchemes is { Length: > 0 })
        {
            CodeEmitHelpers.EmitSecuritySchemeMetadata(w, securitySchemes);
        }

        // Drift D5 (see the drift catalogue): the 2.0 generator never wrote the per-operation SecurityRequirements class.
        if (this.specVersion != "2.0")
        {
            CodeEmitHelpers.EmitSecurityRequirements(w, operations, securitySchemes);
        }

        for (int i = 0; i < operations.Count; i++)
        {
            if (i > 0)
            {
                w.WriteLine();
            }

            this.EmitInterfaceMethodSignature(w, operations[i]);

            if (HasRawStreamBody(operations[i]))
            {
                w.WriteLine();
                this.EmitInterfaceMethodSignature(w, operations[i], rawStreamAsWriter: true);
            }

            if (this.HasContextThreadedBody(operations[i]))
            {
                w.WriteLine();
                this.EmitInterfaceMethodSignature(w, operations[i], contextThreaded: true);
            }
        }

        w.CloseBrace();

        return new GeneratedFile($"I{clientName}Client.cs", w.ToString());
    }

    private static void EmitCreateServerUri(IndentedWriter w, ServerInfo serverInfo)
    {
        if (serverInfo.Variables.Length == 0)
        {
            // No variables — simple parameterless factory.
            string resolvedUrl = serverInfo.UrlTemplate;
            w.WriteLine("/// <summary>");
            w.WriteLine("/// Creates a <see cref=\"Uri\"/> for the default server.");
            w.WriteLine("/// </summary>");
            w.WriteLine("/// <returns>A <see cref=\"Uri\"/> for the server.</returns>");
            w.WriteLine(
                $"public static Uri CreateServerUri() => new({CodeEmitHelpers.FormatStringLiteral(resolvedUrl)});");
        }
        else
        {
            // Variables — emit parameters with defaults.
            w.WriteLine("/// <summary>");
            w.WriteLine("/// Creates a <see cref=\"Uri\"/> for the server, substituting");
            w.WriteLine("/// any server variables with the provided or default values.");
            w.WriteLine("/// </summary>");

            foreach (ServerVariableInfo v in serverInfo.Variables)
            {
                string paramName = CodeEmitHelpers.SanitizeParameterName(v.Name);
                w.Write($"/// <param name=\"{paramName}\">The {v.Name} variable.");
                if (v.AllowedValues is not null)
                {
                    w.Write($" Allowed: {string.Join(", ", v.AllowedValues)}.");
                }

                w.WriteLine("</param>");
            }

            w.WriteLine("/// <returns>A <see cref=\"Uri\"/> for the server.</returns>");

            // Build the parameter list.
            w.Write("public static Uri CreateServerUri(");
            for (int i = 0; i < serverInfo.Variables.Length; i++)
            {
                if (i > 0)
                {
                    w.Write(", ");
                }

                ServerVariableInfo v = serverInfo.Variables[i];
                string paramName = CodeEmitHelpers.SanitizeParameterName(v.Name);
                w.Write($"string {paramName} = {CodeEmitHelpers.FormatStringLiteral(v.DefaultValue)}");
            }

            w.Write(") => new($\"");

            // Build the interpolated string from the URL template, replacing
            // {varName} with {paramName}. We manually escape only backslash
            // and double-quote since the {paramName} parts must remain as
            // interpolation expressions (not escaped by FormatStringLiteral).
            string template = serverInfo.UrlTemplate;
            foreach (ServerVariableInfo v in serverInfo.Variables)
            {
                string paramName = CodeEmitHelpers.SanitizeParameterName(v.Name);
                template = template.Replace(
                    $"{{{v.Name}}}", $"{{{paramName}}}", StringComparison.Ordinal);
            }

            w.Write(SymbolDisplay.FormatLiteral(template, false));
            w.WriteLine("\");");
        }

        w.WriteLine();
    }

    private static bool HasRawStreamBody(OperationInfo op)
        => op.RequestBody is { } rb
            && !IsMultipartMixedRequestBody(rb)
            && IsRawStreamRequestBody(rb);

    private void EmitInterfaceMethodSignature(IndentedWriter w, OperationInfo op, bool contextThreaded = false, bool rawStreamAsWriter = false)
    {
        string responseName = $"{op.CSharpMethodName()}{GeneratedClientTypeNaming.ResponseSuffix}";

        EmitMethodDoc(w, op);

        if (op.IsDeprecated)
        {
            w.WriteLine("[Obsolete(\"This operation is deprecated.\")]");
        }

        List<string> paramParts = this.BuildParameterList(op, contextThreaded, rawStreamAsWriter);
        string generic = contextThreaded ? "<TContext>" : string.Empty;
        w.WriteLine(
            $"ValueTask<{responseName}> {op.CSharpMethodName()}Async{generic}({string.Join(", ", paramParts)})" +
            (contextThreaded ? string.Empty : ";"));

        if (contextThreaded)
        {
            CodeEmitHelpers.EmitContextConstraint(w);
            w.WriteLine(";");
        }
    }

    // ── Implementation emission ─────────────────────────────────────────
    private GeneratedFile EmitImplementation(
        string clientName,
        IReadOnlyList<OperationInfo> operations)
    {
        IndentedWriter w = new();

        CodeEmitHelpers.EmitHeader(w);
        w.WriteLine($"namespace {this.rootNamespace};");
        w.WriteLine();

        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Client implementation for the {clientName} API operations.");
        w.WriteLine("/// </summary>");
        w.WriteLine($"public sealed class {clientName}Client : I{clientName}Client");
        w.OpenBrace();

        w.WriteLine("private readonly IApiTransport transport;");

        // Emit static readonly encoding dictionaries for all operations that need them.
        Dictionary<string, string> encodingFieldNames = EmitStaticEncodingFields(w, operations);

        w.WriteLine();

        w.WriteLine("/// <summary>");
        w.WriteLine(
            $"/// Initializes a new instance of the <see cref=\"{clientName}Client\"/> class.");
        w.WriteLine("/// </summary>");
        w.WriteLine(
            "/// <param name=\"transport\">The API transport to use for sending requests.</param>");
        w.WriteLine($"public {clientName}Client(IApiTransport transport)");
        w.OpenBrace();
        w.WriteLine(
            "this.transport = transport ?? throw new ArgumentNullException(nameof(transport));");
        w.CloseBrace();

        for (int i = 0; i < operations.Count; i++)
        {
            w.WriteLine();
            this.EmitClientMethod(w, operations[i], encodingFieldNames);

            // A raw-stream body also gets a write-callback overload, mirroring the
            // push model BinaryPartData already offers for multipart parts.
            if (HasRawStreamBody(operations[i]))
            {
                w.WriteLine();
                this.EmitClientMethod(w, operations[i], encodingFieldNames, rawStreamAsWriter: true);
            }

            // A body whose type carries a Source<TContext> also gets the closure-free, single-materialisation form,
            // matching what a server result factory already offers for a response body.
            if (this.HasContextThreadedBody(operations[i]))
            {
                w.WriteLine();
                this.EmitClientMethod(w, operations[i], encodingFieldNames, contextThreaded: true);
            }
        }

        w.WriteLine();
        w.WriteLine("/// <inheritdoc/>");
        w.WriteLine("public ValueTask DisposeAsync() => default;");

        // Determine which send helpers this client actually needs.
        bool needsSendAsync = false;
        bool needsSendWithBody = false;
        bool needsSendWithStreamBody = false;
        bool needsSendWithBodyWriter = false;

        foreach (OperationInfo op in operations)
        {
            bool hasBody = op.RequestBody is not null;
            bool isMultipartMixed = hasBody && IsMultipartMixedRequestBody(op.RequestBody!.Value);
            bool isRawStream = hasBody && !isMultipartMixed && IsRawStreamRequestBody(op.RequestBody!.Value);
            bool isFormUrlEncoded = hasBody && !isRawStream && !isMultipartMixed && IsFormUrlEncodedRequestBody(op.RequestBody!.Value);
            bool isMultipart = hasBody && !isRawStream && !isFormUrlEncoded && !isMultipartMixed && IsMultipartRequestBody(op.RequestBody!.Value);

            if (isRawStream)
            {
                needsSendWithStreamBody = true;

                // The write-callback overload rides the body-writer core.
                needsSendWithBodyWriter = true;
            }
            else if (isFormUrlEncoded || isMultipart || isMultipartMixed)
            {
                needsSendWithBodyWriter = true;
            }
            else if (hasBody)
            {
                needsSendWithBody = true;

                // An optional JSON body also emits a bodyless SendAsyncCore path for when the caller omits it.
                if (!op.RequestBody!.Value.IsRequired && !HasRequestBasedExpressions(op))
                {
                    needsSendAsync = true;
                }
            }
            else
            {
                needsSendAsync = true;
            }
        }

        w.WriteLine();
        CodeEmitHelpers.EmitSendAsyncCoreHelpers(w, needsSendAsync, needsSendWithBody, needsSendWithStreamBody, needsSendWithBodyWriter);

        w.CloseBrace();

        return new GeneratedFile($"{clientName}Client.cs", w.ToString());
    }

    private void EmitClientMethod(IndentedWriter w, OperationInfo op, Dictionary<string, string> encodingFieldNames, bool contextThreaded = false, bool rawStreamAsWriter = false)
    {
        string requestName = $"{op.CSharpMethodName()}{GeneratedClientTypeNaming.RequestSuffix}";
        string responseName = $"{op.CSharpMethodName()}{GeneratedClientTypeNaming.ResponseSuffix}";

        EmitMethodDoc(w, op);

        if (op.IsDeprecated)
        {
            w.WriteLine("[Obsolete(\"This operation is deprecated.\")]");
        }

        List<string> paramParts = this.BuildParameterList(op, contextThreaded, rawStreamAsWriter);

        bool hasRequestExprLinks = HasRequestBasedExpressions(op);

        bool isMultipartMixedBody = op.RequestBody is not null && IsMultipartMixedRequestBody(op.RequestBody!.Value);

        string generic = contextThreaded ? "<TContext>" : string.Empty;
        w.WriteLine(
            $"public ValueTask<{responseName}> {op.CSharpMethodName()}Async{generic}(" +
            $"{string.Join(", ", paramParts)})");

        if (contextThreaded)
        {
            CodeEmitHelpers.EmitContextConstraint(w);
        }

        w.OpenBrace();

        bool hasParams = op.Parameters.Length > 0;
        bool hasBody = op.RequestBody is not null;
        bool isRawStreamBody = hasBody && !isMultipartMixedBody && IsRawStreamRequestBody(op.RequestBody!.Value);
        bool isFormUrlEncodedBody = hasBody && !isRawStreamBody && !isMultipartMixedBody && IsFormUrlEncodedRequestBody(op.RequestBody!.Value);
        bool isMultipartBody = hasBody && !isRawStreamBody && !isFormUrlEncodedBody && !isMultipartMixedBody && IsMultipartRequestBody(op.RequestBody!.Value);

        bool hasRequestBodyExprLinks = hasRequestExprLinks && HasRequestBodyExpressions(op)
            && hasBody && !isRawStreamBody && !isMultipartMixedBody;

        w.WriteLine("JsonWorkspace workspace = JsonWorkspace.CreateUnrented();");

        // Multipart/mixed methods cannot be async because Source is a ref struct (CS4012).
        // Instead, we use a non-async method that returns the ValueTask from SendWithBodyWriterAsyncCore
        // directly. This is safe because:
        //
        // 1. The caller still gets a single awaiter — SendWithBodyWriterAsyncCore returns a ValueTask
        //    that is awaited exactly once by the consumer. No fire-and-forget.
        //
        // 2. The try/catch here guards only the synchronous setup (request construction, validation,
        //    builder materialisation). If any of that throws, workspace is disposed immediately.
        //
        // 3. Once we reach the return, ownership of workspace transfers to SendWithBodyWriterAsyncCore
        //    which disposes it in its own finally block — covering both success and async exceptions.
        //
        // 4. No "using" on builders — the workspace manages their memory. Premature disposal via
        //    "using" would invalidate values captured in the body-writer lambda before it executes.
        if (isMultipartMixedBody)
        {
            w.WriteLine("try");
            w.OpenBrace();
        }

        // An OPTIONAL JSON body the caller omitted (`body` is undefined) must go out as NO body — not as an empty
        // object — because the server treats the body as optional too. We materialise/validate/send under a runtime
        // `hasBodyValue` guard so an undefined Source is never built (a zero-member builder would fault) and the
        // request is sent bodyless. Scoped to plain JSON bodies without request-expression links (the common case);
        // required bodies and form/multipart bodies are unchanged.
        bool optionalJsonBody = hasBody && !isRawStreamBody && !isFormUrlEncodedBody && !isMultipartBody
            && !isMultipartMixedBody && !hasRequestExprLinks && !op.RequestBody!.Value.IsRequired;

        // Materialise body from Source → immutable typed element (JSON and form-urlencoded).
        // For multipart/mixed, parts are materialised individually below, not as a single body.
        string? bodyTypeName = null;
        if (hasBody && !isRawStreamBody && !isMultipartMixedBody)
        {
            bodyTypeName = this.ResolveRequestBodyTypeName(op.RequestBody!.Value);
            string bodyArg = contextThreaded ? "in body" : "body";
            if (optionalJsonBody)
            {
                w.WriteLine("bool hasBodyValue = !body.IsUndefined;");
                w.WriteLine(
                    $"{bodyTypeName} bodyValue = hasBodyValue ? {bodyTypeName}.CreateBuilder(workspace, {bodyArg}, 30).RootElement : default;");
            }
            else
            {
                w.WriteLine(
                    $"{bodyTypeName} bodyValue = {bodyTypeName}.CreateBuilder(workspace, {bodyArg}, 30).RootElement;");
            }
        }

        if (hasParams)
        {
            ParameterInfo[] requiredParams = op.Parameters.Where(p => p.IsRequired).ToArray();
            ParameterInfo[] optionalParams = op.Parameters.Where(p => !p.IsRequired).ToArray();

            foreach (ParameterInfo param in requiredParams)
            {
                string fieldName = CodeEmitHelpers.SanitizeIdentifier(param.Name);
                string paramIdentifier = CodeEmitHelpers.EscapeCSharpKeyword(
                    CodeEmitHelpers.SanitizeParameterName(param.Name));
                string typeName = this.ResolveSchemaTypeName(param.SchemaPointer);
                w.WriteLine(
                    $"{typeName} {fieldName}Value = {typeName}.CreateBuilder(workspace, {paramIdentifier}, 30).RootElement;");
            }

            string ctorArgs = string.Join(
                ", ",
                requiredParams.Select(p => $"{CodeEmitHelpers.SanitizeIdentifier(p.Name)}Value"));

            w.Write($"{requestName} request = new({ctorArgs})");

            if (optionalParams.Length > 0)
            {
                w.WriteLine();
                w.OpenBrace();

                foreach (ParameterInfo param in optionalParams)
                {
                    string fieldName = CodeEmitHelpers.SanitizeIdentifier(param.Name);
                    string paramIdentifier = CodeEmitHelpers.EscapeCSharpKeyword(
                        CodeEmitHelpers.SanitizeParameterName(param.Name));
                    string typeName = this.ResolveSchemaTypeName(param.SchemaPointer);

                    string undefinedFallback = CodeEmitHelpers.FormatDefaultValueExpression(
                        typeName, param.DefaultValueJson, param.DefaultValueKind);

                    w.WriteLine(
                        $"{fieldName} = {paramIdentifier}.IsUndefined ? {undefinedFallback} : " +
                        $"({typeName}){typeName}.CreateBuilder(workspace, {paramIdentifier}, 30).RootElement,");
                }

                w.CloseBrace().Write(";");
                w.WriteLine();
            }
            else
            {
                w.WriteLine(";");
            }
        }
        else
        {
            w.WriteLine($"{requestName} request = new();");
        }

        w.WriteLine();

        // Validate parameters.
        w.WriteLine("request.Validate(validationMode);");

        // Validate JSON body if present (skip text/plain, stream, and multipart/mixed bodies). An omitted optional
        // body is not validated — there is nothing to validate, and the request goes out bodyless.
        if (hasBody && !isRawStreamBody && !isMultipartMixedBody)
        {
            w.WriteLine();
            if (optionalJsonBody)
            {
                w.WriteLine("if (hasBodyValue)");
                w.OpenBrace();
            }

            w.WriteLine("if (validationMode == ValidationMode.Detailed)");
            w.OpenBrace();
            w.WriteLine("using JsonSchemaResultsCollector bodyCollector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);");
            w.WriteLine("if (!bodyValue.EvaluateSchema(bodyCollector))");
            w.OpenBrace();
            w.WriteLine("ThrowHelper.ThrowRequestBodyValidationFailed(SchemaValidationDetail.FormatResults(bodyCollector));");
            w.CloseBrace();
            w.CloseBrace();
            w.WriteLine("else if (validationMode != ValidationMode.None && !bodyValue.EvaluateSchema())");
            w.OpenBrace();
            w.WriteLine("ThrowHelper.ThrowRequestBodyValidationFailed();");
            w.CloseBrace();

            if (optionalJsonBody)
            {
                w.CloseBrace();
            }
        }

        w.WriteLine();

        if (isRawStreamBody)
        {
            // Find the actual content type string from the spec for the raw stream body.
            string streamContentType = op.RequestBody!.Value.Content
                .First(c => CodeEmitHelpers.IsRawStreamMediaType(c.MediaType)).MediaType;

            string streamSendCore = rawStreamAsWriter ? "SendWithBodyWriterAsyncCore" : "SendWithStreamBodyAsyncCore";
            if (hasRequestExprLinks)
            {
                w.WriteLine(
                    $"return CaptureRequestAsync(" +
                    $"{streamSendCore}<{requestName}, " +
                    $"{responseName}>(JsonWorkspace.CreateUnrented(), request, body, " +
                    $"{CodeEmitHelpers.FormatStringLiteral(streamContentType)}, responseValidationMode, cancellationToken), request, workspace);");
            }
            else
            {
                w.WriteLine(
                    $"return {streamSendCore}<{requestName}, " +
                    $"{responseName}>(workspace, request, body, " +
                    $"{CodeEmitHelpers.FormatStringLiteral(streamContentType)}, responseValidationMode, cancellationToken);");
            }
        }
        else if (isFormUrlEncodedBody)
        {
            string formEncodingsFieldName = encodingFieldNames.GetValueOrDefault(op.CSharpMethodName() + "_Form", string.Empty);

            if (formEncodingsFieldName.Length > 0)
            {
                if (hasRequestExprLinks)
                {
                    w.WriteLine(
                        $"return CaptureRequestAsync(" +
                        $"SendWithBodyWriterAsyncCore<{requestName}, " +
                        $"{responseName}>(JsonWorkspace.CreateUnrented(), request, " +
                        $"(stream, ct) => {{ FormUrlEncodedSerializer.Serialize(bodyValue, stream, {formEncodingsFieldName}); return default; }}, " +
                        $"\"application/x-www-form-urlencoded\", responseValidationMode, cancellationToken), request, {(hasRequestBodyExprLinks ? "bodyValue, " : "")}workspace);");
                }
                else
                {
                    w.WriteLine(
                        $"return SendWithBodyWriterAsyncCore<{requestName}, " +
                        $"{responseName}>(workspace, request, " +
                        $"(stream, ct) => {{ FormUrlEncodedSerializer.Serialize(bodyValue, stream, {formEncodingsFieldName}); return default; }}, " +
                        $"\"application/x-www-form-urlencoded\", responseValidationMode, cancellationToken);");
                }
            }
            else
            {
                if (hasRequestExprLinks)
                {
                    w.WriteLine(
                        $"return CaptureRequestAsync(" +
                        $"SendWithBodyWriterAsyncCore<{requestName}, " +
                        $"{responseName}>(JsonWorkspace.CreateUnrented(), request, " +
                        $"(stream, ct) => {{ FormUrlEncodedSerializer.Serialize(bodyValue, stream); return default; }}, " +
                        $"\"application/x-www-form-urlencoded\", responseValidationMode, cancellationToken), request, {(hasRequestBodyExprLinks ? "bodyValue, " : "")}workspace);");
                }
                else
                {
                    w.WriteLine(
                        $"return SendWithBodyWriterAsyncCore<{requestName}, " +
                        $"{responseName}>(workspace, request, " +
                        $"(stream, ct) => {{ FormUrlEncodedSerializer.Serialize(bodyValue, stream); return default; }}, " +
                        $"\"application/x-www-form-urlencoded\", responseValidationMode, cancellationToken);");
                }
            }
        }
        else if (isMultipartMixedBody)
        {
            EmitMultipartMixedBody(w, op, requestName, responseName);

            // Close the try block. The catch only fires for synchronous exceptions
            // (before SendWithBodyWriterAsyncCore is called). Once the async helper
            // takes ownership of workspace, it handles disposal in its own finally.
            w.CloseBrace();
            w.WriteLine("catch");
            w.OpenBrace();
            w.WriteLine("workspace.Dispose();");
            w.WriteLine("throw;");
            w.CloseBrace();
        }
        else if (isMultipartBody)
        {
            string multipartEncodingsFieldName = encodingFieldNames.GetValueOrDefault(op.CSharpMethodName() + "_Multipart", string.Empty);

            BinaryPropertyInfo[] binaryProps = op.RequestBody!.Value.BinaryProperties;
            bool hasBinaryParts = binaryProps.Length > 0;

            w.WriteLine("string boundary = MultipartFormDataSerializer.GenerateBoundary();");

            if (hasBinaryParts)
            {
                EmitBinaryPartsDictionary(w, binaryProps);
            }

            if (hasBinaryParts)
            {
                // Async path: binary parts use WriteContentAsync which is async.
                string encodingsArg = multipartEncodingsFieldName.Length > 0 ? $", {multipartEncodingsFieldName}" : ", null";
                string serializeAsyncArgs = $"bodyValue, stream, boundary{encodingsArg}, binaryParts, ct";

                if (hasRequestExprLinks)
                {
                    w.WriteLine(
                        $"return CaptureRequestAsync(" +
                        $"SendWithBodyWriterAsyncCore<{requestName}, " +
                        $"{responseName}>(JsonWorkspace.CreateUnrented(), request, " +
                        $"(stream, ct) => MultipartFormDataSerializer.SerializeAsync({serializeAsyncArgs}), " +
                        $"\"multipart/form-data; boundary=\" + boundary, responseValidationMode, cancellationToken), request, {(hasRequestBodyExprLinks ? "bodyValue, " : "")}workspace);");
                }
                else
                {
                    w.WriteLine(
                        $"return SendWithBodyWriterAsyncCore<{requestName}, " +
                        $"{responseName}>(workspace, request, " +
                        $"(stream, ct) => MultipartFormDataSerializer.SerializeAsync({serializeAsyncArgs}), " +
                        $"\"multipart/form-data; boundary=\" + boundary, responseValidationMode, cancellationToken);");
                }
            }
            else
            {
                // Sync path: no binary parts, wrap sync Serialize in async delegate.
                string serializeArgs = multipartEncodingsFieldName.Length > 0
                    ? $"bodyValue, stream, boundary, {multipartEncodingsFieldName}"
                    : "bodyValue, stream, boundary";

                if (hasRequestExprLinks)
                {
                    w.WriteLine(
                        $"return CaptureRequestAsync(" +
                        $"SendWithBodyWriterAsyncCore<{requestName}, " +
                        $"{responseName}>(JsonWorkspace.CreateUnrented(), request, " +
                        $"(stream, ct) => {{ MultipartFormDataSerializer.Serialize({serializeArgs}); return default; }}, " +
                        $"\"multipart/form-data; boundary=\" + boundary, responseValidationMode, cancellationToken), request, {(hasRequestBodyExprLinks ? "bodyValue, " : "")}workspace);");
                }
                else
                {
                    w.WriteLine(
                        $"return SendWithBodyWriterAsyncCore<{requestName}, " +
                        $"{responseName}>(workspace, request, " +
                        $"(stream, ct) => {{ MultipartFormDataSerializer.Serialize({serializeArgs}); return default; }}, " +
                        $"\"multipart/form-data; boundary=\" + boundary, responseValidationMode, cancellationToken);");
                }
            }
        }
        else if (hasBody)
        {
            if (hasRequestExprLinks)
            {
                w.WriteLine(
                    $"return CaptureRequestAsync(" +
                    $"SendWithBodyAsyncCore<{requestName}, {bodyTypeName}, " +
                    $"{responseName}>(JsonWorkspace.CreateUnrented(), request, bodyValue, responseValidationMode, cancellationToken), request, {(hasRequestBodyExprLinks ? "bodyValue, " : "")}workspace);");
            }
            else if (optionalJsonBody)
            {
                // The caller supplied a body → send it; otherwise send the request bodyless (the server's
                // optional-body path). This is the client counterpart to the optional request-body server fix.
                w.WriteLine("if (hasBodyValue)");
                w.OpenBrace();
                w.WriteLine(
                    $"return SendWithBodyAsyncCore<{requestName}, {bodyTypeName}, " +
                    $"{responseName}>(workspace, request, bodyValue, responseValidationMode, cancellationToken);");
                w.CloseBrace();
                w.WriteLine();
                w.WriteLine(
                    $"return SendAsyncCore<{requestName}, " +
                    $"{responseName}>(workspace, request, responseValidationMode, cancellationToken);");
            }
            else
            {
                w.WriteLine(
                    $"return SendWithBodyAsyncCore<{requestName}, {bodyTypeName}, " +
                    $"{responseName}>(workspace, request, bodyValue, responseValidationMode, cancellationToken);");
            }
        }
        else
        {
            if (hasRequestExprLinks)
            {
                w.WriteLine(
                    $"return CaptureRequestAsync(" +
                    $"SendAsyncCore<{requestName}, " +
                    $"{responseName}>(JsonWorkspace.CreateUnrented(), request, responseValidationMode, cancellationToken), request, workspace);");
            }
            else
            {
                w.WriteLine(
                    $"return SendAsyncCore<{requestName}, " +
                    $"{responseName}>(workspace, request, responseValidationMode, cancellationToken);");
            }
        }

        if (hasRequestExprLinks)
        {
            w.WriteLine();

            if (hasRequestBodyExprLinks)
            {
                w.WriteLine(
                    $"static async ValueTask<{responseName}> CaptureRequestAsync(" +
                    $"ValueTask<{responseName}> sendTask, {requestName} request, {bodyTypeName} bodyValue, JsonWorkspace workspace)");
                w.OpenBrace();
                w.WriteLine($"{responseName} response = await sendTask;");
                w.WriteLine("response.sourceRequest = request;");
                w.WriteLine("response.sourceBody = bodyValue;");
                w.WriteLine("response.sourceWorkspace = workspace;");
                w.WriteLine("return response;");
                w.CloseBrace();
            }
            else
            {
                w.WriteLine(
                    $"static async ValueTask<{responseName}> CaptureRequestAsync(" +
                    $"ValueTask<{responseName}> sendTask, {requestName} request, JsonWorkspace workspace)");
                w.OpenBrace();
                w.WriteLine($"{responseName} response = await sendTask;");
                w.WriteLine("response.sourceRequest = request;");
                w.WriteLine("response.sourceWorkspace = workspace;");
                w.WriteLine("return response;");
                w.CloseBrace();
            }
        }

        w.CloseBrace();
    }

    private Dictionary<string, string> EmitStaticEncodingFields(
        IndentedWriter w,
        IReadOnlyList<OperationInfo> operations)
    {
        Dictionary<string, string> fieldNames = [];

        foreach (OperationInfo op in operations)
        {
            if (op.RequestBody is null)
            {
                continue;
            }

            bool isMultipartMixed = IsMultipartMixedRequestBody(op.RequestBody!.Value);
            bool isRawStream = !isMultipartMixed && IsRawStreamRequestBody(op.RequestBody!.Value);
            bool isFormUrlEncoded = !isRawStream && !isMultipartMixed && IsFormUrlEncodedRequestBody(op.RequestBody!.Value);
            bool isMultipart = !isRawStream && !isFormUrlEncoded && !isMultipartMixed && IsMultipartRequestBody(op.RequestBody!.Value);

            if (isFormUrlEncoded)
            {
                IReadOnlyDictionary<string, ContentEncodingInfo>? encodings =
                    GetRequestBodyEncodings(op.RequestBody!.Value, CodeEmitHelpers.IsFormUrlEncodedMediaType);

                if (encodings is { Count: > 0 })
                {
                    string fieldName = $"{op.CSharpMethodName()}Encodings";
                    fieldNames[op.CSharpMethodName() + "_Form"] = fieldName;
                    w.WriteLine();
                    CodeEmitHelpers.EmitEncodingsField(w, fieldName, encodings);
                }
            }

            if (isMultipart)
            {
                IReadOnlyDictionary<string, ContentEncodingInfo>? encodings =
                    GetRequestBodyEncodings(op.RequestBody!.Value, CodeEmitHelpers.IsMultipartMediaType);

                if (encodings is { Count: > 0 })
                {
                    string fieldName = $"{op.CSharpMethodName()}MultipartEncodings";
                    fieldNames[op.CSharpMethodName() + "_Multipart"] = fieldName;
                    w.WriteLine();
                    CodeEmitHelpers.EmitEncodingsField(w, fieldName, encodings);
                }
            }
        }

        return fieldNames;
    }

    private static void EmitBinaryPartsDictionary(
        IndentedWriter w,
        BinaryPropertyInfo[] binaryProperties)
    {
        w.WriteLine("Dictionary<string, BinaryPartData> binaryParts = new(StringComparer.Ordinal)");
        w.WriteLine("{");
        w.PushIndent();

        foreach (BinaryPropertyInfo prop in binaryProperties)
        {
            string propNameLiteral = CodeEmitHelpers.FormatStringLiteral(prop.PropertyName);
            if (prop.ContentType is { } specContentType)
            {
                // The spec declares a content type for this part: substitute it when the
                // caller left BinaryPartData.ContentType unset.
                string contentTypeLiteral = CodeEmitHelpers.FormatStringLiteral(specContentType);
                w.WriteLine($"[{propNameLiteral}] = {prop.CSharpParameterName()}.ContentType is null ? {prop.CSharpParameterName()} with {{ ContentType = {contentTypeLiteral} }} : {prop.CSharpParameterName()},");
            }
            else
            {
                w.WriteLine($"[{propNameLiteral}] = {prop.CSharpParameterName()},");
            }
        }

        w.PopIndent();
        w.WriteLine("};");
    }

    private static void EmitMethodDoc(IndentedWriter w, OperationInfo op)
    {
        if (op.Summary is not null)
        {
            w.WriteLine("/// <summary>");
            w.WriteLine($"/// {CodeEmitHelpers.EscapeXml(op.Summary)}");
            w.WriteLine("/// </summary>");
        }

        if (op.Description is not null)
        {
            w.WriteLine("/// <remarks>");
            w.WriteLine($"/// {CodeEmitHelpers.EscapeXml(op.Description)}");
            w.WriteLine("/// </remarks>");
        }

        foreach (ParameterInfo param in op.Parameters)
        {
            string sanitizedParam = CodeEmitHelpers.SanitizeParameterName(param.Name);
            string defaultNote = param.DefaultValueJson is not null
                ? $" Default: {CodeEmitHelpers.EscapeXml(param.DefaultValueJson)}."
                : string.Empty;
            w.WriteLine($"/// <param name=\"{sanitizedParam}\">The {param.Name} parameter.{defaultNote}</param>");
        }

        if (op.RequestBody is { } rb)
        {
            string bodyDesc = rb.Description ?? "The request body.";
            w.WriteLine($"/// <param name=\"body\">{CodeEmitHelpers.EscapeXml(bodyDesc)}.</param>");

            foreach (BinaryPropertyInfo binaryProp in rb.BinaryProperties)
            {
                w.WriteLine($"/// <param name=\"{binaryProp.CSharpParameterName()}\">Binary data for the '{binaryProp.PropertyName}' part.</param>");
            }
        }

        w.WriteLine("/// <param name=\"cancellationToken\">A cancellation token.</param>");
        w.WriteLine("/// <param name=\"validationMode\">The validation mode applied to the request before it is sent.</param>");
        w.WriteLine("/// <param name=\"responseValidationMode\">The validation mode applied to the response body.</param>");
    }

    private List<string> BuildParameterList(OperationInfo op, bool contextThreaded = false, bool rawStreamAsWriter = false)
    {
        List<string> paramParts = [];

        // Required parameters first.
        foreach (ParameterInfo param in op.Parameters.Where(p => p.IsRequired))
        {
            string typeName = this.GetParameterSourceTypeName(param);
            string paramIdentifier = CodeEmitHelpers.EscapeCSharpKeyword(
                CodeEmitHelpers.SanitizeParameterName(param.Name));

            paramParts.Add($"{typeName} {paramIdentifier}");
        }

        // Request body parameter.
        if (op.RequestBody is not null)
        {
            bool bodyRequired = op.RequestBody.Value.IsRequired;

            if (IsMultipartMixedRequestBody(op.RequestBody.Value))
            {
                // multipart/mixed with prefixEncoding: one parameter per position.
                if (op.RequestBody.Value.PrefixParts is { } prefixParts)
                {
                    for (int i = 0; i < prefixParts.Length; i++)
                    {
                        MixedPartInfo part = prefixParts[i];
                        string paramName = $"part{i}";

                        if (part.IsBinary)
                        {
                            paramParts.Add($"BinaryPartData {paramName}");
                        }
                        else
                        {
                            string typeName = this.ResolveSchemaTypeName(part.SchemaPointer);
                            paramParts.Add($"{typeName}.Source {paramName}");
                        }
                    }
                }
                else if (op.RequestBody.Value.ItemPart is { } itemPart)
                {
                    // multipart/mixed with itemEncoding: collection of items.
                    if (itemPart.IsBinary)
                    {
                        paramParts.Add("IEnumerable<BinaryPartData> items");
                    }
                    else
                    {
                        string typeName = this.ResolveSchemaTypeName(itemPart.SchemaPointer);
                        paramParts.Add($"IEnumerable<{typeName}.Source> items");
                    }
                }
            }
            else if (IsRawStreamRequestBody(op.RequestBody.Value))
            {
                if (rawStreamAsWriter)
                {
                    // The write-callback overload requires the body even when the spec
                    // marks it optional; omission is served by the Stream overload.
                    paramParts.Add("Func<Stream, CancellationToken, ValueTask> body");
                }
                else
                {
                    string suffix = bodyRequired ? string.Empty : " = default";
                    paramParts.Add($"Stream body{suffix}");
                }
            }
            else
            {
                string bodyTypeName = this.ResolveRequestBodyTypeName(op.RequestBody.Value);
                string suffix = bodyRequired ? string.Empty : " = default";
                string sourceType = contextThreaded ? "Source<TContext>" : "Source";
                paramParts.Add($"{bodyTypeName}.{sourceType} body{suffix}");
            }
        }

        // Binary part parameters (for multipart/form-data file uploads).
        if (op.RequestBody is { } rbForBinary
            && rbForBinary.BinaryProperties.Length > 0
            && !IsMultipartMixedRequestBody(rbForBinary))
        {
            foreach (BinaryPropertyInfo binaryProp in rbForBinary.BinaryProperties)
            {
                paramParts.Add($"BinaryPartData {binaryProp.CSharpParameterName()}");
            }
        }

        // Optional parameters after all required ones.
        foreach (ParameterInfo param in op.Parameters.Where(p => !p.IsRequired))
        {
            string typeName = this.GetParameterSourceTypeName(param);
            string paramIdentifier = CodeEmitHelpers.EscapeCSharpKeyword(
                CodeEmitHelpers.SanitizeParameterName(param.Name));

            paramParts.Add($"{typeName} {paramIdentifier} = default");
        }

        paramParts.Add("CancellationToken cancellationToken = default");
        paramParts.Add("ValidationMode validationMode = ValidationMode.Basic");
        paramParts.Add("ValidationMode responseValidationMode = ValidationMode.None");

        return paramParts;
    }

    /// <summary>
    /// Emits the body of a multipart/mixed client method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The generated code materialises Source ref structs into typed values synchronously (via
    /// CreateBuilder on the workspace), then returns a <c>ValueTask</c> from
    /// <c>SendWithBodyWriterAsyncCore</c>. This is a non-async method — we cannot mark it async
    /// because Source parameters are ref structs (CS4012).
    /// </para>
    /// <para>
    /// Workspace lifetime: the try/catch emitted by the caller guards the synchronous setup. Once
    /// <c>SendWithBodyWriterAsyncCore</c> is called, it takes ownership of the workspace and
    /// disposes it in its own <c>finally</c> block. Builders are NOT disposed via <c>using</c>
    /// because the workspace manages their memory; premature disposal would invalidate values
    /// captured by the body-writer lambda.
    /// </para>
    /// </remarks>
    private void EmitMultipartMixedBody(
        IndentedWriter w,
        OperationInfo op,
        string requestName,
        string responseName)
    {
        RequestBodyInfo rb = op.RequestBody!.Value;

        w.WriteLine("Guid guid = Guid.NewGuid();");

        if (rb.PrefixParts is { } prefixParts)
        {
            // Materialise JSON prefix parts from Source → typed values.
            // The workspace manages builder lifetimes — no using on the builders.
            for (int i = 0; i < prefixParts.Length; i++)
            {
                MixedPartInfo part = prefixParts[i];
                if (!part.IsBinary)
                {
                    string typeName = this.ResolveSchemaTypeName(part.SchemaPointer);
                    w.WriteLine(
                        $"var part{i}Builder = {typeName}.CreateBuilder(workspace, part{i}, 30);");
                    w.WriteLine(
                        $"{typeName} part{i}Value = part{i}Builder.RootElement;");
                }
            }

            bool hasBinaryPrefix = Array.Exists(prefixParts, p => p.IsBinary);

            // Transfer workspace ownership to the async helper. From this point,
            // SendWithBodyWriterAsyncCore disposes workspace in its finally block.
            // The returned ValueTask is awaited exactly once by the caller — no leak.
            if (hasBinaryPrefix)
            {
                w.WriteLine(
                    $"return SendWithBodyWriterAsyncCore<{requestName}, " +
                    $"{responseName}>(workspace, request, async (stream, ct) =>");
                w.OpenBrace();

                for (int i = 0; i < prefixParts.Length; i++)
                {
                    MixedPartInfo part = prefixParts[i];

                    if (part.IsBinary)
                    {
                        w.WriteLine(
                            $"await MultipartMixedSerializer.WriteBinaryPartAsync(stream, guid, part{i}, ct).ConfigureAwait(false);");
                    }
                    else
                    {
                        w.WriteLine(
                            $"MultipartMixedSerializer.WriteJsonPart(stream, guid, part{i}Value);");
                    }
                }

                w.WriteLine("MultipartMixedSerializer.WriteClosingBoundary(stream, guid);");
                w.CloseBraceNoNewline();
                w.WriteLine(
                    ", MultipartMixedSerializer.GetContentType(guid), responseValidationMode, cancellationToken);");
            }
            else
            {
                w.WriteLine(
                    $"return SendWithBodyWriterAsyncCore<{requestName}, " +
                    $"{responseName}>(workspace, request, (stream, ct) =>");
                w.OpenBrace();

                for (int i = 0; i < prefixParts.Length; i++)
                {
                    w.WriteLine(
                        $"MultipartMixedSerializer.WriteJsonPart(stream, guid, part{i}Value);");
                }

                w.WriteLine("MultipartMixedSerializer.WriteClosingBoundary(stream, guid);");
                w.WriteLine("return default;");
                w.CloseBraceNoNewline();
                w.WriteLine(
                    ", MultipartMixedSerializer.GetContentType(guid), responseValidationMode, cancellationToken);");
            }
        }
        else if (rb.ItemPart is { } itemPart)
        {
            if (itemPart.IsBinary)
            {
                // Homogeneous binary batch — async.
                w.WriteLine(
                    $"return SendWithBodyWriterAsyncCore<{requestName}, " +
                    $"{responseName}>(workspace, request, async (stream, ct) =>");
                w.OpenBrace();
                w.WriteLine("foreach (BinaryPartData item in items)");
                w.OpenBrace();
                w.WriteLine(
                    "await MultipartMixedSerializer.WriteBinaryPartAsync(stream, guid, item, ct).ConfigureAwait(false);");
                w.CloseBrace();
                w.WriteLine("MultipartMixedSerializer.WriteClosingBoundary(stream, guid);");
                w.CloseBraceNoNewline();
                w.WriteLine(
                    ", MultipartMixedSerializer.GetContentType(guid), responseValidationMode, cancellationToken);");
            }
            else
            {
                // Homogeneous JSON batch — sync wrapper, workspace manages builder lifetimes.
                string typeName = this.ResolveSchemaTypeName(itemPart.SchemaPointer);
                w.WriteLine(
                    $"return SendWithBodyWriterAsyncCore<{requestName}, " +
                    $"{responseName}>(workspace, request, (stream, ct) =>");
                w.OpenBrace();
                w.WriteLine($"foreach ({typeName}.Source item in items)");
                w.OpenBrace();
                w.WriteLine(
                    $"var itemBuilder = {typeName}.CreateBuilder(workspace, item, 30);");
                w.WriteLine(
                    $"{typeName} itemValue = itemBuilder.RootElement;");
                w.WriteLine(
                    "MultipartMixedSerializer.WriteJsonPart(stream, guid, itemValue);");
                w.CloseBrace();
                w.WriteLine("MultipartMixedSerializer.WriteClosingBoundary(stream, guid);");
                w.WriteLine("return default;");
                w.CloseBraceNoNewline();
                w.WriteLine(
                    ", MultipartMixedSerializer.GetContentType(guid), responseValidationMode, cancellationToken);");
            }
        }
    }
}