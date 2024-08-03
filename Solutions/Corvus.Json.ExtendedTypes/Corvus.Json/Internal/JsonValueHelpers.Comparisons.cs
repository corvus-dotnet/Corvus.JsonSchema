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
    /// <param name="item1">The first value.</param>
    /// <param name="item2">The second value.</param>
    /// <returns><c>True</c> if they are equal.</returns>
    public static bool CompareWithString<TItem1>(in TItem1 item1, ReadOnlySpan<char> item2)
        where TItem1 : struct, IJsonValue<TItem1>
    {
        JsonValueKind thisKind = item1.ValueKind;

        if (thisKind != JsonValueKind.String)
        {
            return false;
        }

        if (thisKind == JsonValueKind.String)
        {
            return item1.AsString.EqualsString(item2);
        }

        return false;
    }

    /// <summary>
    /// Compares two values.
    /// </summary>
    /// <typeparam name="TItem1">The type of the first value.</typeparam>
    /// <param name="item1">The first value.</param>
    /// <param name="item2">The second value.</param>
    /// <returns><c>True</c> if they are equal.</returns>
    public static bool CompareWithUtf8Bytes<TItem1>(in TItem1 item1, ReadOnlySpan<byte> item2)
        where TItem1 : struct, IJsonValue<TItem1>
    {
        JsonValueKind thisKind = item1.ValueKind;

        if (thisKind != JsonValueKind.String)
        {
            return false;
        }

        if (thisKind == JsonValueKind.String)
        {
            return item1.AsString.EqualsUtf8Bytes(item2);
        }

        return false;
    }

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

        return thisKind switch
        {
            JsonValueKind.Array => CompareArrays(item1.AsArray, item2.AsArray),
            JsonValueKind.False or JsonValueKind.True => true,
            JsonValueKind.Null => true,
            JsonValueKind.Undefined => false,
            JsonValueKind.Number => CompareNumbers(item1.AsNumber, item2.AsNumber),
            JsonValueKind.Object => CompareObjects(item1.AsObject, item2.AsObject),
            JsonValueKind.String => CompareStrings(item1.AsString, item2.AsString),
            _ => false,
        };
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

        return thisKind switch
        {
            JsonValueKind.Array => CompareArrays(item1.AsArray, item2.AsArray),
            JsonValueKind.False or JsonValueKind.True => true,
            JsonValueKind.Null => true,
            JsonValueKind.Undefined => false,
            JsonValueKind.Number => item1.AsNumber.Equals(item2.AsNumber),
            JsonValueKind.Object => CompareObjects(item1.AsObject, item2.AsObject),
            JsonValueKind.String => CompareStrings(item1.AsString, item2.AsString),
            _ => false,
        };
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

        return thisKind switch
        {
            JsonValueKind.Array => CompareArrays(item1.AsArray, item2.AsArray),
            JsonValueKind.False or JsonValueKind.True => true,
            JsonValueKind.Null => true,
            JsonValueKind.Undefined => false,
            JsonValueKind.Number => item1.AsNumber.Equals(item2.AsNumber),
            JsonValueKind.Object => CompareObjects(item1.AsObject, item2.AsObject),
            JsonValueKind.String => CompareStrings(item1.AsString, item2.AsString),
            _ => false,
        };
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

        return thisKind switch
        {
            JsonValueKind.Array => CompareArrays(item1.AsArray, item2.AsArray),
            JsonValueKind.False or JsonValueKind.True => true,
            JsonValueKind.Null => true,
            JsonValueKind.Undefined => true,
            JsonValueKind.Number => item1.AsNumber.Equals(item2.AsNumber),
            JsonValueKind.Object => CompareObjects(item1.AsObject, item2.AsObject),
            JsonValueKind.String => CompareStrings(item1.AsString, item2.AsString),
            _ => false,
        };
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
    /// Compares two number values.
    /// </summary>
    /// <typeparam name="TItem1">The type of the first number value.</typeparam>
    /// <typeparam name="TItem2">The type of the second number value.</typeparam>
    /// <param name="item1">The first value.</param>
    /// <param name="item2">The second value.</param>
    /// <returns><c>True</c> if they are equal.</returns>
    /// <exception cref="InvalidOperationException">The values were not numbers.</exception>
    public static bool CompareNumbers<TItem1, TItem2>(in TItem1 item1, in TItem2 item2)
        where TItem1 : struct, IJsonNumber<TItem1>
        where TItem2 : struct, IJsonNumber<TItem2>
    {
        JsonValueKind item1ValueKind = item1.ValueKind;
        JsonValueKind item2ValueKind = item2.ValueKind;

        if (item1ValueKind != item2ValueKind)
        {
            // We can't be equal if we are not the same underlying type
            return false;
        }

        if (item1.IsNull())
        {
            // Nulls are always equal
            return true;
        }

        if (item1ValueKind != JsonValueKind.Number)
        {
            // Not a number is invalid
            throw new InvalidOperationException();
        }

        if (item1.HasDotnetBacking && item2.HasDotnetBacking)
        {
            return BinaryJsonNumber.Equals(item1.AsBinaryJsonNumber, item2.AsBinaryJsonNumber);
        }

        // After item1 point there is no need to check both value kinds because our first quick test verified that they were the same.
        // If either one is a Backing.Number or a JsonValueKind.Number then we know the item2 is compatible.
        if (item1.HasDotnetBacking && !item2.HasDotnetBacking)
        {
            return BinaryJsonNumber.Equals(item1.AsBinaryJsonNumber, item2.AsJsonElement);
        }

        if (item1.HasJsonElementBacking &&
            item2.HasDotnetBacking)
        {
            return BinaryJsonNumber.Equals(item2.AsBinaryJsonNumber, item1.AsJsonElement);
        }

        return JsonValueHelpers.NumericEquals(item1.AsJsonElement, item2.AsJsonElement);
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
#if NET8_0_OR_GREATER
                return item1.AsJsonElement.ValueEquals((string)item2);
#else
                return item1.AsJsonElement.ValueEquals((string)item2.AsString);
#endif
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
#if NET8_0_OR_GREATER
                return item2.AsJsonElement.ValueEquals((string)item1);
#else
                return item2.AsJsonElement.ValueEquals((string)item1.AsString);
#endif

            }
            else
            {
                item1.AsJsonElement.TryGetValue(CompareValues, item2.AsJsonElement, out bool areEqual);
                return areEqual;
            }
        }

#if NET8_0_OR_GREATER
        return ((string)item1).Equals((string)item2, StringComparison.Ordinal);
#else
        return ((string)item1.AsString).Equals((string)item2.AsString, StringComparison.Ordinal);
#endif

        static bool CompareValues(ReadOnlySpan<byte> span, in JsonElement firstItem, out bool value)
        {
            value = firstItem.ValueEquals(span);
            return true;
        }
    }
}