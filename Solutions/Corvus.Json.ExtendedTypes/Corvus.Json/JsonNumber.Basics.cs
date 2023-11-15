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
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonNumber(byte value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonNumber(decimal value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonNumber(double value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonNumber(Half value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonNumber(short value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonNumber(int value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonNumber(long value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonNumber(Int128 value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonNumber(sbyte value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonNumber(float value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonNumber(ushort value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonNumber(uint value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonNumber(ulong value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonNumber(UInt128 value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Conversion to byte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a byte.</exception>
    public static explicit operator byte(JsonNumber value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetByte();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return value.numberBacking.CreateChecked<byte>();
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion to decimal.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a decimal.</exception>
    public static explicit operator decimal(JsonNumber value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetDecimal();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return value.numberBacking.CreateChecked<decimal>();
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion to double.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static explicit operator double(JsonNumber value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetDouble();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return value.numberBacking.CreateChecked<double>();
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion to Int16.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an Int16.</exception>
    public static explicit operator short(JsonNumber value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetInt16();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return value.numberBacking.CreateChecked<short>();
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion to Int32.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an Int32.</exception>
    public static explicit operator int(JsonNumber value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetInt32();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return value.numberBacking.CreateChecked<int>();
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion to Int64.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an Int64.</exception>
    public static explicit operator long(JsonNumber value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetInt64();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return value.numberBacking.CreateChecked<long>();
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion to SByte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an SByte.</exception>
    public static explicit operator sbyte(JsonNumber value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetSByte();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return value.numberBacking.CreateChecked<sbyte>();
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion to Half.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a Single.</exception>
    public static explicit operator Half(JsonNumber value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetHalf();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return value.numberBacking.CreateChecked<Half>();
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion to Single.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a Single.</exception>
    public static explicit operator float(JsonNumber value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetSingle();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return value.numberBacking.CreateChecked<float>();
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion to UInt16.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an UInt16.</exception>
    public static explicit operator ushort(JsonNumber value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetUInt16();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return value.numberBacking.CreateChecked<ushort>();
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion to UInt32.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an UInt32.</exception>
    public static explicit operator uint(JsonNumber value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetUInt32();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return value.numberBacking.CreateChecked<uint>();
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion to UInt64.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an UInt64.</exception>
    public static explicit operator ulong(JsonNumber value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetUInt64();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return value.numberBacking.CreateChecked<ulong>();
        }

        throw new InvalidOperationException();
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
    /// Conversion from byte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonNumber(byte value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion from decimal.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonNumber(decimal value)
    {
        return new(value);
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
    /// Conversion from Half.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonNumber(Half value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion from short.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonNumber(short value)
    {
        return new(value);
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
    /// Conversion from long.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonNumber(long value)
    {
        return new(value);
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
    /// Conversion from float.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonNumber(float value)
    {
        return new(value);
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
    /// Conversion from uint.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonNumber(uint value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion from ulong.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonNumber(ulong value)
    {
        return new(value);
    }
}