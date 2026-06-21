// <copyright file="PooledDocumentList.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A read-only batch of pooled documents whose lifetime the consumer owns: enumerate the document values, then
/// <see cref="Dispose"/> the batch once to return every backing buffer — and the batch's own backing array — to the
/// pool. Build it by <see cref="Add"/>-ing documents the batch takes ownership of; the backing array is rented from
/// <see cref="ArrayPool{T}"/> and grown by re-renting, so a list query allocates no managed array of its own (only the
/// per-document pooled buffers, which are also returned on dispose). A consumer that needs a value to outlive the
/// batch clones it out first (see <see cref="PersistedJson.ToPooledDocument{T}"/>). Indexing/enumeration yield the
/// document <em>values</em> (root elements); they are valid only until the batch is disposed.
/// </summary>
/// <typeparam name="T">The Corvus.Text.Json document type.</typeparam>
public sealed class PooledDocumentList<T> : IReadOnlyList<T>, IDisposable
    where T : struct, IJsonElement<T>
{
    private const int InitialCapacity = 4;

    private ParsedJsonDocument<T>[]? rented;
    private int count;
    private bool ownershipTransferred;

    /// <summary>Initializes a new, empty batch; the backing array is rented lazily on the first <see cref="Add"/>.</summary>
    public PooledDocumentList()
    {
    }

    /// <summary>Initializes a new batch, renting a backing array sized for <paramref name="capacity"/> documents (use when the count is known up front).</summary>
    /// <param name="capacity">The expected document count.</param>
    public PooledDocumentList(int capacity)
    {
        if (capacity > 0)
        {
            this.rented = ArrayPool<ParsedJsonDocument<T>>.Shared.Rent(capacity);
        }
    }

    /// <summary>Gets an empty, read-only batch (owns nothing; <see cref="Dispose"/> is a no-op).</summary>
    public static PooledDocumentList<T> Empty { get; } = new();

    /// <inheritdoc/>
    public int Count => this.count;

    /// <inheritdoc/>
    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)this.count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return this.rented![index].RootElement;
        }
    }

    /// <summary>Appends a document the batch takes ownership of (disposed when the batch is disposed). Grows the rented backing array by re-renting when full.</summary>
    /// <param name="document">The pooled document to take ownership of.</param>
    public void Add(ParsedJsonDocument<T> document)
    {
        if (this.rented is null)
        {
            this.rented = ArrayPool<ParsedJsonDocument<T>>.Shared.Rent(InitialCapacity);
        }
        else if (this.count == this.rented.Length)
        {
            ParsedJsonDocument<T>[] larger = ArrayPool<ParsedJsonDocument<T>>.Shared.Rent(this.rented.Length * 2);
            Array.Copy(this.rented, larger, this.count);
            ArrayPool<ParsedJsonDocument<T>>.Shared.Return(this.rented, clearArray: true);
            this.rented = larger;
        }

        this.rented[this.count++] = document;
    }

    /// <summary>Sorts the batch in place (used by backends without a server-side ordering). Pass a cached singleton comparer to avoid a per-call allocation.</summary>
    /// <param name="comparer">A comparer over the pooled documents (typically a <see langword="static"/> singleton comparing root-element values).</param>
    public void Sort(IComparer<ParsedJsonDocument<T>> comparer)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        if (this.count > 1)
        {
            Array.Sort(this.rented!, 0, this.count, comparer);
        }
    }

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < this.count; i++)
        {
            yield return this.rented![i].RootElement;
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    /// <summary>
    /// Hands ownership of every document to <paramref name="workspace"/>, which then disposes them when it is disposed
    /// (at the end of the request). Use this when the batch's values must outlive the batch — e.g. a handler that
    /// projects the documents into a response body whose Source is materialized <em>after</em> the handler returns
    /// (response validation/serialization). The documents stay enumerable from this batch for that projection; a
    /// subsequent <see cref="Dispose"/> returns only the rented backing array, not the (now workspace-owned) documents.
    /// </summary>
    /// <param name="workspace">The workspace that will own and dispose the documents.</param>
    public void TransferOwnershipTo(JsonWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        for (int i = 0; i < this.count; i++)
        {
            workspace.TakeOwnership(this.rented![i]);
        }

        this.ownershipTransferred = true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.rented is null)
        {
            return;
        }

        // When ownership has been transferred to a workspace, the documents are disposed there — return only our own
        // rented backing array here.
        if (!this.ownershipTransferred)
        {
            for (int i = 0; i < this.count; i++)
            {
                this.rented[i]?.Dispose();
            }
        }

        ArrayPool<ParsedJsonDocument<T>>.Shared.Return(this.rented, clearArray: true);
        this.rented = null;
        this.count = 0;
    }
}