// <copyright file="JsonReaderHelperUnescapeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using Corvus.Json;

namespace CoverageGap;

/// <summary>
/// Tests targeting uncovered unescape paths in JsonReaderHelper by going through
/// JsonAny.Parse with JSON string values containing various escape sequences.
/// Each escape character exercises a distinct branch in Unescape.
/// </summary>
[TestClass]
public class JsonReaderHelperUnescapeTests
{
    [TestMethod]
    public void Unescape_EscapedQuote()
    {
        var value = JsonAny.Parse("\"say \\\"hi\\\"\"");
        Assert.AreEqual("say \"hi\"", (string)value.AsString);
    }

    [TestMethod]
    public void Unescape_Newline()
    {
        var value = JsonAny.Parse("\"line1\\nline2\"");
        Assert.AreEqual("line1\nline2", (string)value.AsString);
    }

    [TestMethod]
    public void Unescape_CarriageReturn()
    {
        var value = JsonAny.Parse("\"a\\rb\"");
        Assert.AreEqual("a\rb", (string)value.AsString);
    }

    [TestMethod]
    public void Unescape_Tab()
    {
        var value = JsonAny.Parse("\"a\\tb\"");
        Assert.AreEqual("a\tb", (string)value.AsString);
    }

    [TestMethod]
    public void Unescape_Backslash()
    {
        var value = JsonAny.Parse("\"a\\\\b\"");
        Assert.AreEqual("a\\b", (string)value.AsString);
    }

    [TestMethod]
    public void Unescape_ForwardSlash()
    {
        var value = JsonAny.Parse("\"a\\/b\"");
        Assert.AreEqual("a/b", (string)value.AsString);
    }

    [TestMethod]
    public void Unescape_Backspace()
    {
        var value = JsonAny.Parse("\"a\\bb\"");
        Assert.AreEqual("a\bb", (string)value.AsString);
    }

    [TestMethod]
    public void Unescape_FormFeed()
    {
        var value = JsonAny.Parse("\"a\\fb\"");
        Assert.AreEqual("a\fb", (string)value.AsString);
    }

    [TestMethod]
    public void Unescape_UnicodeBMP()
    {
        var value = JsonAny.Parse("\"caf\\u00E9\"");
        Assert.AreEqual("café", (string)value.AsString);
    }

    [TestMethod]
    public void Unescape_UnicodeSurrogatePair()
    {
        var value = JsonAny.Parse("\"\\uD83D\\uDE00\"");
        Assert.AreEqual("😀", (string)value.AsString);
    }

    [TestMethod]
    public void Unescape_AllEscapesInOneString()
    {
        var value = JsonAny.Parse("\"a\\nb\\tc\\\\d\\/e\\bf\"");
        Assert.AreEqual("a\nb\tc\\d/e\bf", (string)value.AsString);
    }

    [TestMethod]
    public void Unescape_NoEscapes()
    {
        var value = JsonAny.Parse("\"simple text\"");
        Assert.AreEqual("simple text", (string)value.AsString);
    }

    [TestMethod]
    public void EqualsString_WithEscapedContent()
    {
        var value = JsonAny.Parse("\"hello\\nworld\"");
        Assert.IsTrue(value.AsString.EqualsString("hello\nworld"));
    }

    [TestMethod]
    public void EqualsString_WithSurrogatePairContent()
    {
        var value = JsonAny.Parse("\"\\uD83D\\uDE00\"");
        Assert.IsTrue(value.AsString.EqualsString("😀"));
    }

    [TestMethod]
    public void EqualsUtf8Bytes_WithEscapedContent()
    {
        var value = JsonAny.Parse("\"hello\\tworld\"");
        byte[] expected = System.Text.Encoding.UTF8.GetBytes("hello\tworld");
        Assert.IsTrue(value.AsString.EqualsUtf8Bytes(expected));
    }
}