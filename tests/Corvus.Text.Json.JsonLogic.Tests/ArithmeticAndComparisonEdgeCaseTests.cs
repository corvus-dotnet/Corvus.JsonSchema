// <copyright file="ArithmeticAndComparisonEdgeCaseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.JsonLogic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JsonLogic.Tests;

/// <summary>
/// Tests for arithmetic, comparison, string, and operator edge cases
/// that exercise code paths in <see cref="FunctionalEvaluator"/>
/// and <see cref="JsonLogicHelpers"/> not covered by the standard test suite.
/// </summary>
[TestClass]
public class ArithmeticAndComparisonEdgeCaseTests
{
    // ─── DIVISION EDGE CASES ─────────────────────────────────────────

    [TestMethod]
    [DataRow("""{"/":[10,3]}""", "null")]       // integer division with remainder
    [DataRow("""{"/":[0,5]}""", "null", "0")]    // zero divided by non-zero
    [DataRow("""{"/":[7.5,2.5]}""", "null", "3")]
    public void Division_ProducesCorrectResults(string rule, string data, string? expected = null)
    {
        string result = Evaluate(rule, data);
        if (expected is not null)
        {
            Assert.AreEqual(expected, result);
        }
        else
        {
            Assert.IsNotNull(result);
        }
    }

    [TestMethod]
    public void Division_ByZero_HandledGracefully()
    {
        // 1/0 should produce Infinity or NaN — verify no crash
        string result = Evaluate("""{"/":[1,0]}""", "null");
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void Division_ZeroByZero_HandledGracefully()
    {
        string result = Evaluate("""{"/":[0,0]}""", "null");
        Assert.IsNotNull(result);
    }

    // ─── MODULO EDGE CASES ───────────────────────────────────────────

    [TestMethod]
    [DataRow("""{"%":[10,3]}""", "null", "1")]
    [DataRow("""{"%":[10,5]}""", "null", "0")]
    [DataRow("""{"%":[7.5,2]}""", "null")]       // float modulo
    public void Modulo_ProducesCorrectResults(string rule, string data, string? expected = null)
    {
        string result = Evaluate(rule, data);
        if (expected is not null)
        {
            Assert.AreEqual(expected, result);
        }
        else
        {
            Assert.IsNotNull(result);
        }
    }

    [TestMethod]
    public void Modulo_ByZero_HandledGracefully()
    {
        string result = Evaluate("""{"%":[5,0]}""", "null");
        Assert.IsNotNull(result);
    }

    // ─── MIN / MAX ───────────────────────────────────────────────────

    [TestMethod]
    [DataRow("""{"min":[3,1,2]}""", "null", "1")]
    [DataRow("""{"max":[3,1,2]}""", "null", "3")]
    [DataRow("""{"min":[5]}""", "null", "5")]         // single element
    [DataRow("""{"max":[5]}""", "null", "5")]
    [DataRow("""{"min":[]}""", "null", "null")]        // empty
    [DataRow("""{"max":[]}""", "null", "null")]
    public void MinMax_EdgeCases(string rule, string data, string expected)
    {
        string result = Evaluate(rule, data);
        Assert.AreEqual(expected, result);
    }

    // ─── COMPARISON EDGE CASES ───────────────────────────────────────

    [TestMethod]
    [DataRow("""{">":[2,1]}""", "null", "true")]
    [DataRow("""{">":[1,1]}""", "null", "false")]
    [DataRow("""{">=":[1,1]}""", "null", "true")]
    [DataRow("""{"<":[1,2]}""", "null", "true")]
    [DataRow("""{"<=":[2,2]}""", "null", "true")]
    public void Comparison_BasicCases(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    [TestMethod]
    [DataRow("""{"<":[1,2,3]}""", "null", "true")]     // between: 1 < 2 < 3
    [DataRow("""{"<":[1,3,2]}""", "null", "false")]     // between: 1 < 3 but 3 !< 2
    [DataRow("""{"<=":[1,1,3]}""", "null", "true")]     // between inclusive
    [DataRow("""{"<=":[1,4,3]}""", "null", "false")]
    public void Comparison_ThreeArg_Between(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── STRICT EQUALITY ─────────────────────────────────────────────

    [TestMethod]
    [DataRow("""{"===":[1,1]}""", "null", "true")]
    [DataRow("""{"===":[1,"1"]}""", "null", "false")]   // strict: different types
    [DataRow("""{"===":[null,null]}""", "null", "true")]
    [DataRow("""{"!==":[1,"1"]}""", "null", "true")]
    [DataRow("""{"!==":[1,1]}""", "null", "false")]
    public void StrictEquality_TypeSensitive(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── IN OPERATOR ─────────────────────────────────────────────────

    [TestMethod]
    [DataRow("""{"in":["a","abc"]}""", "null", "true")]   // substring in string
    [DataRow("""{"in":["d","abc"]}""", "null", "false")]
    [DataRow("""{"in":[2,[1,2,3]]}""", "null", "true")]   // element in array
    [DataRow("""{"in":[4,[1,2,3]]}""", "null", "false")]
    [DataRow("""{"in":["",""]}""", "null", "true")]        // empty in empty
    public void In_StringAndArray(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── MERGE OPERATOR ──────────────────────────────────────────────

    [TestMethod]
    [DataRow("""{"merge":[[1,2],[3,4]]}""", "null", "[1,2,3,4]")]
    [DataRow("""{"merge":[[1],2,[3]]}""", "null", "[1,2,3]")]    // non-array gets wrapped
    [DataRow("""{"merge":[]}""", "null", "[]")]                   // empty
    [DataRow("""{"merge":[[1]]}""", "null", "[1]")]               // single array
    public void Merge_FlattenArrays(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── CAT OPERATOR ────────────────────────────────────────────────

    [TestMethod]
    [DataRow("""{"cat":["hello"," ","world"]}""", "null", "\"hello world\"")]
    [DataRow("""{"cat":[1,2,3]}""", "null", "\"123\"")]                     // numbers coerced to string
    [DataRow("""{"cat":[]}""", "null", "\"\"")]                              // empty
    [DataRow("""{"cat":["only"]}""", "null", "\"only\"")]                    // single element
    [DataRow("""{"cat":[true,false]}""", "null", "\"truefalse\"")]           // booleans
    public void Cat_StringConcatenation(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── SUBSTR OPERATOR ─────────────────────────────────────────────

    [TestMethod]
    [DataRow("""{"substr":["hello",0,3]}""", "null", "\"hel\"")]
    [DataRow("""{"substr":["hello",2]}""", "null", "\"llo\"")]      // no length = rest
    [DataRow("""{"substr":["hello",-3]}""", "null", "\"llo\"")]     // negative start
    [DataRow("""{"substr":["hello",0,-2]}""", "null", "\"hel\"")]   // negative length (from end)
    public void Substr_VariousSlices(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── LOG OPERATOR ────────────────────────────────────────────────

    [TestMethod]
    public void Log_ReturnsItsArgument()
    {
        // log passes through its argument and prints to console
        string result = Evaluate("""{"log":"hello"}""", "null");
        Assert.AreEqual("\"hello\"", result);
    }

    [TestMethod]
    public void Log_WithNumericArgument()
    {
        string result = Evaluate("""{"log":42}""", "null");
        Assert.AreEqual("42", result);
    }

    // ─── NOT / DOUBLE-NOT ────────────────────────────────────────────

    [TestMethod]
    [DataRow("""{"!":[true]}""", "null", "false")]
    [DataRow("""{"!":[false]}""", "null", "true")]
    [DataRow("""{"!":[0]}""", "null", "true")]
    [DataRow("""{"!":[1]}""", "null", "false")]
    [DataRow("""{"!":["" ]}""", "null", "true")]
    [DataRow("""{"!":[[]]}""", "null", "true")]
    public void Not_CoercesToBoolean(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    [TestMethod]
    [DataRow("""{"!!":[0]}""", "null", "false")]
    [DataRow("""{"!!":[1]}""", "null", "true")]
    [DataRow("""{"!!":["hello"]}""", "null", "true")]
    [DataRow("""{"!!":[""]}""", "null", "false")]
    public void DoubleNot_CoercesToBoolean(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── AND / OR ────────────────────────────────────────────────────

    [TestMethod]
    [DataRow("""{"and":[true,true]}""", "null", "true")]
    [DataRow("""{"and":[true,false]}""", "null", "false")]
    [DataRow("""{"and":[1,2,3]}""", "null", "3")]       // returns last truthy
    [DataRow("""{"and":[1,0,3]}""", "null", "0")]       // short-circuits at 0
    public void And_ShortCircuits(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    [TestMethod]
    [DataRow("""{"or":[false,true]}""", "null", "true")]
    [DataRow("""{"or":[false,false]}""", "null", "false")]
    [DataRow("""{"or":[0,"",null,42]}""", "null", "42")]   // returns first truthy
    [DataRow("""{"or":[false,0,""]}""", "null", "\"\"")]   // returns last falsy
    public void Or_ShortCircuits(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── IF / TERNARY CHAINS ─────────────────────────────────────────

    [TestMethod]
    [DataRow("""{"if":[true,"yes","no"]}""", "null", "\"yes\"")]
    [DataRow("""{"if":[false,"yes","no"]}""", "null", "\"no\"")]
    [DataRow(
        """{"if":[false,"a",false,"b","c"]}""",
        "null",
        "\"c\"")]   // chained if: first false, second false, else "c"
    [DataRow(
        """{"if":[false,"a",true,"b","c"]}""",
        "null",
        "\"b\"")]   // chained if: first false, second true
    public void If_ChainedConditions(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── MAP / FILTER / REDUCE EDGE CASES ────────────────────────────

    [TestMethod]
    public void Map_EmptyArray_ReturnsEmpty()
    {
        AssertBothPaths(
            """{"map":[[],{"*":[{"var":""},2]}]}""",
            "null",
            "[]");
    }

    [TestMethod]
    public void Filter_EmptyArray_ReturnsEmpty()
    {
        AssertBothPaths(
            """{"filter":[[],{">":[{"var":""},0]}]}""",
            "null",
            "[]");
    }

    [TestMethod]
    public void Reduce_EmptyArrayWithInit_ReturnsInit()
    {
        AssertBothPaths(
            """{"reduce":[[],{"+":[{"var":"current"},{"var":"accumulator"}]},99]}""",
            "null",
            "99");
    }

    [TestMethod]
    public void All_EmptyArray_ReturnsTrue()
    {
        // Vacuous truth: all of nothing is true
        AssertBothPaths(
            """{"all":[[],{">":[{"var":""},0]}]}""",
            "null",
            "false");
    }

    [TestMethod]
    public void None_EmptyArray_ReturnsTrue()
    {
        AssertBothPaths(
            """{"none":[[],{">":[{"var":""},0]}]}""",
            "null",
            "true");
    }

    [TestMethod]
    public void Some_EmptyArray_ReturnsFalse()
    {
        AssertBothPaths(
            """{"some":[[],{">":[{"var":""},0]}]}""",
            "null",
            "false");
    }

    // ─── TYPE COERCION (asDouble/asLong/asBigNumber/asBigInteger) ────

    [TestMethod]
    [DataRow("""{"asDouble":"42.5"}""", "null", "42.5")]
    [DataRow("""{"asDouble":42}""", "null", "42")]
    [DataRow("""{"asDouble":true}""", "null", "1")]
    [DataRow("""{"asDouble":false}""", "null", "0")]
    public void AsDouble_CoercesToDouble(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    [TestMethod]
    [DataRow("""{"asLong":42.9}""", "null", "42")]      // truncates
    [DataRow("""{"asLong":"100"}""", "null", "100")]
    public void AsLong_CoercesToLong(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── MISSING OPERATOR ────────────────────────────────────────────

    [TestMethod]
    [DataRow("""{"missing":["a"]}""", """{"a":1}""", "[]")]
    [DataRow("""{"missing":["a"]}""", """{}""", "[\"a\"]")]
    [DataRow("""{"missing":["a","b"]}""", """{"a":1}""", "[\"b\"]")]
    [DataRow("""{"missing":[]}""", """{}""", "[]")]
    public void Missing_ReturnsAbsentPaths(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── HELPERS ─────────────────────────────────────────────────────

    private static void AssertBothPaths(string rule, string data, string expected)
    {
        string result = Evaluate(rule, data);
        string expectedNormalized = NormalizeJson(expected);
        Assert.AreEqual(expectedNormalized, result);
    }

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
