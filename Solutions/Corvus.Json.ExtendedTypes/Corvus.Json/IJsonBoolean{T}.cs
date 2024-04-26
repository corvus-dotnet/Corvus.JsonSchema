// <copyright file="IJsonBoolean{T}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json;

/// <summary>
/// A JSON boolean.
/// </summary>
/// <typeparam name="T">The type implementin the interface.</typeparam>
public interface IJsonBoolean<T> : IJsonValue<T>
    where T : struct, IJsonBoolean<T>
{
#if NET8_0_OR_GREATER
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
#endif

    /// <summary>
    /// Try to retrieve the value as a boolean.
    /// </summary>
    /// <param name="result"><see langword="true"/> if the value was true, otherwise <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if the value was representable as a boolean, otherwise <see langword="false"/>.</returns>
    bool TryGetBoolean([NotNullWhen(true)] out bool result);

    /// <summary>
    /// Get the value as a boolean.
    /// </summary>
    /// <returns>The value of the boolean, or <see langword="null"/> if the value was not representable as a boolean.</returns>
    bool? GetBoolean();
}