// <copyright file="JsonataBinding.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// A function that can be bound to a JSONata variable.
/// Receives its evaluated arguments as a correctly-sized <see cref="ReadOnlySpan{T}"/>
/// of <see cref="Sequence"/> values. Use <see cref="Sequence.AsDouble"/>,
/// <see cref="Sequence.AsElement()"/>, etc. to read argument values.
/// </summary>
/// <param name="args">The evaluated arguments, sliced to the actual call-site argument count.</param>
/// <param name="workspace">The current <see cref="JsonWorkspace"/> for memory allocation.</param>
/// <returns>The function result as a <see cref="Sequence"/>.</returns>
public delegate Sequence SequenceFunction(ReadOnlySpan<Sequence> args, JsonWorkspace workspace);

/// <summary>
/// Represents a binding that can be passed to the JSONata evaluator.
/// A binding is either a JSON value or a callable function.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="FromValue(JsonElement)"/> to create a value binding, or <see cref="FromFunction(SequenceFunction, int, string?)"/>
/// to create a function binding. Value bindings are equivalent to the existing
/// <c>IReadOnlyDictionary&lt;string, JsonElement&gt;</c> API. Function bindings allow
/// calling C# functions from within JSONata expressions.
/// </para>
/// <para>
/// Function bindings accept <see cref="Sequence"/>-based delegates, allowing them to
/// participate in the JSONata runtime's value proxy system. For example, returning
/// <see cref="Sequence.FromDouble"/> stores a raw <see langword="double"/> inline
/// without materializing a <see cref="JsonElement"/>:
/// <code>
/// var bindings = new Dictionary&lt;string, JsonataBinding&gt;
/// {
///     ["threshold"] = JsonataBinding.FromValue(42.0),
///     ["cosine"]    = JsonataBinding.FromFunction(
///         (args, ws) =&gt; Sequence.FromDouble(Math.Cos(args[0].AsDouble()), ws), 1),
/// };
/// </code>
/// </para>
/// </remarks>
public readonly struct JsonataBinding
{
    private readonly JsonElement elementValue;
    private readonly double numericValue;
    private readonly object? payload; // function delegate or string value
    private readonly int parameterCount;
    private readonly string? signature;
    private readonly BindingKind kind;

    private JsonataBinding(JsonElement value)
    {
        this.elementValue = value;
        this.kind = BindingKind.ElementValue;
    }

    private JsonataBinding(double value)
    {
        this.numericValue = value;
        this.kind = BindingKind.DoubleValue;
    }

    private JsonataBinding(string value)
    {
        this.payload = value;
        this.kind = BindingKind.StringValue;
    }

    private JsonataBinding(bool value)
    {
        this.numericValue = value ? 1.0 : 0.0;
        this.kind = BindingKind.BoolValue;
    }

    private JsonataBinding(SequenceFunction function, int parameterCount, string? signature)
    {
        this.payload = function;
        this.parameterCount = parameterCount;
        this.signature = signature;
        this.kind = BindingKind.SequenceFunction;
    }

    private JsonataBinding(Func<JsonElement[], JsonWorkspace, JsonElement> function, int parameterCount, string? signature)
    {
        this.payload = function;
        this.parameterCount = parameterCount;
        this.signature = signature;
        this.kind = BindingKind.ElementFunction;
    }

    /// <summary>
    /// Gets a value indicating whether this binding is a function.
    /// </summary>
    public bool IsFunction => this.kind is BindingKind.SequenceFunction or BindingKind.ElementFunction;

    /// <summary>
    /// Creates a value binding from a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="value">The JSON value to bind.</param>
    /// <returns>A new value binding.</returns>
    public static JsonataBinding FromValue(JsonElement value) => new(value);

    /// <summary>
    /// Creates a value binding from a <see langword="double"/>. The value is stored
    /// as a raw double and only materialized to a <see cref="JsonElement"/> when needed.
    /// </summary>
    /// <param name="value">The numeric value to bind.</param>
    /// <returns>A new value binding.</returns>
    public static JsonataBinding FromValue(double value) => new(value);

    /// <summary>
    /// Creates a value binding from a <see langword="string"/>. The string is materialized
    /// to a <see cref="JsonElement"/> when bound to the evaluation environment.
    /// </summary>
    /// <param name="value">The string value to bind.</param>
    /// <returns>A new value binding.</returns>
    public static JsonataBinding FromValue(string value)
    {
#if NET
        ArgumentNullException.ThrowIfNull(value);
#else
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }
#endif

        return new(value);
    }

    /// <summary>
    /// Creates a value binding from a <see langword="bool"/>. Boolean elements are
    /// pre-cached singletons, so this incurs no allocation.
    /// </summary>
    /// <param name="value">The boolean value to bind.</param>
    /// <returns>A new value binding.</returns>
    public static JsonataBinding FromValue(bool value) => new(value);

    /// <summary>
    /// Creates a function binding using <see cref="Sequence"/>-based arguments and return value.
    /// This is the preferred overload as it allows functions to participate in the JSONata
    /// runtime's value proxy system, avoiding unnecessary <see cref="JsonElement"/> materialization.
    /// </summary>
    /// <param name="function">
    /// The function to call. Receives the evaluated arguments as a correctly-sized
    /// <see cref="ReadOnlySpan{T}"/> of <see cref="Sequence"/> values and the current
    /// <see cref="JsonWorkspace"/>. Returns a <see cref="Sequence"/> result.
    /// Use <see cref="Sequence.TryGetDouble"/>, <see cref="Sequence.AsElement()"/>, etc. to
    /// read argument values, and <see cref="Sequence.FromDouble"/>, <see cref="Sequence.FromString"/>,
    /// <see cref="Sequence.FromBool"/>, or the <see cref="Sequence(in JsonElement)"/> constructor
    /// to produce results.
    /// </param>
    /// <param name="parameterCount">The number of parameters the function expects.</param>
    /// <param name="signature">
    /// An optional JSONata type signature string for argument validation (e.g. <c>"&lt;n:n&gt;"</c>).
    /// When provided, the JSONata runtime validates argument types before calling the function.
    /// </param>
    /// <returns>A new function binding.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="function"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameterCount"/> is negative.</exception>
    public static JsonataBinding FromFunction(
        SequenceFunction function,
        int parameterCount,
        string? signature = null)
    {
#if NET
        ArgumentNullException.ThrowIfNull(function);
        ArgumentOutOfRangeException.ThrowIfNegative(parameterCount);
#else
        if (function is null)
        {
            throw new ArgumentNullException(nameof(function));
        }

        if (parameterCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parameterCount));
        }
#endif

        return new JsonataBinding(function, parameterCount, signature);
    }

    /// <summary>
    /// Creates a unary <see langword="double"/>→<see langword="double"/> function binding.
    /// The argument is automatically extracted via <see cref="Sequence.AsDouble"/> and the
    /// result is stored as a raw double proxy (zero allocation).
    /// </summary>
    /// <example><c>JsonataBinding.FromFunction((v) =&gt; Math.Cos(v))</c></example>
    /// <param name="function">A function that takes a single double and returns a double.</param>
    /// <returns>A new function binding with parameter count 1.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="function"/> is <see langword="null"/>.</exception>
    public static JsonataBinding FromFunction(Func<double, double> function)
    {
#if NET
        ArgumentNullException.ThrowIfNull(function);
#else
        if (function is null)
        {
            throw new ArgumentNullException(nameof(function));
        }
#endif

        return new JsonataBinding(
            (ReadOnlySpan<Sequence> args, JsonWorkspace ws) => Sequence.FromDouble(function(args[0].AsDouble()), ws),
            1,
            null);
    }

    /// <summary>
    /// Creates a binary (<see langword="double"/>, <see langword="double"/>)→<see langword="double"/>
    /// function binding. Arguments are automatically extracted via <see cref="Sequence.AsDouble"/>
    /// and the result is stored as a raw double proxy (zero allocation).
    /// </summary>
    /// <example><c>JsonataBinding.FromFunction((a, b) =&gt; Math.Min(a, b) + 1)</c></example>
    /// <param name="function">A function that takes two doubles and returns a double.</param>
    /// <returns>A new function binding with parameter count 2.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="function"/> is <see langword="null"/>.</exception>
    public static JsonataBinding FromFunction(Func<double, double, double> function)
    {
#if NET
        ArgumentNullException.ThrowIfNull(function);
#else
        if (function is null)
        {
            throw new ArgumentNullException(nameof(function));
        }
#endif

        return new JsonataBinding(
            (ReadOnlySpan<Sequence> args, JsonWorkspace ws) => Sequence.FromDouble(function(args[0].AsDouble(), args[1].AsDouble()), ws),
            2,
            null);
    }

    /// <summary>
    /// Creates a function binding using <see cref="JsonElement"/>-based arguments and return value.
    /// </summary>
    /// <param name="function">
    /// The function to call. Receives an array of evaluated <see cref="JsonElement"/> arguments
    /// and the current <see cref="JsonWorkspace"/> for memory allocation. Returns a
    /// <see cref="JsonElement"/> result.
    /// </param>
    /// <param name="parameterCount">The number of parameters the function expects.</param>
    /// <param name="signature">
    /// An optional JSONata type signature string for argument validation (e.g. <c>"&lt;s-s:s&gt;"</c>).
    /// When provided, the JSONata runtime validates argument types before calling the function.
    /// </param>
    /// <returns>A new function binding.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="function"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameterCount"/> is negative.</exception>
    public static JsonataBinding FromFunction(
        Func<JsonElement[], JsonWorkspace, JsonElement> function,
        int parameterCount,
        string? signature = null)
    {
#if NET
        ArgumentNullException.ThrowIfNull(function);
        ArgumentOutOfRangeException.ThrowIfNegative(parameterCount);
#else
        if (function is null)
        {
            throw new ArgumentNullException(nameof(function));
        }

        if (parameterCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parameterCount));
        }
#endif

        return new JsonataBinding(function, parameterCount, signature);
    }

    /// <summary>
    /// Implicitly converts a <see cref="JsonElement"/> to a value binding.
    /// </summary>
    /// <param name="value">The JSON value.</param>
    public static implicit operator JsonataBinding(JsonElement value) => FromValue(value);

    /// <summary>
    /// Implicitly converts a <see langword="double"/> to a value binding.
    /// </summary>
    /// <param name="value">The numeric value.</param>
    public static implicit operator JsonataBinding(double value) => FromValue(value);

    /// <summary>
    /// Implicitly converts a <see langword="bool"/> to a value binding.
    /// </summary>
    /// <param name="value">The boolean value.</param>
    public static implicit operator JsonataBinding(bool value) => FromValue(value);

    /// <summary>
    /// Gets the binding kind for internal dispatch in <see cref="JsonataEvaluator"/>.
    /// </summary>
    internal BindingKind Kind => this.kind;

    /// <summary>
    /// Gets the JSON element for an <see cref="BindingKind.ElementValue"/> binding.
    /// </summary>
    internal JsonElement GetElementValue() => this.elementValue;

    /// <summary>
    /// Gets the double for a <see cref="BindingKind.DoubleValue"/> binding.
    /// </summary>
    internal double GetDoubleValue() => this.numericValue;

    /// <summary>
    /// Gets the string for a <see cref="BindingKind.StringValue"/> binding.
    /// </summary>
    internal string GetStringValue() => (string)this.payload!;

    /// <summary>
    /// Gets the boolean for a <see cref="BindingKind.BoolValue"/> binding.
    /// </summary>
    internal bool GetBoolValue() => this.numericValue != 0.0;

    /// <summary>
    /// Gets the <see cref="Sequence"/>-based function delegate.
    /// </summary>
    internal SequenceFunction GetSequenceFunction() =>
        (SequenceFunction)this.payload!;

    /// <summary>
    /// Gets the <see cref="JsonElement"/>-based function delegate.
    /// </summary>
    internal Func<JsonElement[], JsonWorkspace, JsonElement> GetElementFunction() =>
        (Func<JsonElement[], JsonWorkspace, JsonElement>)this.payload!;

    /// <summary>
    /// Gets the parameter count for a function binding.
    /// </summary>
    internal int GetParameterCount() => this.parameterCount;

    /// <summary>
    /// Gets the optional signature string for a function binding.
    /// </summary>
    internal string? GetSignature() => this.signature;

    /// <summary>
    /// Internal discriminator for binding dispatch.
    /// </summary>
    internal enum BindingKind : byte
    {
        /// <summary>Value binding holding a <see cref="JsonElement"/>.</summary>
        ElementValue,

        /// <summary>Value binding holding a raw <see langword="double"/>.</summary>
        DoubleValue,

        /// <summary>Value binding holding a <see langword="string"/>.</summary>
        StringValue,

        /// <summary>Value binding holding a <see langword="bool"/>.</summary>
        BoolValue,

        /// <summary>Function binding with <see cref="Sequence"/>-based delegate.</summary>
        SequenceFunction,

        /// <summary>Function binding with <see cref="JsonElement"/>-based delegate.</summary>
        ElementFunction,
    }
}