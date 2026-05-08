// <copyright file="EncodeUrlSurrogateTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Tests;

using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that <c>$encodeUrl</c> and <c>$encodeUrlComponent</c> correctly throw
/// D3140 when the input string contains lone UTF-16 surrogates (encoded as WTF-8
/// in the UTF-8 fast path).
/// </summary>
/// <remarks>
/// <para>
/// These tests correspond to the upstream JSONata test suite cases
/// <c>function-encodeUrl/case002</c> and <c>function-encodeUrlComponent/case002</c>,
/// which verify that encoding a lone surrogate (<c>\uD800</c>) produces error D3140.
/// </para>
/// <para>
/// The conformance test runner cannot exercise these cases through the standard expression
/// path because the lexer's <see cref="System.Text.Encoding.UTF8"/> string materialisation
/// rejects WTF-8 byte sequences. Instead, we construct the invalid data directly and
/// verify the validation in both the runtime (RT) and code-generation (CG) paths.
/// </para>
/// </remarks>
[TestClass]
public class EncodeUrlSurrogateTests
{
    // WTF-8 encoding of lone high surrogate U+D800: ED A0 80
    private static readonly byte[] HighSurrogateWtf8 = [0xED, 0xA0, 0x80];

    // WTF-8 encoding of lone low surrogate U+DC00: ED B0 80
    private static readonly byte[] LowSurrogateWtf8 = [0xED, 0xB0, 0x80];

    // Valid codepoint U+D7FF (just below surrogate range): ED 9F BF
    private static readonly byte[] ValidNearSurrogate = [0xED, 0x9F, 0xBF];

    // Valid supplementary codepoint U+10000 (4-byte UTF-8): F0 90 80 80
    private static readonly byte[] ValidSupplementary = [0xF0, 0x90, 0x80, 0x80];

    #region RT (runtime evaluator) tests

    [TestMethod]
    public void RT_EncodeUrl_HighSurrogate_ThrowsD3140()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonataHelpers.StringFromUnescapedUtf8(HighSurrogateWtf8, workspace);

        byte[] expression = "$encodeUrl($)"u8.ToArray();
        var evaluator = new JsonataEvaluator();

        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            evaluator.Evaluate(expression, data, workspace));
        Assert.AreEqual("D3140", ex.Code);
    }

    [TestMethod]
    public void RT_EncodeUrl_LowSurrogate_ThrowsD3140()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonataHelpers.StringFromUnescapedUtf8(LowSurrogateWtf8, workspace);

        byte[] expression = "$encodeUrl($)"u8.ToArray();
        var evaluator = new JsonataEvaluator();

        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            evaluator.Evaluate(expression, data, workspace));
        Assert.AreEqual("D3140", ex.Code);
    }

    [TestMethod]
    public void RT_EncodeUrlComponent_HighSurrogate_ThrowsD3140()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonataHelpers.StringFromUnescapedUtf8(HighSurrogateWtf8, workspace);

        byte[] expression = "$encodeUrlComponent($)"u8.ToArray();
        var evaluator = new JsonataEvaluator();

        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            evaluator.Evaluate(expression, data, workspace));
        Assert.AreEqual("D3140", ex.Code);
    }

    [TestMethod]
    public void RT_EncodeUrlComponent_LowSurrogate_ThrowsD3140()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonataHelpers.StringFromUnescapedUtf8(LowSurrogateWtf8, workspace);

        byte[] expression = "$encodeUrlComponent($)"u8.ToArray();
        var evaluator = new JsonataEvaluator();

        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            evaluator.Evaluate(expression, data, workspace));
        Assert.AreEqual("D3140", ex.Code);
    }

    [TestMethod]
    public void RT_EncodeUrl_ValidNearSurrogate_DoesNotThrow()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonataHelpers.StringFromUnescapedUtf8(ValidNearSurrogate, workspace);

        byte[] expression = "$encodeUrl($)"u8.ToArray();
        var evaluator = new JsonataEvaluator();

        // U+D7FF is a valid codepoint just below the surrogate range — should encode normally
        JsonElement result = evaluator.Evaluate(expression, data, workspace);
        Assert.AreEqual(JsonValueKind.String, result.ValueKind);
    }

    [TestMethod]
    public void RT_EncodeUrl_SupplementaryCharacter_DoesNotThrow()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonataHelpers.StringFromUnescapedUtf8(ValidSupplementary, workspace);

        byte[] expression = "$encodeUrl($)"u8.ToArray();
        var evaluator = new JsonataEvaluator();

        // U+10000 is a valid supplementary codepoint (4-byte UTF-8) — no false positive
        JsonElement result = evaluator.Evaluate(expression, data, workspace);
        Assert.AreEqual(JsonValueKind.String, result.ValueKind);
    }

    #endregion

    #region CG (code generation helpers) tests

    [TestMethod]
    public void CG_EncodeUrl_HighSurrogate_ThrowsD3140()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonataHelpers.StringFromUnescapedUtf8(HighSurrogateWtf8, workspace);

        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            JsonataCodeGenHelpers.EncodeUrl(data, workspace));
        Assert.AreEqual("D3140", ex.Code);
    }

    [TestMethod]
    public void CG_EncodeUrl_LowSurrogate_ThrowsD3140()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonataHelpers.StringFromUnescapedUtf8(LowSurrogateWtf8, workspace);

        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            JsonataCodeGenHelpers.EncodeUrl(data, workspace));
        Assert.AreEqual("D3140", ex.Code);
    }

    [TestMethod]
    public void CG_EncodeUrlComponent_HighSurrogate_ThrowsD3140()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonataHelpers.StringFromUnescapedUtf8(HighSurrogateWtf8, workspace);

        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            JsonataCodeGenHelpers.EncodeUrlComponent(data, workspace));
        Assert.AreEqual("D3140", ex.Code);
    }

    [TestMethod]
    public void CG_EncodeUrlComponent_LowSurrogate_ThrowsD3140()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonataHelpers.StringFromUnescapedUtf8(LowSurrogateWtf8, workspace);

        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            JsonataCodeGenHelpers.EncodeUrlComponent(data, workspace));
        Assert.AreEqual("D3140", ex.Code);
    }

    [TestMethod]
    public void CG_EncodeUrl_ValidNearSurrogate_DoesNotThrow()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonataHelpers.StringFromUnescapedUtf8(ValidNearSurrogate, workspace);

        // U+D7FF just below surrogate range — should encode normally
        JsonElement result = JsonataCodeGenHelpers.EncodeUrl(data, workspace);
        Assert.AreEqual(JsonValueKind.String, result.ValueKind);
    }

    [TestMethod]
    public void CG_EncodeUrlComponent_ValidNearSurrogate_DoesNotThrow()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement data = JsonataHelpers.StringFromUnescapedUtf8(ValidNearSurrogate, workspace);

        // U+D7FF just below surrogate range — should encode normally
        JsonElement result = JsonataCodeGenHelpers.EncodeUrlComponent(data, workspace);
        Assert.AreEqual(JsonValueKind.String, result.ValueKind);
    }

    #endregion

    #region ContainsUtf8Surrogate unit tests

    [TestMethod]
    public void ContainsUtf8Surrogate_HighSurrogate_ReturnsTrue()
    {
        Assert.IsTrue(BuiltInFunctions.ContainsUtf8Surrogate(HighSurrogateWtf8));
    }

    [TestMethod]
    public void ContainsUtf8Surrogate_LowSurrogate_ReturnsTrue()
    {
        Assert.IsTrue(BuiltInFunctions.ContainsUtf8Surrogate(LowSurrogateWtf8));
    }

    [TestMethod]
    public void ContainsUtf8Surrogate_ValidNearSurrogate_ReturnsFalse()
    {
        // ED 9F BF = U+D7FF, just below surrogate range
        Assert.IsFalse(BuiltInFunctions.ContainsUtf8Surrogate(ValidNearSurrogate));
    }

    [TestMethod]
    public void ContainsUtf8Surrogate_SupplementaryCharacter_ReturnsFalse()
    {
        // F0 90 80 80 = U+10000, 4-byte UTF-8 — not a 3-byte surrogate pattern
        Assert.IsFalse(BuiltInFunctions.ContainsUtf8Surrogate(ValidSupplementary));
    }

    [TestMethod]
    public void ContainsUtf8Surrogate_EmptySpan_ReturnsFalse()
    {
        Assert.IsFalse(BuiltInFunctions.ContainsUtf8Surrogate(ReadOnlySpan<byte>.Empty));
    }

    [TestMethod]
    public void ContainsUtf8Surrogate_TwoBytes_ReturnsFalse()
    {
        // Too short to contain a 3-byte sequence
        Assert.IsFalse(BuiltInFunctions.ContainsUtf8Surrogate(new byte[] { 0xED, 0xA0 }));
    }

    [TestMethod]
    public void ContainsUtf8Surrogate_SurrogateMidString_ReturnsTrue()
    {
        // "hello" + U+D800 + "world"
        byte[] data = [
            (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o',
            0xED, 0xA0, 0x80,
            (byte)'w', (byte)'o', (byte)'r', (byte)'l', (byte)'d',
        ];
        Assert.IsTrue(BuiltInFunctions.ContainsUtf8Surrogate(data));
    }

    [TestMethod]
    public void ContainsUtf8Surrogate_HighSurrogateMaxValue_ReturnsTrue()
    {
        // U+DBFF = ED AF BF (highest high surrogate)
        Assert.IsTrue(BuiltInFunctions.ContainsUtf8Surrogate(new byte[] { 0xED, 0xAF, 0xBF }));
    }

    [TestMethod]
    public void ContainsUtf8Surrogate_LowSurrogateMaxValue_ReturnsTrue()
    {
        // U+DFFF = ED BF BF (highest low surrogate)
        Assert.IsTrue(BuiltInFunctions.ContainsUtf8Surrogate(new byte[] { 0xED, 0xBF, 0xBF }));
    }

    [TestMethod]
    public void ContainsUtf8Surrogate_JustAboveSurrogateRange_ReturnsFalse()
    {
        // U+E000 = EE 80 80 (first codepoint above surrogate range)
        Assert.IsFalse(BuiltInFunctions.ContainsUtf8Surrogate(new byte[] { 0xEE, 0x80, 0x80 }));
    }

    #endregion
}
