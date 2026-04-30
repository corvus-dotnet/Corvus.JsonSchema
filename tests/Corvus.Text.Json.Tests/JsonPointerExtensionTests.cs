// <copyright file="JsonPointerExtensionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Text.Json;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for JSON Pointer (RFC 6901) extension methods and segment enumeration.
/// </summary>
public class JsonPointerExtensionTests
{
    private const string Rfc6901ExampleDocument = """
        {
            "foo": ["bar", "baz"],
            "": 0,
            "a/b": 1,
            "c%d": 2,
            "e^f": 3,
            "g|h": 4,
            "i\\j": 5,
            "k\"l": 6,
            " ": 7,
            "m~n": 8
        }
        """;

    #region RFC 6901 Section 5 - UTF-8 overload

    [Fact]
    public void ResolveEmptyPointer_ReturnsWholeDocument()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer(""u8, out JsonElement resolved);
        Assert.True(result);
        Assert.Equal(JsonValueKind.Object, resolved.ValueKind);
    }

    [Fact]
    public void ResolveFoo0_ReturnsBar()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("/foo/0"u8, out JsonElement resolved);
        Assert.True(result);
        Assert.Equal("bar", resolved.GetString());
    }

    [Fact]
    public void ResolveEmptyKey_ReturnsZero()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("/"u8, out JsonElement resolved);
        Assert.True(result);
        Assert.Equal(0, resolved.GetInt32());
    }

    [Fact]
    public void ResolveSlashInKey_UsesEscaping()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("/a~1b"u8, out JsonElement resolved);
        Assert.True(result);
        Assert.Equal(1, resolved.GetInt32());
    }

    [Fact]
    public void ResolvePercentInKey()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("/c%d"u8, out JsonElement resolved);
        Assert.True(result);
        Assert.Equal(2, resolved.GetInt32());
    }

    [Fact]
    public void ResolveCaretInKey()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("/e^f"u8, out JsonElement resolved);
        Assert.True(result);
        Assert.Equal(3, resolved.GetInt32());
    }

    [Fact]
    public void ResolvePipeInKey()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("/g|h"u8, out JsonElement resolved);
        Assert.True(result);
        Assert.Equal(4, resolved.GetInt32());
    }

    [Fact]
    public void ResolveBackslashInKey()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("/i\\j"u8, out JsonElement resolved);
        Assert.True(result);
        Assert.Equal(5, resolved.GetInt32());
    }

    [Fact]
    public void ResolveDoubleQuoteInKey()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("/k\"l"u8, out JsonElement resolved);
        Assert.True(result);
        Assert.Equal(6, resolved.GetInt32());
    }

    [Fact]
    public void ResolveSpaceInKey()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("/ "u8, out JsonElement resolved);
        Assert.True(result);
        Assert.Equal(7, resolved.GetInt32());
    }

    [Fact]
    public void ResolveTildeInKey_UsesEscaping()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("/m~0n"u8, out JsonElement resolved);
        Assert.True(result);
        Assert.Equal(8, resolved.GetInt32());
    }

    #endregion

    #region String overload

    [Fact]
    public void StringOverload_ResolveEmptyPointer()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("", out JsonElement resolved);
        Assert.True(result);
        Assert.Equal(JsonValueKind.Object, resolved.ValueKind);
    }

    [Fact]
    public void StringOverload_ResolveFoo0()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("/foo/0", out JsonElement resolved);
        Assert.True(result);
        Assert.Equal("bar", resolved.GetString());
    }

    [Fact]
    public void StringOverload_ResolveEscaping()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("/a~1b", out JsonElement resolved);
        Assert.True(result);
        Assert.Equal(1, resolved.GetInt32());
    }

    #endregion

    #region ReadOnlySpan char overload

    [Fact]
    public void CharSpanOverload_ResolveEmptyPointer()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("".AsSpan(), out JsonElement resolved);
        Assert.True(result);
        Assert.Equal(JsonValueKind.Object, resolved.ValueKind);
    }

    [Fact]
    public void CharSpanOverload_ResolveFoo0()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("/foo/0".AsSpan(), out JsonElement resolved);
        Assert.True(result);
        Assert.Equal("bar", resolved.GetString());
    }

    #endregion

    #region Error Cases

    [Fact]
    public void InvalidPointer_NoLeadingSlash_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("foo"u8, out JsonElement _);
        Assert.False(result);
    }

    [Fact]
    public void NonexistentProperty_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("/nonexistent"u8, out JsonElement _);
        Assert.False(result);
    }

    [Fact]
    public void ArrayIndexOutOfRange_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("/foo/99"u8, out JsonElement _);
        Assert.False(result);
    }

    [Fact]
    public void InvalidEscape_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("/~2"u8, out JsonElement _);
        Assert.False(result);
    }

    [Fact]
    public void FragmentPointer_WithHash_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        bool result = doc.RootElement.TryResolvePointer("#/foo"u8, out JsonElement _);
        Assert.False(result);
    }

    [Fact]
    public void ResolveIntoScalar_ReturnsFalse()
    {
        string json = """{"a": "hello"}""";
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        bool result = doc.RootElement.TryResolvePointer("/a/child"u8, out JsonElement _);
        Assert.False(result);
    }

    #endregion

    #region Encoded Segment Enumerator

    [Fact]
    public void EnumerateSegments_EmptyPointer_YieldsNoSegments()
    {
        Utf8JsonPointer.TryCreateJsonPointer(""u8, out Utf8JsonPointer pointer);
        int count = 0;
        foreach (ReadOnlySpan<byte> _ in pointer.EnumerateEncodedSegments())
        {
            count++;
        }

        Assert.Equal(0, count);
    }

    [Fact]
    public void EnumerateSegments_SingleSlash_YieldsOneEmptySegment()
    {
        Utf8JsonPointer.TryCreateJsonPointer("/"u8, out Utf8JsonPointer pointer);
        int count = 0;
        int totalLength = 0;
        foreach (ReadOnlySpan<byte> segment in pointer.EnumerateEncodedSegments())
        {
            totalLength += segment.Length;
            count++;
        }

        Assert.Equal(1, count);
        Assert.Equal(0, totalLength);
    }

    [Fact]
    public void EnumerateSegments_DoubleSlash_YieldsTwoEmptySegments()
    {
        Utf8JsonPointer.TryCreateJsonPointer("//"u8, out Utf8JsonPointer pointer);
        int count = 0;
        foreach (ReadOnlySpan<byte> segment in pointer.EnumerateEncodedSegments())
        {
            Assert.Equal(0, segment.Length);
            count++;
        }

        Assert.Equal(2, count);
    }

    [Fact]
    public void EnumerateSegments_TrailingSlash_YieldsTrailingEmptySegment()
    {
        Utf8JsonPointer.TryCreateJsonPointer("/a/"u8, out Utf8JsonPointer pointer);
        List<string> segments = new();
        foreach (ReadOnlySpan<byte> segment in pointer.EnumerateEncodedSegments())
        {
            segments.Add(JsonReaderHelper.TranscodeHelper(segment));
        }

        Assert.Equal(2, segments.Count);
        Assert.Equal("a", segments[0]);
        Assert.Equal("", segments[1]);
    }

    [Fact]
    public void EnumerateSegments_FooZero_YieldsTwoSegments()
    {
        Utf8JsonPointer.TryCreateJsonPointer("/foo/0"u8, out Utf8JsonPointer pointer);
        List<string> segments = new();
        foreach (ReadOnlySpan<byte> segment in pointer.EnumerateEncodedSegments())
        {
            segments.Add(JsonReaderHelper.TranscodeHelper(segment));
        }

        Assert.Equal(2, segments.Count);
        Assert.Equal("foo", segments[0]);
        Assert.Equal("0", segments[1]);
    }

    [Fact]
    public void EnumerateSegments_EncodedTilde_YieldsEncodedSegment()
    {
        Utf8JsonPointer.TryCreateJsonPointer("/m~0n"u8, out Utf8JsonPointer pointer);
        List<string> segments = new();
        foreach (ReadOnlySpan<byte> segment in pointer.EnumerateEncodedSegments())
        {
            segments.Add(JsonReaderHelper.TranscodeHelper(segment));
        }

        Assert.Single(segments);
        Assert.Equal("m~0n", segments[0]);
    }

    [Fact]
    public void EnumerateSegments_EncodedSlash_YieldsEncodedSegment()
    {
        Utf8JsonPointer.TryCreateJsonPointer("/a~1b"u8, out Utf8JsonPointer pointer);
        List<string> segments = new();
        foreach (ReadOnlySpan<byte> segment in pointer.EnumerateEncodedSegments())
        {
            segments.Add(JsonReaderHelper.TranscodeHelper(segment));
        }

        Assert.Single(segments);
        Assert.Equal("a~1b", segments[0]);
    }

    [Fact]
    public void EnumerateSegments_DeepPath_YieldsAllSegments()
    {
        Utf8JsonPointer.TryCreateJsonPointer("/a/b/c/d/e"u8, out Utf8JsonPointer pointer);
        List<string> segments = new();
        foreach (ReadOnlySpan<byte> segment in pointer.EnumerateEncodedSegments())
        {
            segments.Add(JsonReaderHelper.TranscodeHelper(segment));
        }

        Assert.Equal(5, segments.Count);
        Assert.Equal("a", segments[0]);
        Assert.Equal("b", segments[1]);
        Assert.Equal("c", segments[2]);
        Assert.Equal("d", segments[3]);
        Assert.Equal("e", segments[4]);
    }

    #endregion

    #region Non-BMP and nested

    [Fact]
    public void ResolveNonBmpCharacterProperty()
    {
        string json = """{"𝄞": "treble"}""";
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        bool result = doc.RootElement.TryResolvePointer("/𝄞", out JsonElement resolved);
        Assert.True(result);
        Assert.Equal("treble", resolved.GetString());
    }

    [Fact]
    public void ResolveDeepNestedPath()
    {
        string json = """{"a": {"b": {"c": {"d": 42}}}}""";
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        bool result = doc.RootElement.TryResolvePointer("/a/b/c/d"u8, out JsonElement resolved);
        Assert.True(result);
        Assert.Equal(42, resolved.GetInt32());
    }

    [Fact]
    public void ResolveNestedArrayElement()
    {
        string json = """{"matrix": [[1, 2], [3, 4]]}""";
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        bool result = doc.RootElement.TryResolvePointer("/matrix/1/0"u8, out JsonElement resolved);
        Assert.True(result);
        Assert.Equal(3, resolved.GetInt32());
    }

    #endregion
}
