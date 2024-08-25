// <copyright file="JsonObjectEnumerator{T}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections;
using System.Collections.Immutable;
using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// An enumerator for a JSON object.
/// </summary>
/// <typeparam name="T">The type of the properties in the object.</typeparam>
public struct JsonObjectEnumerator<T> : IEnumerable, IEnumerator, IEnumerable<JsonObjectProperty<T>>, IEnumerator<JsonObjectProperty<T>>, IDisposable
    where T : struct, IJsonValue<T>
{
    private readonly Backing backing;
    private JsonElement.ObjectEnumerator jsonElementEnumerator;
    private ImmutableList<JsonObjectProperty>.Enumerator propertyBackingEnumerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonObjectEnumerator{T}"/> struct.
    /// </summary>
    /// <param name="jsonElement">The Json Element to enumerate.</param>
    public JsonObjectEnumerator(JsonElement jsonElement)
    {
        this.backing = Backing.JsonElementEnumerator;
        this.jsonElementEnumerator = jsonElement.EnumerateObject();
        this.propertyBackingEnumerator = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonObjectEnumerator{T}"/> struct.
    /// </summary>
    /// <param name="dictionary">The property dictionary to enumerate.</param>
    public JsonObjectEnumerator(ImmutableList<JsonObjectProperty> dictionary)
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
    public JsonObjectProperty<T> Current
    {
        get
        {
            if ((this.backing & Backing.JsonElementEnumerator) != 0)
            {
                return new JsonObjectProperty<T>(this.jsonElementEnumerator.Current);
            }

            if ((this.backing & Backing.PropertyBackingEnumerator) != 0)
            {
                return (JsonObjectProperty<T>)this.propertyBackingEnumerator.Current;
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
    public readonly JsonObjectEnumerator<T> GetEnumerator()
    {
        JsonObjectEnumerator<T> result = this;
        result.Reset();
        return result;
    }

    /// <inheritdoc/>
    readonly IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }

    /// <inheritdoc/>
    readonly IEnumerator<JsonObjectProperty<T>> IEnumerable<JsonObjectProperty<T>>.GetEnumerator()
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