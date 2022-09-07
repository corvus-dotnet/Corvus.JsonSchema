// <copyright file="JsonAny.Array.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents any JSON value.
/// </summary>
public readonly partial struct JsonAny : IJsonArray<JsonAny>
{
    /// <summary>
    /// Gets an empty array.
    /// </summary>
    public static readonly JsonAny EmptyArray = From(ImmutableList<JsonAny>.Empty);

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonAny"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonAny(ImmutableList<JsonAny> value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Array;
        this.stringBacking = string.Empty;
        this.boolBacking = default;
        this.numberBacking = default;
        this.arrayBacking = value;
        this.objectBacking = ImmutableDictionary<JsonPropertyName, JsonAny>.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonAny"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonAny(IEnumerable<JsonAny> value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Array;
        this.stringBacking = string.Empty;
        this.boolBacking = default;
        this.numberBacking = default;
        this.arrayBacking = value.ToImmutableList();
        this.objectBacking = ImmutableDictionary<JsonPropertyName, JsonAny>.Empty;
    }

    /// <inheritdoc/>
    public JsonAny this[int index]
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return new JsonAny(this.jsonElementBacking[index]);
            }

            if ((this.backing & Backing.Array) != 0)
            {
                try
                {
                    return this.arrayBacking[index];
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    throw new IndexOutOfRangeException(ex.Message, ex);
                }
            }

            throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Conversion from immutable list.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator ImmutableList<JsonAny>(JsonAny value)
    {
        return value.GetImmutableList();
    }

    /// <summary>s
    /// Conversion to immutable list.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonAny(ImmutableList<JsonAny> value)
    {
        return new(value);
    }

    /// <summary>
    /// Construct an instance of the array from a list of json values.
    /// </summary>
    /// <param name="items">The list of items from which to construct the array.</param>
    /// <returns>An instance of the array constructed from the list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonAny From(ImmutableList<JsonAny> items)
    {
        return new(items);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonAny"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    /// <returns>A JsonAny instantiated from the given array.</returns>
    public static JsonAny FromItems(params JsonAny[] value)
    {
        return new(value.ToImmutableList());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonAny"/> struct.
    /// </summary>
    /// <param name="value1">The first value from which to construct the instance.</param>
    /// <returns>A JsonAny instantiated from the given array.</returns>
    public static JsonAny FromItems(in JsonAny value1)
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        builder.Add(value1.AsAny);
        return new(builder.ToImmutable());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonAny"/> struct.
    /// </summary>
    /// <param name="value1">The first value from which to construct the instance.</param>
    /// <param name="value2">The second value from which to construct the instance.</param>
    /// <returns>A JsonAny instantiated from the given array.</returns>
    public static JsonAny FromItems(in JsonAny value1, in JsonAny value2)
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        builder.Add(value1);
        builder.Add(value2);
        return new(builder.ToImmutable());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonAny"/> struct.
    /// </summary>
    /// <param name="value1">The first value from which to construct the instance.</param>
    /// <param name="value2">The second value from which to construct the instance.</param>
    /// <param name="value3">The thirdvalue from which to construct the instance.</param>
    /// <returns>A JsonAny instantiated from the given array.</returns>
    public static JsonAny FromItems(in JsonAny value1, in JsonAny value2, in JsonAny value3)
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        builder.Add(value1);
        builder.Add(value2);
        builder.Add(value3);
        return new(builder.ToImmutable());
    }

    /// <summary>
    /// Create an array from the given items.
    /// </summary>
    /// <typeparam name="T">The type of the <paramref name="items"/> from which to create the array.</typeparam>
    /// <param name="items">The items from which to create the array.</param>
    /// <returns>The new array created from the items.</returns>
    /// <remarks>
    /// This will serialize the items to create the underlying JsonArray. Note the
    /// other overloads which avoid this serialization step.
    /// </remarks>
    public static JsonAny From<T>(IEnumerable<T> items)
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        foreach (T item in items)
        {
            var abw = new ArrayBufferWriter<byte>();
            using var writer = new Utf8JsonWriter(abw);
            JsonSerializer.Serialize(writer, item);
            writer.Flush();
            builder.Add(JsonAny.Parse(abw.WrittenMemory));
        }

        return new(builder.ToImmutable());
    }

    /// <summary>
    /// Create an array from the given items.
    /// </summary>
    /// <typeparam name="TItem">The type of the items in the array.</typeparam>
    /// <param name="items">The items from which to create the array.</param>
    /// <returns>The new array created from the items.</returns>
    public static JsonAny FromRange<TItem>(IEnumerable<TItem> items)
        where TItem : struct, IJsonValue<TItem>
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        foreach (TItem item in items)
        {
            builder.Add(item.AsAny);
        }

        return new JsonAny(builder.ToImmutable());
    }

    /// <summary>
    /// Create an array from the given items.
    /// </summary>
    /// <param name="items">The items from which to create the array.</param>
    /// <returns>The new array created from the items.</returns>
    public static JsonAny FromRange(IEnumerable<JsonAny> items)
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        foreach (JsonAny item in items)
        {
            builder.Add(item);
        }

        return new JsonArray(builder.ToImmutable());
    }

    /// <summary>
    /// Create an array from the given items.
    /// </summary>
    /// <param name="items">The items from which to create the array.</param>
    /// <returns>The new array created from the items.</returns>
    public static JsonAny FromRange(IEnumerable<string> items)
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        foreach (string item in items)
        {
            builder.Add(new JsonAny(item));
        }

        return new(builder.ToImmutable());
    }

    /// <summary>
    /// Create an array from the given items.
    /// </summary>
    /// <param name="items">The items from which to create the array.</param>
    /// <returns>The new array created from the items.</returns>
    public static JsonAny FromRange(IEnumerable<double> items)
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        foreach (double item in items)
        {
            builder.Add(new JsonAny(item));
        }

        return new(builder.ToImmutable());
    }

    /// <summary>
    /// Create an array from the given items.
    /// </summary>
    /// <param name="items">The items from which to create the array.</param>
    /// <returns>The new array created from the items.</returns>
    public static JsonAny FromRange(IEnumerable<float> items)
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        foreach (float item in items)
        {
            builder.Add(new JsonAny(item));
        }

        return new(builder.ToImmutable());
    }

    /// <summary>
    /// Create an array from the given items.
    /// </summary>
    /// <param name="items">The items from which to create the array.</param>
    /// <returns>The new array created from the items.</returns>
    public static JsonAny FromRange(IEnumerable<int> items)
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        foreach (int item in items)
        {
            builder.Add(new JsonAny(item));
        }

        return new(builder.ToImmutable());
    }

    /// <summary>
    /// Create an array from the given items.
    /// </summary>
    /// <param name="items">The items from which to create the array.</param>
    /// <returns>The new array created from the items.</returns>
    public static JsonAny FromRange(IEnumerable<long> items)
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        foreach (long item in items)
        {
            builder.Add(new JsonAny(item));
        }

        return new(builder.ToImmutable());
    }

    /// <summary>
    /// Create an array from the given items.
    /// </summary>
    /// <param name="items">The items from which to create the array.</param>
    /// <returns>The new array created from the items.</returns>
    public static JsonAny FromRange(IEnumerable<bool> items)
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        foreach (bool item in items)
        {
            builder.Add(new JsonAny(item));
        }

        return new(builder.ToImmutable());
    }

    /// <inheritdoc/>
    public ImmutableList<JsonAny> AsImmutableList()
    {
        return this.GetImmutableList();
    }

    /// <inheritdoc/>
    public ImmutableList<JsonAny>.Builder AsImmutableListBuilder()
    {
        return this.GetImmutableListBuilder();
    }

    /// <inheritdoc/>
    public int GetArrayLength()
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            return this.jsonElementBacking.GetArrayLength();
        }

        if ((this.backing & Backing.Array) != 0)
        {
            return this.arrayBacking.Count;
        }

        return 0;
    }

    /// <inheritdoc/>
    public JsonArrayEnumerator EnumerateArray()
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            return new JsonArrayEnumerator(this.jsonElementBacking);
        }

        if ((this.backing & Backing.Array) != 0)
        {
            return new JsonArrayEnumerator(this.arrayBacking);
        }

        throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public JsonArrayEnumerator<TItem> EnumerateArray<TItem>()
        where TItem : struct, IJsonValue<TItem>
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            return new JsonArrayEnumerator<TItem>(this.jsonElementBacking);
        }

        if ((this.backing & Backing.Array) != 0)
        {
            return new JsonArrayEnumerator<TItem>(this.arrayBacking);
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Builds an <see cref="ImmutableList{JsonAny}"/> from the array.
    /// </summary>
    /// <returns>An immutable list of <see cref="JsonAny"/> built from the array.</returns>
    /// <exception cref="InvalidOperationException">The value is not an array.</exception>
    private ImmutableList<JsonAny> GetImmutableList()
    {
        if ((this.backing & Backing.Array) != 0)
        {
            return this.arrayBacking;
        }

        return this.GetImmutableListBuilder().ToImmutable();
    }

    /// <summary>
    /// Builds an <see cref="ImmutableList{JsonAny}.Builder"/> from the array.
    /// </summary>
    /// <returns>An immutable list builder of <see cref="JsonAny"/>, built from the existing array.</returns>
    /// <exception cref="InvalidOperationException">The value is not an array.</exception>
    private ImmutableList<JsonAny>.Builder GetImmutableListBuilder()
    {
        if ((this.backing & Backing.JsonElement) != 0 && this.jsonElementBacking.ValueKind == JsonValueKind.Array)
        {
            ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
            foreach (JsonElement item in this.jsonElementBacking.EnumerateArray())
            {
                builder.Add(new(item));
            }

            return builder;
        }

        if ((this.backing & Backing.Array) != 0)
        {
            return this.arrayBacking.ToBuilder();
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Builds an <see cref="ImmutableList{JsonAny}"/> from the array, removing the first item that equals the given value.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <returns>An immutable list containing the contents of the list, without the first instance that matches the given item.</returns>
    /// <exception cref="InvalidOperationException">The value is not an array.</exception>
    private ImmutableList<JsonAny> GetImmutableListWithout(in JsonAny item)
    {
        if ((this.backing & Backing.JsonElement) != 0 && this.jsonElementBacking.ValueKind == JsonValueKind.Array)
        {
            return JsonValueHelpers.GetImmutableListFromJsonElementWithout(this.jsonElementBacking, item);
        }

        if ((this.backing & Backing.Array) != 0)
        {
            return this.arrayBacking.Remove(item);
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Builds an <see cref="ImmutableList{JsonAny}"/> from the array, removing the given range.
    /// </summary>
    /// <param name="index">The start index of the range to remove.</param>
    /// <param name="count">The length of the range to remove.</param>
    /// <returns>An immutable list containing the contents of the list, without the given range of items.</returns>
    /// <exception cref="InvalidOperationException">The value is not an array.</exception>
    /// <exception cref="IndexOutOfRangeException">Thrown if the range is beyond the bounds of the array.</exception>
    private ImmutableList<JsonAny> GetImmutableListWithoutRange(int index, int count)
    {
        if ((this.backing & Backing.JsonElement) != 0 && this.jsonElementBacking.ValueKind == JsonValueKind.Array)
        {
            return JsonValueHelpers.GetImmutableListFromJsonElementWithoutRange(this.jsonElementBacking, index, count);
        }

        if ((this.backing & Backing.Array) != 0)
        {
            try
            {
                return this.arrayBacking.RemoveRange(index, count);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new IndexOutOfRangeException(ex.Message, ex);
            }
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Builds an <see cref="ImmutableList{JsonAny}"/> from the array, adding the given item.
    /// </summary>
    /// <param name="index">The index at which to add the element.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>An immutable list containing the contents of the list, with the item inserted at the given index.</returns>
    /// <exception cref="InvalidOperationException">The value is not an array.</exception>
    /// <exception cref="IndexOutOfRangeException">Thrown if the range is beyond the bounds of the array.</exception>
    private ImmutableList<JsonAny> GetImmutableListWith(int index, in JsonAny value)
    {
        if ((this.backing & Backing.JsonElement) != 0 && this.jsonElementBacking.ValueKind == JsonValueKind.Array)
        {
            return JsonValueHelpers.GetImmutableListFromJsonElementWith(this.jsonElementBacking, index, value);
        }

        if ((this.backing & Backing.Array) != 0)
        {
            try
            {
                return this.arrayBacking.Insert(index, value);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new IndexOutOfRangeException(ex.Message, ex);
            }
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Builds an <see cref="ImmutableList{JsonAny}"/> from the array, replacing the item at the specified index with the given item.
    /// </summary>
    /// <param name="index">The index at which to add the element.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>An immutable list containing the contents of the list, with the specified item at the index.</returns>
    /// <exception cref="InvalidOperationException">The value is not an array.</exception>
    /// <exception cref="IndexOutOfRangeException">Thrown if the range is beyond the bounds of the array.</exception>
    private ImmutableList<JsonAny> GetImmutableListSetting(int index, in JsonAny value)
    {
        if ((this.backing & Backing.JsonElement) != 0 && this.jsonElementBacking.ValueKind == JsonValueKind.Array)
        {
            return JsonValueHelpers.GetImmutableListFromJsonElementSetting(this.jsonElementBacking, index, value);
        }

        if ((this.backing & Backing.Array) != 0)
        {
            try
            {
                return this.arrayBacking.SetItem(index, value);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new IndexOutOfRangeException(ex.Message, ex);
            }
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Builds an <see cref="ImmutableList{JsonAny}"/> from the array, removing the first item that equals the given value, and replacing it with the specified item.
    /// </summary>
    /// <param name="oldItem">The item to remove.</param>
    /// <param name="newItem">The item to insert.</param>
    /// <returns>An immutable list containing the contents of the list, without the first instance that matches the old item, replacing it with the new item.</returns>
    /// <exception cref="InvalidOperationException">The value is not an array.</exception>
    private ImmutableList<JsonAny> GetImmutableListReplacing(in JsonAny oldItem, in JsonAny newItem)
    {
        if ((this.backing & Backing.JsonElement) != 0 && this.jsonElementBacking.ValueKind == JsonValueKind.Array)
        {
            return JsonValueHelpers.GetImmutableListFromJsonElementReplacing(this.jsonElementBacking, oldItem, newItem);
        }

        if ((this.backing & Backing.Array) != 0)
        {
            return this.arrayBacking.Replace(oldItem, newItem);
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Builds an <see cref="ImmutableList{JsonAny}"/> from the array, adding the given item.
    /// </summary>
    /// <param name="index">The index at which to add the element.</param>
    /// <param name="values">The values to add.</param>
    /// <returns>An immutable list containing the contents of the list, without the array.</returns>
    /// <exception cref="InvalidOperationException">The value is not an array.</exception>
    /// <exception cref="IndexOutOfRangeException">Thrown if the range is beyond the bounds of the array.</exception>
    private ImmutableList<JsonAny> GetImmutableListWith<TEnumerable>(int index, TEnumerable values)
        where TEnumerable : IEnumerable<JsonAny>
    {
        if ((this.backing & Backing.JsonElement) != 0 && this.jsonElementBacking.ValueKind == JsonValueKind.Array)
        {
            return JsonValueHelpers.GetImmutableListFromJsonElementWith(this.jsonElementBacking, index, values);
        }

        if ((this.backing & Backing.Array) != 0)
        {
            try
            {
                return this.arrayBacking.InsertRange(index, values);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new IndexOutOfRangeException(ex.Message, ex);
            }
        }

        throw new InvalidOperationException();
    }
}