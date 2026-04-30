// <copyright file="JsonDocumentBuilderSnapshot.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Threading;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

/// <summary>
/// A snapshot of a <see cref="JsonDocumentBuilder{T}"/>'s state that can be used to cheaply
/// restore a builder to its initial state via <see cref="JsonDocumentBuilder{T}.Restore"/>,
/// avoiding the cost of re-traversing the source document.
/// </summary>
/// <typeparam name="T">The type of mutable JSON element the snapshotted builder works with.</typeparam>
[CLSCompliant(false)]
public sealed class JsonDocumentBuilderSnapshot<T> : IDisposable
    where T : struct, IMutableJsonElement<T>
{
    private byte[]? _metadataBytes;

    internal JsonDocumentBuilderSnapshot(
        byte[] metadataBytes,
        int metadataLength,
        byte[]? valueBacking,
        int valueOffset,
        byte[]? propertyMapBacking,
        int propertyMapOffset,
        int[]? bucketsBacking,
        int bucketOffset,
        byte[]? entriesBacking,
        int entryOffset)
    {
        _metadataBytes = metadataBytes;
        MetadataLength = metadataLength;
        ValueBacking = valueBacking;
        ValueOffset = valueOffset;
        PropertyMapBacking = propertyMapBacking;
        PropertyMapOffset = propertyMapOffset;
        BucketsBacking = bucketsBacking;
        BucketOffset = bucketOffset;
        EntriesBacking = entriesBacking;
        EntryOffset = entryOffset;
    }

    internal byte[] MetadataBytes => _metadataBytes ?? throw new ObjectDisposedException(nameof(JsonDocumentBuilderSnapshot<T>));

    internal int MetadataLength { get; }

    internal byte[]? ValueBacking { get; }

    internal int ValueOffset { get; }

    internal byte[]? PropertyMapBacking { get; }

    internal int PropertyMapOffset { get; }

    internal int[]? BucketsBacking { get; }

    internal int BucketOffset { get; }

    internal byte[]? EntriesBacking { get; }

    internal int EntryOffset { get; }

    /// <summary>
    /// Releases the rented buffers held by this snapshot.
    /// </summary>
    public void Dispose()
    {
        byte[]? metadataBytes = Interlocked.Exchange(ref _metadataBytes, null);
        if (metadataBytes == null)
        {
            return;
        }

        ArrayPool<byte>.Shared.Return(metadataBytes);

        if (ValueBacking is byte[] valueBacking)
        {
            ArrayPool<byte>.Shared.Return(valueBacking);
        }

        if (PropertyMapBacking is byte[] propertyMapBacking)
        {
            ArrayPool<byte>.Shared.Return(propertyMapBacking);
        }

        if (BucketsBacking is int[] bucketsBacking)
        {
            ArrayPool<int>.Shared.Return(bucketsBacking);
        }

        if (EntriesBacking is byte[] entriesBacking)
        {
            ArrayPool<byte>.Shared.Return(entriesBacking);
        }
    }
}