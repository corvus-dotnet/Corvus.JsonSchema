// <copyright file="PatternRegexFieldCollisionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Corvus.Text.Json.EvaluatorTestSuite.Tests;

/// <summary>
/// Repro tests for https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/947.
/// The standalone evaluator must compile a distinct <c>Regex</c> for every distinct
/// <c>pattern</c> keyword in the document; a shared field name causes the first
/// compiled regex to be used at every pattern evaluation site.
/// </summary>
[TestClass]
public class PatternRegexFieldCollisionTests
{
    /// <summary>
    /// Issue 947 false-rejection reproducer: the <c>not</c> subschema's pattern site
    /// must match against <c>^forbidden$</c>, not the first pattern's compiled regex.
    /// </summary>
    private const string FalseRejectionSchema =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "allOf": [
                { "pattern": "^[a-z]+$" },
                { "not": { "pattern": "^forbidden$" } }
            ]
        }
        """;

    /// <summary>
    /// Issue 947 false-acceptance reproducer: the second pattern site must match
    /// against <c>[13579]$</c>, not the first pattern's compiled regex.
    /// </summary>
    private const string FalseAcceptanceSchema =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "allOf": [
                { "pattern": "^[0-9]+$" },
                { "pattern": "[13579]$" }
            ]
        }
        """;

    /// <summary>
    /// The two patterns are distinct regexes but collapse to the same safe identifier
    /// (<c>ab_cd</c>), so a fix that names fields by sanitized pattern text alone still
    /// collides. Their languages are disjoint, so whichever single regex wins the
    /// collision, "a-c" (valid: matches the first, not the second) is rejected.
    /// </summary>
    private const string CollidingIdentifierSchema =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "allOf": [
                { "pattern": "^[ab]-[cd]$" },
                { "not": { "pattern": "^[ab]\\+[cd]$" } }
            ]
        }
        """;

    /// <summary>
    /// The patternProperties path names fields by sanitized pattern text; these two
    /// distinct patterns both collapse to <c>a_b</c>. "a.a" matches only the second
    /// pattern, so it must be validated as a string and nothing else.
    /// </summary>
    private const string PatternPropertiesCollisionSchema =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "patternProperties": {
                "^[a-b]+$": { "type": "integer" },
                "^[a.b]+$": { "type": "string" }
            }
        }
        """;

    [TestMethod]
    public async Task DistinctPatternKeywords_ValidInstance_IsAccepted()
    {
        CompiledEvaluator evaluator = await GenerateAsync(FalseRejectionSchema, "issue947FalseRejection.json");

        AssertEvaluates(evaluator, "\"hello\"", expected: true, "\"hello\" matches ^[a-z]+$ and is not \"forbidden\", so it must be valid.");
        AssertEvaluates(evaluator, "\"forbidden\"", expected: false, "\"forbidden\" matches the not-pattern, so it must be invalid.");
    }

    [TestMethod]
    public async Task DistinctPatternKeywords_InvalidInstance_IsRejected()
    {
        CompiledEvaluator evaluator = await GenerateAsync(FalseAcceptanceSchema, "issue947FalseAcceptance.json");

        AssertEvaluates(evaluator, "\"24\"", expected: false, "\"24\" does not match [13579]$, so it must be invalid.");
        AssertEvaluates(evaluator, "\"13\"", expected: true, "\"13\" matches both patterns, so it must be valid.");
    }

    [TestMethod]
    public async Task DistinctPatternKeywords_EmitOneCompiledRegexFieldEach()
    {
        CompiledEvaluator evaluator = await GenerateAsync(FalseAcceptanceSchema, "issue947FalseAcceptance.json");

        int fieldCount = CountOccurrences(evaluator.GeneratedCode, "private static readonly System.Text.RegularExpressions.Regex PatternRegex_");
        Assert.AreEqual(2, fieldCount, $"Each distinct pattern must get its own compiled Regex field. Generated code:{System.Environment.NewLine}{evaluator.GeneratedCode}");
    }

    [TestMethod]
    public async Task PatternKeywordsWithCollidingSafeIdentifiers_AreCompiledSeparately()
    {
        CompiledEvaluator evaluator = await GenerateAsync(CollidingIdentifierSchema, "issue947CollidingIdentifiers.json");

        AssertEvaluates(evaluator, "\"a-c\"", expected: true, "\"a-c\" matches ^[ab]-[cd]$ and not ^[ab]\\+[cd]$, so it must be valid.");
        AssertEvaluates(evaluator, "\"a+c\"", expected: false, "\"a+c\" does not match ^[ab]-[cd]$, so it must be invalid.");
    }

    [TestMethod]
    public async Task PatternPropertiesWithCollidingSafeIdentifiers_AreCompiledSeparately()
    {
        CompiledEvaluator evaluator = await GenerateAsync(PatternPropertiesCollisionSchema, "issue947PatternProperties.json");

        AssertEvaluates(evaluator, /*lang=json*/ """{"a.a": "hello"}""", expected: true, "\"a.a\" matches only ^[a.b]+$, and the value is a string, so it must be valid.");
        AssertEvaluates(evaluator, /*lang=json*/ """{"a.a": 5}""", expected: false, "\"a.a\" matches only ^[a.b]+$, and the value is not a string, so it must be invalid.");
    }

    private static void AssertEvaluates(CompiledEvaluator evaluator, string instanceJson, bool expected, string message)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(instanceJson);
        Assert.AreEqual(expected, evaluator.Evaluate(doc.RootElement), message);
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static ValueTask<CompiledEvaluator> GenerateAsync(string schemaText, string virtualFilename)
    {
        return TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
            virtualFilename,
            schemaText,
            "Corvus.Text.Json.EvaluatorTestSuite.Tests.PatternRegexFieldCollision",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: false,
            Assembly.GetExecutingAssembly());
    }
}