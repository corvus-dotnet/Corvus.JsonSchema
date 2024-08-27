// <copyright file="ValidateWithoutCoreType.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// JsonSchema validation errors, without asserting the core type.
/// </summary>
public static partial class ValidateWithoutCoreType
{
    /// <summary>
    /// Validate a string type value.
    /// </summary>
    /// <param name="valueKind">The actual value kind.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="typeKeyword">The type keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeString(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null)
    {
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation {typeKeyword ?? "type"} - should have been 'string' but was '{valueKind}'.", typeKeyword ?? "type");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been 'string'.", typeKeyword ?? "type");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }
        else if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type");
        }

        return validationContext;
    }

    /// <summary>
    /// Validate a number type value.
    /// </summary>
    /// <param name="valueKind">The actual value kind.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="typeKeyword">The type keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeNumber(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null)
    {
        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation {typeKeyword ?? "type"} - should have been 'number' but was '{valueKind}'.", typeKeyword ?? "type");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been 'number'.", typeKeyword ?? "type");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }
        else if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'number'.", typeKeyword ?? "type");
        }

        return validationContext;
    }

    /// <summary>
    /// Validate a null type value.
    /// </summary>
    /// <param name="valueKind">The actual value kind.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="typeKeyword">The type keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeNull(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null)
    {
        if (valueKind != JsonValueKind.Null)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation {typeKeyword ?? "type"} - should have been 'null' but was '{valueKind}'.", typeKeyword ?? "type");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been 'null'.", typeKeyword ?? "type");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }
        else if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'null'.", typeKeyword ?? "type");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format integer.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="typeKeyword">The type keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeInteger<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;

        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation {typeKeyword ?? "type"} - should have been 'integer' but was '{valueKind}'.", typeKeyword ?? "type");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been 'integer'.", typeKeyword ?? "type");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        double value = (double)instance.AsNumber;
        if (value != Math.Floor(value))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been 'integer' ' but was {value}.", typeKeyword ?? "type");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been 'integer'.", typeKeyword ?? "type");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'integer'", typeKeyword ?? "type");
        }

        return validationContext;
    }

    /// <summary>
    /// Validate an array type value.
    /// </summary>
    /// <param name="valueKind">The actual value kind.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="typeKeyword">The type keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeArray(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null)
    {
        if (valueKind != JsonValueKind.Array)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation {typeKeyword ?? "type"} - should have been 'array' but was '{valueKind}'.", typeKeyword ?? "type");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been 'array'.", typeKeyword ?? "type");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }
        else if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'array'.", typeKeyword ?? "type");
        }

        return validationContext;
    }

    /// <summary>
    /// Validate a boolean type value.
    /// </summary>
    /// <param name="valueKind">The actual value kind.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="typeKeyword">The type keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeBoolean(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null)
    {
        if (valueKind != JsonValueKind.True && valueKind != JsonValueKind.False)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation {typeKeyword ?? "type"} - should have been 'boolean' but was '{valueKind}'.", typeKeyword ?? "type");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been 'boolean'.", typeKeyword ?? "type");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }
        else if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'boolean'.", typeKeyword ?? "type");
        }

        return validationContext;
    }

    /// <summary>
    /// Validate an object type value.
    /// </summary>
    /// <param name="valueKind">The actual value kind.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="typeKeyword">The type keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeObject(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null)
    {
        if (valueKind != JsonValueKind.Object)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation {typeKeyword ?? "type"} - should have been 'object' but was '{valueKind}'.", typeKeyword ?? "type");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been 'object'.", typeKeyword ?? "type");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }
        else if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'object'.", typeKeyword ?? "type");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format byte.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeByte<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        double value = (double)instance.AsNumber;
        if (value != Math.Floor(value) || value > byte.MaxValue || value < byte.MinValue)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been byte 'number' but was '{instance.ValueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been byte 'number'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation type - was byte 'number'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format sbyte.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The type keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeSByte<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        double value = (double)instance.AsNumber;
        if (value != Math.Floor(value) || value > sbyte.MaxValue || value < sbyte.MinValue)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been sbyte 'number' but was '{instance.ValueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been sbyte 'number'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation type - was sbyte 'number'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format int16.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeInt16<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        double value = (double)instance.AsNumber;
        if (value != Math.Floor(value) || value > short.MaxValue || value < short.MinValue)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been int16 'number' but was '{instance.ValueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been int16 'number'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation type - was int16  'number'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format uint16.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUInt16<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        double value = (double)instance.AsNumber;
        if (value != Math.Floor(value) || value > ushort.MaxValue || value < ushort.MinValue)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been uint16 'number' but was '{instance.ValueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been uint16 'number'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation type - was uint16 'number'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format int32.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeInt32<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        double value = (double)instance.AsNumber;
        if (value != Math.Floor(value) || value > int.MaxValue || value < int.MinValue)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been int32 'number' but was '{instance.ValueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been int32 'number'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation type - was int32 'number'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format uint32.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUInt32<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        double value = (double)instance.AsNumber;
        if (value != Math.Floor(value) || value > uint.MaxValue || value < uint.MinValue)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been uint32 'number' but was '{instance.ValueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been uint32 'number'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation type - was uint32 'number'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format int64.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeInt64<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonNumber number = instance.AsNumber;
        double value = (double)number;
        if (value != Math.Floor(value) || (instance.HasJsonElementBacking && !instance.AsJsonElement.TryGetInt64(out long _)))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been int64 'number' but was '{instance.ValueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been int64 'number'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation type - was int64 'number'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format uint64.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUInt64<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonNumber number = instance.AsNumber;
        double value = (double)number;
        if (value != Math.Floor(value) || value < 0 || (instance.HasJsonElementBacking && !instance.AsJsonElement.TryGetUInt64(out ulong _)))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been uint64 'number' but was '{instance.ValueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been uint64 'number'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation type - was uint64 'number'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format int128.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeInt128<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
#if NET8_0_OR_GREATER
        try
        {
            _ = (Int128)instance.AsNumber;
        }
        catch (FormatException)
        {
            if (level >= ValidationLevel.Detailed)
            {
                double value = (double)instance.AsNumber;
                return validationContext.WithResult(isValid: false, $"Validation type - should have been int128 'number' but was '{instance.ValueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been int128 'number'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }
#endif

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation type - was int128 'number'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format uint128.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUInt128<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
#if NET8_0_OR_GREATER
        try
        {
            _ = (UInt128)instance.AsNumber;
        }
        catch
        {
            if (level >= ValidationLevel.Detailed)
            {
                double value = (double)instance.AsNumber;
                return validationContext.WithResult(isValid: false, $"Validation type - should have been uint128 'number' but was '{instance.ValueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been uint128 'number'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }
#else
        double value = (double)instance.AsNumber;
        if (value != Math.Floor(value) || value < 0)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been uint128 'number' but was '{instance.ValueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been uint128 'number'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }
#endif

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation type - was uint128 'number'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format half.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeHalf<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
#if NET8_0_OR_GREATER
        try
        {
            _ = (Half)instance.AsNumber;
        }
        catch (FormatException)
        {
            if (level >= ValidationLevel.Detailed)
            {
                double value = (double)instance.AsNumber;
                return validationContext.WithResult(isValid: false, $"Validation type - should have been half 'number' but was '{instance.ValueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been half 'number'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }
#endif

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation type - was half 'number'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format single.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeSingle<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        try
        {
            _ = (float)instance.AsNumber;
        }
        catch (FormatException)
        {
            if (level >= ValidationLevel.Detailed)
            {
                double value = (double)instance.AsNumber;
                return validationContext.WithResult(isValid: false, $"Validation type - should have been single 'number' but was '{instance.ValueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been single 'number'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation type - was uint16 'number'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format double.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeDouble<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        try
        {
            _ = (double)instance.AsNumber;
        }
        catch (FormatException)
        {
            if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been double 'number'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation type - was double 'number'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format decimal.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeDecimal<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        try
        {
            _ = (decimal)instance.AsNumber;
        }
        catch (FormatException)
        {
            if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been single 'number'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation type - was decimal 'number'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format uri-template.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUriTemplate<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        ValidationContext result = validationContext;

        instance.AsString.TryGetValue(UriTemplateValidator, new Validate.ValidationContextWrapper(result, level), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool UriTemplateValidator(ReadOnlySpan<char> input, in Validate.ValidationContextWrapper context, out ValidationContext result)
        {
            result = context.Context;

            if (!Validate.UriTemplatePattern.IsMatch(input))
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
#if NET8_0_OR_GREATER
                    result = context.Context.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'uri-template', but was '{input}'.");
#else
                    result = context.Context.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'uri-template', but was '{input.ToString()}'.");
#endif
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation type - should have been a 'string' with format 'uri-template'.");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context.WithResult(isValid: true, "Validation type - was a 'string' with format 'uri-template'.");
            }

            return true;
        }
    }

    /// <summary>
    /// Validates the format idn-email.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeIdnEmail<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        string email = (string)instance.AsString;

        bool isMatch = false;

        try
        {
            // Normalize the domain
            email = Validate.IdnEmailReplacePattern.Replace(email, DomainMapper);
            isMatch = Validate.IdnEmailMatchPattern.IsMatch(email);

            // Examines the domain part of the email and normalizes it.
            static string DomainMapper(Match match)
            {
                // Pull out and process domain name (throws ArgumentException on invalid)
                string domainName = Validate.IdnMapping.GetAscii(match.Groups[2].Value);

                return match.Groups[1].Value + domainName;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            isMatch = false;
        }
        catch (ArgumentException)
        {
            isMatch = false;
        }

        if (!isMatch)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'idn-email', but was '{email}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been a 'string' with format 'idn-email'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation type - was a 'string' with format 'idn-email'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format idn-hostname.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeIdnHostName<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        string value = (string)instance.AsString;

        bool isMatch;
        if (value.StartsWith("xn--"))
        {
            string decodedValue;

            try
            {
                decodedValue = Validate.IdnMapping.GetUnicode(value);
                isMatch = !Validate.InvalidIdnHostNamePattern.IsMatch(decodedValue);
            }
            catch (ArgumentException)
            {
                isMatch = false;
            }
        }
        else
        {
            try
            {
                Validate.IdnMapping.GetAscii(value);
                isMatch = !Validate.InvalidIdnHostNamePattern.IsMatch(value);
            }
            catch (ArgumentException)
            {
                isMatch = false;
            }
        }

        if (!isMatch)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'idn-hostname', but was '{value}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been a 'string' with format 'idn-hostname'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation type - was a 'string' with format 'idn-hostname'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format hostname.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeHostname<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        ValidationContext result = validationContext;

        instance.AsString.TryGetValue(HostnameValidator, new Validate.ValidationContextWrapper(result, level), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool HostnameValidator(ReadOnlySpan<char> input, in Validate.ValidationContextWrapper context, out ValidationContext result)
        {
            bool isMatch;
            result = context.Context;

#if NET8_0_OR_GREATER
            if (input.StartsWith("xn--"))
#else
            if (input.StartsWith("xn--".AsSpan()))
#endif
            {
                try
                {
                    // Sadly there's no support for readonly span in IdnMapping.
                    string decodedValue = Validate.IdnMapping.GetUnicode(input.ToString());
                    isMatch = Validate.HostnamePattern.IsMatch(decodedValue);
                }
                catch (ArgumentException)
                {
                    isMatch = false;
                }
            }
            else
            {
                isMatch = Validate.HostnamePattern.IsMatch(input);
            }

            if (!isMatch)
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
                    result = context.Context.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'hostname', but was '{input.ToString()}'.");
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation type - should have been a 'string' with format 'hostname'.");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context.WithResult(isValid: true, "Validation type - was a 'string' with format 'hostname'.");
            }

            return true;
        }
    }

    /// <summary>
    /// Validates the format uuid.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUuid<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        ValidationContext result = validationContext;

        instance.AsString.TryGetValue(UuidValidator, new Validate.ValidationContextWrapper(result, level), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool UuidValidator(ReadOnlySpan<char> input, in Validate.ValidationContextWrapper context, out ValidationContext result)
        {
            result = context.Context;

            if (!Validate.UuidTemplatePattern.IsMatch(input))
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
                    result = context.Context.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'uuid', but was '{input.ToString()}'.");
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation type - should have been a 'string' with format 'uuid'.");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context.WithResult(isValid: true, "Validation type - was a 'string' with format 'uuid'.");
            }

            return true;
        }
    }

    /// <summary>
    /// Validates the format duration.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeDuration<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        ValidationContext result = validationContext;

        instance.AsString.TryGetValue(DurationValidator, new Validate.ValidationContextWrapper(result, level), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool DurationValidator(ReadOnlySpan<char> input, in Validate.ValidationContextWrapper context, out ValidationContext result)
        {
            result = context.Context;

            if (!Validate.DurationPattern.IsMatch(input))
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
#if NET8_0_OR_GREATER
                    result = context.Context.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'duration', but was '{input}'.");
#else
                    result = context.Context.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'duration', but was '{input.ToString()}'.");
#endif
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation type - should have been a 'string' with format 'duration'.");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context.WithResult(isValid: true, "Validation type - was a 'string' with format 'duration'.");
            }

            return true;
        }
    }

    /// <summary>
    /// Validates the format email.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeEmail<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        ValidationContext result = validationContext;

        instance.AsString.TryGetValue(EmailValidator, new Validate.ValidationContextWrapper(result, level), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool EmailValidator(ReadOnlySpan<char> input, in Validate.ValidationContextWrapper context, out ValidationContext result)
        {
            result = context.Context;

            if (!Validate.EmailPattern.IsMatch(input))
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
#if NET8_0_OR_GREATER
                    result = context.Context.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'email', but was '{input}'.");
#else
                    result = context.Context.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'email', but was '{input.ToString()}'.");
#endif
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation type - should have been a 'string' with format 'email'.");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context.WithResult(isValid: true, "Validation type - was a 'string' with format 'email'.");
            }

            return true;
        }
    }

    /// <summary>
    /// Validates the format relative-json-pointer.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeRelativePointer<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        ValidationContext result = validationContext;

        instance.AsString.TryGetValue(RelativePointerValidator, new Validate.ValidationContextWrapper(result, level), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool RelativePointerValidator(ReadOnlySpan<char> input, in Validate.ValidationContextWrapper context, out ValidationContext result)
        {
            result = context.Context;

            if (!Validate.JsonRelativePointerPattern.IsMatch(input))
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
#if NET8_0_OR_GREATER
                    result = context.Context.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'relative-json-pointer', but was '{input}'.");
#else
                    result = context.Context.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'relative-json-pointer', but was '{input.ToString()}'.");
#endif
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation type - should have been a 'string' with format 'relative-json-pointer'.");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context.WithResult(isValid: true, "Validation type - was a 'string' with format 'relative-json-pointer'.");
            }

            return true;
        }
    }

    /// <summary>
    /// Validates the format json-pointer.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypePointer<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        ValidationContext result = validationContext;

        instance.AsString.TryGetValue(PointerValidator, new Validate.ValidationContextWrapper(result, level), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool PointerValidator(ReadOnlySpan<char> input, in Validate.ValidationContextWrapper context, out ValidationContext result)
        {
            result = context.Context;

            if (!Validate.JsonPointerPattern.IsMatch(input))
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
#if NET8_0_OR_GREATER
                    result = context.Context.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'json-pointer', but was '{input}'.");
#else
                    result = context.Context.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'json-pointer', but was '{input.ToString()}'.");
#endif
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation type - should have been a 'string' with format 'json-pointer'.");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context.WithResult(isValid: true, "Validation type - was a 'string' with format 'json-pointer'.");
            }

            return true;
        }
    }

    /// <summary>
    /// Validates a content value.
    /// </summary>
    /// <typeparam name="TValue">The type of the <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The instance to validate.</param>
    /// <param name="validationContext">The current validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeContentPre201909<TValue>(in TValue value, in ValidationContext validationContext, ValidationLevel level)
        where TValue : struct, IJsonValue<TValue>
    {
        return TypeContent(value, validationContext, level, false);
    }

    /// <summary>
    /// Validates a content value.
    /// </summary>
    /// <typeparam name="TValue">The type of the <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The instance to validate.</param>
    /// <param name="validationContext">The current validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="alwaysPassAndAnnotateFailuresInContentDecodingChecks">Always pass failures in content decoding, but annotate.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeContent<TValue>(in TValue value, in ValidationContext validationContext, ValidationLevel level, bool alwaysPassAndAnnotateFailuresInContentDecodingChecks = true)
    where TValue : struct, IJsonValue<TValue>
    {
        ValidationContext result = validationContext;

        JsonContent content = value.As<JsonContent>();
        EncodedContentMediaTypeParseStatus status = content.TryGetJsonDocument(out JsonDocument? _);
        if (status == EncodedContentMediaTypeParseStatus.UnableToDecode)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return result.WithResult(isValid: false, "Validation 8.3 contentEncoding - should have been a 'string' with contentMediaType 'application/json'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return result.WithResult(isValid: false, "Validation 8.3 contentEncoding - should have been a 'string' with contentMediaType 'application/json'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }
        else if (status == EncodedContentMediaTypeParseStatus.UnableToParseToMediaType)
        {
            // Should be Valid, but we just annotate.
            if (level >= ValidationLevel.Detailed)
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks, "Validation 8.4 contentMediaType - should have been a 'string' with contentMediaType 'application/json'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks, "Validation 8.4 contentMediaType - should have been a 'string' with contentMediaType 'application/json'.");
            }
            else
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks);
            }
        }
        else if (level == ValidationLevel.Verbose)
        {
            return result
                .WithResult(isValid: true, "Validation 8.4 contentMediaType - was a'string' with contentMediaType 'application/json'.");
        }

        return result;
    }

    /// <summary>
    /// Validates a base64Content value.
    /// </summary>
    /// <typeparam name="TValue">The type of the <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The instance to validate.</param>
    /// <param name="validationContext">The current validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeBase64ContentPre201909<TValue>(in TValue value, in ValidationContext validationContext, ValidationLevel level)
        where TValue : struct, IJsonValue<TValue>
    {
        return TypeBase64Content(value, validationContext, level, false);
    }

    /// <summary>
    /// Validates a base64Content value.
    /// </summary>
    /// <typeparam name="TValue">The type of the <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The instance to validate.</param>
    /// <param name="validationContext">The current validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="alwaysPassAndAnnotateFailuresInContentDecodingChecks">Always pass failures in content decoding, but annotate.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeBase64Content<TValue>(in TValue value, in ValidationContext validationContext, ValidationLevel level, bool alwaysPassAndAnnotateFailuresInContentDecodingChecks = true)
    where TValue : struct, IJsonValue<TValue>
    {
        ValidationContext result = validationContext;

        JsonBase64Content content = value.As<JsonBase64Content>();
        EncodedContentMediaTypeParseStatus status = content.TryGetJsonDocument(out JsonDocument? _);
        if (status == EncodedContentMediaTypeParseStatus.UnableToDecode)
        {
            // Is valid, but we annotate
            if (level >= ValidationLevel.Detailed)
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks, "Validation 8.3 contentEncoding - should have been a base64 encoded 'string'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks, "Validation 8.3 contentEncoding - should have been a base64 encoded 'string'.");
            }
            else
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks);
            }
        }
        else if (status == EncodedContentMediaTypeParseStatus.UnableToParseToMediaType)
        {
            // Validates true, but we will annotate ite
            if (level >= ValidationLevel.Detailed)
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks, "Validation 8.4 contentMediaType - valid, but should have been a base64 encoded 'string' of type 'application/json'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks, "Validation 8.4 contentMediaType - valid, but should have been a base64 encoded 'string' of type 'application/json'.");
            }
            else
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks);
            }
        }
        else if (level == ValidationLevel.Verbose)
        {
            return result
                .WithResult(isValid: true, "Validation 8.3 contentEncoding - was a base64 encoded 'string'.")
                .WithResult(isValid: true, "Validation 8.4 contentMediaType - was a base64 encoded 'string' of type 'application/json'.");
        }

        return result;
    }

    /// <summary>
    /// Validates a base64 value.
    /// </summary>
    /// <typeparam name="TValue">The type of the <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The instance to validate.</param>
    /// <param name="validationContext">The current validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeBase64StringPre201909<TValue>(in TValue value, in ValidationContext validationContext, ValidationLevel level)
        where TValue : struct, IJsonValue<TValue>
    {
        return TypeBase64String(value, validationContext, level, false);
    }

    /// <summary>
    /// Validates a base64 value.
    /// </summary>
    /// <typeparam name="TValue">The type of the <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The instance to validate.</param>
    /// <param name="validationContext">The current validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="alwaysPassAndAnnotateFailuresInContentDecodingChecks">Always pass but annotate the nodes on encoding failure.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeBase64String<TValue>(in TValue value, in ValidationContext validationContext, ValidationLevel level, bool alwaysPassAndAnnotateFailuresInContentDecodingChecks = true)
    where TValue : struct, IJsonValue<TValue>
    {
        ValidationContext result = validationContext;

        JsonBase64String base64String = value.As<JsonBase64String>();

        if (!base64String.HasBase64Bytes())
        {
            // Valid, but we annotate
            if (level >= ValidationLevel.Detailed)
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks, "Validation 8.3 contentEncoding - should have been a base64 encoded 'string'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks, "Validation 8.3 contentEncoding - should have been a base64 encoded 'string'.");
            }
            else
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks);
            }
        }
        else if (level == ValidationLevel.Verbose)
        {
            return result
                .WithResult(isValid: true, "Validation 8.3 contentEncoding - was a base64 encoded 'string'.");
        }

        return result;
    }

    /// <summary>
    /// Validates the format regex.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeRegex<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonRegex regexInstance = instance.As<JsonRegex>();

        if (!regexInstance.TryGetRegex(out Regex _))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'regex' but was '{regexInstance}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been a 'string' with format 'regex'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation type - was a 'string' with format 'regex'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format iri-reference.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeIriReference<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonIriReference iri = instance.As<JsonIriReference>();

        if (!iri.TryGetUri(out Uri? uri) ||
            uri.OriginalString.StartsWith("\\\\") ||
            (uri.IsAbsoluteUri && uri.Fragment.Contains('\\')) ||
#if NET8_0_OR_GREATER
            (uri.OriginalString.StartsWith('#') && uri.OriginalString.Contains('\\')))
#else
            (uri.OriginalString.StartsWith("#") && uri.OriginalString.Contains('\\')))
#endif
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'iri-reference' but was '{iri}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been a 'string' with format 'iri-reference'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation type - was a 'string' with format 'iri-reference'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format iri.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeIri<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonIri iri = instance.As<JsonIri>();

        if (!iri.TryGetUri(out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'iri' but was '{iri}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been a 'string' with format 'iri'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation type - was a 'string' with format 'iri'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format uri.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUri<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonUri uriInstance = instance.As<JsonUri>();

        if (!(uriInstance.TryGetUri(out Uri? testUri) && (!testUri.IsAbsoluteUri || !testUri.IsUnc)))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'uri' but was '{uriInstance}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been a 'string' with format 'uri'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation type - was a 'string' with format 'uri'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format uri-reference.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUriReference<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonUriReference uriReferenceInstance = instance.As<JsonUriReference>();

        if (!uriReferenceInstance.TryGetUri(out Uri? uri) ||
            uri.OriginalString.StartsWith("\\\\") ||
            (uri.IsAbsoluteUri && uri.Fragment.Contains('\\')) ||
#if NET8_0_OR_GREATER
            (uri.OriginalString.StartsWith('#') && uri.OriginalString.Contains('\\')))
#else
            (uri.OriginalString.StartsWith("#") && uri.OriginalString.Contains('\\')))
#endif
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'uri-reference' but was '{uriReferenceInstance}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been a 'string' with format 'uri-reference'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation type - was a 'string' with format 'uri-reference'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format time.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeTime<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonTime time = instance.As<JsonTime>();

        if (!time.TryGetTime(out _))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'time' but was '{time}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been a 'string' with format 'time'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation type - was a 'string' with format 'time'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format date.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeDate<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonDate date = instance.As<JsonDate>();

        if (!date.TryGetDate(out _))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'date' but was '{date}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been a 'string' with format 'date'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation type - was a 'string' with format 'date'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format ipv6.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeIpV6<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        ValidationContext result = validationContext;

#if NET8_0_OR_GREATER
        instance.AsString.TryGetValue(IpV6Validator, new Validate.ValidationContextWrapper(result, level), out result);
#else
        IpV6Validator(instance.AsString.GetString(), new Validate.ValidationContextWrapper(result, level), out result);
#endif

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

#if NET8_0_OR_GREATER
        static bool IpV6Validator(ReadOnlySpan<char> input, in Validate.ValidationContextWrapper context, out ValidationContext result)
#else
        static bool IpV6Validator(string input, in Validate.ValidationContextWrapper context, out ValidationContext result)
#endif
        {
            result = context.Context;

            if (StandardIPAddress.IPAddressParser(input, null, out IPAddress? address) &&
                address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 &&
                !Validate.ZoneIdExpression.IsMatch(input))
            {
                if (context.Level == ValidationLevel.Verbose)
                {
                    result = context.Context.WithResult(isValid: true, "Validation type - was a 'string' with format 'ipv6'.");
                }

                return true;
            }
            else
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
#if NET8_0_OR_GREATER
                    result = context.Context.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'ipv6', but was '{input}'.");
#else
                    result = context.Context.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'ipv6', but was '{input}'.");
#endif
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation type - should have been a 'string' with format 'ipv6'.");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }
        }
    }

    /// <summary>
    /// Validates the format ipv4.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeIpV4<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        ValidationContext result = validationContext;
#if NET8_0_OR_GREATER
        instance.AsString.TryGetValue(IpV4Validator, new Validate.ValidationContextWrapper(result, level), out result);
#else
        IpV4Validator(instance.AsString.GetString(), new Validate.ValidationContextWrapper(result, level), out result);
#endif
        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

#if NET8_0_OR_GREATER
        static bool IpV4Validator(ReadOnlySpan<char> input, in Validate.ValidationContextWrapper context, out ValidationContext result)
#else
        static bool IpV4Validator(string input, in Validate.ValidationContextWrapper context, out ValidationContext result)
#endif
        {
            result = context.Context;

            if (StandardIPAddress.IPAddressParser(input, null, out IPAddress? address) &&
                address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                Validate.IpV4Pattern.IsMatch(input))
            {
                if (context.Level == ValidationLevel.Verbose)
                {
                    result = context.Context.WithResult(isValid: true, "Validation type - was a 'string' with format 'ipv4'.");
                }

                return true;
            }
            else
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
#if NET8_0_OR_GREATER
                    result = context.Context.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'ipv4', but was '{input}'.");
#else
                    result = context.Context.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'ipv4', but was '{input}'.");
#endif
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation type - should have been a 'string' with format 'ipv4'.");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }
        }
    }

    /// <summary>
    /// Validates the format datetime.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeDateTime<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonDateTime date = instance.As<JsonDateTime>();

        if (!date.TryGetDateTime(out _))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been 'string' with format 'datetime' but was '{date}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been a 'string' with format 'datetime'.");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation type - was a 'string' with format 'datetime'.");
        }

        return validationContext;
    }
}