// <copyright file="IWorkflowExecutorProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// Builds the compiled workflow executor assembly baked into a workflow package at catalog-add time, so an
/// execution host can dynamically load and run the workflow without re-generating or re-compiling it. The
/// code-generation layer implements this (generate clients + executor + a host adapter, then compile a single
/// release assembly); the catalog depends only on this abstraction and treats the result as opaque bytes.
/// </summary>
public interface IWorkflowExecutorProvider
{
    /// <summary>
    /// Builds the compiled executor artifact for a workflow and its referenced source documents.
    /// </summary>
    /// <param name="workflowUtf8">The Arazzo workflow document as UTF-8 JSON (with its assigned versioned id).</param>
    /// <param name="sources">The referenced source documents (name to UTF-8 JSON bytes), keyed by their
    /// <c>sourceDescriptions</c> name.</param>
    /// <param name="packageHash">The version's content hash, recorded in the manifest so a runner can verify the
    /// assembly belongs to the version it is loading for.</param>
    /// <returns>The compiled artifact, or <see langword="null"/> if none can be produced (the version is then
    /// catalogued but not runnable).</returns>
    WorkflowExecutorArtifact? BuildExecutor(
        ReadOnlyMemory<byte> workflowUtf8,
        IReadOnlyList<KeyValuePair<string, byte[]>> sources,
        string packageHash);
}

/// <summary>The output of <see cref="IWorkflowExecutorProvider.BuildExecutor"/>: the compiled assembly and its manifest.</summary>
/// <param name="Assembly">The compiled workflow executor assembly (a .NET DLL).</param>
/// <param name="Manifest">The executor manifest as UTF-8 JSON (target framework, integrity binding, entry type, …).</param>
public readonly record struct WorkflowExecutorArtifact(ReadOnlyMemory<byte> Assembly, ReadOnlyMemory<byte> Manifest);