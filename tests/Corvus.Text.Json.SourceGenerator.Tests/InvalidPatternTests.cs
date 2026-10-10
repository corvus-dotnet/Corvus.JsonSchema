// <copyright file="InvalidPatternTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using Corvus.Json.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.SourceGenerator.Tests;

/// <summary>
/// A schema whose <c>pattern</c> or <c>patternProperties</c> pattern is not a valid ECMA-262 regular expression is an
/// error of the generator, which names the pattern, and nothing is generated for the schema.
/// </summary>
[TestClass]
public class InvalidPatternTests
{
    private const string SchemaFileName = "invalid-pattern-schema.json";

    private static readonly string Source = $$"""
        using Corvus.Text.Json;

        namespace InvalidPatternTests;

        [JsonSchemaTypeGenerator("{{SchemaFileName}}")]
        public readonly partial struct Root;
        """;

    private static readonly string TestDirectory = Path.Combine(Path.GetTempPath(), "corvus-invalid-pattern-tests");

    [TestMethod]
    [DataRow("""{"type": "string", "pattern": "^[a-z]{3,1}$"}""", "Error generating C# code: The pattern '^[a-z]{3,1}$' of the keyword 'pattern' is not a valid ECMA-262 regular expression. Numbers out of order in {} quantifier at offset 11.")]
    [DataRow("""{"type": "object", "patternProperties": {"^x\\b+": {"type": "integer"}}}""", "Error generating C# code: The pattern '^x\\b+' of the keyword 'patternProperties' is not a valid ECMA-262 regular expression. Nothing to repeat at offset 2.")]
    public void ASchemaWithAnInvalidPattern_IsAnErrorThatNamesThePattern(string schema, string expectedMessage)
    {
        GeneratorRunResult result = Run(schema);

        Assert.IsNull(result.Exception, $"The generator threw: {result.Exception}");
        Diagnostic[] errors = [.. result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)];
        Assert.AreEqual(1, errors.Length, string.Join("\n", result.Diagnostics));
        Assert.AreEqual("CRV1001", errors[0].Id);
        Assert.AreEqual(expectedMessage, errors[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture));
        Assert.IsFalse(result.GeneratedSources.Any(s => s.SourceText.ToString().Contains("partial struct Root", StringComparison.Ordinal)), "Nothing is generated for the schema.");
    }

    private static GeneratorRunResult Run(string schema)
    {
        IEnumerable<MetadataReference> references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p) is "System.Runtime.dll" or "System.Private.CoreLib.dll" or "netstandard.dll" or "mscorlib.dll")
            .Select(p => MetadataReference.CreateFromFile(p));

        CSharpCompilation compilation = CSharpCompilation.Create(
            "InvalidPatternTests",
            [CSharpSyntaxTree.ParseText(Source, path: Path.Combine(TestDirectory, "Root.cs"))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new IncrementalSourceGenerator().AsSourceGenerator()],
            additionalTexts: [new InMemoryAdditionalText(Path.Combine(TestDirectory, SchemaFileName), schema)]);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> _);
        return driver.GetRunResult().Results[0];
    }

    private sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
    {
        private readonly SourceText text = SourceText.From(text, System.Text.Encoding.UTF8);

        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => this.text;
    }
}