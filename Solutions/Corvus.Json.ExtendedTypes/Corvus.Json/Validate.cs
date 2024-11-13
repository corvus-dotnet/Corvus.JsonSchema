// <copyright file="Validate.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// JsonSchema validation errors.
/// </summary>
public static partial class Validate
{
    /// <summary>
    /// Gets the pattern for ipv4.
    /// </summary>
    internal static readonly Regex IpV4Pattern = CreateIpV4Pattern();

    /// <summary>
    /// Gets the pattern for a ZoneId.
    /// </summary>
    internal static readonly Regex ZoneIdExpression = CreateZoneIdExpression();

    /// <summary>
    /// Gets the pattern for an email address.
    /// </summary>
    internal static readonly Regex EmailPattern = CreateEmailPattern();

    /// <summary>
    /// Gets the pattern for a duration.
    /// </summary>
    internal static readonly Regex DurationPattern = CreateDurationPattern(); // ECMAScript mode stops \d matching non-ASCII digits

    /// <summary>
    /// Gets the pattern for a Hostname.
    /// </summary>
    internal static readonly Regex HostnamePattern = CreateHostnamePattern();

    /// <summary>
    /// Gets the pattern for a an invalid IDN Hostname.
    /// </summary>
    internal static readonly Regex InvalidIdnHostNamePattern = CreateInvalidIdnHostNamePattern();

    /// <summary>
    /// Gets the pattern for a UriTemplate.
    /// </summary>
    internal static readonly Regex UriTemplatePattern = CreateUriTemplatePattern();

    /// <summary>
    /// Gets the pattern for a UUID.
    /// </summary>
    internal static readonly Regex UuidTemplatePattern = CreateUuidTemplatePattern();

    /// <summary>
    /// Gets the pattern for a JSON Pointer.
    /// </summary>
    internal static readonly Regex JsonPointerPattern = CreateJsonPointerPattern();

    /// <summary>
    /// Gets the pattern for a JSON Relative Pointer.
    /// </summary>
    internal static readonly Regex JsonRelativePointerPattern = CreateJsonRelativePointerPattern();

    /// <summary>
    /// Gets the pattern for fixing an IDN Email.
    /// </summary>
    internal static readonly Regex IdnEmailReplacePattern = CreateIdnEmailReplacePattern();

    /// <summary>
    /// Gets the pattern for matching and IDN Email.
    /// </summary>
    internal static readonly Regex IdnEmailMatchPattern = CreateIdnEmailMatchPattern();

    /// <summary>
    /// Gets the standard IDN mapping.
    /// </summary>
    internal static readonly IdnMapping IdnMapping = new() { AllowUnassigned = true, UseStd3AsciiRules = true };

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
            return validationContext.WithResult(isValid: true, "Validation type - was 'string'.", typeKeyword ?? "type");
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
            return validationContext.WithResult(isValid: true, "Validation type - was 'number'.", typeKeyword ?? "type");
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
            return validationContext.WithResult(isValid: true, "Validation type - was 'null'.", typeKeyword ?? "type");
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
        else
        {
            double value = (double)instance.AsNumber;
            if (value != Math.Floor(value))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation {typeKeyword ?? "type"} - should have been 'integer' but was '{value}'.", typeKeyword ?? "type");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation type - should have been 'integer' with zero fractional part.", typeKeyword ?? "type");
                }
                else
                {
                    return ValidationContext.InvalidContext;
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext.WithResult(isValid: true, "Validation type - was 'number' with zero fractional part.", typeKeyword ?? "type");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeByte<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation {typeKeyword ?? "type"} - should have been 'number' but was '{valueKind}'.", typeKeyword ?? "type");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation type - should have been 'number'.,", typeKeyword ?? "type");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }
        else
        {
            double value = (double)instance.AsNumber;
            if (value != Math.Floor(value) || value > byte.MaxValue || value < byte.MinValue)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'byte' but was '{valueKind}' with value {value} and fractional part {value - Math.Floor(value)}.", formatKeyword ?? "format");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation format - should have been 'byte'.", formatKeyword ?? "format");
                }
                else
                {
                    return ValidationContext.InvalidContext;
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'number'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'byte'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeSByte<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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
        else
        {
            double value = (double)instance.AsNumber;
            if (value != Math.Floor(value) || value > sbyte.MaxValue || value < sbyte.MinValue)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'sbyte' but was '{valueKind}' with value {value} and fractional part {value - Math.Floor(value)}.", formatKeyword ?? "format");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation format - should have been sbyte 'number'.", formatKeyword ?? "format");
                }
                else
                {
                    return ValidationContext.InvalidContext;
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'number'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'sbyte'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeInt16<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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
        else
        {
            double value = (double)instance.AsNumber;
            if (value != Math.Floor(value) || value > short.MaxValue || value < short.MinValue)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'int16' but was '{value}'.", formatKeyword ?? "format");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation format - should have been 'int16'.", formatKeyword ?? "format");
                }
                else
                {
                    return ValidationContext.InvalidContext;
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'number'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'int16'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUInt16<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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
        else
        {
            double value = (double)instance.AsNumber;
            if (value != Math.Floor(value) || value > ushort.MaxValue || value < ushort.MinValue)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'uint16' but was '{value}.", formatKeyword ?? "format");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation format - should have been 'uint16'.", formatKeyword ?? "format");
                }
                else
                {
                    return ValidationContext.InvalidContext;
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'number'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'uint16'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeInt32<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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
        else
        {
            double value = (double)instance.AsNumber;
            if (value != Math.Floor(value) || value > int.MaxValue || value < int.MinValue)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'int32 but was {value}.", formatKeyword ?? "format");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation format - should have been 'int32'.", formatKeyword ?? "format");
                }
                else
                {
                    return ValidationContext.InvalidContext;
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'number'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'int32'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUInt32<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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
        else
        {
            double value = (double)instance.AsNumber;
            if (value != Math.Floor(value) || value > uint.MaxValue || value < uint.MinValue)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'uint32' but was {value}.", formatKeyword ?? "format");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation format - should have been 'uint32'.", formatKeyword ?? "format");
                }
                else
                {
                    return ValidationContext.InvalidContext;
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'number'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'uint32.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeInt64<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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
        else
        {
            JsonNumber number = instance.AsNumber;
            double value = (double)number;
            if (value != Math.Floor(value) || (instance.HasJsonElementBacking && !instance.AsJsonElement.TryGetInt64(out long _)))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'int64' but was {value}.", formatKeyword ?? "format");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation format - should have been 'int64'.", formatKeyword ?? "format");
                }
                else
                {
                    return ValidationContext.InvalidContext;
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'number'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'int64'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUInt64<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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
        else
        {
            JsonNumber number = instance.AsNumber;
            double value = (double)number;
            if (value != Math.Floor(value) || value < 0 || (instance.HasJsonElementBacking && !instance.AsJsonElement.TryGetUInt64(out ulong _)))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been '' but was {value}.", formatKeyword ?? "format");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation format - should have been 'uint64'.", formatKeyword ?? "format");
                }
                else
                {
                    return ValidationContext.InvalidContext;
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'number'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'uint64'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeInt128<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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
#if NET8_0_OR_GREATER
        else
        {
            try
            {
                JsonNumber number = instance.AsNumber;
                _ = (Int128)number;

                if (number.HasDotnetBacking && !BinaryJsonNumber.IsInteger(number.AsBinaryJsonNumber))
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        double value = (double)instance.AsNumber;
                        return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'int128' but was {value}.", formatKeyword ?? "format");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        return validationContext.WithResult(isValid: false, "Validation format - should have been 'int128'.", formatKeyword ?? "format");
                    }
                    else
                    {
                        return ValidationContext.InvalidContext;
                    }
                }
            }
            catch (FormatException)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    double value = (double)instance.AsNumber;
                    return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'int128' but was {value}.", formatKeyword ?? "format");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation format - should have been 'int128'.", formatKeyword ?? "format");
                }
                else
                {
                    return ValidationContext.InvalidContext;
                }
            }
        }
#else
        else
        {
            // In net4.8 we only support 64 bits of precision.
            JsonNumber number = instance.AsNumber;
            double value = (double)number;
            if (value != Math.Floor(value))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'int128' but was {value}.", formatKeyword ?? "format");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation format - should have been 'int128'.", formatKeyword ?? "format");
                }
                else
                {
                    return ValidationContext.InvalidContext;
                }
            }
        }
#endif

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'number'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'int128'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUInt128<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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
#if NET8_0_OR_GREATER
        else
        {
            try
            {
                JsonNumber number = instance.AsNumber;
                _ = (UInt128)number;
                if (number.HasDotnetBacking && !BinaryJsonNumber.IsInteger(number.AsBinaryJsonNumber))
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        double value = (double)instance.AsNumber;
                        return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'uint128' but was {value}.", formatKeyword ?? "format");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        return validationContext.WithResult(isValid: false, "Validation format - should have been 'uint128'.", formatKeyword ?? "format");
                    }
                    else
                    {
                        return ValidationContext.InvalidContext;
                    }
                }
            }
            catch
            {
                if (level >= ValidationLevel.Detailed)
                {
                    double value = (double)instance.AsNumber;
                    return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'uint128' but was {value}.", formatKeyword ?? "format");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation format - should have been 'uint128'.", formatKeyword ?? "format");
                }
                else
                {
                    return ValidationContext.InvalidContext;
                }
            }
        }
#else
        double value = (double)instance.AsNumber;
        if (value != Math.Floor(value) || value < 0)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'uint128' but was {value} .", formatKeyword ?? "format");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation format - should have been 'uint128'.", formatKeyword ?? "format");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }
#endif

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'number'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'uint128'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeHalf<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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
        else
        {
            double value = (double)instance.AsNumber;
            if (value < -65504 || value > 65504)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'half' but was {value}.", formatKeyword ?? "format");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation format - should have been 'half'.", formatKeyword ?? "format");
                }
                else
                {
                    return ValidationContext.InvalidContext;
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'number'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'half'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeSingle<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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
                    return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'single' but was {value}.", formatKeyword ?? "format");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation format - should have been 'single.", formatKeyword ?? "format");
                }
                else
                {
                    return ValidationContext.InvalidContext;
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'number'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'single'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeDouble<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
        if (valueKind != JsonValueKind.Number)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation type - should have been 'number' but was '{valueKind}'.", typeKeyword ?? "type");
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

        try
        {
            _ = (double)instance.AsNumber;
        }
        catch (FormatException)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'double' but was {instance}.", formatKeyword ?? "format");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation format - should have been 'double'.", formatKeyword ?? "format");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'number'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'double'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeDecimal<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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
        else
        {
            try
            {
                _ = (decimal)instance.AsNumber;
            }
            catch (FormatException)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'decimal' but was {instance}.", formatKeyword ?? "format");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation format - should have been 'decimal'.", formatKeyword ?? "format");
                }
                else
                {
                    return ValidationContext.InvalidContext;
                }
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"}- was 'number'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'decimal'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUriTemplate<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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

        ValidationContext result;
        if (level >= ValidationLevel.Verbose)
        {
            result = validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type");
        }
        else
        {
            result = validationContext;
        }

        instance.AsString.TryGetValue(UriTemplateValidator, new ValidationContextWrapperWithFormatKeyword(result, level, formatKeyword), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool UriTemplateValidator(ReadOnlySpan<char> input, in ValidationContextWrapperWithFormatKeyword context, out ValidationContext result)
        {
            result = context.Context;

            if (!UriTemplatePattern.IsMatch(input))
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
#if NET8_0_OR_GREATER
                    result = context.Context.WithResult(isValid: false, $"Validation {context.FormatKeyword ?? "format"} - should have been 'uri-template', but was '{input}'.", context.FormatKeyword ?? "format");
#else
                    result = context.Context.WithResult(isValid: false, $"Validation {context.FormatKeyword ?? "format"} - should have been 'uri-template', but was '{input.ToString()}'.", context.FormatKeyword ?? "format");
#endif
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation format - should have been 'uri-template'.", context.FormatKeyword ?? "format");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context
                    .WithResult(isValid: true, $"Validation {context.FormatKeyword ?? "format"} - was 'uri-template'.", context.FormatKeyword);
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeIdnEmail<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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
                return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'idn-email', but was '{email}'.", formatKeyword ?? "format");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation format - should have been 'idn-email'.", formatKeyword ?? "format");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'idn-email'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeIdnHostName<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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
                return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'idn-hostname', but was '{value}'.", formatKeyword ?? "format");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation format - should have been 'idn-hostname'.", formatKeyword ?? "format");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'idn-hostname'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeHostname<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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

        ValidationContext result;
        if (level >= ValidationLevel.Verbose)
        {
            result = validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type");
        }
        else
        {
            result = validationContext;
        }

        instance.AsString.TryGetValue(HostnameValidator, new ValidationContextWrapperWithFormatKeyword(result, level, formatKeyword), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool HostnameValidator(ReadOnlySpan<char> input, in ValidationContextWrapperWithFormatKeyword context, out ValidationContext result)
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
                    result = context.Context.WithResult(isValid: false, $"Validation {context.FormatKeyword ?? "format"} - should have been 'hostname', but was '{input.ToString()}'.", context.FormatKeyword ?? "format");
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation format - should have been 'hostname'.", context.FormatKeyword ?? "format");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context
                    .WithResult(isValid: true, $"Validation {context.FormatKeyword ?? "format"} - was 'hostname'.", context.FormatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUuid<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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

        ValidationContext result;
        if (level >= ValidationLevel.Verbose)
        {
            result = validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type");
        }
        else
        {
            result = validationContext;
        }

        instance.AsString.TryGetValue(UuidValidator, new ValidationContextWrapperWithFormatKeyword(result, level, formatKeyword), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool UuidValidator(ReadOnlySpan<char> input, in ValidationContextWrapperWithFormatKeyword context, out ValidationContext result)
        {
            result = context.Context;

            if (!UuidTemplatePattern.IsMatch(input))
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
                    result = context.Context.WithResult(isValid: false, $"Validation {context.FormatKeyword ?? "format"} - should have been 'uuid', but was '{input.ToString()}'.", context.FormatKeyword ?? "format");
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation format - should have been 'uuid'.", context.FormatKeyword ?? "format");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context.WithResult(isValid: true, $"Validation {context.FormatKeyword ?? "format"} - was 'uuid'.", context.FormatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeDuration<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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

        ValidationContext result;
        if (level >= ValidationLevel.Verbose)
        {
            result = validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type");
        }
        else
        {
            result = validationContext;
        }

        instance.AsString.TryGetValue(DurationValidator, new ValidationContextWrapperWithFormatKeyword(result, level, formatKeyword), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool DurationValidator(ReadOnlySpan<char> input, in ValidationContextWrapperWithFormatKeyword context, out ValidationContext result)
        {
            result = context.Context;

            if (!DurationPattern.IsMatch(input))
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
#if NET8_0_OR_GREATER
                    result = context.Context.WithResult(isValid: false, $"Validation {context.FormatKeyword ?? "format"} - should have been 'duration', but was '{input}'.", context.FormatKeyword ?? "format");
#else
                    result = context.Context.WithResult(isValid: false, $"Validation {context.FormatKeyword ?? "format"} - should have been 'duration', but was '{input.ToString()}'.", context.FormatKeyword ?? "format");
#endif
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation format - should have been 'duration'.", context.FormatKeyword ?? "format");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context.WithResult(isValid: true, $"Validation {context.FormatKeyword ?? "format"} - was a 'duration'.", context.FormatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeEmail<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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

        ValidationContext result;
        if (level >= ValidationLevel.Verbose)
        {
            result = validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type");
        }
        else
        {
            result = validationContext;
        }

        instance.AsString.TryGetValue(EmailValidator, new ValidationContextWrapperWithFormatKeyword(result, level, formatKeyword), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool EmailValidator(ReadOnlySpan<char> input, in ValidationContextWrapperWithFormatKeyword context, out ValidationContext result)
        {
            result = context.Context;

            if (!EmailPattern.IsMatch(input))
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
#if NET8_0_OR_GREATER
                    result = context.Context.WithResult(isValid: false, $"Validation {context.FormatKeyword ?? "format"} - should have been 'email', but was '{input}'.", context.FormatKeyword ?? "format");
#else
                    result = context.Context.WithResult(isValid: false, $"Validation {context.FormatKeyword ?? "format"} - should have been 'email', but was '{input.ToString()}'.", context.FormatKeyword ?? "format");
#endif
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation format - should have been 'email'.", context.FormatKeyword ?? "format");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context.WithResult(isValid: true, $"Validation {context.FormatKeyword ?? "format"} - was 'email'.", context.FormatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeRelativePointer<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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

        ValidationContext result;
        if (level >= ValidationLevel.Verbose)
        {
            result = validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type");
        }
        else
        {
            result = validationContext;
        }

        instance.AsString.TryGetValue(RelativePointerValidator, new ValidationContextWrapperWithFormatKeyword(result, level, formatKeyword), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool RelativePointerValidator(ReadOnlySpan<char> input, in ValidationContextWrapperWithFormatKeyword context, out ValidationContext result)
        {
            result = context.Context;

            if (!JsonRelativePointerPattern.IsMatch(input))
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
#if NET8_0_OR_GREATER
                    result = context.Context.WithResult(isValid: false, $"Validation {context.FormatKeyword ?? "format"} - should have been 'relative-json-pointer', but was '{input}'.", context.FormatKeyword ?? "format");
#else
                    result = context.Context.WithResult(isValid: false, $"Validation {context.FormatKeyword ?? "format"} - should have been 'relative-json-pointer', but was '{input.ToString()}'.", context.FormatKeyword ?? "format");
#endif
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation format - should have been 'relative-json-pointer'.", context.FormatKeyword ?? "format");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context.WithResult(isValid: true, "Validation format - was 'relative-json-pointer'.", context.FormatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypePointer<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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

        ValidationContext result;
        if (level >= ValidationLevel.Verbose)
        {
            result = validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type");
        }
        else
        {
            result = validationContext;
        }

        instance.AsString.TryGetValue(PointerValidator, new ValidationContextWrapperWithFormatKeyword(result, level, formatKeyword), out result);

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

        static bool PointerValidator(ReadOnlySpan<char> input, in ValidationContextWrapperWithFormatKeyword context, out ValidationContext result)
        {
            result = context.Context;

            if (!JsonPointerPattern.IsMatch(input))
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
#if NET8_0_OR_GREATER
                    result = context.Context.WithResult(isValid: false, $"Validation {context.FormatKeyword ?? "format"} - should have been 'json-pointer', but was '{input}'.", context.FormatKeyword ?? "format");
#else
                    result = context.Context.WithResult(isValid: false, $"Validation {context.FormatKeyword ?? "format"} - should have been 'json-pointer', but was '{input.ToString()}'.", context.FormatKeyword ?? "format");
#endif
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation format - should have been 'json-pointer'.", context.FormatKeyword ?? "format");
                }
                else
                {
                    result = context.Context.WithResult(isValid: false);
                }

                return true;
            }

            if (context.Level == ValidationLevel.Verbose)
            {
                result = context.Context.WithResult(isValid: true, $"Validation {context.FormatKeyword ?? "format"} - was 'json-pointer'.", context.FormatKeyword ?? "format");
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
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum2))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum3))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum3}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum4))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum4}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum5))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum5}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum6))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum6}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum7))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum7}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum8))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum8}'.", "enum");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}, {enum3}, {enum4}, {enum5}, {enum6}, {enum7}, {enum8}].", "enum");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.", "enum");
        }
        else
        {
            return ValidationContext.InvalidContext;
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
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum2))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum3))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum3}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum4))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum4}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum5))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum5}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum6))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum6}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum7))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum7}'.", "enum");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}, {enum3}, {enum4}, {enum5}, {enum6}, {enum7}].", "enum");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.", "enum");
        }
        else
        {
            return ValidationContext.InvalidContext;
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
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum2))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum3))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum3}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum4))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum4}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum5))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum5}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum6))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum6}'.", "enum");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}, {enum3}, {enum4}, {enum5}, {enum6}].", "enum");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.", "enum");
        }
        else
        {
            return ValidationContext.InvalidContext;
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
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum2))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum3))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum3}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum4))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum4}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum5))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum5}'.", "enum");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}, {enum3}, {enum4}, {enum5}].", "enum");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.", "enum");
        }
        else
        {
            return ValidationContext.InvalidContext;
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
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum2))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum3))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum3}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum4))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum4}'.", "enum");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}, {enum3}, {enum4}].", "enum");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.", "enum");
        }
        else
        {
            return ValidationContext.InvalidContext;
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
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum2))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum3))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum3}'.", "enum");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}, {enum3}].", "enum");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.", "enum");
        }
        else
        {
            return ValidationContext.InvalidContext;
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
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.", "enum");
            }

            return validationContext;
        }

        if (value.Equals(enum2))
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.", "enum");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}].", "enum");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.", "enum");
        }
        else
        {
            return ValidationContext.InvalidContext;
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
                return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.", "enum");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}].", "enum");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.", "enum");
        }
        else
        {
            return ValidationContext.InvalidContext;
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
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum -  '{value}' matched '{enumValue}'.", "enum");
                }

                return validationContext;
            }
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum -  '{value}' did not match any of the required values: [{string.Join(", ", enums)}].", "enum");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.", "enum");
        }
        else
        {
            return ValidationContext.InvalidContext;
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
                return validationContext.WithResult(isValid: true, $"Validation 6.1.3 const - '{value}' matched '{constValue}'.", "const");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Detailed)
        {
            return validationContext.WithResult(isValid: false, $"Validation 6.1.3 const - '{value}' did not match the required value: {constValue}.", "const");
        }
        else if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: false, "Validation 6.1.3 const - did not match the required value.", "const");
        }
        else
        {
            return ValidationContext.InvalidContext;
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
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.2.1 multipleOf - ignored because the value is not a number", "multipleOf");
                }

                if (maximum.HasValue)
                {
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.2.2 maximum- ignored because the value is not a number", "maximum");
                }

                if (exclusiveMaximum.HasValue)
                {
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.2.3 exclusiveMaximum - ignored because the value is not a number", "exclusiveMaximum");
                }

                if (minimum.HasValue)
                {
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.2.4 minimum - ignored because the value is not a number", "minimum");
                }

                if (exclusiveMinimum.HasValue)
                {
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.2.5 exclusiveMinimum - ignored because the value is not a number", "exclusiveMinimum");
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
                        result = result.WithResult(isValid: true, $"Validation 6.2.1 multipleOf -  {value} was a multiple of {multipleOf}.", "multipleOf");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.1 multipleOf -  {value} was not a multiple of {multipleOf}.", "multipleOf");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.1 multipleOf - was not a multiple of the required value.", "multipleOf");
                    }
                    else
                    {
                        return ValidationContext.InvalidContext;
                    }
                }
            }

            if (maximum.HasValue)
            {
                if (maximum >= number)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.2 maximum -  {value} was less than or equal to {maximum}.", "maximum");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.2 maximum -  {value} was greater than {maximum}.", "maximum");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.2 maximum - was greater than the required value.", "maximum");
                    }
                    else
                    {
                        return ValidationContext.InvalidContext;
                    }
                }
            }

            if (exclusiveMaximum.HasValue)
            {
                if (exclusiveMaximum > number)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.3 exclusiveMaximum -  {value} was less than {exclusiveMaximum}.", "exclusiveMaximum");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.3 exclusiveMaximum -  {value} was greater than or equal to {exclusiveMaximum}.", "exclusiveMaximum");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.3 exclusiveMaximum - was greater than or equal to the required value.", "exclusiveMaximum");
                    }
                    else
                    {
                        return ValidationContext.InvalidContext;
                    }
                }
            }

            if (minimum.HasValue)
            {
                if (minimum <= number)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.4 minimum -  {value} was greater than or equal to {minimum}.", "minimum");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.4 minimum - {value} was less than {minimum}.", "minimum");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.4 minimum - was less than the required value.", "minimum");
                    }
                    else
                    {
                        return ValidationContext.InvalidContext;
                    }
                }
            }

            if (exclusiveMinimum.HasValue)
            {
                if (exclusiveMinimum < number)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.5 exclusiveMinimum -  {value} was greater than {exclusiveMinimum}.", "exclusiveMinimum");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.5 exclusiveMinimum -  {value} was less than or equal to {exclusiveMinimum}.", "exclusiveMinimum");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.5 exclusiveMinimum - was less than or equal to the required value.", "exclusiveMinimum");
                    }
                    else
                    {
                        return ValidationContext.InvalidContext;
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
                        result = result.WithResult(isValid: true, $"Validation 6.2.1 multipleOf -  {currentValue} was a multiple of {multipleOf}.", "multipleOf");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.1 multipleOf -  {currentValue} was not a multiple of {multipleOf}.", "multipleOf");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.1 multipleOf - was not a multiple of the required value.", "multipleOf");
                    }
                    else
                    {
                        return ValidationContext.InvalidContext;
                    }
                }
            }

            if (maximum.HasValue)
            {
                if (currentValue <= maximum)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.2 maximum -  {currentValue} was less than or equal to {maximum}.", "maximum");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.2 maximum -  {currentValue} was greater than {maximum}.", "maximum");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.2 maximum - was greater than the required value.", "maximum");
                    }
                    else
                    {
                        return ValidationContext.InvalidContext;
                    }
                }
            }

            if (exclusiveMaximum.HasValue)
            {
                if (currentValue < exclusiveMaximum)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.3 exclusiveMaximum -  {currentValue} was less than {exclusiveMaximum}.", "exclusiveMaximum");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.3 exclusiveMaximum -  {currentValue} was greater than or equal to {exclusiveMaximum}.", "exclusiveMaximum");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.3 exclusiveMaximum - was greater than or equal to the required value.", "exclusiveMaximum");
                    }
                    else
                    {
                        return ValidationContext.InvalidContext;
                    }
                }
            }

            if (minimum.HasValue)
            {
                if (currentValue >= minimum)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.4 minimum -  {currentValue} was greater than or equal to {minimum}.", "minimum");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.4 minimum - {currentValue} was less than {minimum}.", "minimum");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.4 minimum - was less than the required value.", "minimum");
                    }
                    else
                    {
                        return ValidationContext.InvalidContext;
                    }
                }
            }

            if (exclusiveMinimum.HasValue)
            {
                if (currentValue > exclusiveMinimum)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.5 exclusiveMinimum -  {currentValue} was greater than {exclusiveMinimum}.", "exclusiveMinimum");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.5 exclusiveMinimum -  {currentValue} was less than or equal to {exclusiveMinimum}.", "exclusiveMinimum");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.5 exclusiveMinimum - was less than or equal to the required value.", "exclusiveMinimum");
                    }
                    else
                    {
                        return ValidationContext.InvalidContext;
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
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.3.1 maxLength - ignored because the value is not a string", "maxLength");
                }

                if (minLength is not null)
                {
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.3.2 minLength - ignored because the value is not a string", "minLength");
                }

                if (pattern is not null)
                {
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.3.3 pattern - ignored because the value is not a string", "pattern");
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
                        result = result.WithResult(isValid: true, $"Validation 6.3.1 maxLength - {input.ToString()} of {length} was less than or equal to {maxl}.", "maxLength");
                    }
                }
                else
                {
                    if (context.Level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.3.1 maxLength - {input.ToString()} of {length} was greater than {maxl}.", "maxLength");
                    }
                    else if (context.Level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.3.1 maxLength - was greater than the required length.", "maxLength");
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
                        result = result.WithResult(isValid: true, $"Validation 6.3.2 minLength - {input.ToString()} of {length} was greater than or equal to {minl}.", "minLength");
                    }
                }
                else
                {
                    if (context.Level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.3.2 minLength - {input.ToString()} of {length} was less than {minl}.", "minLength");
                    }
                    else if (context.Level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.3.2 minLength - was less than the required length.", "minLength");
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
                        result = result.WithResult(isValid: true, $"Validation 6.3.3 pattern - {input.ToString()} matched {prex}.", "pattern");
                    }
                }
                else
                {
                    if (context.Level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.3.3 pattern - {input.ToString()} did not match {prex}.", "pattern");
                    }
                    else if (context.Level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.3.13 pattern - did not match the required pattern.", "pattern");
                    }
                    else
                    {
                        result = result.WithResult(isValid: false);
                    }
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Count the runes in a <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    /// <param name="str">The string in which to count the runes.</param>
    /// <returns>The rune count for the string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountRunes(ReadOnlySpan<char> str)
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

    /// <summary>
    /// Validates a content value.
    /// </summary>
    /// <typeparam name="TValue">The type of the <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The instance to validate.</param>
    /// <param name="validationContext">The current validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="typeKeyword">The type keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeContentPre201909<TValue>(in TValue value, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null)
        where TValue : struct, IJsonValue<TValue>
    {
        return TypeContent(value, validationContext, level, false, typeKeyword);
    }

    /// <summary>
    /// Validates a content value.
    /// </summary>
    /// <typeparam name="TValue">The type of the <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The instance to validate.</param>
    /// <param name="validationContext">The current validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="alwaysPassAndAnnotateFailuresInContentDecodingChecks">Always pass failures in content decoding, but annotate.</param>
    /// <param name="typeKeyword">The type keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeContent<TValue>(in TValue value, in ValidationContext validationContext, ValidationLevel level, bool alwaysPassAndAnnotateFailuresInContentDecodingChecks = true, string? typeKeyword = null)
    where TValue : struct, IJsonValue<TValue>
    {
        ValidationContext result = validationContext;

        JsonValueKind valueKind = value.ValueKind;

        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return result.WithResult(isValid: false, $"Validation {typeKeyword ?? "type"} - should have been 'string' but was '{valueKind}'.", typeKeyword ?? "type");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return result.WithResult(isValid: false, "Validation type - should have been 'string'.", typeKeyword ?? "type");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        JsonContent content = value.As<JsonContent>();
        EncodedContentMediaTypeParseStatus status = content.TryGetJsonDocument(out JsonDocument? _);
        if (status == EncodedContentMediaTypeParseStatus.UnableToDecode)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return result.WithResult(isValid: false, "Validation contentMediaType - should have been 'application/json'.", "contentMediaType");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return result.WithResult(isValid: false, "Validation contentMediaType - should have been 'application/json'.", "contentMediaType");
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
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks, "Validation contentMediaType - should have been 'application/json'.", "contentMediaType");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks, "Validation contentMediaType - should have been 'application/json'.", "contentMediaType");
            }
            else
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks);
            }
        }
        else if (level == ValidationLevel.Verbose)
        {
            return result
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'application/json'.", typeKeyword ?? "type")
                .WithResult(isValid: true, "Validation contentMediaType - was'application/json'.", "contentMediaType");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeBase64ContentPre201909<TValue>(in TValue value, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null)
        where TValue : struct, IJsonValue<TValue>
    {
        return TypeBase64Content(value, validationContext, level, false, typeKeyword);
    }

    /// <summary>
    /// Validates a base64Content value.
    /// </summary>
    /// <typeparam name="TValue">The type of the <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The instance to validate.</param>
    /// <param name="validationContext">The current validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="alwaysPassAndAnnotateFailuresInContentDecodingChecks">Always pass failures in content decoding, but annotate.</param>
    /// <param name="typeKeyword">The type keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeBase64Content<TValue>(in TValue value, in ValidationContext validationContext, ValidationLevel level, bool alwaysPassAndAnnotateFailuresInContentDecodingChecks = true, string? typeKeyword = null)
    where TValue : struct, IJsonValue<TValue>
    {
        ValidationContext result = validationContext;

        JsonValueKind valueKind = value.ValueKind;

        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return result.WithResult(isValid: false, $"Validation {typeKeyword ?? "type"} - should have been 'string' but was '{valueKind}'.", typeKeyword ?? "type");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return result.WithResult(isValid: false, "Validation type - should have been 'string'.", typeKeyword ?? "type");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        JsonBase64Content content = value.As<JsonBase64Content>();
        EncodedContentMediaTypeParseStatus status = content.TryGetJsonDocument(out JsonDocument? _);
        if (status == EncodedContentMediaTypeParseStatus.UnableToDecode)
        {
            // Is valid, but we annotate
            if (level >= ValidationLevel.Detailed)
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks, "Validation contentEncoding - should have been a 'base64'.", "contentEncoding");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks, "Validation contentEncoding - should have been 'base64'.", "contentEncoding");
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
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks, "Validation contentMediaType - valid, but should have been 'application/json'.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks, "Validation contentMediaType - valid, but should have been 'application/json'.");
            }
            else
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks);
            }
        }
        else if (level == ValidationLevel.Verbose)
        {
            return result
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type")
                .WithResult(isValid: true, "Validation contentEncoding - was 'base64'.")
                .WithResult(isValid: true, "Validation contentMediaType - was 'application/json'.");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeBase64StringPre201909<TValue>(in TValue value, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null)
        where TValue : struct, IJsonValue<TValue>
    {
        return TypeBase64String(value, validationContext, level, false, typeKeyword);
    }

    /// <summary>
    /// Validates a base64 value.
    /// </summary>
    /// <typeparam name="TValue">The type of the <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="value">The instance to validate.</param>
    /// <param name="validationContext">The current validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="alwaysPassAndAnnotateFailuresInContentDecodingChecks">Always pass but annotate the nodes on encoding failure.</param>
    /// <param name="typeKeyword">The type keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeBase64String<TValue>(in TValue value, in ValidationContext validationContext, ValidationLevel level, bool alwaysPassAndAnnotateFailuresInContentDecodingChecks = true, string? typeKeyword = null)
    where TValue : struct, IJsonValue<TValue>
    {
        ValidationContext result = validationContext;

        JsonValueKind valueKind = value.ValueKind;

        if (valueKind != JsonValueKind.String)
        {
            if (level >= ValidationLevel.Detailed)
            {
                return result.WithResult(isValid: false, $"Validation {typeKeyword ?? "type"} - should have been 'string' but was '{valueKind}'.", typeKeyword ?? "type");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return result.WithResult(isValid: false, "Validation type - should have been 'string'.", typeKeyword ?? "type");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        JsonBase64String base64String = value.As<JsonBase64String>();

        if (!base64String.HasBase64Bytes())
        {
            // Valid, but we annotate
            if (level >= ValidationLevel.Detailed)
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks, "Validation contentEncoding - should have been 'base64'.", "contentEncoding");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks, "Validation contentEncoding - should have been 'base64'.", "contentEncoding");
            }
            else
            {
                return result.WithResult(isValid: alwaysPassAndAnnotateFailuresInContentDecodingChecks);
            }
        }
        else if (level == ValidationLevel.Verbose)
        {
            return result
                .WithResult(isValid: true, $"Validation contentEncoding - was 'base64'.", "contentEncoding")
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeRegex<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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

        JsonRegex regexInstance = instance.As<JsonRegex>();

        if (!regexInstance.TryGetRegex(out Regex _))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'regex' but was '{regexInstance}'.", formatKeyword ?? "format");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation format - should have been 'regex'.", formatKeyword ?? "format");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'regex'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeIriReference<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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
                return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'iri-reference' but was '{iri}'.", typeKeyword ?? "type");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation format - should have been 'iri-reference'.", formatKeyword ?? "format");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'iri-reference'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeIri<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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

        JsonIri iri = instance.As<JsonIri>();

        if (!iri.TryGetUri(out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'iri' but was '{iri}'.", formatKeyword ?? "format");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation format - should have been 'iri'.", formatKeyword ?? "format");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'iri'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUri<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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

        JsonUri uriInstance = instance.As<JsonUri>();

        if (!(uriInstance.TryGetUri(out Uri? testUri) && (!testUri.IsAbsoluteUri || !testUri.IsUnc)))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'uri' but was '{uriInstance}'.", formatKeyword ?? "format");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation format - should have been 'uri'.", formatKeyword ?? "format");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'uri'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeUriReference<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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
                return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'uri-reference' but was '{uriReferenceInstance}'.", formatKeyword ?? "format");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation format - should have been 'uri-reference'.", formatKeyword ?? "format");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'uri-reference'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeTime<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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

        JsonTime time = instance.As<JsonTime>();

        if (!time.TryGetTime(out _))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'time' but was '{time}'.", formatKeyword ?? "format");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation format - should have been a 'time'.", formatKeyword ?? "format");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'time'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeDate<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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

        JsonDate date = instance.As<JsonDate>();

        if (!date.TryGetDate(out _))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been'date' but was '{date}'.", formatKeyword ?? "format");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation format - should have been 'date'.", formatKeyword ?? "format");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'date'.", formatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeIpV6<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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

        ValidationContext result;
        if (level >= ValidationLevel.Verbose)
        {
            result = validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type");
        }
        else
        {
            result = validationContext;
        }

#if NET8_0_OR_GREATER
        instance.AsString.TryGetValue(IpV6Validator, new ValidationContextWrapperWithFormatKeyword(result, level, formatKeyword), out result);
#else
        IpV6Validator(instance.AsString.GetString(), new ValidationContextWrapperWithFormatKeyword(result, level, formatKeyword), out result);
#endif

        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

#if NET8_0_OR_GREATER
        static bool IpV6Validator(ReadOnlySpan<char> input, in ValidationContextWrapperWithFormatKeyword context, out ValidationContext result)
#else
        static bool IpV6Validator(string input, in ValidationContextWrapperWithFormatKeyword context, out ValidationContext result)
#endif
        {
            result = context.Context;

            if (StandardIPAddress.IPAddressParser(input, null, out IPAddress? address) &&
                address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 &&
                !ZoneIdExpression.IsMatch(input))
            {
                if (context.Level == ValidationLevel.Verbose)
                {
                    result = context.Context.WithResult(isValid: true, $"Validation {context.FormatKeyword ?? "format"} - was 'ipv6'.", context.FormatKeyword ?? "format");
                }

                return true;
            }
            else
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
                    result = context.Context.WithResult(isValid: false, $"Validation {context.FormatKeyword ?? "format"} - should have been 'ipv6', but was '{input}'.", context.FormatKeyword ?? "format");
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation format - should have been 'ipv6'.", context.FormatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeIpV4<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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

        ValidationContext result;
        if (level >= ValidationLevel.Verbose)
        {
            result = validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type");
        }
        else
        {
            result = validationContext;
        }

#if NET8_0_OR_GREATER
        instance.AsString.TryGetValue(IpV4Validator, new ValidationContextWrapperWithFormatKeyword(result, level, formatKeyword), out result);
#else
        IpV4Validator(instance.AsString.GetString(), new ValidationContextWrapperWithFormatKeyword(result, level, formatKeyword), out result);
#endif
        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        return result;

#if NET8_0_OR_GREATER
        static bool IpV4Validator(ReadOnlySpan<char> input, in ValidationContextWrapperWithFormatKeyword context, out ValidationContext result)
#else
        static bool IpV4Validator(string input, in ValidationContextWrapperWithFormatKeyword context, out ValidationContext result)
#endif
        {
            result = context.Context;

            if (StandardIPAddress.IPAddressParser(input, null, out IPAddress? address) &&
                address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                IpV4Pattern.IsMatch(input))
            {
                if (context.Level == ValidationLevel.Verbose)
                {
                    result = context.Context.WithResult(isValid: true, $"Validation {context.FormatKeyword ?? "format"} - was 'ipv4'.", context.FormatKeyword ?? "format");
                }

                return true;
            }
            else
            {
                if (context.Level >= ValidationLevel.Detailed)
                {
                    result = context.Context.WithResult(isValid: false, $"Validation {context.FormatKeyword ?? "format"} - should have been 'ipv4', but was '{input}'.", context.FormatKeyword ?? "format");
                }
                else if (context.Level >= ValidationLevel.Basic)
                {
                    result = context.Context.WithResult(isValid: false, "Validation format - should have been 'ipv4'.", context.FormatKeyword ?? "format");
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
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context.</returns>
    public static ValidationContext TypeDateTime<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? typeKeyword = null, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = instance.ValueKind;
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

        JsonDateTime date = instance.As<JsonDateTime>();

        if (!date.TryGetDateTime(out _))
        {
            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation {formatKeyword ?? "format"} - should have been 'datetime' but was '{date}'.", formatKeyword ?? "format");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation format - should have been 'datetime'.", formatKeyword ?? "format");
            }
            else
            {
                return ValidationContext.InvalidContext;
            }
        }

        if (level == ValidationLevel.Verbose)
        {
            return validationContext
                .WithResult(isValid: true, $"Validation {typeKeyword ?? "type"} - was 'string'.", typeKeyword ?? "type")
                .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'datetime'.", formatKeyword ?? "format");
        }

        return validationContext;
    }

#if NET8_0_OR_GREATER
    [GeneratedRegex("^(?:(?:^|\\.)(?:2(?:5[0-5]|[0-4]\\d)|1?\\d?\\d)){4}$", RegexOptions.Compiled)]
    private static partial Regex CreateIpV4Pattern();

    [GeneratedRegex("%.*$", RegexOptions.Compiled)]
    private static partial Regex CreateZoneIdExpression();

    [GeneratedRegex("^(?:[a-zA-Z0-9!#$%&'*+/=?^_`{|}~-]+(?:\\.[a-zA-Z0-9!#$%&'*+/=?^_`{|}~-]+)*|\"(?:[ \\x01-\\x08\\x0b\\x0c\\x0e-\\x1f\\x21\\x23-\\x5b\\x5d-\\x7f]|\\\\[\\x01-\\x09\\x0b\\x0c\\x0e-\\x7f])*\")@(?:(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?\\.)+[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-z0-9])?|\\[(((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|IPv6:(([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])))\\])$", RegexOptions.Compiled)]
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

    private static Regex CreateEmailPattern() => new("^(?:[a-zA-Z0-9!#$%&'*+/=?^_`{|}~-]+(?:\\.[a-zA-Z0-9!#$%&'*+/=?^_`{|}~-]+)*|\"(?:[ \\x01-\\x08\\x0b\\x0c\\x0e-\\x1f\\x21\\x23-\\x5b\\x5d-\\x7f]|\\\\[\\x01-\\x09\\x0b\\x0c\\x0e-\\x7f])*\")@(?:(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?\\.)+[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?|\\[(((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|IPv6:(([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])))\\])$", RegexOptions.Compiled);

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
    /// <summary>
    /// A wrapper for the <see cref="ValidationContext"/> and <see cref="ValidationLevel"/>.
    /// </summary>
    /// <param name="Context">The validation context.</param>
    /// <param name="Level">The validation level.</param>
    public readonly record struct ValidationContextWrapper(in ValidationContext Context, ValidationLevel Level);

    /// <summary>
    /// A wrapper for the <see cref="ValidationContext"/> and <see cref="ValidationLevel"/>.
    /// </summary>
    /// <param name="Context">The validation context.</param>
    /// <param name="Level">The validation level.</param>
    /// <param name="FormatKeyword">The format keyword.</param>
    public readonly record struct ValidationContextWrapperWithFormatKeyword(in ValidationContext Context, ValidationLevel Level, string? FormatKeyword);

    private readonly record struct StringValidationContextWrapper(in ValidationContext Context, ValidationLevel Level, int? MinLength, int? MaxLength, Regex? Pattern);
#else
    /// <summary>
    /// A wrapper for the <see cref="ValidationContext"/> and <see cref="ValidationLevel"/>.
    /// </summary>
    public readonly struct ValidationContextWrapper
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationContextWrapper"/> struct.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <param name="level">The validation level.</param>
        public ValidationContextWrapper(in ValidationContext context, ValidationLevel level)
        {
            this.Context = context;
            this.Level = level;
        }

        /// <summary>
        /// Gets the validation context.
        /// </summary>
        public ValidationContext Context { get; }

        /// <summary>
        /// Gets the validation level.
        /// </summary>
        public ValidationLevel Level { get; }
    }

    /// <summary>
    /// A wrapper for the <see cref="ValidationContext"/> and <see cref="ValidationLevel"/>.
    /// </summary>
    public readonly struct ValidationContextWrapperWithFormatKeyword
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationContextWrapper"/> struct.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <param name="formatKeyword">The format keyword.</param>
        public ValidationContextWrapperWithFormatKeyword(in ValidationContext context, ValidationLevel level, string? formatKeyword)
        {
            this.Context = context;
            this.Level = level;
            this.FormatKeyword = formatKeyword;
        }

        /// <summary>
        /// Gets the validation context.
        /// </summary>
        public ValidationContext Context { get; }

        /// <summary>
        /// Gets the validation level.
        /// </summary>
        public ValidationLevel Level { get; }

        /// <summary>
        /// Gets the format keyword.
        /// </summary>
        public string? FormatKeyword { get; }
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