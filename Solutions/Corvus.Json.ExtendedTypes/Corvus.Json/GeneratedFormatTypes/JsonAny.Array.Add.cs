// <copyright file="JsonAny.Array.Add.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;

namespace Corvus.Json;

/// <summary>
/// Represents any JSON value.
/// </summary>
public readonly partial struct JsonAny
{
    /// <inheritdoc/>
    public JsonAny Add(in JsonAny item1)
    {
        ImmutableList<JsonAny>.Builder builder = this.GetImmutableListBuilder();
        builder.Add(item1);
        return new(builder.ToImmutable());
    }

    /// <inheritdoc/>
    public JsonAny Add(in JsonAny item1, in JsonAny item2)
    {
        ImmutableList<JsonAny>.Builder builder = this.GetImmutableListBuilder();
        builder.Add(item1);
        builder.Add(item2);
        return new(builder.ToImmutable());
    }

    /// <inheritdoc/>
    public JsonAny Add(params JsonAny[] items)
    {
        ImmutableList<JsonAny>.Builder builder = this.GetImmutableListBuilder();
        builder.AddRange(items);
        return new(builder.ToImmutable());
    }

    /// <inheritdoc/>
    public JsonAny AddRange<TArray>(in TArray items)
        where TArray : struct, IJsonArray<TArray>
    {
        ImmutableList<JsonAny>.Builder builder = this.GetImmutableListBuilder();
        foreach (JsonAny item in items.EnumerateArray())
        {
            builder.Add(item.AsAny);
        }

        return new(builder.ToImmutable());
    }

    /// <inheritdoc/>
    public JsonAny AddRange<TItem>(IEnumerable<TItem> items)
        where TItem : struct, IJsonValue<TItem>
    {
        ImmutableList<JsonAny>.Builder builder = this.GetImmutableListBuilder();
        foreach (TItem item in items)
        {
            builder.Add(item.AsAny);
        }

        return new(builder.ToImmutable());
    }

    /// <inheritdoc/>
    public JsonAny AddRange(IEnumerable<JsonAny> items)
    {
        ImmutableList<JsonAny>.Builder builder = this.GetImmutableListBuilder();
        builder.AddRange(items);
        return new(builder.ToImmutable());
    }

    /// <inheritdoc/>
    public JsonAny Insert(int index, in JsonAny item1)
    {
        return new(this.GetImmutableListWith(index, item1));
    }

    /// <inheritdoc/>
    public JsonAny InsertRange<TArray>(int index, in TArray items)
        where TArray : struct, IJsonArray<TArray>
    {
        return new(this.GetImmutableListWith(index, items.EnumerateArray()));
    }

    /// <inheritdoc/>
    public JsonAny InsertRange<TItem>(int index, IEnumerable<TItem> items)
        where TItem : struct, IJsonValue<TItem>
    {
        return new(this.GetImmutableListWith(index, items.Select(item => item.AsAny)));
    }

    /// <inheritdoc/>
    public JsonAny InsertRange(int index, IEnumerable<JsonAny> items)
    {
        return new(this.GetImmutableListWith(index, items));
    }

    /// <inheritdoc/>
    public JsonAny Replace(in JsonAny oldValue, in JsonAny newValue)
    {
        return this.GetImmutableListReplacing(oldValue, newValue);
    }

    /// <inheritdoc/>
    public JsonAny SetItem(int index, in JsonAny value)
    {
        return this.GetImmutableListSetting(index, value.AsAny);
    }
}