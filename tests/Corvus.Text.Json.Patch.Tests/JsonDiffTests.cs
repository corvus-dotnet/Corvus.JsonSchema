// <copyright file="JsonDiffTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.Patch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Patch.Tests;

/// <summary>
/// Tests for <see cref="JsonDiffExtensions.CreatePatch"/> which produces
/// RFC 6902 JSON Patch documents that transform a source into a target.
/// </summary>
[TestClass]
public class JsonDiffTests
{
    // ──────────────────────────────────────────────
    // Identity / no-op
    // ──────────────────────────────────────────────

    [TestMethod]
    public void IdenticalObjectsProduceEmptyPatch()
    {
        AssertDiffRoundTrips(
            """{"a":1,"b":2}""",
            """{"a":1,"b":2}""");
    }

    [TestMethod]
    public void IdenticalArraysProduceEmptyPatch()
    {
        AssertDiffRoundTrips(
            """[1,2,3]""",
            """[1,2,3]""");
    }

    [TestMethod]
    public void IdenticalScalarsProduceEmptyPatch()
    {
        AssertDiffRoundTrips("""42""", """42""");
        AssertDiffRoundTrips("\"hello\"", "\"hello\"");
        AssertDiffRoundTrips("""true""", """true""");
        AssertDiffRoundTrips("""null""", """null""");
    }

    // ──────────────────────────────────────────────
    // Root-level replacement
    // ──────────────────────────────────────────────

    [TestMethod]
    public void DifferentRootTypesProduceReplace()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonPatchDocument patch = CreatePatch("""42""", "\"hello\"", workspace);
        AssertPatchOperationCount(patch, 1);
        AssertDiffRoundTrips("""42""", "\"hello\"");
    }

    [TestMethod]
    public void DifferentRootScalarsProduceReplace()
    {
        AssertDiffRoundTrips("""1""", """2""");
        AssertDiffRoundTrips("\"a\"", "\"b\"");
        AssertDiffRoundTrips("""true""", """false""");
    }

    [TestMethod]
    public void NullToObjectProducesReplace()
    {
        AssertDiffRoundTrips("""null""", """{"a":1}""");
    }

    // ──────────────────────────────────────────────
    // Object property changes
    // ──────────────────────────────────────────────

    [TestMethod]
    public void AddedPropertyProducesAdd()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonPatchDocument patch = CreatePatch(
            """{"a":1}""",
            """{"a":1,"b":2}""",
            workspace);

        AssertPatchOperationCount(patch, 1);
        AssertDiffRoundTrips("""{"a":1}""", """{"a":1,"b":2}""");
    }

    [TestMethod]
    public void RemovedPropertyProducesRemove()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonPatchDocument patch = CreatePatch(
            """{"a":1,"b":2}""",
            """{"a":1}""",
            workspace);

        AssertPatchOperationCount(patch, 1);
        AssertDiffRoundTrips("""{"a":1,"b":2}""", """{"a":1}""");
    }

    [TestMethod]
    public void ChangedPropertyProducesReplace()
    {
        AssertDiffRoundTrips(
            """{"a":1}""",
            """{"a":2}""");
    }

    [TestMethod]
    public void MultiplePropertyChanges()
    {
        AssertDiffRoundTrips(
            """{"a":1,"b":2,"c":3}""",
            """{"a":1,"b":99,"d":4}""");
    }

    [TestMethod]
    public void NestedObjectChanges()
    {
        AssertDiffRoundTrips(
            """{"a":{"b":{"c":1}}}""",
            """{"a":{"b":{"c":2}}}""");
    }

    [TestMethod]
    public void ReorderedPropertiesProduceEmptyPatch()
    {
        // Object property order is not significant
        AssertDiffRoundTrips(
            """{"a":1,"b":2}""",
            """{"b":2,"a":1}""");
    }

    // ──────────────────────────────────────────────
    // Array changes
    // ──────────────────────────────────────────────

    [TestMethod]
    public void ArrayElementChangeProducesReplace()
    {
        AssertDiffRoundTrips(
            """[1,2,3]""",
            """[1,99,3]""");
    }

    [TestMethod]
    public void DifferentLengthArraysProduceWholeReplace()
    {
        AssertDiffRoundTrips(
            """[1,2,3]""",
            """[1,2,3,4]""");
    }

    [TestMethod]
    public void EmptyToNonEmptyArrayProducesReplace()
    {
        AssertDiffRoundTrips("""[]""", """[1]""");
    }

    [TestMethod]
    public void NestedArrayChanges()
    {
        AssertDiffRoundTrips(
            """[[1,2],[3,4]]""",
            """[[1,2],[3,99]]""");
    }

    // ──────────────────────────────────────────────
    // Pointer escaping (RFC 6901)
    // ──────────────────────────────────────────────

    [TestMethod]
    public void PropertyNameWithTildeIsEscaped()
    {
        AssertDiffRoundTrips(
            """{"a~b":1}""",
            """{"a~b":2}""");
    }

    [TestMethod]
    public void PropertyNameWithSlashIsEscaped()
    {
        AssertDiffRoundTrips(
            """{"a/b":1}""",
            """{"a/b":2}""");
    }

    [TestMethod]
    public void EmptyPropertyName()
    {
        AssertDiffRoundTrips(
            """{"":1}""",
            """{"":2}""");
    }

    [TestMethod]
    public void PropertyNameWithTildeAndSlash()
    {
        AssertDiffRoundTrips(
            """{"a~/b":1}""",
            """{"a~/b":2}""");
    }

    // ──────────────────────────────────────────────
    // Mixed nested structures
    // ──────────────────────────────────────────────

    [TestMethod]
    public void ComplexNestedDiff()
    {
        AssertDiffRoundTrips(
            """{"users":[{"name":"Alice","age":30},{"name":"Bob","age":25}],"count":2}""",
            """{"users":[{"name":"Alice","age":31},{"name":"Bob","age":25}],"count":2,"active":true}""");
    }

    [TestMethod]
    public void ObjectInsideArrayChange()
    {
        AssertDiffRoundTrips(
            """[{"a":1},{"b":2}]""",
            """[{"a":1},{"b":3}]""");
    }

    [TestMethod]
    public void ReplaceObjectWithScalar()
    {
        AssertDiffRoundTrips(
            """{"a":{"b":1}}""",
            """{"a":42}""");
    }

    [TestMethod]
    public void ReplaceScalarWithObject()
    {
        AssertDiffRoundTrips(
            """{"a":42}""",
            """{"a":{"b":1}}""");
    }

    [TestMethod]
    public void ReplaceArrayWithObject()
    {
        AssertDiffRoundTrips(
            """{"a":[1,2]}""",
            """{"a":{"x":1}}""");
    }

    // ──────────────────────────────────────────────
    // Empty containers
    // ──────────────────────────────────────────────

    [TestMethod]
    public void EmptyObjectToNonEmpty()
    {
        AssertDiffRoundTrips("""{}""", """{"a":1}""");
    }

    [TestMethod]
    public void NonEmptyObjectToEmpty()
    {
        AssertDiffRoundTrips("""{"a":1}""", """{}""");
    }

    [TestMethod]
    public void EmptyObjectsAreEqual()
    {
        AssertDiffRoundTrips("""{}""", """{}""");
    }

    [TestMethod]
    public void EmptyArraysAreEqual()
    {
        AssertDiffRoundTrips("""[]""", """[]""");
    }

    // ──────────────────────────────────────────────
    // Deep nesting
    // ──────────────────────────────────────────────

    [TestMethod]
    public void DeepNesting()
    {
        AssertDiffRoundTrips(
            """{"a":{"b":{"c":{"d":{"e":1}}}}}""",
            """{"a":{"b":{"c":{"d":{"e":2}}}}}""");
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static JsonPatchDocument CreatePatch(string sourceJson, string targetJson, JsonWorkspace workspace)
    {
        using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse(sourceJson);
        using ParsedJsonDocument<JsonElement> targetDoc = ParsedJsonDocument<JsonElement>.Parse(targetJson);
        return JsonDiffExtensions.CreatePatch(sourceDoc.RootElement, targetDoc.RootElement, workspace);
    }

    private static void AssertPatchOperationCount(in JsonPatchDocument patch, int expected)
    {
        int count = 0;
        foreach (JsonPatchDocument.PatchOperation op in patch.EnumerateArray())
        {
            _ = op;
            count++;
        }

        Assert.AreEqual(expected, count);
    }

    /// <summary>
    /// Creates a patch from source → target, applies it to source, and verifies
    /// the result equals the target. This is the gold-standard correctness check.
    /// </summary>
    private static void AssertDiffRoundTrips(string sourceJson, string targetJson)
    {
        using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse(sourceJson);
        using ParsedJsonDocument<JsonElement> targetDoc = ParsedJsonDocument<JsonElement>.Parse(targetJson);

        // Apply the patch to a mutable copy of the source
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonPatchDocument patch = JsonDiffExtensions.CreatePatch(sourceDoc.RootElement, targetDoc.RootElement, workspace);

        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable mutable = builder.RootElement;
        bool applied = mutable.TryApplyPatch(patch);
        Assert.IsTrue(applied, "Patch application should succeed");

        // Verify the patched result matches the target
        JsonElement patchedElement = mutable.Clone();
        Assert.AreEqual(targetDoc.RootElement, patchedElement);
    }
}