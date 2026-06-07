// <copyright file="ValidateWithoutCoreType.Warning.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// Warning-mode format validation helpers that do not assert the core type.
/// </summary>
/// <remarks>
/// Each method mirrors its <c>Type&lt;Format&gt;</c> sibling but, for a value that does not
/// satisfy the format, records a <c>WARNING</c> annotation and reports the value as valid
/// rather than failing.
/// </remarks>
public static partial class ValidateWithoutCoreType
{
    /// <summary>
    /// Validates the format <c>uri-template</c> in warning mode.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context. A non-conformant value is reported as valid with a warning.</returns>
    public static ValidationContext TypeUriTemplateWarning<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        if (ValidateWithoutCoreType.TypeUriTemplate(instance, ValidationContext.ValidContext, ValidationLevel.Flag, formatKeyword).IsValid)
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'uri-template'.", formatKeyword ?? "format");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: true, $"WARNING: Validation {formatKeyword ?? "format"} - should have been 'uri-template'.", formatKeyword ?? "format");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format <c>idn-email</c> in warning mode.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context. A non-conformant value is reported as valid with a warning.</returns>
    public static ValidationContext TypeIdnEmailWarning<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        if (ValidateWithoutCoreType.TypeIdnEmail(instance, ValidationContext.ValidContext, ValidationLevel.Flag, formatKeyword).IsValid)
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'idn-email'.", formatKeyword ?? "format");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: true, $"WARNING: Validation {formatKeyword ?? "format"} - should have been 'idn-email'.", formatKeyword ?? "format");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format <c>idn-hostname</c> in warning mode.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context. A non-conformant value is reported as valid with a warning.</returns>
    public static ValidationContext TypeIdnHostNameWarning<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        if (ValidateWithoutCoreType.TypeIdnHostName(instance, ValidationContext.ValidContext, ValidationLevel.Flag, formatKeyword).IsValid)
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'idn-hostname'.", formatKeyword ?? "format");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: true, $"WARNING: Validation {formatKeyword ?? "format"} - should have been 'idn-hostname'.", formatKeyword ?? "format");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format <c>hostname</c> in warning mode.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context. A non-conformant value is reported as valid with a warning.</returns>
    public static ValidationContext TypeHostnameWarning<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        if (ValidateWithoutCoreType.TypeHostname(instance, ValidationContext.ValidContext, ValidationLevel.Flag, formatKeyword).IsValid)
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'hostname'.", formatKeyword ?? "format");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: true, $"WARNING: Validation {formatKeyword ?? "format"} - should have been 'hostname'.", formatKeyword ?? "format");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format <c>uuid</c> in warning mode.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context. A non-conformant value is reported as valid with a warning.</returns>
    public static ValidationContext TypeUuidWarning<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        if (ValidateWithoutCoreType.TypeUuid(instance, ValidationContext.ValidContext, ValidationLevel.Flag, formatKeyword).IsValid)
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'uuid'.", formatKeyword ?? "format");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: true, $"WARNING: Validation {formatKeyword ?? "format"} - should have been 'uuid'.", formatKeyword ?? "format");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format <c>duration</c> in warning mode.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context. A non-conformant value is reported as valid with a warning.</returns>
    public static ValidationContext TypeDurationWarning<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        if (ValidateWithoutCoreType.TypeDuration(instance, ValidationContext.ValidContext, ValidationLevel.Flag, formatKeyword).IsValid)
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'duration'.", formatKeyword ?? "format");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: true, $"WARNING: Validation {formatKeyword ?? "format"} - should have been 'duration'.", formatKeyword ?? "format");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format <c>email</c> in warning mode.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context. A non-conformant value is reported as valid with a warning.</returns>
    public static ValidationContext TypeEmailWarning<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        if (ValidateWithoutCoreType.TypeEmail(instance, ValidationContext.ValidContext, ValidationLevel.Flag, formatKeyword).IsValid)
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'email'.", formatKeyword ?? "format");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: true, $"WARNING: Validation {formatKeyword ?? "format"} - should have been 'email'.", formatKeyword ?? "format");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format <c>relative-json-pointer</c> in warning mode.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context. A non-conformant value is reported as valid with a warning.</returns>
    public static ValidationContext TypeRelativePointerWarning<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        if (ValidateWithoutCoreType.TypeRelativePointer(instance, ValidationContext.ValidContext, ValidationLevel.Flag, formatKeyword).IsValid)
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'relative-json-pointer'.", formatKeyword ?? "format");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: true, $"WARNING: Validation {formatKeyword ?? "format"} - should have been 'relative-json-pointer'.", formatKeyword ?? "format");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format <c>json-pointer</c> in warning mode.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context. A non-conformant value is reported as valid with a warning.</returns>
    public static ValidationContext TypePointerWarning<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        if (ValidateWithoutCoreType.TypePointer(instance, ValidationContext.ValidContext, ValidationLevel.Flag, formatKeyword).IsValid)
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'json-pointer'.", formatKeyword ?? "format");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: true, $"WARNING: Validation {formatKeyword ?? "format"} - should have been 'json-pointer'.", formatKeyword ?? "format");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format <c>regex</c> in warning mode.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context. A non-conformant value is reported as valid with a warning.</returns>
    public static ValidationContext TypeRegexWarning<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        if (ValidateWithoutCoreType.TypeRegex(instance, ValidationContext.ValidContext, ValidationLevel.Flag, formatKeyword).IsValid)
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'regex'.", formatKeyword ?? "format");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: true, $"WARNING: Validation {formatKeyword ?? "format"} - should have been 'regex'.", formatKeyword ?? "format");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format <c>iri-reference</c> in warning mode.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context. A non-conformant value is reported as valid with a warning.</returns>
    public static ValidationContext TypeIriReferenceWarning<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        if (ValidateWithoutCoreType.TypeIriReference(instance, ValidationContext.ValidContext, ValidationLevel.Flag, formatKeyword).IsValid)
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'iri-reference'.", formatKeyword ?? "format");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: true, $"WARNING: Validation {formatKeyword ?? "format"} - should have been 'iri-reference'.", formatKeyword ?? "format");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format <c>iri</c> in warning mode.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context. A non-conformant value is reported as valid with a warning.</returns>
    public static ValidationContext TypeIriWarning<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        if (ValidateWithoutCoreType.TypeIri(instance, ValidationContext.ValidContext, ValidationLevel.Flag, formatKeyword).IsValid)
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'iri'.", formatKeyword ?? "format");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: true, $"WARNING: Validation {formatKeyword ?? "format"} - should have been 'iri'.", formatKeyword ?? "format");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format <c>uri</c> in warning mode.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context. A non-conformant value is reported as valid with a warning.</returns>
    public static ValidationContext TypeUriWarning<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        if (ValidateWithoutCoreType.TypeUri(instance, ValidationContext.ValidContext, ValidationLevel.Flag, formatKeyword).IsValid)
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'uri'.", formatKeyword ?? "format");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: true, $"WARNING: Validation {formatKeyword ?? "format"} - should have been 'uri'.", formatKeyword ?? "format");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format <c>uri-reference</c> in warning mode.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context. A non-conformant value is reported as valid with a warning.</returns>
    public static ValidationContext TypeUriReferenceWarning<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        if (ValidateWithoutCoreType.TypeUriReference(instance, ValidationContext.ValidContext, ValidationLevel.Flag, formatKeyword).IsValid)
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'uri-reference'.", formatKeyword ?? "format");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: true, $"WARNING: Validation {formatKeyword ?? "format"} - should have been 'uri-reference'.", formatKeyword ?? "format");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format <c>time</c> in warning mode.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context. A non-conformant value is reported as valid with a warning.</returns>
    public static ValidationContext TypeTimeWarning<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        if (ValidateWithoutCoreType.TypeTime(instance, ValidationContext.ValidContext, ValidationLevel.Flag, formatKeyword).IsValid)
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'time'.", formatKeyword ?? "format");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: true, $"WARNING: Validation {formatKeyword ?? "format"} - should have been 'time'.", formatKeyword ?? "format");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format <c>date</c> in warning mode.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context. A non-conformant value is reported as valid with a warning.</returns>
    public static ValidationContext TypeDateWarning<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        if (ValidateWithoutCoreType.TypeDate(instance, ValidationContext.ValidContext, ValidationLevel.Flag, formatKeyword).IsValid)
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'date'.", formatKeyword ?? "format");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: true, $"WARNING: Validation {formatKeyword ?? "format"} - should have been 'date'.", formatKeyword ?? "format");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format <c>ipv6</c> in warning mode.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context. A non-conformant value is reported as valid with a warning.</returns>
    public static ValidationContext TypeIpV6Warning<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        if (ValidateWithoutCoreType.TypeIpV6(instance, ValidationContext.ValidContext, ValidationLevel.Flag, formatKeyword).IsValid)
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'ipv6'.", formatKeyword ?? "format");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: true, $"WARNING: Validation {formatKeyword ?? "format"} - should have been 'ipv6'.", formatKeyword ?? "format");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format <c>ipv4</c> in warning mode.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context. A non-conformant value is reported as valid with a warning.</returns>
    public static ValidationContext TypeIpV4Warning<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        if (ValidateWithoutCoreType.TypeIpV4(instance, ValidationContext.ValidContext, ValidationLevel.Flag, formatKeyword).IsValid)
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'ipv4'.", formatKeyword ?? "format");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: true, $"WARNING: Validation {formatKeyword ?? "format"} - should have been 'ipv4'.", formatKeyword ?? "format");
        }

        return validationContext;
    }

    /// <summary>
    /// Validates the format <c>date-time</c> in warning mode.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <returns>The updated validation context. A non-conformant value is reported as valid with a warning.</returns>
    public static ValidationContext TypeDateTimeWarning<T>(in T instance, in ValidationContext validationContext, ValidationLevel level, string? formatKeyword = null)
        where T : struct, IJsonValue<T>
    {
        if (ValidateWithoutCoreType.TypeDateTime(instance, ValidationContext.ValidContext, ValidationLevel.Flag, formatKeyword).IsValid)
        {
            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, $"Validation {formatKeyword ?? "format"} - was 'date-time'.", formatKeyword ?? "format");
            }

            return validationContext;
        }

        if (level >= ValidationLevel.Basic)
        {
            return validationContext.WithResult(isValid: true, $"WARNING: Validation {formatKeyword ?? "format"} - should have been 'date-time'.", formatKeyword ?? "format");
        }

        return validationContext;
    }
}