// <copyright file="IJsonArray{T}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;

namespace Corvus.Json;

/// <summary>
/// A JSON array value.
/// </summary>
/// <typeparam name="T">The type implementing the interface.</typeparam>
public interface IJsonArray<T> : IJsonValue<T>
    where T : struct, IJsonArray<T>
{
    /// <summary>
    /// Gets the item at the given index.
    /// </summary>
    /// <param name="index">The index at which to retrieve the item.</param>
    /// <returns>The item at the given index.</returns>
    /// <exception cref="IndexOutOfRangeException">The index was outside the bounds of the array.</exception>
    /// <exception cref="InvalidOperationException">The value is not an array.</exception>
    JsonAny this[int index] { get; }

    /// <summary>
    /// Conversion from immutable list.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    static abstract implicit operator T(ImmutableList<JsonAny> value);

    /// <summary>s
    /// Conversion to immutable list.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a string.</exception>
    static abstract implicit operator ImmutableList<JsonAny>(T value);

    /// <summary>
    /// Gets the length of the IJsonArray.
    /// </summary>
    /// <returns>The length of the array.</returns>
    /// <exception cref="InvalidOperationException">The value is not an array.</exception>
    int GetArrayLength();

    /// <summary>
    /// Enumerate the array.
    /// </summary>
    /// <returns>An enumerator for the array.</returns>
    /// <exception cref="InvalidOperationException">The value is not an array.</exception>
    JsonArrayEnumerator EnumerateArray();

    /// <summary>
    /// Enumerate the array as a specific type.
    /// </summary>
    /// <typeparam name="TItem">The type of the items in the array.</typeparam>
    /// <returns>An enumerator for the array.</returns>
    /// <exception cref="InvalidOperationException">The value is not an array.</exception>
    JsonArrayEnumerator<TItem> EnumerateArray<TItem>()
        where TItem : struct, IJsonValue<TItem>;

    /// <summary>
    /// Construct an ImmutableList from the items.
    /// </summary>
    /// <returns>An immutable list of items constructed from the array.</returns>
    ImmutableList<JsonAny> AsImmutableList();

    /// <summary>
    /// Construct an ImmutableList.Builder from the items.
    /// </summary>
    /// <returns>An immutable list builder of items constructed from the array.</returns>
    ImmutableList<JsonAny>.Builder AsImmutableListBuilder();

    /// <summary>
    /// Add an item to the array.
    /// </summary>
    /// <param name="item1">The item to add.</param>
    /// <returns>An instance of the array with the item added.</returns>
    /// <exception cref="InvalidOperationException">The value was not an array.</exception>
    T Add(in JsonAny item1);

    /// <summary>
    /// Add a set of items to the array.
    /// </summary>
    /// <param name="items">The items to add.</param>
    /// <returns>An instance of the array with the items added.</returns>
    /// <exception cref="InvalidOperationException">The value was not an array.</exception>
    T Add(params JsonAny[] items);

    /// <summary>
    /// Add a set of items to the array.
    /// </summary>
    /// <param name="items">The items to add.</param>
    /// <returns>An instance of the array with the items added.</returns>
    /// <exception cref="InvalidOperationException">The value was not an array.</exception>
    T AddRange(IEnumerable<JsonAny> items);

    /// <summary>
    /// Add a set of items to the array.
    /// </summary>
    /// <typeparam name="TItem">The type of the items to add.</typeparam>
    /// <param name="items">The items to add.</param>
    /// <returns>An instance of the array with the items added.</returns>
    /// <exception cref="InvalidOperationException">The value was not an array.</exception>
    T AddRange<TItem>(IEnumerable<TItem> items)
        where TItem : struct, IJsonValue<TItem>;

    /// <summary>
    /// Add a set of items to the array.
    /// </summary>
    /// <typeparam name="TArray">The type of the array containing the items to add.</typeparam>
    /// <param name="items">The items to add.</param>
    /// <returns>An instance of the array with the items added.</returns>
    /// <exception cref="InvalidOperationException">The value was not an array.</exception>
    T AddRange<TArray>(in TArray items)
        where TArray : struct, IJsonArray<TArray>;

    /// <summary>
    /// Insert an item into the array at the given index.
    /// </summary>
    /// <param name="index">The index at which to add the item.</param>
    /// <param name="item1">The item to add.</param>
    /// <returns>An instance of the array with the item added.</returns>
    /// <exception cref="InvalidOperationException">The value was not an array.</exception>
    T Insert(int index, in JsonAny item1);

    /// <summary>
    /// Insert items into the array at the given index.
    /// </summary>
    /// <typeparam name="TArray">The type of the array containing the items to add.</typeparam>
    /// <param name="index">The index at which to add the items.</param>
    /// <param name="items">The items to add.</param>
    /// <returns>An instance of the array with the item added.</returns>
    /// <exception cref="IndexOutOfRangeException">The index was outside the bounds of the array.</exception>
    /// <exception cref="InvalidOperationException">The value was not an array.</exception>
    T InsertRange<TArray>(int index, in TArray items)
        where TArray : struct, IJsonArray<TArray>;

    /// <summary>
    /// Insert items into the array at the given index.
    /// </summary>
    /// <param name="index">The index at which to add the items.</param>
    /// <param name="items">The items to add.</param>
    /// <returns>An instance of the array with the items added.</returns>
    /// <exception cref="InvalidOperationException">The value was not an array.</exception>
    /// <exception cref="IndexOutOfRangeException">The index was outside the bounds of the array.</exception>
    T InsertRange(int index, IEnumerable<JsonAny> items);

    /// <summary>
    /// Insert items into the array at the given index.
    /// </summary>
    /// <typeparam name="TItem">The type of the items to add.</typeparam>
    /// <param name="index">The index at which to add the items.</param>
    /// <param name="items">The items to add.</param>
    /// <returns>An instance of the array with the items added.</returns>
    /// <exception cref="InvalidOperationException">The value was not an array.</exception>
    /// <exception cref="IndexOutOfRangeException">The index was outside the bounds of the array.</exception>
    T InsertRange<TItem>(int index, IEnumerable<TItem> items)
        where TItem : struct, IJsonValue<TItem>;

    /// <summary>
    /// Remove the specified item from the array.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <returns>An instance of the array with the item removed.</returns>
    /// <exception cref="InvalidOperationException">The value was not an array.</exception>
    T Remove(in JsonAny item);

    /// <summary>
    /// Remove the item at the index from the array.
    /// </summary>
    /// <param name="index">The index at which to remove the item.</param>
    /// <returns>An instance of the array with the item removed.</returns>
    /// <exception cref="InvalidOperationException">The value was not an array.</exception>
    /// <exception cref="IndexOutOfRangeException">The index was outside the bounds of the array.</exception>
    T RemoveAt(int index);

    /// <summary>
    /// Remove the item at the index from the array.
    /// </summary>
    /// <param name="index">The index at which to start to remove the items.</param>
    /// <param name="count">The number of items to remove.</param>
    /// <returns>An instance of the array with the item removed.</returns>
    /// <exception cref="InvalidOperationException">The value was not an array.</exception>
    /// <exception cref="IndexOutOfRangeException">The range was outside the bounds of the array.</exception>
    T RemoveRange(int index, int count);

    /// <summary>
    /// Replace the first instance of the given value with the new value, even if the items are identical.
    /// </summary>
    /// <param name="oldValue">The item to remove.</param>
    /// <param name="newValue">The item to insert.</param>
    /// <returns>An instance of the array with the item replaced.</returns>
    /// <exception cref="InvalidOperationException">The value was not an array.</exception>
    T Replace(in JsonAny oldValue, in JsonAny newValue);

    /// <summary>
    /// Set the item at the given index.
    /// </summary>
    /// <param name="index">The index at which to set the item.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>An instance of the array with the item set to the given value.</returns>
    T SetItem(int index, in JsonAny value);

    /// <summary>
    /// Construct an instance of the array from a list of json values.
    /// </summary>
    /// <param name="items">The list of items from which to construct the array.</param>
    /// <returns>An instance of the array constructed from the list.</returns>
    static abstract T From(ImmutableList<JsonAny> items);

    /// <summary>
    /// Construct an instance of the array from a list of json values.
    /// </summary>
    /// <param name="items">The list of items from which to construct the array.</param>
    /// <returns>An instance of the array constructed from the list.</returns>
    static abstract T FromRange(IEnumerable<JsonAny> items);

    /// <summary>
    /// Construct an instance of the array from a list of json values.
    /// </summary>
    /// <typeparam name="TItem">The type of the items in the enumerable.</typeparam>
    /// <param name="items">The list of items from which to construct the array.</param>
    /// <returns>An instance of the array constructed from the list.</returns>
    static abstract T FromRange<TItem>(IEnumerable<TItem> items)
        where TItem : struct, IJsonValue<TItem>;
}