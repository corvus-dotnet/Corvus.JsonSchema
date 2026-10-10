// <copyright file="V4SourceGeneratorRegexPatternTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

extern alias V4SourceGenerator;

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.SourceGenerator.Tests;

/// <summary>
/// The regular expressions in the code the V4 source generator (<c>Corvus.Json.SourceGenerator</c>) emits. Roslyn does not chain generators, so there is
/// no <c>[GeneratedRegex]</c> in it: a generated type constructs its expression, with the match timeout of 10
/// seconds. A pattern that is not a valid ECMA-262 regular expression is reported as the diagnostic CRV1001.
/// </summary>
[TestClass]
public class V4SourceGeneratorRegexPatternTests
{
    private const string SchemaFileName = "regex-pattern-schema.json";

    private static readonly string Source = $$"""
        using Corvus.Json;

        namespace RegexPatternTests;

        [JsonSchemaTypeGenerator("{{SchemaFileName}}")]
        public readonly partial struct Root;
        """;

    private static readonly string TestDirectory = Path.Combine(Path.GetTempPath(), "corvus-v4-regex-pattern-tests");

    [TestMethod]
    public void EmittedCode_ConstructsItsRegularExpressionsWithTheTimeout()
    {
        GeneratorRunResult result = Run("""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "properties": {
                "code": { "type": "string", "pattern": "^(a|aa)+$" },
                "twice": { "type": "string", "pattern": "^(?i:(a)\\1)$" }
              },
              "patternProperties": {
                "^(a|aa)+-$": { "type": "integer" }
              }
            }
            """);

        Assert.IsNull(result.Exception, $"The generator threw: {result.Exception}");
        Assert.AreEqual(0, result.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error), string.Join("\n", result.Diagnostics));

        string[] lines =
        [
            .. result.GeneratedSources
                .SelectMany(s => s.SourceText.ToString().Split('\n'))
                .Select(l => l.Trim())
                .Where(l => l.Contains("GeneratedRegex(", StringComparison.Ordinal) || (l.Contains("Regex Create", StringComparison.Ordinal) && l.Contains("=> new(", StringComparison.Ordinal)))
                .OrderBy(l => l, StringComparer.Ordinal)
        ];

        string[] expected =
        [
            """private static Regex CreatePattern() => new("^(?:(?<1>[Aa])(?(1)(?i:\\1)))\\z", RegexOptions.Compiled | RegexOptions.CultureInvariant, global::System.TimeSpan.FromMilliseconds(10000));""",
            """private static Regex CreatePattern() => new("^(?:a|aa)+\\z", RegexOptions.Compiled, global::System.TimeSpan.FromMilliseconds(10000));""",
            """private static Regex CreatePatternProperties() => new("^(?:a|aa)+-\\z", RegexOptions.Compiled, global::System.TimeSpan.FromMilliseconds(10000));""",
        ];

        if (!expected.SequenceEqual(lines, StringComparer.Ordinal))
        {
            Assert.Fail($"Expected:\n{string.Join("\n", expected)}\nActual:\n{string.Join("\n", lines)}");
        }
    }

    [TestMethod]
    public void APatternThatIsNotValid_IsReportedAsADiagnostic()
    {
        GeneratorRunResult result = Run("""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "string",
              "pattern": "a{2,1}"
            }
            """);

        Assert.IsNull(result.Exception, $"The generator threw: {result.Exception}");
        Diagnostic[] errors = [.. result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)];
        Assert.AreEqual(1, errors.Length, string.Join("\n", result.Diagnostics));
        Assert.AreEqual("CRV1001", errors[0].Id);
        StringAssert.Contains(errors[0].GetMessage(), "The pattern 'a{2,1}' of the keyword 'pattern' is not a valid ECMA-262 regular expression. Numbers out of order in {} quantifier at offset 6.");
    }

    private static GeneratorRunResult Run(string schema)
    {
        IEnumerable<MetadataReference> references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p) is "System.Runtime.dll" or "System.Private.CoreLib.dll" or "netstandard.dll" or "mscorlib.dll")
            .Select(p => MetadataReference.CreateFromFile(p));

        CSharpCompilation compilation = CSharpCompilation.Create(
            "RegexPatternTests",
            [CSharpSyntaxTree.ParseText(Source, path: Path.Combine(TestDirectory, "Root.cs"))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new V4SourceGenerator::Corvus.Json.SourceGenerator.IncrementalSourceGenerator().AsSourceGenerator()],
            additionalTexts: [new InMemoryAdditionalText(Path.Combine(TestDirectory, SchemaFileName), schema)]);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> _);
        return driver.GetRunResult().Results[0];
    }

    private sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
    {
        private readonly SourceText sourceText = SourceText.From(text, Encoding.UTF8);

        public override string Path => path;

        public override SourceText? GetText(CancellationToken cancellationToken = default) => this.sourceText;
    }
}