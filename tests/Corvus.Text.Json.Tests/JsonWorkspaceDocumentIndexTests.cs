// <copyright file="JsonWorkspaceDocumentIndexTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// A document caches the workspace, generation and index of its first registration in three separate properties. The
/// workspace uses the cached index only when it names that document, so a torn or stale cache cannot resolve another
/// document (issue #928).
/// </summary>
[TestClass]
public class JsonWorkspaceDocumentIndexTests
{
    [TestMethod]
    [DataRow(0, DisplayName = "cached index names another document")]
    [DataRow(5, DisplayName = "cached index is out of range")]
    public void TakeOwnership_CacheNamingThisWorkspaceWithTheWrongIndex_RegistersTheDocument(int cachedIndex)
    {
        JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        try
        {
            IJsonDocument other = ParsedJsonDocument<JsonElement>.Parse("""{"other":1}""");
            IJsonDocument shared = ParsedJsonDocument<JsonElement>.Parse("""{"shared":2}""");
            workspace.TakeOwnership(other);

            // The state two workspaces registering one shared document at the same time can leave: this workspace and its
            // generation, with an index written for another registration.
            shared.CachedWorkspace = workspace;
            shared.CachedWorkspaceDocumentIndex = cachedIndex;
            shared.CachedWorkspaceGeneration = other.CachedWorkspaceGeneration;

            workspace.TakeOwnership(shared);

            Assert.AreSame(other, workspace.GetDocument(workspace.GetDocumentIndex(other)));
            Assert.AreSame(shared, workspace.GetDocument(workspace.GetDocumentIndex(shared)));
        }
        finally
        {
            workspace.Dispose();
        }
    }

    [TestMethod]
    [DataRow(3, DisplayName = "linear scan")]
    [DataRow(20, DisplayName = "index dictionary, after the backing array grows")]
    public void GetDocumentIndex_EveryCachedIndexNamingAnotherDocument_ResolvesEachDocument(int count)
    {
        JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        try
        {
            IJsonDocument[] documents = new IJsonDocument[count];
            for (int i = 0; i < count; i++)
            {
                documents[i] = ParsedJsonDocument<JsonElement>.Parse($$"""{"document":{{i}}}""");
                workspace.TakeOwnership(documents[i]);
            }

            // Registered documents whose cached index names their neighbour: found by the scan or the dictionary.
            for (int i = 0; i < count; i++)
            {
                documents[i].CachedWorkspaceDocumentIndex = (i + 1) % count;
            }

            for (int i = 0; i < count; i++)
            {
                Assert.AreSame(documents[i], workspace.GetDocument(workspace.GetDocumentIndex(documents[i])));
            }

            // A document not yet registered, with a torn cache naming the first document: added under its own index.
            IJsonDocument added = ParsedJsonDocument<JsonElement>.Parse("""{"added":true}""");
            added.CachedWorkspace = workspace;
            added.CachedWorkspaceDocumentIndex = 0;
            added.CachedWorkspaceGeneration = documents[0].CachedWorkspaceGeneration;
            workspace.TakeOwnership(added);

            Assert.AreSame(added, workspace.GetDocument(workspace.GetDocumentIndex(added)));
            Assert.AreSame(documents[0], workspace.GetDocument(workspace.GetDocumentIndex(documents[0])));
        }
        finally
        {
            workspace.Dispose();
        }
    }
}