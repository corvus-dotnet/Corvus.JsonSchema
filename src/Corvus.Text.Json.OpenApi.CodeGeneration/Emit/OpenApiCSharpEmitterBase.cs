// <copyright file="OpenApiCSharpEmitterBase.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The shared, version-neutral C# <see cref="IClientEmitter"/>: turns the shared intermediate
/// representation into C# client and server source files.
/// </summary>
/// <remarks>
/// <para>
/// This is the emit-phase counterpart of <see cref="OpenApiWalkerBase"/>. Every emit method here
/// references only the shared intermediate representation, the injected
/// <see cref="ISchemaTypeResolver"/>, and the shared text utilities (<see cref="CodeEmitHelpers"/>,
/// <see cref="IndentedWriter"/>, <see cref="SchemaPointerBuilder"/>); none of them touch the
/// strongly-typed version model. Because <see cref="OperationInfo"/> and its companions are the
/// OpenAPI 3.2 superset, the 3.2-only emit (streaming/SSE responses, multipart/mixed bodies,
/// custom HTTP methods, and the server-side metadata) is gated on the intermediate-representation
/// fields that signal those features (<see cref="ContentInfo.ItemSchemaPointer"/>,
/// <see cref="RequestBodyInfo.PrefixParts"/>/<see cref="RequestBodyInfo.ItemPart"/>,
/// <see cref="OperationInfo.CustomMethodName"/>, and the extracted security schemes). Those fields
/// are left at their defaults by the 3.0 and 3.1 walkers, so the gated emit is inert for those
/// versions and the generated output is byte-identical to the dedicated per-version emitters it
/// replaces.
/// </para>
/// <para>
/// The single piece of emit that needs the strongly-typed model is <see cref="PrepareContext"/>:
/// the OpenAPI 3.2 document-identity and security-scheme extraction reads the typed
/// <c>OpenApiDocument</c>. That is confined to the <see langword="virtual"/> hook overridden by the
/// thin per-version subclass that lives in the version project (where the typed model can be named);
/// the 3.0 and 3.1 subclasses use the default no-extraction implementation.
/// </para>
/// </remarks>
public abstract class OpenApiCSharpEmitterBase : IClientEmitter
{
    private readonly string rootNamespace;
    private readonly string? clientNamePrefix;
    private readonly bool ignoreEmptyFormUrlEncodedBody;
    private readonly ISchemaTypeResolver schemaTypeResolver;
    private readonly OpenApiWalkerBase walker;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiCSharpEmitterBase"/> class.
    /// </summary>
    /// <param name="rootNamespace">The root namespace for generated code.</param>
    /// <param name="clientNamePrefix">Optional prefix for client type names.</param>
    /// <param name="ignoreEmptyFormUrlEncodedBody">
    /// When <see langword="true"/>, form-urlencoded request bodies whose schema defines no
    /// properties are treated as if the body were absent.
    /// </param>
    /// <param name="schemaTypeResolver">The schema-type resolver.</param>
    /// <param name="walker">The walker (used for client-name synthesis).</param>
    protected OpenApiCSharpEmitterBase(
        string rootNamespace,
        string? clientNamePrefix,
        bool ignoreEmptyFormUrlEncodedBody,
        ISchemaTypeResolver schemaTypeResolver,
        OpenApiWalkerBase walker)
    {
        this.rootNamespace = rootNamespace;
        this.clientNamePrefix = clientNamePrefix;
        this.ignoreEmptyFormUrlEncodedBody = ignoreEmptyFormUrlEncodedBody;
        this.schemaTypeResolver = schemaTypeResolver;
        this.walker = walker;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The default implementation carries no document-level metadata beyond the spec root, resolver
    /// and root server. A version whose document-identity or security-scheme metadata is not present
    /// in the intermediate representation overrides this hook to extract it from its typed model.
    /// </remarks>
    public virtual ClientEmitContext PrepareContext(
        JsonElement specRoot,
        IOpenApiReferenceResolver referenceResolver,
        ServerInfo? rootServer)
        => new(specRoot, referenceResolver, rootServer);

    /// <inheritdoc/>
    public GeneratedFile EmitRequestModule(OperationInfo op)
        => this.EmitRequestStruct(op);

    /// <inheritdoc/>
    public GeneratedFile EmitResponseModule(OperationInfo op, IReadOnlyList<OperationInfo> allOperations)
        => this.EmitResponseStruct(op, [.. allOperations]);

    /// <inheritdoc/>
    public GeneratedFile EmitClientInterface(
        string tag,
        IReadOnlyList<OperationInfo> tagOperations,
        ClientEmitContext context)
        => this.EmitInterface(
            this.walker.GetClientName(tag),
            tagOperations,
            context.RootServer,
            context.DocumentSelf,
            context.SecuritySchemes);

    /// <inheritdoc/>
    public GeneratedFile EmitClientImplementation(
        string tag,
        IReadOnlyList<OperationInfo> tagOperations,
        ClientEmitContext context)
        => this.EmitImplementation(this.walker.GetClientName(tag), tagOperations);

    /// <inheritdoc/>
    public GeneratedFile? EmitServerParamsModule(OperationInfo op)
        => this.EmitServerOperationParams(op);

    /// <inheritdoc/>
    public GeneratedFile? EmitServerResultModule(OperationInfo op)
        => this.EmitServerOperationResult(op);

    /// <inheritdoc/>
    public GeneratedFile? EmitServerHandlerModule(
        string tag,
        IReadOnlyList<OperationInfo> tagOperations)
        => this.EmitServerHandlerInterface(this.GetHandlerName(tag), tagOperations);

    /// <inheritdoc/>
    public GeneratedFile? EmitServerRegistrationModule(
        IReadOnlyDictionary<string, List<OperationInfo>> groups,
        IReadOnlyList<OperationInfo> operations,
        bool isCallbackServer,
        ClientEmitContext context)
        => this.EmitServerEndpointRegistration(
            (Dictionary<string, List<OperationInfo>>)groups,
            context.SecuritySchemes,
            operations,
            isCallbackServer);

    private string GetParameterTypeName(ParameterInfo param)
    {
        string resolved = this.schemaTypeResolver.ResolveTypeName(param.SchemaPointer);
        return resolved;
    }

    private string GetParameterSourceTypeName(ParameterInfo param)
    {
        string resolved = this.schemaTypeResolver.ResolveTypeName(param.SchemaPointer);
        return $"{resolved}.Source";
    }

    private string ResolveRequestBodyTypeName(RequestBodyInfo requestBody)
    {
        foreach (ContentInfo content in requestBody.Content)
        {
            if (CodeEmitHelpers.IsJsonMediaType(content.MediaType)
                || CodeEmitHelpers.IsFormUrlEncodedMediaType(content.MediaType)
                || CodeEmitHelpers.IsMultipartMediaType(content.MediaType))
            {
                return this.schemaTypeResolver.ResolveTypeName(content.SchemaPointer);
            }
        }

        return "JsonElement";
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
    /// Returns <see langword="true"/> if the request body's primary content type
    /// is <c>application/x-www-form-urlencoded</c>.
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
    /// Returns the per-property encoding overrides for the form-urlencoded or
    /// multipart content entry, or <see langword="null"/> if none are defined.
    /// </summary>
    private static IReadOnlyDictionary<string, EncodingInfo>? GetRequestBodyEncodings(
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
                return this.schemaTypeResolver.ResolveTypeName(content.SchemaPointer);
            }
        }

        return null;
    }

    private string? ResolveItemSchemaTypeName(ResponseInfo resp)
    {
        foreach (ContentInfo content in resp.Content)
        {
            if (content.ItemSchemaPointer is not null)
            {
                return this.schemaTypeResolver.ResolveTypeName(content.ItemSchemaPointer);
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

    // ── Request struct emission ─────────────────────────────────────────
    private GeneratedFile EmitRequestStruct(OperationInfo op)
    {
        string structName = $"{op.MethodName}Request";
        IndentedWriter w = new();

        CodeEmitHelpers.EmitHeader(w);
        w.WriteLine($"namespace {this.rootNamespace};");
        w.WriteLine();

        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Request type for the {op.MethodName} operation.");
        w.WriteLine("/// </summary>");

        string? remarks = op.Description ?? op.Summary;
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
                .Select(c => (c.MediaType, c.SchemaPointer?.PositionalPointer)));

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
            bool allowReserved = matchingParam?.AllowReserved ?? false;

            CodeEmitHelpers.EmitPathParamWrite(w, paramName, $"this.{fieldName}", fieldName, kind, style, explode, allowReserved);
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
                CodeEmitHelpers.EmitQueryParamWrite(w, param.Name, $"this.{fieldName}", fieldName, kind, param.Style, param.Explode);
                w.CloseBrace();
                w.WriteLine();
            }
            else
            {
                CodeEmitHelpers.EmitQueryParamWrite(w, param.Name, $"this.{fieldName}", fieldName, kind, param.Style, param.Explode);
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
                CodeEmitHelpers.EmitHeaderParamWrite(w, $"this.{fieldName}", fieldName, kind, param.Style, param.Explode);
                w.CloseBrace();
                w.WriteLine();
            }
            else
            {
                CodeEmitHelpers.EmitHeaderParamWrite(w, $"this.{fieldName}", fieldName, kind, param.Style, param.Explode);
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
                CodeEmitHelpers.EmitCookieParamWrite(w, param.Name, $"this.{fieldName}", fieldName, kind, param.Style, param.Explode, effectiveAllowReserved);
                w.CloseBrace();
                w.WriteLine();
            }
            else
            {
                CodeEmitHelpers.EmitCookieParamWrite(w, param.Name, $"this.{fieldName}", fieldName, kind, param.Style, param.Explode, effectiveAllowReserved);
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
        string structName = $"{op.MethodName}Response";
        IndentedWriter w = new();

        CodeEmitHelpers.EmitHeader(w);
        w.WriteLine($"namespace {this.rootNamespace};");
        w.WriteLine();

        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Response type for the {op.MethodName} operation.");
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
            w.WriteLine($"internal {op.MethodName}Request sourceRequest;");
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

        w.WriteLine("if (this.owner is not null)");
        w.OpenBrace();
        w.WriteLine("await this.owner.DisposeAsync().ConfigureAwait(false);");
        w.CloseBrace();
        w.CloseBrace();

        // Emit the helper method for reading streams into rented buffers.
        if (hasTextBody)
        {
            CodeEmitHelpers.EmitReadStreamToRentedBufferHelper(w);
        }

        // Emit link accessor struct and property when the operation has links.
        if (hasLinks)
        {
            this.EmitLinksAccessor(w, structName, op, allOperations);
        }

        w.CloseBrace();

        return new GeneratedFile($"{structName}.cs", w.ToString());
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
        string targetResponseType = $"{target.MethodName}Response";
        string targetRequestType = $"{target.MethodName}Request";

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
            w.WriteLine($"/// {link.Description}");
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
                    ? this.schemaTypeResolver.ResolveTypeName(header.SchemaPointer)
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
                    if (header.HasDeepNesting && header.SchemaPointer is { } headerSchemaRef)
                    {
                        string elementPointer = header.SerializationKind is ParameterSerializationKind.Array
                            ? headerSchemaRef.PositionalPointer + "/items"
                            : headerSchemaRef.PositionalPointer + "/additionalProperties";
                        elementTypeName = this.schemaTypeResolver.ResolveElementTypeName(new SchemaRef(elementPointer));
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
            EmitSecuritySchemeMetadata(w, securitySchemes);
        }

        // Only the versions that extract security schemes (a non-null array) emit the
        // client-interface security-requirements metadata; the 3.0/3.1 emitters pass null and
        // never produce this nested class.
        if (securitySchemes is not null)
        {
            EmitSecurityRequirements(w, operations, securitySchemes);
        }

        for (int i = 0; i < operations.Count; i++)
        {
            if (i > 0)
            {
                w.WriteLine();
            }

            this.EmitInterfaceMethodSignature(w, operations[i]);
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

    private static void EmitSecuritySchemeMetadata(IndentedWriter w, SecuritySchemeInfo[] schemes)
    {
        w.WriteLine("/// <summary>");
        w.WriteLine("/// Security scheme metadata from the specification.");
        w.WriteLine("/// </summary>");
        w.WriteLine("public static class SecuritySchemes");
        w.OpenBrace();

        for (int i = 0; i < schemes.Length; i++)
        {
            if (i > 0)
            {
                w.WriteLine();
            }

            SecuritySchemeInfo scheme = schemes[i];
            string propName = CodeEmitHelpers.SanitizeIdentifier(scheme.SchemeName);

            EmitSecurityProperty(w, scheme, propName, "Name", scheme.SchemeName,
                $"Gets the name of the <c>{CodeEmitHelpers.EscapeXml(scheme.SchemeName)}</c> security scheme.");

            EmitSecurityProperty(w, scheme, propName, "Type", scheme.SchemeType,
                $"Gets the type of the <c>{CodeEmitHelpers.EscapeXml(scheme.SchemeName)}</c> security scheme.");

            if (scheme.HttpScheme is not null)
            {
                EmitSecurityProperty(w, scheme, propName, "Scheme", scheme.HttpScheme,
                    $"Gets the HTTP scheme for <c>{CodeEmitHelpers.EscapeXml(scheme.SchemeName)}</c>.");
            }

            if (scheme.ApiKeyName is not null)
            {
                EmitSecurityProperty(w, scheme, propName, "KeyName", scheme.ApiKeyName,
                    $"Gets the API key parameter name for <c>{CodeEmitHelpers.EscapeXml(scheme.SchemeName)}</c>.");
            }

            if (scheme.ApiKeyIn is not null)
            {
                EmitSecurityProperty(w, scheme, propName, "KeyLocation", scheme.ApiKeyIn,
                    $"Gets the API key location for <c>{CodeEmitHelpers.EscapeXml(scheme.SchemeName)}</c>.");
            }

            if (scheme.OpenIdConnectUrl is not null)
            {
                EmitSecurityProperty(w, scheme, propName, "OpenIdConnectUrl", scheme.OpenIdConnectUrl,
                    $"Gets the OpenID Connect discovery URL for <c>{CodeEmitHelpers.EscapeXml(scheme.SchemeName)}</c>.");
            }

            if (scheme.Oauth2MetadataUrl is not null)
            {
                EmitSecurityProperty(w, scheme, propName, "Oauth2MetadataUrl", scheme.Oauth2MetadataUrl,
                    $"Gets the OAuth2 metadata URL for <c>{CodeEmitHelpers.EscapeXml(scheme.SchemeName)}</c>.");
            }

            if (scheme.DeviceAuthorizationUrl is not null)
            {
                EmitSecurityProperty(w, scheme, propName, "DeviceAuthorizationUrl", scheme.DeviceAuthorizationUrl,
                    $"Gets the device authorization URL for <c>{CodeEmitHelpers.EscapeXml(scheme.SchemeName)}</c>.");
            }

            if (scheme.TokenUrl is not null)
            {
                EmitSecurityProperty(w, scheme, propName, "TokenUrl", scheme.TokenUrl,
                    $"Gets the token URL for <c>{CodeEmitHelpers.EscapeXml(scheme.SchemeName)}</c>.");
            }

            if (scheme.AuthorizationUrl is not null)
            {
                EmitSecurityProperty(w, scheme, propName, "AuthorizationUrl", scheme.AuthorizationUrl,
                    $"Gets the authorization URL for <c>{CodeEmitHelpers.EscapeXml(scheme.SchemeName)}</c>.");
            }

            if (scheme.AvailableScopes is { Length: > 0 })
            {
                w.WriteLine();
                EmitDeprecatedAttribute(w, scheme);
                w.WriteLine("/// <summary>");
                w.WriteLine($"/// Gets all available scopes for <c>{CodeEmitHelpers.EscapeXml(scheme.SchemeName)}</c>.");
                w.WriteLine("/// </summary>");
                string scopeArray = string.Join(", ", scheme.AvailableScopes.Select(s => CodeEmitHelpers.FormatStringLiteral(s)));
                w.WriteLine($"public static readonly string[] {propName}AvailableScopes = [{scopeArray}];");
            }
        }

        w.CloseBrace();
        w.WriteLine();
    }

    private static void EmitSecurityProperty(
        IndentedWriter w,
        SecuritySchemeInfo scheme,
        string propName,
        string suffix,
        string value,
        string docSummary)
    {
        w.WriteLine();
        w.WriteLine("/// <summary>");
        w.WriteLine($"/// {docSummary}");
        w.WriteLine("/// </summary>");
        if (scheme.IsDeprecated)
        {
            w.WriteLine("[Obsolete(\"This security scheme is deprecated.\")]");
        }

        w.WriteLine(
            $"public static string {propName}{suffix} => {CodeEmitHelpers.FormatStringLiteral(value)};");
    }

    private static void EmitDeprecatedAttribute(IndentedWriter w, SecuritySchemeInfo scheme)
    {
        if (scheme.IsDeprecated)
        {
            w.WriteLine("[Obsolete(\"This security scheme is deprecated.\")]");
        }
    }

    private static void EmitSecurityRequirements(
        IndentedWriter w,
        IReadOnlyList<OperationInfo> operations,
        SecuritySchemeInfo[]? securitySchemes)
    {
        // Collect all operation security requirements into per-operation arrays
        List<(string MethodName, string SchemePropName, string[] Scopes)> perOperationEntries = [];

        foreach (OperationInfo op in operations)
        {
            if (op.SecurityRequirements is not { Length: > 0 })
            {
                continue;
            }

            // Scope constants are collected across every alternative; the OR/AND grouping does not
            // affect which scopes the operation can request.
            foreach (OperationSecurityRequirementSet set in op.SecurityRequirements)
            {
                foreach (OperationSecurityRequirement req in set.Requirements)
                {
                    if (req.Scopes.Length == 0)
                    {
                        continue;
                    }

                    string schemePropName = CodeEmitHelpers.SanitizeIdentifier(req.SchemeName);
                    perOperationEntries.Add((op.MethodName, schemePropName, req.Scopes));
                }
            }
        }

        if (perOperationEntries.Count == 0)
        {
            return;
        }

        w.WriteLine("/// <summary>");
        w.WriteLine("/// Per-operation security requirements from the specification.");
        w.WriteLine("/// </summary>");
        w.WriteLine("public static class SecurityRequirements");
        w.OpenBrace();

        // Emit per-operation scope arrays
        bool first = true;
        foreach ((string methodName, string schemePropName, string[] scopes) in perOperationEntries)
        {
            if (!first)
            {
                w.WriteLine();
            }

            first = false;
            string scopeArray = string.Join(", ", scopes.Select(s => CodeEmitHelpers.FormatStringLiteral(s)));
            w.WriteLine("/// <summary>");
            w.WriteLine($"/// Gets the scopes required by <c>{CodeEmitHelpers.EscapeXml(methodName)}</c> for the <c>{CodeEmitHelpers.EscapeXml(schemePropName)}</c> scheme.");
            w.WriteLine("/// </summary>");
            w.WriteLine($"public static readonly string[] {methodName}{schemePropName}Scopes = [{scopeArray}];");
        }

        // Emit union scope arrays per scheme (deduplicated)
        Dictionary<string, SortedSet<string>> unionByScheme = new(StringComparer.Ordinal);
        foreach ((_, string schemePropName, string[] scopes) in perOperationEntries)
        {
            if (!unionByScheme.TryGetValue(schemePropName, out SortedSet<string>? unionScopes))
            {
                unionScopes = new SortedSet<string>(StringComparer.Ordinal);
                unionByScheme[schemePropName] = unionScopes;
            }

            foreach (string scope in scopes)
            {
                unionScopes.Add(scope);
            }
        }

        foreach ((string schemePropName, SortedSet<string> scopes) in unionByScheme)
        {
            w.WriteLine();
            string allScopeArray = string.Join(", ", scopes.Select(s => CodeEmitHelpers.FormatStringLiteral(s)));
            w.WriteLine("/// <summary>");
            w.WriteLine($"/// Gets all scopes required by any operation for the <c>{CodeEmitHelpers.EscapeXml(schemePropName)}</c> scheme.");
            w.WriteLine("/// </summary>");
            w.WriteLine($"public static readonly string[] All{schemePropName}Scopes = [{allScopeArray}];");
        }

        w.CloseBrace();
        w.WriteLine();
    }

    private void EmitInterfaceMethodSignature(IndentedWriter w, OperationInfo op)
    {
        string responseName = $"{op.MethodName}Response";

        EmitMethodDoc(w, op);

        if (op.IsDeprecated)
        {
            w.WriteLine("[Obsolete(\"This operation is deprecated.\")]");
        }

        List<string> paramParts = this.BuildParameterList(op);
        w.WriteLine(
            $"ValueTask<{responseName}> {op.MethodName}Async({string.Join(", ", paramParts)});");
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
            }
            else if (isFormUrlEncoded || isMultipart || isMultipartMixed)
            {
                needsSendWithBodyWriter = true;
            }
            else if (hasBody)
            {
                needsSendWithBody = true;
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

    private void EmitClientMethod(IndentedWriter w, OperationInfo op, Dictionary<string, string> encodingFieldNames)
    {
        string requestName = $"{op.MethodName}Request";
        string responseName = $"{op.MethodName}Response";

        EmitMethodDoc(w, op);

        if (op.IsDeprecated)
        {
            w.WriteLine("[Obsolete(\"This operation is deprecated.\")]");
        }

        List<string> paramParts = this.BuildParameterList(op);

        bool hasRequestExprLinks = HasRequestBasedExpressions(op);

        bool isMultipartMixedBody = op.RequestBody is not null && IsMultipartMixedRequestBody(op.RequestBody!.Value);

        w.WriteLine(
            $"public ValueTask<{responseName}> {op.MethodName}Async(" +
            $"{string.Join(", ", paramParts)})");

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

        // Materialise body from Source → immutable typed element (JSON and form-urlencoded).
        // For multipart/mixed, parts are materialised individually below, not as a single body.
        string? bodyTypeName = null;
        if (hasBody && !isRawStreamBody && !isMultipartMixedBody)
        {
            bodyTypeName = this.ResolveRequestBodyTypeName(op.RequestBody!.Value);
            w.WriteLine(
                $"{bodyTypeName} bodyValue = {bodyTypeName}.CreateBuilder(workspace, body, 30).RootElement;");
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
                string typeName = this.schemaTypeResolver.ResolveTypeName(param.SchemaPointer);
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
                    string typeName = this.schemaTypeResolver.ResolveTypeName(param.SchemaPointer);

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

        // Validate JSON body if present (skip text/plain, stream, and multipart/mixed bodies).
        if (hasBody && !isRawStreamBody && !isMultipartMixedBody)
        {
            w.WriteLine();
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
        }

        w.WriteLine();

        if (isRawStreamBody)
        {
            // Find the actual content type string from the spec for the raw stream body.
            string streamContentType = op.RequestBody!.Value.Content
                .First(c => CodeEmitHelpers.IsRawStreamMediaType(c.MediaType)).MediaType;

            if (hasRequestExprLinks)
            {
                w.WriteLine(
                    $"return CaptureRequestAsync(" +
                    $"SendWithStreamBodyAsyncCore<{requestName}, " +
                    $"{responseName}>(JsonWorkspace.CreateUnrented(), request, body, " +
                    $"{CodeEmitHelpers.FormatStringLiteral(streamContentType)}, responseValidationMode, cancellationToken), request, workspace);");
            }
            else
            {
                w.WriteLine(
                    $"return SendWithStreamBodyAsyncCore<{requestName}, " +
                    $"{responseName}>(workspace, request, body, " +
                    $"{CodeEmitHelpers.FormatStringLiteral(streamContentType)}, responseValidationMode, cancellationToken);");
            }
        }
        else if (isFormUrlEncodedBody)
        {
            string formEncodingsFieldName = encodingFieldNames.GetValueOrDefault(op.MethodName + "_Form", string.Empty);

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
            string multipartEncodingsFieldName = encodingFieldNames.GetValueOrDefault(op.MethodName + "_Multipart", string.Empty);

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
                IReadOnlyDictionary<string, EncodingInfo>? encodings =
                    GetRequestBodyEncodings(op.RequestBody!.Value, CodeEmitHelpers.IsFormUrlEncodedMediaType);

                if (encodings is { Count: > 0 })
                {
                    string fieldName = $"{op.MethodName}Encodings";
                    fieldNames[op.MethodName + "_Form"] = fieldName;
                    w.WriteLine();
                    EmitEncodingsField(w, fieldName, encodings);
                }
            }

            if (isMultipart)
            {
                IReadOnlyDictionary<string, EncodingInfo>? encodings =
                    GetRequestBodyEncodings(op.RequestBody!.Value, CodeEmitHelpers.IsMultipartMediaType);

                if (encodings is { Count: > 0 })
                {
                    string fieldName = $"{op.MethodName}MultipartEncodings";
                    fieldNames[op.MethodName + "_Multipart"] = fieldName;
                    w.WriteLine();
                    EmitEncodingsField(w, fieldName, encodings);
                }
            }
        }

        return fieldNames;
    }

    private static void EmitEncodingsField(
        IndentedWriter w,
        string fieldName,
        IReadOnlyDictionary<string, EncodingInfo> encodings)
    {
        w.WriteLine($"private static readonly Dictionary<string, PropertyEncoding> {fieldName} = new(StringComparer.Ordinal)");
        w.WriteLine("{");
        w.PushIndent();

        foreach (KeyValuePair<string, EncodingInfo> kvp in encodings)
        {
            string propNameLiteral = CodeEmitHelpers.FormatStringLiteral(kvp.Key);
            EncodingInfo enc = kvp.Value;

            List<string> args = [];

            if (enc.Style is not null)
            {
                args.Add($"Style: {CodeEmitHelpers.FormatStringLiteral(enc.Style)}");
            }

            if (enc.Explode is not null)
            {
                args.Add($"Explode: {(enc.Explode.Value ? "true" : "false")}");
            }

            if (enc.AllowReserved)
            {
                args.Add("AllowReserved: true");
            }

            if (enc.ContentType is not null)
            {
                args.Add($"ContentType: {CodeEmitHelpers.FormatStringLiteral(enc.ContentType)}");
            }

            string ctor = $"new({string.Join(", ", args)})";
            w.WriteLine($"[{propNameLiteral}] = {ctor},");
        }

        w.PopIndent();
        w.WriteLine("};");
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
            w.WriteLine($"[{propNameLiteral}] = {prop.ParameterName},");
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
                w.WriteLine($"/// <param name=\"{binaryProp.ParameterName}\">Binary data for the '{binaryProp.PropertyName}' part.</param>");
            }
        }

        w.WriteLine("/// <param name=\"cancellationToken\">A cancellation token.</param>");
    }

    private List<string> BuildParameterList(OperationInfo op)
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
                            string typeName = this.schemaTypeResolver.ResolveTypeName(part.SchemaPointer);
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
                        string typeName = this.schemaTypeResolver.ResolveTypeName(itemPart.SchemaPointer);
                        paramParts.Add($"IEnumerable<{typeName}.Source> items");
                    }
                }
            }
            else if (IsRawStreamRequestBody(op.RequestBody.Value))
            {
                string suffix = bodyRequired ? string.Empty : " = default";
                paramParts.Add($"Stream body{suffix}");
            }
            else
            {
                string bodyTypeName = this.ResolveRequestBodyTypeName(op.RequestBody.Value);
                string suffix = bodyRequired ? string.Empty : " = default";
                paramParts.Add($"{bodyTypeName}.Source body{suffix}");
            }
        }

        // Binary part parameters (for multipart/form-data file uploads).
        if (op.RequestBody is { } rbForBinary
            && rbForBinary.BinaryProperties.Length > 0
            && !IsMultipartMixedRequestBody(rbForBinary))
        {
            foreach (BinaryPropertyInfo binaryProp in rbForBinary.BinaryProperties)
            {
                paramParts.Add($"BinaryPartData {binaryProp.ParameterName}");
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
                    string typeName = this.schemaTypeResolver.ResolveTypeName(part.SchemaPointer);
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
                string typeName = this.schemaTypeResolver.ResolveTypeName(itemPart.SchemaPointer);
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

    private static string EscapeStringLiteral(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private string GetHandlerName(string tag)
    {
        string prefix = this.clientNamePrefix ?? "Api";
        string sanitized = CodeEmitHelpers.SanitizeIdentifier(tag);
        return $"{prefix}{sanitized}";
    }

    private GeneratedFile EmitServerHandlerInterface(
        string handlerName,
        IReadOnlyList<OperationInfo> operations)
    {
        string interfaceName = $"I{handlerName}Handler";
        IndentedWriter w = new();

        CodeEmitHelpers.EmitHeader(w);
        w.WriteLine($"namespace {this.rootNamespace};");
        w.WriteLine();

        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Handler interface for {handlerName} API operations.");
        w.WriteLine("/// Implement this interface to provide the server-side logic.");
        w.WriteLine("/// </summary>");
        w.WriteLine($"public interface {interfaceName}");
        w.OpenBrace();

        for (int i = 0; i < operations.Count; i++)
        {
            if (i > 0)
            {
                w.WriteLine();
            }

            OperationInfo op = operations[i];
            string paramsName = $"{op.MethodName}Params";
            string resultName = $"{op.MethodName}Result";

            w.WriteLine("/// <summary>");
            string httpMethod = op.Method.ToString().ToUpperInvariant();
            string summary = op.Summary is not null
                ? $"Handles {httpMethod} {op.PathTemplate} \u2014 {CodeEmitHelpers.EscapeXml(op.Summary)}"
                : $"Handles {httpMethod} {op.PathTemplate}.";
            w.WriteLine($"/// {summary}");
            w.WriteLine("/// </summary>");
            w.WriteLine($"/// <param name=\"parameters\">The operation parameters.</param>");
            w.WriteLine($"/// <param name=\"workspace\">The workspace for building response values.</param>");
            w.WriteLine($"/// <param name=\"cancellationToken\">A cancellation token.</param>");
            w.WriteLine($"/// <returns>The operation result.</returns>");

            if (op.IsDeprecated)
            {
                w.WriteLine("[Obsolete(\"This operation is deprecated.\")]");
            }

            w.WriteLine(
                $"ValueTask<{resultName}> Handle{op.MethodName}Async({paramsName} parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default);");
        }

        w.CloseBrace();

        return new GeneratedFile($"{interfaceName}.cs", w.ToString());
    }

    private GeneratedFile EmitServerOperationParams(OperationInfo op)
    {
        string structName = $"{op.MethodName}Params";
        IndentedWriter w = new();

        CodeEmitHelpers.EmitHeader(w);
        w.WriteLine($"namespace {this.rootNamespace};");
        w.WriteLine();

        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Parameters for the {op.MethodName} operation ({(op.CustomMethodName ?? op.Method.ToString()).ToUpperInvariant()} {op.PathTemplate}).");
        w.WriteLine("/// </summary>");

        string? remarks = op.Description ?? op.Summary;
        if (remarks is not null)
        {
            w.WriteLine($"/// <remarks>{CodeEmitHelpers.EscapeXml(remarks)}</remarks>");
        }

        w.WriteLine($"public readonly struct {structName}");
        w.OpenBrace();

        // Emit properties for each parameter
        foreach (ParameterInfo param in op.Parameters)
        {
            string fieldName = CodeEmitHelpers.SanitizeIdentifier(param.Name);
            string typeName = this.GetParameterTypeName(param);

            w.WriteLine();
            w.WriteLine("/// <summary>");
            w.WriteLine($"/// Gets the '{param.Name}' {param.Location.ToString().ToLowerInvariant()} parameter.");
            w.WriteLine("/// </summary>");
            w.WriteLine($"public {typeName} {fieldName} {{ get; init; }}");
        }

        // Emit Body property if operation has a request body
        if (op.RequestBody is { } rb)
        {
            bool isRawStream = IsRawStreamRequestBody(rb);
            string bodyTypeName = isRawStream ? "System.IO.Stream" : this.ResolveRequestBodyTypeName(rb);
            w.WriteLine();
            w.WriteLine("/// <summary>");
            string bodyDesc = rb.Description ?? "Gets the request body.";
            w.WriteLine($"/// {CodeEmitHelpers.EscapeXml(bodyDesc)}");
            w.WriteLine("/// </summary>");
            w.WriteLine($"public {bodyTypeName} Body {{ get; init; }}");
        }

        w.CloseBrace();

        return new GeneratedFile($"{structName}.cs", w.ToString());
    }

    private GeneratedFile EmitServerOperationResult(OperationInfo op)
    {
        string structName = $"{op.MethodName}Result";
        IndentedWriter w = new();

        CodeEmitHelpers.EmitHeader(w);
        w.WriteLine($"namespace {this.rootNamespace};");
        w.WriteLine();

        // Collect all distinct response headers across all responses.
        List<(HeaderInfo Header, string TypeName, string FieldName, string PropertyName)> allHeaders = [];
        HashSet<string> emittedHeaderNames = [];
        foreach (ResponseInfo resp in op.Responses)
        {
            foreach (HeaderInfo header in resp.Headers)
            {
                if (!emittedHeaderNames.Add(header.HeaderName))
                {
                    continue;
                }

                string propertyName = CodeEmitHelpers.HeaderNameToPropertyName(header.HeaderName);
                string fieldName = CodeEmitHelpers.ToCamelCase(propertyName);
                string? typeName = header.SchemaPointer is not null
                    ? this.schemaTypeResolver.ResolveTypeName(header.SchemaPointer)
                    : null;

                // All response headers are stored as JsonElement (already built via workspace).
                allHeaders.Add((header, typeName ?? "JsonElement", fieldName, propertyName));
            }
        }

        bool hasHeaders = allHeaders.Count > 0;
        bool hasStreamingResponses = op.Responses.Any(r => GetStreamingContent(r).Count > 0);
        string streamTypeName = $"{op.MethodName}Stream";
        string streamWriterDelegateName = $"{op.MethodName}StreamWriter";
        string streamWriterInvokerName = $"{op.MethodName}StreamWriterInvoker";

        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Result type for the {op.MethodName} operation.");
        w.WriteLine("/// </summary>");
        w.WriteLine($"public readonly struct {structName}");
        w.OpenBrace();

        // Private constructor — includes header parameters if any headers exist.
        if (hasHeaders)
        {
            w.Write($"private {structName}(int statusCode, JsonElement body, string? contentType");
            if (hasStreamingResponses)
            {
                w.Write($", {streamWriterInvokerName}? streamWriter = null, object? streamWriterContext = null");
            }

            foreach (var (_, typeName, fieldName, _) in allHeaders)
            {
                w.Write($", {typeName} {fieldName} = default");
            }

            w.WriteLine(")");
            w.OpenBrace();
            w.WriteLine("this.StatusCode = statusCode;");
            w.WriteLine("this.Body = body;");
            w.WriteLine("this.ContentType = contentType;");
            if (hasStreamingResponses)
            {
                w.WriteLine("this.streamWriter = streamWriter;");
                w.WriteLine("this.streamWriterContext = streamWriterContext;");
            }

            foreach (var (_, _, fieldName, propertyName) in allHeaders)
            {
                w.WriteLine($"this.{propertyName} = {fieldName};");
            }

            w.CloseBrace();
        }
        else
        {
            w.Write($"private {structName}(int statusCode, JsonElement body = default, string? contentType = null");
            if (hasStreamingResponses)
            {
                w.Write($", {streamWriterInvokerName}? streamWriter = null, object? streamWriterContext = null");
            }

            w.WriteLine(")");
            w.OpenBrace();
            w.WriteLine("this.StatusCode = statusCode;");
            w.WriteLine("this.Body = body;");
            w.WriteLine("this.ContentType = contentType;");
            if (hasStreamingResponses)
            {
                w.WriteLine("this.streamWriter = streamWriter;");
                w.WriteLine("this.streamWriterContext = streamWriterContext;");
            }

            w.CloseBrace();
        }

        w.WriteLine();
        if (hasStreamingResponses)
        {
            w.WriteLine($"private readonly {streamWriterInvokerName}? streamWriter;");
            w.WriteLine("private readonly object? streamWriterContext;");
            w.WriteLine();
        }

        w.WriteLine("/// <summary>Gets the HTTP status code.</summary>");
        w.WriteLine("public int StatusCode { get; }");
        w.WriteLine();
        w.WriteLine("/// <summary>Gets the response body.</summary>");
        w.WriteLine("public JsonElement Body { get; }");
        w.WriteLine();
        w.WriteLine("/// <summary>Gets the content type for the response body.</summary>");
        w.WriteLine("public string? ContentType { get; }");

        if (hasStreamingResponses)
        {
            w.WriteLine();
            w.WriteLine("/// <summary>Gets a value indicating whether this result has a streaming response body.</summary>");
            w.WriteLine("public bool HasStreamingBody => this.streamWriter is not null;");
        }

        // Header properties
        foreach (var (header, typeName, _, propertyName) in allHeaders)
        {
            w.WriteLine();
            w.WriteLine("/// <summary>");
            w.WriteLine($"/// Gets the value of the <c>{header.HeaderName}</c> response header.");
            w.WriteLine("/// </summary>");
            w.WriteLine($"public {typeName} {propertyName} {{ get; }}");
        }

        // Factory methods for each declared response
        foreach (ResponseInfo resp in op.Responses)
        {
            string factoryName = CodeEmitHelpers.StatusCodeToName(resp.StatusCode);
            string? typeName = this.ResolveResponseTypeName(resp);

            // Determine which headers this response defines.
            List<(HeaderInfo Header, string TypeName, string FieldName, string PropertyName)> respHeaders = [];
            foreach (HeaderInfo header in resp.Headers)
            {
                var match = allHeaders.Find(h => h.Header.HeaderName == header.HeaderName);
                respHeaders.Add(match);
            }

            w.WriteLine();

            if (resp.StatusCode == "default")
            {
                // Default response — takes a status code parameter
                w.WriteLine("/// <summary>");
                string defaultDesc = resp.Summary ?? "Creates a default error result.";
                w.WriteLine($"/// {CodeEmitHelpers.EscapeXml(defaultDesc)}");
                w.WriteLine("/// </summary>");

                this.EmitServerResultFactory(w, structName, factoryName, typeName, resp, respHeaders, resp.StatusCode, hasHeaders, streamTypeName, streamWriterDelegateName);
            }
            else
            {
                // Named status code
                w.WriteLine("/// <summary>");
                string desc = resp.Summary ?? $"Creates a {resp.StatusCode} {factoryName} result.";
                w.WriteLine($"/// {CodeEmitHelpers.EscapeXml(desc)}");
                w.WriteLine("/// </summary>");

                this.EmitServerResultFactory(w, structName, factoryName, typeName, resp, respHeaders, resp.StatusCode, hasHeaders, streamTypeName, streamWriterDelegateName);
            }
        }

        // ValidateBody method — validates the response body against the typed schema
        // for the current status code.
        w.WriteLine();
        w.WriteLine("/// <summary>");
        w.WriteLine("/// Validates the response body against the schema for the current status code.");
        w.WriteLine("/// </summary>");
        w.WriteLine("/// <returns><see langword=\"true\"/> if the body is valid or undefined; otherwise <see langword=\"false\"/>.</returns>");
        w.WriteLine("public bool ValidateBody()");
        w.OpenBrace();
        w.WriteLine("if (this.Body.IsUndefined()) return true;");

        // Emit switch arms for each response that has a typed body
        bool hasAnyTypedResponse = false;
        foreach (ResponseInfo resp in op.Responses)
        {
            string? typeName = this.ResolveResponseTypeName(resp);
            if (typeName is null || resp.StatusCode == "default")
            {
                continue;
            }

            if (!hasAnyTypedResponse)
            {
                w.WriteLine("return this.StatusCode switch");
                w.OpenBrace();
                hasAnyTypedResponse = true;
            }

            w.WriteLine($"{resp.StatusCode} => {typeName}.From(this.Body).EvaluateSchema(),");
        }

        if (hasAnyTypedResponse)
        {
            w.WriteLine("_ => true,");
            w.CloseBraceNoNewline().Write(";");
            w.WriteLine();
        }
        else
        {
            w.WriteLine("return true;");
        }

        w.CloseBrace();

        // WriteBody method
        w.WriteLine();
        w.WriteLine("/// <summary>");
        w.WriteLine("/// Writes the response body to the specified writer.");
        w.WriteLine("/// </summary>");
        w.WriteLine("/// <param name=\"writer\">The UTF-8 JSON writer.</param>");
        w.WriteLine("public void WriteBody(Utf8JsonWriter writer)");
        w.OpenBrace();
        w.WriteLine("if (!this.Body.IsUndefined())");
        w.OpenBrace();
        w.WriteLine("this.Body.WriteTo(writer);");
        w.CloseBrace();
        w.CloseBrace();

        // WriteResponseHeaders method (only if there are headers)
        if (hasHeaders)
        {
            w.WriteLine();
            w.WriteLine("/// <summary>");
            w.WriteLine("/// Writes the response headers using the specified callback.");
            w.WriteLine("/// </summary>");
            w.WriteLine("/// <typeparam name=\"TState\">The state type passed to the callback.</typeparam>");
            w.WriteLine("/// <param name=\"callback\">A callback that receives the header name and value.</param>");
            w.WriteLine("/// <param name=\"state\">State to pass to the callback.</param>");
            w.WriteLine("public void WriteResponseHeaders<TState>(HeaderCallback<TState> callback, TState state)");
            w.OpenBrace();

            foreach (var (header, _, _, propertyName) in allHeaders)
            {
                string uid = CodeEmitHelpers.SanitizeIdentifier(header.HeaderName);
                w.WriteLine($"if (!this.{propertyName}.IsUndefined())");
                w.OpenBrace();
                w.WriteLine($"ReadOnlySpan<byte> nameUtf8{uid} = \"{header.HeaderName}\"u8;");
                CodeEmitHelpers.EmitHeaderParamWrite(w, $"this.{propertyName}", uid, header.SerializationKind, ParameterStyle.Simple, header.Explode);
                w.CloseBrace();
                w.WriteLine();
            }

            w.CloseBrace();
        }

        if (hasStreamingResponses)
        {
            w.WriteLine();
            w.WriteLine("/// <summary>");
            w.WriteLine("/// Writes the streaming response body.");
            w.WriteLine("/// </summary>");
            w.WriteLine("/// <param name=\"writer\">The stream writer.</param>");
            w.WriteLine("/// <param name=\"cancellationToken\">The cancellation token.</param>");
            w.WriteLine("/// <returns>A value task that completes when the stream has been written.</returns>");
            w.WriteLine("public ValueTask WriteStreamAsync(JsonStreamWriter writer, CancellationToken cancellationToken = default)");
            w.OpenBrace();
            w.WriteLine("return this.streamWriter is null");
            w.PushIndent();
            w.WriteLine("? ValueTask.CompletedTask");
            w.WriteLine(": this.streamWriter(writer, this.streamWriterContext, cancellationToken);");
            w.PopIndent();
            w.CloseBrace();
            w.WriteLine();
            w.WriteLine($"private delegate ValueTask {streamWriterInvokerName}(JsonStreamWriter writer, object? context, CancellationToken cancellationToken);");
            w.WriteLine();
            w.WriteLine($"private sealed class {streamWriterDelegateName}Context<TContext>");
            w.OpenBrace();
            w.WriteLine($"public {streamWriterDelegateName}Context(TContext context, {streamWriterDelegateName}<TContext> writer)");
            w.OpenBrace();
            w.WriteLine("this.Context = context;");
            w.WriteLine("this.Writer = writer;");
            w.CloseBrace();
            w.WriteLine();
            w.WriteLine("public TContext Context { get; }");
            w.WriteLine();
            w.WriteLine($"public {streamWriterDelegateName}<TContext> Writer {{ get; }}");
            w.CloseBrace();
        }

        w.CloseBrace();

        if (hasStreamingResponses)
        {
            this.EmitServerStreamingTypes(w, op, streamTypeName, streamWriterDelegateName);
        }

        return new GeneratedFile($"{structName}.cs", w.ToString());
    }

    private void EmitServerStreamingTypes(
        IndentedWriter w,
        OperationInfo op,
        string streamTypeName,
        string streamWriterDelegateName)
    {
        List<string> itemTypeNames = [];
        foreach (ResponseInfo response in op.Responses)
        {
            foreach (ContentInfo content in GetStreamingContent(response))
            {
                if (content.ItemSchemaPointer is not null)
                {
                    string typeName = this.schemaTypeResolver.ResolveTypeName(content.ItemSchemaPointer);
                    if (!itemTypeNames.Contains(typeName))
                    {
                        itemTypeNames.Add(typeName);
                    }
                }
            }
        }

        w.WriteLine();
        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Writes items for the {op.MethodName} streaming response.");
        w.WriteLine("/// </summary>");
        w.WriteLine($"public readonly struct {streamTypeName}");
        w.OpenBrace();
        w.WriteLine("private readonly JsonStreamWriter writer;");
        w.WriteLine();
        w.WriteLine($"internal {streamTypeName}(JsonStreamWriter writer)");
        w.OpenBrace();
        w.WriteLine("this.writer = writer;");
        w.CloseBrace();

        HashSet<string> emittedMethodNames = [];
        foreach (string itemTypeName in itemTypeNames)
        {
            string methodName = $"Append{GetSimpleTypeIdentifier(itemTypeName)}";
            if (!emittedMethodNames.Add(methodName))
            {
                methodName = $"Append{CodeEmitHelpers.SanitizeIdentifier(itemTypeName)}";
            }

            w.WriteLine();
            w.WriteLine("/// <summary>");
            w.WriteLine($"/// Appends a <see cref=\"{itemTypeName}\"/> item to the response stream.");
            w.WriteLine("/// </summary>");
            w.WriteLine("/// <param name=\"item\">The item to append.</param>");
            w.WriteLine("/// <param name=\"cancellationToken\">The cancellation token.</param>");
            w.WriteLine("/// <returns>A value task that completes when the item has been flushed.</returns>");
            w.WriteLine($"public ValueTask {methodName}(in {itemTypeName}.Source item, CancellationToken cancellationToken = default)");
            w.OpenBrace();
            w.WriteLine("using JsonWorkspace workspace = JsonWorkspace.Create();");
            w.WriteLine($"using JsonDocumentBuilder<{itemTypeName}.Mutable> builder = {itemTypeName}.CreateBuilder(workspace, item);");
            w.WriteLine("return this.writer.WriteItemAsync(builder.RootElement, cancellationToken);");
            w.CloseBrace();
            w.WriteLine();
            w.WriteLine("/// <summary>");
            w.WriteLine($"/// Appends a <see cref=\"{itemTypeName}\"/> item to the response stream.");
            w.WriteLine("/// </summary>");
            w.WriteLine("/// <param name=\"item\">The item to append.</param>");
            w.WriteLine("/// <param name=\"cancellationToken\">The cancellation token.</param>");
            w.WriteLine("/// <returns>A value task that completes when the item has been flushed.</returns>");
            w.WriteLine($"public ValueTask {methodName}({itemTypeName} item, CancellationToken cancellationToken = default)");
            w.OpenBrace();
            w.WriteLine("return this.writer.WriteItemAsync(item, cancellationToken);");
            w.CloseBrace();
        }

        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Callback used to write a {streamTypeName} response.");
        w.WriteLine("/// </summary>");
        w.WriteLine($"/// <param name=\"stream\">The {streamTypeName} writer.</param>");
        w.WriteLine("/// <param name=\"cancellationToken\">The cancellation token.</param>");
        w.WriteLine("/// <returns>A value task that completes when the stream has been written.</returns>");
        w.WriteLine($"public delegate ValueTask {streamWriterDelegateName}({streamTypeName} stream, CancellationToken cancellationToken);");
        w.WriteLine();
        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Callback used to write a {streamTypeName} response with a context value.");
        w.WriteLine("/// </summary>");
        w.WriteLine("/// <typeparam name=\"TContext\">The callback context type.</typeparam>");
        w.WriteLine($"/// <param name=\"stream\">The {streamTypeName} writer.</param>");
        w.WriteLine("/// <param name=\"context\">The callback context.</param>");
        w.WriteLine("/// <param name=\"cancellationToken\">The cancellation token.</param>");
        w.WriteLine("/// <returns>A value task that completes when the stream has been written.</returns>");
        w.WriteLine($"public delegate ValueTask {streamWriterDelegateName}<TContext>({streamTypeName} stream, TContext context, CancellationToken cancellationToken);");
    }

    private void EmitServerResultFactory(
        IndentedWriter w,
        string structName,
        string factoryName,
        string? bodyTypeName,
        ResponseInfo response,
        List<(HeaderInfo Header, string TypeName, string FieldName, string PropertyName)> respHeaders,
        string statusCode,
        bool structHasHeaders,
        string streamTypeName,
        string streamWriterDelegateName)
    {
        bool isDefault = statusCode == "default";
        List<ContentInfo> streamingContent = GetStreamingContent(response);
        bool hasStreamingBody = streamingContent.Count > 0;
        bool hasBody = bodyTypeName is not null && !hasStreamingBody;
        bool hasRespHeaders = respHeaders.Count > 0;

        if (hasStreamingBody)
        {
            this.EmitServerStreamingResultFactories(
                w,
                structName,
                factoryName,
                streamingContent,
                respHeaders,
                statusCode,
                streamTypeName,
                streamWriterDelegateName);
            return;
        }

        // Parameters
        StringBuilder paramList = new();
        if (isDefault)
        {
            paramList.Append("int statusCode");
        }

        if (hasBody)
        {
            if (paramList.Length > 0)
            {
                paramList.Append(", ");
            }

            paramList.Append($"{bodyTypeName}.Source body, JsonWorkspace workspace");
        }
        else if (hasRespHeaders)
        {
            if (paramList.Length > 0)
            {
                paramList.Append(", ");
            }

            paramList.Append("JsonWorkspace workspace");
        }

        foreach (var (header, typeName, fieldName, _) in respHeaders)
        {
            if (paramList.Length > 0)
            {
                paramList.Append(", ");
            }

            // Headers use Source pattern (consistent with body parameters).
            paramList.Append($"{typeName}.Source {fieldName} = default");
        }

        // XML doc params
        if (isDefault)
        {
            w.WriteLine($"/// <param name=\"statusCode\">The HTTP status code.</param>");
        }

        if (hasBody)
        {
            w.WriteLine($"/// <param name=\"body\">The response body.</param>");
            w.WriteLine($"/// <param name=\"workspace\">The workspace for building the response value.</param>");
        }
        else if (hasRespHeaders)
        {
            w.WriteLine($"/// <param name=\"workspace\">The workspace for building header values.</param>");
        }

        foreach (var (header, _, fieldName, _) in respHeaders)
        {
            w.WriteLine($"/// <param name=\"{fieldName}\">The value for the <c>{header.HeaderName}</c> response header.</param>");
        }

        w.WriteLine($"/// <returns>A <see cref=\"{structName}\"/> with status {statusCode}.</returns>");

        // Method signature + body
        string statusExpr = isDefault ? "statusCode" : statusCode;
        string bodyExpr = hasBody
            ? $"{bodyTypeName}.CreateBuilder(workspace, body, 30).RootElement"
            : "default";
        string contentTypeExpr = hasBody ? "\"application/json\"" : "null";

        if (structHasHeaders)
        {
            // Use the expanded constructor form
            StringBuilder ctorArgs = new();
            ctorArgs.Append($"{statusExpr}, {bodyExpr}, {contentTypeExpr}");
            foreach (var (_, typeName, fieldName, _) in respHeaders)
            {
                ctorArgs.Append($", {fieldName}: {fieldName}.IsUndefined ? default : {typeName}.CreateBuilder(workspace, {fieldName}, 30).RootElement");
            }

            w.WriteLine($"public static {structName} {factoryName}({paramList}) => new({ctorArgs});");
        }
        else
        {
            w.WriteLine($"public static {structName} {factoryName}({paramList}) => new({statusExpr}, {bodyExpr}, {contentTypeExpr});");
        }
    }

    private void EmitServerStreamingResultFactories(
        IndentedWriter w,
        string structName,
        string factoryName,
        List<ContentInfo> streamingContent,
        List<(HeaderInfo Header, string TypeName, string FieldName, string PropertyName)> respHeaders,
        string statusCode,
        string streamTypeName,
        string streamWriterDelegateName)
    {
        bool isDefault = statusCode == "default";
        bool includeMediaTypeSuffix = streamingContent.Count > 1;
        string statusExpr = isDefault ? "statusCode" : statusCode;

        foreach (ContentInfo content in streamingContent)
        {
            string methodName = GetStreamingFactoryName(factoryName, content.MediaType, includeMediaTypeSuffix);
            string contentTypeExpr = SymbolDisplay.FormatLiteral(content.MediaType, quote: true);
            string writerParam = $"{streamWriterDelegateName} writer";
            string contextWriterParam = $"{streamWriterDelegateName}<TContext> writer";

            StringBuilder commonParams = new();
            if (isDefault)
            {
                commonParams.Append("int statusCode, ");
            }

            commonParams.Append(writerParam);
            bool hasHeaders = respHeaders.Count > 0;
            if (hasHeaders)
            {
                commonParams.Append(", JsonWorkspace workspace");
            }

            foreach (var (_, typeName, fieldName, _) in respHeaders)
            {
                commonParams.Append($", {typeName}.Source {fieldName} = default");
            }

            StringBuilder contextParams = new();
            if (isDefault)
            {
                contextParams.Append("int statusCode, ");
            }

            contextParams.Append($"TContext context, {contextWriterParam}");
            if (hasHeaders)
            {
                contextParams.Append(", JsonWorkspace workspace");
            }

            foreach (var (_, typeName, fieldName, _) in respHeaders)
            {
                contextParams.Append($", {typeName}.Source {fieldName} = default");
            }

            if (isDefault)
            {
                w.WriteLine("/// <param name=\"statusCode\">The HTTP status code.</param>");
            }

            w.WriteLine($"/// <param name=\"writer\">The callback that appends items to the <see cref=\"{streamTypeName}\"/>.</param>");
            if (hasHeaders)
            {
                w.WriteLine("/// <param name=\"workspace\">The workspace for building header values.</param>");
            }

            foreach (var (header, _, fieldName, _) in respHeaders)
            {
                w.WriteLine($"/// <param name=\"{fieldName}\">The value for the <c>{header.HeaderName}</c> response header.</param>");
            }

            w.WriteLine($"/// <returns>A <see cref=\"{structName}\"/> with status {statusCode}.</returns>");
            StringBuilder ctorArgs = new();
            ctorArgs.Append($"{statusExpr}, default, {contentTypeExpr}, streamWriter: static (jsonStreamWriter, context, cancellationToken) => (({streamWriterDelegateName})context!)(new {streamTypeName}(jsonStreamWriter), cancellationToken), streamWriterContext: writer");
            foreach (var (_, typeName, fieldName, _) in respHeaders)
            {
                ctorArgs.Append($", {fieldName}: {fieldName}.IsUndefined ? default : {typeName}.CreateBuilder(workspace, {fieldName}, 30).RootElement");
            }

            w.WriteLine($"public static {structName} {methodName}({commonParams}) => new({ctorArgs});");
            w.WriteLine();
            w.WriteLine($"/// <typeparam name=\"TContext\">The callback context type.</typeparam>");
            if (isDefault)
            {
                w.WriteLine("/// <param name=\"statusCode\">The HTTP status code.</param>");
            }

            w.WriteLine($"/// <param name=\"context\">The callback context.</param>");
            w.WriteLine($"/// <param name=\"writer\">The callback that appends items to the <see cref=\"{streamTypeName}\"/>.</param>");
            if (hasHeaders)
            {
                w.WriteLine("/// <param name=\"workspace\">The workspace for building header values.</param>");
            }

            foreach (var (header, _, fieldName, _) in respHeaders)
            {
                w.WriteLine($"/// <param name=\"{fieldName}\">The value for the <c>{header.HeaderName}</c> response header.</param>");
            }

            w.WriteLine($"/// <returns>A <see cref=\"{structName}\"/> with status {statusCode}.</returns>");
            StringBuilder contextCtorArgs = new();
            contextCtorArgs.Append($"{statusExpr}, default, {contentTypeExpr}, streamWriter: static (jsonStreamWriter, context, cancellationToken) =>");
            w.WriteLine($"public static {structName} {methodName}<TContext>({contextParams}) => new({contextCtorArgs}");
            w.PushIndent();
            w.WriteLine("{");
            w.PushIndent();
            w.WriteLine($"var wrapper = ({streamWriterDelegateName}Context<TContext>)context!;");
            w.WriteLine($"return wrapper.Writer(new {streamTypeName}(jsonStreamWriter), wrapper.Context, cancellationToken);");
            w.PopIndent();
            w.WriteLine("},");
            w.Write($"streamWriterContext: new {streamWriterDelegateName}Context<TContext>(context, writer)");
            foreach (var (_, typeName, fieldName, _) in respHeaders)
            {
                w.Write($", {fieldName}: {typeName}.CreateBuilder(workspace, {fieldName}, 30).RootElement");
            }

            w.WriteLine(");");
            w.PopIndent();
        }
    }

    private GeneratedFile EmitServerEndpointRegistration(
        Dictionary<string, List<OperationInfo>> groups,
        SecuritySchemeInfo[]? securitySchemes,
        IReadOnlyList<OperationInfo> operations,
        bool isCallbackServer)
    {
        string prefix = this.clientNamePrefix ?? "Api";
        string className = $"{prefix}EndpointRegistration";
        IndentedWriter w = new();

        w.WriteLine("// <auto-generated>");
        w.WriteLine("// This code was generated by the Corvus.Text.Json OpenAPI code generator.");
        w.WriteLine("// Do not edit this file directly.");
        w.WriteLine("// </auto-generated>");
        w.WriteLine();
        w.WriteLine("#nullable enable");
        w.WriteLine();
        w.WriteLine("using System.Diagnostics.CodeAnalysis;");
        w.WriteLine("using Corvus.Runtime.InteropServices;");
        w.WriteLine("using Corvus.Text.Json;");
        w.WriteLine("using Corvus.Text.Json.Internal;");
        w.WriteLine("using Corvus.Text.Json.OpenApi;");
        w.WriteLine("using Microsoft.AspNetCore.Builder;");
        w.WriteLine("using Microsoft.AspNetCore.Http;");
        w.WriteLine("using Microsoft.AspNetCore.Routing;");
        w.WriteLine();

        w.WriteLine($"namespace {this.rootNamespace};");
        w.WriteLine();

        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Extension methods for registering {prefix} API endpoints.");
        w.WriteLine("/// </summary>");
        w.WriteLine($"public static class {className}");
        w.OpenBrace();

        // Build method parameters — one handler per tag
        List<string> handlerParams = [];
        List<string> handlerParamNames = [];
        foreach ((string tag, _) in groups)
        {
            string handlerName = this.GetHandlerName(tag);
            string paramName = CodeEmitHelpers.SanitizeParameterName(tag) + "Handler";
            handlerParams.Add($"I{handlerName}Handler {paramName}");
            handlerParamNames.Add(paramName);
        }

        // Identify operations whose path templates contain runtime expressions.
        // These require the caller to provide the route at registration time because
        // the actual URL is determined dynamically by the client (e.g. {$request.body#/callbackUrl}).
        Dictionary<string, string> runtimeExpressionRouteParams = [];
        HashSet<string> seenRouteParamNames = [];
        foreach (OperationInfo op in operations)
        {
            if (ContainsRuntimeExpression(op.PathTemplate))
            {
                string routeParamName = CodeEmitHelpers.SanitizeParameterName(op.MethodName) + "Route";
                if (!seenRouteParamNames.Add(routeParamName))
                {
                    continue;
                }

                string opKey = $"{op.CustomMethodName ?? op.Method.ToString()}:{op.PathTemplate}";
                runtimeExpressionRouteParams[opKey] = routeParamName;
            }
        }

        // Emit the XML doc-comment header shared by both overloads.
        void EmitMapEndpointsDocComment(bool includeConfigureEndpoint)
        {
            w.WriteLine("/// <summary>");
            w.WriteLine($"/// Maps all {prefix} API endpoints to the application.");
            w.WriteLine("/// </summary>");
            w.WriteLine("/// <param name=\"app\">The endpoint route builder.</param>");
            foreach ((string docTag, _) in groups)
            {
                string docParamName = CodeEmitHelpers.SanitizeParameterName(docTag) + "Handler";
                string docHandlerName = this.GetHandlerName(docTag);
                w.WriteLine($"/// <param name=\"{docParamName}\">The handler for {docHandlerName} operations.</param>");
            }

            foreach ((string _, string routeParamName) in runtimeExpressionRouteParams)
            {
                w.WriteLine($"/// <param name=\"{routeParamName}\">The route template to register for this callback endpoint.</param>");
            }

            if (includeConfigureEndpoint)
            {
                w.WriteLine("/// <param name=\"configureEndpoint\">An optional callback invoked once per generated endpoint, after the route is mapped, to apply per-endpoint conventions (authorization, naming, tags, output caching, rate limiting, etc.). May be <see langword=\"null\"/>.</param>");
            }

            w.WriteLine("/// <returns>The endpoint route builder for chaining.</returns>");
        }

        // Original overload — preserved verbatim for source- and binary-compatibility.
        // It delegates to the new overload below, passing a null callback.
        EmitMapEndpointsDocComment(includeConfigureEndpoint: false);
        w.Write($"public static IEndpointRouteBuilder Map{prefix}Endpoints(this IEndpointRouteBuilder app");
        foreach (string hp in handlerParams)
        {
            w.Write($", {hp}");
        }

        foreach ((string _, string routeParamName) in runtimeExpressionRouteParams)
        {
            w.Write($", string {routeParamName}");
        }

        w.WriteLine(")");
        w.OpenBrace();
        w.Write($"return Map{prefix}Endpoints(app");
        foreach (string hpn in handlerParamNames)
        {
            w.Write($", {hpn}");
        }

        foreach ((string _, string routeParamName) in runtimeExpressionRouteParams)
        {
            w.Write($", {routeParamName}");
        }

        w.WriteLine(", configureEndpoint: null);");
        w.CloseBrace();
        w.WriteLine();

        // New, additive overload carrying the per-endpoint customization callback.
        EmitMapEndpointsDocComment(includeConfigureEndpoint: true);
        w.Write($"public static IEndpointRouteBuilder Map{prefix}Endpoints(this IEndpointRouteBuilder app");
        foreach (string hp in handlerParams)
        {
            w.Write($", {hp}");
        }

        foreach ((string _, string routeParamName) in runtimeExpressionRouteParams)
        {
            w.Write($", string {routeParamName}");
        }

        w.Write(", ConfigureEndpoint? configureEndpoint");
        w.WriteLine(")");
        w.OpenBrace();

        // Emit a MapXxx call for each operation. Track registered operations to avoid
        // duplicates when the same operation appears under multiple tags.
        HashSet<string> registeredOperations = [];
        foreach ((string tag, List<OperationInfo> tagOps) in groups)
        {
            string paramName = CodeEmitHelpers.SanitizeParameterName(tag) + "Handler";

            foreach (OperationInfo op in tagOps)
            {
                string operationKey = $"{op.CustomMethodName ?? op.Method.ToString()}:{op.PathTemplate}";
                if (!registeredOperations.Add(operationKey))
                {
                    continue;
                }

                string mapMethod = GetAspNetMapMethod(op.Method);
                string paramsName = $"{op.MethodName}Params";
                string resultName = $"{op.MethodName}Result";
                bool hasBody = op.RequestBody is not null;
                bool opHasStreamingResponses = op.Responses.Any(r => GetStreamingContent(r).Count > 0);

                w.WriteLine();

                // Determine the route string: use the parameter if this is a runtime expression path,
                // otherwise emit the literal route.
                string routeExpression;
                if (runtimeExpressionRouteParams.TryGetValue(operationKey, out string? routeParam))
                {
                    routeExpression = routeParam;
                }
                else
                {
                    routeExpression = $"\"{ConvertToAspNetRoute(op.PathTemplate)}\"";
                }

                // Capture the route builder so the per-endpoint configuration callback can be
                // invoked against it after the route is mapped.
                string endpointVar = $"__{op.MethodName}Endpoint";
                string httpVerb = op.CustomMethodName ?? op.Method.ToString().ToUpperInvariant();
                if (mapMethod == "MapMethods")
                {
                    w.WriteLine($"IEndpointConventionBuilder {endpointVar} = app.MapMethods({routeExpression}, new[] {{ \"{httpVerb}\" }}, async (HttpContext context) =>");
                }
                else
                {
                    w.WriteLine($"IEndpointConventionBuilder {endpointVar} = app.{mapMethod}({routeExpression}, async (HttpContext context) =>");
                }

                w.OpenBrace();

                // Create workspace for parameter parsing and response writing
                // (mirrors client response pattern: workspace manages param/header document lifetimes)
                bool isRawStreamBody = hasBody && IsRawStreamRequestBody(op.RequestBody!.Value);
                string? bodyTypeName = hasBody && !isRawStreamBody ? this.ResolveRequestBodyTypeName(op.RequestBody!.Value) : null;
                w.WriteLine("JsonWorkspace workspace = JsonWorkspace.CreateUnrented();");
                if (hasBody && !isRawStreamBody)
                {
                    w.WriteLine($"ParsedJsonDocument<{bodyTypeName}>? bodyDoc = null;");
                }

                w.WriteLine("try");
                w.OpenBrace();

                // Parse parameters via HeaderValueParser (mirrors client response header parsing)
                foreach (ParameterInfo param in op.Parameters)
                {
                    string fieldName = CodeEmitHelpers.SanitizeIdentifier(param.Name);
                    string typeName = this.GetParameterTypeName(param);
                    EmitServerParameterParsing(w, param, fieldName, typeName);
                }

                // Validate parameters: check required parameters are present and all parameters pass schema validation.
                if (op.Parameters.Length > 0)
                {
                    w.WriteLine();

                    // Required parameter presence checks
                    foreach (ParameterInfo param in op.Parameters)
                    {
                        if (param.IsRequired)
                        {
                            string fieldName = CodeEmitHelpers.SanitizeIdentifier(param.Name);
                            w.WriteLine($"if ({fieldName}Value.IsUndefined())");
                            w.OpenBrace();
                            EmitProblemDetailsResponse(w, 400, "Bad Request", $"The required parameter '{param.Name}' is missing.");
                            w.WriteLine("return;");
                            w.CloseBrace();
                            w.WriteLine();
                        }
                    }

                    // Schema validation for all parameters that have a value
                    foreach (ParameterInfo param in op.Parameters)
                    {
                        string fieldName = CodeEmitHelpers.SanitizeIdentifier(param.Name);
                        w.WriteLine($"if (!{fieldName}Value.IsUndefined() && !{fieldName}Value.EvaluateSchema())");
                        w.OpenBrace();
                        EmitProblemDetailsResponse(w, 400, "Bad Request", $"The parameter '{param.Name}' failed schema validation.");
                        w.WriteLine("return;");
                        w.CloseBrace();
                        w.WriteLine();
                    }
                }

                // Parse body from request stream into a document.
                // For form-urlencoded bodies, use the symmetric deserializer (inverse of
                // FormUrlEncodedSerializer.Serialize used by the client).
                // For multipart/form-data bodies, use the symmetric deserializer (inverse of
                // MultipartFormDataSerializer.Serialize used by the client).
                // For raw stream bodies (application/octet-stream), pass the stream directly.
                // For JSON bodies, parse directly from the stream.
                if (hasBody)
                {
                    if (op.Parameters.Length > 0)
                    {
                        w.WriteLine();
                    }

                    if (IsRawStreamRequestBody(op.RequestBody!.Value))
                    {
                        // Raw stream body — no parsing needed, pass context.Request.Body directly.
                    }
                    else if (IsFormUrlEncodedRequestBody(op.RequestBody!.Value))
                    {
                        w.WriteLine("try");
                        w.OpenBrace();
                        w.WriteLine($"bodyDoc = await FormUrlEncodedSerializer.DeserializeAsync<{bodyTypeName}>(context.Request.Body, context.RequestAborted).ConfigureAwait(false);");
                        w.CloseBrace();
                        w.WriteLine("catch");
                        w.OpenBrace();
                        EmitProblemDetailsResponse(w, 400, "Bad Request", "The request body could not be parsed.");
                        w.WriteLine("return;");
                        w.CloseBrace();
                        w.WriteLine();
                        EmitRequestBodySchemaValidation(w, bodyTypeName);
                    }
                    else if (IsMultipartRequestBody(op.RequestBody!.Value))
                    {
                        w.WriteLine("try");
                        w.OpenBrace();
                        w.WriteLine($"bodyDoc = await MultipartFormDataSerializer.DeserializeAsync<{bodyTypeName}>(context.Request.Body, context.Request.ContentType, cancellationToken: context.RequestAborted).ConfigureAwait(false);");
                        w.CloseBrace();
                        w.WriteLine("catch");
                        w.OpenBrace();
                        EmitProblemDetailsResponse(w, 400, "Bad Request", "The request body could not be parsed.");
                        w.WriteLine("return;");
                        w.CloseBrace();

                        // Note: schema validation is skipped for multipart/form-data bodies because
                        // binary file fields (format: binary) are serialized as JSON strings whose
                        // content cannot meaningfully be validated against the schema format annotation.
                    }
                    else if (IsMultipartMixedRequestBody(op.RequestBody!.Value))
                    {
                        w.WriteLine("try");
                        w.OpenBrace();
                        w.WriteLine($"bodyDoc = await MultipartMixedSerializer.DeserializeAsync<{bodyTypeName}>(context.Request.Body, context.Request.ContentType, cancellationToken: context.RequestAborted).ConfigureAwait(false);");
                        w.CloseBrace();
                        w.WriteLine("catch");
                        w.OpenBrace();
                        EmitProblemDetailsResponse(w, 400, "Bad Request", "The request body could not be parsed.");
                        w.WriteLine("return;");
                        w.CloseBrace();
                        w.WriteLine();
                        EmitRequestBodySchemaValidation(w, bodyTypeName);
                    }
                    else
                    {
                        w.WriteLine("try");
                        w.OpenBrace();
                        w.WriteLine($"bodyDoc = await ParsedJsonDocument<{bodyTypeName}>.ParseAsync(context.Request.Body, default, context.RequestAborted).ConfigureAwait(false);");
                        w.CloseBrace();
                        w.WriteLine("catch");
                        w.OpenBrace();
                        EmitProblemDetailsResponse(w, 400, "Bad Request", "The request body could not be parsed.");
                        w.WriteLine("return;");
                        w.CloseBrace();
                        w.WriteLine();
                        EmitRequestBodySchemaValidation(w, bodyTypeName);
                    }
                }

                w.WriteLine();

                // Bind parameters
                if (op.Parameters.Length > 0 || hasBody)
                {
                    w.WriteLine($"{paramsName} parameters = new()");
                    w.OpenBrace();

                    foreach (ParameterInfo param in op.Parameters)
                    {
                        string fieldName = CodeEmitHelpers.SanitizeIdentifier(param.Name);
                        w.WriteLine($"{fieldName} = {fieldName}Value,");
                    }

                    if (hasBody)
                    {
                        if (isRawStreamBody)
                        {
                            w.WriteLine("Body = context.Request.Body,");
                        }
                        else
                        {
                            w.WriteLine("Body = bodyDoc!.RootElement,");
                        }
                    }

                    w.CloseBrace().Write(";");
                    w.WriteLine();
                }
                else
                {
                    w.WriteLine($"{paramsName} parameters = new();");
                }

                w.WriteLine();

                // Call handler
                w.WriteLine($"{resultName} result = await {paramName}.Handle{op.MethodName}Async(parameters, workspace, context.RequestAborted).ConfigureAwait(false);");
                w.WriteLine();

                // Validate the response body against the schema for the returned status code.
                // If the handler produces an invalid response, return 500 Internal Server Error.
                w.WriteLine("if (!result.ValidateBody())");
                w.OpenBrace();
                EmitProblemDetailsResponse(w, 500, "Internal Server Error", "The response body failed schema validation.");
                w.WriteLine("return;");
                w.CloseBrace();
                w.WriteLine();

                // Write response using workspace-rented writer to PipeWriter
                // (mirrors client request pattern: workspace builds outgoing data)
                w.WriteLine("context.Response.StatusCode = result.StatusCode;");

                // Write response headers (if the operation defines any).
                bool opHasResponseHeaders = op.Responses.Any(r => r.Headers.Length > 0);
                if (opHasResponseHeaders)
                {
                    w.WriteLine("result.WriteResponseHeaders<Microsoft.AspNetCore.Http.IHeaderDictionary>(static (name, value, headers) =>");
                    w.OpenBrace();
                    w.WriteLine("headers.Append(System.Text.Encoding.UTF8.GetString(name), System.Text.Encoding.UTF8.GetString(value));");
                    w.CloseBraceNoNewline().Write(", context.Response.Headers);");
                    w.WriteLine();
                }

                if (opHasStreamingResponses)
                {
                    w.WriteLine("if (result.HasStreamingBody)");
                    w.OpenBrace();
                    w.WriteLine("context.Response.ContentType = result.ContentType!;");

                    // Streaming responses hold the writer across the await on WriteStreamAsync,
                    // whose continuations can resume on a different thread. Use a dedicated
                    // (non-thread-local-cached) writer that is safe across await boundaries; a
                    // rented writer would corrupt the thread-local cache when released on the
                    // continuation thread. See issue #814.
                    w.WriteLine("Utf8JsonWriter writer = workspace.CreateWriter(context.Response.BodyWriter);");
                    w.WriteLine("try");
                    w.OpenBrace();
                    w.WriteLine("JsonStreamWriter streamWriter = new(context.Response.BodyWriter, writer, result.ContentType!);");
                    w.WriteLine("await result.WriteStreamAsync(streamWriter, context.RequestAborted).ConfigureAwait(false);");
                    w.CloseBrace();
                    w.WriteLine("finally");
                    w.OpenBrace();
                    w.WriteLine("await writer.DisposeAsync().ConfigureAwait(false);");
                    w.CloseBrace();
                    w.WriteLine();
                    w.WriteLine("await context.Response.BodyWriter.FlushAsync(context.RequestAborted).ConfigureAwait(false);");
                    w.CloseBrace();
                    w.WriteLine("else if (!result.Body.IsUndefined())");
                }
                else
                {
                    w.WriteLine("if (!result.Body.IsUndefined())");
                }

                w.OpenBrace();
                w.WriteLine("context.Response.ContentType = result.ContentType ?? \"application/json\";");
                w.WriteLine("Utf8JsonWriter writer = workspace.RentWriter(context.Response.BodyWriter);");
                w.WriteLine("try");
                w.OpenBrace();
                w.WriteLine("result.WriteBody(writer);");
                w.WriteLine("writer.Flush();");
                w.CloseBrace();
                w.WriteLine("finally");
                w.OpenBrace();
                w.WriteLine("workspace.ReturnWriter(writer);");
                w.CloseBrace();
                w.WriteLine();
                w.WriteLine("await context.Response.BodyWriter.FlushAsync(context.RequestAborted).ConfigureAwait(false);");
                w.CloseBrace();

                // Finally: dispose workspace (releases param documents) and body document
                w.CloseBrace();
                w.WriteLine("finally");
                w.OpenBrace();
                w.WriteLine("workspace.Dispose();");
                if (hasBody && !isRawStreamBody)
                {
                    w.WriteLine("bodyDoc?.Dispose();");
                }

                w.CloseBrace();

                w.CloseBrace().Write(");");
                w.WriteLine();

                // Invoke the per-endpoint configuration callback (if provided) after the route is
                // mapped, so any conventions the consumer applies take precedence over generator
                // conventions emitted above.
                EmitConfigureEndpointInvocation(w, op, endpointVar, routeExpression, httpVerb, isCallbackServer);
            }
        }

        w.WriteLine();
        w.WriteLine("return app;");
        w.CloseBrace();

        if (securitySchemes is { Length: > 0 })
        {
            EmitSecuritySchemeMetadata(w, securitySchemes);
        }

        // Only the versions that extract security schemes (a non-null array) emit the server
        // security-requirements metadata; the 3.0/3.1 emitters pass null and never produce it.
        if (securitySchemes is not null)
        {
            EmitSecurityRequirements(w, operations, securitySchemes);
        }

        w.CloseBrace();

        // Emit the supporting public types for the per-endpoint configuration callback at
        // namespace scope, alongside the registration class.
        EmitEndpointConfigurationTypes(w);

        return new GeneratedFile($"{className}.cs", w.ToString());
    }

    /// <summary>
    /// Emits the <c>configureEndpoint?.Invoke(...)</c> call for a single mapped operation, building
    /// an <c>EndpointDescriptor</c> from the operation's metadata.
    /// </summary>
    private static void EmitConfigureEndpointInvocation(
        IndentedWriter w,
        OperationInfo op,
        string endpointVar,
        string routeExpression,
        string httpVerb,
        bool isCallbackServer)
    {
        string operationIdLiteral = op.OperationId is null
            ? "null"
            : CodeEmitHelpers.FormatStringLiteral(op.OperationId);
        string tagsLiteral = op.Tags is { Length: > 0 }
            ? $"new[] {{ {string.Join(", ", op.Tags.Select(CodeEmitHelpers.FormatStringLiteral))} }}"
            : "System.Array.Empty<string>()";
        string securityLiteral = FormatSecurityRequirementsLiteral(op.SecurityRequirements);

        w.WriteLine("configureEndpoint?.Invoke(");
        w.PushIndent();
        w.WriteLine("new EndpointDescriptor(");
        w.PushIndent();
        w.WriteLine($"operationId: {operationIdLiteral},");
        w.WriteLine($"methodName: {CodeEmitHelpers.FormatStringLiteral(op.MethodName)},");
        w.WriteLine($"httpMethod: {CodeEmitHelpers.FormatStringLiteral(httpVerb)},");
        w.WriteLine($"routeTemplate: {routeExpression},");
        w.WriteLine($"tags: {tagsLiteral},");
        w.WriteLine($"isCallback: {(isCallbackServer ? "true" : "false")},");
        w.WriteLine($"securityRequirements: {securityLiteral}),");
        w.PopIndent();
        w.WriteLine($"{endpointVar});");
        w.PopIndent();
    }

    /// <summary>
    /// Formats a C# expression for the <c>securityRequirements</c> constructor argument from the
    /// operation's extracted security requirement sets (the OR alternatives).
    /// </summary>
    private static string FormatSecurityRequirementsLiteral(OperationSecurityRequirementSet[]? sets)
    {
        if (sets is not { Length: > 0 })
        {
            return "System.Array.Empty<EndpointSecurityRequirementSet>()";
        }

        IEnumerable<string> setEntries = sets.Select(set =>
        {
            string requirements = set.Requirements is { Length: > 0 }
                ? $"new EndpointSecurityRequirement[] {{ {string.Join(", ", set.Requirements.Select(FormatSecurityRequirementLiteral))} }}"
                : "System.Array.Empty<EndpointSecurityRequirement>()";
            return $"new EndpointSecurityRequirementSet({requirements}, {(set.IsOptional ? "true" : "false")})";
        });

        return $"new EndpointSecurityRequirementSet[] {{ {string.Join(", ", setEntries)} }}";
    }

    private static string FormatSecurityRequirementLiteral(OperationSecurityRequirement req)
    {
        string scopes = req.Scopes is { Length: > 0 }
            ? $"new[] {{ {string.Join(", ", req.Scopes.Select(CodeEmitHelpers.FormatStringLiteral))} }}"
            : "System.Array.Empty<string>()";
        string schemeType = req.SchemeType is null
            ? "null"
            : CodeEmitHelpers.FormatStringLiteral(req.SchemeType);
        return $"new EndpointSecurityRequirement({CodeEmitHelpers.FormatStringLiteral(req.SchemeName)}, {scopes}, {schemeType})";
    }

    /// <summary>
    /// Emits the public <c>ConfigureEndpoint</c> delegate, <c>EndpointDescriptor</c> struct, and
    /// <c>EndpointSecurityRequirement</c> struct at namespace scope.
    /// </summary>
    private static void EmitEndpointConfigurationTypes(IndentedWriter w)
    {
        w.WriteLine();
        w.WriteLine("/// <summary>");
        w.WriteLine("/// Configures a single generated API endpoint. Invoked once per mapped operation.");
        w.WriteLine("/// </summary>");
        w.WriteLine("/// <param name=\"endpoint\">A descriptor identifying the operation being mapped.</param>");
        w.WriteLine("/// <param name=\"builder\">The endpoint convention builder for the mapped route.</param>");
        w.WriteLine("public delegate void ConfigureEndpoint(in EndpointDescriptor endpoint, IEndpointConventionBuilder builder);");
        w.WriteLine();

        w.WriteLine("/// <summary>");
        w.WriteLine("/// Describes a single generated API endpoint passed to a <see cref=\"ConfigureEndpoint\"/> callback.");
        w.WriteLine("/// </summary>");
        w.WriteLine("public readonly struct EndpointDescriptor");
        w.OpenBrace();
        w.WriteLine("/// <summary>");
        w.WriteLine("/// Initializes a new instance of the <see cref=\"EndpointDescriptor\"/> struct.");
        w.WriteLine("/// </summary>");
        w.WriteLine("/// <param name=\"operationId\">The OpenAPI <c>operationId</c>, or <see langword=\"null\"/> if the operation declares none.</param>");
        w.WriteLine("/// <param name=\"methodName\">The generated handler method name (the <c>{MethodName}</c> in <c>Handle{MethodName}Async</c>).</param>");
        w.WriteLine("/// <param name=\"httpMethod\">The HTTP method (e.g. <c>GET</c>, <c>POST</c>).</param>");
        w.WriteLine("/// <param name=\"routeTemplate\">The ASP.NET route template as registered.</param>");
        w.WriteLine("/// <param name=\"tags\">The OpenAPI tags for the operation.</param>");
        w.WriteLine("/// <param name=\"isCallback\">Whether the operation originates from a webhook/callback rather than the main paths.</param>");
        w.WriteLine("/// <param name=\"securityRequirements\">The operation's declared security as a list of alternatives (any one satisfies the operation).</param>");
        w.WriteLine("public EndpointDescriptor(string? operationId, string methodName, string httpMethod, string routeTemplate, System.Collections.Generic.IReadOnlyList<string> tags, bool isCallback, System.Collections.Generic.IReadOnlyList<EndpointSecurityRequirementSet> securityRequirements)");
        w.OpenBrace();
        w.WriteLine("this.OperationId = operationId;");
        w.WriteLine("this.MethodName = methodName;");
        w.WriteLine("this.HttpMethod = httpMethod;");
        w.WriteLine("this.RouteTemplate = routeTemplate;");
        w.WriteLine("this.Tags = tags;");
        w.WriteLine("this.IsCallback = isCallback;");
        w.WriteLine("this.SecurityRequirements = securityRequirements;");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("/// <summary>Gets the OpenAPI <c>operationId</c>, or <see langword=\"null\"/> if the operation declares none.</summary>");
        w.WriteLine("public string? OperationId { get; }");
        w.WriteLine();
        w.WriteLine("/// <summary>Gets the generated handler method name.</summary>");
        w.WriteLine("public string MethodName { get; }");
        w.WriteLine();
        w.WriteLine("/// <summary>Gets the HTTP method (e.g. <c>GET</c>, <c>POST</c>).</summary>");
        w.WriteLine("public string HttpMethod { get; }");
        w.WriteLine();
        w.WriteLine("/// <summary>Gets the ASP.NET route template as registered.</summary>");
        w.WriteLine("public string RouteTemplate { get; }");
        w.WriteLine();
        w.WriteLine("/// <summary>Gets the OpenAPI tags for the operation.</summary>");
        w.WriteLine("public System.Collections.Generic.IReadOnlyList<string> Tags { get; }");
        w.WriteLine();
        w.WriteLine("/// <summary>Gets a value indicating whether the operation originates from a webhook/callback rather than the main paths.</summary>");
        w.WriteLine("public bool IsCallback { get; }");
        w.WriteLine();
        w.WriteLine("/// <summary>");
        w.WriteLine("/// Gets the operation's declared security as a list of alternatives. The operation is satisfied if");
        w.WriteLine("/// <em>any one</em> alternative (an <see cref=\"EndpointSecurityRequirementSet\"/>) is satisfied (OR); an empty");
        w.WriteLine("/// list means the operation declares no security.");
        w.WriteLine("/// </summary>");
        w.WriteLine("public System.Collections.Generic.IReadOnlyList<EndpointSecurityRequirementSet> SecurityRequirements { get; }");
        w.CloseBrace();
        w.WriteLine();

        w.WriteLine("/// <summary>");
        w.WriteLine("/// A single alternative within an operation's declared security (one OpenAPI \"Security Requirement Object\").");
        w.WriteLine("/// All of its <see cref=\"Requirements\"/> must be satisfied together (AND); any one set satisfying the");
        w.WriteLine("/// operation is enough (OR across sets).");
        w.WriteLine("/// </summary>");
        w.WriteLine("public readonly struct EndpointSecurityRequirementSet");
        w.OpenBrace();
        w.WriteLine("/// <summary>");
        w.WriteLine("/// Initializes a new instance of the <see cref=\"EndpointSecurityRequirementSet\"/> struct.");
        w.WriteLine("/// </summary>");
        w.WriteLine("/// <param name=\"requirements\">The scheme requirements that must all be satisfied together.</param>");
        w.WriteLine("/// <param name=\"isOptional\">Whether this alternative is the empty OpenAPI requirement (<c>{}</c>), which permits anonymous access.</param>");
        w.WriteLine("public EndpointSecurityRequirementSet(System.Collections.Generic.IReadOnlyList<EndpointSecurityRequirement> requirements, bool isOptional)");
        w.OpenBrace();
        w.WriteLine("this.Requirements = requirements;");
        w.WriteLine("this.IsOptional = isOptional;");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("/// <summary>Gets the scheme requirements that must all be satisfied together (AND).</summary>");
        w.WriteLine("public System.Collections.Generic.IReadOnlyList<EndpointSecurityRequirement> Requirements { get; }");
        w.WriteLine();
        w.WriteLine("/// <summary>Gets a value indicating whether this alternative is the empty OpenAPI requirement (<c>{}</c>), which permits anonymous access.</summary>");
        w.WriteLine("public bool IsOptional { get; }");
        w.WriteLine();
        w.WriteLine("/// <summary>");
        w.WriteLine("/// Gets the canonical authorization policy name for this alternative: the single requirement's");
        w.WriteLine("/// <see cref=\"EndpointSecurityRequirement.PolicyName\"/> when it has one scheme, otherwise the scheme");
        w.WriteLine("/// policy names joined with <c> &amp;&amp; </c> (all must pass). Empty for an anonymous (<c>{}</c>) alternative.");
        w.WriteLine("/// </summary>");
        w.WriteLine("public string PolicyName");
        w.OpenBrace();
        w.WriteLine("get");
        w.OpenBrace();
        w.WriteLine("if (this.Requirements.Count == 0)");
        w.OpenBrace();
        w.WriteLine("return string.Empty;");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("if (this.Requirements.Count == 1)");
        w.OpenBrace();
        w.WriteLine("return this.Requirements[0].PolicyName;");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("string[] names = new string[this.Requirements.Count];");
        w.WriteLine("for (int i = 0; i < this.Requirements.Count; i++)");
        w.OpenBrace();
        w.WriteLine("names[i] = this.Requirements[i].PolicyName;");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("return string.Join(\" && \", names);");
        w.CloseBrace();
        w.CloseBrace();
        w.CloseBrace();
        w.WriteLine();

        w.WriteLine("/// <summary>");
        w.WriteLine("/// A single security requirement (a scheme name and the scopes it requires) for an operation.");
        w.WriteLine("/// </summary>");
        w.WriteLine("public readonly struct EndpointSecurityRequirement");
        w.OpenBrace();
        w.WriteLine("/// <summary>");
        w.WriteLine("/// Initializes a new instance of the <see cref=\"EndpointSecurityRequirement\"/> struct.");
        w.WriteLine("/// </summary>");
        w.WriteLine("/// <param name=\"schemeName\">The name of the security scheme.</param>");
        w.WriteLine("/// <param name=\"scopes\">The scopes required by this requirement.</param>");
        w.WriteLine("/// <param name=\"schemeType\">The OpenAPI type of the security scheme (e.g. <c>oauth2</c>, <c>apiKey</c>, <c>http</c>, <c>openIdConnect</c>), or <see langword=\"null\"/> if the scheme is not declared in <c>components.securitySchemes</c>.</param>");
        w.WriteLine("public EndpointSecurityRequirement(string schemeName, System.Collections.Generic.IReadOnlyList<string> scopes, string? schemeType = null)");
        w.OpenBrace();
        w.WriteLine("this.SchemeName = schemeName;");
        w.WriteLine("this.Scopes = scopes;");
        w.WriteLine("this.SchemeType = schemeType;");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("/// <summary>Gets the name of the security scheme.</summary>");
        w.WriteLine("public string SchemeName { get; }");
        w.WriteLine();
        w.WriteLine("/// <summary>Gets the scopes required by this requirement.</summary>");
        w.WriteLine("public System.Collections.Generic.IReadOnlyList<string> Scopes { get; }");
        w.WriteLine();
        w.WriteLine("/// <summary>Gets the OpenAPI type of the security scheme (e.g. <c>oauth2</c>, <c>apiKey</c>, <c>http</c>, <c>openIdConnect</c>), or <see langword=\"null\"/> if the scheme is not declared in <c>components.securitySchemes</c>.</summary>");
        w.WriteLine("public string? SchemeType { get; }");
        w.WriteLine();
        w.WriteLine("/// <summary>");
        w.WriteLine("/// Gets the canonical authorization policy name for this requirement: the scheme name alone");
        w.WriteLine("/// when no scopes are required, otherwise <c>{schemeName}:{scope+scope...}</c>. Use the same value");
        w.WriteLine("/// when registering policies with <c>AddAuthorization</c> so endpoint mapping and policy registration stay in sync.");
        w.WriteLine("/// </summary>");
        w.WriteLine("public string PolicyName => this.Scopes.Count == 0 ? this.SchemeName : this.SchemeName + \":\" + string.Join(\"+\", this.Scopes);");
        w.CloseBrace();

        EmitEndpointSecurityConventions(w);
    }

    /// <summary>
    /// Emits the public <c>EndpointSecurityConventions</c> static class providing the
    /// <c>RequireDeclaredAuthorization</c> extension method, which applies an operation's declared
    /// security to a mapped endpoint using the canonical policy-name convention.
    /// </summary>
    private static void EmitEndpointSecurityConventions(IndentedWriter w)
    {
        w.WriteLine();
        w.WriteLine("/// <summary>");
        w.WriteLine("/// Extension methods that translate an <see cref=\"EndpointDescriptor\"/>'s declared OpenAPI security");
        w.WriteLine("/// into ASP.NET Core authorization conventions.");
        w.WriteLine("/// </summary>");
        w.WriteLine("public static class EndpointSecurityConventions");
        w.OpenBrace();
        w.WriteLine("/// <summary>");
        w.WriteLine("/// Applies the endpoint's declared OpenAPI security to the route using the canonical policy-name");
        w.WriteLine("/// convention (see <see cref=\"EndpointSecurityRequirement.PolicyName\"/>). When the operation declares");
        w.WriteLine("/// no security the endpoint is marked <c>AllowAnonymous</c>; otherwise <c>RequireAuthorization</c> is");
        w.WriteLine("/// called once per declared requirement.");
        w.WriteLine("/// </summary>");
        w.WriteLine("/// <param name=\"builder\">The endpoint convention builder for the mapped route.</param>");
        w.WriteLine("/// <param name=\"endpoint\">The descriptor for the operation being mapped.</param>");
        w.WriteLine("/// <returns>The same <paramref name=\"builder\"/>, for chaining.</returns>");
        w.WriteLine("/// <remarks>");
        w.WriteLine("/// <para>Behaviour follows the operation's declared OpenAPI security (a list of alternatives):</para>");
        w.WriteLine("/// <list type=\"bullet\">");
        w.WriteLine("/// <item>No declared security, or any anonymous (<c>{}</c>) alternative, marks the endpoint <c>AllowAnonymous</c>.</item>");
        w.WriteLine("/// <item>A single alternative requires every scheme in it (AND), each via its <see cref=\"EndpointSecurityRequirement.PolicyName\"/>.</item>");
        w.WriteLine("/// <item>Multiple alternatives (OR) require one combined policy named with the alternatives' <see cref=\"EndpointSecurityRequirementSet.PolicyName\"/> joined by <c> || </c>, since ASP.NET endpoint conventions cannot OR policies; register that policy with your own OR logic.</item>");
        w.WriteLine("/// </list>");
        w.WriteLine("/// <para>You must register the referenced policies (and call <c>AddAuthentication</c>/<c>UseAuthentication</c>/<c>UseAuthorization</c>) for these conventions to take effect.</para>");
        w.WriteLine("/// </remarks>");
        w.WriteLine("public static IEndpointConventionBuilder RequireDeclaredAuthorization(this IEndpointConventionBuilder builder, in EndpointDescriptor endpoint)");
        w.OpenBrace();
        w.WriteLine("System.Collections.Generic.IReadOnlyList<EndpointSecurityRequirementSet> alternatives = endpoint.SecurityRequirements;");
        w.WriteLine();
        w.WriteLine("// No declared security, or an explicit anonymous ({}) alternative, leaves the endpoint open.");
        w.WriteLine("if (alternatives.Count == 0)");
        w.OpenBrace();
        w.WriteLine("return builder.AllowAnonymous();");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("foreach (EndpointSecurityRequirementSet alternative in alternatives)");
        w.OpenBrace();
        w.WriteLine("if (alternative.IsOptional)");
        w.OpenBrace();
        w.WriteLine("return builder.AllowAnonymous();");
        w.CloseBrace();
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("if (alternatives.Count == 1)");
        w.OpenBrace();
        w.WriteLine("// Single alternative: every scheme in it must be satisfied (AND).");
        w.WriteLine("foreach (EndpointSecurityRequirement requirement in alternatives[0].Requirements)");
        w.OpenBrace();
        w.WriteLine("builder.RequireAuthorization(requirement.PolicyName);");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("return builder;");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("// Multiple alternatives are OR'd. ASP.NET endpoint conventions cannot express OR across policies,");
        w.WriteLine("// so require a single policy whose name combines the alternatives; register it with your OR logic.");
        w.WriteLine("string[] alternativePolicyNames = new string[alternatives.Count];");
        w.WriteLine("for (int i = 0; i < alternatives.Count; i++)");
        w.OpenBrace();
        w.WriteLine("alternativePolicyNames[i] = alternatives[i].PolicyName;");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("return builder.RequireAuthorization(string.Join(\" || \", alternativePolicyNames));");
        w.CloseBrace();
        w.CloseBrace();
    }

    private void EmitServerParameterParsing(
        IndentedWriter w,
        ParameterInfo param,
        string fieldName,
        string typeName)
    {
        string paramNameLiteral = EscapeStringLiteral(param.Name);

        // For array/object types, resolve the element/value type name.
        string? elementTypeName = null;
        if (param.HasDeepNesting && param.SchemaPointer is { } paramSchemaRef)
        {
            string elementPointer = param.SerializationKind is ParameterSerializationKind.Array
                ? paramSchemaRef.PositionalPointer + "/items"
                : paramSchemaRef.PositionalPointer + "/additionalProperties";
            elementTypeName = this.schemaTypeResolver.ResolveElementTypeName(new SchemaRef(elementPointer));
        }

        switch (param.Location)
        {
            case ParameterLocation.Path:
                EmitServerPathParameterParsing(w, param, fieldName, typeName, paramNameLiteral, elementTypeName);
                break;

            case ParameterLocation.Query:
            case ParameterLocation.Querystring:
                EmitServerQueryParameterParsing(w, param, fieldName, typeName, paramNameLiteral, elementTypeName);
                break;

            case ParameterLocation.Header:
                EmitServerHeaderParameterParsing(w, param, fieldName, typeName, paramNameLiteral, elementTypeName);
                break;

            case ParameterLocation.Cookie:
                EmitServerCookieParameterParsing(w, param, fieldName, typeName, paramNameLiteral, elementTypeName);
                break;
        }
    }

    private static void EmitServerPathParameterParsing(
        IndentedWriter w,
        ParameterInfo param,
        string fieldName,
        string typeName,
        string paramNameLiteral,
        string? elementTypeName)
    {
        // Extract raw string from route values.
        w.WriteLine($"{typeName} {fieldName}Value = default;");
        w.WriteLine($"if (context.Request.RouteValues.TryGetValue(\"{paramNameLiteral}\", out object? {fieldName}RouteVal) && {fieldName}RouteVal is string {fieldName}Raw)");
        w.OpenBrace();

        // Strip style prefix if needed.
        switch (param.Style)
        {
            case ParameterStyle.Matrix:
                // matrix: ;name=value or ;name=v1,v2
                w.WriteLine($"if ({fieldName}Raw.StartsWith(\";\"))");
                w.OpenBrace();
                if (param.SerializationKind is ParameterSerializationKind.Array or ParameterSerializationKind.Object && param.Explode)
                {
                    // matrix+explode: ;name=v1;name=v2 or ;k=v;k=v — strip leading ';'
                    w.WriteLine($"{fieldName}Raw = {fieldName}Raw.Substring(1);");
                }
                else
                {
                    // matrix+!explode: ;name=v1,v2 — strip ";name="
                    w.WriteLine($"int eqIdx = {fieldName}Raw.IndexOf('=');");
                    w.WriteLine($"{fieldName}Raw = eqIdx >= 0 ? {fieldName}Raw.Substring(eqIdx + 1) : {fieldName}Raw.Substring(1);");
                }

                w.CloseBrace();
                break;

            case ParameterStyle.Label:
                // label: .value or .v1.v2
                w.WriteLine($"if ({fieldName}Raw.StartsWith(\".\"))");
                w.OpenBrace();
                w.WriteLine($"{fieldName}Raw = {fieldName}Raw.Substring(1);");
                w.CloseBrace();
                break;
        }

        // Parse the stripped value.
        EmitServerValueParse(w, param, fieldName, typeName, $"{fieldName}Raw", "workspace", elementTypeName);

        w.CloseBrace();
    }

    private static void EmitServerQueryParameterParsing(
        IndentedWriter w,
        ParameterInfo param,
        string fieldName,
        string typeName,
        string paramNameLiteral,
        string? elementTypeName)
    {
        // For deepObject style: name[key]=value pattern — requires special handling.
        if (param.Style == ParameterStyle.DeepObject && param.SerializationKind == ParameterSerializationKind.Object)
        {
            EmitServerDeepObjectQueryParsing(w, param, fieldName, typeName, paramNameLiteral, elementTypeName);
            return;
        }

        // For form+explode+array: multiple query values for the same key.
        if (param.Style == ParameterStyle.Form && param.Explode && param.SerializationKind == ParameterSerializationKind.Array)
        {
            w.WriteLine($"{typeName} {fieldName}Value = default;");
            w.WriteLine($"if (context.Request.Query.TryGetValue(\"{paramNameLiteral}\", out var {fieldName}QueryValues) && {fieldName}QueryValues.Count > 0)");
            w.OpenBrace();
            CodeEmitHelpers.EmitArrayParseFromMultipleValues(w, $"{fieldName}Value", $"{fieldName}QueryValues", "workspace", typeName, param.ElementSerializationKind);
            w.CloseBrace();
            return;
        }

        // All other query styles: single value, different separator.
        w.WriteLine($"{typeName} {fieldName}Value = default;");
        w.WriteLine($"if (context.Request.Query.TryGetValue(\"{paramNameLiteral}\", out var {fieldName}QueryVal) && {fieldName}QueryVal.Count > 0)");
        w.OpenBrace();
        w.WriteLine($"string {fieldName}Raw = {fieldName}QueryVal[0]!;");
        EmitServerValueParse(w, param, fieldName, typeName, $"{fieldName}Raw", "workspace", elementTypeName);
        w.CloseBrace();
    }

    private static void EmitServerDeepObjectQueryParsing(
        IndentedWriter w,
        ParameterInfo param,
        string fieldName,
        string typeName,
        string paramNameLiteral,
        string? elementTypeName)
    {
        // deepObject: name[key]=value pairs across multiple query keys.
        // Collect all query keys that start with "name[" and build an object.
        w.WriteLine($"{typeName} {fieldName}Value = default;");
        w.WriteLine($"string {fieldName}Prefix = \"{paramNameLiteral}[\";");
        w.Write($"{fieldName}Value = {typeName}.CreateBuilder<(string prefix, Microsoft.AspNetCore.Http.IQueryCollection query)>(workspace, ({fieldName}Prefix, context.Request.Query), static (in (string prefix, Microsoft.AspNetCore.Http.IQueryCollection query) ctx, ref {typeName}.Builder objectBuilder) =>");
        w.WriteLine();
        w.OpenBrace();
        w.WriteLine("foreach (string queryKey in ctx.query.Keys)");
        w.OpenBrace();
        w.WriteLine("if (!queryKey.StartsWith(ctx.prefix)) continue;");
        w.WriteLine("int closeBracket = queryKey.IndexOf(']', ctx.prefix.Length);");
        w.WriteLine("if (closeBracket < 0) continue;");
        w.WriteLine("System.ReadOnlySpan<char> key = queryKey.AsSpan().Slice(ctx.prefix.Length, closeBracket - ctx.prefix.Length);");
        w.WriteLine("if (ctx.query.TryGetValue(queryKey, out var vals) && vals.Count > 0 && vals[0] is string v)");
        w.OpenBrace();

        string valueSourceExpr = CodeEmitHelpers.GetElementSourceExpressionPublic(param.ElementSerializationKind, "v.AsSpan()");
        w.WriteLine($"objectBuilder.AddProperty(key, {valueSourceExpr});");
        w.CloseBrace();
        w.CloseBrace();
        w.CloseBraceNoNewline();
        w.WriteLine(").RootElement;");
    }

    private static void EmitServerHeaderParameterParsing(
        IndentedWriter w,
        ParameterInfo param,
        string fieldName,
        string typeName,
        string paramNameLiteral,
        string? elementTypeName)
    {
        // Headers always use style: simple.
        w.WriteLine($"{typeName} {fieldName}Value = default;");
        w.WriteLine($"if (context.Request.Headers.TryGetValue(\"{paramNameLiteral}\", out var {fieldName}HeaderVal) && {fieldName}HeaderVal.Count > 0)");
        w.OpenBrace();
        w.WriteLine($"string {fieldName}Raw = {fieldName}HeaderVal[0]!;");
        EmitServerValueParse(w, param, fieldName, typeName, $"{fieldName}Raw", "workspace", elementTypeName);
        w.CloseBrace();
    }

    private static void EmitServerCookieParameterParsing(
        IndentedWriter w,
        ParameterInfo param,
        string fieldName,
        string typeName,
        string paramNameLiteral,
        string? elementTypeName)
    {
        // Cookies always use style: form.
        w.WriteLine($"{typeName} {fieldName}Value = default;");
        w.WriteLine($"if (context.Request.Cookies.TryGetValue(\"{paramNameLiteral}\", out string? {fieldName}CookieVal) && {fieldName}CookieVal is not null)");
        w.OpenBrace();
        w.WriteLine($"string {fieldName}Raw = {fieldName}CookieVal;");
        EmitServerValueParse(w, param, fieldName, typeName, $"{fieldName}Raw", "workspace", elementTypeName);
        w.CloseBrace();
    }

    private static void EmitServerValueParse(
        IndentedWriter w,
        ParameterInfo param,
        string fieldName,
        string typeName,
        string rawVar,
        string workspaceExpr,
        string? elementTypeName)
    {
        if (param.SerializationKind == ParameterSerializationKind.Array)
        {
            // Determine separator based on style.
            string separator = GetArraySeparator(param.Style, param.Explode);
            CodeEmitHelpers.EmitArrayParseFromSeparatedString(
                w, $"{fieldName}Value", rawVar, workspaceExpr, typeName,
                separator, param.ElementSerializationKind, param.HasDeepNesting, elementTypeName);
        }
        else if (param.SerializationKind == ParameterSerializationKind.Object)
        {
            // Determine separator based on style.
            string separator = GetObjectSeparator(param.Style, param.Explode);
            CodeEmitHelpers.EmitObjectParseFromSeparatedString(
                w, $"{fieldName}Value", rawVar, workspaceExpr, typeName,
                separator, param.Explode, param.ElementSerializationKind, param.HasDeepNesting, elementTypeName);
        }
        else
        {
            // Scalar value.
            CodeEmitHelpers.EmitScalarParse(w, $"{fieldName}Value", rawVar, workspaceExpr, typeName, param.SerializationKind);
        }
    }

    private static string GetArraySeparator(ParameterStyle style, bool explode) =>
        style switch
        {
            ParameterStyle.Label when explode => "'.'",
            ParameterStyle.Matrix when explode => "';'",
            ParameterStyle.PipeDelimited => "'|'",
            ParameterStyle.SpaceDelimited => "' '",
            _ => "','", // simple, form, label+!explode, matrix+!explode
        };

    private static string GetObjectSeparator(ParameterStyle style, bool explode) =>
        style switch
        {
            ParameterStyle.Label when explode => "'.'",
            ParameterStyle.Matrix when explode => "';'",
            _ => "','", // simple, form
        };

    private static string GetAspNetMapMethod(OperationMethod method) =>
        method switch
        {
            OperationMethod.Get => "MapGet",
            OperationMethod.Post => "MapPost",
            OperationMethod.Put => "MapPut",
            OperationMethod.Delete => "MapDelete",
            OperationMethod.Patch => "MapPatch",
            _ => "MapMethods",
        };

    /// <summary>
    /// Converts an OpenAPI path template to an ASP.NET Core route template by stripping
    /// label-style (.) and matrix-style (;) prefixes from path parameter names.
    /// E.g., <c>/label/{.items}</c> becomes <c>/label/{items}</c>.
    /// </summary>
    /// <remarks>
    /// This should not be called for paths containing OpenAPI runtime expressions
    /// (e.g. <c>{$request.body#/callbackUrl}</c>). Use <see cref="ContainsRuntimeExpression"/>
    /// to check first.
    /// </remarks>
    private static string ConvertToAspNetRoute(string openApiPath)
    {
        // Replace {.paramName} and {;paramName} with {paramName}
        int idx = openApiPath.IndexOf('{');
        if (idx < 0)
        {
            return openApiPath;
        }

        var sb = new System.Text.StringBuilder(openApiPath.Length);
        int pos = 0;
        while (idx >= 0)
        {
            sb.Append(openApiPath, pos, idx - pos + 1); // Include the '{'
            pos = idx + 1;

            // Skip style prefix if present (. or ;)
            if (pos < openApiPath.Length && (openApiPath[pos] == '.' || openApiPath[pos] == ';'))
            {
                pos++;
            }

            idx = openApiPath.IndexOf('{', pos);
        }

        sb.Append(openApiPath, pos, openApiPath.Length - pos);
        return sb.ToString();
    }

    /// <summary>
    /// Returns <see langword="true"/> if the path template contains an OpenAPI runtime expression
    /// (a parameter starting with <c>$</c>, e.g. <c>{$request.body#/callbackUrl}</c>).
    /// </summary>
    private static bool ContainsRuntimeExpression(string pathTemplate)
    {
        int idx = pathTemplate.IndexOf('{');
        while (idx >= 0 && idx + 1 < pathTemplate.Length)
        {
            if (pathTemplate[idx + 1] == '$')
            {
                return true;
            }

            idx = pathTemplate.IndexOf('{', idx + 1);
        }

        return false;
    }

    /// <summary>
    /// Emits an RFC 9457 Problem Details JSON response with the given status, title, and detail.
    /// </summary>
    private static void EmitProblemDetailsResponse(IndentedWriter w, int statusCode, string title, string detail)
    {
        w.WriteLine($"context.Response.StatusCode = {statusCode};");
        w.WriteLine("context.Response.ContentType = \"application/problem+json\";");
        w.WriteLine($"await context.Response.WriteAsync(\"{{\u005C\"type\u005C\":\u005C\"about:blank\u005C\",\u005C\"title\u005C\":\u005C\"{title}\u005C\",\u005C\"status\u005C\":{statusCode},\u005C\"detail\u005C\":\u005C\"{detail}\u005C\"}}\", context.RequestAborted).ConfigureAwait(false);");
    }

    /// <summary>
    /// Emits schema validation of the parsed request body, returning 400 if invalid.
    /// </summary>
    private static void EmitRequestBodySchemaValidation(IndentedWriter w, string? bodyTypeName)
    {
        if (bodyTypeName is null)
        {
            return;
        }

        w.WriteLine("if (!bodyDoc!.RootElement.EvaluateSchema())");
        w.OpenBrace();
        EmitProblemDetailsResponse(w, 400, "Bad Request", "The request body failed schema validation.");
        w.WriteLine("return;");
        w.CloseBrace();
        w.WriteLine();
    }
}