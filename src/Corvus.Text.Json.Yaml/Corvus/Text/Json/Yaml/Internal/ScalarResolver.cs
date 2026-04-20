// <copyright file="ScalarResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.Yaml.Internal;

/// <summary>
/// Resolves plain scalar values to JSON types according to the selected YAML schema.
/// </summary>
internal static class ScalarResolver
{
    /// <summary>
    /// The resolved JSON type for a plain scalar.
    /// </summary>
    internal enum ScalarType : byte
    {
        /// <summary>JSON string.</summary>
        String,

        /// <summary>JSON null.</summary>
        Null,

        /// <summary>JSON true.</summary>
        True,

        /// <summary>JSON false.</summary>
        False,

        /// <summary>JSON integer (decimal).</summary>
        Integer,

        /// <summary>JSON integer (octal, needs conversion).</summary>
        OctalInteger,

        /// <summary>JSON integer (hex, needs conversion).</summary>
        HexInteger,

        /// <summary>JSON floating-point number.</summary>
        Float,

        /// <summary>JSON null (for .inf/.nan which have no JSON representation).</summary>
        InfinityOrNaN,
    }

    /// <summary>
    /// Resolves a plain (unquoted) scalar to its JSON type based on the YAML schema.
    /// </summary>
    /// <param name="value">The UTF-8 bytes of the plain scalar value.</param>
    /// <param name="schema">The YAML schema to use for resolution.</param>
    /// <returns>The resolved scalar type.</returns>
    public static ScalarType Resolve(ReadOnlySpan<byte> value, YamlSchema schema)
    {
        return schema switch
        {
            YamlSchema.Core => ResolveCore(value),
            YamlSchema.Json => ResolveJson(value),
            YamlSchema.Failsafe => ScalarType.String,
            YamlSchema.Yaml11 => ResolveYaml11(value),
            _ => ScalarType.String,
        };
    }

    /// <summary>
    /// Resolves using the YAML 1.2 Core schema.
    /// </summary>
    /// <remarks>
    /// Core schema resolution:
    /// - null: null, Null, NULL, ~ , empty
    /// - bool: true, True, TRUE, false, False, FALSE
    /// - int: [+-]? [0-9]+ | 0o [0-7]+ | 0x [0-9a-fA-F]+
    /// - float: [-+]? ( \. [0-9]+ | [0-9]+ ( \. [0-9]* )? ) ( [eE] [-+]? [0-9]+ )? | [-+]? \. (inf|Inf|INF) | \. (nan|NaN|NAN)
    /// - string: everything else
    /// </remarks>
    private static ScalarType ResolveCore(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return ScalarType.Null;
        }

        byte first = value[0];

        // null
        if (first == (byte)'~' && value.Length == 1)
        {
            return ScalarType.Null;
        }

        if (first == (byte)'n')
        {
            if (value.SequenceEqual("null"u8))
            {
                return ScalarType.Null;
            }
        }
        else if (first == (byte)'N')
        {
            if (value.SequenceEqual("Null"u8) || value.SequenceEqual("NULL"u8))
            {
                return ScalarType.Null;
            }
        }

        // bool
        if (first == (byte)'t')
        {
            if (value.SequenceEqual("true"u8))
            {
                return ScalarType.True;
            }
        }
        else if (first == (byte)'T')
        {
            if (value.SequenceEqual("True"u8) || value.SequenceEqual("TRUE"u8))
            {
                return ScalarType.True;
            }
        }

        if (first == (byte)'f')
        {
            if (value.SequenceEqual("false"u8))
            {
                return ScalarType.False;
            }
        }
        else if (first == (byte)'F')
        {
            if (value.SequenceEqual("False"u8) || value.SequenceEqual("FALSE"u8))
            {
                return ScalarType.False;
            }
        }

        // Integer or float
        return ResolveNumber(value);
    }

    /// <summary>
    /// Resolves using the YAML 1.2 JSON schema (strict JSON-only patterns).
    /// </summary>
    /// <remarks>
    /// JSON schema:
    /// - null: null only
    /// - bool: true, false only
    /// - number: JSON number format only
    /// - string: everything else
    /// </remarks>
    private static ScalarType ResolveJson(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return ScalarType.String;
        }

        if (value.SequenceEqual("null"u8))
        {
            return ScalarType.Null;
        }

        if (value.SequenceEqual("true"u8))
        {
            return ScalarType.True;
        }

        if (value.SequenceEqual("false"u8))
        {
            return ScalarType.False;
        }

        // JSON numbers: must start with digit or minus
        byte first = value[0];
        if (YamlCharacters.IsDigit(first) || first == (byte)'-')
        {
            return IsJsonNumber(value) ? ScalarType.Float : ScalarType.String;
        }

        return ScalarType.String;
    }

    /// <summary>
    /// Resolves using the YAML 1.1 compatibility schema.
    /// </summary>
    /// <remarks>
    /// YAML 1.1 additions:
    /// - bool: y, Y, yes, Yes, YES, n, N, no, No, NO, on, On, ON, off, Off, OFF (plus true/false)
    /// - null: null, Null, NULL, ~, empty
    /// - int: same as core + binary (0b...) and sexagesimal (1:23:45)
    /// </remarks>
    private static ScalarType ResolveYaml11(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return ScalarType.Null;
        }

        byte first = value[0];

        // null
        if (first == (byte)'~' && value.Length == 1)
        {
            return ScalarType.Null;
        }

        if (value.SequenceEqual("null"u8) || value.SequenceEqual("Null"u8) || value.SequenceEqual("NULL"u8))
        {
            return ScalarType.Null;
        }

        // Extended booleans
        if (IsYaml11True(value))
        {
            return ScalarType.True;
        }

        if (IsYaml11False(value))
        {
            return ScalarType.False;
        }

        // Integer or float (same number resolution as core)
        return ResolveNumber(value);
    }

    private static bool IsYaml11True(ReadOnlySpan<byte> value)
    {
        if (value.Length == 1)
        {
            return value[0] == (byte)'y' || value[0] == (byte)'Y';
        }

        return value.SequenceEqual("yes"u8)
            || value.SequenceEqual("Yes"u8)
            || value.SequenceEqual("YES"u8)
            || value.SequenceEqual("true"u8)
            || value.SequenceEqual("True"u8)
            || value.SequenceEqual("TRUE"u8)
            || value.SequenceEqual("on"u8)
            || value.SequenceEqual("On"u8)
            || value.SequenceEqual("ON"u8);
    }

    private static bool IsYaml11False(ReadOnlySpan<byte> value)
    {
        if (value.Length == 1)
        {
            return value[0] == (byte)'n' || value[0] == (byte)'N';
        }

        return value.SequenceEqual("no"u8)
            || value.SequenceEqual("No"u8)
            || value.SequenceEqual("NO"u8)
            || value.SequenceEqual("false"u8)
            || value.SequenceEqual("False"u8)
            || value.SequenceEqual("FALSE"u8)
            || value.SequenceEqual("off"u8)
            || value.SequenceEqual("Off"u8)
            || value.SequenceEqual("OFF"u8);
    }

    /// <summary>
    /// Resolves a numeric value (int or float) from the Core schema number productions.
    /// </summary>
    private static ScalarType ResolveNumber(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return ScalarType.String;
        }

        byte first = value[0];

        // .inf, .nan, -.inf, +.inf
        if (first == (byte)'.')
        {
            return IsInfNan(value) ? ScalarType.InfinityOrNaN : ScalarType.String;
        }

        int start = 0;
        if (first == (byte)'+' || first == (byte)'-')
        {
            if (value.Length < 2)
            {
                return ScalarType.String;
            }

            // -.inf, +.inf
            if (value[1] == (byte)'.')
            {
                return IsInfNan(value.Slice(1)) ? ScalarType.InfinityOrNaN : ScalarType.String;
            }

            start = 1;
        }

        if (!YamlCharacters.IsDigit(value[start]))
        {
            return ScalarType.String;
        }

        // 0x or 0o prefix
        if (value[start] == (byte)'0' && value.Length > start + 1)
        {
            byte prefix = value[start + 1];
            if (prefix == (byte)'x' || prefix == (byte)'X')
            {
                return IsHexInteger(value, start + 2) ? ScalarType.HexInteger : ScalarType.String;
            }

            if (prefix == (byte)'o' || prefix == (byte)'O')
            {
                return IsOctalInteger(value, start + 2) ? ScalarType.OctalInteger : ScalarType.String;
            }
        }

        return ClassifyDecimalNumber(value, start);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInfNan(ReadOnlySpan<byte> value)
    {
        return value.SequenceEqual(".inf"u8)
            || value.SequenceEqual(".Inf"u8)
            || value.SequenceEqual(".INF"u8)
            || value.SequenceEqual(".nan"u8)
            || value.SequenceEqual(".NaN"u8)
            || value.SequenceEqual(".NAN"u8);
    }

    private static bool IsHexInteger(ReadOnlySpan<byte> value, int start)
    {
        if (start >= value.Length)
        {
            return false;
        }

        for (int i = start; i < value.Length; i++)
        {
            if (!YamlCharacters.IsHexDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsOctalInteger(ReadOnlySpan<byte> value, int start)
    {
        if (start >= value.Length)
        {
            return false;
        }

        for (int i = start; i < value.Length; i++)
        {
            if (!YamlCharacters.IsOctalDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Classifies a decimal number as integer or float.
    /// Format: [0-9]+ ( . [0-9]* )? ( [eE] [+-]? [0-9]+ )?
    /// </summary>
    private static ScalarType ClassifyDecimalNumber(ReadOnlySpan<byte> value, int start)
    {
        int i = start;
        bool isFloat = false;

        // Consume digits
        while (i < value.Length && YamlCharacters.IsDigit(value[i]))
        {
            i++;
        }

        // Decimal point
        if (i < value.Length && value[i] == (byte)'.')
        {
            isFloat = true;
            i++;

            // Consume fractional digits
            while (i < value.Length && YamlCharacters.IsDigit(value[i]))
            {
                i++;
            }
        }

        // Exponent
        if (i < value.Length && (value[i] == (byte)'e' || value[i] == (byte)'E'))
        {
            isFloat = true;
            i++;

            // Optional sign
            if (i < value.Length && (value[i] == (byte)'+' || value[i] == (byte)'-'))
            {
                i++;
            }

            // Must have at least one digit
            if (i >= value.Length || !YamlCharacters.IsDigit(value[i]))
            {
                return ScalarType.String;
            }

            while (i < value.Length && YamlCharacters.IsDigit(value[i]))
            {
                i++;
            }
        }

        // Must have consumed everything
        if (i != value.Length)
        {
            return ScalarType.String;
        }

        return isFloat ? ScalarType.Float : ScalarType.Integer;
    }

    /// <summary>
    /// Checks if the value matches JSON number format strictly.
    /// </summary>
    private static bool IsJsonNumber(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return false;
        }

        int i = 0;

        // Optional minus
        if (value[i] == (byte)'-')
        {
            i++;
            if (i >= value.Length)
            {
                return false;
            }
        }

        // Integer part: 0 | [1-9][0-9]*
        if (value[i] == (byte)'0')
        {
            i++;
        }
        else if (YamlCharacters.IsDigit(value[i]))
        {
            while (i < value.Length && YamlCharacters.IsDigit(value[i]))
            {
                i++;
            }
        }
        else
        {
            return false;
        }

        // Fraction
        if (i < value.Length && value[i] == (byte)'.')
        {
            i++;
            if (i >= value.Length || !YamlCharacters.IsDigit(value[i]))
            {
                return false;
            }

            while (i < value.Length && YamlCharacters.IsDigit(value[i]))
            {
                i++;
            }
        }

        // Exponent
        if (i < value.Length && (value[i] == (byte)'e' || value[i] == (byte)'E'))
        {
            i++;
            if (i < value.Length && (value[i] == (byte)'+' || value[i] == (byte)'-'))
            {
                i++;
            }

            if (i >= value.Length || !YamlCharacters.IsDigit(value[i]))
            {
                return false;
            }

            while (i < value.Length && YamlCharacters.IsDigit(value[i]))
            {
                i++;
            }
        }

        return i == value.Length;
    }

    /// <summary>
    /// Writes the resolved scalar value to the JSON writer.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The scalar value bytes.</param>
    /// <param name="scalarType">The resolved scalar type.</param>
    public static void WriteScalar(Utf8JsonWriter writer, ReadOnlySpan<byte> value, ScalarType scalarType)
    {
        switch (scalarType)
        {
            case ScalarType.Null:
                writer.WriteNullValue();
                break;

            case ScalarType.True:
                writer.WriteBooleanValue(true);
                break;

            case ScalarType.False:
                writer.WriteBooleanValue(false);
                break;

            case ScalarType.Integer:
                WriteInteger(writer, value);
                break;

            case ScalarType.Float:
                WriteFloat(writer, value);
                break;

            case ScalarType.HexInteger:
                WriteHexInteger(writer, value);
                break;

            case ScalarType.OctalInteger:
                WriteOctalInteger(writer, value);
                break;

            case ScalarType.InfinityOrNaN:
                // .inf and .nan have no JSON representation; write null
                writer.WriteNullValue();
                break;

            default:
                writer.WriteStringValue(value);
                break;
        }
    }

    /// <summary>
    /// Writes a resolved scalar as a JSON property value.
    /// </summary>
    public static void WriteScalarProperty(Utf8JsonWriter writer, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, ScalarType scalarType)
    {
        switch (scalarType)
        {
            case ScalarType.Null:
                writer.WriteNull(key);
                break;

            case ScalarType.True:
                writer.WriteBoolean(key, true);
                break;

            case ScalarType.False:
                writer.WriteBoolean(key, false);
                break;

            case ScalarType.Integer:
                writer.WritePropertyName(key);
                WriteInteger(writer, value);
                break;

            case ScalarType.Float:
                writer.WritePropertyName(key);
                WriteFloat(writer, value);
                break;

            case ScalarType.HexInteger:
                writer.WritePropertyName(key);
                WriteHexInteger(writer, value);
                break;

            case ScalarType.OctalInteger:
                writer.WritePropertyName(key);
                WriteOctalInteger(writer, value);
                break;

            case ScalarType.InfinityOrNaN:
                writer.WriteNull(key);
                break;

            default:
                writer.WriteString(key, value);
                break;
        }
    }

    private static void WriteInteger(Utf8JsonWriter writer, ReadOnlySpan<byte> value)
    {
        // Try to parse as long first, which covers all JSON-safe integer ranges
        int start = 0;
        bool negative = false;

        if (value[0] == (byte)'+')
        {
            start = 1;
        }
        else if (value[0] == (byte)'-')
        {
            negative = true;
            start = 1;
        }

        // Fast path: parse digits directly to avoid string allocation
        long result = 0;
        bool overflow = false;
        for (int i = start; i < value.Length; i++)
        {
            int digit = value[i] - YamlConstants.Zero;
            long prev = result;
            result = (result * 10) + digit;
            if (result < prev)
            {
                overflow = true;
                break;
            }
        }

        if (!overflow)
        {
            if (negative)
            {
                result = -result;
            }

            writer.WriteNumberValue(result);
        }
        else
        {
            // Fall back to writing as a raw number string
            writer.WriteRawValue(value, skipInputValidation: true);
        }
    }

    private static void WriteFloat(Utf8JsonWriter writer, ReadOnlySpan<byte> value)
    {
        // Parse as double to normalize the representation (e.g., 450.00 → 450).
        // Use Utf8Parser for zero-allocation parsing from UTF-8 bytes.
        if (System.Buffers.Text.Utf8Parser.TryParse(value, out double d, out int bytesConsumed)
            && bytesConsumed == value.Length
            && !double.IsInfinity(d)
            && !double.IsNaN(d))
        {
            writer.WriteNumberValue(d);
        }
        else
        {
            // Fallback: write the raw decimal representation
            writer.WriteRawValue(value, skipInputValidation: true);
        }
    }

    private static void WriteHexInteger(Utf8JsonWriter writer, ReadOnlySpan<byte> value)
    {
        // Format: [+-]?0x[0-9a-fA-F]+
        int start = 0;
        bool negative = false;

        if (value[0] == (byte)'+')
        {
            start = 1;
        }
        else if (value[0] == (byte)'-')
        {
            negative = true;
            start = 1;
        }

        // Skip "0x"
        start += 2;

        long result = 0;
        for (int i = start; i < value.Length; i++)
        {
            result = (result << 4) | (uint)HexValue(value[i]);
        }

        if (negative)
        {
            result = -result;
        }

        writer.WriteNumberValue(result);
    }

    private static void WriteOctalInteger(Utf8JsonWriter writer, ReadOnlySpan<byte> value)
    {
        // Format: [+-]?0o[0-7]+
        int start = 0;
        bool negative = false;

        if (value[0] == (byte)'+')
        {
            start = 1;
        }
        else if (value[0] == (byte)'-')
        {
            negative = true;
            start = 1;
        }

        // Skip "0o"
        start += 2;

        long result = 0;
        for (int i = start; i < value.Length; i++)
        {
            result = (result << 3) | (uint)(value[i] - YamlConstants.Zero);
        }

        if (negative)
        {
            result = -result;
        }

        writer.WriteNumberValue(result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HexValue(byte c)
    {
        if ((uint)(c - (byte)'0') <= 9)
        {
            return c - (byte)'0';
        }

        if ((uint)(c - (byte)'a') <= 5)
        {
            return c - (byte)'a' + 10;
        }

        return c - (byte)'A' + 10;
    }
}