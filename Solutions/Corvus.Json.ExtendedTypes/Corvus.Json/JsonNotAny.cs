// <copyright file="JsonNotAny.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;
    using System.Collections.Immutable;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// A JSON Value.
    /// </summary>
    public readonly struct JsonNotAny : IJsonObject<JsonNotAny>, IJsonArray<JsonNotAny>, IEquatable<JsonNotAny>
    {
        private readonly JsonElement jsonElementBacking;
        private readonly ImmutableDictionary<string, JsonAny>? objectBacking;
        private readonly ImmutableList<JsonAny>? arrayBacking;
        private readonly double? numberBacking;
        private readonly string? stringBacking;
        private readonly bool? booleanBacking;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
        /// </summary>
        /// <param name="value">The backing <see cref="JsonElement"/>.</param>
        public JsonNotAny(JsonElement value)
        {
            this.jsonElementBacking = value;
            this.objectBacking = default;
            this.arrayBacking = default;
            this.numberBacking = default;
            this.stringBacking = default;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
        /// </summary>
        /// <param name="value">A property dictionary.</param>
        public JsonNotAny(ImmutableDictionary<string, JsonAny> value)
        {
            this.jsonElementBacking = default;
            this.objectBacking = value;
            this.arrayBacking = default;
            this.numberBacking = default;
            this.stringBacking = default;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
        /// </summary>
        /// <param name="value">An array list.</param>
        public JsonNotAny(ImmutableList<JsonAny> value)
        {
            this.jsonElementBacking = default;
            this.objectBacking = default;
            this.arrayBacking = value;
            this.numberBacking = default;
            this.stringBacking = default;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
        /// </summary>
        /// <param name="value">A number value.</param>
        public JsonNotAny(double value)
        {
            this.jsonElementBacking = default;
            this.objectBacking = default;
            this.arrayBacking = default;
            this.numberBacking = value;
            this.stringBacking = default;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
        /// </summary>
        /// <param name="value">A number value.</param>
        public JsonNotAny(int value)
        {
            this.jsonElementBacking = default;
            this.objectBacking = default;
            this.arrayBacking = default;
            this.numberBacking = value;
            this.stringBacking = default;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
        /// </summary>
        /// <param name="value">A number value.</param>
        public JsonNotAny(float value)
        {
            this.jsonElementBacking = default;
            this.objectBacking = default;
            this.arrayBacking = default;
            this.numberBacking = value;
            this.stringBacking = default;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
        /// </summary>
        /// <param name="value">A number value.</param>
        public JsonNotAny(long value)
        {
            this.jsonElementBacking = default;
            this.objectBacking = default;
            this.arrayBacking = default;
            this.numberBacking = value;
            this.stringBacking = default;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
        /// </summary>
        /// <param name="value">A string value.</param>
        public JsonNotAny(string value)
        {
            this.jsonElementBacking = default;
            this.objectBacking = default;
            this.arrayBacking = default;
            this.numberBacking = default;
            this.stringBacking = value;
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
        /// </summary>
        /// <param name="value">A string value.</param>
        public JsonNotAny(ReadOnlySpan<char> value)
        {
            this.jsonElementBacking = default;
            this.objectBacking = default;
            this.arrayBacking = default;
            this.numberBacking = default;
            this.stringBacking = value.ToString();
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
        /// </summary>
        /// <param name="value">A string value.</param>
        public JsonNotAny(ReadOnlySpan<byte> value)
        {
            this.jsonElementBacking = default;
            this.objectBacking = default;
            this.arrayBacking = default;
            this.numberBacking = default;
            this.stringBacking = Encoding.UTF8.GetString(value);
            this.booleanBacking = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
        /// </summary>
        /// <param name="jsonObject">The <see cref="JsonObject"/> from which to construct the value.</param>
        public JsonNotAny(JsonObject jsonObject)
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
        /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
        /// </summary>
        /// <param name="jsonArray">The <see cref="JsonArray"/> from which to construct the value.</param>
        public JsonNotAny(JsonArray jsonArray)
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
        /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
        /// </summary>
        /// <param name="jsonNumber">The <see cref="JsonNumber"/> from which to construct the value.</param>
        public JsonNotAny(JsonNumber jsonNumber)
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
        /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
        /// </summary>
        /// <param name="jsonString">The <see cref="JsonString"/> from which to construct the value.</param>
        public JsonNotAny(JsonString jsonString)
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
        /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
        /// </summary>
        /// <param name="jsonBoolean">The <see cref="JsonBoolean"/> from which to construct the value.</param>
        public JsonNotAny(JsonBoolean jsonBoolean)
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
        public bool HasJsonElement => this.objectBacking is null && this.arrayBacking is null && this.numberBacking is null && this.stringBacking is null && this.booleanBacking is null;

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
                if (this.HasJsonElement)
                {
                    return new JsonAny(this.jsonElementBacking);
                }

                return this.ValueKind switch
                {
                    JsonValueKind.Object => new JsonAny(this.objectBacking!),
                    JsonValueKind.Array => new JsonAny(this.arrayBacking!),
                    JsonValueKind.Number => new JsonAny(this.numberBacking!.Value),
                    JsonValueKind.String => new JsonAny(this.stringBacking!),
                    JsonValueKind.True => new JsonAny(true),
                    JsonValueKind.False => new JsonAny(false),
                    JsonValueKind.Undefined => default,
                    JsonValueKind.Null => JsonNull.Instance,
                    _ => default,
                };
            }
        }

        /// <summary>
        /// Gets the value as a <see cref="JsonObject"/>.
        /// </summary>
        public JsonObject AsObject
        {
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
        /// Gets the value as a <see cref="JsonBoolean"/>.
        /// </summary>
        public JsonBoolean AsBoolean
        {
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
            get
            {
                return default;
            }
        }

        /// <summary>
        /// Conversion from JsonNumber.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonNotAny(JsonNumber value)
        {
            return new JsonNotAny(value);
        }

        /// <summary>
        /// Conversion to JsonNumber.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator JsonNumber(JsonNotAny value)
        {
            return value.AsNumber;
        }

        /// <summary>
        /// Conversion from JsonArray.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonNotAny(JsonArray value)
        {
            return new JsonNotAny(value);
        }

        /// <summary>
        /// Conversion to JsonArray.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator JsonArray(JsonNotAny value)
        {
            return value.AsArray;
        }

        /// <summary>
        /// Conversion from JsonBoolean.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonNotAny(JsonBoolean value)
        {
            return new JsonNotAny(value);
        }

        /// <summary>
        /// Conversion to JsonBoolean.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator JsonBoolean(JsonNotAny value)
        {
            return value.AsBoolean;
        }

        /// <summary>
        /// Conversion from JsonBoolean.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonNotAny(bool value)
        {
            return new JsonNotAny(value);
        }

        /// <summary>
        /// Conversion to JsonBoolean.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator bool(JsonNotAny value)
        {
            return (bool)value.AsBoolean;
        }

        /// <summary>
        /// Conversion from JsonObject.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonNotAny(JsonObject value)
        {
            return new JsonNotAny(value);
        }

        /// <summary>
        /// Conversion to JsonObject.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator JsonObject(JsonNotAny value)
        {
            return value.AsObject;
        }

        /// <summary>
        /// Conversion from JsonString.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonNotAny(JsonString value)
        {
            return new JsonNotAny(value);
        }

        /// <summary>
        /// Conversion to JsonString.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator JsonString(JsonNotAny value)
        {
            return value.AsString;
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonNotAny(string value)
        {
            return new JsonNotAny(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator string(JsonNotAny value)
        {
            return value.AsString.GetString();
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonNotAny(ReadOnlySpan<char> value)
        {
            return new JsonNotAny(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<char>(JsonNotAny value)
        {
            return value.AsString.AsSpan();
        }

        /// <summary>
        /// Conversion from utf8 bytes.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonNotAny(ReadOnlySpan<byte> value)
        {
            return new JsonNotAny(value);
        }

        /// <summary>
        /// Conversion to utf8 bytes.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<byte>(JsonNotAny value)
        {
            return Encoding.UTF8.GetBytes(value);
        }

        /// <summary>
        /// Conversion from <see cref="JsonAny"/>.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonNotAny(JsonAny value)
        {
            if (value.HasJsonElement)
            {
                return new JsonNotAny(value.AsJsonElement);
            }

            return value.As<JsonNotAny>();
        }

        /// <summary>
        /// Conversion to <see cref="JsonAny"/>.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator JsonAny(JsonNotAny value)
        {
            return value.AsAny;
        }

        /// <summary>
        /// Conversion from double.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonNotAny(double value)
        {
            return new JsonNotAny(value);
        }

        /// <summary>
        /// Conversion to double.
        /// </summary>
        /// <param name="number">The number from which to convert.</param>
        public static implicit operator double(JsonNotAny number)
        {
            return number.AsNumber.GetDouble();
        }

        /// <summary>
        /// Conversion from float.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonNotAny(float value)
        {
            return new JsonNotAny(value);
        }

        /// <summary>
        /// Conversion to float.
        /// </summary>
        /// <param name="number">The number from which to convert.</param>
        public static implicit operator float(JsonNotAny number)
        {
            return number.AsNumber.GetSingle();
        }

        /// <summary>
        /// Conversion from long.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonNotAny(long value)
        {
            return new JsonNotAny(value);
        }

        /// <summary>
        /// Conversion to long.
        /// </summary>
        /// <param name="number">The number from which to convert.</param>
        public static implicit operator long(JsonNotAny number)
        {
            return number.AsNumber.GetInt64();
        }

        /// <summary>
        /// Conversion from int.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonNotAny(int value)
        {
            return new JsonNotAny(value);
        }

        /// <summary>
        /// Conversion to int.
        /// </summary>
        /// <param name="number">The number from which to convert.</param>
        public static implicit operator int(JsonNotAny number)
        {
            return number.AsNumber.GetInt32();
        }

        /// <summary>
        /// Implicit conversion to an <see cref="ImmutableList{T}"/> of <see cref="JsonNotAny"/>.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator ImmutableList<JsonAny>(JsonNotAny value)
        {
            return value.AsArray.AsItemsList;
        }

        /// <summary>
        /// Implicit conversion from an <see cref="ImmutableList{T}"/> of <see cref="JsonNotAny"/>.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonNotAny(ImmutableList<JsonAny> value)
        {
            return new JsonNotAny(value);
        }

        /// <summary>
        /// Implicit conversion to a property dictionary.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator ImmutableDictionary<string, JsonAny>(JsonNotAny value)
        {
            return value.AsObject.AsPropertyDictionary;
        }

        /// <summary>
        /// Implicit conversion from a property dictionary.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonNotAny(ImmutableDictionary<string, JsonAny> value)
        {
            return new JsonNotAny(value);
        }

        /// <summary>
        /// Standard equality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are equal.</returns>
        public static bool operator ==(JsonNotAny lhs, JsonNotAny rhs)
        {
            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Standard inequality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are not equal.</returns>
        public static bool operator !=(JsonNotAny lhs, JsonNotAny rhs)
        {
            return !lhs.Equals(rhs);
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
        public override string ToString()
        {
            return this.Serialize();
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
            return this.AsObject.EnumerateObject();
        }

        /// <inheritdoc/>
        public JsonArrayEnumerator EnumerateArray()
        {
            return this.AsArray.EnumerateArray();
        }

        /// <inheritdoc/>
        public bool TryGetProperty(string name, out JsonAny value)
        {
            return this.AsObject.TryGetProperty(name, out value);
        }

        /// <inheritdoc/>
        public bool TryGetProperty(ReadOnlySpan<char> name, out JsonAny value)
        {
            return this.AsObject.TryGetProperty(name, out value);
        }

        /// <inheritdoc/>
        public bool TryGetProperty(ReadOnlySpan<byte> utf8name, out JsonAny value)
        {
            return this.AsObject.TryGetProperty(utf8name, out value);
        }

        /// <inheritdoc/>
        public bool HasProperty(string name)
        {
            return this.AsObject.HasProperty(name);
        }

        /// <inheritdoc/>
        public bool HasProperty(ReadOnlySpan<char> name)
        {
            return this.AsObject.HasProperty(name);
        }

        /// <inheritdoc/>
        public bool HasProperty(ReadOnlySpan<byte> utf8name)
        {
            return this.AsObject.HasProperty(utf8name);
        }

        /// <inheritdoc/>
        public bool Equals<T>(T other)
            where T : struct, IJsonValue
        {
            return this.Equals((JsonNotAny)other.AsAny);
        }

        /// <inheritdoc/>
        public bool Equals(JsonNotAny other)
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

        /// <inheritdoc/>
        public JsonNotAny SetProperty<TValue>(string name, TValue value)
            where TValue : struct, IJsonValue
        {
            if (this.ValueKind == JsonValueKind.Object || this.ValueKind == JsonValueKind.Undefined)
            {
                return new JsonNotAny(this.AsObject.SetProperty(name, value));
            }

            return this;
        }

        /// <inheritdoc/>
        public JsonNotAny SetProperty<TValue>(ReadOnlySpan<char> name, TValue value)
            where TValue : struct, IJsonValue
        {
            if (this.ValueKind == JsonValueKind.Object || this.ValueKind == JsonValueKind.Undefined)
            {
                return new JsonNotAny(this.AsObject.SetProperty(name, value));
            }

            return this;
        }

        /// <inheritdoc/>
        public JsonNotAny SetProperty<TValue>(ReadOnlySpan<byte> utf8Name, TValue value)
            where TValue : struct, IJsonValue
        {
            if (this.ValueKind == JsonValueKind.Object || this.ValueKind == JsonValueKind.Undefined)
            {
                return new JsonNotAny(this.AsObject.SetProperty(utf8Name, value));
            }

            return this;
        }

        /// <inheritdoc/>
        public JsonNotAny RemoveProperty(string name)
        {
            if (this.ValueKind == JsonValueKind.Object)
            {
                return new JsonNotAny(this.AsObject.RemoveProperty(name));
            }

            return this;
        }

        /// <inheritdoc/>
        public JsonNotAny RemoveProperty(ReadOnlySpan<char> name)
        {
            if (this.ValueKind == JsonValueKind.Object)
            {
                return new JsonNotAny(this.AsObject.RemoveProperty(name));
            }

            return this;
        }

        /// <inheritdoc/>
        public JsonNotAny RemoveProperty(ReadOnlySpan<byte> utf8Name)
        {
            if (this.ValueKind == JsonValueKind.Object)
            {
                return new JsonNotAny(this.AsObject.RemoveProperty(utf8Name));
            }

            return this;
        }

        /// <inheritdoc/>
        public JsonNotAny Add<TItem>(TItem item)
            where TItem : struct, IJsonValue
        {
            if (this.ValueKind == JsonValueKind.Array)
            {
                return new JsonNotAny(this.AsArray.Add(item));
            }

            return this;
        }

        /// <inheritdoc/>
        public JsonNotAny Add<TItem1, TItem2>(TItem1 item1, TItem2 item2)
            where TItem1 : struct, IJsonValue
            where TItem2 : struct, IJsonValue
        {
            if (this.ValueKind == JsonValueKind.Array)
            {
                return new JsonNotAny(this.AsArray.Add(item1, item2));
            }

            return this;
        }

        /// <inheritdoc/>
        public JsonNotAny Add<TItem1, TItem2, TItem3>(TItem1 item1, TItem2 item2, TItem3 item3)
            where TItem1 : struct, IJsonValue
            where TItem2 : struct, IJsonValue
            where TItem3 : struct, IJsonValue
        {
            if (this.ValueKind == JsonValueKind.Array)
            {
                return new JsonNotAny(this.AsArray.Add(item1, item2, item3));
            }

            return this;
        }

        /// <inheritdoc/>
        public JsonNotAny Add<TItem1, TItem2, TItem3, TItem4>(TItem1 item1, TItem2 item2, TItem3 item3, TItem4 item4)
            where TItem1 : struct, IJsonValue
            where TItem2 : struct, IJsonValue
            where TItem3 : struct, IJsonValue
            where TItem4 : struct, IJsonValue
        {
            if (this.ValueKind == JsonValueKind.Array)
            {
                return new JsonNotAny(this.AsArray.Add(item1, item2, item3, item4));
            }

            return this;
        }

        /// <inheritdoc/>
        public JsonNotAny Add<TItem>(params TItem[] items)
            where TItem : struct, IJsonValue
        {
            if (this.ValueKind == JsonValueKind.Array)
            {
                return new JsonNotAny(this.AsArray.Add(items));
            }

            return this;
        }

        /// <inheritdoc/>
        public JsonNotAny AddRange<TItem>(IEnumerable<TItem> items)
            where TItem : struct, IJsonValue
        {
            if (this.ValueKind == JsonValueKind.Array)
            {
                return new JsonNotAny(this.AsArray.AddRange(items));
            }

            return this;
        }

        /// <inheritdoc/>
        public JsonNotAny Insert<TItem>(int index, TItem item)
            where TItem : struct, IJsonValue
        {
            if (this.ValueKind == JsonValueKind.Array)
            {
                return new JsonNotAny(this.AsArray.Insert(index, item));
            }

            return this;
        }

        /// <inheritdoc/>
        public JsonNotAny Replace<TItem>(TItem oldValue, TItem newValue)
            where TItem : struct, IJsonValue
        {
            if (this.ValueKind == JsonValueKind.Array)
            {
                return new JsonNotAny(this.AsArray.Replace(oldValue, newValue));
            }

            return this;
        }

        /// <inheritdoc/>
        public JsonNotAny RemoveAt(int index)
        {
            if (this.ValueKind == JsonValueKind.Array)
            {
                return new JsonNotAny(this.AsArray.RemoveAt(index));
            }

            return this;
        }

        /// <inheritdoc/>
        public JsonNotAny RemoveRange(int index, int count)
        {
            if (this.ValueKind == JsonValueKind.Array)
            {
                return new JsonNotAny(this.AsArray.RemoveRange(index, count));
            }

            return this;
        }

        /// <inheritdoc/>
        public JsonNotAny SetItem<TItem>(int index, TItem value)
            where TItem : struct, IJsonValue
        {
            if (this.ValueKind == JsonValueKind.Array)
            {
                return new JsonNotAny(this.AsArray.SetItem(index, value));
            }

            return this;
        }

        /// <inheritdoc/>
        public T As<T>()
            where T : struct, IJsonValue
        {
            return this.As<JsonNotAny, T>();
        }

        /// <inheritdoc/>
        public ValidationContext Validate(in ValidationContext validationContext, ValidationLevel level = ValidationLevel.Flag)
        {
            if (level == ValidationLevel.Flag)
            {
                return validationContext.WithResult(isValid: false);
            }

            ValidationContext result = validationContext;
            return result.WithResult(isValid: false, "9.2.1.4 not - the instance matches {}.");
        }
    }
}
