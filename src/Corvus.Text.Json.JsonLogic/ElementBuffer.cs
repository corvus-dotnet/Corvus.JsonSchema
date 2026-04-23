// <copyright file="ElementBuffer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.JsonLogic;

/// <summary>
/// Lightweight ArrayPool-backed buffer for collecting <see cref="JsonElement"/> values.
/// Used by generated code to accumulate intermediate results (filter, map, merge, missing)
/// without the MetadataDb overhead of repeated <c>Mutable.AddItem</c> calls.
/// </summary>
/// <remarks>
/// <para>
/// Always call <see cref="Dispose"/> (or use a <c>try/finally</c>) to return the rented
/// array to the pool.
/// </para>
/// </remarks>
public struct ElementBuffer : IDisposable
{
    private JsonElement[]? array;
    private int count;

    /// <summary>Gets the current number of elements in the buffer.</summary>
    public readonly int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.count;
    }

    /// <summary>
    /// Adds an element to the buffer.
    /// </summary>
    /// <param name="value">The element to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(in JsonElement value)
    {
        if (this.array is null)
        {
            this.array = ArrayPool<JsonElement>.Shared.Rent(8);
        }
        else if (this.count == this.array.Length)
        {
            this.Grow();
        }

        this.array[this.count++] = value;
    }

    /// <summary>
    /// Materializes the buffer contents as a JSON array <see cref="JsonElement"/>.
    /// Always returns an array, even for empty buffers.
    /// </summary>
    /// <param name="workspace">The workspace for document building.</param>
    /// <returns>A JSON array element containing the buffered items.</returns>
    public readonly JsonElement ToArrayResult(JsonWorkspace workspace)
    {
        if (this.count == 0)
        {
            return JsonLogicHelpers.EmptyArray();
        }

        return this.BuildArray(workspace);
    }

    /// <summary>
    /// Returns the rented array to the pool and resets the buffer.
    /// </summary>
    public void Dispose()
    {
        if (this.array is not null)
        {
            // Clear references to allow GC of source documents.
            Array.Clear(this.array, 0, this.count);
            ArrayPool<JsonElement>.Shared.Return(this.array);
            this.array = null;
        }

        this.count = 0;
    }

    private readonly JsonElement BuildArray(JsonWorkspace workspace)
    {
        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            (this.array!, this.count),
            static (in (JsonElement[] Arr, int Count) ctx, ref JsonElement.ArrayBuilder builder) =>
            {
                for (int i = 0; i < ctx.Count; i++)
                {
                    builder.AddItem(ctx.Arr[i]);
                }
            },
            estimatedMemberCount: this.count + 2);

        return (JsonElement)doc.RootElement;
    }

    private void Grow()
    {
        int newCapacity = this.array!.Length * 2;
        JsonElement[] newArray = ArrayPool<JsonElement>.Shared.Rent(newCapacity);
        Array.Copy(this.array, newArray, this.count);
        Array.Clear(this.array, 0, this.count);
        ArrayPool<JsonElement>.Shared.Return(this.array);
        this.array = newArray;
    }
}