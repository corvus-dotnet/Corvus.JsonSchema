// <copyright file="IJsonString{T}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json;

/// <summary>
/// A JSON string.
/// </summary>
/// <typeparam name="T">The type implementin the interface.</typeparam>
public interface IJsonString<T> : IJsonValue<T>
    where T : struct, IJsonString<T>
{
    /// <summary>
    /// Conversion from string.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    static abstract implicit operator T(string value);

    /// <summary>
    /// Conversion to string.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a string.</exception>
    static abstract implicit operator string(T value);

    /// <summary>
    /// Conversion from string.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    static abstract implicit operator T(ReadOnlySpan<char> value);

    /// <summary>
    /// Conversion to string.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a string.</exception>
    static abstract implicit operator ReadOnlySpan<char>(T value);

    /// <summary>
    /// Conversion from string.
    /// </summary>
    /// <param name="utf8Value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The values were not strings.</exception>
    static abstract implicit operator T(ReadOnlySpan<byte> utf8Value);

    /// <summary>
    /// Try to get the string value.
    /// </summary>
    /// <param name="value">The value as a string.</param>
    /// <returns><c>True</c> if the value can be recovered as a string.</returns>
    bool TryGetString([NotNullWhen(true)] out string? value);

    /// <summary>
    /// Get the string value as a <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    /// <returns>The string as a <see cref="ReadOnlySpan{Char}"/>.</returns>
    /// <exception cref="InvalidOperationException">The value was not a string.</exception>
    ReadOnlySpan<char> AsSpan();

    /// <summary>
    /// Get the string value as <see cref="Nullable{String}"/>.
    /// </summary>
    /// <returns>If the value is a string, the value as a string. Otherwise <c>null</c>.</returns>
    string? AsOptionalString();
}