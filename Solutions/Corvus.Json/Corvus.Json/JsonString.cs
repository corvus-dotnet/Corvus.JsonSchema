// <copyright file="JsonString.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;
    using System.Buffers;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// A JSON object.
    /// </summary>
    public readonly struct JsonString : IJsonValue, IEquatable<JsonString>
    {
        private readonly JsonElement jsonElement;
        private readonly string? value;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonString"/> struct.
        /// </summary>
        /// <param name="jsonElement">The JSON element from which to construct the object.</param>
        public JsonString(JsonElement jsonElement)
        {
            this.jsonElement = jsonElement;
            this.value = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonString"/> struct.
        /// </summary>
        /// <param name="value">The string value.</param>
        public JsonString(string value)
        {
            this.jsonElement = default;
            this.value = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonString"/> struct.
        /// </summary>
        /// <param name="value">The string value.</param>
        public JsonString(ReadOnlySpan<char> value)
        {
            this.jsonElement = default;
            this.value = value.ToString();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonString"/> struct.
        /// </summary>
        /// <param name="value">The utf8-encoded string value.</param>
        public JsonString(ReadOnlySpan<byte> value)
        {
            this.jsonElement = default;
            this.value = Encoding.UTF8.GetString(value);
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
                    return JsonValueKind.String;
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
                if (this.value is string value)
                {
                    return StringToJsonElement(value);
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
        public static implicit operator JsonAny(JsonString value)
        {
            return value.AsAny;
        }

        /// <summary>
        /// Implicit conversion from JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonString(JsonAny value)
        {
            return value.AsString;
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonString(string value)
        {
            return new JsonString(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator string(JsonString value)
        {
            return value.GetString();
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonString(ReadOnlySpan<char> value)
        {
            return new JsonString(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<char>(JsonString value)
        {
            return value.AsSpan();
        }

        /// <summary>
        /// Conversion from utf8 bytes.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonString(ReadOnlySpan<byte> value)
        {
            return new JsonString(value);
        }

        /// <summary>
        /// Conversion to utf8 bytes.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<byte>(JsonString value)
        {
            return Encoding.UTF8.GetBytes(value);
        }

        /// <summary>
        /// Standard equality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are equal.</returns>
        public static bool operator ==(JsonString lhs, JsonString rhs)
        {
            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Standard inequality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are not equal.</returns>
        public static bool operator !=(JsonString lhs, JsonString rhs)
        {
            return !lhs.Equals(rhs);
        }

        /// <summary>
        /// Write a property dictionary to a <see cref="JsonElement"/>.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <returns>A JsonElement serialized from the properties.</returns>
        public static JsonElement StringToJsonElement(string value)
        {
            var abw = new ArrayBufferWriter<byte>();
            using var writer = new Utf8JsonWriter(abw);
            writer.WriteStringValue(value);
            writer.Flush();
            var reader = new Utf8JsonReader(abw.WrittenSpan);
            using var document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.Clone();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.GetString();
        }

        /// <inheritdoc/>
        public ValidationContext Validate(in ValidationContext? validationContext = null, ValidationLevel level = ValidationLevel.Flag)
        {
            ValidationContext result = validationContext ?? ValidationContext.ValidContext;

            JsonValueKind valueKind = this.ValueKind;

            return Json.Validate.TypeString(valueKind, result, level);
        }

        /// <inheritdoc/>
        public T As<T>()
            where T : struct, IJsonValue
        {
            return this.As<JsonString, T>();
        }

        /// <summary>
        /// Gets the value as a string.
        /// </summary>
        /// <returns>The value as a string.</returns>
        public string GetString()
        {
            if (this.TryGetString(out string result))
            {
                return result;
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets the value as a string.
        /// </summary>
        /// <param name="result">The value as a string.</param>
        /// <returns><c>True</c> if the value could be retrieved.</returns>
        public bool TryGetString(out string result)
        {
            if (this.value is string value)
            {
                result = value;
                return true;
            }

            if (this.jsonElement.ValueKind == JsonValueKind.String)
            {
                string? str = this.jsonElement.GetString();
                result = str!;
                return true;
            }

            result = string.Empty;
            return false;
        }

        /// <summary>
        /// Gets the value as a span.
        /// </summary>
        /// <returns>The value as a span of char.</returns>
        public ReadOnlySpan<char> AsSpan()
        {
            if (this.value is string value)
            {
                return value;
            }

            if (this.jsonElement.ValueKind == JsonValueKind.String)
            {
                string? str = this.jsonElement.GetString();
                return str!.AsSpan();
            }

            return ReadOnlySpan<char>.Empty;
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
                JsonValueKind.String => this.GetString().GetHashCode(),
                JsonValueKind.Null => JsonNull.NullHashCode,
                _ => JsonAny.UndefinedHashCode,
            };
        }

        /// <summary>
        /// Writes the string to the <see cref="Utf8JsonWriter"/>.
        /// </summary>
        /// <param name="writer">The writer to which to write the object.</param>
        public void WriteTo(Utf8JsonWriter writer)
        {
            if (this.value is string value)
            {
                writer.WriteStringValue(value);
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

            if (other.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            return this.Equals(other.AsString());
        }

        /// <inheritdoc/>
        public bool Equals(JsonString other)
        {
            if (this.IsNull() && other.IsNull())
            {
                return true;
            }

            if (other.ValueKind != this.ValueKind || this.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            // If they are both json elements, it is faster to avoid re-encoding, so we go via the strings
            // This would be obviated if
            if (this.HasJsonElement && other.HasJsonElement)
            {
                // I wish we could compare JSON elements...
                return this.jsonElement.ValueEquals(other.jsonElement.GetString());
            }

            // If we have a json element and they don't, it's faster to go via our value equals
            // over their json encoded text.
            if (this.HasJsonElement)
            {
                return this.jsonElement.ValueEquals(other.GetString());
            }

            // If they have a json element and we don't, it's faster to go via their value equals
            // over our string.
            if (other.HasJsonElement)
            {
                return other.jsonElement.ValueEquals(this.GetString());
            }

            // Otherwise, it is faster to go via the two text instances
            return this.GetString().Equals(other.GetString(), StringComparison.Ordinal);
        }
    }
}
