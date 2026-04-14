// <copyright file="BuiltInFunctions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Registry and implementation of JSONata built-in functions.
/// Phase 2 includes core aggregate and utility functions;
/// Phase 3 will add the full ~60-function library.
/// </summary>
internal static class BuiltInFunctions
{
    private static readonly Regex WhitespaceCollapseRegex = new(@"\s+", RegexOptions.Compiled);

    private static readonly string[] Iso8601Formats =
    [
        "yyyy-MM-ddTHH:mm:ss.fffzzz",
        "yyyy-MM-ddTHH:mm:ss.fffZ",
        "yyyy-MM-ddTHH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:sszzz",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mmzzz",
        "yyyy-MM-ddTHH:mmZ",
        "yyyy-MM-ddTHH:mm",
        "yyyy-MM-dd",
        "yyyy-MM",
        "yyyy",
    ];

    /// <summary>
    /// Evaluator that returns the current context as a singleton sequence.
    /// Used for context-binding (0-arg calls like <c>$number()</c>).
    /// </summary>
    private static readonly ExpressionEvaluator ContextArg = static (in JsonElement input, Environment env) => new Sequence(input);

    /// <summary>
    /// Tries to get a compile-time function compiler for the given built-in name.
    /// </summary>
    /// <param name="name">The function name (without <c>$</c> prefix).</param>
    /// <returns>A function that takes compiled argument evaluators and returns a compiled evaluator, or <c>null</c> if not a built-in.</returns>
    public static Func<ExpressionEvaluator[], ExpressionEvaluator>? TryGetCompiler(string name)
    {
        return name switch
        {
            "count" => CompileCount,
            "sum" => CompileSum,
            "max" => CompileMax,
            "min" => CompileMin,
            "average" => CompileAverage,
            "string" => CompileToString,
            "number" => CompileToNumber,
            "boolean" => CompileToBoolean,
            "not" => CompileNot,
            "exists" => CompileExists,
            "type" => CompileType,
            "length" => CompileLength,
            "keys" => CompileKeys,
            "values" => CompileValues,
            "append" => CompileAppend,
            "sort" => CompileSortFunc,
            "reverse" => CompileReverse,
            "abs" => CompileAbs,
            "floor" => CompileFloor,
            "ceil" => CompileCeil,
            "round" => CompileRound,
            "power" => CompilePower,
            "sqrt" => CompileSqrt,
            "substring" => CompileSubstring,
            "substringBefore" => CompileSubstringBefore,
            "substringAfter" => CompileSubstringAfter,
            "uppercase" => CompileUppercase,
            "lowercase" => CompileLowercase,
            "trim" => CompileTrim,
            "join" => CompileJoin,
            "split" => CompileSplit,
            "contains" => CompileContains,
            "map" => CompileMap,
            "filter" => CompileFilterFunc,
            "reduce" => CompileReduce,
            "each" => CompileEach,
            "merge" => CompileMerge,
            "spread" => CompileSpread,
            "lookup" => CompileLookup,
            "distinct" => CompileDistinct,
            "flatten" => CompileFlatten,
            "single" => CompileSingle,
            "sift" => CompileSift,
            "pad" => CompilePad,
            "match" => CompileMatch,
            "replace" => CompileReplace,
            "base64encode" => CompileBase64Encode,
            "base64decode" => CompileBase64Decode,
            "encodeUrlComponent" => CompileEncodeUrlComponent,
            "decodeUrlComponent" => CompileDecodeUrlComponent,
            "encodeUrl" => CompileEncodeUrl,
            "decodeUrl" => CompileDecodeUrl,
            "random" => CompileRandom,
            "formatNumber" => CompileFormatNumber,
            "formatBase" => CompileFormatBase,
            "shuffle" => CompileShuffle,
            "zip" => CompileZip,
            "error" => CompileError,
            "assert" => CompileAssert,
            "now" => CompileNow,
            "millis" => CompileMillis,
            "fromMillis" => CompileFromMillis,
            "toMillis" => CompileToMillis,
            "formatInteger" => CompileFormatInteger,
            "parseInteger" => CompileParseInteger,
            "eval" => CompileEval,
            _ => null,
        };
    }

    // --- Aggregate functions ---
    private static ExpressionEvaluator CompileCount(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_CountExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            int count;
            if (seq.IsUndefined)
            {
                count = 0;
            }
            else if (seq.IsSingleton && seq.FirstOrDefault.ValueKind == JsonValueKind.Array)
            {
                count = seq.FirstOrDefault.GetArrayLength();
            }
            else
            {
                count = seq.Count;
            }

            return Sequence.FromDouble(count, env.Workspace);
        };
    }

    private static ExpressionEvaluator CompileSum(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_SumExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            double total = 0;
            EnumerateNumericValues(seq, ref total, static (ref double acc, double val) => acc += val);
            return Sequence.FromDouble(total, env.Workspace);
        };
    }

    private static ExpressionEvaluator CompileMax(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_MaxExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            double max = double.NegativeInfinity;
            bool found = false;
            EnumerateNumericValues(seq, ref max, (ref double acc, double val) =>
            {
                if (val > acc)
                {
                    acc = val;
                }

                found = true;
            });

            if (!found)
            {
                return Sequence.Undefined;
            }

            return Sequence.FromDouble(max, env.Workspace);
        };
    }

    private static ExpressionEvaluator CompileMin(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_MinExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            double min = double.PositiveInfinity;
            bool found = false;
            EnumerateNumericValues(seq, ref min, (ref double acc, double val) =>
            {
                if (val < acc)
                {
                    acc = val;
                }

                found = true;
            });

            if (!found)
            {
                return Sequence.Undefined;
            }

            return Sequence.FromDouble(min, env.Workspace);
        };
    }

    private static ExpressionEvaluator CompileAverage(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_AverageExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            double total = 0;
            int count = 0;
            EnumerateNumericValues(seq, ref total, (ref double acc, double val) =>
            {
                acc += val;
                count++;
            });

            if (count == 0)
            {
                return Sequence.Undefined;
            }

            return Sequence.FromDouble(total / count, env.Workspace);
        };
    }

    // --- Type coercion functions ---
    private static ExpressionEvaluator CompileToString(ExpressionEvaluator[] args)
    {
        if (args.Length > 2)
        {
            throw new JsonataException("T0410", SR.T0410_StringExpectsAtMost2Arguments, 0);
        }

        // 0 args → use current context
        var arg = args.Length >= 1 ? args[0] : ContextArg;
        var prettyArg = args.Length >= 2 ? args[1] : null;

        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);

            // Functions/lambdas stringify to ""
            if (seq.IsLambda)
            {
                return new Sequence(JsonataHelpers.EmptyString());
            }

            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            // Non-finite numeric values (Infinity, NaN) cannot be stringified
            if (seq.IsNonFinite)
            {
                throw new JsonataException("D3001", SR.D3001_AttemptingToInvokeStringFunctionOnInfinityOrNan, 0);
            }

            var element = seq.FirstOrDefault;

            // When the context itself is undefined (e.g. $string() with no data),
            // the element will have ValueKind.Undefined even though the sequence is not empty.
            if (element.ValueKind == JsonValueKind.Undefined)
            {
                return Sequence.Undefined;
            }

            // Check for NaN/Infinity (non-finite numbers cannot be stringified)
            if (element.ValueKind == JsonValueKind.Number)
            {
                double d = element.GetDouble();
                if (double.IsNaN(d) || double.IsInfinity(d))
                {
                    throw new JsonataException("D3001", SR.D3001_AttemptingToInvokeStringFunctionOnInfinityOrNan, 0);
                }
            }

            bool prettyPrint = false;
            if (prettyArg is not null)
            {
                var prettySeq = prettyArg(input, env);
                if (!prettySeq.IsUndefined)
                {
                    var prettyElem = prettySeq.FirstOrDefault;
                    if (prettyElem.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
                    {
                        throw new JsonataException("T0410", SR.T0410_SecondArgumentOfStringMustBeABoolean, 0);
                    }

                    prettyPrint = prettyElem.ValueKind == JsonValueKind.True;
                }
            }

            // For arrays and objects, produce JSON
            if (element.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                string json = StringifyElement(element, prettyPrint);
                return new Sequence(JsonataHelpers.StringFromString(json, env.Workspace));
            }

            string result = FunctionalCompiler.CoerceElementToString(element);
            return new Sequence(JsonataHelpers.StringFromString(result, env.Workspace));
        };
    }

    /// <summary>
    /// Serializes a JSON element to a compact or pretty-printed string,
    /// replacing lambda values with empty strings and formatting numbers like JS.
    /// </summary>
    private static string StringifyElement(JsonElement element, bool prettyPrint)
    {
        using var ms = new MemoryStream(256);
        using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions
        {
            Indented = prettyPrint,
            NewLine = "\n",
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        WriteStringifiedElement(element, writer);
        writer.Flush();

        ms.Position = 0;
        using var reader = new StreamReader(ms);
        return reader.ReadToEnd();
    }

    private static void WriteStringifiedElement(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in element.EnumerateObject())
                {
                    writer.WritePropertyName(prop.Name);
                    WriteStringifiedElement(prop.Value, writer);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteStringifiedElement(item, writer);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.Number:
                double d = element.GetDouble();
                if (double.IsNaN(d) || double.IsInfinity(d))
                {
                    writer.WriteNullValue();
                }
                else
                {
#if NET
                    // On modern .NET, Ryu produces shortest roundtrip output and
                    // G15 toPrecision(15) semantics are already satisfied.
                    writer.WriteNumberValue(d);
#else
                    // On netstandard2.0 / net481, Utf8Formatter uses G17 which
                    // produces non-shortest representations (e.g. 39.399999999999999
                    // instead of 39.4). Format via G15 to match JSONata's
                    // Number(val.toPrecision(15)) and write the raw UTF-8 bytes.
                    string g15 = d.ToString("G15", CultureInfo.InvariantCulture);
                    Span<byte> numBuf = stackalloc byte[32];
                    for (int c = 0; c < g15.Length; c++)
                    {
                        numBuf[c] = (byte)g15[c];
                    }

                    using var tmpDoc = FixedJsonValueDocument<JsonElement>.ForNumberFromSpan(numBuf.Slice(0, g15.Length));
                    tmpDoc.RootElement.WriteTo(writer);
#endif
                }

                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static ExpressionEvaluator CompileToNumber(ExpressionEvaluator[] args)
    {
        if (args.Length > 1)
        {
            throw new JsonataException("T0410", SR.T0410_NumberExpects0Or1Arguments, 0);
        }

        // 0 args → use current context
        var arg = args.Length == 1 ? args[0] : ContextArg;
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);

            if (seq.IsLambda)
            {
                throw new JsonataException("T0410", SR.T0410_Argument1OfFunctionNumberIsNotOfTheCorrectType, 0);
            }

            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            var element = seq.FirstOrDefault;

            // Reject types that cannot be converted to number
            if (element.ValueKind is JsonValueKind.Array or JsonValueKind.Object or JsonValueKind.Null)
            {
                throw new JsonataException("T0410", SR.T0410_Argument1OfFunctionNumberIsNotOfTheCorrectType, 0);
            }

            // Booleans: true → 1, false → 0
            if (element.ValueKind == JsonValueKind.True)
            {
                return Sequence.FromDouble(1, env.Workspace);
            }

            if (element.ValueKind == JsonValueKind.False)
            {
                return Sequence.FromDouble(0, env.Workspace);
            }

            if (element.ValueKind == JsonValueKind.Number)
            {
                double num = element.GetDouble();
                if (double.IsInfinity(num))
                {
                    throw new JsonataException("D3030", SR.D3030_UnableToCastValueToANumber, 0);
                }

                return Sequence.FromDouble(num, env.Workspace);
            }

            // String conversion
            if (element.ValueKind == JsonValueKind.String)
            {
#if NET
                using UnescapedUtf16JsonString utf16 = element.GetUtf16String();
                ReadOnlySpan<char> s = utf16.Span;
                if (s.Length == 0)
                {
                    throw new JsonataException("D3030", SR.D3030_UnableToCastValueToANumber, 0);
                }

                if (s.Length > 2 && s[0] == '0')
                {
                    char prefix = s[1];
                    if (prefix is 'x' or 'X')
                    {
                        if (long.TryParse(s.Slice(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hexVal))
                        {
                            return Sequence.FromDouble(hexVal, env.Workspace);
                        }

                        throw new JsonataException("D3030", SR.D3030_UnableToCastValueToANumber, 0);
                    }

                    if (prefix is 'b' or 'B')
                    {
                        if (TryParseBinary(s.Slice(2), out long binVal))
                        {
                            return Sequence.FromDouble(binVal, env.Workspace);
                        }

                        throw new JsonataException("D3030", SR.D3030_UnableToCastValueToANumber, 0);
                    }

                    if (prefix is 'o' or 'O')
                    {
                        if (TryParseOctal(s.Slice(2), out long octVal))
                        {
                            return Sequence.FromDouble(octVal, env.Workspace);
                        }

                        throw new JsonataException("D3030", SR.D3030_UnableToCastValueToANumber, 0);
                    }
                }

                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                {
                    if (double.IsInfinity(parsed))
                    {
                        throw new JsonataException("D3030", SR.D3030_UnableToCastValueToANumber, 0);
                    }

                    return Sequence.FromDouble(parsed, env.Workspace);
                }

                throw new JsonataException("D3030", SR.D3030_UnableToCastValueToANumber, 0);
#else
                string? s2 = element.GetString();
                if (s2 is null || s2.Length == 0)
                {
                    throw new JsonataException("D3030", SR.D3030_UnableToCastValueToANumber, 0);
                }

                // Handle hex (0x/0X), binary (0b/0B), and octal (0o/0O) prefixes
                if (s2.Length > 2 && s2[0] == '0')
                {
                    char prefix = s2[1];
                    if (prefix is 'x' or 'X')
                    {
                        if (long.TryParse(s2.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hexVal))
                        {
                            return Sequence.FromDouble(hexVal, env.Workspace);
                        }

                        throw new JsonataException("D3030", SR.D3030_UnableToCastValueToANumber, 0);
                    }

                    if (prefix is 'b' or 'B')
                    {
                        if (TryParseBinary(s2, 2, out long binVal))
                        {
                            return Sequence.FromDouble(binVal, env.Workspace);
                        }

                        throw new JsonataException("D3030", SR.D3030_UnableToCastValueToANumber, 0);
                    }

                    if (prefix is 'o' or 'O')
                    {
                        if (TryParseOctal(s2, 2, out long octVal))
                        {
                            return Sequence.FromDouble(octVal, env.Workspace);
                        }

                        throw new JsonataException("D3030", SR.D3030_UnableToCastValueToANumber, 0);
                    }
                }

                if (double.TryParse(s2, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                {
                    if (double.IsInfinity(parsed))
                    {
                        throw new JsonataException("D3030", SR.D3030_UnableToCastValueToANumber, 0);
                    }

                    return Sequence.FromDouble(parsed, env.Workspace);
                }

                throw new JsonataException("D3030", SR.D3030_UnableToCastValueToANumber, 0);
#endif
            }

            return Sequence.Undefined;
        };
    }

    private static bool TryParseBinary(string digits, int startIndex, out long result)
    {
        result = 0;
        if (startIndex >= digits.Length)
        {
            return false;
        }

        for (int i = startIndex; i < digits.Length; i++)
        {
            char c = digits[i];
            if (c is not ('0' or '1'))
            {
                return false;
            }

            result = (result << 1) | (long)(uint)(c - '0');
        }

        return true;
    }

#if NET
    private static bool TryParseBinary(ReadOnlySpan<char> digits, out long result)
    {
        result = 0;
        if (digits.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < digits.Length; i++)
        {
            char c = digits[i];
            if (c is not ('0' or '1'))
            {
                return false;
            }

            result = (result << 1) | (long)(uint)(c - '0');
        }

        return true;
    }
#endif

    private static bool TryParseOctal(string digits, int startIndex, out long result)
    {
        result = 0;
        if (startIndex >= digits.Length)
        {
            return false;
        }

        for (int i = startIndex; i < digits.Length; i++)
        {
            char c = digits[i];
            if (c < '0' || c > '7')
            {
                return false;
            }

            result = (result << 3) | (long)(uint)(c - '0');
        }

        return true;
    }

#if NET
    private static bool TryParseOctal(ReadOnlySpan<char> digits, out long result)
    {
        result = 0;
        if (digits.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < digits.Length; i++)
        {
            char c = digits[i];
            if (c < '0' || c > '7')
            {
                return false;
            }

            result = (result << 3) | (long)(uint)(c - '0');
        }

        return true;
    }
#endif

    private static ExpressionEvaluator CompileToBoolean(ExpressionEvaluator[] args)
    {
        if (args.Length > 1)
        {
            throw new JsonataException("T0410", SR.T0410_BooleanExpects0Or1Arguments, 0);
        }

        var arg = args.Length == 1 ? args[0] : ContextArg;
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);

            // Functions/lambdas are not boolean-convertible — return false
            if (seq.IsLambda)
            {
                return new Sequence(JsonataHelpers.BooleanElement(false));
            }

            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            return new Sequence(JsonataHelpers.BooleanElement(FunctionalCompiler.IsTruthy(seq)));
        };
    }

    private static ExpressionEvaluator CompileNot(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_NotExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            return new Sequence(JsonataHelpers.BooleanElement(!FunctionalCompiler.IsTruthy(seq)));
        };
    }

    private static ExpressionEvaluator CompileExists(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_ExistsExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);

            // Lambda/function references exist
            if (seq.IsLambda)
            {
                return new Sequence(JsonataHelpers.BooleanElement(true));
            }

            return new Sequence(JsonataHelpers.BooleanElement(!seq.IsUndefined));
        };
    }

    private static ExpressionEvaluator CompileType(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_TypeExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);

            if (seq.IsLambda)
            {
                return new Sequence(JsonataHelpers.StringFromString("function", env.Workspace));
            }

            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            string typeName = seq.FirstOrDefault.ValueKind switch
            {
                JsonValueKind.Null => "null",
                JsonValueKind.Number => "number",
                JsonValueKind.String => "string",
                JsonValueKind.True or JsonValueKind.False => "boolean",
                JsonValueKind.Array => "array",
                JsonValueKind.Object => "object",
                _ => "undefined",
            };

            return new Sequence(JsonataHelpers.StringFromString(typeName, env.Workspace));
        };
    }

    // --- String functions (core subset) ---
    private static ExpressionEvaluator CompileLength(ExpressionEvaluator[] args)
    {
        if (args.Length > 1)
        {
            throw new JsonataException("T0410", SR.T0410_LengthExpects0Or1Arguments, 0);
        }

        bool isContextArg = args.Length == 0;
        var arg = isContextArg ? ContextArg : args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            var el = seq.FirstOrDefault;
            if (el.ValueKind == JsonValueKind.String)
            {
#if NET
                using UnescapedUtf16JsonString utf16 = el.GetUtf16String();
                int len = CountCodePoints(utf16.Span);
                return Sequence.FromDouble(len, env.Workspace);
#else
                string str = el.GetString() ?? string.Empty;
                int len = CountCodePoints(str);
                return Sequence.FromDouble(len, env.Workspace);
#endif
            }

            // Wrong type: T0411 when context-derived, T0410 when explicit arg
            throw new JsonataException(
                isContextArg ? "T0411" : "T0410",
                SR.T0410_Argument1OfFunctionLengthIsNotCorrectType,
                0);
        };
    }

    private static ExpressionEvaluator CompileSubstring(ExpressionEvaluator[] args)
    {
        if (args.Length < 2 || args.Length > 3)
        {
            throw new JsonataException("T0410", SR.T0410_SubstringExpects2Or3Arguments, 0);
        }

        var strArg = args[0];
        var startArg = args[1];
        var lengthArg = args.Length > 2 ? args[2] : null;

        return (in JsonElement input, Environment env) =>
        {
            var strSeq = strArg(input, env);
            var startSeq = startArg(input, env);

            if (strSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            var strElem = strSeq.FirstOrDefault;
            if (strElem.ValueKind != JsonValueKind.String)
            {
                throw new JsonataException("T0410", SR.T0410_Argument1OfFunctionSubstringIsNotAString, 0);
            }

            var startElem = startSeq.FirstOrDefault;
            if (!FunctionalCompiler.TryCoerceToNumber(startElem, out double startD))
            {
                throw new JsonataException("T0410", SR.T0410_Argument2OfFunctionSubstringIsNotANumber, 0);
            }

#if NET
            using UnescapedUtf16JsonString utf16 = strElem.GetUtf16String();
            ReadOnlySpan<char> span = utf16.Span;

            int cpLen = CountCodePoints(span);
            int start = (int)startD;
            if (start < 0)
            {
                start = Math.Max(0, cpLen + start);
            }

            start = Math.Min(start, cpLen);

            int count;
            if (lengthArg is not null)
            {
                var lenSeq = lengthArg(input, env);
                if (!FunctionalCompiler.TryCoerceToNumber(lenSeq.FirstOrDefault, out double lenD))
                {
                    throw new JsonataException("T0410", SR.T0410_Argument3OfFunctionSubstringIsNotANumber, 0);
                }

                count = Math.Max(0, (int)lenD);
                count = Math.Min(count, cpLen - start);
            }
            else
            {
                count = cpLen - start;
            }

            int startCharIdx = CodePointToCharIndex(span, start);
            int endCharIdx = CodePointToCharIndex(span.Slice(startCharIdx), count) + startCharIdx;
            ReadOnlySpan<char> result = span.Slice(startCharIdx, endCharIdx - startCharIdx);
            return new Sequence(JsonataHelpers.StringFromChars(result, env.Workspace));
#else
            string? str = strElem.GetString();
            if (str is null)
            {
                return Sequence.Undefined;
            }

            // Convert to code point array for proper Unicode handling
            int[] codePoints = ToCodePointArray(str);
            int cpLen = codePoints.Length;

            int start = (int)startD;
            if (start < 0)
            {
                start = Math.Max(0, cpLen + start);
            }

            start = Math.Min(start, cpLen);

            int count;
            if (lengthArg is not null)
            {
                var lenSeq = lengthArg(input, env);
                if (!FunctionalCompiler.TryCoerceToNumber(lenSeq.FirstOrDefault, out double lenD))
                {
                    throw new JsonataException("T0410", SR.T0410_Argument3OfFunctionSubstringIsNotANumber, 0);
                }

                count = Math.Max(0, (int)lenD);
                count = Math.Min(count, cpLen - start);
            }
            else
            {
                count = cpLen - start;
            }

            string result = CodePointsToString(codePoints, start, count);
            return new Sequence(JsonataHelpers.StringFromString(result, env.Workspace));
#endif
        };
    }

    private static ExpressionEvaluator CompileSubstringBefore(ExpressionEvaluator[] args)
    {
        if (args.Length == 1)
        {
            return CompileStringSearch(ContextArg, args[0], before: true, contextImplied: true);
        }

        if (args.Length != 2)
        {
            throw new JsonataException("T0411", SR.T0411_SubstringbeforeExpects2Arguments, 0);
        }

        return CompileStringSearch(args[0], args[1], before: true);
    }

    private static ExpressionEvaluator CompileSubstringAfter(ExpressionEvaluator[] args)
    {
        if (args.Length == 1)
        {
            return CompileStringSearch(ContextArg, args[0], before: false, contextImplied: true);
        }

        if (args.Length != 2)
        {
            throw new JsonataException("T0411", SR.T0411_SubstringafterExpects2Arguments, 0);
        }

        return CompileStringSearch(args[0], args[1], before: false);
    }

    private static ExpressionEvaluator CompileStringSearch(ExpressionEvaluator strArg, ExpressionEvaluator searchArg, bool before, bool contextImplied = false)
    {
        return (in JsonElement input, Environment env) =>
        {
            var strSeq = strArg(input, env);
            var searchSeq = searchArg(input, env);

            if (strSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            var strElem = strSeq.FirstOrDefault;
            if (strElem.ValueKind != JsonValueKind.String)
            {
                throw new JsonataException(
                    contextImplied ? "T0411" : "T0410",
                    SR.T0410_Argument1OfStringFunctionIsNotAString,
                    0);
            }

            if (searchSeq.IsUndefined || searchSeq.FirstOrDefault.ValueKind != JsonValueKind.String)
            {
                throw new JsonataException("T0410", SR.T0410_Argument2OfStringFunctionIsNotAString, 0);
            }

#if NET
            using UnescapedUtf16JsonString strUtf16 = strElem.GetUtf16String();
            ReadOnlySpan<char> strSpan = strUtf16.Span;
            using UnescapedUtf16JsonString searchUtf16 = searchSeq.FirstOrDefault.GetUtf16String();
            ReadOnlySpan<char> searchSpan = searchUtf16.Span;

            int idx = strSpan.IndexOf(searchSpan, StringComparison.Ordinal);
            ReadOnlySpan<char> result = idx < 0
                ? strSpan
                : before ? strSpan.Slice(0, idx) : strSpan.Slice(idx + searchSpan.Length);

            return new Sequence(JsonataHelpers.StringFromChars(result, env.Workspace));
#else
            string? str = strElem.GetString();
            string? search = searchSeq.FirstOrDefault.GetString();

            if (str is null || search is null)
            {
                return Sequence.Undefined;
            }

            int idx = str.IndexOf(search, StringComparison.Ordinal);
            string result = idx < 0
                ? str
                : before ? str.Substring(0, idx) : str.Substring(idx + search.Length);

            return new Sequence(JsonataHelpers.StringFromString(result, env.Workspace));
#endif
        };
    }

    private static ExpressionEvaluator CompileUppercase(ExpressionEvaluator[] args)
    {
#if NET
        return CompileStringSpanTransform(args, static (source, dest) => source.ToUpperInvariant(dest));
#else
        return CompileStringTransform(args, s => s.ToUpperInvariant());
#endif
    }

    private static ExpressionEvaluator CompileLowercase(ExpressionEvaluator[] args)
    {
#if NET
        return CompileStringSpanTransform(args, static (source, dest) => source.ToLowerInvariant(dest));
#else
        return CompileStringTransform(args, s => s.ToLowerInvariant());
#endif
    }

    private static ExpressionEvaluator CompileTrim(ExpressionEvaluator[] args)
    {
        // JSONata $trim normalizes all whitespace (including newlines, tabs) to single spaces
        return CompileStringTransform(args, s => WhitespaceCollapseRegex.Replace(s.Trim(), " "));
    }

    private static ExpressionEvaluator CompileStringTransform(ExpressionEvaluator[] args, Func<string, string> transform)
    {
        if (args.Length > 1)
        {
            throw new JsonataException("T0410", SR.T0410_StringFunctionExpects0Or1Arguments, 0);
        }

        var arg = args.Length == 1 ? args[0] : ContextArg;
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            var elem = seq.FirstOrDefault;
            if (elem.ValueKind == JsonValueKind.Undefined)
            {
                return Sequence.Undefined;
            }

            if (elem.ValueKind != JsonValueKind.String)
            {
                throw new JsonataException("T0410", SR.T0410_StringFunctionArgumentMustBeAString, 0);
            }

            string? str = elem.GetString();
            if (str is null)
            {
                return Sequence.Undefined;
            }

            return new Sequence(JsonataHelpers.StringFromString(transform(str), env.Workspace));
        };
    }

#if NET
    /// <summary>
    /// Compiles a string function that transforms via a span-to-span operation
    /// (e.g., ToUpperInvariant, ToLowerInvariant) without allocating intermediate strings.
    /// </summary>
    private static ExpressionEvaluator CompileStringSpanTransform(
        ExpressionEvaluator[] args,
        SpanAction spanTransform)
    {
        if (args.Length > 1)
        {
            throw new JsonataException("T0410", SR.T0410_StringFunctionExpects0Or1Arguments, 0);
        }

        var arg = args.Length == 1 ? args[0] : ContextArg;
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            var elem = seq.FirstOrDefault;
            if (elem.ValueKind == JsonValueKind.Undefined)
            {
                return Sequence.Undefined;
            }

            if (elem.ValueKind != JsonValueKind.String)
            {
                throw new JsonataException("T0410", SR.T0410_StringFunctionArgumentMustBeAString, 0);
            }

            using UnescapedUtf16JsonString utf16 = elem.GetUtf16String();
            ReadOnlySpan<char> source = utf16.Span;

            if (source.Length == 0)
            {
                return new Sequence(JsonataHelpers.EmptyString());
            }

            char[]? rentedChars = null;
            Span<char> dest = source.Length <= 128
                ? stackalloc char[128]
                : (rentedChars = ArrayPool<char>.Shared.Rent(source.Length));

            try
            {
                int written = spanTransform(source, dest);
                return new Sequence(JsonataHelpers.StringFromChars(
                    dest.Slice(0, written), env.Workspace));
            }
            finally
            {
                if (rentedChars is not null)
                {
                    ArrayPool<char>.Shared.Return(rentedChars);
                }
            }
        };
    }

    private delegate int SpanAction(ReadOnlySpan<char> source, Span<char> destination);
#endif

    private static ExpressionEvaluator CompileJoin(ExpressionEvaluator[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            throw new JsonataException("T0410", SR.T0410_JoinExpects1Or2Arguments, 0);
        }

        var arrArg = args[0];
        var sepArg = args.Length > 1 ? args[1] : null;

        return (in JsonElement input, Environment env) =>
        {
            var arrSeq = arrArg(input, env);
            if (arrSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            // Get separator as raw UTF-8 bytes (without quotes), copied to a small buffer
            byte[]? sepBuffer = null;
            int sepLen = 0;
            if (sepArg is not null)
            {
                var sepSeq = sepArg(input, env);
                if (!sepSeq.IsUndefined && sepSeq.FirstOrDefault.ValueKind != JsonValueKind.String)
                {
                    throw new JsonataException("T0410", SR.T0410_SecondArgumentOfJoinMustBeAString, 0);
                }

                if (!sepSeq.IsUndefined && sepSeq.FirstOrDefault.ValueKind == JsonValueKind.String)
                {
                    using RawUtf8JsonString sepRaw = JsonMarshal.GetRawUtf8Value(sepSeq.FirstOrDefault);
                    ReadOnlySpan<byte> sepSpan = sepRaw.Span;
                    if (sepSpan.Length > 2)
                    {
                        sepLen = sepSpan.Length - 2;
                        sepBuffer = ArrayPool<byte>.Shared.Rent(sepLen);
                        sepSpan.Slice(1, sepLen).CopyTo(sepBuffer);
                    }
                }
            }

            try
            {
                // Build the joined result directly as quoted UTF-8
                byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
                int pos = 0;
                buffer[pos++] = (byte)'"';
                bool first = true;

                if (arrSeq.Count > 1)
                {
                    for (int i = 0; i < arrSeq.Count; i++)
                    {
                        AppendJoinElement(arrSeq[i], ref buffer, ref pos, ref first, sepBuffer, sepLen);
                    }
                }
                else
                {
                    var arr = arrSeq.FirstOrDefault;
                    if (arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in arr.EnumerateArray())
                        {
                            AppendJoinElement(item, ref buffer, ref pos, ref first, sepBuffer, sepLen);
                        }
                    }
                    else if (arr.ValueKind == JsonValueKind.String)
                    {
                        AppendJoinElement(arr, ref buffer, ref pos, ref first, sepBuffer, sepLen);
                    }
                    else
                    {
                        throw new JsonataException("T0412", SR.T0412_Argument1OfFunctionJoinMustBeAnArrayOfStrings, 0);
                    }
                }

                JsonataHelpers.GrowBufferIfNeeded(ref buffer, pos, 1);
                buffer[pos++] = (byte)'"';

                JsonElement result = JsonataHelpers.StringFromQuotedUtf8Span(
                    new ReadOnlySpan<byte>(buffer, 0, pos), env.Workspace);
                ArrayPool<byte>.Shared.Return(buffer);
                return new Sequence(result);
            }
            finally
            {
                if (sepBuffer is not null)
                {
                    ArrayPool<byte>.Shared.Return(sepBuffer);
                }
            }
        };
    }

    private static ExpressionEvaluator CompileSplit(ExpressionEvaluator[] args)
    {
        ExpressionEvaluator strArg;
        ExpressionEvaluator sepArg;
        ExpressionEvaluator? limitArg;

        if (args.Length == 1)
        {
            strArg = ContextArg;
            sepArg = args[0];
            limitArg = null;
        }
        else if (args.Length == 2)
        {
            strArg = args[0];
            sepArg = args[1];
            limitArg = null;
        }
        else if (args.Length == 3)
        {
            strArg = args[0];
            sepArg = args[1];
            limitArg = args[2];
        }
        else
        {
            throw new JsonataException("T0410", SR.T0410_SplitExpects13Arguments, 0);
        }

        return (in JsonElement input, Environment env) =>
        {
            var strSeq = strArg(input, env);
            var sepSeq = sepArg(input, env);

            if (strSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            // Validate input is a string
            if (strSeq.IsLambda || strSeq.FirstOrDefault.ValueKind != JsonValueKind.String)
            {
                throw new JsonataException("T0410", SR.T0410_Argument1OfFunctionSplitIsNotOfTheCorrectType, 0);
            }

            JsonElement strElement = strSeq.FirstOrDefault;
            if (strElement.ValueKind != JsonValueKind.String)
            {
                return Sequence.Undefined;
            }

            // Validate separator is a string or regex
            if (!sepSeq.IsRegex)
            {
                if (sepSeq.IsLambda)
                {
                    throw new JsonataException("T1010", SR.T1010_TheFunctionArgumentToSplitDoesNotMatchFunctionSignature, 0);
                }

                if (sepSeq.IsUndefined || sepSeq.FirstOrDefault.ValueKind != JsonValueKind.String)
                {
                    throw new JsonataException("T0410", SR.T0410_Argument2OfFunctionSplitIsNotOfTheCorrectType, 0);
                }
            }

            int limit = int.MaxValue;
            if (limitArg is not null)
            {
                var limitSeq = limitArg(input, env);
                if (!limitSeq.IsUndefined)
                {
                    if (limitSeq.IsLambda || limitSeq.FirstOrDefault.ValueKind != JsonValueKind.Number)
                    {
                        throw new JsonataException("T0410", SR.T0410_Argument3OfFunctionSplitIsNotOfTheCorrectType, 0);
                    }

                    if (FunctionalCompiler.TryCoerceToNumber(limitSeq.FirstOrDefault, out double limitD))
                    {
                        if (limitD < 0)
                        {
                            throw new JsonataException("D3020", SR.D3020_ThirdArgumentOfTheSplitFunctionMustEvaluateToAPositiveNumber, 0);
                        }

                        // Floor the limit (JSONata accepts non-integer limits)
                        limit = (int)Math.Floor(limitD);
                    }
                }
            }

            if (sepSeq.IsRegex)
            {
                return new Sequence(SplitRegex(strElement, sepSeq.Regex!, limit, env.Workspace));
            }
            else
            {
                return new Sequence(SplitString(strElement, sepSeq.FirstOrDefault, limit, env.Workspace));
            }
        };
    }

    private static ExpressionEvaluator CompileContains(ExpressionEvaluator[] args)
    {
        ExpressionEvaluator strArg;
        ExpressionEvaluator searchArg;
        if (args.Length == 1)
        {
            strArg = ContextArg;
            searchArg = args[0];
        }
        else if (args.Length == 2)
        {
            strArg = args[0];
            searchArg = args[1];
        }
        else
        {
            throw new JsonataException("T0410", SR.T0410_ContainsExpects1Or2Arguments, 0);
        }

        return (in JsonElement input, Environment env) =>
        {
            var strSeq = strArg(input, env);
            var searchSeq = searchArg(input, env);

            if (strSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

#if NET
            if (strSeq.FirstOrDefault.ValueKind != JsonValueKind.String)
            {
                throw new JsonataException("T0410", SR.T0410_Argument1OfFunctionContainsMustBeAString, 0);
            }

            using UnescapedUtf16JsonString strUtf16 = strSeq.FirstOrDefault.GetUtf16String();
            ReadOnlySpan<char> strSpan = strUtf16.Span;

            if (searchSeq.IsRegex)
            {
                bool isMatch = searchSeq.Regex!.IsMatch(strSpan);
                return new Sequence(JsonataHelpers.BooleanElement(isMatch));
            }

            if (searchSeq.IsUndefined || searchSeq.FirstOrDefault.ValueKind != JsonValueKind.String)
            {
                throw new JsonataException("T0410", SR.T0410_Argument2OfFunctionContainsMustBeAStringOrRegex, 0);
            }

            using UnescapedUtf16JsonString searchUtf16 = searchSeq.FirstOrDefault.GetUtf16String();
            bool result = strSpan.Contains(searchUtf16.Span, StringComparison.Ordinal);
            return new Sequence(JsonataHelpers.BooleanElement(result));
#else
            string? str = strSeq.FirstOrDefault.GetString();
            if (str is null)
            {
                return Sequence.Undefined;
            }

            if (searchSeq.IsRegex)
            {
                bool isMatch = searchSeq.Regex!.IsMatch(str);
                return new Sequence(JsonataHelpers.BooleanElement(isMatch));
            }

            string? search = searchSeq.FirstOrDefault.GetString();
            bool result = search is not null && str.Contains(search);
            return new Sequence(JsonataHelpers.BooleanElement(result));
#endif
        };
    }

    // --- Numeric functions ---
    private static ExpressionEvaluator CompileAbs(ExpressionEvaluator[] args) => CompileMathFunc(args, Math.Abs);

    private static ExpressionEvaluator CompileFloor(ExpressionEvaluator[] args) => CompileMathFunc(args, Math.Floor);

    private static ExpressionEvaluator CompileCeil(ExpressionEvaluator[] args) => CompileMathFunc(args, Math.Ceiling);

    private static ExpressionEvaluator CompileSqrt(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_SqrtExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined || !FunctionalCompiler.TryCoerceToNumber(seq.FirstOrDefault, out double num))
            {
                return Sequence.Undefined;
            }

            if (num < 0)
            {
                throw new JsonataException("D3060", SR.D3060_TheArgumentOfTheSqrtFunctionMustBeNonNegative, 0);
            }

            return Sequence.FromDouble(Math.Sqrt(num), env.Workspace);
        };
    }

    private static ExpressionEvaluator CompileRound(ExpressionEvaluator[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            throw new JsonataException("T0410", SR.T0410_RoundExpects1Or2Arguments, 0);
        }

        var numArg = args[0];
        var precArg = args.Length > 1 ? args[1] : null;

        return (in JsonElement input, Environment env) =>
        {
            var seq = numArg(input, env);
            if (seq.IsUndefined || !FunctionalCompiler.TryCoerceToNumber(seq.FirstOrDefault, out double num))
            {
                return Sequence.Undefined;
            }

            int precision = 0;
            if (precArg is not null)
            {
                var precSeq = precArg(input, env);
                if (FunctionalCompiler.TryCoerceToNumber(precSeq.FirstOrDefault, out double precD))
                {
                    precision = (int)precD;
                }
            }

            double result;
            if (precision < 0)
            {
                double factor = Math.Pow(10, -precision);
                result = Math.Round(num / factor, MidpointRounding.ToEven) * factor;
            }
            else
            {
                // Use decimal arithmetic for precise banker's rounding,
                // avoiding IEEE 754 double representation errors.
                decimal decValue = (decimal)num;
                decimal rounded = Math.Round(decValue, Math.Min(precision, 15), MidpointRounding.ToEven);
                result = (double)rounded;
            }

            return Sequence.FromDouble(result, env.Workspace);
        };
    }

    private static ExpressionEvaluator CompilePower(ExpressionEvaluator[] args)
    {
        if (args.Length != 2)
        {
            throw new JsonataException("T0410", SR.T0410_PowerExpects2Arguments, 0);
        }

        var baseArg = args[0];
        var expArg = args[1];

        return (in JsonElement input, Environment env) =>
        {
            var baseSeq = baseArg(input, env);
            var expSeq = expArg(input, env);

            if (!FunctionalCompiler.TryCoerceToNumber(baseSeq.FirstOrDefault, out double baseNum)
                || !FunctionalCompiler.TryCoerceToNumber(expSeq.FirstOrDefault, out double expNum))
            {
                return Sequence.Undefined;
            }

            double result = Math.Pow(baseNum, expNum);
            if (double.IsInfinity(result) || double.IsNaN(result))
            {
                throw new JsonataException("D3061", SR.D3061_PowerFunctionResultOutOfRange, 0);
            }

            return Sequence.FromDouble(result, env.Workspace);
        };
    }

    private static ExpressionEvaluator CompileMathFunc(ExpressionEvaluator[] args, Func<double, double> func)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_MathFunctionExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined || !FunctionalCompiler.TryCoerceToNumber(seq.FirstOrDefault, out double num))
            {
                return Sequence.Undefined;
            }

            return Sequence.FromDouble(func(num), env.Workspace);
        };
    }

    // --- Object functions ---
    private static ExpressionEvaluator CompileKeys(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_KeysExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            return KeysCore(seq, env);
        };
    }

    private static Sequence KeysCore(Sequence seq, Environment env)
    {
        const int estimate = 8;

        Span<int> keyBuckets = estimate <= Utf8KeyHashSet.StackAllocBucketSize
            ? stackalloc int[Utf8KeyHashSet.StackAllocBucketSize]
            : default;
        Span<byte> keyEntries = estimate * 20 <= Utf8KeyHashSet.StackAllocEntrySize
            ? stackalloc byte[Utf8KeyHashSet.StackAllocEntrySize]
            : default;
        Span<byte> keyBuffer = stackalloc byte[Utf8KeyHashSet.StackAllocKeyBufferSize];

        var keySet = new Utf8KeyHashSet(estimate, keyBuckets, keyEntries, keyBuffer);
        try
        {
            JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(env.Workspace, estimate);
            JsonElement.Mutable arrayRoot = arrayDoc.RootElement;

            for (int i = 0; i < seq.Count; i++)
            {
                CollectUniqueKeysRT(seq[i], ref keySet, ref arrayRoot);
            }

            if (keySet.Count == 0)
            {
                return Sequence.Undefined;
            }

            if (keySet.Count == 1)
            {
                return new Sequence(arrayRoot[0]);
            }

            return new Sequence((JsonElement)arrayRoot);
        }
        finally
        {
            keySet.Dispose();
        }
    }

    private static void CollectUniqueKeysRT(JsonElement el, ref Utf8KeyHashSet keySet, ref JsonElement.Mutable arrayRoot)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                using UnescapedUtf8JsonString nameUtf8 = prop.Utf8NameSpan;
                if (keySet.AddIfNotExists(nameUtf8.Span))
                {
                    arrayRoot.AddItem(nameUtf8.Span);
                }
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                CollectUniqueKeysRT(item, ref keySet, ref arrayRoot);
            }
        }
    }

    private static ExpressionEvaluator CompileValues(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_ValuesExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined || seq.FirstOrDefault.ValueKind != JsonValueKind.Object)
            {
                return Sequence.Undefined;
            }

            var obj = seq.FirstOrDefault;
            JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
                env.Workspace,
                obj,
                static (in JsonElement ctx, ref JsonElement.ArrayBuilder builder) =>
                {
                    foreach (var prop in ctx.EnumerateObject())
                    {
                        builder.AddItem(prop.Value);
                    }
                },
                estimatedMemberCount: 10);

            return new Sequence((JsonElement)doc.RootElement);
        };
    }

    // --- Array functions (stubs — full implementation in Phase 3) ---
    private static ExpressionEvaluator CompileAppend(ExpressionEvaluator[] args)
    {
        if (args.Length != 2)
        {
            throw new JsonataException("T0410", SR.T0410_AppendExpects2Arguments, 0);
        }

        var arr1 = args[0];
        var arr2 = args[1];

        return (in JsonElement input, Environment env) =>
        {
            var seq1 = arr1(input, env);
            var seq2 = arr2(input, env);

            // If either is undefined, return the other
            if (seq1.IsUndefined && seq2.IsUndefined)
            {
                return Sequence.Undefined;
            }

            if (seq2.IsUndefined)
            {
                return seq1;
            }

            if (seq1.IsUndefined)
            {
                return seq2;
            }

            JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(env.Workspace, 16);
            JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
            AddSequenceToArray(seq1, ref arrayRoot);
            AddSequenceToArray(seq2, ref arrayRoot);
            return new Sequence((JsonElement)arrayRoot);
        };
    }

    private static ExpressionEvaluator CompileSortFunc(ExpressionEvaluator[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            throw new JsonataException("T0410", SR.T0410_SortExpects12Arguments, 0);
        }

        var arrArg = args[0];
        var funcArg = args.Length > 1 ? args[1] : null;

        return (in JsonElement input, Environment env) =>
        {
            var seq = arrArg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            // Collect elements from array, sequence, or wrap singleton — flatten arrays
            var elements = default(SequenceBuilder);
            for (int i = 0; i < seq.Count; i++)
            {
                var el = seq[i];
                if (el.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in el.EnumerateArray())
                    {
                        elements.Add(item);
                    }
                }
                else
                {
                    elements.Add(el);
                }
            }

            if (funcArg is not null)
            {
                var funcSeq = funcArg(input, env);
                if (funcSeq.IsLambda)
                {
                    var lambda = funcSeq.Lambda!;
                    var sortInput = input;
                    var invokeEnv = lambda.CreateInvokeEnv(env);
                    try
                    {
                        StableSort(ref elements, (a, b) =>
                        {
                            Sequence sa = new Sequence(a);
                            Sequence sb = new Sequence(b);
#if NET9_0_OR_GREATER
                            var result = lambda.InvokeReusing([sa, sb], sortInput, invokeEnv, env);
#else
                            var result = lambda.InvokeReusing(new Sequence[] { sa, sb }, sortInput, invokeEnv, env);
#endif
                            if (result.IsUndefined)
                            {
                                return 0;
                            }

                            var el = result.FirstOrDefault;

                            // Boolean comparator: true means a > b (a should come after b).
                            // false could mean a < b OR a == b, so check the reverse.
                            if (el.ValueKind == JsonValueKind.True)
                            {
                                return 1;
                            }

                            if (el.ValueKind == JsonValueKind.False)
                            {
#if NET9_0_OR_GREATER
                                var reverseResult = lambda.InvokeReusing([sb, sa], sortInput, invokeEnv, env);
#else
                                var reverseResult = lambda.InvokeReusing(new Sequence[] { sb, sa }, sortInput, invokeEnv, env);
#endif
                                if (!reverseResult.IsUndefined
                                    && reverseResult.FirstOrDefault.ValueKind == JsonValueKind.True)
                                {
                                    return -1;
                                }

                                return 0;
                            }

                            if (FunctionalCompiler.TryCoerceToNumber(el, out double num))
                            {
                                return num < 0 ? -1 : (num > 0 ? 1 : 0);
                            }

                            return 0;
                        });
                    }
                    finally
                    {
                        Environment.ReturnChild(invokeEnv);
                    }
                }
            }
            else
            {
                // Default sort: check types are homogeneous
                bool hasObjects = false;
                for (int i = 0; i < elements.Count; i++)
                {
                    if (elements[i].ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        hasObjects = true;
                        break;
                    }
                }

                if (hasObjects)
                {
                    throw new JsonataException("D3070", SR.D3070_SortSingleArgRequiresStringsOrNumbers, 0);
                }

                StableSort(ref elements, (a, b) =>
                {
                    if (a.ValueKind == JsonValueKind.Number && b.ValueKind == JsonValueKind.Number)
                    {
                        return a.GetDouble().CompareTo(b.GetDouble());
                    }

                    return string.CompareOrdinal(
                        FunctionalCompiler.CoerceElementToString(a),
                        FunctionalCompiler.CoerceElementToString(b));
                });
            }

            return new Sequence(JsonataHelpers.ArrayFromBuilder(ref elements, env.Workspace));
        };
    }

    private static ExpressionEvaluator CompileReverse(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_ReverseExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            var arr = seq.FirstOrDefault;
            if (arr.ValueKind != JsonValueKind.Array)
            {
                return seq;
            }

            int len = arr.GetArrayLength();
            JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
                env.Workspace,
                (arr, len),
                static (in (JsonElement Arr, int Len) ctx, ref JsonElement.ArrayBuilder builder) =>
                {
                    for (int i = ctx.Len - 1; i >= 0; i--)
                    {
                        builder.AddItem(ctx.Arr[i]);
                    }
                },
                estimatedMemberCount: len + 2);

            return new Sequence((JsonElement)doc.RootElement);
        };
    }

    private static ExpressionEvaluator CompileDistinct(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_DistinctExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            return DistinctCore(seq, env);
        };
    }

    private static Sequence DistinctCore(Sequence seq, Environment env)
    {
        // Collect all items to deduplicate, flattening arrays in multi-valued sequences
        var items = default(SequenceBuilder);
        for (int si = 0; si < seq.Count; si++)
        {
            var el = seq[si];
            if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in el.EnumerateArray())
                {
                    items.Add(item);
                }
            }
            else
            {
                items.Add(el);
            }
        }

        // If only one non-array item, return as-is
        if (items.Count <= 1 && seq.Count == 1 && seq.FirstOrDefault.ValueKind != JsonValueKind.Array)
        {
            items.ReturnArray();
            return seq;
        }

        // Use UniqueItemsHashSet for allocation-free dedup
        var (parentDoc, _) = JsonElementHelpers.GetParentDocumentAndIndex(items[0]);

        Span<int> buckets = items.Count <= UniqueItemsHashSet.StackAllocBucketSize
            ? stackalloc int[UniqueItemsHashSet.StackAllocBucketSize]
            : default;
        int entryBytes = items.Count * 12;
        Span<byte> entries = entryBytes <= UniqueItemsHashSet.StackAllocEntrySize
            ? stackalloc byte[UniqueItemsHashSet.StackAllocEntrySize]
            : default;

        var hashSet = new UniqueItemsHashSet(parentDoc, items.Count, buckets, entries);
        try
        {
            JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(env.Workspace, items.Count);
            JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
            for (int i = 0; i < items.Count; i++)
            {
                var (_, idx) = JsonElementHelpers.GetParentDocumentAndIndex(items[i]);
                if (hashSet.AddItemIfNotExists(idx))
                {
                    arrayRoot.AddItem(items[i]);
                }
            }

            items.ReturnArray();
            return new Sequence((JsonElement)arrayRoot);
        }
        finally
        {
            hashSet.Dispose();
        }
    }

    private static ExpressionEvaluator CompileFlatten(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_FlattenExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            var arr = seq.FirstOrDefault;
            if (arr.ValueKind != JsonValueKind.Array)
            {
                return seq;
            }

            JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
                env.Workspace,
                arr,
                static (in JsonElement ctx, ref JsonElement.ArrayBuilder builder) =>
                {
                    FlattenIntoBuilder(ctx, ref builder);
                },
                estimatedMemberCount: 16);

            return new Sequence((JsonElement)doc.RootElement);
        };

        static void FlattenIntoBuilder(JsonElement element, ref JsonElement.ArrayBuilder builder)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    FlattenIntoBuilder(item, ref builder);
                }
            }
            else
            {
                builder.AddItem(element);
            }
        }
    }

    // --- Higher-order functions (stubs) ---
    private static ExpressionEvaluator CompileMap(ExpressionEvaluator[] args)
    {
        if (args.Length != 2)
        {
            throw new JsonataException("T0410", SR.T0410_MapExpects2Arguments, 0);
        }

        var arrArg = args[0];
        var funcArg = args[1];

        return (in JsonElement input, Environment env) =>
        {
            var seq = arrArg(input, env);
            var funcSeq = funcArg(input, env);

            if (seq.IsUndefined || !funcSeq.IsLambda)
            {
                return Sequence.Undefined;
            }

            var lambda = funcSeq.Lambda!;
            int arity = lambda.Arity;

            // Collect all items into a flat list for indexing
            var items = default(SequenceBuilder);
            if (seq.IsSingleton && seq.FirstOrDefault.ValueKind == JsonValueKind.Array)
            {
                var arr = seq.FirstOrDefault;
                foreach (var item in arr.EnumerateArray())
                {
                    items.Add(item);
                }
            }
            else
            {
                for (int i = 0; i < seq.Count; i++)
                {
                    var elem = seq[i];
                    if (elem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in elem.EnumerateArray())
                        {
                            items.Add(item);
                        }
                    }
                    else
                    {
                        items.Add(elem);
                    }
                }
            }

            // Build the flattened array as a Sequence for the 3rd lambda arg,
            // but only if the lambda actually uses it (arity >= 3)
            Sequence flatArrSeq = default;
            if (arity >= 3)
            {
                JsonDocumentBuilder<JsonElement.Mutable> flatDoc = JsonElement.CreateArrayBuilder(env.Workspace, items.Count);
                JsonElement.Mutable flatRoot = flatDoc.RootElement;
                for (int i = 0; i < items.Count; i++)
                {
                    flatRoot.AddItem(items[i]);
                }

                flatArrSeq = new Sequence((JsonElement)flatRoot);
            }

            var invokeEnv = lambda.CreateInvokeEnv(env);

            // Dual-mode collection: optimistic doubles, fallback to elements.
            // Then build via CVB for direct MetadataDb writes (no Mutable.AddItem overhead).
            double[]? doubleResults = ArrayPool<double>.Shared.Rent(items.Count);
            JsonElement[]? elementResults = null;
            bool allDoubles = true;
            int resultCount = 0;

            try
            {
                for (int i = 0; i < items.Count; i++)
                {
                    Sequence arg0 = new Sequence(items[i]);
                    Sequence arg1 = arity >= 2 ? Sequence.FromDouble(i, env.Workspace) : default;

#if NET9_0_OR_GREATER
                    var result = lambda.InvokeReusing([arg0, arg1, flatArrSeq], items[i], invokeEnv, env);
#else
                    var result = lambda.InvokeReusing(new Sequence[] { arg0, arg1, flatArrSeq }, items[i], invokeEnv, env);
#endif

                    if (allDoubles && result.IsSingleton && result.IsRawDouble)
                    {
                        result.TryGetDouble(out double d);
                        if (resultCount == doubleResults.Length)
                        {
                            var bigger = ArrayPool<double>.Shared.Rent(doubleResults.Length * 2);
                            doubleResults.AsSpan(0, resultCount).CopyTo(bigger);
                            ArrayPool<double>.Shared.Return(doubleResults);
                            doubleResults = bigger;
                        }

                        doubleResults[resultCount++] = d;
                    }
                    else
                    {
                        if (allDoubles)
                        {
                            // Switch from double to element mode.
                            allDoubles = false;
                            elementResults = ArrayPool<JsonElement>.Shared.Rent(Math.Max(items.Count, resultCount + result.Count));

                            // Materialize pending doubles.
                            for (int j = 0; j < resultCount; j++)
                            {
                                elementResults[j] = JsonataHelpers.NumberFromDouble(doubleResults[j], env.Workspace);
                            }
                        }

                        for (int j = 0; j < result.Count; j++)
                        {
                            if (resultCount >= elementResults!.Length)
                            {
                                var bigger = ArrayPool<JsonElement>.Shared.Rent(elementResults.Length * 2);
                                elementResults.AsSpan(0, resultCount).CopyTo(bigger);
                                ArrayPool<JsonElement>.Shared.Return(elementResults);
                                elementResults = bigger;
                            }

                            elementResults![resultCount++] = result[j];
                        }
                    }
                }

                items.ReturnArray();

                if (resultCount == 0)
                {
                    return Sequence.Undefined;
                }

                // Singleton auto-unwrap: JSONata returns just the value, not [value].
                if (resultCount == 1)
                {
                    if (allDoubles)
                    {
                        return Sequence.FromDouble(doubleResults![0], env.Workspace);
                    }

                    return new Sequence(elementResults![0]);
                }

                // Build via CVB — writes directly to MetadataDb, no Mutable per-item overhead.
                JsonDocumentBuilder<JsonElement.Mutable> doc;
                if (allDoubles)
                {
                    doc = JsonElement.CreateBuilder(
                        env.Workspace,
                        (doubleResults!, resultCount),
                        static (in (double[] Array, int Count) ctx, ref JsonElement.ArrayBuilder builder) =>
                        {
                            for (int i = 0; i < ctx.Count; i++)
                            {
                                builder.AddItem(ctx.Array[i]);
                            }
                        },
                        estimatedMemberCount: resultCount + 2,
                        initialValueBufferSize: Math.Max(8192, resultCount * 28));
                }
                else
                {
                    doc = JsonElement.CreateBuilder(
                        env.Workspace,
                        (elementResults!, resultCount),
                        static (in (JsonElement[] Array, int Count) ctx, ref JsonElement.ArrayBuilder builder) =>
                        {
                            for (int i = 0; i < ctx.Count; i++)
                            {
                                builder.AddItem(ctx.Array[i]);
                            }
                        },
                        estimatedMemberCount: resultCount + 2);
                }

                return new Sequence((JsonElement)doc.RootElement);
            }
            finally
            {
                Environment.ReturnChild(invokeEnv);

                if (doubleResults is not null)
                {
                    ArrayPool<double>.Shared.Return(doubleResults);
                }

                if (elementResults is not null)
                {
                    ArrayPool<JsonElement>.Shared.Return(elementResults);
                }
            }
        };
    }

    private static ExpressionEvaluator CompileFilterFunc(ExpressionEvaluator[] args)
    {
        if (args.Length != 2)
        {
            throw new JsonataException("T0410", SR.T0410_FilterExpects2Arguments, 0);
        }

        var arrArg = args[0];
        var funcArg = args[1];

        return (in JsonElement input, Environment env) =>
        {
            var seq = arrArg(input, env);
            var funcSeq = funcArg(input, env);

            if (seq.IsUndefined || !funcSeq.IsLambda)
            {
                return Sequence.Undefined;
            }

            var lambda = funcSeq.Lambda!;
            int arity = lambda.Arity;
            bool inputWasArray = seq.IsSingleton && seq.FirstOrDefault.ValueKind == JsonValueKind.Array;

            // Flatten multi-valued sequences with arrays (e.g., Account.Order.Product)
            if (!inputWasArray && seq.Count > 1)
            {
                bool hasArrays = false;
                for (int i = 0; i < seq.Count; i++)
                {
                    if (seq[i].ValueKind == JsonValueKind.Array)
                    {
                        hasArrays = true;
                        break;
                    }
                }

                if (hasArrays)
                {
                    JsonDocumentBuilder<JsonElement.Mutable> flatDoc = JsonElement.CreateArrayBuilder(env.Workspace, seq.Count * 2);
                    JsonElement.Mutable flatRoot = flatDoc.RootElement;
                    for (int i = 0; i < seq.Count; i++)
                    {
                        var elem = seq[i];
                        if (elem.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in elem.EnumerateArray())
                            {
                                flatRoot.AddItem(item);
                            }
                        }
                        else
                        {
                            flatRoot.AddItem(elem);
                        }
                    }

                    seq = new Sequence((JsonElement)flatRoot);
                    inputWasArray = true;
                }
            }

            // Collect matching elements into a pooled array, then build via CVB.
            JsonElement[]? matchResults = null;
            int matchCount = 0;

            var invokeEnv = lambda.CreateInvokeEnv(env);
            Sequence arrSeq = arity >= 3 ? seq : default;

            try
            {
                if (inputWasArray)
                {
                    var arr = seq.FirstOrDefault;
                    int arrLen = arr.GetArrayLength();
                    matchResults = ArrayPool<JsonElement>.Shared.Rent(Math.Max(arrLen, 4));

                    int i = 0;
                    foreach (var item in arr.EnumerateArray())
                    {
                        Sequence arg0 = new Sequence(item);
                        Sequence arg1 = arity >= 2 ? Sequence.FromDouble(i, env.Workspace) : default;

#if NET9_0_OR_GREATER
                        var result = lambda.InvokeReusing([arg0, arg1, arrSeq], item, invokeEnv, env);
#else
                        var result = lambda.InvokeReusing(new Sequence[] { arg0, arg1, arrSeq }, item, invokeEnv, env);
#endif
                        if (FunctionalCompiler.IsTruthy(result))
                        {
                            matchResults[matchCount++] = item;
                        }

                        i++;
                    }
                }
                else
                {
                    matchResults = ArrayPool<JsonElement>.Shared.Rent(Math.Max(seq.Count, 4));

                    for (int i = 0; i < seq.Count; i++)
                    {
                        Sequence arg0 = new Sequence(seq[i]);
                        Sequence arg1 = arity >= 2 ? Sequence.FromDouble(i, env.Workspace) : default;

#if NET9_0_OR_GREATER
                        var result = lambda.InvokeReusing([arg0, arg1, arrSeq], seq[i], invokeEnv, env);
#else
                        var result = lambda.InvokeReusing(new Sequence[] { arg0, arg1, arrSeq }, seq[i], invokeEnv, env);
#endif
                        if (FunctionalCompiler.IsTruthy(result))
                        {
                            matchResults[matchCount++] = seq[i];
                        }
                    }
                }

                // If input was not an array, unwrap single results
                if (!inputWasArray)
                {
                    if (matchCount == 0)
                    {
                        return Sequence.Undefined;
                    }

                    if (matchCount == 1)
                    {
                        return new Sequence(matchResults[0]);
                    }
                }

                if (matchCount == 0)
                {
                    return new Sequence(JsonataHelpers.EmptyArray());
                }

                // Build via CVB — writes directly to MetadataDb, no Mutable per-item overhead.
                var doc = JsonElement.CreateBuilder(
                    env.Workspace,
                    (matchResults!, matchCount),
                    static (in (JsonElement[] Array, int Count) ctx, ref JsonElement.ArrayBuilder builder) =>
                    {
                        for (int i = 0; i < ctx.Count; i++)
                        {
                            builder.AddItem(ctx.Array[i]);
                        }
                    },
                    estimatedMemberCount: matchCount + 2);

                return new Sequence((JsonElement)doc.RootElement);
            }
            finally
            {
                Environment.ReturnChild(invokeEnv);

                if (matchResults is not null)
                {
                    ArrayPool<JsonElement>.Shared.Return(matchResults);
                }
            }
        };
    }

    private static ExpressionEvaluator CompileReduce(ExpressionEvaluator[] args)
    {
        if (args.Length < 2 || args.Length > 3)
        {
            throw new JsonataException("T0410", SR.T0410_ReduceExpects23Arguments, 0);
        }

        var arrArg = args[0];
        var funcArg = args[1];
        var initArg = args.Length > 2 ? args[2] : null;

        return (in JsonElement input, Environment env) =>
        {
            var seq = arrArg(input, env);
            var funcSeq = funcArg(input, env);

            if (seq.IsUndefined || !funcSeq.IsLambda)
            {
                return Sequence.Undefined;
            }

            var lambda = funcSeq.Lambda!;

            // Validate the reducer function accepts at least 2 parameters
            // (skip for built-in functions which have dynamic arity)
            if (lambda.NativeFunc is null && lambda.Arity < 2)
            {
                throw new JsonataException("D3050", SR.D3050_ReduceSecondArgMustBeFunction, 0);
            }

            // Collect items using SequenceBuilder instead of List<Sequence>
            bool isTuple = seq.IsTupleSequence;
            var items = default(SequenceBuilder);
            if (!isTuple)
            {
                if (seq.IsSingleton && seq.FirstOrDefault.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in seq.FirstOrDefault.EnumerateArray())
                    {
                        items.Add(el);
                    }
                }
                else
                {
                    for (int i = 0; i < seq.Count; i++)
                    {
                        items.Add(seq[i]);
                    }
                }

                if (items.Count == 0)
                {
                    return Sequence.Undefined;
                }
            }
            else if (seq.Count == 0)
            {
                return Sequence.Undefined;
            }

            int itemCount = isTuple ? seq.Count : items.Count;

            Sequence accumulator;
            int startIdx;
            if (initArg is not null)
            {
                accumulator = initArg(input, env);
                startIdx = 0;
            }
            else
            {
                accumulator = isTuple ? seq.GetItemSequence(0) : new Sequence(items[0]);
                startIdx = 1;
            }

            // Build array element for the 4th lambda arg, only if lambda uses it
            int arity = lambda.Arity;
            Sequence arrSeq;
            if (arity >= 4 && !isTuple)
            {
                JsonDocumentBuilder<JsonElement.Mutable> arrDoc = JsonElement.CreateArrayBuilder(env.Workspace, items.Count);
                JsonElement.Mutable arrRoot = arrDoc.RootElement;
                for (int i = 0; i < items.Count; i++)
                {
                    arrRoot.AddItem(items[i]);
                }

                arrSeq = new Sequence((JsonElement)arrRoot);
            }
            else
            {
                arrSeq = Sequence.Undefined;
            }

            // Use InvokeReusing to avoid per-iteration Environment allocation.
            // After each iteration, check if the body captured the reused environment
            // (e.g. by defining a closure). If so, create a fresh one — correct but rare.
            var invokeEnv = lambda.CreateInvokeEnv(env);

            for (int i = startIdx; i < itemCount; i++)
            {
                Sequence arg0 = accumulator;
                Sequence arg1 = isTuple ? seq.GetItemSequence(i) : new Sequence(items[i]);
                Sequence arg2 = arity >= 3 ? Sequence.FromDouble(i, env.Workspace) : default;

#if NET9_0_OR_GREATER
                accumulator = lambda.InvokeReusing([arg0, arg1, arg2, arrSeq], input, invokeEnv, env);
#else
                accumulator = lambda.InvokeReusing(new Sequence[] { arg0, arg1, arg2, arrSeq }, input, invokeEnv, env);
#endif

                // If the body created a closure that captured our reused environment,
                // we must not overwrite its bindings — allocate a fresh one.
                if (invokeEnv.IsCaptured)
                {
                    invokeEnv = lambda.CreateInvokeEnv(env);
                }
            }

            items.ReturnArray();
            Environment.ReturnChild(invokeEnv);

            return accumulator;
        };
    }

    private static ExpressionEvaluator CompileEach(ExpressionEvaluator[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            throw new JsonataException("T0410", SR.T0410_EachExpects12Arguments, 0);
        }

        // When called with 1 arg, the object is the current context
        var objArg = args.Length == 2 ? args[0] : (ExpressionEvaluator)((in JsonElement input, Environment env) => new Sequence(input));
        var funcArg = args.Length == 2 ? args[1] : args[0];

        return (in JsonElement input, Environment env) =>
        {
            var seq = objArg(input, env);
            var funcSeq = funcArg(input, env);

            if (seq.IsUndefined || !funcSeq.IsLambda)
            {
                return Sequence.Undefined;
            }

            var obj = seq.FirstOrDefault;
            if (obj.ValueKind != JsonValueKind.Object)
            {
                return Sequence.Undefined;
            }

            var lambda = funcSeq.Lambda!;
            JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(env.Workspace, 16);
            JsonElement.Mutable arrayRoot = arrayDoc.RootElement;

            Environment reuseEnv = lambda.CreateInvokeEnv(env);
            Sequence[] lambdaArgs = ArrayPool<Sequence>.Shared.Rent(2);
            try
            {
                foreach (var prop in obj.EnumerateObject())
                {
                    lambdaArgs[0] = new Sequence(prop.Value);
                    using UnescapedUtf8JsonString nameUtf8 = prop.Utf8NameSpan;
                    lambdaArgs[1] = new Sequence(JsonataHelpers.StringFromUnescapedUtf8(nameUtf8.Span, env.Workspace));
                    var result = lambda.InvokeReusing(lambdaArgs, input, reuseEnv, env);
                    if (!result.IsUndefined)
                    {
                        if (result.IsSingleton)
                        {
                            arrayRoot.AddItem(result.FirstOrDefault);
                        }
                        else
                        {
                            for (int i = 0; i < result.Count; i++)
                            {
                                arrayRoot.AddItem(result[i]);
                            }
                        }
                    }
                }
            }
            finally
            {
                ArrayPool<Sequence>.Shared.Return(lambdaArgs);
                Environment.ReturnChild(reuseEnv);
            }

            return new Sequence((JsonElement)arrayRoot);
        };
    }

    private static ExpressionEvaluator CompileMerge(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_MergeExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            if (seq.IsSingleton && seq.FirstOrDefault.ValueKind == JsonValueKind.Object)
            {
                // Single object — return as-is
                return seq;
            }

            if (seq.IsSingleton && seq.FirstOrDefault.ValueKind == JsonValueKind.Array)
            {
                JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
                    env.Workspace,
                    seq.FirstOrDefault,
                    static (in JsonElement ctx, ref JsonElement.ObjectBuilder builder) =>
                    {
                        foreach (var item in ctx.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var prop in item.EnumerateObject())
                                {
                                    using UnescapedUtf8JsonString nameUtf8 = prop.Utf8NameSpan;
                                    builder.AddProperty(nameUtf8.Span, prop.Value, escapeName: false, nameRequiresUnescaping: false);
                                }
                            }
                        }
                    },
                    estimatedMemberCount: 16);

                return new Sequence((JsonElement)doc.RootElement);
            }

            if (seq.Count > 1)
            {
                // Multi-element sequence — build from first element that is an object,
                // then overlay remaining. Use CVB with the first object as seed.
                JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
                    env.Workspace,
                    seq,
                    static (in Sequence ctx, ref JsonElement.ObjectBuilder builder) =>
                    {
                        for (int i = 0; i < ctx.Count; i++)
                        {
                            var item = ctx[i];
                            if (item.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var prop in item.EnumerateObject())
                                {
                                    using UnescapedUtf8JsonString nameUtf8 = prop.Utf8NameSpan;
                                    builder.AddProperty(nameUtf8.Span, prop.Value, escapeName: false, nameRequiresUnescaping: false);
                                }
                            }
                        }
                    },
                    estimatedMemberCount: 16);

                return new Sequence((JsonElement)doc.RootElement);
            }

            return Sequence.Undefined;
        };
    }

    private static ExpressionEvaluator CompileSpread(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_SpreadExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);

            // For lambdas/functions, $spread passes them through
            // (must check before IsUndefined since lambda sequences have count==0)
            if (seq.IsLambda)
            {
                return seq;
            }

            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            // Single-element fast path: delegate to the CG helper which uses CVB
            if (seq.Count == 1)
            {
                var el = seq[0];
                if (el.ValueKind == JsonValueKind.Object || el.ValueKind == JsonValueKind.Array)
                {
                    JsonElement result = JsonataCodeGenHelpers.Spread(el, env.Workspace);
                    return result.IsNullOrUndefined() ? Sequence.Undefined : new Sequence(result);
                }

                return new Sequence(el);
            }

            // Multi-element sequence: count total properties for capacity, then build
            int totalProps = 0;
            for (int i = 0; i < seq.Count; i++)
            {
                var el = seq[i];
                if (el.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in el.EnumerateObject())
                    {
                        totalProps++;
                    }
                }
                else if (el.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in el.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var p in item.EnumerateObject())
                            {
                                totalProps++;
                            }
                        }
                    }
                }
                else
                {
                    // Non-objects return as-is
                    return new Sequence(el);
                }
            }

            if (totalProps == 0)
            {
                return Sequence.Undefined;
            }

            JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(env.Workspace, totalProps);
            JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
            for (int i = 0; i < seq.Count; i++)
            {
                var el = seq[i];
                if (el.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in el.EnumerateObject())
                    {
                        arrayRoot.AddItem(prop, SpreadPropertyCallback, 1);
                    }
                }
                else if (el.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in el.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in item.EnumerateObject())
                            {
                                arrayRoot.AddItem(prop, SpreadPropertyCallback, 1);
                            }
                        }
                    }
                }
            }

            if (totalProps == 1)
            {
                return new Sequence(arrayRoot[0]);
            }

            return new Sequence((JsonElement)arrayRoot);
        };
    }

    private static void SpreadPropertyCallback(in JsonProperty<JsonElement> ctx, ref JsonElement.ObjectBuilder builder)
    {
        using UnescapedUtf8JsonString nameUtf8 = ctx.Utf8NameSpan;
        builder.AddProperty(nameUtf8.Span, ctx.Value, escapeName: false, nameRequiresUnescaping: false);
    }

    private static ExpressionEvaluator CompileLookup(ExpressionEvaluator[] args)
    {
        if (args.Length != 2)
        {
            throw new JsonataException("T0410", SR.T0410_LookupExpects2Arguments, 0);
        }

        var objArg = args[0];
        var keyArg = args[1];

        return (in JsonElement input, Environment env) =>
        {
            var objSeq = objArg(input, env);
            var keySeq = keyArg(input, env);

            if (objSeq.IsUndefined || keySeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

#if NET
            using UnescapedUtf16JsonString keyUtf16 = keySeq.FirstOrDefault.GetUtf16String();
            ReadOnlySpan<char> key = keyUtf16.Span;
#else
            string? key = keySeq.FirstOrDefault.GetString();
            if (key is null)
            {
                return Sequence.Undefined;
            }
#endif

            // Collect all objects from the sequence, flattening any nested arrays
            var results = default(SequenceBuilder);
            LookupCollect(objSeq, key, ref results);

            Sequence resultSeq = results.ToSequence();
            if (resultSeq.IsUndefined)
            {
                results.ReturnArray();
                return Sequence.Undefined;
            }

            if (resultSeq.Count == 1)
            {
                results.ReturnArray();
                return new Sequence(resultSeq[0]);
            }

            JsonElement arrResult = JsonataHelpers.ArrayFromSequence(resultSeq, env.Workspace);
            results.ReturnArray();
            return new Sequence(arrResult);
        };
    }

#if NET
    private static void LookupCollect(Sequence seq, ReadOnlySpan<char> key, ref SequenceBuilder results)
    {
        for (int i = 0; i < seq.Count; i++)
        {
            var el = seq[i];
            LookupCollectElement(el, key, ref results);
        }
    }

    private static void LookupCollectElement(JsonElement el, ReadOnlySpan<char> key, ref SequenceBuilder results)
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                LookupCollectElement(item, key, ref results);
            }
        }
        else if (el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty(key, out var value))
            {
                results.Add(value);
            }
        }
    }
#else
    private static void LookupCollect(Sequence seq, string key, ref SequenceBuilder results)
    {
        for (int i = 0; i < seq.Count; i++)
        {
            var el = seq[i];
            LookupCollectElement(el, key, ref results);
        }
    }

    private static void LookupCollectElement(JsonElement el, string key, ref SequenceBuilder results)
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                LookupCollectElement(item, key, ref results);
            }
        }
        else if (el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty(key, out var value))
            {
                results.Add(value);
            }
        }
    }
#endif

    // --- Thread-safe random for netstandard2.0/net481 compatibility ---
    [ThreadStatic]
    private static Random? t_random;

    private static Random ThreadRandom => t_random ??= new Random();

    // --- Higher-order: single, sift ---
    private static ExpressionEvaluator CompileSingle(ExpressionEvaluator[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            throw new JsonataException("T0410", SR.T0410_SingleExpects12Arguments, 0);
        }

        var arrArg = args[0];
        var funcArg = args.Length > 1 ? args[1] : null;

        return (in JsonElement input, Environment env) =>
        {
            var seq = arrArg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            // No predicate — just check that there is exactly one element
            if (funcArg is null)
            {
                if (seq.IsSingleton && seq.FirstOrDefault.ValueKind == JsonValueKind.Array)
                {
                    var arr = seq.FirstOrDefault;
                    int len = arr.GetArrayLength();
                    if (len == 0)
                    {
                        throw new JsonataException("D3139", SR.D3139_TheSingleFunctionMatchedZeroResults, 0);
                    }

                    if (len != 1)
                    {
                        throw new JsonataException("D3138", SR.D3138_TheSingleFunctionExpectedExactlyOneMatchingResult, 0);
                    }

                    return new Sequence(arr[0]);
                }

                if (seq.Count == 0)
                {
                    throw new JsonataException("D3139", SR.D3139_TheSingleFunctionMatchedZeroResults, 0);
                }

                if (seq.Count != 1)
                {
                    throw new JsonataException("D3138", SR.D3138_TheSingleFunctionExpectedExactlyOneMatchingResult, 0);
                }

                return new Sequence(seq.FirstOrDefault);
            }

            // With predicate — iterate directly, no intermediate SequenceBuilder
            var funcSeq = funcArg(input, env);
            if (!funcSeq.IsLambda)
            {
                return Sequence.Undefined;
            }

            var lambda = funcSeq.Lambda!;
            int arity = lambda.Arity;
            var invokeEnv = lambda.CreateInvokeEnv(env);
            Sequence arrSeq = arity >= 3 ? seq : default;

            try
            {
                JsonElement? match = null;

                if (seq.IsSingleton && seq.FirstOrDefault.ValueKind == JsonValueKind.Array)
                {
                    var arr = seq.FirstOrDefault;
                    int i = 0;
                    foreach (var item in arr.EnumerateArray())
                    {
                        Sequence arg0 = new Sequence(item);
                        Sequence arg1 = arity >= 2 ? Sequence.FromDouble(i, env.Workspace) : default;

#if NET9_0_OR_GREATER
                        var result = lambda.InvokeReusing([arg0, arg1, arrSeq], item, invokeEnv, env);
#else
                        var result = lambda.InvokeReusing(new Sequence[] { arg0, arg1, arrSeq }, item, invokeEnv, env);
#endif
                        if (FunctionalCompiler.IsTruthy(result))
                        {
                            if (match.HasValue)
                            {
                                throw new JsonataException("D3138", SR.D3138_TheSingleFunctionExpectedExactlyOneMatchingResult, 0);
                            }

                            match = item;
                        }

                        i++;
                    }
                }
                else
                {
                    for (int i = 0; i < seq.Count; i++)
                    {
                        var item = seq[i];
                        Sequence arg0 = new Sequence(item);
                        Sequence arg1 = arity >= 2 ? Sequence.FromDouble(i, env.Workspace) : default;

#if NET9_0_OR_GREATER
                        var result = lambda.InvokeReusing([arg0, arg1, arrSeq], item, invokeEnv, env);
#else
                        var result = lambda.InvokeReusing(new Sequence[] { arg0, arg1, arrSeq }, item, invokeEnv, env);
#endif
                        if (FunctionalCompiler.IsTruthy(result))
                        {
                            if (match.HasValue)
                            {
                                throw new JsonataException("D3138", SR.D3138_TheSingleFunctionExpectedExactlyOneMatchingResult, 0);
                            }

                            match = item;
                        }
                    }
                }

                if (!match.HasValue)
                {
                    throw new JsonataException("D3139", SR.D3139_TheSingleFunctionMatchedZeroResults, 0);
                }

                return new Sequence(match.Value);
            }
            finally
            {
                Environment.ReturnChild(invokeEnv);
            }
        };
    }

    private static ExpressionEvaluator CompileSift(ExpressionEvaluator[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            throw new JsonataException("T0410", SR.T0410_SiftExpects12Arguments, 0);
        }

        // When called with 1 arg, the object is the current context
        var objArg = args.Length == 2 ? args[0] : (ExpressionEvaluator)((in JsonElement input, Environment env) => new Sequence(input));
        var funcArg = args.Length == 2 ? args[1] : args[0];

        return (in JsonElement input, Environment env) =>
        {
            var seq = objArg(input, env);
            var funcSeq = funcArg(input, env);
            if (seq.IsUndefined || !funcSeq.IsLambda)
            {
                return Sequence.Undefined;
            }

            var obj = seq.FirstOrDefault;
            if (obj.ValueKind != JsonValueKind.Object)
            {
                return Sequence.Undefined;
            }

            var lambda = funcSeq.Lambda!;
            JsonDocumentBuilder<JsonElement.Mutable> objDoc = JsonElement.CreateObjectBuilder(env.Workspace, 16);
            JsonElement.Mutable objRoot = objDoc.RootElement;
            bool anyMatch = false;

            Environment reuseEnv = lambda.CreateInvokeEnv(env);
            Sequence[] lambdaArgs = ArrayPool<Sequence>.Shared.Rent(3);
            try
            {
                lambdaArgs[2] = new Sequence(obj);
                foreach (var prop in obj.EnumerateObject())
                {
                    lambdaArgs[0] = new Sequence(prop.Value);
                    using UnescapedUtf8JsonString nameUtf8 = prop.Utf8NameSpan;
                    lambdaArgs[1] = new Sequence(JsonataHelpers.StringFromUnescapedUtf8(nameUtf8.Span, env.Workspace));
                    var result = lambda.InvokeReusing(lambdaArgs, input, reuseEnv, env);
                    if (FunctionalCompiler.IsTruthy(result))
                    {
                        objRoot.SetProperty(nameUtf8.Span, prop.Value);
                        anyMatch = true;
                    }
                }
            }
            finally
            {
                ArrayPool<Sequence>.Shared.Return(lambdaArgs);
                Environment.ReturnChild(reuseEnv);
            }

            if (!anyMatch)
            {
                return Sequence.Undefined;
            }

            return new Sequence((JsonElement)objRoot);
        };
    }

    // --- String functions: pad, match, replace ---
    private static ExpressionEvaluator CompilePad(ExpressionEvaluator[] args)
    {
        if (args.Length < 2 || args.Length > 3)
        {
            throw new JsonataException("T0410", SR.T0410_PadExpects23Arguments, 0);
        }

        var strArg = args[0];
        var widthArg = args[1];
        var charArg = args.Length > 2 ? args[2] : null;

        return (in JsonElement input, Environment env) =>
        {
            var strSeq = strArg(input, env);
            var widthSeq = widthArg(input, env);
            if (strSeq.IsUndefined || widthSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            string str = FunctionalCompiler.CoerceElementToString(strSeq.FirstOrDefault);
            if (!FunctionalCompiler.TryCoerceToNumber(widthSeq.FirstOrDefault, out double widthNum))
            {
                return Sequence.Undefined;
            }

            int width = (int)widthNum;
            string padStr = " ";
            if (charArg is not null)
            {
                var charSeq = charArg(input, env);
                if (!charSeq.IsUndefined)
                {
                    string charVal = FunctionalCompiler.CoerceElementToString(charSeq.FirstOrDefault);
                    if (charVal.Length > 0)
                    {
                        padStr = charVal;
                    }
                }
            }

            int cpLen = CountCodePoints(str);
            int absWidth = Math.Abs(width);
            int padNeeded = absWidth - cpLen;
            if (padNeeded <= 0)
            {
                return new Sequence(JsonataHelpers.StringFromString(str, env.Workspace));
            }

            // Build cyclic pad string by code points
            int[] padCodePoints = ToCodePointArray(padStr);
            var sb = new StringBuilder();
            for (int i = 0; i < padNeeded; i++)
            {
                int cp = padCodePoints[i % padCodePoints.Length];
                if (cp > 0xFFFF)
                {
                    sb.Append(char.ConvertFromUtf32(cp));
                }
                else
                {
                    sb.Append((char)cp);
                }
            }

            string padding = sb.ToString();
            string result = width > 0 ? str + padding : padding + str;
            return new Sequence(JsonataHelpers.StringFromString(result, env.Workspace));
        };
    }

    private static ExpressionEvaluator CompileMatch(ExpressionEvaluator[] args)
    {
        ExpressionEvaluator strArg;
        ExpressionEvaluator patternArg;
        ExpressionEvaluator? limitArg;

        if (args.Length == 1)
        {
            strArg = ContextArg;
            patternArg = args[0];
            limitArg = null;
        }
        else if (args.Length == 2)
        {
            strArg = args[0];
            patternArg = args[1];
            limitArg = null;
        }
        else if (args.Length == 3)
        {
            strArg = args[0];
            patternArg = args[1];
            limitArg = args[2];
        }
        else
        {
            throw new JsonataException("T0410", SR.T0410_MatchExpects13Arguments, 0);
        }

        return (in JsonElement input, Environment env) =>
        {
            var strSeq = strArg(input, env);
            var patSeq = patternArg(input, env);

            if (strSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            // Custom matcher protocol: pattern is a function that returns
            // {match, start, end, groups, next} where next() returns the next match
            if (patSeq.IsLambda)
            {
                string? matcherStr = strSeq.FirstOrDefault.GetString();
                if (matcherStr is null)
                {
                    return Sequence.Undefined;
                }

                int matcherLimit = int.MaxValue;
                if (limitArg is not null)
                {
                    var limitSeqVal = limitArg(input, env);
                    if (FunctionalCompiler.TryCoerceToNumber(limitSeqVal.FirstOrDefault, out double limitD))
                    {
                        matcherLimit = (int)limitD;
                    }
                }

                var matcherLambda = patSeq.Lambda!;
                var matcherResults = default(SequenceBuilder);
                int matcherCount = 0;

                // Call the matcher with the string
                var matchResult = matcherLambda.Invoke(new[] { strSeq }, 1, input, env);

                while (!matchResult.IsUndefined && matcherCount < matcherLimit)
                {
                    var matchObj = matchResult.FirstOrDefault;
                    if (matchObj.ValueKind != JsonValueKind.Object)
                    {
                        break;
                    }

                    // Extract match, start, groups from the result
                    string matchStr = string.Empty;
                    int matchIndex = 0;
                    var matcherGroups = new List<string>();

                    if (matchObj.TryGetProperty("match"u8, out JsonElement matchProp))
                    {
                        matchStr = matchProp.GetString() ?? string.Empty;
                    }

                    if (matchObj.TryGetProperty("start"u8, out JsonElement startProp))
                    {
                        if (FunctionalCompiler.TryCoerceToNumber(startProp, out double startD))
                        {
                            matchIndex = (int)startD;
                        }
                    }

                    if (matchObj.TryGetProperty("groups"u8, out JsonElement groupsProp)
                        && groupsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var g in groupsProp.EnumerateArray())
                        {
                            matcherGroups.Add(g.GetString() ?? string.Empty);
                        }
                    }

                    matcherResults.Add(JsonataHelpers.CreateMatchObject(matchStr, matchIndex, matcherGroups, env.Workspace));
                    matcherCount++;

                    // Follow the 'next' property — a lambda carried in ObjectLambdas
                    if (matchResult.ObjectLambdas?.TryGetValue("next", out LambdaValue? nextLambda) == true
                        && nextLambda is not null)
                    {
                        matchResult = nextLambda.Invoke(Array.Empty<Sequence>(), 0, input, env);
                    }
                    else
                    {
                        break;
                    }
                }

                Sequence customResult = matcherResults.ToSequence();
                if (customResult.IsUndefined)
                {
                    matcherResults.ReturnArray();
                    return Sequence.Undefined;
                }

                if (customResult.Count == 1)
                {
                    matcherResults.ReturnArray();
                    return new Sequence(customResult[0]);
                }

                // Wrap multi-element result in an array — consistent with all other array-returning functions
                JsonElement array = JsonataHelpers.ArrayFromSequence(customResult, env.Workspace);
                matcherResults.ReturnArray();
                return new Sequence(array);
            }

            if (!patSeq.IsRegex)
            {
                return Sequence.Undefined;
            }

            int limit = int.MaxValue;
            if (limitArg is not null)
            {
                var limitSeqVal = limitArg(input, env);
                if (FunctionalCompiler.TryCoerceToNumber(limitSeqVal.FirstOrDefault, out double limitD))
                {
                    limit = (int)limitD;
                }
            }

            Regex regex = patSeq.Regex!;

#if NET
            if (strSeq.FirstOrDefault.ValueKind != JsonValueKind.String)
            {
                return Sequence.Undefined;
            }

            bool hasGroups = regex.GetGroupNumbers().Length > 1;

            if (!hasGroups)
            {
                // Fused single-document: outer array with nested object builders per match
                using UnescapedUtf16JsonString utf16Str = strSeq.FirstOrDefault.GetUtf16String();
                ReadOnlyMemory<char> charMemory = utf16Str.Memory;

                var doc = JsonElement.CreateBuilder(
                    env.Workspace,
                    (charMemory, regex, limit),
                    static (in (ReadOnlyMemory<char> Chars, Regex Regex, int Limit) ctx, ref JsonElement.ArrayBuilder ab) =>
                    {
                        int count = 0;
                        foreach (ValueMatch vm in ctx.Regex.EnumerateMatches(ctx.Chars.Span))
                        {
                            if (count >= ctx.Limit)
                            {
                                break;
                            }

                            ab.AddItem(
                                (ctx.Chars, vm.Index, vm.Length),
                                static (in (ReadOnlyMemory<char> C, int Index, int Length) mctx, ref JsonElement.ObjectBuilder ob) =>
                                {
                                    ob.AddProperty("match"u8, mctx.C.Span.Slice(mctx.Index, mctx.Length));
                                    ob.AddProperty("index"u8, mctx.Index);
                                    ob.AddProperty("groups"u8, static (ref JsonElement.ArrayBuilder _) => { });
                                });
                            count++;
                        }
                    },
                    estimatedMemberCount: 30);

                return UnwrapMatchArray((JsonElement)doc.RootElement);
            }
            else
            {
                // Fused single-document: outer array with nested object+groups builders per match
                string regexStr = strSeq.FirstOrDefault.GetString()!;
                ReadOnlyMemory<char> charMemory = regexStr.AsMemory();

                var doc = JsonElement.CreateBuilder(
                    env.Workspace,
                    (charMemory, regex, regexStr, limit),
                    static (in (ReadOnlyMemory<char> Chars, Regex Regex, string RegexStr, int Limit) ctx, ref JsonElement.ArrayBuilder ab) =>
                    {
                        int count = 0;
                        Match m = ctx.Regex.Match(ctx.RegexStr);
                        while (m.Success && count < ctx.Limit)
                        {
                            ab.AddItem(
                                (ctx.Chars, m),
                                static (in (ReadOnlyMemory<char> C, Match M) mctx, ref JsonElement.ObjectBuilder ob) =>
                                {
                                    ob.AddProperty("match"u8, mctx.C.Span.Slice(mctx.M.Index, mctx.M.Length));
                                    ob.AddProperty("index"u8, mctx.M.Index);
                                    ob.AddProperty("groups"u8, (mctx.C, mctx.M),
                                        static (in (ReadOnlyMemory<char> C, Match M) gctx, ref JsonElement.ArrayBuilder gab) =>
                                        {
                                            for (int g = 1; g < gctx.M.Groups.Count; g++)
                                            {
                                                Group grp = gctx.M.Groups[g];
                                                gab.AddItem(gctx.C.Span.Slice(grp.Index, grp.Length));
                                            }
                                        });
                                });
                            count++;
                            m = m.NextMatch();
                        }
                    },
                    estimatedMemberCount: 30);

                return UnwrapMatchArray((JsonElement)doc.RootElement);
            }
#else
            string? regexStr = strSeq.FirstOrDefault.GetString();
            if (regexStr is null)
            {
                return Sequence.Undefined;
            }

            ReadOnlyMemory<char> charMemory = regexStr.AsMemory();
            MatchCollection matches = regex.Matches(regexStr);

            // Fused single-document: outer array with nested object+groups builders per match
            var doc = JsonElement.CreateBuilder(
                env.Workspace,
                (charMemory, matches, limit),
                static (in (ReadOnlyMemory<char> Chars, MatchCollection Matches, int Limit) ctx, ref JsonElement.ArrayBuilder ab) =>
                {
                    int count = 0;
                    foreach (Match m in ctx.Matches)
                    {
                        if (count >= ctx.Limit)
                        {
                            break;
                        }

                        ab.AddItem(
                            (ctx.Chars, m),
                            static (in (ReadOnlyMemory<char> C, Match M) mctx, ref JsonElement.ObjectBuilder ob) =>
                            {
                                ob.AddProperty("match"u8, mctx.C.Span.Slice(mctx.M.Index, mctx.M.Length));
                                ob.AddProperty("index"u8, mctx.M.Index);
                                ob.AddProperty("groups"u8, (mctx.C, mctx.M),
                                    static (in (ReadOnlyMemory<char> C, Match M) gctx, ref JsonElement.ArrayBuilder gab) =>
                                    {
                                        for (int g = 1; g < gctx.M.Groups.Count; g++)
                                        {
                                            Group grp = gctx.M.Groups[g];
                                            gab.AddItem(gctx.C.Span.Slice(grp.Index, grp.Length));
                                        }
                                    });
                            });
                        count++;
                    }
                },
                estimatedMemberCount: 30);

            return UnwrapMatchArray((JsonElement)doc.RootElement);
#endif
        };
    }

    private static ExpressionEvaluator CompileReplace(ExpressionEvaluator[] args)
    {
        ExpressionEvaluator strArg;
        ExpressionEvaluator patternArg;
        ExpressionEvaluator replacementArg;
        ExpressionEvaluator? limitArg;

        if (args.Length == 2)
        {
            strArg = ContextArg;
            patternArg = args[0];
            replacementArg = args[1];
            limitArg = null;
        }
        else if (args.Length == 3)
        {
            strArg = args[0];
            patternArg = args[1];
            replacementArg = args[2];
            limitArg = null;
        }
        else if (args.Length == 4)
        {
            strArg = args[0];
            patternArg = args[1];
            replacementArg = args[2];
            limitArg = args[3];
        }
        else
        {
            throw new JsonataException("T0410", SR.T0410_ReplaceExpects24Arguments, 0);
        }

        return (in JsonElement input, Environment env) =>
        {
            var strSeq = strArg(input, env);
            var patSeq = patternArg(input, env);
            var repSeq = replacementArg(input, env);
            if (strSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            // Validate input is a string
            if (strSeq.IsLambda || strSeq.FirstOrDefault.ValueKind != JsonValueKind.String)
            {
                throw new JsonataException("T0410", SR.T0410_Argument1OfFunctionReplaceIsNotOfTheCorrectType, 0);
            }

            // Validate pattern is a string or regex
            if (!patSeq.IsRegex)
            {
                if (patSeq.IsUndefined || patSeq.IsLambda || patSeq.FirstOrDefault.ValueKind != JsonValueKind.String)
                {
                    throw new JsonataException("T0410", SR.T0410_Argument2OfFunctionReplaceIsNotOfTheCorrectType, 0);
                }
            }

            // Validate replacement is a string or function
            bool isLambdaReplacement = repSeq.IsLambda;
            if (!isLambdaReplacement)
            {
                if (repSeq.IsUndefined || repSeq.FirstOrDefault.ValueKind != JsonValueKind.String)
                {
                    throw new JsonataException("T0410", SR.T0410_Argument3OfFunctionReplaceIsNotOfTheCorrectType, 0);
                }
            }

            int limit = int.MaxValue;
            if (limitArg is not null)
            {
                var limSeq = limitArg(input, env);
                if (!limSeq.IsUndefined)
                {
                    if (limSeq.IsLambda || limSeq.FirstOrDefault.ValueKind != JsonValueKind.Number)
                    {
                        throw new JsonataException("T0410", SR.T0410_Argument4OfFunctionReplaceIsNotOfTheCorrectType, 0);
                    }

                    if (FunctionalCompiler.TryCoerceToNumber(limSeq.FirstOrDefault, out double n))
                    {
                        if (n < 0)
                        {
                            throw new JsonataException("D3011", SR.D3011_TheFourthArgumentOfTheReplaceFunctionMustBeAPositiveNumber, 0);
                        }

                        limit = (int)n;
                    }
                }
            }

            if (patSeq.IsRegex)
            {
                Regex regex = patSeq.Regex!;
                if (isLambdaReplacement)
                {
                    string str = FunctionalCompiler.CoerceElementToString(strSeq.FirstOrDefault);
                    str = RegexReplaceWithFunction(str, regex, repSeq.Lambda!, limit, input, env);
                    return new Sequence(JsonataHelpers.StringFromString(str, env.Workspace));
                }
                else
                {
                    return new Sequence(RegexReplaceWithStringElement(
                        strSeq.FirstOrDefault, regex, repSeq.FirstOrDefault, limit, env.Workspace));
                }
            }
            else
            {
                return new Sequence(StringReplaceElement(
                    strSeq.FirstOrDefault, patSeq.FirstOrDefault, repSeq.FirstOrDefault, limit, env.Workspace));
            }
        };
    }

    private static string RegexReplaceWithString(string str, Regex regex, string replacement, int limit)
    {
        if (limit <= 0)
        {
            return str;
        }

        int count = 0;
        int searchStart = 0;
        var sb = new StringBuilder();

        while (count < limit && searchStart <= str.Length)
        {
            Match m = regex.Match(str, searchStart);
            if (!m.Success)
            {
                break;
            }

            if (m.Length == 0)
            {
                throw new JsonataException("D1004", SR.D1004_RegularExpressionMatchesZeroLengthString, 0);
            }

            sb.Append(str, searchStart, m.Index - searchStart);
            sb.Append(ApplyJsonataBackreferences(replacement, m));
            searchStart = m.Index + m.Length;
            count++;
        }

        sb.Append(str, searchStart, str.Length - searchStart);
        return sb.ToString();
    }

    private static string RegexReplaceWithFunction(
        string str,
        Regex regex,
        LambdaValue lambda,
        int limit,
        in JsonElement input,
        Environment env)
    {
        if (limit <= 0)
        {
            return str;
        }

        int count = 0;
        int searchStart = 0;
        var sb = new StringBuilder();

        while (count < limit && searchStart <= str.Length)
        {
            Match m = regex.Match(str, searchStart);
            if (!m.Success)
            {
                break;
            }

            if (m.Length == 0)
            {
                throw new JsonataException("D1004", SR.D1004_RegularExpressionMatchesZeroLengthString, 0);
            }

            sb.Append(str, searchStart, m.Index - searchStart);

            var groups = new List<string>();
            for (int g = 1; g < m.Groups.Count; g++)
            {
                groups.Add(m.Groups[g].Value);
            }

            JsonElement matchObj = JsonataHelpers.CreateMatchObject(m.Value, m.Index, groups, env.Workspace);
            Sequence result = lambda.Invoke(new[] { new Sequence(matchObj) }, 1, input, env);

            if (result.IsUndefined || result.FirstOrDefault.ValueKind != JsonValueKind.String)
            {
                throw new JsonataException("D3012", SR.D3012_TheReplacementFunctionMustReturnAString, 0);
            }

            sb.Append(result.FirstOrDefault.GetString());
            searchStart = m.Index + m.Length;
            count++;
        }

        sb.Append(str, searchStart, str.Length - searchStart);
        return sb.ToString();
    }

    private static string ApplyJsonataBackreferences(string replacement, Match match)
    {
        int numGroups = match.Groups.Count - 1;
        var sb = new StringBuilder(replacement.Length);

        for (int i = 0; i < replacement.Length; i++)
        {
            if (replacement[i] != '$')
            {
                sb.Append(replacement[i]);
                continue;
            }

            // At '$'
            if (i + 1 >= replacement.Length)
            {
                // $ at end of string → literal $
                sb.Append('$');
                continue;
            }

            char next = replacement[i + 1];

            if (next == '$')
            {
                // $$ → literal $
                sb.Append('$');
                i++;
            }
            else if (next >= '0' && next <= '9')
            {
                // Read all following digits
                int digitStart = i + 1;
                int digitEnd = digitStart;
                while (digitEnd < replacement.Length && replacement[digitEnd] >= '0' && replacement[digitEnd] <= '9')
                {
                    digitEnd++;
                }

                string allDigits = replacement.Substring(digitStart, digitEnd - digitStart);

                // Try longest valid prefix (shorten from the right)
                int consumed = allDigits.Length;
                bool found = false;
                while (consumed > 0)
                {
                    int groupNum = int.Parse(allDigits.Substring(0, consumed), CultureInfo.InvariantCulture);
                    if (groupNum <= numGroups)
                    {
                        sb.Append(match.Groups[groupNum].Value);

                        // Remaining digits become literal
                        sb.Append(allDigits, consumed, allDigits.Length - consumed);
                        found = true;
                        break;
                    }

                    consumed--;
                }

                if (!found)
                {
                    // No valid group reference; remaining digits after the single invalid digit are literal
                    sb.Append(allDigits, 1, allDigits.Length - 1);
                }

                i = digitEnd - 1;
            }
            else
            {
                // $ followed by non-digit → literal $<char>
                sb.Append('$');
                sb.Append(next);
                i++;
            }
        }

        return sb.ToString();
    }

    // --- Optimized string replace ---

    /// <summary>
    /// Optimized string-pattern replace: uses <see cref="Utf8ValueStringBuilder"/> and
    /// UTF-8 byte-level <see cref="MemoryExtensions.IndexOf{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/>
    /// to avoid intermediate string allocations.
    /// </summary>
    private static JsonElement StringReplaceElement(
        in JsonElement strElement,
        in JsonElement patElement,
        in JsonElement repElement,
        int limit,
        JsonWorkspace workspace)
    {
        using UnescapedUtf8JsonString utf8Str = strElement.GetUtf8String();
        using UnescapedUtf8JsonString utf8Pat = patElement.GetUtf8String();
        using UnescapedUtf8JsonString utf8Rep = repElement.GetUtf8String();

        ReadOnlySpan<byte> str = utf8Str.Span;
        ReadOnlySpan<byte> pat = utf8Pat.Span;
        ReadOnlySpan<byte> rep = utf8Rep.Span;

        if (pat.Length == 0)
        {
            throw new JsonataException("D3010", SR.D3010_TheSecondArgumentOfTheReplaceFunctionCannotBeAnEmptyString, 0);
        }

        Utf8ValueStringBuilder sb = new(stackalloc byte[JsonConstants.StackallocByteThreshold]);
        try
        {
            int count = 0;
            int searchStart = 0;
            int idx;
            while (count < limit && (idx = str.Slice(searchStart).IndexOf(pat)) >= 0)
            {
                sb.Append(str.Slice(searchStart, idx));
                sb.Append(rep);
                searchStart += idx + pat.Length;
                count++;
            }

            sb.Append(str.Slice(searchStart));

            return JsonataHelpers.StringFromUnescapedUtf8(sb.AsSpan(), workspace);
        }
        finally
        {
            sb.Dispose();
        }
    }

    // --- Optimized regex replace ---

    /// <summary>
    /// Optimized regex replace: uses <see cref="ValueStringBuilder"/> and
    /// <see cref="UnescapedUtf16JsonString"/> to avoid intermediate string allocations.
    /// On NET, uses <see cref="Regex.EnumerateMatches(ReadOnlySpan{char})"/> for patterns
    /// whose replacement string contains no backreferences.
    /// </summary>
    private static JsonElement RegexReplaceWithStringElement(
        in JsonElement strElement,
        Regex regex,
        in JsonElement repElement,
        int limit,
        JsonWorkspace workspace)
    {
        if (limit <= 0)
        {
            return strElement;
        }

        using var utf16Str = strElement.GetUtf16String();
        ReadOnlySpan<char> strChars = utf16Str.Span;

        using var utf16Rep = repElement.GetUtf16String();
        ReadOnlySpan<char> repChars = utf16Rep.Span;

        ValueStringBuilder sb = new(stackalloc char[JsonConstants.StackallocCharThreshold]);
        try
        {
#if NET
            bool hasBackrefs = repChars.Contains('$');

            if (!hasBackrefs)
            {
                // Fast path: EnumerateMatches, no group access needed
                int count = 0;
                int searchStart = 0;
                foreach (ValueMatch vm in regex.EnumerateMatches(strChars))
                {
                    if (count >= limit)
                    {
                        break;
                    }

                    if (vm.Length == 0)
                    {
                        throw new JsonataException("D1004", SR.D1004_RegularExpressionMatchesZeroLengthString, 0);
                    }

                    sb.Append(strChars.Slice(searchStart, vm.Index - searchStart));
                    sb.Append(repChars);
                    searchStart = vm.Index + vm.Length;
                    count++;
                }

                sb.Append(strChars.Slice(searchStart));
            }
            else
            {
                // Backreference path: need Match objects for group access
                string str = strElement.GetString()!;
                int count = 0;
                int searchStart = 0;
                while (count < limit && searchStart <= str.Length)
                {
                    Match m = regex.Match(str, searchStart);
                    if (!m.Success)
                    {
                        break;
                    }

                    if (m.Length == 0)
                    {
                        throw new JsonataException("D1004", SR.D1004_RegularExpressionMatchesZeroLengthString, 0);
                    }

                    sb.Append(strChars.Slice(searchStart, m.Index - searchStart));
                    ApplyJsonataBackreferencesSpan(repChars, m, ref sb);
                    searchStart = m.Index + m.Length;
                    count++;
                }

                sb.Append(strChars.Slice(searchStart));
            }
#else
            // netstandard: use Match objects
            string str = strElement.GetString()!;
            int count = 0;
            int searchStart = 0;
            while (count < limit && searchStart <= str.Length)
            {
                Match m = regex.Match(str, searchStart);
                if (!m.Success)
                {
                    break;
                }

                if (m.Length == 0)
                {
                    throw new JsonataException("D1004", SR.D1004_RegularExpressionMatchesZeroLengthString, 0);
                }

                sb.Append(strChars.Slice(searchStart, m.Index - searchStart));
                ApplyJsonataBackreferencesSpan(repChars, m, ref sb);
                searchStart = m.Index + m.Length;
                count++;
            }

            sb.Append(strChars.Slice(searchStart));
#endif

            return JsonataHelpers.StringFromChars(sb.AsSpan(), workspace);
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Applies JSONata backreference substitution ($0, $1, etc.) from a replacement span
    /// directly into a <see cref="ValueStringBuilder"/>, avoiding intermediate string allocations.
    /// </summary>
    private static void ApplyJsonataBackreferencesSpan(
        ReadOnlySpan<char> replacement, Match match, ref ValueStringBuilder sb)
    {
        int numGroups = match.Groups.Count - 1;
        int segStart = 0;

        for (int i = 0; i < replacement.Length; i++)
        {
            if (replacement[i] != '$')
            {
                continue;
            }

            // Flush segment before '$'
            if (i > segStart)
            {
                sb.Append(replacement.Slice(segStart, i - segStart));
            }

            if (i + 1 >= replacement.Length)
            {
                // $ at end of string → literal $
                sb.Append('$');
                segStart = i + 1;
                continue;
            }

            char next = replacement[i + 1];

            if (next == '$')
            {
                // $$ → literal $
                sb.Append('$');
                i++;
                segStart = i + 1;
            }
            else if (next >= '0' && next <= '9')
            {
                // Read all following digits
                int digitStart = i + 1;
                int digitEnd = digitStart;
                while (digitEnd < replacement.Length && replacement[digitEnd] >= '0' && replacement[digitEnd] <= '9')
                {
                    digitEnd++;
                }

                // Try longest valid prefix (shorten from the right)
                int consumed = digitEnd - digitStart;
                bool found = false;
                while (consumed > 0)
                {
                    int groupNum = ParseDigits(replacement.Slice(digitStart, consumed));
                    if (groupNum <= numGroups)
                    {
#if NET
                        sb.Append(match.Groups[groupNum].ValueSpan);
#else
                        sb.Append(match.Groups[groupNum].Value);
#endif

                        // Remaining digits become literal
                        if (digitEnd > digitStart + consumed)
                        {
                            sb.Append(replacement.Slice(digitStart + consumed, digitEnd - digitStart - consumed));
                        }

                        found = true;
                        break;
                    }

                    consumed--;
                }

                if (!found)
                {
                    // No valid group reference; remaining digits after the single invalid digit are literal
                    if (digitEnd > digitStart + 1)
                    {
                        sb.Append(replacement.Slice(digitStart + 1, digitEnd - digitStart - 1));
                    }
                }

                i = digitEnd - 1;
                segStart = digitEnd;
            }
            else
            {
                // $ followed by non-digit, non-$ → literal $<char>
                sb.Append('$');
                sb.Append(next);
                i++;
                segStart = i + 1;
            }
        }

        // Flush remaining segment
        if (segStart < replacement.Length)
        {
            sb.Append(replacement.Slice(segStart));
        }
    }

    /// <summary>
    /// Parses a span of ASCII digit characters as a non-negative integer.
    /// </summary>
    private static int ParseDigits(ReadOnlySpan<char> digits)
    {
        int result = 0;
        for (int i = 0; i < digits.Length; i++)
        {
            result = (result * 10) + (digits[i] - '0');
        }

        return result;
    }

    /// <summary>
    /// Span-based string split for RT. Uses UTF-8 byte-level IndexOf to avoid
    /// allocating <c>string[]</c> or <c>GetString()</c> calls.
    /// </summary>
    private static JsonElement SplitString(
        in JsonElement strElement, in JsonElement sepElement, int limit, JsonWorkspace workspace)
    {
        if (sepElement.ValueKind != JsonValueKind.String)
        {
            return default;
        }

        using UnescapedUtf8JsonString utf8Str = strElement.GetUtf8String();
        using UnescapedUtf8JsonString utf8Sep = sepElement.GetUtf8String();
        ReadOnlySpan<byte> str = utf8Str.Span;
        ReadOnlySpan<byte> sep = utf8Sep.Span;

        if (sep.Length == 0)
        {
            // Split into individual UTF-8 code points
            int cpCount = 0;
            for (int i = 0; i < str.Length;)
            {
                cpCount++;
                byte b = str[i];
                i += b < 0x80 ? 1 : b < 0xE0 ? 2 : b < 0xF0 ? 3 : 4;
            }

            int count = Math.Min(cpCount, limit);
            JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, count);
            JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
            int pos = 0;
            for (int c = 0; c < count; c++)
            {
                byte b = str[pos];
                int cpLen = b < 0x80 ? 1 : b < 0xE0 ? 2 : b < 0xF0 ? 3 : 4;
                arrayRoot.AddItem(JsonataHelpers.StringFromUnescapedUtf8(str.Slice(pos, cpLen), workspace));
                pos += cpLen;
            }

            return (JsonElement)arrayRoot;
        }
        else if (limit <= 0)
        {
            JsonDocumentBuilder<JsonElement.Mutable> emptyDoc = JsonElement.CreateArrayBuilder(workspace, 0);
            return (JsonElement)emptyDoc.RootElement;
        }
        else
        {
            // Count splits up to limit - 1 (to produce limit parts)
            int matchCount = 0;
            int searchStart = 0;
            while (matchCount < limit - 1)
            {
                int idx = str.Slice(searchStart).IndexOf(sep);
                if (idx < 0)
                {
                    break;
                }

                matchCount++;
                searchStart += idx + sep.Length;

                if (searchStart > str.Length)
                {
                    break;
                }
            }

            // JSONata: limit caps the number of results. If we found fewer splits
            // than limit-1, all parts fit. Otherwise, emit exactly limit parts
            // (no remainder appended beyond limit).
            bool exhausted = matchCount < limit - 1;
            int resultCount = exhausted ? matchCount + 1 : limit;
            JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, resultCount);
            JsonElement.Mutable arrayRoot = arrayDoc.RootElement;

            searchStart = 0;
            for (int i = 0; i < resultCount - 1; i++)
            {
                int idx = str.Slice(searchStart).IndexOf(sep);
                arrayRoot.AddItem(JsonataHelpers.StringFromUnescapedUtf8(str.Slice(searchStart, idx), workspace));
                searchStart += idx + sep.Length;
            }

            if (exhausted)
            {
                // All parts fit — add remainder
                arrayRoot.AddItem(JsonataHelpers.StringFromUnescapedUtf8(str.Slice(searchStart), workspace));
            }
            else
            {
                // Hit limit: take text up to next separator (or end)
                int idx = str.Slice(searchStart).IndexOf(sep);
                int partLen = idx >= 0 ? idx : str.Length - searchStart;
                arrayRoot.AddItem(JsonataHelpers.StringFromUnescapedUtf8(str.Slice(searchStart, partLen), workspace));
            }

            return (JsonElement)arrayRoot;
        }
    }

    /// <summary>
    /// Span-based regex split for RT. On NET8+, uses <see cref="Regex.EnumerateMatches(ReadOnlySpan{char})"/>
    /// to avoid per-match object allocations.
    /// </summary>
    private static JsonElement SplitRegex(
        in JsonElement strElement, Regex regex, int limit, JsonWorkspace workspace)
    {
        using UnescapedUtf16JsonString utf16Str = strElement.GetUtf16String();
        ReadOnlySpan<char> chars = utf16Str.Span;

        if (limit <= 0)
        {
            JsonDocumentBuilder<JsonElement.Mutable> emptyDoc = JsonElement.CreateArrayBuilder(workspace, 0);
            return (JsonElement)emptyDoc.RootElement;
        }

#if NET8_0_OR_GREATER
        // Pass 1: count matches up to limit - 1
        int matchCount = 0;
        foreach (ValueMatch vm0 in regex.EnumerateMatches(chars))
        {
            matchCount++;
            if (matchCount >= limit - 1)
            {
                break;
            }
        }

        bool exhausted = matchCount < limit - 1;
        int resultCount = exhausted ? matchCount + 1 : limit;
        JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, resultCount);
        JsonElement.Mutable arrayRoot = arrayDoc.RootElement;

        // Pass 2: emit parts
        int emitted = 0;
        int pos = 0;
        foreach (ValueMatch vm in regex.EnumerateMatches(chars))
        {
            if (emitted >= resultCount - 1)
            {
                break;
            }

            arrayRoot.AddItem(JsonataHelpers.StringFromChars(chars.Slice(pos, vm.Index - pos), workspace));
            pos = vm.Index + vm.Length;
            emitted++;
        }

        if (exhausted)
        {
            arrayRoot.AddItem(JsonataHelpers.StringFromChars(chars.Slice(pos), workspace));
        }
        else
        {
            // Hit limit: find next match boundary and take text up to it
            int nextEnd = chars.Length;
            int scan = 0;
            int mIdx = 0;
            foreach (ValueMatch vm2 in regex.EnumerateMatches(chars))
            {
                if (mIdx == resultCount - 1)
                {
                    nextEnd = vm2.Index;
                    break;
                }

                scan = vm2.Index + vm2.Length;
                mIdx++;
            }

            arrayRoot.AddItem(JsonataHelpers.StringFromChars(chars.Slice(pos, nextEnd - pos), workspace));
        }

        return (JsonElement)arrayRoot;
#else
        string str = chars.ToString();
        string[] parts = regex.Split(str);
        int resultCount = Math.Min(parts.Length, limit);
        JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, resultCount);
        JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
        for (int i = 0; i < resultCount; i++)
        {
            arrayRoot.AddItem(JsonataHelpers.StringFromString(parts[i], workspace));
        }

        return (JsonElement)arrayRoot;
#endif
    }

    // --- Encoding functions ---
    private static ExpressionEvaluator CompileBase64Encode(ExpressionEvaluator[] args)
    {
        if (args.Length > 1)
        {
            throw new JsonataException("T0410", SR.T0410_Base64encodeExpects01Arguments, 0);
        }

        if (args.Length == 0)
        {
            return static (in JsonElement input, Environment env) => Sequence.Undefined;
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            string str = FunctionalCompiler.CoerceElementToString(seq.FirstOrDefault);
            return new Sequence(JsonataHelpers.StringFromString(
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(str)), env.Workspace));
        };
    }

    private static ExpressionEvaluator CompileBase64Decode(ExpressionEvaluator[] args)
    {
        if (args.Length > 1)
        {
            throw new JsonataException("T0410", SR.T0410_Base64decodeExpects01Arguments, 0);
        }

        if (args.Length == 0)
        {
            return static (in JsonElement input, Environment env) => Sequence.Undefined;
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            string str = FunctionalCompiler.CoerceElementToString(seq.FirstOrDefault);
            return new Sequence(JsonataHelpers.StringFromString(
                System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(str)), env.Workspace));
        };
    }

    private static ExpressionEvaluator CompileEncodeUrlComponent(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_EncodeurlcomponentExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            string str;
            try
            {
                str = FunctionalCompiler.CoerceElementToString(seq.FirstOrDefault);
            }
            catch (InvalidOperationException)
            {
                throw new JsonataException("D3140", SR.D3140_MalformedUrlEncodeUrlComponent, 0);
            }

            ValidateNoUnpairedSurrogates(str, "$encodeUrlComponent");
            return new Sequence(JsonataHelpers.StringFromString(Uri.EscapeDataString(str), env.Workspace));
        };
    }

    private static ExpressionEvaluator CompileDecodeUrlComponent(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_DecodeurlcomponentExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            string str = FunctionalCompiler.CoerceElementToString(seq.FirstOrDefault);

            if (HasInvalidPercentEncoding(str))
            {
                throw new JsonataException("D3140", SR.Format(SR.D3140_MalformedUrlPassedToDecodeUrlComponent, str), 0);
            }

            try
            {
                return new Sequence(JsonataHelpers.StringFromString(Uri.UnescapeDataString(str), env.Workspace));
            }
            catch (Exception ex) when (ex is UriFormatException or ArgumentException or FormatException)
            {
                throw new JsonataException("D3140", SR.Format(SR.D3140_MalformedUrlPassedToDecodeUrlComponent, str), 0);
            }
        };
    }

#pragma warning disable SYSLIB0013 // Uri.EscapeUriString is obsolete
    private static ExpressionEvaluator CompileEncodeUrl(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_EncodeurlExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            string str;
            try
            {
                str = FunctionalCompiler.CoerceElementToString(seq.FirstOrDefault);
            }
            catch (InvalidOperationException)
            {
                throw new JsonataException("D3140", SR.D3140_MalformedUrlEncodeUrl, 0);
            }

            ValidateNoUnpairedSurrogates(str, "$encodeUrl");
            return new Sequence(JsonataHelpers.StringFromString(Uri.EscapeUriString(str), env.Workspace));
        };
    }
#pragma warning restore SYSLIB0013

    private static ExpressionEvaluator CompileDecodeUrl(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_DecodeurlExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            string str = FunctionalCompiler.CoerceElementToString(seq.FirstOrDefault);

            if (HasInvalidPercentEncoding(str))
            {
                throw new JsonataException("D3140", SR.Format(SR.D3140_MalformedUrlPassedToDecodeUrl, str), 0);
            }

            try
            {
                return new Sequence(JsonataHelpers.StringFromString(Uri.UnescapeDataString(str), env.Workspace));
            }
            catch (Exception ex) when (ex is UriFormatException or ArgumentException or FormatException)
            {
                throw new JsonataException("D3140", SR.Format(SR.D3140_MalformedUrlPassedToDecodeUrl, str), 0);
            }
        };
    }

    // --- Numeric functions: random, formatNumber, formatBase ---
    internal static bool HasInvalidPercentEncoding(string input)
    {
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '%')
            {
                if (i + 2 >= input.Length ||
                    !IsHexDigit(input[i + 1]) ||
                    !IsHexDigit(input[i + 2]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static void ValidateNoUnpairedSurrogates(string str, string funcName)
    {
        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 >= str.Length || !char.IsLowSurrogate(str[i + 1]))
                {
                    throw new JsonataException("D3140", SR.Format(SR.D3140_MalformedUrlPassedToFunc, funcName, str), 0);
                }

                i++;
            }
            else if (char.IsLowSurrogate(c))
            {
                throw new JsonataException("D3140", SR.Format(SR.D3140_MalformedUrlPassedToFunc, funcName, str), 0);
            }
        }
    }

    private static bool IsHexDigit(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private static ExpressionEvaluator CompileRandom(ExpressionEvaluator[] args)
    {
        return static (in JsonElement input, Environment env) =>
        {
            return Sequence.FromDouble(ThreadRandom.NextDouble(), env.Workspace);
        };
    }

    private static ExpressionEvaluator CompileFormatNumber(ExpressionEvaluator[] args)
    {
        if (args.Length < 2 || args.Length > 3)
        {
            throw new JsonataException("T0410", SR.T0410_FormatnumberExpects2Or3Arguments, 0);
        }

        var numArg = args[0];
        var picArg = args[1];
        var optArg = args.Length >= 3 ? args[2] : null;
        return (in JsonElement input, Environment env) =>
        {
            var numSeq = numArg(input, env);
            var picSeq = picArg(input, env);
            if (numSeq.IsUndefined || picSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            if (!FunctionalCompiler.TryCoerceToNumber(numSeq.FirstOrDefault, out double num))
            {
                return Sequence.Undefined;
            }

            string picture = FunctionalCompiler.CoerceElementToString(picSeq.FirstOrDefault);

            JsonElement options = default;
            if (optArg is not null)
            {
                var optSeq = optArg(input, env);
                if (!optSeq.IsUndefined)
                {
                    options = optSeq.FirstOrDefault;
                }
            }

            return new Sequence(JsonataHelpers.StringFromString(
                FormatNumberXPath(num, picture, options), env.Workspace));
        };
    }

    internal static string FormatNumberXPath(double value, string picture, JsonElement options)
    {
        // Build formatting properties (defaults overridden by options).
        char decSep = '.';
        char grpSep = ',';
        char expSep = 'e';
        string minusSign = "-";
        string pct = "%";
        string perMille = "\u2030";
        char zeroDigit = '0';
        char optDigit = '#';
        char patSep = ';';

        if (options.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in options.EnumerateObject())
            {
                string v = prop.Value.GetString() ?? string.Empty;
                switch (prop.Name)
                {
                    case "decimal-separator": if (v.Length > 0) decSep = v[0]; break;
                    case "grouping-separator": if (v.Length > 0) grpSep = v[0]; break;
                    case "exponent-separator": if (v.Length > 0) expSep = v[0]; break;
                    case "minus-sign": if (v.Length > 0) minusSign = v; break;
                    case "percent": pct = v; break;
                    case "per-mille": perMille = v; break;
                    case "zero-digit": if (v.Length > 0) zeroDigit = v[0]; break;
                    case "digit": if (v.Length > 0) optDigit = v[0]; break;
                    case "pattern-separator": if (v.Length > 0) patSep = v[0]; break;
                }
            }
        }

        // Digit family: 10 consecutive code points starting at zeroDigit.
        bool IsInDigitFamily(char c) => c >= zeroDigit && c < (char)(zeroDigit + 10);
        bool IsActiveNotExp(char c) => (IsInDigitFamily(c) || c == optDigit || c == decSep || c == grpSep) && c != expSep;
        bool IsActive(char c) => IsInDigitFamily(c) || c == optDigit || c == decSep || c == grpSep || c == expSep;
        bool IsDigitOrOpt(char c) => IsInDigitFamily(c) || c == optDigit;
        char DigitFamilyChar(int n) => (char)(zeroDigit + n);

        // Split on pattern separator.
        string[] subPics = picture.Split(patSep);
        if (subPics.Length > 2)
        {
            throw new JsonataException("D3080", SR.D3080_ThePictureStringMustNotContainMoreThanOnePatternSeparator, 0);
        }

        // Parse each sub-picture.
        int numPics = subPics.Length;
        string[] prefixes = new string[numPics];
        string[] suffixes = new string[numPics];
        string[] activeParts = new string[numPics];
        string[] mantissaParts = new string[numPics];
        string?[] exponentParts = new string?[numPics];
        string[] integerParts = new string[numPics];
        string[] fractionalParts = new string[numPics];

        for (int p = 0; p < numPics; p++)
        {
            string sub = subPics[p];

            // Find prefix: everything before first active char (excluding expSep).
            int prefixEnd = sub.Length;
            for (int i = 0; i < sub.Length; i++)
            {
                if (IsActiveNotExp(sub[i]))
                {
                    prefixEnd = i;
                    break;
                }
            }

            // Find suffix: everything after last active char (excluding expSep).
            int suffixStart = 0;
            for (int i = sub.Length - 1; i >= 0; i--)
            {
                if (IsActiveNotExp(sub[i]))
                {
                    suffixStart = i + 1;
                    break;
                }
            }

            string pfx = sub.Substring(0, prefixEnd);
            string sfx = sub.Substring(suffixStart);
            string active = (prefixEnd <= suffixStart)
                ? sub.Substring(prefixEnd, suffixStart - prefixEnd)
                : string.Empty;

            // Find exponent separator in subpicture (starting from prefix end).
            int expPos = sub.IndexOf(expSep, prefixEnd);
            string mant;
            string? expo;
            if (expPos == -1 || expPos >= suffixStart)
            {
                mant = active;
                expo = null;
            }
            else
            {
                int expPosInActive = expPos - prefixEnd;
                mant = active.Substring(0, expPosInActive);
                expo = active.Substring(expPosInActive + 1);
            }

            // Split mantissa on decimal separator.
            int decIdx = mant.IndexOf(decSep);
            string intPart;
            string fracPart;
            if (decIdx == -1)
            {
                intPart = mant;
                fracPart = sfx;
            }
            else
            {
                intPart = mant.Substring(0, decIdx);
                fracPart = mant.Substring(decIdx + 1);
            }

            prefixes[p] = pfx;
            suffixes[p] = sfx;
            activeParts[p] = active;
            mantissaParts[p] = mant;
            exponentParts[p] = expo;
            integerParts[p] = intPart;
            fractionalParts[p] = fracPart;
        }

        // Validate each sub-picture (F&O 4.7.3).
        for (int p = 0; p < numPics; p++)
        {
            string sub = subPics[p];

            if (sub.IndexOf(decSep) != sub.LastIndexOf(decSep))
            {
                throw new JsonataException("D3081", SR.D3081_ThePictureStringMustNotContainMoreThanOneDecimalSeparator, 0);
            }

            if (pct.Length > 0)
            {
                int firstPct = sub.IndexOf(pct, StringComparison.Ordinal);
                if (firstPct != -1 && sub.IndexOf(pct, firstPct + pct.Length, StringComparison.Ordinal) != -1)
                {
                    throw new JsonataException("D3082", SR.D3082_ThePictureStringMustNotContainMoreThanOnePercentCharacter, 0);
                }
            }

            if (perMille.Length > 0)
            {
                int firstPm = sub.IndexOf(perMille, StringComparison.Ordinal);
                if (firstPm != -1 && sub.IndexOf(perMille, firstPm + perMille.Length, StringComparison.Ordinal) != -1)
                {
                    throw new JsonataException("D3083", SR.D3083_ThePictureStringMustNotContainMoreThanOnePerMilleCharacter, 0);
                }
            }

            if (pct.Length > 0 && perMille.Length > 0 &&
                sub.IndexOf(pct, StringComparison.Ordinal) != -1 &&
                sub.IndexOf(perMille, StringComparison.Ordinal) != -1)
            {
                throw new JsonataException("D3084", SR.D3084_PictureStringBothPercentAndPerMille, 0);
            }

            bool hasDigit = false;
            for (int i = 0; i < mantissaParts[p].Length; i++)
            {
                if (IsInDigitFamily(mantissaParts[p][i]) || mantissaParts[p][i] == optDigit)
                {
                    hasDigit = true;
                    break;
                }
            }

            if (!hasDigit)
            {
                throw new JsonataException("D3085", SR.D3085_MantissaMustContainDigit, 0);
            }

            for (int i = 0; i < activeParts[p].Length; i++)
            {
                if (!IsActive(activeParts[p][i]))
                {
                    throw new JsonataException("D3086", SR.D3086_PassiveCharacterBetweenActive, 0);
                }
            }

            int decPosInSub = sub.IndexOf(decSep);
            if (decPosInSub != -1)
            {
                if ((decPosInSub > 0 && sub[decPosInSub - 1] == grpSep) ||
                    (decPosInSub < sub.Length - 1 && sub[decPosInSub + 1] == grpSep))
                {
                    throw new JsonataException("D3087", SR.D3087_GroupingSeparatorAdjacentToDecimal, 0);
                }
            }
            else
            {
                if (integerParts[p].Length > 0 && integerParts[p][integerParts[p].Length - 1] == grpSep)
                {
                    throw new JsonataException("D3088", SR.D3088_IntegerPartEndsWithGroupingSeparator, 0);
                }
            }

            string doubleGrp = new string(grpSep, 2);
            if (sub.IndexOf(doubleGrp, StringComparison.Ordinal) != -1)
            {
                throw new JsonataException("D3089", SR.D3089_ThePictureStringMustNotContainAdjacentGroupingSeparators, 0);
            }

            int optPos = integerParts[p].IndexOf(optDigit);
            if (optPos != -1)
            {
                for (int i = 0; i < optPos; i++)
                {
                    if (IsInDigitFamily(integerParts[p][i]))
                    {
                        throw new JsonataException("D3090", SR.D3090_MandatoryDigitBeforeOptional, 0);
                    }
                }
            }

            int lastOptPos = fractionalParts[p].LastIndexOf(optDigit);
            if (lastOptPos != -1)
            {
                for (int i = lastOptPos; i < fractionalParts[p].Length; i++)
                {
                    if (IsInDigitFamily(fractionalParts[p][i]))
                    {
                        throw new JsonataException("D3091", SR.D3091_MandatoryDigitAfterOptional, 0);
                    }
                }
            }

            bool expExists = exponentParts[p] is not null;
            if (expExists && exponentParts[p]!.Length > 0 &&
                (sub.IndexOf(pct, StringComparison.Ordinal) != -1 ||
                 sub.IndexOf(perMille, StringComparison.Ordinal) != -1))
            {
                throw new JsonataException("D3092", SR.D3092_ExponentWithPercentOrPerMille, 0);
            }

            if (expExists)
            {
                if (exponentParts[p]!.Length == 0)
                {
                    throw new JsonataException("D3093", SR.D3093_TheExponentPartOfThePictureStringMustContainAtLeastOneDigit, 0);
                }

                for (int i = 0; i < exponentParts[p]!.Length; i++)
                {
                    if (!IsInDigitFamily(exponentParts[p]![i]))
                    {
                        throw new JsonataException("D3093", SR.D3093_TheExponentPartOfThePictureStringMustContainOnlyDigitCharact, 0);
                    }
                }
            }
        }

        // Analyse each sub-picture (F&O 4.7.4).
        List<int> GetGroupingPositions(string part, bool toLeft, string searchPart)
        {
            var positions = new List<int>();
            int grpPos = part.IndexOf(grpSep);
            while (grpPos != -1)
            {
                string segment = toLeft ? part.Substring(0, grpPos) : part.Substring(grpPos);
                int count = 0;
                for (int i = 0; i < segment.Length; i++)
                {
                    if (IsDigitOrOpt(segment[i]))
                    {
                        count++;
                    }
                }

                positions.Add(count);

                // Reference impl searches integerPart for subsequent positions.
                grpPos = searchPart.IndexOf(grpSep, grpPos + 1);
            }

            return positions;
        }

        static int ComputeRegularGrouping(List<int> positions)
        {
            if (positions.Count == 0)
            {
                return 0;
            }

            static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
            int factor = positions[0];
            for (int i = 1; i < positions.Count; i++)
            {
                factor = Gcd(factor, positions[i]);
            }

            for (int idx = 1; idx <= positions.Count; idx++)
            {
                if (!positions.Contains(idx * factor))
                {
                    return 0;
                }
            }

            return factor;
        }

        var intGrpPositions = new List<int>[numPics];
        var fracGrpPositions = new List<int>[numPics];
        int[] regularGroupings = new int[numPics];
        int[] minIntSizes = new int[numPics];
        int[] scalingFactors = new int[numPics];
        int[] minFracSizes = new int[numPics];
        int[] maxFracSizes = new int[numPics];
        int[] minExpSizes = new int[numPics];

        for (int p = 0; p < numPics; p++)
        {
            intGrpPositions[p] = GetGroupingPositions(integerParts[p], false, integerParts[p]);
            regularGroupings[p] = ComputeRegularGrouping(intGrpPositions[p]);
            fracGrpPositions[p] = GetGroupingPositions(fractionalParts[p], true, integerParts[p]);

            int minInt = 0;
            for (int i = 0; i < integerParts[p].Length; i++)
            {
                if (IsInDigitFamily(integerParts[p][i]))
                {
                    minInt++;
                }
            }

            int sf = minInt;
            int minFrac = 0;
            int maxFrac = 0;
            for (int i = 0; i < fractionalParts[p].Length; i++)
            {
                if (IsInDigitFamily(fractionalParts[p][i]))
                {
                    minFrac++;
                }

                if (IsDigitOrOpt(fractionalParts[p][i]))
                {
                    maxFrac++;
                }
            }

            bool expPresent = exponentParts[p] is not null;

            if (minInt == 0 && maxFrac == 0)
            {
                if (expPresent)
                {
                    minFrac = 1;
                    maxFrac = 1;
                }
                else
                {
                    minInt = 1;
                }
            }

            if (expPresent && minInt == 0 && integerParts[p].IndexOf(optDigit) != -1)
            {
                minInt = 1;
            }

            if (minInt == 0 && minFrac == 0)
            {
                minFrac = 1;
            }

            int minExp = 0;
            if (expPresent)
            {
                for (int i = 0; i < exponentParts[p]!.Length; i++)
                {
                    if (IsInDigitFamily(exponentParts[p]![i]))
                    {
                        minExp++;
                    }
                }
            }

            minIntSizes[p] = minInt;
            scalingFactors[p] = sf;
            minFracSizes[p] = minFrac;
            maxFracSizes[p] = maxFrac;
            minExpSizes[p] = minExp;
        }

        // If only one sub-picture, create a negative variant with minus prefix.
        string negPrefix;
        string negSuffix;
        string negPicture;
        List<int> negIntGrpPos;
        List<int> negFracGrpPos;
        int negRegGrp;
        int negMinInt;
        int negScaleFactor;
        int negMinFrac;
        int negMaxFrac;
        int negMinExp;

        if (numPics == 1)
        {
            negPrefix = minusSign + prefixes[0];
            negSuffix = suffixes[0];
            negPicture = subPics[0];
            negIntGrpPos = intGrpPositions[0];
            negFracGrpPos = fracGrpPositions[0];
            negRegGrp = regularGroupings[0];
            negMinInt = minIntSizes[0];
            negScaleFactor = scalingFactors[0];
            negMinFrac = minFracSizes[0];
            negMaxFrac = maxFracSizes[0];
            negMinExp = minExpSizes[0];
        }
        else
        {
            negPrefix = prefixes[1];
            negSuffix = suffixes[1];
            negPicture = subPics[1];
            negIntGrpPos = intGrpPositions[1];
            negFracGrpPos = fracGrpPositions[1];
            negRegGrp = regularGroupings[1];
            negMinInt = minIntSizes[1];
            negScaleFactor = scalingFactors[1];
            negMinFrac = minFracSizes[1];
            negMaxFrac = maxFracSizes[1];
            negMinExp = minExpSizes[1];
        }

        // Select sub-picture based on sign.
        string picPrefix;
        string picSuffix;
        string picPicture;
        List<int> picIntGrpPos;
        List<int> picFracGrpPos;
        int picRegGrp;
        int picMinInt;
        int picScaleFactor;
        int picMinFrac;
        int picMaxFrac;
        int picMinExp;

        if (value >= 0)
        {
            picPrefix = prefixes[0];
            picSuffix = suffixes[0];
            picPicture = subPics[0];
            picIntGrpPos = intGrpPositions[0];
            picFracGrpPos = fracGrpPositions[0];
            picRegGrp = regularGroupings[0];
            picMinInt = minIntSizes[0];
            picScaleFactor = scalingFactors[0];
            picMinFrac = minFracSizes[0];
            picMaxFrac = maxFracSizes[0];
            picMinExp = minExpSizes[0];
        }
        else
        {
            picPrefix = negPrefix;
            picSuffix = negSuffix;
            picPicture = negPicture;
            picIntGrpPos = negIntGrpPos;
            picFracGrpPos = negFracGrpPos;
            picRegGrp = negRegGrp;
            picMinInt = negMinInt;
            picScaleFactor = negScaleFactor;
            picMinFrac = negMinFrac;
            picMaxFrac = negMaxFrac;
            picMinExp = negMinExp;
        }

        // Apply percent/per-mille scaling.
        double adjustedNumber;
        if (pct.Length > 0 && picPicture.IndexOf(pct, StringComparison.Ordinal) != -1)
        {
            adjustedNumber = value * 100;
        }
        else if (perMille.Length > 0 && picPicture.IndexOf(perMille, StringComparison.Ordinal) != -1)
        {
            adjustedNumber = value * 1000;
        }
        else
        {
            adjustedNumber = value;
        }

        // Handle exponent normalization.
        double fmtMantissa;
        int? exponentValue = null;
        if (picMinExp == 0)
        {
            fmtMantissa = adjustedNumber;
        }
        else
        {
            double maxMantissa = Math.Pow(10, picScaleFactor);
            double minMantissa = Math.Pow(10, picScaleFactor - 1);
            fmtMantissa = adjustedNumber;
            int exp = 0;
            double absMant = Math.Abs(fmtMantissa);

            if (absMant != 0)
            {
                while (absMant < minMantissa)
                {
                    fmtMantissa *= 10;
                    absMant *= 10;
                    exp--;
                }

                while (absMant > maxMantissa)
                {
                    fmtMantissa /= 10;
                    absMant /= 10;
                    exp++;
                }
            }

            exponentValue = exp;
        }

        // Convert mantissa to string with custom digit family.
        string MakeString(double val, int dp)
        {
            string str = Math.Abs(val).ToString("F" + dp, CultureInfo.InvariantCulture);
            if (zeroDigit != '0')
            {
                var sb = new StringBuilder(str.Length);
                for (int i = 0; i < str.Length; i++)
                {
                    char c = str[i];
                    if (c >= '0' && c <= '9')
                    {
                        sb.Append(DigitFamilyChar(c - '0'));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }

                str = sb.ToString();
            }

            return str;
        }

        string stringValue = MakeString(fmtMantissa, picMaxFrac);

        // Replace the '.' from ToString("F...") with custom decimal separator.
        int dotPos = stringValue.IndexOf('.');
        if (dotPos == -1)
        {
            stringValue += decSep;
        }
        else
        {
            stringValue = stringValue.Substring(0, dotPos) + decSep + stringValue.Substring(dotPos + 1);
        }

        // Strip leading zero-digits.
        while (stringValue.Length > 0 && stringValue[0] == zeroDigit)
        {
            stringValue = stringValue.Substring(1);
        }

        // Strip trailing zero-digits.
        while (stringValue.Length > 0 && stringValue[stringValue.Length - 1] == zeroDigit)
        {
            stringValue = stringValue.Substring(0, stringValue.Length - 1);
        }

        // Pad to minimum sizes.
        int decimalPos = stringValue.IndexOf(decSep);
        int padLeft = picMinInt - decimalPos;
        int padRight = picMinFrac - (stringValue.Length - decimalPos - 1);

        if (padLeft > 0)
        {
            stringValue = new string(zeroDigit, padLeft) + stringValue;
        }

        if (padRight > 0)
        {
            stringValue += new string(zeroDigit, padRight);
        }

        // Insert integer grouping separators.
        decimalPos = stringValue.IndexOf(decSep);
        if (picRegGrp > 0)
        {
            int groupCount = (decimalPos - 1) / picRegGrp;
            for (int group = 1; group <= groupCount; group++)
            {
                int insertPos = decimalPos - (group * picRegGrp);
                stringValue = stringValue.Substring(0, insertPos) + grpSep + stringValue.Substring(insertPos);
            }
        }
        else
        {
            foreach (int pos in picIntGrpPos)
            {
                int insertPos = decimalPos - pos;
                stringValue = stringValue.Substring(0, insertPos) + grpSep + stringValue.Substring(insertPos);
                decimalPos++;
            }
        }

        // Insert fractional grouping separators.
        decimalPos = stringValue.IndexOf(decSep);
        foreach (int pos in picFracGrpPos)
        {
            int insertPos = pos + decimalPos + 1;
            stringValue = stringValue.Substring(0, insertPos) + grpSep + stringValue.Substring(insertPos);
        }

        // Remove decimal separator if not in picture or at end of string.
        decimalPos = stringValue.IndexOf(decSep);
        if (picPicture.IndexOf(decSep) == -1 || decimalPos == stringValue.Length - 1)
        {
            stringValue = stringValue.Substring(0, stringValue.Length - 1);
        }

        // Format exponent.
        if (exponentValue is not null)
        {
            string stringExponent = MakeString(exponentValue.Value, 0);
            int expPadLeft = picMinExp - stringExponent.Length;
            if (expPadLeft > 0)
            {
                stringExponent = new string(zeroDigit, expPadLeft) + stringExponent;
            }

            stringValue = stringValue + expSep + (exponentValue.Value < 0 ? minusSign : "") + stringExponent;
        }

        // Add prefix and suffix.
        return picPrefix + stringValue + picSuffix;
    }

    private static ExpressionEvaluator CompileFormatBase(ExpressionEvaluator[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            throw new JsonataException("T0410", SR.T0410_FormatbaseExpects1Or2Arguments, 0);
        }

        var numArg = args[0];
        var radixArg = args.Length >= 2 ? args[1] : null;
        return (in JsonElement input, Environment env) =>
        {
            var numSeq = numArg(input, env);
            if (numSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            if (!FunctionalCompiler.TryCoerceToNumber(numSeq.FirstOrDefault, out double num))
            {
                return Sequence.Undefined;
            }

            int radixInt = 10;
            if (radixArg is not null)
            {
                var radixSeq = radixArg(input, env);
                if (radixSeq.IsUndefined)
                {
                    return Sequence.Undefined;
                }

                if (!FunctionalCompiler.TryCoerceToNumber(radixSeq.FirstOrDefault, out double radix))
                {
                    return Sequence.Undefined;
                }

                radixInt = (int)Math.Truncate(radix);
            }

            if (radixInt < 2 || radixInt > 36)
            {
                throw new JsonataException("D3100", SR.Format(SR.D3100_FormatBaseRadixOutOfRange, radixInt), 0);
            }

            long numLong = (long)Math.Round(num, MidpointRounding.ToEven);
            bool negative = numLong < 0;
            string result = ConvertToBase(Math.Abs(numLong), radixInt);
            if (negative)
            {
                result = "-" + result;
            }

            return new Sequence(JsonataHelpers.StringFromString(result, env.Workspace));
        };
    }

    internal static string ConvertToBase(long value, int radix)
    {
        if (radix is 2 or 8 or 10 or 16)
        {
            return Convert.ToString(value, radix);
        }

        if (value == 0)
        {
            return "0";
        }

        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        var chars = new char[64];
        int pos = chars.Length;

        long remaining = value;
        while (remaining > 0)
        {
            chars[--pos] = digits[(int)(remaining % radix)];
            remaining /= radix;
        }

        return new string(chars, pos, chars.Length - pos);
    }

    // --- Array functions: shuffle, zip ---
    private static ExpressionEvaluator CompileShuffle(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", SR.T0410_ShuffleExpects1Argument, 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            var elements = default(SequenceBuilder);

            if (seq.IsSingleton && seq.FirstOrDefault.ValueKind == JsonValueKind.Array)
            {
                // Singleton array element — extract items into builder
                var arr = seq.FirstOrDefault;
                foreach (var item in arr.EnumerateArray())
                {
                    elements.Add(item);
                }
            }
            else if (seq.Count > 1)
            {
                // Multi-element Sequence — treat the elements as the array to shuffle
                for (int i = 0; i < seq.Count; i++)
                {
                    elements.Add(seq[i]);
                }

                seq.ReturnBackingArray();
            }
            else
            {
                // Singleton non-array — return as-is
                return seq;
            }

            if (elements.Count == 0)
            {
                elements.ReturnArray();
                return Sequence.Undefined;
            }

            // Fisher-Yates shuffle
            for (int i = elements.Count - 1; i > 0; i--)
            {
                int j = ThreadRandom.Next(i + 1);
                (elements[i], elements[j]) = (elements[j], elements[i]);
            }

            JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(env.Workspace, elements.Count);
            JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
            for (int k = 0; k < elements.Count; k++)
            {
                arrayRoot.AddItem(elements[k]);
            }

            elements.ReturnArray();
            return new Sequence((JsonElement)arrayRoot);
        };
    }

    private static ExpressionEvaluator CompileZip(ExpressionEvaluator[] args)
    {
        if (args.Length == 0)
        {
            return static (in JsonElement input, Environment env) => Sequence.Undefined;
        }

        if (args.Length == 2)
        {
            return CompileZip2(args[0], args[1]);
        }

        return CompileZipN(args);
    }

    private static ExpressionEvaluator CompileZip2(ExpressionEvaluator arg0Eval, ExpressionEvaluator arg1Eval)
    {
        return (in JsonElement input, Environment env) =>
        {
            var seq0 = arg0Eval(input, env);
            var seq1 = arg1Eval(input, env);

            int len0 = GetZipArgLength(in seq0);
            int len1 = GetZipArgLength(in seq1);
            int minLen = Math.Min(len0, len1);

            if (minLen == 0)
            {
                seq0.ReturnBackingArray();
                seq1.ReturnBackingArray();
                var emptyDoc = JsonElement.CreateArrayBuilder(env.Workspace, 0);
                return new Sequence((JsonElement)emptyDoc.RootElement);
            }

            var doc = JsonElement.CreateBuilder(
                env.Workspace,
                (seq0, seq1, minLen),
                static (in (Sequence S0, Sequence S1, int MinLen) ctx, ref JsonElement.ArrayBuilder outer) =>
                {
                    for (int i = 0; i < ctx.MinLen; i++)
                    {
                        outer.AddItem(
                            (ctx.S0, ctx.S1, i),
                            static (in (Sequence S0, Sequence S1, int I) ictx, ref JsonElement.ArrayBuilder inner) =>
                            {
                                inner.AddItem(GetZipElement(in ictx.S0, ictx.I));
                                inner.AddItem(GetZipElement(in ictx.S1, ictx.I));
                            });
                    }
                },
                estimatedMemberCount: (minLen * 3) + 2);

            // Return rented backing arrays to the pool now that the builder has
            // consumed all elements. The CreateBuilder callback executes synchronously,
            // so the arrays are no longer referenced by the time we reach this point.
            seq0.ReturnBackingArray();
            seq1.ReturnBackingArray();

            return new Sequence((JsonElement)doc.RootElement);
        };
    }

    private static ExpressionEvaluator CompileZipN(ExpressionEvaluator[] args)
    {
        return (in JsonElement input, Environment env) =>
        {
            // Evaluate args and keep the Sequences — no List copying.
            var evaluated = new (Sequence Seq, int Length)[args.Length];
            int minLen = int.MaxValue;

            for (int a = 0; a < args.Length; a++)
            {
                evaluated[a].Seq = args[a](input, env);
                evaluated[a].Length = GetZipArgLength(in evaluated[a].Seq);

                if (evaluated[a].Length < minLen)
                {
                    minLen = evaluated[a].Length;
                }
            }

            if (minLen == 0 || minLen == int.MaxValue)
            {
                for (int a = 0; a < evaluated.Length; a++)
                {
                    evaluated[a].Seq.ReturnBackingArray();
                }

                var emptyDoc = JsonElement.CreateArrayBuilder(env.Workspace, 0);
                return new Sequence((JsonElement)emptyDoc.RootElement);
            }

            var doc = JsonElement.CreateBuilder(
                env.Workspace,
                (evaluated, minLen),
                static (in ((Sequence Seq, int Length)[] Evaluated, int MinLen) ctx, ref JsonElement.ArrayBuilder outer) =>
                {
                    for (int i = 0; i < ctx.MinLen; i++)
                    {
                        outer.AddItem(
                            (ctx.Evaluated, i),
                            static (in ((Sequence Seq, int Length)[] Evaluated, int I) ictx, ref JsonElement.ArrayBuilder inner) =>
                            {
                                for (int a = 0; a < ictx.Evaluated.Length; a++)
                                {
                                    inner.AddItem(GetZipElement(in ictx.Evaluated[a].Seq, ictx.I));
                                }
                            });
                    }
                },
                estimatedMemberCount: (minLen * (evaluated.Length + 1)) + 2);

            for (int a = 0; a < evaluated.Length; a++)
            {
                evaluated[a].Seq.ReturnBackingArray();
            }

            return new Sequence((JsonElement)doc.RootElement);
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetZipArgLength(in Sequence seq)
    {
        if (seq.IsUndefined)
        {
            return 0;
        }

        if (seq.IsSingleton && seq.FirstOrDefault.ValueKind == JsonValueKind.Array)
        {
            return seq.FirstOrDefault.GetArrayLength();
        }

        // Multi-element sequence or scalar — treat elements as individual array entries
        return seq.Count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static JsonElement GetZipElement(in Sequence seq, int i)
    {
        if (seq.IsSingleton && seq.FirstOrDefault.ValueKind == JsonValueKind.Array)
        {
            return seq.FirstOrDefault[i];
        }

        return seq[i];
    }

    // --- Misc functions: error, assert, now, millis, fromMillis, toMillis, eval ---
    private static ExpressionEvaluator CompileError(ExpressionEvaluator[] args)
    {
        var msgArg = args.Length > 0 ? args[0] : null;
        return (in JsonElement input, Environment env) =>
        {
            string message = "An error occurred";
            if (msgArg is not null)
            {
                var msgSeq = msgArg(input, env);
                if (!msgSeq.IsUndefined)
                {
                    var el = msgSeq.FirstOrDefault;
                    if (el.ValueKind != JsonValueKind.String)
                    {
                        throw new JsonataException("T0410", SR.T0410_ErrorExpectsAStringArgument, 0);
                    }

                    message = el.GetString() ?? message;
                }
            }

            throw new JsonataException("D3137", message, 0);
        };
    }

    private static ExpressionEvaluator CompileAssert(ExpressionEvaluator[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            throw new JsonataException("T0410", SR.T0410_AssertExpects12Arguments, 0);
        }

        var condArg = args[0];
        var msgArg = args.Length > 1 ? args[1] : null;
        return (in JsonElement input, Environment env) =>
        {
            var cond = condArg(input, env);

            // $assert requires a boolean argument
            if (cond.IsUndefined || cond.FirstOrDefault.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new JsonataException("T0410", SR.T0410_Argument1OfFunctionAssertMustBeABoolean, 0);
            }

            if (cond.FirstOrDefault.ValueKind == JsonValueKind.False)
            {
                string message = "Assertion failed";
                if (msgArg is not null)
                {
                    var msgSeq = msgArg(input, env);
                    if (!msgSeq.IsUndefined)
                    {
                        message = FunctionalCompiler.CoerceElementToString(msgSeq.FirstOrDefault);
                    }
                }

                throw new JsonataException("D3141", message, 0);
            }

            return Sequence.Undefined;
        };
    }

    private static ExpressionEvaluator CompileNow(ExpressionEvaluator[] args)
    {
        return static (in JsonElement input, Environment env) =>
        {
            return new Sequence(JsonataHelpers.StringFromString(
                DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture), env.Workspace));
        };
    }

    private static ExpressionEvaluator CompileMillis(ExpressionEvaluator[] args)
    {
        return static (in JsonElement input, Environment env) =>
        {
            return Sequence.FromDouble(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), env.Workspace);
        };
    }

    private static ExpressionEvaluator CompileFromMillis(ExpressionEvaluator[] args)
    {
        var msArg = args.Length > 0 ? args[0] : null;
        var pictureArg = args.Length > 1 ? args[1] : null;
        var tzArg = args.Length > 2 ? args[2] : null;

        return (in JsonElement input, Environment env) =>
        {
            Sequence msSeq;
            if (msArg is not null)
            {
                msSeq = msArg(input, env);
            }
            else
            {
                // 0-arg form: use context as the milliseconds value
                msSeq = new Sequence(input);
            }

            if (msSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            if (!FunctionalCompiler.TryCoerceToNumber(msSeq.FirstOrDefault, out double millisVal))
            {
                return Sequence.Undefined;
            }

            var dt = DateTimeOffset.FromUnixTimeMilliseconds((long)millisVal);

            // Apply timezone offset if provided
            TimeSpan offset = TimeSpan.Zero;
            bool hasTz = false;
            if (tzArg != null)
            {
                var tzSeq = tzArg(input, env);
                if (!tzSeq.IsUndefined)
                {
                    string tzStr = FunctionalCompiler.CoerceElementToString(tzSeq.FirstOrDefault);
                    offset = XPathDateTimeFormatter.ParseTimezoneArgument(tzStr);
                    hasTz = true;
                    dt = dt.ToOffset(offset);
                }
            }

            // If no picture, use ISO 8601 default
            if (pictureArg == null)
            {
                if (hasTz)
                {
                    return new Sequence(JsonataHelpers.StringFromString(
                        FormatIso8601WithOffset(dt, offset), env.Workspace));
                }

                return new Sequence(JsonataHelpers.StringFromString(
                    dt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture), env.Workspace));
            }

            var picSeq = pictureArg(input, env);
            if (picSeq.IsUndefined)
            {
                // Undefined picture -> use ISO 8601 default with timezone
                if (hasTz)
                {
                    return new Sequence(JsonataHelpers.StringFromString(
                        FormatIso8601WithOffset(dt, offset), env.Workspace));
                }

                return new Sequence(JsonataHelpers.StringFromString(
                    dt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture), env.Workspace));
            }

            string picture = FunctionalCompiler.CoerceElementToString(picSeq.FirstOrDefault);
            string result = XPathDateTimeFormatter.FormatDateTime(dt, picture);
            return new Sequence(JsonataHelpers.StringFromString(result, env.Workspace));
        };
    }

    internal static string FormatIso8601WithOffset(DateTimeOffset dt, TimeSpan offset)
    {
        string formatted = dt.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);
        if (offset == TimeSpan.Zero)
        {
            return formatted + "Z";
        }

        string sign = offset >= TimeSpan.Zero ? "+" : "-";
        int totalMin = (int)Math.Abs(offset.TotalMinutes);
        int h = totalMin / 60;
        int m = totalMin % 60;
        return formatted + sign + h.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') + ":" + m.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');
    }

    /// <summary>
    /// Parse a string as strict ISO 8601 date/time or throw D3110 for invalid format.
    /// </summary>
    internal static Sequence ParseIso8601ToMillis(string str, JsonWorkspace workspace)
    {
        if (DateTimeOffset.TryParseExact(
            str,
            Iso8601Formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var dt))
        {
            return Sequence.FromDouble(dt.ToUnixTimeMilliseconds(), workspace);
        }

        throw new JsonataException("D3110", SR.Format(SR.D3110_StringCannotBeParsedAsTimestamp, str), 0);
    }

    private static ExpressionEvaluator CompileToMillis(ExpressionEvaluator[] args)
    {
        if (args.Length < 1)
        {
            throw new JsonataException("T0410", SR.T0410_TomillisExpectsAtLeast1Argument, 0);
        }

        var strArg = args[0];
        var pictureArg = args.Length > 1 ? args[1] : null;

        return (in JsonElement input, Environment env) =>
        {
            var strSeq = strArg(input, env);
            if (strSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            string str = FunctionalCompiler.CoerceElementToString(strSeq.FirstOrDefault);

            if (pictureArg == null)
            {
                return ParseIso8601ToMillis(str, env.Workspace);
            }

            var picSeq = pictureArg(input, env);
            if (picSeq.IsUndefined)
            {
                return ParseIso8601ToMillis(str, env.Workspace);
            }

            string picture = FunctionalCompiler.CoerceElementToString(picSeq.FirstOrDefault);

            try
            {
                if (XPathDateTimeFormatter.TryParseDateTime(str, picture, out long millis))
                {
                    return Sequence.FromDouble(millis, env.Workspace);
                }
            }
            catch (JsonataException)
            {
                throw;
            }

            return Sequence.Undefined;
        };
    }

    private static ExpressionEvaluator CompileFormatInteger(ExpressionEvaluator[] args)
    {
        if (args.Length < 2)
        {
            throw new JsonataException("T0410", SR.T0410_FormatintegerExpectsAtLeast2Arguments, 0);
        }

        var numArg = args[0];
        var picArg = args[1];

        return (in JsonElement input, Environment env) =>
        {
            var numSeq = numArg(input, env);
            if (numSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            var numElement = numSeq.FirstOrDefault;

            var picSeq = picArg(input, env);
            string picture = FunctionalCompiler.CoerceElementToString(picSeq.FirstOrDefault);

            string result;

            // Try to parse as long directly from the raw UTF-8 text to avoid
            // precision loss when converting large integers through double.
            if (numElement.ValueKind == JsonValueKind.Number && numElement.TryGetInt64(out long longVal))
            {
                result = XPathDateTimeFormatter.FormatInteger(longVal, picture);
            }
            else if (FunctionalCompiler.TryCoerceToNumber(numElement, out double numVal))
            {
                if (numVal >= long.MinValue && numVal <= long.MaxValue)
                {
                    result = XPathDateTimeFormatter.FormatInteger((long)numVal, picture);
                }
                else
                {
                    result = XPathDateTimeFormatter.FormatInteger(numVal, picture);
                }
            }
            else
            {
                return Sequence.Undefined;
            }

            return new Sequence(JsonataHelpers.StringFromString(result, env.Workspace));
        };
    }

    private static ExpressionEvaluator CompileParseInteger(ExpressionEvaluator[] args)
    {
        if (args.Length < 2)
        {
            throw new JsonataException("T0410", SR.T0410_ParseintegerExpectsAtLeast2Arguments, 0);
        }

        var strArg = args[0];
        var picArg = args[1];

        return (in JsonElement input, Environment env) =>
        {
            var strSeq = strArg(input, env);
            if (strSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            string str = FunctionalCompiler.CoerceElementToString(strSeq.FirstOrDefault);

            var picSeq = picArg(input, env);
            string picture = FunctionalCompiler.CoerceElementToString(picSeq.FirstOrDefault);

            if (XPathDateTimeFormatter.TryParseInteger(str, picture, out double dblValue))
            {
                return Sequence.FromDouble(dblValue, env.Workspace);
            }

            return Sequence.Undefined;
        };
    }

    private static ExpressionEvaluator CompileEval(ExpressionEvaluator[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            throw new JsonataException("T0410", SR.T0410_EvalExpects1Or2Arguments, 0);
        }

        var exprArg = args[0];
        var contextArg = args.Length > 1 ? args[1] : null;
        return (in JsonElement input, Environment env) =>
        {
            var exprSeq = exprArg(input, env);
            if (exprSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            var el = exprSeq.FirstOrDefault;
            if (el.ValueKind != JsonValueKind.String)
            {
                throw new JsonataException("T0410", SR.T0410_EvalExpectsAStringArgument, 0);
            }

            string expr = el.GetString() ?? string.Empty;

            Ast.JsonataNode ast;
            try
            {
                ast = Parser.Parse(expr);
            }
            catch (JsonataException ex)
            {
                throw new JsonataException("D3120", SR.Format(SR.D3120_SyntaxErrorInEval, ex.Message), 0);
            }

            ExpressionEvaluator compiled;
            try
            {
                compiled = FunctionalCompiler.Compile(ast);
            }
            catch (JsonataException ex)
            {
                throw new JsonataException("D3121", SR.Format(SR.D3121_DynamicErrorInEval, ex.Message), 0);
            }

            // Determine input for the compiled expression
            JsonElement evalInput = input;
            if (contextArg is not null)
            {
                var ctxSeq = contextArg(input, env);
                if (ctxSeq.IsSingleton)
                {
                    evalInput = ctxSeq.FirstOrDefault;
                }
                else if (!ctxSeq.IsUndefined)
                {
                    var elements = default(SequenceBuilder);
                    elements.AddRange(ctxSeq);
                    evalInput = JsonataHelpers.ArrayFromBuilder(ref elements, env.Workspace);
                }
            }

            try
            {
                return compiled(evalInput, env);
            }
            catch (JsonataException ex)
            {
                throw new JsonataException("D3121", SR.Format(SR.D3121_DynamicErrorInEval, ex.Message), 0);
            }
        };
    }

    // --- Helpers ---
    private delegate void NumericAccumulator(ref double accumulator, double value);

    /// <summary>
    /// Unwraps a match result array: 0 elements → Undefined, 1 element → singleton
    /// Sequence wrapping the match object, N elements → singleton Sequence wrapping
    /// the array (consistent with all other array-returning built-in functions).
    /// </summary>
    private static Sequence UnwrapMatchArray(JsonElement array)
    {
        int count = array.GetArrayLength();
        if (count == 0)
        {
            return Sequence.Undefined;
        }

        if (count == 1)
        {
            return new Sequence(array[0]);
        }

        return new Sequence(array);
    }

    private static void EnumerateNumericValues(Sequence seq, ref double accumulator, NumericAccumulator action)
    {
        if (seq.IsSingleton)
        {
            var el = seq.FirstOrDefault;
            if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in el.EnumerateArray())
                {
                    ValidateAndAccumulate(item, ref accumulator, action);
                }
            }
            else
            {
                ValidateAndAccumulate(el, ref accumulator, action);
            }
        }
        else
        {
            for (int i = 0; i < seq.Count; i++)
            {
                ValidateAndAccumulate(seq[i], ref accumulator, action);
            }
        }
    }

    private static void ValidateAndAccumulate(JsonElement el, ref double accumulator, NumericAccumulator action)
    {
        if (el.ValueKind == JsonValueKind.Number)
        {
            action(ref accumulator, el.GetDouble());
        }
        else if (el.ValueKind is JsonValueKind.String or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Object or JsonValueKind.Array)
        {
            throw new JsonataException("T0412", SR.T0412_Argument1OfAggregateFunctionMustBeAnArrayOfNumbers, 0);
        }
    }

    private static void AddSequenceToArray(Sequence seq, ref JsonElement.Mutable arrayRoot)
    {
        if (seq.IsUndefined)
        {
            return;
        }

        if (seq.IsSingleton)
        {
            var el = seq.FirstOrDefault;
            if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in el.EnumerateArray())
                {
                    arrayRoot.AddItem(item);
                }
            }
            else
            {
                arrayRoot.AddItem(el);
            }
        }
        else
        {
            for (int i = 0; i < seq.Count; i++)
            {
                arrayRoot.AddItem(seq[i]);
            }
        }
    }

    /// <summary>
    /// Counts Unicode code points in a string (handles surrogate pairs).
    /// </summary>
    private static int CountCodePoints(string str)
    {
        int count = 0;
        for (int i = 0; i < str.Length; i++)
        {
            count++;
            if (char.IsHighSurrogate(str[i]) && i + 1 < str.Length && char.IsLowSurrogate(str[i + 1]))
            {
                i++; // Skip low surrogate
            }
        }

        return count;
    }

#if NET
    private static int CountCodePoints(ReadOnlySpan<char> span)
    {
        int count = 0;
        int i = 0;
        while (i < span.Length)
        {
            Rune.DecodeFromUtf16(span.Slice(i), out _, out int charsConsumed);
            count++;
            i += charsConsumed;
        }

        return count;
    }

    /// <summary>
    /// Finds the char index of a given code point offset in a span.
    /// </summary>
    private static int CodePointToCharIndex(ReadOnlySpan<char> span, int codePointOffset)
    {
        int cpIdx = 0;
        int charIdx = 0;
        while (cpIdx < codePointOffset && charIdx < span.Length)
        {
            Rune.DecodeFromUtf16(span.Slice(charIdx), out _, out int consumed);
            charIdx += consumed;
            cpIdx++;
        }

        return charIdx;
    }
#endif

    /// <summary>
    /// Converts a string to an array of Unicode code points.
    /// </summary>
    private static int[] ToCodePointArray(string str)
    {
        var codePoints = new List<int>(str.Length);
        for (int i = 0; i < str.Length; i++)
        {
            if (char.IsHighSurrogate(str[i]) && i + 1 < str.Length && char.IsLowSurrogate(str[i + 1]))
            {
                codePoints.Add(char.ConvertToUtf32(str[i], str[i + 1]));
                i++;
            }
            else
            {
                codePoints.Add(str[i]);
            }
        }

        return codePoints.ToArray();
    }

    /// <summary>
    /// Converts a slice of code points back to a string.
    /// </summary>
    private static string CodePointsToString(int[] codePoints, int start, int count)
    {
        var sb = new StringBuilder(count * 2);
        for (int i = start; i < start + count && i < codePoints.Length; i++)
        {
            if (codePoints[i] > 0xFFFF)
            {
                sb.Append(char.ConvertFromUtf32(codePoints[i]));
            }
            else
            {
                sb.Append((char)codePoints[i]);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Appends a string element's unquoted UTF-8 content to a join buffer,
    /// preceded by the separator if this is not the first element.
    /// </summary>
    private static void AppendJoinElement(
        JsonElement el, ref byte[] buffer, ref int pos, ref bool first,
        byte[]? sepBuffer, int sepLen)
    {
        if (el.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0412", SR.T0412_Argument1OfFunctionJoinMustBeAnArrayOfStrings, 0);
        }

        if (!first && sepBuffer is not null)
        {
            JsonataHelpers.GrowBufferIfNeeded(ref buffer, pos, sepLen);
            sepBuffer.AsSpan(0, sepLen).CopyTo(buffer.AsSpan(pos));
            pos += sepLen;
        }

        first = false;

        using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(el);
        ReadOnlySpan<byte> span = raw.Span;
        if (span.Length > 2)
        {
            int contentLen = span.Length - 2;
            JsonataHelpers.GrowBufferIfNeeded(ref buffer, pos, contentLen);
            span.Slice(1, contentLen).CopyTo(buffer.AsSpan(pos));
            pos += contentLen;
        }
    }

    /// <summary>
    /// Performs a stable sort on a <see cref="SequenceBuilder"/> using insertion sort.
    /// Insertion sort is inherently stable (preserves relative order of equal elements)
    /// and allocates nothing. O(n²) is acceptable for the small arrays typical in JSONata.
    /// </summary>
    private static void StableSort(ref SequenceBuilder builder, Comparison<JsonElement> comparison)
    {
        int count = builder.Count;
        for (int i = 1; i < count; i++)
        {
            JsonElement key = builder[i];
            int j = i - 1;
            while (j >= 0 && comparison(builder[j], key) > 0)
            {
                builder[j + 1] = builder[j];
                j--;
            }

            builder[j + 1] = key;
        }
    }
}