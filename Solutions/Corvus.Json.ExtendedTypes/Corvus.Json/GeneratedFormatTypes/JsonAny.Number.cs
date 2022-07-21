// <copyright file="JsonAny.Number.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents any JSON value.
/// </summary>
public readonly partial struct JsonAny : IJsonNumber<JsonAny>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonAny"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonAny(double value)
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
    public static implicit operator JsonAny(double value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to double.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator double(JsonAny value)
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
    public static implicit operator JsonAny(float value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to double.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator float(JsonAny value)
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
    public static implicit operator JsonAny(int value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to int.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator int(JsonAny value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.GetInt32();
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
    public static implicit operator JsonAny(long value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to long.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator long(JsonAny value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.GetInt64();
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
    public static explicit operator JsonAny(uint value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to uint.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator uint(JsonAny value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.GetUInt32();
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
    public static implicit operator JsonAny(ushort value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to ushort.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator ushort(JsonAny value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.GetUInt16();
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
    public static implicit operator JsonAny(ulong value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to ulong.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator ulong(JsonAny value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.GetUInt64();
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
    public static implicit operator JsonAny(byte value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to byte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator byte(JsonAny value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.GetByte();
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
    public static implicit operator JsonAny(sbyte value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to sbyte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator sbyte(JsonAny value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.GetSByte();
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