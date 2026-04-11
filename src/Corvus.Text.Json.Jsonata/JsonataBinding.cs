// <copyright file="JsonataBinding.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Represents a binding that can be passed to the JSONata evaluator.
/// A binding is either a JSON value or a callable function.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="FromValue"/> to create a value binding, or <see cref="FromFunction"/>
/// to create a function binding. Value bindings are equivalent to the existing
/// <c>IReadOnlyDictionary&lt;string, JsonElement&gt;</c> API. Function bindings allow
/// calling C# functions from within JSONata expressions.
/// </para>
/// <para>
/// An implicit conversion from <see cref="JsonElement"/> is provided for ergonomic use:
/// <code>
/// var bindings = new Dictionary&lt;string, JsonataBinding&gt;
/// {
///     ["threshold"] = someJsonElement,         // implicit value binding
///     ["convert"]   = JsonataBinding.FromFunction(myFunc, 1),
/// };
/// </code>
/// </para>
/// </remarks>
public readonly struct JsonataBinding
{
    private readonly JsonElement value;
    private readonly Func<JsonElement[], JsonWorkspace, JsonElement>? function;
    private readonly int parameterCount;
    private readonly string? signature;
    private readonly bool isFunction;

    private JsonataBinding(JsonElement value)
    {
        this.value = value;
        this.function = null;
        this.parameterCount = 0;
        this.signature = null;
        this.isFunction = false;
    }

    private JsonataBinding(Func<JsonElement[], JsonWorkspace, JsonElement> function, int parameterCount, string? signature)
    {
        this.value = default;
        this.function = function;
        this.parameterCount = parameterCount;
        this.signature = signature;
        this.isFunction = true;
    }

    /// <summary>
    /// Gets a value indicating whether this binding is a function.
    /// </summary>
    public bool IsFunction => this.isFunction;

    /// <summary>
    /// Creates a value binding from a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="value">The JSON value to bind.</param>
    /// <returns>A new value binding.</returns>
    public static JsonataBinding FromValue(JsonElement value) => new(value);

    /// <summary>
    /// Creates a function binding.
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
    /// Gets the JSON value for a value binding.
    /// </summary>
    /// <returns>The bound JSON value.</returns>
    /// <exception cref="InvalidOperationException">This binding is a function, not a value.</exception>
    internal JsonElement GetValue()
    {
        if (this.isFunction)
        {
            throw new InvalidOperationException("Cannot get a value from a function binding.");
        }

        return this.value;
    }

    /// <summary>
    /// Gets the function delegate for a function binding.
    /// </summary>
    /// <returns>The bound function.</returns>
    /// <exception cref="InvalidOperationException">This binding is a value, not a function.</exception>
    internal Func<JsonElement[], JsonWorkspace, JsonElement> GetFunction()
    {
        if (!this.isFunction || this.function is null)
        {
            throw new InvalidOperationException("Cannot get a function from a value binding.");
        }

        return this.function;
    }

    /// <summary>
    /// Gets the parameter count for a function binding.
    /// </summary>
    internal int GetParameterCount() => this.parameterCount;

    /// <summary>
    /// Gets the optional signature string for a function binding.
    /// </summary>
    internal string? GetSignature() => this.signature;
}