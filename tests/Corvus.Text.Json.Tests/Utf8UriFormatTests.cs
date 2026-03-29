// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Xunit;

namespace Corvus.Text.Json.Tests;

public class Utf8UriFormatTests
{
    [Theory]
    [InlineData("http://example.com/path", "http://example.com/path")]
    [InlineData("HTTP://EXAMPLE.COM/PATH", "http://example.com/PATH")]
    [InlineData("http://example.com:80/path", "http://example.com/path")]
    [InlineData("http://example.com/%7Etilde", "http://example.com/~tilde")]
    [InlineData("http://example.com/%7etilde", "http://example.com/~tilde")]
    [InlineData("http://user@example.com:8080/path?query=value#fragment", "http://user@example.com:8080/path?query=value#fragment")]
    public void Utf8Uri_TryFormatDisplay_ProducesDisplayForm(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var uri = Utf8Uri.CreateUri(inputBytes);
        bool success = uri.TryFormatDisplay(buffer, out int written);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("http://example.com/path", "http://example.com/path")]
    [InlineData("HTTP://EXAMPLE.COM/PATH", "http://example.com/PATH")]
    [InlineData("http://example.com:80/path", "http://example.com/path")]
    [InlineData("http://example.com/path%2fwith%2fencoded", "http://example.com/path%2Fwith%2Fencoded")]
    [InlineData("http://example.com/%7etilde", "http://example.com/~tilde")]
    [InlineData("http://example.com/%7Etilde", "http://example.com/~tilde")]
    [InlineData("http://example.com/reserved%21%23%24", "http://example.com/reserved%21%23%24")]
    public void Utf8Uri_TryFormatCanonical_ProducesCanonicalForm(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var uri = Utf8Uri.CreateUri(inputBytes);
        bool success = uri.TryFormatCanonical(buffer, out int written);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("http://example.com/path%C3%A9", "http://example.com/path%C3%A9")]
    [InlineData("http://example.com/path%c3%a9", "http://example.com/path%C3%A9")]
    public void Utf8Uri_TryFormatCanonical_NormalizesHexToUppercase(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var uri = Utf8Uri.CreateUri(inputBytes);
        bool success = uri.TryFormatCanonical(buffer, out int written);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("relative/path", "relative/path")]
    [InlineData("../parent/path", "../parent/path")]
    [InlineData("path/%7Etilde", "path/~tilde")]
    [InlineData("path/%7etilde", "path/~tilde")]
    public void Utf8UriReference_TryFormatDisplay_ProducesDisplayForm(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var uriRef = Utf8UriReference.CreateUriReference(inputBytes);
        bool success = uriRef.TryFormatDisplay(buffer, out int written);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("relative/path", "relative/path")]
    [InlineData("path%2fwith%2fencoded", "path%2Fwith%2Fencoded")]
    [InlineData("path/%7etilde", "path/~tilde")]
    [InlineData("path/%7Etilde", "path/~tilde")]
    public void Utf8UriReference_TryFormatCanonical_ProducesCanonicalForm(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var uriRef = Utf8UriReference.CreateUriReference(inputBytes);
        bool success = uriRef.TryFormatCanonical(buffer, out int written);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("http://example.com/pathé", "http://example.com/pathé")]
    [InlineData("HTTP://EXAMPLE.COM/PATHÉ", "http://example.com/PATHÉ")]
    [InlineData("http://example.com:80/pathé", "http://example.com/pathé")]
    [InlineData("http://example.com/path%C3%A9", "http://example.com/pathé")]
    [InlineData("http://example.com/%7Etilde", "http://example.com/~tilde")]
    public void Utf8Iri_TryFormatDisplay_ProducesDisplayForm(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var iri = Utf8Iri.CreateIri(inputBytes);
        bool success = iri.TryFormatDisplay(buffer, out int written);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("http://example.com/pathé", "http://example.com/pathé")]
    [InlineData("HTTP://EXAMPLE.COM/PATHÉ", "http://example.com/PATHÉ")]
    [InlineData("http://example.com:80/pathé", "http://example.com/pathé")]
    [InlineData("http://example.com/path%C3%A9", "http://example.com/pathé")]
    [InlineData("http://example.com/path%c3%a9", "http://example.com/pathé")]
    [InlineData("http://example.com/%7Etilde", "http://example.com/~tilde")]
    [InlineData("http://example.com/%7etilde", "http://example.com/~tilde")]
    [InlineData("http://example.com/reserved%21%23%24", "http://example.com/reserved%21%23%24")]
    public void Utf8Iri_TryFormatCanonical_ProducesCanonicalFormWithIri(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var iri = Utf8Iri.CreateIri(inputBytes);
        bool success = iri.TryFormatCanonical(buffer, out int written);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("relative/pathé", "relative/pathé")]
    [InlineData("path%C3%A9", "pathé")]
    [InlineData("path/%7Etilde", "path/~tilde")]
    public void Utf8IriReference_TryFormatDisplay_ProducesDisplayForm(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var iriRef = Utf8IriReference.CreateIriReference(inputBytes);
        bool success = iriRef.TryFormatDisplay(buffer, out int written);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("relative/pathé", "relative/pathé")]
    [InlineData("path%C3%A9", "pathé")]
    [InlineData("path%c3%a9", "pathé")]
    [InlineData("path/%7etilde", "path/~tilde")]
    [InlineData("path/%7Etilde", "path/~tilde")]
    [InlineData("path%2fwith%2fencoded", "path%2Fwith%2Fencoded")]
    public void Utf8IriReference_TryFormatCanonical_ProducesCanonicalFormWithIri(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var iriRef = Utf8IriReference.CreateIriReference(inputBytes);
        bool success = iriRef.TryFormatCanonical(buffer, out int written);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Utf8Uri_TryFormatDisplay_InsufficientBuffer_ReturnsFalse()
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes("http://example.com/path");
        byte[] buffer = new byte[10]; // Too small

        var uri = Utf8Uri.CreateUri(inputBytes);
        bool success = uri.TryFormatDisplay(buffer, out _);

        Assert.False(success);
    }

    [Fact]
    public void Utf8Uri_TryFormatCanonical_InsufficientBuffer_ReturnsFalse()
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes("http://example.com/path");
        byte[] buffer = new byte[10]; // Too small

        var uri = Utf8Uri.CreateUri(inputBytes);
        bool success = uri.TryFormatCanonical(buffer, out _);

        Assert.False(success);
    }

    [Theory]
    [InlineData("http://example.com/path?query=value#fragment", "http://example.com/path?query=value#fragment")]
    [InlineData("http://example.com/path?query%3Dvalue", "http://example.com/path?query%3Dvalue")]
    [InlineData("http://example.com/path#fragment%21", "http://example.com/path#fragment%21")]
    public void Utf8Uri_TryFormatDisplay_QueryAndFragment_KeepsEncoding(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var uri = Utf8Uri.CreateUri(inputBytes);
        bool success = uri.TryFormatDisplay(buffer, out int written);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("http://example.com/path?query%3dvalue", "http://example.com/path?query%3Dvalue")]
    [InlineData("http://example.com/path#fragment%21", "http://example.com/path#fragment%21")]
    [InlineData("http://example.com/path?a%7eb", "http://example.com/path?a~b")]
    public void Utf8Uri_TryFormatCanonical_QueryAndFragment_NormalizesUnreserved(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var uri = Utf8Uri.CreateUri(inputBytes);
        bool success = uri.TryFormatCanonical(buffer, out int written);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("http://user%40:pass@example.com/", "http://user%40:pass@example.com/")]
    [InlineData("http://user%3A@example.com/", "http://user%3A@example.com/")]
    public void Utf8Uri_TryFormatDisplay_UserInfo_KeepsEncoding(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var uri = Utf8Uri.CreateUri(inputBytes);
        bool success = uri.TryFormatDisplay(buffer, out int written);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("http://user%40:pass@example.com/", "http://user%40:pass@example.com/")]
    [InlineData("http://user%3a@example.com/", "http://user%3A@example.com/")]
    [InlineData("http://user%7e@example.com/", "http://user~@example.com/")]
    public void Utf8Uri_TryFormatCanonical_UserInfo_NormalizesUnreserved(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var uri = Utf8Uri.CreateUri(inputBytes);
        bool success = uri.TryFormatCanonical(buffer, out int written);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(expected, result);
    }

    // Edge case tests for non-printable and special Unicode characters
    [Theory]
    [InlineData("http://example.com/path%00", "http://example.com/path%00")] // NULL - control char, stay encoded
    [InlineData("http://example.com/path%0A", "http://example.com/path%0A")] // LF - control char, stay encoded
    [InlineData("http://example.com/path%7F", "http://example.com/path%7F")] // DEL - control char, stay encoded
    public void Utf8Iri_TryFormatCanonical_ControlCharacters_StayEncoded(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var iri = Utf8Iri.CreateIri(inputBytes);
        bool success = iri.TryFormatCanonical(buffer, out int written);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("http://example.com/path%E2%80%8B", "http://example.com/path%E2%80%8B")] // U+200B ZERO WIDTH SPACE
    [InlineData("http://example.com/path%E2%80%8C", "http://example.com/path%E2%80%8C")] // U+200C ZERO WIDTH NON-JOINER
    [InlineData("http://example.com/path%E2%80%8D", "http://example.com/path%E2%80%8D")] // U+200D ZERO WIDTH JOINER
    [InlineData("http://example.com/path%EF%BB%BF", "http://example.com/path%EF%BB%BF")] // U+FEFF ZERO WIDTH NO-BREAK SPACE (BOM)
    public void Utf8Iri_TryFormatDisplay_ZeroWidthCharacters_StayEncoded(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var iri = Utf8Iri.CreateIri(inputBytes);
        bool success = iri.TryFormatDisplay(buffer, out int written);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("http://example.com/path%E2%80%8E", "http://example.com/path%E2%80%8E")] // U+200E LEFT-TO-RIGHT MARK
    [InlineData("http://example.com/path%E2%80%8F", "http://example.com/path%E2%80%8F")] // U+200F RIGHT-TO-LEFT MARK
    [InlineData("http://example.com/path%E2%80%AA", "http://example.com/path%E2%80%AA")] // U+202A LEFT-TO-RIGHT EMBEDDING
    public void Utf8Iri_TryFormatDisplay_BidiControlCharacters_StayEncoded(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var iri = Utf8Iri.CreateIri(inputBytes);
        bool success = iri.TryFormatDisplay(buffer, out int written);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("http://example.com/path%EF%BF%BE", "http://example.com/path%EF%BF%BE")] // U+FFFE non-character
    [InlineData("http://example.com/path%EF%BF%BF", "http://example.com/path%EF%BF%BF")] // U+FFFF non-character
    public void Utf8Iri_TryFormatDisplay_NonCharacters_StayEncoded(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var iri = Utf8Iri.CreateIri(inputBytes);
        bool success = iri.TryFormatDisplay(buffer, out int written);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("http://example.com/path%C2%A0", "http://example.com/path\u00A0")] // U+00A0 NO-BREAK SPACE - printable, allowed
    [InlineData("http://example.com/path%E4%B8%AD", "http://example.com/path\u4E2D")] // U+4E2D Chinese "middle" - printable, allowed
    public void Utf8Iri_TryFormatCanonical_PrintableNonAscii_Decoded(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var iri = Utf8Iri.CreateIri(inputBytes);
        bool success = iri.TryFormatCanonical(buffer, out int written);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(expected, result);
    }

}