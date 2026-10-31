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

    /// <summary>
    /// A pattern over a limit of size is an error of its own, CRV1003, whose message says that the pattern is too
    /// large and names the limit. It is not reported as a pattern that is not valid.
    /// </summary>
    [TestMethod]
    [DataRow("""{"type": "string", "pattern": "(?:(?:(?:(?:(?:(?:(?:(?:(?:ab){4}){4}){4}){4}){4}){4}){4}){4}){4}"}""", "pattern")]
    [DataRow("""{"type": "object", "patternProperties": {"(?:(?:(?:(?:(?:(?:(?:(?:(?:ab){4}){4}){4}){4}){4}){4}){4}){4}){4}": {"type": "integer"}}}""", "patternProperties")]
    public void ASchemaWithAPatternOverALimitOfSize_IsAnErrorOfItsOwn(string schema, string keyword)
    {
        GeneratorRunResult result = Run(schema);

        Assert.IsNull(result.Exception, $"The generator threw: {result.Exception}");
        Diagnostic[] errors = [.. result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)];
        Assert.AreEqual(1, errors.Length, string.Join("\n", result.Diagnostics));
        Assert.AreEqual("CRV1003", errors[0].Id);
        Assert.AreEqual(
            "The pattern '(?:(?:(?:(?:(?:(?:(?:(?:(?:ab){4}){4}){4}){4}){4}){4}){4}){4}){4}' of the keyword '" + keyword + "' is a valid ECMA-262 regular expression, and is too large. Its repetitions with a least count of 2, 3 or 4, one inside another, come to more than 65,536 copies of what they repeat, beyond four times its size.",
            errors[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture));
        Assert.IsFalse(result.GeneratedSources.Any(s => s.SourceText.ToString().Contains("partial struct Root", StringComparison.Ordinal)), "Nothing is generated for the schema.");
    }

    [TestMethod]
    public void ASchemaWithAPatternValidOnlyWithoutTheUFlag_IsGenerated()
    {
        GeneratorRunResult result = Run("""{"type": "object", "patternProperties": {"^a\\-b{$": {"type": "integer"}}}""");

        Assert.IsNull(result.Exception, $"The generator threw: {result.Exception}");
        Assert.AreEqual(0, result.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error), string.Join("\n", result.Diagnostics));

        string[] lines =
        [
            .. result.GeneratedSources
                .SelectMany(s => s.SourceText.ToString().Split('\n'))
                .Select(l => l.Trim())
                .Where(l => l.Contains("Regex Create", StringComparison.Ordinal) && l.Contains("=> new(", StringComparison.Ordinal))
        ];

        string[] expected =
        [
            """private static Regex CreatePatternProperties() => new("^a-b\\{\\z", RegexOptions.Compiled, global::System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);""",
        ];

        if (!expected.SequenceEqual(lines, StringComparer.Ordinal))
        {
            Assert.Fail($"Expected:\n{string.Join("\n", expected)}\nActual:\n{string.Join("\n", lines)}");
        }
    }

    // The tests above read patterns with the translation to .NET's regular expressions, as the DotNet engine does;
    // the ones that follow read them with Corvus.Regex.Ecma262, as the default engine does.
    [TestMethod]
    [DataRow("""{"type": "string", "pattern": "^[a-z]{3,1}$"}""", "pattern", "^[a-z]{3,1}$")]
    [DataRow("""{"type": "object", "patternProperties": {"^x\\b+": {"type": "integer"}}}""", "patternProperties", "^x\\b+")]
    public void WithTheLibrary_ASchemaWithAnInvalidPattern_IsAnErrorThatNamesThePattern(string schema, string keyword, string pattern)
    {
        GeneratorRunResult result = Run(schema, "Ecma262");

        Assert.IsNull(result.Exception, $"The generator threw: {result.Exception}");
        Diagnostic[] errors = [.. result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)];
        Assert.AreEqual(1, errors.Length, string.Join("\n", result.Diagnostics));
        Assert.AreEqual("CRV1001", errors[0].Id);
        StringAssert.StartsWith(errors[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture), $"Error generating C# code: The pattern '{pattern}' of the keyword '{keyword}' is not a valid ECMA-262 regular expression.");
    }

    [TestMethod]
    [DataRow("pattern")]
    [DataRow("patternProperties")]
    public void WithTheLibrary_ASchemaWithAPatternOverItsLimits_IsAnErrorOfItsOwn(string keyword)
    {
        // Nested past the 200 groups the library compiles.
        string pattern = new string('(', 201) + "a" + new string(')', 201);
        string schema = keyword == "pattern"
            ? "{\"type\": \"string\", \"pattern\": \"" + pattern + "\"}"
            : "{\"type\": \"object\", \"patternProperties\": {\"" + pattern + "\": {\"type\": \"integer\"}}}";
        GeneratorRunResult result = Run(schema, "Ecma262");

        Assert.IsNull(result.Exception, $"The generator threw: {result.Exception}");
        Diagnostic[] errors = [.. result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)];
        Assert.AreEqual(1, errors.Length, string.Join("\n", result.Diagnostics));
        Assert.AreEqual("CRV1003", errors[0].Id);
        StringAssert.Contains(errors[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture), "too large");
    }

    [TestMethod]
    public void WithTheLibrary_ASchemaWithAPatternValidOnlyWithoutTheUFlag_IsGeneratedWithNoFlag()
    {
        GeneratorRunResult result = Run("""{"type": "object", "patternProperties": {"^a\\-b{$": {"type": "integer"}}}""", "Ecma262");

        Assert.IsNull(result.Exception, $"The generator threw: {result.Exception}");
        Assert.AreEqual(0, result.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error), string.Join("\n", result.Diagnostics));
        string code = string.Concat(result.GeneratedSources.Select(s => s.SourceText.ToString()));
        StringAssert.Contains(code, "public static readonly global::Corvus.Ecma262.RegularExpressions.Regex PatternProperties = global::InvalidPatternTests.CorvusJsonSchemaRegularExpressions.Regex0;");
        StringAssert.Contains(code, "(global::Corvus.Ecma262.RegularExpressions.RegexFlags)0,");
    }

    private static GeneratorRunResult Run(string schema, string engine = "DotNet")
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
            additionalTexts: [new InMemoryAdditionalText(Path.Combine(TestDirectory, SchemaFileName), schema)],
            optionsProvider: RegexEngineOptions.For(engine));

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