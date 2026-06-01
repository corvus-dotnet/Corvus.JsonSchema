// <copyright file="JsonWorkspaceTakeOwnershipTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class JsonWorkspaceTakeOwnershipTests
{
    [TestMethod]
    public void TakeOwnership_DisposesDocumentOnWorkspaceDispose()
    {
        ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"Alice"}"""u8.ToArray());

        using (JsonWorkspace workspace = JsonWorkspace.CreateUnrented())
        {
            workspace.TakeOwnership(doc);
        }

        // After workspace disposal, the document should be disposed.
        // Accessing RootElement on a disposed document throws.
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = doc.RootElement.ToString());
    }

    [TestMethod]
    public void TakeOwnership_DisposesDocumentOnWorkspaceReset()
    {
        using JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}"""u8.ToArray());

        workspace.TakeOwnership(doc);
        workspace.Reset();

        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = doc.RootElement.ToString());
    }

    [TestMethod]
    public void TakeOwnership_IsIdempotent()
    {
        ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""[1,2,3]"""u8.ToArray());

        using (JsonWorkspace workspace = JsonWorkspace.CreateUnrented())
        {
            workspace.TakeOwnership(doc);
            workspace.TakeOwnership(doc);
        }

        // Should still only dispose once — no exception from double-dispose
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = doc.RootElement.ToString());
    }

    [TestMethod]
    public void TakeOwnership_DocumentAccessibleBeforeDispose()
    {
        using JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"key":"value"}"""u8.ToArray());

        workspace.TakeOwnership(doc);

        // Document should remain accessible while workspace is alive
        Assert.AreEqual("""{"key":"value"}""", doc.RootElement.ToString());
    }

    [TestMethod]
    public void TakeOwnership_WorksWithRentedWorkspace()
    {
        ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"rented":true}"""u8.ToArray());

        using (JsonWorkspace workspace = JsonWorkspace.Create())
        {
            workspace.TakeOwnership(doc);
        }

        // Rented workspace returns to cache on Dispose, which resets state
        // and should dispose owned documents
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = doc.RootElement.ToString());
    }
}