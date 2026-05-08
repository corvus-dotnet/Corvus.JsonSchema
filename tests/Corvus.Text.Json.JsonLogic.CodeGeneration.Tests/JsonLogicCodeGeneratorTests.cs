// <copyright file="JsonLogicCodeGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonLogic.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JsonLogic.CodeGeneration.Tests;

/// <summary>
/// Tests for <see cref="JsonLogicCodeGenerator"/>.
/// </summary>
[TestClass]
public class JsonLogicCodeGeneratorTests
{
    [TestMethod]
    public void Generate_SimpleVar_ContainsEvaluateMethod()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"var":"x"}""",
            "SimpleVarRule",
            "Test.Generated");

        StringAssert.Contains(result, "public static JsonElement Evaluate(in JsonElement data, JsonWorkspace workspace)");
        StringAssert.Contains(result, "namespace Test.Generated;");
        StringAssert.Contains(result, "class SimpleVarRule");
    }

    [TestMethod]
    public void Generate_SimpleVar_ResolvesProperty()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"var":"x"}""",
            "SimpleVarRule",
            "Test.Generated");

        // Should contain a property lookup for "x"
        StringAssert.Contains(result, "\"x\"u8");
    }

    [TestMethod]
    public void Generate_Arithmetic_ContainsOperations()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"+":[1,{"*":[2,{"var":"x"}]},3]}""",
            "ArithmeticRule",
            "Test.Generated");

        StringAssert.Contains(result, "class ArithmeticRule");
        StringAssert.Contains(result, "Evaluate");
        // Should reference var "x"
        StringAssert.Contains(result, "\"x\"u8");
    }

    [TestMethod]
    public void Generate_Comparison_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"<":[1,{"var":"temp"},110]}""",
            "ComparisonRule",
            "Test.Generated");

        StringAssert.Contains(result, "class ComparisonRule");
        StringAssert.Contains(result, "\"temp\"u8");
    }

    [TestMethod]
    public void Generate_StringCat_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"cat":["Hello, ",{"var":"name"},"!"]}""",
            "CatRule",
            "Test.Generated");

        StringAssert.Contains(result, "class CatRule");
        StringAssert.Contains(result, "\"name\"u8");
    }

    [TestMethod]
    public void Generate_If_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"if":[{">":[{"var":"age"},18]},"adult","minor"]}""",
            "IfRule",
            "Test.Generated");

        StringAssert.Contains(result, "class IfRule");
        StringAssert.Contains(result, "\"age\"u8");
    }

    [TestMethod]
    public void Generate_And_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"and":[{">":[{"var":"a"},0]},{"<":[{"var":"b"},10]}]}""",
            "AndRule",
            "Test.Generated");

        StringAssert.Contains(result, "class AndRule");
        StringAssert.Contains(result, "\"a\"u8");
        StringAssert.Contains(result, "\"b\"u8");
    }

    [TestMethod]
    public void Generate_AndInIf_SharedVarDoesNotLeakScope()
    {
        // Regression: var "total" inside and's short-circuit block leaked into
        // _varCache, causing consequent/else branches to reference a variable
        // that might not be definitely assigned. The fix uses goto-based short-
        // circuit (no do/while scope) plus cache save/restore so each context
        // allocates its own variable for the same var path.
        string result = JsonLogicCodeGenerator.Generate(
            """
            {"if":[
                {"and":[
                    {"==":[{"var":"tier"},"gold"]},
                    {">=":[{"var":"total"},100]}
                ]},
                {"*":[{"var":"total"},0.8]},
                {">=":[{"var":"total"},200]},
                {"*":[{"var":"total"},0.9]},
                {"var":"total"}
            ]}
            """,
            "AndScopeLeakRule",
            "Test.Generated");

        StringAssert.Contains(result, "class AndScopeLeakRule");

        // Verify goto-based short-circuit (no do/while(false))
        Assert.DoesNotContain("while (false)", result);
        StringAssert.Contains(result, "goto");

        // Each context must declare its own variable for "total" because the
        // cache is restored after the and block and after each if-branch.
        int totalLookups = CountOccurrences(result, "\"total\"u8");
        Assert.IsTrue(totalLookups > 1, $"Expected multiple 'total' lookups across scopes, got {totalLookups}");
    }

    [TestMethod]
    public void Generate_OrInIf_SharedVarDoesNotLeakScope()
    {
        // Same regression as and, but for or's short-circuit block.
        string result = JsonLogicCodeGenerator.Generate(
            """
            {"if":[
                {"or":[
                    {"==":[{"var":"role"},"admin"]},
                    {"==":[{"var":"role"},"owner"]}
                ]},
                {"cat":["Welcome, ",{"var":"role"}]},
                {"var":"role"}
            ]}
            """,
            "OrScopeLeakRule",
            "Test.Generated");

        StringAssert.Contains(result, "class OrScopeLeakRule");

        // Verify goto-based short-circuit (no do/while(false))
        Assert.DoesNotContain("while (false)", result);
        StringAssert.Contains(result, "goto");

        int roleLookups = CountOccurrences(result, "\"role\"u8");
        Assert.IsTrue(roleLookups > 1, $"Expected multiple 'role' lookups across scopes, got {roleLookups}");
    }

    [TestMethod]
    public void Generate_Or_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"or":[{"==":[{"var":"role"},"admin"]},{"==":[{"var":"role"},"owner"]}]}""",
            "OrRule",
            "Test.Generated");

        StringAssert.Contains(result, "class OrRule");
        StringAssert.Contains(result, "\"role\"u8");
    }

    [TestMethod]
    public void Generate_Filter_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"filter":[{"var":"items"},{">":[{"var":""},5]}]}""",
            "FilterRule",
            "Test.Generated");

        StringAssert.Contains(result, "class FilterRule");
        StringAssert.Contains(result, "\"items\"u8");
    }

    [TestMethod]
    public void Generate_Map_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"map":[{"var":"items"},{"*":[{"var":""},2]}]}""",
            "MapRule",
            "Test.Generated");

        StringAssert.Contains(result, "class MapRule");
        StringAssert.Contains(result, "\"items\"u8");
    }

    [TestMethod]
    public void Generate_Reduce_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"reduce":[{"var":"items"},{"+":[{"var":"current"},{"var":"accumulator"}]},0]}""",
            "ReduceRule",
            "Test.Generated");

        StringAssert.Contains(result, "class ReduceRule");
        StringAssert.Contains(result, "\"items\"u8");
    }

    [TestMethod]
    public void Generate_FusedMapReduce_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"reduce":[{"map":[{"var":"items"},{"*":[{"var":""},2]}]},{"+":[{"var":"current"},{"var":"accumulator"}]},0]}""",
            "FusedMapReduceRule",
            "Test.Generated");

        StringAssert.Contains(result, "class FusedMapReduceRule");
    }

    [TestMethod]
    public void Generate_Missing_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"missing":["a","b","c"]}""",
            "MissingRule",
            "Test.Generated");

        StringAssert.Contains(result, "class MissingRule");
    }

    [TestMethod]
    public void Generate_Not_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"!":[{"var":"active"}]}""",
            "NotRule",
            "Test.Generated");

        StringAssert.Contains(result, "class NotRule");
        StringAssert.Contains(result, "\"active\"u8");
    }

    [TestMethod]
    public void Generate_DoubleNot_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"!!":[{"var":"value"}]}""",
            "DoubleNotRule",
            "Test.Generated");

        StringAssert.Contains(result, "class DoubleNotRule");
    }

    [TestMethod]
    public void Generate_Equality_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"==":[{"var":"status"},"active"]}""",
            "EqualityRule",
            "Test.Generated");

        StringAssert.Contains(result, "class EqualityRule");
        StringAssert.Contains(result, "\"status\"u8");
    }

    [TestMethod]
    public void Generate_StrictEquality_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"===":[{"var":"count"},0]}""",
            "StrictEqRule",
            "Test.Generated");

        StringAssert.Contains(result, "class StrictEqRule");
    }

    [TestMethod]
    public void Generate_In_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"in":["a",{"var":"items"}]}""",
            "InRule",
            "Test.Generated");

        StringAssert.Contains(result, "class InRule");
    }

    [TestMethod]
    public void Generate_Min_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"min":[1,2,3]}""",
            "MinRule",
            "Test.Generated");

        StringAssert.Contains(result, "class MinRule");
    }

    [TestMethod]
    public void Generate_Max_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"max":[1,2,3]}""",
            "MaxRule",
            "Test.Generated");

        StringAssert.Contains(result, "class MaxRule");
    }

    [TestMethod]
    public void Generate_DottedVarPath_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"var":"person.name"}""",
            "DottedVarRule",
            "Test.Generated");

        StringAssert.Contains(result, "class DottedVarRule");
        StringAssert.Contains(result, "\"person\"u8");
        StringAssert.Contains(result, "\"name\"u8");
    }

    [TestMethod]
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

        StringAssert.Contains(result, "class ComplexRule");
        StringAssert.Contains(result, "\"age\"u8");
        StringAssert.Contains(result, "\"name\"u8");
    }

    [TestMethod]
    public void Generate_MissingDataBenchmark_ProducesValidCode()
    {
        string rule = """{"if":[{"missing":["a","b"]},{"cat":["Missing: ",{"missing":["a","b"]}]},{"+":[{"var":"a"},{"var":"b"}]}]}""";

        string result = JsonLogicCodeGenerator.Generate(rule, "MissingDataRule", "Test.Generated");

        StringAssert.Contains(result, "class MissingDataRule");
        StringAssert.Contains(result, "\"a\"u8");
        StringAssert.Contains(result, "\"b\"u8");
    }

    [TestMethod]
    public void Generate_LiteralNumber_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate("42", "LiteralRule", "Test.Generated");

        StringAssert.Contains(result, "class LiteralRule");
        StringAssert.Contains(result, "Evaluate");
    }

    [TestMethod]
    public void Generate_LiteralString_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate("\"hello\"", "LiteralRule", "Test.Generated");

        StringAssert.Contains(result, "class LiteralRule");
    }

    [TestMethod]
    public void Generate_LiteralTrue_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate("true", "LiteralRule", "Test.Generated");

        StringAssert.Contains(result, "class LiteralRule");
    }

    [TestMethod]
    public void Generate_LiteralNull_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate("null", "LiteralRule", "Test.Generated");

        StringAssert.Contains(result, "class LiteralRule");
    }

    [TestMethod]
    public void Generate_OutputIsAutoGenerated()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"var":"x"}""",
            "TestRule",
            "Test.Generated");

        Assert.StartsWith("// <auto-generated/>", result);
    }

    [TestMethod]
    public void Generate_OutputIncludesRequiredUsings()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"var":"x"}""",
            "TestRule",
            "Test.Generated");

        StringAssert.Contains(result, "using Corvus.Text.Json;");
        StringAssert.Contains(result, "using Corvus.Text.Json.JsonLogic;");
    }

    // ─── JlopsParser Tests ──────────────────────────────────────

    [TestMethod]
    public void JlopsParser_ExpressionForm_ParsesCorrectly()
    {
        string content = """op discount(price, percent) => BigNumberToElement(CoerceToBigNumber(price) * (BigNumber.One - CoerceToBigNumber(percent) / (BigNumber)100), workspace);""";

        IReadOnlyList<CustomOperator> ops = JlopsParser.Parse(content);

        Assert.AreEqual(1, (ops).Count());
        Assert.AreEqual("discount", ops[0].Name);
        CollectionAssert.AreEqual(new[] { "price", "percent" }, ops[0].Parameters);
        Assert.IsTrue(ops[0].IsExpression);
        StringAssert.Contains(ops[0].Body, "CoerceToBigNumber(price)");
    }

    [TestMethod]
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

        Assert.AreEqual(1, (ops).Count());
        Assert.AreEqual("clamp", ops[0].Name);
        CollectionAssert.AreEqual(new[] { "value", "lo", "hi" }, ops[0].Parameters);
        Assert.IsFalse(ops[0].IsExpression);
        StringAssert.Contains(ops[0].Body, "BigNumber v = CoerceToBigNumber(value);");
        StringAssert.Contains(ops[0].Body, "return BigNumberToElement(clamped, workspace);");
    }

    [TestMethod]
    public void JlopsParser_MultipleOperators_ParsesAll()
    {
        string content = """
            // Custom operators for pricing
            op double(x) => BigNumberToElement(CoerceToBigNumber(x) * (BigNumber)2, workspace);
            op negate(x) => BigNumberToElement(-CoerceToBigNumber(x), workspace);
            """;

        IReadOnlyList<CustomOperator> ops = JlopsParser.Parse(content);

        Assert.AreEqual(2, ops.Count);
        Assert.AreEqual("double", ops[0].Name);
        Assert.AreEqual("negate", ops[1].Name);
    }

    [TestMethod]
    public void JlopsParser_CommentsAndBlankLinesSkipped()
    {
        string content = """
            // This is a comment

            // Another comment
            op identity(x) => x;

            """;

        IReadOnlyList<CustomOperator> ops = JlopsParser.Parse(content);

        Assert.AreEqual(1, (ops).Count());
        Assert.AreEqual("identity", ops[0].Name);
    }

    [TestMethod]
    public void JlopsParser_ZeroParameters_Allowed()
    {
        string content = "op pi() => JsonLogicHelpers.NumberFromSpan(\"3.14159\"u8);";

        IReadOnlyList<CustomOperator> ops = JlopsParser.Parse(content);

        Assert.AreEqual(1, (ops).Count());
        Assert.AreEqual("pi", ops[0].Name);
        Assert.AreEqual(0, (ops[0].Parameters).Count());
    }

    [TestMethod]
    public void JlopsParser_MissingOpKeyword_Throws()
    {
        string content = "discount(price) => price;";

        Assert.ThrowsExactly<FormatException>(() => JlopsParser.Parse(content));
    }

    [TestMethod]
    public void JlopsParser_MissingParentheses_Throws()
    {
        string content = "op discount => price;";

        Assert.ThrowsExactly<FormatException>(() => JlopsParser.Parse(content));
    }

    [TestMethod]
    public void JlopsParser_UnmatchedBrace_Throws()
    {
        string content = """
            op broken(x)
            {
                return x;
            """;

        Assert.ThrowsExactly<FormatException>(() => JlopsParser.Parse(content));
    }

    // ─── Code Generator with Custom Operators ───────────────────

    [TestMethod]
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

        StringAssert.Contains(result, "CustomOp_double_it(");
        StringAssert.Contains(result, "CoerceToBigNumber(x) * (BigNumber)2");
    }

    [TestMethod]
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

        StringAssert.Contains(result, "CustomOp_clamp(");
        StringAssert.Contains(result, "BigNumber v = CoerceToBigNumber(value);");
    }

    [TestMethod]
    public void Generate_CustomOperator_WrongArgCount_Throws()
    {
        List<CustomOperator> customOps =
        [
            new("double_it", new[] { "x" }, "x", isExpression: true),
        ];

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            JsonLogicCodeGenerator.Generate(
                """{"double_it":[1, 2]}""",
                "BadRule",
                "Test.Generated",
                customOps));
    }

    [TestMethod]
    public void Generate_UnknownOperator_StillThrows()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            JsonLogicCodeGenerator.Generate(
                """{"nonexistent":[1]}""",
                "BadRule",
                "Test.Generated",
                null));
    }

    [TestMethod]
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

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int idx = 0;
        while ((idx = source.IndexOf(value, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += value.Length;
        }

        return count;
    }
}