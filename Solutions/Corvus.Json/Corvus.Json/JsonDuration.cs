// <copyright file="JsonDuration.cs" company="Endjin Limited">
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
    public readonly struct JsonDuration : IJsonValue, IEquatable<JsonDuration>
    {
        private readonly JsonElement jsonElement;
        private readonly string? value;
        private readonly Period? localPeriodValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonDuration"/> struct.
        /// </summary>
        /// <param name="jsonElement">The JSON element from which to construct the object.</param>
        public JsonDuration(JsonElement jsonElement)
        {
            this.jsonElement = jsonElement;
            this.value = default;
            this.localPeriodValue = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonDuration"/> struct.
        /// </summary>
        /// <param name="value">The string value.</param>
        public JsonDuration(JsonString value)
        {
            if (value.HasJsonElement)
            {
                this.jsonElement = value.AsJsonElement;
                this.value = default;
                this.localPeriodValue = default;
            }
            else
            {
                this.jsonElement = default;
                this.value = value;
                this.localPeriodValue = default;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonDuration"/> struct.
        /// </summary>
        /// <param name="value">The base64 encoded string value.</param>
        public JsonDuration(string value)
        {
            this.jsonElement = default;
            this.value = value;
            this.localPeriodValue = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonDuration"/> struct.
        /// </summary>
        /// <param name="value">The base64 encoded string value.</param>
        public JsonDuration(ReadOnlySpan<char> value)
        {
            this.jsonElement = default;
            this.value = value.ToString();
            this.localPeriodValue = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonDuration"/> struct.
        /// </summary>
        /// <param name="value">The utf8-encoded string value.</param>
        public JsonDuration(ReadOnlySpan<byte> value)
        {
            this.jsonElement = default;
            this.value = Encoding.UTF8.GetString(value);
            this.localPeriodValue = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonDuration"/> struct.
        /// </summary>
        /// <param name="value">The NodaTime date value.</param>
        public JsonDuration(Period value)
        {
            this.jsonElement = default;
            this.value = FormatPeriod(value);
            this.localPeriodValue = value;
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
        public static implicit operator JsonString(JsonDuration value)
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
        /// Implicit conversion from JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonDuration(JsonString value)
        {
            return new JsonDuration(value);
        }

        /// <summary>
        /// Implicit conversion to Period.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator Period(JsonDuration value)
        {
            return value.GetPeriod();
        }

        /// <summary>
        /// Implicit conversion from JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonDuration(Period value)
        {
            return new JsonDuration(value);
        }

        /// <summary>
        /// Implicit conversion to JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(JsonDuration value)
        {
            return value.AsAny;
        }

        /// <summary>
        /// Implicit conversion from JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonDuration(JsonAny value)
        {
            return value.AsString;
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonDuration(string value)
        {
            return new JsonDuration(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator string(JsonDuration value)
        {
            return value.GetString();
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonDuration(ReadOnlySpan<char> value)
        {
            return new JsonDuration(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<char>(JsonDuration value)
        {
            return value.AsSpan();
        }

        /// <summary>
        /// Conversion from utf8 bytes.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonDuration(ReadOnlySpan<byte> value)
        {
            return new JsonDuration(value);
        }

        /// <summary>
        /// Conversion to utf8 bytes.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<byte>(JsonDuration value)
        {
            return Encoding.UTF8.GetBytes(value);
        }

        /// <summary>
        /// Standard equality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are equal.</returns>
        public static bool operator ==(JsonDuration lhs, JsonDuration rhs)
        {
            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Standard inequality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are not equal.</returns>
        public static bool operator !=(JsonDuration lhs, JsonDuration rhs)
        {
            return !lhs.Equals(rhs);
        }

        /// <inheritdoc/>
        public ValidationContext Validate(in ValidationContext? validationContext = null, ValidationLevel level = ValidationLevel.Flag)
        {
            ValidationContext result = validationContext ?? ValidationContext.ValidContext;

            return Json.Validate.TypeDuration(this, result, level);
        }

        /// <inheritdoc/>
        public T As<T>()
            where T : struct, IJsonValue
        {
            if (typeof(T) == typeof(JsonDuration))
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

            return this.As<JsonDuration, T>();
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
        /// Gets the value as a Period.
        /// </summary>
        /// <returns>The value as a Period.</returns>
        public Period GetPeriod()
        {
            if (this.TryGetPeriod(out Period result))
            {
                return result;
            }

            return Period.Zero;
        }

        /// <summary>
        /// Try to get the date value.
        /// </summary>
        /// <param name="result">The date value.</param>
        /// <returns><c>True</c> if it was possible to get a date value from the instance.</returns>
        public bool TryGetPeriod(out Period result)
        {
            if (this.localPeriodValue is Period localDateValue)
            {
                result = localDateValue;
                return true;
            }

            if (this.value is string value)
            {
                return TryParsePeriod(value, out result);
            }

            if (this.jsonElement.ValueKind == JsonValueKind.String)
            {
                string? str = this.jsonElement.GetString();
                return TryParsePeriod(str!, out result);
            }

            result = Period.Zero;
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

            if (!TryParsePeriod((string)other.AsString(), out Period otherDate))
            {
                return false;
            }

            if (!this.TryGetPeriod(out Period thisDate))
            {
                return false;
            }

            return thisDate.Equals(otherDate);
        }

        /// <inheritdoc/>
        public bool Equals(JsonDuration other)
        {
            if (this.IsNull() && other.IsNull())
            {
                return true;
            }

            if (other.ValueKind != this.ValueKind || this.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            if (!this.TryGetPeriod(out Period thisDate))
            {
                return false;
            }

            if (!other.TryGetPeriod(out Period otherDate))
            {
                return false;
            }

            return thisDate.Equals(otherDate);
        }

        private static string FormatPeriod(Period value)
        {
            return PeriodPattern.NormalizingIso.Format(value);
        }

        private static bool TryParsePeriod(string text, out Period value)
        {
            string tupper = text.ToUpperInvariant();
            ParseResult<Period> parseResult = PeriodPattern.NormalizingIso.Parse(tupper);
            if (parseResult.Success)
            {
                value = parseResult.Value;
                return true;
            }

            value = Period.Zero;
            return false;
        }
    }
}
