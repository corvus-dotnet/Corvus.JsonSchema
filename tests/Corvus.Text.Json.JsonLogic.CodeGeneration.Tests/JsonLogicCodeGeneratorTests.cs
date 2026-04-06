// <copyright file="JsonLogicCodeGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonLogic.CodeGeneration;
using Xunit;

namespace Corvus.Text.Json.JsonLogic.CodeGeneration.Tests;

/// <summary>
/// Tests for <see cref="JsonLogicCodeGenerator"/>.
/// </summary>
public class JsonLogicCodeGeneratorTests
{
    [Fact]
    public void Generate_SimpleVar_ContainsEvaluateMethod()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"var":"x"}""",
            "SimpleVarRule",
            "Test.Generated");

        Assert.Contains("public static JsonElement Evaluate(in JsonElement data, JsonWorkspace workspace)", result);
        Assert.Contains("namespace Test.Generated;", result);
        Assert.Contains("class SimpleVarRule", result);
    }

    [Fact]
    public void Generate_SimpleVar_ResolvesProperty()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"var":"x"}""",
            "SimpleVarRule",
            "Test.Generated");

        // Should contain a property lookup for "x"
        Assert.Contains("\"x\"u8", result);
    }

    [Fact]
    public void Generate_Arithmetic_ContainsOperations()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"+":[1,{"*":[2,{"var":"x"}]},3]}""",
            "ArithmeticRule",
            "Test.Generated");

        Assert.Contains("class ArithmeticRule", result);
        Assert.Contains("Evaluate", result);
        // Should reference var "x"
        Assert.Contains("\"x\"u8", result);
    }

    [Fact]
    public void Generate_Comparison_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"<":[1,{"var":"temp"},110]}""",
            "ComparisonRule",
            "Test.Generated");

        Assert.Contains("class ComparisonRule", result);
        Assert.Contains("\"temp\"u8", result);
    }

    [Fact]
    public void Generate_StringCat_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"cat":["Hello, ",{"var":"name"},"!"]}""",
            "CatRule",
            "Test.Generated");

        Assert.Contains("class CatRule", result);
        Assert.Contains("\"name\"u8", result);
    }

    [Fact]
    public void Generate_If_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"if":[{">":[{"var":"age"},18]},"adult","minor"]}""",
            "IfRule",
            "Test.Generated");

        Assert.Contains("class IfRule", result);
        Assert.Contains("\"age\"u8", result);
    }

    [Fact]
    public void Generate_And_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"and":[{">":[{"var":"a"},0]},{"<":[{"var":"b"},10]}]}""",
            "AndRule",
            "Test.Generated");

        Assert.Contains("class AndRule", result);
        Assert.Contains("\"a\"u8", result);
        Assert.Contains("\"b\"u8", result);
    }

    [Fact]
    public void Generate_Or_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"or":[{"==":[{"var":"role"},"admin"]},{"==":[{"var":"role"},"owner"]}]}""",
            "OrRule",
            "Test.Generated");

        Assert.Contains("class OrRule", result);
        Assert.Contains("\"role\"u8", result);
    }

    [Fact]
    public void Generate_Filter_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"filter":[{"var":"items"},{">":[{"var":""},5]}]}""",
            "FilterRule",
            "Test.Generated");

        Assert.Contains("class FilterRule", result);
        Assert.Contains("\"items\"u8", result);
    }

    [Fact]
    public void Generate_Map_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"map":[{"var":"items"},{"*":[{"var":""},2]}]}""",
            "MapRule",
            "Test.Generated");

        Assert.Contains("class MapRule", result);
        Assert.Contains("\"items\"u8", result);
    }

    [Fact]
    public void Generate_Reduce_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"reduce":[{"var":"items"},{"+":[{"var":"current"},{"var":"accumulator"}]},0]}""",
            "ReduceRule",
            "Test.Generated");

        Assert.Contains("class ReduceRule", result);
        Assert.Contains("\"items\"u8", result);
    }

    [Fact]
    public void Generate_FusedMapReduce_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"reduce":[{"map":[{"var":"items"},{"*":[{"var":""},2]}]},{"+":[{"var":"current"},{"var":"accumulator"}]},0]}""",
            "FusedMapReduceRule",
            "Test.Generated");

        Assert.Contains("class FusedMapReduceRule", result);
    }

    [Fact]
    public void Generate_Missing_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"missing":["a","b","c"]}""",
            "MissingRule",
            "Test.Generated");

        Assert.Contains("class MissingRule", result);
    }

    [Fact]
    public void Generate_Not_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"!":[{"var":"active"}]}""",
            "NotRule",
            "Test.Generated");

        Assert.Contains("class NotRule", result);
        Assert.Contains("\"active\"u8", result);
    }

    [Fact]
    public void Generate_DoubleNot_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"!!":[{"var":"value"}]}""",
            "DoubleNotRule",
            "Test.Generated");

        Assert.Contains("class DoubleNotRule", result);
    }

    [Fact]
    public void Generate_Equality_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"==":[{"var":"status"},"active"]}""",
            "EqualityRule",
            "Test.Generated");

        Assert.Contains("class EqualityRule", result);
        Assert.Contains("\"status\"u8", result);
    }

    [Fact]
    public void Generate_StrictEquality_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"===":[{"var":"count"},0]}""",
            "StrictEqRule",
            "Test.Generated");

        Assert.Contains("class StrictEqRule", result);
    }

    [Fact]
    public void Generate_In_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"in":["a",{"var":"items"}]}""",
            "InRule",
            "Test.Generated");

        Assert.Contains("class InRule", result);
    }

    [Fact]
    public void Generate_Min_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"min":[1,2,3]}""",
            "MinRule",
            "Test.Generated");

        Assert.Contains("class MinRule", result);
    }

    [Fact]
    public void Generate_Max_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"max":[1,2,3]}""",
            "MaxRule",
            "Test.Generated");

        Assert.Contains("class MaxRule", result);
    }

    [Fact]
    public void Generate_DottedVarPath_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"var":"person.name"}""",
            "DottedVarRule",
            "Test.Generated");

        Assert.Contains("class DottedVarRule", result);
        Assert.Contains("\"person\"u8", result);
        Assert.Contains("\"name\"u8", result);
    }

    [Fact]
    public void Generate_ComplexRule_ProducesValidCode()
    {
        // The complex benchmark rule
        string rule = """
            {"if":[
                {"<":[{"var":"age"},13]},
                {"cat":["Hello, ",{"var":"name"},"! You are a child."]},
                {"<":[{"var":"age"},18]},
                {"cat":["Hello, ",{"var":"name"},"! You are a teenager."]},
                {">=":[{"var":"age"},65]},
                {"cat":["Hello, ",{"var":"name"},"! You are a senior."]},
                {"cat":["Hello, ",{"var":"name"},"! You are an adult."]}
            ]}
            """;

        string result = JsonLogicCodeGenerator.Generate(rule, "ComplexRule", "Test.Generated");

        Assert.Contains("class ComplexRule", result);
        Assert.Contains("\"age\"u8", result);
        Assert.Contains("\"name\"u8", result);
    }

    [Fact]
    public void Generate_MissingDataBenchmark_ProducesValidCode()
    {
        string rule = """{"if":[{"missing":["a","b"]},{"cat":["Missing: ",{"missing":["a","b"]}]},{"+":[{"var":"a"},{"var":"b"}]}]}""";

        string result = JsonLogicCodeGenerator.Generate(rule, "MissingDataRule", "Test.Generated");

        Assert.Contains("class MissingDataRule", result);
        Assert.Contains("\"a\"u8", result);
        Assert.Contains("\"b\"u8", result);
    }

    [Fact]
    public void Generate_LiteralNumber_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate("42", "LiteralRule", "Test.Generated");

        Assert.Contains("class LiteralRule", result);
        Assert.Contains("Evaluate", result);
    }

    [Fact]
    public void Generate_LiteralString_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate("\"hello\"", "LiteralRule", "Test.Generated");

        Assert.Contains("class LiteralRule", result);
    }

    [Fact]
    public void Generate_LiteralTrue_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate("true", "LiteralRule", "Test.Generated");

        Assert.Contains("class LiteralRule", result);
    }

    [Fact]
    public void Generate_LiteralNull_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate("null", "LiteralRule", "Test.Generated");

        Assert.Contains("class LiteralRule", result);
    }

    [Fact]
    public void Generate_OutputIsAutoGenerated()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"var":"x"}""",
            "TestRule",
            "Test.Generated");

        Assert.StartsWith("// <auto-generated/>", result);
    }

    [Fact]
    public void Generate_OutputIncludesRequiredUsings()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"var":"x"}""",
            "TestRule",
            "Test.Generated");

        Assert.Contains("using Corvus.Text.Json;", result);
        Assert.Contains("using Corvus.Text.Json.JsonLogic;", result);
    }

    // ─── JlopsParser Tests ──────────────────────────────────────

    [Fact]
    public void JlopsParser_ExpressionForm_ParsesCorrectly()
    {
        string content = """op discount(price, percent) => BigNumberToElement(CoerceToBigNumber(price) * (BigNumber.One - CoerceToBigNumber(percent) / (BigNumber)100), workspace);""";

        IReadOnlyList<CustomOperator> ops = JlopsParser.Parse(content);

        Assert.Single(ops);
        Assert.Equal("discount", ops[0].Name);
        Assert.Equal(new[] { "price", "percent" }, ops[0].Parameters);
        Assert.True(ops[0].IsExpression);
        Assert.Contains("CoerceToBigNumber(price)", ops[0].Body);
    }

    [Fact]
    public void JlopsParser_BlockForm_ParsesCorrectly()
    {
        string content = """
            op clamp(value, lo, hi)
            {
                BigNumber v = CoerceToBigNumber(value);
                BigNumber low = CoerceToBigNumber(lo);
                BigNumber high = CoerceToBigNumber(hi);
                BigNumber clamped = v < low ? low : v > high ? high : v;
                return BigNumberToElement(clamped, workspace);
            }
            """;

        IReadOnlyList<CustomOperator> ops = JlopsParser.Parse(content);

        Assert.Single(ops);
        Assert.Equal("clamp", ops[0].Name);
        Assert.Equal(new[] { "value", "lo", "hi" }, ops[0].Parameters);
        Assert.False(ops[0].IsExpression);
        Assert.Contains("BigNumber v = CoerceToBigNumber(value);", ops[0].Body);
        Assert.Contains("return BigNumberToElement(clamped, workspace);", ops[0].Body);
    }

    [Fact]
    public void JlopsParser_MultipleOperators_ParsesAll()
    {
        string content = """
            // Custom operators for pricing
            op double(x) => BigNumberToElement(CoerceToBigNumber(x) * (BigNumber)2, workspace);
            op negate(x) => BigNumberToElement(-CoerceToBigNumber(x), workspace);
            """;

        IReadOnlyList<CustomOperator> ops = JlopsParser.Parse(content);

        Assert.Equal(2, ops.Count);
        Assert.Equal("double", ops[0].Name);
        Assert.Equal("negate", ops[1].Name);
    }

    [Fact]
    public void JlopsParser_CommentsAndBlankLinesSkipped()
    {
        string content = """
            // This is a comment

            // Another comment
            op identity(x) => x;

            """;

        IReadOnlyList<CustomOperator> ops = JlopsParser.Parse(content);

        Assert.Single(ops);
        Assert.Equal("identity", ops[0].Name);
    }

    [Fact]
    public void JlopsParser_ZeroParameters_Allowed()
    {
        string content = "op pi() => JsonLogicHelpers.NumberFromSpan(\"3.14159\"u8);";

        IReadOnlyList<CustomOperator> ops = JlopsParser.Parse(content);

        Assert.Single(ops);
        Assert.Equal("pi", ops[0].Name);
        Assert.Empty(ops[0].Parameters);
    }

    [Fact]
    public void JlopsParser_MissingOpKeyword_Throws()
    {
        string content = "discount(price) => price;";

        Assert.Throws<FormatException>(() => JlopsParser.Parse(content));
    }

    [Fact]
    public void JlopsParser_MissingParentheses_Throws()
    {
        string content = "op discount => price;";

        Assert.Throws<FormatException>(() => JlopsParser.Parse(content));
    }

    [Fact]
    public void JlopsParser_UnmatchedBrace_Throws()
    {
        string content = """
            op broken(x)
            {
                return x;
            """;

        Assert.Throws<FormatException>(() => JlopsParser.Parse(content));
    }

    // ─── Code Generator with Custom Operators ───────────────────

    [Fact]
    public void Generate_WithCustomOperator_EmitsHelperMethod()
    {
        List<CustomOperator> customOps =
        [
            new("double_it", new[] { "x" }, "BigNumberToElement(CoerceToBigNumber(x) * (BigNumber)2, workspace)", isExpression: true),
        ];

        string result = JsonLogicCodeGenerator.Generate(
            """{"double_it":[{"var":"x"}]}""",
            "CustomOpRule",
            "Test.Generated",
            customOps);

        Assert.Contains("CustomOp_double_it(", result);
        Assert.Contains("CoerceToBigNumber(x) * (BigNumber)2", result);
    }

    [Fact]
    public void Generate_WithCustomOperator_BlockForm_EmitsHelperMethod()
    {
        List<CustomOperator> customOps =
        [
            new("clamp", new[] { "value", "lo", "hi" },
                """
                BigNumber v = CoerceToBigNumber(value);
                BigNumber low = CoerceToBigNumber(lo);
                BigNumber high = CoerceToBigNumber(hi);
                BigNumber clamped = v < low ? low : v > high ? high : v;
                return BigNumberToElement(clamped, workspace);
                """,
                isExpression: false),
        ];

        string result = JsonLogicCodeGenerator.Generate(
            """{"clamp":[{"var":"x"}, 0, 100]}""",
            "ClampRule",
            "Test.Generated",
            customOps);

        Assert.Contains("CustomOp_clamp(", result);
        Assert.Contains("BigNumber v = CoerceToBigNumber(value);", result);
    }

    [Fact]
    public void Generate_CustomOperator_WrongArgCount_Throws()
    {
        List<CustomOperator> customOps =
        [
            new("double_it", new[] { "x" }, "x", isExpression: true),
        ];

        Assert.Throws<InvalidOperationException>(() =>
            JsonLogicCodeGenerator.Generate(
                """{"double_it":[1, 2]}""",
                "BadRule",
                "Test.Generated",
                customOps));
    }

    [Fact]
    public void Generate_UnknownOperator_StillThrows()
    {
        Assert.Throws<InvalidOperationException>(() =>
            JsonLogicCodeGenerator.Generate(
                """{"nonexistent":[1]}""",
                "BadRule",
                "Test.Generated",
                null));
    }

    [Fact]
    public void Generate_CustomOperator_NotUsed_OmitsMethod()
    {
        List<CustomOperator> customOps =
        [
            new("unused_op", new[] { "x" }, "x", isExpression: true),
        ];

        string result = JsonLogicCodeGenerator.Generate(
            """{"var":"x"}""",
            "SimpleRule",
            "Test.Generated",
            customOps);

        Assert.DoesNotContain("CustomOp_unused_op", result);
    }
}