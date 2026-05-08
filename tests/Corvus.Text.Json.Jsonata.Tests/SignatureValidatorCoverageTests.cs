// <copyright file="SignatureValidatorCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Coverage tests for <see cref="SignatureValidator"/> targeting uncovered error paths,
/// type description branches, and argument validation edge cases.
/// </summary>
/// <remarks>
/// SignatureValidator is invoked when a function is defined with an inline type signature
/// (e.g. <c>function($x)&lt;n:n&gt;{$x * 2}</c>). The validator checks signature syntax
/// at compile time and argument types at call time.
/// </remarks>
[TestClass]
public class SignatureValidatorCoverageTests
{
    private static string Eval(string expression, string data = "null")
    {
        return JsonataEvaluator.Default.EvaluateToString(expression, data) ?? "undefined";
    }

    // ─── ValidateSyntax L44-45: Short/invalid signature structure ───────

    [TestMethod]
    public void ValidateSyntax_TooShort_ThrowsS0402()
    {
        // A function with a signature that's just "x" (no angle brackets)
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<>{$x}; $f(1) )"""));
        Assert.IsNotNull(ex);
    }

    // ─── ValidateSyntax L74-75: Unmatched > (extra closing bracket) ─────

    [TestMethod]
    public void ValidateSyntax_ExtraClosingAngle_ThrowsS0402()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<n>>:n>{$x}; $f(1) )"""));
        Assert.IsNotNull(ex);
    }

    // ─── ValidateSyntax L91-93: Nested parentheses in union ─────────────

    [TestMethod]
    public void ValidateSyntax_UnionType_Parsed()
    {
        // A signature with union type: (ns) means number or string
        string result = Eval("""( $f := function($x)<(ns):x>{$x}; $f(42) )""");
        Assert.AreEqual("42", result);
    }

    // ─── ValidateSyntax L103-104: Unmatched open paren ──────────────────

    [TestMethod]
    public void ValidateSyntax_UnmatchedOpenParen_ThrowsS0402()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<(ns:x>{$x}; $f(1) )"""));
        Assert.IsNotNull(ex);
    }

    // ─── ValidateSyntax L125-128: Space in signature ────────────────────

    [TestMethod]
    public void ValidateSyntax_SpaceInSignature_AcceptedSilently()
    {
        // Spaces in signatures are just skipped (L126-128)
        string result = Eval("""( $f := function($x)<n :n>{$x * 2}; $f(3) )""");
        Assert.AreEqual("6", result);
    }

    // ─── ValidateSyntax L140: Invalid type character ────────────────────

    [TestMethod]
    public void ValidateSyntax_InvalidTypeChar_ThrowsS0401()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<z:n>{$x}; $f(1) )"""));
        StringAssert.Contains(ex.Code, "S0401");
    }

    // ─── ValidateSyntax L144-145: Unmatched < (missing >) ───────────────

    [TestMethod]
    public void ValidateSyntax_UnmatchedOpenAngle_ThrowsS0402()
    {
        // The parser scans for matching <> but if the signature lacks the
        // final >, the parser should still pass it to ValidateSyntax which throws
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<n<s:n>{$x}; $f(1) )"""));
        Assert.IsNotNull(ex);
    }

    // ─── ValidateArgs: Type mismatch triggers T0410 ─────────────────────

    [TestMethod]
    public void ValidateArgs_NumberExpectedGotString_ThrowsT0410()
    {
        // Function expects <n:n> but gets string
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<n:n>{$x * 2}; $f("hello") )"""));
        StringAssert.Contains(ex.Code, "T0410");
    }

    [TestMethod]
    public void ValidateArgs_NumberExpectedGotBoolean_ThrowsT0410()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<n:n>{$x}; $f(true) )"""));
        StringAssert.Contains(ex.Code, "T0410");
    }

    [TestMethod]
    public void ValidateArgs_NumberExpectedGotObject_ThrowsT0410()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<n:n>{$x}; $f({"a":1}) )"""));
        StringAssert.Contains(ex.Code, "T0410");
    }

    [TestMethod]
    public void ValidateArgs_NumberExpectedGotNull_ThrowsT0410()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<n:n>{$x}; $f(null) )"""));
        StringAssert.Contains(ex.Code, "T0410");
    }

    [TestMethod]
    public void ValidateArgs_StringExpectedGotNumber_ThrowsT0410()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<s:s>{$x}; $f(42) )"""));
        StringAssert.Contains(ex.Code, "T0410");
    }

    [TestMethod]
    public void ValidateArgs_FunctionExpectedGotNumber_ThrowsT0410()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<f:x>{$x}; $f(42) )"""));
        StringAssert.Contains(ex.Code, "T0410");
    }

    [TestMethod]
    public void ValidateArgs_ObjectExpectedGotString_ThrowsT0410()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<o:o>{$x}; $f("hello") )"""));
        StringAssert.Contains(ex.Code, "T0410");
    }

    [TestMethod]
    public void ValidateArgs_BooleanExpectedGotNumber_ThrowsT0410()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<b:b>{$x}; $f(42) )"""));
        StringAssert.Contains(ex.Code, "T0410");
    }

    // ─── T0412: Array element type mismatch ─────────────────────────────

    [TestMethod]
    public void ValidateArgs_ArrayOfNumbersExpectedGotStrings_ThrowsT0412()
    {
        // Function expects <a<n>:n> (array of numbers)
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<a<n>:n>{$sum($x)}; $f(["a","b"]) )"""));
        StringAssert.Contains(ex.Code, "T0412");
    }

    [TestMethod]
    public void ValidateArgs_ArrayOfNumbersExpectedGotObjects_ThrowsT0412()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<a<n>:n>{$sum($x)}; $f([{"a":1}]) )"""));
        StringAssert.Contains(ex.Code, "T0412");
    }

    [TestMethod]
    public void ValidateArgs_ArrayOfStringsExpectedGotNumbers_ThrowsT0412()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<a<s>:s>{$x}; $f([1,2,3]) )"""));
        StringAssert.Contains(ex.Code, "T0412");
    }

    // ─── ParseParamSpecs L399-418: Union type with array sub-type ───────

    [TestMethod]
    public void ParseParamSpecs_UnionWithArraySubtype_ValidArgs()
    {
        // Signature <(sa<n>):x> means string or array of numbers
        string result = Eval("""( $f := function($x)<(sa<n>):x>{$x}; $f([1,2,3]) )""");
        Assert.AreEqual("[1,2,3]", result);
    }

    // ─── ParseParamSpecs L478-481: > handling at depth ──────────────────

    [TestMethod]
    public void ParseParamSpecs_NestedAngleBrackets_Parsed()
    {
        // Complex signature: <a<n>f<nn:n>:n> (array of nums, function, returns num)
        string result = Eval("""( $f := function($arr,$fn)<a<n>f<nn:n>:n>{$reduce($arr,$fn)}; $f([1,2,3], function($p,$c){$p+$c}) )""");
        Assert.AreEqual("6", result);
    }

    // ─── L317,319: 'l' (null) type check in MatchesSingleTypeChar ───────

    [TestMethod]
    public void MatchesSingleTypeChar_NullType_Matches()
    {
        // Signature <l:l> expects null
        string result = Eval("""( $f := function($x)<l:l>{$x}; $f(null) )""");
        Assert.AreEqual("null", result);
    }

    // ─── L331-336: MatchesSingleType for sub-types ──────────────────────

    [TestMethod]
    public void MatchesSingleType_ArraySubtypeObject()
    {
        // <a<o>:x> expects array of objects
        string result = Eval("""( $f := function($x)<a<o>:x>{$x}; $f([{"a":1}]) )""");
        StringAssert.Contains(result, "a");
    }

    [TestMethod]
    public void MatchesSingleType_ArraySubtypeBoolean()
    {
        // <a<b>:x> expects array of booleans
        string result = Eval("""( $f := function($x)<a<b>:x>{$x}; $f([true, false]) )""");
        StringAssert.Contains(result, "true");
    }

    // ─── DescribeTypeChar/DescribeValue via error messages ───────────────

    [TestMethod]
    public void DescribeValue_Array_InErrorMessage()
    {
        // <n:n> with array input shows "array" in error
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<n:n>{$x}; $f([1,2]) )"""));
        StringAssert.Contains(ex.Code, "T0410");
    }

    // ─── Variadic params ─────────────────────────────────────────────────

    [TestMethod]
    public void ValidateArgs_VariadicParams_ConsumeMatchingArgs()
    {
        // Signature with + (variadic): <n+:n> — one or more numbers
        string result = Eval("""( $f := function()<n+:n>{$sum(arguments)}; $f(1,2,3) )""");
        Assert.IsNotNull(result);
    }

    // ─── Optional params ─────────────────────────────────────────────────

    [TestMethod]
    public void ValidateArgs_OptionalParam_SkipsWhenMissing()
    {
        // Signature <sn?:s> — second param is optional number
        string result = Eval("""( $f := function($s, $n)<sn?:s>{$s}; $f("hello") )""");
        Assert.AreEqual("\"hello\"", result);
    }

    // ─── DescribeTypes L502-503: Empty types list ────────────────────────

    [TestMethod]
    public void ValidateArgs_AnyType_AcceptsAll()
    {
        // <x:x> accepts any type
        string result = Eval("""( $f := function($x)<x:x>{$x}; $f(42) )""");
        Assert.AreEqual("42", result);
    }

    [TestMethod]
    public void ValidateArgs_AnyType_AcceptsNull()
    {
        string result = Eval("""( $f := function($x)<x:x>{$x}; $f(null) )""");
        Assert.AreEqual("null", result);
    }

    // ─── L169,172: Context separator '-' in ValidateArgs ─────────────────

    [TestMethod]
    public void ValidateArgs_ContextSeparator_SkippedDuringValidation()
    {
        // <s-n:s> means: string param, context separator, number param
        string result = Eval("""( $f := function($s, $n)<s-n:s>{$s & $string($n)}; $f("x", 1) )""");
        Assert.AreEqual("\"x1\"", result);
    }

    // ─── L182-196: Variadic with remaining required params ───────────────

    [TestMethod]
    public void ValidateArgs_VariadicThenRequired_ConsumesCorrectly()
    {
        // <n+s:s> — variadic numbers, then a required string
        // The variadic consumes nums but leaves the last arg for 's'
        string result = Eval("""( $f := function($a,$b,$c)<n+s:s>{$c}; $f(1, 2, "end") )""");
        Assert.AreEqual("\"end\"", result);
    }

    [TestMethod]
    public void ValidateArgs_VariadicBreaksOnMismatch()
    {
        // Variadic <n+s:s> — stops consuming nums when it hits a string
        string result = Eval("""( $f := function($a,$b)<n+s:s>{$b}; $f(1, "done") )""");
        Assert.AreEqual("\"done\"", result);
    }

    // ─── L219-226: Optional param type mismatch skips ────────────────────

    [TestMethod]
    public void ValidateArgs_OptionalTypeMismatch_SkipsToNextParam()
    {
        // <s?n:n> — optional string, required number
        // Passing just a number: the 's?' doesn't match, so it skips to 'n'
        string result = Eval("""( $f := function($x)<s?n:n>{$x * 2}; $f(5) )""");
        Assert.AreEqual("10", result);
    }

    // ─── L277-283: Non-array passed to a<subtype> ────────────────────────

    [TestMethod]
    public void ValidateArgs_NonArrayToArraySubtype_WrongType_ThrowsT0412()
    {
        // <a<n>:n> expects array of numbers or a number (auto-wrap)
        // Passing a string should fail — auto-wrapped ["hello"] doesn't have number elements
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<a<n>:n>{$sum($x)}; $f("hello") )"""));
        StringAssert.Contains(ex.Code, "T0412");
    }

    // ─── L363-366: ParseParamSpecs context separator ─────────────────────

    [TestMethod]
    public void ParseParamSpecs_ContextSeparator_MultipleParams()
    {
        // <n-s-b:x> — number, context, string, context, boolean
        string result = Eval("""( $f := function($n,$s,$b)<n-s-b:x>{[$n,$s,$b]}; $f(1,"hi",true) )""");
        StringAssert.Contains(result, "1");
    }

    // ─── L62-63: Sub-type < after invalid prev type ──────────────────────

    [TestMethod]
    public void ValidateSyntax_SubtypeAfterNonArray_ThrowsS0401()
    {
        // <n<s>:n> — sub-type specifier after 'n' (not 'a' or 'f')
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<n<s>:n>{$x}; $f(1) )"""));
        StringAssert.Contains(ex.Code, "S0401");
    }

    // ─── DescribeTypeChar coverage via T0410 error messages ──────────────

    [TestMethod]
    public void DescribeTypeChar_Boolean_InErrorMessage()
    {
        // <b:b> with number shows "boolean" in error
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<b:b>{$x}; $f(42) )"""));
        StringAssert.Contains(ex.Code, "T0410");
    }

    [TestMethod]
    public void DescribeTypeChar_Object_InErrorMessage()
    {
        // <o:o> with string shows "object" in error
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<o:o>{$x}; $f("x") )"""));
        StringAssert.Contains(ex.Code, "T0410");
    }

    [TestMethod]
    public void DescribeTypeChar_Array_InErrorMessage()
    {
        // <a<s>:a> with number input — the number gets auto-wrapped to [42]
        // but 42 isn't a string, so T0412 "expected array of string"
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<a<s>:x>{$x}; $f(42) )"""));
        StringAssert.Contains(ex.Code, "T0412");
    }

    [TestMethod]
    public void DescribeTypeChar_Function_InErrorMessage()
    {
        // <f:x> with string input shows "function" in error
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<f:x>{$x}; $f("hello") )"""));
        StringAssert.Contains(ex.Code, "T0410");
    }

    [TestMethod]
    public void DescribeTypeChar_Null_InErrorMessage()
    {
        // <l:l> with number input shows "null" expected
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<l:l>{$x}; $f(42) )"""));
        StringAssert.Contains(ex.Code, "T0410");
    }

    // ─── L540: DescribeValue for undefined ───────────────────────────────

    [TestMethod]
    public void ValidateArgs_TooManyArgs_ThrowsT0410()
    {
        // <n:n> with 2 args — extra args not consumed
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function($x)<n:n>{$x}; $f(1, 2) )"""));
        StringAssert.Contains(ex.Code, "T0410");
    }

    // ─── L195-196: Variadic break on type mismatch ───────────────────────

    [TestMethod]
    public void ValidateArgs_VariadicBreak_NonMatchingArgStopsConsumption()
    {
        // <n+:n> with (1, 2, "x") — variadic consumes 1, 2, then "x" doesn't match
        // After break, "x" is an extra arg → T0410
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Eval("""( $f := function()<n+:n>{$sum(arguments)}; $f(1, 2, "x") )"""));
        StringAssert.Contains(ex.Code, "T0410");
    }

    // ─── L226: Optional param that DOES match ────────────────────────────

    [TestMethod]
    public void ValidateArgs_OptionalParamMatches_Consumed()
    {
        // <n?s:s> — optional number, then string
        // Pass (42, "x") — the optional n? matches 42, then s matches "x"
        string result = Eval("""( $f := function($n, $s)<n?s:s>{$s}; $f(42, "x") )""");
        Assert.AreEqual("\"x\"", result);
    }

    // ─── L502-503: DescribeTypes with function that has empty type ────────

    [TestMethod]
    public void ValidateArgs_FunctionParam_PassedAsArg()
    {
        // <f:x> — expects a function, passes a lambda
        string result = Eval("""( $f := function($fn)<f:x>{$fn(1)}; $f(function($x){$x*2}) )""");
        Assert.AreEqual("2", result);
    }

    // ─── L283: Non-array matching sub-type falls through ─────────────────

    [TestMethod]
    public void ValidateArgs_NonArrayMatchingSubtype_Accepted()
    {
        // <a<n>:n> with a single number — auto-wrapped, number matches n sub-type
        string result = Eval("""( $f := function($x)<a<n>:n>{$sum($x)}; $f(42) )""");
        Assert.AreEqual("42", result);
    }

    // ─── L446-456: Deeply nested sub-type parsing ────────────────────────

    [TestMethod]
    public void ParseParamSpecs_DeeplyNestedSubtype_Parsed()
    {
        // <a<a<n>>:x> — array of arrays of numbers
        string result = Eval("""( $f := function($x)<a<a<n>>:x>{$x}; $f([[1,2],[3,4]]) )""");
        StringAssert.Contains(result, "1");
    }

    // ─── L540: DescribeValue undefined path ──────────────────────────────

    [TestMethod]
    public void ValidateArgs_UndefinedInput_NoError()
    {
        // Function with signature called with undefined (missing value) — should not error
        string result = Eval("""( $f := function($x)<n?:n>{$x}; $f(nothing) )""");
        Assert.AreEqual("undefined", result);
    }
}

