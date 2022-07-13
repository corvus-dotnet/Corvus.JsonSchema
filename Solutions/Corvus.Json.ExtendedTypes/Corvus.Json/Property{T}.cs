// <copyright file="Property{T}.cs" company="Endjin Limited">
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
    /// <typeparam name="T">The type of the property.</typeparam>
    public readonly struct Property<T>
        where T : struct, IJsonValue
    {
        private readonly JsonProperty? jsonProperty;
        private readonly string? name;
        private readonly T? value;

        /// <summary>
        /// Initializes a new instance of the <see cref="Property{T}"/> struct.
        /// </summary>
        /// <param name="jsonProperty">The JSON property over which to construct this instance.</param>
        public Property(JsonProperty jsonProperty)
        {
            this.jsonProperty = jsonProperty;
            this.name = default;
            this.value = default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Property{T}"/> struct.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="value">The property value.</param>
        public Property(string name, T value)
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

                if (this.value is T value)
                {
                    return value.ValueKind;
                }

                return JsonValueKind.Undefined;
            }
        }

        /// <summary>
        /// Gets the value of the property.
        /// </summary>
        public T Value
        {
            get
            {
                if (this.jsonProperty is JsonProperty jsonProperty)
                {
                    return new JsonAny(jsonProperty.Value).As<T>();
                }

                if (this.value is T value)
                {
                    return value;
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
                return name.SequenceEqual(ourName.AsSpan());
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
    }
}
