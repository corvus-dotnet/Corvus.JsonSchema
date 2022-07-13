// <copyright file="JsonBase64Content.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;
    using System.Buffers;
    using System.Diagnostics.CodeAnalysis;
    using System.Text;
    using System.Text.Json;
    using Corvus.Extensions;

    /// <summary>
    /// A JSON object.
    /// </summary>
    public readonly struct JsonBase64Content : IJsonValue, IEquatable<JsonBase64Content>
    {
        private readonly JsonElement jsonElement;
        private readonly string? value;
        private readonly JsonDocument? jsonDocumentValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonBase64Content"/> struct.
        /// </summary>
        /// <param name="jsonElement">The JSON element from which to construct the object.</param>
        public JsonBase64Content(JsonElement jsonElement)
        {
            this.jsonElement = jsonElement;
            this.value = default;
            this.jsonDocumentValue = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonBase64Content"/> struct.
        /// </summary>
        /// <param name="value">The string value.</param>
        public JsonBase64Content(JsonString value)
        {
            if (value.HasJsonElement)
            {
                this.jsonElement = value.AsJsonElement;
                this.value = default;
                this.jsonDocumentValue = default;
            }
            else
            {
                this.jsonElement = default;
                this.value = value;
                this.jsonDocumentValue = default;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonBase64Content"/> struct.
        /// </summary>
        /// <param name="value">The base64 encoded string value.</param>
        public JsonBase64Content(string value)
        {
            this.jsonElement = default;
            this.value = value;
            this.jsonDocumentValue = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonBase64Content"/> struct.
        /// </summary>
        /// <param name="value">The <see cref="JsonDocument"/> from which to construct the Base64 content.</param>
        /// <remarks>
        /// This does not take ownership of the document. The caller should dispose of it in the usual way, once its
        /// use is in this scope is complete.
        /// </remarks>
        public JsonBase64Content(JsonDocument value)
        {
            // We both serialize it on creation...
            var abw = new ArrayBufferWriter<byte>();
            using var writer = new Utf8JsonWriter(abw);
            value.WriteTo(writer);
            this.value = Convert.ToBase64String(abw.WrittenSpan);
            this.jsonElement = default;

            // ...and stash it away so we can return it quickly if required.
            this.jsonDocumentValue = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonBase64Content"/> struct.
        /// </summary>
        /// <param name="value">The string value.</param>
        public JsonBase64Content(ReadOnlySpan<char> value)
        {
            this.jsonElement = default;
            this.value = value.ToString();
            this.jsonDocumentValue = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonBase64Content"/> struct.
        /// </summary>
        /// <param name="value">The utf8-encoded string value.</param>
        public JsonBase64Content(ReadOnlySpan<byte> value)
        {
            this.jsonElement = default;
            this.value = Encoding.UTF8.GetString(value);
            this.jsonDocumentValue = default;
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
        public static implicit operator JsonString(JsonBase64Content value)
        {
            if (value.value is string str)
            {
                return new JsonString(str);
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
        public static implicit operator JsonBase64Content(JsonString value)
        {
            return new JsonBase64Content(value);
        }

        /// <summary>
        /// Implicit conversion to JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(JsonBase64Content value)
        {
            return value.AsAny;
        }

        /// <summary>
        /// Implicit conversion from JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonBase64Content(JsonAny value)
        {
            return value.AsString;
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonBase64Content(string value)
        {
            return new JsonBase64Content(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator string(JsonBase64Content value)
        {
            return value.GetString();
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonBase64Content(ReadOnlySpan<char> value)
        {
            return new JsonBase64Content(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<char>(JsonBase64Content value)
        {
            return value.AsSpan();
        }

        /// <summary>
        /// Conversion from utf8 bytes.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonBase64Content(ReadOnlySpan<byte> value)
        {
            return new JsonBase64Content(value);
        }

        /// <summary>
        /// Conversion to utf8 bytes.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<byte>(JsonBase64Content value)
        {
            return value.GetUtf8BytesBase64EncodedString();
        }

        /// <summary>
        /// Standard equality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are equal.</returns>
        public static bool operator ==(JsonBase64Content lhs, JsonBase64Content rhs)
        {
            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Standard inequality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are not equal.</returns>
        public static bool operator !=(JsonBase64Content lhs, JsonBase64Content rhs)
        {
            return !lhs.Equals(rhs);
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
        /// Get the base64 encoded string.
        /// </summary>
        /// <returns>The base 64 encoded string.</returns>
        public ReadOnlySpan<byte> GetUtf8BytesBase64EncodedString()
        {
            if (this.value is string value)
            {
                return Encoding.UTF8.GetBytes(value);
            }

            if (this.jsonElement.ValueKind == JsonValueKind.String)
            {
                if (this.jsonElement.GetString() is string decoded)
                {
                    return Encoding.UTF8.GetBytes(decoded);
                }
            }

            return ReadOnlySpan<byte>.Empty;
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
                Span<byte> result = new byte[value.Length];
                if (!Convert.TryFromBase64String(value, result, out int bytesWritten))
                {
                    return ReadOnlySpan<byte>.Empty;
                }

                return result[..bytesWritten];
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
        /// Try to get the JSON document from the content.
        /// </summary>
        /// <param name="result">A JSON document produced from the content, or null if the content did not represent a Base64 encoded JSON document.</param>
        /// <returns><c>True</c> if the document was parsed successfully.</returns>
        [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1011:Closing square brackets should be spaced correctly", Justification = "Stylecop does not yet support nullable array annotations.")]
        public EncodedContentMediaTypeParseStatus TryGetJsonDocument(out JsonDocument? result)
        {
            if (this.jsonDocumentValue is JsonDocument jdoc)
            {
                result = jdoc;
                return EncodedContentMediaTypeParseStatus.Success;
            }

            if (this.value is string value)
            {
                Span<byte> decoded1 = stackalloc byte[value.Length];
                if (Convert.TryFromBase64String(value, decoded1, out int bytesWritten))
                {
                    var reader = new Utf8JsonReader(decoded1);
                    if (JsonDocument.TryParseValue(ref reader, out result))
                    {
                        return EncodedContentMediaTypeParseStatus.Success;
                    }
                }

                result = default;
                return EncodedContentMediaTypeParseStatus.UnableToParseToMediaType;
            }

            if (this.jsonElement.ValueKind == JsonValueKind.String &&
                this.jsonElement.TryGetBytesFromBase64(out byte[]? decoded))
            {
                var reader = new Utf8JsonReader(decoded);
#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception.
                try
                {
                    if (JsonDocument.TryParseValue(ref reader, out result))
                    {
                        return EncodedContentMediaTypeParseStatus.Success;
                    }
                }
                catch (Exception)
                {
                    // Fall through to the return...
                }
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception.

                result = default;
                return EncodedContentMediaTypeParseStatus.UnableToParseToMediaType;
            }

            result = null;
            return EncodedContentMediaTypeParseStatus.UnableToDecode;
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
                    return result.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with contentEncoding 'base64' and contentMediaType 'application/json' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return result.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with contentEncoding 'base64' and contentMediaType 'application/json'.");
                }
                else
                {
                    return result.WithResult(isValid: false);
                }
            }

            EncodedContentMediaTypeParseStatus status = this.TryGetJsonDocument(out JsonDocument? _);
            if (status == EncodedContentMediaTypeParseStatus.UnableToDecode)
            {
                // Is valid, but we annotate
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
            else if (status == EncodedContentMediaTypeParseStatus.UnableToParseToMediaType)
            {
                // Validates true, but we will annotate ite
                if (level >= ValidationLevel.Detailed)
                {
                    return result.WithResult(isValid: true, $"Validation 8.4 contentMediaType - valid, but should have been a base64 encoded 'string' of type 'application/json'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return result.WithResult(isValid: true, "Validation 8.4 contentMediaType - valid, but should have been a base64 encoded 'string' of type 'application/json'.");
                }
                else
                {
                    return result.WithResult(isValid: true);
                }
            }
            else if (level == ValidationLevel.Verbose)
            {
                return result
                    .WithResult(isValid: true, "Validation 8.3 contentEncoding - was a base64 encoded 'string'.")
                    .WithResult(isValid: true, "Validation 8.4 contentMediaType - was a base64 encoded 'string' of type 'application/json'.");
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

            if (typeof(T) == typeof(JsonBase64String))
            {
                if (this.value is string value)
                {
                    return CastTo<T>.From(new JsonBase64String(value));
                }
                else
                {
                    return CastTo<T>.From(new JsonBase64String(this.jsonElement));
                }
            }

            return this.As<JsonBase64Content, T>();
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

            return this.Equals((JsonBase64Content)other.AsString());
        }

        /// <inheritdoc/>
        public bool Equals(JsonBase64Content other)
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
