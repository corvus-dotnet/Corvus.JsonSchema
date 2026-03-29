// <copyright file="Utf8IriMakeRelativeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for making relative IRI references from absolute IRI references.
/// </summary>
public class Utf8IriMakeRelativeTests
{
    // Make relative references from absolute IRIs
    [Theory]
    [InlineData("https://endjin.com/home/user/test.json", "https://endjin.com/home/user/test.json", "")]
    [InlineData("https://endjin.com/home/user/test.json", "https://endjin.com/home/user/test.json#/$defs/TestType", "#/$defs/TestType")]
    [InlineData("https://endjin.com/home/user/test.json", "https://endjin.com/home/user/other.json", "other.json")]
    [InlineData("https://endjin.com/home/user/test.json", "https://endjin.com/home/user/other.json#/$defs/TestType", "other.json#/$defs/TestType")]
    [InlineData("https://endjin.com/home/user/test.json", "https://endjin.com/home/other/file.json", "../other/file.json")]
    [InlineData("https://endjin.com/schema.json", "https://endjin.com/schema.json#/$defs/Type", "#/$defs/Type")]
    [InlineData("https://endjin.com/path/to/schema.json", "https://endjin.com/path/to/schema.json#/properties/name", "#/properties/name")]
    public void Utf8Iri_MakeRelative_AbsoluteIris(string baseIri, string targetIri, string expectedRelative)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseIri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetIri);
        byte[] buffer = new byte[2048];

        var baseIriObj = Utf8Iri.CreateIri(baseBytes);
        var targetIriObj = Utf8Iri.CreateIri(targetBytes);

        bool success = baseIriObj.TryMakeRelative(targetIriObj, buffer, out Utf8IriReference result);

        Assert.True(success);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedRelative); Assert.True(result.OriginalIriReference.SequenceEqual(expectedBytes), $"Expected: {expectedRelative}, Actual: {JsonReaderHelper.GetTextFromUtf8(result.OriginalIriReference)}");
    }

    // Test with non-ASCII characters (IRIs support Unicode)
    [Theory]
    [InlineData("http://example.com/données/test.json", "http://example.com/données/test.json", "")]
    [InlineData("http://example.com/données/test.json", "http://example.com/données/other.json", "other.json")]
    [InlineData("http://example.com/données/test.json", "http://example.com/données/other.json#/$defs/Type", "other.json#/$defs/Type")]
    [InlineData("http://例え.jp/パス/test.json", "http://例え.jp/パス/other.json", "other.json")]
    public void Utf8Iri_MakeRelative_WithNonAsciiCharacters(string baseIri, string targetIri, string expectedRelative)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseIri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetIri);
        byte[] buffer = new byte[2048];

        var baseIriObj = Utf8Iri.CreateIri(baseBytes);
        var targetIriObj = Utf8Iri.CreateIri(targetBytes);

        bool success = baseIriObj.TryMakeRelative(targetIriObj, buffer, out Utf8IriReference result);

        Assert.True(success);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedRelative); Assert.True(result.OriginalIriReference.SequenceEqual(expectedBytes), $"Expected: {expectedRelative}, Actual: {JsonReaderHelper.GetTextFromUtf8(result.OriginalIriReference)}");
    }

    // Test cases that should return full target IRI when scheme/host/port don't match
    [Theory]
    [InlineData("https://endjin.com/path/test.json", "http://endjin.com/path/test.json")] // Different scheme
    [InlineData("https://endjin.com/path/test.json", "https://example.com/path/test.json")] // Different host
    [InlineData("https://endjin.com:8080/path/test.json", "https://endjin.com/path/test.json")] // Different port
    public void Utf8Iri_MakeRelative_DifferentAuthority_ReturnsFullIri(string baseIri, string targetIri)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseIri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetIri);
        byte[] buffer = new byte[2048];

        var baseIriObj = Utf8Iri.CreateIri(baseBytes);
        var targetIriObj = Utf8Iri.CreateIri(targetBytes);

        bool success = baseIriObj.TryMakeRelative(targetIriObj, buffer, out Utf8IriReference result);

        Assert.True(success);
        Assert.Equal(targetIri, result.ToString());
    }

    // Test with query strings
    [Theory]
    [InlineData("https://endjin.com/path/test.json", "https://endjin.com/path/other.json?query=value", "other.json?query=value")]
    [InlineData("https://endjin.com/path/test.json?q1=v1", "https://endjin.com/path/other.json?q2=v2", "other.json?q2=v2")]
    public void Utf8Iri_MakeRelative_WithQueryStrings(string baseIri, string targetIri, string expectedRelative)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseIri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetIri);
        byte[] buffer = new byte[2048];

        var baseIriObj = Utf8Iri.CreateIri(baseBytes);
        var targetIriObj = Utf8Iri.CreateIri(targetBytes);

        bool success = baseIriObj.TryMakeRelative(targetIriObj, buffer, out Utf8IriReference result);

        Assert.True(success);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedRelative); Assert.True(result.OriginalIriReference.SequenceEqual(expectedBytes), $"Expected: {expectedRelative}, Actual: {JsonReaderHelper.GetTextFromUtf8(result.OriginalIriReference)}");
    }

    // Test TryMakeRelative with Utf8Uri target
    [Theory]
    [InlineData("https://endjin.com/home/user/test.json", "https://endjin.com/home/user/other.json", "other.json")]
    [InlineData("https://endjin.com/home/user/test.json", "https://endjin.com/home/other/file.json", "../other/file.json")]
    public void Utf8Iri_MakeRelative_WithUtf8UriTarget(string baseIri, string targetUri, string expectedRelative)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseIri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetUri);
        byte[] buffer = new byte[2048];

        var baseIriObj = Utf8Iri.CreateIri(baseBytes);
        var targetUriObj = Utf8Uri.CreateUri(targetBytes);

        bool success = baseIriObj.TryMakeRelative(targetUriObj, buffer, out Utf8IriReference result);

        Assert.True(success);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedRelative); Assert.True(result.OriginalIriReference.SequenceEqual(expectedBytes), $"Expected: {expectedRelative}, Actual: {JsonReaderHelper.GetTextFromUtf8(result.OriginalIriReference)}");
    }

    // Additional path difference scenarios
    [Theory]
    [InlineData("https://endjin.com/a/b/c/d/test.json", "https://endjin.com/a/b/c/d/e/other.json", "e/other.json")]
    [InlineData("https://endjin.com/a/b/c/d/test.json", "https://endjin.com/a/b/other.json", "../../other.json")]
    [InlineData("https://endjin.com/a/b/c/d/test.json", "https://endjin.com/a/other.json", "../../../other.json")]
    [InlineData("https://endjin.com/a/b/test.json", "https://endjin.com/x/y/other.json", "../../x/y/other.json")]
    public void Utf8Iri_MakeRelative_VariousPathDifferences(string baseIri, string targetIri, string expectedRelative)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseIri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetIri);
        byte[] buffer = new byte[2048];

        var baseIriObj = Utf8Iri.CreateIri(baseBytes);
        var targetIriObj = Utf8Iri.CreateIri(targetBytes);

        bool success = baseIriObj.TryMakeRelative(targetIriObj, buffer, out Utf8IriReference result);

        Assert.True(success);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedRelative); Assert.True(result.OriginalIriReference.SequenceEqual(expectedBytes), $"Expected: {expectedRelative}, Actual: {JsonReaderHelper.GetTextFromUtf8(result.OriginalIriReference)}");
    }

    // Test case insensitivity for scheme and host
    [Theory]
    [InlineData("HTTPS://ENDJIN.COM/path/test.json", "https://endjin.com/path/other.json", "other.json")]
    [InlineData("https://endjin.com/path/test.json", "HTTPS://ENDJIN.COM/path/other.json", "other.json")]
    public void Utf8Iri_MakeRelative_CaseInsensitiveSchemeAndHost(string baseIri, string targetIri, string expectedRelative)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseIri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetIri);
        byte[] buffer = new byte[2048];

        var baseIriObj = Utf8Iri.CreateIri(baseBytes);
        var targetIriObj = Utf8Iri.CreateIri(targetBytes);

        bool success = baseIriObj.TryMakeRelative(targetIriObj, buffer, out Utf8IriReference result);

        Assert.True(success);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedRelative); Assert.True(result.OriginalIriReference.SequenceEqual(expectedBytes), $"Expected: {expectedRelative}, Actual: {JsonReaderHelper.GetTextFromUtf8(result.OriginalIriReference)}");
    }

    // Test root paths
    [Theory]
    [InlineData("https://endjin.com/test.json", "https://endjin.com/other.json", "other.json")]
    [InlineData("https://endjin.com/a/test.json", "https://endjin.com/other.json", "../other.json")]
    public void Utf8Iri_MakeRelative_RootPaths(string baseIri, string targetIri, string expectedRelative)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseIri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetIri);
        byte[] buffer = new byte[2048];

        var baseIriObj = Utf8Iri.CreateIri(baseBytes);
        var targetIriObj = Utf8Iri.CreateIri(targetBytes);

        bool success = baseIriObj.TryMakeRelative(targetIriObj, buffer, out Utf8IriReference result);

        Assert.True(success);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedRelative); Assert.True(result.OriginalIriReference.SequenceEqual(expectedBytes), $"Expected: {expectedRelative}, Actual: {JsonReaderHelper.GetTextFromUtf8(result.OriginalIriReference)}");
    }

    // Test colon handling in first path segment (RFC 3986, Section 4.2)
    [Theory]
    [InlineData("https://endjin.com/path/", "https://endjin.com/path/c:test", "./c:test")]
    [InlineData("https://endjin.com/path/", "https://endjin.com/path/file:name", "./file:name")]
    public void Utf8Iri_MakeRelative_ColonInFirstSegment_PrependsCurrentDirectory(string baseIri, string targetIri, string expectedRelative)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseIri);
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetIri);
        byte[] buffer = new byte[2048];

        var baseIriObj = Utf8Iri.CreateIri(baseBytes);
        var targetIriObj = Utf8Iri.CreateIri(targetBytes);

        bool success = baseIriObj.TryMakeRelative(targetIriObj, buffer, out Utf8IriReference result);

        Assert.True(success);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedRelative); Assert.True(result.OriginalIriReference.SequenceEqual(expectedBytes), $"Expected: {expectedRelative}, Actual: {JsonReaderHelper.GetTextFromUtf8(result.OriginalIriReference)}");
    }
}