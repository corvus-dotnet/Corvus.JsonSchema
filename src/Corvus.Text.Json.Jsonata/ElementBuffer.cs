// <copyright file="ElementBuffer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Lightweight ArrayPool-backed buffer for collecting <see cref="JsonElement"/> values.
/// Used by generated code to accumulate intermediate navigation results
/// without the MetadataDb overhead of <see cref="JsonElement.CreateArrayBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the code-generation equivalent of the runtime's internal <see cref="SequenceBuilder"/>.
/// The runtime operates on <see cref="Sequence"/> (a 32-byte tagged union) throughout its
/// path evaluation and only materializes to <see cref="JsonElement"/> at the boundary.
/// Generated code operates on <see cref="JsonElement"/> directly, so this buffer stores
/// elements and materializes to a result <see cref="JsonElement"/> via <see cref="ToResult"/>.
/// </para>
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

    /// <summary>Gets the element at the specified index.</summary>
    public readonly JsonElement this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.array![index];
    }

    /// <summary>
    /// Adds an element to the buffer.
    /// </summary>
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
    /// Adds each element of an array to the buffer (auto-flatten one level).
    /// If <paramref name="value"/> is not an array, adds it directly.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddFlatten(in JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in value.EnumerateArray())
            {
                this.Add(child);
            }
        }
        else if (value.ValueKind != JsonValueKind.Undefined)
        {
            this.Add(value);
        }
    }

    /// <summary>
    /// Materializes the buffer contents as a single <see cref="JsonElement"/>.
    /// Returns <c>default</c> for empty, the single element for count == 1,
    /// or a new array element for multiple elements.
    /// </summary>
    public readonly JsonElement ToResult(JsonWorkspace workspace)
    {
        if (this.count == 0)
        {
            return default;
        }

        if (this.count == 1)
        {
            return this.array![0];
        }

        return this.BuildArray(workspace);
    }

    /// <summary>
    /// Materializes the buffer contents as a JSON array <see cref="JsonElement"/>.
    /// Always returns an array, even for empty or single-element buffers.
    /// Used by <c>$map</c> which always produces an array result.
    /// </summary>
    public readonly JsonElement ToArrayResult(JsonWorkspace workspace)
    {
        if (this.count == 0)
        {
            return JsonataHelpers.EmptyArray();
        }

        return this.BuildArray(workspace);
    }

    /// <summary>
    /// Builds a JSON array from the buffer contents using the CVB (CreateBuilder + ArrayBuilder)
    /// pattern, which writes directly to the MetadataDb without per-item staleness checks or
    /// array-length scans.
    /// </summary>
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

    /// <summary>
    /// Returns the rented array to the pool and resets the buffer.
    /// </summary>
    public void Dispose()
    {
        if (this.array is not null)
        {
            ArrayPool<JsonElement>.Shared.Return(this.array);
            this.array = null;
        }

        this.count = 0;
    }

    private void Grow()
    {
        int newCapacity = this.array!.Length * 2;
        JsonElement[] newArray = ArrayPool<JsonElement>.Shared.Rent(newCapacity);
        Array.Copy(this.array, newArray, this.count);
        ArrayPool<JsonElement>.Shared.Return(this.array);
        this.array = newArray;
    }
}