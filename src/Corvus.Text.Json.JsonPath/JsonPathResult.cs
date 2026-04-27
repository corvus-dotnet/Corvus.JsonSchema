// <copyright file="JsonPathResult.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// A ref struct that accumulates JSONPath query result nodes using a
/// caller-provided <see cref="Span{T}"/> of <see cref="JsonElement"/>
/// and overflows to <see cref="ArrayPool{T}"/> when necessary.
/// </summary>
/// <remarks>
/// <para>
/// This follows the <c>ValueListBuilder&lt;T&gt;</c> pattern: if all
/// result nodes fit in the initial buffer (typically stack-allocated),
/// zero heap allocation occurs. Overflow is handled transparently via
/// <see cref="ArrayPool{T}.Shared"/>.
/// </para>
/// <para>
/// Callers must dispose instances that might have overflowed:
/// <code>
/// Span&lt;JsonElement&gt; buf = stackalloc JsonElement[16];
/// using JsonPathResult result = evaluator.QueryNodes(expression, data, buf);
/// ReadOnlySpan&lt;JsonElement&gt; nodes = result.Nodes;
/// </code>
/// </para>
/// </remarks>
public ref struct JsonPathResult
{
    private Span<JsonElement> _span;
    private JsonElement[]? _arrayFromPool;
    private int _pos;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPathResult"/> struct
    /// with the specified initial buffer.
    /// </summary>
    /// <param name="initialBuffer">
    /// A caller-provided span (typically stack-allocated) for storing result nodes.
    /// </param>
    public JsonPathResult(Span<JsonElement> initialBuffer)
    {
        _span = initialBuffer;
    }

    /// <summary>Gets the number of result nodes.</summary>
    public readonly int Count => _pos;

    /// <summary>Gets the result nodes as a read-only span.</summary>
    public readonly ReadOnlySpan<JsonElement> Nodes => _span.Slice(0, _pos);

    /// <summary>Gets the result node at the specified index.</summary>
    /// <param name="index">The zero-based index.</param>
    public readonly JsonElement this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _span[index];
    }

    /// <summary>
    /// Returns any rented backing array to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        JsonElement[]? toReturn = _arrayFromPool;
        if (toReturn != null)
        {
            _arrayFromPool = null;
            Array.Clear(toReturn, 0, _pos);
            ArrayPool<JsonElement>.Shared.Return(toReturn);
        }

        _pos = 0;
    }

    /// <summary>
    /// Gets a value indicating whether the result overflowed the initial buffer
    /// and rented from <see cref="ArrayPool{T}"/>.
    /// </summary>
    public readonly bool HasSpilled => _arrayFromPool is not null;

    /// <summary>
    /// Creates a <see cref="JsonPathResult"/> backed entirely by an
    /// <see cref="ArrayPool{T}"/> rental.
    /// </summary>
    /// <param name="initialCapacity">The minimum capacity to rent.</param>
    /// <returns>A pooled <see cref="JsonPathResult"/>.</returns>
    public static JsonPathResult CreatePooled(int initialCapacity)
    {
        JsonElement[] array = ArrayPool<JsonElement>.Shared.Rent(initialCapacity);
        JsonPathResult result = default;
        result._span = array;
        result._arrayFromPool = array;
        return result;
    }

    /// <summary>
    /// Appends a result node.
    /// </summary>
    /// <param name="value">The element to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(in JsonElement value)
    {
        int pos = _pos;
        Span<JsonElement> span = _span;
        if ((uint)pos < (uint)span.Length)
        {
            span[pos] = value;
            _pos = pos + 1;
        }
        else
        {
            AppendWithResize(value);
        }
    }

    /// <summary>
    /// Resets the count to zero so the buffer can be reused.
    /// </summary>
    public void Clear()
    {
        _span.Slice(0, _pos).Clear();
        _pos = 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AppendWithResize(in JsonElement value)
    {
        Grow(1);
        _span[_pos++] = value;
    }

    private void Grow(int additionalCapacityRequired)
    {
        int nextCapacity = Math.Max(
            _span.Length != 0 ? _span.Length * 2 : 4,
            _span.Length + additionalCapacityRequired);

        JsonElement[] array = ArrayPool<JsonElement>.Shared.Rent(nextCapacity);
        _span.Slice(0, _pos).CopyTo(array);

        JsonElement[]? toReturn = _arrayFromPool;
        _span = _arrayFromPool = array;
        if (toReturn != null)
        {
            Array.Clear(toReturn, 0, _pos);
            ArrayPool<JsonElement>.Shared.Return(toReturn);
        }
    }
}
