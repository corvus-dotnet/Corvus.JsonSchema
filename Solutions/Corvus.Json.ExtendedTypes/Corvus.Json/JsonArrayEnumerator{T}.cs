// <copyright file="JsonArrayEnumerator{T}.cs" company="Endjin Limited">
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
    /// An enumerator for a JSON array.
    /// </summary>
    /// <typeparam name="T">The type of the items in the array.</typeparam>
    public struct JsonArrayEnumerator<T> : IEnumerable, IEnumerator, IEnumerable<T>, IEnumerator<T>, IDisposable
        where T : struct, IJsonValue
    {
        private readonly bool hasJsonElementEnumerator;
        private readonly bool hasListEnumerator;
        private JsonElement.ArrayEnumerator jsonElementEnumerator;
        private ImmutableList<JsonAny>.Enumerator listEnumerator;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonArrayEnumerator{T}"/> struct.
        /// </summary>
        /// <param name="jsonElement">The Json Element to enumerate.</param>
        public JsonArrayEnumerator(JsonElement jsonElement)
        {
            this.jsonElementEnumerator = jsonElement.EnumerateArray();
            this.hasJsonElementEnumerator = true;
            this.listEnumerator = default;
            this.hasListEnumerator = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonArrayEnumerator{T}"/> struct.
        /// </summary>
        /// <param name="list">The property list to enumerate.</param>
        public JsonArrayEnumerator(ImmutableList<JsonAny> list)
        {
            this.jsonElementEnumerator = default;
            this.hasJsonElementEnumerator = false;
            this.listEnumerator = list.GetEnumerator();
            this.hasListEnumerator = true;
        }

        /// <inheritdoc/>
        public T Current
        {
            get
            {
                if (this.hasJsonElementEnumerator)
                {
                    return new JsonAny(this.jsonElementEnumerator.Current).As<T>();
                }

                if (this.hasListEnumerator)
                {
                    return this.listEnumerator.Current.As<T>();
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
                this.listEnumerator.Dispose();
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
        public JsonArrayEnumerator<T> GetEnumerator()
        {
            JsonArrayEnumerator<T> result = this;
            result.Reset();
            return result;
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <inheritdoc/>
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
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

            if (this.hasListEnumerator)
            {
                return this.listEnumerator.MoveNext();
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

            if (this.hasListEnumerator)
            {
                this.listEnumerator.Reset();
            }
        }
    }
}
