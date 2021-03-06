// <copyright file="JsonArray.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// A JSON array.
    /// </summary>
    public readonly struct JsonArray : IJsonArray<JsonArray>, IEquatable<JsonArray>
    {
        /// <summary>
        /// An empty JsonArray.
        /// </summary>
#pragma warning disable SA1000 // Keywords should be spaced correctly
        public static readonly JsonArray Empty = new(ImmutableList<JsonAny>.Empty);
#pragma warning restore SA1000 // Keywords should be spaced correctly

        private readonly JsonElement jsonElement;
        private readonly ImmutableList<JsonAny>? items;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonArray"/> struct.
        /// </summary>
        /// <param name="jsonElement">The JSON element from which to construct the array.</param>
        public JsonArray(JsonElement jsonElement)
        {
            this.jsonElement = jsonElement;
            this.items = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonArray"/> struct.
        /// </summary>
        /// <param name="items">An immutable list of items in the array.</param>
        public JsonArray(ImmutableList<JsonAny> items)
        {
            this.jsonElement = default;
            this.items = items;
        }

        /// <summary>
        /// Gets the <see cref="JsonValueKind"/>.
        /// </summary>
        public JsonValueKind ValueKind
        {
            get
            {
                if (this.items is not null)
                {
                    return JsonValueKind.Array;
                }

                return this.jsonElement.ValueKind;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this is backed by a <see cref="JsonElement"/>.
        /// </summary>
        public bool HasJsonElement => this.items is null;

        /// <inheritdoc/>
        public int Length
        {
            get
            {
                if (this.items is ImmutableList<JsonAny> items)
                {
                    return items.Count;
                }

                return this.jsonElement.GetArrayLength();
            }
        }

        /// <summary>
        /// Gets the backing <see cref="JsonElement"/>.
        /// </summary>
        public JsonElement AsJsonElement
        {
            get
            {
                if (this.items is ImmutableList<JsonAny> items)
                {
                    return ItemsToJsonElement(items);
                }

                return this.jsonElement;
            }
        }

        /// <inheritdoc/>
        public JsonAny AsAny
        {
            get
            {
                return new JsonAny(this);
            }
        }

        /// <summary>
        /// Gets the instance as a list of <see cref="JsonAny"/>.
        /// </summary>
        public ImmutableList<JsonAny> AsItemsList
        {
            get
            {
                if (this.items is ImmutableList<JsonAny> items)
                {
                    return items;
                }

                if (this.jsonElement.ValueKind == JsonValueKind.Array)
                {
                    ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
                    foreach (JsonElement item in this.jsonElement.EnumerateArray())
                    {
                        builder.Add(new JsonAny(item));
                    }

                    return builder.ToImmutable();
                }

                return ImmutableList<JsonAny>.Empty;
            }
        }

        /// <summary>
        /// Implicit conversion to JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(JsonArray value)
        {
            return value.AsAny;
        }

        /// <summary>
        /// Implicit conversion from JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonArray(JsonAny value)
        {
            return value.AsArray;
        }

        /// <summary>
        /// Implicit conversion to an <see cref="ImmutableList{T}"/> of <see cref="JsonAny"/>.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator ImmutableList<JsonAny>(JsonArray value)
        {
            return value.AsItemsList;
        }

        /// <summary>
        /// Implicit conversion from an <see cref="ImmutableList{T}"/> of <see cref="JsonAny"/>.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonArray(ImmutableList<JsonAny> value)
        {
            return new JsonArray(value);
        }

        /// <summary>
        /// Standard equality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are equal.</returns>
        public static bool operator ==(JsonArray lhs, JsonArray rhs)
        {
            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Standard inequality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are not equal.</returns>
        public static bool operator !=(JsonArray lhs, JsonArray rhs)
        {
            return !lhs.Equals(rhs);
        }

        /// <summary>
        /// Create an array from the given items.
        /// </summary>
        /// <param name="items">The items from which to create the array.</param>
        /// <returns>The new array created from the items.</returns>
        public static JsonArray From(params JsonAny[] items)
        {
            return new JsonArray(items.ToImmutableList());
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
        public static JsonArray From<T>(IEnumerable<T> items)
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

            return new JsonArray(builder.ToImmutable());
        }

        /// <summary>
        /// Create an array from the given items.
        /// </summary>
        /// <param name="items">The items from which to create the array.</param>
        /// <returns>The new array created from the items.</returns>
        public static JsonArray From(IEnumerable<IJsonValue> items)
        {
            ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
            foreach (IJsonValue item in items)
            {
                builder.Add(item.AsAny);
            }

            return new JsonArray(builder.ToImmutable());
        }

        /// <summary>
        /// Create an array from the given items.
        /// </summary>
        /// <param name="items">The items from which to create the array.</param>
        /// <returns>The new array created from the items.</returns>
        public static JsonArray From(IEnumerable<string> items)
        {
            ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
            foreach (string item in items)
            {
                builder.Add(new JsonAny(item));
            }

            return new JsonArray(builder.ToImmutable());
        }

        /// <summary>
        /// Create an array from the given items.
        /// </summary>
        /// <param name="items">The items from which to create the array.</param>
        /// <returns>The new array created from the items.</returns>
        public static JsonArray From(IEnumerable<double> items)
        {
            ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
            foreach (double item in items)
            {
                builder.Add(new JsonAny(item));
            }

            return new JsonArray(builder.ToImmutable());
        }

        /// <summary>
        /// Create an array from the given items.
        /// </summary>
        /// <param name="items">The items from which to create the array.</param>
        /// <returns>The new array created from the items.</returns>
        public static JsonArray From(IEnumerable<float> items)
        {
            ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
            foreach (float item in items)
            {
                builder.Add(new JsonAny(item));
            }

            return new JsonArray(builder.ToImmutable());
        }

        /// <summary>
        /// Create an array from the given items.
        /// </summary>
        /// <param name="items">The items from which to create the array.</param>
        /// <returns>The new array created from the items.</returns>
        public static JsonArray From(IEnumerable<int> items)
        {
            ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
            foreach (int item in items)
            {
                builder.Add(new JsonAny(item));
            }

            return new JsonArray(builder.ToImmutable());
        }

        /// <summary>
        /// Create an array from the given items.
        /// </summary>
        /// <param name="items">The items from which to create the array.</param>
        /// <returns>The new array created from the items.</returns>
        public static JsonArray From(IEnumerable<long> items)
        {
            ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
            foreach (long item in items)
            {
                builder.Add(new JsonAny(item));
            }

            return new JsonArray(builder.ToImmutable());
        }

        /// <summary>
        /// Create an array from the given items.
        /// </summary>
        /// <param name="items">The items from which to create the array.</param>
        /// <returns>The new array created from the items.</returns>
        public static JsonArray From(IEnumerable<bool> items)
        {
            ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
            foreach (bool item in items)
            {
                builder.Add(new JsonAny(item));
            }

            return new JsonArray(builder.ToImmutable());
        }

        /// <summary>
        /// Create an array from the given items.
        /// </summary>
        /// <param name="item1">The items from which to create the array.</param>
        /// <returns>The new array created from the items.</returns>
        public static JsonArray From(JsonAny item1)
        {
            return new JsonArray(ImmutableList.Create(item1));
        }

        /// <summary>
        /// Create an array from the given items.
        /// </summary>
        /// <param name="item1">The first item from which to create the array.</param>
        /// <param name="item2">The second item from which to create the array.</param>
        /// <returns>The new array created from the items.</returns>
        public static JsonArray From(JsonAny item1, JsonAny item2)
        {
            return new JsonArray(ImmutableList.Create(item1, item2));
        }

        /// <summary>
        /// Create an array from the given items.
        /// </summary>
        /// <param name="item1">The first item from which to create the array.</param>
        /// <param name="item2">The second item from which to create the array.</param>
        /// <param name="item3">The third item from which to create the array.</param>
        /// <returns>The new array created from the items.</returns>
        public static JsonArray From(JsonAny item1, JsonAny item2, JsonAny item3)
        {
            return new JsonArray(ImmutableList.Create(item1, item2, item3));
        }

        /// <summary>
        /// Create an array from the given items.
        /// </summary>
        /// <param name="item1">The first item from which to create the array.</param>
        /// <param name="item2">The second item from which to create the array.</param>
        /// <param name="item3">The third item from which to create the array.</param>
        /// <param name="item4">The fourth item from which to create the array.</param>
        /// <returns>The new array created from the items.</returns>
        public static JsonArray From(JsonAny item1, JsonAny item2, JsonAny item3, JsonAny item4)
        {
            return new JsonArray(ImmutableList.Create(item1, item2, item3, item4));
        }

        /// <summary>
        /// Convert an items array to a <see cref="JsonElement"/>.
        /// </summary>
        /// <param name="items">The items to convert.</param>
        /// <returns>The <see cref="JsonElement"/>.</returns>
        public static JsonElement ItemsToJsonElement(ImmutableList<JsonAny> items)
        {
            var abw = new ArrayBufferWriter<byte>();
            using var writer = new Utf8JsonWriter(abw);
            WriteItems(items, writer);
            writer.Flush();
            var reader = new Utf8JsonReader(abw.WrittenSpan);
            using var document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.Clone();
        }

        /// <summary>
        /// Write an items array to a <see cref="Utf8JsonWriter"/>.
        /// </summary>
        /// <param name="items">The items to write.</param>
        /// <param name="writer">The writer to which to write the array.</param>
        public static void WriteItems(ImmutableList<JsonAny> items, Utf8JsonWriter writer)
        {
            writer.WriteStartArray();

            foreach (JsonAny item in items)
            {
                item.WriteTo(writer);
            }

            writer.WriteEndArray();
        }

        /// <inheritdoc/>
        public T As<T>()
            where T : struct, IJsonValue
        {
            return this.As<JsonArray, T>();
        }

        /// <inheritdoc/>
        public ValidationContext Validate(in ValidationContext validationContext, ValidationLevel level = ValidationLevel.Flag)
        {
            ValidationContext result = validationContext;

            return Json.Validate.TypeArray(this.ValueKind, result, level);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (obj is IJsonValue jv)
            {
                return this.Equals(jv.AsAny);
            }

            return obj is null && this.IsNull();
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            JsonValueKind valueKind = this.ValueKind;

            return valueKind switch
            {
                JsonValueKind.Array => this.GetHashCodeCore(),
                JsonValueKind.Null => JsonNull.NullHashCode,
                _ => JsonAny.UndefinedHashCode,
            };
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.Serialize();
        }

        /// <summary>
        /// Writes the array to the <see cref="Utf8JsonWriter"/>.
        /// </summary>
        /// <param name="writer">The writer to which to write the array.</param>
        public void WriteTo(Utf8JsonWriter writer)
        {
            if (this.items is ImmutableList<JsonAny> items)
            {
                WriteItems(items, writer);
            }
            else
            {
                this.jsonElement.WriteTo(writer);
            }
        }

        /// <inheritdoc/>
        public JsonArrayEnumerator EnumerateArray()
        {
            if (this.items is ImmutableList<JsonAny> items)
            {
                return new JsonArrayEnumerator(items);
            }

            if (this.jsonElement.ValueKind == JsonValueKind.Array)
            {
                return new JsonArrayEnumerator(this.jsonElement);
            }

            return default;
        }

        /// <inheritdoc/>
        public bool Equals<T>(T other)
            where T : struct, IJsonValue
        {
            if (this.IsNull() && other.IsNull())
            {
                return true;
            }

            if (other.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return this.Equals(other.AsArray());
        }

        /// <inheritdoc/>
        public bool Equals(JsonArray other)
        {
            if (this.IsNull() && other.IsNull())
            {
                return true;
            }

            if (other.ValueKind != this.ValueKind || this.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            JsonArrayEnumerator lhs = this.EnumerateArray();
            JsonArrayEnumerator rhs = other.EnumerateArray();
            while (lhs.MoveNext())
            {
                if (!rhs.MoveNext())
                {
                    return false;
                }

                if (!lhs.Current.Equals(rhs.Current))
                {
                    return false;
                }
            }

            if (rhs.MoveNext())
            {
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public JsonArray Add<TItem>(TItem item)
            where TItem : struct, IJsonValue
        {
            return this.AsItemsListWith(item.AsAny);
        }

        /// <inheritdoc/>
        public JsonArray Add<TItem1, TItem2>(TItem1 item1, TItem2 item2)
            where TItem1 : struct, IJsonValue
            where TItem2 : struct, IJsonValue
        {
            return this.AsItemsListWith(item1.AsAny, item2.AsAny);
        }

        /// <inheritdoc/>
        public JsonArray Add<TItem1, TItem2, TItem3>(TItem1 item1, TItem2 item2, TItem3 item3)
            where TItem1 : struct, IJsonValue
            where TItem2 : struct, IJsonValue
            where TItem3 : struct, IJsonValue
        {
            return this.AsItemsListWith(item1.AsAny, item2.AsAny, item3.AsAny);
        }

        /// <inheritdoc/>
        public JsonArray Add<TItem1, TItem2, TItem3, TItem4>(TItem1 item1, TItem2 item2, TItem3 item3, TItem4 item4)
            where TItem1 : struct, IJsonValue
            where TItem2 : struct, IJsonValue
            where TItem3 : struct, IJsonValue
            where TItem4 : struct, IJsonValue
        {
            return this.AsItemsListWith(item1.AsAny, item2.AsAny, item3.AsAny, item4.AsAny);
        }

        /// <inheritdoc/>
        public JsonArray Add<TItem>(params TItem[] items)
            where TItem : struct, IJsonValue
        {
            return this.AsItemsListWith(items);
        }

        /// <inheritdoc/>
        public JsonArray AddRange<TItem>(IEnumerable<TItem> items)
            where TItem : struct, IJsonValue
        {
            return this.AsItemsListWith(items);
        }

        /// <inheritdoc/>
        public JsonArray Insert<TItem>(int index, TItem item)
            where TItem : struct, IJsonValue
        {
            return this.AsItemsListInserting(index, item.AsAny);
        }

        /// <inheritdoc/>
        public JsonArray Replace<TItem>(TItem oldValue, TItem newValue)
            where TItem : struct, IJsonValue
        {
            return this.AsItemsListReplacing(oldValue.AsAny, newValue.AsAny);
        }

        /// <inheritdoc/>
        public JsonArray SetItem<TItem>(int index, TItem value)
            where TItem : struct, IJsonValue
        {
            return this.AsItemsListSetting(index, value.AsAny);
        }

        /// <inheritdoc/>
        public JsonArray RemoveAt(int index)
        {
            return this.AsItemsListRemovingAt(index);
        }

        /// <inheritdoc/>
        public JsonArray RemoveRange(int index, int count)
        {
            return this.AsItemsListRemovingRange(index, count);
        }

        private ImmutableList<JsonAny> AsItemsListRemovingAt(int index)
        {
            return this.AsItemsListRemovingRange(index, 1);
        }

        private ImmutableList<JsonAny> AsItemsListInserting(int index, JsonAny value)
        {
            if (this.items is ImmutableList<JsonAny> items)
            {
                return items.Insert(index, value);
            }

            if (this.jsonElement.ValueKind == JsonValueKind.Array)
            {
                int arrayLength = this.jsonElement.GetArrayLength();
                if (index > arrayLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
                int current = 0;
                bool inserted = false;
                foreach (JsonElement existingItem in this.jsonElement.EnumerateArray())
                {
                    if (current == index)
                    {
                        inserted = true;
                        builder.Add(value);
                    }

                    builder.Add(new JsonAny(existingItem));
                    ++current;
                }

                if (!inserted)
                {
                    builder.Add(value);
                }

                return builder.ToImmutable();
            }

            return ImmutableList<JsonAny>.Empty;
        }

        private ImmutableList<JsonAny> AsItemsListReplacing(JsonAny oldValue, JsonAny newValue)
        {
            if (this.items is ImmutableList<JsonAny> items)
            {
                return items.Replace(oldValue, newValue);
            }

            if (this.jsonElement.ValueKind == JsonValueKind.Array)
            {
                ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
                foreach (JsonElement existingItem in this.jsonElement.EnumerateArray())
                {
                    var oldAny = new JsonAny(existingItem);
                    if (oldAny == oldValue)
                    {
                        builder.Add(newValue);
                    }
                    else
                    {
                        builder.Add(oldAny);
                    }
                }

                return builder.ToImmutable();
            }

            return ImmutableList<JsonAny>.Empty;
        }

        private ImmutableList<JsonAny> AsItemsListSetting(int index, JsonAny value)
        {
            if (this.items is ImmutableList<JsonAny> items)
            {
                return items.SetItem(index, value);
            }

            if (this.jsonElement.ValueKind == JsonValueKind.Array)
            {
                int arrayLength = this.jsonElement.GetArrayLength();
                if (index >= arrayLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
                int current = 0;
                foreach (JsonElement existingItem in this.jsonElement.EnumerateArray())
                {
                    if (current == index)
                    {
                        builder.Add(value);
                    }
                    else
                    {
                        builder.Add(new JsonAny(existingItem));
                    }

                    ++current;
                }

                return builder.ToImmutable();
            }

            return ImmutableList<JsonAny>.Empty;
        }

        private ImmutableList<JsonAny> AsItemsListRemovingRange(int index, int count)
        {
            if (this.items is ImmutableList<JsonAny> items)
            {
                return items.RemoveRange(index, count);
            }

            if (this.jsonElement.ValueKind == JsonValueKind.Array)
            {
                int arrayLength = this.jsonElement.GetArrayLength();
                if (index >= arrayLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                if (index + count > arrayLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(count));
                }

                ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
                int current = 0;
                int end = index + count;
                foreach (JsonElement existingItem in this.jsonElement.EnumerateArray())
                {
                    if (current >= index && current < end)
                    {
                        ++current;
                        continue;
                    }

                    builder.Add(new JsonAny(existingItem));
                    ++current;
                }

                return builder.ToImmutable();
            }

            return ImmutableList<JsonAny>.Empty;
        }

        private ImmutableList<JsonAny> AsItemsListWith(JsonAny item)
        {
            if (this.items is ImmutableList<JsonAny> items)
            {
                return items.Add(item);
            }

            if (this.jsonElement.ValueKind == JsonValueKind.Array)
            {
                ImmutableList<JsonAny>.Builder builder = this.CreateListFromJsonElement();
                builder.Add(item);
                return builder.ToImmutable();
            }

            return ImmutableList<JsonAny>.Empty;
        }

        private ImmutableList<JsonAny> AsItemsListWith(in JsonAny item1, in JsonAny item2)
        {
            JsonAny[] itemsArray = ArrayPool<JsonAny>.Shared.Rent(2);
            itemsArray[0] = item1;
            itemsArray[1] = item2;

            try
            {
                return this.AsItemsListWith(itemsArray.AsSpan()[..2]);
            }
            finally
            {
                ArrayPool<JsonAny>.Shared.Return(itemsArray);
            }
        }

        private ImmutableList<JsonAny> AsItemsListWith(in JsonAny item1, in JsonAny item2, in JsonAny item3)
        {
            JsonAny[] itemsArray = ArrayPool<JsonAny>.Shared.Rent(3);
            itemsArray[0] = item1;
            itemsArray[1] = item2;
            itemsArray[2] = item3;

            try
            {
                return this.AsItemsListWith(itemsArray.AsSpan()[..3]);
            }
            finally
            {
                ArrayPool<JsonAny>.Shared.Return(itemsArray);
            }
        }

        private ImmutableList<JsonAny> AsItemsListWith(in JsonAny item1, in JsonAny item2, in JsonAny item3, in JsonAny item4)
        {
            JsonAny[] itemsArray = ArrayPool<JsonAny>.Shared.Rent(4);
            itemsArray[0] = item1;
            itemsArray[1] = item2;
            itemsArray[2] = item3;
            itemsArray[3] = item4;

            try
            {
                return this.AsItemsListWith(itemsArray.AsSpan()[..4]);
            }
            finally
            {
                ArrayPool<JsonAny>.Shared.Return(itemsArray);
            }
        }

        private ImmutableList<JsonAny> AsItemsListWith<TItem>(TItem[] items)
            where TItem : struct, IJsonValue
        {
            JsonAny[] itemsArray = ArrayPool<JsonAny>.Shared.Rent(items.Length);
            for (int i = 0; i < items.Length; ++i)
            {
                itemsArray[i] = items[i].AsAny;
            }

            try
            {
                return this.AsItemsListWith(itemsArray.AsSpan()[..items.Length]);
            }
            finally
            {
                ArrayPool<JsonAny>.Shared.Return(itemsArray);
            }
        }

        private ImmutableList<JsonAny> AsItemsListWith<TItem>(IEnumerable<TItem> itemsToAdd)
            where TItem : struct, IJsonValue
        {
            if (this.items is ImmutableList<JsonAny> items)
            {
                return items
                    .AddRange(itemsToAdd.Select(s => s.AsAny));
            }

            if (this.jsonElement.ValueKind == JsonValueKind.Array)
            {
                ImmutableList<JsonAny>.Builder builder = this.CreateListFromJsonElement();
                builder.AddRange(itemsToAdd.Select(s => s.AsAny));
                return builder.ToImmutable();
            }

            return ImmutableList<JsonAny>.Empty;
        }

        private ImmutableList<JsonAny> AsItemsListWith(ReadOnlySpan<JsonAny> itemsArray)
        {
            if (this.items is ImmutableList<JsonAny> items)
            {
                var builder = items.ToBuilder();
                foreach (JsonAny item in itemsArray)
                {
                    builder.Add(item);
                }

                return builder.ToImmutable();
            }

            if (this.jsonElement.ValueKind == JsonValueKind.Array)
            {
                ImmutableList<JsonAny>.Builder builder = this.CreateListFromJsonElement();
                foreach (JsonAny item in itemsArray)
                {
                    builder.Add(item);
                }

                return builder.ToImmutable();
            }

            return ImmutableList<JsonAny>.Empty;
        }

        private ImmutableList<JsonAny>.Builder CreateListFromJsonElement()
        {
            ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
            foreach (JsonElement existingItem in this.jsonElement.EnumerateArray())
            {
                builder.Add(new JsonAny(existingItem));
            }

            return builder;
        }

        private int GetHashCodeCore()
        {
            HashCode hash = default;

            foreach (JsonAny item in this.EnumerateArray())
            {
                hash.Add(item);
            }

            return hash.ToHashCode();
        }
    }
}
