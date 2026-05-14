// <copyright file="ClientCodeEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Emits C# source files from a <see cref="ClientModel"/>.
/// </summary>
/// <remarks>
/// <para>
/// The emitter produces:
/// <list type="bullet">
/// <item>One client interface per tag group (e.g. <c>IPetsClient</c>).</item>
/// <item>One client implementation per tag group (e.g. <c>PetsClient</c>).</item>
/// </list>
/// </para>
/// <para>
/// Generated client methods parse API responses directly into V5 types via
/// <see cref="ParsedJsonDocument{T}"/>. The caller is responsible for disposing
/// the parsed document when finished with the response data.
/// </para>
/// </remarks>
public sealed class ClientCodeEmitter
{
    private readonly string rootNamespace;
    private readonly string? clientNamePrefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientCodeEmitter"/> class.
    /// </summary>
    /// <param name="rootNamespace">The root namespace for generated code.</param>
    /// <param name="clientNamePrefix">
    /// Optional prefix for client type names. If <see langword="null"/>,
    /// the API title is used.
    /// </param>
    public ClientCodeEmitter(string rootNamespace, string? clientNamePrefix = null)
    {
        this.rootNamespace = rootNamespace;
        this.clientNamePrefix = clientNamePrefix;
    }

    /// <summary>
    /// Emits all generated files from the given client model.
    /// </summary>
    /// <param name="model">The client model to emit.</param>
    /// <returns>The generated source files.</returns>
    public IReadOnlyList<GeneratedFile> Emit(ClientModel model)
    {
        List<GeneratedFile> files = [];
        IReadOnlyDictionary<string, IReadOnlyList<ClientOperation>> groups =
            model.GetOperationsByTag();

        foreach ((string tag, IReadOnlyList<ClientOperation> operations) in groups)
        {
            string clientName = GetClientName(tag);
            files.Add(EmitInterface(clientName, operations));
            files.Add(EmitImplementation(clientName, operations));
        }

        return files;
    }

    private static string SanitizeIdentifier(string name)
    {
        char[] chars = new char[name.Length];
        bool capitalizeNext = true;
        int written = 0;

        foreach (char c in name)
        {
            if (c is ' ' or '_' or '-' or '.')
            {
                capitalizeNext = true;
                continue;
            }

            if (!char.IsLetterOrDigit(c))
            {
                continue;
            }

            chars[written++] = capitalizeNext ? char.ToUpperInvariant(c) : c;
            capitalizeNext = false;
        }

        return new string(chars, 0, written);
    }

    private static void EmitMethodSignature(
        IndentedWriter w,
        ClientOperation op,
        bool isInterface)
    {
        string methodName = op.GetMethodName();
        string returnType = GetReturnType(op);

        // XML doc
        if (op.Summary is not null)
        {
            w.WriteLine($"/// <summary>");
            w.WriteLine($"/// {EscapeXml(op.Summary)}");
            w.WriteLine($"/// </summary>");
        }

        // Parameters doc
        foreach (ClientParameter param in op.Parameters)
        {
            w.WriteLine($"/// <param name=\"{param.Name}\">{EscapeXml(param.Description ?? $"The {param.Name} parameter.")}.</param>");
        }

        if (op.RequestBody is not null)
        {
            w.WriteLine($"/// <param name=\"body\">{EscapeXml(op.RequestBody.Description ?? "The request body.")}.</param>");
        }

        w.WriteLine("/// <param name=\"cancellationToken\">A cancellation token.</param>");

        // Build parameter list
        List<string> paramParts = [];

        foreach (ClientParameter param in op.Parameters)
        {
            string typeName = GetParameterTypeName(param);
            string paramName = EscapeCSharpKeyword(param.Name);

            if (param.IsRequired)
            {
                paramParts.Add($"{typeName} {paramName}");
            }
            else
            {
                paramParts.Add($"{typeName} {paramName} = default");
            }
        }

        if (op.RequestBody is not null)
        {
            string bodyType = op.RequestBody.IsRequired
                ? "ReadOnlyMemory<byte>"
                : "ReadOnlyMemory<byte>?";
            paramParts.Add($"{bodyType} body{(op.RequestBody.IsRequired ? string.Empty : " = default")}");
        }

        paramParts.Add("CancellationToken cancellationToken = default");

        string parameters = string.Join(", ", paramParts);

        if (isInterface)
        {
            w.WriteLine($"Task<ApiResponse> {methodName}Async({parameters});");
        }
        else
        {
            w.WriteLine($"public async Task<ApiResponse> {methodName}Async({parameters})");
        }
    }

    private static string GetReturnType(ClientOperation op)
    {
        // For now, all methods return ApiResponse. The caller uses
        // ParsedJsonDocument<T>.Parse(response.Body) to get typed results.
        return "Task<ApiResponse>";
    }

    private static string GetParameterTypeName(ClientParameter param)
    {
        // For now, all parameters are string. The V5 codegen will later
        // resolve schema pointers to typed parameter values.
        return param.IsRequired ? "string" : "string?";
    }

    private static string EscapeCSharpKeyword(string name) =>
        name switch
        {
            "abstract" or "as" or "base" or "bool" or "break" or "byte" or "case" or
            "catch" or "char" or "checked" or "class" or "const" or "continue" or
            "decimal" or "default" or "delegate" or "do" or "double" or "else" or
            "enum" or "event" or "explicit" or "extern" or "false" or "finally" or
            "fixed" or "float" or "for" or "foreach" or "goto" or "if" or "implicit" or
            "in" or "int" or "interface" or "internal" or "is" or "lock" or "long" or
            "namespace" or "new" or "null" or "object" or "operator" or "out" or
            "override" or "params" or "private" or "protected" or "public" or "readonly" or
            "ref" or "return" or "sbyte" or "sealed" or "short" or "sizeof" or
            "stackalloc" or "static" or "string" or "struct" or "switch" or "this" or
            "throw" or "true" or "try" or "typeof" or "uint" or "ulong" or "unchecked" or
            "unsafe" or "ushort" or "using" or "virtual" or "void" or "volatile" or "while"
                => $"@{name}",
            _ => name,
        };

    private static string EscapeXml(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static string HttpMethodConstant(OperationMethod method) =>
        method switch
        {
            OperationMethod.Get => "GET",
            OperationMethod.Post => "POST",
            OperationMethod.Put => "PUT",
            OperationMethod.Delete => "DELETE",
            OperationMethod.Patch => "PATCH",
            OperationMethod.Head => "HEAD",
            OperationMethod.Options => "OPTIONS",
            OperationMethod.Trace => "TRACE",
            _ => method.ToString().ToUpperInvariant(),
        };

    private string GetClientName(string tag)
    {
        string prefix = this.clientNamePrefix ?? "Api";
        string sanitized = SanitizeIdentifier(tag);
        return $"{prefix}{sanitized}";
    }

    private GeneratedFile EmitInterface(
        string clientName,
        IReadOnlyList<ClientOperation> operations)
    {
        IndentedWriter w = new();

        EmitHeader(w);
        w.WriteLine($"namespace {this.rootNamespace};");
        w.WriteLine();

        w.WriteLine($"/// <summary>");
        w.WriteLine($"/// Client interface for the {clientName} API operations.");
        w.WriteLine($"/// </summary>");
        w.WriteLine($"public interface I{clientName}Client : IDisposable, IAsyncDisposable");
        w.OpenBrace();

        for (int i = 0; i < operations.Count; i++)
        {
            if (i > 0)
            {
                w.WriteLine();
            }

            EmitMethodSignature(w, operations[i], isInterface: true);
        }

        w.CloseBrace();

        return new GeneratedFile($"I{clientName}Client.cs", w.ToString());
    }

    private GeneratedFile EmitImplementation(
        string clientName,
        IReadOnlyList<ClientOperation> operations)
    {
        IndentedWriter w = new();

        EmitHeader(w);
        w.WriteLine($"namespace {this.rootNamespace};");
        w.WriteLine();

        w.WriteLine($"/// <summary>");
        w.WriteLine($"/// Client implementation for the {clientName} API operations.");
        w.WriteLine($"/// </summary>");
        w.WriteLine($"public sealed class {clientName}Client : I{clientName}Client");
        w.OpenBrace();

        // Field
        w.WriteLine("private readonly IApiTransport transport;");
        w.WriteLine();

        // Constructor
        w.WriteLine($"/// <summary>");
        w.WriteLine($"/// Initializes a new instance of the <see cref=\"{clientName}Client\"/> class.");
        w.WriteLine($"/// </summary>");
        w.WriteLine($"/// <param name=\"transport\">The API transport to use for sending requests.</param>");
        w.WriteLine($"public {clientName}Client(IApiTransport transport)");
        w.OpenBrace();
        w.WriteLine("this.transport = transport ?? throw new ArgumentNullException(nameof(transport));");
        w.CloseBrace();
        w.WriteLine();

        // Methods
        for (int i = 0; i < operations.Count; i++)
        {
            if (i > 0)
            {
                w.WriteLine();
            }

            EmitMethodImplementation(w, operations[i]);
        }

        w.WriteLine();

        // Dispose
        w.WriteLine("/// <inheritdoc/>");
        w.WriteLine("public void Dispose()");
        w.OpenBrace();
        w.CloseBrace();
        w.WriteLine();

        w.WriteLine("/// <inheritdoc/>");
        w.WriteLine("public ValueTask DisposeAsync() => default;");

        w.CloseBrace();

        return new GeneratedFile($"{clientName}Client.cs", w.ToString());
    }

    private static void EmitMethodImplementation(
        IndentedWriter w,
        ClientOperation op)
    {
        EmitMethodSignature(w, op, isInterface: false);
        w.OpenBrace();

        string httpMethod = HttpMethodConstant(op.Method);

        // Build path with parameter substitution
        w.WriteLine($"string path = \"{op.Path}\";");

        // Substitute path parameters
        foreach (ClientParameter param in op.Parameters.Where(p => p.Location == ParameterLocation.Path))
        {
            string paramName = EscapeCSharpKeyword(param.Name);
            w.WriteLine($"path = path.Replace(\"{{{param.Name}}}\", Uri.EscapeDataString({paramName}), StringComparison.Ordinal);");
        }

        w.WriteLine();
        w.WriteLine($"ApiRequest request = new(path, \"{httpMethod}\");");

        // Add query parameters
        foreach (ClientParameter param in op.Parameters.Where(p => p.Location == ParameterLocation.Query))
        {
            string paramName = EscapeCSharpKeyword(param.Name);

            if (param.IsRequired)
            {
                w.WriteLine($"request = request.WithQueryParameter(\"{param.Name}\", {paramName});");
            }
            else
            {
                w.WriteLine($"if ({paramName} is not null)");
                w.OpenBrace();
                w.WriteLine($"request = request.WithQueryParameter(\"{param.Name}\", {paramName});");
                w.CloseBrace();
            }
        }

        // Add header parameters
        foreach (ClientParameter param in op.Parameters.Where(p => p.Location == ParameterLocation.Header))
        {
            string paramName = EscapeCSharpKeyword(param.Name);

            if (param.IsRequired)
            {
                w.WriteLine($"request = request.WithHeader(\"{param.Name}\", {paramName});");
            }
            else
            {
                w.WriteLine($"if ({paramName} is not null)");
                w.OpenBrace();
                w.WriteLine($"request = request.WithHeader(\"{param.Name}\", {paramName});");
                w.CloseBrace();
            }
        }

        // Add body
        if (op.RequestBody is not null)
        {
            string contentType = op.RequestBody.Content.Count > 0
                ? op.RequestBody.Content[0].MediaType
                : "application/json";

            if (op.RequestBody.IsRequired)
            {
                w.WriteLine($"request = request.WithBody(body, \"{contentType}\");");
            }
            else
            {
                w.WriteLine("if (body.HasValue)");
                w.OpenBrace();
                w.WriteLine($"request = request.WithBody(body.Value, \"{contentType}\");");
                w.CloseBrace();
            }
        }

        w.WriteLine();
        w.WriteLine("return await this.transport.SendAsync(request, cancellationToken).ConfigureAwait(false);");

        w.CloseBrace();
    }

    private static void EmitHeader(IndentedWriter w)
    {
        w.WriteLine("// <auto-generated>");
        w.WriteLine("// This code was generated by the Corvus.Text.Json OpenAPI code generator.");
        w.WriteLine("// Do not edit this file directly.");
        w.WriteLine("// </auto-generated>");
        w.WriteLine();
        w.WriteLine("#nullable enable");
        w.WriteLine();
        w.WriteLine("using Corvus.Text.Json.OpenApi;");
        w.WriteLine();
    }
}