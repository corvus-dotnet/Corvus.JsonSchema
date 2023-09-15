// <copyright file="NumericTypeExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// Extension methods for numeric types.
/// </summary>
public static class NumericTypeExtensions
{
    private const double Error = 1.0E-9;

    /// <summary>
    /// Safely get an int32 value.
    /// </summary>
    /// <typeparam name="T">The type of number for which to get the value.</typeparam>
    /// <param name="value">The value to get.</param>
    /// <param name="result">The value as an integer.</param>
    /// <returns><see langword="true"/> if the value coudld be represented as an int32.</returns>
    public static bool TryGetInt32<T>(this T value, [NotNullWhen(true)] out int result)
        where T : struct, IJsonNumber<T>
    {
        result = default;

        if (value.HasJsonElementBacking)
        {
            return TryGetInt32(value.AsJsonElement, out result);
        }

        double doubleResult = (double)value;
        if (doubleResult < int.MinValue || doubleResult > int.MaxValue)
        {
            return false;
        }

        result = (int)doubleResult;
        return Math.Abs(result - doubleResult) < Error;
    }

    /// <summary>
    /// Safely get an int32 value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <param name="result">The value as an integer.</param>
    /// <returns><see langword="true"/> if the value coudld be represented as an int32.</returns>
    public static bool TryGetInt32(this JsonElement value, [NotNullWhen(true)] out int result)
    {
        result = default;
        if (value.TryGetInt32(out result))
        {
            return true;
        }

        if (value.TryGetDouble(out double doubleResult))
        {
            if (doubleResult < int.MinValue || doubleResult > int.MaxValue)
            {
                return false;
            }

            result = (int)doubleResult;
            return Math.Abs(result - doubleResult) < Error;
        }

        return false;
    }

    /// <summary>
    /// Safely get an int32 value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <returns>The value as an integer.</returns>
    /// <exception cref="FormatException">The value could not be formatted as an integer.</exception>
    public static int SafeGetInt32(this JsonElement value)
    {
        if (TryGetInt32(value, out int result))
        {
            return result;
        }

        throw new FormatException();
    }

    /// <summary>
    /// Safely get an single value.
    /// </summary>
    /// <typeparam name="T">The type of number for which to get the value.</typeparam>
    /// <param name="value">The value to get.</param>
    /// <param name="result">The value as an floateger.</param>
    /// <returns><see langword="true"/> if the value coudld be represented as an single.</returns>
    public static bool TryGetSingle<T>(this T value, [NotNullWhen(true)] out float result)
        where T : struct, IJsonNumber<T>
    {
        result = default;

        if (value.HasJsonElementBacking)
        {
            return TryGetSingle(value.AsJsonElement, out result);
        }

        double doubleResult = (double)value;
        if (doubleResult < float.MinValue || doubleResult > float.MaxValue)
        {
            return false;
        }

        result = (float)doubleResult;
        return true;
    }

    /// <summary>
    /// Safely get an single value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <param name="result">The value as an floateger.</param>
    /// <returns><see langword="true"/> if the value coudld be represented as an single.</returns>
    public static bool TryGetSingle(this JsonElement value, [NotNullWhen(true)] out float result)
    {
        result = default;
        if (value.TryGetSingle(out result))
        {
            return true;
        }

        if (value.TryGetDouble(out double doubleResult))
        {
            if (doubleResult < float.MinValue || doubleResult > float.MaxValue)
            {
                return false;
            }

            result = (float)doubleResult;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Safely get an single value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <returns>The value as an floateger.</returns>
    /// <exception cref="FormatException">The value could not be formatted as an floateger.</exception>
    public static float SafeGetSingle(this JsonElement value)
    {
        if (TryGetSingle(value, out float result))
        {
            return result;
        }

        throw new FormatException();
    }

    /// <summary>
    /// Safely get an double value.
    /// </summary>
    /// <typeparam name="T">The type of number for which to get the value.</typeparam>
    /// <param name="value">The value to get.</param>
    /// <param name="result">The value as an doubleeger.</param>
    /// <returns><see langword="true"/> if the value coudld be represented as an double.</returns>
    public static bool TryGetDouble<T>(this T value, [NotNullWhen(true)] out double result)
        where T : struct, IJsonNumber<T>
    {
        if (value.HasJsonElementBacking)
        {
            return value.AsJsonElement.TryGetDouble(out result);
        }

        result = (double)value;
        return true;
    }

    /// <summary>
    /// Safely get an double value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <returns>The value as an doubleeger.</returns>
    /// <exception cref="FormatException">The value could not be formatted as an doubleeger.</exception>
    public static double SafeGetDouble(this JsonElement value)
    {
        if (value.TryGetDouble(out double result))
        {
            return result;
        }

        throw new FormatException();
    }

    /// <summary>
    /// Safely get an int64 value.
    /// </summary>
    /// <typeparam name="T">The type of number for which to get the value.</typeparam>
    /// <param name="value">The value to get.</param>
    /// <param name="result">The value as an integer.</param>
    /// <returns><see langword="true"/> if the value coudld be represented as an int64.</returns>
    public static bool TryGetInt64<T>(this T value, [NotNullWhen(true)] out long result)
        where T : struct, IJsonNumber<T>
    {
        result = default;

        if (value.HasJsonElementBacking)
        {
            return TryGetInt64(value.AsJsonElement, out result);
        }

        double doubleResult = (double)value;
        if (doubleResult < long.MinValue || doubleResult > long.MaxValue)
        {
            return false;
        }

        result = (long)doubleResult;
        return Math.Abs(result - doubleResult) < Error;
    }

    /// <summary>
    /// Safely get an int64 value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <param name="result">The value as an integer.</param>
    /// <returns><see langword="true"/> if the value coudld be represented as an int64.</returns>
    public static bool TryGetInt64(this JsonElement value, [NotNullWhen(true)] out long result)
    {
        result = default;
        if (value.TryGetInt64(out result))
        {
            return true;
        }

        if (value.TryGetDouble(out double doubleResult))
        {
            if (doubleResult < long.MinValue || doubleResult > long.MaxValue)
            {
                return false;
            }

            result = (long)doubleResult;
            return Math.Abs(result - doubleResult) < Error;
        }

        return false;
    }

    /// <summary>
    /// Safely get an int64 value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <returns>The value as an integer.</returns>
    /// <exception cref="FormatException">The value could not be formatted as an integer.</exception>
    public static long SafeGetInt64(this JsonElement value)
    {
        if (TryGetInt64(value, out long result))
        {
            return result;
        }

        throw new FormatException();
    }

    /// <summary>
    /// Safely get an uint32 value.
    /// </summary>
    /// <typeparam name="T">The type of number for which to get the value.</typeparam>
    /// <param name="value">The value to get.</param>
    /// <param name="result">The value as an integer.</param>
    /// <returns><see langword="true"/> if the value coudld be represented as an int64.</returns>
    public static bool TryGetUInt32<T>(this T value, [NotNullWhen(true)] out uint result)
        where T : struct, IJsonNumber<T>
    {
        result = default;

        if (value.HasJsonElementBacking)
        {
            return TryGetUInt32(value.AsJsonElement, out result);
        }

        double doubleResult = (double)value;
        if (doubleResult < uint.MinValue || doubleResult > uint.MaxValue)
        {
            return false;
        }

        result = (uint)doubleResult;
        return Math.Abs(result - doubleResult) < Error;
    }

    /// <summary>
    /// Safely get an uint32 value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <param name="result">The value as an integer.</param>
    /// <returns><see langword="true"/> if the value coudld be represented as an int64.</returns>
    public static bool TryGetUInt32(this JsonElement value, [NotNullWhen(true)] out uint result)
    {
        result = default;
        if (value.TryGetUInt32(out result))
        {
            return true;
        }

        if (value.TryGetDouble(out double doubleResult))
        {
            if (doubleResult < uint.MinValue || doubleResult > uint.MaxValue)
            {
                return false;
            }

            result = (uint)doubleResult;
            return Math.Abs(result - doubleResult) < Error;
        }

        return false;
    }

    /// <summary>
    /// Safely get an uint32 value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <returns>The value as an integer.</returns>
    /// <exception cref="FormatException">The value could not be formatted as an integer.</exception>
    public static uint SafeGetUInt32(this JsonElement value)
    {
        if (TryGetUInt32(value, out uint result))
        {
            return result;
        }

        throw new FormatException();
    }

    /// <summary>
    /// Safely get an ushort value.
    /// </summary>
    /// <typeparam name="T">The type of number for which to get the value.</typeparam>
    /// <param name="value">The value to get.</param>
    /// <param name="result">The value as an integer.</param>
    /// <returns><see langword="true"/> if the value coudld be represented as an int64.</returns>
    public static bool TryGetUInt16<T>(this T value, [NotNullWhen(true)] out ushort result)
        where T : struct, IJsonNumber<T>
    {
        result = default;

        if (value.HasJsonElementBacking)
        {
            return TryGetUInt16(value.AsJsonElement, out result);
        }

        double doubleResult = (double)value;
        if (doubleResult < ushort.MinValue || doubleResult > ushort.MaxValue)
        {
            return false;
        }

        result = (ushort)doubleResult;
        return Math.Abs(result - doubleResult) < Error;
    }

    /// <summary>
    /// Safely get an ushort value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <param name="result">The value as an integer.</param>
    /// <returns><see langword="true"/> if the value coudld be represented as an int64.</returns>
    public static bool TryGetUInt16(this JsonElement value, [NotNullWhen(true)] out ushort result)
    {
        result = default;
        if (value.TryGetUInt16(out result))
        {
            return true;
        }

        if (value.TryGetDouble(out double doubleResult))
        {
            if (doubleResult < uint.MinValue || doubleResult > uint.MaxValue)
            {
                return false;
            }

            result = (ushort)doubleResult;
            return Math.Abs(result - doubleResult) < Error;
        }

        return false;
    }

    /// <summary>
    /// Safely get an ushort value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <returns>The value as an integer.</returns>
    /// <exception cref="FormatException">The value could not be formatted as an integer.</exception>
    public static uint SafeGetUInt16(this JsonElement value)
    {
        if (TryGetUInt16(value, out ushort result))
        {
            return result;
        }

        throw new FormatException();
    }

    /// <summary>
    /// Safely get a ulong value.
    /// </summary>
    /// <typeparam name="T">The type of number for which to get the value.</typeparam>
    /// <param name="value">The value to get.</param>
    /// <param name="result">The value as an integer.</param>
    /// <returns><see langword="true"/> if the value coudld be represented as an int64.</returns>
    public static bool TryGetUInt64<T>(this T value, [NotNullWhen(true)] out ulong result)
        where T : struct, IJsonNumber<T>
    {
        result = default;

        if (value.HasJsonElementBacking)
        {
            return TryGetUInt64(value.AsJsonElement, out result);
        }

        double doubleResult = (double)value;
        if (doubleResult < ulong.MinValue || doubleResult > ulong.MaxValue)
        {
            return false;
        }

        result = (ulong)doubleResult;
        return Math.Abs(result - doubleResult) < Error;
    }

    /// <summary>
    /// Safely get a ulong value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <param name="result">The value as an integer.</param>
    /// <returns><see langword="true"/> if the value coudld be represented as an int64.</returns>
    public static bool TryGetUInt64(this JsonElement value, [NotNullWhen(true)] out ulong result)
    {
        result = default;
        if (value.TryGetUInt64(out result))
        {
            return true;
        }

        if (value.TryGetDouble(out double doubleResult))
        {
            if (doubleResult < uint.MinValue || doubleResult > uint.MaxValue)
            {
                return false;
            }

            result = (ulong)doubleResult;
            return Math.Abs(result - doubleResult) < Error;
        }

        return false;
    }

    /// <summary>
    /// Safely get a ulong value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <returns>The value as an integer.</returns>
    /// <exception cref="FormatException">The value could not be formatted as an integer.</exception>
    public static ulong SafeGetUInt64(this JsonElement value)
    {
        if (TryGetUInt64(value, out ulong result))
        {
            return result;
        }

        throw new FormatException();
    }

    /// <summary>
    /// Safely get a byte value.
    /// </summary>
    /// <typeparam name="T">The type of number for which to get the value.</typeparam>
    /// <param name="value">The value to get.</param>
    /// <param name="result">The value as an integer.</param>
    /// <returns><see langword="true"/> if the value coudld be represented as an int64.</returns>
    public static bool TryGetByte<T>(this T value, [NotNullWhen(true)] out byte result)
        where T : struct, IJsonNumber<T>
    {
        result = default;

        if (value.HasJsonElementBacking)
        {
            return TryGetByte(value.AsJsonElement, out result);
        }

        double doubleResult = (double)value;
        if (doubleResult < byte.MinValue || doubleResult > byte.MaxValue)
        {
            return false;
        }

        result = (byte)doubleResult;
        return Math.Abs(result - doubleResult) < Error;
    }

    /// <summary>
    /// Safely get a byte value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <param name="result">The value as an integer.</param>
    /// <returns><see langword="true"/> if the value coudld be represented as an int64.</returns>
    public static bool TryGetByte(this JsonElement value, [NotNullWhen(true)] out byte result)
    {
        result = default;
        if (value.TryGetByte(out result))
        {
            return true;
        }

        if (value.TryGetDouble(out double doubleResult))
        {
            if (doubleResult < uint.MinValue || doubleResult > uint.MaxValue)
            {
                return false;
            }

            result = (byte)doubleResult;
            return Math.Abs(result - doubleResult) < Error;
        }

        return false;
    }

    /// <summary>
    /// Safely get a byte value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <returns>The value as an integer.</returns>
    /// <exception cref="FormatException">The value could not be formatted as an integer.</exception>
    public static byte SafeGetByte(this JsonElement value)
    {
        if (TryGetByte(value, out byte result))
        {
            return result;
        }

        throw new FormatException();
    }

    /// <summary>
    /// Safely get an sbyte value.
    /// </summary>
    /// <typeparam name="T">The type of number for which to get the value.</typeparam>
    /// <param name="value">The value to get.</param>
    /// <param name="result">The value as an integer.</param>
    /// <returns><see langword="true"/> if the value coudld be represented as an int64.</returns>
    public static bool TryGetSByte<T>(this T value, [NotNullWhen(true)] out sbyte result)
        where T : struct, IJsonNumber<T>
    {
        result = default;

        if (value.HasJsonElementBacking)
        {
            return TryGetSByte(value.AsJsonElement, out result);
        }

        double doubleResult = (double)value;
        if (doubleResult < sbyte.MinValue || doubleResult > sbyte.MaxValue)
        {
            return false;
        }

        result = (sbyte)doubleResult;
        return Math.Abs(result - doubleResult) < Error;
    }

    /// <summary>
    /// Safely get an sbyte value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <param name="result">The value as an integer.</param>
    /// <returns><see langword="true"/> if the value coudld be represented as an int64.</returns>
    public static bool TryGetSByte(this JsonElement value, [NotNullWhen(true)] out sbyte result)
    {
        result = default;
        if (value.TryGetSByte(out result))
        {
            return true;
        }

        if (value.TryGetDouble(out double doubleResult))
        {
            if (doubleResult < uint.MinValue || doubleResult > uint.MaxValue)
            {
                return false;
            }

            result = (sbyte)doubleResult;
            return Math.Abs(result - doubleResult) < Error;
        }

        return false;
    }

    /// <summary>
    /// Safely get an sbyte value.
    /// </summary>
    /// <param name="value">The value to get.</param>
    /// <returns>The value as an integer.</returns>
    /// <exception cref="FormatException">The value could not be formatted as an integer.</exception>
    public static sbyte SafeGetSByte(this JsonElement value)
    {
        if (TryGetSByte(value, out sbyte result))
        {
            return result;
        }

        throw new FormatException();
    }
}