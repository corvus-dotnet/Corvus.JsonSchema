// <copyright file="IJsonObject.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;

    /// <summary>
    /// Interface implemented by a JSON object.
    /// </summary>
    public interface IJsonObject : IJsonValue
    {
        /// <summary>
        /// Enumerate the object.
        /// </summary>
        /// <returns>A <see cref="JsonObjectEnumerator"/>.</returns>
        JsonObjectEnumerator EnumerateObject();

        /// <summary>
        /// Try to get a property value.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <returns><c>True</c> if the property was found.</returns>
        bool TryGetProperty(string name, out JsonAny value);

        /// <summary>
        /// Try to get a property value.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <returns><c>True</c> if the property was found.</returns>
        bool TryGetProperty(ReadOnlySpan<char> name, out JsonAny value);

        /// <summary>
        /// Try to get a property value.
        /// </summary>
        /// <param name="utf8name">The name of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <returns><c>True</c> if the property was found.</returns>
        bool TryGetProperty(ReadOnlySpan<byte> utf8name, out JsonAny value);

        /// <summary>
        /// Gets a value indicating whether the object has a property of the given name.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <returns><c>True</c> if the property was found.</returns>
        bool HasProperty(string name);

        /// <summary>
        /// Gets a value indicating whether the object has a property of the given name.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <returns><c>True</c> if the property was found.</returns>
        bool HasProperty(ReadOnlySpan<char> name);

        /// <summary>
        /// Gets a value indicating whether the object has a property of the given name.
        /// </summary>
        /// <param name="utf8Name">The utf8 encoded name of the property.</param>
        /// <returns><c>True</c> if the property was found.</returns>
        bool HasProperty(ReadOnlySpan<byte> utf8Name);
    }
}