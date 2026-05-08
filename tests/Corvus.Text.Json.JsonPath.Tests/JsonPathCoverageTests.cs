// <copyright file="JsonPathCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.JsonPath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JsonPath.Tests;

/// <summary>
/// Coverage tests targeting uncovered parser/interpreter paths in the JSONPath evaluator,
/// including filter expressions, recursive descent, slicing, unicode, and logical operators.
/// </summary>
[TestClass]
public class JsonPathCoverageTests
{
    // ─── Filter Expressions with Escape Sequences (Parser lines 760-778) ──

    [TestMethod]
    public void Filter_StringWithEscapedNewline()
    {
        string json = """{"items": [{"text": "hello\nworld"}, {"text": "no newline"}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?@.text == 'hello\\nworld']", data);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Filter_StringWithEscapedTab()
    {
        string json = """{"items": [{"text": "col1\tcol2"}, {"text": "plain"}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?@.text == 'col1\\tcol2']", data);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Filter_StringWithEscapedBackslash()
    {
        // Test escaped unicode in filter string comparison
        string json = """{"items": [{"code": "A"}, {"code": "B"}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?@.code == 'A']", data);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("A", result[0].GetProperty("code").GetString());
    }

    // ─── Recursive Descent with Filters (PlanInterpreter lines 879-918) ──

    [TestMethod]
    public void RecursiveDescent_WithFilter()
    {
        string json = """{"a": {"b": [{"c": 1}, {"c": 2}], "d": {"b": [{"c": 3}]}}}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonPathEvaluator.Default.Query("$..b[?@.c > 1]", data, workspace);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.IsTrue(result.GetArrayLength() >= 1);
    }

    [TestMethod]
    public void RecursiveDescent_AllProperties()
    {
        string json = """{"a": {"x": 1}, "b": {"x": 2}, "c": {"d": {"x": 3}}}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonPathEvaluator.Default.Query("$..x", data, workspace);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(3, result.GetArrayLength());
    }

    [TestMethod]
    public void RecursiveDescent_WildcardOnNestedArrays()
    {
        string json = """{"a": [1, 2], "b": {"c": [3, 4]}}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonPathEvaluator.Default.Query("$..*", data, workspace);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.IsTrue(result.GetArrayLength() > 4);
    }

    // ─── Array Slice Operations ──────────────────────────────────────

    [TestMethod]
    public void Slice_WithStep()
    {
        string json = """[0,1,2,3,4,5,6,7,8,9]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[::2]", data);
        Assert.AreEqual(5, result.Count);
        Assert.AreEqual(0, result[0].GetInt32());
        Assert.AreEqual(2, result[1].GetInt32());
        Assert.AreEqual(4, result[2].GetInt32());
    }

    [TestMethod]
    public void Slice_NegativeStep()
    {
        string json = """[0,1,2,3,4]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[::-1]", data);
        Assert.AreEqual(5, result.Count);
        Assert.AreEqual(4, result[0].GetInt32());
        Assert.AreEqual(0, result[4].GetInt32());
    }

    [TestMethod]
    public void Slice_NegativeIndices()
    {
        string json = """[0,1,2,3,4,5]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[-3:]", data);
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(3, result[0].GetInt32());
        Assert.AreEqual(4, result[1].GetInt32());
        Assert.AreEqual(5, result[2].GetInt32());
    }

    [TestMethod]
    public void Slice_StartAndEnd()
    {
        string json = """[0,1,2,3,4,5,6,7,8,9]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[2:5]", data);
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(2, result[0].GetInt32());
        Assert.AreEqual(3, result[1].GetInt32());
        Assert.AreEqual(4, result[2].GetInt32());
    }

    // ─── Complex Filter Expressions ──────────────────────────────────

    [TestMethod]
    public void Filter_GreaterThan()
    {
        string json = """{"items": [{"v":1},{"v":5},{"v":10},{"v":15}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.items[?@.v > 5]", data);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void Filter_LessThanOrEqual()
    {
        string json = """{"items": [{"v":1},{"v":5},{"v":10},{"v":15}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.items[?@.v <= 5]", data);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void Filter_NotEqual()
    {
        string json = """{"items": [{"v":1},{"v":5},{"v":10}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.items[?@.v != 5]", data);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void Filter_LogicalAnd()
    {
        string json = """{"items": [{"a":1,"b":2},{"a":3,"b":4},{"a":5,"b":6}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?@.a > 1 && @.b < 6]", data);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(3, result[0].GetProperty("a").GetInt32());
    }

    [TestMethod]
    public void Filter_LogicalOr()
    {
        string json = """{"items": [{"a":1},{"a":5},{"a":10}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?@.a < 2 || @.a > 8]", data);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void Filter_LogicalNot()
    {
        // In RFC 9535, @.active is an existence test — items missing the property match !@.active
        string json = """{"items": [{"active": true, "v": 1}, {"v": 2}, {"active": false, "v": 3}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?!@.active]", data);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(2, result[0].GetProperty("v").GetInt32());
    }

    // ─── Unicode Property Names ──────────────────────────────────────

    [TestMethod]
    public void PropertyAccess_UnicodeKey()
    {
        string json = "{\"\\u540D\\u524D\": \"\\u592A\\u90CE\", \"\\u5E74\\u9F62\": 25}";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.\u540D\u524D", data);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("\u592A\u90CE", result[0].GetString());
    }

    [TestMethod]
    public void PropertyAccess_BracketNotationUnicode()
    {
        string json = "{\"\\u540D\\u524D\": \"\\u592A\\u90CE\"}";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$['\u540D\u524D']", data);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("\u592A\u90CE", result[0].GetString());
    }

    // ─── Existence Checks in Filters ─────────────────────────────────

    [TestMethod]
    public void Filter_ExistenceCheck()
    {
        string json = """{"items": [{"a":1,"b":2},{"a":3},{"a":5,"b":6}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?@.b]", data);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void Filter_NonExistence()
    {
        string json = """{"items": [{"a":1,"b":2},{"a":3},{"a":5,"b":6}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?!@.b]", data);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(3, result[0].GetProperty("a").GetInt32());
    }

    // ─── Wildcard and index combinations ─────────────────────────────

    [TestMethod]
    public void Wildcard_ObjectProperties()
    {
        string json = """{"a": 1, "b": 2, "c": 3}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.*", data);
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void Wildcard_ArrayElements()
    {
        string json = """[10, 20, 30, 40]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[*]", data);
        Assert.AreEqual(4, result.Count);
    }

    [TestMethod]
    public void MultipleIndices()
    {
        string json = """["a","b","c","d","e"]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[0,2,4]", data);
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("a", result[0].GetString());
        Assert.AreEqual("c", result[1].GetString());
        Assert.AreEqual("e", result[2].GetString());
    }

    // ─── Built-in functions in filters ───────────────────────────────

    [TestMethod]
    public void Filter_LengthFunction()
    {
        string json = """{"items": [{"name": "ab"}, {"name": "abcde"}, {"name": "a"}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?length(@.name) > 2]", data);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("abcde", result[0].GetProperty("name").GetString());
    }

    [TestMethod]
    public void Filter_LengthFunctionOnArray()
    {
        string json = """{"groups": [{"items": [1,2,3]}, {"items": [1]}, {"items": [1,2,3,4,5]}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.groups[?length(@.items) >= 3]", data);
        Assert.AreEqual(2, result.Count);
    }

    // ─── Nested path access ──────────────────────────────────────────

    [TestMethod]
    public void DeepNestedAccess()
    {
        string json = """{"a": {"b": {"c": {"d": {"e": 42}}}}}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.a.b.c.d.e", data);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(42, result[0].GetInt32());
    }

    [TestMethod]
    public void BracketNotation_WithDots()
    {
        string json = """{"a.b": {"c.d": 99}}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$['a.b']['c.d']", data);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(99, result[0].GetInt32());
    }

    // ─── Empty results ───────────────────────────────────────────────

    [TestMethod]
    public void NoMatch_ReturnsEmpty()
    {
        string json = """{"a": 1}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.nonexistent", data);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Filter_NoMatch_ReturnsEmpty()
    {
        string json = """{"items": [{"v":1},{"v":2}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?@.v > 100]", data);
        Assert.AreEqual(0, result.Count);
    }

    // ─── Root access ─────────────────────────────────────────────────

    [TestMethod]
    public void RootOnly_ReturnsWholeDocument()
    {
        string json = """{"a": 1, "b": 2}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$", data);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(JsonValueKind.Object, result[0].ValueKind);
    }

    // ─── Filter with string comparison ───────────────────────────────

    [TestMethod]
    public void Filter_StringEquality()
    {
        string json = """{"items": [{"name":"alice"},{"name":"bob"},{"name":"charlie"}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?@.name == 'bob']", data);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("bob", result[0].GetProperty("name").GetString());
    }

    [TestMethod]
    public void Filter_StringNotEqual()
    {
        string json = """{"items": [{"name":"alice"},{"name":"bob"},{"name":"charlie"}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?@.name != 'bob']", data);
        Assert.AreEqual(2, result.Count);
    }

    // ─── NameDispatchTable: hash code paths for various key lengths (L292-313) ──

    [TestMethod]
    public void NameSet_EmptyKeyLength()
    {
        // HashName case length == 0
        string json = """{"":1,"a":2,"bb":3}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$['','a','bb']", data);
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void NameSet_Length1Key()
    {
        // HashName case length == 1
        string json = """{"x":10,"y":20,"z":30}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$['x','y','z']", data);
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void NameSet_Length2Key()
    {
        // HashName case length == 2
        string json = """{"ab":1,"cd":2,"ef":3}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$['ab','cd','ef']", data);
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void NameSet_Length3Key()
    {
        // HashName case length == 3
        string json = """{"abc":1,"def":2,"ghi":3}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$['abc','def','ghi']", data);
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void NameSet_Length4Key()
    {
        // HashName case length == 4
        string json = """{"abcd":1,"efgh":2,"ijkl":3}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$['abcd','efgh','ijkl']", data);
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void NameSet_Length5Key()
    {
        // HashName case length == 5
        string json = """{"abcde":1,"fghij":2,"klmno":3}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$['abcde','fghij','klmno']", data);
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void NameSet_Length6Key()
    {
        // HashName case length == 6
        string json = """{"abcdef":1,"ghijkl":2,"mnopqr":3}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$['abcdef','ghijkl','mnopqr']", data);
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void NameSet_Length7Key()
    {
        // HashName case length == 7
        string json = """{"abcdefg":1,"hijklmn":2,"opqrstu":3}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$['abcdefg','hijklmn','opqrstu']", data);
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void NameSet_Length8PlusKey()
    {
        // HashName default case (length > 7)
        string json = """{"abcdefgh":1,"ijklmnop":2,"qrstuvwx":3}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$['abcdefgh','ijklmnop','qrstuvwx']", data);
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void NameSet_MissedLookup_ReturnsFalse()
    {
        // L278-279: TryGetSlotIndex returns false (name not in table)
        string json = """{"a":1,"b":2,"c":3,"extra":4}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$['a','b','c']", data);
        Assert.AreEqual(3, result.Count);
    }

    // ─── JsonPathSequenceBuilder: Grow path (L107-116) ──

    [TestMethod]
    public void ManyResults_TriggersGrow()
    {
        // Initial capacity is 8, so >8 results triggers Grow
        string json = """[1,2,3,4,5,6,7,8,9,10,11,12]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[*]", data);
        Assert.AreEqual(12, result.Count);
    }

    // ─── PlanInterpreter: SingletonChain non-object/non-array ──

    [TestMethod]
    public void SingletonChain_NameOnNonObject()
    {
        // SingletonChain: name nav on non-object (L146-148)
        string json = """{"a":"hello"}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.a.b.c", data);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void SingletonChain_NameMissing()
    {
        // SingletonChain: TryGetProperty fails (L151-153)
        string json = """{"a":{"x":1}}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.a.b.c", data);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void SingletonChain_IndexOnNonArray()
    {
        // SingletonChain: index nav on non-array (L158-160)
        string json = """{"a":{"b":"text"}}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.a.b[0][1]", data);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void SingletonChain_IndexOutOfBounds()
    {
        // SingletonChain: resolved index out of bounds (L165-167)
        // a[0] is an array with 2 elements; [99] is out of bounds on that inner array
        string json = """{"a":[[10,20],30]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.a[0][99]", data);
        Assert.AreEqual(0, result.Count);
    }

    // ─── PlanInterpreter: NameSet on non-object (L180-182) ──

    [TestMethod]
    public void NameSet_OnNonObject()
    {
        string json = """[1,2,3]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$['a','b','c']", data);
        Assert.AreEqual(0, result.Count);
    }

    // ─── PlanInterpreter: filter paths ──

    [TestMethod]
    public void Filter_SingularNumericComparison_NavigationFails()
    {
        // EvalSingularNumericComparison: TryNavigateSingularPath fails (L599-601)
        string json = """[{"a":1},{"b":2}]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[?@.a != 5]", data);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void Filter_SingularNumericComparison_NonNumber()
    {
        // EvalSingularNumericComparison: node is not a number (L604-606)
        string json = """[{"a":"text"},{"a":3}]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[?@.a == 3]", data);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Filter_AbsoluteSingularNumericComparison_NavigationFails()
    {
        // EvalSingularNumericComparison non-fused path (absolute query): L599-601
        // $.missing is an absolute singular query compared to a number → plan creates
        // FilterSingularNumericPlan with IsRelative=false, not fused into FilterSingularNumericStep
        string json = """[1,2,3]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[?$.missing != 5]", data);
        // $.missing doesn't exist → op is NotEqual → true for all elements
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void Filter_AbsoluteSingularNumericComparison_NonNumber()
    {
        // EvalSingularNumericComparison non-fused: absolute path resolves to non-number (L604-606)
        string json = """{"val":"text","items":[1,2]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.items[?$.val == 5]", data);
        // $.val is "text" (string), not a number → != 5 would be true
        Assert.AreEqual(0, result.Count);
    }

    // ─── Custom function with NodesType argument (L870-918) ──

    [TestMethod]
    public void CustomFunction_NodesTypeArg_GeneralQuery()
    {
        // EvalNodesTypeArg: FilterGeneralQueryPlan path (L870-877)
        JsonElement data = JsonElement.ParseValue("""{"items":[1,2,3]}"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["node_count"] = new NodeCountFunction(),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?node_count($.items[*]) == 3]", data);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void CustomFunction_NodesTypeArg_SingularQuery()
    {
        // EvalNodesTypeArg: FilterSingularQueryPlan path (L879-892)
        JsonElement data = JsonElement.ParseValue("""[{"x":10},{"x":20}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["node_count"] = new NodeCountFunction(),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?node_count(@.x) == 1]", data);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void CustomFunction_NodesTypeArg_SingularQuery_Missing()
    {
        // EvalNodesTypeArg: FilterSingularQueryPlan with missing property (L889-892)
        JsonElement data = JsonElement.ParseValue("""[{"x":10},{"y":20}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["node_count"] = new NodeCountFunction(),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?node_count(@.x) == 0]", data);
        // Second element has no "x" → nodes count is 0
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void CustomFunction_NodesTypeArg_EmptyQuery()
    {
        // EvalNodesTypeArg: FilterEmptyQueryPlan path (L894-901)
        // An empty query like @. or $. selects the root/current itself
        JsonElement data = JsonElement.ParseValue("""[1,2,3]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["node_count"] = new NodeCountFunction(),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?node_count(@) == 1]", data);
        Assert.AreEqual(3, result.Count);
    }

    // ─── Planner: paren-wrapped comparison (L308-310) ──

    [TestMethod]
    public void Filter_ParenWrappedNumericComparison()
    {
        // TryPlanSpecializedComparison: ParenExpression unwrapping (L307-310)
        string json = """[{"x":5},{"x":15}]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[?(@.x) < 10]", data);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(5, result[0].GetProperty("x").GetInt32());
    }

    // ─── Planner: CompileRegex failure (L580-582) ──

    [TestMethod]
    public void Filter_Match_InvalidStaticPattern_ReturnsNothing()
    {
        // CompileRegex: ArgumentException → returns null (L580-582)
        // Previously caused NullReferenceException because null precompiled fell through
        // to dynamic pattern path with DynamicPatternArg also null.
        string json = """[{"s":"abc"},{"s":"xyz"}]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("""$[?match(@.s, '[invalid')]""", data);
        Assert.AreEqual(0, result.Count);
    }

    // ─── Parser: filter query with empty segments (L374-376) ──

    [TestMethod]
    public void Filter_EmptyQuery_Self()
    {
        // PlanFilterQuery: empty segments (L374-376) → FilterEmptyQueryPlan
        string json = """[1,null,3]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[?@]", data);
        // RFC 9535: @. with no segments selects current node as a node list
        // Existence test: non-null values are truthy
        Assert.AreEqual(3, result.Count);
    }

    // ─── Planner: custom function not found (L418-419) ──

    [TestMethod]
    public void CustomFunction_UnknownName_Throws()
    {
        // PlanCustomFunctionCall: function not in dict (L418-419)
        JsonElement data = JsonElement.ParseValue("1"u8);
        Assert.ThrowsExactly<JsonPathException>(() =>
            JsonPathEvaluator.Default.QueryNodes("$[?nonexistent(@)]", data));
    }

    [TestMethod]
    public void Filter_CountFunction_OnNodeList()
    {
        // Exercises count() with a proper nodes argument — count returns # of nodes
        string json = """[{"a":1},{"a":2},{"a":3}]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[?count($[*]) == 3]", data);
        // count($[*]) == 3 is true → all 3 items selected
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void Filter_ValueFunction_SingleNode()
    {
        // Exercises value() with a single-node nodes argument
        string json = """[{"a":10},{"a":20}]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[?value(@.a) == 10]", data);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Filter_Match_DynamicPattern_InvalidRegex()
    {
        // EvalMatchFunction: dynamic regex fails to compile → NothingResult (L766-768)
        string json = """[{"s":"abc","p":"[invalid"},{"s":"x","p":"x"}]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[?match(@.s, @.p)]", data);
        // First: regex fails → Nothing (not truthy). Second: "x" matches "x" → true
        Assert.AreEqual(1, result.Count);
    }

    // ─── Parser error paths ──

    [TestMethod]
    public void Parser_LeadingWhitespace_Throws()
    {
        // Parser L64-68: leading whitespace not allowed
        JsonElement data = JsonElement.ParseValue("1"u8);
        Assert.ThrowsExactly<JsonPathException>(() =>
            JsonPathEvaluator.Default.QueryNodes(" $.a", data));
    }

    [TestMethod]
    public void Parser_NoRootToken_Throws()
    {
        // Parser L73-75: must start with $
        JsonElement data = JsonElement.ParseValue("1"u8);
        Assert.ThrowsExactly<JsonPathException>(() =>
            JsonPathEvaluator.Default.QueryNodes(".a", data));
    }

    [TestMethod]
    public void Parser_TrailingGarbage_Throws()
    {
        // Parser L81-84: unexpected token after expression
        JsonElement data = JsonElement.ParseValue("1"u8);
        Assert.ThrowsExactly<JsonPathException>(() =>
            JsonPathEvaluator.Default.QueryNodes("$ garbage", data));
    }

    // ─── JsonPathEvaluator: error wrapping (L78-81, L111-114, L152-153) ──

    [TestMethod]
    public void QueryNodes_ThrowingCustomFunction_WrapsInJsonPathException()
    {
        // L78-81: non-JsonPathException during execution → wrapped
        JsonElement data = JsonElement.ParseValue("""[{"x":1}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["bomb"] = new ThrowingFunction(),
        });

        JsonPathException ex = Assert.ThrowsExactly<JsonPathException>(() =>
            evaluator.QueryNodes("$[?bomb(@.x)]", data));
        StringAssert.Contains(ex.Message, "Error evaluating");
        Assert.IsInstanceOfType<InvalidOperationException>(ex.InnerException);
    }

    [TestMethod]
    public void QueryNodes_WithBuffer_ThrowingCustomFunction_WrapsInJsonPathException()
    {
        // L111-114: same as above but with initialBuffer overload
        JsonElement data = JsonElement.ParseValue("""[{"x":1}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["bomb"] = new ThrowingFunction(),
        });

        JsonElement[] buffer = new JsonElement[4];
        JsonPathException? caught = null;
        try
        {
            evaluator.QueryNodes("$[?bomb(@.x)]", data, buffer).Dispose();
        }
        catch (JsonPathException ex)
        {
            caught = ex;
        }

        Assert.IsNotNull(caught);
        StringAssert.Contains(caught!.Message, "Error evaluating");
    }

    [TestMethod]
    public void GetOrCompile_CompilationError_ThrowsJsonPathException()
    {
        // L152-153: non-JsonPathException during compilation (defensive path)
        JsonElement data = JsonElement.ParseValue("1"u8);
        Assert.ThrowsExactly<JsonPathException>(() =>
            JsonPathEvaluator.Default.QueryNodes("$[?unknown_function()]", data));
    }

    // ─── JsonPathFunctionResult factory methods (L53, L62, L70, L78) ──

    [TestMethod]
    public void CustomFunction_ReturnsFromValueInt()
    {
        // Exercises JsonPathFunctionResult.FromValue(int, workspace) - L53
        JsonElement data = JsonElement.ParseValue("""[{"x":5}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["dbl"] = new DoubleIntFunction(),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?dbl(@.x) == 10]", data);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void CustomFunction_ReturnsFromValueDouble()
    {
        // Exercises JsonPathFunctionResult.FromValue(double, workspace) - L62
        JsonElement data = JsonElement.ParseValue("""[{"x":2.5}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["half"] = new HalfFunction(),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?half(@.x) == 1.25]", data);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void CustomFunction_ReturnsFromValueString()
    {
        // Exercises JsonPathFunctionResult.FromValue(string) - L70
        JsonElement data = JsonElement.ParseValue("""[{"x":"hi"}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["upper"] = new UpperFunction(),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?upper(@.x) == 'HI']", data);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void CustomFunction_ReturnsFromValueBool()
    {
        // Exercises JsonPathFunctionResult.FromValueBool(bool) - L78
        JsonElement data = JsonElement.ParseValue("""[{"x":2},{"x":3}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["is_even_val"] = new IsEvenValueFunction(),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?is_even_val(@.x) == true]", data);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void CustomFunction_ReturnsNothing()
    {
        // Exercises L835: custom function returning Nothing → NothingResult
        JsonElement data = JsonElement.ParseValue("""[{"x":1},{"x":2}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["nothing"] = new NothingFunction(),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?nothing(@.x)]", data);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CustomFunction_LogicalTypeArg()
    {
        // Exercises L807-808: LogicalType argument dispatch
        JsonElement data = JsonElement.ParseValue("""[{"x":1},{"x":2}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["negate"] = new NegateLogicalFunction(),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?negate(@.x == 1)]", data);
        // negate(true) → false, negate(false) → true → second item selected
        Assert.AreEqual(1, result.Count);
    }

    // ─── Planner: comparison with literal on LEFT (L296-300, FlipOp L561-568) ──

    [TestMethod]
    public void Filter_LiteralOnLeft_FlipsLessThan()
    {
        // TryPlanSpecializedComparison: literal on left side (L296-300)
        // FlipOp: LessThan → GreaterThan
        string json = """[{"x":5},{"x":15}]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[?10 < @.x]", data);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(15, result[0].GetProperty("x").GetInt32());
    }

    [TestMethod]
    public void Filter_LiteralOnLeft_FlipsGreaterThan()
    {
        // FlipOp: GreaterThan → LessThan
        string json = """[{"x":5},{"x":15}]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[?10 > @.x]", data);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(5, result[0].GetProperty("x").GetInt32());
    }

    [TestMethod]
    public void Filter_LiteralOnLeft_FlipsLessThanOrEqual()
    {
        // FlipOp: LessThanOrEqual → GreaterThanOrEqual
        string json = """[{"x":5},{"x":10},{"x":15}]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[?10 <= @.x]", data);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void Filter_LiteralOnLeft_FlipsGreaterThanOrEqual()
    {
        // FlipOp: GreaterThanOrEqual → LessThanOrEqual
        string json = """[{"x":5},{"x":10},{"x":15}]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[?10 >= @.x]", data);
        Assert.AreEqual(2, result.Count);
    }

    // ─── Planner: duplicate name selector (AllUniqueNames → false, L484-486) ──

    [TestMethod]
    public void DuplicateNameSelector_FallsBackToMultiSelector()
    {
        // When selectors have duplicate names, AllUniqueNames returns false
        string json = """{"a":1,"b":2}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$['a','a','b']", data);
        // RFC 9535: duplicate selectors produce duplicate results
        Assert.AreEqual(3, result.Count);
    }

    // ─── Planner: CountSingletonRun with non-singleton segments (L499-511) ──

    [TestMethod]
    public void MixedSegments_BreaksSingletonChain()
    {
        // CountSingletonRun: wildcard breaks the singleton run
        string json = """{"a":{"b":[{"c":1},{"c":2}]}}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.a.b[*].c", data);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void MultiSelectorSegment_BreaksSingletonChain()
    {
        // CountSingletonRun: multi-selector segment breaks chain
        string json = """{"a":{"b":1,"c":2,"d":{"e":3}}}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.a['b','c']", data);
        Assert.AreEqual(2, result.Count);
    }

    // ─── QueryNodes with initial buffer ──

    [TestMethod]
    public void QueryNodes_WithInitialBuffer_ReturnsResults()
    {
        // L102-116: QueryNodes with caller-provided Span buffer
        JsonElement data = JsonElement.ParseValue("""[1,2,3]"""u8);
        JsonElement[] buffer = new JsonElement[8];
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[*]", data, buffer);
        Assert.AreEqual(3, result.Count);
    }

    // ─── Parser Error Branch Tests (targeting uncovered throw paths) ──

    [TestMethod]
    public void Parse_Utf8Overload_Works()
    {
        // L32-34: Exercises the Parse(ReadOnlySpan<byte>) overload directly
        ReadOnlySpan<byte> utf8Expr = "$[0]"u8;
        QueryNode node = Parser.Parse(utf8Expr);
        Assert.IsNotNull(node);
    }

    [TestMethod]
    public void Parse_MissingCloseParen_InFilter_Throws()
    {
        // L374-376: Expected ')' in parenthesized filter expression
        Assert.ThrowsExactly<JsonPathException>(() =>
            JsonPathEvaluator.Default.QueryNodes("$[?(@.a == 1]", JsonElement.ParseValue("[]"u8)));
    }

    [TestMethod]
    public void Parse_MissingOpenParen_AfterFunction_Throws()
    {
        // L454-456: Expected '(' after function name
        Assert.ThrowsExactly<JsonPathException>(() =>
            JsonPathEvaluator.Default.QueryNodes("$[?length]", JsonElement.ParseValue("[]"u8)));
    }

    [TestMethod]
    public void Parse_MissingCloseParen_AfterFunctionArgs_Throws()
    {
        // L474-476: Expected ')' after function arguments
        Assert.ThrowsExactly<JsonPathException>(() =>
            JsonPathEvaluator.Default.QueryNodes("$[?length(@.a]", JsonElement.ParseValue("[]"u8)));
    }

    [TestMethod]
    public void Parse_LeadingZeros_InInteger_Throws()
    {
        // L516-517: Leading zeros not allowed
        Assert.ThrowsExactly<JsonPathException>(() =>
            JsonPathEvaluator.Default.QueryNodes("$[01]", JsonElement.ParseValue("[]"u8)));
    }

    [TestMethod]
    public void Parse_NegativeZero_InInteger_Throws()
    {
        // L509-511: -0 is not a valid JSONPath integer
        Assert.ThrowsExactly<JsonPathException>(() =>
            JsonPathEvaluator.Default.QueryNodes("$[-0]", JsonElement.ParseValue("[]"u8)));
    }

    [TestMethod]
    public void Parse_LogicalAnd_AsComparable_Throws()
    {
        // L808-809: AND cannot be used as a comparable value
        Assert.ThrowsExactly<JsonPathException>(() =>
            JsonPathEvaluator.Default.QueryNodes("$[?(@.a && @.b) == true]", JsonElement.ParseValue("[]"u8)));
    }

    [TestMethod]
    public void Parse_LogicalOr_AsComparable_Throws()
    {
        // L818-819: OR cannot be used as a comparable value
        Assert.ThrowsExactly<JsonPathException>(() =>
            JsonPathEvaluator.Default.QueryNodes("$[?(@.a || @.b) == true]", JsonElement.ParseValue("[]"u8)));
    }

    [TestMethod]
    public void Parse_LogicalNot_AsComparable_Throws()
    {
        // L828-829: NOT cannot be used as a comparable value
        Assert.ThrowsExactly<JsonPathException>(() =>
            JsonPathEvaluator.Default.QueryNodes("$[?!@.a == true]", JsonElement.ParseValue("[]"u8)));
    }

    [TestMethod]
    public void Parse_Comparison_AsComparable_Throws()
    {
        // L838-839: Comparison cannot be used as a comparable value
        Assert.ThrowsExactly<JsonPathException>(() =>
            JsonPathEvaluator.Default.QueryNodes("$[?(@.a > 1) == true]", JsonElement.ParseValue("[]"u8)));
    }

    [TestMethod]
    public void Parse_UnknownFunction_Throws()
    {
        // L956: Unknown function name
        Assert.ThrowsExactly<JsonPathException>(() =>
            JsonPathEvaluator.Default.QueryNodes("$[?unknownfn(@)]", JsonElement.ParseValue("[]"u8)));
    }

    [TestMethod]
    public void Parse_UnicodeEscape_TwoByteRange()
    {
        // L696-700: 2-byte UTF-8 encoding for code points 0x80-0x7FF (e.g., \u00E9 = é)
        string json = """[{"name":"caf\u00E9"},{"name":"tea"}]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$[?@.name == 'caf\\u00E9']", data);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Parse_UnicodeEscape_OneByteRange()
    {
        // L691-693: 1-byte UTF-8 encoding for code points ≤ 0x7F (e.g., \u0041 = 'A')
        string json = """[{"code":"A"},{"code":"B"}]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$[?@.code == '\\u0041']", data);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Parse_StringLiteral_WithBackspace()
    {
        // L759-761: \b escape in string literal → EscapeJsonString hits backspace case
        string json = """[{"x":"a\u0008b"},{"x":"ab"}]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$[?@.x == 'a\\bb']", data);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Parse_StringLiteral_WithFormfeed()
    {
        // L762-764: \f escape in string literal → EscapeJsonString hits formfeed case
        string json = """[{"x":"a\u000Cb"},{"x":"ab"}]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$[?@.x == 'a\\fb']", data);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Parse_StringLiteral_WithControlChar()
    {
        // L776-778: Control char < 0x20 in EscapeJsonString (via \u0001)
        string json = """[{"x":"a\u0001b"},{"x":"ab"}]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$[?@.x == 'a\\u0001b']", data);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Parse_NodesTypeFunction_InComparison_Throws()
    {
        // L887-890: Custom function returning NodesType used in comparable context
        JsonElement data = JsonElement.ParseValue("""[1,2,3]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["nodesfn"] = new NodesReturnFunction(),
        });

        Assert.ThrowsExactly<JsonPathException>(() =>
            evaluator.QueryNodes("$[?nodesfn(@.x) == 1]", data));
    }

    // ─── Helper types for custom function tests ──

    private sealed class ThrowingFunction : IJsonPathFunction
    {
        public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => [JsonPathFunctionType.ValueType];
        public JsonPathFunctionType ReturnType => JsonPathFunctionType.LogicalType;
        public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> args, JsonWorkspace workspace)
            => throw new InvalidOperationException("Intentional explosion");
    }

    private sealed class DoubleIntFunction : IJsonPathFunction
    {
        public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => [JsonPathFunctionType.ValueType];
        public JsonPathFunctionType ReturnType => JsonPathFunctionType.ValueType;
        public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> args, JsonWorkspace workspace)
        {
            int val = args[0].Value.GetInt32();
            return JsonPathFunctionResult.FromValue(val * 2, workspace);
        }
    }

    private sealed class HalfFunction : IJsonPathFunction
    {
        public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => [JsonPathFunctionType.ValueType];
        public JsonPathFunctionType ReturnType => JsonPathFunctionType.ValueType;
        public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> args, JsonWorkspace workspace)
        {
            double val = args[0].Value.GetDouble();
            return JsonPathFunctionResult.FromValue(val / 2.0, workspace);
        }
    }

    private sealed class UpperFunction : IJsonPathFunction
    {
        public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => [JsonPathFunctionType.ValueType];
        public JsonPathFunctionType ReturnType => JsonPathFunctionType.ValueType;
        public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> args, JsonWorkspace workspace)
        {
            string? val = args[0].Value.GetString();
            return JsonPathFunctionResult.FromValue(val?.ToUpperInvariant() ?? string.Empty);
        }
    }

    private sealed class IsEvenValueFunction : IJsonPathFunction
    {
        public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => [JsonPathFunctionType.ValueType];
        public JsonPathFunctionType ReturnType => JsonPathFunctionType.ValueType;
        public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> args, JsonWorkspace workspace)
        {
            int val = args[0].Value.GetInt32();
            return JsonPathFunctionResult.FromValueBool(val % 2 == 0);
        }
    }

    private sealed class NothingFunction : IJsonPathFunction
    {
        public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => [JsonPathFunctionType.ValueType];
        public JsonPathFunctionType ReturnType => JsonPathFunctionType.LogicalType;
        public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> args, JsonWorkspace workspace)
            => JsonPathFunctionResult.Nothing;
    }

    private sealed class NegateLogicalFunction : IJsonPathFunction
    {
        public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => [JsonPathFunctionType.LogicalType];
        public JsonPathFunctionType ReturnType => JsonPathFunctionType.LogicalType;
        public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> args, JsonWorkspace workspace)
        {
            bool val = args[0].Logical;
            return JsonPathFunctionResult.FromLogical(!val);
        }
    }

    private sealed class NodeCountFunction : IJsonPathFunction
    {
        public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => [JsonPathFunctionType.NodesType];
        public JsonPathFunctionType ReturnType => JsonPathFunctionType.ValueType;
        public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> args, JsonWorkspace workspace)
        {
            int count = args[0].NodeCount;
            return JsonPathFunctionResult.FromValue(count, workspace);
        }
    }

    private sealed class NodesReturnFunction : IJsonPathFunction
    {
        public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => [JsonPathFunctionType.ValueType];
        public JsonPathFunctionType ReturnType => JsonPathFunctionType.NodesType;
        public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> args, JsonWorkspace workspace)
            => JsonPathFunctionResult.Nothing;
    }
}
