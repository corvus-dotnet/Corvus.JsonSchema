// <copyright file="MetadataValueBoxes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Boxes type declaration metadata values, reusing one box per value for the value types the
/// analysis stores most (<see cref="bool"/>, small <see cref="int"/> values and <see cref="CoreTypes"/>).
/// </summary>
/// <remarks>
/// A boxed value is immutable and metadata values are only ever read back by casting, so a shared box
/// is indistinguishable from a fresh one; it saves an allocation per stored flag, count or core-type set.
/// </remarks>
internal static class MetadataValueBoxes
{
    private const int SmallInt32Count = 256;

    private static readonly object True = true;
    private static readonly object False = false;
    private static readonly object[] SmallInt32s = CreateSmallInt32s();
    private static readonly object[] CoreTypesValues = CreateCoreTypesValues();

    /// <summary>
    /// Boxes a metadata value, as <c>(object?)value</c> does.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The boxed value.</returns>
    public static object? Box<T>(T value)
    {
        if (typeof(T) == typeof(bool))
        {
            return (bool)(object)value! ? True : False;
        }

        if (typeof(T) == typeof(bool?))
        {
            bool? flag = (bool?)(object?)value;
            return flag.HasValue ? (flag.Value ? True : False) : null;
        }

        if (typeof(T) == typeof(int))
        {
            return BoxInt32((int)(object)value!);
        }

        if (typeof(T) == typeof(int?))
        {
            int? number = (int?)(object?)value;
            return number.HasValue ? BoxInt32(number.Value) : null;
        }

        if (typeof(T) == typeof(CoreTypes))
        {
            return CoreTypesValues[(byte)(CoreTypes)(object)value!];
        }

        return value;
    }

    private static object BoxInt32(int value)
    {
        return value >= 0 && value < SmallInt32Count ? SmallInt32s[value] : value;
    }

    private static object[] CreateSmallInt32s()
    {
        object[] result = new object[SmallInt32Count];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = i;
        }

        return result;
    }

    private static object[] CreateCoreTypesValues()
    {
        object[] result = new object[256];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (CoreTypes)i;
        }

        return result;
    }
}