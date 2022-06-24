// <copyright file="JsonContent.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;
    using System.Buffers;
    using System.Text;
    using System.Text.Json;
    using Corvus.Extensions;

    /// <summary>
    /// A JSON object.
    /// </summary>
    public readonly struct JsonContent : IJsonValue, IEquatable<JsonContent>
    {
        private readonly JsonElement jsonElement;
        private readonly string? value;
        private readonly JsonDocument? jsonDocumentValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonContent"/> struct.
        /// </summary>
        /// <param name="jsonElement">The JSON element from which to construct the object.</param>
        public JsonContent(JsonElement jsonElement)
        {
            this.jsonElement = jsonElement;
            this.value = default;
            this.jsonDocumentValue = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonContent"/> struct.
        /// </summary>
        /// <param name="value">The string value.</param>
        public JsonContent(JsonString value)
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
        /// Initializes a new instance of the <see cref="JsonContent"/> struct.
        /// </summary>
        /// <param name="value">The base64 encoded string value.</param>
        public JsonContent(string value)
        {
            this.jsonElement = default;
            this.value = value;
            this.jsonDocumentValue = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonContent"/> struct.
        /// </summary>
        /// <param name="value">The <see cref="JsonDocument"/> from which to construct the Base64 content.</param>
        /// <remarks>
        /// This does not take ownership of the document. The caller should dispose of it in the usual way, once its
        /// use is in this scope is complete.
        /// </remarks>
        public JsonContent(JsonDocument value)
        {
            // We both serialize it on creation...
            var abw = new ArrayBufferWriter<byte>();
            using var writer = new Utf8JsonWriter(abw);
            value.WriteTo(writer);
            this.value = abw.WrittenSpan.ToString();
            this.jsonElement = default;

            // ...and stash it away so we can return it quickly if required.
            this.jsonDocumentValue = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonContent"/> struct.
        /// </summary>
        /// <param name="value">The string value.</param>
        public JsonContent(ReadOnlySpan<char> value)
        {
            this.jsonElement = default;
            this.value = value.ToString();
            this.jsonDocumentValue = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonContent"/> struct.
        /// </summary>
        /// <param name="value">The utf8-encoded string value.</param>
        public JsonContent(ReadOnlySpan<byte> value)
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
        public static implicit operator JsonString(JsonContent value)
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
        public static implicit operator JsonContent(JsonString value)
        {
            return new JsonContent(value);
        }

        /// <summary>
        /// Implicit conversion to JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(JsonContent value)
        {
            return value.AsAny;
        }

        /// <summary>
        /// Implicit conversion from JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonContent(JsonAny value)
        {
            return value.AsString;
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonContent(string value)
        {
            return new JsonContent(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator string(JsonContent value)
        {
            return value.GetString();
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonContent(ReadOnlySpan<char> value)
        {
            return new JsonContent(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<char>(JsonContent value)
        {
            return value.AsSpan();
        }

        /// <summary>
        /// Conversion from utf8 bytes.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonContent(ReadOnlySpan<byte> value)
        {
            return new JsonContent(value);
        }

        /// <summary>
        /// Conversion to utf8 bytes.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<byte>(JsonContent value)
        {
            string result = value.GetString();
            return Encoding.UTF8.GetBytes(result);
        }

        /// <summary>
        /// Standard equality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are equal.</returns>
        public static bool operator ==(JsonContent lhs, JsonContent rhs)
        {
            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Standard inequality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are not equal.</returns>
        public static bool operator !=(JsonContent lhs, JsonContent rhs)
        {
            return !lhs.Equals(rhs);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.Serialize();
        }

        /// <summary>
        /// Try to get the JSON document from the content.
        /// </summary>
        /// <param name="result">A JSON document produced from the content, or null if the content did not represent a Base64 encoded JSON document.</param>
        /// <returns><c>True</c> if the document was parsed successfully.</returns>
        public EncodedContentMediaTypeParseStatus TryGetJsonDocument(out JsonDocument? result)
        {
            if (this.jsonDocumentValue is JsonDocument jdoc)
            {
                result = jdoc;
                return EncodedContentMediaTypeParseStatus.Success;
            }

            if (this.value is string value)
            {
                try
                {
                    ReadOnlySpan<byte> utf8Source = Encoding.UTF8.GetBytes(value);

                    int idx = utf8Source.IndexOf(JsonConstants.BackSlash);

                    ReadOnlySpan<byte> utf8Unescaped;
                    if (idx >= 0)
                    {
                        utf8Unescaped = JsonReaderHelper.GetUnescapedSpan(utf8Source, idx);
                    }
                    else
                    {
                        utf8Unescaped = utf8Source;
                    }

                    var reader2 = new Utf8JsonReader(utf8Unescaped);
                    if (JsonDocument.TryParseValue(ref reader2, out result))
                    {
                        return EncodedContentMediaTypeParseStatus.Success;
                    }
                }
                catch (Exception)
                {
                    // Fall through to the return...
                }

                result = null;
                return EncodedContentMediaTypeParseStatus.UnableToParseToMediaType;
            }

            if (this.jsonElement.ValueKind == JsonValueKind.String)
            {
                try
                {
#pragma warning disable CS8604 // Possible null reference argument - this is not possible if this.jsonElement.ValueKind == JsonValueKind.String as above.
                    ReadOnlySpan<byte> utf8Source = Encoding.UTF8.GetBytes(this.jsonElement.GetString());
#pragma warning restore CS8604 // Possible null reference argument.

                    int idx = utf8Source.IndexOf(JsonConstants.BackSlash);

                    ReadOnlySpan<byte> utf8Unescaped;
                    if (idx >= 0)
                    {
                        utf8Unescaped = JsonReaderHelper.GetUnescapedSpan(utf8Source, idx);
                    }
                    else
                    {
                        utf8Unescaped = utf8Source;
                    }

                    var reader2 = new Utf8JsonReader(utf8Unescaped);
                    if (JsonDocument.TryParseValue(ref reader2, out result))
                    {
                        return EncodedContentMediaTypeParseStatus.Success;
                    }
                }
                catch (Exception)
                {
                    // Fall through to the return...
                }

                result = default;
                return EncodedContentMediaTypeParseStatus.UnableToParseToMediaType;
            }

            result = null;
            return EncodedContentMediaTypeParseStatus.UnableToDecode;
        }

        /// <inheritdoc/>
        public ValidationContext Validate(in ValidationContext? validationContext = null, ValidationLevel level = ValidationLevel.Flag)
        {
            ValidationContext result = validationContext ?? ValidationContext.ValidContext;

            JsonValueKind valueKind = this.ValueKind;

            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return result.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' withcontentMediaType 'application/json' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return result.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with contentMediaType 'application/json'.");
                }
                else
                {
                    return result.WithResult(isValid: false);
                }
            }

            EncodedContentMediaTypeParseStatus status = this.TryGetJsonDocument(out JsonDocument? _);
            if (status == EncodedContentMediaTypeParseStatus.UnableToDecode)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return result.WithResult(isValid: false, $"Validation 8.3 contentEncoding - should have been a 'string' with contentMediaType 'application/json'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return result.WithResult(isValid: false, "Validation 8.3 contentEncoding - should have been a 'string' with contentMediaType 'application/json'.");
                }
                else
                {
                    return result.WithResult(isValid: false);
                }
            }
            else if (status == EncodedContentMediaTypeParseStatus.UnableToParseToMediaType)
            {
                // Should be Valid, but we just annotate.
                if (level >= ValidationLevel.Detailed)
                {
                    return result.WithResult(isValid: true, $"Validation 8.4 contentMediaType - should have been a 'string' with contentMediaType 'application/json'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return result.WithResult(isValid: true, "Validation 8.4 contentMediaType - should have been a 'string' with contentMediaType 'application/json'.");
                }
                else
                {
                    return result.WithResult(isValid: true);
                }
            }
            else if (level == ValidationLevel.Verbose)
            {
                return result
                    .WithResult(isValid: true, "Validation 8.4 contentMediaType - was a'string' with contentMediaType 'application/json'.");
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

            return this.As<JsonContent, T>();
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
                ReadOnlySpan<byte> utf8Source = Encoding.UTF8.GetBytes(value);

                int idx = utf8Source.IndexOf(JsonConstants.BackSlash);

                ReadOnlySpan<byte> utf8Unescaped;
                if (idx >= 0)
                {
                    utf8Unescaped = JsonReaderHelper.GetUnescapedSpan(utf8Source, idx);
                }
                else
                {
                    utf8Unescaped = utf8Source;
                }

                result = Encoding.UTF8.GetString(utf8Unescaped);
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
                ReadOnlySpan<byte> utf8Source = Encoding.UTF8.GetBytes(value);

                int idx = utf8Source.IndexOf(JsonConstants.BackSlash);

                ReadOnlySpan<byte> utf8Unescaped;
                if (idx >= 0)
                {
                    utf8Unescaped = JsonReaderHelper.GetUnescapedSpan(utf8Source, idx);
                }
                else
                {
                    utf8Unescaped = utf8Source;
                }

                Span<char> result = new char[Encoding.UTF8.GetMaxCharCount(utf8Unescaped.Length)];
                int written = Encoding.UTF8.GetChars(utf8Unescaped, result);
                return result[..written];
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

            return this.Equals((JsonContent)other.AsString());
        }

        /// <inheritdoc/>
        public bool Equals(JsonContent other)
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
