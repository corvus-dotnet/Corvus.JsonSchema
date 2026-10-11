// <copyright file="InvalidPatternGenerationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// The generator stops at a schema whose <c>pattern</c> or <c>patternProperties</c> pattern is not a valid ECMA-262
/// regular expression, with or without a program image, as the runtime evaluator refuses to compile such a schema.
/// </summary>
[TestClass]
public class InvalidPatternGenerationTests
{
    [TestMethod]
    [DataRow("""{"type": "string", "pattern": "a{2,1}"}""", "a{2,1}", "pattern", false)]
    [DataRow("""{"type": "string", "pattern": "a{2,1}"}""", "a{2,1}", "pattern", true)]
    [DataRow("""{"type": "object", "patternProperties": {"^(?<n>a)(?<n>b)": {"type": "integer"}}}""", "^(?<n>a)(?<n>b)", "patternProperties", false)]
    [DataRow("""{"type": "object", "patternProperties": {"^(?<n>a)(?<n>b)": {"type": "integer"}}}""", "^(?<n>a)(?<n>b)", "patternProperties", true)]
    [DataRow("""{"type": "object", "patternProperties": {"^.{5,2}$": {"type": "integer"}}}""", "^.{5,2}$", "patternProperties", false)]
    public async Task GenerateCode_ForASchemaWithAnInvalidPattern_StopsAndNamesThePattern(string schema, string pattern, string keyword, bool withProgramImage)
    {
        InvalidOperationException thrown = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => GenerateAsync(schema, withProgramImage));
        StringAssert.StartsWith(thrown.Message, $"The pattern '{pattern}' of the keyword '{keyword}' is not a valid ECMA-262 regular expression. ");
    }

    [TestMethod]
    public async Task GenerateCode_ForASchemaWithAnInvalidPattern_SaysWhatIsWrongAndWhere()
    {
        InvalidOperationException thrown = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => GenerateAsync("""{"type": "string", "pattern": "^[a-z]{3,1}$"}""", withProgramImage: true));
        Assert.AreEqual(
            "The pattern '^[a-z]{3,1}$' of the keyword 'pattern' is not a valid ECMA-262 regular expression. Numbers out of order in {} quantifier at offset 11.",
            thrown.Message);
    }

    /// <summary>
    /// A pattern over a limit of size stops the generation too, with or without a program image. The exception is
    /// not the one of a pattern that is not valid, and its message says that the pattern is too large and names the
    /// keyword and the limit.
    /// </summary>
    [TestMethod]
    [DataRow("pattern", false)]
    [DataRow("pattern", true)]
    [DataRow("patternProperties", false)]
    [DataRow("patternProperties", true)]
    public async Task GenerateCode_ForASchemaWithAPatternOverALimitOfSize_StopsAndSaysItIsTooLarge(string keyword, bool withProgramImage)
    {
        const string Pattern = "(?:(?:(?:(?:(?:(?:(?:(?:(?:ab){4}){4}){4}){4}){4}){4}){4}){4}){4}";
        string schema = keyword == "pattern"
            ? "{\"type\": \"string\", \"pattern\": \"" + Pattern + "\"}"
            : "{\"type\": \"object\", \"patternProperties\": {\"" + Pattern + "\": {\"type\": \"integer\"}}}";

        EcmaRegexPatternTooLargeException thrown = await Assert.ThrowsExactlyAsync<EcmaRegexPatternTooLargeException>(() => GenerateAsync(schema, withProgramImage));
        Assert.AreEqual(
            $"The pattern '{Pattern}' of the keyword '{keyword}' is a valid ECMA-262 regular expression, and is too large. Its repetitions with a least count of 2, 3 or 4, one inside another, come to more than 65,536 copies of what they repeat, beyond four times its size.",
            thrown.Message);
        Assert.IsInstanceOfType<InvalidOperationException>(thrown);
    }

    /// <summary>
    /// A pattern that is valid only without the <c>u</c> flag is a valid pattern, and its translation is what the
    /// generated type's helper matches with.
    /// </summary>
    [TestMethod]
    public async Task GenerateCode_ForAPatternValidOnlyWithoutTheUFlag_WritesItsTranslation()
    {
        IReadOnlyList<GeneratedCodeFile> files = await GenerateAsync("""{"type": "object", "patternProperties": {"^a\\-b+{$": {"type": "integer"}}}""", withProgramImage: false);

        string[] lines =
        [
            .. files.SelectMany(f => f.FileContent.Split('\n'))
                .Select(l => l.Trim())
                .Where(l => l.StartsWith("[GeneratedRegex(", StringComparison.Ordinal))
        ];

        string[] expected =
        [
            """[GeneratedRegex("^a-b+\\{\\z", RegexOptions.None, matchTimeoutMilliseconds: 1000)]""",
        ];

        if (!expected.SequenceEqual(lines, StringComparer.Ordinal))
        {
            Assert.Fail($"Expected:\n{string.Join("\n", expected)}\nActual:\n{string.Join("\n", lines)}");
        }
    }

    /// <summary>
    /// A case-insensitive backreference is the one construct whose translation uses .NET's own case comparison, so
    /// the expression is constructed with the invariant culture.
    /// </summary>
    [TestMethod]
    public async Task GenerateCode_ForACaseInsensitiveBackreference_UsesTheInvariantCulture()
    {
        IReadOnlyList<GeneratedCodeFile> files = await GenerateAsync("""{"type": "object", "patternProperties": {"^(?i:(a)\\1)$": {"type": "integer"}}}""", withProgramImage: false);

        string[] lines =
        [
            .. files.SelectMany(f => f.FileContent.Split('\n'))
                .Select(l => l.Trim())
                .Where(l => l.Contains("GeneratedRegex(", StringComparison.Ordinal) || (l.Contains("Regex Create", StringComparison.Ordinal) && l.Contains("=> new(", StringComparison.Ordinal)))
        ];

        string[] expected =
        [
            """[GeneratedRegex("^(?:(?<1>[Aa])(?(1)(?i:\\1)))\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]""",
            """private static Regex CreatePatternProperties() => new("^(?:(?<1>[Aa])(?(1)(?i:\\1)))\\z", RegexOptions.Compiled | RegexOptions.CultureInvariant, global::System.TimeSpan.FromMilliseconds(1000));""",
        ];

        if (!expected.SequenceEqual(lines, StringComparer.Ordinal))
        {
            Assert.Fail($"Expected:\n{string.Join("\n", expected)}\nActual:\n{string.Join("\n", lines)}");
        }
    }

    /// <summary>
    /// A translation longer than <c>EcmaRegexTranslator.LongestCompiledTranslation</c> is constructed without
    /// <c>RegexOptions.Compiled</c>, which .NET would take seconds over when the type first validates.
    /// </summary>
    [TestMethod]
    public async Task GenerateCode_ForALongTranslation_DoesNotCompileIt()
    {
        IReadOnlyList<GeneratedCodeFile> files = await GenerateAsync("""{"type": "object", "patternProperties": {"^(?i:(.+)-\\1)$": {"type": "integer"}}}""", withProgramImage: false);

        string[] lines =
        [
            .. files.SelectMany(f => f.FileContent.Split('\n'))
                .Select(l => l.Trim())
                .Where(l => l.Contains("Regex Create", StringComparison.Ordinal) && l.Contains("=> new(", StringComparison.Ordinal))
        ];

        Assert.AreEqual(1, lines.Length);
        Assert.IsTrue(lines[0].Length > 100_000, $"{lines[0].Length} characters");
        Assert.IsFalse(lines[0].Contains("RegexOptions.Compiled", StringComparison.Ordinal));
        StringAssert.EndsWith(lines[0], """, RegexOptions.CultureInvariant, global::System.TimeSpan.FromMilliseconds(1000));""");
    }

    /// <summary>
    /// A translation marked for the interpreted engine has no <c>[GeneratedRegex]</c> form, in the type or in the
    /// schema program: .NET's compiled engine does not always return from it, and the regular expression generator
    /// writes the same algorithm. The type constructs it with no <c>RegexOptions.Compiled</c>, and the program
    /// leaves it to the evaluator.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task GenerateCode_ForATranslationMarkedForTheInterpretedEngine_DoesNotCompileIt(bool withProgramImage)
    {
        IReadOnlyList<GeneratedCodeFile> files = await GenerateAsync("""{"type": "object", "patternProperties": {"(?=((?:ab){0,2}?)c)\\1": {"type": "integer"}}}""", withProgramImage);

        string[] lines =
        [
            .. files.SelectMany(f => f.FileContent.Split('\n'))
                .Select(l => l.Trim())
                .Where(l => l.Contains("GeneratedRegex(", StringComparison.Ordinal) || (l.Contains("Regex Create", StringComparison.Ordinal) && l.Contains("=> new(", StringComparison.Ordinal)))
        ];

        string[] expected =
        [
            """private static Regex CreatePatternProperties() => new("(?#interpreted)(?=(?<1>(?:(?:ab)(?:(?:ab))??)??)c)(?(1)\\1)", RegexOptions.None, global::System.TimeSpan.FromMilliseconds(1000));""",
        ];

        if (!expected.SequenceEqual(lines, StringComparer.Ordinal))
        {
            Assert.Fail($"Expected:\n{string.Join("\n", expected)}\nActual:\n{string.Join("\n", lines)}");
        }
    }

    private static async Task<IReadOnlyList<GeneratedCodeFile>> GenerateAsync(string schema, bool withProgramImage)
    {
        string path = Path.Combine(Path.GetTempPath(), $"invalid-pattern-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, schema);
        using CompoundDocumentResolver resolver = new(new FileSystemDocumentResolver());
        try
        {
            VocabularyRegistry vocabularyRegistry = new();
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(resolver, vocabularyRegistry);
            JsonSchemaTypeBuilder typeBuilder = new(resolver, vocabularyRegistry);
            TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
                new JsonReference(path),
                Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary);

            CSharpLanguageProvider.Options options = new(
                "TestGenerated",
                namedTypes: [new CSharpLanguageProvider.NamedType(rootType.ReducedTypeDeclaration().ReducedType.LocatedSchema.Location, "Root")],
                programCompiler: withProgramImage ? Corvus.Json.CodeGenerator.RuntimeProgramCompiler.Compile : null);
            CSharpLanguageProvider languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);
            return [.. typeBuilder.GenerateCodeUsing(languageProvider, [rootType], CancellationToken.None).OrderBy(f => f.FileName, StringComparer.Ordinal)];
        }
        finally
        {
            File.Delete(path);
        }
    }
}