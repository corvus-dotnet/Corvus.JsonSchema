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
    private ImmutableList<JsonObjectProperty>.Enumerator propertyBackingEnumerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonObjectEnumerator"/> struct.
    /// </summary>
    /// <param name="jsonElement">The Json Element to enumerate.</param>
    public JsonObjectEnumerator(JsonElement jsonElement)
    {
        this.backing = Backing.JsonElementEnumerator;
        this.jsonElementEnumerator = jsonElement.EnumerateObject();
        this.propertyBackingEnumerator = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonObjectEnumerator"/> struct.
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
    public JsonObjectProperty Current
    {
        get
        {
            if ((this.backing & Backing.JsonElementEnumerator) != 0)
            {
                return new JsonObjectProperty(this.jsonElementEnumerator.Current);
            }

            if ((this.backing & Backing.PropertyBackingEnumerator) != 0)
            {
                return this.propertyBackingEnumerator.Current;
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
    public readonly JsonObjectEnumerator GetEnumerator()
    {
        JsonObjectEnumerator result = this;
        result.Reset();
        return result;
    }

    /// <inheritdoc/>
    readonly IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }

    /// <inheritdoc/>
    readonly IEnumerator<JsonObjectProperty> IEnumerable<JsonObjectProperty>.GetEnumerator()
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