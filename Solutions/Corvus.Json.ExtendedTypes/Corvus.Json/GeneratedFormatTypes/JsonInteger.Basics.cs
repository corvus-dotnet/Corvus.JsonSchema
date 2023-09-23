// <copyright file="JsonInteger.Basics.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON integer.
/// </summary>
public readonly partial struct JsonInteger
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonInteger"/> struct.
    /// </summary>
    private JsonInteger(BinaryJsonNumber value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = value;
    }

    /// <summary>
    /// Conversion to JsonNumber.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonNumber(JsonInteger value)
    {
        return value.AsNumber;
    }

    /// <summary>
    /// Conversion to JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonAny(JsonInteger value)
    {
        return value.AsAny;
    }

    /// <summary>
    /// Less than operator.
    /// </summary>
    /// <param name="left">The LHS of the comparison.</param>
    /// <param name="right">The RHS of the comparison.</param>
    /// <returns><see langword="true"/> if the left is less than the right, otherwise <see langword="false"/>.</returns>
    public static bool operator <(in JsonInteger left, in JsonInteger right)
    {
        return Compare(left, right) < 0;
    }

    /// <summary>
    /// Greater than operator.
    /// </summary>
    /// <param name="left">The LHS of the comparison.</param>
    /// <param name="right">The RHS of the comparison.</param>
    /// <returns><see langword="true"/> if the left is greater than the right, otherwise <see langword="false"/>.</returns>
    public static bool operator >(in JsonInteger left, in JsonInteger right)
    {
        return Compare(left, right) > 0;
    }

    /// <summary>
    /// Less than operator.
    /// </summary>
    /// <param name="left">The LHS of the comparison.</param>
    /// <param name="right">The RHS of the comparison.</param>
    /// <returns><see langword="true"/> if the left is less than the right, otherwise <see langword="false"/>.</returns>
    public static bool operator <=(in JsonInteger left, in JsonInteger right)
    {
        return Compare(left, right) <= 0;
    }

    /// <summary>
    /// Greater than operator.
    /// </summary>
    /// <param name="left">The LHS of the comparison.</param>
    /// <param name="right">The RHS of the comparison.</param>
    /// <returns><see langword="true"/> if the left is greater than the right, otherwise <see langword="false"/>.</returns>
    public static bool operator >=(in JsonInteger left, in JsonInteger right)
    {
        return Compare(left, right) >= 0;
    }

    /// <summary>
    /// Compare with another number.
    /// </summary>
    /// <param name="lhs">The lhs of the comparison.</param>
    /// <param name="rhs">The rhs of the comparison.</param>
    /// <returns>0 if the numbers are equal, -1 if the lhs is less than the rhs, and 1 if the lhs is greater than the rhs.</returns>
    public static int Compare(in JsonInteger lhs, in JsonInteger rhs)
    {
        if (lhs.ValueKind != rhs.ValueKind)
        {
            // We can't be equal if we are not the same underlying type
            return -1;
        }

        if (lhs.IsNull())
        {
            // Nulls are always equal
            return 0;
        }

        if (lhs.backing == Backing.Number &&
            rhs.backing == Backing.Number)
        {
            return BinaryJsonNumber.Compare(lhs.numberBacking, rhs.numberBacking);
        }

        // After this point there is no need to check both value kinds because our first quick test verified that they were the same.
        // If either one is a Backing.Number or a JsonValueKind.Number then we know the rhs is conmpatible.
        if (lhs.backing == Backing.Number &&
            rhs.backing == Backing.Number)
        {
            return BinaryJsonNumber.Compare(lhs.numberBacking, rhs.numberBacking);
        }

        if (lhs.backing == Backing.Number &&
            rhs.backing == Backing.JsonElement)
        {
            return BinaryJsonNumber.Compare(lhs.numberBacking, rhs.jsonElementBacking);
        }

        if (lhs.backing == Backing.JsonElement && rhs.backing == Backing.Number)
        {
            return BinaryJsonNumber.Compare(lhs.jsonElementBacking, rhs.numberBacking);
        }

        if (lhs.backing == Backing.JsonElement && rhs.backing == Backing.JsonElement && rhs.jsonElementBacking.ValueKind == JsonValueKind.Number)
        {
            return JsonValueHelpers.NumericCompare(lhs.jsonElementBacking, rhs.jsonElementBacking);
        }

        throw new InvalidOperationException();
    }
}