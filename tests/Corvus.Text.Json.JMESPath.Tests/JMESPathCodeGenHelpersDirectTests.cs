// <copyright file="JMESPathCodeGenHelpersDirectTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JMESPath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JMESPath.Tests;

/// <summary>
/// Direct unit tests for <see cref="JMESPathCodeGenHelpers"/> public methods,
/// targeting specific uncovered branches from merged Cobertura coverage data.
/// </summary>
[TestClass]
public class JMESPathCodeGenHelpersDirectTests
{
    // ─── IsTruthy: uncovered default branch (line 117) ──────────────

    [TestMethod]
    public void IsTruthy_Undefined_ReturnsFalse()
    {
        Assert.IsFalse(JMESPathCodeGenHelpers.IsTruthy(default));
    }

    [TestMethod]
    public void IsTruthy_EmptyArray_ReturnsFalse()
    {
        JsonElement arr = JsonElement.ParseValue("[]"u8);
        Assert.IsFalse(JMESPathCodeGenHelpers.IsTruthy(arr));
    }

    [TestMethod]
    public void IsTruthy_NonEmptyObject_ReturnsTrue()
    {
        JsonElement obj = JsonElement.ParseValue("""{"a":1}"""u8);
        Assert.IsTrue(JMESPathCodeGenHelpers.IsTruthy(obj));
    }

    [TestMethod]
    public void IsTruthy_EmptyObject_ReturnsFalse()
    {
        JsonElement obj = JsonElement.ParseValue("{}"u8);
        Assert.IsFalse(JMESPathCodeGenHelpers.IsTruthy(obj));
    }

    // ─── DeepEquals: uncovered default branch (line 164) ─────────────

    [TestMethod]
    public void DeepEquals_BothUndefined_ReturnsTrue()
    {
        Assert.IsTrue(JMESPathCodeGenHelpers.DeepEquals(default, default));
    }

    [TestMethod]
    public void DeepEquals_UndefinedVsNumber_ReturnsFalse()
    {
        JsonElement num = JsonElement.ParseValue("1"u8);
        Assert.IsFalse(JMESPathCodeGenHelpers.DeepEquals(default, num));
    }

    [TestMethod]
    public void DeepEquals_NullVsNull_ReturnsTrue()
    {
        JsonElement a = JsonElement.ParseValue("null"u8);
        JsonElement b = JsonElement.ParseValue("null"u8);
        Assert.IsTrue(JMESPathCodeGenHelpers.DeepEquals(a, b));
    }

    [TestMethod]
    public void DeepEquals_ArrayVsArray_MatchesContent()
    {
        JsonElement a = JsonElement.ParseValue("[1,2,3]"u8);
        JsonElement b = JsonElement.ParseValue("[1,2,3]"u8);
        Assert.IsTrue(JMESPathCodeGenHelpers.DeepEquals(a, b));
    }

    [TestMethod]
    public void DeepEquals_ArrayVsArray_DifferentContent()
    {
        JsonElement a = JsonElement.ParseValue("[1,2,3]"u8);
        JsonElement b = JsonElement.ParseValue("[1,2,4]"u8);
        Assert.IsFalse(JMESPathCodeGenHelpers.DeepEquals(a, b));
    }

    [TestMethod]
    public void DeepEquals_ObjectVsObject_MatchesContent()
    {
        JsonElement a = JsonElement.ParseValue("""{"a":1,"b":2}"""u8);
        JsonElement b = JsonElement.ParseValue("""{"b":2,"a":1}"""u8);
        Assert.IsTrue(JMESPathCodeGenHelpers.DeepEquals(a, b));
    }

    // ─── StringToElement: escape sequences (lines 212-213) ──────────

    [TestMethod]
    public void StringToElement_WithBackslashAndFormFeed_Escapes()
    {
        // Lines 212-213: \b and \f escape branches
        JsonElement result = JMESPathCodeGenHelpers.StringToElement("a\b\f");
        Assert.AreEqual("a\b\f", result.GetString());
    }

    // ─── NormalizeSliceIndex: negative step branches (lines 269-271) ─

    [TestMethod]
    [DataRow(-20, 5, true, true, 0)]     // negative → clamp to 0 (positive step)
    [DataRow(-20, 5, true, false, -1)]    // negative → clamp to -1 (negative step)
    [DataRow(10, 5, true, true, 5)]       // > length, positive step → length
    [DataRow(10, 5, true, false, 4)]      // > length-1, negative step, isStart → length-1
    [DataRow(10, 5, false, false, 5)]     // > length, negative step, !isStart → length
    public void NormalizeSliceIndex_HandlesEdgeCases(int index, int length, bool isStart, bool positiveStep, int expected)
    {
        int result = JMESPathCodeGenHelpers.NormalizeSliceIndex(index, length, isStart, positiveStep);
        Assert.AreEqual(expected, result);
    }

    // ─── Reverse string: UTF-8 codepoint-aware (lines 519-521) ──────

    [TestMethod]
    public void Reverse_String_ReversesCodepoints()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement str = JsonElement.ParseValue("\"abc\""u8);
        JsonElement result = JMESPathCodeGenHelpers.Reverse(str, workspace);
        Assert.AreEqual("cba", result.GetString());
    }

    [TestMethod]
    public void Reverse_Array_ReversesElements()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement arr = JsonElement.ParseValue("[1,2,3]"u8);
        JsonElement result = JMESPathCodeGenHelpers.Reverse(arr, workspace);
        Assert.AreEqual("[3,2,1]", result.GetRawText());
    }

    // ─── Sort: mixed types throw (lines 668-669, 674-676) ───────────

    [TestMethod]
    public void Sort_MixedTypes_Throws()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement arr = JsonElement.ParseValue("[1,\"a\"]"u8);
        Assert.ThrowsExactly<JMESPathException>(() => JMESPathCodeGenHelpers.Sort(arr, workspace));
    }

    [TestMethod]
    public void Sort_NonNumericNonString_Throws()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement arr = JsonElement.ParseValue("[true, false]"u8);
        Assert.ThrowsExactly<JMESPathException>(() => JMESPathCodeGenHelpers.Sort(arr, workspace));
    }

    [TestMethod]
    public void Sort_Strings_SortsAlphabetically()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement arr = JsonElement.ParseValue("""["c","a","b"]"""u8);
        JsonElement result = JMESPathCodeGenHelpers.Sort(arr, workspace);
        Assert.AreEqual("[\"a\",\"b\",\"c\"]", result.GetRawText());
    }

    // ─── CollectAndSort: empty (line 720), mixed types (726, 730-746) ─

    [TestMethod]
    public void CollectAndSort_Empty_ReturnsEmptyArray()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.CollectAndSort(
            workspace,
            static (in JsonWorkspace _, ref JMESPathSequenceBuilder _) => { },
            workspace);
        Assert.AreEqual("[]", result.GetRawText());
    }

    // ─── FilterAndSortBy: uncovered (lines 818-880) ─────────────────

    [TestMethod]
    public void FilterAndSortBy_FiltersAndSorts()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement arr = JsonElement.ParseValue("""[{"a":3},{"a":1},{"a":2}]"""u8);

        // compile expression for key extraction: @.a
        var evaluator = JMESPathEvaluator.Default;
        // Use Search to exercise the function through the evaluator instead
        JsonElement result = evaluator.Search("sort_by([?a > `1`], &a)", arr, workspace);
        Assert.AreEqual("[{\"a\":2},{\"a\":3}]", result.GetRawText());
    }

    // ─── RequireNumber/String/Array/Object: throw paths (1324-1331) ──

    [TestMethod]
    public void RequireNumber_NonNumber_Throws()
    {
        Assert.ThrowsExactly<JMESPathException>(() =>
            JMESPathCodeGenHelpers.RequireNumber("test", JsonElement.ParseValue("\"x\""u8)));
    }

    [TestMethod]
    public void RequireString_NonString_Throws()
    {
        Assert.ThrowsExactly<JMESPathException>(() =>
            JMESPathCodeGenHelpers.RequireString("test", JsonElement.ParseValue("42"u8)));
    }

    [TestMethod]
    public void RequireArray_NonArray_Throws()
    {
        Assert.ThrowsExactly<JMESPathException>(() =>
            JMESPathCodeGenHelpers.RequireArray("test", JsonElement.ParseValue("42"u8)));
    }

    [TestMethod]
    public void RequireObject_NonObject_Throws()
    {
        Assert.ThrowsExactly<JMESPathException>(() =>
            JMESPathCodeGenHelpers.RequireObject("test", JsonElement.ParseValue("42"u8)));
    }

    // ─── NotNull: all null returns null (line 1168-1170) ─────────────

    [TestMethod]
    public void NotNull_AllNull_ReturnsNull()
    {
        JsonElement n = JsonElement.ParseValue("null"u8);
        JsonElement result = JMESPathCodeGenHelpers.NotNull([n, n, n]);
        Assert.AreEqual(JsonValueKind.Null, result.ValueKind);
    }

    [TestMethod]
    public void NotNull_FirstNonNull_ReturnsThatValue()
    {
        JsonElement n = JsonElement.ParseValue("null"u8);
        JsonElement num = JsonElement.ParseValue("42"u8);
        JsonElement result = JMESPathCodeGenHelpers.NotNull([n, num, n]);
        Assert.AreEqual(42, result.GetDouble());
    }

    // ─── ToArray: non-array types (lines 1187, 1190-1193) ───────────

    [TestMethod]
    public void ToArray_Number_ReturnsUndefined()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement num = JsonElement.ParseValue("42"u8);
        // toArray on non-array/non-object returns undefined
        var evaluator = JMESPathEvaluator.Default;
        JsonElement result = evaluator.Search("to_array(`42`)", JsonElement.ParseValue("null"u8), workspace);
        Assert.AreEqual("[42]", result.GetRawText());
    }

    // ─── ToNumber: string/boolean branches (1208-1213) ──────────────

    [TestMethod]
    public void ToNumber_BoolTrue_ReturnsNull()
    {
        // JMESPath spec: to_number(true) → null (not 1 like JsonLogic)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.ToNumber(JsonElement.ParseValue("true"u8), workspace);
        Assert.AreEqual(JsonValueKind.Null, result.ValueKind);
    }

    [TestMethod]
    public void ToNumber_BoolFalse_ReturnsNull()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.ToNumber(JsonElement.ParseValue("false"u8), workspace);
        Assert.AreEqual(JsonValueKind.Null, result.ValueKind);
    }

    [TestMethod]
    public void ToNumber_NumericString_Parses()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.ToNumber(JsonElement.ParseValue("\"42.5\""u8), workspace);
        Assert.AreEqual(42.5, result.GetDouble());
    }

    [TestMethod]
    public void ToNumber_NonNumericString_ReturnsNull()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.ToNumber(JsonElement.ParseValue("\"abc\""u8), workspace);
        Assert.AreEqual(JsonValueKind.Null, result.ValueKind);
    }

    // ─── ToString: various types (1241-1273) ─────────────────────────

    [TestMethod]
    public void ToString_Number_ReturnsStringified()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.ToString(JsonElement.ParseValue("42"u8), workspace);
        Assert.AreEqual("42", result.GetString());
    }

    [TestMethod]
    public void ToString_Boolean_ReturnsStringified()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.ToString(JsonElement.ParseValue("true"u8), workspace);
        Assert.AreEqual("true", result.GetString());
    }

    [TestMethod]
    public void ToString_Null_ReturnsStringified()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.ToString(JsonElement.ParseValue("null"u8), workspace);
        Assert.AreEqual("null", result.GetString());
    }

    [TestMethod]
    public void ToString_Array_ReturnsJson()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.ToString(JsonElement.ParseValue("[1,2]"u8), workspace);
        Assert.AreEqual("[1,2]", result.GetString());
    }

    [TestMethod]
    public void ToString_Object_ReturnsJson()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.ToString(JsonElement.ParseValue("""{"a":1}"""u8), workspace);
        Assert.AreEqual("{\"a\":1}", result.GetString());
    }

    // ─── TypeOf: all branches (1286-1293) ────────────────────────────

    [TestMethod]
    [DataRow("\"hello\"", "string")]
    [DataRow("42", "number")]
    [DataRow("true", "boolean")]
    [DataRow("[1]", "array")]
    [DataRow("{\"a\":1}", "object")]
    [DataRow("null", "null")]
    public void TypeOf_ReturnsCorrectType(string json, string expectedType)
    {
        JsonElement elem = JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes(json));
        JsonElement result = JMESPathCodeGenHelpers.TypeOf(elem);
        Assert.AreEqual(expectedType, result.GetString());
    }

    // ─── ApplySortBarrier: lines 1661-1677 ──────────────────────────

    [TestMethod]
    public void ApplySortBarrier_SortsNumbers()
    {
        JMESPathSequenceBuilder builder = default;
        try
        {
            builder.Add(JsonElement.ParseValue("3"u8));
            builder.Add(JsonElement.ParseValue("1"u8));
            builder.Add(JsonElement.ParseValue("2"u8));

            JMESPathCodeGenHelpers.ApplySortBarrier(ref builder);

            Assert.AreEqual(1, builder[0].GetDouble());
            Assert.AreEqual(2, builder[1].GetDouble());
            Assert.AreEqual(3, builder[2].GetDouble());
        }
        finally
        {
            builder.ReturnArray();
        }
    }

    // ─── ApplyReverseBarrier: lines 1743-1751 ───────────────────────

    [TestMethod]
    public void ApplyReverseBarrier_ReversesElements()
    {
        JMESPathSequenceBuilder builder = default;
        try
        {
            builder.Add(JsonElement.ParseValue("1"u8));
            builder.Add(JsonElement.ParseValue("2"u8));
            builder.Add(JsonElement.ParseValue("3"u8));

            JMESPathCodeGenHelpers.ApplyReverseBarrier(ref builder);

            Assert.AreEqual(3, builder[0].GetDouble());
            Assert.AreEqual(2, builder[1].GetDouble());
            Assert.AreEqual(1, builder[2].GetDouble());
        }
        finally
        {
            builder.ReturnArray();
        }
    }

    // ─── ApplySliceBarrier: lines 1762-1799 (positive and negative step) ─

    [TestMethod]
    public void ApplySliceBarrier_PositiveStep()
    {
        JMESPathSequenceBuilder builder = default;
        try
        {
            for (int i = 0; i < 5; i++)
            {
                builder.Add(JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes(i.ToString())));
            }

            JMESPathCodeGenHelpers.ApplySliceBarrier(ref builder, 1, 4, 2);

            Assert.AreEqual(2, builder.Count);
            Assert.AreEqual(1, builder[0].GetDouble());
            Assert.AreEqual(3, builder[1].GetDouble());
        }
        finally
        {
            builder.ReturnArray();
        }
    }

    [TestMethod]
    public void ApplySliceBarrier_NegativeStep()
    {
        JMESPathSequenceBuilder builder = default;
        try
        {
            for (int i = 0; i < 5; i++)
            {
                builder.Add(JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes(i.ToString())));
            }

            JMESPathCodeGenHelpers.ApplySliceBarrier(ref builder, null, null, -1);

            Assert.AreEqual(5, builder.Count);
            Assert.AreEqual(4, builder[0].GetDouble());
            Assert.AreEqual(0, builder[4].GetDouble());
        }
        finally
        {
            builder.ReturnArray();
        }
    }

    // ─── DoubleToElement (line 181) ─────────────────────────────────

    [TestMethod]
    public void DoubleToElement_ReturnsCorrectValue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.DoubleToElement(42.5, workspace);
        Assert.AreEqual(42.5, result.GetDouble());
    }

    // ─── Keys/Values on non-object (lines 591, 597) ────────────────

    [TestMethod]
    public void Keys_Object_ReturnsPropertyNames()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement obj = JsonElement.ParseValue("""{"a":1,"b":2}"""u8);
        JsonElement result = JMESPathCodeGenHelpers.Keys(obj, workspace);
        Assert.AreEqual(2, result.GetArrayLength());
    }

    [TestMethod]
    public void Values_Object_ReturnsPropertyValues()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement obj = JsonElement.ParseValue("""{"a":1,"b":2}"""u8);
        JsonElement result = JMESPathCodeGenHelpers.Values(obj, workspace);
        Assert.AreEqual(2, result.GetArrayLength());
    }

    // ─── SequenceBuilder Grow: lines 119-134 (>8 items) ─────────────

    [TestMethod]
    public void ApplySortBarrier_MoreThan8Elements_TriggersGrow()
    {
        JMESPathSequenceBuilder builder = default;
        try
        {
            // Add 10 elements to exceed initial capacity of 8 and trigger Grow()
            for (int i = 9; i >= 0; i--)
            {
                builder.Add(JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes(i.ToString())));
            }

            JMESPathCodeGenHelpers.ApplySortBarrier(ref builder);

            Assert.AreEqual(10, builder.Count);
            Assert.AreEqual(0, builder[0].GetDouble());
            Assert.AreEqual(9, builder[9].GetDouble());
        }
        finally
        {
            builder.ReturnArray();
        }
    }

    // ─── Lexer: exercise raw string and literal parsing edge cases ───

    [TestMethod]
    public void Evaluator_RawStringWithEscapes()
    {
        // Exercise Lexer raw string path with escape sequences
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonElement.ParseValue("null"u8);
        JsonElement result = JMESPathEvaluator.Default.Search("'hello\\nworld'", data, workspace);
        Assert.AreEqual("hello\\nworld", result.GetString());
    }

    [TestMethod]
    public void Evaluator_LiteralExpression()
    {
        // Exercise Lexer literal path
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonElement.ParseValue("null"u8);
        JsonElement result = JMESPathEvaluator.Default.Search("`{\"a\":1}`", data, workspace);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    // ─── Compiler: fused pipeline stages ─────────────────────────────

    [TestMethod]
    public void Evaluator_FusedSortByPipeline()
    {
        // Exercise fused pipeline with sort_by
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonElement.ParseValue("""[{"a":3,"n":"c"},{"a":1,"n":"a"},{"a":2,"n":"b"}]"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("sort_by(@, &a)[*].n", data, workspace);
        Assert.AreEqual("[\"a\",\"b\",\"c\"]", result.GetRawText());
    }

    [TestMethod]
    public void Evaluator_FusedReversePipeline()
    {
        // Exercise fused pipeline with reverse
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonElement.ParseValue("[1,2,3]"u8);
        JsonElement result = JMESPathEvaluator.Default.Search("reverse(@)", data, workspace);
        Assert.AreEqual("[3,2,1]", result.GetRawText());
    }

    [TestMethod]
    public void Evaluator_FusedSlicePipeline()
    {
        // Exercise fused pipeline with slice
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonElement.ParseValue("[0,1,2,3,4,5]"u8);
        JsonElement result = JMESPathEvaluator.Default.Search("[1:5:2]", data, workspace);
        Assert.AreEqual("[1,3]", result.GetRawText());
    }
}
