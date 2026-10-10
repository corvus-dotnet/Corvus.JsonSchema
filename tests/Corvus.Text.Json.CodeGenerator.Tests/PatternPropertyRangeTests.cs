// <copyright file="PatternPropertyRangeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// The emitter matches a <c>patternProperties</c> pattern of the form <c>^.{m,n}$</c> inline instead of with a regular
/// expression. Only ASCII digits are quantifier bounds there, so a pattern with other decimal digits in the braces is a
/// full regular expression (issue #964).
/// </summary>
[TestClass]
public class PatternPropertyRangeTests
{
    [TestMethod]
    public async Task GenerateCode_PatternPropertyWithNonAsciiDigitsInBraces_IsAFullRegularExpression()
    {
        const string schema =
            """
            {
              "type": "object",
              "patternProperties": {
                "^.{٣,٥}$": { "type": "string" },
                "^.{3,5}$": { "type": "integer" }
              }
            }
            """;

        IReadOnlyCollection<GeneratedCodeFile> files = await InProcessGenerationTests.GenerateInProcessFromContent(schema);

        string[] lines = files
            .SelectMany(f => f.FileContent.Split('\n'))
            .Select(l => l.Trim())
            .Where(l => l.Contains("MatchRangeRegularExpression", StringComparison.Ordinal) || (l.Contains("Regex", StringComparison.Ordinal) && l.Contains("٣", StringComparison.Ordinal)))
            .ToArray();

        // ^.{3,5}$ is matched inline; ^.{٣,٥}$ is a regular expression ('.' and '$' translated from ECMAScript).
        string[] expected =
        [
            "return JsonSchemaEvaluation.MatchRangeRegularExpression(propertyName, 3, 5);",
            """[GeneratedRegex("^[^\\n\\r\\u2028\\u2029]{٣,٥}\\z", RegexOptions.None, matchTimeoutMilliseconds: 10000)]""",
            """private static Regex CreatePatternProperties2() => new("^[^\\n\\r\\u2028\\u2029]{٣,٥}\\z", RegexOptions.Compiled, global::System.TimeSpan.FromMilliseconds(10000));""",
        ];

        if (!expected.SequenceEqual(lines, StringComparer.Ordinal))
        {
            Assert.Fail($"Expected:\n{string.Join("\n", expected)}\nActual:\n{string.Join("\n", lines)}");
        }
    }

    /// <summary>
    /// A pattern that ends in a line feed is a full regular expression. The emitter's own tests for a prefix and for a
    /// length range end in <c>$</c>, which in .NET also holds before a final line feed, so <c>^abc\n</c> was taken for
    /// the prefix <c>^abc</c> and <c>^.{1,3}$\n</c> for the range <c>^.{1,3}$</c>.
    /// </summary>
    [TestMethod]
    public async Task GenerateCode_PatternPropertyThatEndsInALineFeed_IsAFullRegularExpression()
    {
        const string schema =
            """
            {
              "type": "object",
              "patternProperties": {
                "^abc\n": { "type": "string" },
                "^.{1,3}$\n": { "type": "integer" },
                "^abd.*\n": { "type": "boolean" },
                "^abc": { "type": "null" }
              }
            }
            """;

        IReadOnlyCollection<GeneratedCodeFile> files = await InProcessGenerationTests.GenerateInProcessFromContent(schema);

        string[] lines = files
            .SelectMany(f => f.FileContent.Split('\n'))
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("return ", StringComparison.Ordinal) && (l.Contains("MatchRangeRegularExpression", StringComparison.Ordinal) || l.Contains("MatchRegularExpression(propertyName", StringComparison.Ordinal) || l.Contains("propertyName.StartsWith(", StringComparison.Ordinal)))
            .Distinct()
            .OrderBy(l => l, StringComparer.Ordinal)
            .ToArray();

        // Only ^abc, the second in order, is a prefix. The three patterns that end in a line feed are matched by their
        // regular expressions.
        string[] expected =
        [
            "return JsonSchemaEvaluation.MatchRegularExpression(propertyName, JsonSchema.PatternProperties1);",
            "return JsonSchemaEvaluation.MatchRegularExpression(propertyName, JsonSchema.PatternProperties3);",
            "return JsonSchemaEvaluation.MatchRegularExpression(propertyName, JsonSchema.PatternProperties4);",
            "return propertyName.StartsWith(\"abc\"u8);",
        ];

        if (!expected.SequenceEqual(lines, StringComparer.Ordinal))
        {
            Assert.Fail($"Expected:\n{string.Join("\n", expected)}\nActual:\n{string.Join("\n", lines)}");
        }
    }
}