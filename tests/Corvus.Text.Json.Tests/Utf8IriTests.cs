// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public partial class Utf8IriTests
{
    [TestMethod]
    [DataRow("https://example.com/path")]
    [DataRow("http://user@example.com:8080/path?query=value#fragment")]
    [DataRow("ftp://files.example.com/public/")]
    [DataRow("mailto:user@example.com")]
    [DataRow("file:///C:/Users/Documents/file.txt")]
    public void CreateIri_ValidAbsoluteIris_ReturnsUtf8Iri(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIri.ToArray());
    }

    [TestMethod]
    [DataRow("relative/path")]
    [DataRow("../parent/path")]
    [DataRow("?query=only")]
    [DataRow("#fragment-only")]
    [DataRow("/absolute/path")]
    [DataRow("")]
    public void CreateIri_RelativeIris_ThrowsArgumentException(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => Utf8Iri.CreateIri(iriBytes));
        Assert.AreEqual("The value is not a valid JSON reference.", exception.Message);
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

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => Utf8Iri.CreateIri(iriBytes));
        Assert.AreEqual("The value is not a valid JSON reference.", exception.Message);
    }

    [TestMethod]
    [DataRow("https://example.com/path")]
    [DataRow("http://user@example.com:8080/path?query=value#fragment")]
    [DataRow("ftp://files.example.com/public/")]
    [DataRow("mailto:user@example.com")]
    [DataRow("file:///C:/Users/Documents/file.txt")]
    public void TryCreateIri_ValidAbsoluteIris_ReturnsTrueAndValidIri(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        bool result = Utf8Iri.TryCreateIri(iriBytes, out Utf8Iri reference);

        Assert.IsTrue(result);
        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIri.ToArray());
    }

    [TestMethod]
    [DataRow("relative/path")]
    [DataRow("../parent/path")]
    [DataRow("?query=only")]
    [DataRow("#fragment-only")]
    [DataRow("/absolute/path")]
    [DataRow("")]
    [DataRow("http://[invalid-ipv6")]
    [DataRow("http://example.com:99999")]
    [DataRow("ht tp://example.com")]
    [DataRow("\x01\x02\x03")]
    [DataRow("http://example.com/path with spaces")]
    public void TryCreateIri_InvalidOrRelativeIris_ReturnsFalse(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        bool result = Utf8Iri.TryCreateIri(iriBytes, out Utf8Iri reference);

        Assert.IsFalse(result);
        Assert.IsFalse(reference.IsValid);
    }

    [TestMethod]
    [DataRow("https://user@example.com:8080/path?query=value#fragment", "https", "user@example.com:8080", "user", "example.com", "8080", "/path", "query=value", "fragment")]
    [DataRow("http://example.com/path", "http", "example.com", "", "example.com", "", "/path", "", "")]
    [DataRow("ftp://files.example.com:21/public/", "ftp", "files.example.com:21", "", "files.example.com", "21", "/public/", "", "")]
    [DataRow("mailto:user@example.com", "mailto", "user@example.com", "user", "example.com", "", "", "", "")]
    [DataRow("file:///C:/Users/Documents/file.txt", "file", "", "", "", "", "/C:/Users/Documents/file.txt", "", "")]
    [DataRow("http://a", "http", "a", "", "a", "", "", "", "")]
    [DataRow("file:///", "file", "", "", "", "", "/", "", "")]
    public void ComponentExtractionIri_AbsoluteIris_ExtractsCorrectly(string iri, string expectedScheme, string expectedAuthority, string expectedUser, string expectedHost, string expectedPort, string expectedPath, string expectedQuery, string expectedFragment)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8Iri.CreateIri(iriBytes);

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
    public void HasProperties_VariousIris_ReturnsCorrectValues(string iri, bool hasScheme, bool hasAuthority, bool hasUser, bool hasHost, bool hasPath, bool hasQuery, bool hasFragment)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8Iri.CreateIri(iriBytes);

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
    public void PortValue_VariousIris_ReturnsCorrectPortAndDefaultFlag(string iri, int expectedPort, bool expectedIsDefault)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.AreEqual(expectedPort, reference.PortValue);
        Assert.AreEqual(expectedIsDefault, reference.IsDefaultPort);
    }

    [TestMethod]
    [DataRow("https://example.com/path")]
    [DataRow("http://example.com")]
    [DataRow("ftp://files.example.com/public/")]
    [DataRow("mailto:user@example.com")]
    [DataRow("file:///C:/Users/Documents/file.txt")]
    public void IsRelative_AbsoluteIris_ReturnsFalse(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.IsFalse(reference.IsRelative);
    }

    [TestMethod]
    [DataRow("https://example.com/path")]
    [DataRow("http://user@example.com:8080/path?query=value#fragment")]
    [DataRow("ftp://files.example.com/public/")]
    [DataRow("mailto:user@example.com")]
    [DataRow("file:///C:/Users/Documents/file.txt")]
    public void GetUri_ValidIris_ReturnsEquivalentUri(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8Iri.CreateIri(iriBytes);

        Uri systemUri = reference.GetUri();
        var expectedUri = new Uri(iri, UriKind.Absolute);

        Assert.AreEqual(expectedUri.ToString(), systemUri.ToString());
        Assert.IsTrue(systemUri.IsAbsoluteUri);
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

        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIri.ToArray());
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

        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIri.ToArray());
    }

    [TestMethod]
    [DataRow("urn:isbn:0451450523")]
    [DataRow("news:comp.lang.ada")]
    [DataRow("tel:+1-816-555-1212")]
    [DataRow("ldap://[2001:db8::7]/c=GB?objectClass?one")]
    public void CreateIri_NonHttpSchemes_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIri.ToArray());
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

        Utf8Iri.TryCreateIri(iriBytes, out Utf8Iri reference);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIri.ToArray());
    }

    [TestMethod]
    public void CreateIri_LargeIri_HandlesCorrectly()
    {
        string largePathSegment = new('a', 1000);
        string largeIri = $"http://example.com/{largePathSegment}";
        byte[] iriBytes = Encoding.UTF8.GetBytes(largeIri);

        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIri.ToArray());
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

        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIri.ToArray());
    }

    [TestMethod]
    [DataRow("http://example.com:1")]
    [DataRow("http://example.com:65535")]
    [DataRow("http://example.com:0")]
    public void CreateIri_BoundaryPortValues_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIri.ToArray());
    }

    [TestMethod]
    [DataRow("scheme:")]
    [DataRow("scheme:/")]
    [DataRow("scheme://")]
    [DataRow("scheme:///")]
    public void ComponentBoundariesIri_MinimalComponents_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIri.ToArray());
    }

    [TestMethod]
    public void ComponentAccessIri_DefaultIri_ReturnsEmptySpans()
    {
        Utf8Iri reference = default;

        Assert.IsTrue(reference.Scheme.IsEmpty);
        Assert.IsTrue(reference.Authority.IsEmpty);
        Assert.IsTrue(reference.User.IsEmpty);
        Assert.IsTrue(reference.Host.IsEmpty);
        Assert.IsTrue(reference.Port.IsEmpty);
        Assert.IsTrue(reference.Path.IsEmpty);
        Assert.IsTrue(reference.Query.IsEmpty);
        Assert.IsTrue(reference.Fragment.IsEmpty);
        Assert.IsTrue(reference.OriginalIri.IsEmpty);
        Assert.AreEqual(0, reference.PortValue);
        Assert.IsFalse(reference.IsValid);
    }

    [TestMethod]
    [DataRow("http://example.com:00080", true)]
    [DataRow("http://example.com:999999", false)]
    [DataRow("http://[invalid-bracket", false)]
    [DataRow("http://host]/path", false)]
    [DataRow("http://host with spaces", false)]
    [DataRow("http:////example.com", false)]
    [DataRow("scheme:", true)]
    [DataRow("custom+scheme://example.com", true)]
    [DataRow("custom-scheme://example.com", true)]
    [DataRow("custom.scheme://example.com", true)]
    public void CreateIri_EdgeCaseIris_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8Iri.CreateIri(iriBytes);
            Assert.IsTrue(reference.IsValid);
            CollectionAssert.AreEqual(iriBytes, reference.OriginalIri.ToArray());
        }
        else
        {
            bool result = Utf8Iri.TryCreateIri(iriBytes, out Utf8Iri reference);
            Assert.IsFalse(result);
            Assert.IsFalse(reference.IsValid);
        }
    }

    [TestMethod]
    [DataRow("http://example.com/path%20space", true)]
    [DataRow("http://example.com/path%2", false)]
    [DataRow("http://example.com/path%ZZ", false)]
    [DataRow("http://example.com/path%00", true)]
    [DataRow("http://example.com/path%FF", true)]
    [DataRow("http://example.com/path%C3%A9", true)]
    [DataRow("http://example.com/path%E2%82%AC", true)]
    public void CreateIri_PercentEncodingEdgeCases_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8Iri.CreateIri(iriBytes);
            Assert.IsTrue(reference.IsValid);
            CollectionAssert.AreEqual(iriBytes, reference.OriginalIri.ToArray());
        }
        else
        {
            bool result = Utf8Iri.TryCreateIri(iriBytes, out Utf8Iri reference);
            Assert.IsFalse(result);
            Assert.IsFalse(reference.IsValid);
        }
    }

    [TestMethod]
    [DataRow("http://[::1]", true)]
    [DataRow("http://[2001:db8::1]", true)]
    [DataRow("http://[2001:db8::1]:8080", true)]
    [DataRow("http://[::ffff:192.0.2.1]", true)]
    [DataRow("http://[invalid", false)]
    [DataRow("http://invalid]", false)]
    [DataRow("http://[::1::2]", false)]
    [DataRow("http://192.168.1.256", true)]
    [DataRow("http://192.168.1", true)]
    public void CreateIri_IpAddressEdgeCases_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8Iri.CreateIri(iriBytes);
            Assert.IsTrue(reference.IsValid);
            CollectionAssert.AreEqual(iriBytes, reference.OriginalIri.ToArray());
        }
        else
        {
            bool result = Utf8Iri.TryCreateIri(iriBytes, out Utf8Iri reference);
            Assert.IsFalse(result);
            Assert.IsFalse(reference.IsValid);
        }
    }

    [TestMethod]
    [DataRow("SCHEME://EXAMPLE.COM/PATH")]
    [DataRow("Http://Example.Com/Path")]
    [DataRow("HTTPS://USER@HOST.COM:443/PATH?QUERY=VALUE#FRAGMENT")]
    public void CreateIri_CaseVariations_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIri.ToArray());
    }

    [TestMethod]
    [DataRow("data:text/plain;base64,SGVsbG8sIFdvcmxkIQ==")]
    [DataRow("blob:http://example.com/abc-123")]
    [DataRow("about:blank")]
    [DataRow("javascript:void(0)")]
    public void CreateIri_SpecialSchemes_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.IsTrue(reference.IsValid);
        CollectionAssert.AreEqual(iriBytes, reference.OriginalIri.ToArray());
    }

    [TestMethod]
    [DataRow("file://", true)]
    [DataRow("file:///", true)]
    [DataRow("file://server/share/file.txt", false)]
    [DataRow("file://server/share/dir/", false)]
    [DataRow("file://server/share", false)]
    [DataRow("file://server/share/", false)]
    [DataRow("file://server", false)]
    [DataRow("file://server/", false)]
    [DataRow("file://server//share", false)]
    public void CreateIri_UncFileIris_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8Iri.CreateIri(iriBytes);
            Assert.IsTrue(reference.IsValid);
            CollectionAssert.AreEqual(iriBytes, reference.OriginalIri.ToArray());
        }
        else
        {
            bool result = Utf8Iri.TryCreateIri(iriBytes, out Utf8Iri reference);
            Assert.IsFalse(result);
            Assert.IsFalse(reference.IsValid);
        }
    }

    [TestMethod]
    [DataRow("http://a", true)]
    [DataRow("http://", false)]
    [DataRow("http:///a", false)]
    public void CreateIri_HttpSchemeEdgeCases_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8Iri.CreateIri(iriBytes);
            Assert.IsTrue(reference.IsValid);
            CollectionAssert.AreEqual(iriBytes, reference.OriginalIri.ToArray());
        }
        else
        {
            bool result = Utf8Iri.TryCreateIri(iriBytes, out Utf8Iri reference);
            Assert.IsFalse(result);
            Assert.IsFalse(reference.IsValid);
        }
    }

    [TestMethod]
    [DataRow("c:/my/file.txt", true)]
    [DataRow("c:/my/file.txt#fragment", true)]
    public void CreateIri_DriveLetterSchemes_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8Iri.CreateIri(iriBytes);
            Assert.IsTrue(reference.IsValid);
            CollectionAssert.AreEqual(iriBytes, reference.OriginalIri.ToArray());
        }
        else
        {
            bool result = Utf8Iri.TryCreateIri(iriBytes, out Utf8Iri reference);
            Assert.IsFalse(result);
            Assert.IsFalse(reference.IsValid);
        }
    }
}
