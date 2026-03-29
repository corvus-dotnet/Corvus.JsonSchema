// <copyright file="JsonConstants.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json;

/// <summary>
/// Provides constants for common JSON characters and tokens represented as bytes.
/// </summary>
internal static partial class JsonConstants
{
    /// <summary>The opening brace character '{'.</summary>
    public const byte OpenBrace = (byte)'{';

    /// <summary>The closing brace character '}'.</summary>
    public const byte CloseBrace = (byte)'}';

    /// <summary>The opening bracket character '['.</summary>
    public const byte OpenBracket = (byte)'[';

    /// <summary>The closing bracket character ']'.</summary>
    public const byte CloseBracket = (byte)']';

    /// <summary>The space character ' '.</summary>
    public const byte Space = (byte)' ';

    /// <summary>The carriage return character '\r'.</summary>
    public const byte CarriageReturn = (byte)'\r';

    /// <summary>The line feed character '\n'.</summary>
    public const byte LineFeed = (byte)'\n';

    /// <summary>The tab character '\t'.</summary>
    public const byte Tab = (byte)'\t';

    /// <summary>The list separator character ','.</summary>
    public const byte ListSeparator = (byte)',';

    /// <summary>The key-value separator character ':'.</summary>
    public const byte KeyValueSeparator = (byte)':';

    /// <summary>The quote character '"'.</summary>
    public const byte Quote = (byte)'"';

    /// <summary>The backslash character '\'.</summary>
    public const byte BackSlash = (byte)'\\';

    /// <summary>The forward slash character '/'.</summary>
    public const byte Slash = (byte)'/';

    /// <summary>The backspace character '\b'.</summary>
    public const byte BackSpace = (byte)'\b';

    /// <summary>The form feed character '\f'.</summary>
    public const byte FormFeed = (byte)'\f';

    /// <summary>The asterisk character '*'.</summary>
    public const byte Asterisk = (byte)'*';

    /// <summary>The colon character ':'.</summary>
    public const byte Colon = (byte)':';

    /// <summary>The period character '.'.</summary>
    public const byte Period = (byte)'.';

    /// <summary>The plus character '+'.</summary>
    public const byte Plus = (byte)'+';

    /// <summary>The hyphen character '-'.</summary>
    public const byte Hyphen = (byte)'-';

    /// <summary>The UTC offset token 'Z'.</summary>
    public const byte UtcOffsetToken = (byte)'Z';

    /// <summary>The time prefix 'T'.</summary>
    public const byte TimePrefix = (byte)'T';

    public const string NewLineLineFeed = "\n";

    public const string NewLineCarriageReturnLineFeed = "\r\n";

    // \u2028 and \u2029 are considered respectively line and paragraph separators
    // UTF-8 representation for them is E2, 80, A8/A9
    public const byte StartingByteOfNonStandardSeparator = 0xE2;

    public static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    public static ReadOnlySpan<byte> TrueValue => "true"u8;

    public static ReadOnlySpan<byte> FalseValue => "false"u8;

    public static ReadOnlySpan<byte> NullValue => "null"u8;

    public static ReadOnlySpan<byte> ZeroValue => "0"u8;

    public static ReadOnlySpan<byte> OneValue => "1"u8;

    public static ReadOnlySpan<byte> ToStringTrueValue => "True"u8;

    public static ReadOnlySpan<byte> ToStringFalseValue => "False"u8;

    public static ReadOnlySpan<char> ToStringTrueValueUtf16 => "True";

    public static ReadOnlySpan<char> ToStringFalseValueUtf16 => "False";

    public static string ToStringTrueValueUtf16String => "True";

    public static string ToStringFalseValueUtf16String => "False";

    public static byte[] TrueValueArray { get; } = TrueValue.ToArray();

    public static byte[] FalseValueArray { get; } = FalseValue.ToArray();

    public static byte[] NullValueArray { get; } = NullValue.ToArray();

    public static byte[] ZeroValueArray { get; } = ZeroValue.ToArray();

    public static byte[] OneValueArray { get; } = OneValue.ToArray();

    public static ReadOnlySpan<byte> NaNValue => "NaN"u8;

    public static ReadOnlySpan<byte> PositiveInfinityValue => "Infinity"u8;

    public static ReadOnlySpan<byte> NegativeInfinityValue => "-Infinity"u8;

    // Used to search for the end of a number
    public static ReadOnlySpan<byte> Delimiters => ",}] \n\r\t/"u8;

    // Explicitly skipping ReverseSolidus since that is handled separately
    public static ReadOnlySpan<byte> EscapableChars => "\"nrt/ubf"u8;

    public const int RemoveFlagsBitMask = 0x7FFFFFFF;

    // In the worst case, an ASCII character represented as a single utf-8 byte could expand 6x when escaped.
    // For example: '+' becomes '\u0043'
    // Escaping surrogate pairs (represented by 3 or 4 utf-8 bytes) would expand to 12 bytes (which is still <= 6x).
    // The same factor applies to utf-16 characters.
    public const int MaxExpansionFactorWhileEscaping = 6;

    public const int MaxExpansionFactorWhileEncodingPointer = 2;

    // In the worst case, a single UTF-16 character could be expanded to 3 UTF-8 bytes.
    // Only surrogate pairs expand to 4 UTF-8 bytes but that is a transformation of 2 UTF-16 characters going to 4 UTF-8 bytes (factor of 2).
    // All other UTF-16 characters can be represented by either 1 or 2 UTF-8 bytes.
    public const int MaxExpansionFactorWhileTranscoding = 3;

    // When transcoding from UTF8 -> UTF16, the byte count threshold where we rent from the array pool before performing a normal alloc.
    public const long ArrayPoolMaxSizeBeforeUsingNormalAlloc =
#if NET
        1024 * 1024 * 1024; // ArrayPool limit increased in .NET 6

#else
        1024 * 1024;
#endif

    // The maximum number of characters allowed when writing raw UTF-16 JSON. This is the maximum length that we can guarantee can
    // be safely transcoded to UTF-8 and fit within an integer-length span, given the max expansion factor of a single character (3).
    public const int MaxUtf16RawValueLength = int.MaxValue / MaxExpansionFactorWhileTranscoding;

    public const int MaxEscapedTokenSize = 1_000_000_000;   // Max size for already escaped value.
    public const int MaxUnescapedTokenSize = MaxEscapedTokenSize / MaxExpansionFactorWhileEscaping;  // 166_666_666 bytes
    public const int MaxCharacterTokenSize = MaxEscapedTokenSize / MaxExpansionFactorWhileEscaping; // 166_666_666 characters

    public const int MaximumFormatBooleanLength = 5;

    public const int MaximumFormatInt64Length = 20;   // 19 + sign (i.e. -9223372036854775808)
    public const int MaximumFormatUInt32Length = 10;  // i.e. 4294967295
    public const int MaximumFormatUInt64Length = 20;  // i.e. 18446744073709551615
    public const int MaximumFormatDoubleLength = 128;  // default (i.e. 'G'), using 128 (rather than say 32) to be future-proof.
    public const int MaximumFormatSingleLength = 128;  // default (i.e. 'G'), using 128 (rather than say 32) to be future-proof.
    public const int MaximumFormatDecimalLength = 31; // default (i.e. 'G')
    public const int MaximumFormatGuidLength = 36;    // default (i.e. 'D'), 8 + 4 + 4 + 4 + 12 + 4 for the hyphens (e.g. 094ffa0a-0442-494d-b452-04003fa755cc)
    public const int MaximumFormatInt128Length = 40;

    public const int InitialFormatBigIntegerLength = 80;

    public const int InitialFormatBigNumberLength = InitialFormatBigIntegerLength + MaximumFormatUInt32Length;

    public const int MaximumFormatUInt128Length = 39;

    public const int MaximumFormatNumberLength = 2048; // We only permit up to 2048 characters when writing a number in order to prevent DoS scenarios where an extremely large number is written that would cause excessive memory usage when parsed.
    public const int MaximumFormatHalfLength = 20;

    public const int MaximumEscapedGuidLength = MaxExpansionFactorWhileEscaping * MaximumFormatGuidLength;

    public const int MaximumFormatDateTimeLength = 27;    // StandardFormat 'O', e.g. 2017-06-12T05:30:45.7680000
    public const int MaximumFormatDateLength = 10;    // StandardFormat 'O', e.g. 2017-06-12
    public const int MaximumFormatOffsetDateLength = 16;    // StandardFormat 'O', e.g. 2017-06-12-07:00
    public const int MaximumFormatOffsetTimeLength = 22;    // StandardFormat 'O', e.g. 05:30:45.7680000-07:00
    public const int MaximumFormatPeriodLength = ((MaximumFormatUInt32Length + 1) * 7) + 13;    // StandardFormat e.g. P2147483647Y2147483647M2147483647W2147483647DT2147483647H2147483647M-2147483647.123456789S
    public const int MaximumFormatDateTimeOffsetLength = 33;  // StandardFormat 'O', e.g. 2017-06-12T05:30:45.7680000-07:00
    public const int MaxDateTimeUtcOffsetHours = 14; // The UTC offset portion of a TimeSpan or DateTime can be no more than 14 hours and no less than -14 hours.
    public const int DateTimeNumFractionDigits = 7;  // TimeSpan and DateTime formats allow exactly up to many digits for specifying the fraction after the seconds.
    public const int MaxDateTimeFraction = 9_999_999;  // The largest fraction expressible by TimeSpan and DateTime formats
    public const int DateTimeParseNumFractionDigits = 16; // The maximum number of fraction digits the Json DateTime parser allows

    public const int MaximumDateTimeOffsetParseLength = (MaximumFormatDateTimeOffsetLength +
        (DateTimeParseNumFractionDigits - DateTimeNumFractionDigits)); // Like StandardFormat 'O' for DateTimeOffset, but allowing 9 additional (up to 16) fraction digits.

    public const int MinimumDateTimeParseLength = 10; // YYYY-MM-DD
    public const int MinimumDateTimeOffsetParseLength = 19; // YYYY-MM-DDTHH:MM:SS
    public const int MinimumDateParseLength = 10; // YYYY-MM-DD
    public const int MinimumTimeParseLength = 8; // HH:MM:SS
    public const int MaximumEscapedDateTimeOffsetParseLength = MaxExpansionFactorWhileEscaping * MaximumDateTimeOffsetParseLength;

    public const int MaximumLiteralLength = 5; // Must be able to fit null, true, & false.

    // Encoding Helpers
    public const char HighSurrogateStart = '\ud800';

    public const char HighSurrogateEnd = '\udbff';

    public const char LowSurrogateStart = '\udc00';

    public const char LowSurrogateEnd = '\udfff';

    public const int UnicodePlane01StartValue = 0x10000;

    public const int HighSurrogateStartValue = 0xD800;

    public const int HighSurrogateEndValue = 0xDBFF;

    public const int LowSurrogateStartValue = 0xDC00;

    public const int LowSurrogateEndValue = 0xDFFF;

    public const int BitShiftBy10 = 0x400;

    // The maximum number of parameters a constructor can have where it can be considered
    // for a path on deserialization where we don't box the constructor arguments.
    public const int UnboxedParameterCountThreshold = 4;

    // Two space characters is the default indentation.
    public const char DefaultIndentCharacter = ' ';

    public const char TabIndentCharacter = '\t';

    public const int DefaultIndentSize = 2;

    public const int MinimumIndentSize = 0;

    public const int MaximumIndentSize = 127; // If this value is changed, the impact on the options masking used in the JsonWriterOptions struct must be checked carefully.
}