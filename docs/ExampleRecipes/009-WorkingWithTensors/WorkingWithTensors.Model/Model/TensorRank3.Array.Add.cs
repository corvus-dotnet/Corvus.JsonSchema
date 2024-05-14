//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
#nullable enable
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Corvus.Json;

namespace JsonSchemaSample.Api;
/// <summary>
/// A 4x4x4 tensor of JsonDouble
/// </summary>
public readonly partial struct TensorRank3
{
    /// <inheritdoc/>
    TensorRank3 IJsonArray<TensorRank3>.Add(in JsonAny item1)
    {
        ImmutableList<JsonAny>.Builder builder = this.GetImmutableListBuilder();
        builder.Add(item1);
        return new(builder.ToImmutable());
    }

    /// <inheritdoc/>
    TensorRank3 IJsonArray<TensorRank3>.Add(params JsonAny[] items)
    {
        ImmutableList<JsonAny>.Builder builder = this.GetImmutableListBuilder();
        builder.AddRange(items);
        return new(builder.ToImmutable());
    }

    /// <inheritdoc/>
    public TensorRank3 AddRange<TArray>(in TArray items)
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
    TensorRank3 IJsonArray<TensorRank3>.AddRange<TItem>(IEnumerable<TItem> items)
    {
        ImmutableList<JsonAny>.Builder builder = this.GetImmutableListBuilder();
        foreach (TItem item in items)
        {
            builder.Add(item.AsAny);
        }

        return new(builder.ToImmutable());
    }

    /// <inheritdoc/>
    TensorRank3 IJsonArray<TensorRank3>.AddRange(IEnumerable<JsonAny> items)
    {
        ImmutableList<JsonAny>.Builder builder = this.GetImmutableListBuilder();
        builder.AddRange(items);
        return new(builder.ToImmutable());
    }

    /// <inheritdoc/>
    TensorRank3 IJsonArray<TensorRank3>.Insert(int index, in JsonAny item1)
    {
        return new(this.GetImmutableListWith(index, item1));
    }

    /// <inheritdoc/>
    public TensorRank3 InsertRange<TArray>(int index, in TArray items)
        where TArray : struct, IJsonArray<TArray>
    {
        return new(this.GetImmutableListWith(index, items.EnumerateArray()));
    }

    /// <inheritdoc/>
    TensorRank3 IJsonArray<TensorRank3>.InsertRange<TItem>(int index, IEnumerable<TItem> items)
    {
        return new(this.GetImmutableListWith(index, items.Select(item => item.AsAny)));
    }

    /// <inheritdoc/>
    TensorRank3 IJsonArray<TensorRank3>.InsertRange(int index, IEnumerable<JsonAny> items)
    {
        return new(this.GetImmutableListWith(index, items));
    }

    /// <inheritdoc/>
    TensorRank3 IJsonArray<TensorRank3>.Replace(in JsonAny oldValue, in JsonAny newValue)
    {
        return new(this.GetImmutableListReplacing(oldValue.AsAny, newValue.AsAny));
    }

    /// <inheritdoc/>
    TensorRank3 IJsonArray<TensorRank3>.SetItem(int index, in JsonAny value)
    {
        return new(this.GetImmutableListSetting(index, value.AsAny));
    }

    /// <summary>
    /// Set the item at the given location in an array of dimension 3.
    /// </summary>
    /// <param name = "index1">The index for dimension 1.</param>
    /// <param name = "index2">The index for dimension 2.</param>
    /// <param name = "index3">The index for dimension 3.</param>
    /// <returns>The array with the item at the given index set.</returns>
    public TensorRank3 SetItem(int index1, int index2, int index3, in Corvus.Json.JsonDouble value)
    {
        return this.SetItem(index1, this[index1].SetItem(index2, index3, value));
    }

    /// <summary>
    /// Add an item to the array.
    /// </summary>
    /// <param name = "item1">The item to add.</param>
    /// <returns>An instance of the array with the item added.</returns>
    /// <exception cref = "InvalidOperationException">The value was not an array.</exception>
    public TensorRank3 Add(in JsonSchemaSample.Api.TensorRank3.SecondRank item1)
    {
        ImmutableList<JsonAny>.Builder builder = this.GetImmutableListBuilder();
        builder.Add(item1);
        return new(builder.ToImmutable());
    }

    /// <summary>
    /// Add a set of items to the array.
    /// </summary>
    /// <param name = "items">The items to add.</param>
    /// <returns>An instance of the array with the items added.</returns>
    /// <exception cref = "InvalidOperationException">The value was not an array.</exception>
    public TensorRank3 Add(params JsonSchemaSample.Api.TensorRank3.SecondRank[] items)
    {
        ImmutableList<JsonAny>.Builder builder = this.GetImmutableListBuilder();
        foreach (JsonSchemaSample.Api.TensorRank3.SecondRank item in items)
        {
            builder.Add(item.AsAny);
        }

        return new(builder.ToImmutable());
    }

    /// <summary>
    /// Add a set of items to the array.
    /// </summary>
    /// <param name = "items">The items to add.</param>
    /// <returns>An instance of the array with the items added.</returns>
    /// <exception cref = "InvalidOperationException">The value was not an array.</exception>
    public TensorRank3 AddRange(IEnumerable<JsonSchemaSample.Api.TensorRank3.SecondRank> items)
    {
        ImmutableList<JsonAny>.Builder builder = this.GetImmutableListBuilder();
        foreach (JsonSchemaSample.Api.TensorRank3.SecondRank item in items)
        {
            builder.Add(item.AsAny);
        }

        return new(builder.ToImmutable());
    }

    /// <summary>
    /// Insert an item into the array at the given index.
    /// </summary>
    /// <param name = "index">The index at which to add the item.</param>
    /// <param name = "item1">The item to add.</param>
    /// <returns>An instance of the array with the item added.</returns>
    /// <exception cref = "InvalidOperationException">The value was not an array.</exception>
    public TensorRank3 Insert(int index, in JsonSchemaSample.Api.TensorRank3.SecondRank item1)
    {
        return new(this.GetImmutableListWith(index, item1));
    }

    /// <summary>
    /// Insert items into the array at the given index.
    /// </summary>
    /// <param name = "index">The index at which to add the items.</param>
    /// <param name = "items">The items to add.</param>
    /// <returns>An instance of the array with the items added.</returns>
    /// <exception cref = "InvalidOperationException">The value was not an array.</exception>
    /// <exception cref = "IndexOutOfRangeException">The index was outside the bounds of the array.</exception>
    public TensorRank3 InsertRange(int index, IEnumerable<JsonSchemaSample.Api.TensorRank3.SecondRank> items)
    {
        return new(this.GetImmutableListWith(index, items.Select(item => item.AsAny)));
    }

    /// <summary>
    /// Replace the first instance of the given value with the new value, even if the items are identical.
    /// </summary>
    /// <param name = "oldValue">The item to remove.</param>
    /// <param name = "newValue">The item to insert.</param>
    /// <returns>An instance of the array with the item replaced.</returns>
    /// <exception cref = "InvalidOperationException">The value was not an array.</exception>
    public TensorRank3 Replace(in JsonSchemaSample.Api.TensorRank3.SecondRank oldValue, in JsonSchemaSample.Api.TensorRank3.SecondRank newValue)
    {
        return new(this.GetImmutableListReplacing(oldValue.AsAny, newValue.AsAny));
    }

    /// <summary>
    /// Set the item at the given index.
    /// </summary>
    /// <param name = "index">The index at which to set the item.</param>
    /// <param name = "value">The value to set.</param>
    /// <returns>An instance of the array with the item set to the given value.</returns>
    public TensorRank3 SetItem(int index, in JsonSchemaSample.Api.TensorRank3.SecondRank value)
    {
        return new(this.GetImmutableListSetting(index, value.AsAny));
    }
}