// <copyright file="JsonPointerExtensionsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600

using System.Text.Json;
using Corvus.Json;

namespace Corvus.Json.Specs.Tests.CoverageGap;

[TestClass]
public class JsonPointerExtensionsTests
{
    private static readonly string TestJson = """{"a":{"b":{"c":42}},"arr":[10,20,30],"x~y":"tilde","m/n":"slash"}""";

    private static JsonAny ParseRoot()
    {
        return JsonAny.Parse(TestJson);
    }

    [TestMethod]
    public void DecodePointer_NoEscapes()
    {
        char[] output = new char[10];
        int written = JsonPointerExtensions.DecodePointer("abc".AsSpan(), output);
        Assert.AreEqual(3, written);
        Assert.AreEqual("abc", new string(output, 0, written));
    }

    [TestMethod]
    public void DecodePointer_TildeZero()
    {
        char[] output = new char[10];
        int written = JsonPointerExtensions.DecodePointer("a~0b".AsSpan(), output);
        Assert.AreEqual(3, written);
        Assert.AreEqual("a~b", new string(output, 0, written));
    }

    [TestMethod]
    public void DecodePointer_TildeOne()
    {
        char[] output = new char[10];
        int written = JsonPointerExtensions.DecodePointer("a~1b".AsSpan(), output);
        Assert.AreEqual(3, written);
        Assert.AreEqual("a/b", new string(output, 0, written));
    }

    [TestMethod]
    public void DecodePointer_TrailingTilde_Throws()
    {
        Assert.ThrowsExactly<JsonException>(
            () =>
            {
                char[] output = new char[10];
                JsonPointerExtensions.DecodePointer("abc~".AsSpan(), output);
            });
    }

    [TestMethod]
    public void DecodePointer_InvalidEscape_Throws()
    {
        Assert.ThrowsExactly<JsonException>(
            () =>
            {
                char[] output = new char[10];
                JsonPointerExtensions.DecodePointer("a~2b".AsSpan(), output);
            });
    }

    [TestMethod]
    public void ResolvePointer_Span_ObjectProperty()
    {
        JsonAny root = ParseRoot();
        JsonAny result = root.ResolvePointer("/a/b/c".AsSpan());
        Assert.AreEqual(42, (int)result.AsNumber);
    }

    [TestMethod]
    public void ResolvePointer_Span_HashPrefix()
    {
        JsonAny root = ParseRoot();
        JsonAny result = root.ResolvePointer("#/a/b/c".AsSpan());
        Assert.AreEqual(42, (int)result.AsNumber);
    }

    [TestMethod]
    public void ResolvePointer_Span_ArrayIndex()
    {
        JsonAny root = ParseRoot();
        JsonAny result = root.ResolvePointer("/arr/1".AsSpan());
        Assert.AreEqual(20, (int)result.AsNumber);
    }

    [TestMethod]
    public void ResolvePointer_Span_ArrayIndexZero()
    {
        JsonAny root = ParseRoot();
        JsonAny result = root.ResolvePointer("/arr/0".AsSpan());
        Assert.AreEqual(10, (int)result.AsNumber);
    }

    [TestMethod]
    public void ResolvePointer_Span_TildeEscapedProperty()
    {
        JsonAny root = ParseRoot();
        JsonAny result = root.ResolvePointer("/x~0y".AsSpan());
        Assert.AreEqual("tilde", (string)result.AsString);
    }

    [TestMethod]
    public void ResolvePointer_Span_SlashEscapedProperty()
    {
        JsonAny root = ParseRoot();
        JsonAny result = root.ResolvePointer("/m~1n".AsSpan());
        Assert.AreEqual("slash", (string)result.AsString);
    }

    [TestMethod]
    public void ResolvePointer_Span_MissingProperty_Throws()
    {
        JsonAny root = ParseRoot();
        Assert.ThrowsExactly<JsonException>(
            () => root.ResolvePointer("/nonexistent".AsSpan()));
    }

    [TestMethod]
    public void TryResolvePointer_Span_MissingProperty_ReturnsFalse()
    {
        JsonAny root = ParseRoot();
        bool found = root.TryResolvePointer("/nonexistent".AsSpan(), out _);
        Assert.IsFalse(found);
    }

    [TestMethod]
    public void TryResolvePointer_Span_Found_ReturnsTrue()
    {
        JsonAny root = ParseRoot();
        bool found = root.TryResolvePointer("/a/b/c".AsSpan(), out JsonAny result);
        Assert.IsTrue(found);
        Assert.AreEqual(42, (int)result.AsNumber);
    }

    [TestMethod]
    public void ResolvePointer_Span_ArrayLeadingZero_Throws()
    {
        JsonAny root = ParseRoot();
        Assert.ThrowsExactly<JsonException>(
            () => root.ResolvePointer("/arr/01".AsSpan()));
    }

    [TestMethod]
    public void TryResolvePointer_Span_ArrayLeadingZero_ReturnsFalse()
    {
        JsonAny root = ParseRoot();
        bool found = root.TryResolvePointer("/arr/01".AsSpan(), out _);
        Assert.IsFalse(found);
    }

    [TestMethod]
    public void ResolvePointer_Span_ArrayNonInteger_Throws()
    {
        JsonAny root = ParseRoot();
        Assert.ThrowsExactly<JsonException>(
            () => root.ResolvePointer("/arr/abc".AsSpan()));
    }

    [TestMethod]
    public void TryResolvePointer_Span_ArrayNonInteger_ReturnsFalse()
    {
        JsonAny root = ParseRoot();
        bool found = root.TryResolvePointer("/arr/abc".AsSpan(), out _);
        Assert.IsFalse(found);
    }

    [TestMethod]
    public void ResolvePointer_Span_ArrayOutOfRange_Throws()
    {
        JsonAny root = ParseRoot();
        Assert.ThrowsExactly<JsonException>(
            () => root.ResolvePointer("/arr/999".AsSpan()));
    }

    [TestMethod]
    public void TryResolvePointer_Span_ArrayOutOfRange_ReturnsFalse()
    {
        JsonAny root = ParseRoot();
        bool found = root.TryResolvePointer("/arr/999".AsSpan(), out _);
        Assert.IsFalse(found);
    }

    [TestMethod]
    public void ResolvePointer_JsonPointer_ObjectProperty()
    {
        JsonAny root = ParseRoot();
        JsonPointer pointer = JsonPointer.Parse("\"/a/b\"");
        JsonAny result = root.ResolvePointer(pointer);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void TryResolvePointer_JsonPointer_Found()
    {
        JsonAny root = ParseRoot();
        JsonPointer pointer = JsonPointer.Parse("\"/a\"");
        bool found = root.TryResolvePointer(pointer, out JsonAny result);
        Assert.IsTrue(found);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void TryResolvePointer_JsonPointer_NotFound()
    {
        JsonAny root = ParseRoot();
        JsonPointer pointer = JsonPointer.Parse("\"/z\"");
        bool found = root.TryResolvePointer(pointer, out _);
        Assert.IsFalse(found);
    }
}