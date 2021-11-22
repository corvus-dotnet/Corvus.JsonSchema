// <copyright file="IJsonObject{T}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;

    /// <summary>
    /// Interface implemented by a JSON object.
    /// </summary>
    /// <typeparam name="T">The type of the entity implementing the <see cref="IJsonObject{T}"/>.</typeparam>
    public interface IJsonObject<T> : IJsonObject
        where T : struct, IJsonObject<T>
    {
        /// <summary>
        /// Sets the given property value.
        /// </summary>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <returns>The instance with the property set.</returns>
        T SetProperty<TValue>(string name, TValue value)
            where TValue : struct, IJsonValue;

        /// <summary>
        /// Sets the given property value.
        /// </summary>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <returns>The instance with the property set.</returns>
        T SetProperty<TValue>(ReadOnlySpan<char> name, TValue value)
            where TValue : struct, IJsonValue;

        /// <summary>
        /// Sets the given property value.
        /// </summary>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="utf8Name">The utf8-encoded name of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <returns>The instance with the property set.</returns>
        T SetProperty<TValue>(ReadOnlySpan<byte> utf8Name, TValue value)
            where TValue : struct, IJsonValue;

        /// <summary>
        /// Removes the given property value.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <returns>The isntance with the property removed.</returns>
        T RemoveProperty(string name);

        /// <summary>
        /// Removes the given property value.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <returns>The isntance with the property removed.</returns>
        T RemoveProperty(ReadOnlySpan<char> name);

        /// <summary>
        /// Removes the given property value.
        /// </summary>
        /// <param name="utf8Name">The utf8-encoded name of the property.</param>
        /// <returns>The isntance with the property removed.</returns>
        T RemoveProperty(ReadOnlySpan<byte> utf8Name);
    }
}