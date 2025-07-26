// <copyright file="PrepopulatedDocumentResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// A document resolver which requires you to pre-load the documents it can resolve.
/// </summary>
public class PrepopulatedDocumentResolver : IDocumentResolver
{
    private readonly Dictionary<string, JsonDocument> documents = [];
    private bool disposedValue;

    /// <inheritdoc/>
    public bool AddDocument(string uri, JsonDocument document)
    {
        this.CheckDisposed();
#if NET8_0_OR_GREATER
        return this.documents.TryAdd(uri, document);
#else
        if (this.documents.ContainsKey(uri))
        {
            return false;
        }

        this.documents.Add(uri, document);
        return true;
#endif
    }

    /// <inheritdoc/>
    public bool AddDocument(IMetaSchema metaSchema)
        => this.AddDocument(metaSchema.Uri, metaSchema.Document);

    /// <inheritdoc/>
    public ValueTask<JsonElement?> TryResolve(JsonReference reference)
    {
        this.CheckDisposed();
        string uri = reference.Uri.ToString();

        if (this.documents.TryGetValue(uri, out JsonDocument? result))
        {
            if (JsonPointerUtilities.TryResolvePointer(result, reference.Fragment, out JsonElement? element))
            {
#if NET8_0_OR_GREATER
                return ValueTask.FromResult(element);
#else
                return new ValueTask<JsonElement?>(Task.FromResult(element));
#endif
            }
        }

#if NET8_0_OR_GREATER
        return ValueTask.FromResult<JsonElement?>(default);
#else
        return new ValueTask<JsonElement?>(Task.FromResult<JsonElement?>(default));
#endif
    }

    /// <inheritdoc/>
    public void Reset()
    {
        this.CheckDisposed();

        this.DisposeDocumentsAndClear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Do not change this code. Put clean-up code in 'Dispose(bool disposing)' method
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Implements the dispose pattern.
    /// </summary>
    /// <param name="disposing">True if we are disposing.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                this.DisposeDocumentsAndClear();
            }

            this.disposedValue = true;
        }
    }

    private void DisposeDocumentsAndClear()
    {
        foreach (KeyValuePair<string, JsonDocument> document in this.documents)
        {
            document.Value.Dispose();
        }

        this.documents.Clear();
    }

    private void CheckDisposed()
    {
#if NET8_0_OR_GREATER
        ObjectDisposedException.ThrowIf(this.disposedValue, this);
#else
        if (this.disposedValue)
        {
            throw new ObjectDisposedException(nameof(PrepopulatedDocumentResolver));
        }
#endif
    }
}