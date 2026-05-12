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
/// <remarks>
/// When all added values are raw double singletons (e.g. arithmetic results),
/// this builder defers materialization into <see cref="JsonElement"/> by storing
/// the doubles in a separate pool-rented <c>double[]</c>. The resulting
/// <see cref="Sequence"/> is then a <see cref="Sequence.IsRawDoubleArray"/> variant,
/// allowing consumers like <see cref="JsonataHelpers.ArrayFromSequence"/> to write
/// the doubles directly into a mutable document via <c>Source(double)</c> without
/// creating intermediate <c>FixedJsonValueDocument</c> instances.
/// </remarks>
internal struct SequenceBuilder
{
    private JsonElement[]? array;
    private double[]? doubleArray;
    private int count;
    private int rawDoubleCount;
    private JsonWorkspace? doubleWorkspace;

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
        // Switching from all-doubles mode to mixed: materialize pending doubles.
        if (this.rawDoubleCount > 0 && this.rawDoubleCount == this.count)
        {
            this.MaterializePendingDoubles();
        }

        this.EnsureElementCapacity();
        this.array![this.count++] = value;
    }

    /// <summary>
    /// Adds all values from a sequence to the builder.
    /// </summary>
    public void AddRange(Sequence sequence)
    {
        // Fast path: singleton raw doubles stay deferred.
        if (sequence.IsSingleton && sequence.IsRawDouble)
        {
            // Only defer if we're still in all-doubles mode (or empty).
            if (this.rawDoubleCount == this.count)
            {
                sequence.TryGetDouble(out double d);
                this.EnsureDoubleCapacity();
                this.doubleArray![this.rawDoubleCount++] = d;
                this.count++;
                this.doubleWorkspace = sequence.RawDoubleWorkspace;
                return;
            }

            // Mixed mode: materialize this double immediately.
            this.EnsureElementCapacity();
            this.array![this.count++] = sequence.FirstOrDefault;
            return;
        }

        // Non-double elements: leave all-doubles mode if needed.
        if (this.rawDoubleCount > 0 && this.rawDoubleCount == this.count)
        {
            this.MaterializePendingDoubles();
        }

        for (int i = 0; i < sequence.Count; i++)
        {
            this.EnsureElementCapacity();
            this.array![this.count++] = sequence[i];
        }
    }

    /// <summary>
    /// Builds the final <see cref="Sequence"/> from the collected values.
    /// </summary>
    /// <returns>
    /// A <see cref="Sequence"/>. For zero or one values, this is stack-only
    /// (no rented array). For multiple values, ownership of the backing array
    /// transfers to the returned <see cref="Sequence"/>; the builder's reference
    /// is nulled so that a subsequent <see cref="ReturnArray"/> is a safe no-op.
    /// When all values are raw doubles, returns a <see cref="Sequence.IsRawDoubleArray"/>
    /// variant backed by the rented <c>double[]</c>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sequence ToSequence()
    {
        if (this.count == 0)
        {
            // Return any rented arrays — the Sequence doesn't need them.
            this.ReturnArraysToPool();
            return Sequence.Undefined;
        }

        // All-doubles path: defer materialization through the Sequence.
        if (this.rawDoubleCount == this.count)
        {
            if (this.count == 1)
            {
                var result = Sequence.FromDouble(this.doubleArray![0], this.doubleWorkspace!);
                this.ReturnArraysToPool();
                return result;
            }

            // Transfer ownership of the double array to the Sequence.
            var da = this.doubleArray!;
            this.doubleArray = null;

            // Return element array if rented (not needed).
            this.ReturnElementArray();
            return Sequence.FromDoubleArray(da, this.rawDoubleCount, this.doubleWorkspace!);
        }

        if (this.count == 1)
        {
            var result = new Sequence(this.array![0]);
            this.ReturnArraysToPool();
            return result;
        }

        // Transfer ownership of the element array to the Sequence.
        var arr = this.array!;
        this.array = null;

        // Return double array if rented (not needed after materialization).
        this.ReturnDoubleArray();
        return new Sequence(arr, this.count);
    }

    /// <summary>
    /// Returns the rented backing array to the pool.
    /// Call this after the <see cref="Sequence"/> returned by <see cref="ToSequence"/>
    /// is no longer needed.
    /// </summary>
    /// <remarks>
    /// After <see cref="ToSequence"/>, this is always a safe no-op: for count ≤ 1 the
    /// arrays were already returned inside <see cref="ToSequence"/>; for count ≥ 2
    /// ownership transferred to the <see cref="Sequence"/>.
    /// </remarks>
    public void ReturnArray()
    {
        this.ReturnArraysToPool();
        this.count = 0;
        this.rawDoubleCount = 0;
    }

    /// <summary>
    /// Resets the builder to empty without returning the backing array.
    /// The existing array is reused for subsequent <see cref="Add"/> calls.
    /// </summary>
    public void Clear()
    {
        this.count = 0;
        this.rawDoubleCount = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReturnArraysToPool()
    {
        this.ReturnElementArray();
        this.ReturnDoubleArray();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReturnElementArray()
    {
        if (this.array is not null)
        {
            ArrayPool<JsonElement>.Shared.Return(this.array);
            this.array = null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReturnDoubleArray()
    {
        if (this.doubleArray is not null)
        {
            ArrayPool<double>.Shared.Return(this.doubleArray);
            this.doubleArray = null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureElementCapacity()
    {
        if (this.array is null)
        {
            this.array = ArrayPool<JsonElement>.Shared.Rent(8);
        }
        else if (this.count == this.array.Length)
        {
            this.GrowElementArray();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureDoubleCapacity()
    {
        if (this.doubleArray is null)
        {
            this.doubleArray = ArrayPool<double>.Shared.Rent(8);
        }
        else if (this.rawDoubleCount == this.doubleArray.Length)
        {
            this.GrowDoubleArray();
        }
    }

    private void MaterializePendingDoubles()
    {
        this.EnsureElementCapacity();

        // Ensure element array has room for all pending doubles.
        while (this.array!.Length < this.rawDoubleCount)
        {
            this.GrowElementArray();
        }

        for (int i = 0; i < this.rawDoubleCount; i++)
        {
            this.array[i] = JsonataHelpers.NumberFromDouble(this.doubleArray![i], this.doubleWorkspace!);
        }

        // Free the double array (no longer needed).
        this.ReturnDoubleArray();

        this.rawDoubleCount = 0;
    }

    private void GrowElementArray()
    {
        int newCapacity = this.array!.Length * 2;
        var newArray = ArrayPool<JsonElement>.Shared.Rent(newCapacity);
        Array.Copy(this.array, newArray, this.count);
        ArrayPool<JsonElement>.Shared.Return(this.array);
        this.array = newArray;
    }

    private void GrowDoubleArray()
    {
        int newCapacity = this.doubleArray!.Length * 2;
        var newArray = ArrayPool<double>.Shared.Rent(newCapacity);
        Array.Copy(this.doubleArray, newArray, this.rawDoubleCount);
        ArrayPool<double>.Shared.Return(this.doubleArray);
        this.doubleArray = newArray;
    }
}