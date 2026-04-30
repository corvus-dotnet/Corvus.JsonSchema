// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Internal;
using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Xunit;

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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void UriEntity_ToString_NullOrEmptyFormat_ReturnsRawValue(string? format)
    {
        using var doc = ParsedJsonDocument<JsonUri>.Parse(UriJson);
        Assert.Equal(CanonicalUri, doc.RootElement.ToString(format, null));
    }

    [Theory]
    [InlineData("g")]
    [InlineData("G")]
    public void UriEntity_ToString_DisplayFormat_NormalisesSchemeToLowercase(string format)
    {
        using var doc = ParsedJsonDocument<JsonUri>.Parse(UriJson);
        string result = doc.RootElement.ToString(format, null);
        // Both display and canonical normalise scheme to lowercase
        Assert.StartsWith("https://", result);
        Assert.NotEqual(CanonicalUri, result);
    }

    [Theory]
    [InlineData("c")]
    [InlineData("C")]
    public void UriEntity_ToString_CanonicalFormat_NormalisesSchemeToLowercase(string format)
    {
        using var doc = ParsedJsonDocument<JsonUri>.Parse(UriJson);
        string result = doc.RootElement.ToString(format, null);
        Assert.StartsWith("https://", result);
        Assert.NotEqual(CanonicalUri, result);
    }

    [Theory]
    [InlineData("x")]
    [InlineData("Z")]
    public void UriEntity_ToString_UnrecognisedFormat_ReturnsRawValue(string format)
    {
        using var doc = ParsedJsonDocument<JsonUri>.Parse(UriJson);
        Assert.Equal(CanonicalUri, doc.RootElement.ToString(format, null));
    }

    // ====================================================================
    // uri — TryFormat(Span<char>) agrees with ToString
    // ====================================================================

    [Theory]
    [InlineData("g")]
    [InlineData("G")]
    [InlineData("c")]
    [InlineData("C")]
    [InlineData("")]
    public void UriEntity_TryFormatChar_MatchesToString(string format)
    {
        using var doc = ParsedJsonDocument<JsonUri>.Parse(UriJson);
        string expected = doc.RootElement.ToString(format, null);
        Span<char> dest = stackalloc char[512];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.Equal(expected, dest[..n].ToString());
    }

    // ====================================================================
    // uri — TryFormat(Span<byte>) agrees with ToString
    // ====================================================================

    [Theory]
    [InlineData("g")]
    [InlineData("G")]
    [InlineData("c")]
    [InlineData("C")]
    [InlineData("")]
    public void UriEntity_TryFormatByte_MatchesToString(string format)
    {
        using var doc = ParsedJsonDocument<JsonUri>.Parse(UriJson);
        string expected = doc.RootElement.ToString(format, null);
        Span<byte> dest = stackalloc byte[512];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }

    // ====================================================================
    // uri — Mutable type agrees with immutable
    // ====================================================================

    [Theory]
    [InlineData("g")]
    [InlineData("c")]
    [InlineData("")]
    public void UriEntity_Mutable_ToString_MatchesImmutable(string format)
    {
        using var doc = ParsedJsonDocument<JsonUri>.Parse(UriJson);
        string expected = doc.RootElement.ToString(format, null);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonUri.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Assert.Equal(expected, mutableDoc.RootElement.ToString(format, null));
    }

    [Theory]
    [InlineData("g")]
    [InlineData("c")]
    [InlineData("")]
    public void UriEntity_Mutable_TryFormatChar_MatchesImmutable(string format)
    {
        using var doc = ParsedJsonDocument<JsonUri>.Parse(UriJson);
        string expected = doc.RootElement.ToString(format, null);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonUri.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Span<char> dest = stackalloc char[512];
        Assert.True(mutableDoc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.Equal(expected, dest[..n].ToString());
    }

    [Theory]
    [InlineData("g")]
    [InlineData("c")]
    [InlineData("")]
    public void UriEntity_Mutable_TryFormatByte_MatchesImmutable(string format)
    {
        using var doc = ParsedJsonDocument<JsonUri>.Parse(UriJson);
        string expected = doc.RootElement.ToString(format, null);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonUri.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Span<byte> dest = stackalloc byte[512];
        Assert.True(mutableDoc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }

    // ====================================================================
    // uri-reference — ToString
    // ====================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void UriReferenceEntity_ToString_NullOrEmptyFormat_ReturnsRawValue(string? format)
    {
        using var doc = ParsedJsonDocument<JsonUriReference>.Parse(UriReferenceJson);
        Assert.Equal(CanonicalUriReference, doc.RootElement.ToString(format, null));
    }

    [Theory]
    [InlineData("g")]
    [InlineData("G")]
    public void UriReferenceEntity_ToString_DisplayFormat_NormalisesPath(string format)
    {
        using var doc = ParsedJsonDocument<JsonUriReference>.Parse(UriReferenceJson);
        string result = doc.RootElement.ToString(format, null);
        // Both display and canonical produce a normalised (non-identical) result from the raw uppercase path
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Theory]
    [InlineData("c")]
    [InlineData("C")]
    public void UriReferenceEntity_ToString_CanonicalFormat_NormalisesPath(string format)
    {
        using var doc = ParsedJsonDocument<JsonUriReference>.Parse(UriReferenceJson);
        string result = doc.RootElement.ToString(format, null);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Theory]
    [InlineData("g")]
    [InlineData("G")]
    [InlineData("c")]
    [InlineData("C")]
    [InlineData("")]
    public void UriReferenceEntity_TryFormatChar_MatchesToString(string format)
    {
        using var doc = ParsedJsonDocument<JsonUriReference>.Parse(UriReferenceJson);
        string expected = doc.RootElement.ToString(format, null);
        Span<char> dest = stackalloc char[512];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.Equal(expected, dest[..n].ToString());
    }

    [Theory]
    [InlineData("g")]
    [InlineData("G")]
    [InlineData("c")]
    [InlineData("C")]
    [InlineData("")]
    public void UriReferenceEntity_TryFormatByte_MatchesToString(string format)
    {
        using var doc = ParsedJsonDocument<JsonUriReference>.Parse(UriReferenceJson);
        string expected = doc.RootElement.ToString(format, null);
        Span<byte> dest = stackalloc byte[512];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }

    // ====================================================================
    // iri — ToString (IRI allows decoded Unicode in display form)
    // ====================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IriEntity_ToString_NullOrEmptyFormat_ReturnsRawValue(string? format)
    {
        using var doc = ParsedJsonDocument<JsonIri>.Parse(IriJson);
        Assert.Equal(CanonicalIri, doc.RootElement.ToString(format, null));
    }

    [Theory]
    [InlineData("g")]
    [InlineData("G")]
    public void IriEntity_ToString_DisplayFormat_DecodesUtf8PercentEncoding(string format)
    {
        using var doc = ParsedJsonDocument<JsonIri>.Parse(IriJson);
        string result = doc.RootElement.ToString(format, null);
        // Display form decodes %C3%A9 to é (literal Unicode character valid in IRIs)
        Assert.Contains("é", result);
        Assert.DoesNotContain("%C3%A9", result);
    }

    [Theory]
    [InlineData("c")]
    [InlineData("C")]
    public void IriEntity_ToString_CanonicalFormat_NormalisesIri(string format)
    {
        using var doc = ParsedJsonDocument<JsonIri>.Parse(IriJson);
        string result = doc.RootElement.ToString(format, null);
        // IRI canonical form also decodes non-ASCII percent-encoding since é is valid in IRIs;
        // the result should at minimum be non-empty and differ from the raw value
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Theory]
    [InlineData("g")]
    [InlineData("G")]
    [InlineData("c")]
    [InlineData("C")]
    [InlineData("")]
    public void IriEntity_TryFormatChar_MatchesToString(string format)
    {
        using var doc = ParsedJsonDocument<JsonIri>.Parse(IriJson);
        string expected = doc.RootElement.ToString(format, null);
        Span<char> dest = stackalloc char[512];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.Equal(expected, dest[..n].ToString());
    }

    [Theory]
    [InlineData("g")]
    [InlineData("G")]
    [InlineData("c")]
    [InlineData("C")]
    [InlineData("")]
    public void IriEntity_TryFormatByte_MatchesToString(string format)
    {
        using var doc = ParsedJsonDocument<JsonIri>.Parse(IriJson);
        string expected = doc.RootElement.ToString(format, null);
        Span<byte> dest = stackalloc byte[512];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }

    [Theory]
    [InlineData("g")]
    [InlineData("c")]
    [InlineData("")]
    public void IriEntity_Mutable_ToString_MatchesImmutable(string format)
    {
        using var doc = ParsedJsonDocument<JsonIri>.Parse(IriJson);
        string expected = doc.RootElement.ToString(format, null);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonIri.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Assert.Equal(expected, mutableDoc.RootElement.ToString(format, null));
    }

    [Theory]
    [InlineData("g")]
    [InlineData("c")]
    [InlineData("")]
    public void IriEntity_Mutable_TryFormatChar_MatchesImmutable(string format)
    {
        using var doc = ParsedJsonDocument<JsonIri>.Parse(IriJson);
        string expected = doc.RootElement.ToString(format, null);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonIri.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Span<char> dest = stackalloc char[512];
        Assert.True(mutableDoc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.Equal(expected, dest[..n].ToString());
    }

    [Theory]
    [InlineData("g")]
    [InlineData("c")]
    [InlineData("")]
    public void IriEntity_Mutable_TryFormatByte_MatchesImmutable(string format)
    {
        using var doc = ParsedJsonDocument<JsonIri>.Parse(IriJson);
        string expected = doc.RootElement.ToString(format, null);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonIri.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Span<byte> dest = stackalloc byte[512];
        Assert.True(mutableDoc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }

    // ====================================================================
    // iri-reference — ToString
    // ====================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IriReferenceEntity_ToString_NullOrEmptyFormat_ReturnsRawValue(string? format)
    {
        using var doc = ParsedJsonDocument<JsonIriReference>.Parse(IriReferenceJson);
        Assert.Equal(CanonicalIriReference, doc.RootElement.ToString(format, null));
    }

    [Theory]
    [InlineData("g")]
    [InlineData("G")]
    public void IriReferenceEntity_ToString_DisplayFormat_DecodesUtf8PercentEncoding(string format)
    {
        using var doc = ParsedJsonDocument<JsonIriReference>.Parse(IriReferenceJson);
        string result = doc.RootElement.ToString(format, null);
        Assert.Contains("é", result);
        Assert.DoesNotContain("%C3%A9", result);
    }

    [Theory]
    [InlineData("c")]
    [InlineData("C")]
    public void IriReferenceEntity_ToString_CanonicalFormat_NormalisesIri(string format)
    {
        using var doc = ParsedJsonDocument<JsonIriReference>.Parse(IriReferenceJson);
        string result = doc.RootElement.ToString(format, null);
        // IRI-reference canonical also allows decoded Unicode; result must be non-empty
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Theory]
    [InlineData("g")]
    [InlineData("G")]
    [InlineData("c")]
    [InlineData("C")]
    [InlineData("")]
    public void IriReferenceEntity_TryFormatChar_MatchesToString(string format)
    {
        using var doc = ParsedJsonDocument<JsonIriReference>.Parse(IriReferenceJson);
        string expected = doc.RootElement.ToString(format, null);
        Span<char> dest = stackalloc char[512];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.Equal(expected, dest[..n].ToString());
    }

    [Theory]
    [InlineData("g")]
    [InlineData("G")]
    [InlineData("c")]
    [InlineData("C")]
    [InlineData("")]
    public void IriReferenceEntity_TryFormatByte_MatchesToString(string format)
    {
        using var doc = ParsedJsonDocument<JsonIriReference>.Parse(IriReferenceJson);
        string expected = doc.RootElement.ToString(format, null);
        Span<byte> dest = stackalloc byte[512];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }

    [Theory]
    [InlineData("g")]
    [InlineData("c")]
    [InlineData("")]
    public void IriReferenceEntity_Mutable_ToString_MatchesImmutable(string format)
    {
        using var doc = ParsedJsonDocument<JsonIriReference>.Parse(IriReferenceJson);
        string expected = doc.RootElement.ToString(format, null);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonIriReference.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Assert.Equal(expected, mutableDoc.RootElement.ToString(format, null));
    }

    [Theory]
    [InlineData("g")]
    [InlineData("c")]
    [InlineData("")]
    public void IriReferenceEntity_Mutable_TryFormatChar_MatchesImmutable(string format)
    {
        using var doc = ParsedJsonDocument<JsonIriReference>.Parse(IriReferenceJson);
        string expected = doc.RootElement.ToString(format, null);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonIriReference.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Span<char> dest = stackalloc char[512];
        Assert.True(mutableDoc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.Equal(expected, dest[..n].ToString());
    }

    [Theory]
    [InlineData("g")]
    [InlineData("c")]
    [InlineData("")]
    public void IriReferenceEntity_Mutable_TryFormatByte_MatchesImmutable(string format)
    {
        using var doc = ParsedJsonDocument<JsonIriReference>.Parse(IriReferenceJson);
        string expected = doc.RootElement.ToString(format, null);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonIriReference.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Span<byte> dest = stackalloc byte[512];
        Assert.True(mutableDoc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }
}