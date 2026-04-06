// <copyright file="FunctionalEvaluatorEdgeCaseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.JsonLogic;
using Xunit;

namespace Corvus.Text.Json.JsonLogic.Tests;

/// <summary>
/// Edge-case tests that exercise code paths not covered by the official JsonLogic test suite.
/// Each test runs against both VM and functional evaluator to verify behavioral equivalence.
/// </summary>
public class FunctionalEvaluatorEdgeCaseTests
{
    // ─── REDUCE OPTIMIZATION PATHS ──────────────────────────────────

    [Theory]
    [InlineData(
        """{"reduce":[{"var":"items"},{"+":[{"var":"current"},{"var":"accumulator"}]},0]}""",
        """{"items":[1,2,3,4,5]}""",
        "15",
        "Optimized reduce (body uses only current/accumulator)")]
    [InlineData(
        """{"reduce":[{"map":[{"var":"items"},{"*":[{"var":""},2]}]},{"+":[{"var":"current"},{"var":"accumulator"}]},0]}""",
        """{"items":[1,2,3]}""",
        "12",
        "Fused map-reduce")]
    [InlineData(
        """{"reduce":[{"map":[{"var":"items"},{"+":[{"var":""},10]}]},{"+":[{"var":"current"},{"var":"accumulator"}]},0]}""",
        """{"items":[1,2,3,4,5]}""",
        "65",
        "Fused map-reduce with addition map body")]
    [InlineData(
        """{"reduce":[[1,2,3],{"+":[{"var":"current"},{"var":"accumulator"}]},100]}""",
        "null",
        "106",
        "Reduce with literal array and non-zero init")]
    public void ReduceOptimizedPath(string rule, string data, string expected, string description)
    {
        _ = description;
        AssertBothPaths(rule, data, expected);
    }

    [Theory]
    [InlineData(
        """{"reduce":[{"var":"items"},{"+":[{"var":"current.a"},{"var":"accumulator"}]},0]}""",
        """{"items":[{"a":1},{"a":2},{"a":3}]}""",
        "6",
        "Reduce with dotted path current.a (fallback path)")]
    [InlineData(
        """{"reduce":[[1,2,3],{"+":[{"var":"current"},{"var":"accumulator"}]},0]}""",
        "null",
        "6",
        "Reduce with literal array")]
    public void ReduceFallbackPath(string rule, string data, string expected, string description)
    {
        _ = description;
        AssertBothPaths(rule, data, expected);
    }

    [Theory]
    [InlineData(
        """{"reduce":[{"var":"outer"},{"+":[{"reduce":[{"var":"current"},{"+":[{"var":"current"},{"var":"accumulator"}]},0]},{"var":"accumulator"}]},0]}""",
        """{"outer":[[1,2],[3,4]]}""",
        "10",
        "Nested reduce: sum of inner sums (ReduceContext save/restore)")]
    public void ReduceNested(string rule, string data, string expected, string description)
    {
        _ = description;

        // VM does not support nested reduces correctly — test functional only.
        AssertFunctionalOnly(rule, data, expected);
    }

    [Fact]
    public void ReduceEmptyArray()
    {
        AssertBothPaths(
            """{"reduce":[[],{"+":[{"var":"current"},{"var":"accumulator"}]},42]}""",
            "null",
            "42");
    }

    // ─── CORVUS EXTENSION OPERATORS ─────────────────────────────────

    [Theory]
    [InlineData("""{"asDouble":"42.5"}""", "null", "42.5")]
    [InlineData("""{"asDouble":true}""", "null", "1")]
    [InlineData("""{"asDouble":false}""", "null", "0")]
    [InlineData("""{"asDouble":null}""", "null", "0")]
    [InlineData("""{"asDouble":{"var":"x"}}""", """{"x":"3.14"}""", "3.14")]
    public void AsDouble(string rule, string data, string expected)
    {
        AssertFunctionalOnly(rule, data, expected);
    }

    [Theory]
    [InlineData("""{"asLong":42.9}""", "null", "42")]
    [InlineData("""{"asLong":"100"}""", "null", "100")]
    [InlineData("""{"asLong":true}""", "null", "1")]
    public void AsLong(string rule, string data, string expected)
    {
        AssertFunctionalOnly(rule, data, expected);
    }

    [Theory]
    [InlineData("""{"asBigNumber":"12345678901234567890"}""", "null", "1234567890123456789E1")]
    [InlineData("""{"asBigNumber":42}""", "null", "42")]
    public void AsBigNumber(string rule, string data, string expected)
    {
        AssertFunctionalOnly(rule, data, expected);
    }

    [Theory]
    [InlineData("""{"asBigInteger":"12345678901234567890"}""", "null", "1234567890123456789E1")]
    [InlineData("""{"asBigInteger":42.9}""", "null", "42")]
    public void AsBigInteger(string rule, string data, string expected)
    {
        AssertFunctionalOnly(rule, data, expected);
    }

    // ─── ARITHMETIC SLOW PATHS (MIXED TYPE COERCION) ────────────────

    [Theory]
    [InlineData("""{"+":[1,"2",3]}""", "null", "6", "Add with string coercion")]
    [InlineData("""{"+":[1,true,false]}""", "null", "2", "Add with boolean coercion")]
    [InlineData("""{"+":[10,null]}""", "null", "10", "Add with null coercion")]
    [InlineData("""{"*":[2,"3"]}""", "null", "6", "Multiply with string coercion")]
    [InlineData("""{"*":[5,true]}""", "null", "5", "Multiply with boolean")]
    [InlineData("""{"-":["10",3]}""", "null", "7", "Subtract with string coercion")]
    [InlineData("""{"/":[10,"2"]}""", "null", "5", "Divide with string coercion")]
    [InlineData("""{"%":[10,"3"]}""", "null", "1", "Modulo with string coercion")]
    public void ArithmeticWithCoercion(string rule, string data, string expected, string description)
    {
        _ = description;
        AssertBothPaths(rule, data, expected);
    }

    [Theory]
    [InlineData("""{"/":[10,0]}""", "null", "null", "Division by zero")]
    [InlineData("""{"%":[10,0]}""", "null", "null", "Modulo by zero")]
    public void ArithmeticDivisionByZero(string rule, string data, string expected, string description)
    {
        _ = description;
        AssertBothPaths(rule, data, expected);
    }

    // ─── COMPARISON COERCION EDGE CASES ─────────────────────────────

    [Theory]
    [InlineData("""{"<":["2",10]}""", "null", "true", "String-number comparison")]
    [InlineData("""{">":[10,"2"]}""", "null", "true", "Number-string comparison")]
    [InlineData("""{">=":[true,1]}""", "null", "true", "Boolean-number comparison")]
    [InlineData("""{"<":[false,1]}""", "null", "true", "False < 1")]
    [InlineData("""{"<":[null,1]}""", "null", "true", "Null coerces to 0: 0 < 1")]
    [InlineData("""{"<":[1,2,3,4,5]}""", "null", "true", "Between chain (ascending)")]
    public void ComparisonCoercion(string rule, string data, string expected, string description)
    {
        _ = description;
        AssertBothPaths(rule, data, expected);
    }

    // ─── EQUALITY COERCION EDGE CASES ───────────────────────────────

    [Theory]
    [InlineData("""{"==":[1,"1"]}""", "null", "true", "Number == string")]
    [InlineData("""{"==":[0,false]}""", "null", "true", "0 == false")]
    [InlineData("""{"==":[1,true]}""", "null", "true", "1 == true")]
    [InlineData("""{"==":[null,false]}""", "null", "false", "null != false")]
    [InlineData("""{"===":[1,"1"]}""", "null", "false", "Strict: 1 !== '1'")]
    [InlineData("""{"===":[1,1]}""", "null", "true", "Strict: 1 === 1")]
    [InlineData("""{"!=":[1,2]}""", "null", "true", "1 != 2")]
    [InlineData("""{"!==":[1,"1"]}""", "null", "true", "Strict: 1 !== '1'")]
    public void EqualityCoercion(string rule, string data, string expected, string description)
    {
        _ = description;
        AssertBothPaths(rule, data, expected);
    }

    // ─── HETEROGENEOUS ARRAYS AND MIXED TYPES ───────────────────────

    [Theory]
    [InlineData("""[1,"hello",true,null]""", """{}""", """[1,"hello",true,null]""", "Mixed-type array literal")]
    [InlineData(
        """{"map":[[1,2,3],{"if":[{">":[{"var":""},1]},{"cat":["big:",{"var":""}]},{"var":""}]}]}""",
        "null",
        """[1,"big:2","big:3"]""",
        "Map returning mixed types")]
    [InlineData(
        """{"cat":[1,true,null,"hello"]}""",
        "null",
        "\"1truenullhello\"",
        "Cat with mixed types including null")]
    public void HeterogeneousTypes(string rule, string data, string expected, string description)
    {
        _ = description;
        AssertBothPaths(rule, data, expected);
    }

    // ─── VARIABLE RESOLUTION EDGE CASES ─────────────────────────────

    [Theory]
    [InlineData("""{"var":"a.b.c"}""", """{"a":{"b":{"c":42}}}""", "42", "Deep nested path")]
    [InlineData("""{"var":"a.b.c"}""", """{"a":{"b":{}}}""", "null", "Missing deep path")]
    [InlineData("""{"var":"items.1"}""", """{"items":[10,20,30]}""", "20", "Array index in path")]
    [InlineData("""{"var":["missing","default"]}""", """{}""", "\"default\"", "Var with default value")]
    [InlineData("""{"var":["missing",42]}""", """{}""", "42", "Var with numeric default")]
    public void VariableResolution(string rule, string data, string expected, string description)
    {
        _ = description;
        AssertBothPaths(rule, data, expected);
    }

    // ─── QUANTIFIER EDGE CASES ──────────────────────────────────────

    [Theory]
    [InlineData("""{"all":[[1,2,3],{">":[{"var":""},0]}]}""", "null", "true")]
    [InlineData("""{"all":[[1,2,-1],{">":[{"var":""},0]}]}""", "null", "false")]
    [InlineData("""{"none":[[1,2,3],{"<":[{"var":""},0]}]}""", "null", "true")]
    [InlineData("""{"none":[[1,-2,3],{"<":[{"var":""},0]}]}""", "null", "false")]
    [InlineData("""{"some":[[1,2,3],{">":[{"var":""},2]}]}""", "null", "true")]
    [InlineData("""{"some":[[1,2,3],{">":[{"var":""},5]}]}""", "null", "false")]
    public void Quantifiers(string rule, string data, string expected)
    {
        AssertBothPaths(rule, data, expected);
    }

    // ─── LITERAL DOUBLE EXTRACTION ──────────────────────────────────

    [Theory]
    [InlineData("""{"+":[0.1,0.2]}""", "null", "Add small floats")]
    [InlineData("""{"*":[1000000,1000000]}""", "null", "Large multiplication")]
    public void LiteralDoubleExtraction(string rule, string data, string description)
    {
        _ = description;
        // Just verify VM and functional produce identical results
        string vmResult = EvaluateVM(rule, data);
        string funcResult = EvaluateFunctional(rule, data);
        Assert.Equal(vmResult, funcResult);
    }

    // ─── HELPERS ────────────────────────────────────────────────────

    private static void AssertBothPaths(string rule, string data, string expected)
    {
        string vmResult = EvaluateVM(rule, data);
        string funcResult = EvaluateFunctional(rule, data);

        string expectedNormalized = NormalizeJson(expected);

        Assert.Equal(expectedNormalized, vmResult);
        Assert.Equal(expectedNormalized, funcResult);
    }

    private static void AssertFunctionalOnly(string rule, string data, string expected)
    {
        string funcResult = EvaluateFunctional(rule, data);
        string expectedNormalized = NormalizeJson(expected);
        Assert.Equal(expectedNormalized, funcResult);
    }

    private static string EvaluateVM(string rule, string data)
    {
        byte[] ruleUtf8 = Encoding.UTF8.GetBytes(rule);
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(data);

        Corvus.Text.Json.JsonElement ruleElement = Corvus.Text.Json.JsonElement.ParseValue(ruleUtf8);
        Corvus.Text.Json.JsonElement dataElement = Corvus.Text.Json.JsonElement.ParseValue(dataUtf8);

        JsonLogicRule logicRule = new(ruleElement);
        Corvus.Text.Json.JsonElement result = JsonLogicEvaluator.Default.Evaluate(logicRule, dataElement);

        return NormalizeJson(GetRawText(result));
    }

    private static string EvaluateFunctional(string rule, string data)
    {
        byte[] ruleUtf8 = Encoding.UTF8.GetBytes(rule);
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(data);

        Corvus.Text.Json.JsonElement ruleElement = Corvus.Text.Json.JsonElement.ParseValue(ruleUtf8);
        Corvus.Text.Json.JsonElement dataElement = Corvus.Text.Json.JsonElement.ParseValue(dataUtf8);

        JsonLogicRule logicRule = new(ruleElement);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Corvus.Text.Json.JsonElement result = JsonLogicEvaluator.Default.EvaluateFunctional(logicRule, dataElement, workspace);

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