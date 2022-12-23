// <copyright file="NumericTypeExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// Extension methods for numeric types.
/// </summary>
public static class NumericTypeExtensions
{
    /// <summary>
    /// Safely get an int32 value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <returns>The value as an integer.</returns>
    /// <exception cref="FormatException">The value could not be formatted as an integer.</exception>
    public static int SafeGetInt32(this JsonElement value)
    {
        if (value.TryGetInt32(out int result))
        {
            return result;
        }

        if (value.TryGetDouble(out double doubleResult))
        {
            if (doubleResult < int.MinValue || doubleResult > int.MaxValue)
            {
                throw new FormatException();
            }

            return (int)doubleResult;
        }

        throw new FormatException();
    }

    /// <summary>
    /// Safely get an int64 value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <returns>The value as an integer.</returns>
    /// <exception cref="FormatException">The value could not be formatted as an integer.</exception>
    public static long SafeGetInt64(this JsonElement value)
    {
        if (value.TryGetInt64(out long result))
        {
            return result;
        }

        if (value.TryGetDouble(out double doubleResult))
        {
            if (doubleResult < long.MinValue || doubleResult > long.MaxValue)
            {
                throw new FormatException();
            }

            return (long)doubleResult;
        }

        throw new FormatException();
    }

    /// <summary>
    /// Safely get a uint32 value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <returns>The value as an integer.</returns>
    /// <exception cref="FormatException">The value could not be formatted as an integer.</exception>
    public static uint SafeGetUInt32(this JsonElement value)
    {
        if (value.TryGetUInt32(out uint result))
        {
            return result;
        }

        if (value.TryGetDouble(out double doubleResult))
        {
            if (doubleResult < uint.MinValue || doubleResult > uint.MaxValue)
            {
                throw new FormatException();
            }

            return (uint)doubleResult;
        }

        throw new FormatException();
    }

    /// <summary>
    /// Safely get a uint16 value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <returns>The value as an integer.</returns>
    /// <exception cref="FormatException">The value could not be formatted as an integer.</exception>
    public static ushort SafeGetUInt16(this JsonElement value)
    {
        if (value.TryGetUInt16(out ushort result))
        {
            return result;
        }

        if (value.TryGetDouble(out double doubleResult))
        {
            if (doubleResult < ushort.MinValue || doubleResult > ushort.MaxValue)
            {
                throw new FormatException();
            }

            return (ushort)doubleResult;
        }

        throw new FormatException();
    }

    /// <summary>
    /// Safely get an uint64 value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <returns>The value as an integer.</returns>
    /// <exception cref="FormatException">The value could not be formatted as an integer.</exception>
    public static ulong SafeGetUInt64(this JsonElement value)
    {
        if (value.TryGetUInt64(out ulong result))
        {
            return result;
        }

        if (value.TryGetDouble(out double doubleResult))
        {
            if (doubleResult < ulong.MinValue || doubleResult > ulong.MaxValue)
            {
                throw new FormatException();
            }

            return (ulong)doubleResult;
        }

        throw new FormatException();
    }

    /// <summary>
    /// Safely get a byte value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <returns>The value as an integer.</returns>
    /// <exception cref="FormatException">The value could not be formatted as an integer.</exception>
    public static byte SafeGetByte(this JsonElement value)
    {
        if (value.TryGetByte(out byte result))
        {
            return result;
        }

        if (value.TryGetDouble(out double doubleResult))
        {
            if (doubleResult < byte.MinValue || doubleResult > byte.MaxValue)
            {
                throw new FormatException();
            }

            return (byte)doubleResult;
        }

        throw new FormatException();
    }

    /// <summary>
    /// Safely get an sbyte value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <returns>The value as an integer.</returns>
    /// <exception cref="FormatException">The value could not be formatted as an integer.</exception>
    public static sbyte SafeGetSByte(this JsonElement value)
    {
        if (value.TryGetSByte(out sbyte result))
        {
            return result;
        }

        if (value.TryGetDouble(out double doubleResult))
        {
            if (doubleResult < sbyte.MinValue || doubleResult > sbyte.MaxValue)
            {
                throw new FormatException();
            }

            return (sbyte)doubleResult;
        }

        throw new FormatException();
    }
}