// <copyright file="JsonInteger.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;
    using System.Buffers;
    using System.Text.Json;
    using Corvus.Extensions;

    /// <summary>
    /// A JSON integer.
    /// </summary>
    public readonly struct JsonInteger : IJsonValue, IEquatable<JsonInteger>
    {
        private readonly JsonElement jsonElement;
        private readonly double? value;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonInteger"/> struct.
        /// </summary>
        /// <param name="jsonElement">The JSON element from which to construct the object.</param>
        public JsonInteger(JsonElement jsonElement)
        {
            this.jsonElement = jsonElement;
            this.value = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonInteger"/> struct.
        /// </summary>
        /// <param name="value">The double value.</param>
        public JsonInteger(double value)
        {
            this.jsonElement = default;
            this.value = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonInteger"/> struct.
        /// </summary>
        /// <param name="value">The double value.</param>
        public JsonInteger(float value)
        {
            this.jsonElement = default;
            this.value = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonInteger"/> struct.
        /// </summary>
        /// <param name="value">The double value.</param>
        public JsonInteger(int value)
        {
            this.jsonElement = default;
            this.value = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonInteger"/> struct.
        /// </summary>
        /// <param name="value">The long value.</param>
        public JsonInteger(long value)
        {
            this.jsonElement = default;
            this.value = value;
        }

        /// <summary>
        /// Gets the <see cref="JsonValueKind"/>.
        /// </summary>
        public JsonValueKind ValueKind
        {
            get
            {
                if (this.value is not null)
                {
                    return JsonValueKind.Number;
                }

                return this.jsonElement.ValueKind;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this is backed by a <see cref="JsonElement"/>.
        /// </summary>
        public bool HasJsonElement => this.value is null;

        /// <summary>
        /// Gets the backing <see cref="JsonElement"/>.
        /// </summary>
        public JsonElement AsJsonElement
        {
            get
            {
                if (this.value is double value)
                {
                    return NumberToJsonElement(value);
                }

                return this.jsonElement;
            }
        }

        /// <inheritdoc/>
        public JsonAny AsAny
        {
            get
            {
                if (this.value is double value)
                {
                    return new JsonAny(value);
                }

                return new JsonAny(this.jsonElement);
            }
        }

        /// <summary>
        /// Implicit conversion to JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(JsonInteger value)
        {
            return value.AsAny;
        }

        /// <summary>
        /// Implicit conversion from JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonInteger(JsonAny value)
        {
            return value.AsNumber;
        }

        /// <summary>
        /// Conversion from double.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonInteger(double value)
        {
            return new JsonInteger(value);
        }

        /// <summary>
        /// Conversion to double.
        /// </summary>
        /// <param name="number">The number from which to convert.</param>
        public static implicit operator double(JsonInteger number)
        {
            return number.GetDouble();
        }

        /// <summary>
        /// Conversion from float.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonInteger(float value)
        {
            return new JsonInteger(value);
        }

        /// <summary>
        /// Conversion to float.
        /// </summary>
        /// <param name="number">The number from which to convert.</param>
        public static implicit operator float(JsonInteger number)
        {
            return number.GetSingle();
        }

        /// <summary>
        /// Conversion from long.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonInteger(long value)
        {
            return new JsonInteger(value);
        }

        /// <summary>
        /// Conversion to long.
        /// </summary>
        /// <param name="number">The number from which to convert.</param>
        public static implicit operator long(JsonInteger number)
        {
            return number.GetInt64();
        }

        /// <summary>
        /// Conversion from int.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonInteger(int value)
        {
            return new JsonInteger(value);
        }

        /// <summary>
        /// Conversion to int.
        /// </summary>
        /// <param name="number">The number from which to convert.</param>
        public static implicit operator int(JsonInteger number)
        {
            return number.GetInt32();
        }

        /// <summary>
        /// Conversion from JsonNumber.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonInteger(JsonNumber value)
        {
            if (value.HasJsonElement)
            {
                return new JsonInteger(value.AsJsonElement);
            }

            return new JsonInteger((double)value);
        }

        /// <summary>
        /// Conversion to JsonNumber.
        /// </summary>
        /// <param name="number">The number from which to convert.</param>
        public static implicit operator JsonNumber(JsonInteger number)
        {
            return number.GetInt64();
        }

        /// <summary>
        /// Standard equality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are equal.</returns>
        public static bool operator ==(JsonInteger lhs, JsonInteger rhs)
        {
            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Standard inequality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are not equal.</returns>
        public static bool operator !=(JsonInteger lhs, JsonInteger rhs)
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
        public ValidationContext Validate(in ValidationContext? validationContext = null, ValidationLevel level = ValidationLevel.Flag)
        {
            ValidationContext result = validationContext ?? ValidationContext.ValidContext;

            return Json.Validate.TypeInteger(this, result, level);
        }

        /// <inheritdoc/>
        public T As<T>()
            where T : struct, IJsonValue
        {
            if (typeof(T) == typeof(JsonInteger))
            {
                return CastTo<T>.From(this);
            }

            return this.As<JsonInteger, T>();
        }

        /// <summary>
        /// Gets the <see cref="JsonInteger"/> as a <see cref="double"/>.
        /// </summary>
        /// <returns>The <see cref="double"/>.</returns>
        public double GetDouble()
        {
            if (this.TryGetDouble(out double value))
            {
                return value;
            }

            throw new InvalidOperationException("Unable to get this JsonInteger as a double.");
        }

        /// <summary>
        /// Try to get a <see cref="double"/> value from this <see cref="JsonInteger"/>.
        /// </summary>
        /// <param name="value">The <see cref="JsonInteger"/> as a <see cref="double"/>.</param>
        /// <returns><c>True</c> if we were able to get the value.</returns>
        public bool TryGetDouble(out double value)
        {
            if (this.value is double vad)
            {
                value = vad;
                return true;
            }

            if (this.jsonElement.ValueKind == JsonValueKind.Number)
            {
                return this.jsonElement.TryGetDouble(out value);
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Gets the <see cref="JsonInteger"/> as a <see cref="float"/>.
        /// </summary>
        /// <returns>The <see cref="float"/>.</returns>
        public float GetSingle()
        {
            if (this.TryGetSingle(out float value))
            {
                return value;
            }

            throw new InvalidOperationException("Unable to get the JsonInteger as a single.");
        }

        /// <summary>
        /// Try to get a <see cref="float"/> value from this <see cref="JsonInteger"/>.
        /// </summary>
        /// <param name="value">The <see cref="JsonInteger"/> as a <see cref="float"/>.</param>
        /// <returns><c>True</c> if we were able to get the value.</returns>
        public bool TryGetSingle(out float value)
        {
            if (this.value is double vad)
            {
                if (vad >= float.MinValue && vad <= float.MaxValue)
                {
                    value = (float)vad;
                    return true;
                }
            }

            if (this.jsonElement.ValueKind == JsonValueKind.Number)
            {
                return this.jsonElement.TryGetSingle(out value);
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Gets the <see cref="JsonInteger"/> as a <see cref="long"/>.
        /// </summary>
        /// <returns>The <see cref="long"/>.</returns>
        public long GetInt64()
        {
            if (this.TryGetInt64(out long value))
            {
                return value;
            }

            throw new InvalidOperationException("Unable to get the JsonInteger as an int64.");
        }

        /// <summary>
        /// Try to get a <see cref="long"/> value from this <see cref="JsonInteger"/>.
        /// </summary>
        /// <param name="value">The <see cref="JsonInteger"/> as a <see cref="long"/>.</param>
        /// <returns><c>True</c> if we were able to get the value.</returns>
        public bool TryGetInt64(out long value)
        {
            if (this.value is double vad)
            {
                if (vad >= long.MinValue && vad <= long.MaxValue)
                {
                    value = (long)vad;
                    return true;
                }
            }

            if (this.jsonElement.ValueKind == JsonValueKind.Number)
            {
                return this.jsonElement.TryGetInt64(out value);
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Gets the <see cref="JsonInteger"/> as an <see cref="int"/>.
        /// </summary>
        /// <returns>The <see cref="int"/>.</returns>
        public int GetInt32()
        {
            if (this.TryGetInt32(out int value))
            {
                return value;
            }

            throw new InvalidOperationException("Unable to get the JsonInteger as an int32.");
        }

        /// <summary>
        /// Try to get a <see cref="int"/> value from this <see cref="JsonInteger"/>.
        /// </summary>
        /// <param name="value">The <see cref="JsonInteger"/> as a <see cref="int"/>.</param>
        /// <returns><c>True</c> if we were able to get the value.</returns>
        public bool TryGetInt32(out int value)
        {
            if (this.value is double vad)
            {
                if (vad >= int.MinValue && vad <= int.MaxValue)
                {
                    value = (int)vad;
                    return true;
                }
            }

            if (this.jsonElement.ValueKind == JsonValueKind.Number)
            {
                return this.jsonElement.TryGetInt32(out value);
            }

            value = default;
            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            JsonValueKind valueKind = this.ValueKind;

            return valueKind switch
            {
                JsonValueKind.Number => this.GetDouble().GetHashCode(),
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
            if (this.value is double value)
            {
                writer.WriteNumberValue(value);
            }
            else
            {
                this.jsonElement.WriteTo(writer);
            }
        }

        /// <inheritdoc/>
        public bool Equals<T>(T other)
            where T : struct, IJsonValue
        {
            if (this.IsNull() && other.IsNull())
            {
                return true;
            }

            if (other.ValueKind != JsonValueKind.Number)
            {
                return false;
            }

            return this.AsNumber().Equals(other.AsNumber());
        }

        /// <inheritdoc/>
        public bool Equals(JsonInteger other)
        {
            if (this.IsNull() && other.IsNull())
            {
                return true;
            }

            if (other.ValueKind != this.ValueKind || this.ValueKind != JsonValueKind.Number)
            {
                return false;
            }

            return this.GetDouble() == other.GetDouble();
        }

        /// <summary>
        /// Write a property dictionary to a <see cref="JsonElement"/>.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <returns>A JsonElement serialized from the properties.</returns>
        internal static JsonElement NumberToJsonElement(double value)
        {
            var abw = new ArrayBufferWriter<byte>();
            using var writer = new Utf8JsonWriter(abw);
            writer.WriteNumberValue(value);
            writer.Flush();
            var reader = new Utf8JsonReader(abw.WrittenSpan);
            using var document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.Clone();
        }
    }
}
