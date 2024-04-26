// <copyright file="CompoundDocumentResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Json;

/// <summary>
/// Delegates <see cref="JsonDocument"/> resolution to one of a set of <see cref="IDocumentResolver"/> instances.
/// </summary>
public class CompoundDocumentResolver : IDocumentResolver
{
    private readonly IDocumentResolver[] documentResolvers;
    private readonly Dictionary<string, JsonDocument> documents = [];
    private bool disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompoundDocumentResolver"/> class.
    /// </summary>
    /// <param name="documentResolvers">The document resolvers to which to delegate.</param>
    /// <remarks>Note that we take ownership of the lifecycle of the document resolvers passed to us.</remarks>
    public CompoundDocumentResolver(params IDocumentResolver[] documentResolvers)
    {
        this.documentResolvers = documentResolvers;
    }

    /// <inheritdoc/>
    public bool AddDocument(string uri, JsonDocument document)
    {
        this.CheckDisposed();
        return this.documents.TryAdd(uri, document);
    }

    /// <inheritdoc/>
    public async Task<JsonElement?> TryResolve(JsonReference reference)
    {
        this.CheckDisposed();
        string uri = reference.Uri.ToString();

        if (this.documents.TryGetValue(uri, out JsonDocument? result))
        {
            if (JsonPointerUtilities.TryResolvePointer(result, reference.Fragment, out JsonElement? element))
            {
                return element;
            }

            return default;
        }

        foreach (IDocumentResolver resolver in this.documentResolvers)
        {
            JsonElement? element = await resolver.TryResolve(reference).ConfigureAwait(false);
            if (element is JsonElement je)
            {
                return je;
            }
        }

        return default;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        this.CheckDisposed();

        this.DisposeDocumentsAndClear();

        foreach (IDocumentResolver resolver in this.documentResolvers)
        {
            resolver.Reset();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
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

                foreach (IDocumentResolver resolver in this.documentResolvers)
                {
                    resolver.Dispose();
                }
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
        if (this.disposedValue)
        {
            throw new ObjectDisposedException(nameof(CompoundDocumentResolver));
        }
    }
}