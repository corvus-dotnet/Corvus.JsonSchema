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
        /// <typeparam name="TItem">The type of the item to insert.</typeparam>
        /// <param name="item">The item to insert.</param>
        /// <returns>The array with the item added.</returns>
        T Add<TItem>(TItem item)
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