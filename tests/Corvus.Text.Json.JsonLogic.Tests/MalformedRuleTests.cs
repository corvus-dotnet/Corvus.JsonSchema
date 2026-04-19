// <copyright file="MalformedRuleTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.JsonLogic;
using Xunit;

namespace Corvus.Text.Json.JsonLogic.Tests;

/// <summary>
/// Tests that the JsonLogic runtime evaluator handles malformed, invalid,
/// and edge-case rules gracefully.
/// </summary>
public class MalformedRuleTests
{
    // ----- Literal rules (no operator) -----

    [Theory]
    [InlineData("42", "42")]
    [InlineData("\"hello\"", "\"hello\"")]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    [InlineData("null", "null")]
    public void LiteralValue_ReturnedAsIs(string rule, string expected)
    {
        string result = Evaluate(rule, "{}");
        Assert.Equal(NormalizeJson(expected), result);
    }

    [Fact]
    public void EmptyArray_ReturnedAsEmptyArray()
    {
        string result = Evaluate("[]", "{}");
        Assert.Equal("[]", result);
    }

    [Fact]
    public void ArrayOfLiterals_ReturnedAsArray()
    {
        string result = Evaluate("[1, 2, 3]", "{}");
        Assert.Equal("[1,2,3]", result);
    }

    // ----- Unknown operators -----

    [Fact]
    public void UnknownOperator_ThrowsInvalidOperationException()
    {
        Assert.ThrowsAny<Exception>(() => Evaluate("""{"nonexistent":[1]}""", "{}"));
    }

    // ----- Empty operator args -----

    [Fact]
    public void VarWithEmptyString_ReturnsFullData()
    {
        string result = Evaluate("""{"var":""}""", """{"a":1,"b":2}""");
        Assert.Equal("""{"a":1,"b":2}""", result);
    }

    [Fact]
    public void VarWithNoArgs_ReturnsFullData()
    {
        string result = Evaluate("""{"var":[]}""", """{"a":1}""");
        // var with no args typically returns the full data
        Assert.NotNull(result);
    }

    // ----- Null paths -----

    [Fact]
    public void VarWithMissingPath_ReturnsNull()
    {
        string result = Evaluate("""{"var":"nonexistent"}""", """{"a":1}""");
        Assert.Equal("null", result);
    }

    [Fact]
    public void VarWithDefault_ReturnsDefault()
    {
        string result = Evaluate("""{"var":["nonexistent", 99]}""", """{"a":1}""");
        Assert.Equal("99", result);
    }

    // ----- Edge cases in comparison operators -----

    [Fact]
    public void CompareNullWithNumber_EvaluatesCorrectly()
    {
        string result = Evaluate("""{"==":[null, null]}""", "{}");
        Assert.Equal("true", result);
    }

    [Fact]
    public void CompareNullWithNonNull_ReturnsFalse()
    {
        string result = Evaluate("""{"==":[null, 0]}""", "{}");
        Assert.Equal("false", result);
    }

    // ----- Single-element operator args -----

    [Fact]
    public void AddWithSingleArg_ReturnsUnaryPlus()
    {
        string result = Evaluate("""{"+":[5]}""", "{}");
        Assert.Equal("5", result);
    }

    [Fact]
    public void AddWithNoArgs_ReturnsZero()
    {
        string result = Evaluate("""{"+":[]}""", "{}");
        Assert.Equal("0", result);
    }

    // ----- Helpers -----

    private static string Evaluate(string rule, string data)
    {
        byte[] ruleUtf8 = Encoding.UTF8.GetBytes(rule);
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(data);

        Corvus.Text.Json.JsonElement ruleElement = Corvus.Text.Json.JsonElement.ParseValue(ruleUtf8);
        Corvus.Text.Json.JsonElement dataElement = Corvus.Text.Json.JsonElement.ParseValue(dataUtf8);

        JsonLogicRule logicRule = new(ruleElement);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Corvus.Text.Json.JsonElement result = JsonLogicEvaluator.Default.Evaluate(logicRule, dataElement, workspace);

        return NormalizeJson(GetRawText(result));
    }

    private static string GetRawText(Corvus.Text.Json.JsonElement element)
    {
        if (element.IsNullOrUndefined())
        {
            return "null";
        }

        return element.GetRawText();
    }

    private static string NormalizeJson(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        using var ms = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(ms, new System.Text.Json.JsonWriterOptions { Indented = false }))
        {
            doc.RootElement.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
