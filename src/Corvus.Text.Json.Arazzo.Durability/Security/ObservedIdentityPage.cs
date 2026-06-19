// <copyright file="ObservedIdentityPage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// One keyset page of observed identities (design §16.5.4): the prefix-matched identities for the page, ordered by
/// <c>(subjectValue, subjectKind)</c>, plus an opaque <see cref="NextPageToken"/> to fetch the next page (or
/// <see langword="null"/> when this is the last page). The <see cref="Identities"/> are pooled documents the caller
/// owns — <see cref="Dispose"/> the page (which disposes the list) once read.
/// </summary>
public sealed class ObservedIdentityPage : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="ObservedIdentityPage"/> class.</summary>
    /// <param name="identities">The page's prefix-matched identities, ordered by <c>(subjectValue, subjectKind)</c>.</param>
    /// <param name="nextPageToken">An opaque continuation token for the next page, or <see langword="null"/> if this is the last page.</param>
    public ObservedIdentityPage(PooledDocumentList<ObservedIdentity> identities, string? nextPageToken)
    {
        this.Identities = identities;
        this.NextPageToken = nextPageToken;
    }

    /// <summary>Gets the page's identities, ordered by <c>(subjectValue, subjectKind)</c>.</summary>
    public PooledDocumentList<ObservedIdentity> Identities { get; }

    /// <summary>Gets the opaque token to fetch the next page, or <see langword="null"/> if this is the last page.</summary>
    public string? NextPageToken { get; }

    /// <inheritdoc/>
    public void Dispose() => this.Identities.Dispose();
}