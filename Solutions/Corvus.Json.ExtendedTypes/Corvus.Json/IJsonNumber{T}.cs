// <copyright file="IJsonNumber{T}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// A JSON number.
/// </summary>
/// <typeparam name="T">The type implementin the interface.</typeparam>
public interface IJsonNumber<T> : IJsonValue<T>
    where T : struct, IJsonNumber<T>
{
    /// <summary>
    /// Conversion from double.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    static abstract implicit operator T(double value);

    /// <summary>
    /// Conversion to double.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    static abstract implicit operator double(T value);

    /// <summary>
    /// Conversion from double.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    static abstract implicit operator T(long value);

    /// <summary>
    /// Conversion to double.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a long.</exception>
    static abstract implicit operator long(T value);
}