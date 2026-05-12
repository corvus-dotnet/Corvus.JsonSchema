// <copyright file="ReadOnlyDictionaryJsonObjectEnumerator{T}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections;
using System.Collections.Immutable;
using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// An enumerator for a JSON object as a KeyValuePair (for <see cref="IReadOnlyDictionary{TKey, TValue}"/> implementations).
/// </summary>
/// <typeparam name="T">The type of the properties in the object.</typeparam>
public struct ReadOnlyDictionaryJsonObjectEnumerator<T> : IEnumerable, IEnumerator, IEnumerable<KeyValuePair<JsonPropertyName, T>>, IEnumerator<KeyValuePair<JsonPropertyName, T>>, IDisposable
    where T : struct, IJsonValue<T>
{
    private readonly Backing backing;
    private JsonElement.ObjectEnumerator jsonElementEnumerator;
    private ImmutableList<JsonObjectProperty>.Enumerator propertyBackingEnumerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadOnlyDictionaryJsonObjectEnumerator{T}"/> struct.
    /// </summary>
    /// <param name="jsonElement">The Json Element to enumerate.</param>
    public ReadOnlyDictionaryJsonObjectEnumerator(JsonElement jsonElement)
    {
        this.backing = Backing.JsonElementEnumerator;
        this.jsonElementEnumerator = jsonElement.EnumerateObject();
        this.propertyBackingEnumerator = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadOnlyDictionaryJsonObjectEnumerator{T}"/> struct.
    /// </summary>
    /// <param name="dictionary">The property dictionary to enumerate.</param>
    public ReadOnlyDictionaryJsonObjectEnumerator(ImmutableList<JsonObjectProperty> dictionary)
    {
        this.backing = Backing.PropertyBackingEnumerator;
        this.jsonElementEnumerator = default;
        this.propertyBackingEnumerator = dictionary.GetEnumerator();
    }

    [Flags]
    private enum Backing : byte
    {
        Undefined = 0b00,
        JsonElementEnumerator = 0b01,
        PropertyBackingEnumerator = 0b10,
    }

    /// <inheritdoc/>
    public KeyValuePair<JsonPropertyName, T> Current
    {
        get
        {
            if ((this.backing & Backing.JsonElementEnumerator) != 0)
            {
                JsonObjectProperty<T> property = new(this.jsonElementEnumerator.Current);
                return new KeyValuePair<JsonPropertyName, T>(property.Name, property.Value);
            }

            if ((this.backing & Backing.PropertyBackingEnumerator) != 0)
            {
                return new KeyValuePair<JsonPropertyName, T>(this.propertyBackingEnumerator.Current.Name, this.propertyBackingEnumerator.Current.ValueAs<T>());
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

        if ((this.backing & Backing.PropertyBackingEnumerator) != 0)
        {
            this.propertyBackingEnumerator.Dispose();
        }
    }

    /// <summary>
    /// Gets a new enumerator instance.
    /// </summary>
    /// <returns>A new enumerator instance.</returns>
    public readonly ReadOnlyDictionaryJsonObjectEnumerator<T> GetEnumerator()
    {
        ReadOnlyDictionaryJsonObjectEnumerator<T> result = this;
        result.Reset();
        return result;
    }

    /// <inheritdoc/>
    readonly IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }

    /// <inheritdoc/>
    readonly IEnumerator<KeyValuePair<JsonPropertyName, T>> IEnumerable<KeyValuePair<JsonPropertyName, T>>.GetEnumerator()
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

        if ((this.backing & Backing.PropertyBackingEnumerator) != 0)
        {
            return this.propertyBackingEnumerator.MoveNext();
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

        if ((this.backing & Backing.PropertyBackingEnumerator) != 0)
        {
            this.propertyBackingEnumerator.Reset();
        }
    }
}