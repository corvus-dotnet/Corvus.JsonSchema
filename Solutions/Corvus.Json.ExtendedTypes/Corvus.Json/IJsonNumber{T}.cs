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
    /// Conversion from float.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    static abstract implicit operator T(float value);

    /// <summary>
    /// Conversion to float.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a float.</exception>
    static abstract implicit operator float(T value);

    /// <summary>
    /// Conversion from double.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    static abstract implicit operator T(int value);

    /// <summary>
    /// Conversion to double.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an int.</exception>
    static abstract implicit operator int(T value);

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

    /// <summary>
    /// Conversion from uint.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    static abstract explicit operator T(uint value);

    /// <summary>
    /// Conversion to uint.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a uint.</exception>
    static abstract implicit operator uint(T value);

    /// <summary>
    /// Conversion from ushort.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    static abstract implicit operator T(ushort value);

    /// <summary>
    /// Conversion to ushort.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a ushort.</exception>
    static abstract implicit operator ushort(T value);

    /// <summary>
    /// Conversion from ulong.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    static abstract implicit operator T(ulong value);

    /// <summary>
    /// Conversion to ulong.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a ulong.</exception>
    static abstract implicit operator ulong(T value);

    /// <summary>
    /// Conversion from byte.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    static abstract implicit operator T(byte value);

    /// <summary>
    /// Conversion to byte.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an byte.</exception>
    static abstract implicit operator byte(T value);

    /// <summary>
    /// Conversion from sbyte.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    static abstract implicit operator T(sbyte value);

    /// <summary>
    /// Conversion to sbyte.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an sbyte.</exception>
    static abstract implicit operator sbyte(T value);
}