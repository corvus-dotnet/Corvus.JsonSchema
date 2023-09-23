// <copyright file="BinaryJsonNumber.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using CommunityToolkit.HighPerformance;

namespace Corvus.Json;

/// <summary>
/// A Binary representation of a JSON number.
/// </summary>
public readonly struct BinaryJsonNumber : IEquatable<BinaryJsonNumber>, IComparable<BinaryJsonNumber>, IEquatable<JsonElement>, IComparable<JsonElement>
{
#if NET8_0
    private readonly ByteBuffer16 binaryData;
#else
    private readonly byte[] binaryData = new byte[16];
#endif
    private readonly Kind numericKind;

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(byte value)
    {
        WriteNumeric(value, this.binaryData);
        this.numericKind = Kind.Byte;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(decimal value)
    {
        WriteNumeric(value, this.binaryData);
        this.numericKind = Kind.Decimal;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(double value)
    {
        WriteNumeric(value, this.binaryData);
        this.numericKind = Kind.Double;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(short value)
    {
        WriteNumeric(value, this.binaryData);
        this.numericKind = Kind.Int16;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(int value)
    {
        WriteNumeric(value, this.binaryData);
        this.numericKind = Kind.Int32;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(long value)
    {
        WriteNumeric(value, this.binaryData);
        this.numericKind = Kind.Int64;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(sbyte value)
    {
        WriteNumeric(value, this.binaryData);
        this.numericKind = Kind.SByte;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(float value)
    {
        WriteNumeric(value, this.binaryData);
        this.numericKind = Kind.Single;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(ushort value)
    {
        WriteNumeric(value, this.binaryData);
        this.numericKind = Kind.UInt16;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(uint value)
    {
        WriteNumeric(value, this.binaryData);
        this.numericKind = Kind.UInt32;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(ulong value)
    {
        WriteNumeric(value, this.binaryData);
        this.numericKind = Kind.UInt64;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(Half value)
    {
        WriteNumeric(value, this.binaryData);
        this.numericKind = Kind.Half;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(bool value)
    {
        WriteNumeric(value.ToByte(), this.binaryData);
        this.numericKind = Kind.Bool;
    }

    /// <summary>
    /// The numeric type backing the number.
    /// </summary>
    [Flags]
    public enum Kind
    {
        /// <summary>
        /// No numeric type specified.
        /// </summary>
        None = 0,

        /// <summary>
        /// Represents a <see cref="Byte"/>.
        /// </summary>
        Byte = 0b0000_0000_0001,

        /// <summary>
        /// Represents a <see cref="Decimal"/>.
        /// </summary>
        Decimal = 0b0000_0000_0010,

        /// <summary>
        /// Represents a <see cref="Double"/>.
        /// </summary>
        Double = 0b0000_0000_0100,

        /// <summary>
        /// Represents an <see cref="Int16"/>.
        /// </summary>
        Int16 = 0b0000_0000_1000,

        /// <summary>
        /// Represents an <see cref="Int32"/>.
        /// </summary>
        Int32 = 0b0000_0001_0000,

        /// <summary>
        /// Represents an <see cref="Int64"/>.
        /// </summary>
        Int64 = 0b0000_0010_0000,

        /// <summary>
        /// Represents an <see cref="SByte"/>.
        /// </summary>
        SByte = 0b0000_0100_0000,

        /// <summary>
        /// Represents an <see cref="Single"/>.
        /// </summary>
        Single = 0b0000_1000_0000,

        /// <summary>
        /// Represents an <see cref="UInt16"/>.
        /// </summary>
        UInt16 = 0b0001_0000_0000,

        /// <summary>
        /// Represents an <see cref="UInt32"/>.
        /// </summary>
        UInt32 = 0b0010_0000_0000,

        /// <summary>
        /// Represents an <see cref="UInt64"/>.
        /// </summary>
        UInt64 = 0b0100_0000_0000,

        /// <summary>
        /// Represents an <see cref="Half"/>.
        /// </summary>
        Half = 0b1000_0000_0000,

        /// <summary>
        /// Represents a  <see cref="bool"/>.
        /// </summary>
        Bool = 0b0001_0000_0000_0000,
    }

    /// <summary>
    /// Gets a value indicating whether the binary number has a value.
    /// </summary>
    public bool HasValue => this.numericKind != Kind.None;

    /// <summary>
    /// Equality operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the values are equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(BinaryJsonNumber left, BinaryJsonNumber right) => Equals(left, right);

    /// <summary>
    /// Equality operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the values are equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(BinaryJsonNumber left, JsonElement right) => Equals(left, right);

    /// <summary>
    /// Equality operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the values are equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(JsonElement left, BinaryJsonNumber right) => Equals(left, right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the values are not equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(BinaryJsonNumber left, BinaryJsonNumber right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the values are not equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(BinaryJsonNumber left, JsonElement right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the values are not equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(JsonElement left, BinaryJsonNumber right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Less than operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the left is less than the right.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(BinaryJsonNumber left, BinaryJsonNumber right)
    {
        return Compare(left, right) < 0;
    }

    /// <summary>
    /// Less than or equals operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the left is less than or equal to the right.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(BinaryJsonNumber left, BinaryJsonNumber right)
    {
        return Compare(left, right) <= 0;
    }

    /// <summary>
    /// Greater than operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the left is greater than the right.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(BinaryJsonNumber left, BinaryJsonNumber right)
    {
        return Compare(left, right) > 0;
    }

    /// <summary>
    /// Greater than or equals operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the left is greater than or equal to the right.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(BinaryJsonNumber left, BinaryJsonNumber right)
    {
        return Compare(left, right) >= 0;
    }

    /// <summary>
    /// Less than operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the left is less than the right.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(BinaryJsonNumber left, JsonElement right)
    {
        return Compare(left, right) < 0;
    }

    /// <summary>
    /// Less than or equals operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the left is less than or equal to the right.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(BinaryJsonNumber left, JsonElement right)
    {
        return Compare(left, right) <= 0;
    }

    /// <summary>
    /// Greater than operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the left is greater than the right.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(BinaryJsonNumber left, JsonElement right)
    {
        return Compare(left, right) > 0;
    }

    /// <summary>
    /// Greater than or equals operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the left is greater than or equal to the right.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(BinaryJsonNumber left, JsonElement right)
    {
        return Compare(left, right) >= 0;
    }

    /// <summary>
    /// Less than operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the left is less than the right.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(JsonElement left, BinaryJsonNumber right)
    {
        return Compare(left, right) < 0;
    }

    /// <summary>
    /// Less than or equals operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the left is less than or equal to the right.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(JsonElement left, BinaryJsonNumber right)
    {
        return Compare(left, right) <= 0;
    }

    /// <summary>
    /// Greater than operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the left is greater than the right.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(JsonElement left, BinaryJsonNumber right)
    {
        return Compare(left, right) > 0;
    }

    /// <summary>
    /// Greater than or equals operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the left is greater than or equal to the right.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(JsonElement left, BinaryJsonNumber right)
    {
        return Compare(left, right) >= 0;
    }

    /// <summary>
    /// Compare two numbers in binary form.
    /// </summary>
    /// <param name="left">The binary backing for the lhs.</param>
    /// <param name="right">The binary backing for the rhs.</param>
    /// <returns><see langword="true"/> if the values are equal.</returns>
    /// <exception cref="NotSupportedException">The numeric format is not supported.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Equals(in BinaryJsonNumber left, in BinaryJsonNumber right)
    {
        // They are the same numeric type, we can just get and compare
        if (left.numericKind == right.numericKind)
        {
            return left.numericKind switch
            {
                Kind.Byte => ReadByte(left.binaryData).Equals(ReadByte(right.binaryData)),
                Kind.Decimal => ReadDecimal(left.binaryData).Equals(ReadDecimal(right.binaryData)),
                Kind.Double => ReadDouble(left.binaryData).Equals(ReadDouble(right.binaryData)),
                Kind.Half => ReadHalf(left.binaryData).Equals(ReadHalf(right.binaryData)),
                Kind.Int16 => ReadInt16(left.binaryData).Equals(ReadInt16(right.binaryData)),
                Kind.Int32 => ReadInt32(left.binaryData).Equals(ReadInt32(right.binaryData)),
                Kind.Int64 => ReadInt64(left.binaryData).Equals(ReadInt64(right.binaryData)),
                Kind.SByte => ReadSByte(left.binaryData).Equals(ReadSByte(right.binaryData)),
                Kind.Single => ReadSingle(left.binaryData).Equals(ReadSingle(right.binaryData)),
                Kind.UInt16 => ReadUInt16(left.binaryData).Equals(ReadUInt16(right.binaryData)),
                Kind.UInt32 => ReadUInt32(left.binaryData).Equals(ReadUInt32(right.binaryData)),
                Kind.UInt64 => ReadUInt64(left.binaryData).Equals(ReadUInt64(right.binaryData)),
                _ => throw new NotSupportedException(),
            };
        }

        return right.numericKind switch
        {
            Kind.Byte => Equals(right, ReadByte(left.binaryData)),
            Kind.Decimal => Equals(right, ReadDecimal(left.binaryData)),
            Kind.Double => Equals(right, ReadDouble(left.binaryData)),
            Kind.Half => Equals(right, ReadHalf(left.binaryData)),
            Kind.Int16 => Equals(right, ReadInt16(left.binaryData)),
            Kind.Int32 => Equals(right, ReadInt32(left.binaryData)),
            Kind.Int64 => Equals(right, ReadInt64(left.binaryData)),
            Kind.SByte => Equals(right, ReadSByte(left.binaryData)),
            Kind.Single => Equals(right, ReadSingle(left.binaryData)),
            Kind.UInt16 => Equals(right, ReadUInt16(left.binaryData)),
            Kind.UInt32 => Equals(right, ReadUInt32(left.binaryData)),
            Kind.UInt64 => Equals(right, ReadUInt64(left.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Compare a binary number with a number represented by a JsonElement.
    /// </summary>
    /// <param name="jsonNumber">The jsonElement backing for the rhs.</param>
    /// <param name="binaryNumber">The binary backing for the lhs.</param>
    /// <returns><see langword="true"/> if the values are equal.</returns>
    /// <exception cref="NotSupportedException">The numeric format is not supported.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Equals(in JsonElement jsonNumber, in BinaryJsonNumber binaryNumber)
    {
        return Equals(binaryNumber, jsonNumber);
    }

    /// <summary>
    /// Compare a binary number with a number represented by a JsonElement.
    /// </summary>
    /// <param name="binaryNumber">The binary backing for the lhs.</param>
    /// <param name="jsonNumber">The jsonElement backing for the rhs.</param>
    /// <returns><see langword="true"/> if the values are equal.</returns>
    /// <exception cref="NotSupportedException">The numeric format is not supported.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Equals(in BinaryJsonNumber binaryNumber, in JsonElement jsonNumber)
    {
        if (jsonNumber.ValueKind != JsonValueKind.Number)
        {
            throw new NotSupportedException();
        }

        if (jsonNumber.ValueKind != JsonValueKind.Number)
        {
            throw new FormatException();
        }

        if (jsonNumber.TryGetDouble(out double jsonNumberDouble))
        {
            if (binaryNumber.numericKind == Kind.Decimal)
            {
                decimal binaryNumberDecimal = ReadDecimal(binaryNumber.binaryData);

                if (PreciseConversionTo<double>.TryFrom(binaryNumberDecimal, out double binaryNumberDecimalAsDouble))
                {
                    jsonNumberDouble.CompareTo(binaryNumberDecimalAsDouble);
                }
                else
                {
                    // This will throw if we cannot conver to a decimal
                    jsonNumber.GetDecimal().CompareTo(binaryNumberDecimal);
                }
            }

            // We didn't throw, so we can try to compare with the double
            return binaryNumber.numericKind switch
            {
                Kind.Byte => jsonNumberDouble.Equals(ReadByte(binaryNumber.binaryData)),
                Kind.Double => jsonNumberDouble.Equals(ReadDouble(binaryNumber.binaryData)),
                Kind.Half => jsonNumberDouble.Equals((double)ReadHalf(binaryNumber.binaryData)),
                Kind.Int16 => jsonNumberDouble.Equals(ReadInt16(binaryNumber.binaryData)),
                Kind.Int32 => jsonNumberDouble.Equals(ReadInt32(binaryNumber.binaryData)),
                Kind.Int64 => jsonNumberDouble.Equals(ReadInt64(binaryNumber.binaryData)),
                Kind.SByte => jsonNumberDouble.Equals(ReadSByte(binaryNumber.binaryData)),
                Kind.Single => jsonNumberDouble.Equals((double)ReadSingle(binaryNumber.binaryData)),
                Kind.UInt16 => jsonNumberDouble.Equals(ReadUInt16(binaryNumber.binaryData)),
                Kind.UInt32 => jsonNumberDouble.Equals(ReadUInt32(binaryNumber.binaryData)),
                Kind.UInt64 => jsonNumberDouble.Equals(ReadUInt64(binaryNumber.binaryData)),
                _ => throw new NotSupportedException(),
            };
        }

        // We fall through the decimal handling
        decimal jsonNumberDecimal = jsonNumber.GetDecimal();
        return binaryNumber.numericKind switch
        {
            Kind.Byte => jsonNumberDecimal.Equals(ReadByte(binaryNumber.binaryData)),
            Kind.Decimal => jsonNumberDecimal.Equals(ReadDecimal(binaryNumber.binaryData)),
            Kind.Double => jsonNumberDecimal.Equals(checked((decimal)ReadDouble(binaryNumber.binaryData))),
            Kind.Half => jsonNumberDecimal.Equals(checked((decimal)ReadHalf(binaryNumber.binaryData))),
            Kind.Int16 => jsonNumberDecimal.Equals(ReadInt16(binaryNumber.binaryData)),
            Kind.Int32 => jsonNumberDecimal.Equals(ReadInt32(binaryNumber.binaryData)),
            Kind.Int64 => jsonNumberDecimal.Equals(ReadInt64(binaryNumber.binaryData)),
            Kind.SByte => jsonNumberDecimal.Equals(ReadSByte(binaryNumber.binaryData)),
            Kind.Single => jsonNumberDecimal.Equals(checked((decimal)ReadSingle(binaryNumber.binaryData))),
            Kind.UInt16 => jsonNumberDecimal.Equals(ReadUInt16(binaryNumber.binaryData)),
            Kind.UInt32 => jsonNumberDecimal.Equals(ReadUInt32(binaryNumber.binaryData)),
            Kind.UInt64 => jsonNumberDecimal.Equals(ReadUInt64(binaryNumber.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Compare two numbers in binary form.
    /// </summary>
    /// <param name="left">The binary backing for the lhs.</param>
    /// <param name="right">The binary backing for the rhs.</param>
    /// <returns>0 if the numbers are equal, -1 if the lhs is less than the rhs, and 1 if the lhs is greater than the rhs.</returns>
    /// <exception cref="NotSupportedException">The numeric format is not supported.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Compare(in BinaryJsonNumber left, in BinaryJsonNumber right)
    {
        // They are the same numeric type, we can just get and compare
        if (left.numericKind == right.numericKind)
        {
            return left.numericKind switch
            {
                Kind.Byte => ReadByte(left.binaryData).CompareTo(ReadByte(right.binaryData)),
                Kind.Decimal => ReadDecimal(left.binaryData).CompareTo(ReadDecimal(right.binaryData)),
                Kind.Double => ReadDouble(left.binaryData).CompareTo(ReadDouble(right.binaryData)),
                Kind.Half => ReadHalf(left.binaryData).CompareTo(ReadHalf(right.binaryData)),
                Kind.Int16 => ReadInt16(left.binaryData).CompareTo(ReadInt16(right.binaryData)),
                Kind.Int32 => ReadInt32(left.binaryData).CompareTo(ReadInt32(right.binaryData)),
                Kind.Int64 => ReadInt64(left.binaryData).CompareTo(ReadInt64(right.binaryData)),
                Kind.SByte => ReadSByte(left.binaryData).CompareTo(ReadSByte(right.binaryData)),
                Kind.Single => ReadSingle(left.binaryData).CompareTo(ReadSingle(right.binaryData)),
                Kind.UInt16 => ReadUInt16(left.binaryData).CompareTo(ReadUInt16(right.binaryData)),
                Kind.UInt32 => ReadUInt32(left.binaryData).CompareTo(ReadUInt32(right.binaryData)),
                Kind.UInt64 => ReadUInt64(left.binaryData).CompareTo(ReadUInt64(right.binaryData)),
                _ => throw new NotSupportedException(),
            };
        }

        return left.numericKind switch
        {
            Kind.Byte => Compare(ReadByte(left.binaryData), right),
            Kind.Decimal => Compare(ReadDecimal(left.binaryData), right),
            Kind.Double => Compare(ReadDouble(left.binaryData), right),
            Kind.Half => Compare(ReadHalf(left.binaryData), right),
            Kind.Int16 => Compare(ReadInt16(left.binaryData), right),
            Kind.Int32 => Compare(ReadInt32(left.binaryData), right),
            Kind.Int64 => Compare(ReadInt64(left.binaryData), right),
            Kind.SByte => Compare(ReadSByte(left.binaryData), right),
            Kind.Single => Compare(ReadSingle(left.binaryData), right),
            Kind.UInt16 => Compare(ReadUInt16(left.binaryData), right),
            Kind.UInt32 => Compare(ReadUInt32(left.binaryData), right),
            Kind.UInt64 => Compare(ReadUInt64(left.binaryData), right),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Compare a number represented by a JsonElement with a binary number.
    /// </summary>
    /// <param name="left">The binary backing for the rhs.</param>
    /// <param name="right">The jsonElement backing for the lhs.</param>
    /// <returns>0 if the numbers are equal, -1 if the lhs is less than the rhs, and 1 if the lhs is greater than the rhs.</returns>
    /// <exception cref="NotSupportedException">The numeric format is not supported.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Compare(in BinaryJsonNumber left, in JsonElement right)
    {
        return -Compare(right, left);
    }

    /// <summary>
    /// Compare a number represented by a JsonElement with a binary number.
    /// </summary>
    /// <param name="left">The jsonElement backing for the lhs.</param>
    /// <param name="right">The binary backing for the rhs.</param>
    /// <returns>0 if the numbers are equal, -1 if the lhs is less than the rhs, and 1 if the lhs is greater than the rhs.</returns>
    /// <exception cref="NotSupportedException">The numeric format is not supported.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Compare(in JsonElement left, in BinaryJsonNumber right)
    {
        if (left.ValueKind != JsonValueKind.Number)
        {
            throw new FormatException();
        }

        if (left.TryGetDouble(out double leftDouble))
        {
            if (right.numericKind == Kind.Decimal)
            {
                decimal rightDecimal = ReadDecimal(right.binaryData);

                if (PreciseConversionTo<double>.TryFrom(rightDecimal, out double rightDecimalAsDouble))
                {
                    leftDouble.CompareTo(rightDecimalAsDouble);
                }
                else
                {
                    left.GetDecimal().CompareTo(rightDecimal);
                }
            }

            return right.numericKind switch
            {
                Kind.Byte => leftDouble.CompareTo(ReadByte(right.binaryData)),
                Kind.Double => leftDouble.CompareTo(ReadDouble(right.binaryData)),
                Kind.Half => leftDouble.CompareTo((double)ReadHalf(right.binaryData)),
                Kind.Int16 => leftDouble.CompareTo(ReadInt16(right.binaryData)),
                Kind.Int32 => leftDouble.CompareTo(ReadInt32(right.binaryData)),
                Kind.Int64 => leftDouble.CompareTo(ReadInt64(right.binaryData)),
                Kind.SByte => leftDouble.CompareTo(ReadSByte(right.binaryData)),
                Kind.Single => leftDouble.CompareTo((double)ReadSingle(right.binaryData)),
                Kind.UInt16 => leftDouble.CompareTo(ReadUInt16(right.binaryData)),
                Kind.UInt32 => leftDouble.CompareTo(ReadUInt32(right.binaryData)),
                Kind.UInt64 => leftDouble.CompareTo(ReadUInt64(right.binaryData)),
                _ => throw new NotSupportedException(),
            };
        }

        decimal leftDecimal = left.GetDecimal();
        return right.numericKind switch
        {
            Kind.Byte => leftDecimal.CompareTo(ReadByte(right.binaryData)),
            Kind.Decimal => leftDecimal.CompareTo(ReadDecimal(right.binaryData)),
            Kind.Double => leftDecimal.CompareTo(checked((decimal)ReadDouble(right.binaryData))),
            Kind.Half => leftDecimal.CompareTo(checked((decimal)ReadHalf(right.binaryData))),
            Kind.Int16 => leftDecimal.CompareTo(ReadInt16(right.binaryData)),
            Kind.Int32 => leftDecimal.CompareTo(ReadInt32(right.binaryData)),
            Kind.Int64 => leftDecimal.CompareTo(ReadInt64(right.binaryData)),
            Kind.SByte => leftDecimal.CompareTo(ReadSByte(right.binaryData)),
            Kind.Single => leftDecimal.CompareTo(checked((decimal)ReadSingle(right.binaryData))),
            Kind.UInt16 => leftDecimal.CompareTo(ReadUInt16(right.binaryData)),
            Kind.UInt32 => leftDecimal.CompareTo(ReadUInt32(right.binaryData)),
            Kind.UInt64 => leftDecimal.CompareTo(ReadUInt64(right.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Gets a binary number from a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="jsonElement">The element from which to create the <see cref="BinaryJsonNumber"/>.</param>
    /// <returns>The <see cref="BinaryJsonNumber"/> created from the <see cref="JsonElement"/>.</returns>
    /// <exception cref="FormatException">The JsonElement was not in a supported format.</exception>
    public static BinaryJsonNumber FromJson(in JsonElement jsonElement)
    {
        if (jsonElement.ValueKind != JsonValueKind.Number)
        {
            throw new FormatException();
        }

        // When we read a JSON value we prefer a double
        if (jsonElement.TryGetDouble(out double jsonNumberDouble))
        {
            return new BinaryJsonNumber(jsonNumberDouble);
        }

        // But if we can#t, we'll go with a decimal
        return new BinaryJsonNumber(jsonElement.GetDecimal());
    }

    /// <summary>
    /// Create an instance of an <see cref="INumberBase{TSelf}"/>.
    /// </summary>
    /// <typeparam name="TOther">The type to create.</typeparam>
    /// <returns>An instance of the <see cref="INumberBase{TSelf}"/>.</returns>
    /// <exception cref="NotSupportedException">The number format is not supported.</exception>
    /// <exception cref="OverflowException">The number cannot be precisely represented as the target type without loss of precision.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TOther CreatePrecise<TOther>()
            where TOther : INumberBase<TOther>
    {
        return this.numericKind switch
        {
            Kind.Byte => PreciseConversionTo<TOther>.From(ReadByte(this.binaryData)),
            Kind.Decimal => PreciseConversionTo<TOther>.From(ReadDecimal(this.binaryData)),
            Kind.Double => PreciseConversionTo<TOther>.From(ReadDouble(this.binaryData)),
            Kind.Half => PreciseConversionTo<TOther>.From(ReadHalf(this.binaryData)),
            Kind.Int16 => PreciseConversionTo<TOther>.From(ReadInt16(this.binaryData)),
            Kind.Int32 => PreciseConversionTo<TOther>.From(ReadInt32(this.binaryData)),
            Kind.Int64 => PreciseConversionTo<TOther>.From(ReadInt64(this.binaryData)),
            Kind.SByte => PreciseConversionTo<TOther>.From(ReadSByte(this.binaryData)),
            Kind.Single => PreciseConversionTo<TOther>.From(ReadSingle(this.binaryData)),
            Kind.UInt16 => PreciseConversionTo<TOther>.From(ReadUInt16(this.binaryData)),
            Kind.UInt32 => PreciseConversionTo<TOther>.From(ReadUInt32(this.binaryData)),
            Kind.UInt64 => PreciseConversionTo<TOther>.From(ReadUInt64(this.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Create an instance of an <see cref="INumberBase{TSelf}"/>.
    /// </summary>
    /// <typeparam name="TOther">The type to create.</typeparam>
    /// <returns>An instance of the <see cref="INumberBase{TSelf}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TOther CreateChecked<TOther>()
            where TOther : INumberBase<TOther>
    {
        return this.numericKind switch
        {
            Kind.Byte => TOther.CreateChecked(ReadByte(this.binaryData)),
            Kind.Decimal => TOther.CreateChecked(ReadDecimal(this.binaryData)),
            Kind.Double => TOther.CreateChecked(ReadDouble(this.binaryData)),
            Kind.Half => TOther.CreateChecked(ReadHalf(this.binaryData)),
            Kind.Int16 => TOther.CreateChecked(ReadInt16(this.binaryData)),
            Kind.Int32 => TOther.CreateChecked(ReadInt32(this.binaryData)),
            Kind.Int64 => TOther.CreateChecked(ReadInt64(this.binaryData)),
            Kind.SByte => TOther.CreateChecked(ReadSByte(this.binaryData)),
            Kind.Single => TOther.CreateChecked(ReadSingle(this.binaryData)),
            Kind.UInt16 => TOther.CreateChecked(ReadUInt16(this.binaryData)),
            Kind.UInt32 => TOther.CreateChecked(ReadUInt32(this.binaryData)),
            Kind.UInt64 => TOther.CreateChecked(ReadUInt64(this.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Create an instance of an <see cref="INumberBase{TSelf}"/>.
    /// </summary>
    /// <typeparam name="TOther">The type to create.</typeparam>
    /// <returns>An instance of the <see cref="INumberBase{TSelf}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TOther CreateSaturating<TOther>()
            where TOther : INumberBase<TOther>
    {
        return this.numericKind switch
        {
            Kind.Byte => TOther.CreateSaturating(ReadByte(this.binaryData)),
            Kind.Decimal => TOther.CreateSaturating(ReadDecimal(this.binaryData)),
            Kind.Double => TOther.CreateSaturating(ReadDouble(this.binaryData)),
            Kind.Half => TOther.CreateSaturating(ReadHalf(this.binaryData)),
            Kind.Int16 => TOther.CreateSaturating(ReadInt16(this.binaryData)),
            Kind.Int32 => TOther.CreateSaturating(ReadInt32(this.binaryData)),
            Kind.Int64 => TOther.CreateSaturating(ReadInt64(this.binaryData)),
            Kind.SByte => TOther.CreateSaturating(ReadSByte(this.binaryData)),
            Kind.Single => TOther.CreateSaturating(ReadSingle(this.binaryData)),
            Kind.UInt16 => TOther.CreateSaturating(ReadUInt16(this.binaryData)),
            Kind.UInt32 => TOther.CreateSaturating(ReadUInt32(this.binaryData)),
            Kind.UInt64 => TOther.CreateSaturating(ReadUInt64(this.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Create an instance of an <see cref="INumberBase{TSelf}"/>.
    /// </summary>
    /// <typeparam name="TOther">The type to create.</typeparam>
    /// <returns>An instance of the <see cref="INumberBase{TSelf}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TOther CreateTruncating<TOther>()
            where TOther : INumberBase<TOther>
    {
        return this.numericKind switch
        {
            Kind.Byte => TOther.CreateTruncating(ReadByte(this.binaryData)),
            Kind.Decimal => TOther.CreateTruncating(ReadDecimal(this.binaryData)),
            Kind.Double => TOther.CreateTruncating(ReadDouble(this.binaryData)),
            Kind.Half => TOther.CreateTruncating(ReadHalf(this.binaryData)),
            Kind.Int16 => TOther.CreateTruncating(ReadInt16(this.binaryData)),
            Kind.Int32 => TOther.CreateTruncating(ReadInt32(this.binaryData)),
            Kind.Int64 => TOther.CreateTruncating(ReadInt64(this.binaryData)),
            Kind.SByte => TOther.CreateTruncating(ReadSByte(this.binaryData)),
            Kind.Single => TOther.CreateTruncating(ReadSingle(this.binaryData)),
            Kind.UInt16 => TOther.CreateTruncating(ReadUInt16(this.binaryData)),
            Kind.UInt32 => TOther.CreateTruncating(ReadUInt32(this.binaryData)),
            Kind.UInt64 => TOther.CreateTruncating(ReadUInt64(this.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Determines if this value is a multiple of the other value..
    /// </summary>
    /// <param name="multipleOf">The factor to test.</param>
    /// <returns><see langword="true"/> if the value is a multuple of the given factor.</returns>
    /// <exception cref="NotSupportedException">The number format is not suported.</exception>
    public bool IsMultipleOf(BinaryJsonNumber multipleOf)
    {
        if (this.numericKind == multipleOf.numericKind)
        {
            return this.numericKind switch
            {
                Kind.Byte => ReadByte(this.binaryData) % ReadByte(multipleOf.binaryData) == 0,
                Kind.Double => Math.Abs(Math.IEEERemainder(ReadDouble(this.binaryData), ReadDouble(multipleOf.binaryData))) <= 1.0E-9,
                Kind.Decimal => decimal.Abs(decimal.Remainder(ReadDecimal(this.binaryData), ReadDecimal(multipleOf.binaryData))) <= 1.0E-5M,
                Kind.Half => Half.Abs(Half.Ieee754Remainder(ReadHalf(this.binaryData), ReadHalf(multipleOf.binaryData))) <= Half.CreateTruncating(1.0E-5),
                Kind.Int16 => Math.Abs(ReadInt16(this.binaryData)) % Math.Abs(ReadInt16(multipleOf.binaryData)) == 0,
                Kind.Int32 => Math.Abs(ReadInt32(this.binaryData)) % Math.Abs(ReadInt32(multipleOf.binaryData)) == 0,
                Kind.Int64 => Math.Abs(ReadInt64(this.binaryData)) % Math.Abs(ReadInt64(multipleOf.binaryData)) == 0,
                Kind.SByte => Math.Abs(ReadInt16(this.binaryData)) % Math.Abs(ReadInt16(multipleOf.binaryData)) == 0,
                Kind.Single => Math.Abs(MathF.IEEERemainder(ReadSingle(this.binaryData), ReadSingle(multipleOf.binaryData))) <= 1.0E-5,
                Kind.UInt16 => ReadUInt16(this.binaryData) % ReadUInt16(multipleOf.binaryData) == 0,
                Kind.UInt32 => ReadUInt32(this.binaryData) % ReadUInt32(multipleOf.binaryData) == 0,
                Kind.UInt64 => ReadUInt64(this.binaryData) % ReadUInt64(multipleOf.binaryData) == 0,
                _ => throw new NotSupportedException(),
            };
        }

        return this.numericKind switch
        {
            Kind.Byte => IsMultipleOf(ReadByte(this.binaryData), multipleOf),
            Kind.Double => IsMultipleOf(ReadDouble(this.binaryData), multipleOf),
            Kind.Decimal => IsMultipleOf(ReadDecimal(this.binaryData), multipleOf),
            Kind.Half => IsMultipleOf(ReadHalf(this.binaryData), multipleOf),
            Kind.Int16 => IsMultipleOf(ReadInt16(this.binaryData), multipleOf),
            Kind.Int32 => IsMultipleOf(ReadInt32(this.binaryData), multipleOf),
            Kind.Int64 => IsMultipleOf(ReadInt64(this.binaryData), multipleOf),
            Kind.SByte => IsMultipleOf(ReadSByte(this.binaryData), multipleOf),
            Kind.Single => IsMultipleOf(ReadSingle(this.binaryData), multipleOf),
            Kind.UInt16 => IsMultipleOf(ReadUInt16(this.binaryData), multipleOf),
            Kind.UInt32 => IsMultipleOf(ReadUInt32(this.binaryData), multipleOf),
            Kind.UInt64 => IsMultipleOf(ReadUInt64(this.binaryData), multipleOf),
            _ => throw new NotSupportedException(),
        };
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return (obj is BinaryJsonNumber number && this.Equals(number)) ||
               (obj is JsonElement jsonElement && this.Equals(jsonElement));
    }

    /// <inheritdoc/>
    public bool Equals(BinaryJsonNumber other)
    {
        return Equals(this, other);
    }

    /// <inheritdoc/>
    public bool Equals(JsonElement other)
    {
        return Equals(this, other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        // Special case handling for a decimal which can be represented
        // as a double.
        if (this.numericKind == Kind.Decimal)
        {
            decimal value = ReadDecimal(this.binaryData);
            if (PreciseConversionTo<double>.TryFrom(value, out double asDouble))
            {
                return asDouble.GetHashCode();
            }

            return value.GetHashCode();
        }

        // Build a hashcode which matches the equality semantics.
        return this.numericKind switch
        {
            Kind.Byte => ((double)ReadByte(this.binaryData)).GetHashCode(),
            Kind.Double => ReadDouble(this.binaryData).GetHashCode(),
            Kind.Half => ((double)ReadHalf(this.binaryData)).GetHashCode(),
            Kind.Int16 => ((double)ReadInt16(this.binaryData)).GetHashCode(),
            Kind.Int32 => ((double)ReadInt32(this.binaryData)).GetHashCode(),
            Kind.Int64 => ((double)ReadInt64(this.binaryData)).GetHashCode(),
            Kind.SByte => ((double)ReadSByte(this.binaryData)).GetHashCode(),
            Kind.Single => ((double)ReadSingle(this.binaryData)).GetHashCode(),
            Kind.UInt16 => ((double)ReadUInt16(this.binaryData)).GetHashCode(),
            Kind.UInt32 => ((double)ReadUInt32(this.binaryData)).GetHashCode(),
            Kind.UInt64 => ((double)ReadUInt64(this.binaryData)).GetHashCode(),
            _ => throw new NotSupportedException(),
        };
    }

    /// <inheritdoc/>
    public int CompareTo(BinaryJsonNumber other)
    {
        return Compare(this, other);
    }

    /// <inheritdoc/>
    public int CompareTo(JsonElement other)
    {
        return Compare(this, other);
    }

    /// <summary>
    /// Gets a byte value as a bool.
    /// </summary>
    /// <returns><see langword="false"/> if the value is zero, otherwise <see langword="true"/>.</returns>
    /// <exception cref="FormatException">The value was not a byte.</exception>
    public bool GetByteAsBool()
    {
        if (this.numericKind == Kind.Byte)
        {
            return ReadByte(this.binaryData) != 0;
        }

        throw new FormatException();
    }

    /// <summary>
    /// Write a number value to a UTF8 JSON writer.
    /// </summary>
    /// <param name="writer">The <see cref="Utf8JsonWriter"/>.</param>
    public void WriteTo(Utf8JsonWriter writer)
    {
        switch (this.numericKind)
        {
            case Kind.Byte:
                writer.WriteNumberValue(ReadByte(this.binaryData));
                break;
            case Kind.Decimal:
                writer.WriteNumberValue(ReadDecimal(this.binaryData));
                break;
            case Kind.Double:
                writer.WriteNumberValue(ReadDouble(this.binaryData));
                break;
            case Kind.Int16:
                writer.WriteNumberValue(ReadInt16(this.binaryData));
                break;
            case Kind.Int32:
                writer.WriteNumberValue(ReadInt32(this.binaryData));
                break;
            case Kind.Int64:
                writer.WriteNumberValue(ReadInt64(this.binaryData));
                break;
            case Kind.SByte:
                writer.WriteNumberValue(ReadSByte(this.binaryData));
                break;
            case Kind.Single:
                writer.WriteNumberValue(ReadSingle(this.binaryData));
                break;
            case Kind.UInt16:
                writer.WriteNumberValue(ReadUInt16(this.binaryData));
                break;
            case Kind.UInt32:
                writer.WriteNumberValue(ReadUInt32(this.binaryData));
                break;
            case Kind.UInt64:
                writer.WriteNumberValue(ReadUInt64(this.binaryData));
                break;
            case Kind.Half:
                writer.WriteNumberValue((double)ReadHalf(this.binaryData));
                break;
            default:
                throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Writes a numeric value to a number backing.
    /// </summary>
    /// <param name="value">The <see cref="byte"/> to write.</param>
    /// <param name="numberBacking">The number backing to which to write the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteNumeric(byte value, Span<byte> numberBacking)
    {
        numberBacking[0] = value;
    }

    /// <summary>
    /// Writes a numeric value to a number backing.
    /// </summary>
    /// <param name="value">The <see cref="decimal"/> to write.</param>
    /// <param name="numberBacking">The number backing to which to write the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteNumeric(decimal value, Span<byte> numberBacking)
    {
        Span<int> bits = MemoryMarshal.Cast<byte, int>(numberBacking);
        if (!decimal.TryGetBits(value, bits, out _))
        {
            throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Writes a numeric value to a number backing.
    /// </summary>
    /// <param name="value">The <see cref="Half"/> to write.</param>
    /// <param name="numberBacking">The number backing to which to write the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteNumeric(Half value, Span<byte> numberBacking)
    {
        BinaryPrimitives.WriteHalfLittleEndian(numberBacking, value);
    }

    /// <summary>
    /// Writes a numeric value to a number backing.
    /// </summary>
    /// <param name="value">The <see cref="ulong"/> to write.</param>
    /// <param name="numberBacking">The number backing to which to write the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteNumeric(ulong value, Span<byte> numberBacking)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(numberBacking, value);
    }

    /// <summary>
    /// Writes a numeric value to a number backing.
    /// </summary>
    /// <param name="value">The <see cref="uint"/> to write.</param>
    /// <param name="numberBacking">The number backing to which to write the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteNumeric(uint value, Span<byte> numberBacking)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(numberBacking, value);
    }

    /// <summary>
    /// Writes a numeric value to a number backing.
    /// </summary>
    /// <param name="value">The <see cref="ushort"/> to write.</param>
    /// <param name="numberBacking">The number backing to which to write the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteNumeric(ushort value, Span<byte> numberBacking)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(numberBacking, value);
    }

    /// <summary>
    /// Writes a numeric value to a number backing.
    /// </summary>
    /// <param name="value">The <see cref="float"/> to write.</param>
    /// <param name="numberBacking">The number backing to which to write the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteNumeric(float value, Span<byte> numberBacking)
    {
        BinaryPrimitives.WriteSingleLittleEndian(numberBacking, value);
    }

    /// <summary>
    /// Writes a numeric value to a number backing.
    /// </summary>
    /// <param name="value">The <see cref="sbyte"/> to write.</param>
    /// <param name="numberBacking">The number backing to which to write the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteNumeric(sbyte value, Span<byte> numberBacking)
    {
        numberBacking[0] = (byte)value;
    }

    /// <summary>
    /// Writes a numeric value to a number backing.
    /// </summary>
    /// <param name="value">The <see cref="long"/> to write.</param>
    /// <param name="numberBacking">The number backing to which to write the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteNumeric(long value, Span<byte> numberBacking)
    {
        BinaryPrimitives.WriteInt64LittleEndian(numberBacking, value);
    }

    /// <summary>
    /// Writes a numeric value to a number backing.
    /// </summary>
    /// <param name="value">The <see cref="int"/> to write.</param>
    /// <param name="numberBacking">The number backing to which to write the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteNumeric(int value, Span<byte> numberBacking)
    {
        BinaryPrimitives.WriteInt32LittleEndian(numberBacking, value);
    }

    /// <summary>
    /// Writes a numeric value to a number backing.
    /// </summary>
    /// <param name="value">The <see cref="short"/> to write.</param>
    /// <param name="numberBacking">The number backing to which to write the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteNumeric(short value, Span<byte> numberBacking)
    {
        BinaryPrimitives.WriteInt16LittleEndian(numberBacking, value);
    }

    /// <summary>
    /// Writes a numeric value to a number backing.
    /// </summary>
    /// <param name="value">The <see cref="double"/> to write.</param>
    /// <param name="numberBacking">The number backing to which to write the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteNumeric(double value, Span<byte> numberBacking)
    {
        BinaryPrimitives.WriteDoubleLittleEndian(numberBacking, value);
    }

    /// <summary>
    /// Reads a value from a byte array.
    /// </summary>
    /// <param name="numberBacking">The number backing from which to read the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ReadByte(ReadOnlySpan<byte> numberBacking)
    {
        return numberBacking[0];
    }

    /// <summary>
    /// Reads a value from a byte array.
    /// </summary>
    /// <param name="numberBacking">The number backing from which to read the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static decimal ReadDecimal(ReadOnlySpan<byte> numberBacking)
    {
        ReadOnlySpan<int> bits = MemoryMarshal.Cast<byte, int>(numberBacking);
        return new decimal(bits);
    }

    /// <summary>
    /// Reads a value from a byte array.
    /// </summary>
    /// <param name="numberBacking">The number backing from which to read the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Half ReadHalf(ReadOnlySpan<byte> numberBacking)
    {
        return BinaryPrimitives.ReadHalfLittleEndian(numberBacking);
    }

    /// <summary>
    /// Reads a value from a byte array.
    /// </summary>
    /// <param name="numberBacking">The number backing from which to read the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReadUInt64(ReadOnlySpan<byte> numberBacking)
    {
        return BinaryPrimitives.ReadUInt64LittleEndian(numberBacking);
    }

    /// <summary>
    /// Reads a value from a byte array.
    /// </summary>
    /// <param name="numberBacking">The number backing from which to read the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadUInt32(ReadOnlySpan<byte> numberBacking)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(numberBacking);
    }

    /// <summary>
    /// Reads a value from a byte array.
    /// </summary>
    /// <param name="numberBacking">The number backing from which to read the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort ReadUInt16(ReadOnlySpan<byte> numberBacking)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(numberBacking);
    }

    /// <summary>
    /// Reads a value from a byte array.
    /// </summary>
    /// <param name="numberBacking">The number backing from which to read the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ReadSingle(ReadOnlySpan<byte> numberBacking)
    {
        return BinaryPrimitives.ReadSingleLittleEndian(numberBacking);
    }

    /// <summary>
    /// Reads a value from a byte array.
    /// </summary>
    /// <param name="numberBacking">The number backing from which to read the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static sbyte ReadSByte(ReadOnlySpan<byte> numberBacking)
    {
        return (sbyte)numberBacking[0];
    }

    /// <summary>
    /// Reads a value from a byte array.
    /// </summary>
    /// <param name="numberBacking">The number backing from which to read the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ReadInt64(ReadOnlySpan<byte> numberBacking)
    {
        return BinaryPrimitives.ReadInt64LittleEndian(numberBacking);
    }

    /// <summary>
    /// Reads a value from a byte array.
    /// </summary>
    /// <param name="numberBacking">The number backing from which to read the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadInt32(ReadOnlySpan<byte> numberBacking)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(numberBacking);
    }

    /// <summary>
    /// Reads a value from a byte array.
    /// </summary>
    /// <param name="numberBacking">The number backing from which to read the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short ReadInt16(ReadOnlySpan<byte> numberBacking)
    {
        return BinaryPrimitives.ReadInt16LittleEndian(numberBacking);
    }

    /// <summary>
    /// Reads a value from a byte array.
    /// </summary>
    /// <param name="numberBacking">The number backing from which to read the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ReadDouble(ReadOnlySpan<byte> numberBacking)
    {
        return BinaryPrimitives.ReadDoubleLittleEndian(numberBacking);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Equals<T>(in BinaryJsonNumber binaryNumber, T numberBase)
    where T : INumberBase<T>
    {
        if (PreciseConversionTo<double>.TryFrom(numberBase, out double numberBaseDouble))
        {
            if (binaryNumber.numericKind == Kind.Decimal)
            {
                decimal binaryNumberDecimal = ReadDecimal(binaryNumber.binaryData);

                if (PreciseConversionTo<double>.TryFrom(binaryNumberDecimal, out double binaryNumberDecimalAsDouble))
                {
                    numberBaseDouble.CompareTo(binaryNumberDecimalAsDouble);
                }
                else
                {
                    PreciseConversionTo<decimal>.From(numberBase).CompareTo(binaryNumberDecimal);
                }
            }

            return binaryNumber.numericKind switch
            {
                Kind.Byte => numberBaseDouble.Equals(ReadByte(binaryNumber.binaryData)),
                Kind.Double => numberBaseDouble.Equals(ReadDouble(binaryNumber.binaryData)),
                Kind.Half => numberBaseDouble.Equals((double)ReadHalf(binaryNumber.binaryData)),
                Kind.Int16 => numberBaseDouble.Equals(ReadInt16(binaryNumber.binaryData)),
                Kind.Int32 => numberBaseDouble.Equals(ReadInt32(binaryNumber.binaryData)),
                Kind.Int64 => numberBaseDouble.Equals(ReadInt64(binaryNumber.binaryData)),
                Kind.SByte => numberBaseDouble.Equals(ReadSByte(binaryNumber.binaryData)),
                Kind.Single => numberBaseDouble.Equals((double)ReadSingle(binaryNumber.binaryData)),
                Kind.UInt16 => numberBaseDouble.Equals(ReadUInt16(binaryNumber.binaryData)),
                Kind.UInt32 => numberBaseDouble.Equals(ReadUInt32(binaryNumber.binaryData)),
                Kind.UInt64 => numberBaseDouble.Equals(ReadUInt64(binaryNumber.binaryData)),
                _ => throw new NotSupportedException(),
            };
        }
        else
        {
            decimal numberBaseDecimal = PreciseConversionTo<decimal>.From(numberBase);
            return binaryNumber.numericKind switch
            {
                Kind.Byte => numberBaseDecimal.Equals(ReadByte(binaryNumber.binaryData)),
                Kind.Decimal => numberBaseDecimal.Equals(ReadDecimal(binaryNumber.binaryData)),
                Kind.Double => numberBaseDecimal.Equals(PreciseConversionTo<decimal>.From(ReadDouble(binaryNumber.binaryData))),
                Kind.Half => numberBaseDecimal.Equals(PreciseConversionTo<decimal>.From(ReadHalf(binaryNumber.binaryData))),
                Kind.Int16 => numberBaseDecimal.Equals(ReadInt16(binaryNumber.binaryData)),
                Kind.Int32 => numberBaseDecimal.Equals(ReadInt32(binaryNumber.binaryData)),
                Kind.Int64 => numberBaseDecimal.Equals(ReadInt64(binaryNumber.binaryData)),
                Kind.SByte => numberBaseDecimal.Equals(ReadSByte(binaryNumber.binaryData)),
                Kind.Single => numberBaseDecimal.Equals(PreciseConversionTo<decimal>.From(ReadSingle(binaryNumber.binaryData))),
                Kind.UInt16 => numberBaseDecimal.Equals(ReadUInt16(binaryNumber.binaryData)),
                Kind.UInt32 => numberBaseDecimal.Equals(ReadUInt32(binaryNumber.binaryData)),
                Kind.UInt64 => numberBaseDecimal.Equals(ReadUInt64(binaryNumber.binaryData)),
                _ => throw new NotSupportedException(),
            };
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Compare<T>(T left, in BinaryJsonNumber right)
        where T : INumberBase<T>
    {
        // If the left can be represented as a double...
        if (PreciseConversionTo<double>.TryFrom(left, out double leftDouble))
        {
            // Special case handling for the right being a decimal
            if (right.numericKind == Kind.Decimal)
            {
                // Get it as a decimal
                decimal rightDecimal = ReadDecimal(right.binaryData);

                // Then try to use it as a double
                if (PreciseConversionTo<double>.TryFrom(rightDecimal, out double rightDecimalAsDouble))
                {
                    return leftDouble.CompareTo(rightDecimalAsDouble);
                }

                // Otherwise fall back on the decimal
                if (PreciseConversionTo<decimal>.TryFrom(left, out decimal leftDecimalForRightDecimal))
                {
                    return leftDecimalForRightDecimal.CompareTo(rightDecimal);
                }

                // We couldn't cope - they are mutually incompatible.
                throw new OverflowException();
            }

            // Fall back on basic double handling for the rest.
            return right.numericKind switch
            {
                Kind.Byte => leftDouble.CompareTo(ReadByte(right.binaryData)),
                Kind.Double => leftDouble.CompareTo(ReadDouble(right.binaryData)),
                Kind.Half => leftDouble.CompareTo((double)ReadHalf(right.binaryData)),
                Kind.Int16 => leftDouble.CompareTo(ReadInt16(right.binaryData)),
                Kind.Int32 => leftDouble.CompareTo(ReadInt32(right.binaryData)),
                Kind.Int64 => leftDouble.CompareTo(ReadInt64(right.binaryData)),
                Kind.SByte => leftDouble.CompareTo(ReadSByte(right.binaryData)),
                Kind.Single => leftDouble.CompareTo((double)ReadSingle(right.binaryData)),
                Kind.UInt16 => leftDouble.CompareTo(ReadUInt16(right.binaryData)),
                Kind.UInt32 => leftDouble.CompareTo(ReadUInt32(right.binaryData)),
                Kind.UInt64 => leftDouble.CompareTo(ReadUInt64(right.binaryData)),
                _ => throw new NotSupportedException(),
            };
        }

        // We couldn't handle the left as a double, so we will try as a decimal
        decimal leftDecimal = PreciseConversionTo<decimal>.From(left);
        return right.numericKind switch
        {
            Kind.Byte => leftDecimal.CompareTo(ReadByte(right.binaryData)),
            Kind.Decimal => leftDecimal.CompareTo(ReadDecimal(right.binaryData)),
            Kind.Double => leftDecimal.CompareTo(PreciseConversionTo<decimal>.From(ReadDouble(right.binaryData))),
            Kind.Half => leftDecimal.CompareTo(PreciseConversionTo<decimal>.From(ReadHalf(right.binaryData))),
            Kind.Int16 => leftDecimal.CompareTo(ReadInt16(right.binaryData)),
            Kind.Int32 => leftDecimal.CompareTo(ReadInt32(right.binaryData)),
            Kind.Int64 => leftDecimal.CompareTo(ReadInt64(right.binaryData)),
            Kind.SByte => leftDecimal.CompareTo(ReadSByte(right.binaryData)),
            Kind.Single => leftDecimal.CompareTo(PreciseConversionTo<decimal>.From(ReadSingle(right.binaryData))),
            Kind.UInt16 => leftDecimal.CompareTo(ReadUInt16(right.binaryData)),
            Kind.UInt32 => leftDecimal.CompareTo(ReadUInt32(right.binaryData)),
            Kind.UInt64 => leftDecimal.CompareTo(ReadUInt64(right.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    private static bool IsMultipleOf<TOther>(TOther x, BinaryJsonNumber y)
    where TOther : INumberBase<TOther>
    {
        // We use the standard policy of working through a double if possible, falling back to a decimal if not.
        if (PreciseConversionTo<double>.TryFrom(x, out double xAsDouble) &&
            PreciseConversionTo<double>.TryFrom(y, out double yAsDouble))
        {
            return Math.Abs(Math.IEEERemainder(xAsDouble, yAsDouble)) <= 1.0E-9;
        }

        if (PreciseConversionTo<decimal>.TryFrom(x, out decimal xAsDecimal) &&
            PreciseConversionTo<decimal>.TryFrom(y, out decimal yAsDecimal))
        {
            return decimal.Abs(decimal.Remainder(xAsDecimal, yAsDecimal)) <= 1.0E-5M;
        }

        throw new OverflowException();
    }

#if NET8_0
    [InlineArray(16)]
    private struct ByteBuffer16
    {
        private byte element0;
    }
#endif

    /// <summary>
    /// Provides a cast between numeric types with precision and overflow verification.
    /// </summary>
    /// <typeparam name="TOut">The type of the result.</typeparam>
    private static class PreciseConversionTo<TOut>
        where TOut : INumberBase<TOut>
    {
        public static TOut From<TIn>(TIn value)
            where TIn : INumberBase<TIn>
        {
            if (TryFrom(value, out TOut result))
            {
                return result;
            }

            throw new OverflowException();
        }

        public static TOut From(BinaryJsonNumber value)
        {
            if (TryFrom(value, out TOut result))
            {
                return result;
            }

            throw new OverflowException();
        }

        public static bool TryFrom<TIn>(TIn value, [NotNullWhen(true)] out TOut result)
            where TIn : INumberBase<TIn>
        {
            result = TOut.CreateTruncating(value);
            var delta = TIn.Abs(TIn.CreateTruncating(result) - value);
            return TIn.IsZero(delta);
        }

        public static bool TryFrom(in BinaryJsonNumber value, [NotNullWhen(true)] out TOut result)
        {
            return value.numericKind switch
            {
                Kind.Byte => TryFrom(ReadByte(value.binaryData), out result),
                Kind.Decimal => TryFrom(ReadByte(value.binaryData), out result),
                Kind.Double => TryFrom(ReadByte(value.binaryData), out result),
                Kind.Half => TryFrom(ReadByte(value.binaryData), out result),
                Kind.Int16 => TryFrom(ReadByte(value.binaryData), out result),
                Kind.Int32 => TryFrom(ReadByte(value.binaryData), out result),
                Kind.Int64 => TryFrom(ReadByte(value.binaryData), out result),
                Kind.SByte => TryFrom(ReadByte(value.binaryData), out result),
                Kind.Single => TryFrom(ReadByte(value.binaryData), out result),
                Kind.UInt16 => TryFrom(ReadByte(value.binaryData), out result),
                Kind.UInt32 => TryFrom(ReadByte(value.binaryData), out result),
                Kind.UInt64 => TryFrom(ReadByte(value.binaryData), out result),
                _ => throw new NotSupportedException(),
            };
        }
    }
}