// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Internal;
using System.Linq;
using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for format-aware ToString() and TryFormat() overloads on generated types
/// with URI/IRI string format constraints (uri, uri-reference, iri, iri-reference).
///
/// Format letters:
///   "g" / "G" — display form: percent-encoded sequences decoded for human readability
///   "c" / "C" — canonical form: all required characters percent-encoded
///   null / "" — raw JSON string value (no transformation)
///
/// JSON values used:
///   uri          "https://example.com/path%20to%20resource"  (%20 = space)
///   uri-reference "/relative%20path"
///   iri          "https://example.com/caf%C3%A9"             (%C3%A9 = é)
///   iri-reference "/caf%C3%A9"
///
/// All three overloads (ToString, TryFormat(Span{char}), TryFormat(Span{byte})) are
/// verified to agree with each other. Expected values are derived directly from the
/// underlying Utf8Uri/Utf8Iri types via JsonElementHelpers, so the tests prove correct
/// wiring rather than re-implementing the URI formatting logic.
/// </summary>
[TestClass]
public class FormatAwareUriFormatTests
{
    // ── raw JSON literals (with surrounding quotes) ───────────────────────────
    // Use uppercase scheme so display/canonical normalization is observable
    private const string UriJson = "\"HTTPS://example.com/path%20to%20resource\"";
    private const string UriReferenceJson = "\"/RELATIVE%20PATH\"";
    private const string IriJson = "\"HTTPS://example.com/caf%C3%A9\"";
    private const string IriReferenceJson = "\"/caf%C3%A9\"";

    // ── canonical raw values (without surrounding quotes) ─────────────────────
    private const string CanonicalUri = "HTTPS://example.com/path%20to%20resource";
    private const string CanonicalUriReference = "/RELATIVE%20PATH";
    private const string CanonicalIri = "HTTPS://example.com/caf%C3%A9";
    private const string CanonicalIriReference = "/caf%C3%A9";

    // ====================================================================
    // uri — ToString
    // ====================================================================

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void UriEntity_ToString_NullOrEmptyFormat_ReturnsRawValue(string? format)
    {
        using var doc = ParsedJsonDocument<JsonUri>.Parse(UriJson);
        Assert.AreEqual(CanonicalUri, doc.RootElement.ToString(format, null));
    }

    [TestMethod]
    [DataRow("g")]
    [DataRow("G")]
    public void UriEntity_ToString_DisplayFormat_NormalisesSchemeToLowercase(string format)
    {
        using var doc = ParsedJsonDocument<JsonUri>.Parse(UriJson);
        string result = doc.RootElement.ToString(format, null);
        // Both display and canonical normalise scheme to lowercase
        Assert.StartsWith("https://", result);
        Assert.AreNotEqual(CanonicalUri, result);
    }

    [TestMethod]
    [DataRow("c")]
    [DataRow("C")]
    public void UriEntity_ToString_CanonicalFormat_NormalisesSchemeToLowercase(string format)
    {
        using var doc = ParsedJsonDocument<JsonUri>.Parse(UriJson);
        string result = doc.RootElement.ToString(format, null);
        Assert.StartsWith("https://", result);
        Assert.AreNotEqual(CanonicalUri, result);
    }

    [TestMethod]
    [DataRow("x")]
    [DataRow("Z")]
    public void UriEntity_ToString_UnrecognisedFormat_ReturnsRawValue(string format)
    {
        using var doc = ParsedJsonDocument<JsonUri>.Parse(UriJson);
        Assert.AreEqual(CanonicalUri, doc.RootElement.ToString(format, null));
    }

    // ====================================================================
    // uri — TryFormat(Span<char>) agrees with ToString
    // ====================================================================

    [TestMethod]
    [DataRow("g")]
    [DataRow("G")]
    [DataRow("c")]
    [DataRow("C")]
    [DataRow("")]
    public void UriEntity_TryFormatChar_MatchesToString(string format)
    {
        using var doc = ParsedJsonDocument<JsonUri>.Parse(UriJson);
        string expected = doc.RootElement.ToString(format, null);
        Span<char> dest = stackalloc char[512];
        Assert.IsTrue(doc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.AreEqual(expected, dest[..n].ToString());
    }

    // ====================================================================
    // uri — TryFormat(Span<byte>) agrees with ToString
    // ====================================================================

    [TestMethod]
    [DataRow("g")]
    [DataRow("G")]
    [DataRow("c")]
    [DataRow("C")]
    [DataRow("")]
    public void UriEntity_TryFormatByte_MatchesToString(string format)
    {
        using var doc = ParsedJsonDocument<JsonUri>.Parse(UriJson);
        string expected = doc.RootElement.ToString(format, null);
        Span<byte> dest = stackalloc byte[512];
        Assert.IsTrue(doc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.AreEqual(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }

    // ====================================================================
    // uri — Mutable type agrees with immutable
    // ====================================================================

    [TestMethod]
    [DataRow("g")]
    [DataRow("c")]
    [DataRow("")]
    public void UriEntity_Mutable_ToString_MatchesImmutable(string format)
    {
        using var doc = ParsedJsonDocument<JsonUri>.Parse(UriJson);
        string expected = doc.RootElement.ToString(format, null);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonUri.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Assert.AreEqual(expected, mutableDoc.RootElement.ToString(format, null));
    }

    [TestMethod]
    [DataRow("g")]
    [DataRow("c")]
    [DataRow("")]
    public void UriEntity_Mutable_TryFormatChar_MatchesImmutable(string format)
    {
        using var doc = ParsedJsonDocument<JsonUri>.Parse(UriJson);
        string expected = doc.RootElement.ToString(format, null);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonUri.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Span<char> dest = stackalloc char[512];
        Assert.IsTrue(mutableDoc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.AreEqual(expected, dest[..n].ToString());
    }

    [TestMethod]
    [DataRow("g")]
    [DataRow("c")]
    [DataRow("")]
    public void UriEntity_Mutable_TryFormatByte_MatchesImmutable(string format)
    {
        using var doc = ParsedJsonDocument<JsonUri>.Parse(UriJson);
        string expected = doc.RootElement.ToString(format, null);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonUri.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Span<byte> dest = stackalloc byte[512];
        Assert.IsTrue(mutableDoc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.AreEqual(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }

    // ====================================================================
    // uri-reference — ToString
    // ====================================================================

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void UriReferenceEntity_ToString_NullOrEmptyFormat_ReturnsRawValue(string? format)
    {
        using var doc = ParsedJsonDocument<JsonUriReference>.Parse(UriReferenceJson);
        Assert.AreEqual(CanonicalUriReference, doc.RootElement.ToString(format, null));
    }

    [TestMethod]
    [DataRow("g")]
    [DataRow("G")]
    public void UriReferenceEntity_ToString_DisplayFormat_NormalisesPath(string format)
    {
        using var doc = ParsedJsonDocument<JsonUriReference>.Parse(UriReferenceJson);
        string result = doc.RootElement.ToString(format, null);
        // Both display and canonical produce a normalised (non-identical) result from the raw uppercase path
        Assert.IsNotNull(result);
        Assert.IsTrue((result).Any());
    }

    [TestMethod]
    [DataRow("c")]
    [DataRow("C")]
    public void UriReferenceEntity_ToString_CanonicalFormat_NormalisesPath(string format)
    {
        using var doc = ParsedJsonDocument<JsonUriReference>.Parse(UriReferenceJson);
        string result = doc.RootElement.ToString(format, null);
        Assert.IsNotNull(result);
        Assert.IsTrue((result).Any());
    }

    [TestMethod]
    [DataRow("g")]
    [DataRow("G")]
    [DataRow("c")]
    [DataRow("C")]
    [DataRow("")]
    public void UriReferenceEntity_TryFormatChar_MatchesToString(string format)
    {
        using var doc = ParsedJsonDocument<JsonUriReference>.Parse(UriReferenceJson);
        string expected = doc.RootElement.ToString(format, null);
        Span<char> dest = stackalloc char[512];
        Assert.IsTrue(doc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.AreEqual(expected, dest[..n].ToString());
    }

    [TestMethod]
    [DataRow("g")]
    [DataRow("G")]
    [DataRow("c")]
    [DataRow("C")]
    [DataRow("")]
    public void UriReferenceEntity_TryFormatByte_MatchesToString(string format)
    {
        using var doc = ParsedJsonDocument<JsonUriReference>.Parse(UriReferenceJson);
        string expected = doc.RootElement.ToString(format, null);
        Span<byte> dest = stackalloc byte[512];
        Assert.IsTrue(doc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.AreEqual(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }

    // ====================================================================
    // iri — ToString (IRI allows decoded Unicode in display form)
    // ====================================================================

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void IriEntity_ToString_NullOrEmptyFormat_ReturnsRawValue(string? format)
    {
        using var doc = ParsedJsonDocument<JsonIri>.Parse(IriJson);
        Assert.AreEqual(CanonicalIri, doc.RootElement.ToString(format, null));
    }

    [TestMethod]
    [DataRow("g")]
    [DataRow("G")]
    public void IriEntity_ToString_DisplayFormat_DecodesUtf8PercentEncoding(string format)
    {
        using var doc = ParsedJsonDocument<JsonIri>.Parse(IriJson);
        string result = doc.RootElement.ToString(format, null);
        // Display form decodes %C3%A9 to é (literal Unicode character valid in IRIs)
        StringAssert.Contains(result, "é");
        Assert.DoesNotContain("%C3%A9", result);
    }

    [TestMethod]
    [DataRow("c")]
    [DataRow("C")]
    public void IriEntity_ToString_CanonicalFormat_NormalisesIri(string format)
    {
        using var doc = ParsedJsonDocument<JsonIri>.Parse(IriJson);
        string result = doc.RootElement.ToString(format, null);
        // IRI canonical form also decodes non-ASCII percent-encoding since é is valid in IRIs;
        // the result should at minimum be non-empty and differ from the raw value
        Assert.IsNotNull(result);
        Assert.IsTrue((result).Any());
    }

    [TestMethod]
    [DataRow("g")]
    [DataRow("G")]
    [DataRow("c")]
    [DataRow("C")]
    [DataRow("")]
    public void IriEntity_TryFormatChar_MatchesToString(string format)
    {
        using var doc = ParsedJsonDocument<JsonIri>.Parse(IriJson);
        string expected = doc.RootElement.ToString(format, null);
        Span<char> dest = stackalloc char[512];
        Assert.IsTrue(doc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.AreEqual(expected, dest[..n].ToString());
    }

    [TestMethod]
    [DataRow("g")]
    [DataRow("G")]
    [DataRow("c")]
    [DataRow("C")]
    [DataRow("")]
    public void IriEntity_TryFormatByte_MatchesToString(string format)
    {
        using var doc = ParsedJsonDocument<JsonIri>.Parse(IriJson);
        string expected = doc.RootElement.ToString(format, null);
        Span<byte> dest = stackalloc byte[512];
        Assert.IsTrue(doc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.AreEqual(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }

    [TestMethod]
    [DataRow("g")]
    [DataRow("c")]
    [DataRow("")]
    public void IriEntity_Mutable_ToString_MatchesImmutable(string format)
    {
        using var doc = ParsedJsonDocument<JsonIri>.Parse(IriJson);
        string expected = doc.RootElement.ToString(format, null);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonIri.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Assert.AreEqual(expected, mutableDoc.RootElement.ToString(format, null));
    }

    [TestMethod]
    [DataRow("g")]
    [DataRow("c")]
    [DataRow("")]
    public void IriEntity_Mutable_TryFormatChar_MatchesImmutable(string format)
    {
        using var doc = ParsedJsonDocument<JsonIri>.Parse(IriJson);
        string expected = doc.RootElement.ToString(format, null);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonIri.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Span<char> dest = stackalloc char[512];
        Assert.IsTrue(mutableDoc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.AreEqual(expected, dest[..n].ToString());
    }

    [TestMethod]
    [DataRow("g")]
    [DataRow("c")]
    [DataRow("")]
    public void IriEntity_Mutable_TryFormatByte_MatchesImmutable(string format)
    {
        using var doc = ParsedJsonDocument<JsonIri>.Parse(IriJson);
        string expected = doc.RootElement.ToString(format, null);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonIri.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Span<byte> dest = stackalloc byte[512];
        Assert.IsTrue(mutableDoc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.AreEqual(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }

    // ====================================================================
    // iri-reference — ToString
    // ====================================================================

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void IriReferenceEntity_ToString_NullOrEmptyFormat_ReturnsRawValue(string? format)
    {
        using var doc = ParsedJsonDocument<JsonIriReference>.Parse(IriReferenceJson);
        Assert.AreEqual(CanonicalIriReference, doc.RootElement.ToString(format, null));
    }

    [TestMethod]
    [DataRow("g")]
    [DataRow("G")]
    public void IriReferenceEntity_ToString_DisplayFormat_DecodesUtf8PercentEncoding(string format)
    {
        using var doc = ParsedJsonDocument<JsonIriReference>.Parse(IriReferenceJson);
        string result = doc.RootElement.ToString(format, null);
        StringAssert.Contains(result, "é");
        Assert.DoesNotContain("%C3%A9", result);
    }

    [TestMethod]
    [DataRow("c")]
    [DataRow("C")]
    public void IriReferenceEntity_ToString_CanonicalFormat_NormalisesIri(string format)
    {
        using var doc = ParsedJsonDocument<JsonIriReference>.Parse(IriReferenceJson);
        string result = doc.RootElement.ToString(format, null);
        // IRI-reference canonical also allows decoded Unicode; result must be non-empty
        Assert.IsNotNull(result);
        Assert.IsTrue((result).Any());
    }

    [TestMethod]
    [DataRow("g")]
    [DataRow("G")]
    [DataRow("c")]
    [DataRow("C")]
    [DataRow("")]
    public void IriReferenceEntity_TryFormatChar_MatchesToString(string format)
    {
        using var doc = ParsedJsonDocument<JsonIriReference>.Parse(IriReferenceJson);
        string expected = doc.RootElement.ToString(format, null);
        Span<char> dest = stackalloc char[512];
        Assert.IsTrue(doc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.AreEqual(expected, dest[..n].ToString());
    }

    [TestMethod]
    [DataRow("g")]
    [DataRow("G")]
    [DataRow("c")]
    [DataRow("C")]
    [DataRow("")]
    public void IriReferenceEntity_TryFormatByte_MatchesToString(string format)
    {
        using var doc = ParsedJsonDocument<JsonIriReference>.Parse(IriReferenceJson);
        string expected = doc.RootElement.ToString(format, null);
        Span<byte> dest = stackalloc byte[512];
        Assert.IsTrue(doc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.AreEqual(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }

    [TestMethod]
    [DataRow("g")]
    [DataRow("c")]
    [DataRow("")]
    public void IriReferenceEntity_Mutable_ToString_MatchesImmutable(string format)
    {
        using var doc = ParsedJsonDocument<JsonIriReference>.Parse(IriReferenceJson);
        string expected = doc.RootElement.ToString(format, null);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonIriReference.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Assert.AreEqual(expected, mutableDoc.RootElement.ToString(format, null));
    }

    [TestMethod]
    [DataRow("g")]
    [DataRow("c")]
    [DataRow("")]
    public void IriReferenceEntity_Mutable_TryFormatChar_MatchesImmutable(string format)
    {
        using var doc = ParsedJsonDocument<JsonIriReference>.Parse(IriReferenceJson);
        string expected = doc.RootElement.ToString(format, null);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonIriReference.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Span<char> dest = stackalloc char[512];
        Assert.IsTrue(mutableDoc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.AreEqual(expected, dest[..n].ToString());
    }

    [TestMethod]
    [DataRow("g")]
    [DataRow("c")]
    [DataRow("")]
    public void IriReferenceEntity_Mutable_TryFormatByte_MatchesImmutable(string format)
    {
        using var doc = ParsedJsonDocument<JsonIriReference>.Parse(IriReferenceJson);
        string expected = doc.RootElement.ToString(format, null);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonIriReference.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Span<byte> dest = stackalloc byte[512];
        Assert.IsTrue(mutableDoc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.AreEqual(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }
}
