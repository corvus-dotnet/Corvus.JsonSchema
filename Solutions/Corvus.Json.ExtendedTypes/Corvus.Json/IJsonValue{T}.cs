// <copyright file="IJsonValue.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System.Text.Json;

    /// <summary>
    /// Interface implemented by a JSON value.
    /// </summary>
    /// <typeparam name="T">The type implementing the interface.</typeparam>
    public interface IJsonValue<T> : IJsonValue
        where T : struct, IJsonValue
    {
        /// <summary>
        /// Returns an instance of this T based on the underlying <see cref="JsonElement"/>.
        /// </summary>
        /// <param name="value">The jsonElement with which to back the value.</param>
        /// <returns>An instance of T with the given JsonElement.</returns>
        T With(JsonElement value);
    }
}