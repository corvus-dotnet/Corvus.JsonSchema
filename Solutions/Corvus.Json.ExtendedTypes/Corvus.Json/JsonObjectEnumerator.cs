// <copyright file="JsonObjectEnumerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections;
using System.Collections.Immutable;
using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// An enumerator for a JSON object.
/// </summary>
public struct JsonObjectEnumerator : IEnumerable, IEnumerator, IEnumerable<JsonObjectProperty>, IEnumerator<JsonObjectProperty>, IDisposable
{
    private readonly Backing backing;
    private JsonElement.ObjectEnumerator jsonElementEnumerator;
    private ImmutableDictionary<JsonPropertyName, JsonAny>.Enumerator dictionaryEnumerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonObjectEnumerator"/> struct.
    /// </summary>
    /// <param name="jsonElement">The Json Element to enumerate.</param>
    public JsonObjectEnumerator(JsonElement jsonElement)
    {
        this.backing = Backing.JsonElementEnumerator;
        this.jsonElementEnumerator = jsonElement.EnumerateObject();
        this.dictionaryEnumerator = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonObjectEnumerator"/> struct.
    /// </summary>
    /// <param name="dictionary">The property dictionary to enumerate.</param>
    public JsonObjectEnumerator(ImmutableDictionary<JsonPropertyName, JsonAny> dictionary)
    {
        this.backing = Backing.DictionaryEnumerator;
        this.jsonElementEnumerator = default;
        this.dictionaryEnumerator = dictionary.GetEnumerator();
    }

    [Flags]
    private enum Backing : byte
    {
        Undefined = 0b00,
        JsonElementEnumerator = 0b01,
        DictionaryEnumerator = 0b10,
    }

    /// <inheritdoc/>
    public JsonObjectProperty Current
    {
        get
        {
            if ((this.backing & Backing.JsonElementEnumerator) != 0)
            {
                return new JsonObjectProperty(this.jsonElementEnumerator.Current);
            }

            if ((this.backing & Backing.DictionaryEnumerator) != 0)
            {
                return new JsonObjectProperty(this.dictionaryEnumerator.Current.Key, this.dictionaryEnumerator.Current.Value);
            }

            return default;
        }
    }

    /// <inheritdoc/>
    object IEnumerator.Current => this.Current;

    /// <inheritdoc/>
    public void Dispose()
    {
        if ((this.backing & Backing.JsonElementEnumerator) != 0)
        {
            this.jsonElementEnumerator.Dispose();
        }

        if ((this.backing & Backing.DictionaryEnumerator) != 0)
        {
            this.dictionaryEnumerator.Dispose();
        }
    }

    /// <summary>
    /// Gets a new enumerator instance.
    /// </summary>
    /// <returns>A new enumerator instance.</returns>
    public readonly JsonObjectEnumerator GetEnumerator()
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
    IEnumerator<JsonObjectProperty> IEnumerable<JsonObjectProperty>.GetEnumerator()
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

        if ((this.backing & Backing.DictionaryEnumerator) != 0)
        {
            return this.dictionaryEnumerator.MoveNext();
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

        if ((this.backing & Backing.DictionaryEnumerator) != 0)
        {
            this.dictionaryEnumerator.Reset();
        }
    }
}