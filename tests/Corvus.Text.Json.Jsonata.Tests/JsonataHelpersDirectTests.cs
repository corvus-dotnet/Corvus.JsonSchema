// <copyright file="JsonataHelpersDirectTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json.Jsonata;
using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Direct unit tests for <see cref="JsonataHelpers"/> internal methods, targeting
/// specific uncovered branches identified from merged Cobertura coverage data.
/// </summary>
public class JsonataHelpersDirectTests
{
    // ─── Cached element accessors: lines 66, 78 ─────────────────────

    [Fact]
    public void MinusOne_ReturnsCachedElement()
    {
        JsonElement result = JsonataHelpers.MinusOne();
        Assert.Equal(-1, result.GetDouble());
    }

    [Fact]
    public void EmptyArray_ReturnsCachedElement()
    {
        JsonElement result = JsonataHelpers.EmptyArray();
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(0, result.GetArrayLength());
    }

    // ─── NumberFromDouble slow path: lines 100-102 ──────────────────
    // Line 100: Utf8Formatter.TryFormat fails → NumberFromDoubleSlow
    // This is hard to trigger with a valid double. Skip — we test the fast path instead.

    // ─── NumberFromLong: lines 125-128 (unreachable throw) ──────────
    // The throw at 126 is unreachable for valid longs but still exercises NumberFromLong.

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-100)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void NumberFromLong_CreatesCorrectElement(long value)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonataHelpers.NumberFromLong(value, workspace);
        Assert.Equal(JsonValueKind.Number, result.ValueKind);
    }

    // ─── NumberFromUtf8Span: lines 140-144 (fully uncovered) ────────

    [Fact]
    public void NumberFromUtf8Span_CreatesElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonataHelpers.NumberFromUtf8Span("42"u8, workspace);
        Assert.Equal(42, result.GetDouble());
    }

    [Fact]
    public void NumberFromUtf8Span_NegativeNumber()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonataHelpers.NumberFromUtf8Span("-3.14"u8, workspace);
        Assert.Equal(-3.14, result.GetDouble());
    }

    // ─── StringFromRawUtf8Content: lines 221-222 (empty case) ───────

    [Fact]
    public void StringFromRawUtf8Content_EmptyReturnsEmptyString()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonataHelpers.StringFromRawUtf8Content(ReadOnlySpan<byte>.Empty, workspace);
        Assert.Equal(string.Empty, result.GetString());
    }

    [Fact]
    public void StringFromRawUtf8Content_NonEmptyCreatesString()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonataHelpers.StringFromRawUtf8Content("hello"u8, workspace);
        Assert.Equal("hello", result.GetString());
    }

    // ─── ArrayFromList empty case: lines 308-309 ────────────────────

    [Fact]
    public void ArrayFromList_EmptyList_ReturnsEmptyArray()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonataHelpers.ArrayFromList([], workspace);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(0, result.GetArrayLength());
    }

    [Fact]
    public void ArrayFromList_NonEmpty_CreatesArray()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        List<JsonElement> items =
        [
            JsonataHelpers.NumberFromDouble(1, workspace),
            JsonataHelpers.NumberFromDouble(2, workspace),
        ];
        JsonElement result = JsonataHelpers.ArrayFromList(items, workspace);
        Assert.Equal(2, result.GetArrayLength());
    }

    // ─── ArrayFromReadOnlyList: lines 349-364 (fully uncovered) ─────

    [Fact]
    public void ArrayFromReadOnlyList_EmptyList_ReturnsEmptyArray()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        IReadOnlyList<JsonElement> items = Array.Empty<JsonElement>();
        JsonElement result = JsonataHelpers.ArrayFromReadOnlyList(items, workspace);
        Assert.Equal(0, result.GetArrayLength());
    }

    [Fact]
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
        Assert.Equal(3, result.GetArrayLength());
    }

    // ─── CreateMatchObjectFromMatch: lines 396-416 (regex CVB path) ─

    [Fact]
    public void CreateMatchObjectFromMatch_CreatesCorrectStructure()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        string input = "hello world";
        var regex = new System.Text.RegularExpressions.Regex(@"(hello) (world)");
        var match = regex.Match(input);
        JsonElement result = JsonataHelpers.CreateMatchObjectFromMatch(input.AsMemory(), match, workspace);

        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal("hello world", result.GetProperty("match").GetString());
        Assert.Equal(0, result.GetProperty("index").GetInt32());

        var groups = result.GetProperty("groups");
        Assert.Equal(2, groups.GetArrayLength());
        Assert.Equal("hello", groups[0].GetString());
        Assert.Equal("world", groups[1].GetString());
    }

#if NET
    // ─── CreateMatchObjectNoGroups: lines 425-437 (uncovered) ───────

    [Fact]
    public void CreateMatchObjectNoGroups_CreatesCorrectStructure()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        string input = "hello world";
        JsonElement result = JsonataHelpers.CreateMatchObjectNoGroups(input.AsMemory(), 6, 5, workspace);

        Assert.Equal("world", result.GetProperty("match").GetString());
        Assert.Equal(6, result.GetProperty("index").GetInt32());
        Assert.Equal(0, result.GetProperty("groups").GetArrayLength());
    }
#endif

    // ─── AppendCoercedToBuffer: lines 448-493 (all branches) ────────

    [Fact]
    public void AppendCoercedToBuffer_Undefined_DoesNothing()
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64);
        try
        {
            int pos = 0;
            JsonataHelpers.AppendCoercedToBuffer(default, ref buffer, ref pos);
            Assert.Equal(0, pos);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    [Fact]
    public void AppendCoercedToBuffer_Number_AppendsDigits()
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64);
        try
        {
            int pos = 0;
            JsonElement num = JsonElement.ParseValue("42"u8);
            JsonataHelpers.AppendCoercedToBuffer(num, ref buffer, ref pos);
            Assert.Equal(2, pos);
            Assert.Equal((byte)'4', buffer[0]);
            Assert.Equal((byte)'2', buffer[1]);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    [Fact]
    public void AppendCoercedToBuffer_True_AppendsLiteral()
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64);
        try
        {
            int pos = 0;
            JsonElement t = JsonElement.ParseValue("true"u8);
            JsonataHelpers.AppendCoercedToBuffer(t, ref buffer, ref pos);
            Assert.Equal(4, pos);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    [Fact]
    public void AppendCoercedToBuffer_False_AppendsLiteral()
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64);
        try
        {
            int pos = 0;
            JsonElement f = JsonElement.ParseValue("false"u8);
            JsonataHelpers.AppendCoercedToBuffer(f, ref buffer, ref pos);
            Assert.Equal(5, pos);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    [Fact]
    public void AppendCoercedToBuffer_Null_AppendsLiteral()
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64);
        try
        {
            int pos = 0;
            JsonElement n = JsonElement.ParseValue("null"u8);
            JsonataHelpers.AppendCoercedToBuffer(n, ref buffer, ref pos);
            Assert.Equal(4, pos);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // ─── GrowBufferIfNeeded / GrowBuffer: lines 504-506, 678-683 ────

    [Fact]
    public void GrowBufferIfNeeded_TriggersGrowth()
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(4);
        try
        {
            int pos = 3;
            // Need 10 bytes but only 1 remaining → triggers growth
            JsonataHelpers.GrowBufferIfNeeded(ref buffer, pos, 10);
            Assert.True(buffer.Length >= 13);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // ─── WriteJsonEscapedFromChars: lines 539-566 (control chars) ───

    [Fact]
    public void StringFromString_WithControlChars_EscapesCorrectly()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        // \b, \f, \r, \t are in the uncovered branches
        JsonElement result = JsonataHelpers.StringFromString("a\b\f\r\tb", workspace);
        string val = result.GetString()!;
        Assert.Equal("a\b\f\r\tb", val);
    }

    [Fact]
    public void StringFromString_WithControlCharBelowSpace_EscapesAsUnicode()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        // \x01 is a control char < 0x20 that isn't \b,\f,\n,\r,\t → \u0001
        JsonElement result = JsonataHelpers.StringFromString("\x01", workspace);
        Assert.Equal("\x01", result.GetString());
    }

    // ─── WriteJsonEscapedFromUtf8: lines 622-657 (UTF-8 escaping) ───

    [Fact]
    public void StringFromUnescapedUtf8_WithBackslash_Escapes()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        // Backslash triggers line 622-624
        JsonElement result = JsonataHelpers.StringFromUnescapedUtf8("a\\b"u8, workspace);
        Assert.Equal("a\\b", result.GetString());
    }

    [Fact]
    public void StringFromUnescapedUtf8_WithControlChars_Escapes()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        // \b=0x08, \f=0x0C, \r=0x0D → triggers lines 631-645
        byte[] input = [0x08, 0x0C, 0x0D]; // \b, \f, \r
        JsonElement result = JsonataHelpers.StringFromUnescapedUtf8(input, workspace);
        Assert.Equal("\b\f\r", result.GetString());
    }

    [Fact]
    public void StringFromUnescapedUtf8_WithTabAndLowControl_Escapes()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        // \t=0x09 → line 647-649, \x01 → lines 651-657 (default \u00XX path)
        byte[] input = [0x09, 0x01]; // \t, \x01
        JsonElement result = JsonataHelpers.StringFromUnescapedUtf8(input, workspace);
        Assert.Equal("\t\x01", result.GetString());
    }

    // ─── HexDigit: lines 672-673 (called by control char escape) ────
    // Already exercised by the \x01 test above (writes \u0001 → HexDigit)

    // ─── NumberFromDoubleSlow: lines 687-710 (large double fallback) ─
    // Hard to trigger since Utf8Formatter.TryFormat rarely fails for valid doubles.
    // The primary path (NumberFromDouble) is exercised by many tests.

    // ─── NumberFromDouble normal path ────────────────────────────────

    [Theory]
    [InlineData(0.0)]
    [InlineData(42.5)]
    [InlineData(-1.23e10)]
    public void NumberFromDouble_CreatesCorrectElement(double value)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonataHelpers.NumberFromDouble(value, workspace);
        Assert.Equal(value, result.GetDouble());
    }
}
