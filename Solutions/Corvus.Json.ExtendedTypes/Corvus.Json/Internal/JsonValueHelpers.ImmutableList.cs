// <copyright file="JsonValueHelpers.ImmutableList.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;

namespace Corvus.Json.Internal;

/// <summary>
///  Methods that help you implement <see cref="IJsonValue{T}"/>.
/// </summary>
public static partial class JsonValueHelpers
{
    /// <summary>
    /// Builds an immutable list from a JsonElement, removing the first instance of a specific value.
    /// </summary>
    /// <param name="jsonElement">The json element containing the source array.</param>
    /// <param name="item">The item to remove.</param>
    /// <returns>An immutable list with the item removed.</returns>
    /// <exception cref="InvalidOperationException">The element was not a list.</exception>
    /// <exception cref="IndexOutOfRangeException">The index was outside the bounds of the array.</exception>
    public static ImmutableList<JsonAny> GetImmutableListFromJsonElementWithout(in JsonElement jsonElement, in JsonAny item)
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        JsonElement.ArrayEnumerator enumerator = jsonElement.EnumerateArray();

        while (enumerator.MoveNext())
        {
            // enumerate until we find the item
            JsonAny current = new(enumerator.Current);
            if (current.Equals(item))
            {
                // Drop out of this iterator having found the first item that matches
                break;
            }

            builder.Add(new(enumerator.Current));
        }

        // Now add the rest of the items.
        while (enumerator.MoveNext())
        {
            builder.Add(new(enumerator.Current));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Builds an immutable list from a JsonElement, removing the first instance of a specific value, and replacing it with a new value.
    /// </summary>
    /// <param name="jsonElement">The json element containing the source array.</param>
    /// <param name="oldItem">The item to remove.</param>
    /// <param name="newItem">The item to insert.</param>
    /// <returns>An immutable list with the old item removed, and the new item inserted in its place.</returns>
    /// <exception cref="InvalidOperationException">The element was not a list.</exception>
    /// <exception cref="IndexOutOfRangeException">The index was outside the bounds of the array.</exception>
    public static ImmutableList<JsonAny> GetImmutableListFromJsonElementReplacing(in JsonElement jsonElement, in JsonAny oldItem, in JsonAny newItem)
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        JsonElement.ArrayEnumerator enumerator = jsonElement.EnumerateArray();

        while (enumerator.MoveNext())
        {
            // enumerate until we find the item
            JsonAny current = new(enumerator.Current);
            if (current.Equals(oldItem))
            {
                // Drop out of this iterator having found the first item that matches
                break;
            }

            builder.Add(current);
        }

        builder.Add(newItem);

        // Now add the rest of the items.
        while (enumerator.MoveNext())
        {
            builder.Add(new(enumerator.Current));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Builds an immutable list from a JsonElement, removing a range of values from the specified point.
    /// </summary>
    /// <param name="jsonElement">The json element containing the source array.</param>
    /// <param name="index">The index at which to remove the items.</param>
    /// <param name="count">The number of items to remove.</param>
    /// <returns>An immutable list with the items removed.</returns>
    /// <exception cref="InvalidOperationException">The element was not a list.</exception>
    /// <exception cref="IndexOutOfRangeException">The index was outside the bounds of the array.</exception>
    public static ImmutableList<JsonAny> GetImmutableListFromJsonElementWithoutRange(in JsonElement jsonElement, int index, int count)
    {
        // We come one off the count as we will advance the enumerator 1 automatically
        int end = index + count - 1;
        int length = jsonElement.GetArrayLength();

        if (index < 0 || count < 1 || end >= length)
        {
            throw new IndexOutOfRangeException();
        }

        int current = 0;
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        using JsonElement.ArrayEnumerator enumerator = jsonElement.EnumerateArray();

        while (enumerator.MoveNext() && current < index)
        {
            builder.Add(new(enumerator.Current));
            current++;
        }

        while (current < end && enumerator.MoveNext())
        {
            current++;
        }

        while (enumerator.MoveNext())
        {
            builder.Add(new(enumerator.Current));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Builds an immutable list from a JsonElement, setting the item at the specified index to the given value.
    /// </summary>
    /// <param name="jsonElement">The json element containing the source array.</param>
    /// <param name="index">The index at which to set the item.</param>
    /// <param name="value">The item to add.</param>
    /// <returns>An immutable list with the item set.</returns>
    /// <exception cref="InvalidOperationException">The element was not a list.</exception>
    /// <exception cref="IndexOutOfRangeException">The index was outside the bounds of the array.</exception>
    public static ImmutableList<JsonAny> GetImmutableListFromJsonElementSetting(in JsonElement jsonElement, int index, in JsonAny value)
    {
        if (index < 0 || index >= jsonElement.GetArrayLength())
        {
            throw new IndexOutOfRangeException();
        }

        int current = 0;
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        using JsonElement.ArrayEnumerator enumerator = jsonElement.EnumerateArray();

        // Add until we reach the index we're interested in
        while (current < index && enumerator.MoveNext())
        {
            builder.Add(new(enumerator.Current));
            current++;
        }

        // Add the new item instead of the current item
        enumerator.MoveNext();
        builder.Add(value);

        // And then we can carry on from there.
        while (enumerator.MoveNext())
        {
            builder.Add(new(enumerator.Current));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Builds an immutable list from a JsonElement, adding an item at the specified point.
    /// </summary>
    /// <param name="jsonElement">The json element containing the source array.</param>
    /// <param name="index">The index at which to add the item.</param>
    /// <param name="value">The item to add.</param>
    /// <returns>An immutable list with the item inserted.</returns>
    /// <exception cref="InvalidOperationException">The element was not a list.</exception>
    /// <exception cref="IndexOutOfRangeException">The index was outside the bounds of the array.</exception>
    public static ImmutableList<JsonAny> GetImmutableListFromJsonElementWith(in JsonElement jsonElement, int index, in JsonAny value)
    {
        if (index < 0 || index > jsonElement.GetArrayLength())
        {
            throw new IndexOutOfRangeException();
        }

        int current = 0;
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        using JsonElement.ArrayEnumerator enumerator = jsonElement.EnumerateArray();

        // Add until we reach the index we're interested in
        while (current < index && enumerator.MoveNext())
        {
            builder.Add(new(enumerator.Current));
            current++;
        }

        // Add the new item
        builder.Add(value);

        // And then we can carry on from there.
        while (enumerator.MoveNext())
        {
            builder.Add(new(enumerator.Current));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Builds an immutable list from a JsonElement, adding a range of values at the specified point.
    /// </summary>
    /// <typeparam name="TEnumerable">The type of the values to add.</typeparam>
    /// <param name="jsonElement">The json element containing the source array.</param>
    /// <param name="index">The index at which to add the items.</param>
    /// <param name="values">The items to add.</param>
    /// <returns>An immutable list with the items inserted.</returns>
    /// <exception cref="InvalidOperationException">The element was not a list.</exception>
    /// <exception cref="IndexOutOfRangeException">The index was outside the bounds of the array.</exception>
    public static ImmutableList<JsonAny> GetImmutableListFromJsonElementWith<TEnumerable>(in JsonElement jsonElement, int index, in TEnumerable values)
        where TEnumerable : IEnumerable<JsonAny>
    {
        if (index < 0 || index > jsonElement.GetArrayLength())
        {
            throw new IndexOutOfRangeException();
        }

        int current = 0;
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        using JsonElement.ArrayEnumerator enumerator = jsonElement.EnumerateArray();

        // Add until we reach the index
        while (current < index && enumerator.MoveNext())
        {
            builder.Add(new(enumerator.Current));
            current++;
        }

        foreach (JsonAny value in values)
        {
            builder.Add(value);
        }

        // And then we can carry on from there.
        while (enumerator.MoveNext())
        {
            builder.Add(new(enumerator.Current));
        }

        return builder.ToImmutable();
    }
}