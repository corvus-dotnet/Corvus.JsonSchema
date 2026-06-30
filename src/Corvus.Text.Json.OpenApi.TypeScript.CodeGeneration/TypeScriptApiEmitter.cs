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
/// Phase 0 scope: an operation with no parameters, no request body, and a single 2xx JSON response
/// (plus an optional <c>default</c>/error response). The generated client imports the byte-native
/// runtime contracts from <c>@endjin/corvus-json-client-runtime</c> and the generated models by
/// their <c>Ts_FinalName</c>. The server members return <see langword="null"/> (client-only).
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

    // ── Request module ──────────────────────────────────────────────────
    private GeneratedFile EmitRequestObject(OperationInfo op)
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

        // Phase 0: no parameters, no request body. Accept header is the distinct response media types.
        string[] acceptMediaTypes = CodeEmitHelpers.GetAcceptMediaTypes(
            op.Responses
                .SelectMany(r => r.Content)
                .Select(c => (c.MediaType, c.SchemaPointer?.PositionalPointer)));
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

        // writeResolvedPath — literal template (no path parameters in Phase 0).
        w.WriteLine("writeResolvedPath(writer: ByteWriter): void {");
        w.PushIndent();
        w.WriteLine($"writer.writeAscii({StringLiteral(op.PathTemplate)});");
        w.PopIndent();
        w.WriteLine("},");

        // writeQueryString — no query parameters in Phase 0.
        w.WriteLine("writeQueryString(_writer: ByteWriter): number {");
        w.PushIndent();
        w.WriteLine("return 0;");
        w.PopIndent();
        w.WriteLine("},");

        // writeHeaders — only the Accept header, when there are concrete response media types.
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

        // writeCookies — no cookie parameters in Phase 0.
        w.WriteLine("writeCookies(_writer: ByteWriter): number {");
        w.PushIndent();
        w.WriteLine("return 0;");
        w.PopIndent();
        w.WriteLine("},");

        // validate — no parameters to validate in Phase 0.
        w.WriteLine("validate(_mode: ValidationMode = ValidationMode.Basic): void {");
        w.PushIndent();
        w.WriteLine("/* no parameters to validate */");
        w.PopIndent();
        w.WriteLine("},");

        w.PopIndent();
        w.WriteLine("};");

        return new GeneratedFile($"{moduleName}.ts", w.ToString());
    }

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

        // Import each operation's response class (the method return types).
        foreach (OperationInfo op in operations)
        {
            string responseClass = ResponseClassName(op);
            w.WriteLine($"import type {{ {responseClass} }} from {StringLiteral($"./{responseClass}.js")};");
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
            w.WriteLine($"{methodName}(signal?: AbortSignal): Promise<{responseClass}>;");
        }

        w.PopIndent();
        w.WriteLine("}");

        return new GeneratedFile($"{interfaceName}.ts", w.ToString());
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

        EmitHeader(w);
        w.WriteLine(
            "import type { ApiTransport } from " +
            $"{StringLiteral(this.options.ClientRuntimeModuleSpecifier)};");
        w.WriteLine($"import type {{ {interfaceName} }} from {StringLiteral($"./{interfaceName}.js")};");

        foreach (OperationInfo op in operations)
        {
            string requestModule = RequestModuleName(op);
            string responseClass = ResponseClassName(op);
            string factoryName = ResponseFactoryName(op);
            w.WriteLine($"import {{ {requestModule} }} from {StringLiteral($"./{requestModule}.js")};");
            w.WriteLine(
                $"import {{ {responseClass}, {factoryName} }} from " +
                $"{StringLiteral($"./{responseClass}.js")};");
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
            string methodName = CamelCase(op.MethodName);
            string requestModule = RequestModuleName(op);
            string responseClass = ResponseClassName(op);
            string factoryName = ResponseFactoryName(op);

            w.WriteLine();
            EmitMethodDoc(w, op);
            w.WriteLine($"{methodName}(signal?: AbortSignal): Promise<{responseClass}> {{");
            w.PushIndent();
            w.WriteLine($"return this.transport.send({requestModule}, {factoryName}, undefined, signal);");
            w.PopIndent();
            w.WriteLine("}");
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