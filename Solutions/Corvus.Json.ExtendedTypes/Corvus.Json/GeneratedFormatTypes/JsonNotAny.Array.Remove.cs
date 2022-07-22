// <copyright file="JsonNotAny.Array.Remove.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;

namespace Corvus.Json;

/// <summary>
/// Represents any JSON value, validating false.
/// </summary>
public readonly partial struct JsonNotAny
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsonNotAny Remove(in JsonAny item1)
    {
        return new(this.GetImmutableListWithout(item1));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsonNotAny Remove<TItem1>(in TItem1 item1)
        where TItem1 : struct, IJsonValue<TItem1>
    {
        return this.Remove(item1.AsAny);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsonNotAny RemoveAt(int index)
    {
        return new(this.GetImmutableListWithoutRange(index, 1));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsonNotAny RemoveRange(int index, int count)
    {
        return new(this.GetImmutableListWithoutRange(index, count));
    }
}