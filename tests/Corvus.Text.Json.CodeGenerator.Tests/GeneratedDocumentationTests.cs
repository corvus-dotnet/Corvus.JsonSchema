using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Verifies that generated code compiles without XML documentation diagnostics, so
/// consuming projects can enable <c>GenerateDocumentationFile</c> with
/// <c>TreatWarningsAsErrors</c> without the generated code failing the build.
/// </summary>
/// <remarks>
/// Guards the defect class of issue #918: doc comments whose <c>&lt;param&gt;</c> and
/// <c>&lt;typeparam&gt;</c> tags do not match the signatures they document (CS1572,
/// CS1573 and relatives). CS1591 (missing doc comment) is deliberately not gated here;
/// that surface is tracked separately by issue #919.
/// </remarks>
[TestClass]
public class GeneratedDocumentationTests
{
    /// <summary>
    /// The documentation diagnostics that indicate a doc comment contradicting the code
    /// it documents (or malformed XML), as opposed to merely missing documentation.
    /// </summary>
    private static readonly HashSet<string> DocMismatchDiagnosticIds = new(StringComparer.Ordinal)
    {
        "CS1570", // XML comment has badly formed XML
        "CS1571", // XML comment has a duplicate param tag
        "CS1572", // XML comment has a param tag, but there is no parameter by that name
        "CS1573", // parameter has no matching param tag (but other parameters do)
        "CS1574", // XML comment has cref attribute that could not be resolved
        "CS1580", // invalid type for parameter in XML comment cref attribute
        "CS1581", // invalid return type in XML comment cref attribute
        "CS1584", // XML comment has syntactically incorrect cref attribute
        "CS1587", // XML comment is not placed on a valid language element
        "CS1590", // invalid include element
        "CS1592", // badly formed XML in included comments file
        "CS1710", // XML comment has a duplicate typeparam tag
        "CS1711", // XML comment has a typeparam tag, but there is no type parameter by that name
        "CS1712", // type parameter has no matching typeparam tag (but other type parameters do)
        "CS1723", // XML comment cref attribute refers to a type parameter
    };

    private string _outputDir;

    [TestInitialize]
    public void Initialize()
    {
        _outputDir = CodeGeneratorRunner.CreateTempOutputDirectory();
    }

    [TestCleanup]
    public void Cleanup()
    {
        CodeGeneratorRunner.CleanupTempDirectory(_outputDir);
    }

    [TestMethod]
    [DataRow("complex-validation.json")]
    [DataRow("composed-type.json")]
    [DataRow("array-type.json")]
    [DataRow("pure-oneof.json")]
    [DataRow("const-properties.json")]
    public async Task JsonSchemaOutput_HasNoDocumentationMismatchDiagnostics(string schemaFile)
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", schemaFile);

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace DocCheck.Models --outputPath \"{_outputDir}\"");

        Assert.AreEqual(0, result.ExitCode, $"Generation failed. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");

        AssertNoDocumentationMismatchDiagnostics(_outputDir);
    }

    [TestMethod]
    [DataRow("complex-validation.json")]
    [DataRow("composed-type.json")]
    [DataRow("array-type.json")]
    [DataRow("pure-oneof.json")]
    [DataRow("const-properties.json")]
    public async Task JsonSchemaV4EngineOutput_HasNoDocumentationMismatchDiagnostics(string schemaFile)
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", schemaFile);

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --engine V4 --rootNamespace DocCheck.V4Models --outputPath \"{_outputDir}\"");

        Assert.AreEqual(0, result.ExitCode, $"Generation failed. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");

        AssertNoDocumentationMismatchDiagnostics(_outputDir);
    }

    [TestMethod]
    public async Task OpenApiClientOutput_HasNoDocumentationMismatchDiagnostics()
    {
        string spec = CodeGeneratorRunner.GetFixturePath("OpenApi", "doc-comments-3.0.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"openapi-client \"{spec}\" --rootNamespace DocCheck.Client --outputPath \"{_outputDir}\"");

        Assert.AreEqual(0, result.ExitCode, $"Generation failed. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");

        AssertNoDocumentationMismatchDiagnostics(_outputDir);
    }

    private static void AssertNoDocumentationMismatchDiagnostics(string outputDir)
    {
        string[] files = Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.IsTrue(files.Length > 0, $"Expected generated .cs files in {outputDir}");

        CSharpParseOptions parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithDocumentationMode(DocumentationMode.Diagnose)
            .WithPreprocessorSymbols(ReadCompilationDefines());

        List<SyntaxTree> trees = new(files.Length + 1);
        foreach (string file in files)
        {
            trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(file), parseOptions, path: file));
        }

        // Generated code is consumed from SDK projects with ImplicitUsings enabled, so the
        // compilation supplies the same global usings the SDK would.
        trees.Add(CSharpSyntaxTree.ParseText(
            """
            global using global::System;
            global using global::System.Collections.Generic;
            global using global::System.IO;
            global using global::System.Linq;
            global using global::System.Net.Http;
            global using global::System.Threading;
            global using global::System.Threading.Tasks;
            """,
            parseOptions,
            path: "ImplicitUsings.cs"));

        CSharpCompilation compilation = CSharpCompilation.Create(
            "GeneratedDocCheck",
            trees,
            BuildReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable)
                .WithAllowUnsafe(true));

        List<string> problems = [];
        foreach (Diagnostic diagnostic in compilation.GetDiagnostics())
        {
            // CS8795: [GeneratedRegex] partial methods are implemented by the regex source
            // generator in the consuming project's build; this compilation does not run
            // source generators, so the missing implementation part is expected.
            if (diagnostic.Id == "CS8795")
            {
                continue;
            }

            if (diagnostic.Severity == DiagnosticSeverity.Error ||
                DocMismatchDiagnosticIds.Contains(diagnostic.Id))
            {
                problems.Add(diagnostic.ToString());
            }
        }

        if (problems.Count > 0)
        {
            Assert.Fail(
                $"Generated code produced {problems.Count} compilation or documentation diagnostics:\n" +
                string.Join("\n", problems.Take(25)));
        }
    }

    private static string[] ReadCompilationDefines()
    {
        // Generated code carries #if NET8_0_OR_GREATER (and friends) blocks, so it must be
        // parsed with the same defines a consuming net10.0 project would have. The preserved
        // compilation context records them in the deps file.
        string depsPath = Path.Combine(
            AppContext.BaseDirectory,
            $"{typeof(GeneratedDocumentationTests).Assembly.GetName().Name}.deps.json");

        using FileStream stream = File.OpenRead(depsPath);
        using System.Text.Json.JsonDocument deps = System.Text.Json.JsonDocument.Parse(stream);

        if (!deps.RootElement.TryGetProperty("compilationOptions", out System.Text.Json.JsonElement options) ||
            !options.TryGetProperty("defines", out System.Text.Json.JsonElement defines))
        {
            Assert.Fail($"Expected compilationOptions.defines in {depsPath}. Is PreserveCompilationContext set on this project?");
            return [];
        }

        List<string> result = [];
        foreach (System.Text.Json.JsonElement define in defines.EnumerateArray())
        {
            result.Add(define.GetString());
        }

        return [.. result];
    }

    private static List<MetadataReference> BuildReferences()
    {
        // The project preserves its compilation context, which copies the framework reference
        // assemblies to a 'refs' folder next to the test. Those facades plus the library
        // assemblies deployed to the output directory (Corvus.Text.Json and
        // Corvus.Text.Json.OpenApi arrive there via project references) form the same
        // coherent reference set the test itself compiled against.
        List<MetadataReference> references = [];
        HashSet<string> seenNames = new(StringComparer.OrdinalIgnoreCase);

        string refsDir = Path.Combine(AppContext.BaseDirectory, "refs");
        Assert.IsTrue(
            Directory.Exists(refsDir),
            $"Expected the preserved compilation context 'refs' folder at {refsDir}. Is PreserveCompilationContext set on this project?");

        foreach (string dll in Directory.EnumerateFiles(refsDir, "*.dll"))
        {
            if (seenNames.Add(Path.GetFileNameWithoutExtension(dll)))
            {
                references.Add(MetadataReference.CreateFromFile(dll));
            }
        }

        foreach (string dll in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
        {
            string simpleName = Path.GetFileNameWithoutExtension(dll);
            if (seenNames.Add(simpleName))
            {
                try
                {
                    System.Reflection.AssemblyName.GetAssemblyName(dll);
                    references.Add(MetadataReference.CreateFromFile(dll));
                }
                catch
                {
                    seenNames.Remove(simpleName);
                }
            }
        }

        return references;
    }
}