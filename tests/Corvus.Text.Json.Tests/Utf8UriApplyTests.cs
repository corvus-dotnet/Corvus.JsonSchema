// <copyright file="Utf8UriApplyTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for RFC 3986 URI reference resolution using TryApply methods.
/// </summary>
[TestClass]
public class Utf8UriApplyTests
{
    // Normal examples from RFC 3986 Section 5.4.1
    [TestMethod]
    [DataRow("http://a/b/c/d;p?q", "g:h", "g:h")]
    [DataRow("http://a/b/c/d;p?q", "g", "http://a/b/c/g")]
    [DataRow("http://a/b/c/d;p?q", "./g", "http://a/b/c/g")]
    [DataRow("http://a/b/c/d;p?q", "g/", "http://a/b/c/g/")]
    [DataRow("http://a/b/c/d;p?q", "/g", "http://a/g")]
    [DataRow("http://a/b/c/d;p?q", "//g", "http://g")]
    [DataRow("http://a/b/c/d;p?q", "?y", "http://a/b/c/d;p?y")]
    [DataRow("http://a/b/c/d;p?q", "g?y", "http://a/b/c/g?y")]
    [DataRow("http://a/b/c/d;p?q", "#s", "http://a/b/c/d;p?q#s")]
    [DataRow("http://a/b/c/d;p?q", "g#s", "http://a/b/c/g#s")]
    [DataRow("http://a/b/c/d;p?q", "g?y#s", "http://a/b/c/g?y#s")]
    [DataRow("http://a/b/c/d;p?q", ";x", "http://a/b/c/;x")]
    [DataRow("http://a/b/c/d;p?q", "g;x", "http://a/b/c/g;x")]
    [DataRow("http://a/b/c/d;p?q", "g;x?y#s", "http://a/b/c/g;x?y#s")]
    [DataRow("http://a/b/c/d;p?q", "", "http://a/b/c/d;p?q")]
    [DataRow("http://a/b/c/d;p?q", ".", "http://a/b/c/")]
    [DataRow("http://a/b/c/d;p?q", "./", "http://a/b/c/")]
    [DataRow("http://a/b/c/d;p?q", "..", "http://a/b/")]
    [DataRow("http://a/b/c/d;p?q", "../", "http://a/b/")]
    [DataRow("http://a/b/c/d;p?q", "../g", "http://a/b/g")]
    [DataRow("http://a/b/c/d;p?q", "../..", "http://a/")]
    [DataRow("http://a/b/c/d;p?q", "../../", "http://a/")]
    [DataRow("http://a/b/c/d;p?q", "../../g", "http://a/g")]
    public void Utf8Uri_TryApply_NormalExamples(string baseUri, string reference, string expected)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] refBytes = Encoding.UTF8.GetBytes(reference);
        byte[] buffer = new byte[2048];

        var uri = Utf8Uri.CreateUri(baseBytes);
        var refUri = Utf8UriReference.CreateUriReference(refBytes);

        bool success = uri.TryApply(refUri, buffer, out Utf8Uri result);

        Assert.IsTrue(success);
        Assert.AreEqual(expected, result.ToString());
    }

    // Abnormal examples from RFC 3986 Section 5.4.2
    [TestMethod]
    [DataRow("http://a/b/c/d;p?q", "../../../g", "http://a/g")]
    [DataRow("http://a/b/c/d;p?q", "../../../../g", "http://a/g")]
    [DataRow("http://a/b/c/d;p?q", "/./g", "http://a/g")]
    [DataRow("http://a/b/c/d;p?q", "/../g", "http://a/g")]
    [DataRow("http://a/b/c/d;p?q", "g.", "http://a/b/c/g.")]
    [DataRow("http://a/b/c/d;p?q", ".g", "http://a/b/c/.g")]
    [DataRow("http://a/b/c/d;p?q", "g..", "http://a/b/c/g..")]
    [DataRow("http://a/b/c/d;p?q", "..g", "http://a/b/c/..g")]
    [DataRow("http://a/b/c/d;p?q", "./../g", "http://a/b/g")]
    [DataRow("http://a/b/c/d;p?q", "./g/.", "http://a/b/c/g/")]
    [DataRow("http://a/b/c/d;p?q", "g/./h", "http://a/b/c/g/h")]
    [DataRow("http://a/b/c/d;p?q", "g/../h", "http://a/b/c/h")]
    [DataRow("http://a/b/c/d;p?q", "g;x=1/./y", "http://a/b/c/g;x=1/y")]
    [DataRow("http://a/b/c/d;p?q", "g;x=1/../y", "http://a/b/c/y")]
    [DataRow("http://a/b/c/d;p?q", "g?y/./x", "http://a/b/c/g?y/./x")]
    [DataRow("http://a/b/c/d;p?q", "g?y/../x", "http://a/b/c/g?y/../x")]
    [DataRow("http://a/b/c/d;p?q", "g#s/./x", "http://a/b/c/g#s/./x")]
    [DataRow("http://a/b/c/d;p?q", "g#s/../x", "http://a/b/c/g#s/../x")]
    public void Utf8Uri_TryApply_AbnormalExamples(string baseUri, string reference, string expected)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] refBytes = Encoding.UTF8.GetBytes(reference);
        byte[] buffer = new byte[2048];

        var uri = Utf8Uri.CreateUri(baseBytes);
        var refUri = Utf8UriReference.CreateUriReference(refBytes);

        bool success = uri.TryApply(refUri, buffer, out Utf8Uri result);

        Assert.IsTrue(success);
        Assert.AreEqual(expected, result.ToString());
    }

    // Test with Utf8IriReference (IRI support)
    [TestMethod]
    [DataRow("http://example.com/données/", "fichier.txt", "http://example.com/données/fichier.txt")]
    [DataRow("http://example.com/path/", "../données/fichier.txt", "http://example.com/données/fichier.txt")]
    [DataRow("http://例え.jp/パス/", "ファイル", "http://例え.jp/パス/ファイル")]
    public void Utf8Iri_TryApply_WithNonAsciiCharacters(string baseIri, string reference, string expected)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseIri);
        byte[] refBytes = Encoding.UTF8.GetBytes(reference);
        byte[] buffer = new byte[2048];

        var iri = Utf8Iri.CreateIri(baseBytes);
        var refIri = Utf8IriReference.CreateIriReference(refBytes);

        bool success = iri.TryApply(refIri, buffer, out Utf8Iri result);

        Assert.IsTrue(success);
        Assert.AreEqual(expected, result.ToString());
    }

    // Test with different URI schemes
    [TestMethod]
    [DataRow("https://example.com/path/", "file.txt", "https://example.com/path/file.txt")]
    [DataRow("ftp://ftp.example.com/dir/", "../other.txt", "ftp://ftp.example.com/other.txt")]
    [DataRow("file:///home/user/", "docs/file.txt", "file:///home/user/docs/file.txt")]
    public void Utf8Uri_TryApply_DifferentSchemes(string baseUri, string reference, string expected)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] refBytes = Encoding.UTF8.GetBytes(reference);
        byte[] buffer = new byte[2048];

        var uri = Utf8Uri.CreateUri(baseBytes);
        var refUri = Utf8UriReference.CreateUriReference(refBytes);

        bool success = uri.TryApply(refUri, buffer, out Utf8Uri result);

        Assert.IsTrue(success);
        Assert.AreEqual(expected, result.ToString());
    }

    // Test absolute reference (should ignore base)
    [TestMethod]
    public void Utf8Uri_TryApply_AbsoluteReference_IgnoresBase()
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes("http://example.com/path/");
        byte[] refBytes = Encoding.UTF8.GetBytes("https://other.com/other");
        byte[] buffer = new byte[2048];

        var uri = Utf8Uri.CreateUri(baseBytes);
        var refUri = Utf8UriReference.CreateUriReference(refBytes);

        bool success = uri.TryApply(refUri, buffer, out Utf8Uri result);

        Assert.IsTrue(success);
        Assert.AreEqual("https://other.com/other", result.ToString());
    }

    // Test with authority in reference
    [TestMethod]
    public void Utf8Uri_TryApply_ReferenceWithAuthority()
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes("http://example.com/path/file");
        byte[] refBytes = Encoding.UTF8.GetBytes("//other.com/newpath");
        byte[] buffer = new byte[2048];

        var uri = Utf8Uri.CreateUri(baseBytes);
        var refUri = Utf8UriReference.CreateUriReference(refBytes);

        bool success = uri.TryApply(refUri, buffer, out Utf8Uri result);

        Assert.IsTrue(success);
        Assert.AreEqual("http://other.com/newpath", result.ToString());
    }

    // Test query-only reference
    [TestMethod]
    public void Utf8Uri_TryApply_QueryOnlyReference()
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes("http://example.com/path/file?oldquery");
        byte[] refBytes = Encoding.UTF8.GetBytes("?newquery");
        byte[] buffer = new byte[2048];

        var uri = Utf8Uri.CreateUri(baseBytes);
        var refUri = Utf8UriReference.CreateUriReference(refBytes);

        bool success = uri.TryApply(refUri, buffer, out Utf8Uri result);

        Assert.IsTrue(success);
        Assert.AreEqual("http://example.com/path/file?newquery", result.ToString());
    }

    // Test fragment-only reference
    [TestMethod]
    public void Utf8Uri_TryApply_FragmentOnlyReference()
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes("http://example.com/path/file?query#oldfrag");
        byte[] refBytes = Encoding.UTF8.GetBytes("#newfrag");
        byte[] buffer = new byte[2048];

        var uri = Utf8Uri.CreateUri(baseBytes);
        var refUri = Utf8UriReference.CreateUriReference(refBytes);

        bool success = uri.TryApply(refUri, buffer, out Utf8Uri result);

        Assert.IsTrue(success);
        Assert.AreEqual("http://example.com/path/file?query#newfrag", result.ToString());
    }

    // Test empty reference (should return base WITHOUT fragment per RFC 3986 Section 5.2.2)
    [TestMethod]
    public void Utf8Uri_TryApply_EmptyReference_ReturnsBase()
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes("http://example.com/path/file?query#frag");
        byte[] refBytes = Encoding.UTF8.GetBytes("");
        byte[] buffer = new byte[2048];

        var uri = Utf8Uri.CreateUri(baseBytes);
        var refUri = Utf8UriReference.CreateUriReference(refBytes);

        bool success = uri.TryApply(refUri, buffer, out Utf8Uri result);

        Assert.IsTrue(success);
        // Per RFC 3986: T.fragment = R.fragment (empty reference has no fragment)
        Assert.AreEqual("http://example.com/path/file?query", result.ToString());
    }

    // Test base with no path + relative reference
    [TestMethod]
    public void Utf8Uri_TryApply_BaseNoPath_RelativeReference()
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes("http://example.com");
        byte[] refBytes = Encoding.UTF8.GetBytes("path/file");
        byte[] buffer = new byte[2048];

        var uri = Utf8Uri.CreateUri(baseBytes);
        var refUri = Utf8UriReference.CreateUriReference(refBytes);

        bool success = uri.TryApply(refUri, buffer, out Utf8Uri result);

        Assert.IsTrue(success);
        Assert.AreEqual("http://example.com/path/file", result.ToString());
    }

    // Test with percent-encoded characters  
    [TestMethod]
    [DataRow("http://example.com/path%20with%20spaces/", "file%20name.txt", "http://example.com/path%20with%20spaces/file%20name.txt")]
    [DataRow("http://example.com/path/", "../%E2%9C%93/file", "http://example.com/✓/file")]
    public void Utf8Uri_TryApply_WithPercentEncoding(string baseUri, string reference, string expected)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] refBytes = Encoding.UTF8.GetBytes(reference);
        byte[] buffer = new byte[2048];

        var uri = Utf8Uri.CreateUri(baseBytes);
        var refUri = Utf8UriReference.CreateUriReference(refBytes);

        bool success = uri.TryApply(refUri, buffer, out Utf8Uri result);

        Assert.IsTrue(success);
        Assert.AreEqual(expected, result.ToString());
    }

    // Test insufficient buffer
    [TestMethod]
    public void Utf8Uri_TryApply_InsufficientBuffer_ReturnsFalse()
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes("http://example.com/path/");
        byte[] refBytes = Encoding.UTF8.GetBytes("file.txt");
        byte[] buffer = new byte[10]; // Too small

        var uri = Utf8Uri.CreateUri(baseBytes);
        var refUri = Utf8UriReference.CreateUriReference(refBytes);
        bool success = uri.TryApply(refUri, buffer, out _);

        Assert.IsFalse(success);
    }

    // Test UriReference to UriReference application with relative base
    // Per RFC 3986 Section 5.1: "The base URI must be an absolute URI"
    // Resolution should fail when base is relative
    [TestMethod]
    [DataRow("//example.com/path/", "file.txt")]
    [DataRow("path/to/", "../other")]
    [DataRow("/absolute/path/", "file")]
    public void Utf8UriReference_TryApply_RelativeToRelative_ReturnsFalse(string baseRef, string reference)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseRef);
        byte[] refBytes = Encoding.UTF8.GetBytes(reference);
        byte[] buffer = new byte[2048];

        var uriRef = Utf8UriReference.CreateUriReference(baseBytes);
        var targetRef = Utf8UriReference.CreateUriReference(refBytes);
        bool success = uriRef.TryApply(targetRef, buffer, out _);

        // RFC 3986 requires absolute base URI - should return false for relative base
        Assert.IsFalse(success);
    }

    // Test with ports
    [TestMethod]
    [DataRow("http://example.com:8080/path/", "file.txt", "http://example.com:8080/path/file.txt")]
    [DataRow("http://example.com:8080/path/", "../other", "http://example.com:8080/other")]
    [DataRow("http://example.com:8080/", "//other.com:9090/path", "http://other.com:9090/path")]
    public void Utf8Uri_TryApply_WithPorts(string baseUri, string reference, string expected)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] refBytes = Encoding.UTF8.GetBytes(reference);
        byte[] buffer = new byte[2048];

        var uri = Utf8Uri.CreateUri(baseBytes);
        var refUri = Utf8UriReference.CreateUriReference(refBytes);

        bool success = uri.TryApply(refUri, buffer, out Utf8Uri result);

        Assert.IsTrue(success);
        Assert.AreEqual(expected, result.ToString());
    }

    // Test with user info
    [TestMethod]
    [DataRow("http://user@example.com/path/", "file.txt", "http://user@example.com/path/file.txt")]
    [DataRow("http://user:pass@example.com/path/", "../other", "http://user:pass@example.com/other")]
    public void Utf8Uri_TryApply_WithUserInfo(string baseUri, string reference, string expected)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] refBytes = Encoding.UTF8.GetBytes(reference);
        byte[] buffer = new byte[2048];

        var uri = Utf8Uri.CreateUri(baseBytes);
        var refUri = Utf8UriReference.CreateUriReference(refBytes);

        bool success = uri.TryApply(refUri, buffer, out Utf8Uri result);

        Assert.IsTrue(success);
        Assert.AreEqual(expected, result.ToString());
    }

    // Complex path normalization scenarios
    [TestMethod]
    [DataRow("http://example.com/a/b/c/", "../.././d/../e", "http://example.com/a/e")]
    [DataRow("http://example.com/a/b/c/", ".././../d/./e/..", "http://example.com/a/d/")]
    [DataRow("http://example.com/", "./a/./b/./c", "http://example.com/a/b/c")]
    public void Utf8Uri_TryApply_ComplexPathNormalization(string baseUri, string reference, string expected)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] refBytes = Encoding.UTF8.GetBytes(reference);
        byte[] buffer = new byte[2048];

        var uri = Utf8Uri.CreateUri(baseBytes);
        var refUri = Utf8UriReference.CreateUriReference(refBytes);

        bool success = uri.TryApply(refUri, buffer, out Utf8Uri result);

        Assert.IsTrue(success);
        Assert.AreEqual(expected, result.ToString());
    }

    // Path-rootless tests (RFC 3986 hier-part = scheme ":" path-rootless)
    // These URIs have scheme + path but no authority (no "//")
    // Examples: "g:h", "mailto:user@example.com", "urn:isbn:1234567890"
    [TestMethod]
    [DataRow("g:h")]
    [DataRow("mailto:user@example.com")]
    [DataRow("urn:isbn:0451450523")]
    [DataRow("tel:+1-816-555-1212")]
    [DataRow("data:text/plain;charset=UTF-8;page=21,the%20data:1234,5678")]
    public void Utf8UriReference_PathRootless_ValidUri(string uriString)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uriString);

        var uriRef = Utf8UriReference.CreateUriReference(uriBytes);

        Assert.AreEqual(uriString, uriRef.ToString());
    }

    // Test applying path-rootless URIs as references
    [TestMethod]
    [DataRow("http://a/b/c/d;p?q", "mailto:user@example.com", "mailto:user@example.com")]
    [DataRow("http://a/b/c/d;p?q", "urn:isbn:0451450523", "urn:isbn:0451450523")]
    [DataRow("http://a/b/c/d;p?q", "tel:+1-816-555-1212", "tel:+1-816-555-1212")]
    public void Utf8Uri_TryApply_PathRootlessReference(string baseUri, string reference, string expected)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] refBytes = Encoding.UTF8.GetBytes(reference);
        byte[] buffer = new byte[2048];

        var uri = Utf8Uri.CreateUri(baseBytes);
        var refUri = Utf8UriReference.CreateUriReference(refBytes);

        bool success = uri.TryApply(refUri, buffer, out Utf8Uri result);

        Assert.IsTrue(success);
        Assert.AreEqual(expected, result.ToString());
    }

    // Test using path-rootless URI as base (should work per RFC 3986)
    [TestMethod]
    [DataRow("urn:example:base", "#fragment", "urn:example:base#fragment")]
    [DataRow("urn:example:base", "?query", "urn:example:base?query")]
    public void Utf8Uri_TryApply_PathRootlessBase(string baseUri, string reference, string expected)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] refBytes = Encoding.UTF8.GetBytes(reference);
        byte[] buffer = new byte[2048];

        var uri = Utf8Uri.CreateUri(baseBytes);
        var refUri = Utf8UriReference.CreateUriReference(refBytes);

        bool success = uri.TryApply(refUri, buffer, out Utf8Uri result);

        Assert.IsTrue(success);
        Assert.AreEqual(expected, result.ToString());
    }
}
