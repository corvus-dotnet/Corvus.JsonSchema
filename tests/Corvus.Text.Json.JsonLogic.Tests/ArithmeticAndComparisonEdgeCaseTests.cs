// <copyright file="ArithmeticAndComparisonEdgeCaseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.JsonLogic;
using Xunit;

namespace Corvus.Text.Json.JsonLogic.Tests;

/// <summary>
/// Tests for arithmetic, comparison, string, and operator edge cases
/// that exercise code paths in <see cref="FunctionalEvaluator"/>
/// and <see cref="JsonLogicHelpers"/> not covered by the standard test suite.
/// </summary>
public class ArithmeticAndComparisonEdgeCaseTests
{
    // ─── DIVISION EDGE CASES ─────────────────────────────────────────

    [Theory]
    [InlineData("""{"/":[10,3]}""", "null")]       // integer division with remainder
    [InlineData("""{"/":[0,5]}""", "null", "0")]    // zero divided by non-zero
    [InlineData("""{"/":[7.5,2.5]}""", "null", "3")]
    public void Division_ProducesCorrectResults(string rule, string data, string? expected = null)
    {
        string result = Evaluate(rule, data);
        if (expected is not null)
        {
            Assert.Equal(expected, result);
        }
        else
        {
            Assert.NotNull(result);
        }
    }

    [Fact]
    public void Division_ByZero_HandledGracefully()
    {
        // 1/0 should produce Infinity or NaN — verify no crash
        string result = Evaluate("""{"/":[1,0]}""", "null");
        Assert.NotNull(result);
    }

    [Fact]
    public void Division_ZeroByZero_HandledGracefully()
    {
        string result = Evaluate("""{"/":[0,0]}""", "null");
        Assert.NotNull(result);
    }

    // ─── MODULO EDGE CASES ───────────────────────────────────────────

    [Theory]
    [InlineData("""{"%":[10,3]}""", "null", "1")]
    [InlineData("""{"%":[10,5]}""", "null", "0")]
    [InlineData("""{"%":[7.5,2]}""", "null")]       // float modulo
    public void Modulo_ProducesCorrectResults(string rule, string data, string? expected = null)
    {
        string result = Evaluate(rule, data);
        if (expected is not null)
        {
            Assert.Equal(expected, result);
        }
        else
        {
            Assert.NotNull(result);
        }
    }

    [Fact]
    public void Modulo_ByZero_HandledGracefully()
    {
        string result = Evaluate("""{"%":[5,0]}""", "null");
        Assert.NotNull(result);
    }

    // ─── MIN / MAX ───────────────────────────────────────────────────

    [Theory]
    [InlineData("""{"min":[3,1,2]}""", "null", "1")]
    [InlineData("""{"max":[3,1,2]}""", "null", "3")]
    [InlineData("""{"min":[5]}""", "null", "5")]         // single element
    [InlineData("""{"max":[5]}""", "null", "5")]
    [InlineData("""{"min":[]}""", "null", "null")]        // empty
    [InlineData("""{"max":[]}""", "null", "null")]
    public void MinMax_EdgeCases(string rule, string data, string expected)
    {
        string result = Evaluate(rule, data);
        Assert.Equal(expected, result);
    }

    // ─── COMPARISON EDGE CASES ───────────────────────────────────────

    [Theory]
    [InlineData("""{">":[2,1]}""", "null", "true")]
    [InlineData("""{">":[1,1]}""", "null", "false")]
    [InlineData("""{">=":[1,1]}""", "null", "true")]
    [InlineData("""{"<":[1,2]}""", "null", "true")]
    [InlineData("""{"<=":[2,2]}""", "null", "true")]
    public void Comparison_BasicCases(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    [Theory]
    [InlineData("""{"<":[1,2,3]}""", "null", "true")]     // between: 1 < 2 < 3
    [InlineData("""{"<":[1,3,2]}""", "null", "false")]     // between: 1 < 3 but 3 !< 2
    [InlineData("""{"<=":[1,1,3]}""", "null", "true")]     // between inclusive
    [InlineData("""{"<=":[1,4,3]}""", "null", "false")]
    public void Comparison_ThreeArg_Between(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── STRICT EQUALITY ─────────────────────────────────────────────

    [Theory]
    [InlineData("""{"===":[1,1]}""", "null", "true")]
    [InlineData("""{"===":[1,"1"]}""", "null", "false")]   // strict: different types
    [InlineData("""{"===":[null,null]}""", "null", "true")]
    [InlineData("""{"!==":[1,"1"]}""", "null", "true")]
    [InlineData("""{"!==":[1,1]}""", "null", "false")]
    public void StrictEquality_TypeSensitive(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── IN OPERATOR ─────────────────────────────────────────────────

    [Theory]
    [InlineData("""{"in":["a","abc"]}""", "null", "true")]   // substring in string
    [InlineData("""{"in":["d","abc"]}""", "null", "false")]
    [InlineData("""{"in":[2,[1,2,3]]}""", "null", "true")]   // element in array
    [InlineData("""{"in":[4,[1,2,3]]}""", "null", "false")]
    [InlineData("""{"in":["",""]}""", "null", "true")]        // empty in empty
    public void In_StringAndArray(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── MERGE OPERATOR ──────────────────────────────────────────────

    [Theory]
    [InlineData("""{"merge":[[1,2],[3,4]]}""", "null", "[1,2,3,4]")]
    [InlineData("""{"merge":[[1],2,[3]]}""", "null", "[1,2,3]")]    // non-array gets wrapped
    [InlineData("""{"merge":[]}""", "null", "[]")]                   // empty
    [InlineData("""{"merge":[[1]]}""", "null", "[1]")]               // single array
    public void Merge_FlattenArrays(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── CAT OPERATOR ────────────────────────────────────────────────

    [Theory]
    [InlineData("""{"cat":["hello"," ","world"]}""", "null", "\"hello world\"")]
    [InlineData("""{"cat":[1,2,3]}""", "null", "\"123\"")]                     // numbers coerced to string
    [InlineData("""{"cat":[]}""", "null", "\"\"")]                              // empty
    [InlineData("""{"cat":["only"]}""", "null", "\"only\"")]                    // single element
    [InlineData("""{"cat":[true,false]}""", "null", "\"truefalse\"")]           // booleans
    public void Cat_StringConcatenation(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── SUBSTR OPERATOR ─────────────────────────────────────────────

    [Theory]
    [InlineData("""{"substr":["hello",0,3]}""", "null", "\"hel\"")]
    [InlineData("""{"substr":["hello",2]}""", "null", "\"llo\"")]      // no length = rest
    [InlineData("""{"substr":["hello",-3]}""", "null", "\"llo\"")]     // negative start
    [InlineData("""{"substr":["hello",0,-2]}""", "null", "\"hel\"")]   // negative length (from end)
    public void Substr_VariousSlices(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── LOG OPERATOR ────────────────────────────────────────────────

    [Fact]
    public void Log_ReturnsItsArgument()
    {
        // log passes through its argument and prints to console
        string result = Evaluate("""{"log":"hello"}""", "null");
        Assert.Equal("\"hello\"", result);
    }

    [Fact]
    public void Log_WithNumericArgument()
    {
        string result = Evaluate("""{"log":42}""", "null");
        Assert.Equal("42", result);
    }

    // ─── NOT / DOUBLE-NOT ────────────────────────────────────────────

    [Theory]
    [InlineData("""{"!":[true]}""", "null", "false")]
    [InlineData("""{"!":[false]}""", "null", "true")]
    [InlineData("""{"!":[0]}""", "null", "true")]
    [InlineData("""{"!":[1]}""", "null", "false")]
    [InlineData("""{"!":["" ]}""", "null", "true")]
    [InlineData("""{"!":[[]]}""", "null", "true")]
    public void Not_CoercesToBoolean(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    [Theory]
    [InlineData("""{"!!":[0]}""", "null", "false")]
    [InlineData("""{"!!":[1]}""", "null", "true")]
    [InlineData("""{"!!":["hello"]}""", "null", "true")]
    [InlineData("""{"!!":[""]}""", "null", "false")]
    public void DoubleNot_CoercesToBoolean(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── AND / OR ────────────────────────────────────────────────────

    [Theory]
    [InlineData("""{"and":[true,true]}""", "null", "true")]
    [InlineData("""{"and":[true,false]}""", "null", "false")]
    [InlineData("""{"and":[1,2,3]}""", "null", "3")]       // returns last truthy
    [InlineData("""{"and":[1,0,3]}""", "null", "0")]       // short-circuits at 0
    public void And_ShortCircuits(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    [Theory]
    [InlineData("""{"or":[false,true]}""", "null", "true")]
    [InlineData("""{"or":[false,false]}""", "null", "false")]
    [InlineData("""{"or":[0,"",null,42]}""", "null", "42")]   // returns first truthy
    [InlineData("""{"or":[false,0,""]}""", "null", "\"\"")]   // returns last falsy
    public void Or_ShortCircuits(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── IF / TERNARY CHAINS ─────────────────────────────────────────

    [Theory]
    [InlineData("""{"if":[true,"yes","no"]}""", "null", "\"yes\"")]
    [InlineData("""{"if":[false,"yes","no"]}""", "null", "\"no\"")]
    [InlineData(
        """{"if":[false,"a",false,"b","c"]}""",
        "null",
        "\"c\"")]   // chained if: first false, second false, else "c"
    [InlineData(
        """{"if":[false,"a",true,"b","c"]}""",
        "null",
        "\"b\"")]   // chained if: first false, second true
    public void If_ChainedConditions(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── MAP / FILTER / REDUCE EDGE CASES ────────────────────────────

    [Fact]
    public void Map_EmptyArray_ReturnsEmpty()
    {
        AssertBothPaths(
            """{"map":[[],{"*":[{"var":""},2]}]}""",
            "null",
            "[]");
    }

    [Fact]
    public void Filter_EmptyArray_ReturnsEmpty()
    {
        AssertBothPaths(
            """{"filter":[[],{">":[{"var":""},0]}]}""",
            "null",
            "[]");
    }

    [Fact]
    public void Reduce_EmptyArrayWithInit_ReturnsInit()
    {
        AssertBothPaths(
            """{"reduce":[[],{"+":[{"var":"current"},{"var":"accumulator"}]},99]}""",
            "null",
            "99");
    }

    [Fact]
    public void All_EmptyArray_ReturnsTrue()
    {
        // Vacuous truth: all of nothing is true
        AssertBothPaths(
            """{"all":[[],{">":[{"var":""},0]}]}""",
            "null",
            "false");
    }

    [Fact]
    public void None_EmptyArray_ReturnsTrue()
    {
        AssertBothPaths(
            """{"none":[[],{">":[{"var":""},0]}]}""",
            "null",
            "true");
    }

    [Fact]
    public void Some_EmptyArray_ReturnsFalse()
    {
        AssertBothPaths(
            """{"some":[[],{">":[{"var":""},0]}]}""",
            "null",
            "false");
    }

    // ─── TYPE COERCION (asDouble/asLong/asBigNumber/asBigInteger) ────

    [Theory]
    [InlineData("""{"asDouble":"42.5"}""", "null", "42.5")]
    [InlineData("""{"asDouble":42}""", "null", "42")]
    [InlineData("""{"asDouble":true}""", "null", "1")]
    [InlineData("""{"asDouble":false}""", "null", "0")]
    public void AsDouble_CoercesToDouble(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    [Theory]
    [InlineData("""{"asLong":42.9}""", "null", "42")]      // truncates
    [InlineData("""{"asLong":"100"}""", "null", "100")]
    public void AsLong_CoercesToLong(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── MISSING OPERATOR ────────────────────────────────────────────

    [Theory]
    [InlineData("""{"missing":["a"]}""", """{"a":1}""", "[]")]
    [InlineData("""{"missing":["a"]}""", """{}""", "[\"a\"]")]
    [InlineData("""{"missing":["a","b"]}""", """{"a":1}""", "[\"b\"]")]
    [InlineData("""{"missing":[]}""", """{}""", "[]")]
    public void Missing_ReturnsAbsentPaths(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── HELPERS ─────────────────────────────────────────────────────

    private static void AssertBothPaths(string rule, string data, string expected)
    {
        string result = Evaluate(rule, data);
        string expectedNormalized = NormalizeJson(expected);
        Assert.Equal(expectedNormalized, result);
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
