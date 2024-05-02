//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
#nullable enable
using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Internal;

namespace JsonSchemaSample.Api;
/// <summary>
/// Generated from JSON Schema.
/// </summary>
public readonly partial struct Test : IJsonArray<Test>
{
    /// <summary>
    /// Gets an empty array.
    /// </summary>
    public static readonly Test EmptyArray = From(ImmutableList<JsonAny>.Empty);
    /// <summary>
    /// Initializes a new instance of the <see cref = "Test"/> struct.
    /// </summary>
    /// <param name = "value">The value from which to construct the instance.</param>
    public Test(ImmutableList<JsonAny> value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Array;
        this.arrayBacking = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref = "Test"/> struct.
    /// </summary>
    /// <param name = "value">The value from which to construct the instance.</param>
    public Test(IEnumerable<JsonAny> value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Array;
        this.arrayBacking = value.ToImmutableList();
    }

    /// <inheritdoc/>
    JsonAny IJsonArray<Test>.this[int index]
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
    /// Gets the tuple item as a <see cref = "Corvus.Json.JsonInt32"/>.
    /// </summary>
    public Corvus.Json.JsonInt32 Item1
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return new Corvus.Json.JsonInt32(this.jsonElementBacking[0]);
            }

            if ((this.backing & Backing.Array) != 0)
            {
                try
                {
                    return this.arrayBacking[0].As<Corvus.Json.JsonInt32>();
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
    /// Gets the tuple item as a <see cref = "Corvus.Json.JsonString"/>.
    /// </summary>
    public Corvus.Json.JsonString Item2
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return new Corvus.Json.JsonString(this.jsonElementBacking[1]);
            }

            if ((this.backing & Backing.Array) != 0)
            {
                try
                {
                    return this.arrayBacking[1].As<Corvus.Json.JsonString>();
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
    /// Gets the tuple item as a <see cref = "Corvus.Json.JsonDateTime"/>.
    /// </summary>
    public Corvus.Json.JsonDateTime Item3
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return new Corvus.Json.JsonDateTime(this.jsonElementBacking[2]);
            }

            if ((this.backing & Backing.Array) != 0)
            {
                try
                {
                    return this.arrayBacking[2].As<Corvus.Json.JsonDateTime>();
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
    /// Conversion to tuple.
    /// </summary>
    /// <param name = "value">The value from which to convert.</param>
    public static implicit operator (Corvus.Json.JsonInt32, Corvus.Json.JsonString, Corvus.Json.JsonDateTime)(Test value)
    {
        return (value.Item1, value.Item2, value.Item3);
    }

    /// <summary>
    /// Conversion from tuple.
    /// </summary>
    /// <param name = "value">The value from which to convert.</param>
    public static implicit operator Test((Corvus.Json.JsonInt32, Corvus.Json.JsonString, Corvus.Json.JsonDateTime) value)
    {
        return Test.Create(value.Item1, value.Item2, value.Item3);
    }

    /// <summary>
    /// Conversion from immutable list.
    /// </summary>
    /// <param name = "value">The value from which to convert.</param>
    public static implicit operator ImmutableList<JsonAny>(Test value)
    {
        return value.GetImmutableList();
    }

    /// <summary>s
    /// Conversion to immutable list.
    /// </summary>
    /// <param name = "value">The value from which to convert.</param>
    public static implicit operator Test(ImmutableList<JsonAny> value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion from JsonArray.
    /// </summary>
    /// <param name = "value">The value from which to convert.</param>
    public static implicit operator Test(JsonArray value)
    {
        if (value.HasDotnetBacking && value.ValueKind == JsonValueKind.Array)
        {
            return new(value.AsImmutableList());
        }

        return new(value.AsJsonElement);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref = "Test"/> struct.
    /// </summary>
    /// <param name = "items">The list of items from which to construct the array.</param>
    /// <returns>An instance of the array constructed from the list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Test From(ImmutableList<JsonAny> items)
    {
        return new(items);
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Create an array from the given items.
    /// </summary>
    /// <param name = "items">The items from which to create the array.</param>
    /// <returns>The new array created from the items.</returns>
    /// <remarks>
    /// This will serialize the items to create the underlying JsonArray. Note the
    /// other overloads which avoid this serialization step.
    /// </remarks>
    static Test IJsonArray<Test>.FromRange(IEnumerable<JsonAny> items)
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        foreach (JsonAny item in items)
        {
            builder.Add(item);
        }

        return new Test(builder.ToImmutable());
    }

    /// <summary>
    /// Create an array from the given items.
    /// </summary>
    /// <param name = "items">The items from which to create the array.</param>
    /// <returns>The new array created from the items.</returns>
    /// <remarks>
    /// This will serialize the items to create the underlying JsonArray. Note the
    /// other overloads which avoid this serialization step.
    /// </remarks>
    static Test IJsonArray<Test>.FromRange<T>(IEnumerable<T> items)
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        foreach (T item in items)
        {
            builder.Add(item.AsAny);
        }

        return new Test(builder.ToImmutable());
    }
#endif
    /// <summary>
    /// Create a tuple from the given items.
    /// </summary>
    /// <param name = "item1">An instance of a <see cref = "Corvus.Json.JsonInt32"/>.</param>
    /// <param name = "item2">An instance of a <see cref = "Corvus.Json.JsonString"/>.</param>
    /// <param name = "item3">An instance of a <see cref = "Corvus.Json.JsonDateTime"/>.</param>
    /// <returns>The new tuple created from the items.</returns>
    public static Test Create(Corvus.Json.JsonInt32 item1, Corvus.Json.JsonString item2, Corvus.Json.JsonDateTime item3)
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        builder.Add(item1.AsAny);
        builder.Add(item2.AsAny);
        builder.Add(item3.AsAny);
        return new Test(builder.ToImmutable());
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
    JsonArrayEnumerator<TItem> IJsonArray<Test>.EnumerateArray<TItem>()
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
    /// Builds an <see cref = "ImmutableList{JsonAny}"/> from the array.
    /// </summary>
    /// <returns>An immutable list of <see cref = "JsonAny"/> built from the array.</returns>
    /// <exception cref = "InvalidOperationException">The value is not an array.</exception>
    private ImmutableList<JsonAny> GetImmutableList()
    {
        if ((this.backing & Backing.Array) != 0)
        {
            return this.arrayBacking;
        }

        return this.GetImmutableListBuilder().ToImmutable();
    }

    /// <summary>
    /// Builds an <see cref = "ImmutableList{JsonAny}.Builder"/> from the array.
    /// </summary>
    /// <returns>An immutable list builder of <see cref = "JsonAny"/>, built from the existing array.</returns>
    /// <exception cref = "InvalidOperationException">The value is not an array.</exception>
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
    /// Builds an <see cref = "ImmutableList{JsonAny}"/> from the array, replacing the item at the specified index with the given item.
    /// </summary>
    /// <param name = "index">The index at which to add the element.</param>
    /// <param name = "value">The value to add.</param>
    /// <returns>An immutable list containing the contents of the list, with the specified item at the index.</returns>
    /// <exception cref = "InvalidOperationException">The value is not an array.</exception>
    /// <exception cref = "IndexOutOfRangeException">Thrown if the range is beyond the bounds of the array.</exception>
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
    /// Builds an <see cref = "ImmutableList{JsonAny}"/> from the array, removing the first item that equals the given value, and replacing it with the specified item.
    /// </summary>
    /// <param name = "oldItem">The item to remove.</param>
    /// <param name = "newItem">The item to insert.</param>
    /// <returns>An immutable list containing the contents of the list, without the first instance that matches the old item, replacing it with the new item.</returns>
    /// <exception cref = "InvalidOperationException">The value is not an array.</exception>
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
    /// Builds an <see cref = "ImmutableList{JsonAny}"/> from the array, removing the first item that equals the given value.
    /// </summary>
    /// <param name = "item">The item to remove.</param>
    /// <returns>An immutable list containing the contents of the list, without the first instance that matches the given item.</returns>
    /// <exception cref = "InvalidOperationException">The value is not an array.</exception>
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
    /// Builds an <see cref = "ImmutableList{JsonAny}"/> from the array, removing the given range.
    /// </summary>
    /// <param name = "index">The start index of the range to remove.</param>
    /// <param name = "count">The length of the range to remove.</param>
    /// <returns>An immutable list containing the contents of the list, without the given range of items.</returns>
    /// <exception cref = "InvalidOperationException">The value is not an array.</exception>
    /// <exception cref = "IndexOutOfRangeException">Thrown if the range is beyond the bounds of the array.</exception>
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
    /// Builds an <see cref = "ImmutableList{JsonAny}"/> from the array, adding the given item.
    /// </summary>
    /// <param name = "index">The index at which to add the element.</param>
    /// <param name = "value">The value to add.</param>
    /// <returns>An immutable list containing the contents of the list, without the array.</returns>
    /// <exception cref = "InvalidOperationException">The value is not an array.</exception>
    /// <exception cref = "IndexOutOfRangeException">Thrown if the range is beyond the bounds of the array.</exception>
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
    /// Builds an <see cref = "ImmutableList{JsonAny}"/> from the array, adding the given item.
    /// </summary>
    /// <param name = "index">The index at which to add the element.</param>
    /// <param name = "values">The values to add.</param>
    /// <returns>An immutable list containing the contents of the list, without the array.</returns>
    /// <exception cref = "InvalidOperationException">The value is not an array.</exception>
    /// <exception cref = "IndexOutOfRangeException">Thrown if the range is beyond the bounds of the array.</exception>
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