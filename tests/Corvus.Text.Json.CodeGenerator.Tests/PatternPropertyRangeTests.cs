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

        // ^.{3,5}$ is matched inline; ^.{٣,٥}$ is a regular expression ('.' translated from ECMAScript).
        string[] expected =
        [
            "return JsonSchemaEvaluation.MatchRangeRegularExpression(propertyName, 3, 5);",
            """[GeneratedRegex("^[^\\n\\r\\u2028\\u2029]{٣,٥}$")]""",
            """private static Regex CreatePatternProperties2() => new("^[^\\n\\r\\u2028\\u2029]{٣,٥}$", RegexOptions.Compiled);""",
        ];

        if (!expected.SequenceEqual(lines, StringComparer.Ordinal))
        {
            Assert.Fail($"Expected:\n{string.Join("\n", expected)}\nActual:\n{string.Join("\n", lines)}");
        }
    }
}