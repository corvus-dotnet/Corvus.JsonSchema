// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Corvus.Text.Json.Internal;
using static Corvus.Text.Json.Utf8JsonWriter;

namespace Corvus.Text.Json;

/// <summary>
/// Defines the types of exception resources used for generating error messages.
/// </summary>
internal enum ExceptionResource
{
    ArrayDepthTooLarge,
    EndOfCommentNotFound,
    EndOfStringNotFound,
    RequiredDigitNotFoundAfterDecimal,
    RequiredDigitNotFoundAfterSign,
    RequiredDigitNotFoundEndOfData,
    ExpectedEndAfterSingleJson,
    ExpectedEndOfDigitNotFound,
    ExpectedFalse,
    ExpectedNextDigitEValueNotFound,
    ExpectedNull,
    ExpectedSeparatorAfterPropertyNameNotFound,
    ExpectedStartOfPropertyNotFound,
    ExpectedStartOfPropertyOrValueNotFound,
    ExpectedStartOfPropertyOrValueAfterComment,
    ExpectedStartOfValueNotFound,
    ExpectedTrue,
    ExpectedValueAfterPropertyNameNotFound,
    FoundInvalidCharacter,
    InvalidCharacterWithinString,
    InvalidCharacterAfterEscapeWithinString,
    InvalidHexCharacterWithinString,
    InvalidEndOfJsonNonPrimitive,
    MismatchedObjectArray,
    ObjectDepthTooLarge,
    ZeroDepthAtEnd,
    DepthTooLarge,
    CannotStartObjectArrayWithoutProperty,
    CannotStartObjectArrayAfterPrimitiveOrClose,
    CannotWriteValueWithinObject,
    CannotWriteValueAfterPrimitiveOrClose,
    CannotWritePropertyWithinArray,
    ExpectedJsonTokens,
    TrailingCommaNotAllowedBeforeArrayEnd,
    TrailingCommaNotAllowedBeforeObjectEnd,
    InvalidCharacterAtStartOfComment,
    UnexpectedEndOfDataWhileReadingComment,
    UnexpectedEndOfLineSeparator,
    ExpectedOneCompleteToken,
    NotEnoughData,
    InvalidLeadingZeroInNumber,
    CannotWriteWithinString,
}

/// <summary>
/// Defines the numeric types that can cause format exceptions.
/// </summary>
internal enum NumericType
{
    Byte,
    SByte,
    Int16,
    Int32,
    Int64,
    Int128,
    UInt16,
    UInt32,
    UInt64,
    UInt128,
    Half,
    Single,
    Double,
    Decimal
}

/// <summary>
/// Provides helper methods for throwing exceptions in a consistent manner.
/// </summary>
internal static partial class ThrowHelper
{
    /// <summary>
    /// Exception source value that indicates the serializer should re-throw the exception as a JsonException.
    /// </summary>
    public const string ExceptionSourceValueToRethrowAsJsonException = "Corvus.Text.Json.Rethrowable";

    /// <summary>
    /// Gets an <see cref="ArgumentException"/> for invalid UTF-16 reading operations.
    /// </summary>
    /// <param name="innerException">The inner exception.</param>
    /// <returns>An <see cref="ArgumentException"/> with the appropriate message.</returns>
    public static ArgumentException GetArgumentException_ReadInvalidUTF16(EncoderFallbackException innerException)
    {
        return new ArgumentException(SR.CannotTranscodeInvalidUtf16, innerException);
    }

    /// <summary>
    /// Gets an <see cref="InvalidOperationException"/> with a message and optional inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The optional inner exception.</param>
    /// <returns>An <see cref="InvalidOperationException"/> with the appropriate source.</returns>
    public static InvalidOperationException GetInvalidOperationException(string message, Exception? innerException)
    {
        var ex = new InvalidOperationException(message, innerException)
        {
            Source = ExceptionSourceValueToRethrowAsJsonException
        };
        return ex;
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that a digit was expected after an escape character in a JSON Pointer,
    /// but the end of the document was reached.
    /// </summary>
    public static void ThrowInvalidOperation_JsonPointer_Expected_Digit_After_Escape_Character_Found_End()
    {
        throw GetInvalidOperationException(SR.JsonPointer_Expected_Digit_After_Escape_Character_Found_End);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that a digit was expected after an escape character in a JSON Pointer.
    /// </summary>
    public static void ThrowInvalidOperation_JsonPointer_Expected_Digit_After_Escape_Character()
    {
        throw GetInvalidOperationException(SR.JsonPointer_Expected_Digit_After_Escape_Character);
    }

    /// <summary>
    /// Gets an <see cref="InvalidOperationException"/> based on the exception resource and current state.
    /// </summary>
    /// <param name="resource">The exception resource type.</param>
    /// <param name="currentDepth">The current depth.</param>
    /// <param name="maxDepth">The maximum allowed depth.</param>
    /// <param name="token">The token that caused the exception.</param>
    /// <param name="tokenType">The token type.</param>
    /// <returns>An <see cref="InvalidOperationException"/> with the appropriate message.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static InvalidOperationException GetInvalidOperationException(ExceptionResource resource, int currentDepth, int maxDepth, byte token, JsonTokenType tokenType)
    {
        string message = GetResourceString(resource, currentDepth, maxDepth, token, tokenType);
        InvalidOperationException ex = GetInvalidOperationException(message);
        ex.Source = ExceptionSourceValueToRethrowAsJsonException;
        return ex;
    }

    /// <summary>
    /// Gets an <see cref="InvalidOperationException"/> for invalid UTF-16 reading operations.
    /// </summary>
    /// <param name="innerException">The optional inner exception.</param>
    /// <returns>An <see cref="InvalidOperationException"/> with the appropriate message.</returns>
    public static InvalidOperationException GetInvalidOperationException_ReadInvalidUTF16(DecoderFallbackException? innerException = null)
    {
        return GetInvalidOperationException(SR.CannotTranscodeInvalidUtf16, innerException);
    }

    /// <summary>
    /// Gets an <see cref="InvalidOperationException"/> for invalid UTF-8 reading operations.
    /// </summary>
    /// <param name="innerException">The optional inner exception.</param>
    /// <returns>An <see cref="InvalidOperationException"/> with the appropriate message.</returns>
    public static InvalidOperationException GetInvalidOperationException_ReadInvalidUTF8(DecoderFallbackException? innerException = null)
    {
        return GetInvalidOperationException(SR.CannotTranscodeInvalidUtf8, innerException);
    }

    /// <summary>
    /// Gets a <see cref="JsonException"/> with information from the JSON reader.
    /// </summary>
    /// <param name="json">The JSON reader reference.</param>
    /// <param name="resource">The exception resource type.</param>
    /// <param name="nextByte">The next byte that caused the exception.</param>
    /// <param name="bytes">Additional bytes for context.</param>
    /// <returns>A <see cref="JsonException"/> with the appropriate message and position information.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static JsonException GetJsonReaderException(ref Utf8JsonReader json, ExceptionResource resource, byte nextByte, ReadOnlySpan<byte> bytes)
    {
        string message = GetResourceString(ref json, resource, nextByte, JsonHelpers.Utf8GetString(bytes));

        long lineNumber = json.CurrentState._lineNumber;
        long bytePositionInLine = json.CurrentState._bytePositionInLine;

        message += $" LineNumber: {lineNumber} | BytePositionInLine: {bytePositionInLine}.";
        return new JsonReaderException(message, lineNumber, bytePositionInLine);
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> with the specified message.
    /// </summary>
    /// <param name="message">The error message to include in the exception.</param>
    [DoesNotReturn]
    public static void ThrowArgumentException(string message)
    {
        throw GetArgumentException(message);
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> for property name or value that is too large.
    /// </summary>
    /// <param name="propertyName">The property name span.</param>
    /// <param name="value">The value span.</param>
    [DoesNotReturn]
    public static void ThrowArgumentException(ReadOnlySpan<byte> propertyName, ReadOnlySpan<byte> value)
    {
        if (propertyName.Length > JsonConstants.MaxUnescapedTokenSize)
        {
            ThrowArgumentException(SR.Format(SR.PropertyNameTooLarge, propertyName.Length));
        }
        else
        {
            Debug.Assert(value.Length > JsonConstants.MaxUnescapedTokenSize);
            ThrowArgumentException(SR.Format(SR.ValueTooLarge, value.Length));
        }
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> for property name or value that is too large.
    /// </summary>
    /// <param name="propertyName">The property name span (UTF-8 bytes).</param>
    /// <param name="value">The value span (UTF-16 characters).</param>
    [DoesNotReturn]
    public static void ThrowArgumentException(ReadOnlySpan<byte> propertyName, ReadOnlySpan<char> value)
    {
        if (propertyName.Length > JsonConstants.MaxUnescapedTokenSize)
        {
            ThrowArgumentException(SR.Format(SR.PropertyNameTooLarge, propertyName.Length));
        }
        else
        {
            Debug.Assert(value.Length > JsonConstants.MaxCharacterTokenSize);
            ThrowArgumentException(SR.Format(SR.ValueTooLarge, value.Length));
        }
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> for property name or value that is too large.
    /// </summary>
    /// <param name="propertyName">The property name span (UTF-16 characters).</param>
    /// <param name="value">The value span (UTF-8 bytes).</param>
    [DoesNotReturn]
    public static void ThrowArgumentException(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value)
    {
        if (propertyName.Length > JsonConstants.MaxCharacterTokenSize)
        {
            ThrowArgumentException(SR.Format(SR.PropertyNameTooLarge, propertyName.Length));
        }
        else
        {
            Debug.Assert(value.Length > JsonConstants.MaxUnescapedTokenSize);
            ThrowArgumentException(SR.Format(SR.ValueTooLarge, value.Length));
        }
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> for property name or value that is too large.
    /// </summary>
    /// <param name="propertyName">The property name span (UTF-16 characters).</param>
    /// <param name="value">The value span (UTF-16 characters).</param>
    [DoesNotReturn]
    public static void ThrowArgumentException(ReadOnlySpan<char> propertyName, ReadOnlySpan<char> value)
    {
        if (propertyName.Length > JsonConstants.MaxCharacterTokenSize)
        {
            ThrowArgumentException(SR.Format(SR.PropertyNameTooLarge, propertyName.Length));
        }
        else
        {
            Debug.Assert(value.Length > JsonConstants.MaxCharacterTokenSize);
            ThrowArgumentException(SR.Format(SR.ValueTooLarge, value.Length));
        }
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> when the destination span is too short.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowArgumentException_DestinationTooShort()
    {
        throw GetArgumentException(SR.DestinationTooShort);
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> when a comment value is invalid.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowArgumentException_InvalidCommentValue()
    {
        throw new ArgumentException(SR.CannotWriteCommentWithEmbeddedDelimiter);
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> when an invalid UTF-16 character is encountered.
    /// </summary>
    /// <param name="charAsInt">The invalid character as an integer.</param>
    [DoesNotReturn]
    public static void ThrowArgumentException_InvalidUTF16(int charAsInt)
    {
        throw new ArgumentException(SR.Format(SR.CannotEncodeInvalidUTF16, $"0x{charAsInt:X2}"));
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> when invalid UTF-8 bytes are encountered.
    /// </summary>
    /// <param name="value">The invalid UTF-8 byte sequence.</param>
    [DoesNotReturn]
    public static void ThrowArgumentException_InvalidUTF8(ReadOnlySpan<byte> value)
    {
        var builder = new StringBuilder();

        int printFirst10 = Math.Min(value.Length, 10);

        for (int i = 0; i < printFirst10; i++)
        {
            byte nextByte = value[i];
            if (IsPrintable(nextByte))
            {
                builder.Append((char)nextByte);
            }
            else
            {
                builder.Append($"0x{nextByte:X2}");
            }
        }

        if (printFirst10 < value.Length)
        {
            builder.Append("...");
        }

        throw new ArgumentException(SR.Format(SR.CannotEncodeInvalidUTF8, builder));
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> when a property name is too large.
    /// </summary>
    /// <param name="tokenLength">The length of the token that is too large.</param>
    [DoesNotReturn]
    public static void ThrowArgumentException_PropertyNameTooLarge(int tokenLength)
    {
        throw GetArgumentException(SR.Format(SR.PropertyNameTooLarge, tokenLength));
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> when a value type is not supported.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowArgumentException_ValueNotSupported()
    {
        throw GetArgumentException(SR.SpecialNumberValuesNotSupported);
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> when a value is too large.
    /// </summary>
    /// <param name="tokenLength">The length of the token that is too large.</param>
    [DoesNotReturn]
    public static void ThrowArgumentException_ValueTooLarge(long tokenLength)
    {
        throw GetArgumentException(SR.Format(SR.ValueTooLarge, tokenLength));
    }

    /// <summary>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> when a comment enum value is out of range.
    /// </summary>
    /// <param name="parameterName">The name of the parameter that caused the exception.</param>
    [DoesNotReturn]
    public static void ThrowArgumentOutOfRangeException_CommentEnumMustBeInRange(string parameterName)
    {
        throw GetArgumentOutOfRangeException(parameterName, SR.CommentHandlingMustBeValid);
    }

    /// <summary>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> for an invalid indent character.
    /// </summary>
    /// <param name="parameterName">The name of the parameter that caused the exception.</param>
    [DoesNotReturn]
    public static void ThrowArgumentOutOfRangeException_IndentCharacter(string parameterName)
    {
        throw GetArgumentOutOfRangeException(parameterName, SR.InvalidIndentCharacter);
    }

    /// <summary>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> for an invalid indent size.
    /// </summary>
    /// <param name="parameterName">The name of the parameter that caused the exception.</param>
    /// <param name="minimumSize">The minimum allowed size.</param>
    /// <param name="maximumSize">The maximum allowed size.</param>
    [DoesNotReturn]
    public static void ThrowArgumentOutOfRangeException_IndentSize(string parameterName, int minimumSize, int maximumSize)
    {
        throw GetArgumentOutOfRangeException(parameterName, SR.Format(SR.InvalidIndentSize, minimumSize, maximumSize));
    }

    /// <summary>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> when max depth must be positive.
    /// </summary>
    /// <param name="parameterName">The name of the parameter that caused the exception.</param>
    [DoesNotReturn]
    public static void ThrowArgumentOutOfRangeException_MaxDepthMustBePositive(string parameterName)
    {
        throw GetArgumentOutOfRangeException(parameterName, SR.MaxDepthMustBePositive);
    }

    /// <summary>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> for an invalid new line character.
    /// </summary>
    /// <param name="parameterName">The name of the parameter that caused the exception.</param>
    [DoesNotReturn]
    public static void ThrowArgumentOutOfRangeException_NewLine(string parameterName)
    {
        throw GetArgumentOutOfRangeException(parameterName, SR.InvalidNewLine);
    }

    /// <summary>
    /// Throws a <see cref="FormatException"/> without a specific message.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowFormatException()
    {
        throw new FormatException { Source = ExceptionSourceValueToRethrowAsJsonException };
    }

    /// <summary>
    /// Throws a <see cref="FormatException"/> for a specific numeric type.
    /// </summary>
    /// <param name="numericType">The numeric type that caused the format exception.</param>
    public static void ThrowFormatException(NumericType numericType)
    {
        string message = "";

        switch (numericType)
        {
            case NumericType.Byte:
                message = SR.FormatByte;
                break;

            case NumericType.SByte:
                message = SR.FormatSByte;
                break;

            case NumericType.Int16:
                message = SR.FormatInt16;
                break;

            case NumericType.Int32:
                message = SR.FormatInt32;
                break;

            case NumericType.Int64:
                message = SR.FormatInt64;
                break;

            case NumericType.Int128:
                message = SR.FormatInt128;
                break;

            case NumericType.UInt16:
                message = SR.FormatUInt16;
                break;

            case NumericType.UInt32:
                message = SR.FormatUInt32;
                break;

            case NumericType.UInt64:
                message = SR.FormatUInt64;
                break;

            case NumericType.UInt128:
                message = SR.FormatUInt128;
                break;

            case NumericType.Half:
                message = SR.FormatHalf;
                break;

            case NumericType.Single:
                message = SR.FormatSingle;
                break;

            case NumericType.Double:
                message = SR.FormatDouble;
                break;

            case NumericType.Decimal:
                message = SR.FormatDecimal;
                break;

            default:
                Debug.Fail($"The NumericType enum value: {numericType} is not part of the switch. Add the appropriate case and exception message.");
                break;
        }

        throw new FormatException(message) { Source = ExceptionSourceValueToRethrowAsJsonException };
    }

    /// <summary>
    /// Throws a <see cref="FormatException"/> for a specific data type.
    /// </summary>
    /// <param name="dataType">The data type that caused the format exception.</param>
    [DoesNotReturn]
    public static void ThrowFormatException(DataType dataType)
    {
        string message = "";

        switch (dataType)
        {
            case DataType.Boolean:
            case DataType.DateOnly:
            case DataType.DateTime:
            case DataType.DateTimeOffset:
            case DataType.TimeOnly:
            case DataType.TimeSpan:
            case DataType.OffsetDateTime:
            case DataType.LocalDate:
            case DataType.OffsetDate:
            case DataType.OffsetTime:
            case DataType.Period:
            case DataType.Guid:
            case DataType.Version:
                message = SR.Format(SR.UnsupportedFormat, dataType);
                break;

            case DataType.Base64String:
                message = SR.CannotDecodeInvalidBase64;
                break;

            default:
                Debug.Fail($"The DataType enum value: {dataType} is not part of the switch. Add the appropriate case and exception message.");
                break;
        }

        throw new FormatException(message) { Source = ExceptionSourceValueToRethrowAsJsonException };
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when the depth is too large.
    /// </summary>
    /// <param name="currentDepth">The current depth.</param>
    /// <param name="maxDepth">The maximum allowed depth.</param>
    [DoesNotReturn]
    public static void ThrowInvalidOperationException(int currentDepth, int maxDepth)
    {
        currentDepth &= JsonConstants.RemoveFlagsBitMask;
        Debug.Assert(currentDepth >= maxDepth);
        ThrowInvalidOperationException(SR.Format(SR.DepthTooLarge, currentDepth, maxDepth));
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> with the specified message.
    /// </summary>
    /// <param name="message">The error message to include in the exception.</param>
    [DoesNotReturn]
    public static void ThrowInvalidOperationException(string message)
    {
        throw GetInvalidOperationException(message);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> based on the exception resource and current state.
    /// </summary>
    /// <param name="resource">The exception resource type.</param>
    /// <param name="currentDepth">The current depth.</param>
    /// <param name="maxDepth">The maximum allowed depth.</param>
    /// <param name="token">The token that caused the exception.</param>
    /// <param name="tokenType">The token type.</param>
    [DoesNotReturn]
    public static void ThrowInvalidOperationException(ExceptionResource resource, int currentDepth, int maxDepth, byte token, JsonTokenType tokenType)
    {
        throw GetInvalidOperationException(resource, currentDepth, maxDepth, token, tokenType);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when different encodings cannot be mixed.
    /// </summary>
    /// <param name="previousEncoding">The previous encoding type.</param>
    /// <param name="currentEncoding">The current encoding type.</param>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidOperationException_CannotMixEncodings(EnclosingContainerType previousEncoding, EnclosingContainerType currentEncoding)
    {
        throw GetInvalidOperationException(SR.Format(SR.CannotMixEncodings, GetEncodingName(previousEncoding), GetEncodingName(currentEncoding)));

        static string GetEncodingName(EnclosingContainerType encoding)
        {
            switch (encoding)
            {
                case EnclosingContainerType.Utf8StringSequence: return "UTF-8";
                case EnclosingContainerType.Utf16StringSequence: return "UTF-16";
                case EnclosingContainerType.Base64StringSequence: return "Base64";
                default:
                    Debug.Fail("Unknown encoding.");
                    return "Unknown";
            }
        }
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when skipping is not allowed on partial data.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowInvalidOperationException_CannotSkipOnPartial()
    {
        throw GetInvalidOperationException(SR.CannotSkip);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when a boolean token type is expected.
    /// </summary>
    /// <param name="tokenType">The actual token type encountered.</param>
    [DoesNotReturn]
    public static void ThrowInvalidOperationException_ExpectedBoolean(JsonTokenType tokenType)
    {
        throw GetInvalidOperationException("boolean", tokenType);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when a comment token type is expected.
    /// </summary>
    /// <param name="tokenType">The actual token type encountered.</param>
    [DoesNotReturn]
    public static void ThrowInvalidOperationException_ExpectedComment(JsonTokenType tokenType)
    {
        throw GetInvalidOperationException("comment", tokenType);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when a number token type is expected.
    /// </summary>
    /// <param name="tokenType">The actual token type encountered.</param>
    [DoesNotReturn]
    public static void ThrowInvalidOperationException_ExpectedNumber(JsonTokenType tokenType)
    {
        throw GetInvalidOperationException("number", tokenType);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when an object token type is expected.
    /// </summary>
    /// <param name="tokenType">The actual token type encountered.</param>
    [DoesNotReturn]
    public static void ThrowInvalidOperationException_ExpectedObject(JsonTokenType tokenType)
    {
        throw GetInvalidOperationException(tokenType);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when a string token type is expected.
    /// </summary>
    /// <param name="tokenType">The actual token type encountered.</param>
    [DoesNotReturn]
    public static void ThrowInvalidOperationException_ExpectedString(JsonTokenType tokenType)
    {
        throw GetInvalidOperationException("string", tokenType);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when string comparison is expected.
    /// </summary>
    /// <param name="tokenType">The actual token type encountered.</param>
    [DoesNotReturn]
    public static void ThrowInvalidOperationException_ExpectedStringComparison(JsonTokenType tokenType)
    {
        throw GetInvalidOperationException(tokenType);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when a larger span is needed.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowInvalidOperationException_NeedLargerSpan()
    {
        throw GetInvalidOperationException(SR.FailedToGetLargerSpan);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when incomplete UTF-16 characters are read.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowInvalidOperationException_ReadIncompleteUTF16()
    {
        throw GetInvalidOperationException(SR.CannotReadIncompleteUTF16);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when invalid UTF-16 characters are read.
    /// </summary>
    /// <param name="charAsInt">The invalid character as an integer.</param>
    [DoesNotReturn]
    public static void ThrowInvalidOperationException_ReadInvalidUTF16(int charAsInt)
    {
        throw GetInvalidOperationException(SR.Format(SR.CannotReadInvalidUTF16, $"0x{charAsInt:X2}"));
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> or <see cref="ArgumentException"/> based on property name length or depth.
    /// </summary>
    /// <param name="propertyName">The property name span.</param>
    /// <param name="currentDepth">The current depth.</param>
    /// <param name="maxDepth">The maximum allowed depth.</param>
    [DoesNotReturn]
    public static void ThrowInvalidOperationOrArgumentException(ReadOnlySpan<byte> propertyName, int currentDepth, int maxDepth)
    {
        currentDepth &= JsonConstants.RemoveFlagsBitMask;
        if (currentDepth >= maxDepth)
        {
            ThrowInvalidOperationException(SR.Format(SR.DepthTooLarge, currentDepth, maxDepth));
        }
        else
        {
            Debug.Assert(propertyName.Length > JsonConstants.MaxCharacterTokenSize);
            ThrowArgumentException(SR.Format(SR.PropertyNameTooLarge, propertyName.Length));
        }
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> or <see cref="ArgumentException"/> based on property name length or depth.
    /// </summary>
    /// <param name="propertyName">The property name span (UTF-16 characters).</param>
    /// <param name="currentDepth">The current depth.</param>
    /// <param name="maxDepth">The maximum allowed depth.</param>
    [DoesNotReturn]
    public static void ThrowInvalidOperationOrArgumentException(ReadOnlySpan<char> propertyName, int currentDepth, int maxDepth)
    {
        currentDepth &= JsonConstants.RemoveFlagsBitMask;
        if (currentDepth >= maxDepth)
        {
            ThrowInvalidOperationException(SR.Format(SR.DepthTooLarge, currentDepth, maxDepth));
        }
        else
        {
            Debug.Assert(propertyName.Length > JsonConstants.MaxCharacterTokenSize);
            ThrowArgumentException(SR.Format(SR.PropertyNameTooLarge, propertyName.Length));
        }
    }

    /// <summary>
    /// Throws a <see cref="JsonReaderException"/> with information from the JSON reader.
    /// </summary>
    /// <param name="json">The JSON reader reference.</param>
    /// <param name="resource">The exception resource type.</param>
    /// <param name="nextByte">The next byte that caused the exception.</param>
    /// <param name="bytes">Additional bytes for context.</param>
    [DoesNotReturn]
    public static void ThrowJsonReaderException(ref Utf8JsonReader json, ExceptionResource resource, byte nextByte = default, ReadOnlySpan<byte> bytes = default)
    {
        throw GetJsonReaderException(ref json, resource, nextByte, bytes);
    }

    /// <summary>
    /// Throws an <see cref="ObjectDisposedException"/> for a disposed <see cref="JsonDocument"/>.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowObjectDisposedException_JsonDocument()
    {
        throw new ObjectDisposedException(nameof(JsonDocument));
    }

    /// <summary>
    /// Throws an <see cref="ObjectDisposedException"/> for a disposed <see cref="JsonWorkspace"/>.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowObjectDisposedException_JsonWorkspace()
    {
        throw new ObjectDisposedException(nameof(JsonWorkspace));
    }

    /// <summary>
    /// Throws an <see cref="ObjectDisposedException"/> for a disposed <see cref="Utf8JsonWriter"/>.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowObjectDisposedException_Utf8JsonWriter()
    {
        throw new ObjectDisposedException(nameof(Utf8JsonWriter));
    }

    /// <summary>
    /// Throws an <see cref="OutOfMemoryException"/> when buffer capacity is exceeded.
    /// </summary>
    /// <param name="capacity">The capacity that was exceeded.</param>
    [DoesNotReturn]
    public static void ThrowOutOfMemoryException(uint capacity)
    {
        throw new OutOfMemoryException(SR.Format(SR.BufferMaximumSizeExceeded, capacity));
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> when a property name is too large.
    /// </summary>
    /// <param name="length">The length of the property name that is too large.</param>
    [DoesNotReturn]
    public static void ThrowPropertyNameTooLargeArgumentException(int length)
    {
        throw GetArgumentException(SR.Format(SR.PropertyNameTooLarge, length));
    }

    [DoesNotReturn]
    public static void ThrowInvalidOperationException_JsonElementWrongType(
        JsonValueKind expectedType, JsonValueKind actualType)
    {
        throw GetJsonElementWrongTypeException(expectedType, actualType);
    }

    /// <summary>
    /// Gets an <see cref="InvalidOperationException"/> for incorrect <see cref="JsonElement"/> type access.
    /// </summary>
    /// <param name="expectedType">The expected JSON value kind.</param>
    /// <param name="actualType">The actual JSON value kind.</param>
    /// <returns>An <see cref="InvalidOperationException"/> with the appropriate message.</returns>
    internal static InvalidOperationException GetJsonElementWrongTypeException(
        JsonValueKind expectedType,
        JsonValueKind actualType)
    {
        return GetInvalidOperationException(
            SR.Format(SR.JsonElementHasWrongType, expectedType, actualType));
    }

    /// <summary>
    /// Gets an <see cref="InvalidOperationException"/> for incorrect <see cref="JsonElement"/> type access.
    /// </summary>
    /// <param name="expectedTypeName">The expected type name.</param>
    /// <param name="actualType">The actual JSON value kind.</param>
    /// <returns>An <see cref="InvalidOperationException"/> with the appropriate message.</returns>
    internal static InvalidOperationException GetJsonElementWrongTypeException(
        string expectedTypeName,
        JsonValueKind actualType)
    {
        return GetInvalidOperationException(
            SR.Format(SR.JsonElementHasWrongType, expectedTypeName, actualType));
    }

    /// <summary>
    /// Gets a printable string representation of a byte value.
    /// </summary>
    /// <param name="value">The byte value to convert.</param>
    /// <returns>A character representation if printable, otherwise a hexadecimal representation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetPrintableString(byte value)
    {
        return IsPrintable(value) ? ((char)value).ToString() : $"0x{value:X2}";
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> for incorrect <see cref="JsonElement"/> type access.
    /// </summary>
    /// <param name="expectedType">The expected JSON token type.</param>
    /// <param name="actualType">The actual JSON token type.</param>
    [DoesNotReturn]
    internal static void ThrowJsonElementWrongTypeException(
        JsonTokenType expectedType,
        JsonTokenType actualType)
    {
        throw GetJsonElementWrongTypeException(expectedType.ToValueKind(), actualType.ToValueKind());
    }

    private static ArgumentException GetArgumentException(string message)
    {
        return new ArgumentException(message);
    }

    private static ArgumentOutOfRangeException GetArgumentOutOfRangeException(string parameterName, string message)
    {
        return new ArgumentOutOfRangeException(parameterName, message);
    }

    private static InvalidOperationException GetInvalidOperationException(string message)
    {
        return new InvalidOperationException(message) { Source = ExceptionSourceValueToRethrowAsJsonException };
    }

    private static InvalidOperationException GetInvalidOperationException(string message, JsonTokenType tokenType)
    {
        return GetInvalidOperationException(SR.Format(SR.InvalidCast, tokenType, message));
    }

    private static InvalidOperationException GetInvalidOperationException(JsonTokenType tokenType)
    {
        return GetInvalidOperationException(SR.Format(SR.InvalidComparison, tokenType));
    }

    // This function will convert an ExceptionResource enum value to the resource string.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string GetResourceString(ref Utf8JsonReader json, ExceptionResource resource, byte nextByte, string characters)
    {
        string character = GetPrintableString(nextByte);

        string message = "";
        switch (resource)
        {
            case ExceptionResource.ArrayDepthTooLarge:
                message = SR.Format(SR.ArrayDepthTooLarge, json.CurrentState.Options.MaxDepth);
                break;

            case ExceptionResource.MismatchedObjectArray:
                message = SR.Format(SR.MismatchedObjectArray, character);
                break;

            case ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd:
                message = SR.TrailingCommaNotAllowedBeforeArrayEnd;
                break;

            case ExceptionResource.TrailingCommaNotAllowedBeforeObjectEnd:
                message = SR.TrailingCommaNotAllowedBeforeObjectEnd;
                break;

            case ExceptionResource.EndOfStringNotFound:
                message = SR.EndOfStringNotFound;
                break;

            case ExceptionResource.RequiredDigitNotFoundAfterSign:
                message = SR.Format(SR.RequiredDigitNotFoundAfterSign, character);
                break;

            case ExceptionResource.RequiredDigitNotFoundAfterDecimal:
                message = SR.Format(SR.RequiredDigitNotFoundAfterDecimal, character);
                break;

            case ExceptionResource.RequiredDigitNotFoundEndOfData:
                message = SR.RequiredDigitNotFoundEndOfData;
                break;

            case ExceptionResource.ExpectedEndAfterSingleJson:
                message = SR.Format(SR.ExpectedEndAfterSingleJson, character);
                break;

            case ExceptionResource.ExpectedEndOfDigitNotFound:
                message = SR.Format(SR.ExpectedEndOfDigitNotFound, character);
                break;

            case ExceptionResource.ExpectedNextDigitEValueNotFound:
                message = SR.Format(SR.ExpectedNextDigitEValueNotFound, character);
                break;

            case ExceptionResource.ExpectedSeparatorAfterPropertyNameNotFound:
                message = SR.Format(SR.ExpectedSeparatorAfterPropertyNameNotFound, character);
                break;

            case ExceptionResource.ExpectedStartOfPropertyNotFound:
                message = SR.Format(SR.ExpectedStartOfPropertyNotFound, character);
                break;

            case ExceptionResource.ExpectedStartOfPropertyOrValueNotFound:
                message = SR.ExpectedStartOfPropertyOrValueNotFound;
                break;

            case ExceptionResource.ExpectedStartOfPropertyOrValueAfterComment:
                message = SR.Format(SR.ExpectedStartOfPropertyOrValueAfterComment, character);
                break;

            case ExceptionResource.ExpectedStartOfValueNotFound:
                message = SR.Format(SR.ExpectedStartOfValueNotFound, character);
                break;

            case ExceptionResource.ExpectedValueAfterPropertyNameNotFound:
                message = SR.ExpectedValueAfterPropertyNameNotFound;
                break;

            case ExceptionResource.FoundInvalidCharacter:
                message = SR.Format(SR.FoundInvalidCharacter, character);
                break;

            case ExceptionResource.InvalidEndOfJsonNonPrimitive:
                message = SR.Format(SR.InvalidEndOfJsonNonPrimitive, json.TokenType);
                break;

            case ExceptionResource.ObjectDepthTooLarge:
                message = SR.Format(SR.ObjectDepthTooLarge, json.CurrentState.Options.MaxDepth);
                break;

            case ExceptionResource.ExpectedFalse:
                message = SR.Format(SR.ExpectedFalse, characters);
                break;

            case ExceptionResource.ExpectedNull:
                message = SR.Format(SR.ExpectedNull, characters);
                break;

            case ExceptionResource.ExpectedTrue:
                message = SR.Format(SR.ExpectedTrue, characters);
                break;

            case ExceptionResource.InvalidCharacterWithinString:
                message = SR.Format(SR.InvalidCharacterWithinString, character);
                break;

            case ExceptionResource.InvalidCharacterAfterEscapeWithinString:
                message = SR.Format(SR.InvalidCharacterAfterEscapeWithinString, character);
                break;

            case ExceptionResource.InvalidHexCharacterWithinString:
                message = SR.Format(SR.InvalidHexCharacterWithinString, character);
                break;

            case ExceptionResource.EndOfCommentNotFound:
                message = SR.EndOfCommentNotFound;
                break;

            case ExceptionResource.ZeroDepthAtEnd:
                message = SR.Format(SR.ZeroDepthAtEnd);
                break;

            case ExceptionResource.ExpectedJsonTokens:
                message = SR.ExpectedJsonTokens;
                break;

            case ExceptionResource.NotEnoughData:
                message = SR.NotEnoughData;
                break;

            case ExceptionResource.ExpectedOneCompleteToken:
                message = SR.ExpectedOneCompleteToken;
                break;

            case ExceptionResource.InvalidCharacterAtStartOfComment:
                message = SR.Format(SR.InvalidCharacterAtStartOfComment, character);
                break;

            case ExceptionResource.UnexpectedEndOfDataWhileReadingComment:
                message = SR.Format(SR.UnexpectedEndOfDataWhileReadingComment);
                break;

            case ExceptionResource.UnexpectedEndOfLineSeparator:
                message = SR.Format(SR.UnexpectedEndOfLineSeparator);
                break;

            case ExceptionResource.InvalidLeadingZeroInNumber:
                message = SR.Format(SR.InvalidLeadingZeroInNumber, character);
                break;

            default:
                Debug.Fail($"The ExceptionResource enum value: {resource} is not part of the switch. Add the appropriate case and exception message.");
                break;
        }

        return message;
    }

    // This function will convert an ExceptionResource enum value to the resource string.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string GetResourceString(ExceptionResource resource, int currentDepth, int maxDepth, byte token, JsonTokenType tokenType)
    {
        string message = "";
        switch (resource)
        {
            case ExceptionResource.MismatchedObjectArray:
                Debug.Assert(token == JsonConstants.CloseBracket || token == JsonConstants.CloseBrace);
                message = (tokenType == JsonTokenType.PropertyName) ?
                    SR.Format(SR.CannotWriteEndAfterProperty, (char)token) :
                    SR.Format(SR.MismatchedObjectArray, (char)token);
                break;

            case ExceptionResource.DepthTooLarge:
                message = SR.Format(SR.DepthTooLarge, currentDepth & JsonConstants.RemoveFlagsBitMask, maxDepth);
                break;

            case ExceptionResource.CannotStartObjectArrayWithoutProperty:
                message = SR.Format(SR.CannotStartObjectArrayWithoutProperty, tokenType);
                break;

            case ExceptionResource.CannotStartObjectArrayAfterPrimitiveOrClose:
                message = SR.Format(SR.CannotStartObjectArrayAfterPrimitiveOrClose, tokenType);
                break;

            case ExceptionResource.CannotWriteValueWithinObject:
                message = SR.Format(SR.CannotWriteValueWithinObject, tokenType);
                break;

            case ExceptionResource.CannotWritePropertyWithinArray:
                message = (tokenType == JsonTokenType.PropertyName) ?
                    SR.Format(SR.CannotWritePropertyAfterProperty) :
                    SR.Format(SR.CannotWritePropertyWithinArray, tokenType);
                break;

            case ExceptionResource.CannotWriteValueAfterPrimitiveOrClose:
                message = SR.Format(SR.CannotWriteValueAfterPrimitiveOrClose, tokenType);
                break;

            case ExceptionResource.CannotWriteWithinString:
                message = SR.CannotWriteWithinString;
                break;

            default:
                Debug.Fail($"The ExceptionResource enum value: {resource} is not part of the switch. Add the appropriate case and exception message.");
                break;
        }

        return message;
    }

    private static bool IsPrintable(byte value) => value >= 0x20 && value < 0x7F;
}

/// <summary>
/// Defines the data types that can cause format exceptions.
/// </summary>
internal enum DataType
{
    Boolean,
    DateOnly,
    DateTime,
    DateTimeOffset,
    TimeOnly,
    TimeSpan,
    OffsetDateTime,
    OffsetDate,
    OffsetTime,
    LocalDate,
    Period,
    Base64String,
    Guid,
    Version,
}