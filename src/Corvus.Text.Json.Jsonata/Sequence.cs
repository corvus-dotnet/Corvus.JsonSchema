// <copyright file="Sequence.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

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
    private readonly LambdaValue? lambda;
    private readonly Regex? regex;
    private readonly TailCallContinuation? tailCall;
    private readonly double nonFiniteValue;
    private readonly Sequence[]? tupleItems;
    private readonly Dictionary<string, LambdaValue>? objectLambdas;

    /// <summary>
    /// Initializes a new instance of the <see cref="Sequence"/> struct with a single value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sequence(in JsonElement value)
    {
        this.singleValue = value;
        this.multiValues = null;
        this.count = 1;
        this.lambda = null;
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

        this.lambda = null;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="Sequence"/> struct wrapping a lambda value.
    /// </summary>
    /// <param name="lambda">The lambda (function) value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sequence(LambdaValue lambda)
    {
        this.lambda = lambda;
        this.count = 0;
        this.singleValue = default;
        this.multiValues = null;
        this.regex = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sequence"/> struct wrapping a compiled regular expression.
    /// </summary>
    /// <param name="regex">The compiled regular expression.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sequence(Regex regex)
    {
        this.regex = regex;
        this.count = 0;
        this.singleValue = default;
        this.multiValues = null;
        this.lambda = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sequence"/> struct wrapping a tail-call continuation.
    /// </summary>
    /// <param name="tailCall">The tail-call continuation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sequence(TailCallContinuation tailCall)
    {
        this.tailCall = tailCall;
        this.count = 0;
        this.singleValue = default;
        this.multiValues = null;
        this.lambda = null;
        this.regex = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sequence"/> struct wrapping a non-finite numeric value
    /// (Infinity or NaN) that cannot be represented as a JSON number.
    /// </summary>
    /// <param name="nonFiniteValue">The non-finite double value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sequence(double nonFiniteValue)
    {
        Debug.Assert(double.IsInfinity(nonFiniteValue) || double.IsNaN(nonFiniteValue), "Use this constructor only for non-finite values");
        this.nonFiniteValue = nonFiniteValue;
        this.count = 0;
        this.singleValue = default;
        this.multiValues = null;
        this.lambda = null;
        this.regex = null;
        this.tailCall = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sequence"/> struct as a tuple array
    /// of sub-sequences. Used when an array constructor contains non-JSON values (e.g. lambdas)
    /// that must be preserved without reification into a JSON array.
    /// </summary>
    /// <param name="tupleItems">The sub-sequences, one per array element.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sequence(Sequence[] tupleItems)
    {
        this.tupleItems = tupleItems;
        this.count = tupleItems.Length;
        this.singleValue = default;
        this.multiValues = null;
        this.lambda = null;
        this.regex = null;
        this.tailCall = null;
        this.nonFiniteValue = 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sequence"/> struct wrapping a single JSON element
    /// that has associated lambda property values. Used when an object constructor creates
    /// an object with function-valued properties (e.g. the custom matcher protocol's <c>next</c> property).
    /// </summary>
    /// <param name="value">The JSON element.</param>
    /// <param name="objectLambdas">Dictionary mapping property names to their lambda values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sequence(in JsonElement value, Dictionary<string, LambdaValue> objectLambdas)
    {
        this.singleValue = value;
        this.multiValues = null;
        this.count = 1;
        this.lambda = null;
        this.regex = null;
        this.tailCall = null;
        this.nonFiniteValue = 0;
        this.objectLambdas = objectLambdas;
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
        get => this.count == 0 && this.lambda is null && this.regex is null && this.tailCall is null && !this.IsNonFinite;
    }

    /// <summary>Gets a value indicating whether this is a singleton sequence.</summary>
    public bool IsSingleton
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.count == 1;
    }

    /// <summary>Gets a value indicating whether this sequence holds a lambda (function) value.</summary>
    public bool IsLambda
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.lambda is not null;
    }

    /// <summary>Gets the lambda value, or <c>null</c> if this is not a lambda.</summary>
    public LambdaValue? Lambda
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.lambda;
    }

    /// <summary>Gets a value indicating whether this sequence holds a regular expression value.</summary>
    public bool IsRegex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.regex is not null;
    }

    /// <summary>Gets the compiled regular expression, or <c>null</c> if this is not a regex.</summary>
    public Regex? Regex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.regex;
    }

    /// <summary>Gets a value indicating whether this sequence holds a tail-call continuation.</summary>
    public bool IsTailCall
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.tailCall is not null;
    }

    /// <summary>Gets the tail-call continuation, or <c>null</c> if this is not a tail call.</summary>
    public TailCallContinuation? TailCall
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.tailCall;
    }

    /// <summary>Gets a value indicating whether this sequence holds a non-finite numeric value (Infinity or NaN).</summary>
    public bool IsNonFinite
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => double.IsInfinity(this.nonFiniteValue) || double.IsNaN(this.nonFiniteValue);
    }

    /// <summary>Gets the non-finite double value. Only valid when <see cref="IsNonFinite"/> is <c>true</c>.</summary>
    public double NonFiniteValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.nonFiniteValue;
    }

    /// <summary>Gets a value indicating whether this is a tuple sequence holding sub-sequences (e.g. an array containing lambdas).</summary>
    public bool IsTupleSequence
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.tupleItems is not null;
    }

    /// <summary>
    /// Gets the lambda properties associated with this object element, if any.
    /// Used by the custom matcher protocol to retrieve function-valued properties
    /// (such as <c>next</c>) that cannot be represented in JSON.
    /// </summary>
    public Dictionary<string, LambdaValue>? ObjectLambdas
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.objectLambdas;
    }

    /// <summary>
    /// Gets the full sub-sequence at the given index. For tuple sequences, this returns the
    /// original sub-sequence (which may be a lambda or other non-JSON variant). For non-tuple
    /// sequences, this wraps the JSON element at the index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sequence GetItemSequence(int index)
    {
        if (this.tupleItems is not null)
        {
            return this.tupleItems[index];
        }

        return new Sequence(this[index]);
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

            if (this.tupleItems is not null)
            {
                return this.tupleItems[index].FirstOrDefault;
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

            if (this.tupleItems is not null)
            {
                return this.tupleItems[0].FirstOrDefault;
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