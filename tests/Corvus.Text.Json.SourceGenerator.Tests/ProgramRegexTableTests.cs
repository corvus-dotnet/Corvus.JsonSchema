// <copyright file="ProgramRegexTableTests.cs" company="Endjin Limited">
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
/// The program the source generator compiles carries Corvus.Regex.Ecma262's compiled matcher for each pattern its
/// evaluator matches with a regular expression, and none for a pattern a matcher of the evaluator's own takes.
/// </summary>
[TestClass]
public class ProgramRegexTableTests
{
    private const string SchemaFileName = "program-regex-table-schema.json";

    private static readonly string Source = $$"""
        using Corvus.Text.Json;

        namespace ProgramRegexTableTests;

        [JsonSchemaTypeGenerator("{{SchemaFileName}}")]
        public readonly partial struct Root;
        """;

    private static readonly string TestDirectory = Path.Combine(Path.GetTempPath(), "corvus-program-regex-table-tests");

    [TestMethod]
    public void APatternTheLibraryMatchesHasACompiledMatcherInTheProgram()
    {
        string program = Program("""{"type": "string", "pattern": "^(alpha|beta|gamma|delta)-[0-9]+$"}""", "Ecma262", out string? shared);
        StringAssert.Contains(program, "EcmaRegexProvider = GetEcmaRegex,");
        StringAssert.Contains(program, "0 => global::ProgramRegexTableTests.CorvusJsonSchemaRegularExpressions.Regex0,");
        Assert.IsNotNull(shared, "The matchers are written once, in their own file.");
        StringAssert.Contains(shared, "internal static global::Corvus.Ecma262.RegularExpressions.Regex Regex0 => Regex0Holder.Instance;");
    }

    [TestMethod]
    public void APatternAMatcherOfTheEvaluatorsOwnTakesHasNone()
    {
        string program = Program("""{"type": "string", "pattern": "^[a-z][a-z0-9]*$"}""", "Ecma262", out string? shared);
        Assert.IsFalse(program.Contains("EcmaRegexProvider", StringComparison.Ordinal), program);
        Assert.IsNull(shared, "No regular expression, no file of them.");
    }

    [TestMethod]
    public void AProgramForDotNetsEngineHasNone()
    {
        // Generators do not chain, so .NET's regular expression generator would not implement a table of
        // [GeneratedRegex] methods: the evaluator compiles each pattern when it first needs it.
        string program = Program("""{"type": "string", "pattern": "^(alpha|beta|gamma|delta)-[0-9]+$"}""", "DotNet", out _);
        Assert.IsFalse(program.Contains("EcmaRegexProvider", StringComparison.Ordinal), program);
        Assert.IsFalse(program.Contains("[GeneratedRegex(", StringComparison.Ordinal), program);
    }

    private static string Program(string schema, string engine, out string? shared)
    {
        IEnumerable<MetadataReference> references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p) is "System.Runtime.dll" or "System.Private.CoreLib.dll" or "netstandard.dll" or "mscorlib.dll")
            .Select(p => MetadataReference.CreateFromFile(p));

        CSharpCompilation compilation = CSharpCompilation.Create(
            "ProgramRegexTableTests",
            [CSharpSyntaxTree.ParseText(Source, path: Path.Combine(TestDirectory, "Root.cs"))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new IncrementalSourceGenerator().AsSourceGenerator()],
            additionalTexts: [new InMemoryAdditionalText(Path.Combine(TestDirectory, SchemaFileName), schema)],
            optionsProvider: RegexEngineOptions.For(engine));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> _);
        GeneratorRunResult result = driver.GetRunResult().Results[0];
        Assert.IsNull(result.Exception, $"The generator threw: {result.Exception}");
        Assert.AreEqual(0, result.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error), string.Join("\n", result.Diagnostics));
        shared = result.GeneratedSources.SingleOrDefault(s => s.HintName.Contains("CorvusJsonSchemaRegularExpressions", StringComparison.Ordinal)).SourceText?.ToString();
        return result.GeneratedSources.Single(s => s.HintName.Contains("CorvusJsonSchemaProgram", StringComparison.Ordinal)).SourceText.ToString();
    }

    private sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
    {
        private readonly SourceText text = SourceText.From(text, System.Text.Encoding.UTF8);

        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => this.text;
    }
}