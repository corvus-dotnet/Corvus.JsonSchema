// <copyright file="Utf8UriMakeRelativeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for making relative URI references from absolute URI references.
/// </summary>
public class Utf8UriMakeRelativeTests
{
    private static void AssertRelativeUriEquals(string expected, Utf8UriReference actual)
    {
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        Assert.True(actual.OriginalUriReference.SequenceEqual(expectedBytes),
            $"Expected: {expected}, Actual: {JsonReaderHelper.GetTextFromUtf8(actual.OriginalUriReference)}");
    }

    // Make relative references from absolute URIs
    [Theory]
    [InlineData("https://endjin.com/home/user/test.json", "https://endjin.com/home/user/test.json", "")]
    [InlineData("https://endjin.com/home/user/test.json", "https://endjin.com/home/user/test.json#/$defs/TestType", "#/$defs/TestType")]
    [InlineData("https://endjin.com/home/user/test.json", "https://endjin.com/home/user/other.json", "other.json")]
    [InlineData("https://endjin.com/home/user/test.json", "https://endjin.com/home/user/other.json#/$defs/TestType", "other.json#/$defs/TestType")]
    [InlineData("https://endjin.com/home/user/test.json", "https://endjin.com/home/other/file.json", "../other/file.json")]
    [InlineData("https://endjin.com/schema.json", "https://endjin.com/schema.json#/$defs/Type", "#/$defs/Type")]
    [InlineData("https://endjin.com/path/to/schema.json", "https://endjin.com/path/to/schema.json#/properties/name", "#/properties/name")]
    public void Utf8Uri_MakeRelative_AbsoluteUris(string baseUri, string targetUri, string expectedRelative)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetUri);
        byte[] buffer = new byte[2048];

        var baseUriObj = Utf8Uri.CreateUri(baseBytes);
        var targetUriObj = Utf8Uri.CreateUri(targetBytes);

        bool success = baseUriObj.TryMakeRelative(targetUriObj, buffer, out Utf8UriReference result);

        Assert.True(success);
        AssertRelativeUriEquals(expectedRelative, result);
    }

    // Make relative references from file URIs (Windows-style)
    [Theory]
    [InlineData("file:///C:/Users/test/schema.json", "file:///C:/Users/test/schema.json", "")]
    [InlineData("file:///C:/Users/test/schema.json", "file:///C:/Users/test/schema.json#/$defs/TestType", "#/$defs/TestType")]
    [InlineData("file:///C:/Users/test/schema.json", "file:///C:/Users/test/other.json", "other.json")]
    [InlineData("file:///C:/Users/test/schema.json", "file:///C:/Users/test/other.json#/$defs/TestType", "other.json#/$defs/TestType")]
    [InlineData("file:///C:/Users/test/schema.json", "file:///C:/Users/other/file.json", "../other/file.json")]
    public void Utf8Uri_MakeRelative_FileUris(string baseUri, string targetUri, string expectedRelative)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetUri);
        byte[] buffer = new byte[2048];

        var baseUriObj = Utf8Uri.CreateUri(baseBytes);
        var targetUriObj = Utf8Uri.CreateUri(targetBytes);

        bool success = baseUriObj.TryMakeRelative(targetUriObj, buffer, out Utf8UriReference result);

        Assert.True(success);
        AssertRelativeUriEquals(expectedRelative, result);
    }

    // Test cases that should return full target URI when scheme/host/port don't match
    [Theory]
    [InlineData("https://endjin.com/path/test.json", "http://endjin.com/path/test.json")] // Different scheme
    [InlineData("https://endjin.com/path/test.json", "https://example.com/path/test.json")] // Different host
    [InlineData("https://endjin.com:8080/path/test.json", "https://endjin.com/path/test.json")] // Different port
    public void Utf8Uri_MakeRelative_DifferentAuthority_ReturnsFullUri(string baseUri, string targetUri)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetUri);
        byte[] buffer = new byte[2048];

        var baseUriObj = Utf8Uri.CreateUri(baseBytes);
        var targetUriObj = Utf8Uri.CreateUri(targetBytes);

        bool success = baseUriObj.TryMakeRelative(targetUriObj, buffer, out Utf8UriReference result);

        Assert.True(success);
        Assert.Equal(targetUri, result.ToString());
    }

    // Test colon handling in first path segment (RFC 3986, Section 4.2)
    [Theory]
    [InlineData("https://endjin.com/path/", "https://endjin.com/path/c:test", "./c:test")]
    [InlineData("https://endjin.com/path/", "https://endjin.com/path/file:name", "./file:name")]
    public void Utf8Uri_MakeRelative_ColonInFirstSegment_PrependsCurrentDirectory(string baseUri, string targetUri, string expectedRelative)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetUri);
        byte[] buffer = new byte[2048];

        var baseUriObj = Utf8Uri.CreateUri(baseBytes);
        var targetUriObj = Utf8Uri.CreateUri(targetBytes);

        bool success = baseUriObj.TryMakeRelative(targetUriObj, buffer, out Utf8UriReference result);

        Assert.True(success);
        AssertRelativeUriEquals(expectedRelative, result);
    }

    // Additional path difference scenarios
    [Theory]
    [InlineData("https://endjin.com/a/b/c/d/test.json", "https://endjin.com/a/b/c/d/e/other.json", "e/other.json")]
    [InlineData("https://endjin.com/a/b/c/d/test.json", "https://endjin.com/a/b/other.json", "../../other.json")]
    [InlineData("https://endjin.com/a/b/c/d/test.json", "https://endjin.com/a/other.json", "../../../other.json")]
    [InlineData("https://endjin.com/a/b/test.json", "https://endjin.com/x/y/other.json", "../../x/y/other.json")]
    public void Utf8Uri_MakeRelative_VariousPathDifferences(string baseUri, string targetUri, string expectedRelative)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetUri);
        byte[] buffer = new byte[2048];

        var baseUriObj = Utf8Uri.CreateUri(baseBytes);
        var targetUriObj = Utf8Uri.CreateUri(targetBytes);

        bool success = baseUriObj.TryMakeRelative(targetUriObj, buffer, out Utf8UriReference result);

        Assert.True(success);
        AssertRelativeUriEquals(expectedRelative, result);
    }

    // Test with query strings
    [Theory]
    [InlineData("https://endjin.com/path/test.json", "https://endjin.com/path/other.json?query=value", "other.json?query=value")]
    [InlineData("https://endjin.com/path/test.json?q1=v1", "https://endjin.com/path/other.json?q2=v2", "other.json?q2=v2")]
    public void Utf8Uri_MakeRelative_WithQueryStrings(string baseUri, string targetUri, string expectedRelative)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetUri);
        byte[] buffer = new byte[2048];

        var baseUriObj = Utf8Uri.CreateUri(baseBytes);
        var targetUriObj = Utf8Uri.CreateUri(targetBytes);

        bool success = baseUriObj.TryMakeRelative(targetUriObj, buffer, out Utf8UriReference result);

        Assert.True(success);
        AssertRelativeUriEquals(expectedRelative, result);
    }

    // Test root paths
    [Theory]
    [InlineData("https://endjin.com/test.json", "https://endjin.com/other.json", "other.json")]
    [InlineData("https://endjin.com/a/test.json", "https://endjin.com/other.json", "../other.json")]
    public void Utf8Uri_MakeRelative_RootPaths(string baseUri, string targetUri, string expectedRelative)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetUri);
        byte[] buffer = new byte[2048];

        var baseUriObj = Utf8Uri.CreateUri(baseBytes);
        var targetUriObj = Utf8Uri.CreateUri(targetBytes);

        bool success = baseUriObj.TryMakeRelative(targetUriObj, buffer, out Utf8UriReference result);

        Assert.True(success);
        AssertRelativeUriEquals(expectedRelative, result);
    }

    // Test case insensitivity for scheme and host
    [Theory]
    [InlineData("HTTPS://ENDJIN.COM/path/test.json", "https://endjin.com/path/other.json", "other.json")]
    [InlineData("https://endjin.com/path/test.json", "HTTPS://ENDJIN.COM/path/other.json", "other.json")]
    public void Utf8Uri_MakeRelative_CaseInsensitiveSchemeAndHost(string baseUri, string targetUri, string expectedRelative)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetUri);
        byte[] buffer = new byte[2048];

        var baseUriObj = Utf8Uri.CreateUri(baseBytes);
        var targetUriObj = Utf8Uri.CreateUri(targetBytes);

        bool success = baseUriObj.TryMakeRelative(targetUriObj, buffer, out Utf8UriReference result);

        Assert.True(success);
        AssertRelativeUriEquals(expectedRelative, result);
    }

    // Test with ports
    [Theory]
    [InlineData("https://endjin.com:443/path/test.json", "https://endjin.com:443/path/other.json", "other.json")]
    [InlineData("http://endjin.com:8080/path/test.json", "http://endjin.com:8080/path/other.json", "other.json")]
    public void Utf8Uri_MakeRelative_WithExplicitPorts(string baseUri, string targetUri, string expectedRelative)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetUri);
        byte[] buffer = new byte[2048];

        var baseUriObj = Utf8Uri.CreateUri(baseBytes);
        var targetUriObj = Utf8Uri.CreateUri(targetBytes);

        bool success = baseUriObj.TryMakeRelative(targetUriObj, buffer, out Utf8UriReference result);

        Assert.True(success);
        AssertRelativeUriEquals(expectedRelative, result);
    }

    // Test URIs with single-letter schemes (like C:, D:) - these are valid URIs with scheme C or D, not Windows paths
    [Theory]
    [InlineData("c:/path/to/test.json", "c:/path/to/test.json", "")]
    [InlineData("c:/path/to/test.json", "c:/path/to/test.json#/$defs/TestType", "#/$defs/TestType")]
    [InlineData("c:/path/to/test.json", "c:/path/to/other.json", "other.json")]
    [InlineData("c:/path/to/test.json", "c:/path/other/file.json", "../other/file.json")]
    [InlineData("d:/documents/test.json", "d:/documents/other.json", "other.json")]
    public void Utf8Uri_MakeRelative_SingleLetterSchemes(string baseUri, string targetUri, string expectedRelative)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetUri);
        byte[] buffer = new byte[2048];

        var baseUriObj = Utf8Uri.CreateUri(baseBytes);
        var targetUriObj = Utf8Uri.CreateUri(targetBytes);

        bool success = baseUriObj.TryMakeRelative(targetUriObj, buffer, out Utf8UriReference result);

        Assert.True(success);
        AssertRelativeUriEquals(expectedRelative, result);
    }

    // Single-letter schemes don't match file:// URIs
    [Theory]
    [InlineData("c:/path/test.json", "file:///C:/path/test.json")]
    [InlineData("file:///C:/path/test.json", "c:/path/test.json")]
    public void Utf8Uri_MakeRelative_SingleLetterSchemeVsFile_ReturnsFullUri(string baseUri, string targetUri)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetUri);
        byte[] buffer = new byte[2048];

        var baseUriObj = Utf8Uri.CreateUri(baseBytes);
        var targetUriObj = Utf8Uri.CreateUri(targetBytes);

        bool success = baseUriObj.TryMakeRelative(targetUriObj, buffer, out Utf8UriReference result);

        Assert.True(success);
        Assert.Equal(targetUri, result.ToString());
    }

    // Test handling of non-canonical paths (with . and .. segments)
    [Theory]
    [InlineData("https://example.com/a/b/c/test.json", "https://example.com/a/b/./c/other.json", "other.json")]
    [InlineData("https://example.com/a/b/c/test.json", "https://example.com/a/b/c/../c/other.json", "other.json")]
    [InlineData("https://example.com/a/./b/c/test.json", "https://example.com/a/b/c/other.json", "other.json")]
    [InlineData("https://example.com/a/b/../b/c/test.json", "https://example.com/a/b/c/other.json", "other.json")]
    [InlineData("https://example.com/a/b/./c/test.json", "https://example.com/a/b/c/./other.json", "other.json")]
    public void Utf8Uri_MakeRelative_NonCanonicalPaths(string baseUri, string targetUri, string expectedRelative)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetUri);
        byte[] buffer = new byte[2048];

        var baseUriObj = Utf8Uri.CreateUri(baseBytes);
        var targetUriObj = Utf8Uri.CreateUri(targetBytes);

        bool success = baseUriObj.TryMakeRelative(targetUriObj, buffer, out Utf8UriReference result);

        Assert.True(success);
        AssertRelativeUriEquals(expectedRelative, result);
    }

    // Test handling of paths that require percent-encoding normalization
    [Theory]
    [InlineData("https://example.com/a/b/test.json", "https://example.com/a/b/t%C3%ABst.json", "t%C3%ABst.json")] // %C3%AB stays encoded for URI
    [InlineData("https://example.com/t%C3%ABst/a/file.json", "https://example.com/t%C3%ABst/a/other.json", "other.json")]
    [InlineData("https://example.com/a/t%C3%ABst/file.json", "https://example.com/a/t%C3%ABst/%C3%B6ther.json", "%C3%B6ther.json")] // %C3%B6 stays encoded for URI
    [InlineData("https://example.com/a/b%20c/test.json", "https://example.com/a/b%20c/other.json", "other.json")] // %20 (space) stays encoded
    [InlineData("https://example.com/a/b%7Ec/test.json", "https://example.com/a/b~c/other.json", "other.json")] // %7E decoded to ~ (unreserved)
    public void Utf8Uri_MakeRelative_PercentEncodingInPaths(string baseUri, string targetUri, string expectedRelative)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseUri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetUri);
        byte[] buffer = new byte[2048];
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedRelative);

        var baseUriObj = Utf8Uri.CreateUri(baseBytes);
        var targetUriObj = Utf8Uri.CreateUri(targetBytes);

        bool success = baseUriObj.TryMakeRelative(targetUriObj, buffer, out Utf8UriReference result);

        Assert.True(success);
        Assert.True(result.OriginalUriReference.SequenceEqual(expectedBytes),
            $"Expected: {expectedRelative}, Actual: {JsonReaderHelper.GetTextFromUtf8(result.OriginalUriReference)}");
    }
}