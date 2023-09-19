// <copyright file="PropertyBackingExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;

namespace Corvus.Json.Internal;

/// <summary>
/// Object backing list extensions.
/// </summary>
public static class PropertyBackingExtensions
{
    /// <summary>
    /// Removes a property from the object.
    /// </summary>
    /// <param name="properties">The property collection to modify.</param>
    /// <param name="name">The name of the property to remove.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>The object property backing with the property removed.</returns>
    public static ImmutableList<JsonObjectProperty> SetItem(this ImmutableList<JsonObjectProperty> properties, in JsonPropertyName name, in JsonAny value)
    {
        for (int i = 0; i < properties.Count; i++)
        {
            if (properties[i].NameEquals(name))
            {
                return properties.SetItem(i, new JsonObjectProperty(name, value));
            }
        }

        return properties.Add(new JsonObjectProperty(name, value));
    }

    /// <summary>
    /// Removes a property from the object.
    /// </summary>
    /// <param name="properties">The property collection to modify.</param>
    /// <param name="name">The name of the property to remove.</param>
    /// <returns>The object property backing with the property removed.</returns>
    public static ImmutableList<JsonObjectProperty> Remove(this ImmutableList<JsonObjectProperty> properties, in JsonPropertyName name)
    {
        for (int i = 0; i < properties.Count; i++)
        {
            if (properties[i].NameEquals(name))
            {
                return properties.RemoveAt(i);
            }
        }

        return properties;
    }

    /// <summary>
    /// Removes a property from the object.
    /// </summary>
    /// <param name="properties">The property collection to modify.</param>
    /// <param name="name">The name of the property to remove.</param>
    /// <returns>The object property backing with the property removed.</returns>
    public static ImmutableList<JsonObjectProperty> Remove(this ImmutableList<JsonObjectProperty> properties, string name)
    {
        for (int i = 0; i < properties.Count; i++)
        {
            if (properties[i].NameEquals(name))
            {
                return properties.RemoveAt(i);
            }
        }

        return properties;
    }

    /// <summary>
    /// Removes a property from the object.
    /// </summary>
    /// <param name="properties">The property collection to modify.</param>
    /// <param name="name">The name of the property to remove.</param>
    /// <returns>The object property backing with the property removed.</returns>
    public static ImmutableList<JsonObjectProperty> Remove(this ImmutableList<JsonObjectProperty> properties, ReadOnlySpan<char> name)
    {
        for (int i = 0; i < properties.Count; i++)
        {
            if (properties[i].NameEquals(name))
            {
                return properties.RemoveAt(i);
            }
        }

        return properties;
    }

    /// <summary>
    /// Removes a property from the object.
    /// </summary>
    /// <param name="properties">The property collection to modify.</param>
    /// <param name="utf8Name">The name of the property to remove.</param>
    /// <returns>The object property backing with the property removed.</returns>
    public static ImmutableList<JsonObjectProperty> Remove(this ImmutableList<JsonObjectProperty> properties, ReadOnlySpan<byte> utf8Name)
    {
        for (int i = 0; i < properties.Count; i++)
        {
            if (properties[i].NameEquals(utf8Name))
            {
                return properties.RemoveAt(i);
            }
        }

        return properties;
    }

    /// <summary>
    /// Sets a property on the object.
    /// </summary>
    /// <param name="properties">The property collection to modify.</param>
    /// <param name="name">The name of the property to remove.</param>
    /// <param name="value">The value to set.</param>
    public static void SetItem(this ImmutableList<JsonObjectProperty>.Builder properties, in JsonPropertyName name, in JsonAny value)
    {
        for (int i = 0; i < properties.Count; i++)
        {
            if (properties[i].NameEquals(name))
            {
                properties.RemoveAt(i);
                properties.Insert(i, new JsonObjectProperty(name, value));
                return;
            }
        }

        properties.Add(new JsonObjectProperty(name, value));
    }

    /// <summary>
    /// Adds a property to the object.
    /// </summary>
    /// <param name="properties">The property collection to modify.</param>
    /// <param name="name">The name of the property to remove.</param>
    /// <param name="value">The value to set.</param>
    public static void Add(this ImmutableList<JsonObjectProperty>.Builder properties, in JsonPropertyName name, in JsonAny value)
    {
        properties.Add(new JsonObjectProperty(name, value));
    }

    /// <summary>
    /// Removes a property from the object.
    /// </summary>
    /// <param name="properties">The property collection to modify.</param>
    /// <param name="name">The name of the property to remove.</param>
    public static void Remove(this ImmutableList<JsonObjectProperty>.Builder properties, in JsonPropertyName name)
    {
        for (int i = 0; i < properties.Count; i++)
        {
            if (properties[i].NameEquals(name))
            {
                properties.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>
    /// Removes a property from the object.
    /// </summary>
    /// <param name="properties">The property collection to modify.</param>
    /// <param name="name">The name of the property to remove.</param>
    public static void Remove(this ImmutableList<JsonObjectProperty>.Builder properties, string name)
    {
        for (int i = 0; i < properties.Count; i++)
        {
            if (properties[i].NameEquals(name))
            {
                properties.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>
    /// Removes a property from the object.
    /// </summary>
    /// <param name="properties">The property collection to modify.</param>
    /// <param name="name">The name of the property to remove.</param>
    public static void Remove(this ImmutableList<JsonObjectProperty>.Builder properties, ReadOnlySpan<char> name)
    {
        for (int i = 0; i < properties.Count; i++)
        {
            if (properties[i].NameEquals(name))
            {
                properties.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>
    /// Removes a property from the object.
    /// </summary>
    /// <param name="properties">The property collection to modify.</param>
    /// <param name="utf8Name">The name of the property to remove.</param>
    public static void Remove(this ImmutableList<JsonObjectProperty>.Builder properties, ReadOnlySpan<byte> utf8Name)
    {
        for (int i = 0; i < properties.Count; i++)
        {
            if (properties[i].NameEquals(utf8Name))
            {
                properties.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>
    /// Tries to get the value of the property with the given name.
    /// </summary>
    /// <param name="properties">The properties from which to retrieve the value.</param>
    /// <param name="name">The name to test.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns><see langword="true"/> if the property name exists on the object.</returns>
    public static bool TryGetValue(this ImmutableList<JsonObjectProperty> properties, in JsonPropertyName name, out JsonAny value)
    {
        foreach (JsonObjectProperty property in properties)
        {
            if (property.NameEquals(name))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Gets a value indicating whether the property backing contains the given property.
    /// </summary>
    /// <param name="properties">The properties from which to retrieve the value.</param>
    /// <param name="name">The name to test.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns><see langword="true"/> if the property name exists on the object.</returns>
    public static bool TryGetValue(this ImmutableList<JsonObjectProperty> properties, ReadOnlySpan<char> name, out JsonAny value)
    {
        foreach (JsonObjectProperty property in properties)
        {
            if (property.NameEquals(name))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Gets a value indicating whether the property backing contains the given property.
    /// </summary>
    /// <param name="properties">The properties from which to retrieve the value.</param>
    /// <param name="name">The name to test.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns><see langword="true"/> if the property name exists on the object.</returns>
    public static bool TryGetValue(this ImmutableList<JsonObjectProperty> properties, ReadOnlySpan<byte> name, out JsonAny value)
    {
        foreach (JsonObjectProperty property in properties)
        {
            if (property.NameEquals(name))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Gets a value indicating whether the property backing contains the given property.
    /// </summary>
    /// <param name="properties">The properties from which to retrieve the value.</param>
    /// <param name="name">The name to test.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns><see langword="true"/> if the property name exists on the object.</returns>
    public static bool TryGetValue(this ImmutableList<JsonObjectProperty> properties, string name, out JsonAny value)
    {
        foreach (JsonObjectProperty property in properties)
        {
            if (property.NameEquals(name))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Gets a value indicating whether the property backing contains the given property.
    /// </summary>
    /// <param name="properties">The properties to test.</param>
    /// <param name="name">The name to test.</param>
    /// <returns><see langword="true"/> if the property name exists on the object.</returns>
    public static bool ContainsKey(this ImmutableList<JsonObjectProperty> properties, in JsonPropertyName name)
    {
        foreach (JsonObjectProperty property in properties)
        {
            if (property.Equals(name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets a value indicating whether the property backing contains the given property.
    /// </summary>
    /// <param name="properties">The properties to test.</param>
    /// <param name="name">The name to test.</param>
    /// <returns><see langword="true"/> if the property name exists on the object.</returns>
    public static bool ContainsKey(this ImmutableList<JsonObjectProperty> properties, ReadOnlySpan<char> name)
    {
        foreach (JsonObjectProperty property in properties)
        {
            if (property.NameEquals(name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets a value indicating whether the property backing contains the given property.
    /// </summary>
    /// <param name="properties">The properties to test.</param>
    /// <param name="name">The name to test.</param>
    /// <returns><see langword="true"/> if the property name exists on the object.</returns>
    public static bool ContainsKey(this ImmutableList<JsonObjectProperty> properties, ReadOnlySpan<byte> name)
    {
        foreach (JsonObjectProperty property in properties)
        {
            if (property.NameEquals(name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets a value indicating whether the property backing contains the given property.
    /// </summary>
    /// <param name="properties">The properties to test.</param>
    /// <param name="name">The name to test.</param>
    /// <returns><see langword="true"/> if the property name exists on the object.</returns>
    public static bool ContainsKey(this ImmutableList<JsonObjectProperty> properties, string name)
    {
        foreach (JsonObjectProperty property in properties)
        {
            if (property.NameEquals(name))
            {
                return true;
            }
        }

        return false;
    }
}