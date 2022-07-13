// <copyright file="JsonAny.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;
    using System.Buffers;
    using System.Collections.Immutable;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// A JSON Value.
    /// </summary>
    public readonly struct JsonAny : IJsonObject<JsonAny>, IJsonArray<JsonAny>, IEquatable<JsonAny>
    {
        /// <summary>
        /// Gets the hash code for an undefined object.
        /// </summary>
        public const int UndefinedHashCode = 7260706; // This is the hashcode of the Guid 3b0d8364-8163-4963-8e72-baf49c4bdb25

        private readonly JsonElement jsonElementBacking;
        private readonly ImmutableDictionary<string, JsonAny>? objectBacking;
        private readonly ImmutableList<JsonAny>? arrayBacking;
        private readonly double? numberBacking;
        private readonly string? stringBacking;
        private readonly bool? booleanBacking;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonAny"/> struct.
        /// </summary>
        /// <param name="value">The backing <see cref="JsonElement"/>.</param>
        public JsonAny(JsonElement value)
        {
            this.jsonElementBacking = value;
            this.objectBacking = default;
            this.arrayBacking = default;
            this.numberBacking = default;
            this.stringBacking = default;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonAny"/> struct.
        /// </summary>
        /// <param name="value">A property dictionary.</param>
        public JsonAny(ImmutableDictionary<string, JsonAny> value)
        {
            this.jsonElementBacking = default;
            this.objectBacking = value;
            this.arrayBacking = default;
            this.numberBacking = default;
            this.stringBacking = default;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonAny"/> struct.
        /// </summary>
        /// <param name="value">An array list.</param>
        public JsonAny(ImmutableList<JsonAny> value)
        {
            this.jsonElementBacking = default;
            this.objectBacking = default;
            this.arrayBacking = value;
            this.numberBacking = default;
            this.stringBacking = default;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonAny"/> struct.
        /// </summary>
        /// <param name="value">A number value.</param>
        public JsonAny(double value)
        {
            this.jsonElementBacking = default;
            this.objectBacking = default;
            this.arrayBacking = default;
            this.numberBacking = value;
            this.stringBacking = default;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonAny"/> struct.
        /// </summary>
        /// <param name="value">A number value.</param>
        public JsonAny(int value)
        {
            this.jsonElementBacking = default;
            this.objectBacking = default;
            this.arrayBacking = default;
            this.numberBacking = value;
            this.stringBacking = default;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonAny"/> struct.
        /// </summary>
        /// <param name="value">A number value.</param>
        public JsonAny(float value)
        {
            this.jsonElementBacking = default;
            this.objectBacking = default;
            this.arrayBacking = default;
            this.numberBacking = value;
            this.stringBacking = default;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonAny"/> struct.
        /// </summary>
        /// <param name="value">A number value.</param>
        public JsonAny(long value)
        {
            this.jsonElementBacking = default;
            this.objectBacking = default;
            this.arrayBacking = default;
            this.numberBacking = value;
            this.stringBacking = default;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonAny"/> struct.
        /// </summary>
        /// <param name="value">A string value.</param>
        public JsonAny(string value)
        {
            this.jsonElementBacking = default;
            this.objectBacking = default;
            this.arrayBacking = default;
            this.numberBacking = default;
            this.stringBacking = value;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonAny"/> struct.
        /// </summary>
        /// <param name="value">A string value.</param>
        public JsonAny(ReadOnlySpan<char> value)
        {
            this.jsonElementBacking = default;
            this.objectBacking = default;
            this.arrayBacking = default;
            this.numberBacking = default;
            this.stringBacking = value.ToString();
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonAny"/> struct.
        /// </summary>
        /// <param name="value">A string value.</param>
        public JsonAny(ReadOnlySpan<byte> value)
        {
            this.jsonElementBacking = default;
            this.objectBacking = default;
            this.arrayBacking = default;
            this.numberBacking = default;
            this.stringBacking = System.Text.Encoding.UTF8.GetString(value);
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonAny"/> struct.
        /// </summary>
        /// <param name="jsonObject">The <see cref="JsonObject"/> from which to construct the value.</param>
        public JsonAny(JsonObject jsonObject)
        {
            if (jsonObject.HasJsonElement)
            {
                this.jsonElementBacking = jsonObject.AsJsonElement;
                this.objectBacking = default;
            }
            else
            {
                this.jsonElementBacking = default;
                this.objectBacking = jsonObject.AsPropertyDictionary;
            }

            this.arrayBacking = default;
            this.numberBacking = default;
            this.stringBacking = default;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonAny"/> struct.
        /// </summary>
        /// <param name="jsonArray">The <see cref="JsonArray"/> from which to construct the value.</param>
        public JsonAny(JsonArray jsonArray)
        {
            if (jsonArray.HasJsonElement)
            {
                this.jsonElementBacking = jsonArray.AsJsonElement;
                this.arrayBacking = default;
            }
            else
            {
                this.jsonElementBacking = default;
                this.arrayBacking = jsonArray.AsItemsList;
            }

            this.objectBacking = default;
            this.numberBacking = default;
            this.stringBacking = default;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonAny"/> struct.
        /// </summary>
        /// <param name="jsonNumber">The <see cref="JsonNumber"/> from which to construct the value.</param>
        public JsonAny(JsonNumber jsonNumber)
        {
            if (jsonNumber.HasJsonElement)
            {
                this.jsonElementBacking = jsonNumber.AsJsonElement;
                this.numberBacking = default;
            }
            else
            {
                this.jsonElementBacking = default;
                this.numberBacking = jsonNumber.GetDouble();
            }

            this.arrayBacking = default;
            this.objectBacking = default;
            this.stringBacking = default;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonAny"/> struct.
        /// </summary>
        /// <param name="jsonString">The <see cref="JsonString"/> from which to construct the value.</param>
        public JsonAny(JsonString jsonString)
        {
            if (jsonString.HasJsonElement)
            {
                this.jsonElementBacking = jsonString.AsJsonElement;
                this.stringBacking = default;
            }
            else
            {
                this.jsonElementBacking = default;
                this.stringBacking = jsonString;
            }

            this.numberBacking = default;
            this.arrayBacking = default;
            this.objectBacking = default;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonAny"/> struct.
        /// </summary>
        /// <param name="jsonBoolean">The <see cref="JsonBoolean"/> from which to construct the value.</param>
        public JsonAny(JsonBoolean jsonBoolean)
        {
            if (jsonBoolean.HasJsonElement)
            {
                this.jsonElementBacking = jsonBoolean.AsJsonElement;
                this.booleanBacking = default;
            }
            else
            {
                this.jsonElementBacking = default;
                this.booleanBacking = jsonBoolean.GetBoolean();
            }

            this.numberBacking = default;
            this.arrayBacking = default;
            this.objectBacking = default;
            this.stringBacking = default;
        }

        /// <inheritdoc/>
        public int Length
        {
            get
            {
                if (this.arrayBacking is ImmutableList<JsonAny> items)
                {
                    return items.Count;
                }

                return this.jsonElementBacking.GetArrayLength();
            }
        }

#pragma warning disable SA1648 // inheritdoc should be used with inheriting class
        /// <inheritdoc/>
        public JsonAny this[int index]
#pragma warning restore SA1648 // inheritdoc should be used with inheriting class
        {
            get
            {
                if (this.arrayBacking is ImmutableList<JsonAny> items)
                {
                    if (index >= items.Count)
                    {
                        throw new IndexOutOfRangeException(nameof(index));
                    }

                    return items[index];
                }

                return new JsonAny(this.jsonElementBacking[index]);
            }
        }

        /// <summary>
        /// Gets a value indicating whether this is backed by a JSON element.
        /// </summary>
        public bool HasJsonElement => this.jsonElementBacking.ValueKind != JsonValueKind.Undefined || (this.objectBacking is null && this.arrayBacking is null && this.numberBacking is null && this.stringBacking is null && this.booleanBacking is null);

        /// <summary>
        /// Gets the value as a JsonElement.
        /// </summary>
        public JsonElement AsJsonElement
        {
            get
            {
                if (this.objectBacking is ImmutableDictionary<string, JsonAny> objectBacking)
                {
                    return JsonObject.PropertiesToJsonElement(objectBacking);
                }

                if (this.arrayBacking is ImmutableList<JsonAny> arrayBacking)
                {
                    return JsonArray.ItemsToJsonElement(arrayBacking);
                }

                if (this.numberBacking is double numberBacking)
                {
                    return JsonNumber.NumberToJsonElement(numberBacking);
                }

                if (this.stringBacking is string stringBacking)
                {
                    return JsonString.StringToJsonElement(stringBacking);
                }

                if (this.booleanBacking is bool booleanBacking)
                {
                    return JsonBoolean.BoolToJsonElement(booleanBacking);
                }

                return this.jsonElementBacking;
            }
        }

        /// <inheritdoc/>
        public JsonValueKind ValueKind
        {
            get
            {
                if (this.objectBacking is not null)
                {
                    return JsonValueKind.Object;
                }

                if (this.arrayBacking is not null)
                {
                    return JsonValueKind.Array;
                }

                if (this.numberBacking is double)
                {
                    return JsonValueKind.Number;
                }

                if (this.stringBacking is not null)
                {
                    return JsonValueKind.String;
                }

                if (this.booleanBacking is bool booleanBacking)
                {
                    return booleanBacking ? JsonValueKind.True : JsonValueKind.False;
                }

                return this.jsonElementBacking.ValueKind;
            }
        }

        /// <inheritdoc/>
        public JsonAny AsAny
        {
            get
            {
                return this;
            }
        }

        /// <summary>
        /// Gets the value as a <see cref="JsonObject"/>.
        /// </summary>
        public JsonObject AsObject
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (this.objectBacking is ImmutableDictionary<string, JsonAny> objectBacking)
                {
                    return new JsonObject(objectBacking);
                }
                else
                {
                    return new JsonObject(this.jsonElementBacking);
                }
            }
        }

        /// <summary>
        /// Gets the value as a <see cref="JsonArray"/>.
        /// </summary>
        public JsonArray AsArray
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (this.arrayBacking is ImmutableList<JsonAny> arrayBacking)
                {
                    return new JsonArray(arrayBacking);
                }
                else
                {
                    return new JsonArray(this.jsonElementBacking);
                }
            }
        }

        /// <summary>
        /// Gets the value as a <see cref="JsonNumber"/>.
        /// </summary>
        public JsonNumber AsNumber
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (this.numberBacking is double numberBacking)
                {
                    return new JsonNumber(numberBacking);
                }
                else
                {
                    return new JsonNumber(this.jsonElementBacking);
                }
            }
        }

        /// <summary>
        /// Gets the value as a <see cref="JsonString"/>.
        /// </summary>
        public JsonString AsString
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (this.stringBacking is string stringBacking)
                {
                    return new JsonString(stringBacking);
                }
                else
                {
                    return new JsonString(this.jsonElementBacking);
                }
            }
        }

        /// <summary>
        /// Gets the instance as a dotnet backed value.
        /// </summary>
        public JsonAny AsDotnetBackedValue
        {
            get
            {
                if (this.HasJsonElement)
                {
                    JsonValueKind valueKind = this.ValueKind;

                    return valueKind switch
                    {
                        JsonValueKind.Object => new JsonAny(this.AsObject.AsPropertyDictionary),
                        JsonValueKind.Array => new JsonAny(this.AsArray.AsItemsList),
                        JsonValueKind.Number => new JsonAny(this.AsNumber.GetDouble()),
                        JsonValueKind.String => new JsonAny(this.AsString.GetString()),
                        JsonValueKind.True or JsonValueKind.False => new JsonAny(this.AsBoolean.GetBoolean()),
                        JsonValueKind.Null => JsonNull.Instance.AsAny,
                        _ => this,
                    };
                }

                return this;
            }
        }

        /// <summary>
        /// Gets the value as a <see cref="JsonBoolean"/>.
        /// </summary>
        public JsonBoolean AsBoolean
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (this.booleanBacking is bool booleanBacking)
                {
                    return new JsonBoolean(booleanBacking);
                }
                else
                {
                    return new JsonBoolean(this.jsonElementBacking);
                }
            }
        }

        /// <summary>
        /// Gets the value as a <see cref="JsonNull"/>.
        /// </summary>
#pragma warning disable CA1822 // Mark members as static
        public JsonNull AsNull
#pragma warning restore CA1822 // Mark members as static
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return default;
            }
        }

        /// <summary>
        /// Gets the instance as a list of <see cref="JsonAny"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is intended for operations which mutate the underlying list.
        /// For read-only scenarios, it is generally more efficient to use
        /// the <see cref="EnumerateArray()"/> method.
        /// </para>
        /// </remarks>
        public ImmutableList<JsonAny> AsItemsList
        {
            get
            {
                if (this.arrayBacking is ImmutableList<JsonAny> items)
                {
                    return items;
                }

                if (this.jsonElementBacking.ValueKind == JsonValueKind.Array)
                {
                    ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
                    foreach (JsonElement item in this.jsonElementBacking.EnumerateArray())
                    {
                        builder.Add(new JsonAny(item));
                    }

                    return builder.ToImmutable();
                }

                return ImmutableList<JsonAny>.Empty;
            }
        }

        /// <summary>
        /// Implicit conversion to an <see cref="ImmutableList{T}"/> of <see cref="JsonAny"/>.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator ImmutableList<JsonAny>(JsonAny value)
        {
            return value.AsArray.AsItemsList;
        }

        /// <summary>
        /// Implicit conversion from an <see cref="ImmutableList{T}"/> of <see cref="JsonAny"/>.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(ImmutableList<JsonAny> value)
        {
            return new JsonAny(value);
        }

        /// <summary>
        /// Implicit conversion to a property dictionary.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator ImmutableDictionary<string, JsonAny>(JsonAny value)
        {
            return value.AsObject.AsPropertyDictionary;
        }

        /// <summary>
        /// Implicit conversion from a property dictionary.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(ImmutableDictionary<string, JsonAny> value)
        {
            return new JsonObject(value);
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(string value)
        {
            return new JsonAny(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator string(JsonAny value)
        {
            return value.AsString.GetString();
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(ReadOnlySpan<char> value)
        {
            return new JsonAny(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<char>(JsonAny value)
        {
            return value.AsString.AsSpan();
        }

        /// <summary>
        /// Conversion from utf8 bytes.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(ReadOnlySpan<byte> value)
        {
            return new JsonAny(value);
        }

        /// <summary>
        /// Conversion to utf8 bytes.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<byte>(JsonAny value)
        {
            return value.AsString;
        }

        /// <summary>
        /// Conversion from double.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(double value)
        {
            return new JsonAny(value);
        }

        /// <summary>
        /// Conversion to double.
        /// </summary>
        /// <param name="number">The number from which to convert.</param>
        public static implicit operator double(JsonAny number)
        {
            return number.AsNumber.GetDouble();
        }

        /// <summary>
        /// Conversion from float.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(float value)
        {
            return new JsonAny(value);
        }

        /// <summary>
        /// Conversion to float.
        /// </summary>
        /// <param name="number">The number from which to convert.</param>
        public static implicit operator float(JsonAny number)
        {
            return number.AsNumber.GetSingle();
        }

        /// <summary>
        /// Conversion from long.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(long value)
        {
            return new JsonAny(value);
        }

        /// <summary>
        /// Conversion to long.
        /// </summary>
        /// <param name="number">The number from which to convert.</param>
        public static implicit operator long(JsonAny number)
        {
            return number.AsNumber.GetInt64();
        }

        /// <summary>
        /// Conversion from int.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(int value)
        {
            return new JsonAny(value);
        }

        /// <summary>
        /// Conversion to int.
        /// </summary>
        /// <param name="number">The number from which to convert.</param>
        public static implicit operator int(JsonAny number)
        {
            return number.AsNumber.GetInt32();
        }

        /// <summary>
        /// Conversion from bool.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(bool value)
        {
            return new JsonAny(value);
        }

        /// <summary>
        /// Conversion to bool.
        /// </summary>
        /// <param name="boolean">The boolean from which to convert.</param>
        public static implicit operator bool(JsonAny boolean)
        {
            return boolean.AsBoolean.GetBoolean();
        }

        /// <summary>
        /// Standard equality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are equal.</returns>
        public static bool operator ==(JsonAny lhs, JsonAny rhs)
        {
            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Standard inequality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are not equal.</returns>
        public static bool operator !=(JsonAny lhs, JsonAny rhs)
        {
            return !lhs.Equals(rhs);
        }

        /// <summary>
        /// Create a <see cref="JsonAny"/> instance from an arbitrary object.
        /// </summary>
        /// <typeparam name="T">The type of the object from which to create the instance.</typeparam>
        /// <param name="instance">The object from which to create the instance.</param>
        /// <param name="options">The (optional) <see cref="JsonWriterOptions"/>.</param>
        /// <returns>A <see cref="JsonAny"/> derived from serializing the object.</returns>
        public static JsonAny From<T>(T instance, JsonWriterOptions options = default)
        {
            var abw = new ArrayBufferWriter<byte>();
            using var writer = new Utf8JsonWriter(abw, options);
            JsonSerializer.Serialize(writer, instance, typeof(T));
            writer.Flush();
            return Parse(abw.WrittenMemory);
        }

        /// <summary>
        /// Parses a JSON string into a JsonAny.
        /// </summary>
        /// <param name="json">The json string to parse.</param>
        /// <param name="options">The (optional) JsonDocumentOptions.</param>
        /// <returns>A <see cref="JsonAny"/> instance built from the JSON string.</returns>
        public static JsonAny Parse(string json, JsonDocumentOptions options = default)
        {
            using var jsonDocument = JsonDocument.Parse(json, options);
            return new JsonAny(jsonDocument.RootElement.Clone());
        }

        /// <summary>
        /// Parses a naked value from a URI string.
        /// </summary>
        /// <param name="value">The value to parse.</param>
        /// <param name="options">The (optional) JsonDocumentOptions.</param>
        /// <returns>A <see cref="JsonAny"/> instance representing the value.</returns>
        public static JsonAny ParseUriValue(string value, JsonDocumentOptions options = default)
        {
            try
            {
                // Try to parse the naked value from the URI
                return Parse(value, options);
            }
            catch (Exception)
            {
                // In the event of being unable to parse, treat it as a string.
                return value;
            }
        }

        /// <summary>
        /// Parses a naked value from a URI UTF8-encoded byte array.
        /// </summary>
        /// <param name="value">The value to parse.</param>
        /// <param name="options">The (optional) JsonDocumentOptions.</param>
        /// <returns>A <see cref="JsonAny"/> instance representing the value.</returns>
        public static JsonAny ParseUriValue(ReadOnlyMemory<byte> value, JsonDocumentOptions options = default)
        {
            try
            {
                // Try to parse the naked value from the URI
                return Parse(value, options);
            }
            catch (Exception)
            {
                // In the event of being unable to parse, treat it as a string.
                return new JsonAny(value.Span);
            }
        }

        /// <summary>
        /// Parses a naked value from a URI string.
        /// </summary>
        /// <param name="value">The value to parse.</param>
        /// <param name="options">The (optional) JsonDocumentOptions.</param>
        /// <returns>A <see cref="JsonAny"/> instance representing the value.</returns>
        public static JsonAny ParseUriValue(ReadOnlyMemory<char> value, JsonDocumentOptions options = default)
        {
            try
            {
                // Try to parse the naked value from the URI
                return Parse(value, options);
            }
            catch (Exception)
            {
                // In the event of being unable to parse, treat it as a string.
                return new JsonAny(value.Span);
            }
        }

        /// <summary>
        /// Parses a JSON string into a JsonAny.
        /// </summary>
        /// <param name="utf8Json">The json string to parse.</param>
        /// <param name="options">The (optional) JsonDocumentOptions.</param>
        /// <returns>A <see cref="JsonAny"/> instance built from the JSON string.</returns>
        public static JsonAny Parse(Stream utf8Json, JsonDocumentOptions options = default)
        {
            using var jsonDocument = JsonDocument.Parse(utf8Json, options);
            return new JsonAny(jsonDocument.RootElement.Clone());
        }

        /// <summary>
        /// Parses a JSON string into a JsonAny.
        /// </summary>
        /// <param name="utf8Json">The json string to parse.</param>
        /// <param name="options">The (optional) JsonDocumentOptions.</param>
        /// <returns>A <see cref="JsonAny"/> instance built from the JSON string.</returns>
        public static JsonAny Parse(ReadOnlyMemory<byte> utf8Json, JsonDocumentOptions options = default)
        {
            using var jsonDocument = JsonDocument.Parse(utf8Json, options);
            return new JsonAny(jsonDocument.RootElement.Clone());
        }

        /// <summary>
        /// Parses a JSON string into a JsonAny.
        /// </summary>
        /// <param name="json">The json string to parse.</param>
        /// <param name="options">The (optional) JsonDocumentOptions.</param>
        /// <returns>A <see cref="JsonAny"/> instance built from the JSON string.</returns>
        public static JsonAny Parse(ReadOnlyMemory<char> json, JsonDocumentOptions options = default)
        {
            using var jsonDocument = JsonDocument.Parse(json, options);
            return new JsonAny(jsonDocument.RootElement.Clone());
        }

        /// <summary>
        /// Parses a JSON string into a JsonAny.
        /// </summary>
        /// <param name="utf8Json">The json string to parse.</param>
        /// <param name="options">The (optional) JsonDocumentOptions.</param>
        /// <returns>A <see cref="JsonAny"/> instance built from the JSON string.</returns>
        public static JsonAny Parse(ReadOnlySequence<byte> utf8Json, JsonDocumentOptions options = default)
        {
            using var jsonDocument = JsonDocument.Parse(utf8Json, options);
            return new JsonAny(jsonDocument.RootElement.Clone());
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
                JsonValueKind.Object => this.AsObject.GetHashCode(),
                JsonValueKind.Array => this.AsArray.GetHashCode(),
                JsonValueKind.Number => this.AsNumber.GetHashCode(),
                JsonValueKind.String => this.AsString.GetHashCode(),
                JsonValueKind.True or JsonValueKind.False => this.AsBoolean.GetHashCode(),
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
        /// Writes the object to the <see cref="Utf8JsonWriter"/>.
        /// </summary>
        /// <param name="writer">The writer to which to write the object.</param>
        public void WriteTo(Utf8JsonWriter writer)
        {
            if (this.objectBacking is ImmutableDictionary<string, JsonAny> objectBacking)
            {
                JsonObject.WriteProperties(objectBacking, writer);
                return;
            }

            if (this.arrayBacking is ImmutableList<JsonAny> arrayBacking)
            {
                JsonArray.WriteItems(arrayBacking, writer);
                return;
            }

            if (this.numberBacking is double numberBacking)
            {
                writer.WriteNumberValue(numberBacking);
                return;
            }

            if (this.stringBacking is string stringBacking)
            {
                writer.WriteStringValue(stringBacking);
                return;
            }

            if (this.booleanBacking is bool booleanBacking)
            {
                writer.WriteBooleanValue(booleanBacking);
                return;
            }

            if (this.jsonElementBacking.ValueKind != JsonValueKind.Undefined)
            {
                this.jsonElementBacking.WriteTo(writer);
                return;
            }

            writer.WriteNullValue();
        }

        /// <inheritdoc/>
        public JsonObjectEnumerator EnumerateObject()
        {
            if (this.objectBacking is ImmutableDictionary<string, JsonAny> properties)
            {
                return new JsonObjectEnumerator(properties);
            }

            if (this.jsonElementBacking.ValueKind == JsonValueKind.Object)
            {
                return new JsonObjectEnumerator(this.jsonElementBacking);
            }

            return default;
        }

        /// <inheritdoc/>
        public JsonArrayEnumerator EnumerateArray()
        {
            if (this.arrayBacking is ImmutableList<JsonAny> items)
            {
                return new JsonArrayEnumerator(items);
            }

            if (this.jsonElementBacking.ValueKind == JsonValueKind.Array)
            {
                return new JsonArrayEnumerator(this.jsonElementBacking);
            }

            return default;
        }

        /// <inheritdoc/>
        public bool TryGetProperty(string name, out JsonAny value)
        {
            if (this.objectBacking is ImmutableDictionary<string, JsonAny> properties)
            {
                return properties.TryGetValue(name, out value);
            }

            if (this.jsonElementBacking.ValueKind == JsonValueKind.Object)
            {
                if (this.jsonElementBacking.TryGetProperty(name, out JsonElement jsonElement))
                {
                    value = new JsonAny(jsonElement);
                    return true;
                }
            }

            value = default;
            return false;
        }

        /// <inheritdoc/>
        public bool TryGetProperty(ReadOnlySpan<char> name, out JsonAny value)
        {
            if (this.objectBacking is ImmutableDictionary<string, JsonAny> properties)
            {
                return properties.TryGetValue(name.ToString(), out value);
            }

            if (this.jsonElementBacking.ValueKind == JsonValueKind.Object)
            {
                if (this.jsonElementBacking.TryGetProperty(name, out JsonElement jsonElement))
                {
                    value = new JsonAny(jsonElement);
                    return true;
                }
            }

            value = default;
            return false;
        }

        /// <inheritdoc/>
        public bool TryGetProperty(ReadOnlySpan<byte> utf8name, out JsonAny value)
        {
            if (this.objectBacking is ImmutableDictionary<string, JsonAny> properties)
            {
                return properties.TryGetValue(Encoding.UTF8.GetString(utf8name), out value);
            }

            if (this.jsonElementBacking.ValueKind == JsonValueKind.Object)
            {
                if (this.jsonElementBacking.TryGetProperty(utf8name, out JsonElement jsonElement))
                {
                    value = new JsonAny(jsonElement);
                    return true;
                }
            }

            value = default;
            return false;
        }

        /// <inheritdoc/>
        public bool HasProperty(string name)
        {
            if (this.objectBacking is ImmutableDictionary<string, JsonAny> properties)
            {
                return properties.TryGetValue(name, out _);
            }

            if (this.jsonElementBacking.ValueKind == JsonValueKind.Object)
            {
                return this.jsonElementBacking.TryGetProperty(name, out JsonElement _);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool HasProperty(ReadOnlySpan<char> name)
        {
            if (this.objectBacking is ImmutableDictionary<string, JsonAny> properties)
            {
                return properties.TryGetValue(name.ToString(), out _);
            }

            if (this.jsonElementBacking.ValueKind == JsonValueKind.Object)
            {
                return this.jsonElementBacking.TryGetProperty(name, out JsonElement _);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool HasProperty(ReadOnlySpan<byte> utf8name)
        {
            if (this.objectBacking is ImmutableDictionary<string, JsonAny> properties)
            {
                return properties.TryGetValue(Encoding.UTF8.GetString(utf8name), out _);
            }

            if (this.jsonElementBacking.ValueKind == JsonValueKind.Object)
            {
                return this.jsonElementBacking.TryGetProperty(utf8name, out JsonElement _);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals<T>(T other)
            where T : struct, IJsonValue
        {
            return this.Equals(other.AsAny);
        }

        /// <inheritdoc/>
        public bool Equals(JsonAny other)
        {
            JsonValueKind valueKind = this.ValueKind;

            if (other.ValueKind != valueKind)
            {
                return false;
            }

            return valueKind switch
            {
                JsonValueKind.Object => this.AsObject.Equals(other.AsObject),
                JsonValueKind.Array => this.AsArray.Equals(other.AsArray),
                JsonValueKind.Number => this.AsNumber.Equals(other.AsNumber),
                JsonValueKind.String => this.AsString.Equals(other.AsString),
                JsonValueKind.True or JsonValueKind.False => this.AsBoolean.Equals(other.AsBoolean),
                JsonValueKind.Null => true,
                _ => true,
            };
        }

        /// <summary>
        /// Set the property.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <returns>The value with the property set.</returns>
        public JsonAny SetProperty(string name, JsonAny value)
        {
            if (this.ValueKind == JsonValueKind.Object || this.ValueKind == JsonValueKind.Undefined)
            {
                return new JsonAny(this.AsPropertyDictionaryWith(name, value));
            }

            return this;
        }

        /// <summary>
        /// Set the property.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <returns>The value with the property set.</returns>
        public JsonAny SetProperty(ReadOnlySpan<char> name, JsonAny value)
        {
            if (this.ValueKind == JsonValueKind.Object || this.ValueKind == JsonValueKind.Undefined)
            {
                return new JsonAny(this.AsPropertyDictionaryWith(name.ToString(), value));
            }

            return this;
        }

        /// <summary>
        /// Set the property.
        /// </summary>
        /// <param name="utf8Name">The name of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <returns>The value with the property set.</returns>
        public JsonAny SetProperty(ReadOnlySpan<byte> utf8Name, JsonAny value)
        {
            if (this.ValueKind == JsonValueKind.Object || this.ValueKind == JsonValueKind.Undefined)
            {
                return new JsonAny(this.AsPropertyDictionaryWith(Encoding.UTF8.GetString(utf8Name), value));
            }

            return this;
        }

        /// <inheritdoc/>
        public JsonAny SetProperty<TValue>(string name, TValue value)
            where TValue : struct, IJsonValue
        {
            if (this.ValueKind == JsonValueKind.Object || this.ValueKind == JsonValueKind.Undefined)
            {
                return new JsonAny(this.AsPropertyDictionaryWith(name, value.AsAny));
            }

            return this;
        }

        /// <inheritdoc/>
        public JsonAny SetProperty<TValue>(ReadOnlySpan<char> name, TValue value)
            where TValue : struct, IJsonValue
        {
            return this.SetProperty(name.ToString(), value);
        }

        /// <inheritdoc/>
        public JsonAny SetProperty<TValue>(ReadOnlySpan<byte> utf8Name, TValue value)
            where TValue : struct, IJsonValue
        {
            return this.SetProperty(Encoding.UTF8.GetString(utf8Name), value);
        }

        /// <inheritdoc/>
        public JsonAny RemoveProperty(string name)
        {
            return new JsonAny(this.AsPropertyDictionaryWithout(name));
        }

        /// <inheritdoc/>
        public JsonAny RemoveProperty(ReadOnlySpan<char> name)
        {
            return new JsonAny(this.AsPropertyDictionaryWithout(name.ToString()));
        }

        /// <inheritdoc/>
        public JsonAny RemoveProperty(ReadOnlySpan<byte> utf8Name)
        {
            return this.RemoveProperty(Encoding.UTF8.GetString(utf8Name));
        }

        /// <summary>
        /// Add an item.
        /// </summary>
        /// <param name="item">The item to add.</param>
        /// <returns>The value with the item added.</returns>
        public JsonAny Add(JsonAny item)
        {
            return this.AsItemsListWith(item);
        }

        /// <inheritdoc/>
        public JsonAny Add<TItem>(TItem item)
            where TItem : struct, IJsonValue
        {
            return this.AsItemsListWith(item.AsAny);
        }

        /// <inheritdoc/>
        public JsonAny Add<TItem1, TItem2>(TItem1 item1, TItem2 item2)
            where TItem1 : struct, IJsonValue
            where TItem2 : struct, IJsonValue
        {
            return this.AsItemsListWith(item1.AsAny, item2.AsAny);
        }

        /// <inheritdoc/>
        public JsonAny Add<TItem1, TItem2, TItem3>(TItem1 item1, TItem2 item2, TItem3 item3)
            where TItem1 : struct, IJsonValue
            where TItem2 : struct, IJsonValue
            where TItem3 : struct, IJsonValue
        {
            return this.AsItemsListWith(item1.AsAny, item2.AsAny, item3.AsAny);
        }

        /// <inheritdoc/>
        public JsonAny Add<TItem1, TItem2, TItem3, TItem4>(TItem1 item1, TItem2 item2, TItem3 item3, TItem4 item4)
            where TItem1 : struct, IJsonValue
            where TItem2 : struct, IJsonValue
            where TItem3 : struct, IJsonValue
            where TItem4 : struct, IJsonValue
        {
            return this.AsItemsListWith(item1.AsAny, item2.AsAny, item3.AsAny, item4.AsAny);
        }

        /// <inheritdoc/>
        public JsonAny Add<TItem>(params TItem[] items)
            where TItem : struct, IJsonValue
        {
            return this.AsItemsListWith(items);
        }

        /// <inheritdoc/>
        public JsonAny AddRange<TItem>(IEnumerable<TItem> items)
            where TItem : struct, IJsonValue
        {
            return this.AsItemsListWith(items);
        }

        /// <summary>
        /// Insert an item into the array.
        /// </summary>
        /// <param name="index">The index at which to insert the item.</param>
        /// <param name="item">The item to insert.</param>
        /// <returns>The value with the item inserted.</returns>
        public JsonAny Insert(int index, JsonAny item)
        {
            return this.AsItemsListInserting(index, item);
        }

        /// <inheritdoc/>
        public JsonAny Insert<TItem>(int index, TItem item)
            where TItem : struct, IJsonValue
        {
            return this.AsItemsListInserting(index, item.AsAny);
        }

        /// <inheritdoc/>
        public JsonAny Replace<TItem>(TItem oldValue, TItem newValue)
            where TItem : struct, IJsonValue
        {
            return this.AsItemsListReplacing(oldValue.AsAny, newValue.AsAny);
        }

        /// <inheritdoc/>
        public JsonAny SetItem<TItem>(int index, TItem value)
            where TItem : struct, IJsonValue
        {
            return this.AsItemsListSetting(index, value.AsAny);
        }

        /// <inheritdoc/>
        public JsonAny RemoveAt(int index)
        {
            return this.AsItemsListRemovingAt(index);
        }

        /// <inheritdoc/>
        public JsonAny RemoveRange(int index, int count)
        {
            return this.AsItemsListRemovingRange(index, count);
        }

        /// <inheritdoc/>
        public T As<T>()
            where T : struct, IJsonValue
        {
            return this.As<JsonAny, T>();
        }

        /// <inheritdoc/>
        public ValidationContext Validate(in ValidationContext validationContext, ValidationLevel level = ValidationLevel.Flag)
        {
            return validationContext;
        }

        /// <summary>
        /// Gets the object as a property dictionary.
        /// </summary>
        public ImmutableDictionary<string, JsonAny> AsPropertyDictionary
        {
            get
            {
                if (this.objectBacking is ImmutableDictionary<string, JsonAny> properties)
                {
                    return properties;
                }

                if (this.jsonElementBacking.ValueKind == JsonValueKind.Object)
                {
                    ImmutableDictionary<string, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<string, JsonAny>();
                    foreach (JsonProperty property in this.jsonElementBacking.EnumerateObject())
                    {
                        builder.Add(property.Name, new JsonAny(property.Value));
                    }

                    return builder.ToImmutable();
                }

                return ImmutableDictionary<string, JsonAny>.Empty;
            }
        }

        /// <summary>
        /// Gets the object as a property dictionary, removing a property if presents.
        /// </summary>
        /// <param name="name">The name of the property to remove.</param>
        /// <returns>
        /// An immutable dictionary of properties without the named property.
        /// </returns>
        public ImmutableDictionary<string, JsonAny> AsPropertyDictionaryWithout(string name)
        {
            if (this.objectBacking is ImmutableDictionary<string, JsonAny> properties)
            {
                return properties.Remove(name);
            }

            if (this.jsonElementBacking.ValueKind == JsonValueKind.Object)
            {
                ImmutableDictionary<string, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<string, JsonAny>();

                foreach (JsonProperty property in this.jsonElementBacking.EnumerateObject())
                {
                    string propertyName = property.Name;
                    if (propertyName.Equals(name, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    builder.Add(propertyName, new JsonAny(property.Value));
                }

                return builder.ToImmutable();
            }

            return ImmutableDictionary<string, JsonAny>.Empty;
        }

        /// <summary>
        /// Gets the object as a property dictionary, adding a single object.
        /// </summary>
        /// <param name="name">The name of the property to add.</param>
        /// <param name="value">The value of the property to add.</param>
        /// <returns>
        /// An immutable dictionary of properties with the named property set to the new value.
        /// </returns>
        public ImmutableDictionary<string, JsonAny> AsPropertyDictionaryWith(string name, JsonAny value)
        {
            if (this.objectBacking is ImmutableDictionary<string, JsonAny> properties)
            {
                var builder = properties.ToBuilder();
                builder[name] = value;
                return builder.ToImmutable();
            }

            if (this.jsonElementBacking.ValueKind == JsonValueKind.Object)
            {
                ImmutableDictionary<string, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<string, JsonAny>();
                foreach (JsonProperty property in this.jsonElementBacking.EnumerateObject())
                {
                    builder.Add(property.Name, new JsonAny(property.Value));
                }

                builder[name] = value;

                return builder.ToImmutable();
            }

            if (this.jsonElementBacking.ValueKind == JsonValueKind.Undefined)
            {
                return ImmutableDictionary<string, JsonAny>.Empty.Add(name, value);
            }

            return ImmutableDictionary<string, JsonAny>.Empty;
        }

        private ImmutableList<JsonAny> AsItemsListRemovingAt(int index)
        {
            return this.AsItemsListRemovingRange(index, 1);
        }

        private ImmutableList<JsonAny> AsItemsListInserting(int index, JsonAny value)
        {
            if (this.arrayBacking is ImmutableList<JsonAny> items)
            {
                if (index > items.Count)
                {
                    throw new IndexOutOfRangeException(nameof(index));
                }

                return items.Insert(index, value);
            }

            if (this.jsonElementBacking.ValueKind == JsonValueKind.Array)
            {
                int arrayLength = this.jsonElementBacking.GetArrayLength();
                if (index > arrayLength)
                {
                    throw new IndexOutOfRangeException(nameof(index));
                }

                ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
                int current = 0;
                bool inserted = false;
                foreach (JsonElement existingItem in this.jsonElementBacking.EnumerateArray())
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
            if (this.arrayBacking is ImmutableList<JsonAny> items)
            {
                return items.Replace(oldValue, newValue);
            }

            if (this.jsonElementBacking.ValueKind == JsonValueKind.Array)
            {
                ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
                foreach (JsonElement existingItem in this.jsonElementBacking.EnumerateArray())
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
            if (this.arrayBacking is ImmutableList<JsonAny> items)
            {
                if (index >= items.Count)
                {
                    throw new IndexOutOfRangeException(nameof(index));
                }

                return items.SetItem(index, value);
            }

            if (this.jsonElementBacking.ValueKind == JsonValueKind.Array)
            {
                int arrayLength = this.jsonElementBacking.GetArrayLength();
                if (index >= arrayLength)
                {
                    throw new IndexOutOfRangeException(nameof(index));
                }

                ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
                int current = 0;
                foreach (JsonElement existingItem in this.jsonElementBacking.EnumerateArray())
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
            if (this.arrayBacking is ImmutableList<JsonAny> items)
            {
                if (index >= items.Count)
                {
                    throw new IndexOutOfRangeException(nameof(index));
                }

                if (index + count > items.Count)
                {
                    throw new IndexOutOfRangeException(nameof(count));
                }

                return items.RemoveRange(index, count);
            }

            if (this.jsonElementBacking.ValueKind == JsonValueKind.Array)
            {
                int arrayLength = this.jsonElementBacking.GetArrayLength();
                if (index >= arrayLength)
                {
                    throw new IndexOutOfRangeException(nameof(index));
                }

                if (index + count > arrayLength)
                {
                    throw new IndexOutOfRangeException(nameof(count));
                }

                ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
                int current = 0;
                int end = index + count;
                foreach (JsonElement existingItem in this.jsonElementBacking.EnumerateArray())
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
            if (this.arrayBacking is ImmutableList<JsonAny> items)
            {
                return items.Add(item);
            }

            if (this.jsonElementBacking.ValueKind == JsonValueKind.Array)
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
            if (this.arrayBacking is ImmutableList<JsonAny> items)
            {
                return items
                    .AddRange(itemsToAdd.Select(s => s.AsAny));
            }

            if (this.jsonElementBacking.ValueKind == JsonValueKind.Array)
            {
                ImmutableList<JsonAny>.Builder builder = this.CreateListFromJsonElement();
                builder.AddRange(itemsToAdd.Select(s => s.AsAny));
                return builder.ToImmutable();
            }

            return ImmutableList<JsonAny>.Empty;
        }

        private ImmutableList<JsonAny> AsItemsListWith(ReadOnlySpan<JsonAny> itemsArray)
        {
            if (this.arrayBacking is ImmutableList<JsonAny> items)
            {
                var builder = items.ToBuilder();
                foreach (JsonAny item in itemsArray)
                {
                    builder.Add(item);
                }

                return builder.ToImmutable();
            }

            if (this.jsonElementBacking.ValueKind == JsonValueKind.Array)
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
            foreach (JsonElement existingItem in this.jsonElementBacking.EnumerateArray())
            {
                builder.Add(new JsonAny(existingItem));
            }

            return builder;
        }
    }
}
