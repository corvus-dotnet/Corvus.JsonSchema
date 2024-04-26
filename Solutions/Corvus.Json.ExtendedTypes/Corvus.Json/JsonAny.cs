// <copyright file="JsonAny.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// Represents any JSON value.
/// </summary>
public readonly partial struct JsonAny
{
    /// <summary>
    /// Conversion from string.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonAny(string value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion from bool.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonAny(bool value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion from byte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonAny(byte value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from decimal.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonAny(decimal value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from double.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonAny(double value)
    {
        return new(new BinaryJsonNumber(value));
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Conversion from Half.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonAny(Half value)
    {
        return new(new BinaryJsonNumber(value));
    }
#endif

    /// <summary>
    /// Conversion from short.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonAny(short value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from int.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonAny(int value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from long.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonAny(long value)
    {
        return new(new BinaryJsonNumber(value));
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Conversion from Int128.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonAny(Int128 value)
    {
        return new(new BinaryJsonNumber(value));
    }
#endif

    /// <summary>
    /// Conversion from sbyte.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonAny(sbyte value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from float.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonAny(float value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from ushort.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonAny(ushort value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from uint.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonAny(uint value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from ulong.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonAny(ulong value)
    {
        return new(new BinaryJsonNumber(value));
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Conversion from UInt128.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonAny(UInt128 value)
    {
        return new(new BinaryJsonNumber(value));
    }
#endif

    /// <inheritdoc/>
    public ValidationContext Validate(in ValidationContext validationContext, ValidationLevel level = ValidationLevel.Flag)
    {
        // Always valid.
        return validationContext;
    }
}