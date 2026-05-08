// <copyright file="CoverageGapTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.CoverageGap;

[TestClass]
public class CoverageGapTests
{
    [TestMethod]
    public void AppendFragment_WhenReferenceHasNoHash_AddsHashAndFragment()
    {
        var baseRef = new JsonReference("http://example.com/path");
        var other = new JsonReference("#/foo/bar");

        JsonReference result = baseRef.AppendFragment(other);

        Assert.AreEqual("http://example.com/path#/foo/bar", result.ToString());
    }

    [TestMethod]
    public void AppendFragment_WhenReferenceHasHashButNoTrailingSlash_AppendsWithSlash()
    {
        var baseRef = new JsonReference("http://example.com/path#foo");
        var other = new JsonReference("#/bar");

        JsonReference result = baseRef.AppendFragment(other);

        Assert.AreEqual("http://example.com/path#foo/bar", result.ToString());
    }

    [TestMethod]
    public void AppendFragment_WhenOtherFragmentIsTooShort_ReturnsSelf()
    {
        var baseRef = new JsonReference("http://example.com/path#foo");
        var other = new JsonReference("#");

        JsonReference result = baseRef.AppendFragment(other);

        Assert.AreEqual("http://example.com/path#foo", result.ToString());
    }

    [TestMethod]
    public void Apply_StrictFalse_MatchingSchemes_RemovesScheme()
    {
        var baseRef = new JsonReference("http://example.com/a/b");
        var other = new JsonReference("http://example.com/c/d");

        JsonReference result = baseRef.Apply(other, strict: false);

        Assert.AreEqual("http://example.com/c/d", result.ToString());
    }

    [TestMethod]
    public void Apply_OtherHasAuthorityButNoScheme()
    {
        var baseRef = new JsonReference("http://example.com/a/b");
        var other = new JsonReference("//other.com/c/d");

        JsonReference result = baseRef.Apply(other);

        Assert.AreEqual("http://other.com/c/d", result.ToString());
    }

    [TestMethod]
    public void Apply_OtherHasEmptyPathButHasQuery()
    {
        var baseRef = new JsonReference("http://example.com/a/b?old=1");
        var other = new JsonReference("?new=2");

        JsonReference result = baseRef.Apply(other);

        Assert.AreEqual("http://example.com/a/b?new=2", result.ToString());
    }

    [TestMethod]
    [DataRow("http://example.com/a/b/../c", "http://example.com/a/c")]
    [DataRow("http://example.com/a/./b/c", "http://example.com/a/b/c")]
    [DataRow("http://example.com/a/b/./c/../d", "http://example.com/a/b/d")]
    public void Apply_WithDotSegments_ResolvesCorrectly(string other, string expected)
    {
        var baseRef = new JsonReference("http://example.com/");
        var otherRef = new JsonReference(other);

        JsonReference result = baseRef.Apply(otherRef);

        Assert.AreEqual(expected, result.ToString());
    }

    [TestMethod]
    [DataRow("/a/b/../c", "http://example.com/a/c")]
    [DataRow("/a/./b/c", "http://example.com/a/b/c")]
    [DataRow("/a/b/./c/../d", "http://example.com/a/b/d")]
    [DataRow("/../path", "http://example.com/path")]
    [DataRow("/./path", "http://example.com/path")]
    public void Apply_RelativePathWithDotSegments_ResolvesCorrectly(string other, string expected)
    {
        var baseRef = new JsonReference("http://example.com/base/file");
        var otherRef = new JsonReference(other);

        JsonReference result = baseRef.Apply(otherRef);

        Assert.AreEqual(expected, result.ToString());
    }

    [TestMethod]
    public void Apply_DotAtEnd_RemovesDot()
    {
        var baseRef = new JsonReference("http://example.com/base/file");
        var otherRef = new JsonReference("/a/b/.");

        JsonReference result = baseRef.Apply(otherRef);

        Assert.AreEqual("http://example.com/a/b/", result.ToString());
    }

    [TestMethod]
    public void Apply_DotDotAtEnd_RemovesLastSegment()
    {
        var baseRef = new JsonReference("http://example.com/base/file");
        var otherRef = new JsonReference("/a/b/..");

        JsonReference result = baseRef.Apply(otherRef);

        Assert.AreEqual("http://example.com/a/", result.ToString());
    }

    [TestMethod]
    public void MakeRelative_NormalRelativePath()
    {
        var baseRef = new JsonReference("http://example.com/a/b/c");
        var targetRef = new JsonReference("http://example.com/a/d/e");

        JsonReference result = baseRef.MakeRelative(targetRef);

        Assert.AreEqual("../d/e", result.ToString());
    }

    [TestMethod]
    public void MakeRelative_SamePath_ReturnsDefault()
    {
        var baseRef = new JsonReference("http://example.com/a/b/c");
        var targetRef = new JsonReference("http://example.com/a/b/c");

        JsonReference result = baseRef.MakeRelative(targetRef);

        Assert.AreEqual(string.Empty, result.ToString());
    }

    [TestMethod]
    public void MakeRelative_PathDiffersByFile_ReturnsDotSlash()
    {
        var baseRef = new JsonReference("http://example.com/a/b/file.json");
        var targetRef = new JsonReference("http://example.com/a/b/");

        JsonReference result = baseRef.MakeRelative(targetRef);

        Assert.AreEqual("./", result.ToString());
    }

    [TestMethod]
    public void JsonReferenceBuilder_Port_WhenNoPort_ReturnsEmpty()
    {
        var reference = new JsonReference("http://example.com/path");
        JsonReferenceBuilder builder = reference.AsBuilder();

        Assert.IsTrue(builder.Port.IsEmpty);
    }

    [TestMethod]
    public void JsonReferenceBuilder_Port_WhenHasPort_ReturnsPort()
    {
        var reference = new JsonReference("http://example.com:8080/path");
        JsonReferenceBuilder builder = reference.AsBuilder();

        Assert.AreEqual("8080", builder.Port.ToString());
    }

    [TestMethod]
    public void ValidationContext_PushDocumentArrayIndex_WithStackEnabled()
    {
        Corvus.Json.ValidationContext context = Corvus.Json.ValidationContext.ValidContext
            .UsingStack();

        Corvus.Json.ValidationContext result = context.PushDocumentArrayIndex(3);

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.IsUsingStack);
    }

    [TestMethod]
    public void ValidationContext_PushValidationLocationProperty_WithStackEnabled()
    {
        Corvus.Json.ValidationContext context = Corvus.Json.ValidationContext.ValidContext
            .UsingStack();

        Corvus.Json.ValidationContext result = context.PushValidationLocationProperty("myProp");

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.IsUsingStack);
    }

    [TestMethod]
    public void ValidationContext_PushValidationLocationReducedPathModifier_WithStackEnabled()
    {
        Corvus.Json.ValidationContext context = Corvus.Json.ValidationContext.ValidContext
            .UsingStack();

        Corvus.Json.ValidationContext result = context.PushValidationLocationReducedPathModifier(
            new JsonReference("#/definitions/Foo"));

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.IsUsingStack);
    }

    [TestMethod]
    public void ValidationContext_ReplaceValidationLocationReducedPathModifier_WithStackEnabled()
    {
        Corvus.Json.ValidationContext context = Corvus.Json.ValidationContext.ValidContext
            .UsingStack()
            .PushValidationLocationReducedPathModifier(new JsonReference("#/defs/First"));

        Corvus.Json.ValidationContext result = context.ReplaceValidationLocationReducedPathModifier(
            new JsonReference("#/defs/Second"));

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.IsUsingStack);
    }

    [TestMethod]
    public void ValidationContext_PushValidationLocationReducedPathModifierAndProperty_WithStackEnabled()
    {
        Corvus.Json.ValidationContext context = Corvus.Json.ValidationContext.ValidContext
            .UsingStack();

        Corvus.Json.ValidationContext result = context.PushValidationLocationReducedPathModifierAndProperty(
            new JsonReference("#/properties"),
            "name");

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.IsUsingStack);
    }

    [TestMethod]
    public void ValidationContext_PushDocumentArrayIndex_WithoutStackEnabled_ReturnsSelf()
    {
        Corvus.Json.ValidationContext context = Corvus.Json.ValidationContext.ValidContext;

        Corvus.Json.ValidationContext result = context.PushDocumentArrayIndex(3);

        Assert.IsTrue(result.IsValid);
        Assert.IsFalse(result.IsUsingStack);
    }

    [TestMethod]
    public void TypeDecimal_WithOverflowNumber_ReturnsInvalid_DetailedLevel()
    {
        string json = "79228162514264337593543950336";
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonNumber number = new JsonNumber(doc.RootElement.Clone());

        Corvus.Json.ValidationContext result = ValidateWithoutCoreType.TypeDecimal(
            number,
            Corvus.Json.ValidationContext.ValidContext.UsingResults(),
            ValidationLevel.Detailed);

        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeDecimal_WithOverflowNumber_ReturnsInvalid_BasicLevel()
    {
        string json = "79228162514264337593543950336";
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonNumber number = new JsonNumber(doc.RootElement.Clone());

        Corvus.Json.ValidationContext result = ValidateWithoutCoreType.TypeDecimal(
            number,
            Corvus.Json.ValidationContext.ValidContext.UsingResults(),
            ValidationLevel.Basic);

        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeDecimal_WithOverflowNumber_ReturnsInvalid_FlagLevel()
    {
        string json = "79228162514264337593543950336";
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonNumber number = new JsonNumber(doc.RootElement.Clone());

        Corvus.Json.ValidationContext result = ValidateWithoutCoreType.TypeDecimal(
            number,
            Corvus.Json.ValidationContext.ValidContext,
            ValidationLevel.Flag);

        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeDecimal_WithValidNumber_ReturnsValid()
    {
        string json = "123.456";
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonNumber number = new JsonNumber(doc.RootElement.Clone());

        Corvus.Json.ValidationContext result = ValidateWithoutCoreType.TypeDecimal(
            number,
            Corvus.Json.ValidationContext.ValidContext,
            ValidationLevel.Verbose);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    [DataRow("P1Y2M3D", 1, 2, 3)]
    [DataRow("P0D", 0, 0, 0)]
    [DataRow("P1Y", 1, 0, 0)]
    [DataRow("P6M", 0, 6, 0)]
    [DataRow("P10D", 0, 0, 10)]
    public void Period_Parse_ValidDuration(string input, int years, int months, int days)
    {
        Period result = Period.Parse(input);

        Assert.AreEqual(years, result.Years);
        Assert.AreEqual(months, result.Months);
        Assert.AreEqual(days, result.Days);
    }

    [TestMethod]
    [DataRow("PT1H2M3S", 1, 2, 3)]
    [DataRow("PT0S", 0, 0, 0)]
    public void Period_Parse_WithTimeComponents(string input, int hours, int minutes, int seconds)
    {
        Period result = Period.Parse(input);

        Assert.AreEqual(hours, result.Hours);
        Assert.AreEqual(minutes, result.Minutes);
        Assert.AreEqual(seconds, result.Seconds);
    }

    [TestMethod]
    public void FileSystemDocumentResolver_Dispose_DoesNotThrow()
    {
        var resolver = new FileSystemDocumentResolver();
        resolver.Dispose();
    }

    [TestMethod]
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
            Assert.IsNotNull(resolved);

            resolver.Reset();
        }

        resolver.Dispose();
    }

    [TestMethod]
    public void Apply_RelativePathMergedWithBase()
    {
        var baseRef = new JsonReference("http://example.com/a/b/c");
        var otherRef = new JsonReference("d");

        JsonReference result = baseRef.Apply(otherRef);

        Assert.AreEqual("http://example.com/a/b/d", result.ToString());
    }

    [TestMethod]
    public void Apply_RelativePathWithDotDot()
    {
        var baseRef = new JsonReference("http://example.com/a/b/c");
        var otherRef = new JsonReference("../d");

        JsonReference result = baseRef.Apply(otherRef);

        Assert.AreEqual("http://example.com/a/d", result.ToString());
    }

    [TestMethod]
    public void Apply_EmptyPathEmptyQuery_UsesBasePathAndQuery()
    {
        var baseRef = new JsonReference("http://example.com/a/b?q=1");
        var otherRef = new JsonReference("#frag");

        JsonReference result = baseRef.Apply(otherRef);

        Assert.AreEqual("http://example.com/a/b?q=1#frag", result.ToString());
    }
}