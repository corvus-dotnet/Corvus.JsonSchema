// <copyright file="JsonMarshalRefreshUnsafeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for <see cref="JsonMarshal.RefreshUnsafe{T}"/>.
/// </summary>
[TestClass]
public class JsonMarshalRefreshUnsafeTests
{
    [TestMethod]
    public void RefreshUnsafe_RevivesAVersionStaleParentAfterDescendantMutation()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"a":{"b":{"c":1}}}""");

        // Hold a handle to a non-root element ("a").
        Assert.IsTrue(doc.RootElement.TryGetProperty("a"u8, out JsonElement.Mutable a));

        // Mutate a *descendant* of "a" (a's grandchild "b") through a separate handle. This bumps the
        // document version, leaving the held "a" handle version-stale while its start index is still
        // valid (the mutation is inside a's subtree).
        Assert.IsTrue(a.TryGetProperty("b"u8, out JsonElement.Mutable b));
        b.SetProperty("d"u8, 2);

        // The stale, non-root handle now fails its staleness check on use.
        Assert.ThrowsExactly<InvalidOperationException>(() => a.TryGetProperty("b"u8, out _));

        // RefreshUnsafe mints a fresh handle for the same element with the current version.
        JsonElement.Mutable refreshed = JsonMarshal.RefreshUnsafe(in a);
        Assert.AreEqual(JsonValueKind.Object, refreshed.ValueKind);
        Assert.IsTrue(refreshed.TryGetProperty("b"u8, out JsonElement.Mutable refreshedB));
        Assert.AreEqual("""{"c":1,"d":2}""", refreshedB.ToString());
    }

    [TestMethod]
    public void RefreshUnsafe_OnRootIsANoOpReturningEquivalentElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"a":1}""");

        JsonElement.Mutable root = doc.RootElement;
        JsonElement.Mutable refreshed = JsonMarshal.RefreshUnsafe(in root);

        Assert.AreEqual(root.ToString(), refreshed.ToString());
        Assert.IsTrue(refreshed.TryGetProperty("a"u8, out _));
    }

    [TestMethod]
    public void RefreshUnsafe_ThrowsObjectDisposedWhenDocumentDisposed()
    {
        JsonWorkspace workspace = JsonWorkspace.Create();
        JsonDocumentBuilder<JsonElement.Mutable> doc =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"a":1}""");
        JsonElement.Mutable root = doc.RootElement;

        doc.Dispose();
        workspace.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => JsonMarshal.RefreshUnsafe(in root));
    }
}
