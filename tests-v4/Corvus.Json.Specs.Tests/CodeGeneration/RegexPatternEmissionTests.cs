// <copyright file="RegexPatternEmissionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.Specs.Tests.Infrastructure;
using Drivers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.CodeGeneration;

/// <summary>
/// The regular expressions of V4 generated code. A pattern is translated from ECMA-262 by the translator the V5
/// generator and evaluator use, every expression has a match timeout of 1 second, and a pattern that is not a
/// valid ECMA-262 regular expression stops the generation.
/// </summary>
[TestClass]
public class RegexPatternEmissionTests
{
    private const string TimeoutSchema = """
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

    /// <summary>
    /// The emitted text of both forms a generated type has outside the source generator: the regular expression
    /// generator's attribute, and the constructor where there is no regular expression generator. The source
    /// generator's form is the same constructor (see <c>ValidationCodeGeneratorExtensions.Constants.cs</c>).
    /// </summary>
    [TestMethod]
    public async Task EmittedCode_HasTheMatchTimeoutInEveryForm()
    {
        string[] lines = await RegexLinesAsync(TimeoutSchema, "Timeout");

        string[] expected =
        [
            """[GeneratedRegex("^(?:a|aa)+-\\z", RegexOptions.None, matchTimeoutMilliseconds: 1000)]""",
            """[GeneratedRegex("^(?:a|aa)+\\z", RegexOptions.None, matchTimeoutMilliseconds: 1000)]""",
            """private static Regex CreatePattern() => new("^(?:a|aa)+\\z", RegexOptions.Compiled, global::System.TimeSpan.FromMilliseconds(1000));""",
            """private static Regex CreatePatternProperties() => new("^(?:a|aa)+-\\z", RegexOptions.Compiled, global::System.TimeSpan.FromMilliseconds(1000));""",
        ];

        AssertLines(expected, lines);
    }

    /// <summary>
    /// A case-insensitive backreference to a text of plain ASCII letters is the one construct whose translation uses
    /// .NET's own case comparison, so the expression is constructed with the invariant culture.
    /// </summary>
    [TestMethod]
    public async Task EmittedCode_ForACaseInsensitiveBackreference_UsesTheInvariantCulture()
    {
        string[] lines = await RegexLinesAsync("""{"type": "string", "pattern": "^(?i:(a)\\1)$"}""", "InvariantCulture");

        string[] expected =
        [
            """[GeneratedRegex("^(?:(?<1>[Aa])(?(1)(?i:\\1)))\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]""",
            """private static Regex CreatePattern() => new("^(?:(?<1>[Aa])(?(1)(?i:\\1)))\\z", RegexOptions.Compiled | RegexOptions.CultureInvariant, global::System.TimeSpan.FromMilliseconds(1000));""",
        ];

        AssertLines(expected, lines);
    }

    /// <summary>
    /// A translation too long to be worth compiling is constructed without <c>RegexOptions.Compiled</c>, which .NET
    /// would take seconds over when the type first validates. The only such translations are those of a
    /// case-insensitive backreference to a group that can capture any letter.
    /// </summary>
    [TestMethod]
    public async Task EmittedCode_ForALongTranslation_DoesNotCompileIt()
    {
        string[] lines = await RegexLinesAsync("""{"type": "string", "pattern": "^(?i:(.+)-\\1)$"}""", "LongTranslation");

        Assert.AreEqual(2, lines.Length);
        StringAssert.StartsWith(lines[0], "[GeneratedRegex(");
        StringAssert.EndsWith(lines[0], """, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]""");
        Assert.IsTrue(lines[1].Length > 100_000, $"{lines[1].Length} characters");
        Assert.IsTrue(lines[1].IndexOf("RegexOptions.Compiled", StringComparison.Ordinal) < 0);
        StringAssert.EndsWith(lines[1], """, RegexOptions.CultureInvariant, global::System.TimeSpan.FromMilliseconds(1000));""");
    }

    /// <summary>
    /// A translation marked for the interpreted engine has no <c>[GeneratedRegex]</c> form: .NET's compiled engine
    /// does not always return from it, and the regular expression generator writes the same algorithm. It is
    /// constructed with no <c>RegexOptions.Compiled</c>.
    /// </summary>
    [TestMethod]
    public async Task EmittedCode_ForATranslationMarkedForTheInterpretedEngine_DoesNotCompileIt()
    {
        string[] lines = await RegexLinesAsync("""{"type": "string", "pattern": "(?=((?:ab){0,2}?)c)\\1"}""", "Interpreted");

        string[] expected =
        [
            """private static Regex CreatePattern() => new("(?#interpreted)(?=(?<1>(?:(?:ab)(?:(?:ab))??)??)c)(?(1)\\1)", RegexOptions.None, global::System.TimeSpan.FromMilliseconds(1000));""",
        ];

        AssertLines(expected, lines);
    }

    /// <summary>
    /// A pattern valid only without the <c>u</c> flag is translated by that grammar, as V8 reads it, where it was
    /// given to .NET as it stood.
    /// </summary>
    [TestMethod]
    public async Task EmittedCode_ForAPatternValidOnlyWithoutTheUFlag_IsItsTranslation()
    {
        string[] lines = await RegexLinesAsync("""{"type": "string", "pattern": "^a\\-b{$"}""", "Legacy");

        string[] expected =
        [
            """[GeneratedRegex("^a-b\\{\\z", RegexOptions.None, matchTimeoutMilliseconds: 1000)]""",
            """private static Regex CreatePattern() => new("^a-b\\{\\z", RegexOptions.Compiled, global::System.TimeSpan.FromMilliseconds(1000));""",
        ];

        AssertLines(expected, lines);
    }

    /// <summary>
    /// A pattern that is not a valid ECMA-262 regular expression stops the generation, with the pattern, the keyword
    /// and what is wrong with it.
    /// </summary>
    [TestMethod]
    [DataRow("""{"type": "string", "pattern": "a{2,1}"}""", "The pattern 'a{2,1}' of the keyword 'pattern' is not a valid ECMA-262 regular expression. Numbers out of order in {} quantifier at offset 6.")]
    [DataRow("""{"type": "string", "pattern": "(?i)a"}""", "The pattern '(?i)a' of the keyword 'pattern' is not a valid ECMA-262 regular expression.")]
    [DataRow("""{"type": "string", "pattern": "(?<n>a)(?<n>b)"}""", "The pattern '(?<n>a)(?<n>b)' of the keyword 'pattern' is not a valid ECMA-262 regular expression. Duplicate group name 'n' at offset 12.")]
    [DataRow("""{"type": "object", "patternProperties": {"[z-a]": {"type": "integer"}}}""", "The pattern '[z-a]' of the keyword 'patternProperties' is not a valid ECMA-262 regular expression. Range out of order in character class at offset 2.")]
    public async Task GenerateCode_ForAPatternThatIsNotValid_Throws(string schema, string expectedMessage)
    {
        using JsonSchemaBuilderDriver driver = DriverFactory.CreateDraft202012Driver();

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await driver.GenerateCodeForVirtualFile(schema, $"invalid-pattern-{Guid.NewGuid():N}.json", "RegexPatternEmission", "Invalid", validateFormat: false, optionalAsNullable: false, useImplicitOperatorString: false));

        StringAssert.StartsWith(ex.Message, expectedMessage);
    }

    /// <summary>
    /// A match of the <c>pattern</c> keyword that outlasts the timeout throws to the caller of <c>Validate</c>. It is
    /// not reported as an instance that is not valid. This waits for the 1 second.
    /// </summary>
    [TestMethod]
    public async Task ValidatingWithAPatternThatDoesNotFinish_ThrowsRegexMatchTimeoutException()
    {
        using JsonSchemaBuilderDriver driver = DriverFactory.CreateDraft202012Driver();
        Type type = await driver.GenerateTypeForVirtualFile("""{"type": "string", "pattern": "^(a|aa)+$"}""", $"timeout-pattern-{Guid.NewGuid():N}.json", "RegexPatternEmission", "PatternTimeout", validateFormat: false, optionalAsNullable: false, useImplicitOperatorString: false);

        using var doc = JsonDocument.Parse("\"" + new string('a', 48) + "#\"");
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(type, doc.RootElement);

        // On a worker with a bounded wait, so that a match with no timeout is a failed assertion and not a test run
        // that never ends.
        Task<Exception?> validation = Task.Run(() =>
        {
            try
            {
                instance.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        });

        Assert.IsTrue(await Task.WhenAny(validation, Task.Delay(TimeSpan.FromSeconds(60))) == validation, "The match did not time out.");
        Exception? thrown = await validation;
        Assert.IsInstanceOfType<RegexMatchTimeoutException>(thrown);
        Assert.AreEqual(TimeSpan.FromSeconds(1), ((RegexMatchTimeoutException)thrown!).MatchTimeout);
    }

    /// <summary>
    /// Patterns the translation had wrong before it was written from the syntax tree, through a generated type: a
    /// character beyond the Basic Multilingual Plane is one character, a modifier group is ECMA-262's, a
    /// backreference in a repetition reads the capture of its own iteration, and an empty alternative in a
    /// repetition matches the empty string. The answers are V8's.
    /// </summary>
    [TestMethod]
    [DataRow("^.$", "\uD83D\uDE00", true)]
    [DataRow("^[^a]{2}$", "\uD83D\uDE00", false)]
    [DataRow("^(?i:\u017F)$", "s", true)]
    [DataRow("^(?s:.)$", "\n", true)]
    [DataRow("^(?:(a)|b)*\\1$", "ab", true)]
    [DataRow("^(?:(a)|b)*\\1$", "aba", false)]
    [DataRow("^(?:a+|)+$", "", true)]
    [DataRow("^(?i:(.)\\1)$", "\u03C2\u03C3", true)]
    [DataRow("^a\\-b{$", "a-b{", true)]
    [DataRow("(\\B|[]){4,}(?:(?<=(.??)\\2))$", "-", true)]
    [DataRow("(\\B|[]){4,}(?:(?<=(.??)\\2))$", "-a", false)]
    public async Task AGeneratedTypeAnswersAsV8(string pattern, string text, bool expected)
    {
        using JsonSchemaBuilderDriver driver = DriverFactory.CreateDraft202012Driver();
        string schema = JsonSerializer.Serialize(new Dictionary<string, object> { ["type"] = "string", ["pattern"] = pattern });
        Type type = await driver.GenerateTypeForVirtualFile(schema, $"semantics-{Guid.NewGuid():N}.json", "RegexPatternEmission", "Semantics", validateFormat: false, optionalAsNullable: false, useImplicitOperatorString: false);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(text));
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(type, doc.RootElement);

        Assert.AreEqual(expected, instance.Validate(ValidationContext.ValidContext, ValidationLevel.Flag).IsValid);
    }

    private static async Task<string[]> RegexLinesAsync(string schema, string scenario)
    {
        using JsonSchemaBuilderDriver driver = DriverFactory.CreateDraft202012Driver();
        IReadOnlyCollection<GeneratedCodeFile> files = await driver.GenerateCodeForVirtualFile(schema, $"regex-emission-{Guid.NewGuid():N}.json", "RegexPatternEmission", scenario, validateFormat: false, optionalAsNullable: false, useImplicitOperatorString: false);

        return
        [
            .. files.SelectMany(f => f.FileContent.Split('\n'))
                .Select(l => l.Trim())
                .Where(l => l.IndexOf("GeneratedRegex(", StringComparison.Ordinal) >= 0 || (l.IndexOf("Regex Create", StringComparison.Ordinal) >= 0 && l.IndexOf("=> new(", StringComparison.Ordinal) >= 0))
                .OrderBy(l => l, StringComparer.Ordinal)
        ];
    }

    private static void AssertLines(string[] expected, string[] actual)
    {
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            Assert.Fail($"Expected:\n{string.Join("\n", expected)}\nActual:\n{string.Join("\n", actual)}");
        }
    }
}