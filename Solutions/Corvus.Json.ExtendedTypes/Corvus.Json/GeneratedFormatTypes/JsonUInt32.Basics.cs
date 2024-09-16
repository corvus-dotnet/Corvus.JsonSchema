// <copyright file="JsonUInt32.Basics.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Numerics;
using System.Text.Json;

using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON uint32.
/// </summary>
public readonly partial struct JsonUInt32
#if NET8_0_OR_GREATER
 : IAdditionOperators<JsonUInt32, JsonUInt32, JsonUInt32>,
   ISubtractionOperators<JsonUInt32, JsonUInt32, JsonUInt32>,
   IMultiplyOperators<JsonUInt32, JsonUInt32, JsonUInt32>,
   IDivisionOperators<JsonUInt32, JsonUInt32, JsonUInt32>,
   IIncrementOperators<JsonUInt32>,
   IDecrementOperators<JsonUInt32>
#endif
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonUInt32"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public JsonUInt32(uint value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = new(value);
    }

    /// <summary>
    /// Conversion from JsonNumber.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonUInt32(JsonNumber value)
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
    public static implicit operator JsonNumber(JsonUInt32 value)
    {
        return value.AsNumber;
    }

    /// <summary>
    /// Conversion to JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonAny(JsonUInt32 value)
    {
        return value.AsAny;
    }

    /// <summary>
    /// Conversion from JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonUInt32(JsonAny value)
    {
        return value.As<JsonUInt32>();
    }

    /// <summary>
    /// Conversion to byte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a byte.</exception>
    public static explicit operator byte(JsonUInt32 value)
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
    public static explicit operator decimal(JsonUInt32 value)
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
    public static explicit operator double(JsonUInt32 value)
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
    public static explicit operator short(JsonUInt32 value)
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
    public static explicit operator int(JsonUInt32 value)
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
    public static explicit operator long(JsonUInt32 value)
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

#if NET8_0_OR_GREATER
    /// <summary>
    /// Conversion to Int128.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an Int64.</exception>
    public static explicit operator Int128(JsonUInt32 value)
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
#endif

    /// <summary>
    /// Conversion to SByte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an SByte.</exception>
    public static explicit operator sbyte(JsonUInt32 value)
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

#if NET8_0_OR_GREATER
    /// <summary>
    /// Conversion to Half.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a Single.</exception>
    public static explicit operator Half(JsonUInt32 value)
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
#endif

    /// <summary>
    /// Conversion to Single.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a Single.</exception>
    public static explicit operator float(JsonUInt32 value)
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
    public static explicit operator ushort(JsonUInt32 value)
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
    public static implicit operator uint(JsonUInt32 value)
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
    public static explicit operator ulong(JsonUInt32 value)
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

#if NET8_0_OR_GREATER
    /// <summary>
    /// Conversion to UInt64.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an UInt64.</exception>
    public static explicit operator UInt128(JsonUInt32 value)
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
#endif

    /// <summary>
    /// Conversion from uint32.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonUInt32(uint value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Adds two values together to compute their sum.
    /// </summary>
    /// <param name="left">The left hand side.</param>
    /// <param name="right">The right hand side.</param>
    /// <returns>The resulting value.</returns>
    public static JsonUInt32 operator +(JsonUInt32 left, JsonUInt32 right)
    {
        return new(left.AsBinaryJsonNumber + right.AsBinaryJsonNumber);
    }

    /// <summary>
    /// Subtracts two values together to compute their difference.
    /// </summary>
    /// <param name="left">The left hand side.</param>
    /// <param name="right">The right hand side.</param>
    /// <returns>The resulting value.</returns>
    public static JsonUInt32 operator -(JsonUInt32 left, JsonUInt32 right)
    {
        return new(left.AsBinaryJsonNumber - right.AsBinaryJsonNumber);
    }

    /// <summary>
    /// Multiplies two values together.
    /// </summary>
    /// <param name="left">The left hand side.</param>
    /// <param name="right">The right hand side.</param>
    /// <returns>The resulting value.</returns>
    public static JsonUInt32 operator *(JsonUInt32 left, JsonUInt32 right)
    {
        return new(left.AsBinaryJsonNumber * right.AsBinaryJsonNumber);
    }

    /// <summary>
    /// Divides two values.
    /// </summary>
    /// <param name="left">The left hand side.</param>
    /// <param name="right">The right hand side.</param>
    /// <returns>The resulting value.</returns>
    public static JsonUInt32 operator /(JsonUInt32 left, JsonUInt32 right)
    {
        return new(left.AsBinaryJsonNumber / right.AsBinaryJsonNumber);
    }

    /// <summary>
    /// Increments the value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The resulting value.</returns>
    public static JsonUInt32 operator ++(JsonUInt32 value)
    {
        BinaryJsonNumber num = value.AsBinaryJsonNumber;
        return new(num++);
    }

    /// <summary>
    /// Decrements the value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The resulting value.</returns>
    public static JsonUInt32 operator --(JsonUInt32 value)
    {
        BinaryJsonNumber num = value.AsBinaryJsonNumber;
        return new(num--);
    }
}