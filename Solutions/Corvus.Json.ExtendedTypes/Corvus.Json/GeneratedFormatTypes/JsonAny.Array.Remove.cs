// <copyright file="JsonAny.Array.Remove.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;

namespace Corvus.Json;

/// <summary>
/// Represents any JSON value.
/// </summary>
public readonly partial struct JsonAny
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsonAny Remove(in JsonAny item1)
    {
        return new(this.GetImmutableListWithout(item1));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsonAny RemoveAt(int index)
    {
        return new(this.GetImmutableListWithoutRange(index, 1));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsonAny RemoveRange(int index, int count)
    {
        return new(this.GetImmutableListWithoutRange(index, count));
    }
}