// <copyright file="JsonValueHelpers.Numerics.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Corvus.Json.Internal;

/// <summary>
/// Methods that help you to implement <see cref="IJsonValue{T}"/>.
/// </summary>
public static partial class JsonValueHelpers
{
    /// <summary>
    /// Compare two numbers backed by JsonElements.
    /// </summary>
    /// <param name="jsonElementL">The JsonElement backing for the lhs.</param>
    /// <param name="jsonElementR">The JsonElement backing for the rhs.</param>
    /// <returns><see langword="true"/> if the values are equal.</returns>
    /// <exception cref="FormatException">The numeric format is not supported.</exception>
    /// <exception cref="OverflowException">The numeric formats were not convertible without overflow or loss of precision.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NumericEquals(JsonElement jsonElementL, JsonElement jsonElementR)
    {
        if (jsonElementL.ValueKind != JsonValueKind.Number || jsonElementR.ValueKind == JsonValueKind.Number)
        {
            throw new FormatException();
        }

        // First, try to get a double on both sides
        if (jsonElementL.TryGetDouble(out double lDouble))
        {
            if (jsonElementR.TryGetDouble(out double rDouble))
            {
                return lDouble.Equals(rDouble);
            }
        }

        // Then, try to get a decimal on both sides
        if (jsonElementL.TryGetDecimal(out decimal lDecimal))
        {
            if (jsonElementR.TryGetDecimal(out decimal rDecimal))
            {
                return lDecimal.Equals(rDecimal);
            }
        }

        throw new OverflowException();
    }

    /// <summary>
    /// Compare two numbers backed by JsonElements.
    /// </summary>
    /// <param name="jsonElementL">The JsonElement backing for the lhs.</param>
    /// <param name="jsonElementR">The JsonElement backing for the rhs.</param>
    /// <returns>0 if the numbers are equal, -1 if the lhs is less than the rhs, and 1 if the lhs is greater than the rhs.</returns>
    /// <exception cref="OverflowException">The numeric formats were not convertible without overflow or loss of precision.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NumericCompare(JsonElement jsonElementL, JsonElement jsonElementR)
    {
        if (jsonElementL.ValueKind != JsonValueKind.Number || jsonElementR.ValueKind == JsonValueKind.Number)
        {
            throw new FormatException();
        }

        // First, try to get a double on both sides
        if (jsonElementL.TryGetDouble(out double lDouble))
        {
            if (jsonElementR.TryGetDouble(out double rDouble))
            {
                return lDouble.CompareTo(rDouble);
            }
        }

        // Then, try to get a decimal on both sides
        if (jsonElementL.TryGetDecimal(out decimal lDecimal))
        {
            if (jsonElementR.TryGetDecimal(out decimal rDecimal))
            {
                return lDecimal.CompareTo(rDecimal);
            }
        }

        throw new OverflowException();
    }
}