// <copyright file="JsonElementExtensionsCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Text.Json;
using Corvus.Json;

namespace CoverageGap;

/// <summary>
/// Tests for <see cref="JsonElementExtensions"/> targeting uncovered lines:
/// GetPropertyCount, TryGetValue non-string guard, TryGetRawText.
/// </summary>
[TestClass]
public class JsonElementExtensionsCoverageTests
{
    [TestMethod]
    public void GetPropertyCount_EmptyObject()
    {
        using var doc = JsonDocument.Parse("{}");
        int count = doc.RootElement.GetPropertyCount();
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void GetPropertyCount_SingleProperty()
    {
        using var doc = JsonDocument.Parse("""{"a":1}""");
        int count = doc.RootElement.GetPropertyCount();
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public void GetPropertyCount_MultipleProperties()
    {
        using var doc = JsonDocument.Parse("""{"a":1,"b":2,"c":3}""");
        int count = doc.RootElement.GetPropertyCount();
        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public void TryGetValue_Utf8Parser_NonString_Throws()
    {
        using var doc = JsonDocument.Parse("42");
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            doc.RootElement.TryGetValue(
                (Utf8Parser<object?, int>)(static (ReadOnlySpan<byte> span, in object? state, out int value) =>
                {
                    value = span.Length;
                    return true;
                }),
                (object?)null,
                out int _);
        });
    }

    [TestMethod]
    public void TryGetValue_Utf8Parser_NonString_WithDecode_Throws()
    {
        using var doc = JsonDocument.Parse("[1]");
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            doc.RootElement.TryGetValue(
                (Utf8Parser<object?, int>)(static (ReadOnlySpan<byte> span, in object? state, out int value) =>
                {
                    value = span.Length;
                    return true;
                }),
                (object?)null,
                false,
                out int _);
        });
    }

    [TestMethod]
    public void TryGetValue_CharParser_NonString_Throws()
    {
        using var doc = JsonDocument.Parse("true");
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            doc.RootElement.TryGetValue(
                (Parser<object?, int>)(static (ReadOnlySpan<char> span, in object? state, out int value) =>
                {
                    value = span.Length;
                    return true;
                }),
                (object?)null,
                out int _);
        });
    }

    [TestMethod]
    public void TryGetRawText_Utf8Parser_SimpleString()
    {
        using var doc = JsonDocument.Parse("\"hello\"");
        bool result = doc.RootElement.TryGetRawText(
            (Utf8Parser<object?, int>)(static (ReadOnlySpan<byte> span, in object? state, out int value) =>
            {
                value = span.Length;
                return true;
            }),
            (object?)null,
            out int length);
        Assert.IsTrue(result);
        Assert.AreEqual(5, length);
    }

    [TestMethod]
    public void TryGetRawText_CharParser_SimpleString()
    {
        using var doc = JsonDocument.Parse("\"world\"");
        bool result = doc.RootElement.TryGetRawText(
            (Parser<object?, int>)(static (ReadOnlySpan<char> span, in object? state, out int value) =>
            {
                value = span.Length;
                return true;
            }),
            (object?)null,
            out int length);
        Assert.IsTrue(result);
        Assert.AreEqual(5, length);
    }

    [TestMethod]
    public void TryGetValue_Utf8Parser_StringWithEscapes()
    {
        using var doc = JsonDocument.Parse("\"hello\\nworld\"");
        bool result = doc.RootElement.TryGetValue(
            (Utf8Parser<object?, int>)(static (ReadOnlySpan<byte> span, in object? state, out int value) =>
            {
                value = span.Length;
                return true;
            }),
            (object?)null,
            out int length);
        Assert.IsTrue(result);

        // "hello\nworld" is 11 bytes after unescaping
        Assert.AreEqual(11, length);
    }

    [TestMethod]
    public void TryGetValue_Utf8Parser_StringWithEscapes_NoDecode()
    {
        using var doc = JsonDocument.Parse("\"hello\\nworld\"");
        bool result = doc.RootElement.TryGetValue(
            (Utf8Parser<object?, int>)(static (ReadOnlySpan<byte> span, in object? state, out int value) =>
            {
                value = span.Length;
                return true;
            }),
            (object?)null,
            false,
            out int length);
        Assert.IsTrue(result);

        // Raw bytes include the backslash-n as two bytes: 12 bytes
        Assert.AreEqual(12, length);
    }

    [TestMethod]
    public void TryGetValue_Utf8Parser_StringNoEscapes()
    {
        using var doc = JsonDocument.Parse("\"simple\"");
        bool result = doc.RootElement.TryGetValue(
            (Utf8Parser<object?, int>)(static (ReadOnlySpan<byte> span, in object? state, out int value) =>
            {
                value = span.Length;
                return true;
            }),
            (object?)null,
            out int length);
        Assert.IsTrue(result);
        Assert.AreEqual(6, length);
    }
}