using System.Collections.Immutable;
using System.Text;
using Corvus.Json.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.SourceGenerator.Tests;

/// <summary>
/// The code the source generator emits has the regular expression match timeout of 1 second. Roslyn does not chain
/// generators, so there is no <c>[GeneratedRegex]</c> in it: a generated type constructs its expression, and the
/// program's options give the evaluator the timeout for the expressions it constructs.
/// </summary>
[TestClass]
public class RegexMatchTimeoutTests
{
    private const string SchemaFileName = "regex-timeout-schema.json";

    private const string Schema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "properties": {
            "code": { "type": "string", "pattern": "^(a|aa)+$" }
          },
          "patternProperties": {
            "^(a|aa)+-$": { "type": "integer" }
          }
        }
        """;

    private static readonly string Source = $$"""
        using Corvus.Text.Json;

        namespace RegexTimeoutTests;

        [JsonSchemaTypeGenerator("{{SchemaFileName}}")]
        public readonly partial struct Root;
        """;

    private static readonly string TestDirectory = Path.Combine(Path.GetTempPath(), "corvus-regex-timeout-tests");

    [TestMethod]
    public void EmittedCode_ConstructsItsRegularExpressionsWithTheTimeout()
    {
        IEnumerable<MetadataReference> references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p) is "System.Runtime.dll" or "System.Private.CoreLib.dll" or "netstandard.dll" or "mscorlib.dll")
            .Select(p => MetadataReference.CreateFromFile(p));

        CSharpCompilation compilation = CSharpCompilation.Create(
            "RegexTimeoutTests",
            [CSharpSyntaxTree.ParseText(Source, path: Path.Combine(TestDirectory, "Root.cs"))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // The translation to .NET's regular expressions, as the DotNet engine writes it.
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new IncrementalSourceGenerator().AsSourceGenerator()],
            additionalTexts: [new InMemoryAdditionalText(Path.Combine(TestDirectory, SchemaFileName), Schema)],
            optionsProvider: RegexEngineOptions.For("DotNet"));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> _);
        GeneratorRunResult result = driver.GetRunResult().Results[0];
        Assert.IsNull(result.Exception, $"The generator threw: {result.Exception}");

        string[] lines =
        [
            .. result.GeneratedSources
                .SelectMany(s => s.SourceText.ToString().Split('\n'))
                .Select(l => l.Trim())
                .Where(l => l.Contains("GeneratedRegex(", StringComparison.Ordinal) || (l.Contains("Regex Create", StringComparison.Ordinal) && l.Contains("=> new(", StringComparison.Ordinal)) || l.Contains("RegexMatchTimeout", StringComparison.Ordinal))
                .OrderBy(l => l, StringComparer.Ordinal)
        ];

        string[] expected =
        [
            """RegexMatchTimeout = global::System.TimeSpan.FromMilliseconds(1000),""",
            """private static Regex CreatePatternProperties() => new("^(?:a|aa)+-\\z", RegexOptions.Compiled, global::System.TimeSpan.FromMilliseconds(1000));""",
        ];

        if (!expected.SequenceEqual(lines, StringComparer.Ordinal))
        {
            Assert.Fail($"Expected:\n{string.Join("\n", expected)}\nActual:\n{string.Join("\n", lines)}");
        }
    }

    private sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
    {
        private readonly SourceText sourceText = SourceText.From(text, Encoding.UTF8);

        public override string Path => path;

        public override SourceText? GetText(CancellationToken cancellationToken = default) => this.sourceText;
    }
}