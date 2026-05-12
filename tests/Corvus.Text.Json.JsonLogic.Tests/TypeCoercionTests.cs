// <copyright file="TypeCoercionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.JsonLogic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JsonLogic.Tests;

/// <summary>
/// Tests for JsonLogic type coercion in arithmetic, comparison, equality,
/// truthiness, and string operators.
/// </summary>
[TestClass]
public class TypeCoercionTests
{
    // ─── Arithmetic coercion ─────────────────────────────────────────

    [TestMethod]
    [DataRow("""{"+":[1,"2"]}""", "3")]
    [DataRow("""{"+":[1,true]}""", "2")]
    [DataRow("""{"+":[1,false]}""", "1")]
    [DataRow("""{"+":[1,null]}""", "1")]
    [DataRow("""{"*":["3","4"]}""", "12")]
    [DataRow("""{"-":["10","3"]}""", "7")]
    [DataRow("""{"%":[10,3]}""", "1")]
    public void Arithmetic_CoercesTypes(string rule, string expected)
    {
        string result = Evaluate(rule, "{}");
        Assert.AreEqual(NormalizeJson(expected), result);
    }

    [TestMethod]
    public void Division_ProducesDouble()
    {
        string result = Evaluate("""{"/":[10,3]}""", "{}");

        // 10/3 ≈ 3.333… — the exact representation depends on serializer rounding.
        double actual = double.Parse(result, System.Globalization.CultureInfo.InvariantCulture);
        Assert.IsTrue(actual > 3.33 && actual < 3.34, $"Expected ~3.333 but got {actual}");
    }

    // ─── Comparison coercion ─────────────────────────────────────────

    [TestMethod]
    public void LessThan_StringVsNumber_ComparesNumerically()
    {
        string result = Evaluate("""{"<":["2",11]}""", "{}");
        Assert.AreEqual("true", result);
    }

    // ─── Equality ────────────────────────────────────────────────────

    [TestMethod]
    [DataRow("""{"==":["1",1]}""", "true")]
    [DataRow("""{"===":["1",1]}""", "false")]
    public void Equality_LooseVsStrict(string rule, string expected)
    {
        string result = Evaluate(rule, "{}");
        Assert.AreEqual(expected, result);
    }

    // ─── Truthiness (!! and !) ───────────────────────────────────────

    [TestMethod]
    [DataRow("""{"!!":[0]}""", "false")]
    [DataRow("""{"!!":[""]}""", "false")]
    [DataRow("""{"!!":[1]}""", "true")]
    [DataRow("""{"!":[[1,2,3]]}""", "false")]
    [DataRow("""{"!":[[]]}""", "true")]
    public void Truthiness_CoercesCorrectly(string rule, string expected)
    {
        string result = Evaluate(rule, "{}");
        Assert.AreEqual(expected, result);
    }

    // ─── String operators ────────────────────────────────────────────

    [TestMethod]
    [DataRow("""{"cat":["hello"," ","world"]}""", "\"hello world\"")]
    [DataRow("""{"cat":[1,2]}""", "\"12\"")]
    public void Cat_CoercesAndConcatenates(string rule, string expected)
    {
        string result = Evaluate(rule, "{}");
        Assert.AreEqual(NormalizeJson(expected), result);
    }

    [TestMethod]
    [DataRow("""{"substr":["hello world",6]}""", "\"world\"")]
    [DataRow("""{"substr":["hello world",0,5]}""", "\"hello\"")]
    [DataRow("""{"substr":["hello",-3]}""", "\"llo\"")]
    public void Substr_SlicesCorrectly(string rule, string expected)
    {
        string result = Evaluate(rule, "{}");
        Assert.AreEqual(NormalizeJson(expected), result);
    }

    // ─── Helpers ─────────────────────────────────────────────────────

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
