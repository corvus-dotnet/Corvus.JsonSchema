// <copyright file="JsonStringUnescaperTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Xunit;

namespace Corvus.Text.Json.Tests;

public class JsonStringUnescaperTests
{
    [Theory]
    [InlineData("""say\u0022hi""", "say\"hi")]
    [InlineData("""\u003Chtml\u003E""", "<html>")]
    [InlineData("""\u0026amp;""", "&amp;")]
    [InlineData("""line1\nline2""", "line1\nline2")]
    [InlineData("""tab\there""", "tab\there")]
    [InlineData("""back\\slash""", "back\\slash")]
    [InlineData("""quote\"here""", "quote\"here")]
    [InlineData("""slash\/path""", "slash/path")]
    [InlineData("""cr\rhere""", "cr\rhere")]
    [InlineData("""bs\bhere""", "bs\bhere")]
    [InlineData("""ff\fhere""", "ff\fhere")]
    public void Unescape_HandlesStandardEscapes(string escapedUtf16, string expectedUtf16)
    {
        byte[] source = Encoding.UTF8.GetBytes(escapedUtf16);
        byte[] destination = new byte[source.Length];

        JsonStringUnescaper.Unescape(source, destination, out int written);

        string result = Encoding.UTF8.GetString(destination, 0, written);
        Assert.Equal(expectedUtf16, result);
    }

    [Theory]
    [InlineData("""\uD83D\uDE00""", "\U0001F600")] // 😀 emoji (surrogate pair)
    [InlineData("""\uD834\uDD1E""", "\U0001D11E")] // 𝄞 musical symbol G clef
    public void Unescape_HandlesSurrogatePairs(string escapedUtf16, string expectedUtf16)
    {
        byte[] source = Encoding.UTF8.GetBytes(escapedUtf16);
        byte[] destination = new byte[source.Length];

        JsonStringUnescaper.Unescape(source, destination, out int written);

        string result = Encoding.UTF8.GetString(destination, 0, written);
        Assert.Equal(expectedUtf16, result);
    }

    [Theory]
    [InlineData("""prefix\u0041suffix""", "prefixAsuffix")]
    [InlineData("""\u0048\u0065\u006C\u006C\u006F""", "Hello")]
    public void Unescape_WithIdx_HandlesUnicodeEscapes(string escapedUtf16, string expectedUtf16)
    {
        byte[] source = Encoding.UTF8.GetBytes(escapedUtf16);
        byte[] destination = new byte[source.Length];
        int idx = Array.IndexOf(source, (byte)'\\');

        JsonStringUnescaper.Unescape(source, destination, idx, out int written);

        string result = Encoding.UTF8.GetString(destination, 0, written);
        Assert.Equal(expectedUtf16, result);
    }

    [Fact]
    public void TryUnescape_ReturnsTrueWhenDestinationIsSufficient()
    {
        byte[] source = Encoding.UTF8.GetBytes("""say\u0022hi""");
        byte[] destination = new byte[source.Length];

        bool result = JsonStringUnescaper.TryUnescape(source, destination, out int written);

        Assert.True(result);
        string actual = Encoding.UTF8.GetString(destination, 0, written);
        Assert.Equal("say\"hi", actual);
    }

    [Fact]
    public void TryUnescape_ReturnsFalseWhenDestinationTooSmall()
    {
        byte[] source = Encoding.UTF8.GetBytes("""hello\u0041world""");
        byte[] destination = new byte[2]; // way too small

        bool result = JsonStringUnescaper.TryUnescape(source, destination, out _);

        Assert.False(result);
    }

    [Theory]
    [InlineData("""multiple\u0022escapes\u003Cin\u003Eone\u0026string""", "multiple\"escapes<in>one&string")]
    public void Unescape_HandlesMultipleEscapesInOneString(string escapedUtf16, string expectedUtf16)
    {
        byte[] source = Encoding.UTF8.GetBytes(escapedUtf16);
        byte[] destination = new byte[source.Length];

        JsonStringUnescaper.Unescape(source, destination, out int written);

        string result = Encoding.UTF8.GetString(destination, 0, written);
        Assert.Equal(expectedUtf16, result);
    }
}