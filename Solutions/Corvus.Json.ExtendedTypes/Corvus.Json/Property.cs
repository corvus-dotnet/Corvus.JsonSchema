// <copyright file="Property.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// A property on a <see cref="JsonObject"/>.
    /// </summary>
    public readonly struct Property : IEquatable<Property>
    {
        private readonly JsonProperty? jsonProperty;
        private readonly string? name;
        private readonly JsonAny? value;

        /// <summary>
        /// Initializes a new instance of the <see cref="Property"/> struct.
        /// </summary>
        /// <param name="jsonProperty">The JSON property over which to construct this instance.</param>
        public Property(JsonProperty jsonProperty)
        {
            this.jsonProperty = jsonProperty;
            this.name = default;
            this.value = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Property"/> struct.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="value">The property value.</param>
        public Property(string name, JsonAny value)
        {
            this.jsonProperty = default;
            this.name = name;
            this.value = value;
        }

        /// <summary>
        /// Gets the value kind of the property value.
        /// </summary>
        public JsonValueKind ValueKind
        {
            get
            {
                if (this.jsonProperty is JsonProperty jsonProperty)
                {
                    return jsonProperty.Value.ValueKind;
                }

                if (this.value is JsonAny value)
                {
                    return value.ValueKind;
                }

                return JsonValueKind.Undefined;
            }
        }

        /// <summary>
        /// Gets the value of the property.
        /// </summary>
        public JsonAny Value
        {
            get
            {
                if (this.jsonProperty is JsonProperty jsonProperty)
                {
                    return new JsonAny(jsonProperty.Value);
                }

                if (this.value is JsonAny value)
                {
                    return value;
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the value as a <see cref="JsonNumber"/>.
        /// </summary>
        public JsonNumber ValueAsNumber
        {
            get
            {
                if (this.jsonProperty is JsonProperty jsonProperty)
                {
                    return new JsonNumber(jsonProperty.Value);
                }

                if (this.value is JsonAny value)
                {
                    return value.AsNumber;
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the value as a <see cref="JsonObject"/>.
        /// </summary>
        public JsonObject ValueAsObject
        {
            get
            {
                if (this.jsonProperty is JsonProperty jsonProperty)
                {
                    return new JsonObject(jsonProperty.Value);
                }

                if (this.value is JsonAny value)
                {
                    return value.AsObject;
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the value as a <see cref="JsonString"/>.
        /// </summary>
        public JsonString ValueAsString
        {
            get
            {
                if (this.jsonProperty is JsonProperty jsonProperty)
                {
                    return new JsonString(jsonProperty.Value);
                }

                if (this.value is JsonAny value)
                {
                    return value.AsString;
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the value as a <see cref="JsonArray"/>.
        /// </summary>
        public JsonArray ValueAsArray
        {
            get
            {
                if (this.jsonProperty is JsonProperty jsonProperty)
                {
                    return new JsonArray(jsonProperty.Value);
                }

                if (this.value is JsonAny value)
                {
                    return value.AsArray;
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the value as a <see cref="JsonBoolean"/>.
        /// </summary>
        public JsonBoolean ValueAsBoolean
        {
            get
            {
                if (this.jsonProperty is JsonProperty jsonProperty)
                {
                    return new JsonBoolean(jsonProperty.Value);
                }

                if (this.value is JsonAny value)
                {
                    return value.AsBoolean;
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the name of the property as a string.
        /// </summary>
        public string Name
        {
            get
            {
                if (this.jsonProperty is JsonProperty jsonProperty)
                {
                    return jsonProperty.Name;
                }

                if (this.name is string name)
                {
                    return name;
                }

                throw new InvalidOperationException("The property does not have a name.");
            }
        }

        /// <summary>
        /// Standard equality operator.
        /// </summary>
        /// <param name="left">The LHS of the comparison.</param>
        /// <param name="right">The RHS of the comparison.</param>
        /// <returns>True if they are equal.</returns>
        public static bool operator ==(Property left, Property right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Standard inequality operator.
        /// </summary>
        /// <param name="left">The LHS of the comparison.</param>
        /// <param name="right">The RHS of the comparison.</param>
        /// <returns>True if they are not equal.</returns>
        public static bool operator !=(Property left, Property right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Gets the value as an instance of the given type.
        /// </summary>
        /// <typeparam name="T">The type for which to get the value.</typeparam>
        /// <returns>An instance of the value as the given type.</returns>
        public T ValueAs<T>()
            where T : struct, IJsonValue
        {
            if (this.jsonProperty is JsonProperty jsonProperty)
            {
                return JsonValueExtensions.FromJsonElement<T>(jsonProperty.Value);
            }

            if (this.value is JsonAny value)
            {
                return value.As<T>();
            }

            return default;
        }

        /// <summary>
        /// Compares the specified UTF-8 encoded text to the name of this property.
        /// </summary>
        /// <param name="utf8Name">The name to match.</param>
        /// <returns><c>True</c> if the name matches.</returns>
        public bool NameEquals(ReadOnlySpan<byte> utf8Name)
        {
            if (this.jsonProperty is JsonProperty jsonProperty)
            {
                return jsonProperty.NameEquals(utf8Name);
            }

            if (this.name is string name)
            {
                Span<char> theirName = stackalloc char[Encoding.UTF8.GetMaxCharCount(utf8Name.Length)];
                int written = Encoding.UTF8.GetChars(utf8Name, theirName);
                return theirName[..written].SequenceEqual(name.AsSpan());
            }

            return false;
        }

        /// <summary>
        /// Compares the specified text to the name of this property.
        /// </summary>
        /// <param name="name">The name to match.</param>
        /// <returns><c>True</c> if the name matches.</returns>
        public bool NameEquals(ReadOnlySpan<char> name)
        {
            if (this.jsonProperty is JsonProperty jsonProperty)
            {
                return jsonProperty.NameEquals(name);
            }

            if (this.name is string ourName)
            {
                return ourName.AsSpan().Equals(name, StringComparison.Ordinal);
            }

            return false;
        }

        /// <summary>
        /// Compares the specified text to the name of this property.
        /// </summary>
        /// <param name="name">The name to match.</param>
        /// <returns><c>True</c> if the name matches.</returns>
        public bool NameEquals(string name)
        {
            if (this.jsonProperty is JsonProperty jsonProperty)
            {
                return jsonProperty.NameEquals(name);
            }

            if (this.name is string encodedName)
            {
                return encodedName.Equals(name, StringComparison.Ordinal);
            }

            return false;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is Property property && this.Equals(property);
        }

        /// <inheritdoc/>
        public bool Equals(Property other)
        {
            return this.Value.Equals(other.Value) &&
                   this.Name.Equals(other.Name, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(this.Value, this.Name);
        }
    }
}
