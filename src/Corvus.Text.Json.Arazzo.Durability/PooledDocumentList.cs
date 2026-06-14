// <copyright file="PooledDocumentList.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A read-only batch of pooled documents whose lifetime the consumer owns: enumerate the document values, then
/// <see cref="Dispose"/> the batch once to return every backing buffer to the pool. A consumer that needs a value to
/// outlive the batch clones it out first (see <see cref="PersistedJson.ToPooledDocument{T}"/> for the per-document
/// rationale). Indexing/enumeration yield the document <em>values</em> (root elements); they are valid only until the
/// batch is disposed.
/// </summary>
/// <typeparam name="T">The Corvus.Text.Json document type.</typeparam>
public sealed class PooledDocumentList<T> : IReadOnlyList<T>, IDisposable
    where T : struct, IJsonElement<T>
{
    private readonly List<ParsedJsonDocument<T>> documents;

    /// <summary>Initializes a new instance of the <see cref="PooledDocumentList{T}"/> class, taking ownership of the documents.</summary>
    /// <param name="documents">The pooled documents this batch owns and will dispose.</param>
    public PooledDocumentList(List<ParsedJsonDocument<T>> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        this.documents = documents;
    }

    /// <summary>Gets an empty batch (owns nothing).</summary>
    public static PooledDocumentList<T> Empty { get; } = new([]);

    /// <inheritdoc/>
    public int Count => this.documents.Count;

    /// <inheritdoc/>
    public T this[int index] => this.documents[index].RootElement;

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        foreach (ParsedJsonDocument<T> document in this.documents)
        {
            yield return document.RootElement;
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (ParsedJsonDocument<T> document in this.documents)
        {
            document.Dispose();
        }

        this.documents.Clear();
    }
}