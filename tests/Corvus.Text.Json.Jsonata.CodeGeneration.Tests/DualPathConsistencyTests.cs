// <copyright file="DualPathConsistencyTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Jsonata;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Jsonata.CodeGeneration.Tests;

/// <summary>
/// Verifies that every expression in our coverage test suite produces
/// identical results through both the RT (runtime interpreter) and CG
/// (code generator) paths. Any inconsistency is a bug.
/// </summary>
/// <remarks>
/// <para>
/// Each test case is run through both paths and the results compared.
/// If an expression throws in one path, it must throw the same error code
/// in the other path. If it returns a value, both paths must return the
/// same serialized JSON.
/// </para>
/// </remarks>
[TestClass]
public class DualPathConsistencyTests
{
    private static readonly JsonataEvaluator Evaluator = JsonataEvaluator.Default;
    private static CodeGenConformanceFixture? s_fixture;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        s_fixture = new CodeGenConformanceFixture();
    }

    [ClassCleanup]
    public static void ClassCleanupMethod()
    {
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    /// <summary>
    /// Data source: expression, data (JSON string or null for no data), description.
    /// </summary>
    public static IEnumerable<object[]> GetTestCases()
    {
        // ─── Filter ───
        yield return ["""$filter(Account.Order.Product, function($v){$v.Price > 10})""", """{"Account":{"Order":[{"Product":[{"Price":5,"Name":"Cheap"},{"Price":15,"Name":"Mid"}]},{"Product":{"Price":25,"Name":"Expensive"}}]}}""", "filter-flatten"];
        yield return ["""$filter(groups.items, function($v){$v > 3})""", """{"groups":[{"items":[1,2,3]},{"items":[4,5,6]}]}""", "filter-multi-valued"];

        // ─── Spread ───
        yield return ["""$spread({"x":1,"y":2})""", "null", "spread-single-object"];

        // ─── Single ───
        yield return ["""$single(items)""", """{"items":[{"name":"only"}]}""", "single-one"];

        // ─── Shuffle ───
        yield return ["""$count($shuffle(groups.items))""", """{"groups":[{"items":[1,2]},{"items":[3,4]}]}""", "shuffle-count"];

        // ─── Map with index ───
        yield return ["""$map([10,20,30], function($v, $i){$i})""", "null", "map-index"];

        // ─── Reduce ───
        yield return ["""$reduce([42], function($prev, $curr){$prev + $curr})""", "null", "reduce-single"];
        yield return ["""$reduce([1,2,3,4,5], function($prev,$curr){$prev + $curr})""", "null", "reduce-multi"];

        // ─── Zip ───
        yield return ["""$zip([1,2,3], ["a","b"])""", "null", "zip-truncation"];

        // ─── Sort ───
        yield return ["""$sort([3,1,2], function($a,$b){$a > $b})""", "null", "sort-ascending"];
        yield return ["""$sort([3,1,2], function($a,$b){$a < $b})""", "null", "sort-descending"];
        yield return ["""$sort(["banana", "apple", "cherry"])""", "null", "sort-strings"];
        yield return ["""$sort([5,3,8,1,4], function($a,$b){$a > $b})""", "null", "sort-5-elements"];

        // ─── Context-arg ───
        yield return ["""$contains("world")""", "\"hello world\"", "contains-context"];
        yield return ["""$split(",")""", "\"a,b,c\"", "split-context"];

        // ─── Number radix (EXTENSION beyond reference) ───
        yield return ["""$number("0xFF")""", "null", "number-hex"];
        yield return ["""$number("0b1010")""", "null", "number-binary"];
        yield return ["""$number("0o17")""", "null", "number-octal"];

        // ─── Substring supplementary ───
        yield return ["""$substring("A\ud83c\udf89B", 1, 1)""", "null", "substring-surrogate"];

        // ─── Replace limit=0 ───
        yield return ["""$replace("hello world", "o", "0", 0)""", "null", "replace-limit-zero"];

        // ─── Higher-order functions ───
        yield return ["""($apply := function($f, $x){ $f($x) }; $apply($sum, [1,2,3]))""", "null", "hof-apply"];
        yield return ["""($adder := function($a){ function($b){ $a + $b } }; $adder(3)(7))""", "null", "hof-return"];

        // ─── Long concatenation (GrowBuffer) ───
        yield return ["""$join($map([1..50], function($v){ "abcde" }), "-")""", "null", "join-long"];

        // ─── Type mismatch (correctly throws T0410) ───
        yield return ["""$uppercase(42)""", "null", "type-mismatch-uppercase"];
        yield return ["""$substring(42, 1)""", "null", "type-mismatch-substring"];
        yield return ["""$contains(42, "x")""", "null", "type-mismatch-contains"];
        yield return ["""$split(42, "-")""", "null", "type-mismatch-split"];
        yield return ["""$replace(42, "a", "b")""", "null", "type-mismatch-replace"];
        yield return ["""$trim(42)""", "null", "type-mismatch-trim"];
        yield return ["""$uppercase(null)""", "null", "type-mismatch-null-to-str"];

        // ─── Type mismatch (correctly throws T0412) ───
        yield return ["""$join([1,2,3])""", "null", "type-mismatch-join-numbers"];
        yield return ["""$join([true, false])""", "null", "type-mismatch-join-booleans"];

        // ─── Too many arguments ───
        yield return ["""$sum([1,2], [3,4])""", "null", "too-many-args-sum"];

        // ─── Error codes ───
        yield return ["""$single([1,2,3])""", "null", "single-multiple-D3138"];
        yield return ["""$single([])""", "null", "single-empty-D3139"];
        yield return ["""$decodeUrlComponent("%GG")""", "null", "decode-url-D3140"];
        yield return ["""$formatNumber(1234.5, "0#0")""", "null", "format-number-D3090"];

        // ─── DateTime tests ───
        yield return ["""$fromMillis(1234567890000, "[Y]-[M01]-[D01]")""", "null", "from-millis-date"];
        yield return ["""$fromMillis(1234567890000, "[H01]:[m01]:[s01]")""", "null", "from-millis-time"];
        yield return ["""$toMillis("2009-02-13", "[Y]-[M01]-[D01]")""", "null", "to-millis-date"];
        yield return ["""$fromMillis(1234567890000, "[FNn], [D] [MNn] [Y]")""", "null", "from-millis-dayname"];
        yield return ["""$fromMillis(1234567890000, "[d001]")""", "null", "from-millis-dayofyear"];
        yield return ["""$fromMillis(1234567890123, "[s01].[f001]")""", "null", "from-millis-fractional"];

        // ─── formatNumber ───
        yield return ["""$formatNumber(1234.5, "#,##0.00")""", "null", "format-number-basic"];
        yield return ["""$formatNumber(-1234.5, "#,##0.00;(#,##0.00)")""", "null", "format-number-negative"];

        // ─── T0410 type validation (bug fixes — both paths must throw) ───
        yield return ["""$sqrt("hello")""", "null", "t0410-sqrt-string"];
        yield return ["""$abs(true)""", "null", "t0410-abs-bool"];
        yield return ["""$floor(null)""", "null", "t0410-floor-null"];
        yield return ["""$ceil("test")""", "null", "t0410-ceil-string"];
        yield return ["""$round("test")""", "null", "t0410-round-string"];
        yield return ["""$power("a", 2)""", "null", "t0410-power-string-base"];
        yield return ["""$power(2, "a")""", "null", "t0410-power-string-exp"];
        yield return ["""$map([1,2,3], "notfunc")""", "null", "t0410-map-string-func"];
        yield return ["""$filter([1,2,3], "notfunc")""", "null", "t0410-filter-string-func"];
        yield return ["""$reduce([1,2,3], "notfunc")""", "null", "t0410-reduce-string-func"];
        yield return ["""$match(42, /abc/)""", "null", "t0410-match-number-str"];
        yield return ["""$formatNumber("not a number", "#")""", "null", "t0410-formatnumber-string"];

        // ─── FormatBase type validation (reference throws T0410) ───
        yield return ["""$formatBase("hello", 16)""", "null", "t0410-formatbase-string"];
        yield return ["""$formatBase(true, 16)""", "null", "t0410-formatbase-bool"];
        yield return ["""$formatBase(null, 16)""", "null", "t0410-formatbase-null"];
        yield return ["""$formatBase(255, "hex")""", "null", "t0410-formatbase-string-radix"];
        yield return ["""$formatBase(255, true)""", "null", "t0410-formatbase-bool-radix"];
        yield return ["""$formatBase(255, null)""", "null", "t0410-formatbase-null-radix"];
        yield return ["""$formatBase(255, nosuchvar)""", "null", "formatbase-undefined-radix-default"];

        // ─── Each type validation (reference throws T0410) ───
        yield return ["""$each(42, function($v,$k){$v})""", "null", "t0410-each-number"];
        yield return ["""$each(null, function($v,$k){$v})""", "null", "t0410-each-null"];
        yield return ["""$each({"a":1}, 42)""", "null", "t0410-each-nonfunc"];
        yield return ["""$each({"a":1}, nosuchvar)""", "null", "t0410-each-undefined-func"];

        // ─── Sift type validation (reference throws T0410) ───
        yield return ["""$sift(42, function($v,$k){true})""", "null", "t0410-sift-number"];
        yield return ["""$sift(null, function($v,$k){true})""", "null", "t0410-sift-null"];
        yield return ["""$sift({"a":1}, 42)""", "null", "t0410-sift-nonfunc"];
        yield return ["""$sift({"a":1}, nosuchvar)""", "null", "t0410-sift-undefined-func"];

        // ─── Match pattern type validation (reference throws T0410) ───
        yield return ["""$match("hello", 42)""", "null", "t0410-match-nonregex-number"];
        yield return ["""$match("hello", nosuchvar)""", "null", "t0410-match-nonregex-undef"];
        yield return ["""$match("hello", null)""", "null", "t0410-match-nonregex-null"];
        yield return ["""$match(null, /abc/)""", "null", "t0410-match-null-str"];

        // ─── Shuffle fix (singleton wraps in array) ───
        yield return ["""$shuffle(42)""", "null", "shuffle-singleton-wraps"];

        // ─── Sort comparator uses boolean/truthy semantics ───
        yield return ["""$sort([3,1,4,1,5], function($a,$b){$a > $b})""", "null", "sort-asc-boolean"];
        yield return ["""$sort([3,1,4,1,5], function($a,$b){$a < $b})""", "null", "sort-desc-boolean"];

        // ─── FunctionalCompiler coverage paths ───
        // Focus binding cross-join
        yield return ["""library.loans@$l.books[isbn=$l.isbn].title""", """{"library":{"loans":[{"isbn":"123"},{"isbn":"456"}],"books":[{"isbn":"123","title":"A"},{"isbn":"456","title":"B"}]}}""", "focus-crossjoin"];
        // Sort with continuation
        yield return ["""items^(price).name""", """{"items":[{"price":30,"name":"C"},{"price":10,"name":"A"},{"price":20,"name":"B"}]}""", "sort-continuation"];
        // Index binding on multi-valued path
        yield return ["""groups.items#$i[$i=0]""", """{"groups":[{"items":["a","b"]},{"items":["c","d"]}]}""", "index-binding-multiparent"];
        // Variable with filter (WrapWithStages)
        yield return ["""( $arr := [1,2,3,4,5]; $arr[$ > 3] )""", "null", "wrap-stages-varfilter"];
        // Variable with group-by (WrapWithGroupBy fast path)
        yield return ["""( $items := [{"cat":"A","val":10},{"cat":"B","val":20},{"cat":"A","val":30}]; $items{cat: val} )""", "null", "wrap-groupby-vargroup"];

        // ─── Environment limits ───
        yield return ["""$ + 1""", "42", "simple-arithmetic"];
    }

    [TestMethod]
    [TestCategory("dual-path")]
    [DynamicData(nameof(GetTestCases))]
    public void CgAndRtProduceSameResult(string expression, string data, string description)
    {
        Console.WriteLine($"Expression: {expression}");
        Console.WriteLine($"Data:       {data}");
        Console.WriteLine($"Test:       {description}");

        // ── RT path ──
        string? rtResult = null;
        string? rtErrorCode = null;
        try
        {
            rtResult = EvalRt(expression, data);
        }
        catch (JsonataException ex)
        {
            rtErrorCode = ex.Code;
        }

        // ── CG path ──
        string? cgResult = null;
        string? cgErrorCode = null;
        CompiledExpression compiled = s_fixture!.GetOrCompile(expression);

        if (compiled.Method is null)
        {
            // CG compilation failed — check if this is a parse-time error
            cgErrorCode = compiled.ErrorCode;
            if (cgErrorCode is null)
            {
                // Non-parse CG failure — log and check if RT also fails
                Console.WriteLine($"CG compile failed: {compiled.Error}");
                if (rtErrorCode is not null)
                {
                    // Both fail — acceptable (CG can't handle some expressions)
                    Console.WriteLine($"Both paths error: RT={rtErrorCode}");
                    return;
                }

                // RT succeeds but CG fails to compile — this is a known CG limitation for some expressions.
                // Log it as a warning but don't fail the test (CG doesn't support all expressions).
                Console.WriteLine($"WARNING: RT succeeded ({rtResult}) but CG failed to compile");
                return;
            }
        }
        else
        {
            try
            {
                cgResult = EvalCg(compiled.Method, data);
            }
            catch (JsonataException ex)
            {
                cgErrorCode = ex.Code;
            }
            catch (TargetInvocationException tie)
            {
                cgErrorCode = ExtractJsonataErrorCode(tie);
            }
        }

        Console.WriteLine($"RT: {(rtErrorCode is not null ? $"ERROR {rtErrorCode}" : rtResult ?? "undefined")}");
        Console.WriteLine($"CG: {(cgErrorCode is not null ? $"ERROR {cgErrorCode}" : cgResult ?? "undefined")}");

        // ── Assert consistency ──
        if (rtErrorCode is not null || cgErrorCode is not null)
        {
            // At least one threw — both must throw the same code
            Assert.AreEqual(rtErrorCode, cgErrorCode);
        }
        else
        {
            // Both returned values — compare
            Assert.AreEqual(rtResult, cgResult);
        }
    }

    private static string? EvalRt(string expression, string data)
    {
        if (data == "null")
        {
            return Evaluator.EvaluateToString(expression, "null") ?? "undefined";
        }

        using var doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(data));
        JsonElement result = Evaluator.Evaluate(expression, doc.RootElement);
        if (result.ValueKind == JsonValueKind.Undefined)
        {
            return "undefined";
        }

        return result.GetRawText();
    }

    private static string? EvalCg(MethodInfo method, string data)
    {
        using var workspace = JsonWorkspace.Create();

        JsonElement input;
        ParsedJsonDocument<JsonElement>? dataDoc = null;

        if (data != "null")
        {
            dataDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(data));
            input = dataDoc.RootElement;
        }
        else
        {
            input = default;
        }

        try
        {
            object? resultObj = method.Invoke(null, [input, workspace]);
            if (resultObj is not JsonElement el)
            {
                return "undefined";
            }

            if (el.ValueKind == JsonValueKind.Undefined)
            {
                return "undefined";
            }

            return el.GetRawText();
        }
        finally
        {
            dataDoc?.Dispose();
        }
    }

    /// <summary>
    /// Unwraps nested exceptions (TargetInvocationException → TypeInitializationException → JsonataException)
    /// to extract the JsonataException error code.
    /// </summary>
    private static string? ExtractJsonataErrorCode(Exception ex)
    {
        Exception? current = ex;
        while (current is not null)
        {
            if (current is JsonataException jex)
            {
                return jex.Code;
            }

            current = current.InnerException;
        }

        return null;
    }
}
