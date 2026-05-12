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
public readonly struct Sequence
{
    // --- Tagged union layout: 32 bytes on x64 (was ~116) ---
    //
    // payload  (object?, 8 bytes)  — the single reference: array, lambda, regex, etc.
    // rawValue (double, 8 bytes)   — inline double (RawDouble, NonFinite)
    // singleValue (JE, 12 bytes)   — inline JsonElement for singleton/ObjectLambdas
    // countAndTag (int, 4 bytes)   — bits 0–23 = count, bits 24–31 = variant tag
    //
    // At most ONE reference-type field is non-null at a time, so a single object? suffices.
    private const int TagShift = 24;
    private const int CountMask = 0x00FFFFFF;

    private const int TagUndefined = 0;
    private const int TagSingleton = 1;
    private const int TagMulti = 2;
    private const int TagLambda = 3;
    private const int TagRegex = 4;
    private const int TagTailCall = 5;
    private const int TagNonFinite = 6;
    private const int TagRawDouble = 7;
    private const int TagRawDoubleArray = 8;
    private const int TagTuple = 9;
    private const int TagObjectLambdas = 10;
    private const int TagLazyRange = 11;

    private readonly object? payload;
    private readonly double rawValue;
    private readonly JsonElement singleValue;
    private readonly int countAndTag;

    private int Tag
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.countAndTag >>> TagShift;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Pack(int tag, int count) => (tag << TagShift) | (count & CountMask);

    /// <summary>An empty (undefined) sequence.</summary>
    public static readonly Sequence Undefined = default;

    /// <summary>
    /// Initializes a new instance of the <see cref="Sequence"/> struct with a single value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Sequence(in JsonElement value)
    {
        this.singleValue = value;
        this.countAndTag = Pack(TagSingleton, 1);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sequence"/> struct with multiple values.
    /// </summary>
    /// <param name="values">The backing array (typically rented from a pool).</param>
    /// <param name="count">The number of valid elements in the array.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Sequence(JsonElement[] values, int count)
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
            this.countAndTag = Pack(TagSingleton, 1);
        }
        else
        {
            this.payload = values;
            this.countAndTag = Pack(TagMulti, count);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sequence"/> struct wrapping a lambda value.
    /// </summary>
    /// <param name="lambda">The lambda (function) value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Sequence(LambdaValue lambda)
    {
        this.payload = lambda;
        this.countAndTag = Pack(TagLambda, 0);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sequence"/> struct wrapping a compiled regular expression.
    /// </summary>
    /// <param name="regex">The compiled regular expression.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Sequence(Regex regex)
    {
        this.payload = regex;
        this.countAndTag = Pack(TagRegex, 0);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sequence"/> struct wrapping a tail-call continuation.
    /// </summary>
    /// <param name="tailCall">The tail-call continuation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Sequence(TailCallContinuation tailCall)
    {
        this.payload = tailCall;
        this.countAndTag = Pack(TagTailCall, 0);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sequence"/> struct wrapping a non-finite numeric value
    /// (Infinity or NaN) that cannot be represented as a JSON number.
    /// </summary>
    /// <param name="nonFiniteValue">The non-finite double value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Sequence(double nonFiniteValue)
    {
        Debug.Assert(double.IsInfinity(nonFiniteValue) || double.IsNaN(nonFiniteValue), "Use this constructor only for non-finite values; use FromDouble for finite doubles");
        this.rawValue = nonFiniteValue;
        this.countAndTag = Pack(TagNonFinite, 0);
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
    /// Creates a <see cref="Sequence"/> holding a string value. The string is materialized
    /// to a <see cref="JsonElement"/> immediately using the given workspace.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <param name="workspace">The workspace used to create the string element.</param>
    /// <returns>A singleton sequence representing the string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Sequence FromString(string value, JsonWorkspace workspace)
    {
        return new Sequence(JsonataHelpers.StringFromString(value, workspace));
    }

    /// <summary>
    /// Creates a <see cref="Sequence"/> holding a boolean value. Boolean elements are
    /// pre-cached singletons, so this incurs no allocation.
    /// </summary>
    /// <param name="value">The boolean value.</param>
    /// <returns>A singleton sequence representing <c>true</c> or <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Sequence FromBool(bool value)
    {
        return new Sequence(JsonataHelpers.BooleanElement(value));
    }

    /// <summary>
    /// Private constructor for raw finite doubles.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Sequence(double value, JsonWorkspace workspace)
    {
        this.rawValue = value;
        this.payload = workspace;
        this.countAndTag = Pack(TagRawDouble, 1);
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
        return new Sequence(new RawDoubleArrayPayload(values, workspace), count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Sequence(RawDoubleArrayPayload doubleArray, int count)
    {
        this.payload = doubleArray;
        this.countAndTag = Pack(TagRawDoubleArray, count);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sequence"/> struct as a tuple array
    /// of sub-sequences. Used when an array constructor contains non-JSON values (e.g. lambdas)
    /// that must be preserved without reification into a JSON array.
    /// </summary>
    /// <param name="tupleItems">The sub-sequences, one per array element.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Sequence(Sequence[] tupleItems)
    {
        this.payload = tupleItems;
        this.countAndTag = Pack(TagTuple, tupleItems.Length);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sequence"/> struct wrapping a single JSON element
    /// that has associated lambda property values. Used when an object constructor creates
    /// an object with function-valued properties (e.g. the custom matcher protocol's <c>next</c> property).
    /// </summary>
    /// <param name="value">The JSON element.</param>
    /// <param name="objectLambdas">Dictionary mapping property names to their lambda values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Sequence(in JsonElement value, Dictionary<string, LambdaValue> objectLambdas)
    {
        this.singleValue = value;
        this.payload = objectLambdas;
        this.countAndTag = Pack(TagObjectLambdas, 1);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sequence"/> struct as a lazy range
    /// that generates number elements on demand. Used for large integer ranges
    /// (e.g. <c>[1..10000000]</c>) to avoid materializing millions of elements.
    /// </summary>
    /// <param name="rangeInfo">The lazy range bounds and workspace reference.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Sequence(LazyRangeInfo rangeInfo)
    {
        this.payload = rangeInfo;
        this.countAndTag = Pack(TagLazyRange, rangeInfo.End - rangeInfo.Start + 1);
    }

    /// <summary>Gets the number of values in the sequence.</summary>
    internal int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.countAndTag & CountMask;
    }

    /// <summary>
    /// Returns the multi-value backing array to <see cref="ArrayPool{T}"/>.
    /// Call this when the sequence is no longer needed and its elements have
    /// already been consumed or copied. This is a no-op for singleton, undefined,
    /// or non-array-backed sequences.
    /// </summary>
    /// <remarks>
    /// Only call this on sequences whose backing array you <b>own</b>. Shared
    /// sequences (e.g. from environment lookups) must not have their arrays returned
    /// directly — use <see cref="CopyOwned"/> first if the consumer needs to return
    /// the array later.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReturnBackingArray()
    {
        int tag = this.Tag;
        if (tag == TagMulti)
        {
            ArrayPool<JsonElement>.Shared.Return((JsonElement[])this.payload!);
        }
        else if (tag == TagRawDoubleArray)
        {
            var p = (RawDoubleArrayPayload)this.payload!;
            ArrayPool<double>.Shared.Return(p.Values);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if this sequence and <paramref name="other"/>
    /// share the same backing array (payload reference equality).
    /// Used to guard against returning an array that is still aliased.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool SharesBackingArray(in Sequence other) =>
        this.payload is not null && ReferenceEquals(this.payload, other.payload);

    /// <summary>
    /// Returns an owned copy of this sequence. For multi-value sequences backed by
    /// a pooled array, this rents a fresh array from the pool and copies the elements.
    /// The caller owns the returned copy and is responsible for returning its backing
    /// array. Singletons and other non-array variants are returned as-is (no copy needed).
    /// </summary>
    /// <returns>An owned sequence whose backing array (if any) can safely be returned.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Sequence CopyOwned()
    {
        int tag = this.Tag;
        if (tag == TagMulti)
        {
            int count = this.Count;
            var src = (JsonElement[])this.payload!;
            var copy = ArrayPool<JsonElement>.Shared.Rent(count);
            Array.Copy(src, copy, count);
            return new Sequence(copy, count);
        }

        if (tag == TagRawDoubleArray)
        {
            int count = this.Count;
            var p = (RawDoubleArrayPayload)this.payload!;
            var copy = ArrayPool<double>.Shared.Rent(count);
            Array.Copy(p.Values, copy, count);
            return FromDoubleArray(copy, count, p.Workspace);
        }

        return this;
    }

    /// <summary>Gets a value indicating whether this is an undefined (empty) sequence.</summary>
    public bool IsUndefined
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.countAndTag == 0;
    }

    /// <summary>Gets a value indicating whether this is a singleton sequence.</summary>
    internal bool IsSingleton
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (this.countAndTag & CountMask) == 1;
    }

    /// <summary>Gets a value indicating whether this sequence holds a raw finite double value.</summary>
    internal bool IsRawDouble
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.Tag == TagRawDouble;
    }

    /// <summary>Gets the workspace associated with a raw double sequence.</summary>
    internal JsonWorkspace? RawDoubleWorkspace
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.Tag == TagRawDouble ? (JsonWorkspace?)this.payload : null;
    }

    /// <summary>Gets a value indicating whether this sequence is backed by an array of raw doubles.</summary>
    internal bool IsRawDoubleArray
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.Tag == TagRawDoubleArray;
    }

    /// <summary>Gets the raw double value at the specified index in a raw double array sequence.</summary>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The raw double value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal double GetRawDoubleAt(int index)
    {
        Debug.Assert(this.Tag == TagRawDoubleArray, "Not a raw double array sequence");
        return ((RawDoubleArrayPayload)this.payload!).Values[index];
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
        if (this.Tag == TagRawDouble)
        {
            value = this.rawValue;
            return true;
        }

        if ((this.countAndTag & CountMask) == 1 && this.singleValue.ValueKind == JsonValueKind.Number)
        {
            return this.singleValue.TryGetDouble(out value);
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Gets the double value of this sequence, whether it is a raw double proxy or
    /// a singleton JSON number element.
    /// </summary>
    /// <returns>The double value.</returns>
    /// <exception cref="InvalidOperationException">The sequence does not hold a numeric value.</exception>
    public double AsDouble()
    {
        if (this.TryGetDouble(out double value))
        {
            return value;
        }

        throw new InvalidOperationException("The sequence does not hold a numeric value.");
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
        if (this.Tag == TagRawDouble)
        {
            return JsonataHelpers.NumberFromDouble(this.rawValue, (JsonWorkspace)this.payload!);
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
        if (this.Tag == TagRawDouble)
        {
            return JsonataHelpers.NumberFromDouble(this.rawValue, workspace);
        }

        return this.FirstOrDefault;
    }

    /// <summary>
    /// Gets the <see cref="JsonValueKind"/> of this sequence's value without materializing.
    /// Returns <see cref="JsonValueKind.Number"/> for raw doubles and non-finite values.
    /// </summary>
    internal JsonValueKind SequenceValueKind
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            int tag = this.Tag;
            if (tag == TagRawDouble || tag == TagNonFinite)
            {
                return JsonValueKind.Number;
            }

            if ((this.countAndTag & CountMask) > 0)
            {
                return this.FirstOrDefault.ValueKind;
            }

            return JsonValueKind.Undefined;
        }
    }

    /// <summary>Gets a value indicating whether this sequence holds a lambda (function) value.</summary>
    internal bool IsLambda
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.Tag == TagLambda;
    }

    /// <summary>Gets the lambda value, or <c>null</c> if this is not a lambda.</summary>
    internal LambdaValue? Lambda
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.Tag == TagLambda ? (LambdaValue?)this.payload : null;
    }

    /// <summary>Gets a value indicating whether this sequence holds a regular expression value.</summary>
    internal bool IsRegex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.Tag == TagRegex;
    }

    /// <summary>Gets the compiled regular expression, or <c>null</c> if this is not a regex.</summary>
    internal Regex? Regex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.Tag == TagRegex ? (Regex?)this.payload : null;
    }

    /// <summary>Gets a value indicating whether this sequence holds a tail-call continuation.</summary>
    internal bool IsTailCall
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.Tag == TagTailCall;
    }

    /// <summary>Gets the tail-call continuation, or <c>null</c> if this is not a tail call.</summary>
    internal TailCallContinuation? TailCall
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.Tag == TagTailCall ? (TailCallContinuation?)this.payload : null;
    }

    /// <summary>Gets a value indicating whether this sequence holds a non-finite numeric value (Infinity or NaN).</summary>
    internal bool IsNonFinite
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.Tag == TagNonFinite;
    }

    /// <summary>Gets the non-finite double value. Only valid when <see cref="IsNonFinite"/> is <c>true</c>.</summary>
    internal double NonFiniteValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.rawValue;
    }

    /// <summary>Gets a value indicating whether this is a tuple sequence holding sub-sequences (e.g. an array containing lambdas).</summary>
    internal bool IsTupleSequence
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.Tag == TagTuple;
    }

    /// <summary>Gets a value indicating whether this is a lazy range sequence.</summary>
    internal bool IsLazyRange
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.Tag == TagLazyRange;
    }

    /// <summary>
    /// Gets the lambda properties associated with this object element, if any.
    /// Used by the custom matcher protocol to retrieve function-valued properties
    /// (such as <c>next</c>) that cannot be represented in JSON.
    /// </summary>
    internal Dictionary<string, LambdaValue>? ObjectLambdas
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.Tag == TagObjectLambdas ? (Dictionary<string, LambdaValue>?)this.payload : null;
    }

    /// <summary>
    /// Gets the full sub-sequence at the given index. For tuple sequences, this returns the
    /// original sub-sequence (which may be a lambda or other non-JSON variant). For non-tuple
    /// sequences, this wraps the JSON element at the index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Sequence GetItemSequence(int index)
    {
        if (this.Tag == TagTuple)
        {
            return ((Sequence[])this.payload!)[index];
        }

        return new Sequence(this[index]);
    }

    /// <summary>
    /// Gets the element at the specified index.
    /// </summary>
    internal JsonElement this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)(this.countAndTag & CountMask))
            {
                ThrowIndexOutOfRange();
            }

            int tag = this.Tag;

            if (tag == TagRawDouble)
            {
                Debug.Assert(index == 0, "Raw double sequence index must be 0");
                return JsonataHelpers.NumberFromDouble(this.rawValue, (JsonWorkspace)this.payload!);
            }

            if (tag == TagRawDoubleArray)
            {
                var dbl = (RawDoubleArrayPayload)this.payload!;
                return JsonataHelpers.NumberFromDouble(dbl.Values[index], dbl.Workspace);
            }

            if (tag == TagLazyRange)
            {
                return ((LazyRangeInfo)this.payload!).GetElement(index);
            }

            if (tag == TagTuple)
            {
                return ((Sequence[])this.payload!)[index].FirstOrDefault;
            }

            if (tag == TagMulti)
            {
                return ((JsonElement[])this.payload!)[index];
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
    internal JsonElement FirstOrDefault
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            int ct = this.countAndTag;
            if ((ct & CountMask) == 0)
            {
                return default;
            }

            int tag = ct >>> TagShift;

            if (tag == TagRawDouble)
            {
                return JsonataHelpers.NumberFromDouble(this.rawValue, (JsonWorkspace)this.payload!);
            }

            if (tag == TagTuple)
            {
                return ((Sequence[])this.payload!)[0].FirstOrDefault;
            }

            if (tag == TagMulti)
            {
                return ((JsonElement[])this.payload!)[0];
            }

            return this.singleValue;
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
    internal Enumerator GetEnumerator() => new(this);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowIndexOutOfRange()
    {
        throw new IndexOutOfRangeException();
    }

    /// <summary>
    /// Enumerator for iterating over sequence values without allocation.
    /// </summary>
    internal struct Enumerator
    {
        private readonly Sequence sequence;
        private readonly int count;
        private int index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(Sequence sequence)
        {
            this.sequence = sequence;
            this.count = sequence.Count;
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
            if (next < this.count)
            {
                this.index = next;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Carries a rented double[] and workspace for the RawDoubleArray variant.
    /// This is a class because the Sequence struct has only one object? field.
    /// </summary>
    internal sealed class RawDoubleArrayPayload
    {
        public readonly double[] Values;
        public readonly JsonWorkspace Workspace;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RawDoubleArrayPayload(double[] values, JsonWorkspace workspace)
        {
            this.Values = values;
            this.Workspace = workspace;
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