// <copyright file="JsonMergePatchTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Text.Json.Patch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Patch.Tests;

/// <summary>
/// Tests for JSON Merge Patch (RFC 7396) implementation.
/// Test cases from RFC 7396 Appendix A.
/// </summary>
[TestClass]
public class JsonMergePatchTests
{
    #region RFC 7396 Appendix A test cases

    [TestMethod]
    public void AppendixA_01_AddNewProperty()
    {
        RunMergePatchTest(
            target: """{"a":"b"}""",
            patch: """{"a":"c"}""",
            expected: """{"a":"c"}""");
    }

    [TestMethod]
    public void AppendixA_02_AddSecondProperty()
    {
        RunMergePatchTest(
            target: """{"a":"b"}""",
            patch: """{"b":"c"}""",
            expected: """{"a":"b","b":"c"}""");
    }

    [TestMethod]
    public void AppendixA_03_ReplaceStringWithNumber()
    {
        RunMergePatchTest(
            target: """{"a":"b"}""",
            patch: """{"a":null}""",
            expected: """{}""");
    }

    [TestMethod]
    public void AppendixA_04_ReplaceWithScalar()
    {
        RunMergePatchTest(
            target: """{"a":"b","b":"c"}""",
            patch: """{"a":null}""",
            expected: """{"b":"c"}""");
    }

    [TestMethod]
    public void AppendixA_05_ReplaceArrayWholesale()
    {
        RunMergePatchTest(
            target: """{"a":["b"]}""",
            patch: """{"a":"c"}""",
            expected: """{"a":"c"}""");
    }

    [TestMethod]
    public void AppendixA_06_ReplaceScalarWithArray()
    {
        RunMergePatchTest(
            target: """{"a":"c"}""",
            patch: """{"a":["b"]}""",
            expected: """{"a":["b"]}""");
    }

    [TestMethod]
    public void AppendixA_07_MixedAddAndReplace()
    {
        RunMergePatchTest(
            target: """{"a":{"b":"c"}}""",
            patch: """{"a":{"b":"d","c":null}}""",
            expected: """{"a":{"b":"d"}}""");
    }

    [TestMethod]
    public void AppendixA_08_NestedMerge()
    {
        RunMergePatchTest(
            target: """{"a":[{"b":"c"}]}""",
            patch: """{"a":[1]}""",
            expected: """{"a":[1]}""");
    }

    [TestMethod]
    public void AppendixA_09_ReplaceArrayWithScalar()
    {
        RunMergePatchTest(
            target: """["a","b"]""",
            patch: """["c","d"]""",
            expected: """["c","d"]""");
    }

    [TestMethod]
    public void AppendixA_10_MixedNestedMerge()
    {
        RunMergePatchTest(
            target: """{"a":"b"}""",
            patch: """["c"]""",
            expected: """["c"]""");
    }

    [TestMethod]
    public void AppendixA_11_ScalarPatch()
    {
        RunMergePatchTest(
            target: """{"a":"foo"}""",
            patch: """null""",
            expected: """null""");
    }

    [TestMethod]
    public void AppendixA_12_ScalarPatchOnScalar()
    {
        RunMergePatchTest(
            target: """{"a":"foo"}""",
            patch: "\"bar\"",
            expected: "\"bar\"");
    }

    [TestMethod]
    public void AppendixA_13_NestedObjectMerge()
    {
        RunMergePatchTest(
            target: """{"e":null}""",
            patch: """{"a":1}""",
            expected: """{"e":null,"a":1}""");
    }

    [TestMethod]
    public void AppendixA_14_PatchWithEmptyArray()
    {
        RunMergePatchTest(
            target: """[1,2]""",
            patch: """{"a":"b","c":null}""",
            expected: """{"a":"b"}""");
    }

    [TestMethod]
    public void AppendixA_15_EmptyObjectMerge()
    {
        RunMergePatchTest(
            target: """{}""",
            patch: """{"a":{"bb":{"ccc":null}}}""",
            expected: """{"a":{"bb":{}}}""");
    }

    #endregion

    #region Additional edge cases

    [TestMethod]
    public void EmptyObjectPatch_NoChange()
    {
        RunMergePatchTest(
            target: """{"a":"b","c":"d"}""",
            patch: """{}""",
            expected: """{"a":"b","c":"d"}""");
    }

    [TestMethod]
    public void DeeplyNestedMerge()
    {
        RunMergePatchTest(
            target: """{"a":{"b":{"c":{"d":"old"}}}}""",
            patch: """{"a":{"b":{"c":{"d":"new","e":"added"}}}}""",
            expected: """{"a":{"b":{"c":{"d":"new","e":"added"}}}}""");
    }

    [TestMethod]
    public void PatchNonObjectTargetWithObject_CreatesObject()
    {
        RunMergePatchTest(
            target: "\"just a string\"",
            patch: """{"a":"b"}""",
            expected: """{"a":"b"}""");
    }

    [TestMethod]
    public void PatchNumberTargetWithObject()
    {
        RunMergePatchTest(
            target: """42""",
            patch: """{"a":"b"}""",
            expected: """{"a":"b"}""");
    }

    [TestMethod]
    public void NullPatchValue_RemovesDeepProperty()
    {
        RunMergePatchTest(
            target: """{"a":{"b":"c","d":"e"},"f":"g"}""",
            patch: """{"a":{"b":null}}""",
            expected: """{"a":{"d":"e"},"f":"g"}""");
    }

    #endregion

    private static void RunMergePatchTest(string target, string patch, string expected)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> targetDoc = ParsedJsonDocument<JsonElement>.Parse(target);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = targetDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        JsonElement patchElement = JsonElement.ParseValue(patch);

        JsonMergePatchExtensions.ApplyMergePatch(ref root, in patchElement);

        JsonElement expectedElement = JsonElement.ParseValue(expected);
        Assert.IsTrue(
            root.Equals(expectedElement),
            $"Expected: {expected}\nActual: {root}");
    }
}
