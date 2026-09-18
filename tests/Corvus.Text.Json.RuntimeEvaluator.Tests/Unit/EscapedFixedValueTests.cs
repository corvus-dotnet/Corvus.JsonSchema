// <copyright file="EscapedFixedValueTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.RuntimeEvaluator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// String keywords apply to a string's value, however the document holds it (#971). A value bound from the wire (an
/// OpenAPI query, header or path parameter) is held by a <see cref="FixedJsonValueDocument{T}"/> as JSON text, so a
/// character the encoder escapes is stored as its escape. These cases evaluate such values and must agree with the
/// same value in a parsed document.
/// </summary>
[TestClass]
public class EscapedFixedValueTests
{
    [TestMethod]
    public void A_date_time_with_a_numeric_offset_is_valid()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(
            """{"type":"string","format":"date-time"}""", new JsonSchemaEvaluatorOptions { AssertFormat = true });

        Assert.IsTrue(Evaluate(evaluator, "2026-01-01T01:00:00+00:00"));
        Assert.IsTrue(Evaluate(evaluator, "2026-01-01T01:00:00.0000000+00:00"));
        Assert.IsTrue(Evaluate(evaluator, "2026-01-01T01:00:00Z"));
        Assert.IsFalse(Evaluate(evaluator, "not+a+date"));
    }

    [TestMethod]
    public void Length_counts_the_characters_of_the_value_and_not_of_its_escaped_text()
    {
        using JsonSchemaEvaluator atMostOne = JsonSchemaEvaluator.Compile("""{"type":"string","maxLength":1}""");
        Assert.IsTrue(Evaluate(atMostOne, "+"));

        using JsonSchemaEvaluator atLeastTwo = JsonSchemaEvaluator.Compile("""{"type":"string","minLength":2}""");
        Assert.IsFalse(Evaluate(atLeastTwo, "+"));
    }

    [TestMethod]
    public void A_pattern_is_matched_against_the_value_and_not_against_its_escaped_text()
    {
        using JsonSchemaEvaluator noAngleBrackets = JsonSchemaEvaluator.Compile("""{"type":"string","pattern":"^[^<>]*$"}""");
        Assert.IsTrue(Evaluate(noAngleBrackets, "plain"));
        Assert.IsFalse(Evaluate(noAngleBrackets, "<b>"));

        using JsonSchemaEvaluator plusAddressed = JsonSchemaEvaluator.Compile("""{"type":"string","pattern":"^[a-z]+\\+[a-z]+$"}""");
        Assert.IsTrue(Evaluate(plusAddressed, "user+tag"));
    }

    private static bool Evaluate(JsonSchemaEvaluator evaluator, string value)
    {
        using FixedJsonValueDocument<JsonElement> document = FixedJsonValueDocument<JsonElement>.ForUnescapedString(value.AsSpan());
        return evaluator.Evaluate(document, 0);
    }
}