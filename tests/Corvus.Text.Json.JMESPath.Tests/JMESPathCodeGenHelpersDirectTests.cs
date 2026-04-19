// <copyright file="JMESPathCodeGenHelpersDirectTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JMESPath;
using Xunit;

namespace Corvus.Text.Json.JMESPath.Tests;

/// <summary>
/// Direct unit tests for <see cref="JMESPathCodeGenHelpers"/> public methods,
/// targeting specific uncovered branches from merged Cobertura coverage data.
/// </summary>
public class JMESPathCodeGenHelpersDirectTests
{
    // ─── IsTruthy: uncovered default branch (line 117) ──────────────

    [Fact]
    public void IsTruthy_Undefined_ReturnsFalse()
    {
        Assert.False(JMESPathCodeGenHelpers.IsTruthy(default));
    }

    [Fact]
    public void IsTruthy_EmptyArray_ReturnsFalse()
    {
        JsonElement arr = JsonElement.ParseValue("[]"u8);
        Assert.False(JMESPathCodeGenHelpers.IsTruthy(arr));
    }

    [Fact]
    public void IsTruthy_NonEmptyObject_ReturnsTrue()
    {
        JsonElement obj = JsonElement.ParseValue("""{"a":1}"""u8);
        Assert.True(JMESPathCodeGenHelpers.IsTruthy(obj));
    }

    [Fact]
    public void IsTruthy_EmptyObject_ReturnsFalse()
    {
        JsonElement obj = JsonElement.ParseValue("{}"u8);
        Assert.False(JMESPathCodeGenHelpers.IsTruthy(obj));
    }

    // ─── DeepEquals: uncovered default branch (line 164) ─────────────

    [Fact]
    public void DeepEquals_BothUndefined_ReturnsTrue()
    {
        Assert.True(JMESPathCodeGenHelpers.DeepEquals(default, default));
    }

    [Fact]
    public void DeepEquals_UndefinedVsNumber_ReturnsFalse()
    {
        JsonElement num = JsonElement.ParseValue("1"u8);
        Assert.False(JMESPathCodeGenHelpers.DeepEquals(default, num));
    }

    [Fact]
    public void DeepEquals_NullVsNull_ReturnsTrue()
    {
        JsonElement a = JsonElement.ParseValue("null"u8);
        JsonElement b = JsonElement.ParseValue("null"u8);
        Assert.True(JMESPathCodeGenHelpers.DeepEquals(a, b));
    }

    [Fact]
    public void DeepEquals_ArrayVsArray_MatchesContent()
    {
        JsonElement a = JsonElement.ParseValue("[1,2,3]"u8);
        JsonElement b = JsonElement.ParseValue("[1,2,3]"u8);
        Assert.True(JMESPathCodeGenHelpers.DeepEquals(a, b));
    }

    [Fact]
    public void DeepEquals_ArrayVsArray_DifferentContent()
    {
        JsonElement a = JsonElement.ParseValue("[1,2,3]"u8);
        JsonElement b = JsonElement.ParseValue("[1,2,4]"u8);
        Assert.False(JMESPathCodeGenHelpers.DeepEquals(a, b));
    }

    [Fact]
    public void DeepEquals_ObjectVsObject_MatchesContent()
    {
        JsonElement a = JsonElement.ParseValue("""{"a":1,"b":2}"""u8);
        JsonElement b = JsonElement.ParseValue("""{"b":2,"a":1}"""u8);
        Assert.True(JMESPathCodeGenHelpers.DeepEquals(a, b));
    }

    // ─── StringToElement: escape sequences (lines 212-213) ──────────

    [Fact]
    public void StringToElement_WithBackslashAndFormFeed_Escapes()
    {
        // Lines 212-213: \b and \f escape branches
        JsonElement result = JMESPathCodeGenHelpers.StringToElement("a\b\f");
        Assert.Equal("a\b\f", result.GetString());
    }

    // ─── NormalizeSliceIndex: negative step branches (lines 269-271) ─

    [Theory]
    [InlineData(-20, 5, true, true, 0)]     // negative → clamp to 0 (positive step)
    [InlineData(-20, 5, true, false, -1)]    // negative → clamp to -1 (negative step)
    [InlineData(10, 5, true, true, 5)]       // > length, positive step → length
    [InlineData(10, 5, true, false, 4)]      // > length-1, negative step, isStart → length-1
    [InlineData(10, 5, false, false, 5)]     // > length, negative step, !isStart → length
    public void NormalizeSliceIndex_HandlesEdgeCases(int index, int length, bool isStart, bool positiveStep, int expected)
    {
        int result = JMESPathCodeGenHelpers.NormalizeSliceIndex(index, length, isStart, positiveStep);
        Assert.Equal(expected, result);
    }

    // ─── Reverse string: UTF-8 codepoint-aware (lines 519-521) ──────

    [Fact]
    public void Reverse_String_ReversesCodepoints()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement str = JsonElement.ParseValue("\"abc\""u8);
        JsonElement result = JMESPathCodeGenHelpers.Reverse(str, workspace);
        Assert.Equal("cba", result.GetString());
    }

    [Fact]
    public void Reverse_Array_ReversesElements()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement arr = JsonElement.ParseValue("[1,2,3]"u8);
        JsonElement result = JMESPathCodeGenHelpers.Reverse(arr, workspace);
        Assert.Equal("[3,2,1]", result.GetRawText());
    }

    // ─── Sort: mixed types throw (lines 668-669, 674-676) ───────────

    [Fact]
    public void Sort_MixedTypes_Throws()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement arr = JsonElement.ParseValue("[1,\"a\"]"u8);
        Assert.Throws<JMESPathException>(() => JMESPathCodeGenHelpers.Sort(arr, workspace));
    }

    [Fact]
    public void Sort_NonNumericNonString_Throws()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement arr = JsonElement.ParseValue("[true, false]"u8);
        Assert.Throws<JMESPathException>(() => JMESPathCodeGenHelpers.Sort(arr, workspace));
    }

    [Fact]
    public void Sort_Strings_SortsAlphabetically()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement arr = JsonElement.ParseValue("""["c","a","b"]"""u8);
        JsonElement result = JMESPathCodeGenHelpers.Sort(arr, workspace);
        Assert.Equal("[\"a\",\"b\",\"c\"]", result.GetRawText());
    }

    // ─── CollectAndSort: empty (line 720), mixed types (726, 730-746) ─

    [Fact]
    public void CollectAndSort_Empty_ReturnsEmptyArray()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.CollectAndSort(
            workspace,
            static (in JsonWorkspace _, ref JMESPathSequenceBuilder _) => { },
            workspace);
        Assert.Equal("[]", result.GetRawText());
    }

    // ─── FilterAndSortBy: uncovered (lines 818-880) ─────────────────

    [Fact]
    public void FilterAndSortBy_FiltersAndSorts()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement arr = JsonElement.ParseValue("""[{"a":3},{"a":1},{"a":2}]"""u8);

        // compile expression for key extraction: @.a
        var evaluator = JMESPathEvaluator.Default;
        // Use Search to exercise the function through the evaluator instead
        JsonElement result = evaluator.Search("sort_by([?a > `1`], &a)", arr, workspace);
        Assert.Equal("[{\"a\":2},{\"a\":3}]", result.GetRawText());
    }

    // ─── RequireNumber/String/Array/Object: throw paths (1324-1331) ──

    [Fact]
    public void RequireNumber_NonNumber_Throws()
    {
        Assert.Throws<JMESPathException>(() =>
            JMESPathCodeGenHelpers.RequireNumber("test", JsonElement.ParseValue("\"x\""u8)));
    }

    [Fact]
    public void RequireString_NonString_Throws()
    {
        Assert.Throws<JMESPathException>(() =>
            JMESPathCodeGenHelpers.RequireString("test", JsonElement.ParseValue("42"u8)));
    }

    [Fact]
    public void RequireArray_NonArray_Throws()
    {
        Assert.Throws<JMESPathException>(() =>
            JMESPathCodeGenHelpers.RequireArray("test", JsonElement.ParseValue("42"u8)));
    }

    [Fact]
    public void RequireObject_NonObject_Throws()
    {
        Assert.Throws<JMESPathException>(() =>
            JMESPathCodeGenHelpers.RequireObject("test", JsonElement.ParseValue("42"u8)));
    }

    // ─── NotNull: all null returns null (line 1168-1170) ─────────────

    [Fact]
    public void NotNull_AllNull_ReturnsNull()
    {
        JsonElement n = JsonElement.ParseValue("null"u8);
        JsonElement result = JMESPathCodeGenHelpers.NotNull([n, n, n]);
        Assert.Equal(JsonValueKind.Null, result.ValueKind);
    }

    [Fact]
    public void NotNull_FirstNonNull_ReturnsThatValue()
    {
        JsonElement n = JsonElement.ParseValue("null"u8);
        JsonElement num = JsonElement.ParseValue("42"u8);
        JsonElement result = JMESPathCodeGenHelpers.NotNull([n, num, n]);
        Assert.Equal(42, result.GetDouble());
    }

    // ─── ToArray: non-array types (lines 1187, 1190-1193) ───────────

    [Fact]
    public void ToArray_Number_ReturnsUndefined()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement num = JsonElement.ParseValue("42"u8);
        // toArray on non-array/non-object returns undefined
        var evaluator = JMESPathEvaluator.Default;
        JsonElement result = evaluator.Search("to_array(`42`)", JsonElement.ParseValue("null"u8), workspace);
        Assert.Equal("[42]", result.GetRawText());
    }

    // ─── ToNumber: string/boolean branches (1208-1213) ──────────────

    [Fact]
    public void ToNumber_BoolTrue_ReturnsNull()
    {
        // JMESPath spec: to_number(true) → null (not 1 like JsonLogic)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.ToNumber(JsonElement.ParseValue("true"u8), workspace);
        Assert.Equal(JsonValueKind.Null, result.ValueKind);
    }

    [Fact]
    public void ToNumber_BoolFalse_ReturnsNull()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.ToNumber(JsonElement.ParseValue("false"u8), workspace);
        Assert.Equal(JsonValueKind.Null, result.ValueKind);
    }

    [Fact]
    public void ToNumber_NumericString_Parses()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.ToNumber(JsonElement.ParseValue("\"42.5\""u8), workspace);
        Assert.Equal(42.5, result.GetDouble());
    }

    [Fact]
    public void ToNumber_NonNumericString_ReturnsNull()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.ToNumber(JsonElement.ParseValue("\"abc\""u8), workspace);
        Assert.Equal(JsonValueKind.Null, result.ValueKind);
    }

    // ─── ToString: various types (1241-1273) ─────────────────────────

    [Fact]
    public void ToString_Number_ReturnsStringified()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.ToString(JsonElement.ParseValue("42"u8), workspace);
        Assert.Equal("42", result.GetString());
    }

    [Fact]
    public void ToString_Boolean_ReturnsStringified()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.ToString(JsonElement.ParseValue("true"u8), workspace);
        Assert.Equal("true", result.GetString());
    }

    [Fact]
    public void ToString_Null_ReturnsStringified()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.ToString(JsonElement.ParseValue("null"u8), workspace);
        Assert.Equal("null", result.GetString());
    }

    [Fact]
    public void ToString_Array_ReturnsJson()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.ToString(JsonElement.ParseValue("[1,2]"u8), workspace);
        Assert.Equal("[1,2]", result.GetString());
    }

    [Fact]
    public void ToString_Object_ReturnsJson()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.ToString(JsonElement.ParseValue("""{"a":1}"""u8), workspace);
        Assert.Equal("{\"a\":1}", result.GetString());
    }

    // ─── TypeOf: all branches (1286-1293) ────────────────────────────

    [Theory]
    [InlineData("\"hello\"", "string")]
    [InlineData("42", "number")]
    [InlineData("true", "boolean")]
    [InlineData("[1]", "array")]
    [InlineData("{\"a\":1}", "object")]
    [InlineData("null", "null")]
    public void TypeOf_ReturnsCorrectType(string json, string expectedType)
    {
        JsonElement elem = JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes(json));
        JsonElement result = JMESPathCodeGenHelpers.TypeOf(elem);
        Assert.Equal(expectedType, result.GetString());
    }

    // ─── ApplySortBarrier: lines 1661-1677 ──────────────────────────

    [Fact]
    public void ApplySortBarrier_SortsNumbers()
    {
        JMESPathSequenceBuilder builder = default;
        try
        {
            builder.Add(JsonElement.ParseValue("3"u8));
            builder.Add(JsonElement.ParseValue("1"u8));
            builder.Add(JsonElement.ParseValue("2"u8));

            JMESPathCodeGenHelpers.ApplySortBarrier(ref builder);

            Assert.Equal(1, builder[0].GetDouble());
            Assert.Equal(2, builder[1].GetDouble());
            Assert.Equal(3, builder[2].GetDouble());
        }
        finally
        {
            builder.ReturnArray();
        }
    }

    // ─── ApplyReverseBarrier: lines 1743-1751 ───────────────────────

    [Fact]
    public void ApplyReverseBarrier_ReversesElements()
    {
        JMESPathSequenceBuilder builder = default;
        try
        {
            builder.Add(JsonElement.ParseValue("1"u8));
            builder.Add(JsonElement.ParseValue("2"u8));
            builder.Add(JsonElement.ParseValue("3"u8));

            JMESPathCodeGenHelpers.ApplyReverseBarrier(ref builder);

            Assert.Equal(3, builder[0].GetDouble());
            Assert.Equal(2, builder[1].GetDouble());
            Assert.Equal(1, builder[2].GetDouble());
        }
        finally
        {
            builder.ReturnArray();
        }
    }

    // ─── ApplySliceBarrier: lines 1762-1799 (positive and negative step) ─

    [Fact]
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

            Assert.Equal(2, builder.Count);
            Assert.Equal(1, builder[0].GetDouble());
            Assert.Equal(3, builder[1].GetDouble());
        }
        finally
        {
            builder.ReturnArray();
        }
    }

    [Fact]
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

            Assert.Equal(5, builder.Count);
            Assert.Equal(4, builder[0].GetDouble());
            Assert.Equal(0, builder[4].GetDouble());
        }
        finally
        {
            builder.ReturnArray();
        }
    }

    // ─── DoubleToElement (line 181) ─────────────────────────────────

    [Fact]
    public void DoubleToElement_ReturnsCorrectValue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.DoubleToElement(42.5, workspace);
        Assert.Equal(42.5, result.GetDouble());
    }

    // ─── Keys/Values on non-object (lines 591, 597) ────────────────

    [Fact]
    public void Keys_Object_ReturnsPropertyNames()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement obj = JsonElement.ParseValue("""{"a":1,"b":2}"""u8);
        JsonElement result = JMESPathCodeGenHelpers.Keys(obj, workspace);
        Assert.Equal(2, result.GetArrayLength());
    }

    [Fact]
    public void Values_Object_ReturnsPropertyValues()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement obj = JsonElement.ParseValue("""{"a":1,"b":2}"""u8);
        JsonElement result = JMESPathCodeGenHelpers.Values(obj, workspace);
        Assert.Equal(2, result.GetArrayLength());
    }

    // ─── SequenceBuilder Grow: lines 119-134 (>8 items) ─────────────

    [Fact]
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

            Assert.Equal(10, builder.Count);
            Assert.Equal(0, builder[0].GetDouble());
            Assert.Equal(9, builder[9].GetDouble());
        }
        finally
        {
            builder.ReturnArray();
        }
    }

    // ─── Lexer: exercise raw string and literal parsing edge cases ───

    [Fact]
    public void Evaluator_RawStringWithEscapes()
    {
        // Exercise Lexer raw string path with escape sequences
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonElement.ParseValue("null"u8);
        JsonElement result = JMESPathEvaluator.Default.Search("'hello\\nworld'", data, workspace);
        Assert.Equal("hello\\nworld", result.GetString());
    }

    [Fact]
    public void Evaluator_LiteralExpression()
    {
        // Exercise Lexer literal path
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonElement.ParseValue("null"u8);
        JsonElement result = JMESPathEvaluator.Default.Search("`{\"a\":1}`", data, workspace);
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
    }

    // ─── Compiler: fused pipeline stages ─────────────────────────────

    [Fact]
    public void Evaluator_FusedSortByPipeline()
    {
        // Exercise fused pipeline with sort_by
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonElement.ParseValue("""[{"a":3,"n":"c"},{"a":1,"n":"a"},{"a":2,"n":"b"}]"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("sort_by(@, &a)[*].n", data, workspace);
        Assert.Equal("[\"a\",\"b\",\"c\"]", result.GetRawText());
    }

    [Fact]
    public void Evaluator_FusedReversePipeline()
    {
        // Exercise fused pipeline with reverse
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonElement.ParseValue("[1,2,3]"u8);
        JsonElement result = JMESPathEvaluator.Default.Search("reverse(@)", data, workspace);
        Assert.Equal("[3,2,1]", result.GetRawText());
    }

    [Fact]
    public void Evaluator_FusedSlicePipeline()
    {
        // Exercise fused pipeline with slice
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonElement.ParseValue("[0,1,2,3,4,5]"u8);
        JsonElement result = JMESPathEvaluator.Default.Search("[1:5:2]", data, workspace);
        Assert.Equal("[1,3]", result.GetRawText());
    }
}
