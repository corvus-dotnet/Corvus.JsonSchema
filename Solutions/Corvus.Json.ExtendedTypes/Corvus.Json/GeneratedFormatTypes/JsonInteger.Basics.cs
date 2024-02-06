// <copyright file="JsonInteger.Basics.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Numerics;
using System.Text.Json;

using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON integer.
/// </summary>
public readonly partial struct JsonInteger
 : IAdditionOperators<JsonInteger, JsonInteger, JsonInteger>,
   ISubtractionOperators<JsonInteger, JsonInteger, JsonInteger>,
   IMultiplyOperators<JsonInteger, JsonInteger, JsonInteger>,
   IDivisionOperators<JsonInteger, JsonInteger, JsonInteger>,
   IIncrementOperators<JsonInteger>,
   IDecrementOperators<JsonInteger>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonInteger"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonInteger(short value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonInteger"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonInteger(int value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonInteger"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonInteger(long value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonInteger"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonInteger(Int128 value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonInteger"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonInteger(byte value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonInteger"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonInteger(sbyte value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonInteger"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonInteger(ushort value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonInteger"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonInteger(uint value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonInteger"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonInteger(ulong value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonInteger"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonInteger(UInt128 value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Conversion from JsonNumber.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonInteger(JsonNumber value)
    {
        if (value.HasDotnetBacking && value.ValueKind == JsonValueKind.Number)
        {
            return new(value.AsBinaryJsonNumber);
        }

        return new(value.AsJsonElement);
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
    /// Conversion from JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonInteger(JsonAny value)
    {
        return value.As<JsonInteger>();
    }

    /// <summary>
    /// Conversion to byte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a byte.</exception>
    public static explicit operator byte(JsonInteger value)
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
    public static explicit operator decimal(JsonInteger value)
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
    public static explicit operator double(JsonInteger value)
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
    public static explicit operator short(JsonInteger value)
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
    public static explicit operator int(JsonInteger value)
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
    public static explicit operator long(JsonInteger value)
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
    /// Conversion to Int128.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an Int64.</exception>
    public static explicit operator Int128(JsonInteger value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetInt128();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return value.numberBacking.CreateChecked<Int128>();
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion to SByte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an SByte.</exception>
    public static explicit operator sbyte(JsonInteger value)
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
    public static explicit operator Half(JsonInteger value)
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
    public static explicit operator float(JsonInteger value)
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
    public static explicit operator ushort(JsonInteger value)
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
    public static explicit operator uint(JsonInteger value)
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
    public static explicit operator ulong(JsonInteger value)
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
    /// Conversion to UInt64.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an UInt64.</exception>
    public static explicit operator UInt128(JsonInteger value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetUInt128();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return value.numberBacking.CreateChecked<UInt128>();
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion from decimal.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator JsonInteger(decimal value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from double.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator JsonInteger(double value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from single.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator JsonInteger(float value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from Half.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator JsonInteger(Half value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from byte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator JsonInteger(byte value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from sbyte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator JsonInteger(sbyte value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from int16.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator JsonInteger(short value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from uint16.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator JsonInteger(ushort value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from int32.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator JsonInteger(int value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from uint32.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator JsonInteger(uint value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from int64.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonInteger(long value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from ulong.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator JsonInteger(ulong value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from int128.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator JsonInteger(Int128 value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from uint128.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator JsonInteger(UInt128 value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Adds two values together to compute their sum.
    /// </summary>
    /// <param name="left">The left hand side.</param>
    /// <param name="right">The right hand side.</param>
    /// <returns>The resulting value.</returns>
    public static JsonInteger operator +(JsonInteger left, JsonInteger right)
    {
        return new(left.AsBinaryJsonNumber + right.AsBinaryJsonNumber);
    }

    /// <summary>
    /// Subtracts two values together to compute their difference.
    /// </summary>
    /// <param name="left">The left hand side.</param>
    /// <param name="right">The right hand side.</param>
    /// <returns>The resulting value.</returns>
    public static JsonInteger operator -(JsonInteger left, JsonInteger right)
    {
        return new(left.AsBinaryJsonNumber - right.AsBinaryJsonNumber);
    }

    /// <summary>
    /// Multiplies two values together.
    /// </summary>
    /// <param name="left">The left hand side.</param>
    /// <param name="right">The right hand side.</param>
    /// <returns>The resulting value.</returns>
    public static JsonInteger operator *(JsonInteger left, JsonInteger right)
    {
        return new(left.AsBinaryJsonNumber * right.AsBinaryJsonNumber);
    }

    /// <summary>
    /// Divides two values.
    /// </summary>
    /// <param name="left">The left hand side.</param>
    /// <param name="right">The right hand side.</param>
    /// <returns>The resulting value.</returns>
    public static JsonInteger operator /(JsonInteger left, JsonInteger right)
    {
        return new(left.AsBinaryJsonNumber / right.AsBinaryJsonNumber);
    }

    /// <summary>
    /// Increments the value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The resulting value.</returns>
    public static JsonInteger operator ++(JsonInteger value)
    {
        BinaryJsonNumber num = value.AsBinaryJsonNumber;
        return new(num++);
    }

    /// <summary>
    /// Decrements the value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The resulting value.</returns>
    public static JsonInteger operator --(JsonInteger value)
    {
        BinaryJsonNumber num = value.AsBinaryJsonNumber;
        return new(num--);
    }
}