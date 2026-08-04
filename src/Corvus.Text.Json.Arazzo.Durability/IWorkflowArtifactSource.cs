// <copyright file="IWorkflowArtifactSource.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Where a runner gets the bytes it needs to load a version's executor: the version's content hash, and the named
/// documents of its package.
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately far narrower than <see cref="IWorkflowCatalogStore"/>. Resolving an executor needs a
/// hash and three documents; the catalog store also adds, updates, deletes, purges, and searches. Under ADR 0065 a
/// runner holds no store credential, so what it reaches for has to be the thing it actually uses rather than the
/// interface that happens to contain it.
/// </para>
/// <para>
/// Nothing here is trusted on the strength of where it came from. The runner verifies the assembly against the hash
/// and the manifest's signature after pulling, so a compromised source cannot substitute an executor.
/// </para>
/// </remarks>
public interface IWorkflowArtifactSource
{
    /// <summary>Reads a version's content hash, which its loaded executor is verified against.</summary>
    /// <param name="baseWorkflowId">The unversioned workflow identity.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The hash, or <see langword="null"/> when the version is not available to this caller.</returns>
    ValueTask<string?> GetContentHashAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken);

    /// <summary>Reads one named document of a version's package.</summary>
    /// <param name="baseWorkflowId">The unversioned workflow identity.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="documentName">The document to read.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The document's bytes, or <see langword="null"/> when it is absent or not available to this caller.</returns>
    ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, CancellationToken cancellationToken);
}