// <copyright file="TypeScriptApiEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.OpenApi.TypeScript.CodeGeneration;

/// <summary>
/// The TypeScript <see cref="IClientEmitter"/>: turns the shared OpenAPI intermediate representation
/// into idiomatic TypeScript client source files.
/// </summary>
/// <remarks>
/// <para>
/// This is the TypeScript peer of <c>OpenApiCSharpEmitterBase</c>. It is driven by the
/// version-neutral <see cref="ClientEmitDriver"/> over the same <see cref="OperationInfo"/>
/// intermediate representation the C# emitter consumes, and it resolves schema pointers to
/// TypeScript final names via an injected <see cref="ISchemaTypeResolver"/>
/// (<see cref="TypeScriptSchemaTypeResolver"/>).
/// </para>
/// <para>
/// A no-parameter operation with no request body and a single 2xx JSON response (plus an optional
/// <c>default</c>/error response) is emitted as a const request. A parameterised operation's request
/// becomes a factory (<c>{op}Request(params)</c>) that captures the params in a closure and serializes
/// path, query, header, and cookie parameters across the full OpenAPI parameter-style matrix via the
/// generalized runtime helpers (<c>writePathParam</c> / <c>writeQueryParam</c> / <c>writeHeaderParam</c>
/// / <c>writeCookieParam</c>), each of which dispatches on the value shape (scalar / array / object) and
/// the parameter's style. The generated client imports the byte-native runtime contracts from
/// <c>@endjin/corvus-json-client-runtime</c> and the generated models by their <c>Ts_FinalName</c>. The
/// server members return <see langword="null"/> (client-only).
/// </para>
/// </remarks>
public sealed class TypeScriptApiEmitter : IClientEmitter
{
    private static readonly TypeScriptApiEmitterOptions DefaultOptions = new(
        "@endjin/corvus-json-client-runtime",
        "./models/generated.js");

    private readonly TypeScriptSchemaTypeResolver schemaTypeResolver;
    private readonly TypeScriptApiEmitterOptions options;
    private readonly string? clientNamePrefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeScriptApiEmitter"/> class.
    /// </summary>
    /// <param name="schemaTypeResolver">
    /// Resolves a discovered schema pointer to its TypeScript final name. The emitter uses the
    /// TypeScript-specific resolver so it can enumerate the model names it needs to import.
    /// </param>
    /// <param name="options">The emitter options (runtime and model module specifiers).</param>
    /// <param name="clientNamePrefix">
    /// Optional prefix for client class names. If <see langword="null"/>, <c>"Api"</c> is used.
    /// </param>
    public TypeScriptApiEmitter(
        TypeScriptSchemaTypeResolver schemaTypeResolver,
        TypeScriptApiEmitterOptions options = default,
        string? clientNamePrefix = null)
    {
        this.schemaTypeResolver = schemaTypeResolver;
        this.options = options.ClientRuntimeModuleSpecifier is { Length: > 0 }
            ? options
            : DefaultOptions;
        this.clientNamePrefix = clientNamePrefix;
    }

    /// <inheritdoc/>
    public ClientEmitContext PrepareContext(
        JsonElement specRoot,
        IOpenApiReferenceResolver referenceResolver,
        ServerInfo? rootServer)
        => new(specRoot, referenceResolver, rootServer);

    /// <inheritdoc/>
    public GeneratedFile EmitRequestModule(OperationInfo op)
        => this.EmitRequestObject(op);

    /// <inheritdoc/>
    public GeneratedFile EmitResponseModule(OperationInfo op, IReadOnlyList<OperationInfo> allOperations)
        => this.EmitResponseClass(op);

    /// <inheritdoc/>
    public GeneratedFile EmitClientInterface(
        string tag,
        IReadOnlyList<OperationInfo> tagOperations,
        ClientEmitContext context)
        => this.EmitInterface(this.GetClientName(tag), tagOperations);

    /// <inheritdoc/>
    public GeneratedFile EmitClientImplementation(
        string tag,
        IReadOnlyList<OperationInfo> tagOperations,
        ClientEmitContext context)
        => this.EmitImplementation(this.GetClientName(tag), tagOperations, context.RootServer);

    // The server-side IClientEmitter members default to null (client-only target); no override needed.

    // ── Naming ──────────────────────────────────────────────────────────
    private static string CamelCase(string pascal)
        => pascal.Length == 0 ? pascal : char.ToLowerInvariant(pascal[0]) + pascal[1..];

    private static string RequestModuleName(OperationInfo op) => $"{CamelCase(op.MethodName)}Request";

    private static string ParamsInterfaceName(OperationInfo op) => $"{op.MethodName}Params";

    private static string ResponseClassName(OperationInfo op) => $"{op.MethodName}Response";

    private static string ResponseFactoryName(OperationInfo op) => $"{CamelCase(op.MethodName)}ResponseFactory";

    private string GetClientName(string tag)
    {
        string prefix = this.clientNamePrefix ?? "Api";
        string sanitized = CodeEmitHelpers.SanitizeIdentifier(tag);
        return $"{prefix}{sanitized}";
    }

    private static string OperationMethodMember(OperationMethod method) => method switch
    {
        OperationMethod.Get => "OperationMethod.Get",
        OperationMethod.Put => "OperationMethod.Put",
        OperationMethod.Post => "OperationMethod.Post",
        OperationMethod.Delete => "OperationMethod.Delete",
        OperationMethod.Options => "OperationMethod.Options",
        OperationMethod.Head => "OperationMethod.Head",
        OperationMethod.Patch => "OperationMethod.Patch",
        OperationMethod.Trace => "OperationMethod.Trace",
        OperationMethod.Query => "OperationMethod.Query",
        OperationMethod.Publish => "OperationMethod.Publish",
        OperationMethod.Subscribe => "OperationMethod.Subscribe",
        OperationMethod.Custom => "OperationMethod.Custom",
        _ => "OperationMethod.Get",
    };

    // ── Parameter helpers ───────────────────────────────────────────────
    // Path, query, header, and cookie parameters across the full OpenAPI parameter-style matrix
    // (simple/label/matrix, form/spaceDelimited/pipeDelimited/deepObject, header simple, cookie
    // form/cookie) for scalar, array, and object values. Each parameter is serialized through a single
    // runtime `write*Param` entry point that dispatches on the value shape + style.
    private static ParameterInfo[] PathParams(OperationInfo op)
        => [.. op.Parameters.Where(p => p.Location == ParameterLocation.Path)];

    private static ParameterInfo[] QueryParams(OperationInfo op)
        => [.. op.Parameters.Where(p => p.Location == ParameterLocation.Query)];

    private static ParameterInfo[] HeaderParams(OperationInfo op)
        => [.. op.Parameters.Where(p => p.Location == ParameterLocation.Header)];

    private static ParameterInfo[] CookieParams(OperationInfo op)
        => [.. op.Parameters.Where(p => p.Location == ParameterLocation.Cookie)];

    private static bool IsArrayParam(ParameterInfo p)
        => p.SerializationKind == ParameterSerializationKind.Array;

    // Whether a parameter is one this emitter serializes onto the wire (path/query/header/cookie). The
    // single predicate keeps every gathering/filtering site in lockstep across the four locations.
    private static bool IsSerializedParameter(ParameterInfo p)
        => p.Location is ParameterLocation.Path
            or ParameterLocation.Query
            or ParameterLocation.Header
            or ParameterLocation.Cookie;

    private static string PropertyName(ParameterInfo p) => CamelCase(CodeEmitHelpers.SanitizeIdentifier(p.Name));

    /// <summary>
    /// The TypeScript type of a Phase-1 parameter as it appears in the Params interface. The structural
    /// type implied by the serialization kind is used (<c>string</c> / <c>number</c> / <c>boolean</c> and
    /// arrays of those), rather than the resolved model name, because for an inline scalar/array
    /// parameter schema the model engine emits a validator companion (a value, with <c>evaluate</c>) but
    /// no usable exported TypeScript type — so the interface position must use the structural type. The
    /// companion is still used (by name, in <c>validate()</c>) to evaluate the value against its schema.
    /// </summary>
    private static string ParameterTsType(ParameterInfo p)
    {
        if (IsArrayParam(p))
        {
            return $"{ScalarTsType(p.ElementSerializationKind)}[]";
        }

        return ScalarTsType(p.SerializationKind);
    }

    private static string ScalarTsType(ParameterSerializationKind kind) => kind switch
    {
        ParameterSerializationKind.String => "string",
        ParameterSerializationKind.Boolean => "boolean",
        ParameterSerializationKind.Object => "Record<string, string | number | boolean>",
        ParameterSerializationKind.Array => "unknown[]",
        _ => "number",
    };

    // ── Schema / media-type helpers ─────────────────────────────────────
    private static ContentInfo? JsonContent(ResponseInfo response)
    {
        foreach (ContentInfo content in response.Content)
        {
            if (CodeEmitHelpers.IsJsonMediaType(content.MediaType) && content.SchemaPointer is not null)
            {
                return content;
            }
        }

        return null;
    }

    private string ResponseModelType(ResponseInfo response)
    {
        if (JsonContent(response) is { } content)
        {
            return this.schemaTypeResolver.ResolveTypeName(content.SchemaPointer);
        }

        return "unknown";
    }

    /// <summary>
    /// Selects the request body's primary content entry — the single media type the generated method
    /// serializes — generalizing the former JSON-only body seam across the non-JSON categories.
    /// Preference order: JSON, form-urlencoded, text/plain, then octet-stream (any remaining
    /// non-multipart media type). Multipart is skipped (a later slice): when the only content is
    /// multipart, <see langword="null"/> is returned so the operation falls back to the no-body shape.
    /// </summary>
    private static ContentInfo? PrimaryRequestBodyContent(OperationInfo op)
    {
        if (op.RequestBody is not { } body || body.Content.Length == 0)
        {
            return null;
        }

        ContentInfo? json = null;
        ContentInfo? form = null;
        ContentInfo? text = null;
        ContentInfo? octet = null;

        foreach (ContentInfo content in body.Content)
        {
            // Multipart is out of scope for this slice; skip it so a multipart-only body falls through
            // to the no-body behaviour rather than being mis-categorized.
            if (CodeEmitHelpers.IsMultipartMediaType(content.MediaType)
                || CodeEmitHelpers.IsMultipartMixedMediaType(content.MediaType))
            {
                continue;
            }

            if (json is null && CodeEmitHelpers.IsJsonMediaType(content.MediaType))
            {
                json = content;
            }
            else if (form is null && CodeEmitHelpers.IsFormUrlEncodedMediaType(content.MediaType))
            {
                form = content;
            }
            else if (text is null && CodeEmitHelpers.IsTextPlainMediaType(content.MediaType))
            {
                text = content;
            }
            else if (octet is null)
            {
                // Anything not JSON/form/text/multipart categorizes as a raw binary stream.
                octet = content;
            }
        }

        // The only content was multipart (out of scope) — fall back to the no-body shape.
        return json ?? form ?? text ?? octet;
    }

    /// <summary>
    /// Classifies the primary request body content into a <see cref="ContentCategory"/> the emitter
    /// handles. Multipart is never returned here — <see cref="PrimaryRequestBodyContent"/> has already
    /// skipped it — so anything that is not JSON / form-urlencoded / text/plain is a raw binary stream.
    /// </summary>
    private static ContentCategory RequestBodyCategory(ContentInfo content)
    {
        if (CodeEmitHelpers.IsJsonMediaType(content.MediaType))
        {
            return ContentCategory.Json;
        }

        if (CodeEmitHelpers.IsFormUrlEncodedMediaType(content.MediaType))
        {
            return ContentCategory.FormUrlEncoded;
        }

        if (CodeEmitHelpers.IsTextPlainMediaType(content.MediaType))
        {
            return ContentCategory.TextPlain;
        }

        return ContentCategory.OctetStream;
    }

    /// <summary>
    /// Resolves the form-urlencoded request body's TypeScript param type: the resolved object model
    /// type when it is a usable named type, otherwise <c>FormBody</c>. The call site always casts to
    /// <c>FormBody</c> when this is a named type, so a validator-only companion still type-checks.
    /// </summary>
    private string FormBodyParamType(ContentInfo content)
    {
        string resolved = content.SchemaPointer is null
            ? "unknown"
            : this.schemaTypeResolver.ResolveTypeName(content.SchemaPointer);
        return resolved == "unknown" ? "FormBody" : resolved;
    }

    // ── Request module ──────────────────────────────────────────────────
    private GeneratedFile EmitRequestObject(OperationInfo op)
    {
        ParameterInfo[] pathParams = PathParams(op);
        ParameterInfo[] queryParams = QueryParams(op);
        ParameterInfo[] headerParams = HeaderParams(op);
        ParameterInfo[] cookieParams = CookieParams(op);
        bool hasParams = pathParams.Length > 0
            || queryParams.Length > 0
            || headerParams.Length > 0
            || cookieParams.Length > 0;

        return hasParams
            ? this.EmitRequestFactory(op, pathParams, queryParams, headerParams, cookieParams)
            : this.EmitRequestConst(op);
    }

    // Phase-0 path: no parameters. The request is a const ApiRequest object (back-compat).
    private GeneratedFile EmitRequestConst(OperationInfo op)
    {
        string moduleName = RequestModuleName(op);
        IndentedWriter w = new();
        w.IndentString = "  ";

        EmitHeader(w);
        w.WriteLine(
            "import { OperationMethod, ValidationMode } from " +
            $"{StringLiteral(this.options.ClientRuntimeModuleSpecifier)};");
        w.WriteLine(
            "import type { ApiRequest, ByteWriter, HeaderSink } from " +
            $"{StringLiteral(this.options.ClientRuntimeModuleSpecifier)};");
        w.WriteLine();

        string[] acceptMediaTypes = AcceptMediaTypes(op);
        bool hasAccept = acceptMediaTypes.Length > 0;

        w.WriteLine("/**");
        w.WriteLine($" * Request for the {op.MethodName} operation.");
        w.WriteLine(" *");
        w.WriteLine(" * Composes the wire request byte-natively: the transport calls the write* methods");
        w.WriteLine(" * synchronously before any async I/O.");
        w.WriteLine(" */");
        w.WriteLine($"export const {moduleName}: ApiRequest = {{");
        w.PushIndent();
        w.WriteLine($"method: {OperationMethodMember(op.Method)},");
        w.WriteLine($"pathTemplate: {StringLiteral(op.PathTemplate)},");
        w.WriteLine("hasPathParameters: false,");
        w.WriteLine("hasQueryParameters: false,");
        w.WriteLine($"hasHeaderParameters: {(hasAccept ? "true" : "false")},");
        w.WriteLine("hasCookieParameters: false,");

        w.WriteLine("writeResolvedPath(writer: ByteWriter): void {");
        w.PushIndent();
        w.WriteLine($"writer.writeAscii({StringLiteral(op.PathTemplate)});");
        w.PopIndent();
        w.WriteLine("},");

        w.WriteLine("writeQueryString(_writer: ByteWriter): number {");
        w.PushIndent();
        w.WriteLine("return 0;");
        w.PopIndent();
        w.WriteLine("},");

        if (hasAccept)
        {
            string acceptValue = string.Join(", ", acceptMediaTypes);
            w.WriteLine("writeHeaders(sink: HeaderSink): void {");
            w.PushIndent();
            w.WriteLine($"sink(\"Accept\", {StringLiteral(acceptValue)});");
            w.PopIndent();
            w.WriteLine("},");
        }
        else
        {
            w.WriteLine("writeHeaders(_sink: HeaderSink): void {");
            w.PushIndent();
            w.WriteLine("/* no headers */");
            w.PopIndent();
            w.WriteLine("},");
        }

        w.WriteLine("writeCookies(_writer: ByteWriter): number {");
        w.PushIndent();
        w.WriteLine("return 0;");
        w.PopIndent();
        w.WriteLine("},");

        w.WriteLine("validate(_mode: ValidationMode = ValidationMode.Basic): void {");
        w.PushIndent();
        w.WriteLine("/* no parameters to validate */");
        w.PopIndent();
        w.WriteLine("},");

        w.PopIndent();
        w.WriteLine("};");

        return new GeneratedFile($"{moduleName}.ts", w.ToString());
    }

    // Parameters present. The request is a factory capturing the params in a closure.
    private GeneratedFile EmitRequestFactory(
        OperationInfo op,
        ParameterInfo[] pathParams,
        ParameterInfo[] queryParams,
        ParameterInfo[] headerParams,
        ParameterInfo[] cookieParams)
    {
        string moduleName = RequestModuleName(op);
        string paramsInterface = ParamsInterfaceName(op);
        IndentedWriter w = new();
        w.IndentString = "  ";

        // Which runtime style helpers does this operation need? Each location maps to one generalized
        // entry point that dispatches on the value shape + style internally.
        bool needsPath = pathParams.Length > 0;
        bool needsQuery = queryParams.Length > 0;
        bool needsHeader = headerParams.Length > 0;
        bool needsCookie = cookieParams.Length > 0;

        // Does any parameter have a named model type with an `evaluate` companion for validate()?
        bool hasValidatable = op.Parameters.Any(p =>
            IsSerializedParameter(p)
            && this.schemaTypeResolver.ResolveTypeName(p.SchemaPointer) != "unknown");

        EmitHeader(w);
        w.WriteLine(
            "import { OperationMethod, ValidationMode } from " +
            $"{StringLiteral(this.options.ClientRuntimeModuleSpecifier)};");
        w.WriteLine(
            "import type { ApiRequest, ByteWriter, HeaderSink } from " +
            $"{StringLiteral(this.options.ClientRuntimeModuleSpecifier)};");

        // Import only the style helpers this operation actually uses (keeps the import list minimal).
        // Each location maps to one generalized entry point regardless of the parameter's style/shape.
        List<string> styleImports = [];
        if (needsPath)
        {
            styleImports.Add("writePathParam");
        }

        if (needsQuery)
        {
            styleImports.Add("writeQueryParam");
        }

        if (needsHeader)
        {
            styleImports.Add("writeHeaderParam");
        }

        if (needsCookie)
        {
            styleImports.Add("writeCookieParam");
        }

        if (styleImports.Count > 0)
        {
            w.WriteLine(
                $"import {{ {string.Join(", ", styleImports)} }} from " +
                $"{StringLiteral(this.options.ClientRuntimeModuleSpecifier)};");
        }

        // Import the validator companions used by validate(). These are the model-engine consts (values,
        // each exposing `evaluate`), imported by name from the models module. They are values, not types,
        // so this is a regular (not `import type`) import.
        HashSet<string> validatorCompanions = new(StringComparer.Ordinal);
        foreach (ParameterInfo p in op.Parameters)
        {
            if (!IsSerializedParameter(p))
            {
                continue;
            }

            string resolved = this.schemaTypeResolver.ResolveTypeName(p.SchemaPointer);
            if (resolved != "unknown")
            {
                validatorCompanions.Add(resolved);
            }
        }

        if (validatorCompanions.Count > 0)
        {
            string imports = string.Join(", ", validatorCompanions.OrderBy(m => m, StringComparer.Ordinal));
            w.WriteLine($"import {{ {imports} }} from {StringLiteral(this.options.ModelsModuleSpecifier)};");
        }

        w.WriteLine();

        // The Params interface: required parameters required, optional parameters `?`.
        EmitParamsInterface(w, op, paramsInterface);
        w.WriteLine();

        string[] acceptMediaTypes = AcceptMediaTypes(op);
        bool hasAccept = acceptMediaTypes.Length > 0;
        bool hasHeaders = headerParams.Length > 0 || hasAccept;

        w.WriteLine("/**");
        w.WriteLine($" * Builds the request for the {op.MethodName} operation, capturing the supplied parameters.");
        w.WriteLine(" *");
        w.WriteLine(" * Composes the wire request byte-natively: the transport calls the returned object's write*");
        w.WriteLine(" * methods synchronously before any async I/O.");
        w.WriteLine($" * @param params The {op.MethodName} parameters.");
        w.WriteLine(" * @returns The composed request.");
        w.WriteLine(" */");
        w.WriteLine($"export function {moduleName}(params: {paramsInterface}): ApiRequest {{");
        w.PushIndent();
        w.WriteLine("return {");
        w.PushIndent();
        w.WriteLine($"method: {OperationMethodMember(op.Method)},");
        w.WriteLine($"pathTemplate: {StringLiteral(op.PathTemplate)},");
        w.WriteLine($"hasPathParameters: {(pathParams.Length > 0 ? "true" : "false")},");
        w.WriteLine($"hasQueryParameters: {(queryParams.Length > 0 ? "true" : "false")},");
        w.WriteLine($"hasHeaderParameters: {(hasHeaders ? "true" : "false")},");
        w.WriteLine($"hasCookieParameters: {(cookieParams.Length > 0 ? "true" : "false")},");

        EmitFactoryWriteResolvedPath(w, op.PathTemplate, pathParams);
        EmitFactoryWriteQueryString(w, queryParams);
        EmitFactoryWriteHeaders(w, headerParams, acceptMediaTypes);
        EmitFactoryWriteCookies(w, cookieParams);

        this.EmitFactoryValidate(w, op, hasValidatable);

        w.PopIndent();
        w.WriteLine("};");
        w.PopIndent();
        w.WriteLine("}");

        return new GeneratedFile($"{moduleName}.ts", w.ToString());
    }

    private static void EmitParamsInterface(IndentedWriter w, OperationInfo op, string paramsInterface)
    {
        w.WriteLine("/**");
        w.WriteLine($" * Parameters for the {op.MethodName} operation.");
        w.WriteLine(" */");
        w.WriteLine($"export interface {paramsInterface} {{");
        w.PushIndent();

        foreach (ParameterInfo p in op.Parameters)
        {
            if (!IsSerializedParameter(p))
            {
                continue;
            }

            string propertyName = PropertyName(p);
            string tsType = ParameterTsType(p);
            string optional = p.IsRequired ? string.Empty : "?";

            w.WriteLine("/**");
            w.WriteLine($" * The {p.Name} {LocationWord(p.Location)} parameter.");
            w.WriteLine(" */");
            w.WriteLine($"readonly {propertyName}{optional}: {tsType};");
        }

        w.PopIndent();
        w.WriteLine("}");
    }

    private static string LocationWord(ParameterLocation location) => location switch
    {
        ParameterLocation.Path => "path",
        ParameterLocation.Query => "query",
        ParameterLocation.Header => "header",
        ParameterLocation.Cookie => "cookie",
        _ => "",
    };

    private static string StyleWord(ParameterStyle style) => style switch
    {
        ParameterStyle.Simple => "simple",
        ParameterStyle.Label => "label",
        ParameterStyle.Matrix => "matrix",
        ParameterStyle.Form => "form",
        ParameterStyle.SpaceDelimited => "spaceDelimited",
        ParameterStyle.PipeDelimited => "pipeDelimited",
        ParameterStyle.DeepObject => "deepObject",
        ParameterStyle.Cookie => "cookie",
        _ => "simple",
    };

    private static void EmitFactoryWriteResolvedPath(
        IndentedWriter w,
        string pathTemplate,
        ParameterInfo[] pathParams)
    {
        w.WriteLine("writeResolvedPath(writer: ByteWriter): void {");
        w.PushIndent();

        if (pathParams.Length == 0)
        {
            w.WriteLine($"writer.writeAscii({StringLiteral(pathTemplate)});");
            w.PopIndent();
            w.WriteLine("},");
            return;
        }

        // Walk the template, writing literal segments and substituting {name} via the style helper.
        ReadOnlySpan<char> remaining = pathTemplate;
        while (remaining.Length > 0)
        {
            int openBrace = remaining.IndexOf('{');
            if (openBrace < 0)
            {
                w.WriteLine($"writer.writeAscii({StringLiteral(remaining.ToString())});");
                break;
            }

            if (openBrace > 0)
            {
                w.WriteLine($"writer.writeAscii({StringLiteral(remaining[..openBrace].ToString())});");
            }

            int closeBrace = remaining[(openBrace + 1)..].IndexOf('}');
            if (closeBrace < 0)
            {
                w.WriteLine($"writer.writeAscii({StringLiteral(remaining[openBrace..].ToString())});");
                break;
            }

            string paramName = remaining[(openBrace + 1)..(openBrace + 1 + closeBrace)].ToString();
            ParameterInfo? match = null;
            foreach (ParameterInfo p in pathParams)
            {
                if (p.Name == paramName)
                {
                    match = p;
                    break;
                }
            }

            if (match is { } mp)
            {
                string propertyName = PropertyName(mp);
                string explode = mp.Explode ? "true" : "false";
                string allowReserved = mp.AllowReserved ? "true" : "false";
                w.WriteLine(
                    $"writePathParam(writer, {StringLiteral(mp.Name)}, params.{propertyName}, " +
                    $"{StringLiteral(StyleWord(mp.Style))}, {explode}, {allowReserved});");
            }
            else
            {
                // Unmatched placeholder — write the template token literally (defensive; should not occur).
                w.WriteLine($"writer.writeAscii({StringLiteral("{" + paramName + "}")});");
            }

            remaining = remaining[(openBrace + 1 + closeBrace + 1)..];
        }

        w.PopIndent();
        w.WriteLine("},");
    }

    private static void EmitFactoryWriteQueryString(IndentedWriter w, ParameterInfo[] queryParams)
    {
        w.WriteLine("writeQueryString(writer: ByteWriter): number {");
        w.PushIndent();

        if (queryParams.Length == 0)
        {
            w.WriteLine("return 0;");
            w.PopIndent();
            w.WriteLine("},");
            return;
        }

        w.WriteLine("let written = 0;");

        foreach (ParameterInfo p in queryParams)
        {
            string propertyName = PropertyName(p);
            string explode = p.Explode ? "true" : "false";
            string allowReserved = p.AllowReserved ? "true" : "false";

            string styleWord = StyleWord(p.Style);

            if (p.IsRequired)
            {
                w.WriteLine(
                    $"written += writeQueryParam(writer, {StringLiteral(p.Name)}, params.{propertyName}, " +
                    $"{StringLiteral(styleWord)}, {explode}, {allowReserved}, written === 0);");
            }
            else
            {
                w.WriteLine($"if (params.{propertyName} !== undefined) {{");
                w.PushIndent();
                w.WriteLine(
                    $"written += writeQueryParam(writer, {StringLiteral(p.Name)}, params.{propertyName}, " +
                    $"{StringLiteral(styleWord)}, {explode}, {allowReserved}, written === 0);");
                w.PopIndent();
                w.WriteLine("}");
            }
        }

        w.WriteLine("return written;");
        w.PopIndent();
        w.WriteLine("},");
    }

    private static void EmitFactoryWriteHeaders(
        IndentedWriter w,
        ParameterInfo[] headerParams,
        string[] acceptMediaTypes)
    {
        bool hasAccept = acceptMediaTypes.Length > 0;
        bool emitsAnything = headerParams.Length > 0 || hasAccept;

        w.WriteLine(emitsAnything ? "writeHeaders(sink: HeaderSink): void {" : "writeHeaders(_sink: HeaderSink): void {");
        w.PushIndent();

        if (!emitsAnything)
        {
            w.WriteLine("/* no headers */");
            w.PopIndent();
            w.WriteLine("},");
            return;
        }

        if (hasAccept)
        {
            string acceptValue = string.Join(", ", acceptMediaTypes);
            w.WriteLine($"sink(\"Accept\", {StringLiteral(acceptValue)});");
        }

        foreach (ParameterInfo p in headerParams)
        {
            string propertyName = PropertyName(p);
            string explode = p.Explode ? "true" : "false";

            if (p.IsRequired)
            {
                w.WriteLine(
                    $"sink({StringLiteral(p.Name)}, writeHeaderParam(params.{propertyName}, {explode}));");
            }
            else
            {
                w.WriteLine($"if (params.{propertyName} !== undefined) {{");
                w.PushIndent();
                w.WriteLine(
                    $"sink({StringLiteral(p.Name)}, writeHeaderParam(params.{propertyName}, {explode}));");
                w.PopIndent();
                w.WriteLine("}");
            }
        }

        w.PopIndent();
        w.WriteLine("},");
    }

    private static void EmitFactoryWriteCookies(IndentedWriter w, ParameterInfo[] cookieParams)
    {
        if (cookieParams.Length == 0)
        {
            w.WriteLine("writeCookies(_writer: ByteWriter): number {");
            w.PushIndent();
            w.WriteLine("return 0;");
            w.PopIndent();
            w.WriteLine("},");
            return;
        }

        w.WriteLine("writeCookies(writer: ByteWriter): number {");
        w.PushIndent();
        w.WriteLine("let written = 0;");

        foreach (ParameterInfo p in cookieParams)
        {
            string propertyName = PropertyName(p);
            string explode = p.Explode ? "true" : "false";
            string styleWord = StyleWord(p.Style);

            if (p.IsRequired)
            {
                w.WriteLine(
                    $"written += writeCookieParam(writer, {StringLiteral(p.Name)}, params.{propertyName}, " +
                    $"{StringLiteral(styleWord)}, {explode}, written === 0);");
            }
            else
            {
                w.WriteLine($"if (params.{propertyName} !== undefined) {{");
                w.PushIndent();
                w.WriteLine(
                    $"written += writeCookieParam(writer, {StringLiteral(p.Name)}, params.{propertyName}, " +
                    $"{StringLiteral(styleWord)}, {explode}, written === 0);");
                w.PopIndent();
                w.WriteLine("}");
            }
        }

        w.WriteLine("return written;");
        w.PopIndent();
        w.WriteLine("},");
    }

    private void EmitFactoryValidate(IndentedWriter w, OperationInfo op, bool hasValidatable)
    {
        w.WriteLine("validate(mode: ValidationMode = ValidationMode.Basic): void {");
        w.PushIndent();

        if (!hasValidatable)
        {
            w.WriteLine("/* no parameters with a schema to validate */");
            w.PopIndent();
            w.WriteLine("},");
            return;
        }

        w.WriteLine("if (mode === ValidationMode.None) {");
        w.PushIndent();
        w.WriteLine("return;");
        w.PopIndent();
        w.WriteLine("}");

        foreach (ParameterInfo p in op.Parameters)
        {
            if (!IsSerializedParameter(p))
            {
                continue;
            }

            string modelType = this.schemaTypeResolver.ResolveTypeName(p.SchemaPointer);
            if (modelType == "unknown")
            {
                continue;
            }

            string propertyName = PropertyName(p);
            string guard = p.IsRequired
                ? $"if (!{modelType}.evaluate(params.{propertyName}))"
                : $"if (params.{propertyName} !== undefined && !{modelType}.evaluate(params.{propertyName}))";

            w.WriteLine(guard + " {");
            w.PushIndent();
            w.WriteLine(
                $"throw new Error({StringLiteral($"{op.MethodName} parameter '{p.Name}' failed schema validation.")});");
            w.PopIndent();
            w.WriteLine("}");
        }

        w.PopIndent();
        w.WriteLine("},");
    }

    private string[] AcceptMediaTypes(OperationInfo op)
        => CodeEmitHelpers.GetAcceptMediaTypes(
            op.Responses
                .SelectMany(r => r.Content)
                .Select(c => (c.MediaType, c.SchemaPointer?.PositionalPointer)));

    // ── Response module ─────────────────────────────────────────────────
    private GeneratedFile EmitResponseClass(OperationInfo op)
    {
        string className = ResponseClassName(op);
        string factoryName = ResponseFactoryName(op);
        IndentedWriter w = new();
        w.IndentString = "  ";

        // Order responses: concrete 2xx first, then default/error.
        ResponseInfo[] responses = [.. op.Responses];

        // The distinct model type names referenced by JSON responses (imported by Ts_FinalName).
        HashSet<string> modelTypes = new(StringComparer.Ordinal);
        foreach (ResponseInfo response in responses)
        {
            if (JsonContent(response) is not null)
            {
                modelTypes.Add(this.ResponseModelType(response));
            }
        }

        EmitHeader(w);
        w.WriteLine(
            "import { ValidationMode } from " +
            $"{StringLiteral(this.options.ClientRuntimeModuleSpecifier)};");
        w.WriteLine(
            "import type { ApiResponse, ResponseContext, ResponseFactory } from " +
            $"{StringLiteral(this.options.ClientRuntimeModuleSpecifier)};");

        modelTypes.Remove("unknown");
        if (modelTypes.Count > 0)
        {
            string imports = string.Join(", ", modelTypes.OrderBy(m => m, StringComparer.Ordinal));
            w.WriteLine($"import {{ {imports} }} from {StringLiteral(this.options.ModelsModuleSpecifier)};");
        }

        w.WriteLine();

        w.WriteLine("/**");
        w.WriteLine($" * Response for the {op.MethodName} operation.");
        w.WriteLine(" *");
        w.WriteLine(" * Decomposes and validates the response bytes via the model companions.");
        w.WriteLine(" */");
        w.WriteLine($"export class {className} implements ApiResponse {{");
        w.PushIndent();

        w.WriteLine("readonly statusCode: number;");
        w.WriteLine("private readonly bytes: Uint8Array | null;");
        w.WriteLine();

        w.WriteLine("private constructor(statusCode: number, bytes: Uint8Array | null) {");
        w.PushIndent();
        w.WriteLine("this.statusCode = statusCode;");
        w.WriteLine("this.bytes = bytes;");
        w.PopIndent();
        w.WriteLine("}");
        w.WriteLine();

        // Internal factory the ResponseFactory delegates to.
        w.WriteLine("static async createFrom(context: ResponseContext): Promise<" + className + "> {");
        w.PushIndent();
        w.WriteLine("const bytes = context.body === null ? null : await readAllBytes(context.body);");
        w.WriteLine($"return new {className}(context.statusCode, bytes);");
        w.PopIndent();
        w.WriteLine("}");
        w.WriteLine();

        w.WriteLine("get isSuccess(): boolean {");
        w.PushIndent();
        w.WriteLine("return this.statusCode >= 200 && this.statusCode < 300;");
        w.PopIndent();
        w.WriteLine("}");

        // Per-status typed accessors via the model companion `Type.parse`.
        foreach (ResponseInfo response in responses)
        {
            if (JsonContent(response) is null)
            {
                continue;
            }

            string modelType = this.ResponseModelType(response);
            string accessor = $"tryGet{StatusAccessor(response.StatusCode)}";
            string statusGuard = StatusGuard(response.StatusCode);

            w.WriteLine();
            w.WriteLine("/**");
            w.WriteLine($" * Returns the {response.StatusCode} response body parsed via the model companion,");
            w.WriteLine($" * or {(modelType == "unknown" ? "the raw value" : "undefined")} when the status does not match.");
            w.WriteLine(" */");
            string returnType = modelType == "unknown" ? "unknown" : modelType;
            w.WriteLine($"{accessor}(): {returnType} | undefined {{");
            w.PushIndent();
            w.WriteLine($"if (!({statusGuard}) || this.bytes === null) {{");
            w.PushIndent();
            w.WriteLine("return undefined;");
            w.PopIndent();
            w.WriteLine("}");
            if (modelType == "unknown")
            {
                w.WriteLine("return JSON.parse(new TextDecoder().decode(this.bytes));");
            }
            else
            {
                w.WriteLine($"return {modelType}.parse(this.bytes);");
            }

            w.PopIndent();
            w.WriteLine("}");
        }

        // match — discriminates on status and dispatches to the supplied handlers.
        EmitMatch(w, responses, this);

        // validate — evaluate the active body via the matching model companion.
        w.WriteLine();
        w.WriteLine("/** Validates the active response body against its schema; throws on failure. */");
        w.WriteLine("validate(mode: ValidationMode = ValidationMode.None): void {");
        w.PushIndent();
        w.WriteLine("if (mode === ValidationMode.None || this.bytes === null) {");
        w.PushIndent();
        w.WriteLine("return;");
        w.PopIndent();
        w.WriteLine("}");
        foreach (ResponseInfo response in responses)
        {
            string modelType = this.ResponseModelType(response);
            if (JsonContent(response) is null || modelType == "unknown")
            {
                continue;
            }

            string statusGuard = StatusGuard(response.StatusCode);
            w.WriteLine($"if ({statusGuard}) {{");
            w.PushIndent();
            w.WriteLine($"if (!{modelType}.evaluate(this.bytes)) {{");
            w.PushIndent();
            w.WriteLine(
                $"throw new Error(`{op.MethodName} ${{this.statusCode}} response failed schema validation.`);");
            w.PopIndent();
            w.WriteLine("}");
            w.WriteLine("return;");
            w.PopIndent();
            w.WriteLine("}");
        }

        w.PopIndent();
        w.WriteLine("}");

        // AsyncDisposable — Phase 0 buffers the body, so disposal is a no-op.
        w.WriteLine();
        w.WriteLine("async [Symbol.asyncDispose](): Promise<void> {");
        w.PushIndent();
        w.WriteLine("/* body fully buffered; nothing to dispose */");
        w.PopIndent();
        w.WriteLine("}");

        w.PopIndent();
        w.WriteLine("}");
        w.WriteLine();

        // The exported ResponseFactory the client passes to transport.send.
        w.WriteLine("/** The factory the transport uses to build the typed response from the wire result. */");
        w.WriteLine($"export const {factoryName}: ResponseFactory<{className}> = {{");
        w.PushIndent();
        w.WriteLine($"create(context: ResponseContext): Promise<{className}> {{");
        w.PushIndent();
        w.WriteLine($"return {className}.createFrom(context);");
        w.PopIndent();
        w.WriteLine("},");
        w.PopIndent();
        w.WriteLine("};");

        // Shared body-reading helper (kept local to the module to avoid runtime coupling in Phase 0).
        w.WriteLine();
        w.WriteLine("async function readAllBytes(stream: ReadableStream<Uint8Array>): Promise<Uint8Array> {");
        w.PushIndent();
        w.WriteLine("const reader = stream.getReader();");
        w.WriteLine("const chunks: Uint8Array[] = [];");
        w.WriteLine("let total = 0;");
        w.WriteLine("for (;;) {");
        w.PushIndent();
        w.WriteLine("const { done, value } = await reader.read();");
        w.WriteLine("if (done) {");
        w.PushIndent();
        w.WriteLine("break;");
        w.PopIndent();
        w.WriteLine("}");
        w.WriteLine("if (value !== undefined) {");
        w.PushIndent();
        w.WriteLine("chunks.push(value);");
        w.WriteLine("total += value.length;");
        w.PopIndent();
        w.WriteLine("}");
        w.PopIndent();
        w.WriteLine("}");
        w.WriteLine("const result = new Uint8Array(total);");
        w.WriteLine("let offset = 0;");
        w.WriteLine("for (const chunk of chunks) {");
        w.PushIndent();
        w.WriteLine("result.set(chunk, offset);");
        w.WriteLine("offset += chunk.length;");
        w.PopIndent();
        w.WriteLine("}");
        w.WriteLine("return result;");
        w.PopIndent();
        w.WriteLine("}");

        return new GeneratedFile($"{className}.ts", w.ToString());
    }

    private static void EmitMatch(IndentedWriter w, ResponseInfo[] responses, TypeScriptApiEmitter self)
    {
        // Build a handler-bag type and a match() that dispatches by status.
        ResponseInfo[] jsonResponses = [.. responses.Where(r => JsonContent(r) is not null)];
        if (jsonResponses.Length == 0)
        {
            return;
        }

        w.WriteLine();
        w.WriteLine("/**");
        w.WriteLine(" * Dispatches on the response status, invoking the handler for the matching case.");
        w.WriteLine(" * Returns the handler's result, or the `otherwise` result when no case matches.");
        w.WriteLine(" */");
        w.WriteLine("match<T>(handlers: {");
        w.PushIndent();
        foreach (ResponseInfo response in jsonResponses)
        {
            string handlerName = CamelCase(StatusAccessor(response.StatusCode));
            string modelType = self.ResponseModelType(response);
            string argType = modelType == "unknown" ? "unknown" : modelType;
            w.WriteLine($"{handlerName}?: (body: {argType}) => T;");
        }

        w.WriteLine("otherwise?: (statusCode: number) => T;");
        w.PopIndent();
        w.WriteLine("}): T | undefined {");
        w.PushIndent();
        foreach (ResponseInfo response in jsonResponses)
        {
            string handlerName = CamelCase(StatusAccessor(response.StatusCode));
            string accessor = $"tryGet{StatusAccessor(response.StatusCode)}";
            string statusGuard = StatusGuard(response.StatusCode);
            w.WriteLine($"if ({statusGuard} && handlers.{handlerName} !== undefined) {{");
            w.PushIndent();
            w.WriteLine($"const body = this.{accessor}();");
            w.WriteLine("if (body !== undefined) {");
            w.PushIndent();
            w.WriteLine($"return handlers.{handlerName}(body);");
            w.PopIndent();
            w.WriteLine("}");
            w.PopIndent();
            w.WriteLine("}");
        }

        w.WriteLine("return handlers.otherwise?.(this.statusCode);");
        w.PopIndent();
        w.WriteLine("}");
    }

    private static string StatusAccessor(string statusCode)
        => statusCode == "default" ? "Default" : CodeEmitHelpers.StatusCodeToName(statusCode);

    private static string StatusGuard(string statusCode)
    {
        if (statusCode == "default")
        {
            // The `default` response matches any status not otherwise handled — Phase 0 treats it as a
            // catch-all gated only on non-2xx so the common 200 + default pattern discriminates cleanly.
            return "(this.statusCode < 200 || this.statusCode >= 300)";
        }

        if (statusCode.Length == 3 && (statusCode[1] == 'X' || statusCode[1] == 'x'))
        {
            int lo = (statusCode[0] - '0') * 100;
            return $"(this.statusCode >= {lo} && this.statusCode < {lo + 100})";
        }

        return $"(this.statusCode === {statusCode})";
    }

    // ── Client interface ────────────────────────────────────────────────
    private GeneratedFile EmitInterface(string clientName, IReadOnlyList<OperationInfo> operations)
    {
        string interfaceName = $"I{clientName}Client";
        IndentedWriter w = new();
        w.IndentString = "  ";

        EmitHeader(w);

        // A form-urlencoded body whose param type falls back to `FormBody` references that runtime type
        // in its method signature, so the interface imports it from the client-runtime module.
        if (this.AnyFormBodyParam(operations))
        {
            w.WriteLine(
                "import type { FormBody } from " +
                $"{StringLiteral(this.options.ClientRuntimeModuleSpecifier)};");
        }

        // Import each operation's response class (the method return types) and Params interface.
        foreach (OperationInfo op in operations)
        {
            string responseClass = ResponseClassName(op);
            w.WriteLine($"import type {{ {responseClass} }} from {StringLiteral($"./{responseClass}.js")};");
            if (HasParams(op))
            {
                string requestModule = RequestModuleName(op);
                string paramsInterface = ParamsInterfaceName(op);
                w.WriteLine($"import type {{ {paramsInterface} }} from {StringLiteral($"./{requestModule}.js")};");
            }
        }

        // Import named request-body model types referenced in method signatures (JSON + named form).
        HashSet<string> bodyTypes = this.RequestBodyModelImports(operations);
        if (bodyTypes.Count > 0)
        {
            string imports = string.Join(", ", bodyTypes.OrderBy(m => m, StringComparer.Ordinal));
            w.WriteLine($"import type {{ {imports} }} from {StringLiteral(this.options.ModelsModuleSpecifier)};");
        }

        w.WriteLine();

        w.WriteLine("/**");
        w.WriteLine($" * Client interface for the {clientName} API operations.");
        w.WriteLine(" */");
        w.WriteLine($"export interface {interfaceName} extends AsyncDisposable {{");
        w.PushIndent();

        for (int i = 0; i < operations.Count; i++)
        {
            if (i > 0)
            {
                w.WriteLine();
            }

            OperationInfo op = operations[i];
            EmitMethodDoc(w, op);
            string methodName = CamelCase(op.MethodName);
            string responseClass = ResponseClassName(op);
            string signature = this.BuildMethodSignature(op);
            w.WriteLine($"{methodName}({signature}): Promise<{responseClass}>;");
        }

        w.PopIndent();
        w.WriteLine("}");

        return new GeneratedFile($"{interfaceName}.ts", w.ToString());
    }

    private static bool HasParams(OperationInfo op)
        => op.Parameters.Any(IsSerializedParameter);

    /// <summary>
    /// Builds the TypeScript method parameter list shared by the interface and implementation:
    /// <c>params</c> (when the operation has parameters), then <c>body</c> (when it has a request body
    /// in a category this emitter serializes), then an optional <c>signal</c>. The whole <c>params</c>
    /// argument is optional when every parameter is optional. The <c>body</c> type is per content
    /// category: JSON → the model type (or <c>unknown</c>); form-urlencoded → the object model type or
    /// <c>FormBody</c>; octet-stream → <c>Uint8Array | ReadableStream&lt;Uint8Array&gt;</c>; text/plain
    /// → <c>string</c>.
    /// </summary>
    private string BuildMethodSignature(OperationInfo op)
    {
        List<string> parts = [];

        if (HasParams(op))
        {
            bool allOptional = op.Parameters
                .Where(IsSerializedParameter)
                .All(p => !p.IsRequired);
            string optional = allOptional ? "?" : string.Empty;
            parts.Add($"params{optional}: {ParamsInterfaceName(op)}");
        }

        if (PrimaryRequestBodyContent(op) is { } content)
        {
            bool bodyRequired = op.RequestBody is { } rb && rb.IsRequired;
            string bodyTsType = this.BodyParamType(content);
            parts.Add(bodyRequired ? $"body: {bodyTsType}" : $"body?: {bodyTsType}");
        }

        parts.Add("signal?: AbortSignal");
        return string.Join(", ", parts);
    }

    /// <summary>
    /// The TypeScript <c>body</c> parameter type for the request body's primary content category.
    /// </summary>
    private string BodyParamType(ContentInfo content) => RequestBodyCategory(content) switch
    {
        ContentCategory.Json => this.RequestBodyJsonType(content),
        ContentCategory.FormUrlEncoded => this.FormBodyParamType(content),
        ContentCategory.OctetStream => "Uint8Array | ReadableStream<Uint8Array>",
        ContentCategory.TextPlain => "string",
        _ => "unknown",
    };

    /// <summary>
    /// The TypeScript type of a JSON request body's primary content: the resolved model type, or
    /// <c>unknown</c> when the schema is absent or unmapped.
    /// </summary>
    private string RequestBodyJsonType(ContentInfo content)
        => content.SchemaPointer is null
            ? "unknown"
            : this.schemaTypeResolver.ResolveTypeName(content.SchemaPointer);

    /// <summary>
    /// Whether any operation has a form-urlencoded request body (so the implementation imports
    /// <c>formUrlEncodedBytes</c> from the client-runtime module).
    /// </summary>
    private static bool AnyFormUrlEncodedBody(IReadOnlyList<OperationInfo> operations)
    {
        foreach (OperationInfo op in operations)
        {
            if (PrimaryRequestBodyContent(op) is { } content
                && RequestBodyCategory(content) == ContentCategory.FormUrlEncoded)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether any operation has a form-urlencoded body whose <c>body</c> param type falls back to the
    /// runtime <c>FormBody</c> type (so the interface/implementation imports it).
    /// </summary>
    private bool AnyFormBodyParam(IReadOnlyList<OperationInfo> operations)
    {
        foreach (OperationInfo op in operations)
        {
            if (PrimaryRequestBodyContent(op) is { } content
                && RequestBodyCategory(content) == ContentCategory.FormUrlEncoded
                && this.FormBodyParamType(content) == "FormBody")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The named model types referenced as request-body param types across the operations (JSON model
    /// types, plus a form-urlencoded body's named object model type when it is usable), imported by
    /// <c>Ts_FinalName</c> alongside the JSON body companions.
    /// </summary>
    private HashSet<string> RequestBodyModelImports(IReadOnlyList<OperationInfo> operations)
    {
        HashSet<string> bodyTypes = new(StringComparer.Ordinal);
        foreach (OperationInfo op in operations)
        {
            if (PrimaryRequestBodyContent(op) is not { } content)
            {
                continue;
            }

            ContentCategory category = RequestBodyCategory(content);
            if (category == ContentCategory.Json)
            {
                string jsonType = this.RequestBodyJsonType(content);
                if (jsonType != "unknown")
                {
                    bodyTypes.Add(jsonType);
                }
            }
            else if (category == ContentCategory.FormUrlEncoded)
            {
                string formType = this.FormBodyParamType(content);
                if (formType != "FormBody")
                {
                    bodyTypes.Add(formType);
                }
            }
        }

        return bodyTypes;
    }

    // ── Client implementation ───────────────────────────────────────────
    private GeneratedFile EmitImplementation(
        string clientName,
        IReadOnlyList<OperationInfo> operations,
        ServerInfo? serverInfo)
    {
        string className = $"{clientName}Client";
        string interfaceName = $"I{clientName}Client";
        IndentedWriter w = new();
        w.IndentString = "  ";

        bool anyForm = AnyFormUrlEncodedBody(operations);

        EmitHeader(w);

        // The form-urlencoded `FormBody` type is needed in the implementation whenever any operation has
        // a form body — either as the `body` param type, or as the `body as FormBody` cast target.
        string runtimeTypeImports = anyForm ? "ApiTransport, FormBody, RequestBody" : "ApiTransport, RequestBody";
        w.WriteLine(
            $"import type {{ {runtimeTypeImports} }} from " +
            $"{StringLiteral(this.options.ClientRuntimeModuleSpecifier)};");
        if (anyForm)
        {
            w.WriteLine(
                "import { formUrlEncodedBytes } from " +
                $"{StringLiteral(this.options.ClientRuntimeModuleSpecifier)};");
        }

        w.WriteLine($"import type {{ {interfaceName} }} from {StringLiteral($"./{interfaceName}.js")};");

        // Import the per-operation request module, response class + factory, and Params interface.
        foreach (OperationInfo op in operations)
        {
            string requestModule = RequestModuleName(op);
            string responseClass = ResponseClassName(op);
            string factoryName = ResponseFactoryName(op);
            w.WriteLine($"import {{ {requestModule} }} from {StringLiteral($"./{requestModule}.js")};");
            if (HasParams(op))
            {
                string paramsInterface = ParamsInterfaceName(op);
                w.WriteLine($"import type {{ {paramsInterface} }} from {StringLiteral($"./{requestModule}.js")};");
            }

            w.WriteLine(
                $"import {{ {responseClass}, {factoryName} }} from " +
                $"{StringLiteral($"./{responseClass}.js")};");
        }

        // Import named request-body model companions (the value, for JSON build() / form param types).
        HashSet<string> bodyTypes = this.RequestBodyModelImports(operations);
        if (bodyTypes.Count > 0)
        {
            string imports = string.Join(", ", bodyTypes.OrderBy(m => m, StringComparer.Ordinal));
            w.WriteLine($"import {{ {imports} }} from {StringLiteral(this.options.ModelsModuleSpecifier)};");
        }

        w.WriteLine();

        w.WriteLine("/**");
        w.WriteLine($" * Client implementation for the {clientName} API operations.");
        w.WriteLine(" */");
        w.WriteLine($"export class {className} implements {interfaceName} {{");
        w.PushIndent();

        w.WriteLine("private readonly transport: ApiTransport;");
        w.WriteLine();
        w.WriteLine("constructor(transport: ApiTransport) {");
        w.PushIndent();
        w.WriteLine("this.transport = transport;");
        w.PopIndent();
        w.WriteLine("}");

        if (serverInfo is { } si)
        {
            w.WriteLine();
            EmitServerUriFactory(w, si);
        }

        foreach (OperationInfo op in operations)
        {
            this.EmitClientMethod(w, op);
        }

        w.WriteLine();
        w.WriteLine("async [Symbol.asyncDispose](): Promise<void> {");
        w.PushIndent();
        w.WriteLine("await this.transport[Symbol.asyncDispose]();");
        w.PopIndent();
        w.WriteLine("}");

        w.PopIndent();
        w.WriteLine("}");

        return new GeneratedFile($"{className}.ts", w.ToString());
    }

    private void EmitClientMethod(IndentedWriter w, OperationInfo op)
    {
        string methodName = CamelCase(op.MethodName);
        string requestModule = RequestModuleName(op);
        string responseClass = ResponseClassName(op);
        string factoryName = ResponseFactoryName(op);
        bool hasParams = HasParams(op);
        ContentInfo? bodyContent = PrimaryRequestBodyContent(op);
        bool bodyRequired = op.RequestBody is { } rb && rb.IsRequired;

        w.WriteLine();
        EmitMethodDoc(w, op);
        string signature = this.BuildMethodSignature(op);
        w.WriteLine($"{methodName}({signature}): Promise<{responseClass}> {{");
        w.PushIndent();

        // The request: a factory call (params closure) when parameterised, else the const.
        if (hasParams)
        {
            w.WriteLine($"const request = {requestModule}(params);");
        }
        else
        {
            w.WriteLine($"const request = {requestModule};");
        }

        // The request body: build it per content category when present.
        if (bodyContent is { } content)
        {
            this.EmitRequestBody(w, content, bodyRequired);
            w.WriteLine($"return this.transport.send(request, {factoryName}, requestBody, signal);");
        }
        else
        {
            w.WriteLine($"return this.transport.send(request, {factoryName}, undefined, signal);");
        }

        w.PopIndent();
        w.WriteLine("}");
    }

    // Emits `const requestBody: RequestBody = ...` for the primary content entry, dispatching on its
    // category. The wire `contentType` is the entry's actual media type (not a hardcoded literal), so an
    // octet-stream body declared as e.g. `image/png` is sent as `image/png`. An optional body keeps the
    // `body === undefined ? { kind: "none" } : <built>` shape.
    private void EmitRequestBody(IndentedWriter w, ContentInfo content, bool bodyRequired)
    {
        ContentCategory category = RequestBodyCategory(content);

        // OctetStream chooses kind: "bytes"/"stream" by the runtime value, so it has its own shape.
        if (category == ContentCategory.OctetStream)
        {
            string ct = StringLiteral(content.MediaType);
            string built =
                $"body instanceof Uint8Array ? {{ kind: \"bytes\", content: body, contentType: {ct} }} " +
                $": {{ kind: \"stream\", content: body, contentType: {ct} }}";
            if (bodyRequired)
            {
                w.WriteLine($"const requestBody: RequestBody = {built};");
            }
            else
            {
                w.WriteLine("const requestBody: RequestBody = body === undefined");
                w.PushIndent();
                w.WriteLine("? { kind: \"none\" }");
                w.WriteLine($": ({built});");
                w.PopIndent();
            }

            return;
        }

        string builtBytes = this.BuildBytesRequestBody(content, category);
        if (bodyRequired)
        {
            w.WriteLine($"const requestBody: RequestBody = {builtBytes};");
        }
        else
        {
            w.WriteLine("const requestBody: RequestBody = body === undefined");
            w.PushIndent();
            w.WriteLine("? { kind: \"none\" }");
            w.WriteLine($": {builtBytes};");
            w.PopIndent();
        }
    }

    // The `{ kind: "bytes", content: <expr>, contentType: <mediaType> }` literal for a body category
    // whose content is a single byte buffer (JSON / form-urlencoded / text/plain).
    private string BuildBytesRequestBody(ContentInfo content, ContentCategory category)
    {
        // JSON keeps the hardcoded `application/json` content type for byte-identical output with the
        // existing (C1) JSON request-body path; the docs media type is also `application/json`.
        string jsonContentType = StringLiteral("application/json");
        string mediaContentType = StringLiteral(content.MediaType);

        string contentExpr;
        string contentType;
        switch (category)
        {
            case ContentCategory.Json:
                string jsonModel = this.RequestBodyJsonType(content);
                contentExpr = jsonModel == "unknown"
                    ? "new TextEncoder().encode(JSON.stringify(body))"
                    : $"{jsonModel}.build(body)";
                contentType = jsonContentType;
                break;

            case ContentCategory.FormUrlEncoded:
                string formCast = this.FormBodyParamType(content) == "FormBody" ? "body" : "body as FormBody";
                string encodingsArg = FormEncodingsLiteral(content);
                contentExpr = encodingsArg.Length == 0
                    ? $"formUrlEncodedBytes({formCast})"
                    : $"formUrlEncodedBytes({formCast}, {encodingsArg})";
                contentType = mediaContentType;
                break;

            default: // TextPlain
                contentExpr = "new TextEncoder().encode(body)";
                contentType = mediaContentType;
                break;
        }

        return $"{{ kind: \"bytes\", content: {contentExpr}, contentType: {contentType} }}";
    }

    // Renders the content entry's Encoding objects as a `FormEncodings` object literal, or the empty
    // string when there are none (so the call passes no encodings argument and the runtime default
    // applies). Each entry maps to `{ style?, explode?, allowReserved? }`, omitting absent members.
    private static string FormEncodingsLiteral(ContentInfo content)
    {
        if (content.Encodings is not { Count: > 0 } encodings)
        {
            return string.Empty;
        }

        List<string> entries = [];
        foreach (KeyValuePair<string, EncodingInfo> kvp in encodings)
        {
            EncodingInfo enc = kvp.Value;
            List<string> members = [];
            if (enc.Style is { Length: > 0 } style)
            {
                members.Add($"style: {StringLiteral(style)}");
            }

            if (enc.Explode is { } explode)
            {
                members.Add($"explode: {(explode ? "true" : "false")}");
            }

            if (enc.AllowReserved)
            {
                members.Add("allowReserved: true");
            }

            entries.Add($"{StringLiteral(kvp.Key)}: {{ {string.Join(", ", members)} }}");
        }

        return $"{{ {string.Join(", ", entries)} }}";
    }

    private static void EmitServerUriFactory(IndentedWriter w, ServerInfo serverInfo)
    {
        if (serverInfo.Variables.Length == 0)
        {
            w.WriteLine("/** Creates the base URL for the default server. */");
            w.WriteLine("static serverUri(): URL {");
            w.PushIndent();
            w.WriteLine($"return new URL({StringLiteral(serverInfo.UrlTemplate)});");
            w.PopIndent();
            w.WriteLine("}");
            return;
        }

        // Variables — emit parameters with defaults and substitute into the template.
        w.WriteLine("/** Creates the base URL for the server, substituting any server variables. */");
        w.Write("static serverUri(");
        for (int i = 0; i < serverInfo.Variables.Length; i++)
        {
            if (i > 0)
            {
                w.Write(", ");
            }

            ServerVariableInfo v = serverInfo.Variables[i];
            string paramName = CamelCase(CodeEmitHelpers.SanitizeIdentifier(v.Name));
            w.Write($"{paramName}: string = {StringLiteral(v.DefaultValue)}");
        }

        w.WriteLine("): URL {");
        w.PushIndent();
        w.WriteLine($"let url = {StringLiteral(serverInfo.UrlTemplate)};");
        foreach (ServerVariableInfo v in serverInfo.Variables)
        {
            string paramName = CamelCase(CodeEmitHelpers.SanitizeIdentifier(v.Name));
            w.WriteLine($"url = url.split({StringLiteral("{" + v.Name + "}")}).join({paramName});");
        }

        w.WriteLine("return new URL(url);");
        w.PopIndent();
        w.WriteLine("}");
    }

    // ── Shared text helpers ─────────────────────────────────────────────
    private static void EmitHeader(IndentedWriter w)
    {
        w.WriteLine("// <auto-generated>");
        w.WriteLine("// Generated by Corvus.Text.Json.OpenApi.TypeScript.CodeGeneration. Do not edit.");
        w.WriteLine("// </auto-generated>");
    }

    private static void EmitMethodDoc(IndentedWriter w, OperationInfo op)
    {
        string? summary = op.Summary ?? op.Description;
        w.WriteLine("/**");
        w.WriteLine($" * {EscapeBlockComment(summary ?? $"Invokes the {op.MethodName} operation.")}");
        w.WriteLine(" */");
    }

    private static string EscapeBlockComment(string text)
        => text.Replace("*/", "* /", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ');

    private static string StringLiteral(string value)
    {
        System.Text.StringBuilder sb = new(value.Length + 2);
        sb.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        sb.Append('"');
        return sb.ToString();
    }
}