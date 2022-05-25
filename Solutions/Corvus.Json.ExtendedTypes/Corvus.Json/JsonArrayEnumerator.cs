// <copyright file="JsonArrayEnumerator.cs" company="Endjin Limited">
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
    public struct JsonArrayEnumerator : IEnumerable, IEnumerator, IEnumerable<JsonAny>, IEnumerator<JsonAny>, IDisposable
    {
        private readonly bool hasJsonElementEnumerator;
        private readonly bool hasListEnumerator;
        private JsonElement.ArrayEnumerator jsonElementEnumerator;
        private ImmutableList<JsonAny>.Enumerator listEnumerator;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonArrayEnumerator"/> struct.
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
        /// Initializes a new instance of the <see cref="JsonArrayEnumerator"/> struct.
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
        public JsonAny Current
        {
            get
            {
                if (this.hasJsonElementEnumerator)
                {
                    return new JsonAny(this.jsonElementEnumerator.Current);
                }

                if (this.hasListEnumerator)
                {
                    return this.listEnumerator.Current;
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the current value as a string.
        /// </summary>
        public JsonString CurrentAsString
        {
            get
            {
                if (this.hasJsonElementEnumerator)
                {
                    return new JsonString(this.jsonElementEnumerator.Current);
                }

                if (this.hasListEnumerator)
                {
                    return this.listEnumerator.Current.AsString;
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the current value as a boolean.
        /// </summary>
        public JsonBoolean CurrentAsBoolean
        {
            get
            {
                if (this.hasJsonElementEnumerator)
                {
                    return new JsonBoolean(this.jsonElementEnumerator.Current);
                }

                if (this.hasListEnumerator)
                {
                    return this.listEnumerator.Current.AsBoolean;
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the current value as an object.
        /// </summary>
        public JsonObject CurrentAsObject
        {
            get
            {
                if (this.hasJsonElementEnumerator)
                {
                    return new JsonObject(this.jsonElementEnumerator.Current);
                }

                if (this.hasListEnumerator)
                {
                    return this.listEnumerator.Current.AsObject;
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the current value as an array.
        /// </summary>
        public JsonArray CurrentAsArray
        {
            get
            {
                if (this.hasJsonElementEnumerator)
                {
                    return new JsonArray(this.jsonElementEnumerator.Current);
                }

                if (this.hasListEnumerator)
                {
                    return this.listEnumerator.Current.AsArray;
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the current value as a number.
        /// </summary>
        public JsonNumber CurrentAsNumber
        {
            get
            {
                if (this.hasJsonElementEnumerator)
                {
                    return new JsonNumber(this.jsonElementEnumerator.Current);
                }

                if (this.hasListEnumerator)
                {
                    return this.listEnumerator.Current.AsNumber;
                }

                return default;
            }
        }

        /// <inheritdoc/>
        object IEnumerator.Current => this.Current;

        /// <summary>
        /// Gets the current value as the given type.
        /// </summary>
        /// <typeparam name="T">The type to get.</typeparam>
        /// <returns>The current value as an instance of the given type.</returns>
        public T CurrentAs<T>()
            where T : struct, IJsonValue
        {
            if (this.hasJsonElementEnumerator)
            {
                return JsonValueExtensions.FromJsonElement<T>(this.jsonElementEnumerator.Current);
            }

            if (this.hasListEnumerator)
            {
                return this.listEnumerator.Current.As<T>();
            }

            return default;
        }

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
        public JsonArrayEnumerator GetEnumerator()
        {
            JsonArrayEnumerator result = this;
            result.Reset();
            return result;
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <inheritdoc/>
        IEnumerator<JsonAny> IEnumerable<JsonAny>.GetEnumerator()
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
