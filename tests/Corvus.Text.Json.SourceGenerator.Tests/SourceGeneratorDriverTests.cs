using System.Collections.Immutable;
using System.Text;
using Corvus.Json.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.SourceGenerator.Tests;

/// <summary>
/// Drives <c>Corvus.Text.Json.SourceGenerator</c> through a long-lived <see cref="CSharpGeneratorDriver"/>,
/// the way the compiler server and the IDE do, so that the incremental pipeline is exercised across
/// several generations rather than once per process.
/// </summary>
[TestClass]
public class SourceGeneratorDriverTests
{
    private const string SchemaFileName = "driver-test-schema.json";

    private const string Schema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "properties": {
            "name": { "type": "string" },
            "age": { "type": "integer" },
            "address": {
              "type": "object",
              "properties": {
                "street": { "type": "string" },
                "city": { "type": "string" }
              }
            }
          },
          "required": ["name"]
        }
        """;

    private static readonly string Source = $$"""
        using Corvus.Text.Json;

        namespace DriverTests;

        [JsonSchemaTypeGenerator("{{SchemaFileName}}")]
        public readonly partial struct Person;
        """;

    /// <summary>
    /// Regression test for issue #963: the second generation in the same driver (after a schema edit)
    /// must produce the same output as the first instead of failing with CS8785 because the cached
    /// global options accumulated a duplicate named type.
    /// </summary>
    [TestMethod]
    public void SecondGenerationAfterSchemaEdit_ProducesSameSources()
    {
        string directory = Path.Combine(Path.GetTempPath(), "corvus-driver-tests");
        string schemaPath = Path.Combine(directory, SchemaFileName);
        SyntaxTree tree = CSharpSyntaxTree.ParseText(Source, path: Path.Combine(directory, "Person.cs"));
        CSharpCompilation compilation = CreateCompilation(tree);

        var schema = new InMemoryAdditionalText(schemaPath, Schema);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new IncrementalSourceGenerator().AsSourceGenerator()],
            additionalTexts: [schema],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> firstDiagnostics);
        GeneratorRunResult first = driver.GetRunResult().Results[0];
        AssertNoErrors(firstDiagnostics, first, "first generation");
        Assert.IsTrue(first.GeneratedSources.Length > 1, "The first generation should produce the generated types.");

        // Run again with identical inputs: everything should come from the cache.
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> secondDiagnostics);
        GeneratorRunResult second = driver.GetRunResult().Results[0];
        AssertNoErrors(secondDiagnostics, second, "second generation (same inputs)");
        Assert.AreEqual(first.GeneratedSources.Length, second.GeneratedSources.Length);
        Assert.IsTrue(
            second.TrackedOutputSteps.SelectMany(kv => kv.Value).SelectMany(s => s.Outputs).All(o => o.Reason == IncrementalStepRunReason.Cached),
            "An unchanged run should be served from the incremental cache.");

        // A whitespace-only edit of the schema forces a regeneration in the same driver.
        driver = driver.ReplaceAdditionalText(schema, new InMemoryAdditionalText(schemaPath, Schema + "\n"));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> thirdDiagnostics);
        GeneratorRunResult third = driver.GetRunResult().Results[0];
        AssertNoErrors(thirdDiagnostics, third, "third generation (schema edited)");
        Assert.AreEqual(first.GeneratedSources.Length, third.GeneratedSources.Length, "The regeneration should produce the same set of files.");

        foreach ((GeneratedSourceResult before, GeneratedSourceResult after) in first.GeneratedSources.Zip(third.GeneratedSources))
        {
            Assert.AreEqual(before.HintName, after.HintName);
            Assert.AreEqual(before.SourceText.ToString(), after.SourceText.ToString(), $"Generated file {before.HintName} changed between generations.");
        }
    }

    private static void AssertNoErrors(ImmutableArray<Diagnostic> compilationDiagnostics, GeneratorRunResult result, string stage)
    {
        IEnumerable<Diagnostic> errors = compilationDiagnostics.Concat(result.Diagnostics).Where(d => d.Severity == DiagnosticSeverity.Error);
        Assert.IsTrue(!errors.Any(), $"{stage} reported errors: {string.Join("; ", errors.Select(d => d.ToString()))}; generator exception: {result.Exception}");
        Assert.IsNull(result.Exception, $"{stage} threw: {result.Exception}");
    }

    private static CSharpCompilation CreateCompilation(params SyntaxTree[] trees)
    {
        IEnumerable<MetadataReference> references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p) is "System.Runtime.dll" or "System.Private.CoreLib.dll" or "netstandard.dll" or "mscorlib.dll")
            .Select(p => MetadataReference.CreateFromFile(p));

        return CSharpCompilation.Create(
            "DriverTests",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
    {
        private readonly SourceText sourceText = SourceText.From(text, Encoding.UTF8);

        public override string Path => path;

        public override SourceText? GetText(CancellationToken cancellationToken = default) => this.sourceText;
    }
}
