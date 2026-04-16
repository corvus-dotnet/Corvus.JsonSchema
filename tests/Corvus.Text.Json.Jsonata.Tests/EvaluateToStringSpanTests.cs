// <copyright file="EvaluateToStringSpanTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

public class EvaluateToStringSpanTests
{
    private static readonly JsonataEvaluator Evaluator = JsonataEvaluator.Default;

    #region UTF-8 Span<byte> overload — string expression

    [Fact]
    public void Utf8_StringExpr_SimpleValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"John"}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("name", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.True(success);
        Assert.Equal("\"John\"", Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray()));
    }

    [Fact]
    public void Utf8_StringExpr_NumericResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("1 + 2", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.True(success);
        Assert.Equal("3", Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray()));
    }

    [Fact]
    public void Utf8_StringExpr_UndefinedResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("nosuchfield", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.True(success);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void Utf8_StringExpr_BufferTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"John"}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[2];
        bool success = Evaluator.EvaluateToString("name", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void Utf8_StringExpr_ExactFit()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        // "42" is 2 bytes in UTF-8
        Span<byte> buffer = stackalloc byte[2];
        bool success = Evaluator.EvaluateToString("42", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.True(success);
        Assert.Equal(2, bytesWritten);
        Assert.Equal("42", Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray()));
    }

    [Fact]
    public void Utf8_StringExpr_ObjectResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":{"b":1}}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("a", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());
        Assert.Equal("""{"b":1}""", result);
    }

    [Fact]
    public void Utf8_StringExpr_ArrayResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":[1,2,3]}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("a", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());
        Assert.Equal("[1,2,3]", result);
    }

    [Fact]
    public void Utf8_StringExpr_BooleanResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("true", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.True(success);
        Assert.Equal("true", Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray()));
    }

    [Fact]
    public void Utf8_StringExpr_NullResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":null}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("a", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.True(success);
        Assert.Equal("null", Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray()));
    }

    #endregion

    #region UTF-8 Span<byte> overload — byte[] expression

    [Fact]
    public void Utf8_ByteExpr_SimpleValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"John"}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("name"u8.ToArray(), doc.RootElement, workspace, buffer, out int bytesWritten, cacheKey: "name");

        Assert.True(success);
        Assert.Equal("\"John\"", Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray()));
    }

    [Fact]
    public void Utf8_ByteExpr_UndefinedResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("nosuchfield"u8.ToArray(), doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.True(success);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void Utf8_ByteExpr_BufferTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"John"}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[2];
        bool success = Evaluator.EvaluateToString("name"u8.ToArray(), doc.RootElement, workspace, buffer, out int bytesWritten, cacheKey: "name");

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void Utf8_ByteExpr_NoCacheKey()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("42"u8.ToArray(), doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.True(success);
        Assert.Equal("42", Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray()));
    }

    #endregion

    #region UTF-16 Span<char> overload — string expression

    [Fact]
    public void Utf16_StringExpr_SimpleValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"John"}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<char> buffer = stackalloc char[256];
        bool success = Evaluator.EvaluateToString("name", doc.RootElement, workspace, buffer, out int charsWritten);

        Assert.True(success);
        Assert.Equal("\"John\"", buffer.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Utf16_StringExpr_UndefinedResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<char> buffer = stackalloc char[256];
        bool success = Evaluator.EvaluateToString("nosuchfield", doc.RootElement, workspace, buffer, out int charsWritten);

        Assert.True(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void Utf16_StringExpr_BufferTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"John"}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<char> buffer = stackalloc char[2];
        bool success = Evaluator.EvaluateToString("name", doc.RootElement, workspace, buffer, out int charsWritten);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void Utf16_StringExpr_ExactFit()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        // "42" is 2 chars
        Span<char> buffer = stackalloc char[2];
        bool success = Evaluator.EvaluateToString("42", doc.RootElement, workspace, buffer, out int charsWritten);

        Assert.True(success);
        Assert.Equal(2, charsWritten);
        Assert.Equal("42", buffer.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Utf16_StringExpr_ObjectResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":{"b":1}}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<char> buffer = stackalloc char[256];
        bool success = Evaluator.EvaluateToString("a", doc.RootElement, workspace, buffer, out int charsWritten);

        Assert.True(success);
        Assert.Equal("""{"b":1}""", buffer.Slice(0, charsWritten).ToString());
    }

    #endregion

    #region UTF-16 Span<char> overload — byte[] expression

    [Fact]
    public void Utf16_ByteExpr_SimpleValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"John"}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<char> buffer = stackalloc char[256];
        bool success = Evaluator.EvaluateToString("name"u8.ToArray(), doc.RootElement, workspace, buffer, out int charsWritten, cacheKey: "name");

        Assert.True(success);
        Assert.Equal("\"John\"", buffer.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Utf16_ByteExpr_UndefinedResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<char> buffer = stackalloc char[256];
        bool success = Evaluator.EvaluateToString("nosuchfield"u8.ToArray(), doc.RootElement, workspace, buffer, out int charsWritten);

        Assert.True(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void Utf16_ByteExpr_BufferTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"John"}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<char> buffer = stackalloc char[2];
        bool success = Evaluator.EvaluateToString("name"u8.ToArray(), doc.RootElement, workspace, buffer, out int charsWritten, cacheKey: "name");

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    #endregion

    #region Edge cases

    [Fact]
    public void Utf8_StringExpr_StringWithEscapedChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":"hello\tworld"}"""u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = stackalloc byte[256];
        bool success = Evaluator.EvaluateToString("a", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.True(success);
        // WriteTo may normalize escape sequences
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());
        Assert.Contains("hello", result);
        Assert.Contains("world", result);
    }

    [Fact]
    public void Utf8_ZeroLengthBuffer_UndefinedStillSucceeds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<byte> buffer = Span<byte>.Empty;
        bool success = Evaluator.EvaluateToString("missing", doc.RootElement, workspace, buffer, out int bytesWritten);

        Assert.True(success);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void Utf16_ZeroLengthBuffer_UndefinedStillSucceeds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        Span<char> buffer = Span<char>.Empty;
        bool success = Evaluator.EvaluateToString("missing", doc.RootElement, workspace, buffer, out int charsWritten);

        Assert.True(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void Utf8_ExpressionError_Throws()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();

        // $sum with wrong argument type should throw.
        // Use a heap-allocated array so it can be captured by the lambda.
        byte[] buffer = new byte[256];
        Assert.ThrowsAny<JsonataException>(() =>
            Evaluator.EvaluateToString("$sum(\"notarray\")", doc.RootElement, workspace, buffer.AsSpan(), out _));
    }

    #endregion
}