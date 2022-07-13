// <copyright file="IJsonArray.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    /// <summary>
    /// Interface implemented by a JSON value.
    /// </summary>
    public interface IJsonArray : IJsonValue
    {
        /// <summary>
        /// Gets the length of the array.
        /// </summary>
        int Length { get; }

        /// <summary>
        /// Gets the item at the given index.
        /// </summary>
        /// <param name="index">The index of the item to get.</param>
        /// <returns>A <see cref="JsonAny"/> representing that item at the given index.</returns>
        JsonAny this[int index] { get; }

        /// <summary>
        /// Enumerate the array.
        /// </summary>
        /// <returns>A <see cref="JsonArrayEnumerator"/>.</returns>
        JsonArrayEnumerator EnumerateArray();
    }
}