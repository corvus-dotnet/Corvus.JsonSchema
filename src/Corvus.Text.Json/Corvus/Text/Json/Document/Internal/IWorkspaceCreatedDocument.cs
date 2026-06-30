// <copyright file="IWorkspaceCreatedDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Internal;

/// <summary>
/// A <see cref="IWorkspaceManagedDocument"/> that records the workspace which <em>created</em> it, so that it is
/// disposed only by its creator — never by another workspace that merely <em>referenced</em> it.
/// </summary>
/// <remarks>
/// <para>
/// A workspace's document table (<see cref="JsonWorkspace"/>) registers a document both when it creates one and
/// when it references one from another workspace to resolve a cross-workspace value (the referenced document's
/// value bytes are addressed by its index in the referencing workspace). Disposal must distinguish the two:
/// disposing a document that another workspace created — and still owns — corrupts that workspace. A document
/// implementing this interface is disposed by a workspace only when <see cref="CreatingWorkspace"/> is that
/// workspace.
/// </para>
/// <para>
/// Implemented by builders (<c>JsonDocumentBuilder</c>), whose creator is fixed at construction and is never
/// changed by being referenced elsewhere.
/// </para>
/// </remarks>
[CLSCompliant(false)]
public interface IWorkspaceCreatedDocument : IWorkspaceManagedDocument
{
    /// <summary>
    /// Gets the workspace that created this document and is therefore responsible for disposing it.
    /// </summary>
    JsonWorkspace CreatingWorkspace { get; }
}