// <copyright file="JsonBase64String.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Text;
    using System.Text.Json;
    using Corvus.Extensions;

    /// <summary>
    /// A JSON object.
    /// </summary>
    public readonly struct JsonBase64String : IJsonValue, IEquatable<JsonBase64String>
    {
        private readonly JsonElement jsonElement;
        private readonly string? value;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonBase64String"/> struct.
        /// </summary>
        /// <param name="jsonElement">The JSON element from which to construct the object.</param>
        public JsonBase64String(JsonElement jsonElement)
        {
            this.jsonElement = jsonElement;
            this.value = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonBase64String"/> struct.
        /// </summary>
        /// <param name="value">The string value.</param>
        public JsonBase64String(JsonString value)
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
        /// Initializes a new instance of the <see cref="JsonBase64String"/> struct.
        /// </summary>
        /// <param name="value">The base64 encoded string value.</param>
        public JsonBase64String(string value)
        {
            this.jsonElement = default;
            this.value = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonBase64String"/> struct.
        /// </summary>
        /// <param name="value">The base64 encoded string value.</param>
        public JsonBase64String(ReadOnlySpan<char> value)
        {
            this.jsonElement = default;
            this.value = value.ToString();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonBase64String"/> struct.
        /// </summary>
        /// <param name="value">The utf8-encoded string value.</param>
        public JsonBase64String(ReadOnlySpan<byte> value)
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
        public static implicit operator JsonString(JsonBase64String value)
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
        public static implicit operator JsonBase64String(JsonString value)
        {
            return new JsonBase64String(value);
        }

        /// <summary>
        /// Implicit conversion to JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(JsonBase64String value)
        {
            return value.AsAny;
        }

        /// <summary>
        /// Implicit conversion from JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonBase64String(JsonAny value)
        {
            return value.AsString;
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonBase64String(string value)
        {
            return new JsonBase64String(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator string(JsonBase64String value)
        {
            return value.GetString();
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonBase64String(ReadOnlySpan<char> value)
        {
            return new JsonBase64String(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<char>(JsonBase64String value)
        {
            return value.AsSpan();
        }

        /// <summary>
        /// Conversion from utf8 bytes.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonBase64String(ReadOnlySpan<byte> value)
        {
            return new JsonBase64String(value);
        }

        /// <summary>
        /// Conversion to utf8 bytes.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<byte>(JsonBase64String value)
        {
            return Encoding.UTF8.GetBytes(value);
        }

        /// <summary>
        /// Standard equality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are equal.</returns>
        public static bool operator ==(JsonBase64String lhs, JsonBase64String rhs)
        {
            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Standard inequality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are not equal.</returns>
        public static bool operator !=(JsonBase64String lhs, JsonBase64String rhs)
        {
            return !lhs.Equals(rhs);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="JsonBase64String"/> struct from a byte arrary.
        /// </summary>
        /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/> from which to construct the Base64 content.</param>
        /// <returns>The base 64 encoded string represnetation of the byte array.</returns>
        /// <remarks>This encodes the byte array as a base 64 string.</remarks>
        public static JsonBase64String FromByteArray(ReadOnlySpan<byte> value)
        {
            return new JsonBase64String(Encoding.UTF8.GetString(value));
        }

        /// <summary>
        /// Get the base64 encoded string.
        /// </summary>
        /// <returns>The base 64 encoded string.</returns>
        public ReadOnlySpan<char> GetBase64EncodedString()
        {
            if (this.value is string value)
            {
                return value;
            }
            else if (this.ValueKind == JsonValueKind.String)
            {
                string? result = this.jsonElement.GetString();
                if (result is null)
                {
                    return ReadOnlySpan<char>.Empty;
                }

                return result;
            }

            return ReadOnlySpan<char>.Empty;
        }

        /// <summary>
        /// Get the decoded base64 bytes.
        /// </summary>
        /// <returns>The base 64 bytes.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1011:Closing square brackets should be spaced correctly", Justification = "StyleCop does not handle nullable arrays correctly.")]
        public ReadOnlySpan<byte> GetDecodedBase64Bytes()
        {
            if (this.value is string value)
            {
                Span<byte> decoded = new byte[value.Length];
                if (!Convert.TryFromBase64String(value, decoded, out int bytesWritten))
                {
                    return ReadOnlySpan<byte>.Empty;
                }

                return decoded[..bytesWritten];
            }

            if (this.jsonElement.ValueKind == JsonValueKind.String)
            {
                if (this.jsonElement.TryGetBytesFromBase64(out byte[]? decoded))
                {
                    return decoded;
                }
            }

            return ReadOnlySpan<byte>.Empty;
        }

        /// <summary>
        /// Get a value indicating whether this instance has a Base64-encoded byte array.
        /// </summary>
        /// <returns>The base 64 bytes.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1011:Closing square brackets should be spaced correctly", Justification = "StyleCop does not handle nullable arrays correctly.")]
        public bool HasBase64Bytes()
        {
            if (this.value is string value)
            {
                Span<byte> decoded = stackalloc byte[value.Length];
                return Convert.TryFromBase64String(value, decoded, out _);
            }

            if (this.jsonElement.ValueKind == JsonValueKind.String)
            {
                return this.jsonElement.TryGetBytesFromBase64(out byte[]? _);
            }

            return false;
        }

        /// <inheritdoc/>
        public ValidationContext Validate(in ValidationContext validationContext, ValidationLevel level = ValidationLevel.Flag)
        {
            ValidationContext result = validationContext;

            JsonValueKind valueKind = this.ValueKind;

            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return result.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with contentEncoding 'base64' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return result.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with contentEncoding 'base64'.");
                }
                else
                {
                    return result.WithResult(isValid: false);
                }
            }

            if (!this.HasBase64Bytes())
            {
                // Valid, but we annotate
                if (level >= ValidationLevel.Detailed)
                {
                    return result.WithResult(isValid: true, $"Validation 8.3 contentEncoding - should have been a base64 encoded 'string'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return result.WithResult(isValid: true, "Validation 8.3 contentEncoding - should have been a base64 encoded 'string'.");
                }
                else
                {
                    return result.WithResult(isValid: true);
                }
            }
            else if (level == ValidationLevel.Verbose)
            {
                return result
                    .WithResult(isValid: true, "Validation 8.3 contentEncoding - was a base64 encoded 'string'.");
            }

            return result;
        }

        /// <inheritdoc/>
        public T As<T>()
            where T : struct, IJsonValue
        {
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

            return this.As<JsonBase64String, T>();
        }

        /// <summary>
        /// Gets the value as a string.
        /// </summary>
        /// <returns>The value as a string.</returns>
        public string GetString()
        {
            if (this.TryGetString(out string? result))
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
        public bool TryGetString([NotNullWhen(true)] out string? result)
        {
            if (this.value is string value)
            {
                result = value;
                return true;
            }

            if (this.jsonElement.ValueKind == JsonValueKind.String)
            {
                result = this.jsonElement.GetString();
                return result is not null;
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
                return value.AsSpan();
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

            return this.Equals((JsonBase64String)other.AsString());
        }

        /// <inheritdoc/>
        public bool Equals(JsonBase64String other)
        {
            if (this.IsNull() && other.IsNull())
            {
                return true;
            }

            if (other.ValueKind != this.ValueKind || this.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            return this.AsSpan().Equals(other.AsSpan(), StringComparison.Ordinal);
        }
    }
}
