// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public partial class Utf8IriReferenceTests
{
    [TestMethod]
    [DataRow("https://example.com/path")]
    [DataRow("http://user@example.com:8080/path?query=value#fragment")]
    [DataRow("ftp://files.example.com/public/")]
    [DataRow("mailto:user@example.com")]
    [DataRow("file:///C:/Users/Documents/file.txt")]
    [DataRow("relative/path")]
    [DataRow("../parent/path")]
    [DataRow("?query=only")]
    [DataRow("#fragment-only")]
    [DataRow("/absolute/path")]
    [DataRow("")]
    public void CreateIri_ValidIris_ReturnsUtf8IriReference(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [TestMethod]
    [DataRow("http://[invalid-ipv6")]
    [DataRow("http://example.com:99999")]
    [DataRow("ht tp://example.com")]
    [DataRow("\x01\x02\x03")]
    [DataRow("http://example.com/path with spaces")]
    public void CreateIri_InvalidIris_ThrowsArgumentException(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => Utf8IriReference.CreateIriReference(iriBytes));
        Assert.AreEqual("The value is not a valid JSON reference.", exception.Message);
    }

    [TestMethod]
    [DataRow("https://example.com/path")]
    [DataRow("http://user@example.com:8080/path?query=value#fragment")]
    [DataRow("ftp://files.example.com/public/")]
    [DataRow("mailto:user@example.com")]
    [DataRow("file:///C:/Users/Documents/file.txt")]
    [DataRow("relative/path")]
    [DataRow("../parent/path")]
    [DataRow("?query=only")]
    [DataRow("#fragment-only")]
    [DataRow("/absolute/path")]
    [DataRow("")]
    public void TryCreateIri_ValidIris_ReturnsTrueAndValidReference(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);

        Assert.IsTrue(result);
        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [TestMethod]
    [DataRow("http://[invalid-ipv6")]
    [DataRow("http://example.com:99999")]
    [DataRow("ht tp://example.com")]
    [DataRow("\x01\x02\x03")]
    [DataRow("http://example.com/path with spaces")]
    public void TryCreateIri_InvalidIris_ReturnsFalse(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);

        Assert.IsFalse(result);
        Assert.IsFalse(reference.IsValid);
    }

    [TestMethod]
    [DataRow("https://user@example.com:8080/path?query=value#fragment", "https", "user@example.com:8080", "user", "example.com", "8080", "/path", "query=value", "fragment")]
    [DataRow("http://example.com/path", "http", "example.com", "", "example.com", "", "/path", "", "")]
    [DataRow("ftp://files.example.com:21/public/", "ftp", "files.example.com:21", "", "files.example.com", "21", "/public/", "", "")]
    [DataRow("mailto:user@example.com", "mailto", "user@example.com", "user", "example.com", "", "", "", "")]
    [DataRow("file:///C:/Users/Documents/file.txt", "file", "", "", "", "", "/C:/Users/Documents/file.txt", "", "")]
    // Edge cases: absolute URIs
    [DataRow("http://a", "http", "a", "", "a", "", "", "", "")]
    [DataRow("file:///", "file", "", "", "", "", "/", "", "")]
    public void ComponentExtraction_AbsoluteIris_ExtractsCorrectly(string iri, string expectedScheme, string expectedAuthority, string expectedUser, string expectedHost, string expectedPort, string expectedPath, string expectedQuery, string expectedFragment)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expectedScheme), reference.Scheme.ToArray());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expectedAuthority), reference.Authority.ToArray());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expectedUser), reference.User.ToArray());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expectedHost), reference.Host.ToArray());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expectedPort), reference.Port.ToArray());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expectedPath), reference.Path.ToArray());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expectedQuery), reference.Query.ToArray());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expectedFragment), reference.Fragment.ToArray());
    }

    [TestMethod]
    [DataRow("relative/path", "", "", "", "", "", "relative/path", "", "")]
    [DataRow("../parent/path", "", "", "", "", "", "../parent/path", "", "")]
    [DataRow("?query=only", "", "", "", "", "", "", "query=only", "")]
    [DataRow("#fragment-only", "", "", "", "", "", "", "", "fragment-only")]
    [DataRow("/absolute/path", "", "", "", "", "", "/absolute/path", "", "")]
    [DataRow("path?query=value#fragment", "", "", "", "", "", "path", "query=value", "fragment")]
    // Edge cases: relative URIs and ambiguous forms
    [DataRow("http:a", "", "", "", "", "", "a", "", "")]
    [DataRow("http:/a", "", "", "", "", "", "/a", "", "")]
    [DataRow("http:", "", "", "", "", "", "", "", "")]
    [DataRow("c:/my/file.txt", "c", "", "", "", "", "/my/file.txt", "", "")]
    [DataRow("c:/my/file.txt#fragment", "c", "", "", "", "", "/my/file.txt", "", "fragment")]
    public void ComponentExtraction_RelativeIris_ExtractsCorrectly(string iri, string expectedScheme, string expectedAuthority, string expectedUser, string expectedHost, string expectedPort, string expectedPath, string expectedQuery, string expectedFragment)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expectedScheme), reference.Scheme.ToArray());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expectedAuthority), reference.Authority.ToArray());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expectedUser), reference.User.ToArray());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expectedHost), reference.Host.ToArray());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expectedPort), reference.Port.ToArray());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expectedPath), reference.Path.ToArray());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expectedQuery), reference.Query.ToArray());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expectedFragment), reference.Fragment.ToArray());
    }

    [TestMethod]
    [DataRow("https://user@example.com:8080/path?query=value#fragment", true, true, true, true, true, true, true)]
    [DataRow("http://example.com/path", true, true, false, true, true, false, false)]
    [DataRow("ftp://files.example.com:21/public/", true, true, false, true, true, false, false)]
    [DataRow("mailto:user@example.com", true, true, true, true, false, false, false)]
    [DataRow("file:///C:/Users/Documents/file.txt", true, false, false, false, true, false, false)]
    [DataRow("relative/path", false, false, false, false, true, false, false)]
    [DataRow("../parent/path", false, false, false, false, true, false, false)]
    [DataRow("?query=only", false, false, false, false, false, true, false)]
    [DataRow("#fragment-only", false, false, false, false, false, false, true)]
    [DataRow("/absolute/path", false, false, false, false, true, false, false)]
    [DataRow("path?query=value#fragment", false, false, false, false, true, true, true)]
    [DataRow("", false, false, false, false, false, false, false)]
    public void HasProperties_VariousIris_ReturnsCorrectValues(string iri, bool hasScheme, bool hasAuthority, bool hasUser, bool hasHost, bool hasPath, bool hasQuery, bool hasFragment)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.AreEqual(hasScheme, reference.HasScheme);
        Assert.AreEqual(hasAuthority, reference.HasAuthority);
        Assert.AreEqual(hasUser, reference.HasUser);
        Assert.AreEqual(hasHost, reference.HasHost);
        Assert.AreEqual(hasPath, reference.HasPath);
        Assert.AreEqual(hasQuery, reference.HasQuery);
        Assert.AreEqual(hasFragment, reference.HasFragment);
    }

    [TestMethod]
    [DataRow("http://example.com", 80, true)]
    [DataRow("https://example.com", 443, true)]
    [DataRow("ftp://example.com", 21, true)]
    [DataRow("http://example.com:8080", 8080, false)]
    [DataRow("https://example.com:8443", 8443, false)]
    [DataRow("ftp://example.com:2121", 2121, false)]
    [DataRow("mailto:user@example.com", 25, true)]
    [DataRow("relative/path", 0, true)]
    public void PortValue_VariousIris_ReturnsCorrectPortAndDefaultFlag(string iri, int expectedPort, bool expectedIsDefault)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.AreEqual(expectedPort, reference.PortValue);
        Assert.AreEqual(expectedIsDefault, reference.IsDefaultPort);
    }

    [TestMethod]
    [DataRow("https://example.com/path", false)]
    [DataRow("http://example.com", false)]
    [DataRow("ftp://files.example.com/public/", false)]
    [DataRow("mailto:user@example.com", false)]
    [DataRow("file:///C:/Users/Documents/file.txt", false)]
    [DataRow("relative/path", true)]
    [DataRow("../parent/path", true)]
    [DataRow("?query=only", true)]
    [DataRow("#fragment-only", true)]
    [DataRow("/absolute/path", true)]
    [DataRow("", true)]
    public void IsRelative_VariousIris_ReturnsCorrectValue(string iri, bool expectedIsRelative)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.AreEqual(expectedIsRelative, reference.IsRelative);
    }

    [TestMethod]
    [DataRow("https://example.com/path")]
    [DataRow("http://user@example.com:8080/path?query=value#fragment")]
    [DataRow("ftp://files.example.com/public/")]
    [DataRow("mailto:user@example.com")]
    [DataRow("file:///C:/Users/Documents/file.txt")]
    [DataRow("relative/path")]
    [DataRow("../parent/path")]
    [DataRow("?query=only")]
    [DataRow("#fragment-only")]
    [DataRow("/absolute/path")]
    public void GetUri_ValidIris_ReturnsEquivalentUri(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Uri systemUri = reference.GetUri();
        var expectedUri = new Uri(iri, UriKind.RelativeOrAbsolute);

        Assert.AreEqual(expectedUri.ToString(), systemUri.ToString());
        Assert.AreEqual(expectedUri.IsAbsoluteUri, systemUri.IsAbsoluteUri);
    }

    [TestMethod]
    [DataRow("http://example.com/path%20with%20spaces")]
    [DataRow("http://example.com/path%2Fwith%2Fencoded%2Fslashes")]
    [DataRow("http://example.com?query=value%26encoded")]
    [DataRow("http://example.com#fragment%23encoded")]
    [DataRow("http://example.com/path?query=value&param=encoded%20value#fragment")]
    public void CreateIri_PercentEncodedIris_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [TestMethod]
    [DataRow("http://192.168.1.1")]
    [DataRow("http://192.168.1.1:8080")]
    [DataRow("http://[2001:db8::1]")]
    [DataRow("http://[2001:db8::1]:8080")]
    [DataRow("http://localhost")]
    [DataRow("http://localhost:3000")]
    public void CreateIri_IpAddresses_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [TestMethod]
    [DataRow("urn:isbn:0451450523")]
    [DataRow("news:comp.lang.ada")]
    [DataRow("tel:+1-816-555-1212")]
    [DataRow("ldap://[2001:db8::7]/c=GB?objectClass?one")]
    public void CreateIri_NonHttpSchemes_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [TestMethod]
    public void CreateIri_EmptySpan_HandlesCorrectly()
    {
        ReadOnlySpan<byte> emptySpan = ReadOnlySpan<byte>.Empty;

        var reference = Utf8IriReference.CreateIriReference(emptySpan);

        Assert.IsTrue(reference.IsValid);
        Assert.IsTrue(reference.OriginalIriReference.IsEmpty);
        Assert.IsTrue(reference.IsRelative);
    }

    [TestMethod]
    [DataRow("http://测试.example.com")]
    [DataRow("http://example.com/测试路径")]
    [DataRow("http://example.com?查询=值")]
    [DataRow("http://example.com#片段")]
    [DataRow("http://пример.com")]
    [DataRow("http://例え.テスト")]
    public void CreateIri_UnicodeIri_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [TestMethod]
    public void CreateIri_LargeUri_HandlesCorrectly()
    {
        string largePathSegment = new('a', 1000);
        string largeUri = $"http://example.com/{largePathSegment}";
        byte[] iriBytes = Encoding.UTF8.GetBytes(largeUri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [TestMethod]
    public void CreateIri_ManyQueryParameters_HandlesCorrectly()
    {
        var iriBuilder = new StringBuilder("http://example.com/path?");
        for (int i = 0; i < 100; i++)
        {
            if (i > 0) iriBuilder.Append('&');
            iriBuilder.Append($"param{i}=value{i}");
        }

        string iri = iriBuilder.ToString();
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [TestMethod]
    [DataRow("http://example.com:1")]
    [DataRow("http://example.com:65535")]
    [DataRow("http://example.com:0")]
    public void CreateIri_BoundaryPortValues_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [TestMethod]
    [DataRow("a", true)]
    [DataRow("ab", true)]
    [DataRow("abc", true)]
    public void CreateIri_MinimalSchemes_HandlesCorrectly(string scheme, bool handlesCorrectly)
    {
        string iri = $"{scheme}:path";
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);

        Assert.AreEqual(reference.IsValid, handlesCorrectly);
        if (handlesCorrectly)
        {
            CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
        }
    }

    [TestMethod]
    [DataRow("scheme:")]
    [DataRow("scheme:/")]
    [DataRow("scheme://")]
    [DataRow("scheme:///")]
    public void ComponentBoundariesIri_MinimalComponents_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [TestMethod]
    public void ComponentAccessIri_DefaultReference_ReturnsEmptySpans()
    {
        Utf8IriReference reference = default;

        Assert.IsTrue(reference.Scheme.IsEmpty);
        Assert.IsTrue(reference.Authority.IsEmpty);
        Assert.IsTrue(reference.User.IsEmpty);
        Assert.IsTrue(reference.Host.IsEmpty);
        Assert.IsTrue(reference.Port.IsEmpty);
        Assert.IsTrue(reference.Path.IsEmpty);
        Assert.IsTrue(reference.Query.IsEmpty);
        Assert.IsTrue(reference.Fragment.IsEmpty);
        Assert.IsTrue(reference.OriginalIriReference.IsEmpty);
        Assert.AreEqual(0, reference.PortValue);
        Assert.IsFalse(reference.IsValid);
    }

    // Additional test cases to exercise more Utf8Uri functionality

    [TestMethod]
    [DataRow("http://example.com:00080", true)] // Leading zeros in port are valid (the ABNF specifies *DIGIT)
    [DataRow("http://example.com:999999", false)] // Port too large
    [DataRow("http://[invalid-bracket", false)] // Invalid IPv6 brackets
    [DataRow("http://host]/path", false)] // Invalid bracket
    [DataRow("http://host with spaces", false)] // Spaces in host
    [DataRow("http:////example.com", false)] // Too many slashes
    [DataRow("scheme:", true)] // Minimal valid absolute URI
    [DataRow("custom+scheme://example.com", true)] // Plus in scheme
    [DataRow("custom-scheme://example.com", true)] // Hyphen in scheme
    [DataRow("custom.scheme://example.com", true)] // Dot in scheme
    [DataRow("123scheme://example.com", true)] // Scheme starting with digit should be invalid, but is treated as a relative IRI path
    [DataRow("+scheme://example.com", true)] // Scheme starting with plus should be invalid, but is treated as a relative IRI path
    [DataRow("-scheme://example.com", true)] // Scheme starting with hyphen should be invalid, but is treated as a relative IRI path
    [DataRow(".scheme://example.com", true)] // Scheme starting with dot should be invalid, but is treated as a relative IRI path
    public void CreateIri_EdgeCaseIris_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8IriReference.CreateIriReference(iriBytes);
            Assert.IsTrue(reference.IsValid);
            CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
        }
        else
        {
            bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);
            Assert.IsFalse(result);
            Assert.IsFalse(reference.IsValid);
        }
    }

    [TestMethod]
    [DataRow("http://example.com/path%20space", true)] // Valid percent encoding
    [DataRow("http://example.com/path%2", false)] // Invalid percent encoding (incomplete)
    [DataRow("http://example.com/path%ZZ", false)] // Invalid percent encoding (non-hex)
    [DataRow("http://example.com/path%00", true)] // Null byte percent encoded
    [DataRow("http://example.com/path%FF", true)] // High byte percent encoded
    [DataRow("http://example.com/path%C3%A9", true)] // UTF-8 encoded character (é)
    [DataRow("http://example.com/path%E2%82%AC", true)] // UTF-8 encoded Euro symbol
    public void CreateIri_PercentEncodingEdgeCases_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8IriReference.CreateIriReference(iriBytes);
            Assert.IsTrue(reference.IsValid);
            CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
        }
        else
        {
            bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);
            Assert.IsFalse(result);
            Assert.IsFalse(reference.IsValid);
        }
    }

    [TestMethod]
    [DataRow("http://example.com/path/../other", true)] // Dot segments
    [DataRow("http://example.com/path/./current", true)] // Single dot segment
    [DataRow("http://example.com/path//double/slash", true)] // Double slashes in path
    [DataRow("http://example.com\\windows\\path", false)] // Backslashes should be invalid in HTTP
    [DataRow("file:///C:\\Windows\\Path", true)] // Backslashes in file URLs might be valid
    [DataRow("http://example.com/path?a=1&b=2&c=3", true)] // Multiple query parameters
    [DataRow("http://example.com/path?query=value#frag1#frag2", false)] // Fragment containing hash should be encoded
    [DataRow("http://example.com:80/path", true)] // Default HTTP port explicitly specified
    [DataRow("https://example.com:443/path", true)] // Default HTTPS port explicitly specified
    public void CreateIri_PathAndQueryEdgeCases_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8IriReference.CreateIriReference(iriBytes);
            Assert.IsTrue(reference.IsValid);
            CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
        }
        else
        {
            bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);
            Assert.IsFalse(result);
            Assert.IsFalse(reference.IsValid);
        }
    }

    [TestMethod]
    [DataRow("http://[::1]", true)] // IPv6 loopback
    [DataRow("http://[2001:db8::1]", true)] // IPv6 address
    [DataRow("http://[2001:db8::1]:8080", true)] // IPv6 with port
    [DataRow("http://[::ffff:192.0.2.1]", true)] // IPv4-mapped IPv6
    [DataRow("http://[invalid", false)] // Incomplete IPv6 bracket
    [DataRow("http://invalid]", false)] // Invalid closing bracket
    [DataRow("http://[::1::2]", false)] // Invalid IPv6 (too many colons)
    [DataRow("http://192.168.1.256", true)] // Valid DNS hostname per RFC 1123 (invalid as IPv4, but valid as hostname)
    [DataRow("http://192.168.1", true)] // Valid DNS hostname per RFC 1123 (invalid as IPv4, but valid as hostname)
    public void CreateIri_IpAddressEdgeCases_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8IriReference.CreateIriReference(iriBytes);
            Assert.IsTrue(reference.IsValid);
            CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
        }
        else
        {
            bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);
            Assert.IsFalse(result);
            Assert.IsFalse(reference.IsValid);
        }
    }

    [TestMethod]
    [DataRow("", true)] // Empty string
    [DataRow(" ", true)] // Just whitespace
    [DataRow("\t", false)] // Tab character
    [DataRow("\n", false)] // Newline character
    [DataRow("\r", false)] // Carriage return
    [DataRow("http://example.com ", true)] // Trailing whitespace permitted
    [DataRow(" http://example.com", true)] // Leading whitespace permitted
    [DataRow("http://example.com/\u0000", false)] // Null character
    [DataRow("http://example.com/\u001F", false)] // Control character
    [DataRow("http://example.com/\u007F", false)] // DEL character
    public void CreateIri_WhitespaceAndControlCharacters_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8IriReference.CreateIriReference(iriBytes);
            Assert.IsTrue(reference.IsValid);
            CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
        }
        else
        {
            bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);
            Assert.IsFalse(result);
            Assert.IsFalse(reference.IsValid);
        }
    }

    [TestMethod]
    [DataRow("http://user:password@example.com", "user:password", "example.com")]
    [DataRow("http://user@example.com", "user", "example.com")]
    [DataRow("http://user:@example.com", "user:", "example.com")]
    [DataRow("http://:password@example.com", ":password", "example.com")]
    [DataRow("http://@example.com", "", "example.com")]
    [DataRow("ftp://anonymous:guest@ftp.example.com", "anonymous:guest", "ftp.example.com")]
    public void ComponentExtractionIri_UserInfoVariations_ExtractsCorrectly(string iri, string expectedUserInfo, string expectedHost)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        if (string.IsNullOrEmpty(expectedUserInfo))
        {
            Assert.IsTrue(reference.User.IsEmpty);
        }
        else
        {
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expectedUserInfo), reference.User.ToArray());
        }

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expectedHost), reference.Host.ToArray());
    }

    [TestMethod]
    [DataRow("SCHEME://EXAMPLE.COM/PATH")]
    [DataRow("Http://Example.Com/Path")]
    [DataRow("HTTPS://USER@HOST.COM:443/PATH?QUERY=VALUE#FRAGMENT")]
    public void CreateIri_CaseVariations_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [TestMethod]
    [DataRow("http://example.com", new byte[] { })] // Empty path
    [DataRow("http://example.com/", new byte[] { (byte)'/' })] // Root path
    [DataRow("http://example.com/path", new byte[] { (byte)'/', (byte)'p', (byte)'a', (byte)'t', (byte)'h' })]
    [DataRow("?query", new byte[] { })] // Query-only relative URI
    [DataRow("#fragment", new byte[] { })] // Fragment-only relative URI
    public void ComponentExtractionIri_PathVariations_ExtractsCorrectly(string iri, byte[] expectedPath)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        CollectionAssert.AreEqual(expectedPath, reference.Path.ToArray());
    }

    [TestMethod]
    [DataRow("http://example.com?")]
    [DataRow("http://example.com#")]
    [DataRow("http://example.com/?")]
    [DataRow("http://example.com/#")]
    [DataRow("path?")]
    [DataRow("path#")]
    public void CreateIri_EmptyQueryAndFragment_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [TestMethod]
    public void CreateIri_VeryLongUri_HandlesCorrectly()
    {
        string baseUri = "http://example.com/";
        string longPath = new('a', 4000); // Very long path
        string iri = baseUri + longPath;
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [TestMethod]
    [DataRow("data:text/plain;base64,SGVsbG8sIFdvcmxkIQ==")]
    [DataRow("blob:http://example.com/abc-123")]
    [DataRow("about:blank")]
    [DataRow("javascript:void(0)")]
    public void CreateIri_SpecialSchemes_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [TestMethod]
    // Valid relative paths
    [DataRow("file://", true)]
    [DataRow("file:///", true)]
    // Valid UNC file URLs, but JSON Reference does not permit UNC paths.
    [DataRow("file://server/share/file.txt", false)]
    [DataRow("file://server/share/dir/", false)]
    [DataRow("file://server/share", false)]
    [DataRow("file://server/share/", false)]
    // Invalid UNC file URLs
    [DataRow("file://server", false)]
    [DataRow("file://server/", false)]
    [DataRow("file://server//share", false)]
    public void CreateIri_UncFileIris_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8IriReference.CreateIriReference(iriBytes);
            Assert.IsTrue(reference.IsValid);
            CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
        }
        else
        {
            bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);
            Assert.IsFalse(result);
            Assert.IsFalse(reference.IsValid);
        }
    }

    [TestMethod]
    // RFC 3986 Section 5.4.2 examples for http: scheme
    [DataRow("http:a", true)]
    [DataRow("http:/a", true)]
    [DataRow("http://a", true)]
    [DataRow("http:", true)]
    [DataRow("http:?query", true)]
    [DataRow("http:#frag", true)]
    [DataRow("http://?query", false)]
    [DataRow("http://#frag", false)]
    [DataRow("http://", false)]
    [DataRow("http:///a", false)]
    public void CreateIri_HttpSchemeRelativeIris_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8IriReference.CreateIriReference(iriBytes);
            Assert.IsTrue(reference.IsValid);
            CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
        }
        else
        {
            bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);
            Assert.IsFalse(result);
            Assert.IsFalse(reference.IsValid);
        }
    }

    [TestMethod]
    [DataRow("c:\\my\\file.txt", false)]
    [DataRow("c:/my/file.txt", true)]
    [DataRow("c:/my/file.txt#fragment", true)]
    [DataRow("c:\\my\\file.txt#fragment", false)]
    public void CreateIri_Implicit_File(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8IriReference.CreateIriReference(iriBytes);
            Assert.IsTrue(reference.IsValid);
            CollectionAssert.AreEqual(iriBytes, reference.OriginalIriReference.ToArray());
        }
        else
        {
            bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);
            Assert.IsFalse(result);
            Assert.IsFalse(reference.IsValid);
        }
    }

    [TestMethod]
    // Absolute URIs
    [DataRow("http://example.com", false)]
    [DataRow("https://example.com", false)]
    [DataRow("ftp://example.com", false)]
    [DataRow("mailto:user@example.com", false)]
    [DataRow("file:///C:/Users/Documents/file.txt", false)]
    // Relative URIs
    [DataRow("relative/path", true)]
    [DataRow("../parent/path", true)]
    [DataRow("?query=only", true)]
    [DataRow("#fragment-only", true)]
    [DataRow("/absolute/path", true)]
    [DataRow("", true)]
    // Edge cases
    [DataRow("http:a", true)]
    [DataRow("http:/a", true)]
    [DataRow("http://a", false)]
    [DataRow("http:?query", true)]
    [DataRow("http:#frag", true)]
    [DataRow("http:", true)]
    [DataRow("c:/my/file.txt", false)]
    [DataRow("c:/my/file.txt#fragment", false)]
    public void IsRelativeIri_Validation(string iri, bool expectedIsRelative)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8IriReference.CreateIriReference(iriBytes);
        Assert.AreEqual(expectedIsRelative, reference.IsRelative);
    }
}
