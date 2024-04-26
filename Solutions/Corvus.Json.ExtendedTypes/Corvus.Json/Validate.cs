// <copyright file="Validate.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Corvus.Json;

/// <summary>
/// JsonSchema validation errors.
/// </summary>
public static partial class Validate
{
    private static readonly Regex IpV4Pattern = CreateIpV4Pattern();
    private static readonly Regex ZoneIdExpression = CreateZoneIdExpression();
    private static readonly Regex EmailPattern = CreateEmailPattern();
    private static readonly Regex DurationPattern = CreateDurationPattern(); // ECMAScript mode stops \d matching non-ASCII digits
    private static readonly Regex HostnamePattern = CreateHostnamePattern();
    private static readonly Regex InvalidIdnHostNamePattern = CreateInvalidIdnHostNamePattern();
    private static readonly Regex UriTemplatePattern = CreateUriTemplatePattern();
    private static readonly Regex UuidTemplatePattern = CreateUuidTemplatePattern();
    private static readonly Regex JsonPointerPattern = CreateJsonPointerPattern();
    private static readonly Regex JsonRelativePointerPattern = CreateJsonRelativePointerPattern();
    private static readonly Regex IdnEmailReplacePattern = CreateIdnEmailReplacePattern();
    private static readonly Regex IdnEmailMatchPattern = CreateIdnEmailMatchPattern();

    private static readonly IdnMapping IdnMapping = new() { AllowUnassigned = true, UseStd3AsciiRules = true };

    /// <summary>
    /// Validate a string type value.
    /// </summary>
    /// <param name="valueKind">The actual value kind.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeString(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level)
    {
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
        else if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was 'string'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validate a number type value.
    /// </summary>
    /// <param name="valueKind">The actual value kind.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeNumber(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level)
    {
        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'number' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'number'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
        else if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was 'number'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validate a null type value.
    /// </summary>
    /// <param name="valueKind">The actual value kind.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeNull(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level)
    {
        if (valueKind != JsonValueKind.Null)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'null' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'null'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
        else if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was 'null'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validate an undefined type value.
    /// </summary>
    /// <param name="valueKind">The actual value kind.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUndefined(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level)
    {
        if (valueKind != JsonValueKind.Null)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'undefined' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'undefined'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
        else if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was 'undefined'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeInteger<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'number' with zero fractional part but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'number' with zero fractional part.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
        else
        {
            double value = (double)instance.AsNumber;
            if (value != Math.Floor(value))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'number' with zero fractional part but was '{valueKind}' with fractional part {value - Math.Floor(value)}.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'number' with zero fractional part.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was 'number' with zero fractional part.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeByte<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been byte 'number' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been byte 'number'.,");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
        else
        {
            double value = (double)instance.AsNumber;
            if (value != Math.Floor(value) || value > byte.MaxValue || value < byte.MinValue)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been byte 'number' but was '{valueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been byte 'number'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was byte 'number'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeSByte<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been an sbyte 'number' was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been an sbyte 'number'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
        else
        {
            double value = (double)instance.AsNumber;
            if (value != Math.Floor(value) || value > sbyte.MaxValue || value < sbyte.MinValue)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been sbyte 'number' but was '{valueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been sbyte 'number'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was sbyte 'number'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeInt16<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been an int16 'number' was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been an int16 'number'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
        else
        {
            double value = (double)instance.AsNumber;
            if (value != Math.Floor(value) || value > short.MaxValue || value < short.MinValue)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been int16 'number' but was '{valueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been int16 'number'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was int16  'number'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUInt16<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been an uint16 'number' was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been an uint16 'number'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
        else
        {
            double value = (double)instance.AsNumber;
            if (value != Math.Floor(value) || value > ushort.MaxValue || value < ushort.MinValue)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been uint16 'number' but was '{valueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been uint16 'number'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was uint16 'number'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeInt32<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been an int32 'number' was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been an int32 'number'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
        else
        {
            double value = (double)instance.AsNumber;
            if (value != Math.Floor(value) || value > int.MaxValue || value < int.MinValue)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been int32 'number' but was '{valueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been int32 'number'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was int32 'number'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUInt32<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been an uint32 'number' was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been an uint32 'number'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
        else
        {
            double value = (double)instance.AsNumber;
            if (value != Math.Floor(value) || value > uint.MaxValue || value < uint.MinValue)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been uint32 'number' but was '{valueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been uint32 'number'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was uint32 'number'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeInt64<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been an uint16 'number' was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been an uint16 'number'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
        else
        {
            JsonNumber number = instance.AsNumber;
            double value = (double)number;
#if NET8_0_OR_GREATER
            var i128 = (Int128)number;
            if (value != Math.Floor(value) || i128 > long.MaxValue || i128 < long.MinValue)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been int64 'number' but was '{valueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been int64 'number'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
#else
            if (value != Math.Floor(value) || (instance.HasJsonElementBacking && !instance.AsJsonElement.TryGetInt64(out long _)))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been int64 'number' but was '{valueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been int64 'number'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
#endif
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was int64 'number'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUInt64<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been an uint64 'number' was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been an uint64 'number'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
        else
        {
            JsonNumber number = instance.AsNumber;
            double value = (double)number;
#if NET8_0_OR_GREATER
            var i128 = (Int128)number;
            if (value != Math.Floor(value) || i128 > ulong.MaxValue || i128 < ulong.MinValue)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been uint64 'number' but was '{valueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been uint64 'number'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
#else
            if (value != Math.Floor(value) || value < 0 || (instance.HasJsonElementBacking && !instance.AsJsonElement.TryGetUInt64(out ulong _)))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been uint64 'number' but was '{valueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been uint64 'number'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
#endif
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was uint64 'number'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeInt128<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been an int128 'number' was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been an int128 'number'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
#if NET8_0_OR_GREATER
        else
        {
            try
            {
                _ = (Int128)instance.AsNumber;
            }
            catch (FormatException)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    double value = (double)instance.AsNumber;
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been int128 'number' but was '{valueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been int128 'number'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
        }
#endif

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was int128 'number'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUInt128<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been an uint128 'number' was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been an uint128 'number'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
#if NET8_0_OR_GREATER
        else
        {
            try
            {
                _ = (UInt128)instance.AsNumber;
            }
            catch (FormatException)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    double value = (double)instance.AsNumber;
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been uint128 'number' but was '{valueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been uint128 'number'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
        }
#else
        double value = (double)instance.AsNumber;
        if (value != Math.Floor(value) || value < 0)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been uint128 'number' but was '{valueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been uint128 'number'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
#endif

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was uint128 'number'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeHalf<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been a half 'number' was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a half 'number'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
#if NET8_0_OR_GREATER
        else
        {
            try
            {
                _ = (Half)instance.AsNumber;
            }
            catch (FormatException)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    double value = (double)instance.AsNumber;
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been half 'number' but was '{valueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been half 'number'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
        }
#endif

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was half 'number'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeSingle<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been a single 'number' was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a single 'number'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
        else
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
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been single 'number' but was '{valueKind}' with value {value} and fractional part {value - Math.Floor(value)}.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been single 'number'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was uint16 'number'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeDouble<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been a single 'number' was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a single 'number'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        try
        {
            _ = (double)instance.AsNumber;
        }
        catch (FormatException)
        {
            if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been double 'number'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was double 'number'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeDecimal<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been a decimal 'number' was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a decimal 'number'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
        else
        {
            try
            {
                _ = (decimal)instance.AsNumber;
            }
            catch (FormatException)
            {
                if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been single 'number'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was decimal 'number'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUriTemplate<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'uri-template' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'uri-template'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        ValidationContext result = validationContext;

        instance.AsString.TryGetValue(UriTemplateValidator, new ValidationContextWrapper(result, level), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool UriTemplateValidator(ReadOnlySpan<char> input, in ValidationContextWrapper context, out ValidationContext result)
        {
            result = context.Context;

            if (!UriTemplatePattern.IsMatch(input))
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
#if NET8_0_OR_GREATER
                    result = context.Context.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'uri-template', but was '{input}'.");
#else
                    result = context.Context.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'uri-template', but was '{input.ToString()}'.");
#endif
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'uri-template'.");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context.WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'uri-template'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeIdnEmail<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'idn-email' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'idn-email'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        string email = (string)instance.AsString;

        bool isMatch = false;

        try
        {
            // Normalize the domain
            email = IdnEmailReplacePattern.Replace(email, DomainMapper);
            isMatch = IdnEmailMatchPattern.IsMatch(email);

            // Examines the domain part of the email and normalizes it.
            static string DomainMapper(Match match)
            {
                // Pull out and process domain name (throws ArgumentException on invalid)
                string domainName = IdnMapping.GetAscii(match.Groups[2].Value);

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
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'idn-email', but was '{email}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'idn-email'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'idn-email'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeIdnHostName<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'idn-hostname' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'idn-hostname'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        string value = (string)instance.AsString;

        bool isMatch;
        if (value.StartsWith("xn--"))
        {
            string decodedValue;

            try
            {
                decodedValue = IdnMapping.GetUnicode(value);
                isMatch = !InvalidIdnHostNamePattern.IsMatch(decodedValue);
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
                IdnMapping.GetAscii(value);
                isMatch = !InvalidIdnHostNamePattern.IsMatch(value);
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
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'idn-hostname', but was '{value}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'idn-hostname'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'idn-hostname'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeHostname<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'hostname' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'hostname'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        ValidationContext result = validationContext;

        instance.AsString.TryGetValue(HostnameValidator, new ValidationContextWrapper(result, level), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool HostnameValidator(ReadOnlySpan<char> input, in ValidationContextWrapper context, out ValidationContext result)
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
                    string decodedValue = IdnMapping.GetUnicode(input.ToString());
                    isMatch = HostnamePattern.IsMatch(decodedValue);
                }
                catch (ArgumentException)
                {
                    isMatch = false;
                }
            }
            else
            {
                isMatch = HostnamePattern.IsMatch(input);
            }

            if (!isMatch)
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
                    result = context.Context.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'hostname', but was '{input.ToString()}'.");
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'hostname'.");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context.WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'hostname'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUuid<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'uuid' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'uuid'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        ValidationContext result = validationContext;

        instance.AsString.TryGetValue(UuidValidator, new ValidationContextWrapper(result, level), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool UuidValidator(ReadOnlySpan<char> input, in ValidationContextWrapper context, out ValidationContext result)
        {
            result = context.Context;

            if (!UuidTemplatePattern.IsMatch(input))
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
                    result = context.Context.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'uuid', but was '{input.ToString()}'.");
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'uuid'.");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context.WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'uuid'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeDuration<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'duration' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'duration'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        ValidationContext result = validationContext;

        instance.AsString.TryGetValue(DurationValidator, new ValidationContextWrapper(result, level), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool DurationValidator(ReadOnlySpan<char> input, in ValidationContextWrapper context, out ValidationContext result)
        {
            result = context.Context;

            if (!DurationPattern.IsMatch(input))
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
#if NET8_0_OR_GREATER
                    result = context.Context.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'duration', but was '{input}'.");
#else
                    result = context.Context.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'duration', but was '{input.ToString()}'.");
#endif
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'duration'.");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context.WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'duration'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeEmail<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'email' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'email'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        ValidationContext result = validationContext;

        instance.AsString.TryGetValue(EmailValidator, new ValidationContextWrapper(result, level), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool EmailValidator(ReadOnlySpan<char> input, in ValidationContextWrapper context, out ValidationContext result)
        {
            result = context.Context;

            if (!EmailPattern.IsMatch(input))
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
#if NET8_0_OR_GREATER
                    result = context.Context.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'email', but was '{input}'.");
#else
                    result = context.Context.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'email', but was '{input.ToString()}'.");
#endif
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'email'.");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context.WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'email'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeRelativePointer<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'relative-json-pointer' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'relative-json-pointer'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        ValidationContext result = validationContext;

        instance.AsString.TryGetValue(RelativePointerValidator, new ValidationContextWrapper(result, level), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool RelativePointerValidator(ReadOnlySpan<char> input, in ValidationContextWrapper context, out ValidationContext result)
        {
            result = context.Context;

            if (!JsonRelativePointerPattern.IsMatch(input))
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
#if NET8_0_OR_GREATER
                    result = context.Context.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'relative-json-pointer', but was '{input}'.");
#else
                    result = context.Context.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'relative-json-pointer', but was '{input.ToString()}'.");
#endif
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'relative-json-pointer'.");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context.WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'relative-json-pointer'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypePointer<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'json-pointer' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'json-pointer'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        ValidationContext result = validationContext;

        instance.AsString.TryGetValue(PointerValidator, new ValidationContextWrapper(result, level), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool PointerValidator(ReadOnlySpan<char> input, in ValidationContextWrapper context, out ValidationContext result)
        {
            result = context.Context;

            if (!JsonPointerPattern.IsMatch(input))
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
#if NET8_0_OR_GREATER
                    result = context.Context.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'json-pointer', but was '{input}'.");
#else
                    result = context.Context.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'json-pointer', but was '{input.ToString()}'.");
#endif
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'json-pointer'.");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context.WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'json-pointer'.");
            }

            return true;
        }
    }

    /// <summary>
    /// Validate an array type value.
    /// </summary>
    /// <param name="valueKind">The actual value kind.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeArray(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level)
    {
        if (valueKind != JsonValueKind.Array)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'array' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'array'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
        else if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was 'array'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validate a boolean type value.
    /// </summary>
    /// <param name="valueKind">The actual value kind.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeBoolean(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level)
    {
        if (valueKind != JsonValueKind.True && valueKind != JsonValueKind.False)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'boolean' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'boolean'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
        else if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was 'boolean'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validate an object type value.
    /// </summary>
    /// <param name="valueKind">The actual value kind.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeObject(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level)
    {
        if (valueKind != JsonValueKind.Object)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'object' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'object'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }
        else if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was 'object'.");
        }

        return validationContext;
    }

    /// <summary>
    /// Validate an enumeration.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="enum1">The first enumeration value.</param>
    /// <param name="enum2">The second enumeration value.</param>
    /// <param name="enum3">The third enumeration value.</param>
    /// <param name="enum4">The fourth enumeration value.</param>
    /// <param name="enum5">The fifth enumeration value.</param>
    /// <param name="enum6">The sixth enumeration value.</param>
    /// <param name="enum7">The seventh enumeration value.</param>
    /// <param name="enum8">The eighth enumeration value.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext ValidateEnum<T>(in T value, in ValidationContext validationContext, ValidationLevel level, in T enum1, in T enum2, in T enum3, in T enum4, in T enum5, in T enum6, in T enum7, in T enum8)
        where T : struct, IJsonValue<T>
    {
        if (value.Equals(enum1))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum2))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum3))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum3}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum4))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum4}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum5))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum5}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum6))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum6}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum7))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum7}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum8))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum8}'.");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}, {enum3}, {enum4}, {enum5}, {enum6}, {enum7}, {enum8}].");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.");
        }
        else
        {
            return validationContext.WithResult(isValid: false);
        }
    }

    /// <summary>
    /// Validate an enumeration.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="enum1">The first enumeration value.</param>
    /// <param name="enum2">The second enumeration value.</param>
    /// <param name="enum3">The third enumeration value.</param>
    /// <param name="enum4">The fourth enumeration value.</param>
    /// <param name="enum5">The fifth enumeration value.</param>
    /// <param name="enum6">The sixth enumeration value.</param>
    /// <param name="enum7">The seventh enumeration value.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext ValidateEnum<T>(in T value, in ValidationContext validationContext, ValidationLevel level, in T enum1, in T enum2, in T enum3, in T enum4, in T enum5, in T enum6, in T enum7)
        where T : struct, IJsonValue<T>
    {
        if (value.Equals(enum1))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum2))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum3))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum3}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum4))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum4}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum5))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum5}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum6))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum6}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum7))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum7}'.");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}, {enum3}, {enum4}, {enum5}, {enum6}, {enum7}].");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.");
        }
        else
        {
            return validationContext.WithResult(isValid: false);
        }
    }

    /// <summary>
    /// Validate an enumeration.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="enum1">The first enumeration value.</param>
    /// <param name="enum2">The second enumeration value.</param>
    /// <param name="enum3">The third enumeration value.</param>
    /// <param name="enum4">The fourth enumeration value.</param>
    /// <param name="enum5">The fifth enumeration value.</param>
    /// <param name="enum6">The sixth enumeration value.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext ValidateEnum<T>(in T value, in ValidationContext validationContext, ValidationLevel level, in T enum1, in T enum2, in T enum3, in T enum4, in T enum5, in T enum6)
        where T : struct, IJsonValue<T>
    {
        if (value.Equals(enum1))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum2))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum3))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum3}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum4))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum4}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum5))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum5}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum6))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum6}'.");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}, {enum3}, {enum4}, {enum5}, {enum6}].");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.");
        }
        else
        {
            return validationContext.WithResult(isValid: false);
        }
    }

    /// <summary>
    /// Validate an enumeration.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="enum1">The first enumeration value.</param>
    /// <param name="enum2">The second enumeration value.</param>
    /// <param name="enum3">The third enumeration value.</param>
    /// <param name="enum4">The fourth enumeration value.</param>
    /// <param name="enum5">The fifth enumeration value.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext ValidateEnum<T>(in T value, in ValidationContext validationContext, ValidationLevel level, in T enum1, in T enum2, in T enum3, in T enum4, in T enum5)
        where T : struct, IJsonValue<T>
    {
        if (value.Equals(enum1))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum2))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum3))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum3}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum4))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum4}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum5))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum5}'.");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}, {enum3}, {enum4}, {enum5}].");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.");
        }
        else
        {
            return validationContext.WithResult(isValid: false);
        }
    }

    /// <summary>
    /// Validate an enumeration.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="enum1">The first enumeration value.</param>
    /// <param name="enum2">The second enumeration value.</param>
    /// <param name="enum3">The third enumeration value.</param>
    /// <param name="enum4">The fourth enumeration value.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext ValidateEnum<T>(in T value, in ValidationContext validationContext, ValidationLevel level, in T enum1, in T enum2, in T enum3, in T enum4)
        where T : struct, IJsonValue<T>
    {
        if (value.Equals(enum1))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum2))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum3))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum3}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum4))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum4}'.");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}, {enum3}, {enum4}].");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.");
        }
        else
        {
            return validationContext.WithResult(isValid: false);
        }
    }

    /// <summary>
    /// Validate an enumeration.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="enum1">The first enumeration value.</param>
    /// <param name="enum2">The second enumeration value.</param>
    /// <param name="enum3">The third enumeration value.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext ValidateEnum<T>(in T value, in ValidationContext validationContext, ValidationLevel level, in T enum1, in T enum2, in T enum3)
        where T : struct, IJsonValue<T>
    {
        if (value.Equals(enum1))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum2))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum3))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum3}'.");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}, {enum3}].");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.");
        }
        else
        {
            return validationContext.WithResult(isValid: false);
        }
    }

    /// <summary>
    /// Validate an enumeration.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="enum1">The first enumeration value.</param>
    /// <param name="enum2">The second enumeration value.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext ValidateEnum<T>(in T value, in ValidationContext validationContext, ValidationLevel level, in T enum1, in T enum2)
        where T : struct, IJsonValue<T>
    {
        if (value.Equals(enum1))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.");
            }

            return validationContext;
        }

        if (value.Equals(enum2))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}].");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.");
        }
        else
        {
            return validationContext.WithResult(isValid: false);
        }
    }

    /// <summary>
    /// Validate an enumeration.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="enum1">The first enumeration value.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext ValidateEnum<T>(in T value, in ValidationContext validationContext, ValidationLevel level, in T enum1)
        where T : struct, IJsonValue<T>
    {
        if (value.Equals(enum1))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}].");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.");
        }
        else
        {
            return validationContext.WithResult(isValid: false);
        }
    }

    /// <summary>
    /// Validate an enumeration.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="enums">The enumeration values.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext ValidateEnum<T>(in T value, in ValidationContext validationContext, ValidationLevel level, params T[] enums)
        where T : struct, IJsonValue<T>
    {
        foreach (T enumValue in enums)
        {
            if (value.Equals(enumValue))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum -  '{value}' matched '{enumValue}'.");
                }

                return validationContext;
            }
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum -  '{value}' did not match any of the required values: [{string.Join(", ", enums)}].");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.");
        }
        else
        {
            return validationContext.WithResult(isValid: false);
        }
    }

    /// <summary>
    /// Validate a const value.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="constValue">The const value.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext ValidateConst<T>(in T value, in ValidationContext validationContext, ValidationLevel level, in T constValue)
        where T : struct, IJsonValue<T>
    {
        if (value.Equals(constValue))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.3 const - '{value}' matched '{constValue}'.");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.3 const - '{value}' did not match the required value: {constValue}.");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.3 const - did not match the required value.");
        }
        else
        {
            return validationContext.WithResult(isValid: false);
        }
    }

    /// <summary>
    /// Perform numeric validation on the value.
    /// </summary>
    /// <typeparam name="TValue">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The current validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="multipleOf">The optional multiple-of validation.</param>
    /// <param name="maximum">The optional maximum validation.</param>
    /// <param name="exclusiveMaximum">The optional exclusive maximum validation.</param>
    /// <param name="minimum">The optional minimum validation.</param>
    /// <param name="exclusiveMinimum">The optional exclusive minimum validation.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext ValidateNumber<TValue>(in TValue value, in ValidationContext validationContext, ValidationLevel level, in BinaryJsonNumber multipleOf, in BinaryJsonNumber maximum, in BinaryJsonNumber exclusiveMaximum, in BinaryJsonNumber minimum, in BinaryJsonNumber exclusiveMinimum)
        where TValue : struct, IJsonValue
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            if (level == ValidationLevel.Verbose)
            {
                ValidationContext ignoredResult = validationContext;
                if (multipleOf.HasValue)
                {
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.2.1 multipleOf - ignored because the value is not a number");
                }

                if (maximum.HasValue)
                {
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.2.2 maximum- ignored because the value is not a number");
                }

                if (exclusiveMaximum.HasValue)
                {
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.2.3 exclusiveMaximum - ignored because the value is not a number");
                }

                if (minimum.HasValue)
                {
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.2.4 minimum - ignored because the value is not a number");
                }

                if (exclusiveMinimum.HasValue)
                {
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.2.5 exclusiveMinimum - ignored because the value is not a number");
                }

                return ignoredResult;
            }

            return validationContext;
        }

        ValidationContext result = validationContext;

        if (value.HasJsonElementBacking)
        {
            JsonElement number = value.AsNumber.AsJsonElement;
            if (multipleOf.HasValue)
            {
                if (BinaryJsonNumber.IsMultipleOf(number, multipleOf))
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.1 multipleOf -  {value} was a multiple of {multipleOf}.");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.1 multipleOf -  {value} was not a multiple of {multipleOf}.");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.1 multipleOf - was not a multiple of the required value.");
                    }
                    else
                    {
                        return validationContext.WithResult(isValid: false);
                    }
                }
            }

            if (maximum.HasValue)
            {
                if (maximum >= number)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.2 maximum -  {value} was less than or equal to {maximum}.");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.2 maximum -  {value} was greater than {maximum}.");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.2 maximum - was greater than the required value.");
                    }
                    else
                    {
                        return validationContext.WithResult(isValid: false);
                    }
                }
            }

            if (exclusiveMaximum.HasValue)
            {
                if (exclusiveMaximum > number)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.3 exclusiveMaximum -  {value} was less than {exclusiveMaximum}.");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.3 exclusiveMaximum -  {value} was greater than or equal to {exclusiveMaximum}.");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.3 exclusiveMaximum - was greater than or equal to the required value.");
                    }
                    else
                    {
                        return validationContext.WithResult(isValid: false);
                    }
                }
            }

            if (minimum.HasValue)
            {
                if (minimum <= number)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.4 minimum -  {value} was greater than or equal to {minimum}.");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.4 minimum - {value} was less than {minimum}.");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.4 minimum - was less than the required value.");
                    }
                    else
                    {
                        return validationContext.WithResult(isValid: false);
                    }
                }
            }

            if (exclusiveMinimum.HasValue)
            {
                if (exclusiveMinimum < number)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.5 exclusiveMinimum -  {value} was greater than {exclusiveMinimum}.");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.5 exclusiveMinimum -  {value} was less than or equal to {exclusiveMinimum}.");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.5 exclusiveMinimum - was less than or equal to the required value.");
                    }
                    else
                    {
                        return validationContext.WithResult(isValid: false);
                    }
                }
            }
        }
        else
        {
            BinaryJsonNumber currentValue = value.AsNumber.AsBinaryJsonNumber;
            if (multipleOf.HasValue)
            {
                if (currentValue.IsMultipleOf(multipleOf))
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.1 multipleOf -  {currentValue} was a multiple of {multipleOf}.");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.1 multipleOf -  {currentValue} was not a multiple of {multipleOf}.");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.1 multipleOf - was not a multiple of the required value.");
                    }
                    else
                    {
                        return validationContext.WithResult(isValid: false);
                    }
                }
            }

            if (maximum.HasValue)
            {
                if (currentValue <= maximum)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.2 maximum -  {currentValue} was less than or equal to {maximum}.");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.2 maximum -  {currentValue} was greater than {maximum}.");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.2 maximum - was greater than the required value.");
                    }
                    else
                    {
                        return validationContext.WithResult(isValid: false);
                    }
                }
            }

            if (exclusiveMaximum.HasValue)
            {
                if (currentValue < exclusiveMaximum)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.3 exclusiveMaximum -  {currentValue} was less than {exclusiveMaximum}.");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.3 exclusiveMaximum -  {currentValue} was greater than or equal to {exclusiveMaximum}.");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.3 exclusiveMaximum - was greater than or equal to the required value.");
                    }
                    else
                    {
                        return validationContext.WithResult(isValid: false);
                    }
                }
            }

            if (minimum.HasValue)
            {
                if (currentValue >= minimum)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.4 minimum -  {currentValue} was greater than or equal to {minimum}.");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.4 minimum - {currentValue} was less than {minimum}.");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.4 minimum - was less than the required value.");
                    }
                    else
                    {
                        return validationContext.WithResult(isValid: false);
                    }
                }
            }

            if (exclusiveMinimum.HasValue)
            {
                if (currentValue > exclusiveMinimum)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.5 exclusiveMinimum -  {currentValue} was greater than {exclusiveMinimum}.");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.5 exclusiveMinimum -  {currentValue} was less than or equal to {exclusiveMinimum}.");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.5 exclusiveMinimum - was less than or equal to the required value.");
                    }
                    else
                    {
                        return validationContext.WithResult(isValid: false);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Validates a string value.
    /// </summary>
    /// <typeparam name="TValue">The type of the <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The instance to validate.</param>
    /// <param name="validationContext">The current validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="maxLength">The optional maxLength validation.</param>
    /// <param name="minLength">The optional minLenth validation.</param>
    /// <param name="pattern">The optional pattern validation.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext ValidateString<TValue>(in TValue value, in ValidationContext validationContext, ValidationLevel level, int? maxLength, int? minLength, Regex? pattern)
        where TValue : struct, IJsonValue
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            if (level == ValidationLevel.Verbose)
            {
                ValidationContext ignoredResult = validationContext;
                if (maxLength is not null)
                {
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.3.1 maxLength - ignored because the value is not a string");
                }

                if (minLength is not null)
                {
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.3.2 minLength - ignored because the value is not a string");
                }

                if (pattern is not null)
                {
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.3.3 pattern - ignored because the value is not a string");
                }

                return ignoredResult;
            }

            return validationContext;
        }

        ValidationContext result = validationContext;

        if (maxLength is not null || minLength is not null || pattern is not null)
        {
            value.AsString.TryGetValue(StringValidator, new StringValidationContextWrapper(result, level, minLength, maxLength, pattern), out result);
            if (level == ValidationLevel.Flag && !result.IsValid)
            {
                return result;
            }
        }

        return result;

        static bool StringValidator(ReadOnlySpan<char> input, in StringValidationContextWrapper context, out ValidationContext result)
        {
            int? length = null;
            result = context.Context;

            if (context.MaxLength is int maxl)
            {
                length ??= CountRunes(input);
                if (length <= maxl)
                {
                    if (context.Level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.3.1 maxLength - {input.ToString()} of {length} was less than or equal to {maxl}.");
                    }
                }
                else
                {
                    if (context.Level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.3.1 maxLength - {input.ToString()} of {length} was greater than {maxl}.");
                    }
                    else if (context.Level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.3.1 maxLength - was greater than the required length.");
                    }
                    else
                    {
                        result = context.Context.WithResult(isValid: false);
                        return true;
                    }
                }
            }

            if (context.MinLength is int minl)
            {
                length ??= CountRunes(input);
                if (length >= minl)
                {
                    if (context.Level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.3.2 minLength - {input.ToString()} of {length} was greater than or equal to {minl}.");
                    }
                }
                else
                {
                    if (context.Level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.3.2 minLength - {input.ToString()} of {length} was less than {minl}.");
                    }
                    else if (context.Level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.3.2 minLength - was less than the required length.");
                    }
                    else
                    {
                        result = context.Context.WithResult(isValid: false);
                        return true;
                    }
                }
            }

            if (context.Pattern is Regex prex)
            {
                if (prex.IsMatch(input))
                {
                    if (context.Level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.3.3 pattern - {input.ToString()} matched {prex}.");
                    }
                }
                else
                {
                    if (context.Level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.3.3 pattern - {input.ToString()} did not match {prex}.");
                    }
                    else if (context.Level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.3.13 pattern - did not match the required pattern.");
                    }
                    else
                    {
                        result = result.WithResult(isValid: false);
                    }
                }
            }

            return true;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static int CountRunes(ReadOnlySpan<char> str)
            {
#if NET8_0_OR_GREATER
                int length = 0;
                SpanRuneEnumerator enumerator = str.EnumerateRunes();
                while (enumerator.MoveNext())
                {
                    length++;
                }

                return length;
#else
                return StringInfo.GetTextLengthInRunes(str);
#endif
            }
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

        JsonValueKind valueKind = value.ValueKind;

        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return result.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' withcontentMediaType 'application/json' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return result.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with contentMediaType 'application/json'.");
            }
            else
            {
                return result.WithResult(isValid: false);
            }
        }

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
                return result.WithResult(isValid: false);
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

        JsonValueKind valueKind = value.ValueKind;

        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return result.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with contentEncoding 'base64' and contentMediaType 'application/json' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return result.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with contentEncoding 'base64' and contentMediaType 'application/json'.");
            }
            else
            {
                return result.WithResult(isValid: false);
            }
        }

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

        JsonValueKind valueKind = value.ValueKind;

        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return result.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with contentEncoding 'base64' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return result.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with contentEncoding 'base64'.");
            }
            else
            {
                return result.WithResult(isValid: false);
            }
        }

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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeRegex<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'regex' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'regex'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        JsonRegex regexInstance = instance.As<JsonRegex>();

        if (!regexInstance.TryGetRegex(out Regex _))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'regex' but was '{regexInstance}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'regex'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'regex'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeIriReference<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'iri-reference' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'iri-reference'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

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
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'iri-reference' but was '{iri}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'iri-reference'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'iri-reference'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeIri<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'iri' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'iri'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        JsonIri iri = instance.As<JsonIri>();

        if (!iri.TryGetUri(out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'iri' but was '{iri}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'iri'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'iri'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUri<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'uri' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'uri'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        JsonUri uriInstance = instance.As<JsonUri>();

        if (!(uriInstance.TryGetUri(out Uri? testUri) && (!testUri.IsAbsoluteUri || !testUri.IsUnc)))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'uri' but was '{uriInstance}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'uri'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'uri'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUriReference<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'uri-reference' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'uri-reference'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

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
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'uri-reference' but was '{uriReferenceInstance}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'uri-reference'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'uri-reference'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeTime<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'date' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'date'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        JsonTime time = instance.As<JsonTime>();

        if (!time.TryGetTime(out _))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'time' but was '{time}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'time'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'time'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeDate<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'date' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'date'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        JsonDate date = instance.As<JsonDate>();

        if (!date.TryGetDate(out _))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'date' but was '{date}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'date'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'date'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeIpV6<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'ipv6' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'ipv6'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        ValidationContext result = validationContext;

        instance.AsString.TryGetValue(IPV6Validator, new ValidationContextWrapper(result, level), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool IPV6Validator(ReadOnlySpan<char> input, in ValidationContextWrapper context, out ValidationContext result)
        {
            result = context.Context;

            if (JsonIpV6.IPAddressParser(input, null, out IPAddress? address) &&
                address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 &&
                !ZoneIdExpression.IsMatch(input))
            {
                if (context.Level == ValidationLevel.Verbose)
                {
                    result = context.Context.WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'ipv6'.");
                }

                return true;
            }
            else
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
#if NET8_0_OR_GREATER
                    result = context.Context.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'ipv6', but was '{input}'.");
#else
                    result = context.Context.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'ipv6', but was '{input.ToString()}'.");
#endif
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'ipv6'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeIpV4<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'ipv4' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'ipv4'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        ValidationContext result = validationContext;
        instance.AsString.TryGetValue(IPV6Validator, new ValidationContextWrapper(result, level), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool IPV6Validator(ReadOnlySpan<char> input, in ValidationContextWrapper context, out ValidationContext result)
        {
            result = context.Context;

            if (JsonIpV4.IPAddressParser(input, null, out IPAddress? address) &&
                address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                IpV4Pattern.IsMatch(input))
            {
                if (context.Level == ValidationLevel.Verbose)
                {
                    result = context.Context.WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'ipv4'.");
                }

                return true;
            }
            else
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
#if NET8_0_OR_GREATER
                    result = context.Context.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'ipv4', but was '{input}'.");
#else
                    result = context.Context.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'ipv4', but was '{input.ToString()}'.");
#endif
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'ipv4'.");
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
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeDateTime<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'datetime' but was '{valueKind}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'datetime'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        JsonDateTime date = instance.As<JsonDateTime>();

        if (!date.TryGetDateTime(out _))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'datetime' but was '{date}'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'datetime'.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'datetime'.");
        }

        return validationContext;
    }

#if NET8_0_OR_GREATER
    [GeneratedRegex("^(?:(?:^|\\.)(?:2(?:5[0-5]|[0-4]\\d)|1?\\d?\\d)){4}$", RegexOptions.Compiled)]
    private static partial Regex CreateIpV4Pattern();

    [GeneratedRegex("%.*$", RegexOptions.Compiled)]
    private static partial Regex CreateZoneIdExpression();

    [GeneratedRegex("^(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*|\"(?:[ \\x01-\\x08\\x0b\\x0c\\x0e-\\x1f\\x21\\x23-\\x5b\\x5d-\\x7f]|\\\\[\\x01-\\x09\\x0b\\x0c\\x0e-\\x7f])*\")@(?:(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?|\\[(((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|IPv6:(([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])))\\])$", RegexOptions.Compiled)]
    private static partial Regex CreateEmailPattern();

    [GeneratedRegex("^P(?!$)((\\d+(?:\\.\\d+)?Y)?(\\d+(?:\\.\\d+)?M)?|(\\d+(?:\\.\\d+)?W)?)?(\\d+(?:\\.\\d+)?D)?(T(?=\\d)(\\d+(?:\\.\\d+)?H)?(\\d+(?:\\.\\d+)?M)?(\\d+(?:\\.\\d+)?S)?)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ECMAScript)]
    private static partial Regex CreateDurationPattern();

    [GeneratedRegex("^(?=.{1,255}$)((?!_)\\w)((((?!_)\\w)|\\b-){0,61}((?!_)\\w))?(\\.((?!_)\\w)((((?!_)\\w)|\\b-){0,61}((?!_)\\w))?)*\\.?$", RegexOptions.Compiled)]
    private static partial Regex CreateHostnamePattern();

    [GeneratedRegex("(^[\\p{Mn}\\p{Mc}\\p{Me}\\u302E\\u00b7])|.*\\u302E.*|.*[^l]\\u00b7.*|.*\\u00b7[^l].*|.*\\u00b7$|\\u0374$|\\u0375$|\\u0374[^\\p{IsGreekandCoptic}]|\\u0375[^\\p{IsGreekandCoptic}]|^\\u05F3|[^\\p{IsHebrew}]\\u05f3|^\\u05f4|[^\\p{IsHebrew}]\\u05f4|[\\u0660-\\u0669][\\u06F0-\\u06F9]|[\\u06F0-\\u06F9][\\u0660-\\u0669]|^\\u200D|[^\\uA953\\u094d\\u0acd\\u0c4d\\u0d3b\\u09cd\\u0a4d\\u0b4d\\u0bcd\\u0ccd\\u0d4d\\u1039\\u0d3c\\u0eba\\ua8f3\\ua8f4]\\u200D|^\\u30fb$|[^\\p{IsHiragana}\\p{IsKatakana}\\p{IsCJKUnifiedIdeographs}]\\u30fb|\\u30fb[^\\p{IsHiragana}\\p{IsKatakana}\\p{IsCJKUnifiedIdeographs}]|[\\u0640\\u07fa\\u3031\\u3032\\u3033\\u3034\\u3035\\u302e\\u302f\\u303b]|..--", RegexOptions.Compiled)]
    private static partial Regex CreateInvalidIdnHostNamePattern();

    [GeneratedRegex("^([^\\x00-\\x20\\x7f\"'%<>\\\\^`{|}]|%[0-9A-Fa-f]{2}|{[+#./;?&=,!@|]?((\\w|%[0-9A-Fa-f]{2})(\\.?(\\w|%[0-9A-Fa-f]{2}))*(:[1-9]\\d{0,3}|\\*)?)(,((\\w|%[0-9A-Fa-f]{2})(\\.?(\\w|%[0-9A-Fa-f]{2}))*(:[1-9]\\d{0,3}|\\*)?))*})*$", RegexOptions.Compiled)]
    private static partial Regex CreateUriTemplatePattern();

    [GeneratedRegex("[0-9a-fA-F]{8}\\-[0-9a-fA-F]{4}\\-[0-9a-fA-F]{4}\\-[0-9a-fA-F]{4}\\-[0-9a-fA-F]{12}", RegexOptions.Compiled)]
    private static partial Regex CreateUuidTemplatePattern();

    [GeneratedRegex("^((/(([^/~])|(~[01]))*))*$", RegexOptions.Compiled)]
    private static partial Regex CreateJsonPointerPattern();

    [GeneratedRegex("^(0|[1-9][0-9]*)(#|(/(/|[^/~]|(~[01]))*))?$", RegexOptions.Compiled)]
    private static partial Regex CreateJsonRelativePointerPattern();

    [GeneratedRegex("(@)(.+)$", RegexOptions.Compiled)]
    private static partial Regex CreateIdnEmailReplacePattern();

    [GeneratedRegex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", RegexOptions.Compiled)]
    private static partial Regex CreateIdnEmailMatchPattern();
#else
    private static Regex CreateIpV4Pattern() => new("^(?:(?:^|\\.)(?:2(?:5[0-5]|[0-4]\\d)|1?\\d?\\d)){4}$", RegexOptions.Compiled);

    private static Regex CreateZoneIdExpression() => new("%.*$", RegexOptions.Compiled);

    private static Regex CreateEmailPattern() => new("^(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*|\"(?:[ \\x01-\\x08\\x0b\\x0c\\x0e-\\x1f\\x21\\x23-\\x5b\\x5d-\\x7f]|\\\\[\\x01-\\x09\\x0b\\x0c\\x0e-\\x7f])*\")@(?:(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?|\\[(((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|IPv6:(([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])))\\])$", RegexOptions.Compiled);

    private static Regex CreateDurationPattern() => new("^P(?!$)((\\d+(?:\\.\\d+)?Y)?(\\d+(?:\\.\\d+)?M)?|(\\d+(?:\\.\\d+)?W)?)?(\\d+(?:\\.\\d+)?D)?(T(?=\\d)(\\d+(?:\\.\\d+)?H)?(\\d+(?:\\.\\d+)?M)?(\\d+(?:\\.\\d+)?S)?)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ECMAScript);

    private static Regex CreateHostnamePattern() => new("^(?=.{1,255}$)((?!_)\\w)((((?!_)\\w)|\\b-){0,61}((?!_)\\w))?(\\.((?!_)\\w)((((?!_)\\w)|\\b-){0,61}((?!_)\\w))?)*\\.?$", RegexOptions.Compiled);

    private static Regex CreateInvalidIdnHostNamePattern() => new("(^[\\p{Mn}\\p{Mc}\\p{Me}\\u302E\\u00b7])|.*\\u302E.*|.*[^l]\\u00b7.*|.*\\u00b7[^l].*|.*\\u00b7$|\\u0374$|\\u0375$|\\u0374[^\\p{IsGreekandCoptic}]|\\u0375[^\\p{IsGreekandCoptic}]|^\\u05F3|[^\\p{IsHebrew}]\\u05f3|^\\u05f4|[^\\p{IsHebrew}]\\u05f4|[\\u0660-\\u0669][\\u06F0-\\u06F9]|[\\u06F0-\\u06F9][\\u0660-\\u0669]|^\\u200D|[^\\uA953\\u094d\\u0acd\\u0c4d\\u0d3b\\u09cd\\u0a4d\\u0b4d\\u0bcd\\u0ccd\\u0d4d\\u1039\\u0d3c\\u0eba\\ua8f3\\ua8f4]\\u200D|^\\u30fb$|[^\\p{IsHiragana}\\p{IsKatakana}\\p{IsCJKUnifiedIdeographs}]\\u30fb|\\u30fb[^\\p{IsHiragana}\\p{IsKatakana}\\p{IsCJKUnifiedIdeographs}]|[\\u0640\\u07fa\\u3031\\u3032\\u3033\\u3034\\u3035\\u302e\\u302f\\u303b]|..--", RegexOptions.Compiled);

    private static Regex CreateUriTemplatePattern() => new("^([^\\x00-\\x20\\x7f\"'%<>\\\\^`{|}]|%[0-9A-Fa-f]{2}|{[+#./;?&=,!@|]?((\\w|%[0-9A-Fa-f]{2})(\\.?(\\w|%[0-9A-Fa-f]{2}))*(:[1-9]\\d{0,3}|\\*)?)(,((\\w|%[0-9A-Fa-f]{2})(\\.?(\\w|%[0-9A-Fa-f]{2}))*(:[1-9]\\d{0,3}|\\*)?))*})*$", RegexOptions.Compiled);

    private static Regex CreateUuidTemplatePattern() => new("[0-9a-fA-F]{8}\\-[0-9a-fA-F]{4}\\-[0-9a-fA-F]{4}\\-[0-9a-fA-F]{4}\\-[0-9a-fA-F]{12}", RegexOptions.Compiled);

    private static Regex CreateJsonPointerPattern() => new("^((/(([^/~])|(~[01]))*))*$", RegexOptions.Compiled);

    private static Regex CreateJsonRelativePointerPattern() => new("^(0|[1-9][0-9]*)(#|(/(/|[^/~]|(~[01]))*))?$", RegexOptions.Compiled);

    private static Regex CreateIdnEmailReplacePattern() => new("(@)(.+)$", RegexOptions.Compiled);

    private static Regex CreateIdnEmailMatchPattern() => new("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", RegexOptions.Compiled);
#endif

#if NET8_0_OR_GREATER
    private readonly record struct ValidationContextWrapper(in ValidationContext Context, ValidationLevel Level);

    private readonly record struct StringValidationContextWrapper(in ValidationContext Context, ValidationLevel Level, int? MinLength, int? MaxLength, Regex? Pattern);
#else
    private readonly struct ValidationContextWrapper
    {
        public ValidationContextWrapper(in ValidationContext context, ValidationLevel level)
        {
            this.Context = context;
            this.Level = level;
        }

        public ValidationContext Context { get; }

        public ValidationLevel Level { get; }
    }

    private readonly struct StringValidationContextWrapper
    {
        public StringValidationContextWrapper(in ValidationContext context, ValidationLevel level, int? minLength, int? maxLength, Regex? pattern)
        {
            this.Context = context;
            this.Level = level;
            this.MinLength = minLength;
            this.MaxLength = maxLength;
            this.Pattern = pattern;
        }

        public ValidationContext Context { get; }

        public ValidationLevel Level { get; }

        public int? MinLength { get; }

        public int? MaxLength { get; }

        public Regex? Pattern { get; }
    }
#endif
}