// <copyright file="JsonDateTime.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;
    using System.Text;
    using System.Text.Json;
    using Corvus.Extensions;
    using NodaTime;
    using NodaTime.Text;

    /// <summary>
    /// A JSON object.
    /// </summary>
    public readonly struct JsonDateTime : IJsonValue, IEquatable<JsonDateTime>
    {
        private readonly JsonElement jsonElement;
        private readonly string? value;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonDateTime"/> struct.
        /// </summary>
        /// <param name="jsonElement">The JSON element from which to construct the object.</param>
        public JsonDateTime(JsonElement jsonElement)
        {
            this.jsonElement = jsonElement;
            this.value = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonDateTime"/> struct.
        /// </summary>
        /// <param name="value">The string value.</param>
        public JsonDateTime(JsonString value)
        {
            if (value.HasJsonElement)
            {
                this.jsonElement = value.AsJsonElement;
                this.value = default;
            }
            else
            {
                this.jsonElement = default;
                this.value = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonDateTime"/> struct.
        /// </summary>
        /// <param name="value">The base64 encoded string value.</param>
        public JsonDateTime(string value)
        {
            this.jsonElement = default;
            this.value = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonDateTime"/> struct.
        /// </summary>
        /// <param name="value">The base64 encoded string value.</param>
        public JsonDateTime(ReadOnlySpan<char> value)
        {
            this.jsonElement = default;
            this.value = value.ToString();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonDateTime"/> struct.
        /// </summary>
        /// <param name="value">The utf8-encoded string value.</param>
        public JsonDateTime(ReadOnlySpan<byte> value)
        {
            this.jsonElement = default;
            this.value = Encoding.UTF8.GetString(value);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonDateTime"/> struct.
        /// </summary>
        /// <param name="value">The NodaTime date value.</param>
        public JsonDateTime(OffsetDateTime value)
        {
            this.jsonElement = default;
            this.value = FormatDateTime(value);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonDateTime"/> struct.
        /// </summary>
        /// <param name="value">The date time offset from which to construct the date.</param>
        public JsonDateTime(DateTimeOffset value)
        {
            this.jsonElement = default;
            this.value = FormatDateTime(OffsetDateTime.FromDateTimeOffset(value));
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
                    return JsonString.StringToJsonElement(value);
                }

                return this.jsonElement;
            }
        }

        /// <inheritdoc/>
        public JsonAny AsAny
        {
            get
            {
                if (this.value is string value)
                {
                    return new JsonAny(value);
                }
                else
                {
                    return new JsonAny(this.jsonElement);
                }
            }
        }

        /// <summary>
        /// Implicit conversion to JsonString.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonString(JsonDateTime value)
        {
            if (value.value is string jet)
            {
                return new JsonString(jet);
            }
            else
            {
                return new JsonString(value.jsonElement);
            }
        }

        /// <summary>
        /// Implicit conversion from JsonString.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonDateTime(JsonString value)
        {
            return new JsonDateTime(value);
        }

        /// <summary>
        /// Implicit conversion to JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(JsonDateTime value)
        {
            return value.AsAny;
        }

        /// <summary>
        /// Implicit conversion from JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonDateTime(JsonAny value)
        {
            return value.AsString;
        }

        /// <summary>
        /// Conversion from OffsetDateTime.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonDateTime(OffsetDateTime value)
        {
            return new JsonDateTime(value);
        }

        /// <summary>
        /// Conversion to OffsetDateTime.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator OffsetDateTime(JsonDateTime value)
        {
            return value.GetDateTime();
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonDateTime(string value)
        {
            return new JsonDateTime(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator string(JsonDateTime value)
        {
            return value.GetString();
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonDateTime(ReadOnlySpan<char> value)
        {
            return new JsonDateTime(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<char>(JsonDateTime value)
        {
            return value.AsSpan();
        }

        /// <summary>
        /// Conversion from utf8 bytes.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonDateTime(ReadOnlySpan<byte> value)
        {
            return new JsonDateTime(value);
        }

        /// <summary>
        /// Conversion to utf8 bytes.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<byte>(JsonDateTime value)
        {
            return Encoding.UTF8.GetBytes(value);
        }

        /// <summary>
        /// Standard equality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are equal.</returns>
        public static bool operator ==(JsonDateTime lhs, JsonDateTime rhs)
        {
            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Standard inequality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are not equal.</returns>
        public static bool operator !=(JsonDateTime lhs, JsonDateTime rhs)
        {
            return !lhs.Equals(rhs);
        }

        /// <inheritdoc/>
        public ValidationContext Validate(in ValidationContext validationContext, ValidationLevel level = ValidationLevel.Flag)
        {
            ValidationContext result = validationContext;

            return Json.Validate.TypeDateTime(this, result, level);
        }

        /// <inheritdoc/>
        public T As<T>()
            where T : struct, IJsonValue
        {
            if (typeof(T) == typeof(JsonDate))
            {
                return CastTo<T>.From(this);
            }

            if (typeof(T) == typeof(JsonString))
            {
                if (this.value is string value)
                {
                    return CastTo<T>.From(new JsonString(value));
                }
                else
                {
                    return CastTo<T>.From(new JsonString(this.jsonElement));
                }
            }

            return this.As<JsonDateTime, T>();
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
                result = value.ToString();
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
        /// Gets the value as a OffsetDateTime.
        /// </summary>
        /// <returns>The value as a OffsetDateTime.</returns>
        public OffsetDateTime GetDateTime()
        {
            if (this.TryGetDateTime(out OffsetDateTime result))
            {
                return result;
            }

            return default;
        }

        /// <summary>
        /// Try to get the date value.
        /// </summary>
        /// <param name="result">The date value.</param>
        /// <returns><c>True</c> if it was possible to get a date value from the instance.</returns>
        public bool TryGetDateTime(out OffsetDateTime result)
        {
            if (this.value is string value)
            {
                return TryParseDateTime(value, out result);
            }

            if (this.jsonElement.ValueKind == JsonValueKind.String)
            {
                string? str = this.jsonElement.GetString();
                return TryParseDateTime(str!, out result);
            }

            result = default;
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
                JsonValueKind.String => this.AsString().GetHashCode(),
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

            if (!TryParseDateTime((string)other.AsString(), out OffsetDateTime otherDate))
            {
                return false;
            }

            if (!this.TryGetDateTime(out OffsetDateTime thisDate))
            {
                return false;
            }

            return thisDate.Equals(otherDate);
        }

        /// <inheritdoc/>
        public bool Equals(JsonDateTime other)
        {
            if (this.IsNull() && other.IsNull())
            {
                return true;
            }

            if (other.ValueKind != this.ValueKind || this.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            if (!this.TryGetDateTime(out OffsetDateTime thisDate))
            {
                return false;
            }

            if (!other.TryGetDateTime(out OffsetDateTime otherDate))
            {
                return false;
            }

            return thisDate.Equals(otherDate);
        }

        private static string FormatDateTime(OffsetDateTime value)
        {
            return OffsetDateTimePattern.ExtendedIso.Format(value);
        }

        private static bool TryParseDateTime(string text, out OffsetDateTime value)
        {
            string tupper = text.ToUpperInvariant();
            ParseResult<OffsetDateTime> parseResult = OffsetDateTimePattern.ExtendedIso.Parse(tupper);
            if (parseResult.Success)
            {
                value = parseResult.Value;
                return true;
            }

            value = default;
            return false;
        }
    }
}
