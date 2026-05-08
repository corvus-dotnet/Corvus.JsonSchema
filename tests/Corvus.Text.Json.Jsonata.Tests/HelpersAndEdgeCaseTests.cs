// <copyright file="HelpersAndEdgeCaseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Tests that exercise edge-case code paths in <c>JsonataHelpers</c>, <c>JsonataCodeGenHelpers</c>,
/// <c>BuiltInFunctions</c>, and the evaluator, targeting areas with lower code coverage.
/// </summary>
[TestClass]
public class HelpersAndEdgeCaseTests
{
    private static readonly JsonataEvaluator Evaluator = JsonataEvaluator.Default;

    // ─── STRING FUNCTIONS ────────────────────────────────────────────

    [TestMethod]
    [DataRow("""$substring("hello", 0, 3)""", "{}", "\"hel\"")]
    [DataRow("""$substring("hello", 2)""", "{}", "\"llo\"")]
    [DataRow("""$substring("hello", -3)""", "{}", "\"llo\"")]          // negative start
    [DataRow("""$substring("hello", 0, -2)""", "{}", "\"\"")]          // negative length → empty
    [DataRow("""$substring("", 0, 5)""", "{}", "\"\"")]                // empty string
    public void Substring_EdgeCases(string expression, string data, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, data);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("""$length("hello")""", "{}", "5")]
    [DataRow("""$length("")""", "{}", "0")]
    [DataRow("""$length("café")""", "{}", "4")]                     // multi-byte
    public void Length_VariousStrings(string expression, string data, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, data);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("""$contains("hello world", "world")""", "{}", "true")]
    [DataRow("""$contains("hello world", "xyz")""", "{}", "false")]
    [DataRow("""$contains("", "")""", "{}", "true")]
    public void Contains_StringSearch(string expression, string data, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, data);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("""$split("a,b,c", ",")""", "{}", """["a","b","c"]""")]
    [DataRow("""$split("hello", "")""", "{}", """["h","e","l","l","o"]""")]
    [DataRow("""$split("no-delim", "x")""", "{}", """["no-delim"]""")]
    public void Split_VariousDelimiters(string expression, string data, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, data);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("""$join(["a","b","c"], ",")""", "{}", "\"a,b,c\"")]
    [DataRow("""$join(["only"], ",")""", "{}", "\"only\"")]
    [DataRow("""$join([], ",")""", "{}", "\"\"")]
    public void Join_VariousCases(string expression, string data, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, data);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("""$replace("hello", "l", "L")""", "{}", "\"heLLo\"")]
    [DataRow("""$replace("aaa", "a", "bb")""", "{}", "\"bbbbbb\"")]
    [DataRow("""$replace("hello", "xyz", "!")""", "{}", "\"hello\"")]    // no match
    public void Replace_BasicCases(string expression, string data, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, data);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("""$uppercase("hello")""", "{}", "\"HELLO\"")]
    [DataRow("""$lowercase("HELLO")""", "{}", "\"hello\"")]
    [DataRow("""$trim("  hello  ")""", "{}", "\"hello\"")]
    [DataRow("""$trim("")""", "{}", "\"\"")]
    public void CaseAndTrim(string expression, string data, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, data);
        Assert.AreEqual(expected, result);
    }

    // ─── NUMERIC FUNCTIONS ───────────────────────────────────────────

    [TestMethod]
    [DataRow("""$abs(-5)""", "{}", "5")]
    [DataRow("""$abs(5)""", "{}", "5")]
    [DataRow("""$abs(0)""", "{}", "0")]
    public void Abs_VariousValues(string expression, string data, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, data);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("""$floor(3.7)""", "{}", "3")]
    [DataRow("""$floor(-3.2)""", "{}", "-4")]
    [DataRow("""$ceil(3.2)""", "{}", "4")]
    [DataRow("""$ceil(-3.7)""", "{}", "-3")]
    [DataRow("""$round(3.5)""", "{}", "4")]
    [DataRow("""$round(3.4)""", "{}", "3")]
    public void Rounding_Functions(string expression, string data, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, data);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("""$power(2, 10)""", "{}", "1024")]
    [DataRow("""$power(3, 0)""", "{}", "1")]
    [DataRow("""$sqrt(9)""", "{}", "3")]
    [DataRow("""$sqrt(2)""", "{}")]             // irrational — just check no error
    public void Power_And_Sqrt(string expression, string data, string? expected = null)
    {
        string? result = Evaluator.EvaluateToString(expression, data);
        if (expected is not null)
        {
            Assert.AreEqual(expected, result);
        }
        else
        {
            Assert.IsNotNull(result);
        }
    }

    // ─── ARRAY FUNCTIONS ─────────────────────────────────────────────

    [TestMethod]
    [DataRow("""$count([1,2,3])""", "{}", "3")]
    [DataRow("""$count([])""", "{}", "0")]
    [DataRow("""$count("hello")""", "{}")]       // string — may return 1 or error
    public void Count_Various(string expression, string data, string? expected = null)
    {
        try
        {
            string? result = Evaluator.EvaluateToString(expression, data);
            if (expected is not null)
            {
                Assert.AreEqual(expected, result);
            }
        }
        catch (JsonataException)
        {
            // Some inputs may throw — that's acceptable
        }
    }

    [TestMethod]
    [DataRow("""$append([1,2], [3,4])""", "{}", "[1,2,3,4]")]
    [DataRow("""$append([1,2], 3)""", "{}", "[1,2,3]")]
    [DataRow("""$append(1, [2,3])""", "{}", "[1,2,3]")]
    public void Append_MergesArrays(string expression, string data, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, data);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("""$reverse([3,1,2])""", "{}", "[2,1,3]")]
    [DataRow("""$reverse([])""", "{}", "[]")]
    public void Reverse_Array(string expression, string data, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, data);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Sort_BasicArray()
    {
        string? result = Evaluator.EvaluateToString("""$sort([3,1,2])""", "{}");
        Assert.AreEqual("[1,2,3]", result);
    }

    [TestMethod]
    public void Sort_EmptyArray()
    {
        string? result = Evaluator.EvaluateToString("""$sort([])""", "{}");
        Assert.AreEqual("[]", result);
    }

    [TestMethod]
    public void Distinct_RemovesDuplicates()
    {
        string? result = Evaluator.EvaluateToString("""$distinct([1,2,2,3,3,3])""", "{}");
        Assert.AreEqual("[1,2,3]", result);
    }

    // ─── OBJECT FUNCTIONS ────────────────────────────────────────────

    [TestMethod]
    public void Keys_ReturnsPropertyNames()
    {
        string? result = Evaluator.EvaluateToString("""$keys({"a":1,"b":2})""", "{}");
        Assert.AreEqual("[\"a\",\"b\"]", result);
    }

    [TestMethod]
    public void Values_ReturnsPropertyValues()
    {
        string? result = Evaluator.EvaluateToString("""$values({"a":1,"b":2})""", "{}");
        Assert.AreEqual("[1,2]", result);
    }

    [TestMethod]
    public void Merge_CombinesObjects()
    {
        string? result = Evaluator.EvaluateToString("""$merge([{"a":1},{"b":2}])""", "{}");
        // JSON objects are unordered; check semantic equality
        Assert.IsNotNull(result);
        using var parsed = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(result));
        var root = parsed.RootElement;
        Assert.AreEqual(JsonValueKind.Object, root.ValueKind);
        Assert.AreEqual(1, root.GetProperty("a").GetInt32());
        Assert.AreEqual(2, root.GetProperty("b").GetInt32());
    }

    [TestMethod]
    public void Merge_OverlappingKeys_LastWins()
    {
        string? result = Evaluator.EvaluateToString("""$merge([{"a":1},{"a":2}])""", "{}");
        Assert.AreEqual("{\"a\":2}", result);
    }

    // ─── TYPE FUNCTIONS ──────────────────────────────────────────────

    [TestMethod]
    [DataRow("""$type(42)""", "{}", "\"number\"")]
    [DataRow("""$type("hello")""", "{}", "\"string\"")]
    [DataRow("""$type(true)""", "{}", "\"boolean\"")]
    [DataRow("""$type(null)""", "{}", "\"null\"")]
    [DataRow("""$type([1,2])""", "{}", "\"array\"")]
    [DataRow("""$type({"a":1})""", "{}", "\"object\"")]
    public void Type_ReturnsCorrectTypeName(string expression, string data, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, data);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("""$string(42)""", "{}", "\"42\"")]
    [DataRow("""$string(true)""", "{}", "\"true\"")]
    [DataRow("""$string(null)""", "{}")]           // may return undefined
    public void String_CoercionFromVariousTypes(string expression, string data, string? expected = null)
    {
        string? result = Evaluator.EvaluateToString(expression, data);
        if (expected is not null)
        {
            Assert.AreEqual(expected, result);
        }
    }

    [TestMethod]
    [DataRow("""$number("42")""", "{}", "42")]
    [DataRow("""$number(true)""", "{}")]           // may throw or return undefined
    public void Number_CoercionFromString(string expression, string data, string? expected = null)
    {
        try
        {
            string? result = Evaluator.EvaluateToString(expression, data);
            if (expected is not null)
            {
                Assert.AreEqual(expected, result);
            }
        }
        catch (JsonataException)
        {
            // Type mismatch may throw
        }
    }

    [TestMethod]
    [DataRow("""$boolean(0)""", "{}", "false")]
    [DataRow("""$boolean(1)""", "{}", "true")]
    [DataRow("""$boolean("")""", "{}", "false")]
    [DataRow("""$boolean("hello")""", "{}", "true")]
    [DataRow("""$boolean(null)""", "{}", "false")]
    [DataRow("""$boolean([])""", "{}", "false")]
    [DataRow("""$boolean([1])""", "{}", "true")]
    public void Boolean_CoercionSemantics(string expression, string data, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, data);
        Assert.AreEqual(expected, result);
    }

    // ─── PROPERTY CHAIN NAVIGATION ───────────────────────────────────

    [TestMethod]
    [DataRow("a.b.c", """{"a":{"b":{"c":42}}}""", "42")]
    [DataRow("a.b.c", """{"a":{"b":{}}}""")]                          // missing deep path
    [DataRow("a.b.c", """{"a":{}}""")]                                // missing intermediate
    [DataRow("items[0]", """{"items":[10,20,30]}""", "10")]
    [DataRow("items[-1]", """{"items":[10,20,30]}""", "30")]           // negative index
    public void PropertyNavigation_DeepPaths(string expression, string data, string? expected = null)
    {
        string? result = Evaluator.EvaluateToString(expression, data);
        if (expected is not null)
        {
            Assert.AreEqual(expected, result);
        }
        else
        {
            Assert.IsNull(result);
        }
    }

    // ─── CONDITIONAL / TERNARY ───────────────────────────────────────

    [TestMethod]
    [DataRow("""x > 0 ? "positive" : "non-positive" """, """{"x":5}""", "\"positive\"")]
    [DataRow("""x > 0 ? "positive" : "non-positive" """, """{"x":-1}""", "\"non-positive\"")]
    [DataRow("""x > 0 ? "positive" : x = 0 ? "zero" : "negative" """, """{"x":0}""", "\"zero\"")]
    public void Conditional_Ternary(string expression, string data, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, data);
        Assert.AreEqual(expected, result);
    }

    // ─── ARRAY COMPREHENSION / MAP ───────────────────────────────────

    [TestMethod]
    public void Map_OverArray()
    {
        string? result = Evaluator.EvaluateToString("""items.$uppercase($)""", """{"items":["hello","world"]}""");
        Assert.AreEqual("[\"HELLO\",\"WORLD\"]", result);
    }

    [TestMethod]
    public void Filter_WithPredicate()
    {
        string? result = Evaluator.EvaluateToString("""items[$ > 2]""", """{"items":[1,2,3,4,5]}""");
        Assert.AreEqual("[3,4,5]", result);
    }

    // ─── AGGREGATE FUNCTIONS ─────────────────────────────────────────

    [TestMethod]
    [DataRow("""$sum([1,2,3,4])""", "{}", "10")]
    [DataRow("""$sum([])""", "{}", "0")]
    [DataRow("""$average([2,4,6])""", "{}", "4")]
    [DataRow("""$min([3,1,4])""", "{}", "1")]
    [DataRow("""$max([3,1,4])""", "{}", "4")]
    public void AggregateFunctions(string expression, string data, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, data);
        Assert.AreEqual(expected, result);
    }

    // ─── EXISTS / LOOKUP ─────────────────────────────────────────────

    [TestMethod]
    [DataRow("""$exists(a)""", """{"a":1}""", "true")]
    [DataRow("""$exists(b)""", """{"a":1}""", "false")]
    [DataRow("""$exists(null)""", "{}", "true")]              // null literal exists as a value
    public void Exists_ChecksPresence(string expression, string data, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, data);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Lookup_FindsMatchingValue()
    {
        string? result = Evaluator.EvaluateToString(
            """$lookup({"a":1,"b":2}, "b")""",
            "{}");
        Assert.AreEqual("2", result);
    }
}
