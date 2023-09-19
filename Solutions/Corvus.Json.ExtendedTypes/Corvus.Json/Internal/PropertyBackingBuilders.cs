// <copyright file="PropertyBackingBuilders.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;

namespace Corvus.Json.Internal;

/// <summary>
/// Builders for object property backing from a JSON element.
/// </summary>
public static class PropertyBackingBuilders
{
    /// <summary>
    /// Builds an <see cref="ImmutableList{JsonObjectProperty}.Builder"/> from the object.
    /// </summary>
    /// <param name="jsonElementBacking">The JSON element backing the property.</param>
    /// <returns>An immutable dictionary builder of <see cref="JsonPropertyName"/> to <see cref="JsonAny"/>, built from the existing object.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    public static ImmutableList<JsonObjectProperty>.Builder GetPropertyBackingBuilder(in JsonElement jsonElementBacking)
    {
        if (jsonElementBacking.ValueKind == JsonValueKind.Object)
        {
            ImmutableList<JsonObjectProperty>.Builder builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
            foreach (JsonProperty property in jsonElementBacking.EnumerateObject())
            {
                builder.Add(new JsonObjectProperty(property));
            }

            return builder;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Builds an <see cref="ImmutableList{JsonObjectProperty}.Builder"/> from the object, without a specific property.
    /// </summary>
    /// <param name="jsonElementBacking">The JSON element backing the property.</param>
    /// <param name="name">The name of the property to remove.</param>
    /// <returns>An immutable dictionary builder of <see cref="JsonPropertyName"/> to <see cref="JsonAny"/>, built from the existing object.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    public static ImmutableList<JsonObjectProperty>.Builder GetPropertyBackingBuilderWithout(in JsonElement jsonElementBacking, in JsonPropertyName name)
    {
        if (jsonElementBacking.ValueKind == JsonValueKind.Object)
        {
            ImmutableList<JsonObjectProperty>.Builder builder = ImmutableList.CreateBuilder<JsonObjectProperty>();

            JsonElement.ObjectEnumerator enumerator = jsonElementBacking.EnumerateObject();

            while (enumerator.MoveNext())
            {
                // Use string for the current implementation of JsonPropertyName
                if (name.EqualsPropertyNameOf(enumerator.Current))
                {
                    // Skip this one.
                    break;
                }

                builder.Add(new JsonObjectProperty(enumerator.Current));
            }

            // We've found the property to eliminate, so we can work through the rest without checking names.
            while (enumerator.MoveNext())
            {
                builder.Add(new JsonObjectProperty(enumerator.Current));
            }

            return builder;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Builds an <see cref="ImmutableList{JsonObjectProperty}.Builder"/> from the object, without a specific property.
    /// </summary>
    /// <param name="jsonElementBacking">The JSON element backing the property.</param>
    /// <param name="name">The name of the property to remove.</param>
    /// <returns>An immutable dictionary builder of <see cref="JsonPropertyName"/> to <see cref="JsonAny"/>, built from the existing object.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    public static ImmutableList<JsonObjectProperty>.Builder GetPropertyBackingBuilderWithout(in JsonElement jsonElementBacking, ReadOnlySpan<char> name)
    {
        if (jsonElementBacking.ValueKind == JsonValueKind.Object)
        {
            ImmutableList<JsonObjectProperty>.Builder builder = ImmutableList.CreateBuilder<JsonObjectProperty>();

            JsonElement.ObjectEnumerator enumerator = jsonElementBacking.EnumerateObject();

            while (enumerator.MoveNext())
            {
                // Use string for the current implementation of JsonPropertyName
                if (enumerator.Current.NameEquals(name))
                {
                    // Skip this one.
                    break;
                }

                builder.Add(new JsonObjectProperty(enumerator.Current));
            }

            // We've found the property to eliminate, so we can work through the rest without checking names.
            while (enumerator.MoveNext())
            {
                builder.Add(new JsonObjectProperty(enumerator.Current));
            }

            return builder;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Builds an <see cref="ImmutableList{JsonObjectProperty}.Builder"/> from the object, without a specific property.
    /// </summary>
    /// <param name="jsonElementBacking">The JSON element backing the property.</param>
    /// <param name="utf8Name">The name of the property to remove.</param>
    /// <returns>An immutable dictionary builder of <see cref="JsonPropertyName"/> to <see cref="JsonAny"/>, built from the existing object.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    public static ImmutableList<JsonObjectProperty>.Builder GetPropertyBackingBuilderWithout(in JsonElement jsonElementBacking, ReadOnlySpan<byte> utf8Name)
    {
        if (jsonElementBacking.ValueKind == JsonValueKind.Object)
        {
            ImmutableList<JsonObjectProperty>.Builder builder = ImmutableList.CreateBuilder<JsonObjectProperty>();

            JsonElement.ObjectEnumerator enumerator = jsonElementBacking.EnumerateObject();

            while (enumerator.MoveNext())
            {
                // Use string for the current implementation of JsonPropertyName
                if (enumerator.Current.NameEquals(utf8Name))
                {
                    // Skip this one.
                    break;
                }

                builder.Add(new JsonObjectProperty(enumerator.Current));
            }

            // We've found the property to eliminate, so we can work through the rest without checking names.
            while (enumerator.MoveNext())
            {
                builder.Add(new JsonObjectProperty(enumerator.Current));
            }

            return builder;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Builds an <see cref="ImmutableList{JsonObjectProperty}.Builder"/> from the object, without a specific property.
    /// </summary>
    /// <param name="jsonElementBacking">The JSON element backing the property.</param>
    /// <param name="name">The name of the property to remove.</param>
    /// <returns>An immutable dictionary builder of <see cref="JsonPropertyName"/> to <see cref="JsonAny"/>, built from the existing object.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    public static ImmutableList<JsonObjectProperty>.Builder GetPropertyBackingBuilderWithout(in JsonElement jsonElementBacking, string name)
    {
        if (jsonElementBacking.ValueKind == JsonValueKind.Object)
        {
            ImmutableList<JsonObjectProperty>.Builder builder = ImmutableList.CreateBuilder<JsonObjectProperty>();

            JsonElement.ObjectEnumerator enumerator = jsonElementBacking.EnumerateObject();

            while (enumerator.MoveNext())
            {
                // Use string for the current implementation of JsonPropertyName
                if (enumerator.Current.NameEquals(name))
                {
                    // Skip this one.
                    break;
                }

                builder.Add(new JsonObjectProperty(enumerator.Current));
            }

            // We've found the property to eliminate, so we can work through the rest without checking names.
            while (enumerator.MoveNext())
            {
                builder.Add(new JsonObjectProperty(enumerator.Current));
            }

            return builder;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Builds an <see cref="ImmutableList{JsonObjectProperty}.Builder"/> from the object, replacing a specific property.
    /// </summary>
    /// <param name="jsonElementBacking">The JSON element backing the property.</param>
    /// <param name="name">The name of the property to replace.</param>
    /// <param name="value">The new value of the property.</param>
    /// <returns>An immutable dictionary builder of <see cref="JsonPropertyName"/> to <see cref="JsonAny"/>, built from the existing object.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    public static ImmutableList<JsonObjectProperty>.Builder GetPropertyBackingBuilderReplacing(in JsonElement jsonElementBacking, in JsonPropertyName name, in JsonAny value)
    {
        if (jsonElementBacking.ValueKind == JsonValueKind.Object)
        {
            ImmutableList<JsonObjectProperty>.Builder builder = ImmutableList.CreateBuilder<JsonObjectProperty>();

            JsonElement.ObjectEnumerator enumerator = jsonElementBacking.EnumerateObject();

            while (enumerator.MoveNext())
            {
                if (name.EqualsPropertyNameOf(enumerator.Current))
                {
                    // Replace the property with the new value
                    builder.Add(new JsonObjectProperty(name, value));
                    break;
                }

                builder.Add(new JsonObjectProperty(enumerator.Current));
            }

            // We've found the property to eliminate, so we can work through the rest without checking names.
            while (enumerator.MoveNext())
            {
                builder.Add(new JsonObjectProperty(enumerator.Current));
            }

            return builder;
        }

        throw new InvalidOperationException();
    }
}