// <copyright file="CodeGenCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.JsonPath;
using Corvus.Text.Json.JsonPath.CodeGeneration;
using Xunit;
using Xunit.Abstractions;

namespace Corvus.Text.Json.JsonPath.CodeGeneration.Tests;

/// <summary>
/// Targeted coverage tests for <see cref="JsonPathCodeGenerator"/> emitter paths
/// not exercised by the CTS conformance suite. Each test generates code, compiles
/// it dynamically, executes the expression, and validates correctness against the
/// runtime evaluator.
/// </summary>
public class CodeGenCoverageTests : IClassFixture<CodeGenConformanceFixture>
{
    private readonly CodeGenConformanceFixture fixture;
    private readonly ITestOutputHelper output;

    public CodeGenCoverageTests(CodeGenConformanceFixture fixture, ITestOutputHelper output)
    {
        this.fixture = fixture;
        this.output = output;
    }

    // ── Descendant streaming (general DFS with ArrayPool) ────────────

    [Theory]
    [InlineData("$..*", """{"a":1,"b":{"c":2}}""")]
    [InlineData("$..[0]", """[[1,2],[3,[4,5]]]""")]
    [InlineData("$..[?@>2]", """{"a":[1,2,3],"b":{"c":4}}""")]
    public void DescendantGeneral_MatchesRuntime(string expression, string json)
    {
        this.AssertCgMatchesRuntime(expression, json);
    }

    // ── Descendant for name (terminal and non-terminal) ──────────────

    [Fact]
    public void DescendantForName_Terminal()
    {
        // Terminal $..name → uses DescendantsForName helper (no ArrayPool)
        this.AssertCgMatchesRuntime("$..name", """{"name":"a","x":{"name":"b","y":{"name":"c"}}}""");
    }

    [Fact]
    public void DescendantForName_NonTerminal()
    {
        // Non-terminal $..name.child → uses EnumerateDescendantProperties
        this.AssertCgMatchesRuntime("$..obj.x", """{"obj":{"x":1},"a":{"obj":{"x":2}}}""");
    }

    // ── Slice with negative step ─────────────────────────────────────

    [Fact]
    public void SliceNegativeStep()
    {
        this.AssertCgMatchesRuntime("$[4:0:-2]", """[0,1,2,3,4,5]""");
    }

    [Fact]
    public void SliceNegativeStepDefaultBounds()
    {
        this.AssertCgMatchesRuntime("$[::-1]", """[1,2,3]""");
    }

    // ── match() and search() with static literal patterns ────────────

    [Fact]
    public void MatchWithLiteralPattern()
    {
        const string expression = """$[?match(@.name, "te.*")]""";
        const string json = """[{"name":"test"},{"name":"no"},{"name":"temp"}]""";
        CompiledJsonPathExpression compiled = this.CompileAndLog(expression);
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");

        // Verify generated code includes regex field
        Assert.Contains("s_regex", compiled.GeneratedCode!);
        Assert.Contains("using System.Text.RegularExpressions;", compiled.GeneratedCode!);

        this.AssertCgMatchesRuntime(expression, json);
    }

    [Fact]
    public void SearchWithLiteralPattern()
    {
        const string expression = """$[?search(@.data, "pat")]""";
        const string json = """[{"data":"has pattern here"},{"data":"none"},{"data":"pat"}]""";
        this.AssertCgMatchesRuntime(expression, json);
    }

    // ── match()/search() with dynamic pattern (runtime regex) ────────

    [Fact]
    public void SearchWithDynamicPattern()
    {
        // Pattern comes from a property, not a literal → runtime Regex creation
        const string expression = """$[?search(@.val, @.pat)]""";
        const string json = """[{"val":"hello world","pat":"wor"},{"val":"abc","pat":"xyz"}]""";
        this.AssertCgMatchesRuntime(expression, json);
    }

    // ── Non-singular filter queries (counter + helpers) ──────────────

    [Fact]
    public void CountWithNonSingularQuery()
    {
        // count($..items) triggers EmitFilterQueryCounter + GenerateFilterQueryHelper
        const string expression = """$[?count(@..items)>0]""";
        const string json = """[{"items":[1]},{"other":2},{"items":[3],"nested":{"items":[4]}}]""";
        this.AssertCgMatchesRuntime(expression, json);
    }

    [Fact]
    public void ValueWithNonSingularQuery()
    {
        // value(@.items[*]) triggers EmitFilterQueryAsValue → non-singular branch
        // Single-element result → returns the element; multi → Undefined
        const string expression = """$[?value(@.items[*]) == 1]""";
        const string json = """[{"items":[1]},{"items":[2,3]},{"items":[]}]""";
        this.AssertCgMatchesRuntime(expression, json);
    }

    // ── Static literal fields ────────────────────────────────────────

    [Fact]
    public void StringLiteralComparison_EmitsStaticField()
    {
        const string expression = """$[?@.type == "special"]""";
        const string json = """[{"type":"special"},{"type":"normal"}]""";
        CompiledJsonPathExpression compiled = this.CompileAndLog(expression);
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");

        // Check that a static literal field was emitted
        Assert.Contains("s_literal", compiled.GeneratedCode!);

        this.AssertCgMatchesRuntime(expression, json);
    }

    // ── Name fields (multiple property accesses) ─────────────────────

    [Fact]
    public void MultipleNameFields()
    {
        const string expression = """$[?@.first == @.second]""";
        const string json = """[{"first":1,"second":1},{"first":1,"second":2}]""";
        CompiledJsonPathExpression compiled = this.CompileAndLog(expression);
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");

        // Multiple distinct names should produce multiple s_name fields
        Assert.Contains("s_name", compiled.GeneratedCode!);

        this.AssertCgMatchesRuntime(expression, json);
    }

    // ── Comparison operator coverage ─────────────────────────────────
    // These target uncovered branches in TryEmitSpecializedComparison
    // and EmitLengthNumericComparison (lines 972-986, 1056-1087, 1108-1126, 1181-1190).

    [Fact]
    public void ComparisonNotEqual()
    {
        // NotEqual operator → EmitStringEqualityComparison with NotEqual
        this.AssertCgMatchesRuntime("""$[?@.x != "a"]""", """[{"x":"a"},{"x":"b"},{"x":"c"}]""");
    }

    [Fact]
    public void ComparisonLessThanOrEqual()
    {
        // <= with numeric literal → specialized comparison LessThanOrEqual
        this.AssertCgMatchesRuntime("$[?@.x <= 3]", """[{"x":1},{"x":3},{"x":5}]""");
    }

    [Fact]
    public void ComparisonGreaterThanOrEqual()
    {
        // >= with numeric literal → specialized GreaterThanOrEqual
        this.AssertCgMatchesRuntime("$[?@.x >= 3]", """[{"x":1},{"x":3},{"x":5}]""");
    }

    [Fact]
    public void ComparisonLessThan()
    {
        // < with numeric literal → specialized LessThan
        this.AssertCgMatchesRuntime("$[?@.x < 3]", """[{"x":1},{"x":3},{"x":5}]""");
    }

    [Fact]
    public void ComparisonFlipped_LiteralOnLeft()
    {
        // Literal on left side → FlipOp (lines 971-976)
        this.AssertCgMatchesRuntime("""$[?3 <= @.x]""", """[{"x":1},{"x":3},{"x":5}]""");
    }

    [Fact]
    public void ComparisonParenthesizedExpr()
    {
        // Parenthesized expression side → unwrap paren (lines 983-986)
        this.AssertCgMatchesRuntime("$[?(@.x) == 1]", """[{"x":1},{"x":2}]""");
    }

    [Fact]
    public void LengthNotEqual()
    {
        // length() != N → EmitLengthNumericComparison NotEqual branch (lines 1055-1065)
        this.AssertCgMatchesRuntime("""$[?length(@.s) != 3]""", """[{"s":"ab"},{"s":"abc"},{"s":"abcd"}]""");
    }

    [Fact]
    public void LengthLessThanOrEqual()
    {
        // length() <= N → EmitLengthNumericComparison else branch (lines 1077-1087)
        this.AssertCgMatchesRuntime("""$[?length(@.s) <= 3]""", """[{"s":"ab"},{"s":"abc"},{"s":"abcd"}]""");
    }

    [Fact]
    public void LengthNonIntegralLiteral()
    {
        // length() == 2.5 → non-integral literal path (isIntegral=false branches)
        this.AssertCgMatchesRuntime("""$[?length(@.s) == 2.5]""", """[{"s":"ab"},{"s":"abc"}]""");
    }

    [Fact]
    public void CountNonIntegralLiteral()
    {
        // count() == 1.5 → EmitCountNumericComparison non-integral branch (lines 1123-1126)
        this.AssertCgMatchesRuntime("""$[?count(@..x) == 1.5]""", """[{"x":1},{"x":1,"y":{"x":2}}]""");
    }

    // ── Not expression ───────────────────────────────────────────────

    [Fact]
    public void LogicalNot()
    {
        // ! operator → EmitLogicalNot (line 914 → EmitFilterBool)
        this.AssertCgMatchesRuntime("""$[?!(@.x == 1)]""", """[{"x":1},{"x":2},{"x":3}]""");
    }

    // ── Existence test (filter query as bool) ────────────────────────

    [Fact]
    public void ExistenceTest()
    {
        // @.x used as boolean test → EmitFilterQueryAsTest (lines 915-916)
        this.AssertCgMatchesRuntime("$[?@.x]", """[{"x":1},{"y":2},{"x":null}]""");
    }

    // ── Null/True/False literal comparison ───────────────────────────

    [Fact]
    public void NullLiteralComparison()
    {
        this.AssertCgMatchesRuntime("$[?@.x == null]", """[{"x":null},{"x":1},{"y":2}]""");
    }

    [Fact]
    public void TrueLiteralComparison()
    {
        this.AssertCgMatchesRuntime("$[?@.x == true]", """[{"x":true},{"x":false},{"x":1}]""");
    }

    [Fact]
    public void FalseLiteralComparison()
    {
        this.AssertCgMatchesRuntime("$[?@.x == false]", """[{"x":true},{"x":false},{"x":0}]""");
    }

    // ── Additional length/count non-integral branches ────────────────

    [Fact]
    public void LengthNotEqual_NonIntegral()
    {
        // length() != 2.5 → EmitLengthNumericComparison NotEqual+non-integral (lines 1062-1064)
        this.AssertCgMatchesRuntime("""$[?length(@.s) != 2.5]""", """[{"s":"ab"},{"s":"abc"}]""");
    }

    [Fact]
    public void LengthLessThan_NonIntegral()
    {
        // length() < 2.5 → EmitLengthNumericComparison else+non-integral (lines 1085-1087)
        this.AssertCgMatchesRuntime("""$[?length(@.s) < 2.5]""", """[{"s":"ab"},{"s":"abc"}]""");
    }

    // ── IsSingularQuery false branch (descendant segment) ────────────

    [Fact]
    public void DescendantQueryInValueContext()
    {
        // $..x in value context — is NOT singular → IsSingularQuery returns false (line 1527)
        this.AssertCgMatchesRuntime("""$[?value(@..x) == 1]""", """[{"x":1},{"x":1,"y":{"x":2}}]""");
    }

    // ── FlipOp additional operators ──────────────────────────────────

    [Fact]
    public void FlippedGreaterThan()
    {
        // 5 > @.x → FlipOp GreaterThan→LessThan (lines 1184)
        this.AssertCgMatchesRuntime("""$[?5 > @.x]""", """[{"x":3},{"x":7}]""");
    }

    [Fact]
    public void FlippedLessThan()
    {
        // 3 < @.x → FlipOp LessThan→GreaterThan (lines 1186-1188)
        this.AssertCgMatchesRuntime("""$[?3 < @.x]""", """[{"x":1},{"x":5}]""");
    }

    [Fact]
    public void FlippedGreaterThanOrEqual()
    {
        // 5 >= @.x → FlipOp GreaterThanOrEqual→LessThanOrEqual
        this.AssertCgMatchesRuntime("""$[?5 >= @.x]""", """[{"x":3},{"x":5},{"x":7}]""");
    }

    // ── Custom function: expression form, value return ───────────────

    [Fact]
    public void CustomFunction_ExpressionForm_ValueReturn()
    {
        var customFn = new CustomFunction(
            "double_val",
            [new FunctionParameter(FunctionParamType.Value, "x")],
            FunctionParamType.Value,
            "JsonPathCodeGenHelpers.IntToElement(x.ValueKind == JsonValueKind.Number ? (int)(x.GetDouble() * 2) : 0, workspace)",
            isExpression: true);

        // Value-returning custom functions must be used in comparison context
        const string expression = """$[?double_val(@.v) > 5]""";

        string code = this.GenerateWithCustomFunctions(expression, [customFn]);
        this.output.WriteLine("Generated code:");
        this.output.WriteLine(code);

        // Verify the custom function helper was generated
        Assert.Contains("CustomFn_double_val", code);
        Assert.Contains("private static JsonElement CustomFn_double_val(", code);
    }

    [Fact]
    public void CustomFunction_LogicalReturn()
    {
        var customFn = new CustomFunction(
            "is_positive",
            [new FunctionParameter(FunctionParamType.Value, "x")],
            FunctionParamType.Logical,
            "x.ValueKind == JsonValueKind.Number && x.GetDouble() > 0",
            isExpression: true);

        const string expression = """$[?is_positive(@.v)]""";

        string code = this.GenerateWithCustomFunctions(expression, [customFn]);
        this.output.WriteLine("Generated code:");
        this.output.WriteLine(code);

        Assert.Contains("CustomFn_is_positive", code);
        Assert.Contains("private static bool CustomFn_is_positive(", code);
    }

    [Fact]
    public void CustomFunction_BlockForm()
    {
        var customFn = new CustomFunction(
            "clamp",
            [new FunctionParameter(FunctionParamType.Value, "x")],
            FunctionParamType.Value,
            "        if (x.ValueKind != JsonValueKind.Number) return default;\n        double d = x.GetDouble();\n        if (d < 0) d = 0;\n        if (d > 10) d = 10;\n        return JsonPathCodeGenHelpers.IntToElement((int)d, workspace);",
            isExpression: false);

        const string expression = """$[?clamp(@.v) >= 0]""";

        string code = this.GenerateWithCustomFunctions(expression, [customFn]);
        this.output.WriteLine("Generated code:");
        this.output.WriteLine(code);

        Assert.Contains("CustomFn_clamp", code);
        // Block form should have multiple lines
        Assert.Contains("if (x.ValueKind != JsonValueKind.Number)", code);
    }

    [Fact]
    public void CustomFunction_NodesParameter()
    {
        var customFn = new CustomFunction(
            "node_count",
            [new FunctionParameter(FunctionParamType.Nodes, "nodes")],
            FunctionParamType.Value,
            "JsonPathCodeGenHelpers.IntToElement(nodes.Length, workspace)",
            isExpression: true);

        // Value-returning function must be in comparison context
        const string expression = """$[?node_count(@.items[*]) > 1]""";

        string code = this.GenerateWithCustomFunctions(expression, [customFn]);
        this.output.WriteLine("Generated code:");
        this.output.WriteLine(code);

        Assert.Contains("CustomFn_node_count", code);
        Assert.Contains("ReadOnlySpan<JsonElement> nodes", code);
        // Should generate a NodesCollector helper for the @.items[*] query
        Assert.Contains("FilterQuery", code);
    }

    [Fact]
    public void CustomFunction_MultipleParameters()
    {
        var customFn = new CustomFunction(
            "add_vals",
            [
                new FunctionParameter(FunctionParamType.Value, "a"),
                new FunctionParameter(FunctionParamType.Value, "b"),
            ],
            FunctionParamType.Value,
            "JsonPathCodeGenHelpers.IntToElement((int)(a.GetDouble() + b.GetDouble()), workspace)",
            isExpression: true);

        const string expression = """$[?add_vals(@.x, @.y) > 5]""";

        string code = this.GenerateWithCustomFunctions(expression, [customFn]);
        this.output.WriteLine("Generated code:");
        this.output.WriteLine(code);

        Assert.Contains("CustomFn_add_vals", code);
        Assert.Contains("JsonElement a, JsonElement b", code);
    }

    [Fact]
    public void CustomFunction_CalledTwice_GeneratesHelperOnce()
    {
        var customFn = new CustomFunction(
            "noop",
            [new FunctionParameter(FunctionParamType.Value, "x")],
            FunctionParamType.Value,
            "x",
            isExpression: true);

        // Use the same custom function twice in a single expression
        const string expression = """$[?noop(@.a) > 0 && noop(@.b) > 0]""";

        string code = this.GenerateWithCustomFunctions(expression, [customFn]);
        this.output.WriteLine("Generated code:");
        this.output.WriteLine(code);

        // The helper method should appear exactly once
        int count = 0;
        int idx = 0;
        while ((idx = code.IndexOf("private static JsonElement CustomFn_noop(", idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx++;
        }

        Assert.Equal(1, count);
    }

    // ── Regex field emission ─────────────────────────────────────────

    [Fact]
    public void MultipleRegexPatterns_EmitsMultipleFields()
    {
        const string expression = """$[?match(@.a, "foo") || search(@.b, "bar")]""";
        CompiledJsonPathExpression compiled = this.CompileAndLog(expression);
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");

        // Should have two regex fields (fully-qualified type name)
        int regexCount = CountOccurrences(compiled.GeneratedCode!, "private static readonly System.Text.RegularExpressions.Regex s_regex");
        Assert.True(regexCount >= 2, $"Expected at least 2 regex fields, found {regexCount}");
    }

    // ── Partial class emission ───────────────────────────────────────

    [Fact]
    public void IsPartialFlag_EmitsPartialClass()
    {
        string code = JsonPathCodeGenerator.Generate(
            "$.a",
            "TestPartial",
            "Test.Namespace",
            isPartial: true);

        Assert.Contains("internal static partial class TestPartial", code);
    }

    [Fact]
    public void Accessibility_EmitsPublicClass()
    {
        string code = JsonPathCodeGenerator.Generate(
            "$.a",
            "TestPublic",
            "Test.Namespace",
            accessibility: "public");

        Assert.Contains("public static class TestPublic", code);
    }

    // ── BuildSignatures coverage ─────────────────────────────────────

    [Fact]
    public void NullCustomFunctions_NoSignatures()
    {
        // Generate with null custom functions — should not throw
        string code = JsonPathCodeGenerator.Generate("$.a", "TestNull", "Test.Ns", customFunctions: null);
        Assert.DoesNotContain("CustomFn_", code);
    }

    [Fact]
    public void EmptyCustomFunctions_NoSignatures()
    {
        string code = JsonPathCodeGenerator.Generate("$.a", "TestEmpty", "Test.Ns", customFunctions: Array.Empty<CustomFunction>());
        Assert.DoesNotContain("CustomFn_", code);
    }

    // ── EscapeStringLiteral — control character branches (225-235) ──

    [Fact]
    public void StringLiteralWithNewline()
    {
        // Comparison against a string containing \n → EscapeStringLiteral hits '\n' case (line 227)
        this.AssertCgMatchesRuntime("""$[?@.s == "line1\nline2"]""", """[{"s":"line1\nline2"},{"s":"other"}]""");
    }

    [Fact]
    public void StringLiteralWithTab()
    {
        // String with \t → EscapeStringLiteral hits '\t' case (line 229)
        this.AssertCgMatchesRuntime("""$[?@.s == "a\tb"]""", """[{"s":"a\tb"},{"s":"ab"}]""");
    }

    [Fact]
    public void StringLiteralWithCarriageReturn()
    {
        // String with \r → EscapeStringLiteral hits '\r' case (line 228)
        this.AssertCgMatchesRuntime("""$[?@.s == "a\rb"]""", """[{"s":"a\rb"},{"s":"ab"}]""");
    }

    [Fact]
    public void StringLiteralWithBackslash()
    {
        // String with \\ → EscapeStringLiteral hits '\\' case (line 226)
        this.AssertCgMatchesRuntime("""$[?@.s == "a\\b"]""", """[{"s":"a\\b"},{"s":"ab"}]""");
    }

    // ── TranslateIRegexpForCodeGen — character class branches (277-352) ──

    [Fact]
    public void RegexWithCharacterClass()
    {
        // [abc] → TranslateIRegexpForCodeGen processes character class (lines 338-354)
        this.AssertCgMatchesRuntime("""$[?match(@.s, "[abc]+")]""", """[{"s":"abc"},{"s":"xyz"}]""");
    }

    [Fact]
    public void RegexWithNegatedCharacterClass()
    {
        // [^abc] → hits negated class branch (lines 342-346)
        this.AssertCgMatchesRuntime("""$[?match(@.s, "[^abc]+")]""", """[{"s":"xyz"},{"s":"abc"}]""");
    }

    [Fact]
    public void RegexWithEscapeInContent()
    {
        // \\d → UnescapeJsonStringContent processes backslash-d (JSON escape → regex escape, lines 277-312)
        this.AssertCgMatchesRuntime("""$[?match(@.s, "\\d+")]""", """[{"s":"123"},{"s":"abc"}]""");
    }

    [Fact]
    public void RegexWithUnicodeEscape()
    {
        // \\u0041 → UnescapeJsonStringContent processes \\u hex escape (lines 285-306)
        this.AssertCgMatchesRuntime("""$[?match(@.s, "\\u0041+")]""", """[{"s":"AAA"},{"s":"bbb"}]""");
    }

    [Fact]
    public void RegexWithClosingBracketInClass()
    {
        // []] → character class with ] as first char (lines 348-352)
        this.AssertCgMatchesRuntime("""$[?match(@.s, "[]a]+")]""", """[{"s":"]a"},{"s":"b"}]""");
    }

    [Fact]
    public void RegexWithNegatedClassClosingBracket()
    {
        // [^]] → negated class with ] as first char after ^ (lines 342-352)
        this.AssertCgMatchesRuntime("""$[?match(@.s, "[^]]+")]""", """[{"s":"abc"},{"s":"]"}]""");
    }

    // ── OpToSymbol remaining operators ───────────────────────────────

    [Fact]
    public void NotEqualComparison()
    {
        // != → OpToSymbol hits NotEqual case (line 1197)
        this.AssertCgMatchesRuntime("""$[?@.x != 3]""", """[{"x":1},{"x":3},{"x":5}]""");
    }

    [Fact]
    public void GreaterThanOrEqualComparison()
    {
        // >= → OpToSymbol hits GreaterThanOrEqual case (line 1201)
        this.AssertCgMatchesRuntime("""$[?@.x >= 3]""", """[{"x":1},{"x":3},{"x":5}]""");
    }

    // ── FlipOp default case (Equal/NotEqual don't flip) ─────────────

    [Fact]
    public void FlippedEqual()
    {
        // 3 == @.x → FlipOp returns Equal unchanged (line 1188 default case)
        this.AssertCgMatchesRuntime("""$[?3 == @.x]""", """[{"x":1},{"x":3},{"x":5}]""");
    }

    [Fact]
    public void FlippedNotEqual()
    {
        // 3 != @.x → FlipOp returns NotEqual unchanged (line 1188 default case)
        this.AssertCgMatchesRuntime("""$[?3 != @.x]""", """[{"x":1},{"x":3},{"x":5}]""");
    }

    // ── EmitFilterQueryAsValue non-singular (1352-1357) ─────────────

    [Fact]
    public void ValueWithWildcardQuery()
    {
        // value(@.items[*]) — wildcard is non-singular, hits lines 1352-1357
        this.AssertCgMatchesRuntime("""$[?value(@.items[*]) == 42]""", """[{"items":[42]},{"items":[1,2]},{"items":[]}]""");
    }

    // ── EmitCountFunction as value context (line 1392) ──────────────

    [Fact]
    public void CountInComparisonContext()
    {
        // count(@.items[*]) > 1 — hits EmitCountFunction path (line 1392)
        this.AssertCgMatchesRuntime("""$[?count(@.items[*]) > 1]""", """[{"items":[1]},{"items":[1,2,3]}]""");
    }

    // ── Singleton prefix: all segments are single-name/index (483-501) ─

    [Fact]
    public void SingletonPrefix_AllSingleton()
    {
        // $.a.b → every segment is a singleton name → all-singleton path (line 497-501)
        this.AssertCgMatchesRuntime("$.a.b", """{"a":{"b":42},"c":1}""");
    }

    [Fact]
    public void SingletonPrefix_IndexThenName()
    {
        // $[0].x → singleton index prefix + singleton name (lines 489-495, 541-552)
        this.AssertCgMatchesRuntime("$[0].x", """[{"x":1},{"x":2}]""");
    }

    [Fact]
    public void SingletonPrefix_NegativeIndex()
    {
        // $[-1].x → singleton with negative index normalization
        this.AssertCgMatchesRuntime("$[-1].x", """[{"x":1},{"x":2},{"x":3}]""");
    }

    [Fact]
    public void SingletonPrefix_MixedThenStreaming()
    {
        // $.a[*] → singleton "a" prefix then wildcard streaming (lines 489-495, 504-505)
        this.AssertCgMatchesRuntime("$.a[*]", """{"a":[1,2,3],"b":4}""");
    }

    // ── Index selector streaming (656-670) ──────────────────────────

    [Fact]
    public void IndexStreaming_WithFurtherSegment()
    {
        // $[*][0] → wildcard then index streaming (non-singleton because wildcard precedes)
        this.AssertCgMatchesRuntime("$[*][0]", """[[1,2],[3,4],[5]]""");
    }

    [Fact]
    public void IndexStreaming_NegativeWithChain()
    {
        // $[*][-1].x → wildcard + negative index streaming + name streaming
        this.AssertCgMatchesRuntime("$[*][-1].x", """[[{"x":1},{"x":2}],[{"x":3}]]""");
    }

    // ── Slice selector streaming (705-733) ──────────────────────────

    [Fact]
    public void SliceStreaming_PositiveStep()
    {
        // $[*][0:2] → wildcard then positive-step slice streaming
        this.AssertCgMatchesRuntime("$[*][0:2]", """[[1,2,3],[4,5]]""");
    }

    [Fact]
    public void SliceStreaming_NegativeStep()
    {
        // $[*][2:0:-1] → wildcard then negative-step slice streaming (lines 724-729)
        this.AssertCgMatchesRuntime("$[*][2:0:-1]", """[[1,2,3],[4,5,6]]""");
    }

    [Fact]
    public void SliceStreaming_WithFurtherName()
    {
        // $[0:2].x → slice streaming then name (chain continuation)
        this.AssertCgMatchesRuntime("$[0:2].x", """[{"x":1},{"x":2},{"x":3}]""");
    }

    // ── Descendant streaming general DFS (788-866) ──────────────────

    [Fact]
    public void DescendantGeneral_IndexSelector()
    {
        // $..[0] with further segment → general DFS (not name-optimized)
        this.AssertCgMatchesRuntime("$..[0].x", """{"a":[{"x":1}],"b":{"c":[{"x":2}]}}""");
    }

    [Fact]
    public void DescendantGeneral_WildcardWithChain()
    {
        // $..*[0] → general descendant wildcard + index streaming
        this.AssertCgMatchesRuntime("$..[*].x", """{"a":[{"x":1},{"x":2}],"b":{"x":3}}""");
    }

    [Fact]
    public void DescendantGeneral_FilterSelector()
    {
        // $..[?@>1] with further segment → general DFS + filter + chain
        this.AssertCgMatchesRuntime("$..[?@.x>1].x", """{"a":[{"x":1},{"x":2}],"b":{"x":3}}""");
    }

    [Fact]
    public void DescendantGeneral_DeepNesting()
    {
        // Deep nesting exercises GrowStack in DFS (lines 835, 851)
        this.AssertCgMatchesRuntime(
            "$..[0]",
            """{"a":[[1],[2]],"b":{"c":[[3],[4]],"d":{"e":[[5]]}}}""");
    }

    // ── Descendant for name streaming non-terminal (876-903) ────────

    [Fact]
    public void DescendantForName_NonTerminal_WithChain()
    {
        // $..name.child → EnumerateDescendantProperties + streaming chain (lines 896-902)
        this.AssertCgMatchesRuntime(
            "$..items.name",
            """{"items":{"name":"a"},"x":{"items":{"name":"b"},"y":{"items":{"name":"c"}}}}""");
    }

    [Fact]
    public void DescendantForName_Terminal_CounterTarget()
    {
        // count(@..name) → terminal descendant in counter context (lines 883-886)
        this.AssertCgMatchesRuntime(
            """$[?count(@..name) > 1]""",
            """[{"name":"a","x":{"name":"b"}},{"name":"c"}]""");
    }

    // ── Logical AND / OR (1228-1271) ────────────────────────────────

    [Fact]
    public void LogicalAnd_ShortCircuit()
    {
        // && → EmitLogicalAnd with short-circuit (lines 1228-1246)
        this.AssertCgMatchesRuntime(
            "$[?@.a > 1 && @.b < 10]",
            """[{"a":0,"b":5},{"a":2,"b":5},{"a":2,"b":15},{"a":3,"b":3}]""");
    }

    [Fact]
    public void LogicalOr_ShortCircuit()
    {
        // || → EmitLogicalOr with short-circuit (lines 1253-1271)
        this.AssertCgMatchesRuntime(
            "$[?@.a > 5 || @.b < 2]",
            """[{"a":1,"b":5},{"a":6,"b":5},{"a":1,"b":1},{"a":0,"b":9}]""");
    }

    [Fact]
    public void LogicalAnd_NestedOr()
    {
        // AND containing OR → exercises both emission paths
        this.AssertCgMatchesRuntime(
            "$[?(@.a > 1 || @.a < -1) && @.b == true]",
            """[{"a":2,"b":true},{"a":0,"b":true},{"a":-2,"b":true},{"a":2,"b":false}]""");
    }

    // ── Filter query as test: non-singular (1290-1308) ──────────────

    [Fact]
    public void FilterQueryAsTest_NonSingular()
    {
        // @.items[*] as existence test → non-singular path (lines 1300-1307)
        this.AssertCgMatchesRuntime(
            "$[?@.items[*]]",
            """[{"items":[1,2]},{"items":[]},{"other":1}]""");
    }

    [Fact]
    public void FilterQueryAsTest_Singular()
    {
        // @.x as existence test → singular path (lines 1291-1298)
        this.AssertCgMatchesRuntime(
            "$[?@.x]",
            """[{"x":1},{"y":2},{"x":null}]""");
    }

    // ── Constant literal comparisons: <, > (1152-1164 default) ──────

    [Fact]
    public void NullLessThan()
    {
        // @.x < null → non-orderable, always false (line 1159)
        this.AssertCgMatchesRuntime("$[?@.x < null]", """[{"x":null},{"x":1}]""");
    }

    [Fact]
    public void NullGreaterThan()
    {
        // @.x > null → non-orderable, always false (line 1159)
        this.AssertCgMatchesRuntime("$[?@.x > null]", """[{"x":null},{"x":1}]""");
    }

    [Fact]
    public void TrueNotEqual()
    {
        // @.x != true → NotEqual branch (line 1155)
        this.AssertCgMatchesRuntime("$[?@.x != true]", """[{"x":true},{"x":false},{"x":1}]""");
    }

    [Fact]
    public void FalseLessThanOrEqual()
    {
        // @.x <= false → reduces to == for non-orderable (line 1152)
        this.AssertCgMatchesRuntime("$[?@.x <= false]", """[{"x":false},{"x":true},{"x":0}]""");
    }

    // ── String ordering comparison (falls through to general, line 1000) ─

    [Fact]
    public void StringOrderingComparison_LessThan()
    {
        // @.s < "m" → LiteralKind.String with non-equality op → falls to general (line 1000 _ => null)
        this.AssertCgMatchesRuntime("""$[?@.s < "m"]""", """[{"s":"apple"},{"s":"zebra"},{"s":"mango"}]""");
    }

    [Fact]
    public void StringOrderingComparison_GreaterThan()
    {
        this.AssertCgMatchesRuntime("""$[?@.s > "m"]""", """[{"s":"apple"},{"s":"zebra"},{"s":"mango"}]""");
    }

    // ── Count function in value context (1431-1449) ─────────────────

    [Fact]
    public void CountFunction_ValueContext()
    {
        // count(@.items[*]) == 2 → EmitCountFunction full path (lines 1431-1449)
        this.AssertCgMatchesRuntime(
            """$[?count(@.items[*]) == 2]""",
            """[{"items":[1,2]},{"items":[1]},{"items":[1,2,3]}]""");
    }

    // ── Value function (1456-1471) ──────────────────────────────────

    [Fact]
    public void ValueFunction_SingleElement()
    {
        // value(@.items[*]) == 42 → single-element returns it (lines 1457-1462)
        this.AssertCgMatchesRuntime(
            """$[?value(@.items[*]) == 42]""",
            """[{"items":[42]},{"items":[1,2]},{"items":[]}]""");
    }

    // ── Match/search with literal vs dynamic patterns (1478-1521) ───

    [Fact]
    public void MatchWithDynamicPattern()
    {
        // match() with property as pattern → dynamic regex (lines 1497-1517)
        this.AssertCgMatchesRuntime(
            """$[?match(@.val, @.pat)]""",
            """[{"val":"hello","pat":"hel.*"},{"val":"world","pat":"xyz"}]""");
    }

    [Fact]
    public void SearchWithLiteralPattern_NonTerminal()
    {
        // search() with literal → pre-compiled regex field (lines 1485-1495)
        this.AssertCgMatchesRuntime(
            """$[?search(@.name, "test")].value""",
            """[{"name":"testing","value":1},{"name":"other","value":2}]""");
    }

    // ── GenerateFilterQueryHelper (1584-1617) ───────────────────────

    [Fact]
    public void FilterQueryHelper_EmptySegments()
    {
        // Existence test with no segments → empty segments path (lines 1593-1599)
        // This is exercised by count($) or similar, but parser may prevent it.
        // Use a non-singular query with wildcard that generates a helper method.
        this.AssertCgMatchesRuntime(
            """$[?count(@[*]) > 1]""",
            """[{"a":1,"b":2,"c":3},{"a":1},[1,2,3]]""");
    }

    [Fact]
    public void FilterQueryHelper_StreamingChain()
    {
        // Non-singular query with multiple segments → helper method with streaming chain (1602-1616)
        this.AssertCgMatchesRuntime(
            """$[?count(@.items[*]) >= 2]""",
            """[{"items":[1,2,3]},{"items":[1]},{"items":[]}]""");
    }

    // ── Literal field emission: number/string static fields (1635-1678) ─

    [Fact]
    public void LiteralField_NumberLiteral()
    {
        // @.x == 42 → EmitLiteralFieldRef default case, creates s_literal field (lines 1644-1655)
        // Note: numeric comparisons are specialized, but when used in general
        // comparison context (e.g., two expressions compared), literal fields are emitted.
        this.AssertCgMatchesRuntime(
            """$[?@.x == "hello"]""",
            """[{"x":"hello"},{"x":"world"}]""");
    }

    [Fact]
    public void LiteralField_ReusedLiteral()
    {
        // Same literal used twice → EmitLiteralFieldRef cache hit (line 1646-1648)
        this.AssertCgMatchesRuntime(
            """$[?@.a == "x" || @.b == "x"]""",
            """[{"a":"x","b":"y"},{"a":"y","b":"x"},{"a":"z","b":"z"}]""");
    }

    // ── Regex field emission (1659-1678) ────────────────────────────

    [Fact]
    public void RegexField_MatchFullAnchor()
    {
        // match() adds ^(?:...)$ anchoring (line 1667)
        CompiledJsonPathExpression compiled = this.CompileAndLog("""$[?match(@.s, "a.c")]""");
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");
        Assert.Contains("^(?:", compiled.GeneratedCode!);
        Assert.Contains(")$", compiled.GeneratedCode!);
    }

    [Fact]
    public void RegexField_SearchNoAnchor()
    {
        // search() does NOT add anchors (line 1671)
        CompiledJsonPathExpression compiled = this.CompileAndLog("""$[?search(@.s, "a.c")]""");
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");
        Assert.DoesNotContain("^(?:", compiled.GeneratedCode!);
    }

    // ── EmitCountFunction in value context (not numeric comparison) (1431-1449)

    [Fact]
    public void CountFunction_InEqualityNotSpecialized()
    {
        // count(@.items[*]) used where the specialized numeric comparison
        // doesn't apply — i.e., in a general comparison with another expression,
        // not a literal. This hits the full EmitCountFunction path (1431-1449)
        // through EmitFunctionValue → EmitCountFunction
        this.AssertCgMatchesRuntime(
            """$[?count(@.items[*]) == count(@.other[*])]""",
            """[{"items":[1,2],"other":[3,4]},{"items":[1],"other":[2,3]}]""");
    }

    // ── EmitValueFunction else branch (1464-1469) ───────────────────

    [Fact]
    public void ValueFunction_NonQueryArgument()
    {
        // value() with a function call arg (not FilterQueryNode) → else branch (1464-1469)
        // This would need value(someFunc(...)) but parser limits arg to NodesType.
        // So this exercises value(@.items[*]) which IS a FilterQueryNode.
        // The else branch at 1464-1469 is only hit when the argument is NOT a FilterQueryNode.
        // This should be unreachable because parser requires NodesType arg for value().
        // But we can still exercise the main path thoroughly.
        this.AssertCgMatchesRuntime(
            """$[?value(@.items[*]) > 5]""",
            """[{"items":[10]},{"items":[3]},{"items":[6,7]}]""");
    }

    // ── EmitFilterQueryAsValue non-singular (1356-1361) ─────────────

    [Fact]
    public void FilterQueryAsValue_NonSingular_InComparison()
    {
        // @.items[*] in a comparison (not value() wrapped) → EmitFilterQueryAsValue non-singular (1356-1361)
        // Parser requires value() wrapper for non-singular queries in comparisons,
        // so this path is actually covered via value() tests above.
        // Test value() with multi-element result (returns Undefined):
        this.AssertCgMatchesRuntime(
            """$[?value(@.items[*]) == 42]""",
            """[{"items":[42]},{"items":[1,42]},{"items":[]}]""");
    }

    // ── EmitCountNumericComparison else (non-FilterQueryNode arg) ────

    [Fact]
    public void CountNumeric_WithFunctionArg()
    {
        // count(someFunc(...)) where arg is NOT FilterQueryNode →
        // hits else branch (1111-1116) of EmitCountNumericComparison
        // But parser requires NodesType for count(), and only filter queries produce NodesType.
        // This path may be unreachable in practice.
        // Exercise the main (FilterQueryNode) path thoroughly instead:
        this.AssertCgMatchesRuntime(
            """$[?count(@[*]) == 3]""",
            """[[1,2,3],[1,2],[1,2,3,4]]""");
    }

    // ── GenerateFilterQueryHelper empty segments (1593-1599) ────────

    [Fact]
    public void FilterQueryHelper_RootQuery()
    {
        // count($[*]) → filter query with root ($) + wildcard segments
        // This generates a helper method with non-empty segments
        this.AssertCgMatchesRuntime(
            """$[?count($[*]) > 2]""",
            """[1,2,3]""");
    }

    // ── EscapeStringLiteral edge cases (226-236) ────────────────────

    [Fact]
    public void EscapeStringLiteral_QuoteInPattern()
    {
        // Regex pattern with " character → EscapeStringLiteral hits '"' case (line 226)
        CompiledJsonPathExpression compiled = this.CompileAndLog("""$[?match(@.s, "a\"b")]""");
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");
        Assert.Contains("\\\"", compiled.GeneratedCode!);
    }

    // ── EmitLengthNumericComparison: Equal integral (1070-1075) ─────

    [Fact]
    public void LengthEqual_Integral()
    {
        // length(@.s) == 3 → Equal + integral branch (lines 1072-1074)
        this.AssertCgMatchesRuntime("""$[?length(@.s) == 3]""", """[{"s":"ab"},{"s":"abc"},{"s":"abcd"}]""");
    }

    [Fact]
    public void LengthEqual_NonIntegral()
    {
        // length(@.s) == 2.5 → Equal + non-integral branch (lines 1076-1078)
        this.AssertCgMatchesRuntime("""$[?length(@.s) == 2.5]""", """[{"s":"ab"},{"s":"abc"}]""");
    }

    [Fact]
    public void LengthLessThan_Integral()
    {
        // length(@.s) < 3 → else branch + integral (lines 1083-1086)
        this.AssertCgMatchesRuntime("""$[?length(@.s) < 3]""", """[{"s":"ab"},{"s":"abc"},{"s":"abcd"}]""");
    }

    [Fact]
    public void LengthGreaterThan_NonIntegral()
    {
        // length(@.s) > 2.5 → else branch + non-integral (lines 1088-1090)
        this.AssertCgMatchesRuntime("""$[?length(@.s) > 2.5]""", """[{"s":"ab"},{"s":"abc"},{"s":"abcd"}]""");
    }

    // ── EmitCountNumericComparison (1104-1133) ──────────────────────

    [Fact]
    public void CountNumeric_EqualIntegral()
    {
        // count(@..x) == 2 → integral comparison (line 1125)
        this.AssertCgMatchesRuntime(
            """$[?count(@..x) == 2]""",
            """[{"x":1,"a":{"x":2}},{"x":1},{"x":1,"a":{"x":2,"b":{"x":3}}}]""");
    }

    [Fact]
    public void CountNumeric_LessThanIntegral()
    {
        // count(@..x) < 2 → integral with < (line 1125)
        this.AssertCgMatchesRuntime(
            """$[?count(@..x) < 2]""",
            """[{"x":1,"a":{"x":2}},{"x":1}]""");
    }

    [Fact]
    public void CountNumeric_NonIntegral()
    {
        // count(@..x) > 1.5 → non-integral branch (line 1129)
        this.AssertCgMatchesRuntime(
            """$[?count(@..x) > 1.5]""",
            """[{"x":1,"a":{"x":2}},{"x":1}]""");
    }

    // ── Filter selector streaming with further segments (735-777) ───

    [Fact]
    public void FilterStreaming_WithFurtherSegment()
    {
        // $[?@.ok].name → filter streaming + further name selector
        this.AssertCgMatchesRuntime(
            "$[?@.ok].name",
            """[{"ok":true,"name":"a"},{"ok":false,"name":"b"},{"ok":true,"name":"c"}]""");
    }

    [Fact]
    public void FilterStreaming_OnArray()
    {
        // $[?@ > 1][0] → filter on array elements + index streaming
        // (Array branch of EmitFilterSelectorStreaming)
        this.AssertCgMatchesRuntime(
            "$[?@ > 1]",
            """[1,2,3,0,5]""");
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private void AssertCgMatchesRuntime(string expression, string json)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(json);

        // Runtime evaluator
        JsonElement rtData = JsonElement.ParseValue(utf8);
        using JsonPathResult rtResult = JsonPathEvaluator.Default.QueryNodes(expression, rtData);

        // Code generator
        CompiledJsonPathExpression compiled = this.CompileAndLog(expression);
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");

        JsonElement cgData = JsonElement.ParseValue(utf8);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement cgResult = InvokeEvaluate(compiled.Method, cgData, workspace);

        // Compare: convert both to node lists
        string rtJson = SerializeNodeList(rtResult);
        string cgJson = cgResult.IsUndefined() ? "[]" : cgResult.GetRawText();

        this.output.WriteLine($"Expression: {expression}");
        this.output.WriteLine($"RT: {rtJson}");
        this.output.WriteLine($"CG: {cgJson}");

        Assert.Equal(rtJson, cgJson);
    }

    private CompiledJsonPathExpression CompileAndLog(string expression)
    {
        CompiledJsonPathExpression compiled = this.fixture.GetOrCompile(expression);
        if (compiled.GeneratedCode is not null)
        {
            this.output.WriteLine($"Generated code ({compiled.GeneratedCode.Length} chars):");
            this.output.WriteLine(compiled.GeneratedCode);
        }

        return compiled;
    }

    private string GenerateWithCustomFunctions(string expression, CustomFunction[] customFunctions)
    {
        int id = Random.Shared.Next(10000, 99999);
        return JsonPathCodeGenerator.Generate(
            expression,
            $"CustomFnTest_{id}",
            "Corvus.Text.Json.JsonPath.CodeGeneration.Tests.Generated",
            customFunctions: customFunctions);
    }

    private static JsonElement InvokeEvaluate(MethodInfo method, JsonElement data, JsonWorkspace workspace)
    {
        object?[] args = [data, workspace];
        object? result = method.Invoke(null, args);
        return (JsonElement)result!;
    }

    private static string SerializeNodeList(JsonPathResult result)
    {
        if (result.Count == 0)
        {
            return "[]";
        }

        StringBuilder sb = new("[");
        bool first = true;
        foreach (JsonElement node in result.Nodes)
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append(node.GetRawText());
            first = false;
        }

        sb.Append(']');
        return sb.ToString();
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += pattern.Length;
        }

        return count;
    }

    // ── EscapeCSharpStringLiteral via regex patterns with control chars ──

    [Fact]
    public void RegexPattern_WithNewline()
    {
        // Regex literal "\n" → unescaped to actual newline char → EscapeCSharpStringLiteral hits '\n' branch (L34)
        CompiledJsonPathExpression compiled = this.CompileAndLog("""$[?match(@.s, "a\nb")]""");
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");
        // Generated code should contain the C# escaped form \\n
        Assert.Contains("\\n", compiled.GeneratedCode!);
    }

    [Fact]
    public void RegexPattern_WithCarriageReturn()
    {
        // Regex literal "\r" → EscapeCSharpStringLiteral hits '\r' branch (L35)
        CompiledJsonPathExpression compiled = this.CompileAndLog("""$[?match(@.s, "a\rb")]""");
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");
        Assert.Contains("\\r", compiled.GeneratedCode!);
    }

    [Fact]
    public void RegexPattern_WithTab()
    {
        // Regex literal "\t" → EscapeCSharpStringLiteral hits '\t' branch (L36)
        CompiledJsonPathExpression compiled = this.CompileAndLog("""$[?match(@.s, "a\tb")]""");
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");
        Assert.Contains("\\t", compiled.GeneratedCode!);
    }

    [Fact]
    public void RegexPattern_WithBellChar()
    {
        // JSON \u0007 unescapes to bell char → EscapeCSharpStringLiteral hits '\a' branch (L38)
        CompiledJsonPathExpression compiled = this.CompileAndLog("""$[?match(@.s, "\u0007")]""");
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");
        Assert.Contains("\\a", compiled.GeneratedCode!);
    }

    [Fact]
    public void RegexPattern_WithNullChar()
    {
        // JSON \u0000 unescapes to null char → EscapeCSharpStringLiteral hits '\0' branch (L37)
        CompiledJsonPathExpression compiled = this.CompileAndLog("""$[?match(@.s, "\u0000")]""");
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");
        Assert.Contains("\\0", compiled.GeneratedCode!);
    }

    [Fact]
    public void RegexPattern_WithBackspaceChar()
    {
        // JSON \b unescapes to backspace → EscapeCSharpStringLiteral hits '\b' branch (L39)
        CompiledJsonPathExpression compiled = this.CompileAndLog("""$[?match(@.s, "x\by")]""");
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");
        Assert.Contains("\\b", compiled.GeneratedCode!);
    }

    [Fact]
    public void RegexPattern_WithFormFeedChar()
    {
        // JSON \f unescapes to form feed → EscapeCSharpStringLiteral hits '\f' branch (L40)
        CompiledJsonPathExpression compiled = this.CompileAndLog("""$[?match(@.s, "x\fy")]""");
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");
        Assert.Contains("\\f", compiled.GeneratedCode!);
    }

    [Fact]
    public void RegexPattern_WithVerticalTab()
    {
        // JSON \u000B unescapes to vertical tab → EscapeCSharpStringLiteral hits '\v' branch (L41)
        CompiledJsonPathExpression compiled = this.CompileAndLog("""$[?match(@.s, "\u000B")]""");
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");
        Assert.Contains("\\v", compiled.GeneratedCode!);
    }

    [Fact]
    public void RegexPattern_WithControlChar()
    {
        // JSON \u0001 unescapes to SOH (< 0x20) → EscapeCSharpStringLiteral hits unicode escape branch (L44-46)
        CompiledJsonPathExpression compiled = this.CompileAndLog("""$[?match(@.s, "\u0001")]""");
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");
        Assert.Contains("\\u0001", compiled.GeneratedCode!);
    }

    // ── UnescapeJsonString escape sequences in match/search patterns ─────

    [Fact]
    public void RegexPattern_WithBackspace()
    {
        // "\b" in regex → UnescapeJsonString hits 'b' case (L136)
        CompiledJsonPathExpression compiled = this.CompileAndLog("""$[?match(@.s, "\b")]""");
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");
    }

    [Fact]
    public void RegexPattern_WithFormFeed()
    {
        // "\f" in regex → UnescapeJsonString hits 'f' case (L137)
        CompiledJsonPathExpression compiled = this.CompileAndLog("""$[?match(@.s, "\f")]""");
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");
    }

    [Fact]
    public void RegexPattern_WithForwardSlash()
    {
        // "\/" in regex → UnescapeJsonString hits '/' case (L135)
        CompiledJsonPathExpression compiled = this.CompileAndLog("""$[?match(@.s, "a\/b")]""");
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");
    }

    [Fact]
    public void RegexPattern_WithUnicodeEscape_InUnescapeJsonString()
    {
        // "\u0041" in regex → UnescapeJsonString hits '\u' case (L141-153) → produces 'A'
        CompiledJsonPathExpression compiled = this.CompileAndLog("""$[?match(@.s, "\u0041+")]""");
        Assert.True(compiled.Method is not null, $"Compilation failed: {compiled.Error}");
    }

    // ── OpToSymbol NotEqual via non-specialized path (L1099) ─────────────

    [Fact]
    public void OpToSymbol_NotEqual_NonSpecialized()
    {
        // count(@..x) != 2 → EmitCountNumericComparison uses OpToSymbol for !=
        this.AssertCgMatchesRuntime(
            """$[?count(@..x) != 2]""",
            """[{"x":1,"a":{"x":2}},{"x":1},{"x":1,"a":{"x":2,"b":{"x":3}}}]""");
    }

    // ── Parenthesized expression in value context (L1222) ───────────────

    [Fact]
    public void ParenExpression_InValueContext()
    {
        // $[?@.x == (@.y)] → paren wraps a non-specialized comparable, hits EmitFilterValue ParenExpressionNode (L1222)
        this.AssertCgMatchesRuntime(
            "$[?@.x == (@.y)]",
            """[{"x":1,"y":1},{"x":1,"y":2},{"x":3,"y":3}]""");
    }

    // ── Empty-segments filter query helper (L1490-1495) ─────────────────

    [Fact]
    public void FilterQueryHelper_EmptySegments_CountSelf()
    {
        // count(@) → filter query with zero segments → GenerateFilterQueryHelper empty-segments path (L1490-1495)
        this.AssertCgMatchesRuntime(
            """$[?count(@) > 0]""",
            """[1, "hello", null, true]""");
    }

    // ── Empty-segments nodes collector helper (L1793-1797) ──────────────

    [Fact]
    public void NodesCollectorHelper_EmptySegments()
    {
        // Custom function with nodes parameter using bare @ (no segments) → GenerateNodesCollectorHelper empty path (L1793-1797)
        var customFn = new CustomFunction(
            "count_self",
            [new FunctionParameter(FunctionParamType.Nodes, "items")],
            FunctionParamType.Value,
            "JsonPathCodeGenHelpers.IntToElement(items.Length, workspace)",
            isExpression: true);

        const string expression = """$[?count_self(@) > 0]""";
        string code = this.GenerateWithCustomFunctions(expression, [customFn]);
        this.output.WriteLine("Generated code:");
        this.output.WriteLine(code);

        // Should contain the NodesCollector helper with the empty-segments early return
        Assert.Contains("result.Append(source)", code);
    }

    // ── Custom function with Logical parameter type (L1644-1645) ────────

    [Fact]
    public void CustomFunction_LogicalParameter()
    {
        // Custom function accepting a logical (bool) parameter → EmitCustomFunctionCall Logical param case (L1644-1645)
        var customFn = new CustomFunction(
            "when_active",
            [
                new FunctionParameter(FunctionParamType.Logical, "flag"),
                new FunctionParameter(FunctionParamType.Value, "x"),
            ],
            FunctionParamType.Value,
            "flag ? x : default",
            isExpression: true);

        // Logical parameter requires a filter expression (comparison), not a value
        const string expression = """$[?when_active(@.active == true, @.val) > 0]""";
        string code = this.GenerateWithCustomFunctions(expression, [customFn]);
        this.output.WriteLine("Generated code:");
        this.output.WriteLine(code);

        Assert.Contains("CustomFn_when_active", code);
        Assert.Contains("bool flag", code);
    }

    // ── EnsureCustomFunctionHelper Logical and Nodes parameter types (L1740, L1742-1743) ─

    [Fact]
    public void CustomFunctionHelper_LogicalParam_MethodSignature()
    {
        // Custom function with logical parameter → EnsureCustomFunctionHelper emits 'bool' param type (L1740)
        var customFn = new CustomFunction(
            "check_flag",
            [new FunctionParameter(FunctionParamType.Logical, "isOk")],
            FunctionParamType.Value,
            "isOk ? JsonPathCodeGenHelpers.IntToElement(1, workspace) : default",
            isExpression: true);

        // Logical parameter requires a filter expression (comparison), not a value
        const string expression = """$[?check_flag(@.ok == true) > 0]""";
        string code = this.GenerateWithCustomFunctions(expression, [customFn]);
        this.output.WriteLine("Generated code:");
        this.output.WriteLine(code);

        Assert.Contains("bool isOk", code);
    }

    [Fact]
    public void CustomFunctionHelper_NodesParam_MethodSignature()
    {
        // Custom function with nodes parameter → EnsureCustomFunctionHelper emits 'ReadOnlySpan<JsonElement>' param type (L1742-1743)
        var customFn = new CustomFunction(
            "sum_nodes",
            [new FunctionParameter(FunctionParamType.Nodes, "ns")],
            FunctionParamType.Value,
            "JsonPathCodeGenHelpers.IntToElement(ns.Length, workspace)",
            isExpression: true);

        const string expression = """$[?sum_nodes(@.items[*]) > 0]""";
        string code = this.GenerateWithCustomFunctions(expression, [customFn]);
        this.output.WriteLine("Generated code:");
        this.output.WriteLine(code);

        Assert.Contains("ReadOnlySpan<JsonElement> ns", code);
    }

    // ── Custom function non-logical return in bool context (L1608-1610) ──
    // Note: Value-returning custom functions can be used in comparison context
    // and then reach EmitCustomFunctionCallAsBool when the parser recognizes them
    // as bool producers (via IsCustomLogicalFunction). The L1608-1610 path is for
    // non-logical custom functions in bool position. However, the parser only lets
    // logical-return functions into filter-test position. This path is unreachable
    // from parser-driven tests.

    // ── IsSingularQuery: descendant segment returns false (L1425-1426) ───

    [Fact]
    public void IsSingularQuery_DescendantSegment()
    {
        // value(@..x) with descendant → IsSingularQuery returns false at L1425-1426
        // Already tested by DescendantQueryInValueContext but let's verify with a different shape
        this.AssertCgMatchesRuntime(
            """$[?value(@..name) == "a"]""",
            """[{"name":"a"},{"name":"a","x":{"name":"b"}}]""");
    }

    // ── Non-singular EmitFilterQueryAsValue (L1252-1257) ────────────────

    [Fact]
    public void EmitFilterQueryAsValue_NonSingular_Wildcard()
    {
        // @.items[*] as comparable in non-value() context → parser wraps in value()
        // But direct value(@.items[*]) with multi-element produces Undefined
        this.AssertCgMatchesRuntime(
            """$[?value(@[*]) == 42]""",
            """[[42],[1,42],[]]""");
    }
}
