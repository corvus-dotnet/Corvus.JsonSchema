// <copyright file="JsonNumber.Basics.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a Json number.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Corvus.Json.Internal.JsonValueConverter<JsonNumber>))]
public readonly partial struct JsonNumber
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonNumber(double value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = value;
    }

    /// <summary>
    /// Conversion from JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonNumber(JsonAny value)
    {
        return value.AsNumber;
    }

    /// <summary>
    /// Conversion to JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonAny(JsonNumber value)
    {
        return value.AsAny;
    }

    /// <summary>
    /// Conversion from double.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonNumber(double value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to double.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator double(JsonNumber value)
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
    public static implicit operator JsonNumber(float value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to double.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a float.</exception>
    public static implicit operator float(JsonNumber value)
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
    public static implicit operator JsonNumber(int value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to int.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an int.</exception>
    public static implicit operator int(JsonNumber value)
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
    public static implicit operator JsonNumber(long value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to long.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a long.</exception>
    public static implicit operator long(JsonNumber value)
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
    public static explicit operator JsonNumber(uint value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to uint.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a uint.</exception>
    public static implicit operator uint(JsonNumber value)
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
    public static implicit operator JsonNumber(ushort value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to ushort.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a ushort.</exception>
    public static implicit operator ushort(JsonNumber value)
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
    public static implicit operator JsonNumber(ulong value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to ulong.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a ulong.</exception>
    public static implicit operator ulong(JsonNumber value)
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
    public static implicit operator JsonNumber(byte value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to byte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a byte.</exception>
    public static implicit operator byte(JsonNumber value)
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
    public static implicit operator JsonNumber(sbyte value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to sbyte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an sbyte.</exception>
    public static implicit operator sbyte(JsonNumber value)
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