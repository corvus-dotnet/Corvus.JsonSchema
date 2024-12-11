// <copyright file="BinaryJsonNumber.NetStandard.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if !NET8_0_OR_GREATER
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

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
    /// <returns><see langword="true"/> if the values are not equal.</returns>
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

        if (left.numericKind == Kind.Decimal)
        {
            return right.numericKind == Kind.Single
            ? left.decimalBacking.Equals((decimal)right.singleBacking)
            : ((decimal)right.GetDouble()).Equals(left.decimalBacking);
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
        return Compare(jsonNumber, binaryNumber) == 0;
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

        return right.numericKind switch
        {
            Kind.Byte => ComparePreferByte(left, (byte)right.ulongBacking),
            Kind.Decimal => ComparePreferDecimal(left, right.decimalBacking),
            Kind.Double => ComparePreferDouble(left, right.doubleBacking),
            Kind.Int16 => ComparePreferInt16(left, (short)right.longBacking),
            Kind.Int32 => ComparePreferInt32(left, (int)right.longBacking),
            Kind.Int64 => ComparePreferInt64(left, right.longBacking),
            Kind.SByte => ComparePreferSByte(left, (sbyte)right.longBacking),
            Kind.Single => ComparePreferSingle(left, right.singleBacking),
            Kind.UInt16 => ComparePreferUInt16(left, (ushort)right.ulongBacking),
            Kind.UInt32 => ComparePreferUInt32(left, (uint)right.ulongBacking),
            Kind.UInt64 => ComparePreferUInt64(left, right.ulongBacking),
            _ => throw new NotSupportedException(),
        };

        static int ComparePreferSingle(JsonElement left, float right)
        {
            if (left.TryGetSingle(out float l))
            {
                return l.CompareTo(right);
            }

            if (left.TryGetDouble(out double d))
            {
                return d.CompareTo(right);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return m.CompareTo(right);
            }

            throw new NotSupportedException();
        }

        static int ComparePreferDouble(JsonElement left, double right)
        {
            if (left.TryGetDouble(out double d))
            {
                return d.CompareTo(right);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return m.CompareTo(right);
            }

            throw new NotSupportedException();
        }

        static int ComparePreferDecimal(JsonElement left, decimal right)
        {
            if (left.TryGetDecimal(out decimal m))
            {
                return m.CompareTo(right);
            }

            if (left.TryGetDouble(out double d))
            {
                return d.CompareTo(right);
            }

            throw new NotSupportedException();
        }

        static int ComparePreferByte(JsonElement left, byte right)
        {
            if (left.TryGetByte(out byte l))
            {
                return l.CompareTo(right);
            }

            if (left.TryGetDouble(out double d))
            {
                return d.CompareTo(right);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return m.CompareTo(right);
            }

            throw new NotSupportedException();
        }

        static int ComparePreferUInt16(JsonElement left, ushort right)
        {
            if (left.TryGetUInt16(out ushort l))
            {
                return l.CompareTo(right);
            }

            if (left.TryGetDouble(out double d))
            {
                return d.CompareTo(right);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return m.CompareTo(right);
            }

            throw new NotSupportedException();
        }

        static int ComparePreferUInt32(JsonElement left, uint right)
        {
            if (left.TryGetUInt32(out uint l))
            {
                return l.CompareTo(right);
            }

            if (left.TryGetDouble(out double d))
            {
                return d.CompareTo(right);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return m.CompareTo(right);
            }

            throw new NotSupportedException();
        }

        static int ComparePreferUInt64(JsonElement left, ulong right)
        {
            if (left.TryGetUInt64(out ulong l))
            {
                return l.CompareTo(right);
            }

            if (left.TryGetDouble(out double d))
            {
                return d.CompareTo(right);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return m.CompareTo(right);
            }

            throw new NotSupportedException();
        }

        static int ComparePreferSByte(JsonElement left, sbyte right)
        {
            if (left.TryGetSByte(out sbyte l))
            {
                return l.CompareTo(right);
            }

            if (left.TryGetDouble(out double d))
            {
                return d.CompareTo(right);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return m.CompareTo(right);
            }

            throw new NotSupportedException();
        }

        static int ComparePreferInt16(JsonElement left, short right)
        {
            if (left.TryGetInt16(out short l))
            {
                return l.CompareTo(right);
            }

            if (left.TryGetDouble(out double d))
            {
                return d.CompareTo(right);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return m.CompareTo(right);
            }

            throw new NotSupportedException();
        }

        static int ComparePreferInt32(JsonElement left, int right)
        {
            if (left.TryGetInt32(out int l))
            {
                return l.CompareTo(right);
            }

            if (left.TryGetDouble(out double d))
            {
                return d.CompareTo(right);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return m.CompareTo(right);
            }

            throw new NotSupportedException();
        }

        static int ComparePreferInt64(JsonElement left, long right)
        {
            if (left.TryGetInt64(out long l))
            {
                return l.CompareTo(right);
            }

            if (left.TryGetDouble(out double d))
            {
                return d.CompareTo(right);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return m.CompareTo(right);
            }

            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Gets a binary number from a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="jsonElement">The element from which to create the <see cref="BinaryJsonNumber"/>.</param>
    /// <returns>The <see cref="BinaryJsonNumber"/> created from the <see cref="JsonElement"/>.</returns>
    /// <exception cref="FormatException">The JsonElement was not in a supported format.</exception>
    public static BinaryJsonNumber FromJson(in JsonElement jsonElement)
    {
        return FromJson(jsonElement, Kind.Double);
    }

    /// <summary>
    /// Gets a binary number from a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="jsonElement">The element from which to create the <see cref="BinaryJsonNumber"/>.</param>
    /// <param name="preferredKind">Indicates whether to use 128 processing.</param>
    /// <returns>The <see cref="BinaryJsonNumber"/> created from the <see cref="JsonElement"/>.</returns>
    /// <exception cref="FormatException">The JsonElement was not in a supported format.</exception>
    public static BinaryJsonNumber FromJson(in JsonElement jsonElement, Kind preferredKind)
    {
        if (jsonElement.ValueKind != JsonValueKind.Number)
        {
            throw new FormatException();
        }

        return preferredKind switch
        {
            Kind.Byte => GetPreferByte(jsonElement),
            Kind.Decimal => GetPreferDecimal(jsonElement),
            Kind.Double => GetPreferDouble(jsonElement),
            Kind.Int16 => GetPreferInt16(jsonElement),
            Kind.Int32 => GetPreferInt32(jsonElement),
            Kind.Int64 => GetPreferInt64(jsonElement),
            Kind.SByte => GetPreferSByte(jsonElement),
            Kind.Single => GetPreferSingle(jsonElement),
            Kind.UInt16 => GetPreferUInt16(jsonElement),
            Kind.UInt32 => GetPreferUInt32(jsonElement),
            Kind.UInt64 => GetPreferUInt64(jsonElement),
            Kind.Half => GetPreferDouble(jsonElement),
            Kind.Int128 => GetPreferDouble(jsonElement),
            Kind.UInt128 => GetPreferDouble(jsonElement),
            _ => throw new NotSupportedException(),
        };

        static BinaryJsonNumber GetPreferSingle(JsonElement left)
        {
            if (left.TryGetSingle(out float l))
            {
                return new(l);
            }

            if (left.TryGetDouble(out double d))
            {
                return new(d);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return new(m);
            }

            throw new NotSupportedException();
        }

        static BinaryJsonNumber GetPreferDouble(JsonElement left)
        {
            if (left.TryGetDouble(out double d))
            {
                return new(d);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return new(m);
            }

            throw new NotSupportedException();
        }

        static BinaryJsonNumber GetPreferDecimal(JsonElement left)
        {
            if (left.TryGetDecimal(out decimal m))
            {
                return new(m);
            }

            if (left.TryGetDouble(out double d))
            {
                return new(d);
            }

            throw new NotSupportedException();
        }

        static BinaryJsonNumber GetPreferByte(JsonElement left)
        {
            if (left.TryGetByte(out byte l))
            {
                return new(l);
            }

            if (left.TryGetDouble(out double d))
            {
                return new(d);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return new(m);
            }

            throw new NotSupportedException();
        }

        static BinaryJsonNumber GetPreferUInt16(JsonElement left)
        {
            if (left.TryGetUInt16(out ushort l))
            {
                return new(l);
            }

            if (left.TryGetDouble(out double d))
            {
                return new(d);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return new(m);
            }

            throw new NotSupportedException();
        }

        static BinaryJsonNumber GetPreferUInt32(JsonElement left)
        {
            if (left.TryGetUInt32(out uint l))
            {
                return new(l);
            }

            if (left.TryGetDouble(out double d))
            {
                return new(d);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return new(m);
            }

            throw new NotSupportedException();
        }

        static BinaryJsonNumber GetPreferUInt64(JsonElement left)
        {
            if (left.TryGetUInt64(out ulong l))
            {
                return new(l);
            }

            if (left.TryGetDouble(out double d))
            {
                return new(d);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return new(m);
            }

            throw new NotSupportedException();
        }

        static BinaryJsonNumber GetPreferSByte(JsonElement left)
        {
            if (left.TryGetSByte(out sbyte l))
            {
                return new(l);
            }

            if (left.TryGetDouble(out double d))
            {
                return new(d);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return new(m);
            }

            throw new NotSupportedException();
        }

        static BinaryJsonNumber GetPreferInt16(JsonElement left)
        {
            if (left.TryGetInt16(out short l))
            {
                return new(l);
            }

            if (left.TryGetDouble(out double d))
            {
                return new(d);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return new(m);
            }

            throw new NotSupportedException();
        }

        static BinaryJsonNumber GetPreferInt32(JsonElement left)
        {
            if (left.TryGetInt32(out int l))
            {
                return new(l);
            }

            if (left.TryGetDouble(out double d))
            {
                return new(d);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return new(m);
            }

            throw new NotSupportedException();
        }

        static BinaryJsonNumber GetPreferInt64(JsonElement left)
        {
            if (left.TryGetInt64(out long l))
            {
                return new(l);
            }

            if (left.TryGetDouble(out double d))
            {
                return new(d);
            }

            if (left.TryGetDecimal(out decimal m))
            {
                return new(m);
            }

            throw new NotSupportedException();
        }
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
        if (y.numericKind != Kind.Decimal)
        {
            return Math.Abs(decimal.Remainder(x, (decimal)y.GetDouble())) <= 1.0E-5M;
        }

        return Math.Abs(decimal.Remainder(x, y.decimalBacking)) <= 1.0E-5M;
    }

    /// <summary>
    /// Determines if this value is a multiple of the other value.
    /// </summary>
    /// <param name="x">The value to test.</param>
    /// <param name="y">The factor to test.</param>
    /// <returns><see langword="true"/> if the value is a multiple of the given factor.</returns>
    /// <exception cref="NotSupportedException">The number format is not supported.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsMultipleOf(BinaryJsonNumber x, BinaryJsonNumber y)
    {
        return x.IsMultipleOf(y);
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
            Kind.Byte => 3,
            Kind.Decimal => 29,
            Kind.Double => 324,
            Kind.Int16 => 6,
            Kind.Int32 => 11,
            Kind.Int64 => 20,
            Kind.SByte => 4,
            Kind.Single => 47,
            Kind.UInt16 => 5,
            Kind.UInt32 => 10,
            Kind.UInt64 => 20,
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
            Kind.UInt64 => this.GetDouble().GetHashCode(),
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