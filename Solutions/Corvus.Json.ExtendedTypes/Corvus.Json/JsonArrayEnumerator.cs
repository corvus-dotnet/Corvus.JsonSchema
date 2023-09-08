// <copyright file="JsonArrayEnumerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections;
using System.Collections.Immutable;
using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// An enumerator for a JSON array.
/// </summary>
public struct JsonArrayEnumerator : IEnumerable, IEnumerator, IEnumerable<JsonAny>, IEnumerator<JsonAny>, IDisposable
{
    private readonly Backing backing;
    private JsonElement.ArrayEnumerator jsonElementEnumerator;
    private ImmutableList<JsonAny>.Enumerator listEnumerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonArrayEnumerator"/> struct.
    /// </summary>
    /// <param name="jsonElement">The Json Element to enumerate.</param>
    public JsonArrayEnumerator(JsonElement jsonElement)
    {
        this.jsonElementEnumerator = jsonElement.EnumerateArray();
        this.listEnumerator = default;
        this.backing = Backing.JsonElementEnumerator;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonArrayEnumerator"/> struct.
    /// </summary>
    /// <param name="list">The property list to enumerate.</param>
    public JsonArrayEnumerator(ImmutableList<JsonAny> list)
    {
        this.jsonElementEnumerator = default;
        this.listEnumerator = list.GetEnumerator();
        this.backing = Backing.ListEnumerator;
    }

    [Flags]
    private enum Backing : byte
    {
        Undefined = 0b00,
        JsonElementEnumerator = 0b01,
        ListEnumerator = 0b10,
    }

    /// <inheritdoc/>
    public JsonAny Current
    {
        get
        {
            if ((this.backing & Backing.JsonElementEnumerator) != 0)
            {
                return new JsonAny(this.jsonElementEnumerator.Current);
            }

            if ((this.backing & Backing.ListEnumerator) != 0)
            {
                return this.listEnumerator.Current;
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
        where T : struct, IJsonValue<T>
    {
        if ((this.backing & Backing.JsonElementEnumerator) != 0)
        {
            return T.FromJson(this.jsonElementEnumerator.Current);
        }

        if ((this.backing & Backing.ListEnumerator) != 0)
        {
            return this.listEnumerator.Current.As<T>();
        }

        return default;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if ((this.backing & Backing.JsonElementEnumerator) != 0)
        {
            this.jsonElementEnumerator.Dispose();
        }

        if ((this.backing & Backing.ListEnumerator) != 0)
        {
            this.listEnumerator.Dispose();
        }
    }

    /// <summary>
    /// Gets a new enumerator instance.
    /// </summary>
    /// <returns>A new enumerator instance.</returns>
    public readonly JsonArrayEnumerator GetEnumerator()
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
        if ((this.backing & Backing.JsonElementEnumerator) != 0)
        {
            return this.jsonElementEnumerator.MoveNext();
        }

        if ((this.backing & Backing.ListEnumerator) != 0)
        {
            return this.listEnumerator.MoveNext();
        }

        return false;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        if ((this.backing & Backing.JsonElementEnumerator) != 0)
        {
            this.jsonElementEnumerator.Reset();
        }

        if ((this.backing & Backing.ListEnumerator) != 0)
        {
            this.listEnumerator.Reset();
        }
    }
}