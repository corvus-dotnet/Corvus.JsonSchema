// <copyright file="JsonObjectEnumerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Text.Json;

    /// <summary>
    /// An enumerator for a JSON object.
    /// </summary>
    public struct JsonObjectEnumerator : IEnumerable, IEnumerator, IEnumerable<Property>, IEnumerator<Property>, IDisposable
    {
        private readonly bool hasJsonElementEnumerator;
        private readonly bool hasDictionaryEnumerator;
        private JsonElement.ObjectEnumerator jsonElementEnumerator;
        private ImmutableDictionary<string, JsonAny>.Enumerator dictionaryEnumerator;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonObjectEnumerator"/> struct.
        /// </summary>
        /// <param name="jsonElement">The Json Element to enumerate.</param>
        public JsonObjectEnumerator(JsonElement jsonElement)
        {
            this.jsonElementEnumerator = jsonElement.EnumerateObject();
            this.hasJsonElementEnumerator = true;
            this.dictionaryEnumerator = default;
            this.hasDictionaryEnumerator = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonObjectEnumerator"/> struct.
        /// </summary>
        /// <param name="dictionary">The property dictionary to enumerate.</param>
        public JsonObjectEnumerator(ImmutableDictionary<string, JsonAny> dictionary)
        {
            this.jsonElementEnumerator = default;
            this.hasJsonElementEnumerator = false;
            this.dictionaryEnumerator = dictionary.GetEnumerator();
            this.hasDictionaryEnumerator = true;
        }

        /// <inheritdoc/>
        public Property Current
        {
            get
            {
                if (this.hasJsonElementEnumerator)
                {
                    return new Property(this.jsonElementEnumerator.Current);
                }

                if (this.hasDictionaryEnumerator)
                {
                    return new Property(this.dictionaryEnumerator.Current.Key, this.dictionaryEnumerator.Current.Value);
                }

                return default;
            }
        }

        /// <inheritdoc/>
        object IEnumerator.Current => this.Current;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.hasJsonElementEnumerator)
            {
                this.dictionaryEnumerator.Dispose();
            }

            if (this.hasJsonElementEnumerator)
            {
                this.jsonElementEnumerator.Dispose();
            }
        }

        /// <summary>
        /// Gets a new enumerator instance.
        /// </summary>
        /// <returns>A new enumerator instance.</returns>
        public JsonObjectEnumerator GetEnumerator()
        {
            JsonObjectEnumerator result = this;
            result.Reset();
            return result;
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <inheritdoc/>
        IEnumerator<Property> IEnumerable<Property>.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <inheritdoc/>
        public bool MoveNext()
        {
            if (this.hasJsonElementEnumerator)
            {
                return this.jsonElementEnumerator.MoveNext();
            }

            if (this.hasDictionaryEnumerator)
            {
                return this.dictionaryEnumerator.MoveNext();
            }

            return false;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            if (this.hasJsonElementEnumerator)
            {
                this.jsonElementEnumerator.Reset();
            }

            if (this.hasDictionaryEnumerator)
            {
                this.dictionaryEnumerator.Reset();
            }
        }
    }
}
