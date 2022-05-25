// <copyright file="JsonUri.cs" company="Endjin Limited">
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
    public readonly struct JsonUri : IJsonValue, IEquatable<JsonUri>
    {
        private static readonly Uri EmptyUri = new (string.Empty, UriKind.RelativeOrAbsolute);

        private readonly JsonElement jsonElement;
        private readonly string? value;
        private readonly Uri? localUriValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonUri"/> struct.
        /// </summary>
        /// <param name="jsonElement">The JSON element from which to construct the object.</param>
        public JsonUri(JsonElement jsonElement)
        {
            this.jsonElement = jsonElement;
            this.value = default;
            this.localUriValue = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonUri"/> struct.
        /// </summary>
        /// <param name="value">The base64 encoded string value.</param>
        public JsonUri(JsonString value)
        {
            if (value.HasJsonElement)
            {
                this.jsonElement = value.AsJsonElement;
                this.value = default;
                this.localUriValue = default;
            }
            else
            {
                this.jsonElement = default;
                this.value = value;
                this.localUriValue = default;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonUri"/> struct.
        /// </summary>
        /// <param name="value">The base64 encoded string value.</param>
        public JsonUri(string value)
        {
            this.jsonElement = default;
            this.value = value;
            this.localUriValue = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonUri"/> struct.
        /// </summary>
        /// <param name="value">The base64 encoded string value.</param>
        public JsonUri(ReadOnlySpan<char> value)
        {
            this.jsonElement = default;
            this.value = value.ToString();
            this.localUriValue = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonUri"/> struct.
        /// </summary>
        /// <param name="value">The utf8-encoded string value.</param>
        public JsonUri(ReadOnlySpan<byte> value)
        {
            this.jsonElement = default;
            this.value = Encoding.UTF8.GetString(value);
            this.localUriValue = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonUri"/> struct.
        /// </summary>
        /// <param name="value">The Uri value.</param>
        public JsonUri(Uri value)
        {
            this.jsonElement = default;
            this.value = FormatUri(value);
            this.localUriValue = value;
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
        public static implicit operator JsonString(JsonUri value)
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
        public static implicit operator JsonUri(JsonString value)
        {
            return new JsonUri(value);
        }

        /// <summary>
        /// Implicit conversion to Uri.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator Uri(JsonUri value)
        {
            return value.GetUri();
        }

        /// <summary>
        /// Implicit conversion from Uri.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonUri(Uri value)
        {
            return new JsonUri(value);
        }

        /// <summary>
        /// Implicit conversion to JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(JsonUri value)
        {
            return value.AsAny;
        }

        /// <summary>
        /// Implicit conversion from JsonAny.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonUri(JsonAny value)
        {
            return value.AsString;
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonUri(string value)
        {
            return new JsonUri(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator string(JsonUri value)
        {
            return value.GetString();
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonUri(ReadOnlySpan<char> value)
        {
            return new JsonUri(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<char>(JsonUri value)
        {
            return value.AsSpan();
        }

        /// <summary>
        /// Conversion from utf8 bytes.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonUri(ReadOnlySpan<byte> value)
        {
            return new JsonUri(value);
        }

        /// <summary>
        /// Conversion to utf8 bytes.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<byte>(JsonUri value)
        {
            return Encoding.UTF8.GetBytes(value);
        }

        /// <summary>
        /// Standard equality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are equal.</returns>
        public static bool operator ==(JsonUri lhs, JsonUri rhs)
        {
            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Standard inequality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are not equal.</returns>
        public static bool operator !=(JsonUri lhs, JsonUri rhs)
        {
            return !lhs.Equals(rhs);
        }

        /// <inheritdoc/>
        public ValidationContext Validate(in ValidationContext? validationContext = null, ValidationLevel level = ValidationLevel.Flag)
        {
            ValidationContext result = validationContext ?? ValidationContext.ValidContext;

            return Json.Validate.TypeUri(this, result, level);
        }

        /// <inheritdoc/>
        public T As<T>()
            where T : struct, IJsonValue
        {
            if (typeof(T) == typeof(JsonUri))
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

            return this.As<JsonUri, T>();
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
        /// Gets the value as a Guid.
        /// </summary>
        /// <returns>The value as a Guid.</returns>
        public Uri GetUri()
        {
            if (this.TryGetUri(out Uri? result))
            {
                return result;
            }

            return EmptyUri;
        }

        /// <summary>
        /// Try to get the date value.
        /// </summary>
        /// <param name="result">The date value.</param>
        /// <returns><c>True</c> if it was possible to get a date value from the instance.</returns>
        public bool TryGetUri([NotNullWhen(true)] out Uri? result)
        {
            if (this.localUriValue is Uri localUri)
            {
                result = localUri;
                return true;
            }

            if (this.value is string value)
            {
                return TryParseUri(value, out result);
            }

            if (this.jsonElement.ValueKind == JsonValueKind.String)
            {
                string? str = this.jsonElement.GetString();
                return TryParseUri(str!, out result);
            }

            result = EmptyUri;
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

            return this.AsString().Equals(other.AsString());
        }

        /// <inheritdoc/>
        public bool Equals(JsonUri other)
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

        private static string FormatUri(Uri value)
        {
            return value.OriginalString;
        }

        private static bool TryParseUri(string text, [NotNullWhen(true)] out Uri? value)
        {
            // Uri.TryCreate considers full-qualified file paths to be acceptable as absolute Uris.
            // This means that on Linux "/abc" is considered an acceptable absolute Uri! (This is
            // conceptually equivalent to "C:\abc" being an absolute Uri on Windows, but it's more
            // of a problem because a lot of relative Uris of the kind you come across on the web
            // look exactly like Unix file paths.)
            // https://github.com/dotnet/runtime/issues/22718
            // However, this only needs to be a problem if you insist that the Uri is absolute.
            // If you accept either absolute or relative Uris, it will intepret "/abc" as a
            // relative Uri on either Windows or Linux. It only interprets it as an absolute Uri
            // if you pass UriKind.Absolute when parsing.
            // This is why we take the peculiar-looking step of passing UriKind.RelativeOrAbsolute
            // and then rejecting relative Uris. This causes this method to reject "/abc" on all
            // platforms. Back when we passed UriKind.Absolute, this code incorrectly accepted
            // "abc".
            return Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out value) &&
                value.IsAbsoluteUri;
        }
    }
}
