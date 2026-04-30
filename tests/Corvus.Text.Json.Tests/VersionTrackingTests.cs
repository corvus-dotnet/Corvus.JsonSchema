// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests;

using Xunit;

/// <summary>
/// Tests for the version tracking behavior of <see cref="JsonElement.Mutable"/>,
/// verifying that the root element is always live and that intermediate references
/// are invalidated by sibling mutations.
/// </summary>
public class VersionTrackingTests
{
    /// <summary>
    /// Verifies that a cached root element remains valid after a child element is mutated.
    /// The root element is always at index 0 and is never relocated by mutations.
    /// </summary>
    [Fact]
    public void CachedRootElement_SurvivesChildMutation()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            new JsonElement.Source(static (ref objectBuilder) =>
            {
                objectBuilder.AddProperty("alpha"u8, static (ref b) =>
                {
                    b.AddProperty("value"u8, "a"u8);
                });
                objectBuilder.AddProperty("beta"u8, static (ref b) =>
                {
                    b.AddProperty("value"u8, "b"u8);
                });
            }));

        JsonElement.Mutable root = doc.RootElement;

        // Cache an intermediate child and mutate it — this bumps the document version.
        JsonElement.Mutable alpha = root.GetProperty("alpha"u8);
        alpha.SetProperty("value"u8, "updated-a"u8);

        // Root is always live — navigating from it to a different child still works
        // even though root's stored version is behind the document version.
        JsonElement.Mutable beta = root.GetProperty("beta"u8);
        beta.SetProperty("value"u8, "updated-b"u8);

        // Verify both mutations took effect via the always-live root.
        Assert.Equal("updated-a", root.GetProperty("alpha"u8).GetProperty("value"u8).GetString());
        Assert.Equal("updated-b", root.GetProperty("beta"u8).GetProperty("value"u8).GetString());
    }

    /// <summary>
    /// Verifies that a cached root element survives multiple child mutations in sequence.
    /// Each navigation from root creates a temporary child reference that is used and discarded.
    /// </summary>
    [Fact]
    public void CachedRootElement_SurvivesMultipleChildMutations()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            new JsonElement.Source(static (ref objectBuilder) =>
            {
                objectBuilder.AddProperty("first"u8, static (ref b) =>
                {
                    b.AddProperty("value"u8, "1"u8);
                });
                objectBuilder.AddProperty("second"u8, static (ref b) =>
                {
                    b.AddProperty("value"u8, "2"u8);
                });
                objectBuilder.AddProperty("third"u8, static (ref b) =>
                {
                    b.AddProperty("value"u8, "3"u8);
                });
            }));

        JsonElement.Mutable root = doc.RootElement;

        // Each call navigates from root, creates a temporary child reference, mutates, and discards.
        // Root remains live across all three mutations.
        root.GetProperty("first"u8).SetProperty("value"u8, "updated-1"u8);
        root.GetProperty("second"u8).SetProperty("value"u8, "updated-2"u8);
        root.GetProperty("third"u8).SetProperty("value"u8, "updated-3"u8);

        // Verify all changes through the always-live root.
        Assert.Equal("updated-1", root.GetProperty("first"u8).GetProperty("value"u8).GetString());
        Assert.Equal("updated-2", root.GetProperty("second"u8).GetProperty("value"u8).GetString());
        Assert.Equal("updated-3", root.GetProperty("third"u8).GetProperty("value"u8).GetString());
    }

    /// <summary>
    /// Verifies that a cached intermediate child reference throws after a sibling is mutated.
    /// Unlike the root, intermediate references are invalidated by any other mutation.
    /// </summary>
    [Fact]
    public void CachedIntermediateChild_ThrowsAfterSiblingMutation()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            new JsonElement.Source(static (ref objectBuilder) =>
            {
                objectBuilder.AddProperty("alpha"u8, static (ref b) =>
                {
                    b.AddProperty("value"u8, "a"u8);
                });
                objectBuilder.AddProperty("beta"u8, static (ref b) =>
                {
                    b.AddProperty("value"u8, "b"u8);
                });
            }));

        JsonElement.Mutable root = doc.RootElement;

        // Cache two intermediate children.
        JsonElement.Mutable alpha = root.GetProperty("alpha"u8);
        JsonElement.Mutable beta = root.GetProperty("beta"u8);

        // Mutate through 'alpha' — bumps the document version.
        alpha.SetProperty("value"u8, "mutated-a"u8);

        // 'beta' was obtained before the mutation and is now stale.
        Assert.Throws<InvalidOperationException>(() =>
            beta.SetProperty("value"u8, "this-should-throw"u8));
    }

    /// <summary>
    /// Verifies that multiple mutations on the same element succeed. Each mutation
    /// updates the element's stored version, so subsequent operations on the same
    /// reference remain valid.
    /// </summary>
    [Fact]
    public void SameElement_MultipleMutations_Succeeds()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            new JsonElement.Source(static (ref objectBuilder) =>
            {
                objectBuilder.AddProperty("data"u8, static (ref b) =>
                {
                    b.AddProperty("x"u8, "original-x"u8);
                    b.AddProperty("y"u8, "original-y"u8);
                    b.AddProperty("z"u8, "original-z"u8);
                });
            }));

        JsonElement.Mutable root = doc.RootElement;
        JsonElement.Mutable data = root.GetProperty("data"u8);

        // Multiple mutations on the same element — each updates the element's version.
        data.SetProperty("x"u8, "new-x"u8);
        data.SetProperty("y"u8, "new-y"u8);
        data.SetProperty("z"u8, "new-z"u8);

        // All succeeded.
        Assert.Equal("new-x", data.GetProperty("x"u8).GetString());
        Assert.Equal("new-y", data.GetProperty("y"u8).GetString());
        Assert.Equal("new-z", data.GetProperty("z"u8).GetString());
    }
}
