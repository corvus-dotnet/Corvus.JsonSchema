// <copyright file="JsonValueHelpers.Comparisons.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.Internal;

/// <summary>
/// Methods that help you to implement <see cref="IJsonValue{T}"/>.
/// </summary>
public static partial class JsonValueHelpers
{
    /// <summary>
    /// Compares two values.
    /// </summary>
    /// <typeparam name="TItem1">The type of the first value.</typeparam>
    /// <typeparam name="TItem2">The type of the second value.</typeparam>
    /// <param name="item1">The first value.</param>
    /// <param name="item2">The second value.</param>
    /// <returns><c>True</c> if they are equal.</returns>
    public static bool CompareValues<TItem1, TItem2>(in TItem1 item1, in TItem2 item2)
        where TItem1 : struct, IJsonValue<TItem1>
        where TItem2 : struct, IJsonValue<TItem2>
    {
        JsonValueKind thisKind = item1.ValueKind;
        JsonValueKind otherKind = item2.ValueKind;

        if (thisKind != otherKind)
        {
            return false;
        }

        if (thisKind == JsonValueKind.Array)
        {
            return CompareArrays(item1.AsArray, item2.AsArray);
        }

        if (thisKind == JsonValueKind.False || thisKind == JsonValueKind.True)
        {
            return true;
        }

        if (thisKind == JsonValueKind.Null)
        {
            return true;
        }

        if (thisKind == JsonValueKind.Undefined)
        {
            return false;
        }

        if (thisKind == JsonValueKind.Number)
        {
            return CompareNumbers(item1.AsNumber, item2.AsNumber);
        }

        if (thisKind == JsonValueKind.Object)
        {
            return CompareObjects(item1.AsObject, item2.AsObject);
        }

        if (thisKind == JsonValueKind.String)
        {
            return CompareStrings(item1.AsString, item2.AsString);
        }

        return false;
    }

    /// <summary>
    /// Compares two values.
    /// </summary>
    /// <typeparam name="TItem">The type of the other value.</typeparam>
    /// <param name="item1">The first value.</param>
    /// <param name="item2">The second value.</param>
    /// <returns><c>True</c> if they are equal.</returns>
    /// <exception cref="InvalidOperationException">The values were not numbers.</exception>
    public static bool CompareValues<TItem>(in TItem item1, in JsonAny item2)
        where TItem : struct, IJsonValue<TItem>
    {
        JsonValueKind thisKind = item1.ValueKind;
        JsonValueKind otherKind = item2.ValueKind;

        if (thisKind != otherKind)
        {
            return false;
        }

        if (thisKind == JsonValueKind.Array)
        {
            return CompareArrays(item1.AsArray, item2.AsArray);
        }

        if (thisKind == JsonValueKind.False || thisKind == JsonValueKind.True)
        {
            return true;
        }

        if (thisKind == JsonValueKind.Null)
        {
            return true;
        }

        if (thisKind == JsonValueKind.Undefined)
        {
            return false;
        }

        if (thisKind == JsonValueKind.Number)
        {
            return CompareNumbers(item1.AsNumber, item2.AsNumber);
        }

        if (thisKind == JsonValueKind.Object)
        {
            return CompareObjects(item1.AsObject, item2.AsObject);
        }

        if (thisKind == JsonValueKind.String)
        {
            return CompareStrings(item1.AsString, item2.AsString);
        }

        return false;
    }

    /// <summary>
    /// Compares two values.
    /// </summary>
    /// <typeparam name="TItem">The type of the other value.</typeparam>
    /// <param name="item1">The first value.</param>
    /// <param name="item2">The second value.</param>
    /// <returns><c>True</c> if they are equal.</returns>
    /// <exception cref="InvalidOperationException">The values were not numbers.</exception>
    public static bool CompareValues<TItem>(in JsonAny item1, in TItem item2)
        where TItem : struct, IJsonValue<TItem>
    {
        JsonValueKind thisKind = item1.ValueKind;
        JsonValueKind otherKind = item2.ValueKind;

        if (thisKind != otherKind)
        {
            return false;
        }

        if (thisKind == JsonValueKind.Array)
        {
            return CompareArrays(item1.AsArray, item2.AsArray);
        }

        if (thisKind == JsonValueKind.False || thisKind == JsonValueKind.True)
        {
            return true;
        }

        if (thisKind == JsonValueKind.Null)
        {
            return true;
        }

        if (thisKind == JsonValueKind.Undefined)
        {
            return false;
        }

        if (thisKind == JsonValueKind.Number)
        {
            return CompareNumbers(item1.AsNumber, item2.AsNumber);
        }

        if (thisKind == JsonValueKind.Object)
        {
            return CompareObjects(item1.AsObject, item2.AsObject);
        }

        if (thisKind == JsonValueKind.String)
        {
            return CompareStrings(item1.AsString, item2.AsString);
        }

        return false;
    }

    /// <summary>
    /// Compares two values.
    /// </summary>
    /// <param name="item1">The first value.</param>
    /// <param name="item2">The second value.</param>
    /// <returns><c>True</c> if they are equal.</returns>
    /// <exception cref="InvalidOperationException">The values were not numbers.</exception>
    public static bool CompareValues(in JsonAny item1, in JsonAny item2)
    {
        JsonValueKind thisKind = item1.ValueKind;
        JsonValueKind otherKind = item2.ValueKind;

        if (thisKind != otherKind)
        {
            return false;
        }

        if (thisKind == JsonValueKind.Array)
        {
            return CompareArrays(item1.AsArray, item2.AsArray);
        }

        if (thisKind == JsonValueKind.False || thisKind == JsonValueKind.True)
        {
            return true;
        }

        if (thisKind == JsonValueKind.Null)
        {
            return true;
        }

        if (thisKind == JsonValueKind.Undefined)
        {
            return true;
        }

        if (thisKind == JsonValueKind.Number)
        {
            return CompareNumbers(item1.AsNumber, item2.AsNumber);
        }

        if (thisKind == JsonValueKind.Object)
        {
            return CompareObjects(item1.AsObject, item2.AsObject);
        }

        if (thisKind == JsonValueKind.String)
        {
            return CompareStrings(item1.AsString, item2.AsString);
        }

        return false;
    }

    /// <summary>
    /// Compares two object values.
    /// </summary>
    /// <typeparam name="TItem1">The type of the first object value.</typeparam>
    /// <typeparam name="TItem2">The type of the second object value.</typeparam>
    /// <param name="item1">The first value.</param>
    /// <param name="item2">The second value.</param>
    /// <returns><c>True</c> if they are equal.</returns>
    /// <exception cref="InvalidOperationException">The values were not objects.</exception>
    public static bool CompareObjects<TItem1, TItem2>(in TItem1 item1, in TItem2 item2)
        where TItem1 : struct, IJsonObject<TItem1>
        where TItem2 : struct, IJsonObject<TItem2>
    {
        int count = 0;
        foreach (JsonObjectProperty property in item1.EnumerateObject())
        {
            if (!item2.TryGetProperty(property.Name, out JsonAny value) || !property.Value.Equals(value))
            {
                return false;
            }

            count++;
        }

        int otherCount = 0;
        foreach (JsonObjectProperty otherProperty in item2.EnumerateObject())
        {
            otherCount++;
            if (otherCount > count)
            {
                return false;
            }
        }

        return count == otherCount;
    }

    /// <summary>
    /// Compares two array values.
    /// </summary>
    /// <typeparam name="TItem1">The type of the first array value.</typeparam>
    /// <typeparam name="TItem2">The type of the second array value.</typeparam>
    /// <param name="item1">The first value.</param>
    /// <param name="item2">The second value.</param>
    /// <returns><c>True</c> if they are equal.</returns>
    /// <exception cref="InvalidOperationException">The values were not arrays.</exception>
    public static bool CompareArrays<TItem1, TItem2>(in TItem1 item1, in TItem2 item2)
        where TItem1 : struct, IJsonArray<TItem1>
        where TItem2 : struct, IJsonArray<TItem2>
    {
        JsonArrayEnumerator lhs = item1.EnumerateArray();
        JsonArrayEnumerator rhs = item2.EnumerateArray();
        while (lhs.MoveNext())
        {
            if (!rhs.MoveNext())
            {
                return false;
            }

            if (!lhs.Current.Equals(rhs.Current))
            {
                return false;
            }
        }

        return !rhs.MoveNext();
    }

    /// <summary>
    /// Compares two numeric values.
    /// </summary>
    /// <typeparam name="TItem1">The type of the first numeric value.</typeparam>
    /// <typeparam name="TItem2">The type of the second numeric value.</typeparam>
    /// <param name="item1">The first value.</param>
    /// <param name="item2">The second value.</param>
    /// <returns><c>True</c> if they are equal.</returns>
    /// <exception cref="InvalidOperationException">The values were not numbers.</exception>
    public static bool CompareNumbers<TItem1, TItem2>(in TItem1 item1, in TItem2 item2)
        where TItem1 : struct, IJsonNumber<TItem1>
        where TItem2 : struct, IJsonNumber<TItem2>
    {
        return ((double)item1).Equals((double)item2);
    }

    /// <summary>
    /// Compares two string values.
    /// </summary>
    /// <typeparam name="TItem1">The type of the first string value.</typeparam>
    /// <typeparam name="TItem2">The type of the second string value.</typeparam>
    /// <param name="item1">The first value.</param>
    /// <param name="item2">The second value.</param>
    /// <returns><c>True</c> if they are equal.</returns>
    /// <exception cref="InvalidOperationException">The values were not strings.</exception>
    public static bool CompareStrings<TItem1, TItem2>(in TItem1 item1, in TItem2 item2)
        where TItem1 : struct, IJsonString<TItem1>
        where TItem2 : struct, IJsonString<TItem2>
    {
        if (item1.HasJsonElementBacking)
        {
            if (item2.HasDotnetBacking)
            {
                return item1.AsJsonElement.ValueEquals((string)item2);
            }
            else
            {
                item2.AsJsonElement.TryGetValue(CompareValues, item1.AsJsonElement, out bool areEqual);
                return areEqual;
            }
        }

        if (item2.HasJsonElementBacking)
        {
            if (item1.HasDotnetBacking)
            {
                return item2.AsJsonElement.ValueEquals((string)item1);
            }
            else
            {
                item1.AsJsonElement.TryGetValue(CompareValues, item2.AsJsonElement, out bool areEqual);
                return areEqual;
            }
        }

        return ((string)item1).Equals((string)item2, StringComparison.Ordinal);

        static bool CompareValues(ReadOnlySpan<byte> span, in JsonElement firstItem, out bool value)
        {
            value = firstItem.ValueEquals(span);
            return true;
        }
    }
}