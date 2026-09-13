// <copyright file="StringLeafTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// The string keywords on the flag-mode leaf path: length bounds decided from the byte length where a rune count
/// cannot change the answer, and counted otherwise; escaped values unescaped before every test; the same result in
/// every mode and through the image.
/// </summary>
[TestClass]
public class StringLeafTests
{
    private static void AssertAll(string schema, string instance, bool expected)
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(evaluator.ToProgramImage());
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(instance);
        Assert.AreEqual(expected, evaluator.Evaluate(doc.RootElement), $"flag {schema} {instance}");
        Assert.AreEqual(expected, loaded.Evaluate(doc.RootElement), $"image {schema} {instance}");
        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);
        Assert.AreEqual(expected, evaluator.Evaluate(doc.RootElement, collector), $"collecting {schema} {instance}");

        // The same value as a property, so the leaf is reached from the object plan.
        using JsonSchemaEvaluator wrapped = JsonSchemaEvaluator.Compile("""{"properties": {"v": """ + schema + "}}");
        Assert.AreEqual(expected, wrapped.Evaluate("""{"v": """ + instance + "}"), $"property {schema} {instance}");
    }

    [TestMethod]
    [DataRow("""{"minLength": 2}""", "\"ab\"", true)]
    [DataRow("""{"minLength": 2}""", "\"a\"", false)]
    [DataRow("""{"minLength": 2}""", "\"é\"", false, DisplayName = "two bytes, one rune")]
    [DataRow("""{"minLength": 2}""", "\"éé\"", true)]
    [DataRow("""{"minLength": 2}""", "\"\\u00e9\\u00e9\"", true, DisplayName = "escaped runes are counted after unescaping")]
    [DataRow("""{"minLength": 2}""", "\"\\u00e9\"", false)]
    [DataRow("""{"minLength": 1}""", "\"\"", false)]
    [DataRow("""{"minLength": 1}""", "\"😀\"", true, DisplayName = "four bytes, one rune")]
    [DataRow("""{"minLength": 2}""", "\"😀\"", false)]
    [DataRow("""{"minLength": 2}""", "\"😀😀\"", true, DisplayName = "eight bytes decide without counting")]
    [DataRow("""{"maxLength": 2}""", "\"ab\"", true)]
    [DataRow("""{"maxLength": 2}""", "\"abc\"", false)]
    [DataRow("""{"maxLength": 2}""", "\"éé\"", true, DisplayName = "four bytes, two runes")]
    [DataRow("""{"maxLength": 2}""", "\"ééé\"", false)]
    [DataRow("""{"maxLength": 2}""", "\"😀😀\"", true)]
    [DataRow("""{"maxLength": 2}""", "\"😀😀😀\"", false, DisplayName = "twelve bytes fail without counting")]
    [DataRow("""{"maxLength": 1}""", "\"\\uD83D\\uDE00\"", true, DisplayName = "escaped surrogate pair is one rune")]
    [DataRow("""{"maxLength": 0}""", "\"\"", true)]
    [DataRow("""{"maxLength": 0}""", "\"a\"", false)]
    [DataRow("""{"minLength": 2, "maxLength": 3}""", "\"aé\"", true)]
    [DataRow("""{"minLength": 2, "maxLength": 3}""", "\"aééé\"", false)]
    [DataRow("""{"minLength": 3, "pattern": "^a"}""", "\"a\\u00e9\\u00e9\"", true)]
    [DataRow("""{"minLength": 3, "pattern": "^a"}""", "\"\\u00e9a\\u00e9\"", false)]
    [DataRow("""{"pattern": "^[a-z]+$"}""", "\"\\u0061bc\"", true, DisplayName = "the pattern sees the unescaped text")]
    [DataRow("""{"pattern": "^[a-z]+$"}""", "\"ab\\n\"", false)]
    [DataRow("""{"type": "string", "minLength": 1}""", "1", false)]
    [DataRow("""{"minLength": 1}""", "1", true, DisplayName = "string keywords ignore non-strings")]
    public void StringKeywordsAgreeInEveryMode(string schema, string instance, bool expected)
    {
        AssertAll(schema, instance, expected);
    }

    [TestMethod]
    public void FormatAssertionUsesTheUnescapedValue()
    {
        var options = new JsonSchemaEvaluatorOptions { AssertFormat = true };
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile("""{"format": "date"}""", options);
        Assert.IsTrue(evaluator.Evaluate("\"2024-01-31\""));
        Assert.IsTrue(evaluator.Evaluate("\"2024\\u002d01-31\""));
        Assert.IsFalse(evaluator.Evaluate("\"2024-13-31\""));
        Assert.IsFalse(evaluator.Evaluate("\"2024\\u002d13-31\""));
    }
}
