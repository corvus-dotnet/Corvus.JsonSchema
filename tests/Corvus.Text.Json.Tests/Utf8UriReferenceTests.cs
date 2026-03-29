// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Xunit;

namespace Corvus.Text.Json.Tests;

public static partial class Utf8UriReferenceTests
{
    [Theory]
    [InlineData("https://example.com/path")]
    [InlineData("http://user@example.com:8080/path?query=value#fragment")]
    [InlineData("ftp://files.example.com/public/")]
    [InlineData("mailto:user@example.com")]
    [InlineData("file:///C:/Users/Documents/file.txt")]
    [InlineData("relative/path")]
    [InlineData("../parent/path")]
    [InlineData("?query=only")]
    [InlineData("#fragment-only")]
    [InlineData("/absolute/path")]
    [InlineData("")]
    public static void CreateUri_ValidUris_ReturnsUtf8UriReference(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        var reference = Utf8UriReference.CreateUriReference(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
    }

    [Theory]
    [InlineData("http://[invalid-ipv6")]
    [InlineData("http://example.com:99999")]
    [InlineData("ht tp://example.com")] // Falls back to a relative reference
    [InlineData("\x01\x02\x03")]
    [InlineData("http://example.com/path with spaces")]
    public static void CreateUri_InvalidUris_ThrowsArgumentException(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => Utf8UriReference.CreateUriReference(uriBytes));
        Assert.Equal("The value is not a valid JSON reference.", exception.Message);
    }

    [Theory]
    [InlineData("https://example.com/path")]
    [InlineData("http://user@example.com:8080/path?query=value#fragment")]
    [InlineData("ftp://files.example.com/public/")]
    [InlineData("mailto:user@example.com")]
    [InlineData("file:///C:/Users/Documents/file.txt")]
    [InlineData("relative/path")]
    [InlineData("../parent/path")]
    [InlineData("?query=only")]
    [InlineData("#fragment-only")]
    [InlineData("/absolute/path")]
    [InlineData("")]
    ////[InlineData("ht tp://example.com")] // Falls back to a relative reference
    public static void TryCreateUri_ValidUris_ReturnsTrueAndValidReference(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        bool result = Utf8UriReference.TryCreateUriReference(uriBytes, out Utf8UriReference reference);

        Assert.True(result);
        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
    }

    [Theory]
    [InlineData("http://[invalid-ipv6")]
    [InlineData("http://example.com:99999")]
    [InlineData("\x01\x02\x03")]
    [InlineData("http://example.com/path with spaces")]
    public static void TryCreateUri_InvalidUris_ReturnsFalse(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        bool result = Utf8UriReference.TryCreateUriReference(uriBytes, out Utf8UriReference reference);

        Assert.False(result);
        Assert.False(reference.IsValid);
    }

    [Theory]
    [InlineData("https://user@example.com:8080/path?query=value#fragment", "https", "user@example.com:8080", "user", "example.com", "8080", "/path", "query=value", "fragment")]
    [InlineData("http://example.com/path", "http", "example.com", "", "example.com", "", "/path", "", "")]
    [InlineData("ftp://files.example.com:21/public/", "ftp", "files.example.com:21", "", "files.example.com", "21", "/public/", "", "")]
    [InlineData("mailto:user@example.com", "mailto", "user@example.com", "user", "example.com", "", "", "", "")]
    [InlineData("file:///C:/Users/Documents/file.txt", "file", "", "", "", "", "/C:/Users/Documents/file.txt", "", "")]
    // Edge cases: absolute URIs
    [InlineData("http://a", "http", "a", "", "a", "", "", "", "")]
    [InlineData("file:///", "file", "", "", "", "", "/", "", "")]
    public static void ComponentExtractionUri_AbsoluteUris_ExtractsCorrectly(string uri, string expectedScheme, string expectedAuthority, string expectedUser, string expectedHost, string expectedPort, string expectedPath, string expectedQuery, string expectedFragment)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);
        var reference = Utf8UriReference.CreateUriReference(uriBytes);

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
    [InlineData("relative/path", "", "", "", "", "", "relative/path", "", "")]
    [InlineData("../parent/path", "", "", "", "", "", "../parent/path", "", "")]
    [InlineData("?query=only", "", "", "", "", "", "", "query=only", "")]
    [InlineData("#fragment-only", "", "", "", "", "", "", "", "fragment-only")]
    [InlineData("/absolute/path", "", "", "", "", "", "/absolute/path", "", "")]
    [InlineData("path?query=value#fragment", "", "", "", "", "", "path", "query=value", "fragment")]
    // Edge cases: relative URIs and ambiguous forms
    [InlineData("http:a", "", "", "", "", "", "a", "", "")]
    [InlineData("http:/a", "", "", "", "", "", "/a", "", "")]
    [InlineData("http:", "", "", "", "", "", "", "", "")]
    [InlineData("c:/my/file.txt", "c", "", "", "", "", "/my/file.txt", "", "")]
    [InlineData("c:/my/file.txt#fragment", "c", "", "", "", "", "/my/file.txt", "", "fragment")]
    public static void ComponentExtractionUri_RelativeUris_ExtractsCorrectly(string uri, string expectedScheme, string expectedAuthority, string expectedUser, string expectedHost, string expectedPort, string expectedPath, string expectedQuery, string expectedFragment)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);
        var reference = Utf8UriReference.CreateUriReference(uriBytes);

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
    [InlineData("relative/path", false, false, false, false, true, false, false)]
    [InlineData("../parent/path", false, false, false, false, true, false, false)]
    [InlineData("?query=only", false, false, false, false, false, true, false)]
    [InlineData("#fragment-only", false, false, false, false, false, false, true)]
    [InlineData("/absolute/path", false, false, false, false, true, false, false)]
    [InlineData("path?query=value#fragment", false, false, false, false, true, true, true)]
    [InlineData("", false, false, false, false, false, false, false)]
    public static void HasProperties_VariousUris_ReturnsCorrectValues(string uri, bool hasScheme, bool hasAuthority, bool hasUser, bool hasHost, bool hasPath, bool hasQuery, bool hasFragment)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);
        var reference = Utf8UriReference.CreateUriReference(uriBytes);

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
    [InlineData("relative/path", 0, true)]
    public static void PortValue_VariousUris_ReturnsCorrectPortAndDefaultFlag(string uri, int expectedPort, bool expectedIsDefault)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);
        var reference = Utf8UriReference.CreateUriReference(uriBytes);

        Assert.Equal(expectedPort, reference.PortValue);
        Assert.Equal(expectedIsDefault, reference.IsDefaultPort);
    }

    [Theory]
    [InlineData("https://example.com/path", false)]
    [InlineData("http://example.com", false)]
    [InlineData("ftp://files.example.com/public/", false)]
    [InlineData("mailto:user@example.com", false)]
    [InlineData("file:///C:/Users/Documents/file.txt", false)]
    [InlineData("relative/path", true)]
    [InlineData("../parent/path", true)]
    [InlineData("?query=only", true)]
    [InlineData("#fragment-only", true)]
    [InlineData("/absolute/path", true)]
    [InlineData("", true)]
    public static void IsRelative_VariousUris_ReturnsCorrectValue(string uri, bool expectedIsRelative)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);
        var reference = Utf8UriReference.CreateUriReference(uriBytes);

        Assert.Equal(expectedIsRelative, reference.IsRelative);
    }

    [Theory]
    [InlineData("https://example.com/path")]
    [InlineData("http://user@example.com:8080/path?query=value#fragment")]
    [InlineData("ftp://files.example.com/public/")]
    [InlineData("mailto:user@example.com")]
    [InlineData("file:///C:/Users/Documents/file.txt")]
    [InlineData("relative/path")]
    [InlineData("../parent/path")]
    [InlineData("?query=only")]
    [InlineData("#fragment-only")]
    [InlineData("/absolute/path")]
    public static void GetUri_ValidUris_ReturnsEquivalentUri(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);
        var reference = Utf8UriReference.CreateUriReference(uriBytes);

        Uri systemUri = reference.GetUri();
        var expectedUri = new Uri(uri, UriKind.RelativeOrAbsolute);

        Assert.Equal(expectedUri.ToString(), systemUri.ToString());
        Assert.Equal(expectedUri.IsAbsoluteUri, systemUri.IsAbsoluteUri);
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

        var reference = Utf8UriReference.CreateUriReference(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
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

        var reference = Utf8UriReference.CreateUriReference(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
    }

    [Theory]
    [InlineData("urn:isbn:0451450523")]
    [InlineData("news:comp.lang.ada")]
    [InlineData("tel:+1-816-555-1212")]
    [InlineData("ldap://[2001:db8::7]/c=GB?objectClass?one")]
    public static void CreateUri_NonHttpSchemes_HandlesCorrectly(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        var reference = Utf8UriReference.CreateUriReference(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
    }

    [Fact]
    public static void CreateUri_EmptySpan_HandlesCorrectly()
    {
        ReadOnlySpan<byte> emptySpan = ReadOnlySpan<byte>.Empty;

        var reference = Utf8UriReference.CreateUriReference(emptySpan);

        Assert.True(reference.IsValid);
        Assert.True(reference.OriginalUriReference.IsEmpty);
        Assert.True(reference.IsRelative);
    }

    [Theory]
    [InlineData("http://测试.example.com")]
    [InlineData("http://example.com/测试路径")]
    [InlineData("http://example.com?查询=值")]
    [InlineData("http://example.com#片段")]
    [InlineData("http://пример.com")]
    [InlineData("http://例え.テスト")]
    public static void CreateUri_UnicodeIri_HandlesCorrectly(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        Utf8UriReference.TryCreateUriReference(uriBytes, out Utf8UriReference reference);

        Assert.False(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
    }

    [Fact]
    public static void CreateUri_LargeUri_HandlesCorrectly()
    {
        string largePathSegment = new('a', 1000);
        string largeUri = $"http://example.com/{largePathSegment}";
        byte[] uriBytes = Encoding.UTF8.GetBytes(largeUri);

        var reference = Utf8UriReference.CreateUriReference(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
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

        var reference = Utf8UriReference.CreateUriReference(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
    }

    [Theory]
    [InlineData("http://example.com:1")]
    [InlineData("http://example.com:65535")]
    [InlineData("http://example.com:0")]
    public static void CreateUri_BoundaryPortValues_HandlesCorrectly(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        var reference = Utf8UriReference.CreateUriReference(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("ab", true)]
    [InlineData("abc", true)]
    public static void CreateUri_MinimalSchemes_HandlesCorrectly(string scheme, bool handlesCorrectly)
    {
        string uri = $"{scheme}:path";
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        Utf8UriReference.TryCreateUriReference(uriBytes, out Utf8UriReference reference);

        Assert.Equal(reference.IsValid, handlesCorrectly);
        if (handlesCorrectly)
        {
            Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
        }
    }

    [Theory]
    [InlineData("scheme:")]
    [InlineData("scheme:/")]
    [InlineData("scheme://")]
    [InlineData("scheme:///")]
    public static void ComponentBoundariesUri_MinimalComponents_HandlesCorrectly(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        var reference = Utf8UriReference.CreateUriReference(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
    }

    [Fact]
    public static void ComponentAccessUri_DefaultReference_ReturnsEmptySpans()
    {
        Utf8UriReference reference = default;

        Assert.True(reference.Scheme.IsEmpty);
        Assert.True(reference.Authority.IsEmpty);
        Assert.True(reference.User.IsEmpty);
        Assert.True(reference.Host.IsEmpty);
        Assert.True(reference.Port.IsEmpty);
        Assert.True(reference.Path.IsEmpty);
        Assert.True(reference.Query.IsEmpty);
        Assert.True(reference.Fragment.IsEmpty);
        Assert.True(reference.OriginalUriReference.IsEmpty);
        Assert.Equal(0, reference.PortValue);
        Assert.False(reference.IsValid);
    }

    // Additional test cases to exercise more Utf8Uri functionality

    [Theory]
    [InlineData("http://example.com:00080", true)] // Leading zeros in port are valid (the ABNF specifies *DIGIT)
    [InlineData("http://example.com:999999", false)] // Port too large
    [InlineData("http://[invalid-bracket", false)] // Invalid IPv6 brackets
    [InlineData("http://host]/path", false)] // Invalid bracket
    [InlineData("http://host with spaces", false)] // Spaces in host
    [InlineData("http:////example.com", false)] // Too many slashes
    [InlineData("scheme:", true)] // Minimal valid absolute URI
    [InlineData("custom+scheme://example.com", true)] // Plus in scheme
    [InlineData("custom-scheme://example.com", true)] // Hyphen in scheme
    [InlineData("custom.scheme://example.com", true)] // Dot in scheme
    [InlineData("123scheme://example.com", true)] // Scheme starting with digit should be invalid, but is treated as a relative IRI path
    [InlineData("+scheme://example.com", true)] // Scheme starting with plus should be invalid, but is treated as a relative IRI path
    [InlineData("-scheme://example.com", true)] // Scheme starting with hyphen should be invalid, but is treated as a relative IRI path
    [InlineData(".scheme://example.com", true)] // Scheme starting with dot should be invalid, but is treated as a relative IRI path
    public static void CreateUri_EdgeCaseUris_HandlesCorrectly(string uri, bool shouldBeValid)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        if (shouldBeValid)
        {
            var reference = Utf8UriReference.CreateUriReference(uriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
        }
        else
        {
            bool result = Utf8UriReference.TryCreateUriReference(uriBytes, out Utf8UriReference reference);
            Assert.False(result);
            Assert.False(reference.IsValid);
        }
    }

    [Theory]
    [InlineData("http://example.com/path%20space", true)] // Valid percent encoding
    [InlineData("http://example.com/path%2", false)] // Invalid percent encoding (incomplete)
    [InlineData("http://example.com/path%ZZ", false)] // Invalid percent encoding (non-hex)
    [InlineData("http://example.com/path%00", true)] // Null byte percent encoded
    [InlineData("http://example.com/path%FF", true)] // High byte percent encoded
    [InlineData("http://example.com/path%C3%A9", true)] // UTF-8 encoded character (é)
    [InlineData("http://example.com/path%E2%82%AC", true)] // UTF-8 encoded Euro symbol
    public static void CreateUri_PercentEncodingEdgeCases_HandlesCorrectly(string uri, bool shouldBeValid)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        if (shouldBeValid)
        {
            var reference = Utf8UriReference.CreateUriReference(uriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
        }
        else
        {
            bool result = Utf8UriReference.TryCreateUriReference(uriBytes, out Utf8UriReference reference);
            Assert.False(result);
            Assert.False(reference.IsValid);
        }
    }

    [Theory]
    [InlineData("http://example.com/path/../other", true)] // Dot segments
    [InlineData("http://example.com/path/./current", true)] // Single dot segment
    [InlineData("http://example.com/path//double/slash", true)] // Double slashes in path
    [InlineData("http://example.com\\windows\\path", false)] // Backslashes should be invalid in HTTP
    [InlineData("file:///C:\\Windows\\Path", true)] // Backslashes in file URLs might be valid
    [InlineData("http://example.com/path?a=1&b=2&c=3", true)] // Multiple query parameters
    [InlineData("http://example.com/path?query=value#frag1#frag2", false)] // Fragment containing hash should be encoded
    [InlineData("http://example.com:80/path", true)] // Default HTTP port explicitly specified
    [InlineData("https://example.com:443/path", true)] // Default HTTPS port explicitly specified
    public static void CreateUri_PathAndQueryEdgeCases_HandlesCorrectly(string uri, bool shouldBeValid)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        if (shouldBeValid)
        {
            var reference = Utf8UriReference.CreateUriReference(uriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
        }
        else
        {
            bool result = Utf8UriReference.TryCreateUriReference(uriBytes, out Utf8UriReference reference);
            Assert.False(result);
            Assert.False(reference.IsValid);
        }
    }

    [Theory]
    [InlineData("http://[::1]", true)] // IPv6 loopback
    [InlineData("http://[2001:db8::1]", true)] // IPv6 address
    [InlineData("http://[2001:db8::1]:8080", true)] // IPv6 with port
    [InlineData("http://[::ffff:192.0.2.1]", true)] // IPv4-mapped IPv6
    [InlineData("http://[invalid", false)] // Incomplete IPv6 bracket
    [InlineData("http://invalid]", false)] // Invalid closing bracket
    [InlineData("http://[::1::2]", false)] // Invalid IPv6 (too many colons)
    [InlineData("http://192.168.1.256", true)] // Valid DNS hostname per RFC 1123 (invalid as IPv4, but valid as hostname)
    [InlineData("http://192.168.1", true)] // Valid DNS hostname per RFC 1123 (invalid as IPv4, but valid as hostname)
    public static void CreateUri_IpAddressEdgeCases_HandlesCorrectly(string uri, bool shouldBeValid)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        if (shouldBeValid)
        {
            var reference = Utf8UriReference.CreateUriReference(uriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
        }
        else
        {
            bool result = Utf8UriReference.TryCreateUriReference(uriBytes, out Utf8UriReference reference);
            Assert.False(result);
            Assert.False(reference.IsValid);
        }
    }

    [Theory]
    [InlineData("", true)] // Empty string
    [InlineData(" ", true)] // Just whitespace
    [InlineData("\t", false)] // Tab character
    [InlineData("\n", false)] // Newline character
    [InlineData("\r", false)] // Carriage return
    [InlineData("http://example.com ", true)] // Trailing whitespace permitted
    [InlineData(" http://example.com", true)] // Leading whitespace permitted
    [InlineData("http://example.com/\u0000", false)] // Null character
    [InlineData("http://example.com/\u001F", false)] // Control character
    [InlineData("http://example.com/\u007F", false)] // DEL character
    public static void CreateUri_WhitespaceAndControlCharacters_HandlesCorrectly(string uri, bool shouldBeValid)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        if (shouldBeValid)
        {
            var reference = Utf8UriReference.CreateUriReference(uriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
        }
        else
        {
            bool result = Utf8UriReference.TryCreateUriReference(uriBytes, out Utf8UriReference reference);
            Assert.False(result);
            Assert.False(reference.IsValid);
        }
    }

    [Theory]
    [InlineData("http://user:password@example.com", "user:password", "example.com")]
    [InlineData("http://user@example.com", "user", "example.com")]
    [InlineData("http://user:@example.com", "user:", "example.com")]
    [InlineData("http://:password@example.com", ":password", "example.com")]
    [InlineData("http://@example.com", "", "example.com")]
    [InlineData("ftp://anonymous:guest@ftp.example.com", "anonymous:guest", "ftp.example.com")]
    public static void ComponentExtractionUri_UserInfoVariations_ExtractsCorrectly(string uri, string expectedUserInfo, string expectedHost)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);
        var reference = Utf8UriReference.CreateUriReference(uriBytes);

        if (string.IsNullOrEmpty(expectedUserInfo))
        {
            Assert.True(reference.User.IsEmpty);
        }
        else
        {
            Assert.Equal(Encoding.UTF8.GetBytes(expectedUserInfo), reference.User.ToArray());
        }

        Assert.Equal(Encoding.UTF8.GetBytes(expectedHost), reference.Host.ToArray());
    }

    [Theory]
    [InlineData("SCHEME://EXAMPLE.COM/PATH")]
    [InlineData("Http://Example.Com/Path")]
    [InlineData("HTTPS://USER@HOST.COM:443/PATH?QUERY=VALUE#FRAGMENT")]
    public static void CreateUri_CaseVariations_HandlesCorrectly(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        var reference = Utf8UriReference.CreateUriReference(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
    }

    [Theory]
    [InlineData("http://example.com", new byte[] { })] // Empty path
    [InlineData("http://example.com/", new byte[] { (byte)'/' })] // Root path
    [InlineData("http://example.com/path", new byte[] { (byte)'/', (byte)'p', (byte)'a', (byte)'t', (byte)'h' })]
    [InlineData("?query", new byte[] { })] // Query-only relative URI
    [InlineData("#fragment", new byte[] { })] // Fragment-only relative URI
    public static void ComponentExtractionUri_PathVariations_ExtractsCorrectly(string uri, byte[] expectedPath)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);
        var reference = Utf8UriReference.CreateUriReference(uriBytes);

        Assert.Equal(expectedPath, reference.Path.ToArray());
    }

    [Theory]
    [InlineData("http://example.com?")]
    [InlineData("http://example.com#")]
    [InlineData("http://example.com/?")]
    [InlineData("http://example.com/#")]
    [InlineData("path?")]
    [InlineData("path#")]
    public static void CreateUri_EmptyQueryAndFragment_HandlesCorrectly(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        var reference = Utf8UriReference.CreateUriReference(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
    }

    [Fact]
    public static void CreateUri_VeryLongUri_HandlesCorrectly()
    {
        string baseUri = "http://example.com/";
        string longPath = new('a', 4000); // Very long path
        string uri = baseUri + longPath;
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        var reference = Utf8UriReference.CreateUriReference(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
    }

    [Theory]
    [InlineData("data:text/plain;base64,SGVsbG8sIFdvcmxkIQ==")]
    [InlineData("blob:http://example.com/abc-123")]
    [InlineData("about:blank")]
    [InlineData("javascript:void(0)")]
    public static void CreateUri_SpecialSchemes_HandlesCorrectly(string uri)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        var reference = Utf8UriReference.CreateUriReference(uriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
    }

    [Theory]
    // Valid relative paths
    [InlineData("file://", true)]
    [InlineData("file:///", true)]
    // Valid UNC file URLs, but JSON Reference does not permit UNC paths.
    [InlineData("file://server/share/file.txt", false)]
    [InlineData("file://server/share/dir/", false)]
    [InlineData("file://server/share", false)]
    [InlineData("file://server/share/", false)]
    // Invalid UNC file URLs
    [InlineData("file://server", false)]
    [InlineData("file://server/", false)]
    [InlineData("file://server//share", false)]
    public static void CreateUri_UncFileUris_HandlesCorrectly(string uri, bool shouldBeValid)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        if (shouldBeValid)
        {
            var reference = Utf8UriReference.CreateUriReference(uriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
        }
        else
        {
            bool result = Utf8UriReference.TryCreateUriReference(uriBytes, out Utf8UriReference reference);
            Assert.False(result);
            Assert.False(reference.IsValid);
        }
    }

    [Theory]
    // RFC 3986 Section 5.4.2 examples for http: scheme
    [InlineData("http:a", true)]
    [InlineData("http:/a", true)]
    [InlineData("http://a", true)]
    [InlineData("http:", true)]
    [InlineData("http:?query", true)]
    [InlineData("http:#frag", true)]
    [InlineData("http://?query", false)]
    [InlineData("http://#frag", false)]
    [InlineData("http://", false)]
    [InlineData("http:///a", false)]
    public static void CreateUri_HttpSchemeRelativeUris_HandlesCorrectly(string uri, bool shouldBeValid)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        if (shouldBeValid)
        {
            var reference = Utf8UriReference.CreateUriReference(uriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
        }
        else
        {
            bool result = Utf8UriReference.TryCreateUriReference(uriBytes, out Utf8UriReference reference);
            Assert.False(result);
            Assert.False(reference.IsValid);
        }
    }

    [Theory]
    [InlineData("c:\\my\\file.txt", false)]
    [InlineData("c:/my/file.txt", true)]
    [InlineData("c:/my/file.txt#fragment", true)]
    [InlineData("c:\\my\\file.txt#fragment", false)]
    public static void CreateUri_Implicit_File(string uri, bool shouldBeValid)
    {
        // We do not support implicit file checking
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);

        if (shouldBeValid)
        {
            var reference = Utf8UriReference.CreateUriReference(uriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(uriBytes, reference.OriginalUriReference.ToArray());
        }
        else
        {
            bool result = Utf8UriReference.TryCreateUriReference(uriBytes, out Utf8UriReference reference);
            Assert.False(result);
            Assert.False(reference.IsValid);
        }
    }

    [Theory]
    // Absolute URIs
    [InlineData("http://example.com", false)]
    [InlineData("https://example.com", false)]
    [InlineData("ftp://example.com", false)]
    [InlineData("mailto:user@example.com", false)]
    [InlineData("file:///C:/Users/Documents/file.txt", false)]
    // Relative URIs
    [InlineData("relative/path", true)]
    [InlineData("../parent/path", true)]
    [InlineData("?query=only", true)]
    [InlineData("#fragment-only", true)]
    [InlineData("/absolute/path", true)]
    [InlineData("", true)]
    // Edge cases
    [InlineData("http:a", true)]
    [InlineData("http:/a", true)]
    [InlineData("http://a", false)]
    [InlineData("http:?query", true)]
    [InlineData("http:#frag", true)]
    [InlineData("http:", true)]
    [InlineData("c:/my/file.txt", false)]
    [InlineData("c:/my/file.txt#fragment", false)]
    public static void IsRelativeUri_Validation(string uri, bool expectedIsRelative)
    {
        byte[] uriBytes = Encoding.UTF8.GetBytes(uri);
        var reference = Utf8UriReference.CreateUriReference(uriBytes);
        Assert.Equal(expectedIsRelative, reference.IsRelative);
    }
}