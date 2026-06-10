// <copyright file="SchemaReferenceNormalizationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration.DocumentResolvers;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests for <see cref="SchemaReferenceNormalization.TryNormalizeSchemaReference(string, out string?)"/>,
/// in particular the handling of <c>file://</c> URI references (see issue #817).
/// </summary>
[TestClass]
public class SchemaReferenceNormalizationTests
{
    [TestMethod]
    public void FileUriReference_NormalizesToLocalPath()
    {
        // Build an absolute, OS-correct local path and the file:// URI that addresses it.
        // Using new Uri(localPath).AbsoluteUri keeps this test correct on both Windows
        // (file:///C:/...) and Unix (file:///home/...).
        string localPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "corvus-817", "child.json"));
        string fileUri = new Uri(localPath).AbsoluteUri;

        bool normalized = SchemaReferenceNormalization.TryNormalizeSchemaReference(fileUri, out string result);

        Assert.IsTrue(normalized);
        Assert.AreEqual(localPath.Replace('\\', '/'), result);
    }

    [TestMethod]
    public void FileUriReference_WithPercentEncodedSpace_DecodesToLocalPath()
    {
        string localPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "corvus 817 schemas", "child.json"));
        string fileUri = new Uri(localPath).AbsoluteUri;

        // Sanity-check the precondition: the URI really is percent-encoded.
        Assert.IsTrue(fileUri.Contains("%20"), $"Expected a percent-encoded space in '{fileUri}'.");

        bool normalized = SchemaReferenceNormalization.TryNormalizeSchemaReference(fileUri, out string result);

        Assert.IsTrue(normalized);
        Assert.AreEqual(localPath.Replace('\\', '/'), result);
    }

    [TestMethod]
    public void FileUriReference_WithBasePath_IgnoresBasePathBecauseItIsAbsolute()
    {
        string localPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "corvus-817", "child.json"));
        string fileUri = new Uri(localPath).AbsoluteUri;
        string unrelatedBase = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "some-other-base"));

        bool normalized = SchemaReferenceNormalization.TryNormalizeSchemaReference(fileUri, unrelatedBase, out string result);

        Assert.IsTrue(normalized);
        Assert.AreEqual(localPath.Replace('\\', '/'), result);
    }

    [TestMethod]
    public void RelativeReference_WithBasePath_ResolvesAgainstBasePath()
    {
        string basePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "corvus-817-base"));
        string expected = Path.GetFullPath(Path.Combine(basePath, "sub", "child.json"));

        bool normalized = SchemaReferenceNormalization.TryNormalizeSchemaReference("sub/child.json", basePath, out string result);

        Assert.IsTrue(normalized);
        Assert.AreEqual(expected.Replace('\\', '/'), result);
    }

    [TestMethod]
    public void HttpReference_IsNotTreatedAsAFilePath()
    {
        bool normalized = SchemaReferenceNormalization.TryNormalizeSchemaReference("https://example.com/schemas/child.json", out string result);

        Assert.IsFalse(normalized);
        Assert.IsNull(result);
    }
}