// <copyright file="JsonIpV6.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using Corvus.Extensions;

    /// <summary>
    /// A JSON object.
    /// </summary>
    public readonly struct JsonIpV6 : IJsonValue, IEquatable<JsonIpV6>
    {
        private readonly JsonElement jsonElement;
        private readonly string? value;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonIpV6"/> struct.
        /// </summary>
        /// <param name="jsonElement">The JSON element from which to construct the object.</param>
        public JsonIpV6(JsonElement jsonElement)
        {
            this.jsonElement = jsonElement;
            this.value = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonIpV6"/> struct.
        /// </summary>
        /// <param name="value">The string value.</param>
        public JsonIpV6(JsonString value)
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
        /// Initializes a new instance of the <see cref="JsonIpV6"/> struct.
        /// </summary>
        /// <param name="value">The base64 encoded string value.</param>
        public JsonIpV6(string value)
        {
            this.jsonElement = default;
            this.value = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonIpV6"/> struct.
        /// </summary>
        /// <param name="value">The base64 encoded string value.</param>
        public JsonIpV6(ReadOnlySpan<char> value)
        {
            this.jsonElement = default;
            this.value = value.ToString();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonIpV6"/> struct.
        /// </summary>
        /// <param name="value">The utf8-encoded string value.</param>
        public JsonIpV6(ReadOnlySpan<byte> value)
        {
            this.jsonElement = default;
            this.value = Encoding.UTF8.GetString(value);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonIpV6"/> struct.
        /// </summary>
        /// <param name="value">The utf8-encoded string value.</param>
        public JsonIpV6(IPAddress value)
        {
            this.jsonElement = default;
            this.value = value.ToString();
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
        public static implicit operator JsonString(JsonIpV6 value)
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
        public static implicit operator JsonIpV6(JsonString value)
        {
            return new JsonIpV6(value);
        }

        /// <summary>
        /// Implicit conversion to IPAddress.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator IPAddress(JsonIpV6 value)
        {
            return value.GetIPAddress();
        }

        /// <summary>
        /// Implicit conversion from IPaddress.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonIpV6(IPAddress value)
        {
            return new JsonIpV6(value);
        }

        /// <summary>
        /// Implicit conversion to JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(JsonIpV6 value)
        {
            return value.AsAny;
        }

        /// <summary>
        /// Implicit conversion from JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonIpV6(JsonAny value)
        {
            return value.AsString;
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonIpV6(string value)
        {
            return new JsonIpV6(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator string(JsonIpV6 value)
        {
            return value.GetString();
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonIpV6(ReadOnlySpan<char> value)
        {
            return new JsonIpV6(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<char>(JsonIpV6 value)
        {
            return value.AsSpan();
        }

        /// <summary>
        /// Conversion from utf8 bytes.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonIpV6(ReadOnlySpan<byte> value)
        {
            return new JsonIpV6(value);
        }

        /// <summary>
        /// Conversion to utf8 bytes.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<byte>(JsonIpV6 value)
        {
            return Encoding.UTF8.GetBytes(value);
        }

        /// <summary>
        /// Standard equality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are equal.</returns>
        public static bool operator ==(JsonIpV6 lhs, JsonIpV6 rhs)
        {
            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Standard inequality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are not equal.</returns>
        public static bool operator !=(JsonIpV6 lhs, JsonIpV6 rhs)
        {
            return !lhs.Equals(rhs);
        }

        /// <inheritdoc/>
        public ValidationContext Validate(in ValidationContext validationContext, ValidationLevel level = ValidationLevel.Flag)
        {
            ValidationContext result = validationContext;

            return Json.Validate.TypeIpV6(this, result, level);
        }

        /// <inheritdoc/>
        public T As<T>()
            where T : struct, IJsonValue
        {
            if (typeof(T) == typeof(JsonIpV6))
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

            return this.As<JsonIpV6, T>();
        }

        /// <summary>
        /// Get the value as <see cref="IPAddress"/>.
        /// </summary>
        /// <returns>The IPAddress.</returns>
        public IPAddress GetIPAddress()
        {
            if (this.TryGetIPAddress(out IPAddress? result))
            {
                return result;
            }

            return IPAddress.None;
        }

        /// <summary>
        /// Gets the string as <see cref="IPAddress"/>.
        /// </summary>
        /// <param name="result">The value as IPAddress.</param>
        /// <returns><c>True</c> if the value could be retrieved.</returns>
        public bool TryGetIPAddress([NotNullWhen(true)] out IPAddress? result)
        {
            if (this.value is string jet)
            {
                return IPAddress.TryParse(jet, out result);
            }
            else if (this.jsonElement.ValueKind == JsonValueKind.String)
            {
                return IPAddress.TryParse(this.jsonElement.GetString(), out result);
            }

            result = IPAddress.None;
            return false;
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

        /// <inheritdoc />
        public override string ToString()
        {
            return this.Serialize();
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

            return this.AsString().Equals(other.AsString());
        }

        /// <inheritdoc/>
        public bool Equals(JsonIpV6 other)
        {
            if (this.IsNull() && other.IsNull())
            {
                return true;
            }

            if (other.ValueKind != this.ValueKind || this.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            return this.AsString().Equals(other.AsString());
        }
    }
}
