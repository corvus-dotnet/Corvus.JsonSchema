// <copyright file="JsonBoolean.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;
    using System.Buffers;
    using System.Text.Json;

    /// <summary>
    /// A JSON object.
    /// </summary>
    public readonly struct JsonBoolean : IJsonValue, IEquatable<JsonBoolean>
    {
        private static readonly int TrueHashCode = HashCode.Combine(true.GetHashCode(), "JsonBoolean".GetHashCode());
        private static readonly int FalseHashCode = HashCode.Combine(false.GetHashCode(), "JsonBoolean".GetHashCode());
        private readonly JsonElement jsonElement;
        private readonly bool? value;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonBoolean"/> struct.
        /// </summary>
        /// <param name="jsonElement">The JSON element from which to construct the object.</param>
        public JsonBoolean(JsonElement jsonElement)
        {
            this.jsonElement = jsonElement;
            this.value = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonBoolean"/> struct.
        /// </summary>
        /// <param name="value">The string value.</param>
        public JsonBoolean(bool value)
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
                if (this.value is bool value)
                {
                    return value ? JsonValueKind.True : JsonValueKind.False;
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
                if (this.value is bool value)
                {
                    return BoolToJsonElement(value);
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
        /// Implicit conversion to JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(JsonBoolean value)
        {
            return value.AsAny;
        }

        /// <summary>
        /// Implicit conversion from JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonBoolean(JsonAny value)
        {
            return value.AsBoolean;
        }

        /// <summary>
        /// Conversion from bool.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonBoolean(bool value)
        {
            return new JsonBoolean(value);
        }

        /// <summary>
        /// Conversion to bool.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator bool(JsonBoolean value)
        {
            return value.GetBoolean();
        }

        /// <summary>
        /// Standard equality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are equal.</returns>
        public static bool operator ==(JsonBoolean lhs, JsonBoolean rhs)
        {
            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Standard inequality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are not equal.</returns>
        public static bool operator !=(JsonBoolean lhs, JsonBoolean rhs)
        {
            return !lhs.Equals(rhs);
        }

        /// <summary>
        /// Write a property dictionary to a <see cref="JsonElement"/>.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <returns>A JsonElement serialized from the properties.</returns>
        public static JsonElement BoolToJsonElement(bool value)
        {
            var abw = new ArrayBufferWriter<byte>();
            using var writer = new Utf8JsonWriter(abw);
            writer.WriteBooleanValue(value);
            writer.Flush();
            var reader = new Utf8JsonReader(abw.WrittenSpan);
            using var document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.Clone();
        }

        /// <inheritdoc/>
        public ValidationContext Validate(in ValidationContext validationContext, ValidationLevel level = ValidationLevel.Flag)
        {
            ValidationContext result = validationContext;
            return Json.Validate.TypeBoolean(this.ValueKind, result, level);
        }

        /// <inheritdoc/>
        public T As<T>()
            where T : struct, IJsonValue
        {
            return this.As<JsonBoolean, T>();
        }

        /// <summary>
        /// Get the value as a bool.
        /// </summary>
        /// <returns>The bool value.</returns>
        public bool GetBoolean()
        {
            if (this.TryGetBoolean(out bool result))
            {
                return result;
            }

            return default;
        }

        /// <summary>
        /// Gets the value as <see cref="bool"/>.
        /// </summary>
        /// <param name="result">The value as bool.</param>
        /// <returns><c>True</c> if the value could be retrieved.</returns>
        public bool TryGetBoolean(out bool result)
        {
            if (this.value is bool value)
            {
                result = value;
                return true;
            }

            if (this.jsonElement.ValueKind == JsonValueKind.True)
            {
                result = true;
                return true;
            }

            if (this.jsonElement.ValueKind == JsonValueKind.False)
            {
                result = false;
                return true;
            }

            result = default;
            return false;
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
                JsonValueKind.True => TrueHashCode,
                JsonValueKind.False => FalseHashCode,
                JsonValueKind.Null => JsonNull.NullHashCode,
                _ => JsonAny.UndefinedHashCode,
            };
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            JsonValueKind valueKind = this.ValueKind;

            return valueKind switch
            {
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                _ => string.Empty,
            };
        }

        /// <summary>
        /// Writes the string to the <see cref="Utf8JsonWriter"/>.
        /// </summary>
        /// <param name="writer">The writer to which to write the object.</param>
        public void WriteTo(Utf8JsonWriter writer)
        {
            if (this.value is bool value)
            {
                writer.WriteBooleanValue(value);
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
            JsonValueKind valueKind = this.ValueKind;
            JsonValueKind otherValueKind = other.ValueKind;
            if (valueKind == JsonValueKind.Null && other.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if ((valueKind != JsonValueKind.True && valueKind != JsonValueKind.False) || valueKind != otherValueKind)
            {
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public bool Equals(JsonBoolean other)
        {
            JsonValueKind valueKind = this.ValueKind;
            JsonValueKind otherValueKind = other.ValueKind;
            if (valueKind == JsonValueKind.Null && other.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if ((valueKind != JsonValueKind.True && valueKind != JsonValueKind.False) || valueKind != otherValueKind)
            {
                return false;
            }

            return true;
        }
    }
}
