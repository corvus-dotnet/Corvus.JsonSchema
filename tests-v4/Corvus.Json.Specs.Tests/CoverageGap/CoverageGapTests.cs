// <copyright file="CoverageGapTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Xunit;

namespace Corvus.Json.Specs.Tests.CoverageGap;

public class CoverageGapTests
{
    [Fact]
    public void AppendFragment_WhenReferenceHasNoHash_AddsHashAndFragment()
    {
        var baseRef = new JsonReference("http://example.com/path");
        var other = new JsonReference("#/foo/bar");

        JsonReference result = baseRef.AppendFragment(other);

        Assert.Equal("http://example.com/path#/foo/bar", result.ToString());
    }

    [Fact]
    public void AppendFragment_WhenReferenceHasHashButNoTrailingSlash_AppendsWithSlash()
    {
        var baseRef = new JsonReference("http://example.com/path#foo");
        var other = new JsonReference("#/bar");

        JsonReference result = baseRef.AppendFragment(other);

        Assert.Equal("http://example.com/path#foo/bar", result.ToString());
    }

    [Fact]
    public void AppendFragment_WhenOtherFragmentIsTooShort_ReturnsSelf()
    {
        var baseRef = new JsonReference("http://example.com/path#foo");
        var other = new JsonReference("#");

        JsonReference result = baseRef.AppendFragment(other);

        Assert.Equal("http://example.com/path#foo", result.ToString());
    }

    [Fact]
    public void Apply_StrictFalse_MatchingSchemes_RemovesScheme()
    {
        var baseRef = new JsonReference("http://example.com/a/b");
        var other = new JsonReference("http://example.com/c/d");

        JsonReference result = baseRef.Apply(other, strict: false);

        Assert.Equal("http://example.com/c/d", result.ToString());
    }

    [Fact]
    public void Apply_OtherHasAuthorityButNoScheme()
    {
        var baseRef = new JsonReference("http://example.com/a/b");
        var other = new JsonReference("//other.com/c/d");

        JsonReference result = baseRef.Apply(other);

        Assert.Equal("http://other.com/c/d", result.ToString());
    }

    [Fact]
    public void Apply_OtherHasEmptyPathButHasQuery()
    {
        var baseRef = new JsonReference("http://example.com/a/b?old=1");
        var other = new JsonReference("?new=2");

        JsonReference result = baseRef.Apply(other);

        Assert.Equal("http://example.com/a/b?new=2", result.ToString());
    }

    [Theory]
    [InlineData("http://example.com/a/b/../c", "http://example.com/a/c")]
    [InlineData("http://example.com/a/./b/c", "http://example.com/a/b/c")]
    [InlineData("http://example.com/a/b/./c/../d", "http://example.com/a/b/d")]
    public void Apply_WithDotSegments_ResolvesCorrectly(string other, string expected)
    {
        var baseRef = new JsonReference("http://example.com/");
        var otherRef = new JsonReference(other);

        JsonReference result = baseRef.Apply(otherRef);

        Assert.Equal(expected, result.ToString());
    }

    [Theory]
    [InlineData("/a/b/../c", "http://example.com/a/c")]
    [InlineData("/a/./b/c", "http://example.com/a/b/c")]
    [InlineData("/a/b/./c/../d", "http://example.com/a/b/d")]
    [InlineData("/../path", "http://example.com/path")]
    [InlineData("/./path", "http://example.com/path")]
    public void Apply_RelativePathWithDotSegments_ResolvesCorrectly(string other, string expected)
    {
        var baseRef = new JsonReference("http://example.com/base/file");
        var otherRef = new JsonReference(other);

        JsonReference result = baseRef.Apply(otherRef);

        Assert.Equal(expected, result.ToString());
    }

    [Fact]
    public void Apply_DotAtEnd_RemovesDot()
    {
        var baseRef = new JsonReference("http://example.com/base/file");
        var otherRef = new JsonReference("/a/b/.");

        JsonReference result = baseRef.Apply(otherRef);

        Assert.Equal("http://example.com/a/b/", result.ToString());
    }

    [Fact]
    public void Apply_DotDotAtEnd_RemovesLastSegment()
    {
        var baseRef = new JsonReference("http://example.com/base/file");
        var otherRef = new JsonReference("/a/b/..");

        JsonReference result = baseRef.Apply(otherRef);

        Assert.Equal("http://example.com/a/", result.ToString());
    }

    [Fact]
    public void MakeRelative_NormalRelativePath()
    {
        var baseRef = new JsonReference("http://example.com/a/b/c");
        var targetRef = new JsonReference("http://example.com/a/d/e");

        JsonReference result = baseRef.MakeRelative(targetRef);

        Assert.Equal("../d/e", result.ToString());
    }

    [Fact]
    public void MakeRelative_SamePath_ReturnsDefault()
    {
        var baseRef = new JsonReference("http://example.com/a/b/c");
        var targetRef = new JsonReference("http://example.com/a/b/c");

        JsonReference result = baseRef.MakeRelative(targetRef);

        Assert.Equal(string.Empty, result.ToString());
    }

    [Fact]
    public void MakeRelative_PathDiffersByFile_ReturnsDotSlash()
    {
        var baseRef = new JsonReference("http://example.com/a/b/file.json");
        var targetRef = new JsonReference("http://example.com/a/b/");

        JsonReference result = baseRef.MakeRelative(targetRef);

        Assert.Equal("./", result.ToString());
    }

    [Fact]
    public void JsonReferenceBuilder_Port_WhenNoPort_ReturnsEmpty()
    {
        var reference = new JsonReference("http://example.com/path");
        JsonReferenceBuilder builder = reference.AsBuilder();

        Assert.True(builder.Port.IsEmpty);
    }

    [Fact]
    public void JsonReferenceBuilder_Port_WhenHasPort_ReturnsPort()
    {
        var reference = new JsonReference("http://example.com:8080/path");
        JsonReferenceBuilder builder = reference.AsBuilder();

        Assert.Equal("8080", builder.Port.ToString());
    }

    [Fact]
    public void ValidationContext_PushDocumentArrayIndex_WithStackEnabled()
    {
        Corvus.Json.ValidationContext context = Corvus.Json.ValidationContext.ValidContext
            .UsingStack();

        Corvus.Json.ValidationContext result = context.PushDocumentArrayIndex(3);

        Assert.True(result.IsValid);
        Assert.True(result.IsUsingStack);
    }

    [Fact]
    public void ValidationContext_PushValidationLocationProperty_WithStackEnabled()
    {
        Corvus.Json.ValidationContext context = Corvus.Json.ValidationContext.ValidContext
            .UsingStack();

        Corvus.Json.ValidationContext result = context.PushValidationLocationProperty("myProp");

        Assert.True(result.IsValid);
        Assert.True(result.IsUsingStack);
    }

    [Fact]
    public void ValidationContext_PushValidationLocationReducedPathModifier_WithStackEnabled()
    {
        Corvus.Json.ValidationContext context = Corvus.Json.ValidationContext.ValidContext
            .UsingStack();

        Corvus.Json.ValidationContext result = context.PushValidationLocationReducedPathModifier(
            new JsonReference("#/definitions/Foo"));

        Assert.True(result.IsValid);
        Assert.True(result.IsUsingStack);
    }

    [Fact]
    public void ValidationContext_ReplaceValidationLocationReducedPathModifier_WithStackEnabled()
    {
        Corvus.Json.ValidationContext context = Corvus.Json.ValidationContext.ValidContext
            .UsingStack()
            .PushValidationLocationReducedPathModifier(new JsonReference("#/defs/First"));

        Corvus.Json.ValidationContext result = context.ReplaceValidationLocationReducedPathModifier(
            new JsonReference("#/defs/Second"));

        Assert.True(result.IsValid);
        Assert.True(result.IsUsingStack);
    }

    [Fact]
    public void ValidationContext_PushValidationLocationReducedPathModifierAndProperty_WithStackEnabled()
    {
        Corvus.Json.ValidationContext context = Corvus.Json.ValidationContext.ValidContext
            .UsingStack();

        Corvus.Json.ValidationContext result = context.PushValidationLocationReducedPathModifierAndProperty(
            new JsonReference("#/properties"),
            "name");

        Assert.True(result.IsValid);
        Assert.True(result.IsUsingStack);
    }

    [Fact]
    public void ValidationContext_PushDocumentArrayIndex_WithoutStackEnabled_ReturnsSelf()
    {
        Corvus.Json.ValidationContext context = Corvus.Json.ValidationContext.ValidContext;

        Corvus.Json.ValidationContext result = context.PushDocumentArrayIndex(3);

        Assert.True(result.IsValid);
        Assert.False(result.IsUsingStack);
    }

    [Fact]
    public void TypeDecimal_WithOverflowNumber_ReturnsInvalid_DetailedLevel()
    {
        string json = "79228162514264337593543950336";
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonNumber number = new JsonNumber(doc.RootElement.Clone());

        Corvus.Json.ValidationContext result = ValidateWithoutCoreType.TypeDecimal(
            number,
            Corvus.Json.ValidationContext.ValidContext.UsingResults(),
            ValidationLevel.Detailed);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void TypeDecimal_WithOverflowNumber_ReturnsInvalid_BasicLevel()
    {
        string json = "79228162514264337593543950336";
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonNumber number = new JsonNumber(doc.RootElement.Clone());

        Corvus.Json.ValidationContext result = ValidateWithoutCoreType.TypeDecimal(
            number,
            Corvus.Json.ValidationContext.ValidContext.UsingResults(),
            ValidationLevel.Basic);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void TypeDecimal_WithOverflowNumber_ReturnsInvalid_FlagLevel()
    {
        string json = "79228162514264337593543950336";
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonNumber number = new JsonNumber(doc.RootElement.Clone());

        Corvus.Json.ValidationContext result = ValidateWithoutCoreType.TypeDecimal(
            number,
            Corvus.Json.ValidationContext.ValidContext,
            ValidationLevel.Flag);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void TypeDecimal_WithValidNumber_ReturnsValid()
    {
        string json = "123.456";
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonNumber number = new JsonNumber(doc.RootElement.Clone());

        Corvus.Json.ValidationContext result = ValidateWithoutCoreType.TypeDecimal(
            number,
            Corvus.Json.ValidationContext.ValidContext,
            ValidationLevel.Verbose);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("P1Y2M3D", 1, 2, 3)]
    [InlineData("P0D", 0, 0, 0)]
    [InlineData("P1Y", 1, 0, 0)]
    [InlineData("P6M", 0, 6, 0)]
    [InlineData("P10D", 0, 0, 10)]
    public void Period_Parse_ValidDuration(string input, int years, int months, int days)
    {
        Period result = Period.Parse(input);

        Assert.Equal(years, result.Years);
        Assert.Equal(months, result.Months);
        Assert.Equal(days, result.Days);
    }

    [Theory]
    [InlineData("PT1H2M3S", 1, 2, 3)]
    [InlineData("PT0S", 0, 0, 0)]
    public void Period_Parse_WithTimeComponents(string input, int hours, int minutes, int seconds)
    {
        Period result = Period.Parse(input);

        Assert.Equal(hours, result.Hours);
        Assert.Equal(minutes, result.Minutes);
        Assert.Equal(seconds, result.Seconds);
    }

    [Fact]
    public void FileSystemDocumentResolver_Dispose_DoesNotThrow()
    {
        var resolver = new FileSystemDocumentResolver();
        resolver.Dispose();
    }

    [Fact]
    public async Task FileSystemDocumentResolver_Reset_ClearsDocuments()
    {
        var resolver = new FileSystemDocumentResolver();

        string testSchemaPath = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "JSON-Schema-Test-Suite", "remotes", "integer.json"));

        if (File.Exists(testSchemaPath))
        {
            string uri = new Uri(testSchemaPath).AbsoluteUri;
            JsonElement? resolved = await resolver.TryResolve(new JsonReference(uri));
            Assert.NotNull(resolved);

            resolver.Reset();
        }

        resolver.Dispose();
    }

    [Fact]
    public void Apply_RelativePathMergedWithBase()
    {
        var baseRef = new JsonReference("http://example.com/a/b/c");
        var otherRef = new JsonReference("d");

        JsonReference result = baseRef.Apply(otherRef);

        Assert.Equal("http://example.com/a/b/d", result.ToString());
    }

    [Fact]
    public void Apply_RelativePathWithDotDot()
    {
        var baseRef = new JsonReference("http://example.com/a/b/c");
        var otherRef = new JsonReference("../d");

        JsonReference result = baseRef.Apply(otherRef);

        Assert.Equal("http://example.com/a/d", result.ToString());
    }

    [Fact]
    public void Apply_EmptyPathEmptyQuery_UsesBasePathAndQuery()
    {
        var baseRef = new JsonReference("http://example.com/a/b?q=1");
        var otherRef = new JsonReference("#frag");

        JsonReference result = baseRef.Apply(otherRef);

        Assert.Equal("http://example.com/a/b?q=1#frag", result.ToString());
    }
}