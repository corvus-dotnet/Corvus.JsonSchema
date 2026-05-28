// <copyright file="DynamicCompiler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Extensions.DependencyModel;

namespace Corvus.Text.Json.AsyncApi.CodeGeneration.Tests;

/// <summary>
/// Compiles generated C# source files dynamically using Roslyn to verify
/// that the code generator produces compilable output.
/// </summary>
internal static class DynamicCompiler
{
    private static readonly Lazy<(IEnumerable<MetadataReference> References, CSharpParseOptions ParseOptions)>
        CompilationContext = new(BuildCompilationContext);

    /// <summary>
    /// Compiles the generated files and returns any compilation errors.
    /// </summary>
    /// <param name="files">The generated source files to compile.</param>
    /// <param name="assemblyName">The assembly name for the compilation.</param>
    /// <param name="stubSource">Optional additional source code providing type stubs.</param>
    /// <returns>A list of error diagnostics. Empty if compilation succeeds.</returns>
    public static IReadOnlyList<Diagnostic> Compile(IReadOnlyList<GeneratedFile> files, string assemblyName = "AsyncApiGenerated", string? stubSource = null)
    {
        var (references, parseOptions) = CompilationContext.Value;

        List<SyntaxTree> trees = new(files.Count + 1);
        foreach (GeneratedFile file in files)
        {
            trees.Add(CSharpSyntaxTree.ParseText(file.Content, options: parseOptions, path: file.FileName));
        }

        if (stubSource is not null)
        {
            trees.Add(CSharpSyntaxTree.ParseText(stubSource, options: parseOptions, path: "TypeStubs.cs"));
        }

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        using MemoryStream ms = new();
        EmitResult result = compilation.Emit(ms);

        if (result.Success)
        {
            return [];
        }

        return result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
    }

    /// <summary>
    /// Compiles the generated files and asserts compilation succeeds.
    /// </summary>
    /// <param name="files">The generated source files to compile.</param>
    /// <param name="assemblyName">The assembly name for the compilation.</param>
    /// <param name="stubSource">Optional additional source code providing type stubs.</param>
    public static void AssertCompiles(IReadOnlyList<GeneratedFile> files, string assemblyName = "AsyncApiGenerated", string? stubSource = null)
    {
        IReadOnlyList<Diagnostic> errors = Compile(files, assemblyName, stubSource);
        if (errors.Count > 0)
        {
            string errorText = string.Join(
                Environment.NewLine,
                errors.Take(10).Select(d => d.ToString()));

            string sourceText = string.Join(
                Environment.NewLine + "// ───────────────────────────────────" + Environment.NewLine,
                files.Select(f => $"// File: {f.FileName}{Environment.NewLine}{f.Content}"));

            Assert.Fail($"Generated code failed to compile ({errors.Count} error(s)):\n{errorText}\n\n--- Generated Source ---\n{sourceText}");
        }
    }

    /// <summary>
    /// Generates stub type declarations for schema types referenced by generated code.
    /// Each type is a minimal struct implementing IJsonElement so it satisfies generic
    /// constraints and API surface used in the generated producers/consumers.
    /// </summary>
    /// <param name="schemaTypeMap">The schema type map used for code generation.</param>
    /// <returns>A C# source string declaring stub types.</returns>
    public static string GenerateTypeStubs(IReadOnlyDictionary<string, string> schemaTypeMap)
    {
        HashSet<string> emitted = new();
        System.Text.StringBuilder sb = new();

        sb.AppendLine("using Corvus.Text.Json;");
        sb.AppendLine("using Corvus.Text.Json.Internal;");
        sb.AppendLine();

        foreach (string typeName in schemaTypeMap.Values)
        {
            if (!emitted.Add(typeName))
            {
                continue;
            }

            // Split namespace from type name (e.g., "Streetlights.TurnOnOffPayload" → namespace "Streetlights", type "TurnOnOffPayload")
            int lastDot = typeName.LastIndexOf('.');
            string ns = lastDot >= 0 ? typeName[..lastDot] : "Global";
            string name = lastDot >= 0 ? typeName[(lastDot + 1)..] : typeName;

            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    public partial struct {name} : IJsonElement<{name}>");
            sb.AppendLine("    {");

            // IJsonElement members
            sb.AppendLine($"        public readonly IJsonDocument ParentDocument => null!;");
            sb.AppendLine($"        public readonly int ParentDocumentIndex => 0;");
            sb.AppendLine($"        public readonly JsonTokenType TokenType => JsonTokenType.None;");
            sb.AppendLine($"        public readonly JsonValueKind ValueKind => JsonValueKind.Undefined;");
            sb.AppendLine($"        public readonly void CheckValidInstance() {{ }}");
            sb.AppendLine($"        public readonly bool EvaluateSchema(IJsonSchemaResultsCollector? resultsCollector = null) => true;");
            sb.AppendLine($"        public readonly void WriteTo(Utf8JsonWriter writer) {{ }}");

            // IJsonElement<T> static abstract (NET only)
            sb.AppendLine($"        public static {name} CreateInstance(IJsonDocument parentDocument, int parentDocumentIndex) => default;");

            // Generated API surface: From, Source, CreateBuilder
            sb.AppendLine($"        public static {name} Undefined => default;");
            sb.AppendLine($"        public static {name} From<T>(in T instance) where T : struct, IJsonElement<T> => default;");
            sb.AppendLine($"        public static {name} From(in Corvus.Text.Json.JsonElement instance) => default;");
            sb.AppendLine($"        public readonly bool IsUndefined() => true;");
            sb.AppendLine($"        public readonly bool IsNotUndefined() => false;");

            // Source ref struct (minimal stub)
            sb.AppendLine($"        public ref struct Source");
            sb.AppendLine("        {");
            sb.AppendLine($"            public bool IsUndefined => true;");
            sb.AppendLine("        }");

            // CreateBuilder (static, from Source)
            sb.AppendLine($"        public static JsonDocumentBuilder<{name}.Mutable> CreateBuilder(JsonWorkspace workspace, scoped in Source value, int initialCapacity = 1) => default;");

            // Mutable nested struct (minimal)
            sb.AppendLine($"        public partial struct Mutable : IJsonElement<Mutable>, IMutableJsonElement<Mutable>");
            sb.AppendLine("        {");
            sb.AppendLine($"            public readonly IJsonDocument ParentDocument => null!;");
            sb.AppendLine($"            public readonly int ParentDocumentIndex => 0;");
            sb.AppendLine($"            public readonly JsonTokenType TokenType => JsonTokenType.None;");
            sb.AppendLine($"            public readonly JsonValueKind ValueKind => JsonValueKind.Undefined;");
            sb.AppendLine($"            public readonly void CheckValidInstance() {{ }}");
            sb.AppendLine($"            public readonly bool EvaluateSchema(IJsonSchemaResultsCollector? resultsCollector = null) => true;");
            sb.AppendLine($"            public readonly void WriteTo(Utf8JsonWriter writer) {{ }}");
            sb.AppendLine($"            public static Mutable CreateInstance(IJsonDocument parentDocument, int parentDocumentIndex) => default;");
            sb.AppendLine($"            public static implicit operator {name}(Mutable instance) => default;");
            sb.AppendLine("        }");

            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static (IEnumerable<MetadataReference>, CSharpParseOptions) BuildCompilationContext()
    {
        DependencyContext? ctx = DependencyContext.Load(Assembly.GetExecutingAssembly()) ?? DependencyContext.Default;

        List<MetadataReference> references;
        List<string> defines = ["DYNAMIC_BUILD"];

        if (ctx is not null)
        {
            references = (
                from l in ctx.CompileLibraries
                from r in TryResolveReferencePaths(l)
                select (MetadataReference)MetadataReference.CreateFromFile(r)).ToList();

            defines.AddRange(ctx.CompilationOptions.Defines.Where(d => d is not null)!);
        }
        else
        {
            references = [];
        }

        SupplementWithDirectoryAndAppDomain(references);

        CSharpParseOptions parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithPreprocessorSymbols(defines);

        return (references, parseOptions);
    }

    private static void SupplementWithDirectoryAndAppDomain(List<MetadataReference> references)
    {
        HashSet<string> seenNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (MetadataReference r in references)
        {
            if (r is PortableExecutableReference peRef && peRef.FilePath is string path)
            {
                seenNames.Add(Path.GetFileNameWithoutExtension(path));
            }
        }

        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            {
                string name = a.GetName().Name ?? Path.GetFileNameWithoutExtension(a.Location);
                if (!seenNames.Add(name))
                {
                    references.RemoveAll(r =>
                        r is PortableExecutableReference peRef &&
                        peRef.FilePath is string p &&
                        Path.GetFileNameWithoutExtension(p).Equals(name, StringComparison.OrdinalIgnoreCase));
                }

                references.Add(MetadataReference.CreateFromFile(a.Location));
            }
        }

        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        if (dir is not null && Directory.Exists(dir))
        {
            foreach (string dll in Directory.EnumerateFiles(dir, "*.dll"))
            {
                string simpleName = Path.GetFileNameWithoutExtension(dll);
                if (seenNames.Add(simpleName))
                {
                    try
                    {
                        AssemblyName.GetAssemblyName(dll);
                        references.Add(MetadataReference.CreateFromFile(dll));
                    }
                    catch
                    {
                        seenNames.Remove(simpleName);
                    }
                }
            }
        }

        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!a.IsDynamic)
            {
                foreach (AssemblyName refName in a.GetReferencedAssemblies())
                {
                    try
                    {
                        Assembly resolved = Assembly.Load(refName);
                        if (!resolved.IsDynamic && !string.IsNullOrEmpty(resolved.Location) &&
                            seenNames.Add(resolved.GetName().Name ?? Path.GetFileNameWithoutExtension(resolved.Location)))
                        {
                            references.Add(MetadataReference.CreateFromFile(resolved.Location));
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }
    }

    private static IEnumerable<string> TryResolveReferencePaths(CompilationLibrary library)
    {
        try
        {
            return library.ResolveReferencePaths();
        }
        catch (InvalidOperationException)
        {
            return [];
        }
    }
}