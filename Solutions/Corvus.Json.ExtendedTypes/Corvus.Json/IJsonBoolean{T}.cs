// <copyright file="IJsonBoolean{T}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// A JSON boolean.
/// </summary>
/// <typeparam name="T">The type implementin the interface.</typeparam>
public interface IJsonBoolean<T> : IJsonValue<T>
    where T : struct, IJsonBoolean<T>
{
    /// <summary>
    /// Conversion from bool.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    static abstract implicit operator T(bool value);

    /// <summary>
    /// Conversion to bool.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a string.</exception>
    static abstract implicit operator bool(T value);
}