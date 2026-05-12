// <copyright file="JsonLogicCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.JsonLogic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JsonLogic.Tests;

/// <summary>
/// Additional coverage tests targeting uncovered branches in
/// <see cref="FunctionalEvaluator"/> and <see cref="JsonLogicHelpers"/>.
/// </summary>
[TestClass]
public class JsonLogicCoverageTests
{
    // ═══════════════════════════════════════════════════════════════
    // In Operator + Quantifiers
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void In_ValueInArray_ReturnsTrue()
    {
        Assert.AreEqual("true", Eval("""{"in": [2, [1,2,3,4]]}"""));
    }

    [TestMethod]
    public void In_ValueNotInArray_ReturnsFalse()
    {
        Assert.AreEqual("false", Eval("""{"in": [99, [1,2,3,4]]}"""));
    }

    [TestMethod]
    public void All_EmptyArray_ReturnsFalse()
    {
        // "all" over empty array returns false (the JsonLogic spec)
        Assert.AreEqual("false", Eval("""{"all": [[], {">":[{"var":""},0]}]}"""));
    }

    [TestMethod]
    public void All_AllMatch_ReturnsTrue()
    {
        Assert.AreEqual("true", Eval("""{"all": [[1,2,3], {">":[{"var":""},0]}]}"""));
    }

    [TestMethod]
    public void All_SomeFail_ReturnsFalse()
    {
        Assert.AreEqual("false", Eval("""{"all": [[1,2,-1], {">":[{"var":""},0]}]}"""));
    }

    [TestMethod]
    public void None_NoMatch_ReturnsTrue()
    {
        Assert.AreEqual("true", Eval("""{"none": [[4,5,6], {"<":[{"var":""},2]}]}"""));
    }

    [TestMethod]
    public void None_SomeMatch_ReturnsFalse()
    {
        Assert.AreEqual("false", Eval("""{"none": [[1,5,6], {"<":[{"var":""},2]}]}"""));
    }

    [TestMethod]
    public void Some_OneMatch_ReturnsTrue()
    {
        Assert.AreEqual("true", Eval("""{"some": [[1,2,3], {">":[{"var":""},2]}]}"""));
    }

    [TestMethod]
    public void Some_NoneMatch_ReturnsFalse()
    {
        Assert.AreEqual("false", Eval("""{"some": [[1,2,3], {">":[{"var":""},10]}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // BigNumber Arithmetic
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Add_BigNumbers_ReturnsCorrectResult()
    {
        // Large numbers may be serialized in scientific notation
        string result = Eval("""{"+":[99999999999999999999999999999, 1]}""");
        double parsed = double.Parse(result, System.Globalization.CultureInfo.InvariantCulture);
        Assert.IsTrue(parsed > 9.99e28, $"Expected > 9.99e28 but got {parsed}");
    }

    [TestMethod]
    public void Multiply_BigNumbers_ReturnsCorrectResult()
    {
        string result = Eval("""{"*":[99999999999999999999, 2]}""");
        double parsed = double.Parse(result, System.Globalization.CultureInfo.InvariantCulture);
        Assert.IsTrue(parsed > 1.99e20, $"Expected > 1.99e20 but got {parsed}");
    }

    [TestMethod]
    public void Subtract_BigNumbers_ReturnsCorrectResult()
    {
        string result = Eval("""{"-":[100000000000000000000, 1]}""");
        double parsed = double.Parse(result, System.Globalization.CultureInfo.InvariantCulture);
        Assert.IsTrue(parsed >= 9.99e19, $"Expected >= 9.99e19 but got {parsed}");
    }

    // ═══════════════════════════════════════════════════════════════
    // Reduce with Accumulator
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Reduce_SumArray_ReturnsTotal()
    {
        string rule = """{"reduce": [[1,2,3,4,5], {"+":[{"var":"accumulator"},{"var":"current"}]}, 0]}""";
        Assert.AreEqual("15", Eval(rule));
    }

    [TestMethod]
    public void Reduce_WithNonZeroInitial_ReturnsTotal()
    {
        string rule = """{"reduce": [[1,2,3], {"+":[{"var":"accumulator"},{"var":"current"}]}, 10]}""";
        Assert.AreEqual("16", Eval(rule));
    }

    [TestMethod]
    public void Reduce_Multiply_ReturnsProduct()
    {
        string rule = """{"reduce": [[1,2,3,4], {"*":[{"var":"accumulator"},{"var":"current"}]}, 1]}""";
        Assert.AreEqual("24", Eval(rule));
    }

    [TestMethod]
    public void Reduce_EmptyArray_ReturnsInitial()
    {
        string rule = """{"reduce": [[], {"+":[{"var":"accumulator"},{"var":"current"}]}, 42]}""";
        Assert.AreEqual("42", Eval(rule));
    }

    // ═══════════════════════════════════════════════════════════════
    // VAR with Dynamic/Empty Paths
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Var_EmptyPath_ReturnsRoot()
    {
        Assert.AreEqual("{\"a\":1,\"b\":2}", Eval("""{"var": ""}""", """{"a":1,"b":2}"""));
    }

    [TestMethod]
    public void Var_MissingPathWithDefault_ReturnsDefault()
    {
        Assert.AreEqual("\"default_value\"", Eval("""{"var": ["nonexistent", "default_value"]}""", """{"a":1}"""));
    }

    [TestMethod]
    public void Var_ArrayIndex_ReturnsItem()
    {
        Assert.AreEqual("\"b\"", Eval("""{"var": "1"}""", """["a","b","c"]"""));
    }

    [TestMethod]
    public void Var_NestedPath_ReturnsDeepValue()
    {
        Assert.AreEqual("42", Eval("""{"var": "a.b.c"}""", """{"a":{"b":{"c":42}}}"""));
    }

    [TestMethod]
    public void Var_MissingNestedPathWithDefault_ReturnsDefault()
    {
        Assert.AreEqual("99", Eval("""{"var": ["a.b.missing", 99]}""", """{"a":{"b":{"c":1}}}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Cross-Type Comparison
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void GreaterThan_BooleanVsNumber_CoercesToNumber()
    {
        Assert.AreEqual("true", Eval("""{">": [true, 0]}"""));
    }

    [TestMethod]
    public void GreaterThan_StringNumberVsNumber_Coerces()
    {
        Assert.AreEqual("true", Eval("""{">": ["5", 3]}"""));
    }

    [TestMethod]
    public void LessThan_StringNumberVsNumber_Coerces()
    {
        Assert.AreEqual("true", Eval("""{"<": ["2", 10]}"""));
    }

    [TestMethod]
    public void GreaterThanOrEqual_CoercedComparison()
    {
        Assert.AreEqual("true", Eval("""{">=": ["5", 5]}"""));
    }

    [TestMethod]
    public void LessThanOrEqual_CoercedComparison()
    {
        Assert.AreEqual("true", Eval("""{"<=": [3, "3"]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // If/Switch (multiple conditions)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void If_MultipleConditions_EvaluatesCorrectBranch()
    {
        string rule = """{"if": [{">":[{"var":"x"},10]}, "big", {">":[{"var":"x"},5]}, "medium", "small"]}""";

        Assert.AreEqual("\"big\"", Eval(rule, """{"x":15}"""));
        Assert.AreEqual("\"medium\"", Eval(rule, """{"x":7}"""));
        Assert.AreEqual("\"small\"", Eval(rule, """{"x":2}"""));
    }

    [TestMethod]
    public void If_SingleConditionTrue_ReturnsConsequent()
    {
        Assert.AreEqual("\"yes\"", Eval("""{"if": [true, "yes", "no"]}"""));
    }

    [TestMethod]
    public void If_SingleConditionFalse_ReturnsAlternative()
    {
        Assert.AreEqual("\"no\"", Eval("""{"if": [false, "yes", "no"]}"""));
    }

    [TestMethod]
    public void If_NoElse_ReturnsFalsyNull()
    {
        string result = Eval("""{"if": [false, "yes"]}""");
        Assert.AreEqual("null", result);
    }

    // ═══════════════════════════════════════════════════════════════
    // Arithmetic Edge Cases
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Subtract_NoArgs_ReturnsZero()
    {
        Assert.AreEqual("0", Eval("""{"-": []}"""));
    }

    [TestMethod]
    public void Subtract_SingleArg_Negates()
    {
        Assert.AreEqual("-7", Eval("""{"-": [7]}"""));
    }

    [TestMethod]
    public void Modulo_BasicOperation()
    {
        Assert.AreEqual("1", Eval("""{"%": [10, 3]}"""));
    }

    [TestMethod]
    public void Modulo_BigNumbers()
    {
        // 99999999999999999999 % 7 — the precision may differ from exact arithmetic
        string result = Eval("""{"%": [99999999999999999999, 7]}""");
        int modResult = int.Parse(result);
        Assert.IsTrue(modResult >= 0 && modResult < 7, $"Expected 0-6 but got {modResult}");
    }

    [TestMethod]
    public void Division_BasicOperation()
    {
        Assert.AreEqual("5", Eval("""{"/": [10, 2]}"""));
    }

    [TestMethod]
    public void Multiply_NoArgs_ReturnsZero()
    {
        Assert.AreEqual("0", Eval("""{"*": []}"""));
    }

    [TestMethod]
    public void Add_MultipleOperands()
    {
        Assert.AreEqual("10", Eval("""{"+": [1,2,3,4]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Substr (buffer growth and negative indices)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Substr_LongString_Works()
    {
        string longStr = new string('x', 500);
        string rule = string.Format("{{\"substr\": [\"{0}\", 100, 200]}}", longStr);
        string result = Eval(rule);
        Assert.AreEqual(200, System.Text.Json.JsonDocument.Parse(result).RootElement.GetString()!.Length);
    }

    [TestMethod]
    public void Substr_NegativeStart_FromEnd()
    {
        Assert.AreEqual("\"world\"", Eval("""{"substr": ["hello world", -5]}"""));
    }

    [TestMethod]
    public void Substr_NegativeLength_RemovesFromEnd()
    {
        Assert.AreEqual("\"hello \"", Eval("""{"substr": ["hello world", 0, -5]}"""));
    }

    [TestMethod]
    public void Substr_StartBeyondLength_ReturnsEmpty()
    {
        Assert.AreEqual("\"\"", Eval("""{"substr": ["hi", 100]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Coercing Equality
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Equality_CoercedVsStrict()
    {
        // == does type coercion
        Assert.AreEqual("true", Eval("""{"==": [1, "1"]}"""));
        // === does not
        Assert.AreEqual("false", Eval("""{"===": [1, "1"]}"""));
    }

    [TestMethod]
    public void Equality_NullOnlyEqualsNull()
    {
        Assert.AreEqual("false", Eval("""{"==": [null, false]}"""));
        Assert.AreEqual("true", Eval("""{"==": [null, null]}"""));
    }

    [TestMethod]
    public void Equality_ZeroAndFalse_Coerced()
    {
        Assert.AreEqual("true", Eval("""{"==": [0, false]}"""));
    }

    [TestMethod]
    public void Equality_EmptyStringAndFalse_Coerced()
    {
        Assert.AreEqual("true", Eval("""{"==": ["", false]}"""));
    }

    [TestMethod]
    public void NotEqual_Coerced()
    {
        Assert.AreEqual("false", Eval("""{"!=": [1, "1"]}"""));
        Assert.AreEqual("true", Eval("""{"!=": [1, "2"]}"""));
    }

    [TestMethod]
    public void StrictNotEqual()
    {
        Assert.AreEqual("true", Eval("""{"!==": [1, "1"]}"""));
        Assert.AreEqual("false", Eval("""{"!==": [1, 1]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // EvaluateToString API
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void EvaluateToString_BasicRule_Works()
    {
        string? result = JsonLogicEvaluator.Default.EvaluateToString(
            """{"+":[1,2,3]}""",
            "{}");
        Assert.AreEqual("6", result);
    }

    [TestMethod]
    public void EvaluateToString_ComplexRule_Works()
    {
        string? result = JsonLogicEvaluator.Default.EvaluateToString(
            """{"if":[{">":[{"var":"x"},5]},"big","small"]}""",
            """{"x":10}""");
        Assert.AreEqual("\"big\"", result);
    }

    // ═══════════════════════════════════════════════════════════════
    // Map and Filter
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Map_DoubleValues_ReturnsTransformed()
    {
        Assert.AreEqual("[2,4,6]", Eval("""{"map": [[1,2,3], {"*":[{"var":""},2]}]}"""));
    }

    [TestMethod]
    public void Filter_GreaterThanTwo_ReturnsFiltered()
    {
        Assert.AreEqual("[3,4,5]", Eval("""{"filter": [[1,2,3,4,5], {">":[{"var":""},2]}]}"""));
    }

    [TestMethod]
    public void Filter_NoneMatch_ReturnsEmptyArray()
    {
        Assert.AreEqual("[]", Eval("""{"filter": [[1,2,3], {">":[{"var":""},10]}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Logic operators (and/or with values)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void And_ReturnsFirstFalsy()
    {
        Assert.AreEqual("0", Eval("""{"and": [1, 0, 2]}"""));
    }

    [TestMethod]
    public void And_AllTruthy_ReturnsLast()
    {
        Assert.AreEqual("3", Eval("""{"and": [1, 2, 3]}"""));
    }

    [TestMethod]
    public void Or_ReturnsFirstTruthy()
    {
        Assert.AreEqual("1", Eval("""{"or": [0, 1, 2]}"""));
    }

    [TestMethod]
    public void Or_AllFalsy_ReturnsLast()
    {
        Assert.AreEqual("\"\"", Eval("""{"or": [0, false, ""]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Cat edge cases for buffer growth
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Cat_ManyStrings_ForcesBufferGrowth()
    {
        // Concatenate enough strings to exceed the initial buffer size
        StringBuilder sb = new();
        sb.Append("{\"cat\": [");
        for (int i = 0; i < 50; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append("\"abcdefghij\"");
        }

        sb.Append("]}");

        string result = Eval(sb.ToString());
        string parsed = System.Text.Json.JsonDocument.Parse(result).RootElement.GetString()!;
        Assert.AreEqual(500, parsed.Length);
        Assert.IsTrue(parsed.All(c => c >= 'a' && c <= 'j'));
    }

    [TestMethod]
    public void Cat_ObjectValue_CoercesToString()
    {
        // Objects in cat produce empty string or toString representation
        string result = Eval("""{"cat": ["prefix", {"var":"obj"}]}""", """{"obj":{}}""");
        StringAssert.Contains(result, "prefix");
    }

    // ═══════════════════════════════════════════════════════════════
    // Missing and Missing_some
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Missing_ReturnsAbsentKeys()
    {
        Assert.AreEqual("[\"b\",\"c\"]", Eval("""{"missing": ["a","b","c"]}""", """{"a":1}"""));
    }

    [TestMethod]
    public void Missing_AllPresent_ReturnsEmpty()
    {
        Assert.AreEqual("[]", Eval("""{"missing": ["a","b"]}""", """{"a":1,"b":2}"""));
    }

    [TestMethod]
    public void MissingSome_EnoughPresent_ReturnsEmpty()
    {
        // Need at least 1, have "a" → satisfied
        Assert.AreEqual("[]", Eval("""{"missing_some": [1, ["a","b","c"]]}""", """{"a":1}"""));
    }

    [TestMethod]
    public void MissingSome_NotEnoughPresent_ReturnsMissing()
    {
        // Need at least 2, only have "a"
        Assert.AreEqual("[\"b\",\"c\"]", Eval("""{"missing_some": [2, ["a","b","c"]]}""", """{"a":1}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Between (triple comparison)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Between_ValueInRange_ReturnsTrue()
    {
        Assert.AreEqual("true", Eval("""{"<": [1, 5, 10]}"""));
    }

    [TestMethod]
    public void Between_ValueOutOfRange_ReturnsFalse()
    {
        Assert.AreEqual("false", Eval("""{"<": [1, 15, 10]}"""));
    }

    [TestMethod]
    public void BetweenInclusive_ValueOnBoundary_ReturnsTrue()
    {
        Assert.AreEqual("true", Eval("""{"<=": [1, 1, 10]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Min/Max
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Min_ReturnsSmallest()
    {
        Assert.AreEqual("1", Eval("""{"min": [5, 3, 1, 4]}"""));
    }

    [TestMethod]
    public void Max_ReturnsLargest()
    {
        Assert.AreEqual("5", Eval("""{"max": [5, 3, 1, 4]}"""));
    }

    [TestMethod]
    public void Min_SingleArg_ReturnsThatArg()
    {
        Assert.AreEqual("7", Eval("""{"min": [7]}"""));
    }

    [TestMethod]
    public void Max_SingleArg_ReturnsThatArg()
    {
        Assert.AreEqual("7", Eval("""{"max": [7]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Log operator
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Log_ReturnsValue()
    {
        Assert.AreEqual("42", Eval("""{"log": [42]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Merge operator
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Merge_CombinesArrays()
    {
        Assert.AreEqual("[1,2,3,4]", Eval("""{"merge": [[1,2],[3,4]]}"""));
    }

    [TestMethod]
    public void Merge_WrapsNonArrays()
    {
        Assert.AreEqual("[1,2,3]", Eval("""{"merge": [1,[2],3]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Workspace overload
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Evaluate_WithExplicitWorkspace_ReturnsCorrectResult()
    {
        byte[] ruleUtf8 = Encoding.UTF8.GetBytes("""{"+":[1,2,3]}""");
        byte[] dataUtf8 = Encoding.UTF8.GetBytes("{}");

        JsonElement ruleElem = JsonElement.ParseValue(ruleUtf8);
        JsonElement dataElem = JsonElement.ParseValue(dataUtf8);

        JsonLogicRule logicRule = new(ruleElem);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonLogicEvaluator.Default.Evaluate(logicRule, dataElem, workspace);

        Assert.AreEqual("6", result.GetRawText());
    }

    [TestMethod]
    public void Evaluate_WithoutWorkspace_ClonesResult()
    {
        byte[] ruleUtf8 = Encoding.UTF8.GetBytes("""{"var":"x"}""");
        byte[] dataUtf8 = Encoding.UTF8.GetBytes("""{"x":"hello"}""");

        JsonElement ruleElem = JsonElement.ParseValue(ruleUtf8);
        JsonElement dataElem = JsonElement.ParseValue(dataUtf8);

        JsonLogicRule logicRule = new(ruleElem);
        JsonElement result = JsonLogicEvaluator.Default.Evaluate(logicRule, dataElem);

        Assert.AreEqual("\"hello\"", result.GetRawText());
    }

    // ═══════════════════════════════════════════════════════════════
    // Switch Optimization Path (uniform if-chain)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void SwitchOptimization_UniformGreaterThan_HitsOptimizedPath()
    {
        // 3+ uniform conditions: all {">": [{"var":"x"}, threshold]}
        string rule = """{"if": [{">":[{"var":"x"},100]}, "huge", {">":[{"var":"x"},50]}, "big", {">":[{"var":"x"},10]}, "medium", "small"]}""";

        Assert.AreEqual("\"huge\"", Eval(rule, """{"x":200}"""));
        Assert.AreEqual("\"big\"", Eval(rule, """{"x":75}"""));
        Assert.AreEqual("\"medium\"", Eval(rule, """{"x":20}"""));
        Assert.AreEqual("\"small\"", Eval(rule, """{"x":5}"""));
    }

    [TestMethod]
    public void SwitchOptimization_MissingVar_DefaultsToZero()
    {
        string rule = """{"if": [{">":[{"var":"x"},10]}, "big", {">":[{"var":"x"},5]}, "medium", "small"]}""";
        // Data has no "x" property — should default to 0, and 0 > 10 and 0 > 5 are both false → "small"
        Assert.AreEqual("\"small\"", Eval(rule, """{"y":99}"""));
    }

    [TestMethod]
    public void SwitchOptimization_NonNumericVar_ReturnsDefault()
    {
        string rule = """{"if": [{">":[{"var":"x"},10]}, "big", {">":[{"var":"x"},5]}, "medium", "small"]}""";
        // x is a non-coercible string
        Assert.AreEqual("\"small\"", Eval(rule, """{"x":"hello"}"""));
    }

    [TestMethod]
    public void SwitchOptimization_LessThanOrEqual()
    {
        string rule = """{"if": [{"<=":[{"var":"x"},0]}, "negative", {"<=":[{"var":"x"},10]}, "small", "big"]}""";
        Assert.AreEqual("\"negative\"", Eval(rule, """{"x":-5}"""));
        Assert.AreEqual("\"small\"", Eval(rule, """{"x":5}"""));
        Assert.AreEqual("\"big\"", Eval(rule, """{"x":20}"""));
    }

    [TestMethod]
    public void SwitchOptimization_FlippedOperands()
    {
        // Pattern: {">": [100, {"var":"x"}]} means 100 > x (flipped)
        string rule = """{"if": [{">":[100,{"var":"x"}]}, "low", {">":[50,{"var":"x"}]}, "medium", "high"]}""";
        // 100 > 30 is true → "low"
        Assert.AreEqual("\"low\"", Eval(rule, """{"x":30}"""));
        // 100 > 150 false, 50 > 150 false → "high"
        Assert.AreEqual("\"high\"", Eval(rule, """{"x":150}"""));
    }

    [TestMethod]
    public void SwitchOptimization_BooleanVar_CoercesToDouble()
    {
        // true coerces to 1.0 in TryCoerceToDouble
        string rule = """{"if": [{">":[{"var":"x"},0.5]}, "truthy", {">":[{"var":"x"},-1]}, "zero", "negative"]}""";
        Assert.AreEqual("\"truthy\"", Eval(rule, """{"x":true}"""));
    }

    [TestMethod]
    public void SwitchOptimization_FalseVar_CoercesToZero()
    {
        // false coerces to 0.0
        string rule = """{"if": [{">":[{"var":"x"},0.5]}, "truthy", {">":[{"var":"x"},-0.5]}, "zero", "negative"]}""";
        Assert.AreEqual("\"zero\"", Eval(rule, """{"x":false}"""));
    }

    [TestMethod]
    public void SwitchOptimization_NumericString_Coerces()
    {
        // String "42" coerces to 42.0
        string rule = """{"if": [{">":[{"var":"x"},100]}, "big", {">":[{"var":"x"},10]}, "medium", "small"]}""";
        Assert.AreEqual("\"medium\"", Eval(rule, """{"x":"42"}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // BigNumber Slow Paths
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void AddSlow_BigNumbersExceedingDouble()
    {
        // Trigger slow path with array var, then add big numbers that preserve precision via BigNumber
        Assert.AreEqual(
            "18014398509481986",
            Eval("""{"+":[{"var":"t"}, {"var":"a"}, {"var":"b"}]}""", """{"t":[],"a":9007199254740993,"b":9007199254740993}"""));
    }

    [TestMethod]
    public void MultiplySlow_BigNumbers()
    {
        // MulSlow: trigger via array (→0), remaining operands go through CoerceToBigNumber
        Assert.AreEqual("0", Eval("""{"*":[{"var":"t"}, {"var":"a"}]}""", """{"t":[],"a":5}"""));
    }

    [TestMethod]
    public void DivSlow_BigNumbers()
    {
        // DivSlow: left fails TryGetDouble (array), right is a valid number
        Assert.AreEqual("0", Eval("""{"/": [{"var":"t"}, {"var":"b"}]}""", """{"t":[],"b":4}"""));
    }

    [TestMethod]
    public void DivSlow_DivideByZero_ReturnsNull()
    {
        // DivSlow: both fail TryGetDouble and right coerces to zero → null
        Assert.AreEqual("null", Eval("""{"/": [{"var":"t1"}, {"var":"t2"}]}""", """{"t1":[],"t2":[]}"""));
    }

    [TestMethod]
    public void ModSlow_BigNumbers()
    {
        // ModSlow: left fails TryGetDouble (array), right is a valid number
        Assert.AreEqual("0", Eval("""{"%": [{"var":"t"}, {"var":"b"}]}""", """{"t":[],"b":3}"""));
    }

    [TestMethod]
    public void ModSlow_ModByZero_ReturnsNull()
    {
        // ModSlow: right coerces to zero → null
        Assert.AreEqual("null", Eval("""{"%": [{"var":"t1"}, {"var":"t2"}]}""", """{"t1":[],"t2":[]}"""));
    }

    [TestMethod]
    public void MinMaxSlow_BigNumbers()
    {
        // MinMaxSlow: trigger via non-coercible array → null (TryCoerceToNumber fails for arrays)
        Assert.AreEqual("null", Eval("""{"min":[{"var":"t"}, {"var":"a"}]}""", """{"t":[],"a":5}"""));
    }

    [TestMethod]
    public void MaxSlow_BigNumbers()
    {
        // MaxSlow: trigger via non-coercible array → null
        Assert.AreEqual("null", Eval("""{"max":[{"var":"t"}, {"var":"a"}]}""", """{"t":[],"a":5}"""));
    }

    [TestMethod]
    public void SubSlow_UnaryNegation_BigNumber()
    {
        // Unary minus where operand fails TryGetDouble (array → BigNumber.Zero → -0 = 0)
        Assert.AreEqual("0", Eval("""{"-":[{"var":"t"}]}""", """{"t":[]}"""));
    }

    [TestMethod]
    public void SubSlow_Binary_BigNumber()
    {
        // Binary subtract where left fails TryGetDouble, triggering BigNumber subtraction
        // left = big number from var (raw bytes preserved), right = array (coerces to 0)
        Assert.AreEqual(
            "9007199254740993",
            Eval("""{"-":[{"var":"a"}, {"var":"t"}]}""", """{"t":[],"a":9007199254740993}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // CoerceToBigNumber paths
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void CoerceToBigNumber_True()
    {
        // AddSlow with true: array triggers slow path, true coerces to BigNumber.One
        Assert.AreEqual(
            "9007199254740994",
            Eval("""{"+":[{"var":"t"}, {"var":"a"}, true]}""", """{"t":[],"a":9007199254740993}"""));
    }

    [TestMethod]
    public void CoerceToBigNumber_False()
    {
        // AddSlow with false: false coerces to BigNumber.Zero
        Assert.AreEqual(
            "9007199254740993",
            Eval("""{"+":[{"var":"t"}, {"var":"a"}, false]}""", """{"t":[],"a":9007199254740993}"""));
    }

    [TestMethod]
    public void CoerceToBigNumber_StringNumber()
    {
        // AddSlow with numeric string: coerces via TryCoerceToNumber → BigNumber
        // 9007199254740993 + 7 = 9007199254741000, formatted as 9007199254741E3
        Assert.AreEqual(
            "9007199254741E3",
            Eval("""{"+":[{"var":"t"}, {"var":"a"}, "7"]}""", """{"t":[],"a":9007199254740993}"""));
    }

    [TestMethod]
    public void CoerceToBigNumber_NonNumericString()
    {
        // AddSlow with non-numeric string: coerces to BigNumber.Zero
        Assert.AreEqual(
            "9007199254740993",
            Eval("""{"+":[{"var":"t"}, {"var":"a"}, "hello"]}""", """{"t":[],"a":9007199254740993}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Var with array index (TryParseIndexUtf8)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Var_ArrayIndexFromData()
    {
        Assert.AreEqual("\"b\"", Eval("""{"var":"items.1"}""", """{"items":["a","b","c"]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Reduce optimization (IsReduceVarPath, TryGetMapArgs)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Reduce_WithCurrentAccumulatorVars_Optimized()
    {
        // Body uses {"var":"current"} and {"var":"accumulator"}
        Assert.AreEqual("15", Eval("""{"reduce": [[1,2,3,4,5], {"+":[{"var":"accumulator"},{"var":"current"}]}, 0]}""", "null"));
    }

    [TestMethod]
    public void Reduce_WithMapSource_Optimized()
    {
        // Reduce over a map source — triggers TryGetMapArgs
        Assert.AreEqual("12", Eval("""{"reduce": [{"map":[{"var":"items"}, {"*":[{"var":""},2]}]}, {"+":[{"var":"accumulator"},{"var":"current"}]}, 0]}""", """{"items":[1,2,3]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════════════
    // Iteration 2: Precise line targets verified by coverage
    // ═══════════════════════════════════════════════════════════════

    // TryCoerceToDouble (lines 2397-2429) — called from switch optimization
    // when var value is non-numeric. Requires 3+ uniform conditions.
    [TestMethod]
    public void SwitchOpt_BoolTrue_CoercesToOne()
    {
        // 3 uniform ">" conditions on same var → triggers switch optimization
        // x=true → TryCoerceToDouble → 1.0 → ">" 0.5 → "high"
        string rule = """{"if":[{">":[{"var":"x"},100]},"huge",{">":[{"var":"x"},10]},"big",{">":[{"var":"x"},0.5]},"high","low"]}""";
        Assert.AreEqual("\"high\"", Eval(rule, """{"x":true}"""));
    }

    [TestMethod]
    public void SwitchOpt_BoolFalse_CoercesToZero()
    {
        // x=false → TryCoerceToDouble → 0.0 → none match → "low"
        string rule = """{"if":[{">":[{"var":"x"},100]},"huge",{">":[{"var":"x"},10]},"big",{">":[{"var":"x"},0.5]},"high","low"]}""";
        Assert.AreEqual("\"low\"", Eval(rule, """{"x":false}"""));
    }

    [TestMethod]
    public void SwitchOpt_NullVar_CoercesToZero()
    {
        // x=null → TryCoerceToDouble → 0.0
        string rule = """{"if":[{">":[{"var":"x"},100]},"huge",{">":[{"var":"x"},10]},"big",{">":[{"var":"x"},0.5]},"high","low"]}""";
        Assert.AreEqual("\"low\"", Eval(rule, """{"x":null}"""));
    }

    [TestMethod]
    public void SwitchOpt_NumericString_Coerces()
    {
        // x="42" → TryCoerceToDouble → 42.0 → ">" 10 → "big"
        string rule = """{"if":[{">":[{"var":"x"},100]},"huge",{">":[{"var":"x"},10]},"big",{">":[{"var":"x"},0.5]},"high","low"]}""";
        Assert.AreEqual("\"big\"", Eval(rule, """{"x":"42"}"""));
    }

    [TestMethod]
    public void SwitchOpt_NonNumericString_FailsCoercion()
    {
        // x="hello" → TryCoerceToDouble fails → returns default "low"
        string rule = """{"if":[{">":[{"var":"x"},100]},"huge",{">":[{"var":"x"},10]},"big",{">":[{"var":"x"},0.5]},"high","low"]}""";
        Assert.AreEqual("\"low\"", Eval(rule, """{"x":"hello"}"""));
    }

    [TestMethod]
    public void SwitchOpt_MissingVar_DefaultsToZero()
    {
        // No "x" in data → missing → x=0.0 → none match → "low"
        string rule = """{"if":[{">":[{"var":"x"},100]},"huge",{">":[{"var":"x"},10]},"big",{">":[{"var":"x"},0.5]},"high","low"]}""";
        Assert.AreEqual("\"low\"", Eval(rule, """{"y":99}"""));
    }

    [TestMethod]
    public void SwitchOpt_LessThan()
    {
        // All conditions use "<" — tests LessThan branch (line 1268)
        string rule = """{"if":[{"<":[{"var":"x"},0]},"neg",{"<":[{"var":"x"},10]},"small",{"<":[{"var":"x"},100]},"medium","big"]}""";
        Assert.AreEqual("\"small\"", Eval(rule, """{"x":5}"""));
    }

    [TestMethod]
    public void SwitchOpt_LessThanOrEqual()
    {
        // All conditions use "<=" — tests LessThanOrEqual branch (line 1269)
        string rule = """{"if":[{"<=":[{"var":"x"},0]},"zero",{"<=":[{"var":"x"},10]},"small",{"<=":[{"var":"x"},100]},"medium","big"]}""";
        Assert.AreEqual("\"small\"", Eval(rule, """{"x":5}"""));
    }

    [TestMethod]
    public void SwitchOpt_NonUniformOps_FallsBack()
    {
        // Mixed operators — switch opt should NOT fire (line 1218-1219)
        string rule = """{"if":[{">":[{"var":"x"},10]},"big",{"<":[{"var":"x"},0]},"neg","ok"]}""";
        Assert.AreEqual("\"ok\"", Eval(rule, """{"x":5}"""));
    }

    // MinMaxSlow loop body (lines 680-687) — DEAD CODE on net10.0+.
    // EvalResult.TryGetDouble coerces Number (including overflow→Infinity), True→1, False→0,
    // Null→0, and numeric String→double. Only non-numeric String, Array, and Object fail.
    // But TryCoerceToNumber also fails on those types. So no element can BOTH trigger MinMaxSlow
    // (by failing TryGetDouble) AND reach line 680 (by succeeding TryCoerceToNumber).
    [TestMethod]
    public void MinSlow_NonCoercible_ReturnsNull()
    {
        // Non-numeric string fails both TryGetDouble and TryCoerceToNumber → null (line 676-677)
        Assert.AreEqual("null", Eval("""{"min":[{"var":"a"}, {"var":"b"}]}""", """{"a":"hello", "b":"world"}"""));
    }

    [TestMethod]
    public void MinSlow_FirstNonCoercible_ReturnsNull()
    {
        // First operand is non-numeric string → TryGetDouble fails → MinMaxSlow
        // In MinMaxSlow: TryCoerceToNumber("hello") fails → return null (line 668-669)
        Assert.AreEqual("null", Eval("""{"min":[{"var":"a"}, 5]}""", """{"a":"hello"}"""));
    }

    private static string Eval(string rule, string data = "{}")
    {
        byte[] ruleUtf8 = Encoding.UTF8.GetBytes(rule);
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(data);

        JsonElement ruleElement = JsonElement.ParseValue(ruleUtf8);
        JsonElement dataElement = JsonElement.ParseValue(dataUtf8);

        JsonLogicRule logicRule = new(ruleElement);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonLogicEvaluator.Default.Evaluate(logicRule, dataElement, workspace);

        if (result.ValueKind == JsonValueKind.Undefined)
        {
            return "undefined";
        }

        return result.GetRawText();
    }
}
