// <copyright file="JsonInteger.Basics.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
    private JsonInteger(double value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = value;
    }

    /// <summary>
    /// Conversion from JsonNumber.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonInteger(JsonNumber value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return new((double)value);
    }

    /// <summary>
    /// Conversion to JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonNumber(JsonInteger value)
    {
        return value.AsNumber;
    }

    /// <summary>
    /// Conversion from JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonInteger(JsonAny value)
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
    /// Conversion from double.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonInteger(double value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to double.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static implicit operator double(JsonInteger value)
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
    public static implicit operator JsonInteger(float value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to double.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a float.</exception>
    public static implicit operator float(JsonInteger value)
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
    public static implicit operator JsonInteger(int value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to int.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an int.</exception>
    public static implicit operator int(JsonInteger value)
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
    public static implicit operator JsonInteger(long value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to long.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a long.</exception>
    public static implicit operator long(JsonInteger value)
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
    public static explicit operator JsonInteger(uint value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to uint.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a uint.</exception>
    public static implicit operator uint(JsonInteger value)
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
    public static implicit operator JsonInteger(ushort value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to ushort.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a ushort.</exception>
    public static implicit operator ushort(JsonInteger value)
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
    public static implicit operator JsonInteger(ulong value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to ulong.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a ulong.</exception>
    public static implicit operator ulong(JsonInteger value)
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
    public static implicit operator JsonInteger(byte value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to byte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a byte.</exception>
    public static implicit operator byte(JsonInteger value)
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
    public static implicit operator JsonInteger(sbyte value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to sbyte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an sbyte.</exception>
    public static implicit operator sbyte(JsonInteger value)
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