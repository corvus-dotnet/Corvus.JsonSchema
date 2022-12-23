// <copyright file="JsonNotAny.Number.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents any JSON value, validating false.
/// </summary>
public readonly partial struct JsonNotAny : IJsonNumber<JsonNotAny>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonNotAny(double value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.stringBacking = string.Empty;
        this.boolBacking = default;
        this.numberBacking = value;
        this.arrayBacking = ImmutableList<JsonAny>.Empty;
        this.objectBacking = ImmutableDictionary<JsonPropertyName, JsonAny>.Empty;
    }

    /// <summary>
    /// Conversion from double.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonNotAny(double value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to double.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator double(JsonNotAny value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.GetDouble();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return value.numberBacking;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion from float.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonNotAny(float value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to double.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator float(JsonNotAny value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.GetSingle();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            if (value.numberBacking < float.MinValue || value.numberBacking > float.MaxValue)
            {
                throw new FormatException();
            }

            return (float)value.numberBacking;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion from int.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonNotAny(int value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to int.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator int(JsonNotAny value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetInt32();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            if (value.numberBacking < int.MinValue || value.numberBacking > int.MaxValue)
            {
                throw new FormatException();
            }

            return (int)value.numberBacking;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion from long.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonNotAny(long value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to long.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator long(JsonNotAny value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetInt64();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            if (value.numberBacking < long.MinValue || value.numberBacking > long.MaxValue)
            {
                throw new FormatException();
            }

            return (long)value.numberBacking;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion from uint.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator JsonNotAny(uint value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to uint.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator uint(JsonNotAny value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetUInt32();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            if (value.numberBacking < uint.MinValue || value.numberBacking > uint.MaxValue)
            {
                throw new FormatException();
            }

            return (uint)value.numberBacking;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion from ushort.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonNotAny(ushort value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to ushort.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator ushort(JsonNotAny value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetUInt16();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            if (value.numberBacking < ushort.MinValue || value.numberBacking > ushort.MaxValue)
            {
                throw new FormatException();
            }

            return (ushort)value.numberBacking;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion from ulong.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonNotAny(ulong value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to ulong.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator ulong(JsonNotAny value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetUInt64();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            if (value.numberBacking < ulong.MinValue || value.numberBacking > ulong.MaxValue)
            {
                throw new FormatException();
            }

            return (ulong)value.numberBacking;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion from byte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonNotAny(byte value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to byte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator byte(JsonNotAny value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetByte();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            if (value.numberBacking < byte.MinValue || value.numberBacking > byte.MaxValue)
            {
                throw new FormatException();
            }

            return (byte)value.numberBacking;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion from sbyte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonNotAny(sbyte value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to sbyte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator sbyte(JsonNotAny value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetSByte();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            if (value.numberBacking < sbyte.MinValue || value.numberBacking > sbyte.MaxValue)
            {
                throw new FormatException();
            }

            return (sbyte)value.numberBacking;
        }

        throw new InvalidOperationException();
    }
}