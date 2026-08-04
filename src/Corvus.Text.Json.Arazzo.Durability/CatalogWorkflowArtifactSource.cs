// <copyright file="CatalogWorkflowArtifactSource.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Serves executor artifacts straight from a catalog store, for a host that owns the catalog. A runner without a
/// catalog credential uses the runner API's source instead, and the resolver cannot tell the difference.
/// </summary>
/// <param name="catalog">The catalog to read from.</param>
public sealed class CatalogWorkflowArtifactSource(IWorkflowCatalogStore catalog) : IWorkflowArtifactSource
{
    private readonly IWorkflowCatalogStore catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

    /// <inheritdoc/>
    public async ValueTask<string?> GetContentHashAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        // The version document is owned only long enough to read its hash; the returned string outlives it.
        using ParsedJsonDocument<CatalogVersion>? versionDoc = await this.catalog.GetAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        return versionDoc is { } doc ? (string)doc.RootElement.Hash : null;
    }

    /// <inheritdoc/>
    public ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, CancellationToken cancellationToken)
        => this.catalog.GetDocumentAsync(baseWorkflowId, versionNumber, documentName, cancellationToken);
}