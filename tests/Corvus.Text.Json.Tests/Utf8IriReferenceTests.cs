// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Xunit;

namespace Corvus.Text.Json.Tests;

public static partial class Utf8IriReferenceTests
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
    public static void CreateIri_ValidIris_ReturnsUtf8IriReference(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
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

        ArgumentException exception = Assert.Throws<ArgumentException>(() => Utf8IriReference.CreateIriReference(iriBytes));
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
    public static void TryCreateIri_ValidIris_ReturnsTrueAndValidReference(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);

        Assert.True(result);
        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [Theory]
    [InlineData("http://[invalid-ipv6")]
    [InlineData("http://example.com:99999")]
    [InlineData("ht tp://example.com")]
    [InlineData("\x01\x02\x03")]
    [InlineData("http://example.com/path with spaces")]
    public static void TryCreateIri_InvalidIris_ReturnsFalse(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);

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
    public static void ComponentExtraction_AbsoluteIris_ExtractsCorrectly(string iri, string expectedScheme, string expectedAuthority, string expectedUser, string expectedHost, string expectedPort, string expectedPath, string expectedQuery, string expectedFragment)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8IriReference.CreateIriReference(iriBytes);

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
    public static void ComponentExtraction_RelativeIris_ExtractsCorrectly(string iri, string expectedScheme, string expectedAuthority, string expectedUser, string expectedHost, string expectedPort, string expectedPath, string expectedQuery, string expectedFragment)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8IriReference.CreateIriReference(iriBytes);

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
    public static void HasProperties_VariousIris_ReturnsCorrectValues(string iri, bool hasScheme, bool hasAuthority, bool hasUser, bool hasHost, bool hasPath, bool hasQuery, bool hasFragment)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8IriReference.CreateIriReference(iriBytes);

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
    public static void PortValue_VariousIris_ReturnsCorrectPortAndDefaultFlag(string iri, int expectedPort, bool expectedIsDefault)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8IriReference.CreateIriReference(iriBytes);

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
    public static void IsRelative_VariousIris_ReturnsCorrectValue(string iri, bool expectedIsRelative)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8IriReference.CreateIriReference(iriBytes);

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
    public static void GetUri_ValidIris_ReturnsEquivalentUri(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Uri systemUri = reference.GetUri();
        var expectedUri = new Uri(iri, UriKind.RelativeOrAbsolute);

        Assert.Equal(expectedUri.ToString(), systemUri.ToString());
        Assert.Equal(expectedUri.IsAbsoluteUri, systemUri.IsAbsoluteUri);
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

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
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

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [Theory]
    [InlineData("urn:isbn:0451450523")]
    [InlineData("news:comp.lang.ada")]
    [InlineData("tel:+1-816-555-1212")]
    [InlineData("ldap://[2001:db8::7]/c=GB?objectClass?one")]
    public static void CreateIri_NonHttpSchemes_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [Fact]
    public static void CreateIri_EmptySpan_HandlesCorrectly()
    {
        ReadOnlySpan<byte> emptySpan = ReadOnlySpan<byte>.Empty;

        var reference = Utf8IriReference.CreateIriReference(emptySpan);

        Assert.True(reference.IsValid);
        Assert.True(reference.OriginalIriReference.IsEmpty);
        Assert.True(reference.IsRelative);
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

        Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [Fact]
    public static void CreateIri_LargeUri_HandlesCorrectly()
    {
        string largePathSegment = new('a', 1000);
        string largeUri = $"http://example.com/{largePathSegment}";
        byte[] iriBytes = Encoding.UTF8.GetBytes(largeUri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
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

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [Theory]
    [InlineData("http://example.com:1")]
    [InlineData("http://example.com:65535")]
    [InlineData("http://example.com:0")]
    public static void CreateIri_BoundaryPortValues_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("ab", true)]
    [InlineData("abc", true)]
    public static void CreateIri_MinimalSchemes_HandlesCorrectly(string scheme, bool handlesCorrectly)
    {
        string iri = $"{scheme}:path";
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);

        Assert.Equal(reference.IsValid, handlesCorrectly);
        if (handlesCorrectly)
        {
            Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
        }
    }

    [Theory]
    [InlineData("scheme:")]
    [InlineData("scheme:/")]
    [InlineData("scheme://")]
    [InlineData("scheme:///")]
    public static void ComponentBoundariesIri_MinimalComponents_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [Fact]
    public static void ComponentAccessIri_DefaultReference_ReturnsEmptySpans()
    {
        Utf8IriReference reference = default;

        Assert.True(reference.Scheme.IsEmpty);
        Assert.True(reference.Authority.IsEmpty);
        Assert.True(reference.User.IsEmpty);
        Assert.True(reference.Host.IsEmpty);
        Assert.True(reference.Port.IsEmpty);
        Assert.True(reference.Path.IsEmpty);
        Assert.True(reference.Query.IsEmpty);
        Assert.True(reference.Fragment.IsEmpty);
        Assert.True(reference.OriginalIriReference.IsEmpty);
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
    public static void CreateIri_EdgeCaseIris_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8IriReference.CreateIriReference(iriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
        }
        else
        {
            bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);
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
    public static void CreateIri_PercentEncodingEdgeCases_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8IriReference.CreateIriReference(iriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
        }
        else
        {
            bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);
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
    public static void CreateIri_PathAndQueryEdgeCases_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8IriReference.CreateIriReference(iriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
        }
        else
        {
            bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);
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
    public static void CreateIri_IpAddressEdgeCases_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8IriReference.CreateIriReference(iriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
        }
        else
        {
            bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);
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
    public static void CreateIri_WhitespaceAndControlCharacters_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8IriReference.CreateIriReference(iriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
        }
        else
        {
            bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);
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
    public static void ComponentExtractionIri_UserInfoVariations_ExtractsCorrectly(string iri, string expectedUserInfo, string expectedHost)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8IriReference.CreateIriReference(iriBytes);

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
    public static void CreateIri_CaseVariations_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [Theory]
    [InlineData("http://example.com", new byte[] { })] // Empty path
    [InlineData("http://example.com/", new byte[] { (byte)'/' })] // Root path
    [InlineData("http://example.com/path", new byte[] { (byte)'/', (byte)'p', (byte)'a', (byte)'t', (byte)'h' })]
    [InlineData("?query", new byte[] { })] // Query-only relative URI
    [InlineData("#fragment", new byte[] { })] // Fragment-only relative URI
    public static void ComponentExtractionIri_PathVariations_ExtractsCorrectly(string iri, byte[] expectedPath)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.Equal(expectedPath, reference.Path.ToArray());
    }

    [Theory]
    [InlineData("http://example.com?")]
    [InlineData("http://example.com#")]
    [InlineData("http://example.com/?")]
    [InlineData("http://example.com/#")]
    [InlineData("path?")]
    [InlineData("path#")]
    public static void CreateIri_EmptyQueryAndFragment_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [Fact]
    public static void CreateIri_VeryLongUri_HandlesCorrectly()
    {
        string baseUri = "http://example.com/";
        string longPath = new('a', 4000); // Very long path
        string iri = baseUri + longPath;
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
    }

    [Theory]
    [InlineData("data:text/plain;base64,SGVsbG8sIFdvcmxkIQ==")]
    [InlineData("blob:http://example.com/abc-123")]
    [InlineData("about:blank")]
    [InlineData("javascript:void(0)")]
    public static void CreateIri_SpecialSchemes_HandlesCorrectly(string iri)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        var reference = Utf8IriReference.CreateIriReference(iriBytes);

        Assert.True(reference.IsValid);
        Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
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
    public static void CreateIri_UncFileIris_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8IriReference.CreateIriReference(iriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
        }
        else
        {
            bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);
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
    public static void CreateIri_HttpSchemeRelativeIris_HandlesCorrectly(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8IriReference.CreateIriReference(iriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
        }
        else
        {
            bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);
            Assert.False(result);
            Assert.False(reference.IsValid);
        }
    }

    [Theory]
    [InlineData("c:\\my\\file.txt", false)]
    [InlineData("c:/my/file.txt", true)]
    [InlineData("c:/my/file.txt#fragment", true)]
    [InlineData("c:\\my\\file.txt#fragment", false)]
    public static void CreateIri_Implicit_File(string iri, bool shouldBeValid)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);

        if (shouldBeValid)
        {
            var reference = Utf8IriReference.CreateIriReference(iriBytes);
            Assert.True(reference.IsValid);
            Assert.Equal(iriBytes, reference.OriginalIriReference.ToArray());
        }
        else
        {
            bool result = Utf8IriReference.TryCreateIriReference(iriBytes, out Utf8IriReference reference);
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
    public static void IsRelativeIri_Validation(string iri, bool expectedIsRelative)
    {
        byte[] iriBytes = Encoding.UTF8.GetBytes(iri);
        var reference = Utf8IriReference.CreateIriReference(iriBytes);
        Assert.Equal(expectedIsRelative, reference.IsRelative);
    }
}