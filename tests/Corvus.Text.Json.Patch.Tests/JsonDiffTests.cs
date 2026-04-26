// <copyright file="JsonDiffTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.Patch;
using Xunit;

namespace Corvus.Text.Json.Patch.Tests;

/// <summary>
/// Tests for <see cref="JsonDiffExtensions.CreatePatch"/> which produces
/// RFC 6902 JSON Patch documents that transform a source into a target.
/// </summary>
public class JsonDiffTests
{
    // ──────────────────────────────────────────────
    // Identity / no-op
    // ──────────────────────────────────────────────

    [Fact]
    public void IdenticalObjectsProduceEmptyPatch()
    {
        AssertDiffRoundTrips(
            """{"a":1,"b":2}""",
            """{"a":1,"b":2}""");
    }

    [Fact]
    public void IdenticalArraysProduceEmptyPatch()
    {
        AssertDiffRoundTrips(
            """[1,2,3]""",
            """[1,2,3]""");
    }

    [Fact]
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

    [Fact]
    public void DifferentRootTypesProduceReplace()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonPatchDocument patch = CreatePatch("""42""", "\"hello\"", workspace);
        AssertPatchOperationCount(patch, 1);
        AssertDiffRoundTrips("""42""", "\"hello\"");
    }

    [Fact]
    public void DifferentRootScalarsProduceReplace()
    {
        AssertDiffRoundTrips("""1""", """2""");
        AssertDiffRoundTrips("\"a\"", "\"b\"");
        AssertDiffRoundTrips("""true""", """false""");
    }

    [Fact]
    public void NullToObjectProducesReplace()
    {
        AssertDiffRoundTrips("""null""", """{"a":1}""");
    }

    // ──────────────────────────────────────────────
    // Object property changes
    // ──────────────────────────────────────────────

    [Fact]
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

    [Fact]
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

    [Fact]
    public void ChangedPropertyProducesReplace()
    {
        AssertDiffRoundTrips(
            """{"a":1}""",
            """{"a":2}""");
    }

    [Fact]
    public void MultiplePropertyChanges()
    {
        AssertDiffRoundTrips(
            """{"a":1,"b":2,"c":3}""",
            """{"a":1,"b":99,"d":4}""");
    }

    [Fact]
    public void NestedObjectChanges()
    {
        AssertDiffRoundTrips(
            """{"a":{"b":{"c":1}}}""",
            """{"a":{"b":{"c":2}}}""");
    }

    [Fact]
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

    [Fact]
    public void ArrayElementChangeProducesReplace()
    {
        AssertDiffRoundTrips(
            """[1,2,3]""",
            """[1,99,3]""");
    }

    [Fact]
    public void DifferentLengthArraysProduceWholeReplace()
    {
        AssertDiffRoundTrips(
            """[1,2,3]""",
            """[1,2,3,4]""");
    }

    [Fact]
    public void EmptyToNonEmptyArrayProducesReplace()
    {
        AssertDiffRoundTrips("""[]""", """[1]""");
    }

    [Fact]
    public void NestedArrayChanges()
    {
        AssertDiffRoundTrips(
            """[[1,2],[3,4]]""",
            """[[1,2],[3,99]]""");
    }

    // ──────────────────────────────────────────────
    // Pointer escaping (RFC 6901)
    // ──────────────────────────────────────────────

    [Fact]
    public void PropertyNameWithTildeIsEscaped()
    {
        AssertDiffRoundTrips(
            """{"a~b":1}""",
            """{"a~b":2}""");
    }

    [Fact]
    public void PropertyNameWithSlashIsEscaped()
    {
        AssertDiffRoundTrips(
            """{"a/b":1}""",
            """{"a/b":2}""");
    }

    [Fact]
    public void EmptyPropertyName()
    {
        AssertDiffRoundTrips(
            """{"":1}""",
            """{"":2}""");
    }

    [Fact]
    public void PropertyNameWithTildeAndSlash()
    {
        AssertDiffRoundTrips(
            """{"a~/b":1}""",
            """{"a~/b":2}""");
    }

    // ──────────────────────────────────────────────
    // Mixed nested structures
    // ──────────────────────────────────────────────

    [Fact]
    public void ComplexNestedDiff()
    {
        AssertDiffRoundTrips(
            """{"users":[{"name":"Alice","age":30},{"name":"Bob","age":25}],"count":2}""",
            """{"users":[{"name":"Alice","age":31},{"name":"Bob","age":25}],"count":2,"active":true}""");
    }

    [Fact]
    public void ObjectInsideArrayChange()
    {
        AssertDiffRoundTrips(
            """[{"a":1},{"b":2}]""",
            """[{"a":1},{"b":3}]""");
    }

    [Fact]
    public void ReplaceObjectWithScalar()
    {
        AssertDiffRoundTrips(
            """{"a":{"b":1}}""",
            """{"a":42}""");
    }

    [Fact]
    public void ReplaceScalarWithObject()
    {
        AssertDiffRoundTrips(
            """{"a":42}""",
            """{"a":{"b":1}}""");
    }

    [Fact]
    public void ReplaceArrayWithObject()
    {
        AssertDiffRoundTrips(
            """{"a":[1,2]}""",
            """{"a":{"x":1}}""");
    }

    // ──────────────────────────────────────────────
    // Empty containers
    // ──────────────────────────────────────────────

    [Fact]
    public void EmptyObjectToNonEmpty()
    {
        AssertDiffRoundTrips("""{}""", """{"a":1}""");
    }

    [Fact]
    public void NonEmptyObjectToEmpty()
    {
        AssertDiffRoundTrips("""{"a":1}""", """{}""");
    }

    [Fact]
    public void EmptyObjectsAreEqual()
    {
        AssertDiffRoundTrips("""{}""", """{}""");
    }

    [Fact]
    public void EmptyArraysAreEqual()
    {
        AssertDiffRoundTrips("""[]""", """[]""");
    }

    // ──────────────────────────────────────────────
    // Deep nesting
    // ──────────────────────────────────────────────

    [Fact]
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

        Assert.Equal(expected, count);
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
        Assert.True(applied, "Patch application should succeed");

        // Verify the patched result matches the target
        JsonElement patchedElement = mutable.Clone();
        Assert.Equal(targetDoc.RootElement, patchedElement);
    }
}