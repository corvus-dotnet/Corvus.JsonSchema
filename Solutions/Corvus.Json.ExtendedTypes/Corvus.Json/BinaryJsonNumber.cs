// <copyright file="BinaryJsonNumber.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET8_0_OR_GREATER
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
    IUtf8SpanFormattable,
    INumberBase<BinaryJsonNumber>
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
    /// Gets the BinaryJsonNumber for 1.
    /// </summary>
    public static BinaryJsonNumber One { get; } = new BinaryJsonNumber(1);

    /// <summary>
    /// Gets the Radix for the BinaryJsonNumber (2).
    /// </summary>
    public static int Radix { get; } = 2;

    /// <summary>
    /// Gets the BinaryJsonNumber for 0.
    /// </summary>
    public static BinaryJsonNumber Zero { get; } = new BinaryJsonNumber(0);

    /// <summary>
    /// Gets the BinaryJsonNumber for 0.
    /// </summary>
    public static BinaryJsonNumber AdditiveIdentity => Zero;

    /// <summary>
    /// Gets the BinaryJsonNumber for 1.
    /// </summary>
    public static BinaryJsonNumber MultiplicativeIdentity => One;

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
    /// Addition operator.
    /// </summary>
    /// <param name="left">The left hand side of the addition.</param>
    /// <param name="right">The right hand side of the addition.</param>
    /// <returns>The result of the addition.</returns>
    /// <exception cref="NotSupportedException">The numeric format is not supported.</exception>
    public static BinaryJsonNumber operator +(BinaryJsonNumber left, BinaryJsonNumber right)
    {
        if (left.numericKind == right.numericKind)
        {
            return left.numericKind switch
            {
                Kind.Byte => new(ReadByte(left.binaryData) + ReadByte(right.binaryData)),
                Kind.Decimal => new(ReadDecimal(left.binaryData) + ReadDecimal(right.binaryData)),
                Kind.Double => new(ReadDouble(left.binaryData) + ReadDouble(right.binaryData)),
                Kind.Half => new(ReadHalf(left.binaryData) + ReadHalf(right.binaryData)),
                Kind.Int16 => new(ReadInt16(left.binaryData) + ReadInt16(right.binaryData)),
                Kind.Int32 => new(ReadInt32(left.binaryData) + ReadInt32(right.binaryData)),
                Kind.Int64 => new(ReadInt64(left.binaryData) + ReadInt64(right.binaryData)),
                Kind.Int128 => new(ReadInt128(left.binaryData) + ReadInt128(right.binaryData)),
                Kind.SByte => new(ReadSByte(left.binaryData) + ReadSByte(right.binaryData)),
                Kind.Single => new(ReadSingle(left.binaryData) + ReadSingle(right.binaryData)),
                Kind.UInt16 => new(ReadUInt16(left.binaryData) + ReadUInt16(right.binaryData)),
                Kind.UInt32 => new(ReadUInt32(left.binaryData) + ReadUInt32(right.binaryData)),
                Kind.UInt64 => new(ReadUInt64(left.binaryData) + ReadUInt64(right.binaryData)),
                Kind.UInt128 => new(ReadUInt128(left.binaryData) + ReadUInt128(right.binaryData)),
                _ => throw new NotSupportedException(),
            };
        }

        return right.numericKind switch
        {
            Kind.Byte => Add(left, ReadByte(right.binaryData)),
            Kind.Decimal => Add(left, ReadDecimal(right.binaryData)),
            Kind.Double => Add(left, ReadDouble(right.binaryData)),
            Kind.Half => Add(left, ReadHalf(right.binaryData)),
            Kind.Int16 => Add(left, ReadInt16(right.binaryData)),
            Kind.Int32 => Add(left, ReadInt32(right.binaryData)),
            Kind.Int64 => Add(left, ReadInt64(right.binaryData)),
            Kind.Int128 => Add(left, ReadInt128(right.binaryData)),
            Kind.SByte => Add(left, ReadSByte(right.binaryData)),
            Kind.Single => Add(left, ReadSingle(right.binaryData)),
            Kind.UInt16 => Add(left, ReadUInt16(right.binaryData)),
            Kind.UInt32 => Add(left, ReadUInt32(right.binaryData)),
            Kind.UInt64 => Add(left, ReadUInt64(right.binaryData)),
            Kind.UInt128 => Add(left, ReadUInt128(right.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Subtraction operator.
    /// </summary>
    /// <param name="left">The left hand side of the subtraction.</param>
    /// <param name="right">The right hand side of the subtraction.</param>
    /// <returns>The result of the subtraction.</returns>
    /// <exception cref="NotSupportedException">The numeric format is not supported.</exception>
    public static BinaryJsonNumber operator -(BinaryJsonNumber left, BinaryJsonNumber right)
    {
        if (left.numericKind == right.numericKind)
        {
            return left.numericKind switch
            {
                Kind.Byte => new(ReadByte(left.binaryData) - ReadByte(right.binaryData)),
                Kind.Decimal => new(ReadDecimal(left.binaryData) - ReadDecimal(right.binaryData)),
                Kind.Double => new(ReadDouble(left.binaryData) - ReadDouble(right.binaryData)),
                Kind.Half => new(ReadHalf(left.binaryData) - ReadHalf(right.binaryData)),
                Kind.Int16 => new(ReadInt16(left.binaryData) - ReadInt16(right.binaryData)),
                Kind.Int32 => new(ReadInt32(left.binaryData) - ReadInt32(right.binaryData)),
                Kind.Int64 => new(ReadInt64(left.binaryData) - ReadInt64(right.binaryData)),
                Kind.Int128 => new(ReadInt128(left.binaryData) - ReadInt128(right.binaryData)),
                Kind.SByte => new(ReadSByte(left.binaryData) - ReadSByte(right.binaryData)),
                Kind.Single => new(ReadSingle(left.binaryData) - ReadSingle(right.binaryData)),
                Kind.UInt16 => new(ReadUInt16(left.binaryData) - ReadUInt16(right.binaryData)),
                Kind.UInt32 => new(ReadUInt32(left.binaryData) - ReadUInt32(right.binaryData)),
                Kind.UInt64 => new(ReadUInt64(left.binaryData) - ReadUInt64(right.binaryData)),
                Kind.UInt128 => new(ReadUInt128(left.binaryData) - ReadUInt128(right.binaryData)),
                _ => throw new NotSupportedException(),
            };
        }

        return right.numericKind switch
        {
            Kind.Byte => Subtract(left, ReadByte(right.binaryData)),
            Kind.Decimal => Subtract(left, ReadDecimal(right.binaryData)),
            Kind.Double => Subtract(left, ReadDouble(right.binaryData)),
            Kind.Half => Subtract(left, ReadHalf(right.binaryData)),
            Kind.Int16 => Subtract(left, ReadInt16(right.binaryData)),
            Kind.Int32 => Subtract(left, ReadInt32(right.binaryData)),
            Kind.Int64 => Subtract(left, ReadInt64(right.binaryData)),
            Kind.Int128 => Subtract(left, ReadInt128(right.binaryData)),
            Kind.SByte => Subtract(left, ReadSByte(right.binaryData)),
            Kind.Single => Subtract(left, ReadSingle(right.binaryData)),
            Kind.UInt16 => Subtract(left, ReadUInt16(right.binaryData)),
            Kind.UInt32 => Subtract(left, ReadUInt32(right.binaryData)),
            Kind.UInt64 => Subtract(left, ReadUInt64(right.binaryData)),
            Kind.UInt128 => Subtract(left, ReadUInt128(right.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Multiplication operator.
    /// </summary>
    /// <param name="left">The left hand side of the multiplication.</param>
    /// <param name="right">The right hand side of the multiplication.</param>
    /// <returns>The result of the multiplication.</returns>
    /// <exception cref="NotSupportedException">The numeric format is not supported.</exception>
    public static BinaryJsonNumber operator *(BinaryJsonNumber left, BinaryJsonNumber right)
    {
        if (left.numericKind == right.numericKind)
        {
            return left.numericKind switch
            {
                Kind.Byte => new(ReadByte(left.binaryData) * ReadByte(right.binaryData)),
                Kind.Decimal => new(ReadDecimal(left.binaryData) * ReadDecimal(right.binaryData)),
                Kind.Double => new(ReadDouble(left.binaryData) * ReadDouble(right.binaryData)),
                Kind.Half => new(ReadHalf(left.binaryData) * ReadHalf(right.binaryData)),
                Kind.Int16 => new(ReadInt16(left.binaryData) * ReadInt16(right.binaryData)),
                Kind.Int32 => new(ReadInt32(left.binaryData) * ReadInt32(right.binaryData)),
                Kind.Int64 => new(ReadInt64(left.binaryData) * ReadInt64(right.binaryData)),
                Kind.Int128 => new(ReadInt128(left.binaryData) * ReadInt128(right.binaryData)),
                Kind.SByte => new(ReadSByte(left.binaryData) * ReadSByte(right.binaryData)),
                Kind.Single => new(ReadSingle(left.binaryData) * ReadSingle(right.binaryData)),
                Kind.UInt16 => new(ReadUInt16(left.binaryData) * ReadUInt16(right.binaryData)),
                Kind.UInt32 => new(ReadUInt32(left.binaryData) * ReadUInt32(right.binaryData)),
                Kind.UInt64 => new(ReadUInt64(left.binaryData) * ReadUInt64(right.binaryData)),
                Kind.UInt128 => new(ReadUInt128(left.binaryData) * ReadUInt128(right.binaryData)),
                _ => throw new NotSupportedException(),
            };
        }

        return right.numericKind switch
        {
            Kind.Byte => Multiply(left, ReadByte(right.binaryData)),
            Kind.Decimal => Multiply(left, ReadDecimal(right.binaryData)),
            Kind.Double => Multiply(left, ReadDouble(right.binaryData)),
            Kind.Half => Multiply(left, ReadHalf(right.binaryData)),
            Kind.Int16 => Multiply(left, ReadInt16(right.binaryData)),
            Kind.Int32 => Multiply(left, ReadInt32(right.binaryData)),
            Kind.Int64 => Multiply(left, ReadInt64(right.binaryData)),
            Kind.Int128 => Multiply(left, ReadInt128(right.binaryData)),
            Kind.SByte => Multiply(left, ReadSByte(right.binaryData)),
            Kind.Single => Multiply(left, ReadSingle(right.binaryData)),
            Kind.UInt16 => Multiply(left, ReadUInt16(right.binaryData)),
            Kind.UInt32 => Multiply(left, ReadUInt32(right.binaryData)),
            Kind.UInt64 => Multiply(left, ReadUInt64(right.binaryData)),
            Kind.UInt128 => Multiply(left, ReadUInt128(right.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Division operator.
    /// </summary>
    /// <param name="left">The left hand side of the division.</param>
    /// <param name="right">The right hand side of the division.</param>
    /// <returns>The result of the division.</returns>
    /// <exception cref="NotSupportedException">The numeric format is not supported.</exception>
    public static BinaryJsonNumber operator /(BinaryJsonNumber left, BinaryJsonNumber right)
    {
        if (left.numericKind == right.numericKind)
        {
            return left.numericKind switch
            {
                Kind.Byte => new(ReadByte(left.binaryData) / ReadByte(right.binaryData)),
                Kind.Decimal => new(ReadDecimal(left.binaryData) / ReadDecimal(right.binaryData)),
                Kind.Double => new(ReadDouble(left.binaryData) / ReadDouble(right.binaryData)),
                Kind.Half => new(ReadHalf(left.binaryData) / ReadHalf(right.binaryData)),
                Kind.Int16 => new(ReadInt16(left.binaryData) / ReadInt16(right.binaryData)),
                Kind.Int32 => new(ReadInt32(left.binaryData) / ReadInt32(right.binaryData)),
                Kind.Int64 => new(ReadInt64(left.binaryData) / ReadInt64(right.binaryData)),
                Kind.Int128 => new(ReadInt128(left.binaryData) / ReadInt128(right.binaryData)),
                Kind.SByte => new(ReadSByte(left.binaryData) / ReadSByte(right.binaryData)),
                Kind.Single => new(ReadSingle(left.binaryData) / ReadSingle(right.binaryData)),
                Kind.UInt16 => new(ReadUInt16(left.binaryData) / ReadUInt16(right.binaryData)),
                Kind.UInt32 => new(ReadUInt32(left.binaryData) / ReadUInt32(right.binaryData)),
                Kind.UInt64 => new(ReadUInt64(left.binaryData) / ReadUInt64(right.binaryData)),
                Kind.UInt128 => new(ReadUInt128(left.binaryData) / ReadUInt128(right.binaryData)),
                _ => throw new NotSupportedException(),
            };
        }

        return right.numericKind switch
        {
            Kind.Byte => Divide(left, ReadByte(right.binaryData)),
            Kind.Decimal => Divide(left, ReadDecimal(right.binaryData)),
            Kind.Double => Divide(left, ReadDouble(right.binaryData)),
            Kind.Half => Divide(left, ReadHalf(right.binaryData)),
            Kind.Int16 => Divide(left, ReadInt16(right.binaryData)),
            Kind.Int32 => Divide(left, ReadInt32(right.binaryData)),
            Kind.Int64 => Divide(left, ReadInt64(right.binaryData)),
            Kind.Int128 => Divide(left, ReadInt128(right.binaryData)),
            Kind.SByte => Divide(left, ReadSByte(right.binaryData)),
            Kind.Single => Divide(left, ReadSingle(right.binaryData)),
            Kind.UInt16 => Divide(left, ReadUInt16(right.binaryData)),
            Kind.UInt32 => Divide(left, ReadUInt32(right.binaryData)),
            Kind.UInt64 => Divide(left, ReadUInt64(right.binaryData)),
            Kind.UInt128 => Divide(left, ReadUInt128(right.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Decrement operator.
    /// </summary>
    /// <param name="value">The number to decrement.</param>
    /// <returns>The decremented number.</returns>
    /// <exception cref="NotSupportedException">The numeric format was not supported.</exception>
    public static BinaryJsonNumber operator --(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Byte => new(ReadByte(value.binaryData) - 1),
            Kind.Decimal => new(ReadDecimal(value.binaryData) - 1m),
            Kind.Double => new(ReadDouble(value.binaryData) - 1),
            Kind.Half => new(ReadHalf(value.binaryData) - (Half)1),
            Kind.Int16 => new(ReadInt16(value.binaryData) - 1),
            Kind.Int32 => new(ReadInt32(value.binaryData) - 1),
            Kind.Int64 => new(ReadInt64(value.binaryData) - 1),
            Kind.Int128 => new(ReadInt128(value.binaryData) - 1),
            Kind.SByte => new(ReadSByte(value.binaryData) - 1),
            Kind.Single => new(ReadSingle(value.binaryData) - 1),
            Kind.UInt16 => new(ReadUInt16(value.binaryData) - 1),
            Kind.UInt32 => new(ReadUInt32(value.binaryData) - 1),
            Kind.UInt64 => new(ReadUInt64(value.binaryData) - 1),
            Kind.UInt128 => new(ReadUInt128(value.binaryData) - 1),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// The equality operator.
    /// </summary>
    /// <param name="left">The lhs.</param>
    /// <param name="right">The rhs.</param>
    /// <returns><see langword="true"/> if the values are equal.</returns>
    public static bool operator ==(BinaryJsonNumber left, BinaryJsonNumber right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// The inequality operator.
    /// </summary>
    /// <param name="left">The lhs.</param>
    /// <param name="right">The rhs.</param>
    /// <returns><see langword="true"/> if the the values are not equal.</returns>
    public static bool operator !=(BinaryJsonNumber left, BinaryJsonNumber right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Increment operator.
    /// </summary>
    /// <param name="value">The value to increment.</param>
    /// <returns>The incremented value.</returns>
    /// <exception cref="NotSupportedException">The numeric format was not supported.</exception>
    public static BinaryJsonNumber operator ++(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Byte => new(ReadByte(value.binaryData) + 1),
            Kind.Decimal => new(ReadDecimal(value.binaryData) + 1m),
            Kind.Double => new(ReadDouble(value.binaryData) + 1),
            Kind.Half => new(ReadHalf(value.binaryData) + (Half)1),
            Kind.Int16 => new(ReadInt16(value.binaryData) + 1),
            Kind.Int32 => new(ReadInt32(value.binaryData) + 1),
            Kind.Int64 => new(ReadInt64(value.binaryData) + 1),
            Kind.Int128 => new(ReadInt128(value.binaryData) + 1),
            Kind.SByte => new(ReadSByte(value.binaryData) + 1),
            Kind.Single => new(ReadSingle(value.binaryData) + 1),
            Kind.UInt16 => new(ReadUInt16(value.binaryData) + 1),
            Kind.UInt32 => new(ReadUInt32(value.binaryData) + 1),
            Kind.UInt64 => new(ReadUInt64(value.binaryData) + 1),
            Kind.UInt128 => new(ReadUInt128(value.binaryData) + 1),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Negation operator.
    /// </summary>
    /// <param name="value">The value on which to operate.</param>
    /// <returns>The negated value.</returns>
    /// <exception cref="NotSupportedException">The numeric format was not supported.</exception>
    public static BinaryJsonNumber operator -(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Byte => new(-ReadByte(value.binaryData)),
            Kind.Decimal => new(-ReadDecimal(value.binaryData)),
            Kind.Double => new(-ReadDouble(value.binaryData)),
            Kind.Half => new(-ReadHalf(value.binaryData)),
            Kind.Int16 => new(-ReadInt16(value.binaryData)),
            Kind.Int32 => new(-ReadInt32(value.binaryData)),
            Kind.Int64 => new(-ReadInt64(value.binaryData)),
            Kind.Int128 => new(-ReadInt128(value.binaryData)),
            Kind.SByte => new(-ReadSByte(value.binaryData)),
            Kind.Single => new(-ReadSingle(value.binaryData)),
            Kind.UInt16 => new(-ReadUInt16(value.binaryData)),
            Kind.UInt32 => new(-ReadUInt32(value.binaryData)),
            Kind.UInt64 => value,
            Kind.UInt128 => new(-ReadUInt128(value.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Unary plus operator.
    /// </summary>
    /// <param name="value">The value on which to operate.</param>
    /// <returns>The unary plus value.</returns>
    /// <exception cref="NotSupportedException">The numeric format was not supported.</exception>
    public static BinaryJsonNumber operator +(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Byte => new(+ReadByte(value.binaryData)),
            Kind.Decimal => new(+ReadDecimal(value.binaryData)),
            Kind.Double => new(+ReadDouble(value.binaryData)),
            Kind.Half => new(+ReadHalf(value.binaryData)),
            Kind.Int16 => new(+ReadInt16(value.binaryData)),
            Kind.Int32 => new(+ReadInt32(value.binaryData)),
            Kind.Int64 => new(+ReadInt64(value.binaryData)),
            Kind.Int128 => new(+ReadInt128(value.binaryData)),
            Kind.SByte => new(+ReadSByte(value.binaryData)),
            Kind.Single => new(+ReadSingle(value.binaryData)),
            Kind.UInt16 => new(+ReadUInt16(value.binaryData)),
            Kind.UInt32 => new(+ReadUInt32(value.binaryData)),
            Kind.UInt64 => new(+ReadUInt64(value.binaryData)),
            Kind.UInt128 => new(+ReadUInt128(value.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Computes the absolute of a value.
    /// </summary>
    /// <param name="value">The value for which to calculate the absolute.</param>
    /// <returns>The absolute value.</returns>
    /// <exception cref="NotSupportedException">The numeric format was not supported.</exception>
    public static BinaryJsonNumber Abs(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Byte => value,
            Kind.Decimal => new(decimal.Abs(ReadDecimal(value.binaryData))),
            Kind.Double => new(double.Abs(ReadDouble(value.binaryData))),
            Kind.Half => new(Half.Abs(ReadHalf(value.binaryData))),
            Kind.Int16 => new(short.Abs(ReadInt16(value.binaryData))),
            Kind.Int32 => new(int.Abs(ReadInt32(value.binaryData))),
            Kind.Int64 => new(long.Abs(ReadInt64(value.binaryData))),
            Kind.Int128 => new(Int128.Abs(ReadInt128(value.binaryData))),
            Kind.SByte => new(sbyte.Abs(ReadSByte(value.binaryData))),
            Kind.Single => new(float.Abs(ReadSingle(value.binaryData))),
            Kind.UInt16 => value,
            Kind.UInt32 => value,
            Kind.UInt64 => value,
            Kind.UInt128 => value,
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Determine if a value is in its canonical representation.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is in its canonical representation.</returns>
    public static bool IsCanonical(BinaryJsonNumber value)
    {
        return true;
    }

    /// <summary>
    /// Determine if a value is a complex number.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>Always <see langword="false"/>.</returns>
    public static bool IsComplexNumber(BinaryJsonNumber value)
    {
        return false;
    }

    /// <summary>
    /// Determine if a value is an even integer.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is an even integer.</returns>
    /// <exception cref="NotSupportedException">The numeric format was not supported.</exception>
    public static bool IsEvenInteger(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Byte => byte.IsEvenInteger(ReadByte(value.binaryData)),
            Kind.Decimal => decimal.IsEvenInteger(ReadDecimal(value.binaryData)),
            Kind.Double => double.IsEvenInteger(ReadDouble(value.binaryData)),
            Kind.Half => Half.IsEvenInteger(ReadHalf(value.binaryData)),
            Kind.Int16 => short.IsEvenInteger(ReadInt16(value.binaryData)),
            Kind.Int32 => int.IsEvenInteger(ReadInt32(value.binaryData)),
            Kind.Int64 => long.IsEvenInteger(ReadInt64(value.binaryData)),
            Kind.Int128 => Int128.IsEvenInteger(ReadInt128(value.binaryData)),
            Kind.SByte => sbyte.IsEvenInteger(ReadSByte(value.binaryData)),
            Kind.Single => float.IsEvenInteger(ReadSingle(value.binaryData)),
            Kind.UInt16 => ushort.IsEvenInteger(ReadUInt16(value.binaryData)),
            Kind.UInt32 => uint.IsEvenInteger(ReadUInt32(value.binaryData)),
            Kind.UInt64 => ulong.IsEvenInteger(ReadUInt64(value.binaryData)),
            Kind.UInt128 => UInt128.IsEvenInteger(ReadUInt128(value.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Determine if a value is finite.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is finite.</returns>
    public static bool IsFinite(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Double => double.IsFinite(ReadDouble(value.binaryData)),
            Kind.Half => Half.IsFinite(ReadHalf(value.binaryData)),
            Kind.Single => float.IsFinite(ReadSingle(value.binaryData)),
            _ => true,
        };
    }

    /// <summary>
    /// Determine if a value is an imaginary number.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is imaginary.</returns>
    public static bool IsImaginaryNumber(BinaryJsonNumber value)
    {
        return false;
    }

    /// <summary>
    /// Determine if a value is infinity.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is infinity.</returns>
    public static bool IsInfinity(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Double => double.IsInfinity(ReadDouble(value.binaryData)),
            Kind.Half => Half.IsInfinity(ReadHalf(value.binaryData)),
            Kind.Single => float.IsInfinity(ReadSingle(value.binaryData)),
            _ => false,
        };
    }

    /// <summary>
    /// Determine if a value is an integer.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is an integer.</returns>
    public static bool IsInteger(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Decimal => decimal.IsInteger(ReadDecimal(value.binaryData)),
            Kind.Double => double.IsInteger(ReadDouble(value.binaryData)),
            Kind.Half => Half.IsInteger(ReadHalf(value.binaryData)),
            Kind.Single => float.IsInteger(ReadSingle(value.binaryData)),
            _ => true,
        };
    }

    /// <summary>
    /// Determine if a value is NaN.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is NaN.</returns>
    public static bool IsNaN(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Double => double.IsNaN(ReadDouble(value.binaryData)),
            Kind.Half => Half.IsNaN(ReadHalf(value.binaryData)),
            Kind.Single => float.IsNaN(ReadSingle(value.binaryData)),
            _ => false,
        };
    }

    /// <summary>
    /// Determine if a value is negative.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is negative.</returns>
    public static bool IsNegative(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Decimal => decimal.IsNegative(ReadDecimal(value.binaryData)),
            Kind.Double => double.IsNegative(ReadDouble(value.binaryData)),
            Kind.Half => Half.IsNegative(ReadHalf(value.binaryData)),
            Kind.Int16 => short.IsNegative(ReadInt16(value.binaryData)),
            Kind.Int32 => int.IsNegative(ReadInt32(value.binaryData)),
            Kind.Int64 => long.IsNegative(ReadInt64(value.binaryData)),
            Kind.Int128 => Int128.IsNegative(ReadInt128(value.binaryData)),
            Kind.SByte => sbyte.IsNegative(ReadSByte(value.binaryData)),
            Kind.Single => float.IsNegative(ReadSingle(value.binaryData)),
            _ => false,
        };
    }

    /// <summary>
    /// Determine if a value is negative infinity.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is negative infinity.</returns>
    public static bool IsNegativeInfinity(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Double => double.IsNegativeInfinity(ReadDouble(value.binaryData)),
            Kind.Half => Half.IsNegativeInfinity(ReadHalf(value.binaryData)),
            Kind.Single => float.IsNegativeInfinity(ReadSingle(value.binaryData)),
            _ => false,
        };
    }

    /// <summary>
    /// Determine if a value is normal.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is normal.</returns>
    public static bool IsNormal(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Double => double.IsNormal(ReadDouble(value.binaryData)),
            Kind.Half => Half.IsNormal(ReadHalf(value.binaryData)),
            Kind.Single => float.IsNormal(ReadSingle(value.binaryData)),
            _ => true,
        };
    }

    /// <summary>
    /// Determine if a value is an odd integer.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is an odd integer.</returns>
    /// <exception cref="NotSupportedException">The numeric format was not supported.</exception>
    public static bool IsOddInteger(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Byte => byte.IsOddInteger(ReadByte(value.binaryData)),
            Kind.Decimal => decimal.IsOddInteger(ReadDecimal(value.binaryData)),
            Kind.Double => double.IsOddInteger(ReadDouble(value.binaryData)),
            Kind.Half => Half.IsOddInteger(ReadHalf(value.binaryData)),
            Kind.Int16 => short.IsOddInteger(ReadInt16(value.binaryData)),
            Kind.Int32 => int.IsOddInteger(ReadInt32(value.binaryData)),
            Kind.Int64 => long.IsOddInteger(ReadInt64(value.binaryData)),
            Kind.Int128 => Int128.IsOddInteger(ReadInt128(value.binaryData)),
            Kind.SByte => sbyte.IsOddInteger(ReadSByte(value.binaryData)),
            Kind.Single => float.IsOddInteger(ReadSingle(value.binaryData)),
            Kind.UInt16 => ushort.IsOddInteger(ReadUInt16(value.binaryData)),
            Kind.UInt32 => uint.IsOddInteger(ReadUInt32(value.binaryData)),
            Kind.UInt64 => ulong.IsOddInteger(ReadUInt64(value.binaryData)),
            Kind.UInt128 => UInt128.IsOddInteger(ReadUInt128(value.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Determine if a value is a positive number.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is positive.</returns>
    public static bool IsPositive(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Decimal => decimal.IsPositive(ReadDecimal(value.binaryData)),
            Kind.Double => double.IsPositive(ReadDouble(value.binaryData)),
            Kind.Half => Half.IsPositive(ReadHalf(value.binaryData)),
            Kind.Int16 => short.IsPositive(ReadInt16(value.binaryData)),
            Kind.Int32 => int.IsPositive(ReadInt32(value.binaryData)),
            Kind.Int64 => long.IsPositive(ReadInt64(value.binaryData)),
            Kind.Int128 => Int128.IsPositive(ReadInt128(value.binaryData)),
            Kind.SByte => sbyte.IsPositive(ReadSByte(value.binaryData)),
            Kind.Single => float.IsPositive(ReadSingle(value.binaryData)),
            _ => true,
        };
    }

    /// <summary>
    /// Determine if a value is positive infinity.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is positive infinity.</returns>
    public static bool IsPositiveInfinity(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Double => double.IsNegativeInfinity(ReadDouble(value.binaryData)),
            Kind.Half => Half.IsNegativeInfinity(ReadHalf(value.binaryData)),
            Kind.Single => float.IsNegativeInfinity(ReadSingle(value.binaryData)),
            _ => false,
        };
    }

    /// <summary>
    /// Determine if a value is real.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is real.</returns>
    public static bool IsRealNumber(BinaryJsonNumber value)
    {
        return true;
    }

    /// <summary>
    /// Determine if a value is .
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is .</returns>
    public static bool IsSubnormal(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Double => double.IsNormal(ReadDouble(value.binaryData)),
            Kind.Half => Half.IsNormal(ReadHalf(value.binaryData)),
            Kind.Single => float.IsNormal(ReadSingle(value.binaryData)),
            _ => false,
        };
    }

    /// <summary>
    /// Determine if a value is Zero.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is Zero.</returns>
    public static bool IsZero(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Decimal => ReadDecimal(value.binaryData).Equals(decimal.Zero),
            Kind.Double => ReadDouble(value.binaryData).Equals(0),
            Kind.Half => ReadHalf(value.binaryData).Equals(Half.Zero),
            Kind.Int16 => ReadInt16(value.binaryData).Equals(0),
            Kind.Int32 => ReadInt32(value.binaryData).Equals(0),
            Kind.Int64 => ReadInt64(value.binaryData).Equals(0),
            Kind.Int128 => ReadInt128(value.binaryData).Equals(0),
            Kind.SByte => ReadSByte(value.binaryData).Equals(0),
            Kind.Single => ReadSingle(value.binaryData).Equals(0),
            _ => true,
        };
    }

    /// <summary>
    /// Compares two values to compute which is greater.
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns>The greater of the two values.</returns>
    /// <exception cref="NotSupportedException">The numeric format was not supported.</exception>
    public static BinaryJsonNumber MaxMagnitude(BinaryJsonNumber x, BinaryJsonNumber y)
    {
        if (x.numericKind == y.numericKind)
        {
            return x.numericKind switch
            {
                Kind.Byte => new(byte.Max(ReadByte(x.binaryData), ReadByte(y.binaryData))),
                Kind.Decimal => new(decimal.MaxMagnitude(ReadDecimal(x.binaryData), ReadDecimal(y.binaryData))),
                Kind.Double => new(double.MaxMagnitude(ReadDouble(x.binaryData), ReadDouble(y.binaryData))),
                Kind.Half => new(Half.MaxMagnitude(ReadHalf(x.binaryData), ReadHalf(y.binaryData))),
                Kind.Int16 => new(short.MaxMagnitude(ReadInt16(x.binaryData), ReadInt16(y.binaryData))),
                Kind.Int32 => new(int.MaxMagnitude(ReadInt32(x.binaryData), ReadInt32(y.binaryData))),
                Kind.Int64 => new(long.MaxMagnitude(ReadInt64(x.binaryData), ReadInt64(y.binaryData))),
                Kind.Int128 => new(Int128.MaxMagnitude(ReadInt128(x.binaryData), ReadInt128(y.binaryData))),
                Kind.SByte => new(sbyte.MaxMagnitude(ReadSByte(x.binaryData), ReadSByte(y.binaryData))),
                Kind.Single => new(float.MaxMagnitude(ReadSingle(x.binaryData), ReadSingle(y.binaryData))),
                Kind.UInt16 => new(ushort.Max(ReadUInt16(x.binaryData), ReadUInt16(y.binaryData))),
                Kind.UInt32 => new(uint.Max(ReadUInt32(x.binaryData), ReadUInt32(y.binaryData))),
                Kind.UInt64 => new(ulong.Max(ReadUInt64(x.binaryData), ReadUInt64(y.binaryData))),
                Kind.UInt128 => new(UInt128.Max(ReadUInt128(x.binaryData), ReadUInt128(y.binaryData))),
                _ => throw new NotSupportedException(),
            };
        }

        return y.numericKind switch
        {
            Kind.Byte => MaxMagnitude(x, ReadByte(y.binaryData)),
            Kind.Decimal => MaxMagnitude(x, ReadDecimal(y.binaryData)),
            Kind.Double => MaxMagnitude(x, ReadDouble(y.binaryData)),
            Kind.Half => MaxMagnitude(x, ReadHalf(y.binaryData)),
            Kind.Int16 => MaxMagnitude(x, ReadInt16(y.binaryData)),
            Kind.Int32 => MaxMagnitude(x, ReadInt32(y.binaryData)),
            Kind.Int64 => MaxMagnitude(x, ReadInt64(y.binaryData)),
            Kind.Int128 => MaxMagnitude(x, ReadInt128(y.binaryData)),
            Kind.SByte => MaxMagnitude(x, ReadSByte(y.binaryData)),
            Kind.Single => MaxMagnitude(x, ReadSingle(y.binaryData)),
            Kind.UInt16 => MaxMagnitude(x, ReadUInt16(y.binaryData)),
            Kind.UInt32 => MaxMagnitude(x, ReadUInt32(y.binaryData)),
            Kind.UInt64 => MaxMagnitude(x, ReadUInt64(y.binaryData)),
            Kind.UInt128 => MaxMagnitude(x, ReadUInt128(y.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Compares two values to compute which is greater. If one is NaN, it returns the other.
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns>The greater of the two values.</returns>
    /// <exception cref="NotSupportedException">The numeric format was not supported.</exception>
    public static BinaryJsonNumber MaxMagnitudeNumber(BinaryJsonNumber x, BinaryJsonNumber y)
    {
        if (x.numericKind == y.numericKind)
        {
            return x.numericKind switch
            {
                Kind.Byte => new(byte.Max(ReadByte(x.binaryData), ReadByte(y.binaryData))),
                Kind.Decimal => new(decimal.MaxMagnitude(ReadDecimal(x.binaryData), ReadDecimal(y.binaryData))),
                Kind.Double => new(double.MaxMagnitudeNumber(ReadDouble(x.binaryData), ReadDouble(y.binaryData))),
                Kind.Half => new(Half.MaxMagnitudeNumber(ReadHalf(x.binaryData), ReadHalf(y.binaryData))),
                Kind.Int16 => new(short.MaxMagnitude(ReadInt16(x.binaryData), ReadInt16(y.binaryData))),
                Kind.Int32 => new(int.MaxMagnitude(ReadInt32(x.binaryData), ReadInt32(y.binaryData))),
                Kind.Int64 => new(long.MaxMagnitude(ReadInt64(x.binaryData), ReadInt64(y.binaryData))),
                Kind.Int128 => new(Int128.MaxMagnitude(ReadInt128(x.binaryData), ReadInt128(y.binaryData))),
                Kind.SByte => new(sbyte.MaxMagnitude(ReadSByte(x.binaryData), ReadSByte(y.binaryData))),
                Kind.Single => new(float.MaxMagnitudeNumber(ReadSingle(x.binaryData), ReadSingle(y.binaryData))),
                Kind.UInt16 => new(ushort.Max(ReadUInt16(x.binaryData), ReadUInt16(y.binaryData))),
                Kind.UInt32 => new(uint.Max(ReadUInt32(x.binaryData), ReadUInt32(y.binaryData))),
                Kind.UInt64 => new(ulong.Max(ReadUInt64(x.binaryData), ReadUInt64(y.binaryData))),
                Kind.UInt128 => new(UInt128.Max(ReadUInt128(x.binaryData), ReadUInt128(y.binaryData))),
                _ => throw new NotSupportedException(),
            };
        }

        return y.numericKind switch
        {
            Kind.Byte => MaxMagnitudeNumber(x, ReadByte(y.binaryData)),
            Kind.Decimal => MaxMagnitudeNumber(x, ReadDecimal(y.binaryData)),
            Kind.Double => MaxMagnitudeNumber(x, ReadDouble(y.binaryData)),
            Kind.Half => MaxMagnitudeNumber(x, ReadHalf(y.binaryData)),
            Kind.Int16 => MaxMagnitudeNumber(x, ReadInt16(y.binaryData)),
            Kind.Int32 => MaxMagnitudeNumber(x, ReadInt32(y.binaryData)),
            Kind.Int64 => MaxMagnitudeNumber(x, ReadInt64(y.binaryData)),
            Kind.Int128 => MaxMagnitudeNumber(x, ReadInt128(y.binaryData)),
            Kind.SByte => MaxMagnitudeNumber(x, ReadSByte(y.binaryData)),
            Kind.Single => MaxMagnitudeNumber(x, ReadSingle(y.binaryData)),
            Kind.UInt16 => MaxMagnitudeNumber(x, ReadUInt16(y.binaryData)),
            Kind.UInt32 => MaxMagnitudeNumber(x, ReadUInt32(y.binaryData)),
            Kind.UInt64 => MaxMagnitudeNumber(x, ReadUInt64(y.binaryData)),
            Kind.UInt128 => MaxMagnitudeNumber(x, ReadUInt128(y.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Compares two values to compute which is lesser.
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns>The lesser of the two values.</returns>
    /// <exception cref="NotSupportedException">The numeric format was not supported.</exception>
    public static BinaryJsonNumber MinMagnitude(BinaryJsonNumber x, BinaryJsonNumber y)
    {
        if (x.numericKind == y.numericKind)
        {
            return x.numericKind switch
            {
                Kind.Byte => new(byte.Min(ReadByte(x.binaryData), ReadByte(y.binaryData))),
                Kind.Decimal => new(decimal.MinMagnitude(ReadDecimal(x.binaryData), ReadDecimal(y.binaryData))),
                Kind.Double => new(double.MinMagnitude(ReadDouble(x.binaryData), ReadDouble(y.binaryData))),
                Kind.Half => new(Half.MinMagnitude(ReadHalf(x.binaryData), ReadHalf(y.binaryData))),
                Kind.Int16 => new(short.MinMagnitude(ReadInt16(x.binaryData), ReadInt16(y.binaryData))),
                Kind.Int32 => new(int.MinMagnitude(ReadInt32(x.binaryData), ReadInt32(y.binaryData))),
                Kind.Int64 => new(long.MinMagnitude(ReadInt64(x.binaryData), ReadInt64(y.binaryData))),
                Kind.Int128 => new(Int128.MinMagnitude(ReadInt128(x.binaryData), ReadInt128(y.binaryData))),
                Kind.SByte => new(sbyte.MinMagnitude(ReadSByte(x.binaryData), ReadSByte(y.binaryData))),
                Kind.Single => new(float.MinMagnitude(ReadSingle(x.binaryData), ReadSingle(y.binaryData))),
                Kind.UInt16 => new(ushort.Min(ReadUInt16(x.binaryData), ReadUInt16(y.binaryData))),
                Kind.UInt32 => new(uint.Min(ReadUInt32(x.binaryData), ReadUInt32(y.binaryData))),
                Kind.UInt64 => new(ulong.Min(ReadUInt64(x.binaryData), ReadUInt64(y.binaryData))),
                Kind.UInt128 => new(UInt128.Min(ReadUInt128(x.binaryData), ReadUInt128(y.binaryData))),
                _ => throw new NotSupportedException(),
            };
        }

        return y.numericKind switch
        {
            Kind.Byte => MinMagnitude(x, ReadByte(y.binaryData)),
            Kind.Decimal => MinMagnitude(x, ReadDecimal(y.binaryData)),
            Kind.Double => MinMagnitude(x, ReadDouble(y.binaryData)),
            Kind.Half => MinMagnitude(x, ReadHalf(y.binaryData)),
            Kind.Int16 => MinMagnitude(x, ReadInt16(y.binaryData)),
            Kind.Int32 => MinMagnitude(x, ReadInt32(y.binaryData)),
            Kind.Int64 => MinMagnitude(x, ReadInt64(y.binaryData)),
            Kind.Int128 => MinMagnitude(x, ReadInt128(y.binaryData)),
            Kind.SByte => MinMagnitude(x, ReadSByte(y.binaryData)),
            Kind.Single => MinMagnitude(x, ReadSingle(y.binaryData)),
            Kind.UInt16 => MinMagnitude(x, ReadUInt16(y.binaryData)),
            Kind.UInt32 => MinMagnitude(x, ReadUInt32(y.binaryData)),
            Kind.UInt64 => MinMagnitude(x, ReadUInt64(y.binaryData)),
            Kind.UInt128 => MinMagnitude(x, ReadUInt128(y.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Compares two values to compute which is lesser. If one is NaN, it returns the other.
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns>The lesser of the two values.</returns>
    /// <exception cref="NotSupportedException">The numeric format was not supported.</exception>
    public static BinaryJsonNumber MinMagnitudeNumber(BinaryJsonNumber x, BinaryJsonNumber y)
    {
        if (x.numericKind == y.numericKind)
        {
            return x.numericKind switch
            {
                Kind.Byte => new(byte.Min(ReadByte(x.binaryData), ReadByte(y.binaryData))),
                Kind.Decimal => new(decimal.MinMagnitude(ReadDecimal(x.binaryData), ReadDecimal(y.binaryData))),
                Kind.Double => new(double.MinMagnitudeNumber(ReadDouble(x.binaryData), ReadDouble(y.binaryData))),
                Kind.Half => new(Half.MinMagnitudeNumber(ReadHalf(x.binaryData), ReadHalf(y.binaryData))),
                Kind.Int16 => new(short.MinMagnitude(ReadInt16(x.binaryData), ReadInt16(y.binaryData))),
                Kind.Int32 => new(int.MinMagnitude(ReadInt32(x.binaryData), ReadInt32(y.binaryData))),
                Kind.Int64 => new(long.MinMagnitude(ReadInt64(x.binaryData), ReadInt64(y.binaryData))),
                Kind.Int128 => new(Int128.MinMagnitude(ReadInt128(x.binaryData), ReadInt128(y.binaryData))),
                Kind.SByte => new(sbyte.MinMagnitude(ReadSByte(x.binaryData), ReadSByte(y.binaryData))),
                Kind.Single => new(float.MinMagnitudeNumber(ReadSingle(x.binaryData), ReadSingle(y.binaryData))),
                Kind.UInt16 => new(ushort.Min(ReadUInt16(x.binaryData), ReadUInt16(y.binaryData))),
                Kind.UInt32 => new(uint.Min(ReadUInt32(x.binaryData), ReadUInt32(y.binaryData))),
                Kind.UInt64 => new(ulong.Min(ReadUInt64(x.binaryData), ReadUInt64(y.binaryData))),
                Kind.UInt128 => new(UInt128.Min(ReadUInt128(x.binaryData), ReadUInt128(y.binaryData))),
                _ => throw new NotSupportedException(),
            };
        }

        return y.numericKind switch
        {
            Kind.Byte => MinMagnitudeNumber(x, ReadByte(y.binaryData)),
            Kind.Decimal => MinMagnitudeNumber(x, ReadDecimal(y.binaryData)),
            Kind.Double => MinMagnitudeNumber(x, ReadDouble(y.binaryData)),
            Kind.Half => MinMagnitudeNumber(x, ReadHalf(y.binaryData)),
            Kind.Int16 => MinMagnitudeNumber(x, ReadInt16(y.binaryData)),
            Kind.Int32 => MinMagnitudeNumber(x, ReadInt32(y.binaryData)),
            Kind.Int64 => MinMagnitudeNumber(x, ReadInt64(y.binaryData)),
            Kind.Int128 => MinMagnitudeNumber(x, ReadInt128(y.binaryData)),
            Kind.SByte => MinMagnitudeNumber(x, ReadSByte(y.binaryData)),
            Kind.Single => MinMagnitudeNumber(x, ReadSingle(y.binaryData)),
            Kind.UInt16 => MinMagnitudeNumber(x, ReadUInt16(y.binaryData)),
            Kind.UInt32 => MinMagnitudeNumber(x, ReadUInt32(y.binaryData)),
            Kind.UInt64 => MinMagnitudeNumber(x, ReadUInt64(y.binaryData)),
            Kind.UInt128 => MinMagnitudeNumber(x, ReadUInt128(y.binaryData)),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Parse a string into a <see cref="BinaryJsonNumber"/>.
    /// </summary>
    /// <param name="s">The value to parse.</param>
    /// <param name="style">The number styles to use.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns>An instance of a binary JSON number.</returns>
    /// <remarks>
    /// If you wish to control the underlying numeric kind of the <see cref="BinaryJsonNumber"/> then
    /// you should parse into that underlying type, and create a <see cref="BinaryJsonNumber"/> from the value.
    /// </remarks>
    public static BinaryJsonNumber Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider)
    {
        return new(double.Parse(s, style, provider));
    }

    /// <summary>
    /// Parse a string into a <see cref="BinaryJsonNumber"/>.
    /// </summary>
    /// <param name="s">The value to parse.</param>
    /// <param name="style">The number styles to use.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns>An instance of a binary JSON number.</returns>
    /// <remarks>
    /// If you wish to control the underlying numeric kind of the <see cref="BinaryJsonNumber"/> then
    /// you should parse into that underlying type, and create a <see cref="BinaryJsonNumber"/> from the value.
    /// </remarks>
    public static BinaryJsonNumber Parse(string s, NumberStyles style, IFormatProvider? provider)
    {
        return new(double.Parse(s, style, provider));
    }

    /// <summary>
    /// Try to parse a string into a <see cref="BinaryJsonNumber"/>.
    /// </summary>
    /// <param name="s">The value to parse.</param>
    /// <param name="style">The number styles to use.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="result">The parsed number.</param>
    /// <returns><see langword="true"/> if the value was parsed successfully.</returns>
    /// <remarks>
    /// If you wish to control the underlying numeric kind of the <see cref="BinaryJsonNumber"/> then
    /// you should parse into that underlying type, and create a <see cref="BinaryJsonNumber"/> from the value.
    /// </remarks>
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, [MaybeNullWhen(false)] out BinaryJsonNumber result)
    {
        if (double.TryParse(s, style, provider, out double d))
        {
            result = new(d);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Try to parse a string into a <see cref="BinaryJsonNumber"/>.
    /// </summary>
    /// <param name="s">The value to parse.</param>
    /// <param name="style">The number styles to use.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="result">The parsed number.</param>
    /// <returns><see langword="true"/> if the value was parsed successfully.</returns>
    /// <remarks>
    /// If you wish to control the underlying numeric kind of the <see cref="BinaryJsonNumber"/> then
    /// you should parse into that underlying type, and create a <see cref="BinaryJsonNumber"/> from the value.
    /// </remarks>
    public static bool TryParse([NotNullWhen(true)] string? s, NumberStyles style, IFormatProvider? provider, [MaybeNullWhen(false)] out BinaryJsonNumber result)
    {
        if (double.TryParse(s, style, provider, out double d))
        {
            result = new(d);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Parse a string into a <see cref="BinaryJsonNumber"/>.
    /// </summary>
    /// <param name="s">The value to parse.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns>An instance of a binary JSON number.</returns>
    /// <remarks>
    /// If you wish to control the underlying numeric kind of the <see cref="BinaryJsonNumber"/> then
    /// you should parse into that underlying type, and create a <see cref="BinaryJsonNumber"/> from the value.
    /// </remarks>
    public static BinaryJsonNumber Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        return new(double.Parse(s, provider));
    }

    /// <summary>
    /// Try to parse a string into a <see cref="BinaryJsonNumber"/>.
    /// </summary>
    /// <param name="s">The value to parse.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="result">The parsed number.</param>
    /// <returns><see langword="true"/> if the value was parsed successfully.</returns>
    /// <remarks>
    /// If you wish to control the underlying numeric kind of the <see cref="BinaryJsonNumber"/> then
    /// you should parse into that underlying type, and create a <see cref="BinaryJsonNumber"/> from the value.
    /// </remarks>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out BinaryJsonNumber result)
    {
        if (double.TryParse(s, provider, out double d))
        {
            result = new(d);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Parse a string into a <see cref="BinaryJsonNumber"/>.
    /// </summary>
    /// <param name="s">The value to parse.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns>An instance of a binary JSON number.</returns>
    /// <remarks>
    /// If you wish to control the underlying numeric kind of the <see cref="BinaryJsonNumber"/> then
    /// you should parse into that underlying type, and create a <see cref="BinaryJsonNumber"/> from the value.
    /// </remarks>
    public static BinaryJsonNumber Parse(string s, IFormatProvider? provider)
    {
        return new(double.Parse(s, provider));
    }

    /// <summary>
    /// Try to parse a string into a <see cref="BinaryJsonNumber"/>.
    /// </summary>
    /// <param name="s">The value to parse.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="result">The parsed number.</param>
    /// <returns><see langword="true"/> if the value was parsed successfully.</returns>
    /// <remarks>
    /// If you wish to control the underlying numeric kind of the <see cref="BinaryJsonNumber"/> then
    /// you should parse into that underlying type, and create a <see cref="BinaryJsonNumber"/> from the value.
    /// </remarks>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out BinaryJsonNumber result)
    {
        if (double.TryParse(s, provider, out double d))
        {
            result = new(d);
            return true;
        }

        result = default;
        return false;
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
            if (jsonNumberDouble < 0)
            {
                if (jsonElement.TryGetInt128WithFallbacks(out Int128 jsonNumberInt128))
                {
                    if (jsonNumberInt128 != (Int128)jsonNumberDouble)
                    {
                        // This should be an int128 rather than a double
                        return new BinaryJsonNumber(jsonNumberInt128);
                    }
                }

                return new BinaryJsonNumber(jsonNumberDouble);
            }
            else
            {
                if (jsonElement.TryGetUInt128WithFallbacks(out UInt128 jsonNumberUInt128))
                {
                    if (jsonNumberUInt128 != (UInt128)jsonNumberDouble)
                    {
                        // This should be an int128 rather than a double
                        return new BinaryJsonNumber(jsonNumberUInt128);
                    }
                }

                return new BinaryJsonNumber(jsonNumberDouble);
            }
        }

        // But if we can't, we'll go with a decimal
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
    /// Convert from the given value.
    /// </summary>
    /// <typeparam name="TOther">The type of the other value.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="result">The converted value.</param>
    /// <returns><see langword="true"/> if the value was converted successfully.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool INumberBase<BinaryJsonNumber>.TryConvertFromChecked<TOther>(TOther value, out BinaryJsonNumber result) => TryConvertFromChecked(value, out result);

    /// <summary>
    /// Convert from the given value.
    /// </summary>
    /// <typeparam name="TOther">The type of the other value.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="result">The converted value.</param>
    /// <returns><see langword="true"/> if the value was converted successfully.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool INumberBase<BinaryJsonNumber>.TryConvertFromSaturating<TOther>(TOther value, out BinaryJsonNumber result) => TryConvertFromSaturating(value, out result);

    /// <summary>
    /// Convert from the given value.
    /// </summary>
    /// <typeparam name="TOther">The type of the other value.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="result">The converted value.</param>
    /// <returns><see langword="true"/> if the value was converted successfully.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool INumberBase<BinaryJsonNumber>.TryConvertFromTruncating<TOther>(TOther value, out BinaryJsonNumber result) => TryConvertFromTruncating(value, out result);

    /// <summary>
    /// Convert to the given value.
    /// </summary>
    /// <typeparam name="TOther">The type of the other value.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="result">The converted value.</param>
    /// <returns><see langword="true"/> if the value was converted successfully.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool INumberBase<BinaryJsonNumber>.TryConvertToChecked<TOther>(BinaryJsonNumber value, [MaybeNullWhen(false)] out TOther result) => TryConvertToChecked(value, out result);

    /// <summary>
    /// Convert to the given value.
    /// </summary>
    /// <typeparam name="TOther">The type of the other value.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="result">The converted value.</param>
    /// <returns><see langword="true"/> if the value was converted successfully.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool INumberBase<BinaryJsonNumber>.TryConvertToSaturating<TOther>(BinaryJsonNumber value, [MaybeNullWhen(false)] out TOther result) => TryConvertToSaturating(value, out result);

    /// <summary>
    /// Convert to the given value.
    /// </summary>
    /// <typeparam name="TOther">The type of the other value.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="result">The converted value.</param>
    /// <returns><see langword="true"/> if the value was converted successfully.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool INumberBase<BinaryJsonNumber>.TryConvertToTruncating<TOther>(BinaryJsonNumber value, [MaybeNullWhen(false)] out TOther result) => TryConvertToTruncating(value, out result);

    /// <inheritdoc/>
    public bool Equals(BinaryJsonNumber other)
    {
        return Equals(this, other);
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
    private static BinaryJsonNumber Add<T>(in BinaryJsonNumber binaryNumber, T numberBase)
    where T : INumberBase<T>
    {
        if (PreciseConversionTo<double>.TryFrom(numberBase, out double numberBaseDouble))
        {
            if (binaryNumber.numericKind == Kind.Decimal)
            {
                decimal binaryNumberDecimal = ReadDecimal(binaryNumber.binaryData);

                if (PreciseConversionTo<decimal>.TryFrom(numberBase, out decimal numberBaseAsDecimal))
                {
                    return new(binaryNumberDecimal + numberBaseAsDecimal);
                }
                else
                {
                    return new(PreciseConversionTo<double>.From(binaryNumberDecimal) + numberBaseDouble);
                }
            }

            return binaryNumber.numericKind switch
            {
                Kind.Byte => new(numberBaseDouble + ReadByte(binaryNumber.binaryData)),
                Kind.Double => new(numberBaseDouble + ReadDouble(binaryNumber.binaryData)),
                Kind.Half => new(numberBaseDouble + (double)ReadHalf(binaryNumber.binaryData)),
                Kind.Int16 => new(numberBaseDouble + ReadInt16(binaryNumber.binaryData)),
                Kind.Int32 => new(numberBaseDouble + ReadInt32(binaryNumber.binaryData)),
                Kind.Int64 => new(numberBaseDouble + ReadInt64(binaryNumber.binaryData)),
                Kind.Int128 => new(numberBaseDouble + (double)ReadInt128(binaryNumber.binaryData)),
                Kind.SByte => new(numberBaseDouble + ReadSByte(binaryNumber.binaryData)),
                Kind.Single => new(numberBaseDouble + (double)ReadSingle(binaryNumber.binaryData)),
                Kind.UInt16 => new(numberBaseDouble + ReadUInt16(binaryNumber.binaryData)),
                Kind.UInt32 => new(numberBaseDouble + ReadUInt32(binaryNumber.binaryData)),
                Kind.UInt64 => new(numberBaseDouble + ReadUInt64(binaryNumber.binaryData)),
                Kind.UInt128 => new(numberBaseDouble + (double)ReadUInt128(binaryNumber.binaryData)),
                _ => throw new NotSupportedException(),
            };
        }

        // The decimal handling is special-cased above.
        throw new NotSupportedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BinaryJsonNumber Subtract<T>(in BinaryJsonNumber binaryNumber, T numberBase)
        where T : INumberBase<T>
    {
        if (PreciseConversionTo<double>.TryFrom(numberBase, out double numberBaseDouble))
        {
            if (binaryNumber.numericKind == Kind.Decimal)
            {
                decimal binaryNumberDecimal = ReadDecimal(binaryNumber.binaryData);

                if (PreciseConversionTo<decimal>.TryFrom(numberBase, out decimal numberBaseAsDecimal))
                {
                    return new(binaryNumberDecimal - numberBaseAsDecimal);
                }
                else
                {
                    return new(PreciseConversionTo<double>.From(binaryNumberDecimal) - numberBaseDouble);
                }
            }

            return binaryNumber.numericKind switch
            {
                Kind.Byte => new(ReadByte(binaryNumber.binaryData) - numberBaseDouble),
                Kind.Double => new(ReadDouble(binaryNumber.binaryData) - numberBaseDouble),
                Kind.Half => new((double)ReadHalf(binaryNumber.binaryData) - numberBaseDouble),
                Kind.Int16 => new(ReadInt16(binaryNumber.binaryData) - numberBaseDouble),
                Kind.Int32 => new(ReadInt32(binaryNumber.binaryData) - numberBaseDouble),
                Kind.Int64 => new(ReadInt64(binaryNumber.binaryData) - numberBaseDouble),
                Kind.Int128 => new((double)ReadInt128(binaryNumber.binaryData) - numberBaseDouble),
                Kind.SByte => new(ReadSByte(binaryNumber.binaryData) - numberBaseDouble),
                Kind.Single => new((double)ReadSingle(binaryNumber.binaryData) - numberBaseDouble),
                Kind.UInt16 => new(ReadUInt16(binaryNumber.binaryData) - numberBaseDouble),
                Kind.UInt32 => new(ReadUInt32(binaryNumber.binaryData) - numberBaseDouble),
                Kind.UInt64 => new(ReadUInt64(binaryNumber.binaryData) - numberBaseDouble),
                Kind.UInt128 => new((double)ReadUInt128(binaryNumber.binaryData) - numberBaseDouble),
                _ => throw new NotSupportedException(),
            };
        }

        // The decimal handling is special-cased above.
        throw new NotSupportedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BinaryJsonNumber Multiply<T>(in BinaryJsonNumber binaryNumber, T numberBase)
        where T : INumberBase<T>
    {
        if (PreciseConversionTo<double>.TryFrom(numberBase, out double numberBaseDouble))
        {
            if (binaryNumber.numericKind == Kind.Decimal)
            {
                decimal binaryNumberDecimal = ReadDecimal(binaryNumber.binaryData);

                if (PreciseConversionTo<decimal>.TryFrom(numberBase, out decimal numberBaseAsDecimal))
                {
                    return new(binaryNumberDecimal * numberBaseAsDecimal);
                }
                else
                {
                    return new(PreciseConversionTo<double>.From(binaryNumberDecimal) * numberBaseDouble);
                }
            }

            return binaryNumber.numericKind switch
            {
                Kind.Byte => new(ReadByte(binaryNumber.binaryData) * numberBaseDouble),
                Kind.Double => new(ReadDouble(binaryNumber.binaryData) * numberBaseDouble),
                Kind.Half => new((double)ReadHalf(binaryNumber.binaryData) * numberBaseDouble),
                Kind.Int16 => new(ReadInt16(binaryNumber.binaryData) * numberBaseDouble),
                Kind.Int32 => new(ReadInt32(binaryNumber.binaryData) * numberBaseDouble),
                Kind.Int64 => new(ReadInt64(binaryNumber.binaryData) * numberBaseDouble),
                Kind.Int128 => new((double)ReadInt128(binaryNumber.binaryData) * numberBaseDouble),
                Kind.SByte => new(ReadSByte(binaryNumber.binaryData) * numberBaseDouble),
                Kind.Single => new((double)ReadSingle(binaryNumber.binaryData) * numberBaseDouble),
                Kind.UInt16 => new(ReadUInt16(binaryNumber.binaryData) * numberBaseDouble),
                Kind.UInt32 => new(ReadUInt32(binaryNumber.binaryData) * numberBaseDouble),
                Kind.UInt64 => new(ReadUInt64(binaryNumber.binaryData) * numberBaseDouble),
                Kind.UInt128 => new((double)ReadUInt128(binaryNumber.binaryData) * numberBaseDouble),
                _ => throw new NotSupportedException(),
            };
        }

        // The decimal handling is special-cased above.
        throw new NotSupportedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BinaryJsonNumber Divide<T>(in BinaryJsonNumber binaryNumber, T numberBase)
        where T : INumberBase<T>
    {
        if (PreciseConversionTo<double>.TryFrom(numberBase, out double numberBaseDouble))
        {
            if (binaryNumber.numericKind == Kind.Decimal)
            {
                decimal binaryNumberDecimal = ReadDecimal(binaryNumber.binaryData);

                if (PreciseConversionTo<decimal>.TryFrom(numberBase, out decimal numberBaseAsDecimal))
                {
                    return new(binaryNumberDecimal / numberBaseAsDecimal);
                }
                else
                {
                    return new(PreciseConversionTo<double>.From(binaryNumberDecimal) / numberBaseDouble);
                }
            }

            return binaryNumber.numericKind switch
            {
                Kind.Byte => new(ReadByte(binaryNumber.binaryData) / numberBaseDouble),
                Kind.Double => new(ReadDouble(binaryNumber.binaryData) / numberBaseDouble),
                Kind.Half => new((double)ReadHalf(binaryNumber.binaryData) / numberBaseDouble),
                Kind.Int16 => new(ReadInt16(binaryNumber.binaryData) / numberBaseDouble),
                Kind.Int32 => new(ReadInt32(binaryNumber.binaryData) / numberBaseDouble),
                Kind.Int64 => new(ReadInt64(binaryNumber.binaryData) / numberBaseDouble),
                Kind.Int128 => new((double)ReadInt128(binaryNumber.binaryData) / numberBaseDouble),
                Kind.SByte => new(ReadSByte(binaryNumber.binaryData) / numberBaseDouble),
                Kind.Single => new((double)ReadSingle(binaryNumber.binaryData) / numberBaseDouble),
                Kind.UInt16 => new(ReadUInt16(binaryNumber.binaryData) / numberBaseDouble),
                Kind.UInt32 => new(ReadUInt32(binaryNumber.binaryData) / numberBaseDouble),
                Kind.UInt64 => new(ReadUInt64(binaryNumber.binaryData) / numberBaseDouble),
                Kind.UInt128 => new((double)ReadUInt128(binaryNumber.binaryData) / numberBaseDouble),
                _ => throw new NotSupportedException(),
            };
        }

        // The decimal handling is special-cased above.
        throw new NotSupportedException();
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
    private static BinaryJsonNumber MaxMagnitude<T>(in BinaryJsonNumber binaryNumber, T numberBase)
        where T : INumberBase<T>
    {
        if (PreciseConversionTo<double>.TryFrom(numberBase, out double numberBaseDouble))
        {
            if (binaryNumber.numericKind == Kind.Decimal)
            {
                decimal binaryNumberDecimal = ReadDecimal(binaryNumber.binaryData);

                if (PreciseConversionTo<decimal>.TryFrom(numberBase, out decimal numberBaseAsDecimal))
                {
                    return new(decimal.MaxMagnitude(binaryNumberDecimal, numberBaseAsDecimal));
                }
                else
                {
                    return new(double.MaxMagnitude(PreciseConversionTo<double>.From(binaryNumberDecimal), numberBaseDouble));
                }
            }

            return new(
                binaryNumber.numericKind switch
                {
                    Kind.Byte => double.MaxMagnitude(numberBaseDouble, ReadByte(binaryNumber.binaryData)),
                    Kind.Double => double.MaxMagnitude(numberBaseDouble, ReadDouble(binaryNumber.binaryData)),
                    Kind.Half => double.MaxMagnitude(numberBaseDouble, (double)ReadHalf(binaryNumber.binaryData)),
                    Kind.Int16 => double.MaxMagnitude(numberBaseDouble, ReadInt16(binaryNumber.binaryData)),
                    Kind.Int32 => double.MaxMagnitude(numberBaseDouble, ReadInt32(binaryNumber.binaryData)),
                    Kind.Int64 => double.MaxMagnitude(numberBaseDouble, ReadInt64(binaryNumber.binaryData)),
                    Kind.Int128 => double.MaxMagnitude(numberBaseDouble, (double)ReadInt128(binaryNumber.binaryData)),
                    Kind.SByte => double.MaxMagnitude(numberBaseDouble, ReadSByte(binaryNumber.binaryData)),
                    Kind.Single => double.MaxMagnitude(numberBaseDouble, (double)ReadSingle(binaryNumber.binaryData)),
                    Kind.UInt16 => double.MaxMagnitude(numberBaseDouble, ReadUInt16(binaryNumber.binaryData)),
                    Kind.UInt32 => double.MaxMagnitude(numberBaseDouble, ReadUInt32(binaryNumber.binaryData)),
                    Kind.UInt64 => double.MaxMagnitude(numberBaseDouble, ReadUInt64(binaryNumber.binaryData)),
                    Kind.UInt128 => double.MaxMagnitude(numberBaseDouble, (double)ReadUInt128(binaryNumber.binaryData)),
                    _ => throw new NotSupportedException(),
                });
        }

        // The decimal handling is special-cased above.
        throw new NotSupportedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BinaryJsonNumber MaxMagnitudeNumber<T>(in BinaryJsonNumber binaryNumber, T numberBase)
    where T : INumberBase<T>
    {
        if (PreciseConversionTo<double>.TryFrom(numberBase, out double numberBaseDouble))
        {
            if (binaryNumber.numericKind == Kind.Decimal)
            {
                decimal binaryNumberDecimal = ReadDecimal(binaryNumber.binaryData);

                if (PreciseConversionTo<decimal>.TryFrom(numberBase, out decimal numberBaseAsDecimal))
                {
                    return new(decimal.MaxMagnitude(binaryNumberDecimal, numberBaseAsDecimal));
                }
                else
                {
                    return new(double.MaxMagnitudeNumber(PreciseConversionTo<double>.From(binaryNumberDecimal), numberBaseDouble));
                }
            }

            return new(
                binaryNumber.numericKind switch
                {
                    Kind.Byte => double.MaxMagnitudeNumber(numberBaseDouble, ReadByte(binaryNumber.binaryData)),
                    Kind.Double => double.MaxMagnitudeNumber(numberBaseDouble, ReadDouble(binaryNumber.binaryData)),
                    Kind.Half => double.MaxMagnitudeNumber(numberBaseDouble, (double)ReadHalf(binaryNumber.binaryData)),
                    Kind.Int16 => double.MaxMagnitudeNumber(numberBaseDouble, ReadInt16(binaryNumber.binaryData)),
                    Kind.Int32 => double.MaxMagnitudeNumber(numberBaseDouble, ReadInt32(binaryNumber.binaryData)),
                    Kind.Int64 => double.MaxMagnitudeNumber(numberBaseDouble, ReadInt64(binaryNumber.binaryData)),
                    Kind.Int128 => double.MaxMagnitudeNumber(numberBaseDouble, (double)ReadInt128(binaryNumber.binaryData)),
                    Kind.SByte => double.MaxMagnitudeNumber(numberBaseDouble, ReadSByte(binaryNumber.binaryData)),
                    Kind.Single => double.MaxMagnitudeNumber(numberBaseDouble, (double)ReadSingle(binaryNumber.binaryData)),
                    Kind.UInt16 => double.MaxMagnitudeNumber(numberBaseDouble, ReadUInt16(binaryNumber.binaryData)),
                    Kind.UInt32 => double.MaxMagnitudeNumber(numberBaseDouble, ReadUInt32(binaryNumber.binaryData)),
                    Kind.UInt64 => double.MaxMagnitudeNumber(numberBaseDouble, ReadUInt64(binaryNumber.binaryData)),
                    Kind.UInt128 => double.MaxMagnitudeNumber(numberBaseDouble, (double)ReadUInt128(binaryNumber.binaryData)),
                    _ => throw new NotSupportedException(),
                });
        }

        // The decimal handling is special-cased above.
        throw new NotSupportedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BinaryJsonNumber MinMagnitude<T>(in BinaryJsonNumber binaryNumber, T numberBase)
    where T : INumberBase<T>
    {
        if (PreciseConversionTo<double>.TryFrom(numberBase, out double numberBaseDouble))
        {
            if (binaryNumber.numericKind == Kind.Decimal)
            {
                decimal binaryNumberDecimal = ReadDecimal(binaryNumber.binaryData);

                if (PreciseConversionTo<decimal>.TryFrom(numberBase, out decimal numberBaseAsDecimal))
                {
                    return new(decimal.MinMagnitude(binaryNumberDecimal, numberBaseAsDecimal));
                }
                else
                {
                    return new(double.MinMagnitude(PreciseConversionTo<double>.From(binaryNumberDecimal), numberBaseDouble));
                }
            }

            return new(
                binaryNumber.numericKind switch
                {
                    Kind.Byte => double.MinMagnitude(numberBaseDouble, ReadByte(binaryNumber.binaryData)),
                    Kind.Double => double.MinMagnitude(numberBaseDouble, ReadDouble(binaryNumber.binaryData)),
                    Kind.Half => double.MinMagnitude(numberBaseDouble, (double)ReadHalf(binaryNumber.binaryData)),
                    Kind.Int16 => double.MinMagnitude(numberBaseDouble, ReadInt16(binaryNumber.binaryData)),
                    Kind.Int32 => double.MinMagnitude(numberBaseDouble, ReadInt32(binaryNumber.binaryData)),
                    Kind.Int64 => double.MinMagnitude(numberBaseDouble, ReadInt64(binaryNumber.binaryData)),
                    Kind.Int128 => double.MinMagnitude(numberBaseDouble, (double)ReadInt128(binaryNumber.binaryData)),
                    Kind.SByte => double.MinMagnitude(numberBaseDouble, ReadSByte(binaryNumber.binaryData)),
                    Kind.Single => double.MinMagnitude(numberBaseDouble, (double)ReadSingle(binaryNumber.binaryData)),
                    Kind.UInt16 => double.MinMagnitude(numberBaseDouble, ReadUInt16(binaryNumber.binaryData)),
                    Kind.UInt32 => double.MinMagnitude(numberBaseDouble, ReadUInt32(binaryNumber.binaryData)),
                    Kind.UInt64 => double.MinMagnitude(numberBaseDouble, ReadUInt64(binaryNumber.binaryData)),
                    Kind.UInt128 => double.MinMagnitude(numberBaseDouble, (double)ReadUInt128(binaryNumber.binaryData)),
                    _ => throw new NotSupportedException(),
                });
        }

        // The decimal handling is special-cased above.
        throw new NotSupportedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BinaryJsonNumber MinMagnitudeNumber<T>(in BinaryJsonNumber binaryNumber, T numberBase)
    where T : INumberBase<T>
    {
        if (PreciseConversionTo<double>.TryFrom(numberBase, out double numberBaseDouble))
        {
            if (binaryNumber.numericKind == Kind.Decimal)
            {
                decimal binaryNumberDecimal = ReadDecimal(binaryNumber.binaryData);

                if (PreciseConversionTo<decimal>.TryFrom(numberBase, out decimal numberBaseAsDecimal))
                {
                    return new(decimal.MinMagnitude(binaryNumberDecimal, numberBaseAsDecimal));
                }
                else
                {
                    return new(double.MinMagnitudeNumber(PreciseConversionTo<double>.From(binaryNumberDecimal), numberBaseDouble));
                }
            }

            return new(
                binaryNumber.numericKind switch
                {
                    Kind.Byte => double.MinMagnitudeNumber(numberBaseDouble, ReadByte(binaryNumber.binaryData)),
                    Kind.Double => double.MinMagnitudeNumber(numberBaseDouble, ReadDouble(binaryNumber.binaryData)),
                    Kind.Half => double.MinMagnitudeNumber(numberBaseDouble, (double)ReadHalf(binaryNumber.binaryData)),
                    Kind.Int16 => double.MinMagnitudeNumber(numberBaseDouble, ReadInt16(binaryNumber.binaryData)),
                    Kind.Int32 => double.MinMagnitudeNumber(numberBaseDouble, ReadInt32(binaryNumber.binaryData)),
                    Kind.Int64 => double.MinMagnitudeNumber(numberBaseDouble, ReadInt64(binaryNumber.binaryData)),
                    Kind.Int128 => double.MinMagnitudeNumber(numberBaseDouble, (double)ReadInt128(binaryNumber.binaryData)),
                    Kind.SByte => double.MinMagnitudeNumber(numberBaseDouble, ReadSByte(binaryNumber.binaryData)),
                    Kind.Single => double.MinMagnitudeNumber(numberBaseDouble, (double)ReadSingle(binaryNumber.binaryData)),
                    Kind.UInt16 => double.MinMagnitudeNumber(numberBaseDouble, ReadUInt16(binaryNumber.binaryData)),
                    Kind.UInt32 => double.MinMagnitudeNumber(numberBaseDouble, ReadUInt32(binaryNumber.binaryData)),
                    Kind.UInt64 => double.MinMagnitudeNumber(numberBaseDouble, ReadUInt64(binaryNumber.binaryData)),
                    Kind.UInt128 => double.MinMagnitudeNumber(numberBaseDouble, (double)ReadUInt128(binaryNumber.binaryData)),
                    _ => throw new NotSupportedException(),
                });
        }

        // The decimal handling is special-cased above.
        throw new NotSupportedException();
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertFromChecked<TOther>(TOther value, out BinaryJsonNumber result)
           where TOther : INumberBase<TOther>
    {
        if (typeof(TOther) == typeof(byte))
        {
            byte actualValue = (byte)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(sbyte))
        {
            sbyte actualValue = (sbyte)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(char))
        {
            char actualValue = (char)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(double))
        {
            double actualValue = (double)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(decimal))
        {
            decimal actualValue = (decimal)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(ushort))
        {
            ushort actualValue = (ushort)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(uint))
        {
            uint actualValue = (uint)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(ulong))
        {
            ulong actualValue = (ulong)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(nuint))
        {
            nuint actualValue = (nuint)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(Half))
        {
            var actualValue = (Half)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(Int128))
        {
            var actualValue = (Int128)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(UInt128))
        {
            var actualValue = (UInt128)(object)value;
            result = new(actualValue);
            return true;
        }
        else
        {
            result = default;
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertFromSaturating<TOther>(TOther value, out BinaryJsonNumber result)
       where TOther : INumberBase<TOther>
    {
        if (typeof(TOther) == typeof(byte))
        {
            byte actualValue = (byte)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(sbyte))
        {
            sbyte actualValue = (sbyte)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(char))
        {
            char actualValue = (char)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(double))
        {
            double actualValue = (double)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(decimal))
        {
            decimal actualValue = (decimal)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(ushort))
        {
            ushort actualValue = (ushort)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(uint))
        {
            uint actualValue = (uint)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(ulong))
        {
            ulong actualValue = (ulong)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(nuint))
        {
            nuint actualValue = (nuint)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(Half))
        {
            var actualValue = (Half)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(Int128))
        {
            var actualValue = (Int128)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(UInt128))
        {
            var actualValue = (UInt128)(object)value;
            result = new(actualValue);
            return true;
        }
        else
        {
            result = default;
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertFromTruncating<TOther>(TOther value, out BinaryJsonNumber result)
        where TOther : INumberBase<TOther>
    {
        if (typeof(TOther) == typeof(byte))
        {
            byte actualValue = (byte)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(sbyte))
        {
            sbyte actualValue = (sbyte)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(char))
        {
            char actualValue = (char)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(double))
        {
            double actualValue = (double)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(decimal))
        {
            decimal actualValue = (decimal)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(ushort))
        {
            ushort actualValue = (ushort)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(uint))
        {
            uint actualValue = (uint)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(ulong))
        {
            ulong actualValue = (ulong)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(nuint))
        {
            nuint actualValue = (nuint)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(Half))
        {
            var actualValue = (Half)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(Int128))
        {
            var actualValue = (Int128)(object)value;
            result = new(actualValue);
            return true;
        }
        else if (typeof(TOther) == typeof(UInt128))
        {
            var actualValue = (UInt128)(object)value;
            result = new(actualValue);
            return true;
        }
        else
        {
            result = default;
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertToChecked<TOther>(BinaryJsonNumber value, [MaybeNullWhen(false)] out TOther result)
    where TOther : INumberBase<TOther>
    {
        return value.numericKind switch
        {
            Kind.Byte => TOther.TryConvertFromChecked(ReadByte(value.binaryData), out result),
            Kind.Decimal => TOther.TryConvertFromChecked(ReadDecimal(value.binaryData), out result),
            Kind.Double => TOther.TryConvertFromChecked(ReadDouble(value.binaryData), out result),
            Kind.Half => TOther.TryConvertFromChecked(ReadHalf(value.binaryData), out result),
            Kind.Int16 => TOther.TryConvertFromChecked(ReadInt16(value.binaryData), out result),
            Kind.Int32 => TOther.TryConvertFromChecked(ReadInt32(value.binaryData), out result),
            Kind.Int64 => TOther.TryConvertFromChecked(ReadInt64(value.binaryData), out result),
            Kind.Int128 => TOther.TryConvertFromChecked(ReadInt128(value.binaryData), out result),
            Kind.SByte => TOther.TryConvertFromChecked(ReadSByte(value.binaryData), out result),
            Kind.Single => TOther.TryConvertFromChecked(ReadSingle(value.binaryData), out result),
            Kind.UInt16 => TOther.TryConvertFromChecked(ReadUInt16(value.binaryData), out result),
            Kind.UInt32 => TOther.TryConvertFromChecked(ReadUInt32(value.binaryData), out result),
            Kind.UInt64 => TOther.TryConvertFromChecked(ReadUInt64(value.binaryData), out result),
            Kind.UInt128 => TOther.TryConvertFromChecked(ReadUInt128(value.binaryData), out result),
            _ => throw new NotSupportedException(),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertToTruncating<TOther>(BinaryJsonNumber value, [MaybeNullWhen(false)] out TOther result)
        where TOther : INumberBase<TOther>
    {
        return value.numericKind switch
        {
            Kind.Byte => TOther.TryConvertFromTruncating(ReadByte(value.binaryData), out result),
            Kind.Decimal => TOther.TryConvertFromTruncating(ReadDecimal(value.binaryData), out result),
            Kind.Double => TOther.TryConvertFromTruncating(ReadDouble(value.binaryData), out result),
            Kind.Half => TOther.TryConvertFromTruncating(ReadHalf(value.binaryData), out result),
            Kind.Int16 => TOther.TryConvertFromTruncating(ReadInt16(value.binaryData), out result),
            Kind.Int32 => TOther.TryConvertFromTruncating(ReadInt32(value.binaryData), out result),
            Kind.Int64 => TOther.TryConvertFromTruncating(ReadInt64(value.binaryData), out result),
            Kind.Int128 => TOther.TryConvertFromTruncating(ReadInt128(value.binaryData), out result),
            Kind.SByte => TOther.TryConvertFromTruncating(ReadSByte(value.binaryData), out result),
            Kind.Single => TOther.TryConvertFromTruncating(ReadSingle(value.binaryData), out result),
            Kind.UInt16 => TOther.TryConvertFromTruncating(ReadUInt16(value.binaryData), out result),
            Kind.UInt32 => TOther.TryConvertFromTruncating(ReadUInt32(value.binaryData), out result),
            Kind.UInt64 => TOther.TryConvertFromTruncating(ReadUInt64(value.binaryData), out result),
            Kind.UInt128 => TOther.TryConvertFromTruncating(ReadUInt128(value.binaryData), out result),
            _ => throw new NotSupportedException(),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertToSaturating<TOther>(BinaryJsonNumber value, [MaybeNullWhen(false)] out TOther result)
        where TOther : INumberBase<TOther>
    {
        return value.numericKind switch
        {
            Kind.Byte => TOther.TryConvertFromSaturating(ReadByte(value.binaryData), out result),
            Kind.Decimal => TOther.TryConvertFromSaturating(ReadDecimal(value.binaryData), out result),
            Kind.Double => TOther.TryConvertFromSaturating(ReadDouble(value.binaryData), out result),
            Kind.Half => TOther.TryConvertFromSaturating(ReadHalf(value.binaryData), out result),
            Kind.Int16 => TOther.TryConvertFromSaturating(ReadInt16(value.binaryData), out result),
            Kind.Int32 => TOther.TryConvertFromSaturating(ReadInt32(value.binaryData), out result),
            Kind.Int64 => TOther.TryConvertFromSaturating(ReadInt64(value.binaryData), out result),
            Kind.Int128 => TOther.TryConvertFromSaturating(ReadInt128(value.binaryData), out result),
            Kind.SByte => TOther.TryConvertFromSaturating(ReadSByte(value.binaryData), out result),
            Kind.Single => TOther.TryConvertFromSaturating(ReadSingle(value.binaryData), out result),
            Kind.UInt16 => TOther.TryConvertFromSaturating(ReadUInt16(value.binaryData), out result),
            Kind.UInt32 => TOther.TryConvertFromSaturating(ReadUInt32(value.binaryData), out result),
            Kind.UInt64 => TOther.TryConvertFromSaturating(ReadUInt64(value.binaryData), out result),
            Kind.UInt128 => TOther.TryConvertFromSaturating(ReadUInt128(value.binaryData), out result),
            _ => throw new NotSupportedException(),
        };
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
#else
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
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
    IEquatable<BinaryJsonNumber>
{
    private readonly long longBacking;
    private readonly ulong ulongBacking;
    private readonly decimal decimalBacking;
    private readonly double doubleBacking;
    private readonly float singleBacking;

    private readonly Kind numericKind;

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(byte value)
    {
        this.ulongBacking = value;
        this.numericKind = Kind.Byte;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(decimal value)
    {
        this.decimalBacking = value;
        this.numericKind = Kind.Decimal;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(double value)
    {
        this.doubleBacking = value;
        this.numericKind = Kind.Double;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(short value)
    {
        this.longBacking = value;
        this.numericKind = Kind.Int16;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(int value)
    {
        this.longBacking = value;
        this.numericKind = Kind.Int32;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(long value)
    {
        this.longBacking = value;
        this.numericKind = Kind.Int64;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(sbyte value)
    {
        this.longBacking = value;
        this.numericKind = Kind.SByte;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(float value)
    {
        this.singleBacking = value;
        this.numericKind = Kind.Single;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(ushort value)
    {
        this.ulongBacking = value;
        this.numericKind = Kind.UInt16;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(uint value)
    {
        this.ulongBacking = value;
        this.numericKind = Kind.UInt32;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(ulong value)
    {
        this.ulongBacking = value;
        this.numericKind = Kind.UInt64;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryJsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to initialize the number.</param>
    public BinaryJsonNumber(bool value)
    {
        this.longBacking = value ? 1 : 0;
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
        /// Represents a  <see cref="bool"/>.
        /// </summary>
        Bool = 0b0100_0000_0000_0000,
    }

    /// <summary>
    /// Gets an instance of <see cref="BinaryJsonNumber"/> whose numeric kind is <see cref="Kind.None"/>.
    /// </summary>
    public static BinaryJsonNumber None { get; } = default;

    /// <summary>
    /// Gets the BinaryJsonNumber for 1.
    /// </summary>
    public static BinaryJsonNumber One { get; } = new BinaryJsonNumber(1);

    /// <summary>
    /// Gets the Radix for the BinaryJsonNumber (2).
    /// </summary>
    public static int Radix { get; } = 2;

    /// <summary>
    /// Gets the BinaryJsonNumber for 0.
    /// </summary>
    public static BinaryJsonNumber Zero { get; } = new BinaryJsonNumber(0);

    /// <summary>
    /// Gets the BinaryJsonNumber for 0.
    /// </summary>
    public static BinaryJsonNumber AdditiveIdentity => Zero;

    /// <summary>
    /// Gets the BinaryJsonNumber for 1.
    /// </summary>
    public static BinaryJsonNumber MultiplicativeIdentity => One;

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
    /// Addition operator.
    /// </summary>
    /// <param name="left">The left hand side of the addition.</param>
    /// <param name="right">The right hand side of the addition.</param>
    /// <returns>The result of the addition.</returns>
    /// <exception cref="NotSupportedException">The numeric format is not supported.</exception>
    public static BinaryJsonNumber operator +(BinaryJsonNumber left, BinaryJsonNumber right)
    {
        if (left.numericKind == right.numericKind)
        {
            return left.numericKind switch
            {
                Kind.Byte => new(((byte)left.ulongBacking) + ((byte)right.ulongBacking)),
                Kind.Decimal => new(left.decimalBacking + right.decimalBacking),
                Kind.Double => new(left.doubleBacking + right.doubleBacking),
                Kind.Int16 => new(((short)left.longBacking) + ((short)right.longBacking)),
                Kind.Int32 => new(((int)left.longBacking) + ((int)right.longBacking)),
                Kind.Int64 => new(left.longBacking + right.longBacking),
                Kind.SByte => new(((sbyte)left.longBacking) + ((sbyte)right.longBacking)),
                Kind.Single => new(left.singleBacking + right.singleBacking),
                Kind.UInt16 => new(((ushort)left.ulongBacking) + ((ushort)right.ulongBacking)),
                Kind.UInt32 => new(((uint)left.ulongBacking) + ((uint)right.ulongBacking)),
                Kind.UInt64 => new(left.ulongBacking + right.ulongBacking),
                _ => throw new NotSupportedException(),
            };
        }

        return right.numericKind switch
        {
            Kind.Byte => new BinaryJsonNumber((byte)(left.GetDouble() + right.GetDouble())),
            Kind.Decimal => new BinaryJsonNumber((decimal)left.GetDouble() + right.decimalBacking),
            Kind.Double => new BinaryJsonNumber(left.GetDouble() + right.doubleBacking),
            Kind.Int16 => new BinaryJsonNumber((short)(left.GetDouble() + right.GetDouble())),
            Kind.Int32 => new BinaryJsonNumber((int)(left.GetDouble() + right.GetDouble())),
            Kind.Int64 => new BinaryJsonNumber((long)(left.GetDouble() + right.GetDouble())),
            Kind.SByte => new BinaryJsonNumber((sbyte)(left.GetDouble() + right.GetDouble())),
            Kind.Single => new BinaryJsonNumber((float)(left.GetDouble() + right.GetDouble())),
            Kind.UInt16 => new BinaryJsonNumber((ushort)(left.GetDouble() + right.GetDouble())),
            Kind.UInt32 => new BinaryJsonNumber((uint)(left.GetDouble() + right.GetDouble())),
            Kind.UInt64 => new BinaryJsonNumber((ulong)(left.GetDouble() + right.GetDouble())),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Subtraction operator.
    /// </summary>
    /// <param name="left">The left hand side of the subtraction.</param>
    /// <param name="right">The right hand side of the subtraction.</param>
    /// <returns>The result of the subtraction.</returns>
    /// <exception cref="NotSupportedException">The numeric format is not supported.</exception>
    public static BinaryJsonNumber operator -(BinaryJsonNumber left, BinaryJsonNumber right)
    {
        if (left.numericKind == right.numericKind)
        {
            return left.numericKind switch
            {
                Kind.Byte => new(((byte)left.ulongBacking) - ((byte)right.ulongBacking)),
                Kind.Decimal => new(left.decimalBacking - right.decimalBacking),
                Kind.Double => new(left.doubleBacking - right.doubleBacking),
                Kind.Int16 => new(((short)left.longBacking) - ((short)right.longBacking)),
                Kind.Int32 => new(((int)left.longBacking) - ((int)right.longBacking)),
                Kind.Int64 => new(left.longBacking - right.longBacking),
                Kind.SByte => new(((sbyte)left.longBacking) - ((sbyte)right.longBacking)),
                Kind.Single => new(left.singleBacking - right.singleBacking),
                Kind.UInt16 => new(((ushort)left.ulongBacking) - ((ushort)right.ulongBacking)),
                Kind.UInt32 => new(((uint)left.ulongBacking) - ((uint)right.ulongBacking)),
                Kind.UInt64 => new(left.ulongBacking - right.ulongBacking),
                _ => throw new NotSupportedException(),
            };
        }

        return right.numericKind switch
        {
            Kind.Byte => new BinaryJsonNumber((byte)(left.GetDouble() - right.GetDouble())),
            Kind.Decimal => new BinaryJsonNumber((decimal)left.GetDouble() - right.decimalBacking),
            Kind.Double => new BinaryJsonNumber(left.GetDouble() - right.doubleBacking),
            Kind.Int16 => new BinaryJsonNumber((short)(left.GetDouble() - right.GetDouble())),
            Kind.Int32 => new BinaryJsonNumber((int)(left.GetDouble() - right.GetDouble())),
            Kind.Int64 => new BinaryJsonNumber((long)(left.GetDouble() - right.GetDouble())),
            Kind.SByte => new BinaryJsonNumber((sbyte)(left.GetDouble() - right.GetDouble())),
            Kind.Single => new BinaryJsonNumber((float)(left.GetDouble() - right.GetDouble())),
            Kind.UInt16 => new BinaryJsonNumber((ushort)(left.GetDouble() - right.GetDouble())),
            Kind.UInt32 => new BinaryJsonNumber((uint)(left.GetDouble() - right.GetDouble())),
            Kind.UInt64 => new BinaryJsonNumber((ulong)(left.GetDouble() - right.GetDouble())),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Multiplication operator.
    /// </summary>
    /// <param name="left">The left hand side of the multiplication.</param>
    /// <param name="right">The right hand side of the multiplication.</param>
    /// <returns>The result of the multiplication.</returns>
    /// <exception cref="NotSupportedException">The numeric format is not supported.</exception>
    public static BinaryJsonNumber operator *(BinaryJsonNumber left, BinaryJsonNumber right)
    {
        if (left.numericKind == right.numericKind)
        {
            return left.numericKind switch
            {
                Kind.Byte => new(((byte)left.ulongBacking) * ((byte)right.ulongBacking)),
                Kind.Decimal => new(left.decimalBacking * right.decimalBacking),
                Kind.Double => new(left.doubleBacking * right.doubleBacking),
                Kind.Int16 => new(((short)left.longBacking) * ((short)right.longBacking)),
                Kind.Int32 => new(((int)left.longBacking) * ((int)right.longBacking)),
                Kind.Int64 => new(left.longBacking * right.longBacking),
                Kind.SByte => new(((sbyte)left.longBacking) * ((sbyte)right.longBacking)),
                Kind.Single => new(left.singleBacking * right.singleBacking),
                Kind.UInt16 => new(((ushort)left.ulongBacking) * ((ushort)right.ulongBacking)),
                Kind.UInt32 => new(((uint)left.ulongBacking) * ((uint)right.ulongBacking)),
                Kind.UInt64 => new(left.ulongBacking * right.ulongBacking),
                _ => throw new NotSupportedException(),
            };
        }

        return right.numericKind switch
        {
            Kind.Byte => new BinaryJsonNumber((byte)(left.GetDouble() * right.GetDouble())),
            Kind.Decimal => new BinaryJsonNumber((decimal)left.GetDouble() * right.decimalBacking),
            Kind.Double => new BinaryJsonNumber(left.GetDouble() * right.doubleBacking),
            Kind.Int16 => new BinaryJsonNumber((short)(left.GetDouble() * right.GetDouble())),
            Kind.Int32 => new BinaryJsonNumber((int)(left.GetDouble() * right.GetDouble())),
            Kind.Int64 => new BinaryJsonNumber((long)(left.GetDouble() * right.GetDouble())),
            Kind.SByte => new BinaryJsonNumber((sbyte)(left.GetDouble() * right.GetDouble())),
            Kind.Single => new BinaryJsonNumber((float)(left.GetDouble() * right.GetDouble())),
            Kind.UInt16 => new BinaryJsonNumber((ushort)(left.GetDouble() * right.GetDouble())),
            Kind.UInt32 => new BinaryJsonNumber((uint)(left.GetDouble() * right.GetDouble())),
            Kind.UInt64 => new BinaryJsonNumber((ulong)(left.GetDouble() * right.GetDouble())),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Division operator.
    /// </summary>
    /// <param name="left">The left hand side of the division.</param>
    /// <param name="right">The right hand side of the division.</param>
    /// <returns>The result of the division.</returns>
    /// <exception cref="NotSupportedException">The numeric format is not supported.</exception>
    public static BinaryJsonNumber operator /(BinaryJsonNumber left, BinaryJsonNumber right)
    {
        if (left.numericKind == right.numericKind)
        {
            return left.numericKind switch
            {
                Kind.Byte => new(((byte)left.ulongBacking) / ((byte)right.ulongBacking)),
                Kind.Decimal => new(left.decimalBacking / right.decimalBacking),
                Kind.Double => new(left.doubleBacking / right.doubleBacking),
                Kind.Int16 => new(((short)left.longBacking) / ((short)right.longBacking)),
                Kind.Int32 => new(((int)left.longBacking) / ((int)right.longBacking)),
                Kind.Int64 => new(left.longBacking / right.longBacking),
                Kind.SByte => new(((sbyte)left.longBacking) / ((sbyte)right.longBacking)),
                Kind.Single => new(left.singleBacking / right.singleBacking),
                Kind.UInt16 => new(((ushort)left.ulongBacking) / ((ushort)right.ulongBacking)),
                Kind.UInt32 => new(((uint)left.ulongBacking) / ((uint)right.ulongBacking)),
                Kind.UInt64 => new(left.ulongBacking / right.ulongBacking),
                _ => throw new NotSupportedException(),
            };
        }

        return right.numericKind switch
        {
            Kind.Byte => new BinaryJsonNumber((byte)(left.GetDouble() / right.GetDouble())),
            Kind.Decimal => new BinaryJsonNumber((decimal)left.GetDouble() / right.decimalBacking),
            Kind.Double => new BinaryJsonNumber(left.GetDouble() / right.doubleBacking),
            Kind.Int16 => new BinaryJsonNumber((short)(left.GetDouble() / right.GetDouble())),
            Kind.Int32 => new BinaryJsonNumber((int)(left.GetDouble() / right.GetDouble())),
            Kind.Int64 => new BinaryJsonNumber((long)(left.GetDouble() / right.GetDouble())),
            Kind.SByte => new BinaryJsonNumber((sbyte)(left.GetDouble() / right.GetDouble())),
            Kind.Single => new BinaryJsonNumber((float)(left.GetDouble() / right.GetDouble())),
            Kind.UInt16 => new BinaryJsonNumber((ushort)(left.GetDouble() / right.GetDouble())),
            Kind.UInt32 => new BinaryJsonNumber((uint)(left.GetDouble() / right.GetDouble())),
            Kind.UInt64 => new BinaryJsonNumber((ulong)(left.GetDouble() / right.GetDouble())),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Decrement operator.
    /// </summary>
    /// <param name="value">The number to decrement.</param>
    /// <returns>The decremented number.</returns>
    /// <exception cref="NotSupportedException">The numeric format was not supported.</exception>
    public static BinaryJsonNumber operator --(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Byte => new(((byte)value.ulongBacking) - 1),
            Kind.Decimal => new(value.decimalBacking - 1m),
            Kind.Double => new(value.doubleBacking - 1),
            Kind.Int16 => new(((short)value.longBacking) - 1),
            Kind.Int32 => new(((int)value.longBacking) - 1),
            Kind.Int64 => new(value.longBacking - 1),
            Kind.SByte => new(((sbyte)value.longBacking) - 1),
            Kind.Single => new(value.singleBacking - 1f),
            Kind.UInt16 => new(((ushort)value.ulongBacking) - 1),
            Kind.UInt32 => new(((uint)value.ulongBacking) - 1),
            Kind.UInt64 => new(value.ulongBacking - 1),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// The equality operator.
    /// </summary>
    /// <param name="left">The lhs.</param>
    /// <param name="right">The rhs.</param>
    /// <returns><see langword="true"/> if the values are equal.</returns>
    public static bool operator ==(BinaryJsonNumber left, BinaryJsonNumber right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// The inequality operator.
    /// </summary>
    /// <param name="left">The lhs.</param>
    /// <param name="right">The rhs.</param>
    /// <returns><see langword="true"/> if the the values are not equal.</returns>
    public static bool operator !=(BinaryJsonNumber left, BinaryJsonNumber right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Increment operator.
    /// </summary>
    /// <param name="value">The value to increment.</param>
    /// <returns>The incremented value.</returns>
    /// <exception cref="NotSupportedException">The numeric format was not supported.</exception>
    public static BinaryJsonNumber operator ++(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Byte => new(((byte)value.ulongBacking) + 1),
            Kind.Decimal => new(value.decimalBacking + 1m),
            Kind.Double => new(value.doubleBacking + 1),
            Kind.Int16 => new(((short)value.longBacking) + 1),
            Kind.Int32 => new(((int)value.longBacking) + 1),
            Kind.Int64 => new(value.longBacking + 1),
            Kind.SByte => new(((sbyte)value.longBacking) + 1),
            Kind.Single => new(value.singleBacking + 1f),
            Kind.UInt16 => new(((ushort)value.ulongBacking) + 1),
            Kind.UInt32 => new(((uint)value.ulongBacking) + 1),
            Kind.UInt64 => new(value.ulongBacking + 1),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Negation operator.
    /// </summary>
    /// <param name="value">The value on which to operate.</param>
    /// <returns>The negated value.</returns>
    /// <exception cref="NotSupportedException">The numeric format was not supported.</exception>
    public static BinaryJsonNumber operator -(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Byte => new(-(byte)value.ulongBacking),
            Kind.Decimal => new(-value.decimalBacking),
            Kind.Double => new(-value.doubleBacking),
            Kind.Int16 => new(-(short)value.longBacking),
            Kind.Int32 => new(-(int)value.longBacking),
            Kind.Int64 => new(-value.longBacking),
            Kind.SByte => new(-(sbyte)value.longBacking),
            Kind.Single => new(-value.singleBacking),
            Kind.UInt16 => new(-(ushort)value.ulongBacking),
            Kind.UInt32 => new(-(uint)value.ulongBacking),
            Kind.UInt64 => value,
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Unary plus operator.
    /// </summary>
    /// <param name="value">The value on which to operate.</param>
    /// <returns>The unary plus value.</returns>
    /// <exception cref="NotSupportedException">The numeric format was not supported.</exception>
    public static BinaryJsonNumber operator +(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Byte => new(+(byte)value.ulongBacking),
            Kind.Decimal => new(+value.decimalBacking),
            Kind.Double => new(+value.doubleBacking),
            Kind.Int16 => new(+(short)value.longBacking),
            Kind.Int32 => new(+(int)value.longBacking),
            Kind.Int64 => new(+value.longBacking),
            Kind.SByte => new(+(sbyte)value.longBacking),
            Kind.Single => new(+value.singleBacking),
            Kind.UInt16 => new(+(ushort)value.ulongBacking),
            Kind.UInt32 => new(+(uint)value.ulongBacking),
            Kind.UInt64 => new(+value.ulongBacking),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Computes the absolute of a value.
    /// </summary>
    /// <param name="value">The value for which to calculate the absolute.</param>
    /// <returns>The absolute value.</returns>
    /// <exception cref="NotSupportedException">The numeric format was not supported.</exception>
    public static BinaryJsonNumber Abs(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Byte => value,
            Kind.Decimal => new(Math.Abs(value.decimalBacking)),
            Kind.Double => new(Math.Abs(value.doubleBacking)),
            Kind.Int16 => new(Math.Abs((short)value.longBacking)),
            Kind.Int32 => new(Math.Abs((int)value.longBacking)),
            Kind.Int64 => new(Math.Abs(value.longBacking)),
            Kind.SByte => new(Math.Abs((sbyte)value.longBacking)),
            Kind.Single => new(Math.Abs(value.singleBacking)),
            Kind.UInt16 => value,
            Kind.UInt32 => value,
            Kind.UInt64 => value,
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Determine if a value is infinity.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is infinity.</returns>
    public static bool IsInfinity(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Double => double.IsInfinity(value.doubleBacking),
            Kind.Single => float.IsInfinity(value.singleBacking),
            _ => false,
        };
    }

    /// <summary>
    /// Determine if a value is NaN.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is NaN.</returns>
    public static bool IsNaN(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Double => double.IsNaN(value.doubleBacking),
            Kind.Single => float.IsNaN(value.singleBacking),
            _ => false,
        };
    }

    /// <summary>
    /// Determine if a value is negative infinity.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is negative infinity.</returns>
    public static bool IsNegativeInfinity(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Double => double.IsNegativeInfinity(value.doubleBacking),
            Kind.Single => float.IsNegativeInfinity(value.singleBacking),
            _ => false,
        };
    }

    /// <summary>
    /// Determine if a value is positive infinity.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is positive infinity.</returns>
    public static bool IsPositiveInfinity(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Double => double.IsNegativeInfinity(value.doubleBacking),
            Kind.Single => float.IsNegativeInfinity(value.singleBacking),
            _ => false,
        };
    }

    /// <summary>
    /// Determine if a value is Zero.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is Zero.</returns>
    public static bool IsZero(BinaryJsonNumber value)
    {
        return value.numericKind switch
        {
            Kind.Decimal => value.decimalBacking.Equals(decimal.Zero),
            Kind.Double => value.doubleBacking.Equals(0),
            Kind.Int16 => ((short)value.longBacking).Equals(0),
            Kind.Int32 => ((int)value.longBacking).Equals(0),
            Kind.Int64 => value.longBacking.Equals(0),
            Kind.SByte => ((sbyte)value.longBacking).Equals(0),
            Kind.Single => value.singleBacking.Equals(0f),
            _ => true,
        };
    }

    /// <summary>
    /// Parse a string into a <see cref="BinaryJsonNumber"/>.
    /// </summary>
    /// <param name="s">The value to parse.</param>
    /// <param name="style">The number styles to use.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns>An instance of a binary JSON number.</returns>
    /// <remarks>
    /// If you wish to control the underlying numeric kind of the <see cref="BinaryJsonNumber"/> then
    /// you should parse into that underlying type, and create a <see cref="BinaryJsonNumber"/> from the value.
    /// </remarks>
    public static BinaryJsonNumber Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider)
    {
        return new(double.Parse(s.ToString(), style, provider));
    }

    /// <summary>
    /// Parse a string into a <see cref="BinaryJsonNumber"/>.
    /// </summary>
    /// <param name="s">The value to parse.</param>
    /// <param name="style">The number styles to use.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns>An instance of a binary JSON number.</returns>
    /// <remarks>
    /// If you wish to control the underlying numeric kind of the <see cref="BinaryJsonNumber"/> then
    /// you should parse into that underlying type, and create a <see cref="BinaryJsonNumber"/> from the value.
    /// </remarks>
    public static BinaryJsonNumber Parse(string s, NumberStyles style, IFormatProvider? provider)
    {
        return new(double.Parse(s, style, provider));
    }

    /// <summary>
    /// Try to parse a string into a <see cref="BinaryJsonNumber"/>.
    /// </summary>
    /// <param name="s">The value to parse.</param>
    /// <param name="style">The number styles to use.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="result">The parsed number.</param>
    /// <returns><see langword="true"/> if the value was parsed successfully.</returns>
    /// <remarks>
    /// If you wish to control the underlying numeric kind of the <see cref="BinaryJsonNumber"/> then
    /// you should parse into that underlying type, and create a <see cref="BinaryJsonNumber"/> from the value.
    /// </remarks>
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, [MaybeNullWhen(false)] out BinaryJsonNumber result)
    {
        if (double.TryParse(s.ToString(), style, provider, out double d))
        {
            result = new(d);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Try to parse a string into a <see cref="BinaryJsonNumber"/>.
    /// </summary>
    /// <param name="s">The value to parse.</param>
    /// <param name="style">The number styles to use.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="result">The parsed number.</param>
    /// <returns><see langword="true"/> if the value was parsed successfully.</returns>
    /// <remarks>
    /// If you wish to control the underlying numeric kind of the <see cref="BinaryJsonNumber"/> then
    /// you should parse into that underlying type, and create a <see cref="BinaryJsonNumber"/> from the value.
    /// </remarks>
    public static bool TryParse([NotNullWhen(true)] string? s, NumberStyles style, IFormatProvider? provider, [MaybeNullWhen(false)] out BinaryJsonNumber result)
    {
        if (double.TryParse(s, style, provider, out double d))
        {
            result = new(d);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Parse a string into a <see cref="BinaryJsonNumber"/>.
    /// </summary>
    /// <param name="s">The value to parse.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns>An instance of a binary JSON number.</returns>
    /// <remarks>
    /// If you wish to control the underlying numeric kind of the <see cref="BinaryJsonNumber"/> then
    /// you should parse into that underlying type, and create a <see cref="BinaryJsonNumber"/> from the value.
    /// </remarks>
    public static BinaryJsonNumber Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        return new(double.Parse(s.ToString(), provider));
    }

    /// <summary>
    /// Try to parse a string into a <see cref="BinaryJsonNumber"/>.
    /// </summary>
    /// <param name="s">The value to parse.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="result">The parsed number.</param>
    /// <returns><see langword="true"/> if the value was parsed successfully.</returns>
    /// <remarks>
    /// If you wish to control the underlying numeric kind of the <see cref="BinaryJsonNumber"/> then
    /// you should parse into that underlying type, and create a <see cref="BinaryJsonNumber"/> from the value.
    /// </remarks>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out BinaryJsonNumber result)
    {
        if (double.TryParse(s.ToString(), NumberStyles.Float, provider, out double d))
        {
            result = new(d);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Parse a string into a <see cref="BinaryJsonNumber"/>.
    /// </summary>
    /// <param name="s">The value to parse.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns>An instance of a binary JSON number.</returns>
    /// <remarks>
    /// If you wish to control the underlying numeric kind of the <see cref="BinaryJsonNumber"/> then
    /// you should parse into that underlying type, and create a <see cref="BinaryJsonNumber"/> from the value.
    /// </remarks>
    public static BinaryJsonNumber Parse(string s, IFormatProvider? provider)
    {
        return new(double.Parse(s, provider));
    }

    /// <summary>
    /// Try to parse a string into a <see cref="BinaryJsonNumber"/>.
    /// </summary>
    /// <param name="s">The value to parse.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="result">The parsed number.</param>
    /// <returns><see langword="true"/> if the value was parsed successfully.</returns>
    /// <remarks>
    /// If you wish to control the underlying numeric kind of the <see cref="BinaryJsonNumber"/> then
    /// you should parse into that underlying type, and create a <see cref="BinaryJsonNumber"/> from the value.
    /// </remarks>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out BinaryJsonNumber result)
    {
        if (double.TryParse(s, NumberStyles.Float, provider, out double d))
        {
            result = new(d);
            return true;
        }

        result = default;
        return false;
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
                Kind.Byte => left.ulongBacking.Equals(right.ulongBacking),
                Kind.Decimal => left.decimalBacking.Equals(right.decimalBacking),
                Kind.Double => left.doubleBacking.Equals(right.doubleBacking),
                Kind.Int16 => left.longBacking.Equals(right.longBacking),
                Kind.Int32 => left.longBacking.Equals(right.longBacking),
                Kind.Int64 => left.longBacking.Equals(right.longBacking),
                Kind.SByte => left.longBacking.Equals(right.longBacking),
                Kind.Single => left.singleBacking.Equals(right.singleBacking),
                Kind.UInt16 => left.ulongBacking.Equals(right.ulongBacking),
                Kind.UInt32 => left.ulongBacking.Equals(right.ulongBacking),
                Kind.UInt64 => left.ulongBacking.Equals(right.ulongBacking),
                _ => throw new NotSupportedException(),
            };
        }

        return right.numericKind switch
        {
            Kind.Byte => left.GetDouble().Equals(right.GetDouble()),
            Kind.Decimal =>
                left.numericKind == Kind.Single
                ? right.decimalBacking.Equals((decimal)left.singleBacking)
                : ((decimal)left.GetDouble()).Equals(right.decimalBacking),
            Kind.Double => left.GetDouble().Equals(right.doubleBacking),
            Kind.Int16 => left.GetDouble().Equals(right.GetDouble()),
            Kind.Int32 => left.GetDouble().Equals(right.GetDouble()),
            Kind.Int64 => left.GetDouble().Equals(right.GetDouble()),
            Kind.SByte => left.GetDouble().Equals(right.GetDouble()),
            Kind.Single =>
                left.numericKind == Kind.Decimal
                    ? left.decimalBacking.Equals((decimal)right.singleBacking)
                    : left.GetDouble().Equals(right.GetDouble()),
            Kind.UInt16 => left.GetDouble().Equals(right.GetDouble()),
            Kind.UInt32 => left.GetDouble().Equals(right.GetDouble()),
            Kind.UInt64 => left.GetDouble().Equals(right.GetDouble()),
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
                return Equals(binaryNumber.decimalBacking, jsonNumberDecimal);
            }

            if (jsonNumber.TryGetDouble(out double jsonNumberDouble))
            {
                return Equals((double)binaryNumber.decimalBacking, jsonNumberDouble);
            }
        }
        else
        {
            if (jsonNumber.TryGetDouble(out double jsonNumberDouble))
            {
                return Equals(binaryNumber.GetDouble(), jsonNumberDouble);
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
                Kind.Byte => left.ulongBacking.CompareTo(right.ulongBacking),
                Kind.Decimal => left.decimalBacking.CompareTo(right.decimalBacking),
                Kind.Double => left.doubleBacking.CompareTo(right.doubleBacking),
                Kind.Int16 => left.longBacking.CompareTo(right.longBacking),
                Kind.Int32 => left.longBacking.CompareTo(right.longBacking),
                Kind.Int64 => left.longBacking.CompareTo(right.longBacking),
                Kind.SByte => left.longBacking.CompareTo(right.longBacking),
                Kind.Single => left.singleBacking.CompareTo(right.singleBacking),
                Kind.UInt16 => left.ulongBacking.CompareTo(right.ulongBacking),
                Kind.UInt32 => left.ulongBacking.CompareTo(right.ulongBacking),
                Kind.UInt64 => left.ulongBacking.CompareTo(right.ulongBacking),
                _ => throw new NotSupportedException(),
            };
        }

        return right.numericKind switch
        {
            Kind.Byte => left.GetDouble().CompareTo(right.GetDouble()),
            Kind.Decimal => ((decimal)left.GetDouble()).CompareTo(right.decimalBacking),
            Kind.Double => left.GetDouble().CompareTo(right.doubleBacking),
            Kind.Int16 => left.GetDouble().CompareTo(right.GetDouble()),
            Kind.Int32 => left.GetDouble().CompareTo(right.GetDouble()),
            Kind.Int64 => left.GetDouble().CompareTo(right.GetDouble()),
            Kind.SByte => left.GetDouble().CompareTo(right.GetDouble()),
            Kind.Single => left.GetDouble().CompareTo(right.GetDouble()),
            Kind.UInt16 => left.GetDouble().CompareTo(right.GetDouble()),
            Kind.UInt32 => left.GetDouble().CompareTo(right.GetDouble()),
            Kind.UInt64 => left.GetDouble().CompareTo(right.GetDouble()),
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
                return leftDecimal.CompareTo(right.decimalBacking);
            }

            if (left.TryGetDouble(out double leftDouble))
            {
                return leftDouble.CompareTo(right.GetDouble());
            }
        }
        else
        {
            if (left.TryGetDouble(out double leftDouble))
            {
                return leftDouble.CompareTo(right.GetDouble());
            }

            if (left.TryGetDecimal(out decimal leftDecimal))
            {
                return leftDecimal.CompareTo((decimal)right.GetDouble());
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
            if (jsonNumberDouble < 0)
            {
                return new BinaryJsonNumber(jsonNumberDouble);
            }
            else
            {
                return new BinaryJsonNumber(jsonNumberDouble);
            }
        }

        // But if we can't, we'll go with a decimal
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsMultipleOf(double x, BinaryJsonNumber y)
    {
        // We use the standard policy of working through a double if possible, falling back to a decimal if not.
        if (y.numericKind != Kind.Decimal)
        {
            return Math.Abs(Math.IEEERemainder(x, y.GetDouble())) <= 1.0E-9;
        }

        return Math.Abs(decimal.Remainder((decimal)x, y.decimalBacking)) <= 1.0E-5M;
    }

    /// <summary>
    /// Determines if this value is a multiple of the other value.
    /// </summary>
    /// <param name="x">The value to test.</param>
    /// <param name="y">The factor to test.</param>
    /// <returns><see langword="true"/> if the value is a multiple of the given factor.</returns>
    /// <exception cref="NotSupportedException">The number format is not supported.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsMultipleOf(decimal x, BinaryJsonNumber y)
    {
        if (y.numericKind != Kind.Double)
        {
            return Math.Abs(decimal.Remainder(x, (decimal)y.GetDouble())) <= 1.0E-5M;
        }

        return Math.Abs(decimal.Remainder(x, y.decimalBacking)) <= 1.0E-5M;
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
            Kind.Int16 => 8,
            Kind.Int32 => 16,
            Kind.Int64 => 32,
            Kind.SByte => 8,
            Kind.Single => 64,
            Kind.UInt16 => 8,
            Kind.UInt32 => 16,
            Kind.UInt64 => 32,
            _ => throw new NotSupportedException(),
        };
    }

    /// <inheritdoc/>
    public bool Equals(BinaryJsonNumber other)
    {
        return Equals(this, other);
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

    /// <summary>
    /// Create an instance of a number.
    /// </summary>
    /// <typeparam name="TOther">The numeric type to create.</typeparam>
    /// <returns>An instance of the number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TOther CreateChecked<TOther>()
    {
        return this.numericKind switch
        {
            Kind.Byte => CastTo<TOther>.From(this.ulongBacking),
            Kind.Decimal => CastTo<TOther>.From(this.decimalBacking),
            Kind.Double => CastTo<TOther>.From(this.doubleBacking),
            Kind.Int16 => CastTo<TOther>.From(this.longBacking),
            Kind.Int32 => CastTo<TOther>.From(this.longBacking),
            Kind.Int64 => CastTo<TOther>.From(this.longBacking),
            Kind.SByte => CastTo<TOther>.From(this.longBacking),
            Kind.Single => CastTo<TOther>.From(this.singleBacking),
            Kind.UInt16 => CastTo<TOther>.From(this.ulongBacking),
            Kind.UInt32 => CastTo<TOther>.From(this.ulongBacking),
            Kind.UInt64 => CastTo<TOther>.From(this.ulongBacking),
            _ => throw new NotSupportedException(),
        };
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.numericKind switch
        {
            Kind.Byte => this.ulongBacking.ToString(),
            Kind.Decimal => this.decimalBacking.ToString(),
            Kind.Double => this.doubleBacking.ToString(),
            Kind.Int16 => this.longBacking.ToString(),
            Kind.Int32 => this.longBacking.ToString(),
            Kind.Int64 => this.longBacking.ToString(),
            Kind.SByte => this.longBacking.ToString(),
            Kind.Single => this.singleBacking.ToString(),
            Kind.UInt16 => this.ulongBacking.ToString(),
            Kind.UInt32 => this.ulongBacking.ToString(),
            Kind.UInt64 => this.ulongBacking.ToString(),
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
                Kind.Byte => this.ulongBacking % multipleOf.ulongBacking == 0,
                Kind.Double => Math.Abs(Math.IEEERemainder(this.doubleBacking, multipleOf.doubleBacking)) <= 1.0E-9,
                Kind.Decimal => Math.Abs(decimal.Remainder(this.decimalBacking, multipleOf.decimalBacking)) <= 1.0E-5M,
                Kind.Int16 => Math.Abs(this.longBacking) % Math.Abs(multipleOf.longBacking) == 0,
                Kind.Int32 => Math.Abs(this.longBacking) % Math.Abs(multipleOf.longBacking) == 0,
                Kind.Int64 => Math.Abs(this.longBacking) % Math.Abs(multipleOf.longBacking) == 0,
                Kind.SByte => Math.Abs(this.longBacking) % Math.Abs(multipleOf.longBacking) == 0,
                Kind.Single => Math.Abs(Math.IEEERemainder(this.singleBacking, multipleOf.singleBacking)) <= 1.0E-5,
                Kind.UInt16 => this.ulongBacking % multipleOf.ulongBacking == 0,
                Kind.UInt32 => this.ulongBacking % multipleOf.ulongBacking == 0,
                Kind.UInt64 => this.ulongBacking % multipleOf.ulongBacking == 0,
                _ => throw new NotSupportedException(),
            };
        }

        return this.numericKind switch
        {
            Kind.Byte => IsMultipleOf(this.GetDouble(), multipleOf),
            Kind.Double => IsMultipleOf(this.doubleBacking, multipleOf),
            Kind.Decimal => IsMultipleOf(this.decimalBacking, multipleOf),
            Kind.Int16 => IsMultipleOf(this.GetDouble(), multipleOf),
            Kind.Int32 => IsMultipleOf(this.GetDouble(), multipleOf),
            Kind.Int64 => IsMultipleOf(this.GetDouble(), multipleOf),
            Kind.SByte => IsMultipleOf(this.GetDouble(), multipleOf),
            Kind.Single => IsMultipleOf(this.GetDouble(), multipleOf),
            Kind.UInt16 => IsMultipleOf(this.GetDouble(), multipleOf),
            Kind.UInt32 => IsMultipleOf(this.GetDouble(), multipleOf),
            Kind.UInt64 => IsMultipleOf(this.GetDouble(), multipleOf),
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
        // Build a hashcode which matches the equality semantics.
        return this.numericKind switch
        {
            Kind.Byte => this.GetDouble().GetHashCode(),
            Kind.Double => this.GetDouble().GetHashCode(),
            Kind.Decimal => this.decimalBacking.GetHashCode(),
            Kind.Int16 => this.GetDouble().GetHashCode(),
            Kind.Int32 => this.GetDouble().GetHashCode(),
            Kind.Int64 => this.GetDouble().GetHashCode(),
            Kind.SByte => this.GetDouble().GetHashCode(),
            Kind.Single => this.GetDouble().GetHashCode(),
            Kind.UInt16 => this.GetDouble().GetHashCode(),
            Kind.UInt32 => this.GetDouble().GetHashCode(),
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
            return this.longBacking != 0;
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
                writer.WriteNumberValue(this.ulongBacking);
                break;
            case Kind.Decimal:
                writer.WriteNumberValue(this.decimalBacking);
                break;
            case Kind.Double:
                writer.WriteNumberValue(this.doubleBacking);
                break;
            case Kind.Int16:
                writer.WriteNumberValue(this.longBacking);
                break;
            case Kind.Int32:
                writer.WriteNumberValue(this.longBacking);
                break;
            case Kind.Int64:
                writer.WriteNumberValue(this.longBacking);
                break;
            case Kind.SByte:
                writer.WriteNumberValue(this.longBacking);
                break;
            case Kind.Single:
                writer.WriteNumberValue(this.singleBacking);
                break;
            case Kind.UInt16:
                writer.WriteNumberValue(this.ulongBacking);
                break;
            case Kind.UInt32:
                writer.WriteNumberValue(this.ulongBacking);
                break;
            case Kind.UInt64:
                writer.WriteNumberValue(this.ulongBacking);
                break;
            default:
                throw new NotSupportedException();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetDouble()
    {
        return this.numericKind switch
        {
            Kind.Bool => throw new OverflowException(),
            Kind.Byte => this.ulongBacking,
            Kind.Decimal => checked((double)this.decimalBacking),
            Kind.Double => this.doubleBacking,
            Kind.Int16 => this.longBacking,
            Kind.Int32 => this.longBacking,
            Kind.Int64 => this.longBacking,
            Kind.SByte => this.longBacking,
            Kind.Single => this.singleBacking,
            Kind.UInt16 => this.ulongBacking,
            Kind.UInt32 => this.ulongBacking,
            Kind.UInt64 => this.ulongBacking,
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Class to cast to a specified type.
    /// </summary>
    /// <typeparam name="T">Target type.</typeparam>
    /// <remarks>
    /// The original code was derived from a StackOverflow answer here https://stackoverflow.com/a/23391746.
    /// </remarks>
    public static class CastTo<T>
    {
        /// <summary>
        /// Casts from the source type to the target type.
        /// </summary>
        /// <param name="s">An instance of the source type to be case to the target type.</param>
        /// <typeparam name="TSource">Source type to cast from. Usually a generic type.</typeparam>
        /// <returns>An instance of the target type, cast from the source type.</returns>
        /// <remarks>
        /// This does not cause boxing for value types. It is especially useful in generic methods.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "The intended usage is meant to be self describing, e.g. CastTo<int>.From(x) means 'cast to int from x', so the usual reasoning behind CA1000 does not apply here")]
        public static T From<TSource>(TSource s)
        {
            return Cache<TSource>.Caster(s);
        }

        private static class Cache<TSsource>
        {
            public static readonly Func<TSsource, T> Caster = Get();

            private static Func<TSsource, T> Get()
            {
                ParameterExpression p = Expression.Parameter(typeof(TSsource));
                UnaryExpression c = Expression.ConvertChecked(p, typeof(T));
                return Expression.Lambda<Func<TSsource, T>>(c, p).Compile();
            }
        }
    }
}
#endif