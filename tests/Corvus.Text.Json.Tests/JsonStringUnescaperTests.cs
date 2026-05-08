// <copyright file="JsonStringUnescaperTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class JsonStringUnescaperTests
{
    [TestMethod]
    [DataRow("""say\u0022hi""", "say\"hi")]
    [DataRow("""\u003Chtml\u003E""", "<html>")]
    [DataRow("""\u0026amp;""", "&amp;")]
    [DataRow("""line1\nline2""", "line1\nline2")]
    [DataRow("""tab\there""", "tab\there")]
    [DataRow("""back\\slash""", "back\\slash")]
    [DataRow("""quote\"here""", "quote\"here")]
    [DataRow("""slash\/path""", "slash/path")]
    [DataRow("""cr\rhere""", "cr\rhere")]
    [DataRow("""bs\bhere""", "bs\bhere")]
    [DataRow("""ff\fhere""", "ff\fhere")]
    public void Unescape_HandlesStandardEscapes(string escapedUtf16, string expectedUtf16)
    {
        byte[] source = Encoding.UTF8.GetBytes(escapedUtf16);
        byte[] destination = new byte[source.Length];

        JsonStringUnescaper.Unescape(source, destination, out int written);

        string result = Encoding.UTF8.GetString(destination, 0, written);
        Assert.AreEqual(expectedUtf16, result);
    }

    [TestMethod]
    [DataRow("""\uD83D\uDE00""", "\U0001F600")] // 😀 emoji (surrogate pair)
    [DataRow("""\uD834\uDD1E""", "\U0001D11E")] // 𝄞 musical symbol G clef
    public void Unescape_HandlesSurrogatePairs(string escapedUtf16, string expectedUtf16)
    {
        byte[] source = Encoding.UTF8.GetBytes(escapedUtf16);
        byte[] destination = new byte[source.Length];

        JsonStringUnescaper.Unescape(source, destination, out int written);

        string result = Encoding.UTF8.GetString(destination, 0, written);
        Assert.AreEqual(expectedUtf16, result);
    }

    [TestMethod]
    [DataRow("""prefix\u0041suffix""", "prefixAsuffix")]
    [DataRow("""\u0048\u0065\u006C\u006C\u006F""", "Hello")]
    public void Unescape_WithIdx_HandlesUnicodeEscapes(string escapedUtf16, string expectedUtf16)
    {
        byte[] source = Encoding.UTF8.GetBytes(escapedUtf16);
        byte[] destination = new byte[source.Length];
        int idx = Array.IndexOf(source, (byte)'\\');

        JsonStringUnescaper.Unescape(source, destination, idx, out int written);

        string result = Encoding.UTF8.GetString(destination, 0, written);
        Assert.AreEqual(expectedUtf16, result);
    }

    [TestMethod]
    public void TryUnescape_ReturnsTrueWhenDestinationIsSufficient()
    {
        byte[] source = Encoding.UTF8.GetBytes("""say\u0022hi""");
        byte[] destination = new byte[source.Length];

        bool result = JsonStringUnescaper.TryUnescape(source, destination, out int written);

        Assert.IsTrue(result);
        string actual = Encoding.UTF8.GetString(destination, 0, written);
        Assert.AreEqual("say\"hi", actual);
    }

    [TestMethod]
    public void TryUnescape_ReturnsFalseWhenDestinationTooSmall()
    {
        byte[] source = Encoding.UTF8.GetBytes("""hello\u0041world""");
        byte[] destination = new byte[2]; // way too small

        bool result = JsonStringUnescaper.TryUnescape(source, destination, out _);

        Assert.IsFalse(result);
    }

    [TestMethod]
    [DataRow("""multiple\u0022escapes\u003Cin\u003Eone\u0026string""", "multiple\"escapes<in>one&string")]
    public void Unescape_HandlesMultipleEscapesInOneString(string escapedUtf16, string expectedUtf16)
    {
        byte[] source = Encoding.UTF8.GetBytes(escapedUtf16);
        byte[] destination = new byte[source.Length];

        JsonStringUnescaper.Unescape(source, destination, out int written);

        string result = Encoding.UTF8.GetString(destination, 0, written);
        Assert.AreEqual(expectedUtf16, result);
    }
}
