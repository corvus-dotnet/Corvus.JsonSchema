// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class Utf8UriFormatTests
{
    [TestMethod]
    [DataRow("http://example.com/path", "http://example.com/path")]
    [DataRow("HTTP://EXAMPLE.COM/PATH", "http://example.com/PATH")]
    [DataRow("http://example.com:80/path", "http://example.com/path")]
    [DataRow("http://example.com/%7Etilde", "http://example.com/~tilde")]
    [DataRow("http://example.com/%7etilde", "http://example.com/~tilde")]
    [DataRow("http://user@example.com:8080/path?query=value#fragment", "http://user@example.com:8080/path?query=value#fragment")]
    public void Utf8Uri_TryFormatDisplay_ProducesDisplayForm(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var uri = Utf8Uri.CreateUri(inputBytes);
        bool success = uri.TryFormatDisplay(buffer, out int written);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("http://example.com/path", "http://example.com/path")]
    [DataRow("HTTP://EXAMPLE.COM/PATH", "http://example.com/PATH")]
    [DataRow("http://example.com:80/path", "http://example.com/path")]
    [DataRow("http://example.com/path%2fwith%2fencoded", "http://example.com/path%2Fwith%2Fencoded")]
    [DataRow("http://example.com/%7etilde", "http://example.com/~tilde")]
    [DataRow("http://example.com/%7Etilde", "http://example.com/~tilde")]
    [DataRow("http://example.com/reserved%21%23%24", "http://example.com/reserved%21%23%24")]
    public void Utf8Uri_TryFormatCanonical_ProducesCanonicalForm(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var uri = Utf8Uri.CreateUri(inputBytes);
        bool success = uri.TryFormatCanonical(buffer, out int written);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("http://example.com/path%C3%A9", "http://example.com/path%C3%A9")]
    [DataRow("http://example.com/path%c3%a9", "http://example.com/path%C3%A9")]
    public void Utf8Uri_TryFormatCanonical_NormalizesHexToUppercase(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var uri = Utf8Uri.CreateUri(inputBytes);
        bool success = uri.TryFormatCanonical(buffer, out int written);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("relative/path", "relative/path")]
    [DataRow("../parent/path", "../parent/path")]
    [DataRow("path/%7Etilde", "path/~tilde")]
    [DataRow("path/%7etilde", "path/~tilde")]
    public void Utf8UriReference_TryFormatDisplay_ProducesDisplayForm(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var uriRef = Utf8UriReference.CreateUriReference(inputBytes);
        bool success = uriRef.TryFormatDisplay(buffer, out int written);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("relative/path", "relative/path")]
    [DataRow("path%2fwith%2fencoded", "path%2Fwith%2Fencoded")]
    [DataRow("path/%7etilde", "path/~tilde")]
    [DataRow("path/%7Etilde", "path/~tilde")]
    public void Utf8UriReference_TryFormatCanonical_ProducesCanonicalForm(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var uriRef = Utf8UriReference.CreateUriReference(inputBytes);
        bool success = uriRef.TryFormatCanonical(buffer, out int written);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("http://example.com/pathé", "http://example.com/pathé")]
    [DataRow("HTTP://EXAMPLE.COM/PATHÉ", "http://example.com/PATHÉ")]
    [DataRow("http://example.com:80/pathé", "http://example.com/pathé")]
    [DataRow("http://example.com/path%C3%A9", "http://example.com/pathé")]
    [DataRow("http://example.com/%7Etilde", "http://example.com/~tilde")]
    public void Utf8Iri_TryFormatDisplay_ProducesDisplayForm(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var iri = Utf8Iri.CreateIri(inputBytes);
        bool success = iri.TryFormatDisplay(buffer, out int written);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("http://example.com/pathé", "http://example.com/pathé")]
    [DataRow("HTTP://EXAMPLE.COM/PATHÉ", "http://example.com/PATHÉ")]
    [DataRow("http://example.com:80/pathé", "http://example.com/pathé")]
    [DataRow("http://example.com/path%C3%A9", "http://example.com/pathé")]
    [DataRow("http://example.com/path%c3%a9", "http://example.com/pathé")]
    [DataRow("http://example.com/%7Etilde", "http://example.com/~tilde")]
    [DataRow("http://example.com/%7etilde", "http://example.com/~tilde")]
    [DataRow("http://example.com/reserved%21%23%24", "http://example.com/reserved%21%23%24")]
    public void Utf8Iri_TryFormatCanonical_ProducesCanonicalFormWithIri(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var iri = Utf8Iri.CreateIri(inputBytes);
        bool success = iri.TryFormatCanonical(buffer, out int written);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("relative/pathé", "relative/pathé")]
    [DataRow("path%C3%A9", "pathé")]
    [DataRow("path/%7Etilde", "path/~tilde")]
    public void Utf8IriReference_TryFormatDisplay_ProducesDisplayForm(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var iriRef = Utf8IriReference.CreateIriReference(inputBytes);
        bool success = iriRef.TryFormatDisplay(buffer, out int written);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("relative/pathé", "relative/pathé")]
    [DataRow("path%C3%A9", "pathé")]
    [DataRow("path%c3%a9", "pathé")]
    [DataRow("path/%7etilde", "path/~tilde")]
    [DataRow("path/%7Etilde", "path/~tilde")]
    [DataRow("path%2fwith%2fencoded", "path%2Fwith%2Fencoded")]
    public void Utf8IriReference_TryFormatCanonical_ProducesCanonicalFormWithIri(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var iriRef = Utf8IriReference.CreateIriReference(inputBytes);
        bool success = iriRef.TryFormatCanonical(buffer, out int written);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Utf8Uri_TryFormatDisplay_InsufficientBuffer_ReturnsFalse()
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes("http://example.com/path");
        byte[] buffer = new byte[10]; // Too small

        var uri = Utf8Uri.CreateUri(inputBytes);
        bool success = uri.TryFormatDisplay(buffer, out _);

        Assert.IsFalse(success);
    }

    [TestMethod]
    public void Utf8Uri_TryFormatCanonical_InsufficientBuffer_ReturnsFalse()
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes("http://example.com/path");
        byte[] buffer = new byte[10]; // Too small

        var uri = Utf8Uri.CreateUri(inputBytes);
        bool success = uri.TryFormatCanonical(buffer, out _);

        Assert.IsFalse(success);
    }

    [TestMethod]
    [DataRow("http://example.com/path?query=value#fragment", "http://example.com/path?query=value#fragment")]
    [DataRow("http://example.com/path?query%3Dvalue", "http://example.com/path?query%3Dvalue")]
    [DataRow("http://example.com/path#fragment%21", "http://example.com/path#fragment%21")]
    public void Utf8Uri_TryFormatDisplay_QueryAndFragment_KeepsEncoding(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var uri = Utf8Uri.CreateUri(inputBytes);
        bool success = uri.TryFormatDisplay(buffer, out int written);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("http://example.com/path?query%3dvalue", "http://example.com/path?query%3Dvalue")]
    [DataRow("http://example.com/path#fragment%21", "http://example.com/path#fragment%21")]
    [DataRow("http://example.com/path?a%7eb", "http://example.com/path?a~b")]
    public void Utf8Uri_TryFormatCanonical_QueryAndFragment_NormalizesUnreserved(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var uri = Utf8Uri.CreateUri(inputBytes);
        bool success = uri.TryFormatCanonical(buffer, out int written);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("http://user%40:pass@example.com/", "http://user%40:pass@example.com/")]
    [DataRow("http://user%3A@example.com/", "http://user%3A@example.com/")]
    public void Utf8Uri_TryFormatDisplay_UserInfo_KeepsEncoding(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var uri = Utf8Uri.CreateUri(inputBytes);
        bool success = uri.TryFormatDisplay(buffer, out int written);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("http://user%40:pass@example.com/", "http://user%40:pass@example.com/")]
    [DataRow("http://user%3a@example.com/", "http://user%3A@example.com/")]
    [DataRow("http://user%7e@example.com/", "http://user~@example.com/")]
    public void Utf8Uri_TryFormatCanonical_UserInfo_NormalizesUnreserved(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var uri = Utf8Uri.CreateUri(inputBytes);
        bool success = uri.TryFormatCanonical(buffer, out int written);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.AreEqual(expected, result);
    }

    // Edge case tests for non-printable and special Unicode characters
    [TestMethod]
    [DataRow("http://example.com/path%00", "http://example.com/path%00")] // NULL - control char, stay encoded
    [DataRow("http://example.com/path%0A", "http://example.com/path%0A")] // LF - control char, stay encoded
    [DataRow("http://example.com/path%7F", "http://example.com/path%7F")] // DEL - control char, stay encoded
    public void Utf8Iri_TryFormatCanonical_ControlCharacters_StayEncoded(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var iri = Utf8Iri.CreateIri(inputBytes);
        bool success = iri.TryFormatCanonical(buffer, out int written);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("http://example.com/path%E2%80%8B", "http://example.com/path%E2%80%8B")] // U+200B ZERO WIDTH SPACE
    [DataRow("http://example.com/path%E2%80%8C", "http://example.com/path%E2%80%8C")] // U+200C ZERO WIDTH NON-JOINER
    [DataRow("http://example.com/path%E2%80%8D", "http://example.com/path%E2%80%8D")] // U+200D ZERO WIDTH JOINER
    [DataRow("http://example.com/path%EF%BB%BF", "http://example.com/path%EF%BB%BF")] // U+FEFF ZERO WIDTH NO-BREAK SPACE (BOM)
    public void Utf8Iri_TryFormatDisplay_ZeroWidthCharacters_StayEncoded(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var iri = Utf8Iri.CreateIri(inputBytes);
        bool success = iri.TryFormatDisplay(buffer, out int written);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("http://example.com/path%E2%80%8E", "http://example.com/path%E2%80%8E")] // U+200E LEFT-TO-RIGHT MARK
    [DataRow("http://example.com/path%E2%80%8F", "http://example.com/path%E2%80%8F")] // U+200F RIGHT-TO-LEFT MARK
    [DataRow("http://example.com/path%E2%80%AA", "http://example.com/path%E2%80%AA")] // U+202A LEFT-TO-RIGHT EMBEDDING
    public void Utf8Iri_TryFormatDisplay_BidiControlCharacters_StayEncoded(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var iri = Utf8Iri.CreateIri(inputBytes);
        bool success = iri.TryFormatDisplay(buffer, out int written);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("http://example.com/path%EF%BF%BE", "http://example.com/path%EF%BF%BE")] // U+FFFE non-character
    [DataRow("http://example.com/path%EF%BF%BF", "http://example.com/path%EF%BF%BF")] // U+FFFF non-character
    public void Utf8Iri_TryFormatDisplay_NonCharacters_StayEncoded(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var iri = Utf8Iri.CreateIri(inputBytes);
        bool success = iri.TryFormatDisplay(buffer, out int written);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("http://example.com/path%C2%A0", "http://example.com/path\u00A0")] // U+00A0 NO-BREAK SPACE - printable, allowed
    [DataRow("http://example.com/path%E4%B8%AD", "http://example.com/path\u4E2D")] // U+4E2D Chinese "middle" - printable, allowed
    public void Utf8Iri_TryFormatCanonical_PrintableNonAscii_Decoded(string input, string expected)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] buffer = new byte[1024];

        var iri = Utf8Iri.CreateIri(inputBytes);
        bool success = iri.TryFormatCanonical(buffer, out int written);

        Assert.IsTrue(success);
        string result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.AreEqual(expected, result);
    }

}
