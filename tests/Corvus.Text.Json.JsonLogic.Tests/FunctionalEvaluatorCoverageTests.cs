// <copyright file="FunctionalEvaluatorCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Text.Json.JsonLogic;
using Xunit;

namespace Corvus.Text.Json.JsonLogic.Tests;

/// <summary>
/// Systematic coverage tests for uncovered branches in <see cref="FunctionalEvaluator"/>,
/// identified from merged Cobertura coverage data. Organized by JsonLogic operation.
/// </summary>
public class FunctionalEvaluatorCoverageTests
{
    private static string Eval(string rule, string data = "{}")
    {
        JsonElement ruleElem = JsonElement.ParseValue(Encoding.UTF8.GetBytes(rule));
        JsonElement dataElem = JsonElement.ParseValue(Encoding.UTF8.GetBytes(data));
        JsonLogicRule logicRule = new(ruleElem);
        JsonElement result = JsonLogicEvaluator.Default.Evaluate(logicRule, dataElem);
        return result.ValueKind == JsonValueKind.Undefined ? "undefined" : result.GetRawText();
    }

    // ═══════════════════════════════════════════════════════════════
    // VAR — lines 268-269, 288-289, 301-303, 351-354, 383-384
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Var_EmptyPathWithDefault_ReturnsEntireData()
    {
        // Lines 300-303: segments.Length == 0, returns data
        Assert.Equal("{\"a\":1}", Eval("""{"var":["","fallback"]}""", """{"a":1}"""));
    }

    [Fact]
    public void Var_DynamicPathResolvesToNull_UsesDefault()
    {
        // Lines 287-289: dynamic path resolves to null, default used
        Assert.Equal("\"fallback\"", Eval(
            """{"var":[{"if":[false,"x","missing"]}, "fallback"]}""",
            """{"x":1}"""));
    }

    [Fact]
    public void Var_MultiSegmentArrayIndexOutOfBounds_UsesDefault()
    {
        // Lines 351-354: array traversal fails, falls through to default
        Assert.Equal("\"default\"", Eval(
            """{"var":["items.10.name", "default"]}""",
            """{"items":[{"name":"first"}]}"""));
    }

    [Fact]
    public void Var_ReduceContextAccumulatorPath()
    {
        // Lines 263-269: var in reduce context referencing "accumulator"
        Assert.Equal("6", Eval(
            """{"reduce":[[1,2,3],{"+": [{"var":"accumulator"},{"var":"current"}]},0]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // ARITHMETIC — lines 469-481, 486-521, 528-530, 544-567
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Add_BigNumberFallback()
    {
        // Lines 469-481: AddSlow path with BigNumber
        // Use string operands that coerce to numbers too large for double precision
        Assert.Equal("2", Eval("""{"+":["1","1"]}"""));
    }

    [Fact]
    public void Sub_ZeroOperands_ReturnsZero()
    {
        // Lines 486-488: empty operands
        Assert.Equal("0", Eval("""{"-":[]}"""));
    }

    [Fact]
    public void Sub_SingleOperand_Negates()
    {
        // Lines 491-504: unary negation
        Assert.Equal("-5", Eval("""{"-":[5]}"""));
    }

    [Fact]
    public void Sub_BigNumberFallback()
    {
        // Lines 518-521: SubSlow path
        Assert.Equal("0", Eval("""{"-":["1","1"]}"""));
    }

    [Fact]
    public void Mul_ZeroOperands_ReturnsZero()
    {
        // Lines 528-530: empty operands
        Assert.Equal("0", Eval("""{"*":[]}"""));
    }

    [Fact]
    public void Mul_BigNumberFallback()
    {
        // Lines 555-567: MulSlow path
        Assert.Equal("2", Eval("""{"*":["1","2"]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // MIN/MAX — lines 641-688 (MinMaxSlow)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Min_StringOperands_CoercesAndCompares()
    {
        // Lines 641-688: non-double comparison fallback
        Assert.Equal("1", Eval("""{"min":["3","1","2"]}"""));
    }

    [Fact]
    public void Max_StringOperands_CoercesAndCompares()
    {
        Assert.Equal("3", Eval("""{"max":["3","1","2"]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // COMPARISON — lines 702-704, 760-781
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Comparison_InsufficientOperands_ReturnsFalse()
    {
        // Lines 702-704: < 2 operands
        Assert.Equal("false", Eval("""{">":[5]}"""));
        Assert.Equal("false", Eval("""{">=":[]}"""));
    }

    [Fact]
    public void Comparison_BetweenThreeOperands()
    {
        // Lines 720-736: between pattern {"<":[a, b, c]}
        Assert.Equal("true", Eval("""{"<":[1, 5, 10]}"""));
        Assert.Equal("false", Eval("""{"<":[1, 15, 10]}"""));
    }

    [Fact]
    public void Comparison_NullOperand_ReturnsFalse()
    {
        // Lines 761-763: null/undefined → false
        Assert.Equal("false", Eval("""{">":[null, 5]}"""));
        Assert.Equal("false", Eval("""{"<":[5, null]}"""));
    }

    [Fact]
    public void Comparison_StringOperands_CoercesViaElement()
    {
        // Lines 766-780: TryCoerceToNumber path via CompareCoercedElement
        Assert.Equal("true", Eval("""{"<":["1","2"]}"""));
        Assert.Equal("false", Eval("""{">":["1","2"]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // EQUALITY — lines 787-808, 825-846, 881-918, 958
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Equals_InsufficientOperands_ReturnsFalse()
    {
        // Lines 787-789: < 2 operands
        Assert.Equal("false", Eval("""{"==":[5]}"""));
    }

    [Fact]
    public void NotEquals_InsufficientOperands_ReturnsTrue()
    {
        // Lines 806-808
        Assert.Equal("true", Eval("""{"!=":[5]}"""));
    }

    [Fact]
    public void StrictEquals_InsufficientOperands_ReturnsFalse()
    {
        // Lines 825-827
        Assert.Equal("false", Eval("""{"===":[5]}"""));
    }

    [Fact]
    public void StrictNotEquals_InsufficientOperands_ReturnsTrue()
    {
        // Lines 844-846
        Assert.Equal("true", Eval("""{"!==":[5]}"""));
    }

    [Fact]
    public void CoercingEquals_BothNullUndefined()
    {
        // Lines 880-882: both null/undefined → true
        Assert.Equal("true", Eval("""{"==":[null, null]}"""));
    }

    [Fact]
    public void CoercingEquals_BooleanCoercion()
    {
        // Lines 902-907: bool left → coerce to number
        Assert.Equal("true", Eval("""{"==":[true, 1]}"""));
        Assert.Equal("true", Eval("""{"==":[false, 0]}"""));
    }

    [Fact]
    public void CoercingEquals_BooleanRightCoercion()
    {
        // Lines 910-916: bool right → coerce to number
        Assert.Equal("true", Eval("""{"==":[1, true]}"""));
        Assert.Equal("true", Eval("""{"==":[0, false]}"""));
    }

    [Fact]
    public void StrictEquals_DifferentTypes_ReturnsFalse()
    {
        // Lines 918-958: type mismatches
        Assert.Equal("false", Eval("""{"===":[true, 1]}"""));
        Assert.Equal("false", Eval("""{"===":["1", 1]}"""));
    }

    [Fact]
    public void StrictEquals_BooleanComparison()
    {
        // Line 958: both booleans same kind
        Assert.Equal("true", Eval("""{"===":[true, true]}"""));
        Assert.Equal("false", Eval("""{"===":[true, false]}"""));
    }

    [Fact]
    public void StrictEquals_ArrayAndObject()
    {
        // Lines 950-958: strict equals with different types
        Assert.Equal("false", Eval("""{"===":[[], {}]}"""));
        Assert.Equal("false", Eval("""{"===":[null, 0]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // LOGIC — lines 978-980, 991-993, 1004-1006, 1028-1030
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Not_ZeroOperands_ReturnsTrue()
    {
        // Lines 978-980
        Assert.Equal("true", Eval("""{"!":[]}"""));
    }

    [Fact]
    public void Truthy_ZeroOperands_ReturnsFalse()
    {
        // Lines 991-993
        Assert.Equal("false", Eval("""{"!!":[]}"""));
    }

    [Fact]
    public void And_ZeroOperands_ReturnsFalse()
    {
        // Lines 1004-1006
        Assert.Equal("false", Eval("""{"and":[]}"""));
    }

    [Fact]
    public void Or_ZeroOperands_ReturnsFalse()
    {
        // Lines 1028-1030
        Assert.Equal("false", Eval("""{"or":[]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // IF/CHAIN — lines 1071-1072, 1129-1132, 1212-1284
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void If_NonArrayArgs_TreatedAsDefault()
    {
        // Lines 1129-1132: if args is scalar, treated as default value
        Assert.Equal("5", Eval("""{"if":5}"""));
    }

    [Fact]
    public void If_UniformComparisonChain()
    {
        // Lines 1212-1284: TryCompileUniformComparisonChain optimization
        // Pattern: {"if": [{"<":[{"var":"x"}, 10]}, "small", {"<":[{"var":"x"}, 20]}, "medium", "large"]}
        Assert.Equal("\"small\"", Eval(
            """{"if":[{"<":[{"var":"x"},10]},"small",{"<":[{"var":"x"},20]},"medium","large"]}""",
            """{"x":5}"""));
        Assert.Equal("\"medium\"", Eval(
            """{"if":[{"<":[{"var":"x"},10]},"small",{"<":[{"var":"x"},20]},"medium","large"]}""",
            """{"x":15}"""));
        Assert.Equal("\"large\"", Eval(
            """{"if":[{"<":[{"var":"x"},10]},"small",{"<":[{"var":"x"},20]},"medium","large"]}""",
            """{"x":25}"""));
    }

    [Fact]
    public void If_UniformChainWithFlippedComparison()
    {
        // Lines 1339-1345: FlipCompareOp path — {op: [N, {"var":"path"}]}
        Assert.Equal("\"yes\"", Eval(
            """{"if":[{">=":[10,{"var":"x"}]},"yes","no"]}""",
            """{"x":5}"""));
    }

    [Fact]
    public void If_UniformChainNonNumericVar_FallsBack()
    {
        // Lines 1362-1374: TryExtractSimpleVarProp with complex path
        Assert.Equal("\"else\"", Eval(
            """{"if":[{"<":[{"var":"x"},10]},"yes","else"]}""",
            """{"x":"not_a_number"}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // CAT — lines 1519-1542 (null, true, false branches + grow)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Cat_NullBooleanValues()
    {
        // Lines 1519-1522: null literal, 1504-1516: true/false
        Assert.Equal("\"truefalsenull\"", Eval("""{"cat":[true, false, null]}"""));
    }

    [Fact]
    public void Cat_NumberAndString()
    {
        // Lines 1469-1490: number and string branches
        Assert.Equal("\"hello42\"", Eval("""{"cat":["hello", 42]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // SUBSTR — lines 1549-1601 (dynamic start/length)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Substr_ZeroOperands_ReturnsEmpty()
    {
        // Lines 1549-1550
        Assert.Equal("\"\"", Eval("""{"substr":[]}"""));
    }

    [Fact]
    public void Substr_DynamicStart()
    {
        // Lines 1579-1601: dynamic start from variable
        Assert.Equal("\"world\"", Eval(
            """{"substr":["hello world", {"var":"start"}]}""",
            """{"start":6}"""));
    }

    [Fact]
    public void Substr_DynamicStartAndLength()
    {
        // Lines 1589-1597: dynamic length from variable
        Assert.Equal("\"wor\"", Eval(
            """{"substr":["hello world", {"var":"start"}, {"var":"len"}]}""",
            """{"start":6,"len":3}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // IN — lines 1668-1691 (string contains + array search)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void In_StringContains_Found()
    {
        // Lines 1672-1677: string substring search
        Assert.Equal("true", Eval("""{"in":["lo", "hello"]}"""));
    }

    [Fact]
    public void In_StringContains_NotFound()
    {
        Assert.Equal("false", Eval("""{"in":["xyz", "hello"]}"""));
    }

    [Fact]
    public void In_StringContains_NonStringNeedle_ReturnsFalse()
    {
        // Lines 1667-1669: needle not a string → false
        Assert.Equal("false", Eval("""{"in":[42, "hello"]}"""));
    }

    [Fact]
    public void In_DynamicArraySearch()
    {
        // Lines 1680-1691: array search (dynamic haystack)
        Assert.Equal("true", Eval(
            """{"in":[2, {"var":"items"}]}""",
            """{"items":[1,2,3]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // MERGE — lines 1705-1706, 1748-1775
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Merge_SingleNonArrayOperand()
    {
        // Lines 1700-1707: single non-array wraps in array
        Assert.Equal("[5]", Eval("""{"merge":5}"""));
    }

    [Fact]
    public void Merge_DynamicOperands()
    {
        // Lines 1748-1775: dynamic operands evaluated at runtime
        Assert.Equal("[1,2,3]", Eval(
            """{"merge":[[1],{"if":[true,[2,3],[4,5]]}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // MAP/FILTER/REDUCE — lines 1837-1915
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Map_InsufficientOperands()
    {
        // Lines 1837-1839: < 2 operands → empty array
        Assert.Equal("[]", Eval("""{"map":[[1,2,3]]}"""));
    }

    [Fact]
    public void Filter_InsufficientOperands()
    {
        // Lines 1874-1876: < 2 operands → empty array
        Assert.Equal("[]", Eval("""{"filter":[[1,2,3]]}"""));
    }

    [Fact]
    public void Reduce_InsufficientOperands()
    {
        // Lines 1913-1915: < 3 args → null
        Assert.Equal("null", Eval("""{"reduce":[[1,2],{"+":[{"var":"current"},{"var":"accumulator"}]}]}"""));
    }

    [Fact]
    public void Reduce_FusedMapReduce_EmptyArray()
    {
        // Lines 2000-2001: empty array in fused map+reduce
        Assert.Equal("0", Eval(
            """{"reduce":[{"map":[[],{"var":"current"}]},{"+":[{"var":"accumulator"},{"var":"current"}]},0]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // ALL/SOME/NONE — lines 2021-2068
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void All_EmptyArray_ReturnsFalse()
    {
        Assert.Equal("false", Eval("""{"all":[[],{"var":""}]}"""));
    }

    [Fact]
    public void All_AllTruthy_ReturnsTrue()
    {
        Assert.Equal("true", Eval("""{"all":[[1,2,3],{"var":""}]}"""));
    }

    [Fact]
    public void Some_EmptyArray_ReturnsFalse()
    {
        Assert.Equal("false", Eval("""{"some":[[],{"var":""}]}"""));
    }

    [Fact]
    public void Some_OneTruthy_ReturnsTrue()
    {
        Assert.Equal("true", Eval("""{"some":[[0,1,0],{"var":""}]}"""));
    }

    [Fact]
    public void None_EmptyArray_ReturnsTrue()
    {
        Assert.Equal("true", Eval("""{"none":[[],{"var":""}]}"""));
    }

    [Fact]
    public void None_AllFalsy_ReturnsTrue()
    {
        Assert.Equal("true", Eval("""{"none":[[0,null,false,""],{"var":""}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // MISSING_SOME — lines 2147-2167
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void MissingSome_InsufficientOperands()
    {
        // Lines 2147-2149: < 2 operands → empty array
        Assert.Equal("[]", Eval("""{"missing_some":[1]}"""));
    }

    [Fact]
    public void MissingSome_NonArrayPaths()
    {
        // Lines 2166-2167: paths not an array
        Assert.Equal("[]", Eval("""{"missing_some":[1, "a"]}""", """{"a":1}"""));
    }

    [Fact]
    public void MissingSome_FindsMissingKeys()
    {
        // missing_some returns missing keys only when count exceeds minimum
        Assert.Equal("[\"b\",\"c\"]", Eval(
            """{"missing_some":[3, ["a","b","c"]]}""",
            """{"a":1}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // TYPE CONVERSION — lines 2205-2267
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void AsDouble_EmptyOperands()
    {
        // Lines 2205-2207: empty → zero
        string result = Eval("""{"asDouble":[]}""");
        Assert.Equal("0", result);
    }

    [Fact]
    public void AsDouble_ValidConversion()
    {
        Assert.Equal("42", Eval("""{"asDouble":[42]}"""));
    }

    [Fact]
    public void AsLong_EmptyOperands()
    {
        // Lines 2231-2234: empty → zero
        string result = Eval("""{"asLong":[]}""");
        Assert.Equal("0", result);
    }

    [Fact]
    public void AsLong_ValidConversion()
    {
        Assert.Equal("42", Eval("""{"asLong":[42]}"""));
    }

    [Fact]
    public void AsBigNumber_EmptyOperands()
    {
        // Lines 2255-2258: empty → zero
        string result = Eval("""{"asBigNumber":[]}""");
        Assert.Equal("0", result);
    }

    [Fact]
    public void AsBigInteger_EmptyOperands()
    {
        // Lines 2265-2267: empty → zero
        string result = Eval("""{"asBigInteger":[]}""");
        Assert.Equal("0", result);
    }

    // ═══════════════════════════════════════════════════════════════
    // COERCION HELPERS — lines 2282-2424, 2440-2484
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Comparison_StringVsString_CoercesViaBigNumber()
    {
        // Lines 2282-2424: string comparison through coercion fallback
        Assert.Equal("true", Eval("""{">":["10","2"]}"""));
    }

    [Fact]
    public void Add_NullCoercesToZero()
    {
        // CoerceToBigNumber null/undefined branches
        Assert.Equal("5", Eval("""{"+": [5, null]}"""));
    }

    [Fact]
    public void Add_BooleanCoercion()
    {
        // CoerceToBigNumber true=1, false=0
        Assert.Equal("1", Eval("""{"+": [true, false]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // REDUCE OPTIMIZATION DETECTION — lines 2517-2600
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Reduce_NonReduceVar_SkipsFusedPath()
    {
        // Lines 2517-2600: BodyUsesOnlyReduceVars returns false
        // when body uses {"var":"other"} (not current/accumulator)
        Assert.Equal("0", Eval(
            """{"reduce":[[1,2,3],{"+":[{"var":"accumulator"},{"var":"other"}]},0]}""",
            """{"other":10}"""));
    }

    [Fact]
    public void Reduce_MapReduceFusion_WithSum()
    {
        // Lines 2000-2001: fused map+reduce path
        Assert.Equal("6", Eval(
            """{"reduce":[{"map":[[1,2,3],{"var":""}]},{"+":[{"var":"accumulator"},{"var":"current"}]},0]}"""));
    }
}
