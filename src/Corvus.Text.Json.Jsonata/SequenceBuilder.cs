// <copyright file="SequenceBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Builds a <see cref="Sequence"/> by collecting values into a rented array.
/// The backing array is rented from <see cref="ArrayPool{T}"/> and must
/// be returned by the caller after the <see cref="Sequence"/> is no longer needed.
/// </summary>
internal struct SequenceBuilder
{
    private JsonElement[]? array;
    private int count;

    /// <summary>Gets the current count of values added.</summary>
    public readonly int Count => this.count;

    /// <summary>Gets or sets the element at the specified index.</summary>
    public readonly JsonElement this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.array![index];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => this.array![index] = value;
    }

    /// <summary>
    /// Adds a value to the builder.
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
    /// Adds all values from a sequence to the builder.
    /// </summary>
    public void AddRange(Sequence sequence)
    {
        for (int i = 0; i < sequence.Count; i++)
        {
            this.Add(sequence[i]);
        }
    }

    /// <summary>
    /// Builds the final <see cref="Sequence"/> from the collected values.
    /// </summary>
    /// <returns>
    /// A <see cref="Sequence"/>. For zero or one values, this is stack-only
    /// (no rented array). For multiple values, the backing array is kept rented
    /// and the caller is responsible for returning it via <see cref="ReturnArray"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Sequence ToSequence()
    {
        if (this.count == 0)
        {
            return Sequence.Undefined;
        }

        if (this.count == 1)
        {
            return new Sequence(this.array![0]);
        }

        return new Sequence(this.array!, this.count);
    }

    /// <summary>
    /// Returns the rented backing array to the pool.
    /// Call this after the <see cref="Sequence"/> returned by <see cref="ToSequence"/>
    /// is no longer needed.
    /// </summary>
    public void ReturnArray()
    {
        if (this.array is not null)
        {
            ArrayPool<JsonElement>.Shared.Return(this.array);
            this.array = null;
            this.count = 0;
        }
    }

    /// <summary>
    /// Resets the builder to empty without returning the backing array.
    /// The existing array is reused for subsequent <see cref="Add"/> calls.
    /// </summary>
    public void Clear()
    {
        this.count = 0;
    }

    private void Grow()
    {
        int newCapacity = this.array!.Length * 2;
        var newArray = ArrayPool<JsonElement>.Shared.Rent(newCapacity);
        Array.Copy(this.array, newArray, this.count);
        ArrayPool<JsonElement>.Shared.Return(this.array);
        this.array = newArray;
    }
}