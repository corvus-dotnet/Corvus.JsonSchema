// <copyright file="CoreTypesHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Helper methods for working with <see cref="CoreTypes"/>.
/// </summary>
public static class CoreTypesHelpers
{
    /// <summary>
    /// Get the <see cref="CoreTypes"/> value corresponding to a <see cref="JsonValueKind"/>.
    /// </summary>
    /// <param name="jsonValueKind">The <see cref="JsonValueKind"/> for which to get the <see cref="CoreTypes"/> instance.</param>
    /// <returns>The corresponding <see cref="CoreTypes"/> value.</returns>
    /// <exception cref="ArgumentException">The <see cref="JsonValueKind"/> was not recognized.</exception>
    public static CoreTypes FromValueKind(JsonValueKind jsonValueKind)
    {
        return jsonValueKind switch
        {
            JsonValueKind.Undefined => CoreTypes.None,
            JsonValueKind.Null => CoreTypes.Null,
            JsonValueKind.Number => CoreTypes.Number,
            JsonValueKind.String => CoreTypes.String,
            JsonValueKind.True => CoreTypes.Boolean,
            JsonValueKind.False => CoreTypes.Boolean,
            JsonValueKind.Array => CoreTypes.Array,
            JsonValueKind.Object => CoreTypes.Object,
            _ => throw new ArgumentException("Unknown JsonValueKind", nameof(jsonValueKind)),
        };
    }

    /// <summary>
    /// Get the <see cref="CoreTypes"/> value corresponding to the union of the core type for each of an array of <see cref="JsonValueKind"/>.
    /// </summary>
    /// <param name="jsonValueKinds">The array of <see cref="JsonValueKind"/> instances for which to get the <see cref="CoreTypes"/> instance.</param>
    /// <returns>The corresponding <see cref="CoreTypes"/> value.</returns>
    /// <exception cref="ArgumentException">One or more of the <see cref="JsonValueKind"/> values was not recognized.</exception>
    public static CoreTypes FromValueKinds(JsonValueKind[] jsonValueKinds)
    {
        CoreTypes result = CoreTypes.None;
        foreach (JsonValueKind kind in jsonValueKinds)
        {
            result |= FromValueKind(kind);
        }

        return result;
    }

    /// <summary>
    /// Counts the number of types in the <see cref="CoreTypes"/> union.
    /// </summary>
    /// <param name="coreTypes">The core types to count.</param>
    /// <returns>The number of core types represented in the union.</returns>
    public static int CountTypes(this CoreTypes coreTypes)
    {
        return PopCount(coreTypes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PopCount(CoreTypes coreTypes)
    {
#if NET8_0_OR_GREATER
        return System.Numerics.BitOperations.PopCount((uint)coreTypes);
#else
        int count = 0;
        uint value = (uint)coreTypes;
        while (value != 0)
        {
            count += (int)(value & 1);
            value >>= 1;
        }

        return count;
#endif
    }
}