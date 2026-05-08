// <copyright file="EvaluateToStringSpanTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Jsonata.Tests;

[TestClass]
public class EvaluateToStringSpanTests
{
    private static readonly JsonataEvaluator Evaluator = JsonataEvaluator.Default;

    #region UTF-8 Span<byte> overload — string expression

    [TestMethod]
    public void Utf8_StringExpr_SimpleValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"John"}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("name", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.IsTrue(success);
        Assert.AreEqual("\"John\"", Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray()));
    }

    [TestMethod]
    public void Utf8_StringExpr_NumericResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("1 + 2", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.IsTrue(success);
        Assert.AreEqual("3", Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray()));
    }

    [TestMethod]
    public void Utf8_StringExpr_UndefinedResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("nosuchfield", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.IsTrue(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void Utf8_StringExpr_BufferTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"John"}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[2];
        bool success = Evaluator.EvaluateToString("name", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void Utf8_StringExpr_ExactFit()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        // "42" is 2 bytes in UTF-8
        Span<byte> buffer = stackalloc byte[2];
        bool success = Evaluator.EvaluateToString("42", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.IsTrue(success);
        Assert.AreEqual(2, bytesWritten);
        Assert.AreEqual("42", Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray()));
    }

    [TestMethod]
    public void Utf8_StringExpr_ObjectResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":{"b":1}}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("a", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());
        Assert.AreEqual("""{"b":1}""", result);
    }

    [TestMethod]
    public void Utf8_StringExpr_ArrayResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":[1,2,3]}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("a", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());
        Assert.AreEqual("[1,2,3]", result);
    }

    [TestMethod]
    public void Utf8_StringExpr_BooleanResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("true", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.IsTrue(success);
        Assert.AreEqual("true", Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray()));
    }

    [TestMethod]
    public void Utf8_StringExpr_NullResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":null}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("a", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.IsTrue(success);
        Assert.AreEqual("null", Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray()));
    }

    #endregion

    #region UTF-8 Span<byte> overload — byte[] expression

    [TestMethod]
    public void Utf8_ByteExpr_SimpleValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"John"}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("name"u8.ToArray(), doc.RootElement, workspace, buffer, out int bytesWritten, cacheKey: "name");

        Assert.IsTrue(success);
        Assert.AreEqual("\"John\"", Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray()));
    }

    [TestMethod]
    public void Utf8_ByteExpr_UndefinedResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("nosuchfield"u8.ToArray(), doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.IsTrue(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void Utf8_ByteExpr_BufferTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"John"}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[2];
        bool success = Evaluator.EvaluateToString("name"u8.ToArray(), doc.RootElement, workspace, buffer, out int bytesWritten, cacheKey: "name");

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void Utf8_ByteExpr_NoCacheKey()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("42"u8.ToArray(), doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.IsTrue(success);
        Assert.AreEqual("42", Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray()));
    }

    #endregion

    #region UTF-16 Span<char> overload — string expression

    [TestMethod]
    public void Utf16_StringExpr_SimpleValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"John"}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<char> buffer = stackalloc char[256];
        bool success = Evaluator.EvaluateToString("name", doc.RootElement, workspace, buffer, out int charsWritten);

        Assert.IsTrue(success);
        Assert.AreEqual("\"John\"", buffer.Slice(0, charsWritten).ToString());
    }

    [TestMethod]
    public void Utf16_StringExpr_UndefinedResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<char> buffer = stackalloc char[256];
        bool success = Evaluator.EvaluateToString("nosuchfield", doc.RootElement, workspace, buffer, out int charsWritten);

        Assert.IsTrue(success);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    public void Utf16_StringExpr_BufferTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"John"}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<char> buffer = stackalloc char[2];
        bool success = Evaluator.EvaluateToString("name", doc.RootElement, workspace, buffer, out int charsWritten);

        Assert.IsFalse(success);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    public void Utf16_StringExpr_ExactFit()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        // "42" is 2 chars
        Span<char> buffer = stackalloc char[2];
        bool success = Evaluator.EvaluateToString("42", doc.RootElement, workspace, buffer, out int charsWritten);

        Assert.IsTrue(success);
        Assert.AreEqual(2, charsWritten);
        Assert.AreEqual("42", buffer.Slice(0, charsWritten).ToString());
    }

    [TestMethod]
    public void Utf16_StringExpr_ObjectResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":{"b":1}}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<char> buffer = stackalloc char[256];
        bool success = Evaluator.EvaluateToString("a", doc.RootElement, workspace, buffer, out int charsWritten);

        Assert.IsTrue(success);
        Assert.AreEqual("""{"b":1}""", buffer.Slice(0, charsWritten).ToString());
    }

    #endregion

    #region UTF-16 Span<char> overload — byte[] expression

    [TestMethod]
    public void Utf16_ByteExpr_SimpleValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"John"}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<char> buffer = stackalloc char[256];
        bool success = Evaluator.EvaluateToString("name"u8.ToArray(), doc.RootElement, workspace, buffer, out int charsWritten, cacheKey: "name");

        Assert.IsTrue(success);
        Assert.AreEqual("\"John\"", buffer.Slice(0, charsWritten).ToString());
    }

    [TestMethod]
    public void Utf16_ByteExpr_UndefinedResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<char> buffer = stackalloc char[256];
        bool success = Evaluator.EvaluateToString("nosuchfield"u8.ToArray(), doc.RootElement, workspace, buffer, out int charsWritten);

        Assert.IsTrue(success);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    public void Utf16_ByteExpr_BufferTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"John"}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<char> buffer = stackalloc char[2];
        bool success = Evaluator.EvaluateToString("name"u8.ToArray(), doc.RootElement, workspace, buffer, out int charsWritten, cacheKey: "name");

        Assert.IsFalse(success);
        Assert.AreEqual(0, charsWritten);
    }

    #endregion

    #region Edge cases

    [TestMethod]
    public void Utf8_StringExpr_StringWithEscapedChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":"hello\tworld"}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("a", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.IsTrue(success);
        // WriteTo may normalize escape sequences
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());
        StringAssert.Contains(result, "hello");
        StringAssert.Contains(result, "world");
    }

    [TestMethod]
    public void Utf8_ZeroLengthBuffer_UndefinedStillSucceeds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = Span<byte>.Empty;
        bool success = Evaluator.EvaluateToString("missing", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.IsTrue(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void Utf16_ZeroLengthBuffer_UndefinedStillSucceeds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<char> buffer = Span<char>.Empty;
        bool success = Evaluator.EvaluateToString("missing", doc.RootElement, workspace, buffer, out int charsWritten);

        Assert.IsTrue(success);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    public void Utf8_ExpressionError_Throws()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        // $sum with wrong argument type should throw.
        // Use a heap-allocated array so it can be captured by the lambda.
        byte[] buffer = new byte[256];
        Assert.ThrowsExactly<JsonataException>(() =>
            Evaluator.EvaluateToString("$sum(\"notarray\")", doc.RootElement, workspace, buffer.AsSpan(), out _));
    }

    #endregion
}