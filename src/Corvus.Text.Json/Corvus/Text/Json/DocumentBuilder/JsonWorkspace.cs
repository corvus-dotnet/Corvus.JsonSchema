// <copyright file="JsonWorkspace.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Collections.Generic;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

/// <summary>
/// A workspace for manipulating JSON documents.
/// </summary>
public class JsonWorkspace : IDisposable
{
    private static readonly JsonWriterOptions s_internalWriterOptions = new() { Indented = false };

    private IJsonDocument[] _documents;

    private readonly Dictionary<IJsonDocument, int> _documentIndices;

    private int _length;

    private bool _rented;

    internal JsonWorkspace(bool rented, int initialDocumentCapacity = 5, JsonWriterOptions? options = null)
    {
        _documentIndices = new Dictionary<IJsonDocument, int>(initialDocumentCapacity);
        _documents = ArrayPool<IJsonDocument>.Shared.Rent(initialDocumentCapacity);
        _length = 0;
        Options = options ?? s_internalWriterOptions;
        _rented = rented;
    }

    /// <summary>
    /// Gets the JsonWriterOptions
    /// </summary>
    public JsonWriterOptions Options { get; private set; }

    /// <summary>
    /// Creates an instance of a <see cref="JsonWorkspace"/>.
    /// </summary>
    /// <param name="initialDocumentCapacity">The initial document capacity for the workspace.</param>
    /// <param name="options">The ambient <see cref="JsonWriterOptions"/>.</param>
    /// <returns>The <see cref="JsonWorkspace"/>.</returns>
    public static JsonWorkspace Create(int initialDocumentCapacity = 5, JsonWriterOptions? options = null)
    {
        return JsonWorkspaceCache.RentWorkspace(initialDocumentCapacity, options);
    }

    /// <summary>
    /// Creates an instance of a <see cref="JsonWorkspace"/>.
    /// </summary>
    /// <param name="initialDocumentCapacity">The initial document capacity for the workspace.</param>
    /// <param name="options">The ambient <see cref="JsonWriterOptions"/>.</param>
    /// <returns>The <see cref="JsonWorkspace"/>.</returns>
    public static JsonWorkspace CreateUnrented(int initialDocumentCapacity = 5, JsonWriterOptions? options = null)
    {
        return new(false, initialDocumentCapacity, options);
    }

    /// <summary>
    /// Rents a UTF-8 JSON writer and associated buffer writer from the pool.
    /// </summary>
    /// <param name="defaultBufferSize">The default buffer size to use for the buffer writer.</param>
    /// <param name="bufferWriter">When this method returns, contains the rented buffer writer.</param>
    /// <returns>A rented UTF-8 JSON writer configured with the workspace options.</returns>
    public Utf8JsonWriter RentWriterAndBuffer(int defaultBufferSize, out IByteBufferWriter bufferWriter)
    {
        Utf8JsonWriter result = Utf8JsonWriterCache.RentWriterAndBuffer(Options, defaultBufferSize, out PooledByteBufferWriter writer);
        bufferWriter = writer;
        return result;
    }

    /// <summary>
    /// Rents a UTF-8 JSON writer from the pool that writes to the specified buffer writer.
    /// </summary>
    /// <param name="bufferWriter">The buffer writer to write JSON data to.</param>
    /// <returns>A rented UTF-8 JSON writer configured with the workspace options.</returns>
    public Utf8JsonWriter RentWriter(IBufferWriter<byte> bufferWriter)
    {
        return Utf8JsonWriterCache.RentWriter(Options, bufferWriter);
    }

#pragma warning disable CA1822 // Mark members as static

    /// <summary>
    /// Returns a rented UTF-8 JSON writer and buffer writer to the pool.
    /// </summary>
    /// <param name="writer">The writer to return to the pool.</param>
    /// <param name="bufferWriter">The buffer writer to return to the pool.</param>
    public void ReturnWriterAndBuffer(Utf8JsonWriter writer, IByteBufferWriter bufferWriter)
    {
        Utf8JsonWriterCache.ReturnWriterAndBuffer(writer, bufferWriter);
    }

    /// <summary>
    /// Returns a rented UTF-8 JSON writer to the pool.
    /// </summary>
    /// <param name="writer">The writer to return to the pool.</param>
    public void ReturnWriter(Utf8JsonWriter writer)
    {
        Utf8JsonWriterCache.ReturnWriter(writer);
    }

#pragma warning restore CA1822 // Mark members as static

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize

    /// <summary>
    /// Disposes the workspace. If the workspace was rented from the cache, returns it;
    /// otherwise disposes all child documents and returns the backing array to the pool.
    /// </summary>
    public void Dispose()
    {
        if (_rented)
        {
            JsonWorkspaceCache.ReturnWorkspace(this);
        }
        else
        {
            // Dispose the documents
            DisposeMutable();

            if (_length > 0)
            {
                ArrayPool<IJsonDocument>.Shared.Return(_documents);
                _documentIndices.Clear();
                _length = -1;
            }
        }
    }

#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

    /// <summary>
    /// Creates a document builder for building mutable JSON documents from an existing element.
    /// </summary>
    /// <typeparam name="TElement">The type of the source JSON element.</typeparam>
    /// <typeparam name="TMutableElement">The type of the mutable JSON element to build.</typeparam>
    /// <param name="sourceElement">The source element to build from.</param>
    /// <returns>A document builder for the mutable element type.</returns>
    [CLSCompliant(false)]
    public JsonDocumentBuilder<TMutableElement> CreateBuilder<TElement, TMutableElement>(TElement sourceElement)
        where TElement : struct, IJsonElement<TElement>
        where TMutableElement : struct, IMutableJsonElement<TMutableElement>
    {
        JsonDocumentBuilder<TMutableElement> result = new(this);
        int index = GetDocumentIndex(result);
        result.Initialize(sourceElement, index, convertToAlloc: false);
        return result;
    }

    /// <summary>
    /// Creates a document builder for building mutable JSON documents.
    /// </summary>
    /// <typeparam name="TElement">The type of the mutable JSON element to build.</typeparam>
    /// <param name="initialCapacity">The initial capacity for the document builder.</param>
    /// <param name="initialValueBufferSize">The initial size of the value buffer.</param>
    /// <returns>A document builder for the specified element type.</returns>
    [CLSCompliant(false)]
    public JsonDocumentBuilder<TElement> CreateBuilder<TElement>(int initialCapacity = 30, int initialValueBufferSize = 8192)
        where TElement : struct, IMutableJsonElement<TElement>
    {
        JsonDocumentBuilder<TElement> result = new(this);
        int index = GetDocumentIndex(result);
        result.Initialize(index, initialCapacity, initialValueBufferSize);
        return result;
    }

    /// <summary>
    /// Gets the document at the specified index.
    /// </summary>
    /// <param name="index">The index of the document to retrieve.</param>
    /// <returns>The document at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range.</exception>
    internal IJsonDocument GetDocument(int index)
    {
        if (index < 0 || index >= _length)
        {
            throw new ArgumentOutOfRangeException(SR.ArgumentOutOfRange_IndexMustBeLess);
        }

        return _documents[index];
    }

    /// <summary>
    /// Gets the index of the specified document in the workspace.
    /// </summary>
    /// <param name="document">The document to find the index for.</param>
    /// <returns>The index of the document in the workspace.</returns>
    internal int GetDocumentIndex(IJsonDocument document)
    {
        if (_documentIndices.TryGetValue(document, out int index))
        {
            return index;
        }

        if (_documents.Length == _length)
        {
            IJsonDocument[] newDocuments = ArrayPool<IJsonDocument>.Shared.Rent(_length * 2);
            Array.Copy(_documents, newDocuments, _length);
            IJsonDocument[] documentsToReturn = _documents;
            _documents = newDocuments;
            ArrayPool<IJsonDocument>.Shared.Return(documentsToReturn);
        }

        int result = _length;
        _documents[_length++] = document;
        _documentIndices.Add(document, result);
        return result;
    }

    /// <summary>
    /// Resets the workspace with new capacity and options.
    /// </summary>
    /// <param name="initialDocumentCapacity">The initial document capacity for the workspace.</param>
    /// <param name="options">The JSON writer options to use.</param>
    internal void Reset(int initialDocumentCapacity, JsonWriterOptions? options)
    {
        Options = options ?? s_internalWriterOptions;
        if (_documents.Length < initialDocumentCapacity)
        {
            ArrayPool<IJsonDocument>.Shared.Return(_documents);
            _documents = ArrayPool<IJsonDocument>.Shared.Rent(initialDocumentCapacity);
        }
        else
        {
            if (_length > 0)
            {
                Array.Clear(_documents, 0, _length);
            }
        }

        _documentIndices.Clear();
        _length = 0;
    }

    /// <summary>
    /// Resets all state for cache reuse, disposing any documents and clearing collections.
    /// </summary>
    internal void ResetAllStateForCacheReuse()
    {
        if (_length >= 0)
        {
            DisposeMutable();

            Array.Clear(_documents, 0, _length);
            _length = -1;
            _documentIndices.Clear();
            return;
        }

        ThrowHelper.ThrowObjectDisposedException_JsonWorkspace();
    }

    private void DisposeMutable()
    {
        foreach (IJsonDocument document in _documents.AsSpan(0, _length))
        {
            if (document is IMutableJsonDocument)
            {
                document.Dispose();
            }
        }
    }

    /// <summary>
    /// Creates an empty instance for caching purposes.
    /// </summary>
    /// <returns>An empty workspace instance suitable for caching.</returns>
    internal static JsonWorkspace CreateEmptyInstanceForCaching() => new(true);
}