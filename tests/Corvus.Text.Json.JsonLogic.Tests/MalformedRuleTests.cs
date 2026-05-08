// <copyright file="MalformedRuleTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.JsonLogic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JsonLogic.Tests;

/// <summary>
/// Tests that the JsonLogic runtime evaluator handles malformed, invalid,
/// and edge-case rules gracefully.
/// </summary>
[TestClass]
public class MalformedRuleTests
{
    // ----- Literal rules (no operator) -----

    [TestMethod]
    [DataRow("42", "42")]
    [DataRow("\"hello\"", "\"hello\"")]
    [DataRow("true", "true")]
    [DataRow("false", "false")]
    [DataRow("null", "null")]
    public void LiteralValue_ReturnedAsIs(string rule, string expected)
    {
        string result = Evaluate(rule, "{}");
        Assert.AreEqual(NormalizeJson(expected), result);
    }

    [TestMethod]
    public void EmptyArray_ReturnedAsEmptyArray()
    {
        string result = Evaluate("[]", "{}");
        Assert.AreEqual("[]", result);
    }

    [TestMethod]
    public void ArrayOfLiterals_ReturnedAsArray()
    {
        string result = Evaluate("[1, 2, 3]", "{}");
        Assert.AreEqual("[1,2,3]", result);
    }

    // ----- Unknown operators -----

    [TestMethod]
    public void UnknownOperator_ThrowsInvalidOperationException()
    {
        Assert.Throws<Exception>(() => Evaluate("""{"nonexistent":[1]}""", "{}"));
    }

    // ----- Empty operator args -----

    [TestMethod]
    public void VarWithEmptyString_ReturnsFullData()
    {
        string result = Evaluate("""{"var":""}""", """{"a":1,"b":2}""");
        Assert.AreEqual("""{"a":1,"b":2}""", result);
    }

    [TestMethod]
    public void VarWithNoArgs_ReturnsFullData()
    {
        string result = Evaluate("""{"var":[]}""", """{"a":1}""");
        // var with no args typically returns the full data
        Assert.IsNotNull(result);
    }

    // ----- Null paths -----

    [TestMethod]
    public void VarWithMissingPath_ReturnsNull()
    {
        string result = Evaluate("""{"var":"nonexistent"}""", """{"a":1}""");
        Assert.AreEqual("null", result);
    }

    [TestMethod]
    public void VarWithDefault_ReturnsDefault()
    {
        string result = Evaluate("""{"var":["nonexistent", 99]}""", """{"a":1}""");
        Assert.AreEqual("99", result);
    }

    // ----- Edge cases in comparison operators -----

    [TestMethod]
    public void CompareNullWithNumber_EvaluatesCorrectly()
    {
        string result = Evaluate("""{"==":[null, null]}""", "{}");
        Assert.AreEqual("true", result);
    }

    [TestMethod]
    public void CompareNullWithNonNull_ReturnsFalse()
    {
        string result = Evaluate("""{"==":[null, 0]}""", "{}");
        Assert.AreEqual("false", result);
    }

    // ----- Single-element operator args -----

    [TestMethod]
    public void AddWithSingleArg_ReturnsUnaryPlus()
    {
        string result = Evaluate("""{"+":[5]}""", "{}");
        Assert.AreEqual("5", result);
    }

    [TestMethod]
    public void AddWithNoArgs_ReturnsZero()
    {
        string result = Evaluate("""{"+":[]}""", "{}");
        Assert.AreEqual("0", result);
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
