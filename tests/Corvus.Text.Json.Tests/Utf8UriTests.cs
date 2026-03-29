// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Xunit;

namespace Corvus.Text.Json.Tests;

public static partial class Utf8UriTests
{
    [Theory]
    [InlineData("https://example.com/path")]
    [InlineData("http://user@example.com:8080/path?query=value#fragment")]
    [InlineData("ftp://files.example.com/public/")]
    [InlineData("mailto:user@example.com")]
    [InlineData("file:///C:/Users/Documents/file.txt")]
    public static void CreateUri_ValidAbsoluteUris_ReturnsUtf8Uri(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        var reference = Utf8Uri.CreateUri(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUri.ToArray());
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("../parent/path")]
    [InlineData("?query=only")]
    [InlineData("#fragment-only")]
    [InlineData("/absolute/path")]
    [InlineData("")]
    public static void CreateUri_RelativeUris_ThrowsArgumentException(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => Utf8Uri.CreateUri(uriBytes));
        Assert.Equal("The value is not a valid JSON reference.", exception.Message);
    }

    [Theory]
    [InlineData("http://[invalid-ipv6")]
    [InlineData("http://example.com:99999")]
    [InlineData("ht tp://example.com")]
    [InlineData("\x01\x02\x03")]
    [InlineData("http://example.com/path with spaces")]
    public static void CreateUri_InvalidUris_ThrowsArgumentException(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => Utf8Uri.CreateUri(uriBytes));
        Assert.Equal("The value is not a valid JSON reference.", exception.Message);
    }

    [Theory]
    [InlineData("https://example.com/path")]
    [InlineData("http://user@example.com:8080/path?query=value#fragment")]
    [InlineData("ftp://files.example.com/public/")]
    [InlineData("mailto:user@example.com")]
    [InlineData("file:///C:/Users/Documents/file.txt")]
    public static void TryCreateUri_ValidAbsoluteUris_ReturnsTrueAndValidUri(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        bool result = Utf8Uri.TryCreateUri(uriBytes, out Utf8Uri reference);

        Assert.True(result);
        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUri.ToArray());
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
    public static void TryCreateUri_InvalidOrRelativeUris_ReturnsFalse(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        bool result = Utf8Uri.TryCreateUri(uriBytes, out Utf8Uri reference);

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
    public static void ComponentExtractionUri_AbsoluteUris_ExtractsCorrectly(string uri, string expectedScheme, string expectedAuthority, string expectedUser, string expectedHost, string expectedPort, string expectedPath, string expectedQuery, string expectedFragment)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);
        var reference = Utf8Uri.CreateUri(uriBytes);

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
    public static void HasProperties_VariousUris_ReturnsCorrectValues(string uri, bool hasScheme, bool hasAuthority, bool hasUser, bool hasHost, bool hasPath, bool hasQuery, bool hasFragment)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);
        var reference = Utf8Uri.CreateUri(uriBytes);

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
    public static void PortValue_VariousUris_ReturnsCorrectPortAndDefaultFlag(string uri, int expectedPort, bool expectedIsDefault)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);
        var reference = Utf8Uri.CreateUri(uriBytes);

        Assert.Equal(expectedPort, reference.PortValue);
        Assert.Equal(expectedIsDefault, reference.IsDefaultPort);
    }

    [Theory]
    [InlineData("https://example.com/path")]
    [InlineData("http://example.com")]
    [InlineData("ftp://files.example.com/public/")]
    [InlineData("mailto:user@example.com")]
    [InlineData("file:///C:/Users/Documents/file.txt")]
    public static void IsRelative_AbsoluteUris_ReturnsFalse(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);
        var reference = Utf8Uri.CreateUri(uriBytes);

        Assert.False(reference.IsRelative);
    }

    [Theory]
    [InlineData("https://example.com/path")]
    [InlineData("http://user@example.com:8080/path?query=value#fragment")]
    [InlineData("ftp://files.example.com/public/")]
    [InlineData("mailto:user@example.com")]
    [InlineData("file:///C:/Users/Documents/file.txt")]
    public static void GetUri_ValidUris_ReturnsEquivalentUri(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);
        var reference = Utf8Uri.CreateUri(uriBytes);

        Uri systemUri = reference.GetUri();
        var expectedUri = new Uri(uri, UriKind.Absolute);

        Assert.Equal(expectedUri.ToString(), systemUri.ToString());
        Assert.True(systemUri.IsAbsoluteUri);
    }

    [Theory]
    [InlineData("http://example.com/path%20with%20spaces")]
    [InlineData("http://example.com/path%2Fwith%2Fencoded%2Fslashes")]
    [InlineData("http://example.com?query=value%26encoded")]
    [InlineData("http://example.com#fragment%23encoded")]
    [InlineData("http://example.com/path?query=value&param=encoded%20value#fragment")]
    public static void CreateUri_PercentEncodedUris_HandlesCorrectly(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        var reference = Utf8Uri.CreateUri(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUri.ToArray());
    }

    [Theory]
    [InlineData("http://192.168.1.1")]
    [InlineData("http://192.168.1.1:8080")]
    [InlineData("http://[2001:db8::1]")]
    [InlineData("http://[2001:db8::1]:8080")]
    [InlineData("http://localhost")]
    [InlineData("http://localhost:3000")]
    public static void CreateUri_IpAddresses_HandlesCorrectly(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        var reference = Utf8Uri.CreateUri(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUri.ToArray());
    }

    [Theory]
    [InlineData("urn:isbn:0451450523")]
    [InlineData("news:comp.lang.ada")]
    [InlineData("tel:+1-816-555-1212")]
    [InlineData("ldap://[2001:db8::7]/c=GB?objectClass?one")]
    public static void CreateUri_NonHttpSchemes_HandlesCorrectly(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        var reference = Utf8Uri.CreateUri(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUri.ToArray());
    }

    [Fact]
    public static void CreateUri_LargeUri_HandlesCorrectly()
    {
        string largePathSegment = new('a', 1000);
        string largeUri = $"http://example.com/{largePathSegment}";
        byte[] uriBytes = Encoding.UTF8.GetBytes(largeUri);

        var reference = Utf8Uri.CreateUri(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUri.ToArray());
    }

    [Fact]
    public static void CreateUri_ManyQueryParameters_HandlesCorrectly()
    {
        var uriBuilder = new StringBuilder("http://example.com/path?");
        for (int i = 0; i < 100; i++)
        {
            if (i > 0) uriBuilder.Append('&');
            uriBuilder.Append($"param{i}=value{i}");
        }

        string uri = uriBuilder.ToString();
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        var reference = Utf8Uri.CreateUri(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUri.ToArray());
    }

    [Theory]
    [InlineData("http://example.com:1")]
    [InlineData("http://example.com:65535")]
    [InlineData("http://example.com:0")]
    public static void CreateUri_BoundaryPortValues_HandlesCorrectly(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        var reference = Utf8Uri.CreateUri(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUri.ToArray());
    }

    [Theory]
    [InlineData("scheme:")]
    [InlineData("scheme:/")]
    [InlineData("scheme://")]
    [InlineData("scheme:///")]
    public static void ComponentBoundariesUri_MinimalComponents_HandlesCorrectly(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        var reference = Utf8Uri.CreateUri(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUri.ToArray());
    }

    [Fact]
    public static void ComponentAccessUri_DefaultUri_ReturnsEmptySpans()
    {
        Utf8Uri reference = default;

        Assert.True(reference.Scheme.IsEmpty);
        Assert.True(reference.Authority.IsEmpty);
        Assert.True(reference.User.IsEmpty);
        Assert.True(reference.Host.IsEmpty);
        Assert.True(reference.Port.IsEmpty);
        Assert.True(reference.Path.IsEmpty);
        Assert.True(reference.Query.IsEmpty);
        Assert.True(reference.Fragment.IsEmpty);
        Assert.True(reference.OriginalUri.IsEmpty);
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
    public static void CreateUri_EdgeCaseUris_HandlesCorrectly(string uri, bool shouldBeValid)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        if (shouldBeValid)
        {
            var reference = Utf8Uri.CreateUri(uriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(uriBytes, reference.OriginalUri.ToArray());
        }
        else
        {
            bool result = Utf8Uri.TryCreateUri(uriBytes, out Utf8Uri reference);
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
    public static void CreateUri_PercentEncodingEdgeCases_HandlesCorrectly(string uri, bool shouldBeValid)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        if (shouldBeValid)
        {
            var reference = Utf8Uri.CreateUri(uriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(uriBytes, reference.OriginalUri.ToArray());
        }
        else
        {
            bool result = Utf8Uri.TryCreateUri(uriBytes, out Utf8Uri reference);
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
    public static void CreateUri_IpAddressEdgeCases_HandlesCorrectly(string uri, bool shouldBeValid)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        if (shouldBeValid)
        {
            var reference = Utf8Uri.CreateUri(uriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(uriBytes, reference.OriginalUri.ToArray());
        }
        else
        {
            bool result = Utf8Uri.TryCreateUri(uriBytes, out Utf8Uri reference);
            Assert.False(result);
            Assert.False(reference.IsValid);
        }
    }

    [Theory]
    [InlineData("SCHEME://EXAMPLE.COM/PATH")]
    [InlineData("Http://Example.Com/Path")]
    [InlineData("HTTPS://USER@HOST.COM:443/PATH?QUERY=VALUE#FRAGMENT")]
    public static void CreateUri_CaseVariations_HandlesCorrectly(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        var reference = Utf8Uri.CreateUri(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUri.ToArray());
    }

    [Theory]
    [InlineData("data:text/plain;base64,SGVsbG8sIFdvcmxkIQ==")]
    [InlineData("blob:http://example.com/abc-123")]
    [InlineData("about:blank")]
    [InlineData("javascript:void(0)")]
    public static void CreateUri_SpecialSchemes_HandlesCorrectly(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        var reference = Utf8Uri.CreateUri(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUri.ToArray());
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
    public static void CreateUri_UncFileUris_HandlesCorrectly(string uri, bool shouldBeValid)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        if (shouldBeValid)
        {
            var reference = Utf8Uri.CreateUri(uriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(uriBytes, reference.OriginalUri.ToArray());
        }
        else
        {
            bool result = Utf8Uri.TryCreateUri(uriBytes, out Utf8Uri reference);
            Assert.False(result);
            Assert.False(reference.IsValid);
        }
    }

    [Theory]
    [InlineData("http://a", true)]
    [InlineData("http://", false)]
    [InlineData("http:///a", false)]
    public static void CreateUri_HttpSchemeEdgeCases_HandlesCorrectly(string uri, bool shouldBeValid)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        if (shouldBeValid)
        {
            var reference = Utf8Uri.CreateUri(uriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(uriBytes, reference.OriginalUri.ToArray());
        }
        else
        {
            bool result = Utf8Uri.TryCreateUri(uriBytes, out Utf8Uri reference);
            Assert.False(result);
            Assert.False(reference.IsValid);
        }
    }

    [Theory]
    [InlineData("c:/my/file.txt", true)]
    [InlineData("c:/my/file.txt#fragment", true)]
    public static void CreateUri_DriveLetterSchemes_HandlesCorrectly(string uri, bool shouldBeValid)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        if (shouldBeValid)
        {
            var reference = Utf8Uri.CreateUri(uriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(uriBytes, reference.OriginalUri.ToArray());
        }
        else
        {
            bool result = Utf8Uri.TryCreateUri(uriBytes, out Utf8Uri reference);
            Assert.False(result);
            Assert.False(reference.IsValid);
        }
    }
}