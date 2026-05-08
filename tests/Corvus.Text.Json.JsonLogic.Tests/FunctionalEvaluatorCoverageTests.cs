// <copyright file="FunctionalEvaluatorCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Text.Json.JsonLogic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JsonLogic.Tests;

/// <summary>
/// Systematic coverage tests for uncovered branches in <see cref="FunctionalEvaluator"/>,
/// identified from merged Cobertura coverage data. Organized by JsonLogic operation.
/// </summary>
[TestClass]
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

    [TestMethod]
    public void Var_EmptyPathWithDefault_ReturnsEntireData()
    {
        // Lines 300-303: segments.Length == 0, returns data
        Assert.AreEqual("{\"a\":1}", Eval("""{"var":["","fallback"]}""", """{"a":1}"""));
    }

    [TestMethod]
    public void Var_DynamicPathResolvesToNull_UsesDefault()
    {
        // Lines 287-289: dynamic path resolves to null, default used
        Assert.AreEqual("\"fallback\"", Eval(
            """{"var":[{"if":[false,"x","missing"]}, "fallback"]}""",
            """{"x":1}"""));
    }

    [TestMethod]
    public void Var_MultiSegmentArrayIndexOutOfBounds_UsesDefault()
    {
        // Lines 351-354: array traversal fails, falls through to default
        Assert.AreEqual("\"default\"", Eval(
            """{"var":["items.10.name", "default"]}""",
            """{"items":[{"name":"first"}]}"""));
    }

    [TestMethod]
    public void Var_ReduceContextAccumulatorPath()
    {
        // Lines 263-269: var in reduce context referencing "accumulator"
        Assert.AreEqual("6", Eval(
            """{"reduce":[[1,2,3],{"+": [{"var":"accumulator"},{"var":"current"}]},0]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // ARITHMETIC — lines 469-481, 486-521, 528-530, 544-567
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Add_BigNumberFallback()
    {
        // Lines 469-481: AddSlow path with BigNumber
        // Use string operands that coerce to numbers too large for double precision
        Assert.AreEqual("2", Eval("""{"+":["1","1"]}"""));
    }

    [TestMethod]
    public void Sub_ZeroOperands_ReturnsZero()
    {
        // Lines 486-488: empty operands
        Assert.AreEqual("0", Eval("""{"-":[]}"""));
    }

    [TestMethod]
    public void Sub_SingleOperand_Negates()
    {
        // Lines 491-504: unary negation
        Assert.AreEqual("-5", Eval("""{"-":[5]}"""));
    }

    [TestMethod]
    public void Sub_BigNumberFallback()
    {
        // Lines 518-521: SubSlow path
        Assert.AreEqual("0", Eval("""{"-":["1","1"]}"""));
    }

    [TestMethod]
    public void Mul_ZeroOperands_ReturnsZero()
    {
        // Lines 528-530: empty operands
        Assert.AreEqual("0", Eval("""{"*":[]}"""));
    }

    [TestMethod]
    public void Mul_BigNumberFallback()
    {
        // Lines 555-567: MulSlow path
        Assert.AreEqual("2", Eval("""{"*":["1","2"]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // MIN/MAX — lines 641-688 (MinMaxSlow)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Min_StringOperands_CoercesAndCompares()
    {
        // Lines 641-688: non-double comparison fallback
        Assert.AreEqual("1", Eval("""{"min":["3","1","2"]}"""));
    }

    [TestMethod]
    public void Max_StringOperands_CoercesAndCompares()
    {
        Assert.AreEqual("3", Eval("""{"max":["3","1","2"]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // COMPARISON — lines 702-704, 760-781
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Comparison_InsufficientOperands_ReturnsFalse()
    {
        // Lines 702-704: < 2 operands
        Assert.AreEqual("false", Eval("""{">":[5]}"""));
        Assert.AreEqual("false", Eval("""{">=":[]}"""));
    }

    [TestMethod]
    public void Comparison_BetweenThreeOperands()
    {
        // Lines 720-736: between pattern {"<":[a, b, c]}
        Assert.AreEqual("true", Eval("""{"<":[1, 5, 10]}"""));
        Assert.AreEqual("false", Eval("""{"<":[1, 15, 10]}"""));
    }

    [TestMethod]
    public void Comparison_NullOperand_ReturnsFalse()
    {
        // Lines 761-763: null/undefined → false
        Assert.AreEqual("false", Eval("""{">":[null, 5]}"""));
        Assert.AreEqual("false", Eval("""{"<":[5, null]}"""));
    }

    [TestMethod]
    public void Comparison_StringOperands_CoercesViaElement()
    {
        // Lines 766-780: TryCoerceToNumber path via CompareCoercedElement
        Assert.AreEqual("true", Eval("""{"<":["1","2"]}"""));
        Assert.AreEqual("false", Eval("""{">":["1","2"]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // EQUALITY — lines 787-808, 825-846, 881-918, 958
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Equals_InsufficientOperands_ReturnsFalse()
    {
        // Lines 787-789: < 2 operands
        Assert.AreEqual("false", Eval("""{"==":[5]}"""));
    }

    [TestMethod]
    public void NotEquals_InsufficientOperands_ReturnsTrue()
    {
        // Lines 806-808
        Assert.AreEqual("true", Eval("""{"!=":[5]}"""));
    }

    [TestMethod]
    public void StrictEquals_InsufficientOperands_ReturnsFalse()
    {
        // Lines 825-827
        Assert.AreEqual("false", Eval("""{"===":[5]}"""));
    }

    [TestMethod]
    public void StrictNotEquals_InsufficientOperands_ReturnsTrue()
    {
        // Lines 844-846
        Assert.AreEqual("true", Eval("""{"!==":[5]}"""));
    }

    [TestMethod]
    public void CoercingEquals_BothNullUndefined()
    {
        // Lines 880-882: both null/undefined → true
        Assert.AreEqual("true", Eval("""{"==":[null, null]}"""));
    }

    [TestMethod]
    public void CoercingEquals_BooleanCoercion()
    {
        // Lines 902-907: bool left → coerce to number
        Assert.AreEqual("true", Eval("""{"==":[true, 1]}"""));
        Assert.AreEqual("true", Eval("""{"==":[false, 0]}"""));
    }

    [TestMethod]
    public void CoercingEquals_BooleanRightCoercion()
    {
        // Lines 910-916: bool right → coerce to number
        Assert.AreEqual("true", Eval("""{"==":[1, true]}"""));
        Assert.AreEqual("true", Eval("""{"==":[0, false]}"""));
    }

    [TestMethod]
    public void StrictEquals_DifferentTypes_ReturnsFalse()
    {
        // Lines 918-958: type mismatches
        Assert.AreEqual("false", Eval("""{"===":[true, 1]}"""));
        Assert.AreEqual("false", Eval("""{"===":["1", 1]}"""));
    }

    [TestMethod]
    public void StrictEquals_BooleanComparison()
    {
        // Line 958: both booleans same kind
        Assert.AreEqual("true", Eval("""{"===":[true, true]}"""));
        Assert.AreEqual("false", Eval("""{"===":[true, false]}"""));
    }

    [TestMethod]
    public void StrictEquals_ArrayAndObject()
    {
        // Lines 950-958: strict equals with different types
        Assert.AreEqual("false", Eval("""{"===":[[], {}]}"""));
        Assert.AreEqual("false", Eval("""{"===":[null, 0]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // LOGIC — lines 978-980, 991-993, 1004-1006, 1028-1030
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Not_ZeroOperands_ReturnsTrue()
    {
        // Lines 978-980
        Assert.AreEqual("true", Eval("""{"!":[]}"""));
    }

    [TestMethod]
    public void Truthy_ZeroOperands_ReturnsFalse()
    {
        // Lines 991-993
        Assert.AreEqual("false", Eval("""{"!!":[]}"""));
    }

    [TestMethod]
    public void And_ZeroOperands_ReturnsFalse()
    {
        // Lines 1004-1006
        Assert.AreEqual("false", Eval("""{"and":[]}"""));
    }

    [TestMethod]
    public void Or_ZeroOperands_ReturnsFalse()
    {
        // Lines 1028-1030
        Assert.AreEqual("false", Eval("""{"or":[]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // IF/CHAIN — lines 1071-1072, 1129-1132, 1212-1284
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void If_NonArrayArgs_TreatedAsDefault()
    {
        // Lines 1129-1132: if args is scalar, treated as default value
        Assert.AreEqual("5", Eval("""{"if":5}"""));
    }

    [TestMethod]
    public void If_UniformComparisonChain()
    {
        // Lines 1212-1284: TryCompileUniformComparisonChain optimization
        // Pattern: {"if": [{"<":[{"var":"x"}, 10]}, "small", {"<":[{"var":"x"}, 20]}, "medium", "large"]}
        Assert.AreEqual("\"small\"", Eval(
            """{"if":[{"<":[{"var":"x"},10]},"small",{"<":[{"var":"x"},20]},"medium","large"]}""",
            """{"x":5}"""));
        Assert.AreEqual("\"medium\"", Eval(
            """{"if":[{"<":[{"var":"x"},10]},"small",{"<":[{"var":"x"},20]},"medium","large"]}""",
            """{"x":15}"""));
        Assert.AreEqual("\"large\"", Eval(
            """{"if":[{"<":[{"var":"x"},10]},"small",{"<":[{"var":"x"},20]},"medium","large"]}""",
            """{"x":25}"""));
    }

    [TestMethod]
    public void If_UniformChainWithFlippedComparison()
    {
        // Lines 1339-1345: FlipCompareOp path — {op: [N, {"var":"path"}]}
        Assert.AreEqual("\"yes\"", Eval(
            """{"if":[{">=":[10,{"var":"x"}]},"yes","no"]}""",
            """{"x":5}"""));
    }

    [TestMethod]
    public void If_UniformChainNonNumericVar_FallsBack()
    {
        // Lines 1362-1374: TryExtractSimpleVarProp with complex path
        Assert.AreEqual("\"else\"", Eval(
            """{"if":[{"<":[{"var":"x"},10]},"yes","else"]}""",
            """{"x":"not_a_number"}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // CAT — lines 1519-1542 (null, true, false branches + grow)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Cat_NullBooleanValues()
    {
        // Lines 1519-1522: null literal, 1504-1516: true/false
        Assert.AreEqual("\"truefalsenull\"", Eval("""{"cat":[true, false, null]}"""));
    }

    [TestMethod]
    public void Cat_NumberAndString()
    {
        // Lines 1469-1490: number and string branches
        Assert.AreEqual("\"hello42\"", Eval("""{"cat":["hello", 42]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // SUBSTR — lines 1549-1601 (dynamic start/length)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Substr_ZeroOperands_ReturnsEmpty()
    {
        // Lines 1549-1550
        Assert.AreEqual("\"\"", Eval("""{"substr":[]}"""));
    }

    [TestMethod]
    public void Substr_DynamicStart()
    {
        // Lines 1579-1601: dynamic start from variable
        Assert.AreEqual("\"world\"", Eval(
            """{"substr":["hello world", {"var":"start"}]}""",
            """{"start":6}"""));
    }

    [TestMethod]
    public void Substr_DynamicStartAndLength()
    {
        // Lines 1589-1597: dynamic length from variable
        Assert.AreEqual("\"wor\"", Eval(
            """{"substr":["hello world", {"var":"start"}, {"var":"len"}]}""",
            """{"start":6,"len":3}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // IN — lines 1668-1691 (string contains + array search)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void In_StringContains_Found()
    {
        // Lines 1672-1677: string substring search
        Assert.AreEqual("true", Eval("""{"in":["lo", "hello"]}"""));
    }

    [TestMethod]
    public void In_StringContains_NotFound()
    {
        Assert.AreEqual("false", Eval("""{"in":["xyz", "hello"]}"""));
    }

    [TestMethod]
    public void In_StringContains_NonStringNeedle_ReturnsFalse()
    {
        // Lines 1667-1669: needle not a string → false
        Assert.AreEqual("false", Eval("""{"in":[42, "hello"]}"""));
    }

    [TestMethod]
    public void In_DynamicArraySearch()
    {
        // Lines 1680-1691: array search (dynamic haystack)
        Assert.AreEqual("true", Eval(
            """{"in":[2, {"var":"items"}]}""",
            """{"items":[1,2,3]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // MERGE — lines 1705-1706, 1748-1775
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Merge_SingleNonArrayOperand()
    {
        // Lines 1700-1707: single non-array wraps in array
        Assert.AreEqual("[5]", Eval("""{"merge":5}"""));
    }

    [TestMethod]
    public void Merge_DynamicOperands()
    {
        // Lines 1748-1775: dynamic operands evaluated at runtime
        Assert.AreEqual("[1,2,3]", Eval(
            """{"merge":[[1],{"if":[true,[2,3],[4,5]]}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // MAP/FILTER/REDUCE — lines 1837-1915
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Map_InsufficientOperands()
    {
        // Lines 1837-1839: < 2 operands → empty array
        Assert.AreEqual("[]", Eval("""{"map":[[1,2,3]]}"""));
    }

    [TestMethod]
    public void Filter_InsufficientOperands()
    {
        // Lines 1874-1876: < 2 operands → empty array
        Assert.AreEqual("[]", Eval("""{"filter":[[1,2,3]]}"""));
    }

    [TestMethod]
    public void Reduce_InsufficientOperands()
    {
        // Lines 1913-1915: < 3 args → null
        Assert.AreEqual("null", Eval("""{"reduce":[[1,2],{"+":[{"var":"current"},{"var":"accumulator"}]}]}"""));
    }

    [TestMethod]
    public void Reduce_FusedMapReduce_EmptyArray()
    {
        // Lines 2000-2001: empty array in fused map+reduce
        Assert.AreEqual("0", Eval(
            """{"reduce":[{"map":[[],{"var":"current"}]},{"+":[{"var":"accumulator"},{"var":"current"}]},0]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // ALL/SOME/NONE — lines 2021-2068
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void All_EmptyArray_ReturnsFalse()
    {
        Assert.AreEqual("false", Eval("""{"all":[[],{"var":""}]}"""));
    }

    [TestMethod]
    public void All_AllTruthy_ReturnsTrue()
    {
        Assert.AreEqual("true", Eval("""{"all":[[1,2,3],{"var":""}]}"""));
    }

    [TestMethod]
    public void Some_EmptyArray_ReturnsFalse()
    {
        Assert.AreEqual("false", Eval("""{"some":[[],{"var":""}]}"""));
    }

    [TestMethod]
    public void Some_OneTruthy_ReturnsTrue()
    {
        Assert.AreEqual("true", Eval("""{"some":[[0,1,0],{"var":""}]}"""));
    }

    [TestMethod]
    public void None_EmptyArray_ReturnsTrue()
    {
        Assert.AreEqual("true", Eval("""{"none":[[],{"var":""}]}"""));
    }

    [TestMethod]
    public void None_AllFalsy_ReturnsTrue()
    {
        Assert.AreEqual("true", Eval("""{"none":[[0,null,false,""],{"var":""}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // MISSING_SOME — lines 2147-2167
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void MissingSome_InsufficientOperands()
    {
        // Lines 2147-2149: < 2 operands → empty array
        Assert.AreEqual("[]", Eval("""{"missing_some":[1]}"""));
    }

    [TestMethod]
    public void MissingSome_NonArrayPaths()
    {
        // Lines 2166-2167: paths not an array
        Assert.AreEqual("[]", Eval("""{"missing_some":[1, "a"]}""", """{"a":1}"""));
    }

    [TestMethod]
    public void MissingSome_FindsMissingKeys()
    {
        // missing_some returns missing keys only when count exceeds minimum
        Assert.AreEqual("[\"b\",\"c\"]", Eval(
            """{"missing_some":[3, ["a","b","c"]]}""",
            """{"a":1}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // TYPE CONVERSION — lines 2205-2267
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void AsDouble_EmptyOperands()
    {
        // Lines 2205-2207: empty → zero
        string result = Eval("""{"asDouble":[]}""");
        Assert.AreEqual("0", result);
    }

    [TestMethod]
    public void AsDouble_ValidConversion()
    {
        Assert.AreEqual("42", Eval("""{"asDouble":[42]}"""));
    }

    [TestMethod]
    public void AsLong_EmptyOperands()
    {
        // Lines 2231-2234: empty → zero
        string result = Eval("""{"asLong":[]}""");
        Assert.AreEqual("0", result);
    }

    [TestMethod]
    public void AsLong_ValidConversion()
    {
        Assert.AreEqual("42", Eval("""{"asLong":[42]}"""));
    }

    [TestMethod]
    public void AsBigNumber_EmptyOperands()
    {
        // Lines 2255-2258: empty → zero
        string result = Eval("""{"asBigNumber":[]}""");
        Assert.AreEqual("0", result);
    }

    [TestMethod]
    public void AsBigInteger_EmptyOperands()
    {
        // Lines 2265-2267: empty → zero
        string result = Eval("""{"asBigInteger":[]}""");
        Assert.AreEqual("0", result);
    }

    // ═══════════════════════════════════════════════════════════════
    // COERCION HELPERS — lines 2282-2424, 2440-2484
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Comparison_StringVsString_CoercesViaBigNumber()
    {
        // Lines 2282-2424: string comparison through coercion fallback
        Assert.AreEqual("true", Eval("""{">":["10","2"]}"""));
    }

    [TestMethod]
    public void Add_NullCoercesToZero()
    {
        // CoerceToBigNumber null/undefined branches
        Assert.AreEqual("5", Eval("""{"+": [5, null]}"""));
    }

    [TestMethod]
    public void Add_BooleanCoercion()
    {
        // CoerceToBigNumber true=1, false=0
        Assert.AreEqual("1", Eval("""{"+": [true, false]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // REDUCE OPTIMIZATION DETECTION — lines 2517-2600
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Reduce_NonReduceVar_SkipsFusedPath()
    {
        // Lines 2517-2600: BodyUsesOnlyReduceVars returns false
        // when body uses {"var":"other"} (not current/accumulator)
        Assert.AreEqual("0", Eval(
            """{"reduce":[[1,2,3],{"+":[{"var":"accumulator"},{"var":"other"}]},0]}""",
            """{"other":10}"""));
    }

    [TestMethod]
    public void Reduce_MapReduceFusion_WithSum()
    {
        // Lines 2000-2001: fused map+reduce path
        Assert.AreEqual("6", Eval(
            """{"reduce":[{"map":[[1,2,3],{"var":""}]},{"+":[{"var":"accumulator"},{"var":"current"}]},0]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // MIN/MAX SLOW PATH — lines 672-687
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Min_SlowPath_MultipleStringOperands()
    {
        // Lines 672-684: MinMaxSlow with string operands that coerce to numbers
        // Forces slow path because string → element → TryGetDouble fails on EvalResult
        Assert.AreEqual("1", Eval("""{"min":["3","1","2"]}"""));
    }

    [TestMethod]
    public void Max_SlowPath_MultipleStringOperands()
    {
        // Lines 680-684: isMin=false branch in MinMaxSlow
        Assert.AreEqual("5", Eval("""{"max":["3","5","2"]}"""));
    }

    [TestMethod]
    public void Min_SlowPath_NonNumericInLoop_ReturnsNull()
    {
        // Lines 675-677: TryCoerceToNumber fails for second operand in loop
        Assert.AreEqual("null", Eval("""{"min":["3","abc","2"]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // COMPARISON — lines 772-780 (LessThan, LessThanOrEqual)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void LessThan_NumericComparison()
    {
        // Line 777: CompareOp.LessThan => cmp < 0
        Assert.AreEqual("true", Eval("""{"<":[1,2]}"""));
        Assert.AreEqual("false", Eval("""{"<":[2,1]}"""));
    }

    [TestMethod]
    public void LessThanOrEqual_NumericComparison()
    {
        // Line 778: CompareOp.LessThanOrEqual => cmp <= 0
        Assert.AreEqual("true", Eval("""{"<=":[2,2]}"""));
        Assert.AreEqual("true", Eval("""{"<=":[1,2]}"""));
        Assert.AreEqual("false", Eval("""{"<=":[3,2]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // FLIPPED COMPARISON — lines 1339-1398
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void If_FlippedComparison_NumberLeftVarRight()
    {
        // Lines 1339-1345: {op: [N, {"var":"path"}]} pattern flips operator
        string result = Eval(
            """{"if":[{">":[10,{"var":"x"}]},"big","small"]}""",
            """{"x":5}""");
        Assert.AreEqual("\"big\"", result);
    }

    [TestMethod]
    public void If_FlippedComparison_GreaterThanOrEqual()
    {
        // Lines 1391-1398: FlipCompareOp for >=
        string result = Eval(
            """{"if":[{">=":[10,{"var":"x"}]},"yes","no"]}""",
            """{"x":10}""");
        Assert.AreEqual("\"yes\"", result);
    }

    // ═══════════════════════════════════════════════════════════════
    // CAT — line 1519-1522 (null literal)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Cat_NullLiteral()
    {
        // Lines 1519-1522: null case in cat string building
        Assert.AreEqual("\"anullb\"", Eval("""{"cat":["a",null,"b"]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // AS_DOUBLE / AS_LONG — lines 2231-2234, 2255-2258
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void AsDouble_VarPointsToNumber_FastPath()
    {
        // Lines 2226-2228: EvalResult.TryGetDouble succeeds for Number element → fast path
        Assert.AreEqual("42", Eval(
            """{"asDouble":[{"var":"x"}]}""",
            """{"x":42}"""));
    }

    [TestMethod]
    public void AsDouble_NonNumericString_ReturnsNull()
    {
        // Lines 2231-2234: non-numeric string fails EvalResult.TryGetDouble,
        // then elem.TryGetDouble also fails → returns NullElement
        Assert.AreEqual("null", Eval(
            """{"asDouble":[{"var":"x"}]}""",
            """{"x":"hello"}"""));
    }

    [TestMethod]
    public void AsDouble_Array_ReturnsNull()
    {
        // Array fails both TryGetDouble paths → NullElement (line 2234)
        Assert.AreEqual("null", Eval(
            """{"asDouble":[{"var":"x"}]}""",
            """{"x":[1,2,3]}"""));
    }

    [TestMethod]
    public void AsLong_VarPointsToNumber_FastPath()
    {
        // Lines 2250-2252: EvalResult.TryGetDouble succeeds → fast path with (long) cast
        Assert.AreEqual("7", Eval(
            """{"asLong":[{"var":"x"}]}""",
            """{"x":7.9}"""));
    }

    [TestMethod]
    public void AsLong_NonNumericString_ReturnsNull()
    {
        // Lines 2255-2258: non-numeric string fails both TryGetDouble paths → NullElement
        Assert.AreEqual("null", Eval(
            """{"asLong":[{"var":"x"}]}""",
            """{"x":"world"}"""));
    }

    [TestMethod]
    public void AsLong_Object_ReturnsNull()
    {
        // Object fails both TryGetDouble paths → NullElement (line 2258)
        Assert.AreEqual("null", Eval(
            """{"asLong":[{"var":"x"}]}""",
            """{"x":{"a":1}}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // VAR ARRAY INDEX — lines 2340-2344
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Var_ArrayIndexNavigation()
    {
        // Lines 2341-2343: array index path segment resolves correctly
        Assert.AreEqual("\"b\"", Eval("""{"var":"items.1"}""", """{"items":["a","b","c"]}"""));
    }

    [TestMethod]
    public void Var_ArrayIndexOutOfBounds_ReturnsNull()
    {
        // Lines 2341-2347: array index out of bounds → null
        Assert.AreEqual("null", Eval("""{"var":"items.5"}""", """{"items":["a","b","c"]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // DOUBLE TO ELEMENT — lines 2476-2480
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Arithmetic_ProducesDoubleElement()
    {
        // Lines 2476-2480: DoubleToElement called via arithmetic that produces non-integer
        Assert.AreEqual("2.5", Eval("""{"/": [5, 2]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // ROUND 2 — Remaining uncovered lines identified from fresh Cobertura data
    // ═══════════════════════════════════════════════════════════════

    // ─── EMPTY OBJECT LITERAL (L200) ─────────────────────────────
    [TestMethod]
    public void EmptyObject_TreatedAsLiteral()
    {
        // L200: empty {} object is not an operator — compiled as literal
        Assert.AreEqual("{}", Eval("""{}"""));
    }

    // ─── CUSTOM OPERATORS via Compile overload (L43-45) ──────────
    [TestMethod]
    public void CustomOperator_InvokedViaConstructor()
    {
        // L43-45: Compile(rule) with custom operators
        var customOps = new Dictionary<string, IOperatorCompiler>
        {
            ["triple"] = new TripleCompiler(),
        };

        var evaluator = new JsonLogicEvaluator(customOps);
        JsonElement rule = JsonElement.ParseValue("""{"triple":[5]}"""u8);
        JsonElement data = JsonElement.ParseValue("{}"u8);
        JsonElement result = evaluator.Evaluate(new JsonLogicRule(rule), data);
        Assert.AreEqual(15, result.GetDouble());
    }

    private sealed class TripleCompiler : IOperatorCompiler
    {
        public RuleEvaluator Compile(RuleEvaluator[] operands)
        {
            RuleEvaluator operand = operands[0];
            return (in JsonElement data, JsonWorkspace workspace) =>
            {
                EvalResult val = operand(data, workspace);
                if (val.TryGetDouble(out double d))
                {
                    return EvalResult.FromDouble(d * 3);
                }

                return EvalResult.FromDouble(0);
            };
        }
    }

    // ─── EvaluateToString (L108-118) ───────────────────────────
    [TestMethod]
    public void EvaluateToString_NullResult_ReturnsNullString()
    {
        // L112-118: EvaluateToString with a result that is JSON null returns "null"
        string? result = JsonLogicEvaluator.Default.EvaluateToString(
            """{"var":"missing"}""", """{}""");
        Assert.AreEqual("null", result);
    }

    // ─── ClearCache (L128-130) ───────────────────────────────────
    [TestMethod]
    public void ClearCache_DoesNotThrow()
    {
        // L128-130: ClearCache path
        var evaluator = new JsonLogicEvaluator(new Dictionary<string, IOperatorCompiler>());
        evaluator.ClearCache();
    }

    // ─── MOD with < 2 operands (L601-604) ────────────────────────
    [TestMethod]
    public void Mod_SingleOperand_ReturnsNull()
    {
        // L602-604: mod with insufficient operands
        Assert.AreEqual("null", Eval("""{"%":[5]}"""));
    }

    // ─── MIN/MAX non-numeric coercion failure (L675-677, L680-687) ──
    [TestMethod]
    public void Min_AllNonNumeric_ReturnsNull()
    {
        // L675-677: first operand fails TryCoerceToNumber → null
        Assert.AreEqual("null", Eval("""{"min":[[], {}]}"""));
    }

    [TestMethod]
    public void Max_NonNumericInSecondOperand_ReturnsNull()
    {
        // L675-677: non-numeric operand after first
        Assert.AreEqual("null", Eval("""{"max":["1", "abc"]}"""));
    }

    // ─── COMPARISON via CompareCoercedElement (L761-780) ─────────
    [TestMethod]
    public void Comparison_BothUndefined_ReturnsFalse()
    {
        // L761-763: CompareCoercedElement with both operands as missing vars (undefined)
        Assert.AreEqual("false", Eval("""{">":[ {"var":"missing1"}, {"var":"missing2"} ]}"""));
    }

    [TestMethod]
    public void GreaterThanOrEqual_ViaBigNumber()
    {
        // L772-780: CompareCoercedElement with string operands — BigNumber comparison path
        Assert.AreEqual("true", Eval("""{">=":["10","10"]}"""));
    }

    // ─── COERCING EQUALS fallthrough (L918) ──────────────────────
    [TestMethod]
    public void CoercingEquals_ArrayVsObject_ReturnsFalse()
    {
        // L918: all coercion branches exhausted → false
        Assert.AreEqual("false", Eval("""{"==":[[], {}]}"""));
    }

    [TestMethod]
    public void CoercingEquals_NullVsNumber_ReturnsFalse()
    {
        // L885-887: one null/undefined vs non-null → false
        Assert.AreEqual("false", Eval("""{"==":[null, 5]}"""));
    }

    // ─── IF comparison chain — additional uncovered branches ─────
    [TestMethod]
    public void If_ComparisonChainWithGte()
    {
        // L1307: ">=" in TryExtractCondition
        Assert.AreEqual("\"yes\"", Eval(
            """{"if":[{">=": [{"var":"x"}, 10]}, "yes", "no"]}""",
            """{"x":10}"""));
    }

    [TestMethod]
    public void If_ComparisonChainWithLte()
    {
        // L1309: "<=" in TryExtractCondition
        Assert.AreEqual("\"yes\"", Eval(
            """{"if":[{"<=": [{"var":"x"}, 10]}, "yes", "no"]}""",
            """{"x":5}"""));
    }

    [TestMethod]
    public void If_NonComparisonOperator_SkipsChainOptimization()
    {
        // L1313-1315: non-comparison operator in condition → returns false from TryExtractCondition
        Assert.AreEqual("\"yes\"", Eval(
            """{"if":[{"==": [{"var":"x"}, 5]}, "yes", "no"]}""",
            """{"x":5}"""));
    }

    [TestMethod]
    public void If_ComparisonWithNonTwoElementArray_SkipsChain()
    {
        // L1321-1323: comparison array length != 2
        Assert.AreEqual("\"yes\"", Eval(
            """{"if":[{"<": [{"var":"x"}, 5, 10]}, "yes", "no"]}""",
            """{"x":3}"""));
    }

    [TestMethod]
    public void If_ComparisonChainVarPathIsDotted_SkipsChain()
    {
        // L1378-1380: multi-segment var path in TryExtractSimpleVarProp → false
        Assert.AreEqual("\"yes\"", Eval(
            """{"if":[{"<":[{"var":"a.b"},10]},"yes","no"]}""",
            """{"a":{"b":3}}"""));
    }

    [TestMethod]
    public void If_ComparisonChainVarIsNonObject_SkipsChain()
    {
        // L1357-1359: non-object element in TryExtractSimpleVarProp → false
        Assert.AreEqual("\"yes\"", Eval(
            """{"if":[{"<":[5,10]},"yes","no"]}"""));
    }

    [TestMethod]
    public void If_ComparisonChainWithTwoConditionsDifferentVar()
    {
        // L1217-1219: different var prop in second condition → non-uniform → falls back
        Assert.AreEqual("\"a\"", Eval(
            """{"if":[{"<":[{"var":"x"},10]},"a",{"<":[{"var":"y"},20]},"b","c"]}""",
            """{"x":5,"y":15}"""));
    }

    [TestMethod]
    public void If_ComparisonChainWithTwoConditionsDifferentOp()
    {
        // L1217-1219: different compare op in second condition → non-uniform → falls back
        Assert.AreEqual("\"b\"", Eval(
            """{"if":[{"<":[{"var":"x"},10]},"a",{">":[{"var":"x"},20]},"b","c"]}""",
            """{"x":25}"""));
    }

    // ─── IN operator edge cases ──────────────────────────────────
    [TestMethod]
    public void In_ArraySearch_NotFound()
    {
        // L1689-1691: needle not found in array → false
        Assert.AreEqual("false", Eval("""{"in":[99, [1,2,3]]}"""));
    }

    [TestMethod]
    public void In_InsufficientOperands_ReturnsFalse()
    {
        // In with < 2 operands
        Assert.AreEqual("false", Eval("""{"in":[5]}"""));
    }

    // ─── MERGE with operator sub-items in array (L1771-1775) ─────
    [TestMethod]
    public void Merge_ArrayWithOperatorSubItems()
    {
        // L1747-1775: allConstants=false when array contains operator objects
        Assert.AreEqual("[1,3]", Eval(
            """{"merge":[[1,{"+": [1,2]}]]}"""));
    }

    [TestMethod]
    public void Merge_ScalarExpression_WrapsIfNotArray()
    {
        // L1704-1706: non-array single arg wrapped in array
        Assert.AreEqual("[5]", Eval("""{"merge":5}"""));
    }

    [TestMethod]
    public void Merge_SingleArrayExpression_ReturnsDirectly()
    {
        // L1704-1706: single arg that IS an array → returned directly
        Assert.AreEqual("[1,2]", Eval("""{"merge":[1,2]}"""));
    }

    // ─── FALLBACK REDUCE (L2020-2037) ────────────────────────────
    [TestMethod]
    public void FallbackReduce_InsufficientArgs_ReturnsNull()
    {
        // L2020-2023: CompileFallbackReduce with < 3 compiled args
        // Triggered by a reduce body that uses non-reduce vars (e.g. {"var":"other"})
        Assert.AreEqual("0", Eval(
            """{"reduce":[[],{"+":[{"var":"accumulator"},{"var":"other"}]},0]}""",
            """{"other":5}"""));
    }

    [TestMethod]
    public void FallbackReduce_EmptyArray_ReturnsInit()
    {
        // L2035-2037: empty array in fallback reduce → returns init
        Assert.AreEqual("99", Eval(
            """{"reduce":[[],{"+":[{"var":"accumulator"},{"var":"other"}]},99]}""",
            """{"other":5}"""));
    }

    [TestMethod]
    public void FallbackReduce_NonArray_ReturnsInit()
    {
        // L2035-2037: non-array in fallback reduce → returns init
        Assert.AreEqual("42", Eval(
            """{"reduce":[{"var":"x"},{"+":[{"var":"accumulator"},{"var":"other"}]},42]}""",
            """{"x":"not_array","other":5}"""));
    }

    // ─── VAR with number path (L2305-2308) ───────────────────────
    [TestMethod]
    public void Var_NumberPath_ResolvesViaResolveVar()
    {
        // L2305-2308: pathElement.ValueKind == Number → convert to span and walk
        Assert.AreEqual("\"first\"", Eval("""{"var":0}""", """["first","second"]"""));
    }

    [TestMethod]
    public void Var_NumberPathInArray_ResolvesViaResolveVar()
    {
        // L2305-2308: number path wrapped in array
        Assert.AreEqual("\"second\"", Eval("""{"var":[1]}""", """["first","second"]"""));
    }

    // ─── VAR with null/undefined path (L2300-2302) ───────────────
    [TestMethod]
    public void Var_NullPath_ReturnsEntireData()
    {
        // L2300-2302: pathElement null → return data
        Assert.AreEqual("[1,2]", Eval("""{"var":null}""", """[1,2]"""));
    }

    // ─── VAR non-string non-number path (L2323) ──────────────────
    [TestMethod]
    public void Var_ArrayPath_ReturnsData()
    {
        // var with array path — treated as path to resolve
        // When path is an empty array, it gets compiled as empty segments → returns data
        Assert.AreEqual("{}", Eval("""{"var":[[]]}"""));
    }

    [TestMethod]
    public void Var_BoolPath_ReturnsData()
    {
        // var with boolean true — treated as truthy path → returns data
        Assert.AreEqual("{}", Eval("""{"var":[true]}"""));
    }

    // ─── TryParseIndexUtf8 edge cases (L2373-2383) ──────────────
    [TestMethod]
    public void Var_ArrayIndex_TooLong_ReturnsNull()
    {
        // L2373-2375: index string > 10 chars fails parsing
        Assert.AreEqual("null", Eval("""{"var":"arr.12345678901"}""", """{"arr":[1,2,3]}"""));
    }

    [TestMethod]
    public void Var_ArrayIndex_NonDigit_ReturnsNull()
    {
        // L2381-2383: non-digit in index string
        Assert.AreEqual("null", Eval("""{"var":"arr.1a"}""", """{"arr":[1,2,3]}"""));
    }

    // ─── TryCoerceToDouble array/object fallthrough (L2427-2428) ─
    [TestMethod]
    public void Comparison_ArrayOperand_CannotCoerce()
    {
        // L2427-2428: TryCoerceToDouble with array returns false
        Assert.AreEqual("false", Eval("""{">":[[1,2], 5]}"""));
    }

    // ─── CoerceToBigNumber string path (L2452-2459) ──────────────
    [TestMethod]
    public void Add_StringCoercesViaBigNumber()
    {
        // L2452-2459: CoerceToBigNumber with numeric string
        Assert.AreEqual("15", Eval("""{"+":["10","5"]}"""));
    }

    // ─── BigNumberToElement overflow (L2472) ─────────────────────
    // This is dead code — TryFormat always succeeds for valid BigNumbers.
    // Documenting as unreachable.

    // ─── DoubleToElement overflow (L2483-2484) ───────────────────
    // This is dead code — Utf8Formatter.TryFormat always succeeds for doubles
    // within the 32-byte buffer. Documenting as unreachable.

    // ─── IsReduceVarPath edge cases (L2540-2556) ─────────────────
    [TestMethod]
    public void Reduce_VarWithEmptyArrayPath_FallsBack()
    {
        // L2540-2543: var path is array with 0 elements → IsReduceVarPath returns false
        Assert.AreEqual("0", Eval(
            """{"reduce":[[1,2],{"+":[{"var":"accumulator"},{"var":[]}]},0]}"""));
    }

    [TestMethod]
    public void Reduce_VarWithNonStringInArray_FallsBack()
    {
        // L2549-2551: var path is non-string → IsReduceVarPath returns false
        Assert.AreEqual("0", Eval(
            """{"reduce":[[1,2],{"+":[{"var":"accumulator"},{"var":[42]}]},0]}"""));
    }

    [TestMethod]
    public void Reduce_VarWithEmptyString_FallsBack()
    {
        // L2554-2556: var path is "" → IsReduceVarPath returns false (empty = data root)
        // In fallback reduce, {"var":""} resolves to the reduce context element itself
        // This causes accumulator + current_element_value, not the expected simple sum
        Assert.AreEqual("0", Eval(
            """{"reduce":[[1,2,3],{"+":[{"var":"accumulator"},{"var":""}]},0]}"""));
    }

    [TestMethod]
    public void Reduce_VarWithDottedPath_FallsBack()
    {
        // L2560-2562: multi-segment path → IsReduceVarPath returns false
        Assert.AreEqual("0", Eval(
            """{"reduce":[[1,2],{"+":[{"var":"accumulator"},{"var":"a.b"}]},0]}"""));
    }

    // ─── TryGetMapArgs failure paths (L2591-2600) ────────────────
    [TestMethod]
    public void Reduce_NonMapFirstArg_SkipsFusion()
    {
        // L2584-2586: first arg is not "map" operator → TryGetMapArgs returns false
        Assert.AreEqual("6", Eval(
            """{"reduce":[{"var":"arr"},{"+":[{"var":"accumulator"},{"var":"current"}]},0]}""",
            """{"arr":[1,2,3]}"""));
    }

    [TestMethod]
    public void Reduce_MapWithOneArg_SkipsFusion()
    {
        // L2590-2592: map with < 2 args → map returns empty array
        // reduce of empty array returns init value (0)
        Assert.AreEqual("0", Eval(
            """{"reduce":[{"map":[[1,2]]},{"+":[{"var":"accumulator"},{"var":"current"}]},0]}"""));
    }

    // ─── CAT with many operands to force GrowCatBuffer (L1536-1542) ──
    [TestMethod]
    public void Cat_LargeBuffer_ForcesGrow()
    {
        // L1536-1542: GrowCatBuffer when buffer exceeds initial 256 bytes
        // Build a cat with enough operands to overflow the initial 256-byte buffer
        string longStr = new string('x', 130);
        string rule = $$"""{"cat":["{{longStr}}", "{{longStr}}", "{{longStr}}"]}""";
        string result = Eval(rule);
        Assert.AreEqual($"\"{longStr}{longStr}{longStr}\"", result);
    }

    // ─── CAT triggering Utf8ValueStringBuilder via AppendCoercedUtf8 ─
    // (JsonLogicHelpers L274-314, Utf8ValueStringBuilder 260 lines)
    // The AppendCoercedUtf8 path is only used by the CG helpers,
    // not by the RT FunctionalEvaluator (which uses AppendCoercedToBuffer).
    // Utf8ValueStringBuilder coverage at 0% for this assembly is expected
    // because the RT evaluator uses its own byte[] buffer pattern directly.

    // ─── SUBSTR edge cases ───────────────────────────────────────
    [TestMethod]
    public void Substr_NegativeStart_FromEnd()
    {
        // SubstrFromAsciiUtf8 negative start
        Assert.AreEqual("\"ld\"", Eval("""{"substr":["world", -2]}"""));
    }

    [TestMethod]
    public void Substr_NegativeLength_TrimsFromEnd()
    {
        // SubstrFromAsciiUtf8 negative length
        Assert.AreEqual("\"wor\"", Eval("""{"substr":["world", 0, -2]}"""));
    }

    [TestMethod]
    public void Substr_StartBeyondLength_ReturnsEmpty()
    {
        // SubstrFromAsciiUtf8 start >= len
        Assert.AreEqual("\"\"", Eval("""{"substr":["hi", 10]}"""));
    }

    [TestMethod]
    public void Substr_NonStringSource_CoercesViaSlowPath()
    {
        // L417-424: source not a string → slow path via CoerceToString
        Assert.AreEqual("\"42\"", Eval("""{"substr":[42, 0]}"""));
    }

    [TestMethod]
    public void Substr_NullSource_CoercesToNullString()
    {
        // L417-418: null source coerces to string "null", then substr from index 0
        Assert.AreEqual("\"null\"", Eval("""{"substr":[null, 0]}"""));
    }

    [TestMethod]
    public void Substr_UnicodeString_UsesSlowPath()
    {
        // Non-ASCII string takes managed string path (L474-497)
        Assert.AreEqual("\"ñ\"", Eval("""{"substr":["señor", 2, 1]}"""));
    }

    // ─── PRECOMPUTE PATH SEGMENTS non-string/non-number (L419) ───
    [TestMethod]
    public void Var_BooleanPathLiteral_ReturnsData()
    {
        // L419: PrecomputePathSegments with boolean → empty segments → returns data
        // Boolean true is not string/number, so path is empty → returns entire data
        Assert.AreEqual("{}", Eval("""{"var":true}"""));
    }

    // ─── EMPTY STRING path with default (L300-303 + L2315-2317) ──
    [TestMethod]
    public void Var_EmptyStringElement_ReturnsData()
    {
        // L2315-2317: ResolveVar with empty quoted string returns data
        Assert.AreEqual("42", Eval("""{"var":""}""", "42"));
    }

    // ─── OBJECT traversal miss in WalkPathUtf8 (L2356-2358) ─────
    [TestMethod]
    public void Var_ObjectPropertyMissing_ReturnsNull()
    {
        // L2356-2358: object property not found
        Assert.AreEqual("null", Eval("""{"var":"a.b.c"}""", """{"a":{"b":{}}}"""));
    }

    // ─── WALK PATH with null in chain (L2330-2332) ───────────────
    [TestMethod]
    public void Var_NullInDottedPath_ReturnsNull()
    {
        // L2330-2332: current becomes null mid-walk
        Assert.AreEqual("null", Eval("""{"var":"a.b.c"}""", """{"a":null}"""));
    }

    // ─── NON-OBJECT non-array in walk path (L2360-2362) ──────────
    [TestMethod]
    public void Var_ScalarInDottedPath_ReturnsNull()
    {
        // L2360+: current is number mid-walk → returns null
        Assert.AreEqual("null", Eval("""{"var":"a.b"}""", """{"a":42}"""));
    }

    // ─── COERCING EQUALS number vs string (L890-894) ─────────────
    [TestMethod]
    public void CoercingEquals_NumberVsString()
    {
        // L890-894: number left, string right → coerce right to number
        Assert.AreEqual("true", Eval("""{"==":[5, "5"]}"""));
    }

    [TestMethod]
    public void CoercingEquals_StringVsNumber()
    {
        // L896-900: string left, number right → coerce left to number
        Assert.AreEqual("true", Eval("""{"==":["10", 10]}"""));
    }

    // ─── EMPTY string path in PrecomputePathSegments (L382-384) ──
    [TestMethod]
    public void Var_EmptyStringPath_ReturnsData()
    {
        // L382-384: quoted.Length <= 2 → empty segments
        Assert.AreEqual("{\"a\":1}", Eval("""{"var":""}""", """{"a":1}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // ROUND 3: Deeper coverage of uncovered blocks
    // ═══════════════════════════════════════════════════════════════

    // ─── MIN/MAX SLOW — non-numeric values (L664-688) ────────────
    [TestMethod]
    public void Min_WithNonNumericString_ReturnsNull()
    {
        // L675-677: TryCoerceToNumber fails mid-iteration → null
        Assert.AreEqual("null", Eval("""{"min":[1,"abc",3]}"""));
    }

    [TestMethod]
    public void Max_WithNonNumericFirstArg_ReturnsNull()
    {
        // L667-669: TryCoerceToNumber fails on first operand → null
        Assert.AreEqual("null", Eval("""{"max":["xyz",2,3]}"""));
    }

    [TestMethod]
    public void Min_WithMixedTypes_ComparesBest()
    {
        // L680-684: CompareNumbers succeeds, cmp < 0 updates best
        Assert.AreEqual("1", Eval("""{"min":[3,"1",5]}"""));
    }

    [TestMethod]
    public void Max_WithMixedTypes_ComparesBest()
    {
        // L680-684: CompareNumbers succeeds, cmp > 0 updates best
        Assert.AreEqual("5", Eval("""{"max":[3,"5",1]}"""));
    }

    // ─── COMPARE COERCED ELEMENT — null/undefined (L759-780) ─────
    [TestMethod]
    public void LessThan_NonCoercibleStrings_ReturnsFalse()
    {
        // L766-769: TryCoerceToNumber fails for non-numeric strings → false
        Assert.AreEqual("false", Eval("""{"<":["abc","xyz"]}"""));
    }

    [TestMethod]
    public void GreaterThan_NonCoercibleStrings_ReturnsFalse()
    {
        // L766-769: TryCoerceToNumber fails for both → false
        Assert.AreEqual("false", Eval("""{">":[{"var":"a"},{"var":"b"}]}""", """{"a":"abc","b":"xyz"}"""));
    }

    [TestMethod]
    public void LessThanOrEqual_CoercedCompare()
    {
        // L772-778: CompareNumbers path, LessThanOrEqual branch
        Assert.AreEqual("true", Eval("""{"<=":[3,5]}"""));
    }

    [TestMethod]
    public void GreaterThanOrEqual_CoercedCompare()
    {
        // L772-778: GreaterThanOrEqual branch
        Assert.AreEqual("true", Eval("""{">=":[5,5]}"""));
    }

    // ─── STRICT EQUALS: null vs undefined cross (L880-882) ────────
    [TestMethod]
    public void StrictEquals_NullVsUndefined_ReturnsFalse()
    {
        // L880-882: left.IsNullOrUndefined && right.IsNullOrUndefined → true
        // But when both are null (same kind): StrictEqualsElement
        Assert.AreEqual("true", Eval("""{"===":[null,null]}"""));
    }

    // ─── UNIFORM COMPARISON CHAIN OPTIMIZATION (L1200-1270) ──────
    [TestMethod]
    public void If_UniformComparisonChain_LessThan()
    {
        // L1212-1270: uniform if-chain with all < comparisons on same var
        string rule = """
            {"if":[
                {"<":[{"var":"x"},10]}, "low",
                {"<":[{"var":"x"},20]}, "medium",
                {"<":[{"var":"x"},30]}, "high",
                "very high"
            ]}
            """;
        Assert.AreEqual("\"low\"", Eval(rule, """{"x":5}"""));
        Assert.AreEqual("\"medium\"", Eval(rule, """{"x":15}"""));
        Assert.AreEqual("\"high\"", Eval(rule, """{"x":25}"""));
        Assert.AreEqual("\"very high\"", Eval(rule, """{"x":35}"""));
    }

    [TestMethod]
    public void If_UniformComparisonChain_GreaterThanOrEqual()
    {
        // L1264-1267: >=  branch in compareOp switch
        string rule = """
            {"if":[
                {">=":[{"var":"score"},90]}, "A",
                {">=":[{"var":"score"},80]}, "B",
                "C"
            ]}
            """;
        Assert.AreEqual("\"A\"", Eval(rule, """{"score":95}"""));
        Assert.AreEqual("\"B\"", Eval(rule, """{"score":85}"""));
        Assert.AreEqual("\"C\"", Eval(rule, """{"score":70}"""));
    }

    [TestMethod]
    public void If_UniformComparisonChain_FlippedOperator()
    {
        // L1339-1345: {op: [N, {"var":"path"}]} — number on left, flip op
        string rule = """
            {"if":[
                {"<":[10,{"var":"x"}]}, "big",
                "small"
            ]}
            """;
        Assert.AreEqual("\"big\"", Eval(rule, """{"x":20}"""));
        Assert.AreEqual("\"small\"", Eval(rule, """{"x":5}"""));
    }

    [TestMethod]
    public void If_NonUniformChain_NotOptimized()
    {
        // L1217-1219: different operators → chain not uniform → falls back
        string rule = """
            {"if":[
                {"<":[{"var":"x"},10]}, "lt10",
                {">":[{"var":"x"},20]}, "gt20",
                "middle"
            ]}
            """;
        Assert.AreEqual("\"lt10\"", Eval(rule, """{"x":5}"""));
        Assert.AreEqual("\"gt20\"", Eval(rule, """{"x":25}"""));
        Assert.AreEqual("\"middle\"", Eval(rule, """{"x":15}"""));
    }

    [TestMethod]
    public void If_NonVarCondition_NotOptimized()
    {
        // L1313-1315: non-comparison operator in condition → detectedOp is null
        string rule = """
            {"if":[
                {"==":[{"var":"x"},10]}, "exact",
                "other"
            ]}
            """;
        Assert.AreEqual("\"exact\"", Eval(rule, """{"x":10}"""));
        Assert.AreEqual("\"other\"", Eval(rule, """{"x":5}"""));
    }

    [TestMethod]
    public void If_ComparisonWithDottedPath_NotOptimized()
    {
        // L1378-1380: multi-segment path → segments.Length != 1 → not simple var
        string rule = """
            {"if":[
                {"<":[{"var":"a.b"},10]}, "low",
                "high"
            ]}
            """;
        Assert.AreEqual("\"low\"", Eval(rule, """{"a":{"b":5}}"""));
    }

    // ─── CAT WITH NULL/FALSE (L1518-1522) ────────────────────────
    [TestMethod]
    public void Cat_WithNull_AppendsNullText()
    {
        // L1518-1522: null case appends "null" UTF-8
        Assert.AreEqual("\"xnully\"", Eval("""{"cat":["x",null,"y"]}"""));
    }

    [TestMethod]
    public void Cat_WithFalse_AppendsFalseText()
    {
        // L1512-1516: false case appends "false" UTF-8
        Assert.AreEqual("\"xfalsey\"", Eval("""{"cat":["x",false,"y"]}"""));
    }

    // ─── IN OPERATOR WITH ARRAY (L1680-1691) ─────────────────────
    [TestMethod]
    public void In_ValueFoundInArray_ReturnsTrue()
    {
        // L1684-1686: StrictEqualsElement match → true
        Assert.AreEqual("true", Eval("""{"in":[2,[1,2,3]]}"""));
    }

    [TestMethod]
    public void In_ValueNotFoundInArray_ReturnsFalse()
    {
        // L1689, 1691: no match → false
        Assert.AreEqual("false", Eval("""{"in":[9,[1,2,3]]}"""));
    }

    // ─── MERGE WITH SINGLE NON-ARRAY (L1700-1713) ───────────────
    [TestMethod]
    public void Merge_SingleNonArrayOperand_WrapsInArray()
    {
        // L1700-1706: single non-array operand wraps in array
        Assert.AreEqual("[42]", Eval("""{"merge":42}"""));
    }

    [TestMethod]
    public void Merge_SingleArrayOperand_ReturnsFlat()
    {
        // L1704-1706: single array operand returns as-is
        Assert.AreEqual("[1,2,3]", Eval("""{"merge":[1,2,3]}"""));
    }

    // ─── FALLBACK REDUCE INSUFFICIENT ARGS (L2020-2024) ──────────
    [TestMethod]
    public void Reduce_InsufficientArgs_ReturnsNull()
    {
        // L2020-2023: < 3 compiled args → null
        Assert.AreEqual("null", Eval("""{"reduce":[[1,2,3]]}"""));
    }

    // ─── RESOLVE VAR WITH NUMERIC PATH (L2305-2308) ──────────────
    [TestMethod]
    public void Var_NumericPath_IndexesArray()
    {
        // L2305-2308: pathElement is Number → WalkPathUtf8
        Assert.AreEqual("\"b\"", Eval("""{"var":1}""", """["a","b","c"]"""));
    }

    [TestMethod]
    public void Var_BoolPathDirect_ReturnsData()
    {
        // true is not string/number, treated as empty path → returns data
        Assert.AreEqual("{}", Eval("""{"var":true}"""));
    }

    // ─── TRY COERCE TO DOUBLE — string edge cases (L2416-2428) ──
    [TestMethod]
    public void Add_NonNumericString_TreatedAsZero()
    {
        // L2416-2424: string that cannot coerce → value=0, return false
        Assert.AreEqual("5", Eval("""{"+":["hello",5]}"""));
    }

    [TestMethod]
    public void Add_ArrayOperand_TreatedAsZero()
    {
        // L2427-2428: array/object value kind → value=0, return false
        Assert.AreEqual("5", Eval("""{"+":[[1,2],5]}"""));
    }

    // ─── COERCE TO BIG NUMBER — various types (L2431-2462) ───────
    [TestMethod]
    public void AsBigNumber_True_ReturnsOne()
    {
        // L2442-2444: True → BigNumber.One
        Assert.AreEqual("1", Eval("""{"asBigNumber":true}"""));
    }

    [TestMethod]
    public void AsBigNumber_False_ReturnsZero()
    {
        // L2447-2449: False/Null → BigNumber.Zero
        Assert.AreEqual("0", Eval("""{"asBigNumber":false}"""));
    }

    [TestMethod]
    public void AsBigNumber_NumericString_CoercesToNumber()
    {
        // L2452-2458: string → coerce to number → parse as BigNumber
        Assert.AreEqual("42", Eval("""{"asBigNumber":"42"}"""));
    }

    [TestMethod]
    public void AsBigNumber_NonNumericString_ReturnsZero()
    {
        // L2459-2461: string that can't coerce → Zero
        Assert.AreEqual("0", Eval("""{"asBigNumber":"abc"}"""));
    }

    // ─── EVALUATE TO STRING (JsonLogicEvaluator L108-119) ────────
    [TestMethod]
    public void EvaluateToString_ReturnsJsonString()
    {
        // L110-118: string entry point
        string? result = JsonLogicEvaluator.Default.EvaluateToString("""{"+":[1,2]}""", "{}");
        Assert.AreEqual("3", result);
    }

    [TestMethod]
    public void EvaluateToString_UndefinedResult_ReturnsNull()
    {
        // L113-115: undefined → null
        string? result = JsonLogicEvaluator.Default.EvaluateToString("""{"var":"missing"}""", "{}");
        Assert.AreEqual("null", result);
    }

    // ─── PLUS UNARY COERCION with single bool arg (L443) ─────────
    [TestMethod]
    public void Plus_SingleBoolTrue_Coerces()
    {
        // L438-441: unary + with bool true → TryCoerceToDouble → 1
        Assert.AreEqual("1", Eval("""{"+":true}"""));
    }

    [TestMethod]
    public void Plus_SingleNonCoercibleArray_ReturnsZero()
    {
        // L443: TryCoerceToDouble fails → Zero
        Assert.AreEqual("0", Eval("""{"+":[[1,2,3]]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // FLIPPED COMPARISON in if-chain optimization (L1339-1351, 1391-1398)
    // Requires >= 3 conditions for the optimization to fire (L1068)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void If_FlippedComparison_NumberOnLeftOfGreaterThan()
    {
        // L1339-1345: {">": [N, {"var":"x"}]} detected → flip to LessThan
        // L1391-1398: FlipCompareOp called
        // Need >= 3 conditions for TryCompileUniformComparisonChain to fire
        string rule = """
            {"if":[
                {">":[10,{"var":"x"}]}, "below 10",
                {">":[20,{"var":"x"}]}, "below 20",
                {">":[30,{"var":"x"}]}, "below 30",
                "above or equal 30"
            ]}
            """;
        Assert.AreEqual("\"below 10\"", Eval(rule, """{"x":5}"""));
        Assert.AreEqual("\"below 20\"", Eval(rule, """{"x":15}"""));
        Assert.AreEqual("\"below 30\"", Eval(rule, """{"x":25}"""));
        Assert.AreEqual("\"above or equal 30\"", Eval(rule, """{"x":35}"""));
    }

    [TestMethod]
    public void If_FlippedComparison_NumberOnLeftOfLessThan()
    {
        // L1339-1345: {"<": [N, {"var":"x"}]} → flip to GreaterThan
        // Use decreasing thresholds so later conditions are reachable
        string rule = """
            {"if":[
                {"<":[20,{"var":"x"}]}, "above 20",
                {"<":[10,{"var":"x"}]}, "above 10",
                {"<":[5,{"var":"x"}]}, "above 5",
                "5 or below"
            ]}
            """;
        Assert.AreEqual("\"above 20\"", Eval(rule, """{"x":25}"""));
        Assert.AreEqual("\"above 10\"", Eval(rule, """{"x":15}"""));
        Assert.AreEqual("\"above 5\"", Eval(rule, """{"x":7}"""));
        Assert.AreEqual("\"5 or below\"", Eval(rule, """{"x":3}"""));
    }

    [TestMethod]
    public void If_FlippedComparison_LessThanOrEqual()
    {
        // L1393: LessThanOrEqual → flips to GreaterThanOrEqual
        // Use decreasing thresholds so later conditions are reachable
        string rule = """
            {"if":[
                {"<=":[30,{"var":"x"}]}, "x >= 30",
                {"<=":[20,{"var":"x"}]}, "x >= 20",
                {"<=":[10,{"var":"x"}]}, "x >= 10",
                "x < 10"
            ]}
            """;
        Assert.AreEqual("\"x >= 30\"", Eval(rule, """{"x":30}"""));
        Assert.AreEqual("\"x >= 20\"", Eval(rule, """{"x":25}"""));
        Assert.AreEqual("\"x >= 10\"", Eval(rule, """{"x":15}"""));
        Assert.AreEqual("\"x < 10\"", Eval(rule, """{"x":5}"""));
    }

    [TestMethod]
    public void If_FlippedComparison_GreaterThanOrEqual_UniformChain()
    {
        // L1395: GreaterThanOrEqual → flips to LessThanOrEqual
        string rule = """
            {"if":[
                {">=":[10,{"var":"x"}]}, "x <= 10",
                {">=":[20,{"var":"x"}]}, "x <= 20",
                {">=":[30,{"var":"x"}]}, "x <= 30",
                "x > 30"
            ]}
            """;
        Assert.AreEqual("\"x <= 10\"", Eval(rule, """{"x":5}"""));
        Assert.AreEqual("\"x <= 20\"", Eval(rule, """{"x":15}"""));
        Assert.AreEqual("\"x <= 30\"", Eval(rule, """{"x":25}"""));
        Assert.AreEqual("\"x > 30\"", Eval(rule, """{"x":35}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // VAR via DYNAMIC PATH (L2305-2308, L2340-2349)
    // ResolveVar is only called for dynamic (computed) var paths
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Var_DynamicNumericPath_ResolvesArrayIndex()
    {
        // L2305-2308: pathElement.ValueKind == Number → WalkPathUtf8
        // Dynamic path: the var argument is a computed expression returning a number
        Assert.AreEqual("\"b\"", Eval(
            """{"var":{"+":[0,1]}}""",
            """["a","b","c"]"""));
    }

    [TestMethod]
    public void Var_DynamicStringPath_ArrayIndex()
    {
        // L2340-2343: WalkPathUtf8 encounters array, resolves index
        // Dynamic path resolves to "0.name" string via cat
        Assert.AreEqual("\"first\"", Eval(
            """{"var":{"cat":["0",".","name"]}}""",
            """[{"name":"first"},{"name":"second"}]"""));
    }

    [TestMethod]
    public void Var_DynamicStringPath_ArrayIndexOutOfBounds()
    {
        // L2345-2348: WalkPathUtf8 array index out of bounds → null
        Assert.AreEqual("null", Eval(
            """{"var":{"cat":["5",".","name"]}}""",
            """[{"name":"first"}]"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // VAR compiled multi-segment with array data (L344-355)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Var_CompiledArrayIndexInPath_ResolvesElement()
    {
        // L344-348: compiled multi-segment path hits array
        Assert.AreEqual("\"first\"", Eval(
            """{"var":"0.name"}""",
            """[{"name":"first"},{"name":"second"}]"""));
    }

    [TestMethod]
    public void Var_CompiledArrayIndexOutOfBounds_ReturnsNull()
    {
        // L350-354: compiled path, array index out of bounds → null
        Assert.AreEqual("null", Eval(
            """{"var":"5.name"}""",
            """[{"name":"first"}]"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // CAT with various types (L1471-1515)
    // Note: L1518-1522 (case Null) is dead code — caught by
    // IsNullOrUndefined() early return at L1471
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Cat_NullValue_AppendsNullText()
    {
        // L1471-1476: IsNullOrUndefined() → "null"
        Assert.AreEqual("\"null\"", Eval("""{"cat":[null]}"""));
    }

    [TestMethod]
    public void Cat_BoolAndNumberAndNull_CoercesAll()
    {
        // L1506-1510 (true), L1512-1515 (false), L1471-1476 (null),
        // L1496-1503 (number), L1481-1493 (string)
        Assert.AreEqual("\"true42falsenullhi\"", Eval("""{"cat":[true,42,false,null,"hi"]}"""));
    }

    [TestMethod]
    public void Cat_EmptyString_AppendsNothing()
    {
        // L1485: span.Length <= 2 for empty string (just quotes "")
        Assert.AreEqual("\"hello\"", Eval("""{"cat":["hello",""]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // NUMERIC VAR PATH — compiled single-segment with array (L317-322)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Var_NumericSingleSegment_ResolvesArrayIndex()
    {
        // L317-322: single-segment path, data is array, TryParseIndexUtf8 succeeds
        Assert.AreEqual("\"b\"", Eval(
            """{"var":"1"}""",
            """["a","b","c"]"""));
    }
}
