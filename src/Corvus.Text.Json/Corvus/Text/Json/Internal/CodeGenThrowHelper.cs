// <copyright file="CodeGenThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Specifies the numeric type used in code generation scenarios.
/// </summary>
public enum CodeGenNumericType
{
    /// <summary>Represents an 8-bit unsigned integer.</summary>
    Byte,

    /// <summary>Represents an 8-bit signed integer.</summary>
    SByte,

    /// <summary>Represents a 16-bit signed integer.</summary>
    Int16,

    /// <summary>Represents a 32-bit signed integer.</summary>
    Int32,

    /// <summary>Represents a 64-bit signed integer.</summary>
    Int64,

    /// <summary>Represents a 128-bit signed integer.</summary>
    Int128,

    /// <summary>Represents a 16-bit unsigned integer.</summary>
    UInt16,

    /// <summary>Represents a 32-bit unsigned integer.</summary>
    UInt32,

    /// <summary>Represents a 64-bit unsigned integer.</summary>
    UInt64,

    /// <summary>Represents a 128-bit unsigned integer.</summary>
    UInt128,

    /// <summary>Represents a 16-bit floating point number.</summary>
    Half,

    /// <summary>Represents a 32-bit floating point number.</summary>
    Single,

    /// <summary>Represents a 64-bit floating point number.</summary>
    Double,

    /// <summary>Represents a 128-bit decimal number.</summary>
    Decimal
}

/// <summary>
/// Provides helper methods for throwing exceptions in code generation and runtime scenarios for Corvus.Text.Json.
/// This class centralizes exception creation and throwing logic to ensure consistent error handling and messaging.
/// </summary>
public static partial class CodeGenThrowHelper
{
    // If the exception source is this value, the serializer will re-throw as JsonException.
    public const string ExceptionSourceValueToRethrowAsJsonException = "Corvus.Text.Json.Rethrowable";

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> when an array buffer has an incorrect length.
    /// </summary>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <param name="expectedLength">The expected length of the array buffer.</param>
    /// <exception cref="ArgumentException">Always thrown.</exception>
    public static void ThrowArgumentException_ArrayBufferLength(string paramName, int expectedLength)
    {
        throw new ArgumentException(SR.Format(SR.IncorrectArrayBufferLength, expectedLength), paramName);
    }

    /// <summary>
    /// Throws a generic <see cref="FormatException"/> for format-related errors.
    /// </summary>
    /// <exception cref="FormatException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowFormatException()
    {
        throw new FormatException { Source = ExceptionSourceValueToRethrowAsJsonException };
    }

    /// <summary>
    /// Throws a <see cref="FormatException"/> for numeric type formatting errors.
    /// </summary>
    /// <param name="numericType">The numeric type that failed to format.</param>
    /// <exception cref="FormatException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowFormatException(CodeGenNumericType numericType)
    {
        string message = "";

        switch (numericType)
        {
            case CodeGenNumericType.Byte:
                message = SR.FormatByte;
                break;

            case CodeGenNumericType.SByte:
                message = SR.FormatSByte;
                break;

            case CodeGenNumericType.Int16:
                message = SR.FormatInt16;
                break;

            case CodeGenNumericType.Int32:
                message = SR.FormatInt32;
                break;

            case CodeGenNumericType.Int64:
                message = SR.FormatInt64;
                break;

            case CodeGenNumericType.Int128:
                message = SR.FormatInt128;
                break;

            case CodeGenNumericType.UInt16:
                message = SR.FormatUInt16;
                break;

            case CodeGenNumericType.UInt32:
                message = SR.FormatUInt32;
                break;

            case CodeGenNumericType.UInt64:
                message = SR.FormatUInt64;
                break;

            case CodeGenNumericType.UInt128:
                message = SR.FormatUInt128;
                break;

            case CodeGenNumericType.Half:
                message = SR.FormatHalf;
                break;

            case CodeGenNumericType.Single:
                message = SR.FormatSingle;
                break;

            case CodeGenNumericType.Double:
                message = SR.FormatDouble;
                break;

            case CodeGenNumericType.Decimal:
                message = SR.FormatDecimal;
                break;

            default:
                Debug.Fail($"The CodeGenNumericType enum value: {numericType} is not part of the switch. Add the appropriate case and exception message.");
                break;
        }

        throw new FormatException(message) { Source = ExceptionSourceValueToRethrowAsJsonException };
    }

    /// <summary>
    /// Creates an exception when a JSON element has an unexpected value kind.
    /// </summary>
    /// <param name="expectedType">The expected JSON value kind.</param>
    /// <param name="actualType">The actual JSON value kind.</param>
    /// <returns>An exception indicating the type mismatch.</returns>
    internal static InvalidOperationException GetJsonElementWrongTypeException(
        JsonValueKind expectedType,
        JsonValueKind actualType)
    {
        return GetInvalidOperationException(
            SR.Format(SR.JsonElementHasWrongType, expectedType, actualType));
    }

    private static InvalidOperationException GetInvalidOperationException(string message)
    {
        return new InvalidOperationException(message) { Source = ExceptionSourceValueToRethrowAsJsonException };
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when attempting to set a required property to an undefined value.
    /// </summary>
    /// <param name="propertyName">The name of the required property.</param>
    /// <exception cref="InvalidOperationException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidOperationException_SetRequiredPropertyToUndefined(string propertyName)
    {
        throw GetInvalidOperationException(SR.Format(SR.CannotSetRequiredPropertyToUndefined, propertyName));
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when attempting to set a required property to an undefined value.
    /// </summary>
    /// <param name="propertyName">The name of the required property.</param>
    /// <exception cref="InvalidOperationException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidOperationException_PrefixTupleMustBeCreatedFirst()
    {
        throw GetInvalidOperationException(SR.PrefixTupleMustBeCreatedFirst);
    }
}

/// <summary>
/// Specifies the data type used in code generation scenarios.
/// </summary>
public enum CodeGenDataType
{
    /// <summary>Represents a boolean value.</summary>
    Boolean,

    /// <summary>Represents a date-only value.</summary>
    DateOnly,

    /// <summary>Represents a date and time value.</summary>
    DateTime,

    /// <summary>Represents a date and time with offset value.</summary>
    DateTimeOffset,

    /// <summary>Represents a time-only value.</summary>
    TimeOnly,

    /// <summary>Represents a time span value.</summary>
    TimeSpan,

    /// <summary>Represents a base64-encoded string.</summary>
    Base64String,

    /// <summary>Represents a GUID value.</summary>
    Guid,

    /// <summary>Represents a version value.</summary>
    Version,
}