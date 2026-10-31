// <copyright file="FormatRegexTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// <c>format: regex</c> asserts that a string is an ECMA-262 regular expression in the grammar of the u flag, of the
/// edition the dialect reads patterns with.
/// </summary>
[TestClass]
public class FormatRegexTests
{
    [TestMethod]
    [DataRow("https://json-schema.org/draft/2020-12/schema", false)]
    [DataRow("https://json-schema.org/draft/2019-09/schema", false)]
    [DataRow("http://json-schema.org/draft-07/schema#", true)]
    public void AModifierGroupIsARegexUnderTheDraftsThatReadTodaysGrammar(string dialect, bool valid)
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(Schema(dialect), new JsonSchemaEvaluatorOptions { AssertFormat = true });
        Assert.AreEqual(valid, evaluator.Evaluate("\"(?i:ab)c\""));
        Assert.IsTrue(evaluator.Evaluate("\"^[a-z]+$\""));
        Assert.IsFalse(evaluator.Evaluate("\"^(abc]\""));
    }

    [TestMethod]
    public void TheEditionOptionSetsTheGrammarForEveryDialect()
    {
        var options = new JsonSchemaEvaluatorOptions { AssertFormat = true, RegexEdition = Corvus.Ecma262.RegularExpressions.RegexEdition.Current };
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(Schema("https://json-schema.org/draft/2020-12/schema"), options);
        Assert.IsTrue(evaluator.Evaluate("\"(?i:ab)c\""));
    }

    [TestMethod]
    public void APatternValidOnlyWithoutTheUFlagIsNotARegex()
    {
        // The identity escapes \- and \a are valid only in the grammar of no flag, which a pattern of a schema falls
        // back to and this format does not (the JSON Schema Test Suite's \a is not a regex).
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(Schema("https://json-schema.org/draft/2020-12/schema"), new JsonSchemaEvaluatorOptions { AssertFormat = true });
        Assert.IsFalse(evaluator.Evaluate("\"^x\\\\-y$\""));
        Assert.IsFalse(evaluator.Evaluate("\"\\\\a\""));
        Assert.IsFalse(evaluator.Evaluate("\"a{2,1}\""));

        using JsonSchemaEvaluator pattern = JsonSchemaEvaluator.Compile("{\"pattern\": \"^x\\\\-y$\"}");
        Assert.IsTrue(pattern.Evaluate("\"x-y\""));
    }

    [TestMethod]
    public void APatternOverTheEnginesLimitsIsARegex()
    {
        // The format is the grammar alone. Nested past the 200 groups the engine compiles, the same pattern in a
        // schema's pattern keyword is refused as too large.
        string nested = new string('(', 201) + "a" + new string(')', 201);
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(Schema("https://json-schema.org/draft/2020-12/schema"), new JsonSchemaEvaluatorOptions { AssertFormat = true });
        Assert.IsTrue(evaluator.Evaluate("\"" + nested + "\""));
        Assert.ThrowsExactly<JsonSchemaCompilationException>(() => JsonSchemaEvaluator.Compile("{\"pattern\": \"" + nested + "\"}"));
    }

    [TestMethod]
    public void FormatRegexAndPatternEvaluationAllocateNothing()
    {
        // format: regex reads each value with the reader of the grammar; the first pattern is matched by
        // Corvus.Regex.Ecma262 and the second by a matcher of the evaluator's own.
        const string schema = """
            {
                "$schema": "https://json-schema.org/draft/2020-12/schema",
                "properties": {
                    "re": { "type": "string", "format": "regex" },
                    "library": { "type": "string", "pattern": "^[A-Za-z]{2,}.[A-Za-z]{2,}" },
                    "shape": { "type": "string", "pattern": "^[a-z][a-z0-9]{0,29}$" }
                }
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema, new JsonSchemaEvaluatorOptions { AssertFormat = true });
        using ParsedJsonDocument<JsonElement> valid = ParsedJsonDocument<JsonElement>.Parse("""{"re": "^(?<y>\\d{4})-\\k<y>\\p{L}*$", "library": "ab.cd", "shape": "name1"}""");
        using ParsedJsonDocument<JsonElement> invalid = ParsedJsonDocument<JsonElement>.Parse("""{"re": "(?<y>a)(?<y>b)", "library": "a.b", "shape": "1name"}""");
        ParsedJsonDocument<JsonElement>[] docs = [valid, invalid, valid, invalid];
        Assert.IsTrue(evaluator.Evaluate(valid.RootElement));
        Assert.IsFalse(evaluator.Evaluate(invalid.RootElement));

        // Warm up past tiering, so that the measured calls run the optimised code.
        for (int i = 0; i < 20_000; i++)
        {
            EvaluateAll(evaluator, docs);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        int count = 0;
        for (int i = 0; i < 1000; i++)
        {
            count += EvaluateAll(evaluator, docs);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.AreEqual(2000, count);
        Assert.AreEqual(0L, allocated, "Bytes allocated by 4000 evaluations.");

        // A small method of its own: the evaluator's entry is inlined here, as it is into an application's loop.
        static int EvaluateAll(JsonSchemaEvaluator evaluator, ParsedJsonDocument<JsonElement>[] docs)
        {
            int valid = 0;
            foreach (ParsedJsonDocument<JsonElement> d in docs)
            {
                if (evaluator.Evaluate(d.RootElement))
                {
                    valid++;
                }
            }

            return valid;
        }
    }

    private static string Schema(string dialect) => "{\"$schema\": \"" + dialect + "\", \"type\": \"string\", \"format\": \"regex\"}";
}
