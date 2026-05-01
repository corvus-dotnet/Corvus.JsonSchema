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
}
