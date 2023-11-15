// <copyright file="BinaryJsonNumber.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Diagnostics;
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
[DebuggerDisplay("{ToString()} [{numericKind}]")]
public readonly struct BinaryJsonNumber :
    ISpanFormattable,
    IUtf8SpanFormattable
{
    private readonly ByteBuffer16 binaryData;
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
    public BinaryJsonNumber(Int128 value)
    {
        WriteNumeric(value, this.binaryData);
        this.numericKind = Kind.Int128;
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
    public BinaryJsonNumber(UInt128 value)
    {
        WriteNumeric(value, this.binaryData);
        this.numericKind = Kind.UInt128;
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
        /// Represents an <see cref="UInt128"/>.
        /// </summary>
        UInt128 = 0b1000_0000_0000,

        /// <summary>
        /// Represents an <see cref="Int128"/>.
        /// </summary>
        Int128 = 0b0001_0000_0000_0000,

        /// <summary>
        /// Represents an <see cref="Half"/>.
        /// </summary>
        Half = 0b0010_0000_0000_0000,

        /// <summary>
        /// Represents a  <see cref="bool"/>.
        /// </summary>
        Bool = 0b0100_0000_0000_0000,
    }

    /// <summary>
    /// Gets an instance of <see cref="BinaryJsonNumber"/> whose numeric kind is <see cref="Kind.None"/>.
    /// </summary>
    public static BinaryJsonNumber None { get; } = default;

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
    public static bool operator ==(in BinaryJsonNumber left, in BinaryJsonNumber right) => Equals(left, right);

    /// <summary>
    /// Equality operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the values are equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in BinaryJsonNumber left, in JsonElement right) => Equals(left, right);

    /// <summary>
    /// Equality operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the values are equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in JsonElement left, in BinaryJsonNumber right) => Equals(left, right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the values are not equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(in BinaryJsonNumber left, in BinaryJsonNumber right)
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
    public static bool operator !=(in BinaryJsonNumber left, in JsonElement right)
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
    public static bool operator !=(in JsonElement left, in BinaryJsonNumber right)
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
    public static bool operator <(in BinaryJsonNumber left, in BinaryJsonNumber right)
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
    public static bool operator <=(in BinaryJsonNumber left, in BinaryJsonNumber right)
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
    public static bool operator >(in BinaryJsonNumber left, in BinaryJsonNumber right)
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
    public static bool operator >=(in BinaryJsonNumber left, in BinaryJsonNumber right)
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
    public static bool operator <(in BinaryJsonNumber left, in JsonElement right)
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
    public static bool operator <=(in BinaryJsonNumber left, in JsonElement right)
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
    public static bool operator >(in BinaryJsonNumber left, in JsonElement right)
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
    public static bool operator >=(in BinaryJsonNumber left, in JsonElement right)
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
    public static bool operator <(in JsonElement left, in BinaryJsonNumber right)
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
    public static bool operator <=(in JsonElement left, in BinaryJsonNumber right)
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
    public static bool operator >(in JsonElement left, in BinaryJsonNumber right)
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
                Kind.Int128 => ReadInt128(left.binaryData).Equals(ReadInt128(right.binaryData)),
                Kind.SByte => ReadSByte(left.binaryData).Equals(ReadSByte(right.binaryData)),
                Kind.Single => ReadSingle(left.binaryData).Equals(ReadSingle(right.binaryData)),
                Kind.UInt16 => ReadUInt16(left.binaryData).Equals(ReadUInt16(right.binaryData)),
                Kind.UInt32 => ReadUInt32(left.binaryData).Equals(ReadUInt32(right.binaryData)),
                Kind.UInt64 => ReadUInt64(left.binaryData).Equals(ReadUInt64(right.binaryData)),
                Kind.UInt128 => ReadUInt128(left.binaryData).Equals(ReadUInt128(right.binaryData)),
                _ => throw new NotSupportedException(),
            };
        }

        return right.numericKind switch
        {
            Kind.Byte => Equals(left, ReadByte(right.binaryData)),
            Kind.Decimal => Equals(left, ReadDecimal(right.binaryData)),
            Kind.Double => Equals(left, ReadDouble(right.binaryData)),
            Kind.Half => Equals(left, ReadHalf(right.binaryData)),
            Kind.Int16 => Equals(left, ReadInt16(right.binaryData)),
            Kind.Int32 => Equals(left, ReadInt32(right.binaryData)),
            Kind.Int64 => Equals(left, ReadInt64(right.binaryData)),
            Kind.Int128 => Equals(left, ReadInt128(right.binaryData)),
            Kind.SByte => Equals(left, ReadSByte(right.binaryData)),
            Kind.Single => Equals(left, ReadSingle(right.binaryData)),
            Kind.UInt16 => Equals(left, ReadUInt16(right.binaryData)),
            Kind.UInt32 => Equals(left, ReadUInt32(right.binaryData)),
            Kind.UInt64 => Equals(left, ReadUInt64(right.binaryData)),
            Kind.UInt128 => Equals(left, ReadUInt128(right.binaryData)),
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

        if (binaryNumber.numericKind == Kind.Decimal)
        {
            if (jsonNumber.TryGetDecimal(out decimal jsonNumberDecimal))
            {
                return Equals(binaryNumber, jsonNumberDecimal);
            }

            if (jsonNumber.TryGetDouble(out double jsonNumberDouble))
            {
                return Equals(binaryNumber, jsonNumberDouble);
            }
        }
        else
        {
            if (jsonNumber.TryGetDouble(out double jsonNumberDouble))
            {
                return Equals(binaryNumber, jsonNumberDouble);
            }
        }

        throw new NotSupportedException();
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
                Kind.Int128 => ReadInt128(left.binaryData).CompareTo(ReadInt128(right.binaryData)),
                Kind.SByte => ReadSByte(left.binaryData).CompareTo(ReadSByte(right.binaryData)),
                Kind.Single => ReadSingle(left.binaryData).CompareTo(ReadSingle(right.binaryData)),
                Kind.UInt16 => ReadUInt16(left.binaryData).CompareTo(ReadUInt16(right.binaryData)),
                Kind.UInt32 => ReadUInt32(left.binaryData).CompareTo(ReadUInt32(right.binaryData)),
                Kind.UInt64 => ReadUInt64(left.binaryData).CompareTo(ReadUInt64(right.binaryData)),
                Kind.UInt128 => ReadUInt128(left.binaryData).CompareTo(ReadUInt128(right.binaryData)),
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
            Kind.Int128 => Compare(ReadInt128(left.binaryData), right),
            Kind.SByte => Compare(ReadSByte(left.binaryData), right),
            Kind.Single => Compare(ReadSingle(left.binaryData), right),
            Kind.UInt16 => Compare(ReadUInt16(left.binaryData), right),
            Kind.UInt32 => Compare(ReadUInt32(left.binaryData), right),
            Kind.UInt64 => Compare(ReadUInt64(left.binaryData), right),
            Kind.UInt128 => Compare(ReadUInt128(left.binaryData), right),
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

        if (right.numericKind == Kind.Decimal)
        {
            if (left.TryGetDecimal(out decimal leftDecimal))
            {
                return Compare(leftDecimal, right);
            }

            if (left.TryGetDouble(out double leftDouble))
            {
                return Compare(leftDouble, right);
            }
        }
        else
        {
            if (left.TryGetDouble(out double leftDouble))
            {
                return Compare(leftDouble, right);
            }

            if (left.TryGetDecimal(out decimal leftDecimal))
            {
                return Compare(leftDecimal, right);
            }
        }

        throw new NotSupportedException();
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
    /// Determines if this value is a multiple of the other value.
    /// </summary>
    /// <param name="x">The value to test.</param>
    /// <param name="y">The factor to test.</param>
    /// <returns><see langword="true"/> if the value is a multiple of the given factor.</returns>
    /// <exception cref="NotSupportedException">The number format is not supported.</exception>
    /// <exception cref="OverflowException">The number could not be converted without overflow/loss of precision.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsMultipleOf(JsonElement x, BinaryJsonNumber y)
    {
        if (x.TryGetDouble(out double doubleValue))
        {
            return IsMultipleOf(doubleValue, y);
        }

        if (x.TryGetDecimal(out decimal decimalValue))
        {
            return IsMultipleOf(decimalValue, y);
        }

        throw new OverflowException();
    }

    /// <summary>
    /// Determines if this value is a multiple of the other value.
    /// </summary>
    /// <param name="x">The value to test.</param>
    /// <param name="y">The factor to test.</param>
    /// <returns><see langword="true"/> if the value is a multiple of the given factor.</returns>
    /// <exception cref="NotSupportedException">The number format is not supported.</exception>
    /// <exception cref="OverflowException">The number could not be converted without overflow/loss of precision.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsMultipleOf(decimal x, BinaryJsonNumber y)
    {
        if (PreciseConversionTo<decimal>.TryFrom(y, out decimal yAsDecimal))
        {
            return decimal.Abs(decimal.Remainder(x, yAsDecimal)) <= 1.0E-5M;
        }

        if (PreciseConversionTo<double>.TryFrom(x, out double xAsDouble) &&
            PreciseConversionTo<double>.TryFrom(y, out double yAsDouble))
        {
            return Math.Abs(Math.IEEERemainder(xAsDouble, yAsDouble)) <= 1.0E-9;
        }

        throw new OverflowException();
    }

    /// <summary>
    /// Determines if this value is a multiple of the other value.
    /// </summary>
    /// <typeparam name="TOther">The type of the number for comparison.</typeparam>
    /// <param name="x">The value to test.</param>
    /// <param name="y">The factor to test.</param>
    /// <returns><see langword="true"/> if the value is a multiple of the given factor.</returns>
    /// <exception cref="NotSupportedException">The number format is not supported.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsMultipleOf<TOther>(TOther x, BinaryJsonNumber y)
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

    /// <summary>
    /// Get the maximum number of characters if a number of a particular numeric kind is written to a string.
    /// </summary>
    /// <param name="numericKind">The numeric kind of the number.</param>
    /// <returns>The maximum possible number of characters if the number is written to a string.</returns>
    /// <remarks>Note that your format provider may add additional characters to the output. You should account for those when allocating a format.</remarks>
    public static int GetMaxCharLength(Kind numericKind)
    {
        return numericKind switch
        {
            Kind.Byte => 8,
            Kind.Decimal => 128,
            Kind.Double => 512,
            Kind.Half => 64,
            Kind.Int16 => 8,
            Kind.Int32 => 16,
            Kind.Int64 => 32,
            Kind.Int128 => 64,
            Kind.SByte => 8,
            Kind.Single => 64,
            Kind.UInt16 => 8,
            Kind.UInt32 => 16,
            Kind.UInt64 => 32,
            Kind.UInt128 => 64,
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Get the maximum number of characters if the number is written to a string.
    /// </summary>
    /// <returns>The maximum possible number of characters if the number is written to a string.</returns>
    /// <remarks>Note that your format provider may add additional characters to the output. You should account for those when allocating a format.</remarks>
    public int GetMaxCharLength()
    {
        return GetMaxCharLength(this.numericKind);
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        return this.numericKind switch
        {
            Kind.Byte => ReadByte(this.binaryData).TryFormat(destination, out charsWritten, format, provider),
            Kind.Decimal => ReadDecimal(this.binaryData).TryFormat(destination, out charsWritten, format, provider),
            Kind.Double => ReadDouble(this.binaryData).TryFormat(destination, out charsWritten, format, provider),
            Kind.Half => ReadHalf(this.binaryData).TryFormat(destination, out charsWritten, format, provider),
            Kind.Int16 => ReadInt16(this.binaryData).TryFormat(destination, out charsWritten, format, provider),
            Kind.Int32 => ReadInt32(this.binaryData).TryFormat(destination, out charsWritten, format, provider),
            Kind.Int64 => ReadInt64(this.binaryData).TryFormat(destination, out charsWritten, format, provider),
            Kind.Int128 => ReadInt128(this.binaryData).TryFormat(destination, out charsWritten, format, provider),
            Kind.SByte => ReadSByte(this.binaryData).TryFormat(destination, out charsWritten, format, provider),
            Kind.Single => ReadSingle(this.binaryData).TryFormat(destination, out charsWritten, format, provider),
            Kind.UInt16 => ReadUInt16(this.binaryData).TryFormat(destination, out charsWritten, format, provider),
            Kind.UInt32 => ReadUInt32(this.binaryData).TryFormat(destination, out charsWritten, format, provider),
            Kind.UInt64 => ReadUInt64(this.binaryData).TryFormat(destination, out charsWritten, format, provider),
            Kind.UInt128 => ReadUInt128(this.binaryData).TryFormat(destination, out charsWritten, format, provider),
            _ => throw new NotSupportedException(),
        };
    }

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return this.numericKind switch
        {
            Kind.Byte => ReadByte(this.binaryData).ToString(format, formatProvider),
            Kind.Decimal => ReadDecimal(this.binaryData).ToString(format, formatProvider),
            Kind.Double => ReadDouble(this.binaryData).ToString(format, formatProvider),
            Kind.Half => ReadHalf(this.binaryData).ToString(format, formatProvider),
            Kind.Int16 => ReadInt16(this.binaryData).ToString(format, formatProvider),
            Kind.Int32 => ReadInt32(this.binaryData).ToString(format, formatProvider),
            Kind.Int64 => ReadInt64(this.binaryData).ToString(format, formatProvider),
            Kind.Int128 => ReadInt128(this.binaryData).ToString(format, formatProvider),
            Kind.SByte => ReadSByte(this.binaryData).ToString(format, formatProvider),
            Kind.Single => ReadSingle(this.binaryData).ToString(format, formatProvider),
            Kind.UInt16 => ReadUInt16(this.binaryData).ToString(format, formatProvider),
            Kind.UInt32 => ReadUInt32(this.binaryData).ToString(format, formatProvider),
            Kind.UInt64 => ReadUInt64(this.binaryData).ToString(format, formatProvider),
            Kind.UInt128 => ReadUInt128(this.binaryData).ToString(format, formatProvider),
            _ => throw new NotSupportedException(),
        };
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.numericKind switch
        {
            Kind.Byte => ReadByte(this.binaryData).ToString(),
            Kind.Decimal => ReadDecimal(this.binaryData).ToString(),
            Kind.Double => ReadDouble(this.binaryData).ToString(),
            Kind.Half => ReadHalf(this.binaryData).ToString(),
            Kind.Int16 => ReadInt16(this.binaryData).ToString(),
            Kind.Int32 => ReadInt32(this.binaryData).ToString(),
            Kind.Int64 => ReadInt64(this.binaryData).ToString(),
            Kind.Int128 => ReadInt128(this.binaryData).ToString(),
            Kind.SByte => ReadSByte(this.binaryData).ToString(),
            Kind.Single => ReadSingle(this.binaryData).ToString(),
            Kind.UInt16 => ReadUInt16(this.binaryData).ToString(),
            Kind.UInt32 => ReadUInt32(this.binaryData).ToString(),
            Kind.UInt64 => ReadUInt64(this.binaryData).ToString(),
            Kind.UInt128 => ReadUInt128(this.binaryData).ToString(),
            _ => throw new NotSupportedException(),
        };
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        return this.numericKind switch
        {
            Kind.Byte => ReadByte(this.binaryData).TryFormat(utf8Destination, out bytesWritten, format, provider),
            Kind.Decimal => ReadDecimal(this.binaryData).TryFormat(utf8Destination, out bytesWritten, format, provider),
            Kind.Double => ReadDouble(this.binaryData).TryFormat(utf8Destination, out bytesWritten, format, provider),
            Kind.Half => ReadHalf(this.binaryData).TryFormat(utf8Destination, out bytesWritten, format, provider),
            Kind.Int16 => ReadInt16(this.binaryData).TryFormat(utf8Destination, out bytesWritten, format, provider),
            Kind.Int32 => ReadInt32(this.binaryData).TryFormat(utf8Destination, out bytesWritten, format, provider),
            Kind.Int64 => ReadInt64(this.binaryData).TryFormat(utf8Destination, out bytesWritten, format, provider),
            Kind.Int128 => ReadInt128(this.binaryData).TryFormat(utf8Destination, out bytesWritten, format, provider),
            Kind.SByte => ReadSByte(this.binaryData).TryFormat(utf8Destination, out bytesWritten, format, provider),
            Kind.Single => ReadSingle(this.binaryData).TryFormat(utf8Destination, out bytesWritten, format, provider),
            Kind.UInt16 => ReadUInt16(this.binaryData).TryFormat(utf8Destination, out bytesWritten, format, provider),
            Kind.UInt32 => ReadUInt32(this.binaryData).TryFormat(utf8Destination, out bytesWritten, format, provider),
            Kind.UInt64 => ReadUInt64(this.binaryData).TryFormat(utf8Destination, out bytesWritten, format, provider),
            Kind.UInt128 => ReadUInt128(this.binaryData).TryFormat(utf8Destination, out bytesWritten, format, provider),
            _ => throw new NotSupportedException(),
        };
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
            Kind.Int128 => PreciseConversionTo<TOther>.From(ReadInt128(this.binaryData)),
            Kind.SByte => PreciseConversionTo<TOther>.From(ReadSByte(this.binaryData)),
            Kind.Single => PreciseConversionTo<TOther>.From(ReadSingle(this.binaryData)),
            Kind.UInt16 => PreciseConversionTo<TOther>.From(ReadUInt16(this.binaryData)),
            Kind.UInt32 => PreciseConversionTo<TOther>.From(ReadUInt32(this.binaryData)),
            Kind.UInt64 => PreciseConversionTo<TOther>.From(ReadUInt64(this.binaryData)),
            Kind.UInt128 => PreciseConversionTo<TOther>.From(ReadUInt128(this.binaryData)),
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
            Kind.Int128 => TOther.CreateChecked(ReadInt128(this.binaryData)),
            Kind.SByte => TOther.CreateChecked(ReadSByte(this.binaryData)),
            Kind.Single => TOther.CreateChecked(ReadSingle(this.binaryData)),
            Kind.UInt16 => TOther.CreateChecked(ReadUInt16(this.binaryData)),
            Kind.UInt32 => TOther.CreateChecked(ReadUInt32(this.binaryData)),
            Kind.UInt64 => TOther.CreateChecked(ReadUInt64(this.binaryData)),
            Kind.UInt128 => TOther.CreateChecked(ReadUInt128(this.binaryData)),
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
            Kind.Int128 => TOther.CreateSaturating(ReadInt128(this.binaryData)),
            Kind.SByte => TOther.CreateSaturating(ReadSByte(this.binaryData)),
            Kind.Single => TOther.CreateSaturating(ReadSingle(this.binaryData)),
            Kind.UInt16 => TOther.CreateSaturating(ReadUInt16(this.binaryData)),
            Kind.UInt32 => TOther.CreateSaturating(ReadUInt32(this.binaryData)),
            Kind.UInt64 => TOther.CreateSaturating(ReadUInt64(this.binaryData)),
            Kind.UInt128 => TOther.CreateSaturating(ReadUInt128(this.binaryData)),
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
            Kind.Int128 => TOther.CreateTruncating(ReadInt128(this.binaryData)),
            Kind.SByte => TOther.CreateTruncating(ReadSByte(this.binaryData)),
            Kind.Single => TOther.CreateTruncating(ReadSingle(this.binaryData)),
            Kind.UInt16 => TOther.CreateTruncating(ReadUInt16(this.binaryData)),
            Kind.UInt32 => TOther.CreateTruncating(ReadUInt32(this.binaryData)),
            Kind.UInt64 => TOther.CreateTruncating(ReadUInt64(this.binaryData)),
            Kind.UInt128 => TOther.CreateTruncating(ReadUInt128(this.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Determines if this value is a multiple of the other value..
    /// </summary>
    /// <param name="multipleOf">The factor to test.</param>
    /// <returns><see langword="true"/> if the value is a multiple of the given factor.</returns>
    /// <exception cref="NotSupportedException">The number format is not supported.</exception>
    public bool IsMultipleOf(in BinaryJsonNumber multipleOf)
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
                Kind.Int128 => Int128.Abs(ReadInt128(this.binaryData)) % Int128.Abs(ReadInt128(multipleOf.binaryData)) == 0,
                Kind.SByte => Math.Abs(ReadInt16(this.binaryData)) % Math.Abs(ReadInt16(multipleOf.binaryData)) == 0,
                Kind.Single => Math.Abs(MathF.IEEERemainder(ReadSingle(this.binaryData), ReadSingle(multipleOf.binaryData))) <= 1.0E-5,
                Kind.UInt16 => ReadUInt16(this.binaryData) % ReadUInt16(multipleOf.binaryData) == 0,
                Kind.UInt32 => ReadUInt32(this.binaryData) % ReadUInt32(multipleOf.binaryData) == 0,
                Kind.UInt64 => ReadUInt64(this.binaryData) % ReadUInt64(multipleOf.binaryData) == 0,
                Kind.UInt128 => ReadUInt128(this.binaryData) % ReadUInt128(multipleOf.binaryData) == 0,
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
            Kind.Int128 => IsMultipleOf(ReadInt128(this.binaryData), multipleOf),
            Kind.SByte => IsMultipleOf(ReadSByte(this.binaryData), multipleOf),
            Kind.Single => IsMultipleOf(ReadSingle(this.binaryData), multipleOf),
            Kind.UInt16 => IsMultipleOf(ReadUInt16(this.binaryData), multipleOf),
            Kind.UInt32 => IsMultipleOf(ReadUInt32(this.binaryData), multipleOf),
            Kind.UInt64 => IsMultipleOf(ReadUInt64(this.binaryData), multipleOf),
            Kind.UInt128 => IsMultipleOf(ReadUInt128(this.binaryData), multipleOf),
            _ => throw new NotSupportedException(),
        };
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return (obj is BinaryJsonNumber number && this.Equals(number)) ||
               (obj is JsonElement jsonElement && this.Equals(jsonElement));
    }

    /// <summary>
    /// Equality comparison.
    /// </summary>
    /// <param name="other">The value with which to compare.</param>
    /// <returns><see langword="true"/> if the values are equal.</returns>
    public bool Equals(in BinaryJsonNumber other)
    {
        return Equals(this, other);
    }

    /// <summary>
    /// Equality comparison.
    /// </summary>
    /// <param name="other">The value with which to compare.</param>
    /// <returns><see langword="true"/> if the values are equal.</returns>
    public bool Equals(in JsonElement other)
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
            Kind.Int128 => ((double)ReadInt128(this.binaryData)).GetHashCode(),
            Kind.SByte => ((double)ReadSByte(this.binaryData)).GetHashCode(),
            Kind.Single => ((double)ReadSingle(this.binaryData)).GetHashCode(),
            Kind.UInt16 => ((double)ReadUInt16(this.binaryData)).GetHashCode(),
            Kind.UInt32 => ((double)ReadUInt32(this.binaryData)).GetHashCode(),
            Kind.UInt128 => ((double)ReadUInt128(this.binaryData)).GetHashCode(),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Comparison with another value.
    /// </summary>
    /// <param name="other">The value with which to compare.</param>
    /// <returns>0 if the values are equal, -1 if this value is less than the other, and 1 if this value is greater than the other.</returns>
    public int CompareTo(in BinaryJsonNumber other)
    {
        return Compare(this, other);
    }

    /// <summary>
    /// Comparison with another value.
    /// </summary>
    /// <param name="other">The value with which to compare.</param>
    /// <returns>0 if the values are equal, -1 if this value is less than the other, and 1 if this value is greater than the other.</returns>
    public int CompareTo(in JsonElement other)
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
        if (this.numericKind == Kind.Bool)
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
            case Kind.Int128:
                writer.WriteNumberValue((double)ReadInt128(this.binaryData));
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
            case Kind.UInt128:
                writer.WriteNumberValue((double)ReadUInt128(this.binaryData));
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
    /// <param name="value">The <see cref="UInt128"/> to write.</param>
    /// <param name="numberBacking">The number backing to which to write the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteNumeric(UInt128 value, Span<byte> numberBacking)
    {
        BinaryPrimitives.WriteUInt128LittleEndian(numberBacking, value);
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
    /// <param name="value">The <see cref="Int128"/> to write.</param>
    /// <param name="numberBacking">The number backing to which to write the value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteNumeric(Int128 value, Span<byte> numberBacking)
    {
        BinaryPrimitives.WriteInt128LittleEndian(numberBacking, value);
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
    private static UInt128 ReadUInt128(ReadOnlySpan<byte> numberBacking)
    {
        return BinaryPrimitives.ReadUInt128LittleEndian(numberBacking);
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
    private static Int128 ReadInt128(ReadOnlySpan<byte> numberBacking)
    {
        return BinaryPrimitives.ReadInt128LittleEndian(numberBacking);
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
    private static bool Equals(in BinaryJsonNumber binaryNumber, decimal numberBase)
    {
        if (binaryNumber.numericKind == Kind.Double)
        {
            double binaryNumberAsDouble = ReadDouble(binaryNumber.binaryData);
            if (PreciseConversionTo<decimal>.TryFrom(binaryNumberAsDouble, out decimal binaryNumberAsDecimal))
            {
                return binaryNumberAsDecimal.Equals(numberBase);
            }
            else
            {
                return PreciseConversionTo<double>.From(numberBase).Equals(binaryNumberAsDouble);
            }
        }

        if (binaryNumber.numericKind == Kind.Single)
        {
            float binaryNumberAsFloat = ReadSingle(binaryNumber.binaryData);
            if (PreciseConversionTo<decimal>.TryFrom(binaryNumberAsFloat, out decimal binaryNumberAsDecimal))
            {
                return binaryNumberAsDecimal.Equals(numberBase);
            }
            else
            {
                return PreciseConversionTo<double>.From(numberBase).Equals((double)binaryNumberAsFloat);
            }
        }

        if (binaryNumber.numericKind == Kind.Half)
        {
            Half binaryNumberAsHalf = ReadHalf(binaryNumber.binaryData);
            if (PreciseConversionTo<decimal>.TryFrom(binaryNumberAsHalf, out decimal binaryNumberAsDecimal))
            {
                return binaryNumberAsDecimal.Equals(numberBase);
            }
            else
            {
                return PreciseConversionTo<double>.From(numberBase).Equals((double)binaryNumberAsHalf);
            }
        }

        return binaryNumber.numericKind switch
        {
            Kind.Byte => numberBase.Equals(ReadByte(binaryNumber.binaryData)),
            Kind.Decimal => numberBase.Equals(ReadDecimal(binaryNumber.binaryData)),
            Kind.Int16 => numberBase.Equals(ReadInt16(binaryNumber.binaryData)),
            Kind.Int32 => numberBase.Equals(ReadInt32(binaryNumber.binaryData)),
            Kind.Int64 => numberBase.Equals(ReadInt64(binaryNumber.binaryData)),
            Kind.Int128 => numberBase.Equals(PreciseConversionTo<decimal>.From(ReadInt128(binaryNumber.binaryData))),
            Kind.SByte => numberBase.Equals(ReadSByte(binaryNumber.binaryData)),
            Kind.UInt16 => numberBase.Equals(ReadUInt16(binaryNumber.binaryData)),
            Kind.UInt32 => numberBase.Equals(ReadUInt32(binaryNumber.binaryData)),
            Kind.UInt64 => numberBase.Equals(ReadUInt64(binaryNumber.binaryData)),
            Kind.UInt128 => numberBase.Equals(PreciseConversionTo<decimal>.From(ReadUInt128(binaryNumber.binaryData))),
            _ => throw new NotSupportedException(),
        };
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

                if (PreciseConversionTo<decimal>.TryFrom(numberBase, out decimal numberBaseAsDecimal))
                {
                    return binaryNumberDecimal.Equals(numberBaseAsDecimal);
                }
                else
                {
                    return PreciseConversionTo<double>.From(binaryNumberDecimal).Equals(numberBaseDouble);
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
                Kind.Int128 => numberBaseDouble.Equals((double)ReadInt128(binaryNumber.binaryData)),
                Kind.SByte => numberBaseDouble.Equals(ReadSByte(binaryNumber.binaryData)),
                Kind.Single => numberBaseDouble.Equals((double)ReadSingle(binaryNumber.binaryData)),
                Kind.UInt16 => numberBaseDouble.Equals(ReadUInt16(binaryNumber.binaryData)),
                Kind.UInt32 => numberBaseDouble.Equals(ReadUInt32(binaryNumber.binaryData)),
                Kind.UInt64 => numberBaseDouble.Equals(ReadUInt64(binaryNumber.binaryData)),
                Kind.UInt128 => numberBaseDouble.Equals((double)ReadUInt128(binaryNumber.binaryData)),
                _ => throw new NotSupportedException(),
            };
        }

        // The decimal handling is special-cased above.
        throw new NotSupportedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Compare(decimal left, in BinaryJsonNumber right)
    {
        if (right.numericKind == Kind.Double)
        {
            double rightAsDouble = ReadDouble(right.binaryData);
            if (PreciseConversionTo<decimal>.TryFrom(rightAsDouble, out decimal rightAsDecimal))
            {
                return left.CompareTo(rightAsDecimal);
            }
            else
            {
                return PreciseConversionTo<double>.From(left).CompareTo(rightAsDouble);
            }
        }

        if (right.numericKind == Kind.Single)
        {
            float rightAsFloat = ReadSingle(right.binaryData);
            if (PreciseConversionTo<decimal>.TryFrom(rightAsFloat, out decimal rightAsDecimal))
            {
                return left.CompareTo(rightAsDecimal);
            }
            else
            {
                return PreciseConversionTo<double>.From(left).CompareTo((double)rightAsFloat);
            }
        }

        if (right.numericKind == Kind.Half)
        {
            Half rightAsHalf = ReadHalf(right.binaryData);
            if (PreciseConversionTo<decimal>.TryFrom(rightAsHalf, out decimal rightAsDecimal))
            {
                return left.CompareTo(rightAsDecimal);
            }
            else
            {
                return PreciseConversionTo<double>.From(left).CompareTo((double)rightAsHalf);
            }
        }

        return right.numericKind switch
        {
            Kind.Byte => left.CompareTo(ReadByte(right.binaryData)),
            Kind.Decimal => left.CompareTo(ReadDecimal(right.binaryData)),
            Kind.Int16 => left.CompareTo(ReadInt16(right.binaryData)),
            Kind.Int32 => left.CompareTo(ReadInt32(right.binaryData)),
            Kind.Int64 => left.CompareTo(ReadInt64(right.binaryData)),
            Kind.Int128 => left.CompareTo(PreciseConversionTo<decimal>.From(ReadInt128(right.binaryData))),
            Kind.SByte => left.CompareTo(ReadSByte(right.binaryData)),
            Kind.UInt16 => left.CompareTo(ReadUInt16(right.binaryData)),
            Kind.UInt32 => left.CompareTo(ReadUInt32(right.binaryData)),
            Kind.UInt64 => left.CompareTo(ReadUInt64(right.binaryData)),
            Kind.UInt128 => left.CompareTo(PreciseConversionTo<decimal>.From(ReadUInt128(right.binaryData))),
            _ => throw new NotSupportedException(),
        };
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

                // Then try to use it as a decimal
                if (PreciseConversionTo<decimal>.TryFrom(left, out decimal leftDecimalForRightDecimal))
                {
                    return leftDecimalForRightDecimal.CompareTo(rightDecimal);
                }

                // Otherwise fall back on the double
                if (PreciseConversionTo<double>.TryFrom(rightDecimal, out double rightDecimalAsDouble))
                {
                    return leftDouble.CompareTo(rightDecimalAsDouble);
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
                Kind.Int128 => leftDouble.CompareTo((double)ReadInt128(right.binaryData)),
                Kind.SByte => leftDouble.CompareTo(ReadSByte(right.binaryData)),
                Kind.Single => leftDouble.CompareTo((double)ReadSingle(right.binaryData)),
                Kind.UInt16 => leftDouble.CompareTo(ReadUInt16(right.binaryData)),
                Kind.UInt32 => leftDouble.CompareTo(ReadUInt32(right.binaryData)),
                Kind.UInt64 => leftDouble.CompareTo(ReadUInt64(right.binaryData)),
                Kind.UInt128 => leftDouble.CompareTo((double)ReadUInt128(right.binaryData)),
                _ => throw new NotSupportedException(),
            };
        }

        // The decimal handling is special-cased above.
        throw new NotSupportedException();
    }

// The analyzers have not caught up with this structure.
#pragma warning disable
    [InlineArray(16)]
    private struct ByteBuffer16
    {
        private byte element0;
    }
#pragma warning restore

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

        public static TOut From(in BinaryJsonNumber value)
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
                Kind.Decimal => TryFrom(ReadDecimal(value.binaryData), out result),
                Kind.Double => TryFrom(ReadDouble(value.binaryData), out result),
                Kind.Half => TryFrom(ReadHalf(value.binaryData), out result),
                Kind.Int16 => TryFrom(ReadInt16(value.binaryData), out result),
                Kind.Int32 => TryFrom(ReadInt32(value.binaryData), out result),
                Kind.Int64 => TryFrom(ReadInt64(value.binaryData), out result),
                Kind.Int128 => TryFrom(ReadInt128(value.binaryData), out result),
                Kind.SByte => TryFrom(ReadSByte(value.binaryData), out result),
                Kind.Single => TryFrom(ReadSingle(value.binaryData), out result),
                Kind.UInt16 => TryFrom(ReadUInt16(value.binaryData), out result),
                Kind.UInt32 => TryFrom(ReadUInt32(value.binaryData), out result),
                Kind.UInt64 => TryFrom(ReadUInt64(value.binaryData), out result),
                Kind.UInt128 => TryFrom(ReadUInt128(value.binaryData), out result),
                _ => throw new NotSupportedException(),
            };
        }
    }
}