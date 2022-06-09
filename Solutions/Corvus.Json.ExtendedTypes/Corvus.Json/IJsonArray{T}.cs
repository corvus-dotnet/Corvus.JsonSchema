// <copyright file="IJsonArray{T}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    /// <summary>
    /// Interface implemented by a JSON value.
    /// </summary>
    /// <typeparam name="T">The type of the entity implementing <see cref="IJsonArray{T}"/>.</typeparam>
    public interface IJsonArray<T> : IJsonArray
        where T : struct, IJsonArray<T>
    {
        /// <summary>
        /// Adds an item to the array.
        /// </summary>
        /// <typeparam name="TItem">The type of the item to add.</typeparam>
        /// <param name="item">The item to add.</param>
        /// <returns>The array with the item added.</returns>
        T Add<TItem>(TItem item)
            where TItem : struct, IJsonValue;

        /// <summary>
        /// Adds 2 items to the array.
        /// </summary>
        /// <typeparam name="TItem1">The type of the first item to add.</typeparam>
        /// <typeparam name="TItem2">The type of the second item to add.</typeparam>
        /// <param name="item1">The first item to add.</param>
        /// <param name="item2">The second item to add.</param>
        /// <returns>The array with the item added.</returns>
        T Add<TItem1, TItem2>(TItem1 item1, TItem2 item2)
            where TItem1 : struct, IJsonValue
            where TItem2 : struct, IJsonValue;

        /// <summary>
        /// Adds 3 items to the array.
        /// </summary>
        /// <typeparam name="TItem1">The type of the first item to add.</typeparam>
        /// <typeparam name="TItem2">The type of the second item to add.</typeparam>
        /// <typeparam name="TItem3">The type of the third item to add.</typeparam>
        /// <param name="item1">The first item to add.</param>
        /// <param name="item2">The second item to add.</param>
        /// <param name="item3">The third item to add.</param>
        /// <returns>The array with the item added.</returns>
        T Add<TItem1, TItem2, TItem3>(TItem1 item1, TItem2 item2, TItem3 item3)
            where TItem1 : struct, IJsonValue
            where TItem2 : struct, IJsonValue
            where TItem3 : struct, IJsonValue;

        /// <summary>
        /// Adds 4 items to the array.
        /// </summary>
        /// <typeparam name="TItem1">The type of the first item to add.</typeparam>
        /// <typeparam name="TItem2">The type of the second item to add.</typeparam>
        /// <typeparam name="TItem3">The type of the third item to add.</typeparam>
        /// <typeparam name="TItem4">The type of the fourth item to add.</typeparam>
        /// <param name="item1">The first item to add.</param>
        /// <param name="item2">The second item to add.</param>
        /// <param name="item3">The third item to add.</param>
        /// <param name="item4">The fourth item to add.</param>
        /// <returns>The array with the item added.</returns>
        T Add<TItem1, TItem2, TItem3, TItem4>(TItem1 item1, TItem2 item2, TItem3 item3, TItem4 item4)
            where TItem1 : struct, IJsonValue
            where TItem2 : struct, IJsonValue
            where TItem3 : struct, IJsonValue
            where TItem4 : struct, IJsonValue;

        /// <summary>
        /// Adds items to the array.
        /// </summary>
        /// <typeparam name="TItem">The type of the items to add.</typeparam>
        /// <param name="items">The items to add.</param>
        /// <returns>The array with the items added.</returns>
        T Add<TItem>(params TItem[] items)
            where TItem : struct, IJsonValue;

        /// <summary>
        /// Adds range of items of the same type to the array.
        /// </summary>
        /// <typeparam name="TItem">The type of the items to add.</typeparam>
        /// <param name="items">The items to add.</param>
        /// <returns>The array with the items added.</returns>
        T AddRange<TItem>(IEnumerable<TItem> items)
            where TItem : struct, IJsonValue;

        /// <summary>
        /// Adds an item to the array.
        /// </summary>
        /// <typeparam name="TItem">The type of the item to insert.</typeparam>
        /// <param name="index">The index at which to insert the item.</param>
        /// <param name="item">The item to insert.</param>
        /// <returns>The array with the item added.</returns>
        T Insert<TItem>(int index, TItem item)
            where TItem : struct, IJsonValue;

        /// <summary>
        /// Replaces the first item that matches the given value.
        /// </summary>
        /// <typeparam name="TItem">The type of the item to insert.</typeparam>
        /// <param name="oldValue">The value to replace.</param>
        /// <param name="newValue">The value with which to replace it.</param>
        /// <returns>The array with the item added.</returns>
        T Replace<TItem>(TItem oldValue, TItem newValue)
            where TItem : struct, IJsonValue;

        /// <summary>
        /// Remove the item at the given index.
        /// </summary>
        /// <param name="index">The index of the item to remove.</param>
        /// <returns>The array with the item removed.</returns>
        T RemoveAt(int index);

        /// <summary>
        /// Remove the given range of items.
        /// </summary>
        /// <param name="index">The index of the first item to remove.</param>
        /// <param name="count">The number of items to remove.</param>
        /// <returns>The array with the items removed.</returns>
        T RemoveRange(int index, int count);

        /// <summary>
        /// Set the item at the given index.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="index">The index at which to set the value.</param>
        /// <param name="value">The new value.</param>
        /// <returns>The array with the item set.</returns>
        T SetItem<TItem>(int index, TItem value)
            where TItem : struct, IJsonValue;
    }
}