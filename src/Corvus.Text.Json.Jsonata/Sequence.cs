// <copyright file="Sequence.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// A lightweight value type representing a JSONata result sequence.
/// </summary>
/// <remarks>
/// <para>
/// JSONata operations produce sequences of zero, one, or many JSON values.
/// The <c>Sequence</c> type is optimised for the common singleton case
/// (one result) with zero allocation. Multi-value sequences use a
/// workspace-pooled backing array.
/// </para>
/// <para>
/// A sequence can be in one of three states:
/// <list type="bullet">
///   <item><description><b>Undefined</b> — no values (<see cref="IsUndefined"/> is <c>true</c>).</description></item>
///   <item><description><b>Singleton</b> — exactly one value, stored inline.</description></item>
///   <item><description><b>Multi</b> — two or more values, backed by a pooled array.</description></item>
/// </list>
/// </para>
/// </remarks>
internal readonly struct Sequence
{
    /// <summary>An empty (undefined) sequence.</summary>
    public static readonly Sequence Undefined = default;

    private readonly JsonElement singleValue;
    private readonly JsonElement[]? multiValues;
    private readonly int count;

    /// <summary>
    /// Initializes a new instance of the <see cref="Sequence"/> struct with a single value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sequence(in JsonElement value)
    {
        this.singleValue = value;
        this.multiValues = null;
        this.count = 1;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sequence"/> struct with multiple values.
    /// </summary>
    /// <param name="values">The backing array (typically rented from a pool).</param>
    /// <param name="count">The number of valid elements in the array.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sequence(JsonElement[] values, int count)
    {
        Debug.Assert(count >= 0, "Count must be non-negative");
        Debug.Assert(values.Length >= count, "Array must be large enough for count");

        if (count == 0)
        {
            this = default;
        }
        else if (count == 1)
        {
            this.singleValue = values[0];
            this.multiValues = null;
            this.count = 1;
        }
        else
        {
            this.singleValue = default;
            this.multiValues = values;
            this.count = count;
        }
    }

    /// <summary>Gets the number of values in the sequence.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.count;
    }

    /// <summary>Gets a value indicating whether this is an undefined (empty) sequence.</summary>
    public bool IsUndefined
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.count == 0;
    }

    /// <summary>Gets a value indicating whether this is a singleton sequence.</summary>
    public bool IsSingleton
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.count == 1;
    }

    /// <summary>
    /// Gets the element at the specified index.
    /// </summary>
    public JsonElement this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)this.count)
            {
                ThrowIndexOutOfRange();
            }

            if (this.multiValues is not null)
            {
                return this.multiValues[index];
            }

            Debug.Assert(index == 0, "Singleton sequence index must be 0");
            return this.singleValue;
        }
    }

    /// <summary>
    /// Gets the first (and in singleton case, only) value.
    /// Returns the <c>default</c> <see cref="JsonElement"/> if undefined.
    /// </summary>
    public JsonElement FirstOrDefault
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (this.count == 0)
            {
                return default;
            }

            return this.multiValues is not null ? this.multiValues[0] : this.singleValue;
        }
    }

    /// <summary>
    /// Creates a singleton sequence from a <see cref="JsonElement"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Sequence(in JsonElement value) => new(in value);

    /// <summary>
    /// Returns an enumerator over the values in this sequence.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(this);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowIndexOutOfRange()
    {
        throw new IndexOutOfRangeException();
    }

    /// <summary>
    /// Enumerator for iterating over sequence values without allocation.
    /// </summary>
    public struct Enumerator
    {
        private readonly Sequence sequence;
        private int index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(Sequence sequence)
        {
            this.sequence = sequence;
            this.index = -1;
        }

        /// <summary>Gets the current element.</summary>
        public readonly JsonElement Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.sequence[this.index];
        }

        /// <summary>Advances to the next element.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int next = this.index + 1;
            if (next < this.sequence.count)
            {
                this.index = next;
                return true;
            }

            return false;
        }
    }
}