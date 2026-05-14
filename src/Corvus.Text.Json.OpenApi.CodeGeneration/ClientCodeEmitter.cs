// <copyright file="ClientCodeEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Emits C# source files from a <see cref="ClientModel"/>.
/// </summary>
/// <remarks>
/// <para>
/// The emitter produces:
/// <list type="bullet">
/// <item>One client interface per tag group (e.g. <c>IPetstoreClient</c>).</item>
/// <item>One client implementation per tag group (e.g. <c>PetstoreClient</c>).</item>
/// </list>
/// </para>
/// <para>
/// Generated client methods dispatch to the <see cref="IApiTransport"/>:
/// no-body operations use <see cref="IApiTransport.SendAsync(in ApiRequest, CancellationToken)"/>,
/// while operations with a body use the generic
/// <see cref="IApiTransport.SendAsync{TBody}(in ApiRequest, in TBody, CancellationToken)"/>
/// overload, which writes the body directly into the transport's output stream.
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
        Dictionary<string, List<ClientOperation>> groups = GroupOperationsByTag(model.Operations);

        foreach ((string tag, List<ClientOperation> operations) in groups)
        {
            string clientName = GetClientName(tag);
            files.Add(EmitInterface(clientName, operations));
            files.Add(EmitImplementation(clientName, operations));
        }

        return files;
    }

    private static Dictionary<string, List<ClientOperation>> GroupOperationsByTag(
        ClientOperation[] operations)
    {
        Dictionary<string, List<ClientOperation>> groups = new(StringComparer.Ordinal);

        foreach (ClientOperation op in operations)
        {
            string[] tags = op.GetTags();

            if (tags.Length == 0)
            {
                AddToGroup(groups, "default", op);
            }
            else
            {
                foreach (string tag in tags)
                {
                    AddToGroup(groups, tag, op);
                }
            }
        }

        return groups;

        static void AddToGroup(
            Dictionary<string, List<ClientOperation>> groups,
            string tag,
            ClientOperation op)
        {
            if (!groups.TryGetValue(tag, out List<ClientOperation>? list))
            {
                list = [];
                groups[tag] = list;
            }

            list.Add(op);
        }
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

        // XML doc
        string? summary = op.GetSummary();
        if (summary is not null)
        {
            w.WriteLine("/// <summary>");
            w.WriteLine($"/// {EscapeXml(summary)}");
            w.WriteLine("/// </summary>");
        }

        // Parameters doc
        foreach (ClientParameter param in op.Parameters)
        {
            string paramName = param.GetName();
            string paramDesc = param.GetDescription() ?? $"The {paramName} parameter.";
            w.WriteLine($"/// <param name=\"{paramName}\">{EscapeXml(paramDesc)}.</param>");
        }

        if (op.RequestBody is not null)
        {
            string bodyDesc = op.RequestBody.Value.GetDescription() ?? "The request body.";
            w.WriteLine($"/// <param name=\"body\">{EscapeXml(bodyDesc)}.</param>");
        }

        w.WriteLine("/// <param name=\"cancellationToken\">A cancellation token.</param>");

        // Build parameter list
        List<string> paramParts = [];

        foreach (ClientParameter param in op.Parameters)
        {
            string typeName = GetParameterTypeName(param);
            string paramIdentifier = EscapeCSharpKeyword(param.GetName());

            if (param.IsRequired)
            {
                paramParts.Add($"{typeName} {paramIdentifier}");
            }
            else
            {
                paramParts.Add($"{typeName} {paramIdentifier} = default");
            }
        }

        if (op.RequestBody is not null)
        {
            // Body is a JsonElement — the transport writes it directly via IJsonElement<T>.WriteTo.
            // When V5 schema type resolution is integrated, this will be the specific generated type.
            paramParts.Add("JsonElement body");
        }

        paramParts.Add("CancellationToken cancellationToken = default");

        string parameters = string.Join(", ", paramParts);

        if (isInterface)
        {
            w.WriteLine($"ValueTask<ApiResponse> {methodName}Async({parameters});");
        }
        else
        {
            w.WriteLine($"public ValueTask<ApiResponse> {methodName}Async({parameters})");
        }
    }

    private static string GetParameterTypeName(ClientParameter param)
    {
        // For now, all parameters are string. The V5 codegen will later
        // resolve schema pointers to typed parameter values.
        return param.IsRequired ? "string" : "string?";
    }

    private static string OperationMethodExpression(OperationMethod method) =>
        method switch
        {
            OperationMethod.Get => "OperationMethod.Get",
            OperationMethod.Post => "OperationMethod.Post",
            OperationMethod.Put => "OperationMethod.Put",
            OperationMethod.Delete => "OperationMethod.Delete",
            OperationMethod.Patch => "OperationMethod.Patch",
            OperationMethod.Head => "OperationMethod.Head",
            OperationMethod.Options => "OperationMethod.Options",
            OperationMethod.Trace => "OperationMethod.Trace",
            _ => $"(OperationMethod){(int)method}",
        };

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

        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Client interface for the {clientName} API operations.");
        w.WriteLine("/// </summary>");
        w.WriteLine($"public interface I{clientName}Client : IAsyncDisposable");
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

        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Client implementation for the {clientName} API operations.");
        w.WriteLine("/// </summary>");
        w.WriteLine($"public sealed class {clientName}Client : I{clientName}Client");
        w.OpenBrace();

        // Field — just the transport, no workspace needed (body serialization is the transport's job)
        w.WriteLine("private readonly IApiTransport transport;");
        w.WriteLine();

        // Constructor
        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Initializes a new instance of the <see cref=\"{clientName}Client\"/> class.");
        w.WriteLine("/// </summary>");
        w.WriteLine("/// <param name=\"transport\">The API transport to use for sending requests.</param>");
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

        // DisposeAsync
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

        string methodExpr = OperationMethodExpression(op.Method);
        string pathTemplate = op.GetPathTemplate();

        // Build path with parameter substitution
        w.WriteLine($"string path = \"{pathTemplate}\";");

        // Substitute path parameters
        foreach (ClientParameter param in op.Parameters.Where(p => p.Location == ParameterLocation.Path))
        {
            string paramIdentifier = EscapeCSharpKeyword(param.GetName());
            string rawName = param.GetName();
            w.WriteLine($"path = path.Replace(\"{{{rawName}}}\", Uri.EscapeDataString({paramIdentifier}), StringComparison.Ordinal);");
        }

        w.WriteLine();
        w.WriteLine($"ApiRequest request = new(path, {methodExpr});");

        // Add query parameters
        foreach (ClientParameter param in op.Parameters.Where(p => p.Location == ParameterLocation.Query))
        {
            string paramIdentifier = EscapeCSharpKeyword(param.GetName());
            string rawName = param.GetName();

            if (param.IsRequired)
            {
                w.WriteLine($"request.AddQueryParameter(\"{rawName}\", {paramIdentifier});");
            }
            else
            {
                w.WriteLine($"if ({paramIdentifier} is not null)");
                w.OpenBrace();
                w.WriteLine($"request.AddQueryParameter(\"{rawName}\", {paramIdentifier});");
                w.CloseBrace();
            }
        }

        // Add header parameters
        foreach (ClientParameter param in op.Parameters.Where(p => p.Location == ParameterLocation.Header))
        {
            string paramIdentifier = EscapeCSharpKeyword(param.GetName());
            string rawName = param.GetName();

            if (param.IsRequired)
            {
                w.WriteLine($"request.AddHeader(\"{rawName}\", {paramIdentifier});");
            }
            else
            {
                w.WriteLine($"if ({paramIdentifier} is not null)");
                w.OpenBrace();
                w.WriteLine($"request.AddHeader(\"{rawName}\", {paramIdentifier});");
                w.CloseBrace();
            }
        }

        w.WriteLine();

        // Dispatch: use the generic overload for body, plain overload for no body
        if (op.RequestBody is not null)
        {
            w.WriteLine("return this.transport.SendAsync(in request, in body, cancellationToken);");
        }
        else
        {
            w.WriteLine("return this.transport.SendAsync(in request, cancellationToken);");
        }

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
        w.WriteLine("using Corvus.Text.Json;");
        w.WriteLine("using Corvus.Text.Json.Internal;");
        w.WriteLine("using Corvus.Text.Json.OpenApi;");
        w.WriteLine();
    }
}