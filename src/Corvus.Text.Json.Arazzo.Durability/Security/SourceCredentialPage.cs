// <copyright file="SourceCredentialPage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// One keyset page of source credential bindings (design §13): the reach-visible bindings for the page, ordered by
/// <c>(sourceName, environment)</c>, plus an opaque <see cref="NextPageToken"/> to fetch the next page (or
/// <see langword="null"/> when this is the last page). The <see cref="Bindings"/> are pooled documents the caller
/// owns — <see cref="Dispose"/> the page (which disposes the list) once read.
/// </summary>
public sealed class SourceCredentialPage : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="SourceCredentialPage"/> class.</summary>
    /// <param name="bindings">The page's reach-visible bindings, ordered by <c>(sourceName, environment)</c>.</param>
    /// <param name="nextPageToken">An opaque continuation token for the next page, or <see langword="null"/> if this is the last page.</param>
    public SourceCredentialPage(PooledDocumentList<SourceCredentialBinding> bindings, string? nextPageToken)
    {
        this.Bindings = bindings;
        this.NextPageToken = nextPageToken;
    }

    /// <summary>Gets the page's reach-visible bindings, ordered by <c>(sourceName, environment)</c>.</summary>
    public PooledDocumentList<SourceCredentialBinding> Bindings { get; }

    /// <summary>Gets the opaque token to fetch the next page, or <see langword="null"/> if this is the last page.</summary>
    public string? NextPageToken { get; }

    /// <inheritdoc/>
    public void Dispose() => this.Bindings.Dispose();
}