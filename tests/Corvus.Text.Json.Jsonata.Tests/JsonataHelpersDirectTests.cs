// <copyright file="JsonataHelpersDirectTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json.Jsonata;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Direct unit tests for <see cref="JsonataHelpers"/> internal methods, targeting
/// specific uncovered branches identified from merged Cobertura coverage data.
/// </summary>
[TestClass]
public class JsonataHelpersDirectTests
{
    // ─── Cached element accessors: lines 66, 78 ─────────────────────

    [TestMethod]
    public void MinusOne_ReturnsCachedElement()
    {
        JsonElement result = JsonataHelpers.MinusOne();
        Assert.AreEqual(-1, result.GetDouble());
    }

    [TestMethod]
    public void EmptyArray_ReturnsCachedElement()
    {
        JsonElement result = JsonataHelpers.EmptyArray();
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(0, result.GetArrayLength());
    }

    // ─── NumberFromDouble slow path: lines 100-102 ──────────────────
    // Line 100: Utf8Formatter.TryFormat fails → NumberFromDoubleSlow
    // This is hard to trigger with a valid double. Skip — we test the fast path instead.

    // ─── NumberFromLong: lines 125-128 (unreachable throw) ──────────
    // The throw at 126 is unreachable for valid longs but still exercises NumberFromLong.

    [TestMethod]
    [DataRow(0)]
    [DataRow(42)]
    [DataRow(-100)]
    [DataRow(long.MaxValue)]
    [DataRow(long.MinValue)]
    public void NumberFromLong_CreatesCorrectElement(long value)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonataHelpers.NumberFromLong(value, workspace);
        Assert.AreEqual(JsonValueKind.Number, result.ValueKind);
    }

    // ─── NumberFromUtf8Span: lines 140-144 (fully uncovered) ────────

    [TestMethod]
    public void NumberFromUtf8Span_CreatesElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonataHelpers.NumberFromUtf8Span("42"u8, workspace);
        Assert.AreEqual(42, result.GetDouble());
    }

    [TestMethod]
    public void NumberFromUtf8Span_NegativeNumber()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonataHelpers.NumberFromUtf8Span("-3.14"u8, workspace);
        Assert.AreEqual(-3.14, result.GetDouble());
    }

    // ─── StringFromRawUtf8Content: lines 221-222 (empty case) ───────

    [TestMethod]
    public void StringFromRawUtf8Content_EmptyReturnsEmptyString()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonataHelpers.StringFromRawUtf8Content(ReadOnlySpan<byte>.Empty, workspace);
        Assert.AreEqual(string.Empty, result.GetString());
    }

    [TestMethod]
    public void StringFromRawUtf8Content_NonEmptyCreatesString()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonataHelpers.StringFromRawUtf8Content("hello"u8, workspace);
        Assert.AreEqual("hello", result.GetString());
    }

    // ─── ArrayFromList empty case: lines 308-309 ────────────────────

    [TestMethod]
    public void ArrayFromList_EmptyList_ReturnsEmptyArray()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonataHelpers.ArrayFromList([], workspace);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(0, result.GetArrayLength());
    }

    [TestMethod]
    public void ArrayFromList_NonEmpty_CreatesArray()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        List<JsonElement> items =
        [
            JsonataHelpers.NumberFromDouble(1, workspace),
            JsonataHelpers.NumberFromDouble(2, workspace),
        ];
        JsonElement result = JsonataHelpers.ArrayFromList(items, workspace);
        Assert.AreEqual(2, result.GetArrayLength());
    }

    // ─── ArrayFromReadOnlyList: lines 349-364 (fully uncovered) ─────

    [TestMethod]
    public void ArrayFromReadOnlyList_EmptyList_ReturnsEmptyArray()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        IReadOnlyList<JsonElement> items = Array.Empty<JsonElement>();
        JsonElement result = JsonataHelpers.ArrayFromReadOnlyList(items, workspace);
        Assert.AreEqual(0, result.GetArrayLength());
    }

    [TestMethod]
    public void ArrayFromReadOnlyList_NonEmpty_CreatesArray()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        IReadOnlyList<JsonElement> items = new[]
        {
            JsonataHelpers.True(),
            JsonataHelpers.False(),
            JsonataHelpers.Null(),
        };
        JsonElement result = JsonataHelpers.ArrayFromReadOnlyList(items, workspace);
        Assert.AreEqual(3, result.GetArrayLength());
    }

    // ─── CreateMatchObjectFromMatch: lines 396-416 (regex CVB path) ─

    [TestMethod]
    public void CreateMatchObjectFromMatch_CreatesCorrectStructure()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        string input = "hello world";
        var regex = new System.Text.RegularExpressions.Regex(@"(hello) (world)");
        var match = regex.Match(input);
        JsonElement result = JsonataHelpers.CreateMatchObjectFromMatch(input.AsMemory(), match, workspace);

        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
        Assert.AreEqual("hello world", result.GetProperty("match").GetString());
        Assert.AreEqual(0, result.GetProperty("index").GetInt32());

        var groups = result.GetProperty("groups");
        Assert.AreEqual(2, groups.GetArrayLength());
        Assert.AreEqual("hello", groups[0].GetString());
        Assert.AreEqual("world", groups[1].GetString());
    }

#if NET
    // ─── CreateMatchObjectNoGroups: lines 425-437 (uncovered) ───────

    [TestMethod]
    public void CreateMatchObjectNoGroups_CreatesCorrectStructure()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        string input = "hello world";
        JsonElement result = JsonataHelpers.CreateMatchObjectNoGroups(input.AsMemory(), 6, 5, workspace);

        Assert.AreEqual("world", result.GetProperty("match").GetString());
        Assert.AreEqual(6, result.GetProperty("index").GetInt32());
        Assert.AreEqual(0, result.GetProperty("groups").GetArrayLength());
    }
#endif

    // ─── AppendCoercedToBuffer: lines 448-493 (all branches) ────────

    [TestMethod]
    public void AppendCoercedToBuffer_Undefined_DoesNothing()
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64);
        try
        {
            int pos = 0;
            JsonataHelpers.AppendCoercedToBuffer(default, ref buffer, ref pos);
            Assert.AreEqual(0, pos);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    [TestMethod]
    public void AppendCoercedToBuffer_Number_AppendsDigits()
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64);
        try
        {
            int pos = 0;
            JsonElement num = JsonElement.ParseValue("42"u8);
            JsonataHelpers.AppendCoercedToBuffer(num, ref buffer, ref pos);
            Assert.AreEqual(2, pos);
            Assert.AreEqual((byte)'4', buffer[0]);
            Assert.AreEqual((byte)'2', buffer[1]);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    [TestMethod]
    public void AppendCoercedToBuffer_True_AppendsLiteral()
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64);
        try
        {
            int pos = 0;
            JsonElement t = JsonElement.ParseValue("true"u8);
            JsonataHelpers.AppendCoercedToBuffer(t, ref buffer, ref pos);
            Assert.AreEqual(4, pos);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    [TestMethod]
    public void AppendCoercedToBuffer_False_AppendsLiteral()
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64);
        try
        {
            int pos = 0;
            JsonElement f = JsonElement.ParseValue("false"u8);
            JsonataHelpers.AppendCoercedToBuffer(f, ref buffer, ref pos);
            Assert.AreEqual(5, pos);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    [TestMethod]
    public void AppendCoercedToBuffer_Null_AppendsLiteral()
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64);
        try
        {
            int pos = 0;
            JsonElement n = JsonElement.ParseValue("null"u8);
            JsonataHelpers.AppendCoercedToBuffer(n, ref buffer, ref pos);
            Assert.AreEqual(4, pos);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // ─── GrowBufferIfNeeded / GrowBuffer: lines 504-506, 678-683 ────

    [TestMethod]
    public void GrowBufferIfNeeded_TriggersGrowth()
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(4);
        try
        {
            int pos = 3;
            // Need 10 bytes but only 1 remaining → triggers growth
            JsonataHelpers.GrowBufferIfNeeded(ref buffer, pos, 10);
            Assert.IsTrue(buffer.Length >= 13);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // ─── WriteJsonEscapedFromChars: lines 539-566 (control chars) ───

    [TestMethod]
    public void StringFromString_WithControlChars_EscapesCorrectly()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        // \b, \f, \r, \t are in the uncovered branches
        JsonElement result = JsonataHelpers.StringFromString("a\b\f\r\tb", workspace);
        string val = result.GetString()!;
        Assert.AreEqual("a\b\f\r\tb", val);
    }

    [TestMethod]
    public void StringFromString_WithControlCharBelowSpace_EscapesAsUnicode()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        // \x01 is a control char < 0x20 that isn't \b,\f,\n,\r,\t → \u0001
        JsonElement result = JsonataHelpers.StringFromString("\x01", workspace);
        Assert.AreEqual("\x01", result.GetString());
    }

    // ─── WriteJsonEscapedFromUtf8: lines 622-657 (UTF-8 escaping) ───

    [TestMethod]
    public void StringFromUnescapedUtf8_WithBackslash_Escapes()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        // Backslash triggers line 622-624
        JsonElement result = JsonataHelpers.StringFromUnescapedUtf8("a\\b"u8, workspace);
        Assert.AreEqual("a\\b", result.GetString());
    }

    [TestMethod]
    public void StringFromUnescapedUtf8_WithControlChars_Escapes()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        // \b=0x08, \f=0x0C, \r=0x0D → triggers lines 631-645
        byte[] input = [0x08, 0x0C, 0x0D]; // \b, \f, \r
        JsonElement result = JsonataHelpers.StringFromUnescapedUtf8(input, workspace);
        Assert.AreEqual("\b\f\r", result.GetString());
    }

    [TestMethod]
    public void StringFromUnescapedUtf8_WithTabAndLowControl_Escapes()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        // \t=0x09 → line 647-649, \x01 → lines 651-657 (default \u00XX path)
        byte[] input = [0x09, 0x01]; // \t, \x01
        JsonElement result = JsonataHelpers.StringFromUnescapedUtf8(input, workspace);
        Assert.AreEqual("\t\x01", result.GetString());
    }

    // ─── HexDigit: lines 672-673 (called by control char escape) ────
    // Already exercised by the \x01 test above (writes \u0001 → HexDigit)

    // ─── NumberFromDoubleSlow: lines 687-710 (large double fallback) ─
    // Hard to trigger since Utf8Formatter.TryFormat rarely fails for valid doubles.
    // The primary path (NumberFromDouble) is exercised by many tests.

    // ─── NumberFromDouble normal path ────────────────────────────────

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(42.5)]
    [DataRow(-1.23e10)]
    public void NumberFromDouble_CreatesCorrectElement(double value)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonataHelpers.NumberFromDouble(value, workspace);
        Assert.AreEqual(value, result.GetDouble());
    }
}
