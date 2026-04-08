// <copyright file="Sequence.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
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
    private readonly bool isRawDouble;
    private readonly JsonWorkspace? rawDoubleWorkspace;
    private readonly Sequence[]? tupleItems;
    private readonly Dictionary<string, LambdaValue>? objectLambdas;
    private readonly LazyRangeInfo? lazyRange;
    private readonly double[]? rawDoubleArray;

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
        Debug.Assert(double.IsInfinity(nonFiniteValue) || double.IsNaN(nonFiniteValue), "Use this constructor only for non-finite values; use FromDouble for finite doubles");
        this.nonFiniteValue = nonFiniteValue;
        this.count = 0;
        this.singleValue = default;
        this.multiValues = null;
        this.lambda = null;
        this.regex = null;
        this.tailCall = null;
    }

    /// <summary>
    /// Creates a <see cref="Sequence"/> holding a raw finite double value without materializing
    /// a <see cref="JsonElement"/>. The double is stored inline and only materialized to a
    /// <see cref="JsonElement"/> when needed (e.g. when stored into a JSON array or object).
    /// </summary>
    /// <param name="value">The finite double value.</param>
    /// <param name="workspace">The workspace used to materialize the double to a <see cref="JsonElement"/> on demand.</param>
    /// <returns>A singleton sequence representing the number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Sequence FromDouble(double value, JsonWorkspace workspace)
    {
        Debug.Assert(!double.IsInfinity(value) && !double.IsNaN(value), "Use Sequence(double) constructor for non-finite values");
        return new Sequence(value, workspace);
    }

    /// <summary>
    /// Private constructor for raw finite doubles.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Sequence(double value, JsonWorkspace workspace)
    {
        this.nonFiniteValue = value;
        this.isRawDouble = true;
        this.rawDoubleWorkspace = workspace;
        this.count = 1;
        this.singleValue = default;
        this.multiValues = null;
        this.lambda = null;
        this.regex = null;
        this.tailCall = null;
    }

    /// <summary>
    /// Creates a multi-value sequence backed by an array of raw doubles.
    /// The doubles are only materialized to <see cref="JsonElement"/> when accessed
    /// via the indexer. For bulk operations (e.g. building a JSON array), callers
    /// should use <see cref="IsRawDoubleArray"/> and <see cref="GetRawDoubleAt"/>
    /// to access the raw values directly.
    /// </summary>
    /// <param name="values">The rented double array (ownership transfers to this sequence).</param>
    /// <param name="count">The number of valid doubles in the array.</param>
    /// <param name="workspace">The workspace used for on-demand materialization.</param>
    /// <returns>A multi-value sequence of numbers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Sequence FromDoubleArray(double[] values, int count, JsonWorkspace workspace)
    {
        return new Sequence(values, count, workspace, isDoubleArray: true);
    }

    /// <summary>
    /// Private constructor for raw double arrays.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Sequence(double[] values, int count, JsonWorkspace workspace, bool isDoubleArray)
    {
        Debug.Assert(isDoubleArray);
        this.rawDoubleArray = values;
        this.rawDoubleWorkspace = workspace;
        this.count = count;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="Sequence"/> struct as a lazy range
    /// that generates number elements on demand. Used for large integer ranges
    /// (e.g. <c>[1..10000000]</c>) to avoid materializing millions of elements.
    /// </summary>
    /// <param name="rangeInfo">The lazy range bounds and workspace reference.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sequence(LazyRangeInfo rangeInfo)
    {
        this.lazyRange = rangeInfo;
        this.count = rangeInfo.End - rangeInfo.Start + 1;
        this.singleValue = default;
        this.multiValues = null;
        this.lambda = null;
        this.regex = null;
        this.tailCall = null;
        this.nonFiniteValue = 0;
    }

    /// <summary>Gets the number of values in the sequence.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.count;
    }

    /// <summary>
    /// Returns the multi-value backing array to <see cref="ArrayPool{T}"/>.
    /// Call this when the sequence is no longer needed and its elements have
    /// already been consumed or copied. This is a no-op for singleton, undefined,
    /// or non-array-backed sequences.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReturnBackingArray()
    {
        if (this.multiValues is not null)
        {
            ArrayPool<JsonElement>.Shared.Return(this.multiValues);
        }

        if (this.rawDoubleArray is not null)
        {
            ArrayPool<double>.Shared.Return(this.rawDoubleArray);
        }
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

    /// <summary>Gets a value indicating whether this sequence holds a raw finite double value.</summary>
    public bool IsRawDouble
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.isRawDouble;
    }

    /// <summary>Gets the workspace associated with a raw double sequence.</summary>
    internal JsonWorkspace? RawDoubleWorkspace
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.rawDoubleWorkspace;
    }

    /// <summary>Gets a value indicating whether this sequence is backed by an array of raw doubles.</summary>
    public bool IsRawDoubleArray
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.rawDoubleArray is not null;
    }

    /// <summary>Gets the raw double value at the specified index in a raw double array sequence.</summary>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The raw double value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetRawDoubleAt(int index)
    {
        Debug.Assert(this.rawDoubleArray is not null, "Not a raw double array sequence");
        return this.rawDoubleArray![index];
    }

    /// <summary>
    /// Tries to extract a double value from this sequence. Succeeds for raw doubles
    /// and for singleton number elements.
    /// </summary>
    /// <param name="value">When this method returns <c>true</c>, contains the double value.</param>
    /// <returns><c>true</c> if the sequence holds a numeric value; otherwise <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetDouble(out double value)
    {
        if (this.isRawDouble)
        {
            value = this.nonFiniteValue;
            return true;
        }

        if (this.count == 1 && this.singleValue.ValueKind == JsonValueKind.Number)
        {
            return this.singleValue.TryGetDouble(out value);
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Materializes this sequence's value as a <see cref="JsonElement"/>. For raw doubles,
    /// this creates a <see cref="JsonElement"/> in the stored workspace.
    /// For element-backed sequences, returns the element directly.
    /// </summary>
    /// <returns>The <see cref="JsonElement"/> representation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsonElement AsElement()
    {
        if (this.isRawDouble)
        {
            return JsonataHelpers.NumberFromDouble(this.nonFiniteValue, this.rawDoubleWorkspace!);
        }

        return this.FirstOrDefault;
    }

    /// <summary>
    /// Materializes this sequence's value as a <see cref="JsonElement"/>. For raw doubles,
    /// this creates a <see cref="JsonElement"/> in the given workspace.
    /// For element-backed sequences, returns the element directly.
    /// </summary>
    /// <param name="workspace">The workspace used to create number elements for raw doubles.</param>
    /// <returns>The <see cref="JsonElement"/> representation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsonElement AsElement(JsonWorkspace workspace)
    {
        if (this.isRawDouble)
        {
            return JsonataHelpers.NumberFromDouble(this.nonFiniteValue, workspace);
        }

        return this.FirstOrDefault;
    }

    /// <summary>
    /// Gets the <see cref="JsonValueKind"/> of this sequence's value without materializing.
    /// Returns <see cref="JsonValueKind.Number"/> for raw doubles and non-finite values.
    /// </summary>
    public JsonValueKind SequenceValueKind
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (this.isRawDouble || this.IsNonFinite)
            {
                return JsonValueKind.Number;
            }

            if (this.count > 0)
            {
                return this.FirstOrDefault.ValueKind;
            }

            return JsonValueKind.Undefined;
        }
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

    /// <summary>Gets a value indicating whether this is a lazy range sequence.</summary>
    public bool IsLazyRange
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.lazyRange is not null;
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

            if (this.isRawDouble)
            {
                Debug.Assert(index == 0, "Raw double sequence index must be 0");
                return JsonataHelpers.NumberFromDouble(this.nonFiniteValue, this.rawDoubleWorkspace!);
            }

            if (this.rawDoubleArray is not null)
            {
                return JsonataHelpers.NumberFromDouble(this.rawDoubleArray[index], this.rawDoubleWorkspace!);
            }

            if (this.lazyRange is not null)
            {
                return this.lazyRange.GetElement(index);
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
    /// <remarks>
    /// For raw double sequences, this materializes the double into a <see cref="JsonElement"/>
    /// using the stored workspace reference. Callers on the arithmetic hot path should use
    /// <see cref="TryGetDouble"/> instead to avoid this materialization.
    /// </remarks>
    public JsonElement FirstOrDefault
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (this.count == 0)
            {
                return default;
            }

            if (this.isRawDouble)
            {
                return JsonataHelpers.NumberFromDouble(this.nonFiniteValue, this.rawDoubleWorkspace!);
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

    /// <summary>
    /// Carries bounds and workspace reference for a lazily-evaluated integer range.
    /// Elements are generated on demand, avoiding the cost of materializing millions of <see cref="JsonElement"/> values.
    /// </summary>
    internal sealed class LazyRangeInfo
    {
        /// <summary>Gets the inclusive start of the range.</summary>
        public int Start { get; }

        /// <summary>Gets the inclusive end of the range.</summary>
        public int End { get; }

        private readonly JsonWorkspace workspace;

        /// <summary>
        /// Initializes a new instance of the <see cref="LazyRangeInfo"/> class.
        /// </summary>
        public LazyRangeInfo(int start, int end, JsonWorkspace workspace)
        {
            this.Start = start;
            this.End = end;
            this.workspace = workspace;
        }

        /// <summary>
        /// Gets the JSON number element at the specified index within the range.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsonElement GetElement(int index)
        {
            return JsonataHelpers.NumberFromDouble(this.Start + index, this.workspace);
        }
    }
}