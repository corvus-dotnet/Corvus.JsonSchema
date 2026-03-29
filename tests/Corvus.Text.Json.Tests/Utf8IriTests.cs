// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Xunit;

namespace Corvus.Text.Json.Tests;

public static partial class Utf8IriTests
{
    [Theory]
    [InlineData("https://example.com/path")]
    [InlineData("http://user@example.com:8080/path?query=value#fragment")]
    [InlineData("ftp://files.example.com/public/")]
    [InlineData("mailto:user@example.com")]
    [InlineData("file:///C:/Users/Documents/file.txt")]
    public static void CreateIri_ValidAbsoluteIris_ReturnsUtf8Iri(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIri.ToArray());
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("../parent/path")]
    [InlineData("?query=only")]
    [InlineData("#fragment-only")]
    [InlineData("/absolute/path")]
    [InlineData("")]
    public static void CreateIri_RelativeIris_ThrowsArgumentException(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => Utf8Iri.CreateIri(iriBytes));
        Assert.Equal("The value is not a valid JSON reference.", exception.Message);
    }

    [Theory]
    [InlineData("http://[invalid-ipv6")]
    [InlineData("http://example.com:99999")]
    [InlineData("ht tp://example.com")]
    [InlineData("\x01\x02\x03")]
    [InlineData("http://example.com/path with spaces")]
    public static void CreateIri_InvalidIris_ThrowsArgumentException(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => Utf8Iri.CreateIri(iriBytes));
        Assert.Equal("The value is not a valid JSON reference.", exception.Message);
    }

    [Theory]
    [InlineData("https://example.com/path")]
    [InlineData("http://user@example.com:8080/path?query=value#fragment")]
    [InlineData("ftp://files.example.com/public/")]
    [InlineData("mailto:user@example.com")]
    [InlineData("file:///C:/Users/Documents/file.txt")]
    public static void TryCreateIri_ValidAbsoluteIris_ReturnsTrueAndValidIri(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        bool result = Utf8Iri.TryCreateIri(iriBytes, out Utf8Iri reference);

        Assert.True(result);
        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIri.ToArray());
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("../parent/path")]
    [InlineData("?query=only")]
    [InlineData("#fragment-only")]
    [InlineData("/absolute/path")]
    [InlineData("")]
    [InlineData("http://[invalid-ipv6")]
    [InlineData("http://example.com:99999")]
    [InlineData("ht tp://example.com")]
    [InlineData("\x01\x02\x03")]
    [InlineData("http://example.com/path with spaces")]
    public static void TryCreateIri_InvalidOrRelativeIris_ReturnsFalse(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        bool result = Utf8Iri.TryCreateIri(iriBytes, out Utf8Iri reference);

        Assert.False(result);
        Assert.False(reference.IsValid);
    }

    [Theory]
    [InlineData("https://user@example.com:8080/path?query=value#fragment", "https", "user@example.com:8080", "user", "example.com", "8080", "/path", "query=value", "fragment")]
    [InlineData("http://example.com/path", "http", "example.com", "", "example.com", "", "/path", "", "")]
    [InlineData("ftp://files.example.com:21/public/", "ftp", "files.example.com:21", "", "files.example.com", "21", "/public/", "", "")]
    [InlineData("mailto:user@example.com", "mailto", "user@example.com", "user", "example.com", "", "", "", "")]
    [InlineData("file:///C:/Users/Documents/file.txt", "file", "", "", "", "", "/C:/Users/Documents/file.txt", "", "")]
    [InlineData("http://a", "http", "a", "", "a", "", "", "", "")]
    [InlineData("file:///", "file", "", "", "", "", "/", "", "")]
    public static void ComponentExtractionIri_AbsoluteIris_ExtractsCorrectly(string iri, string expectedScheme, string expectedAuthority, string expectedUser, string expectedHost, string expectedPort, string expectedPath, string expectedQuery, string expectedFragment)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.Equal(Encoding.UTF8.GetBytes(expectedScheme), reference.Scheme.ToArray());
        Assert.Equal(Encoding.UTF8.GetBytes(expectedAuthority), reference.Authority.ToArray());
        Assert.Equal(Encoding.UTF8.GetBytes(expectedUser), reference.User.ToArray());
        Assert.Equal(Encoding.UTF8.GetBytes(expectedHost), reference.Host.ToArray());
        Assert.Equal(Encoding.UTF8.GetBytes(expectedPort), reference.Port.ToArray());
        Assert.Equal(Encoding.UTF8.GetBytes(expectedPath), reference.Path.ToArray());
        Assert.Equal(Encoding.UTF8.GetBytes(expectedQuery), reference.Query.ToArray());
        Assert.Equal(Encoding.UTF8.GetBytes(expectedFragment), reference.Fragment.ToArray());
    }

    [Theory]
    [InlineData("https://user@example.com:8080/path?query=value#fragment", true, true, true, true, true, true, true)]
    [InlineData("http://example.com/path", true, true, false, true, true, false, false)]
    [InlineData("ftp://files.example.com:21/public/", true, true, false, true, true, false, false)]
    [InlineData("mailto:user@example.com", true, true, true, true, false, false, false)]
    [InlineData("file:///C:/Users/Documents/file.txt", true, false, false, false, true, false, false)]
    public static void HasProperties_VariousIris_ReturnsCorrectValues(string iri, bool hasScheme, bool hasAuthority, bool hasUser, bool hasHost, bool hasPath, bool hasQuery, bool hasFragment)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.Equal(hasScheme, reference.HasScheme);
        Assert.Equal(hasAuthority, reference.HasAuthority);
        Assert.Equal(hasUser, reference.HasUser);
        Assert.Equal(hasHost, reference.HasHost);
        Assert.Equal(hasPath, reference.HasPath);
        Assert.Equal(hasQuery, reference.HasQuery);
        Assert.Equal(hasFragment, reference.HasFragment);
    }

    [Theory]
    [InlineData("http://example.com", 80, true)]
    [InlineData("https://example.com", 443, true)]
    [InlineData("ftp://example.com", 21, true)]
    [InlineData("http://example.com:8080", 8080, false)]
    [InlineData("https://example.com:8443", 8443, false)]
    [InlineData("ftp://example.com:2121", 2121, false)]
    [InlineData("mailto:user@example.com", 25, true)]
    public static void PortValue_VariousIris_ReturnsCorrectPortAndDefaultFlag(string iri, int expectedPort, bool expectedIsDefault)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.Equal(expectedPort, reference.PortValue);
        Assert.Equal(expectedIsDefault, reference.IsDefaultPort);
    }

    [Theory]
    [InlineData("https://example.com/path")]
    [InlineData("http://example.com")]
    [InlineData("ftp://files.example.com/public/")]
    [InlineData("mailto:user@example.com")]
    [InlineData("file:///C:/Users/Documents/file.txt")]
    public static void IsRelative_AbsoluteIris_ReturnsFalse(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.False(reference.IsRelative);
    }

    [Theory]
    [InlineData("https://example.com/path")]
    [InlineData("http://user@example.com:8080/path?query=value#fragment")]
    [InlineData("ftp://files.example.com/public/")]
    [InlineData("mailto:user@example.com")]
    [InlineData("file:///C:/Users/Documents/file.txt")]
    public static void GetUri_ValidIris_ReturnsEquivalentUri(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8Iri.CreateIri(iriBytes);

        Uri systemUri = reference.GetUri();
        var expectedUri = new Uri(iri, UriKind.Absolute);

        Assert.Equal(expectedUri.ToString(), systemUri.ToString());
        Assert.True(systemUri.IsAbsoluteUri);
    }

    [Theory]
    [InlineData("http://example.com/path%20with%20spaces")]
    [InlineData("http://example.com/path%2Fwith%2Fencoded%2Fslashes")]
    [InlineData("http://example.com?query=value%26encoded")]
    [InlineData("http://example.com#fragment%23encoded")]
    [InlineData("http://example.com/path?query=value&param=encoded%20value#fragment")]
    public static void CreateIri_PercentEncodedIris_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIri.ToArray());
    }

    [Theory]
    [InlineData("http://192.168.1.1")]
    [InlineData("http://192.168.1.1:8080")]
    [InlineData("http://[2001:db8::1]")]
    [InlineData("http://[2001:db8::1]:8080")]
    [InlineData("http://localhost")]
    [InlineData("http://localhost:3000")]
    public static void CreateIri_IpAddresses_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIri.ToArray());
    }

    [Theory]
    [InlineData("urn:isbn:0451450523")]
    [InlineData("news:comp.lang.ada")]
    [InlineData("tel:+1-816-555-1212")]
    [InlineData("ldap://[2001:db8::7]/c=GB?objectClass?one")]
    public static void CreateIri_NonHttpSchemes_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIri.ToArray());
    }

    [Theory]
    [InlineData("http://测试.example.com")]
    [InlineData("http://example.com/测试路径")]
    [InlineData("http://example.com?查询=值")]
    [InlineData("http://example.com#片段")]
    [InlineData("http://пример.com")]
    [InlineData("http://例え.テスト")]
    public static void CreateIri_UnicodeIri_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        Utf8Iri.TryCreateIri(iriBytes, out Utf8Iri reference);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIri.ToArray());
    }

    [Fact]
    public static void CreateIri_LargeIri_HandlesCorrectly()
    {
        string largePathSegment = new('a', 1000);
        string largeIri = $"http://example.com/{largePathSegment}";
        byte[] iriBytes = Encoding.UTF8.GetBytes(largeIri);

        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIri.ToArray());
    }

    [Fact]
    public static void CreateIri_ManyQueryParameters_HandlesCorrectly()
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

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIri.ToArray());
    }

    [Theory]
    [InlineData("http://example.com:1")]
    [InlineData("http://example.com:65535")]
    [InlineData("http://example.com:0")]
    public static void CreateIri_BoundaryPortValues_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIri.ToArray());
    }

    [Theory]
    [InlineData("scheme:")]
    [InlineData("scheme:/")]
    [InlineData("scheme://")]
    [InlineData("scheme:///")]
    public static void ComponentBoundariesIri_MinimalComponents_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIri.ToArray());
    }

    [Fact]
    public static void ComponentAccessIri_DefaultIri_ReturnsEmptySpans()
    {
        Utf8Iri reference = default;

        Assert.True(reference.Scheme.IsEmpty);
        Assert.True(reference.Authority.IsEmpty);
        Assert.True(reference.User.IsEmpty);
        Assert.True(reference.Host.IsEmpty);
        Assert.True(reference.Port.IsEmpty);
        Assert.True(reference.Path.IsEmpty);
        Assert.True(reference.Query.IsEmpty);
        Assert.True(reference.Fragment.IsEmpty);
        Assert.True(reference.OriginalIri.IsEmpty);
        Assert.Equal(0, reference.PortValue);
        Assert.False(reference.IsValid);
    }

    [Theory]
    [InlineData("http://example.com:00080", true)]
    [InlineData("http://example.com:999999", false)]
    [InlineData("http://[invalid-bracket", false)]
    [InlineData("http://host]/path", false)]
    [InlineData("http://host with spaces", false)]
    [InlineData("http:////example.com", false)]
    [InlineData("scheme:", true)]
    [InlineData("custom+scheme://example.com", true)]
    [InlineData("custom-scheme://example.com", true)]
    [InlineData("custom.scheme://example.com", true)]
    public static void CreateIri_EdgeCaseIris_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8Iri.CreateIri(iriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(iriBytes, reference.OriginalIri.ToArray());
        }
        else
        {
            bool result = Utf8Iri.TryCreateIri(iriBytes, out Utf8Iri reference);
            Assert.False(result);
            Assert.False(reference.IsValid);
        }
    }

    [Theory]
    [InlineData("http://example.com/path%20space", true)]
    [InlineData("http://example.com/path%2", false)]
    [InlineData("http://example.com/path%ZZ", false)]
    [InlineData("http://example.com/path%00", true)]
    [InlineData("http://example.com/path%FF", true)]
    [InlineData("http://example.com/path%C3%A9", true)]
    [InlineData("http://example.com/path%E2%82%AC", true)]
    public static void CreateIri_PercentEncodingEdgeCases_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8Iri.CreateIri(iriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(iriBytes, reference.OriginalIri.ToArray());
        }
        else
        {
            bool result = Utf8Iri.TryCreateIri(iriBytes, out Utf8Iri reference);
            Assert.False(result);
            Assert.False(reference.IsValid);
        }
    }

    [Theory]
    [InlineData("http://[::1]", true)]
    [InlineData("http://[2001:db8::1]", true)]
    [InlineData("http://[2001:db8::1]:8080", true)]
    [InlineData("http://[::ffff:192.0.2.1]", true)]
    [InlineData("http://[invalid", false)]
    [InlineData("http://invalid]", false)]
    [InlineData("http://[::1::2]", false)]
    [InlineData("http://192.168.1.256", true)]
    [InlineData("http://192.168.1", true)]
    public static void CreateIri_IpAddressEdgeCases_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8Iri.CreateIri(iriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(iriBytes, reference.OriginalIri.ToArray());
        }
        else
        {
            bool result = Utf8Iri.TryCreateIri(iriBytes, out Utf8Iri reference);
            Assert.False(result);
            Assert.False(reference.IsValid);
        }
    }

    [Theory]
    [InlineData("SCHEME://EXAMPLE.COM/PATH")]
    [InlineData("Http://Example.Com/Path")]
    [InlineData("HTTPS://USER@HOST.COM:443/PATH?QUERY=VALUE#FRAGMENT")]
    public static void CreateIri_CaseVariations_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIri.ToArray());
    }

    [Theory]
    [InlineData("data:text/plain;base64,SGVsbG8sIFdvcmxkIQ==")]
    [InlineData("blob:http://example.com/abc-123")]
    [InlineData("about:blank")]
    [InlineData("javascript:void(0)")]
    public static void CreateIri_SpecialSchemes_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8Iri.CreateIri(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIri.ToArray());
    }

    [Theory]
    [InlineData("file://", true)]
    [InlineData("file:///", true)]
    [InlineData("file://server/share/file.txt", false)]
    [InlineData("file://server/share/dir/", false)]
    [InlineData("file://server/share", false)]
    [InlineData("file://server/share/", false)]
    [InlineData("file://server", false)]
    [InlineData("file://server/", false)]
    [InlineData("file://server//share", false)]
    public static void CreateIri_UncFileIris_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8Iri.CreateIri(iriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(iriBytes, reference.OriginalIri.ToArray());
        }
        else
        {
            bool result = Utf8Iri.TryCreateIri(iriBytes, out Utf8Iri reference);
            Assert.False(result);
            Assert.False(reference.IsValid);
        }
    }

    [Theory]
    [InlineData("http://a", true)]
    [InlineData("http://", false)]
    [InlineData("http:///a", false)]
    public static void CreateIri_HttpSchemeEdgeCases_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8Iri.CreateIri(iriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(iriBytes, reference.OriginalIri.ToArray());
        }
        else
        {
            bool result = Utf8Iri.TryCreateIri(iriBytes, out Utf8Iri reference);
            Assert.False(result);
            Assert.False(reference.IsValid);
        }
    }

    [Theory]
    [InlineData("c:/my/file.txt", true)]
    [InlineData("c:/my/file.txt#fragment", true)]
    public static void CreateIri_DriveLetterSchemes_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8Iri.CreateIri(iriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(iriBytes, reference.OriginalIri.ToArray());
        }
        else
        {
            bool result = Utf8Iri.TryCreateIri(iriBytes, out Utf8Iri reference);
            Assert.False(result);
            Assert.False(reference.IsValid);
        }
    }
}