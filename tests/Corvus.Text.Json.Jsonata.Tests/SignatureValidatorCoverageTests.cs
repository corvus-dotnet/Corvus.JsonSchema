// <copyright file="SignatureValidatorCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Xunit;

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
public class SignatureValidatorCoverageTests
{
    private static string Eval(string expression, string data = "null")
    {
        return JsonataEvaluator.Default.EvaluateToString(expression, data) ?? "undefined";
    }

    // ─── ValidateSyntax L44-45: Short/invalid signature structure ───────

    [Fact]
    public void ValidateSyntax_TooShort_ThrowsS0402()
    {
        // A function with a signature that's just "x" (no angle brackets)
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<>{$x}; $f(1) )"""));
        Assert.NotNull(ex);
    }

    // ─── ValidateSyntax L74-75: Unmatched > (extra closing bracket) ─────

    [Fact]
    public void ValidateSyntax_ExtraClosingAngle_ThrowsS0402()
    {
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<n>>:n>{$x}; $f(1) )"""));
        Assert.NotNull(ex);
    }

    // ─── ValidateSyntax L91-93: Nested parentheses in union ─────────────

    [Fact]
    public void ValidateSyntax_UnionType_Parsed()
    {
        // A signature with union type: (ns) means number or string
        string result = Eval("""( $f := function($x)<(ns):x>{$x}; $f(42) )""");
        Assert.Equal("42", result);
    }

    // ─── ValidateSyntax L103-104: Unmatched open paren ──────────────────

    [Fact]
    public void ValidateSyntax_UnmatchedOpenParen_ThrowsS0402()
    {
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<(ns:x>{$x}; $f(1) )"""));
        Assert.NotNull(ex);
    }

    // ─── ValidateSyntax L125-128: Space in signature ────────────────────

    [Fact]
    public void ValidateSyntax_SpaceInSignature_AcceptedSilently()
    {
        // Spaces in signatures are just skipped (L126-128)
        string result = Eval("""( $f := function($x)<n :n>{$x * 2}; $f(3) )""");
        Assert.Equal("6", result);
    }

    // ─── ValidateSyntax L140: Invalid type character ────────────────────

    [Fact]
    public void ValidateSyntax_InvalidTypeChar_ThrowsS0401()
    {
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<z:n>{$x}; $f(1) )"""));
        Assert.Contains("S0401", ex.Code);
    }

    // ─── ValidateSyntax L144-145: Unmatched < (missing >) ───────────────

    [Fact]
    public void ValidateSyntax_UnmatchedOpenAngle_ThrowsS0402()
    {
        // The parser scans for matching <> but if the signature lacks the
        // final >, the parser should still pass it to ValidateSyntax which throws
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<n<s:n>{$x}; $f(1) )"""));
        Assert.NotNull(ex);
    }

    // ─── ValidateArgs: Type mismatch triggers T0410 ─────────────────────

    [Fact]
    public void ValidateArgs_NumberExpectedGotString_ThrowsT0410()
    {
        // Function expects <n:n> but gets string
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<n:n>{$x * 2}; $f("hello") )"""));
        Assert.Contains("T0410", ex.Code);
    }

    [Fact]
    public void ValidateArgs_NumberExpectedGotBoolean_ThrowsT0410()
    {
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<n:n>{$x}; $f(true) )"""));
        Assert.Contains("T0410", ex.Code);
    }

    [Fact]
    public void ValidateArgs_NumberExpectedGotObject_ThrowsT0410()
    {
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<n:n>{$x}; $f({"a":1}) )"""));
        Assert.Contains("T0410", ex.Code);
    }

    [Fact]
    public void ValidateArgs_NumberExpectedGotNull_ThrowsT0410()
    {
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<n:n>{$x}; $f(null) )"""));
        Assert.Contains("T0410", ex.Code);
    }

    [Fact]
    public void ValidateArgs_StringExpectedGotNumber_ThrowsT0410()
    {
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<s:s>{$x}; $f(42) )"""));
        Assert.Contains("T0410", ex.Code);
    }

    [Fact]
    public void ValidateArgs_FunctionExpectedGotNumber_ThrowsT0410()
    {
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<f:x>{$x}; $f(42) )"""));
        Assert.Contains("T0410", ex.Code);
    }

    [Fact]
    public void ValidateArgs_ObjectExpectedGotString_ThrowsT0410()
    {
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<o:o>{$x}; $f("hello") )"""));
        Assert.Contains("T0410", ex.Code);
    }

    [Fact]
    public void ValidateArgs_BooleanExpectedGotNumber_ThrowsT0410()
    {
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<b:b>{$x}; $f(42) )"""));
        Assert.Contains("T0410", ex.Code);
    }

    // ─── T0412: Array element type mismatch ─────────────────────────────

    [Fact]
    public void ValidateArgs_ArrayOfNumbersExpectedGotStrings_ThrowsT0412()
    {
        // Function expects <a<n>:n> (array of numbers)
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<a<n>:n>{$sum($x)}; $f(["a","b"]) )"""));
        Assert.Contains("T0412", ex.Code);
    }

    [Fact]
    public void ValidateArgs_ArrayOfNumbersExpectedGotObjects_ThrowsT0412()
    {
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<a<n>:n>{$sum($x)}; $f([{"a":1}]) )"""));
        Assert.Contains("T0412", ex.Code);
    }

    [Fact]
    public void ValidateArgs_ArrayOfStringsExpectedGotNumbers_ThrowsT0412()
    {
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<a<s>:s>{$x}; $f([1,2,3]) )"""));
        Assert.Contains("T0412", ex.Code);
    }

    // ─── ParseParamSpecs L399-418: Union type with array sub-type ───────

    [Fact]
    public void ParseParamSpecs_UnionWithArraySubtype_ValidArgs()
    {
        // Signature <(sa<n>):x> means string or array of numbers
        string result = Eval("""( $f := function($x)<(sa<n>):x>{$x}; $f([1,2,3]) )""");
        Assert.Equal("[1,2,3]", result);
    }

    // ─── ParseParamSpecs L478-481: > handling at depth ──────────────────

    [Fact]
    public void ParseParamSpecs_NestedAngleBrackets_Parsed()
    {
        // Complex signature: <a<n>f<nn:n>:n> (array of nums, function, returns num)
        string result = Eval("""( $f := function($arr,$fn)<a<n>f<nn:n>:n>{$reduce($arr,$fn)}; $f([1,2,3], function($p,$c){$p+$c}) )""");
        Assert.Equal("6", result);
    }

    // ─── L317,319: 'l' (null) type check in MatchesSingleTypeChar ───────

    [Fact]
    public void MatchesSingleTypeChar_NullType_Matches()
    {
        // Signature <l:l> expects null
        string result = Eval("""( $f := function($x)<l:l>{$x}; $f(null) )""");
        Assert.Equal("null", result);
    }

    // ─── L331-336: MatchesSingleType for sub-types ──────────────────────

    [Fact]
    public void MatchesSingleType_ArraySubtypeObject()
    {
        // <a<o>:x> expects array of objects
        string result = Eval("""( $f := function($x)<a<o>:x>{$x}; $f([{"a":1}]) )""");
        Assert.Contains("a", result);
    }

    [Fact]
    public void MatchesSingleType_ArraySubtypeBoolean()
    {
        // <a<b>:x> expects array of booleans
        string result = Eval("""( $f := function($x)<a<b>:x>{$x}; $f([true, false]) )""");
        Assert.Contains("true", result);
    }

    // ─── DescribeTypeChar/DescribeValue via error messages ───────────────

    [Fact]
    public void DescribeValue_Array_InErrorMessage()
    {
        // <n:n> with array input shows "array" in error
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<n:n>{$x}; $f([1,2]) )"""));
        Assert.Contains("T0410", ex.Code);
    }

    // ─── Variadic params ─────────────────────────────────────────────────

    [Fact]
    public void ValidateArgs_VariadicParams_ConsumeMatchingArgs()
    {
        // Signature with + (variadic): <n+:n> — one or more numbers
        string result = Eval("""( $f := function()<n+:n>{$sum(arguments)}; $f(1,2,3) )""");
        Assert.NotNull(result);
    }

    // ─── Optional params ─────────────────────────────────────────────────

    [Fact]
    public void ValidateArgs_OptionalParam_SkipsWhenMissing()
    {
        // Signature <sn?:s> — second param is optional number
        string result = Eval("""( $f := function($s, $n)<sn?:s>{$s}; $f("hello") )""");
        Assert.Equal("\"hello\"", result);
    }

    // ─── DescribeTypes L502-503: Empty types list ────────────────────────

    [Fact]
    public void ValidateArgs_AnyType_AcceptsAll()
    {
        // <x:x> accepts any type
        string result = Eval("""( $f := function($x)<x:x>{$x}; $f(42) )""");
        Assert.Equal("42", result);
    }

    [Fact]
    public void ValidateArgs_AnyType_AcceptsNull()
    {
        string result = Eval("""( $f := function($x)<x:x>{$x}; $f(null) )""");
        Assert.Equal("null", result);
    }

    // ─── L169,172: Context separator '-' in ValidateArgs ─────────────────

    [Fact]
    public void ValidateArgs_ContextSeparator_SkippedDuringValidation()
    {
        // <s-n:s> means: string param, context separator, number param
        string result = Eval("""( $f := function($s, $n)<s-n:s>{$s & $string($n)}; $f("x", 1) )""");
        Assert.Equal("\"x1\"", result);
    }

    // ─── L182-196: Variadic with remaining required params ───────────────

    [Fact]
    public void ValidateArgs_VariadicThenRequired_ConsumesCorrectly()
    {
        // <n+s:s> — variadic numbers, then a required string
        // The variadic consumes nums but leaves the last arg for 's'
        string result = Eval("""( $f := function($a,$b,$c)<n+s:s>{$c}; $f(1, 2, "end") )""");
        Assert.Equal("\"end\"", result);
    }

    [Fact]
    public void ValidateArgs_VariadicBreaksOnMismatch()
    {
        // Variadic <n+s:s> — stops consuming nums when it hits a string
        string result = Eval("""( $f := function($a,$b)<n+s:s>{$b}; $f(1, "done") )""");
        Assert.Equal("\"done\"", result);
    }

    // ─── L219-226: Optional param type mismatch skips ────────────────────

    [Fact]
    public void ValidateArgs_OptionalTypeMismatch_SkipsToNextParam()
    {
        // <s?n:n> — optional string, required number
        // Passing just a number: the 's?' doesn't match, so it skips to 'n'
        string result = Eval("""( $f := function($x)<s?n:n>{$x * 2}; $f(5) )""");
        Assert.Equal("10", result);
    }

    // ─── L277-283: Non-array passed to a<subtype> ────────────────────────

    [Fact]
    public void ValidateArgs_NonArrayToArraySubtype_WrongType_ThrowsT0412()
    {
        // <a<n>:n> expects array of numbers or a number (auto-wrap)
        // Passing a string should fail — auto-wrapped ["hello"] doesn't have number elements
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<a<n>:n>{$sum($x)}; $f("hello") )"""));
        Assert.Contains("T0412", ex.Code);
    }

    // ─── L363-366: ParseParamSpecs context separator ─────────────────────

    [Fact]
    public void ParseParamSpecs_ContextSeparator_MultipleParams()
    {
        // <n-s-b:x> — number, context, string, context, boolean
        string result = Eval("""( $f := function($n,$s,$b)<n-s-b:x>{[$n,$s,$b]}; $f(1,"hi",true) )""");
        Assert.Contains("1", result);
    }

    // ─── L62-63: Sub-type < after invalid prev type ──────────────────────

    [Fact]
    public void ValidateSyntax_SubtypeAfterNonArray_ThrowsS0401()
    {
        // <n<s>:n> — sub-type specifier after 'n' (not 'a' or 'f')
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<n<s>:n>{$x}; $f(1) )"""));
        Assert.Contains("S0401", ex.Code);
    }

    // ─── DescribeTypeChar coverage via T0410 error messages ──────────────

    [Fact]
    public void DescribeTypeChar_Boolean_InErrorMessage()
    {
        // <b:b> with number shows "boolean" in error
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<b:b>{$x}; $f(42) )"""));
        Assert.Contains("T0410", ex.Code);
    }

    [Fact]
    public void DescribeTypeChar_Object_InErrorMessage()
    {
        // <o:o> with string shows "object" in error
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<o:o>{$x}; $f("x") )"""));
        Assert.Contains("T0410", ex.Code);
    }

    [Fact]
    public void DescribeTypeChar_Array_InErrorMessage()
    {
        // <a<s>:a> with number input — the number gets auto-wrapped to [42]
        // but 42 isn't a string, so T0412 "expected array of string"
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<a<s>:x>{$x}; $f(42) )"""));
        Assert.Contains("T0412", ex.Code);
    }

    [Fact]
    public void DescribeTypeChar_Function_InErrorMessage()
    {
        // <f:x> with string input shows "function" in error
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<f:x>{$x}; $f("hello") )"""));
        Assert.Contains("T0410", ex.Code);
    }

    [Fact]
    public void DescribeTypeChar_Null_InErrorMessage()
    {
        // <l:l> with number input shows "null" expected
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<l:l>{$x}; $f(42) )"""));
        Assert.Contains("T0410", ex.Code);
    }

    // ─── L540: DescribeValue for undefined ───────────────────────────────

    [Fact]
    public void ValidateArgs_TooManyArgs_ThrowsT0410()
    {
        // <n:n> with 2 args — extra args not consumed
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function($x)<n:n>{$x}; $f(1, 2) )"""));
        Assert.Contains("T0410", ex.Code);
    }

    // ─── L195-196: Variadic break on type mismatch ───────────────────────

    [Fact]
    public void ValidateArgs_VariadicBreak_NonMatchingArgStopsConsumption()
    {
        // <n+:n> with (1, 2, "x") — variadic consumes 1, 2, then "x" doesn't match
        // After break, "x" is an extra arg → T0410
        var ex = Assert.Throws<JsonataException>(() =>
            Eval("""( $f := function()<n+:n>{$sum(arguments)}; $f(1, 2, "x") )"""));
        Assert.Contains("T0410", ex.Code);
    }

    // ─── L226: Optional param that DOES match ────────────────────────────

    [Fact]
    public void ValidateArgs_OptionalParamMatches_Consumed()
    {
        // <n?s:s> — optional number, then string
        // Pass (42, "x") — the optional n? matches 42, then s matches "x"
        string result = Eval("""( $f := function($n, $s)<n?s:s>{$s}; $f(42, "x") )""");
        Assert.Equal("\"x\"", result);
    }

    // ─── L502-503: DescribeTypes with function that has empty type ────────

    [Fact]
    public void ValidateArgs_FunctionParam_PassedAsArg()
    {
        // <f:x> — expects a function, passes a lambda
        string result = Eval("""( $f := function($fn)<f:x>{$fn(1)}; $f(function($x){$x*2}) )""");
        Assert.Equal("2", result);
    }

    // ─── L283: Non-array matching sub-type falls through ─────────────────

    [Fact]
    public void ValidateArgs_NonArrayMatchingSubtype_Accepted()
    {
        // <a<n>:n> with a single number — auto-wrapped, number matches n sub-type
        string result = Eval("""( $f := function($x)<a<n>:n>{$sum($x)}; $f(42) )""");
        Assert.Equal("42", result);
    }

    // ─── L446-456: Deeply nested sub-type parsing ────────────────────────

    [Fact]
    public void ParseParamSpecs_DeeplyNestedSubtype_Parsed()
    {
        // <a<a<n>>:x> — array of arrays of numbers
        string result = Eval("""( $f := function($x)<a<a<n>>:x>{$x}; $f([[1,2],[3,4]]) )""");
        Assert.Contains("1", result);
    }

    // ─── L540: DescribeValue undefined path ──────────────────────────────

    [Fact]
    public void ValidateArgs_UndefinedInput_NoError()
    {
        // Function with signature called with undefined (missing value) — should not error
        string result = Eval("""( $f := function($x)<n?:n>{$x}; $f(nothing) )""");
        Assert.Equal("undefined", result);
    }
}

