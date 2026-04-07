// <copyright file="BuiltInFunctions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Registry and implementation of JSONata built-in functions.
/// Phase 2 includes core aggregate and utility functions;
/// Phase 3 will add the full ~60-function library.
/// </summary>
internal static class BuiltInFunctions
{
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
            throw new JsonataException("T0410", "$count expects 1 argument", 0);
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

            return new Sequence(FunctionalCompiler.CreateNumberElement(count));
        };
    }

    private static ExpressionEvaluator CompileSum(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$sum expects 1 argument", 0);
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
            return new Sequence(FunctionalCompiler.CreateNumberElement(total));
        };
    }

    private static ExpressionEvaluator CompileMax(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$max expects 1 argument", 0);
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

            return new Sequence(FunctionalCompiler.CreateNumberElement(max));
        };
    }

    private static ExpressionEvaluator CompileMin(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$min expects 1 argument", 0);
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

            return new Sequence(FunctionalCompiler.CreateNumberElement(min));
        };
    }

    private static ExpressionEvaluator CompileAverage(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$average expects 1 argument", 0);
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

            return new Sequence(FunctionalCompiler.CreateNumberElement(total / count));
        };
    }

    // --- Type coercion functions ---
    private static ExpressionEvaluator CompileToString(ExpressionEvaluator[] args)
    {
        if (args.Length > 2)
        {
            throw new JsonataException("T0410", "$string expects at most 2 arguments", 0);
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
                return new Sequence(FunctionalCompiler.CreateStringElement(string.Empty));
            }

            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
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
                    throw new JsonataException("D3001", "Attempting to invoke string function on Infinity or NaN", 0);
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
                        throw new JsonataException("T0410", "Second argument of $string must be a boolean", 0);
                    }

                    prettyPrint = prettyElem.ValueKind == JsonValueKind.True;
                }
            }

            // For arrays and objects, produce JSON
            if (element.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                string json = StringifyElement(element, prettyPrint);
                return new Sequence(FunctionalCompiler.CreateStringElement(json));
            }

            string result = FunctionalCompiler.CoerceElementToString(element);
            return new Sequence(FunctionalCompiler.CreateStringElement(result));
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
                    // Match JSONata's JSON.stringify replacer: Number(val.toPrecision(15))
                    double rounded = double.Parse(
                        d.ToString("G15", CultureInfo.InvariantCulture),
                        System.Globalization.NumberStyles.Float,
                        CultureInfo.InvariantCulture);
                    writer.WriteNumberValue(rounded);
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
            throw new JsonataException("T0410", "$number expects 0 or 1 arguments", 0);
        }

        // 0 args → use current context
        var arg = args.Length == 1 ? args[0] : ContextArg;
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);

            if (seq.IsLambda)
            {
                throw new JsonataException("T0410", "Argument 1 of function $number is not of the correct type", 0);
            }

            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            var element = seq.FirstOrDefault;

            // Reject types that cannot be converted to number
            if (element.ValueKind is JsonValueKind.Array or JsonValueKind.Object or JsonValueKind.Null)
            {
                throw new JsonataException("T0410", "Argument 1 of function $number is not of the correct type", 0);
            }

            // Booleans: true → 1, false → 0
            if (element.ValueKind == JsonValueKind.True)
            {
                return new Sequence(FunctionalCompiler.CreateNumberElement(1));
            }

            if (element.ValueKind == JsonValueKind.False)
            {
                return new Sequence(FunctionalCompiler.CreateNumberElement(0));
            }

            if (element.ValueKind == JsonValueKind.Number)
            {
                double num = element.GetDouble();
                if (double.IsInfinity(num))
                {
                    throw new JsonataException("D3030", "Unable to cast value to a number", 0);
                }

                return new Sequence(FunctionalCompiler.CreateNumberElement(num));
            }

            // String conversion
            if (element.ValueKind == JsonValueKind.String)
            {
                string? s = element.GetString();
                if (s is null || s.Length == 0)
                {
                    throw new JsonataException("D3030", "Unable to cast value to a number", 0);
                }

                // Handle hex (0x/0X), binary (0b/0B), and octal (0o/0O) prefixes
                if (s.Length > 2 && s[0] == '0')
                {
                    char prefix = s[1];
                    if (prefix is 'x' or 'X')
                    {
                        if (long.TryParse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hexVal))
                        {
                            return new Sequence(FunctionalCompiler.CreateNumberElement(hexVal));
                        }

                        throw new JsonataException("D3030", "Unable to cast value to a number", 0);
                    }

                    if (prefix is 'b' or 'B')
                    {
                        if (TryParseBinary(s, 2, out long binVal))
                        {
                            return new Sequence(FunctionalCompiler.CreateNumberElement(binVal));
                        }

                        throw new JsonataException("D3030", "Unable to cast value to a number", 0);
                    }

                    if (prefix is 'o' or 'O')
                    {
                        if (TryParseOctal(s, 2, out long octVal))
                        {
                            return new Sequence(FunctionalCompiler.CreateNumberElement(octVal));
                        }

                        throw new JsonataException("D3030", "Unable to cast value to a number", 0);
                    }
                }

                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                {
                    if (double.IsInfinity(parsed))
                    {
                        throw new JsonataException("D3030", "Unable to cast value to a number", 0);
                    }

                    return new Sequence(FunctionalCompiler.CreateNumberElement(parsed));
                }

                throw new JsonataException("D3030", "Unable to cast value to a number", 0);
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

    private static ExpressionEvaluator CompileToBoolean(ExpressionEvaluator[] args)
    {
        if (args.Length > 1)
        {
            throw new JsonataException("T0410", "$boolean expects 0 or 1 arguments", 0);
        }

        var arg = args.Length == 1 ? args[0] : ContextArg;
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);

            // Functions/lambdas are not boolean-convertible — return false
            if (seq.IsLambda)
            {
                return new Sequence(FunctionalCompiler.CreateBoolElement(false));
            }

            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            return new Sequence(FunctionalCompiler.CreateBoolElement(FunctionalCompiler.IsTruthy(seq)));
        };
    }

    private static ExpressionEvaluator CompileNot(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$not expects 1 argument", 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            return new Sequence(FunctionalCompiler.CreateBoolElement(!FunctionalCompiler.IsTruthy(seq)));
        };
    }

    private static ExpressionEvaluator CompileExists(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$exists expects 1 argument", 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);

            // Lambda/function references exist
            if (seq.IsLambda)
            {
                return new Sequence(FunctionalCompiler.CreateBoolElement(true));
            }

            return new Sequence(FunctionalCompiler.CreateBoolElement(!seq.IsUndefined));
        };
    }

    private static ExpressionEvaluator CompileType(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$type expects 1 argument", 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);

            if (seq.IsLambda)
            {
                return new Sequence(FunctionalCompiler.CreateStringElement("function"));
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

            return new Sequence(FunctionalCompiler.CreateStringElement(typeName));
        };
    }

    // --- String functions (core subset) ---
    private static ExpressionEvaluator CompileLength(ExpressionEvaluator[] args)
    {
        if (args.Length > 1)
        {
            throw new JsonataException("T0410", "$length expects 0 or 1 arguments", 0);
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
                string str = el.GetString() ?? string.Empty;
                int len = CountCodePoints(str);
                return new Sequence(FunctionalCompiler.CreateNumberElement(len));
            }

            // Wrong type: T0411 when context-derived, T0410 when explicit arg
            throw new JsonataException(
                isContextArg ? "T0411" : "T0410",
                "Argument 1 of function $length is not of the correct type",
                0);
        };
    }

    private static ExpressionEvaluator CompileSubstring(ExpressionEvaluator[] args)
    {
        if (args.Length < 2 || args.Length > 3)
        {
            throw new JsonataException("T0410", "$substring expects 2 or 3 arguments", 0);
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
                throw new JsonataException("T0410", "Argument 1 of function $substring is not a string", 0);
            }

            var startElem = startSeq.FirstOrDefault;
            if (!FunctionalCompiler.TryCoerceToNumber(startElem, out double startD))
            {
                throw new JsonataException("T0410", "Argument 2 of function $substring is not a number", 0);
            }

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
                    throw new JsonataException("T0410", "Argument 3 of function $substring is not a number", 0);
                }

                count = Math.Max(0, (int)lenD);
                count = Math.Min(count, cpLen - start);
            }
            else
            {
                count = cpLen - start;
            }

            string result = CodePointsToString(codePoints, start, count);
            return new Sequence(FunctionalCompiler.CreateStringElement(result));
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
            throw new JsonataException("T0411", "$substringBefore expects 2 arguments", 0);
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
            throw new JsonataException("T0411", "$substringAfter expects 2 arguments", 0);
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
                    "Argument 1 of string function is not a string",
                    0);
            }

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

            return new Sequence(FunctionalCompiler.CreateStringElement(result));
        };
    }

    private static ExpressionEvaluator CompileUppercase(ExpressionEvaluator[] args)
    {
        return CompileStringTransform(args, s => s.ToUpperInvariant());
    }

    private static ExpressionEvaluator CompileLowercase(ExpressionEvaluator[] args)
    {
        return CompileStringTransform(args, s => s.ToLowerInvariant());
    }

    private static ExpressionEvaluator CompileTrim(ExpressionEvaluator[] args)
    {
        // JSONata $trim normalizes all whitespace (including newlines, tabs) to single spaces
        return CompileStringTransform(args, s => System.Text.RegularExpressions.Regex.Replace(s.Trim(), @"\s+", " "));
    }

    private static ExpressionEvaluator CompileStringTransform(ExpressionEvaluator[] args, Func<string, string> transform)
    {
        if (args.Length > 1)
        {
            throw new JsonataException("T0410", "String function expects 0 or 1 arguments", 0);
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
                throw new JsonataException("T0410", "String function argument must be a string", 0);
            }

            string? str = elem.GetString();
            if (str is null)
            {
                return Sequence.Undefined;
            }

            return new Sequence(FunctionalCompiler.CreateStringElement(transform(str)));
        };
    }

    private static ExpressionEvaluator CompileJoin(ExpressionEvaluator[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            throw new JsonataException("T0410", "$join expects 1 or 2 arguments", 0);
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

            string separator = string.Empty;
            if (sepArg is not null)
            {
                var sepSeq = sepArg(input, env);
                if (!sepSeq.IsUndefined && sepSeq.FirstOrDefault.ValueKind != JsonValueKind.String)
                {
                    throw new JsonataException("T0410", "Second argument of $join must be a string", 0);
                }

                separator = FunctionalCompiler.CoerceToString(sepSeq);
            }

            var values = new List<string>();

            // Handle multi-valued sequences (e.g., from piping)
            if (arrSeq.Count > 1)
            {
                for (int i = 0; i < arrSeq.Count; i++)
                {
                    var el = arrSeq[i];
                    if (el.ValueKind != JsonValueKind.String)
                    {
                        throw new JsonataException("T0412", "Argument 1 of function $join must be an array of strings", 0);
                    }

                    values.Add(el.GetString() ?? string.Empty);
                }
            }
            else
            {
                var arr = arrSeq.FirstOrDefault;
                if (arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in arr.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.String)
                        {
                            throw new JsonataException("T0412", "Argument 1 of function $join must be an array of strings", 0);
                        }

                        values.Add(item.GetString() ?? string.Empty);
                    }
                }
                else if (arr.ValueKind == JsonValueKind.String)
                {
                    values.Add(arr.GetString() ?? string.Empty);
                }
                else
                {
                    throw new JsonataException("T0412", "Argument 1 of function $join must be an array of strings", 0);
                }
            }

            return new Sequence(FunctionalCompiler.CreateStringElement(string.Join(separator, values)));
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
            throw new JsonataException("T0410", "$split expects 1-3 arguments", 0);
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
                throw new JsonataException("T0410", "Argument 1 of function $split is not of the correct type", 0);
            }

            string? str = strSeq.FirstOrDefault.GetString();
            if (str is null)
            {
                return Sequence.Undefined;
            }

            // Validate separator is a string or regex
            if (!sepSeq.IsRegex)
            {
                if (sepSeq.IsLambda)
                {
                    throw new JsonataException("T1010", "The function argument to '$split' does not match function signature", 0);
                }

                if (sepSeq.IsUndefined || sepSeq.FirstOrDefault.ValueKind != JsonValueKind.String)
                {
                    throw new JsonataException("T0410", "Argument 2 of function $split is not of the correct type", 0);
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
                        throw new JsonataException("T0410", "Argument 3 of function $split is not of the correct type", 0);
                    }

                    if (FunctionalCompiler.TryCoerceToNumber(limitSeq.FirstOrDefault, out double limitD))
                    {
                        if (limitD < 0)
                        {
                            throw new JsonataException("D3020", "Third argument of the split function must evaluate to a positive number", 0);
                        }

                        // Floor the limit (JSONata accepts non-integer limits)
                        limit = (int)Math.Floor(limitD);
                    }
                }
            }

            string[] parts;
            if (sepSeq.IsRegex)
            {
                parts = sepSeq.Regex!.Split(str);
            }
            else
            {
                string? sep = sepSeq.FirstOrDefault.GetString();
                if (sep is null)
                {
                    return Sequence.Undefined;
                }

                if (sep.Length == 0)
                {
                    // Empty separator: split into individual characters
                    parts = new string[str.Length];
                    for (int i = 0; i < str.Length; i++)
                    {
                        parts[i] = str[i].ToString();
                    }
                }
                else
                {
                    parts = str.Split(new[] { sep }, StringSplitOptions.None);
                }
            }

            int count = Math.Min(parts.Length, limit);

            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartArray();
            for (int i = 0; i < count; i++)
            {
                writer.WriteStringValue(parts[i]);
            }

            writer.WriteEndArray();
            writer.Flush();
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            return new Sequence(doc.RootElement.Clone());
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
            throw new JsonataException("T0410", "$contains expects 1 or 2 arguments", 0);
        }

        return (in JsonElement input, Environment env) =>
        {
            var strSeq = strArg(input, env);
            var searchSeq = searchArg(input, env);

            if (strSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            string? str = strSeq.FirstOrDefault.GetString();
            if (str is null)
            {
                return Sequence.Undefined;
            }

            if (searchSeq.IsRegex)
            {
                bool isMatch = searchSeq.Regex!.IsMatch(str);
                return new Sequence(FunctionalCompiler.CreateBoolElement(isMatch));
            }

            string? search = searchSeq.FirstOrDefault.GetString();
            bool result = search is not null && str.Contains(search);
            return new Sequence(FunctionalCompiler.CreateBoolElement(result));
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
            throw new JsonataException("T0410", "$sqrt expects 1 argument", 0);
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
                throw new JsonataException("D3060", "The argument of the $sqrt function must be non-negative", 0);
            }

            return new Sequence(FunctionalCompiler.CreateNumberElement(Math.Sqrt(num)));
        };
    }

    private static ExpressionEvaluator CompileRound(ExpressionEvaluator[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            throw new JsonataException("T0410", "$round expects 1 or 2 arguments", 0);
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

            return new Sequence(FunctionalCompiler.CreateNumberElement(result));
        };
    }

    private static ExpressionEvaluator CompilePower(ExpressionEvaluator[] args)
    {
        if (args.Length != 2)
        {
            throw new JsonataException("T0410", "$power expects 2 arguments", 0);
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
                throw new JsonataException("D3061", "The power function has resulted in a value that cannot be represented as a JSON number", 0);
            }

            return new Sequence(FunctionalCompiler.CreateNumberElement(result));
        };
    }

    private static ExpressionEvaluator CompileMathFunc(ExpressionEvaluator[] args, Func<double, double> func)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "Math function expects 1 argument", 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined || !FunctionalCompiler.TryCoerceToNumber(seq.FirstOrDefault, out double num))
            {
                return Sequence.Undefined;
            }

            return new Sequence(FunctionalCompiler.CreateNumberElement(func(num)));
        };
    }

    // --- Object functions ---
    private static ExpressionEvaluator CompileKeys(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$keys expects 1 argument", 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            // Collect unique keys from objects (handles arrays of objects too)
            var keys = new List<string>();
            var seen = new HashSet<string>();

            void CollectKeys(JsonElement el)
            {
                if (el.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in el.EnumerateObject())
                    {
                        if (seen.Add(prop.Name))
                        {
                            keys.Add(prop.Name);
                        }
                    }
                }
                else if (el.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in el.EnumerateArray())
                    {
                        CollectKeys(item);
                    }
                }
            }

            for (int i = 0; i < seq.Count; i++)
            {
                CollectKeys(seq[i]);
            }

            if (keys.Count == 0)
            {
                return Sequence.Undefined;
            }

            if (keys.Count == 1)
            {
                return new Sequence(FunctionalCompiler.CreateStringElement(keys[0]));
            }

            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartArray();
            foreach (var key in keys)
            {
                writer.WriteStringValue(key);
            }

            writer.WriteEndArray();
            writer.Flush();
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            return new Sequence(doc.RootElement.Clone());
        };
    }

    private static ExpressionEvaluator CompileValues(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$values expects 1 argument", 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined || seq.FirstOrDefault.ValueKind != JsonValueKind.Object)
            {
                return Sequence.Undefined;
            }

            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartArray();
            foreach (var prop in seq.FirstOrDefault.EnumerateObject())
            {
                prop.Value.WriteTo(writer);
            }

            writer.WriteEndArray();
            writer.Flush();
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            return new Sequence(doc.RootElement.Clone());
        };
    }

    // --- Array functions (stubs — full implementation in Phase 3) ---
    private static ExpressionEvaluator CompileAppend(ExpressionEvaluator[] args)
    {
        if (args.Length != 2)
        {
            throw new JsonataException("T0410", "$append expects 2 arguments", 0);
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

            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartArray();
            WriteSequenceToArray(seq1, writer);
            WriteSequenceToArray(seq2, writer);
            writer.WriteEndArray();
            writer.Flush();
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            return new Sequence(doc.RootElement.Clone());
        };
    }

    private static ExpressionEvaluator CompileSortFunc(ExpressionEvaluator[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            throw new JsonataException("T0410", "$sort expects 1-2 arguments", 0);
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
            var elements = new List<JsonElement>();
            for (int i = 0; i < seq.Count; i++)
            {
                var el = seq[i];
                if (el.ValueKind == JsonValueKind.Array)
                {
                    elements.AddRange(el.EnumerateArray());
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
                    StableSort(elements, (a, b) =>
                    {
                        var aSeq = new Sequence(a);
                        var bSeq = new Sequence(b);
                        var result = lambda.Invoke([aSeq, bSeq], sortInput, env);
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
                            var reverseResult = lambda.Invoke([bSeq, aSeq], sortInput, env);
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
            }
            else
            {
                // Default sort: check types are homogeneous
                bool hasObjects = false;
                foreach (var el in elements)
                {
                    if (el.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        hasObjects = true;
                        break;
                    }
                }

                if (hasObjects)
                {
                    throw new JsonataException("D3070", "The single argument form of the $sort function can only be used on an array of strings or an array of numbers", 0);
                }

                StableSort(elements, (a, b) =>
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

            return new Sequence(FunctionalCompiler.CreateJsonArrayElement(elements));
        };
    }

    private static ExpressionEvaluator CompileReverse(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$reverse expects 1 argument", 0);
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

            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartArray();
            int len = arr.GetArrayLength();
            for (int i = len - 1; i >= 0; i--)
            {
                arr[i].WriteTo(writer);
            }

            writer.WriteEndArray();
            writer.Flush();
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            return new Sequence(doc.RootElement.Clone());
        };
    }

    private static ExpressionEvaluator CompileDistinct(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$distinct expects 1 argument", 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            // Collect all items to deduplicate, flattening arrays in multi-valued sequences
            var items = new List<JsonElement>();
            for (int si = 0; si < seq.Count; si++)
            {
                var el = seq[si];
                if (el.ValueKind == JsonValueKind.Array)
                {
                    items.AddRange(el.EnumerateArray());
                }
                else
                {
                    items.Add(el);
                }
            }

            // If only one non-array item, return as-is
            if (items.Count <= 1 && seq.Count == 1 && seq.FirstOrDefault.ValueKind != JsonValueKind.Array)
            {
                return seq;
            }

            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartArray();
            var seen = new HashSet<string>();
            for (int i = 0; i < items.Count; i++)
            {
                var raw = items[i].GetRawText();
                if (seen.Add(raw))
                {
                    items[i].WriteTo(writer);
                }
            }

            writer.WriteEndArray();
            writer.Flush();
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            return new Sequence(doc.RootElement.Clone());
        };
    }

    private static ExpressionEvaluator CompileFlatten(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$flatten expects 1 argument", 0);
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

            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartArray();
            FlattenElement(arr, writer);
            writer.WriteEndArray();
            writer.Flush();
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            return new Sequence(doc.RootElement.Clone());
        };

        static void FlattenElement(JsonElement element, Utf8JsonWriter writer)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    FlattenElement(item, writer);
                }
            }
            else
            {
                element.WriteTo(writer);
            }
        }
    }

    // --- Higher-order functions (stubs) ---
    private static ExpressionEvaluator CompileMap(ExpressionEvaluator[] args)
    {
        if (args.Length != 2)
        {
            throw new JsonataException("T0410", "$map expects 2 arguments", 0);
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
            var builder = default(SequenceBuilder);

            // Collect all items into a flat list for indexing
            var items = new List<JsonElement>();
            if (seq.IsSingleton && seq.FirstOrDefault.ValueKind == JsonValueKind.Array)
            {
                var arr = seq.FirstOrDefault;
                int len = arr.GetArrayLength();
                for (int idx = 0; idx < len; idx++)
                {
                    items.Add(arr[idx]);
                }
            }
            else
            {
                for (int i = 0; i < seq.Count; i++)
                {
                    var elem = seq[i];
                    if (elem.ValueKind == JsonValueKind.Array)
                    {
                        int len = elem.GetArrayLength();
                        for (int j = 0; j < len; j++)
                        {
                            items.Add(elem[j]);
                        }
                    }
                    else
                    {
                        items.Add(elem);
                    }
                }
            }

            // Build the flattened array as a Sequence for the 3rd lambda arg
            JsonElement flatArr;
            {
                using var flatMs = new MemoryStream(256);
                using var flatWriter = new Utf8JsonWriter(flatMs);
                flatWriter.WriteStartArray();
                foreach (var item in items)
                {
                    item.WriteTo(flatWriter);
                }

                flatWriter.WriteEndArray();
                flatWriter.Flush();
                flatMs.Position = 0;
                using var flatDoc = JsonDocument.Parse(flatMs);
                flatArr = flatDoc.RootElement.Clone();
            }

            var arrSeq = new Sequence(flatArr);
            for (int i = 0; i < items.Count; i++)
            {
                var elemSeq = new Sequence(items[i]);
                var idxSeq = new Sequence(FunctionalCompiler.CreateNumberElement(i));
                var result = lambda.Invoke(new[] { elemSeq, idxSeq, arrSeq }, items[i], env);
                builder.AddRange(result);
            }

            // Return as a multi-value Sequence (not wrapped in a JSON array).
            // The evaluator's Sequence→JsonElement conversion handles array
            // creation when there are multiple results, and auto-unwraps
            // singletons. This matches JSONata's auto-wrap/unwrap semantics.
            return builder.ToSequence();
        };
    }

    private static ExpressionEvaluator CompileFilterFunc(ExpressionEvaluator[] args)
    {
        if (args.Length != 2)
        {
            throw new JsonataException("T0410", "$filter expects 2 arguments", 0);
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
                    using var flatMs = new MemoryStream(256);
                    using var flatWriter = new Utf8JsonWriter(flatMs);
                    flatWriter.WriteStartArray();
                    for (int i = 0; i < seq.Count; i++)
                    {
                        var elem = seq[i];
                        if (elem.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in elem.EnumerateArray())
                            {
                                item.WriteTo(flatWriter);
                            }
                        }
                        else
                        {
                            elem.WriteTo(flatWriter);
                        }
                    }

                    flatWriter.WriteEndArray();
                    flatWriter.Flush();
                    flatMs.Position = 0;
                    using var flatDoc = JsonDocument.Parse(flatMs);
                    seq = new Sequence(flatDoc.RootElement.Clone());
                    inputWasArray = true;
                }
            }

            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartArray();
            int matchCount = 0;

            if (inputWasArray)
            {
                var arr = seq.FirstOrDefault;
                int len = arr.GetArrayLength();
                for (int i = 0; i < len; i++)
                {
                    var elemSeq = new Sequence(arr[i]);
                    var idxSeq = new Sequence(FunctionalCompiler.CreateNumberElement(i));
                    var result = lambda.Invoke(new[] { elemSeq, idxSeq, seq }, arr[i], env);
                    if (FunctionalCompiler.IsTruthy(result))
                    {
                        arr[i].WriteTo(writer);
                        matchCount++;
                    }
                }
            }
            else
            {
                for (int i = 0; i < seq.Count; i++)
                {
                    var elemSeq = new Sequence(seq[i]);
                    var idxSeq = new Sequence(FunctionalCompiler.CreateNumberElement(i));
                    var result = lambda.Invoke(new[] { elemSeq, idxSeq, seq }, seq[i], env);
                    if (FunctionalCompiler.IsTruthy(result))
                    {
                        seq[i].WriteTo(writer);
                        matchCount++;
                    }
                }
            }

            writer.WriteEndArray();
            writer.Flush();
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            var resultArr = doc.RootElement.Clone();

            // If input was not an array, unwrap single results
            if (!inputWasArray)
            {
                if (matchCount == 0)
                {
                    return Sequence.Undefined;
                }

                if (matchCount == 1)
                {
                    return new Sequence(resultArr[0].Clone());
                }
            }

            return new Sequence(resultArr);
        };
    }

    private static ExpressionEvaluator CompileReduce(ExpressionEvaluator[] args)
    {
        if (args.Length < 2 || args.Length > 3)
        {
            throw new JsonataException("T0410", "$reduce expects 2-3 arguments", 0);
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
                throw new JsonataException("D3050", "The second argument of the $reduce function must be a function with at least two arguments", 0);
            }

            // Collect array elements
            var elements = new List<JsonElement>();
            if (seq.IsSingleton && seq.FirstOrDefault.ValueKind == JsonValueKind.Array)
            {
                elements.AddRange(seq.FirstOrDefault.EnumerateArray());
            }
            else
            {
                for (int i = 0; i < seq.Count; i++)
                {
                    elements.Add(seq[i]);
                }
            }

            if (elements.Count == 0)
            {
                return Sequence.Undefined;
            }

            Sequence accumulator;
            int startIdx;
            if (initArg is not null)
            {
                accumulator = initArg(input, env);
                startIdx = 0;
            }
            else
            {
                accumulator = new Sequence(elements[0]);
                startIdx = 1;
            }

            // Build array element for the 4th lambda arg
            JsonElement arrElement;
            {
                using var arrMs = new MemoryStream(256);
                using var arrWriter = new Utf8JsonWriter(arrMs);
                arrWriter.WriteStartArray();
                for (int i = 0; i < elements.Count; i++)
                {
                    elements[i].WriteTo(arrWriter);
                }

                arrWriter.WriteEndArray();
                arrWriter.Flush();
                arrMs.Position = 0;
                using var arrDoc = JsonDocument.Parse(arrMs);
                arrElement = arrDoc.RootElement.Clone();
            }

            var arrSeq = new Sequence(arrElement);

            for (int i = startIdx; i < elements.Count; i++)
            {
                var elemSeq = new Sequence(elements[i]);
                var idxSeq = new Sequence(FunctionalCompiler.CreateNumberElement(i));
                accumulator = lambda.Invoke(new[] { accumulator, elemSeq, idxSeq, arrSeq }, input, env);
            }

            return accumulator;
        };
    }

    private static ExpressionEvaluator CompileEach(ExpressionEvaluator[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            throw new JsonataException("T0410", "$each expects 1-2 arguments", 0);
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
            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartArray();

            foreach (var prop in obj.EnumerateObject())
            {
                var valSeq = new Sequence(prop.Value);
                var keySeq = new Sequence(FunctionalCompiler.CreateStringElement(prop.Name));
                var result = lambda.Invoke(new[] { valSeq, keySeq }, input, env);
                if (!result.IsUndefined)
                {
                    if (result.IsSingleton)
                    {
                        result.FirstOrDefault.WriteTo(writer);
                    }
                    else
                    {
                        for (int i = 0; i < result.Count; i++)
                        {
                            result[i].WriteTo(writer);
                        }
                    }
                }
            }

            writer.WriteEndArray();
            writer.Flush();
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            return new Sequence(doc.RootElement.Clone());
        };
    }

    private static ExpressionEvaluator CompileMerge(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$merge expects 1 argument", 0);
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

            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartObject();

            if (seq.IsSingleton && seq.FirstOrDefault.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in seq.FirstOrDefault.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in item.EnumerateObject())
                        {
                            writer.WritePropertyName(prop.Name);
                            prop.Value.WriteTo(writer);
                        }
                    }
                }
            }
            else if (seq.Count > 1)
            {
                for (int i = 0; i < seq.Count; i++)
                {
                    var item = seq[i];
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in item.EnumerateObject())
                        {
                            writer.WritePropertyName(prop.Name);
                            prop.Value.WriteTo(writer);
                        }
                    }
                }
            }

            writer.WriteEndObject();
            writer.Flush();
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            return new Sequence(doc.RootElement.Clone());
        };
    }

    private static ExpressionEvaluator CompileSpread(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$spread expects 1 argument", 0);
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

            // Collect all elements to spread
            var objects = new List<JsonElement>();
            for (int i = 0; i < seq.Count; i++)
            {
                var el = seq[i];
                if (el.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in el.EnumerateObject())
                    {
                        using var ms2 = new MemoryStream(64);
                        using var w2 = new Utf8JsonWriter(ms2);
                        w2.WriteStartObject();
                        w2.WritePropertyName(prop.Name);
                        prop.Value.WriteTo(w2);
                        w2.WriteEndObject();
                        w2.Flush();
                        ms2.Position = 0;
                        using var d2 = JsonDocument.Parse(ms2);
                        objects.Add(d2.RootElement.Clone());
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
                                using var ms2 = new MemoryStream(64);
                                using var w2 = new Utf8JsonWriter(ms2);
                                w2.WriteStartObject();
                                w2.WritePropertyName(prop.Name);
                                prop.Value.WriteTo(w2);
                                w2.WriteEndObject();
                                w2.Flush();
                                ms2.Position = 0;
                                using var d2 = JsonDocument.Parse(ms2);
                                objects.Add(d2.RootElement.Clone());
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

            if (objects.Count == 0)
            {
                return Sequence.Undefined;
            }

            if (objects.Count == 1)
            {
                return new Sequence(objects[0]);
            }

            return new Sequence(FunctionalCompiler.CreateJsonArrayElement(objects));
        };
    }

    private static ExpressionEvaluator CompileLookup(ExpressionEvaluator[] args)
    {
        if (args.Length != 2)
        {
            throw new JsonataException("T0410", "$lookup expects 2 arguments", 0);
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

            string? key = keySeq.FirstOrDefault.GetString();
            if (key is null)
            {
                return Sequence.Undefined;
            }

            // Collect all objects from the sequence, flattening any nested arrays
            var results = new List<JsonElement>();
            LookupCollect(objSeq, key, results);

            if (results.Count == 0)
            {
                return Sequence.Undefined;
            }

            if (results.Count == 1)
            {
                return new Sequence(results[0]);
            }

            return new Sequence(FunctionalCompiler.CreateJsonArrayElement(results));
        };
    }

    private static void LookupCollect(Sequence seq, string key, List<JsonElement> results)
    {
        for (int i = 0; i < seq.Count; i++)
        {
            var el = seq[i];
            LookupCollectElement(el, key, results);
        }
    }

    private static void LookupCollectElement(JsonElement el, string key, List<JsonElement> results)
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                LookupCollectElement(item, key, results);
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

    // --- Thread-safe random for netstandard2.0/net481 compatibility ---
    [ThreadStatic]
    private static Random? t_random;

    private static Random ThreadRandom => t_random ??= new Random();

    // --- Higher-order: single, sift ---
    private static ExpressionEvaluator CompileSingle(ExpressionEvaluator[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            throw new JsonataException("T0410", "$single expects 1-2 arguments", 0);
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

            var elements = new List<JsonElement>();
            if (seq.IsSingleton && seq.FirstOrDefault.ValueKind == JsonValueKind.Array)
            {
                elements.AddRange(seq.FirstOrDefault.EnumerateArray());
            }
            else
            {
                for (int i = 0; i < seq.Count; i++)
                {
                    elements.Add(seq[i]);
                }
            }

            if (funcArg is null)
            {
                if (elements.Count == 0)
                {
                    throw new JsonataException("D3139", "The $single function matched zero results", 0);
                }

                if (elements.Count != 1)
                {
                    throw new JsonataException("D3138", "The $single function expected exactly one matching result", 0);
                }

                return new Sequence(elements[0]);
            }

            var funcSeq = funcArg(input, env);
            if (!funcSeq.IsLambda)
            {
                return Sequence.Undefined;
            }

            var lambda = funcSeq.Lambda!;

            // Build array element for 3rd lambda arg
            JsonElement arrElement;
            {
                using var arrMs = new MemoryStream(256);
                using var arrWriter = new Utf8JsonWriter(arrMs);
                arrWriter.WriteStartArray();
                for (int i = 0; i < elements.Count; i++)
                {
                    elements[i].WriteTo(arrWriter);
                }

                arrWriter.WriteEndArray();
                arrWriter.Flush();
                arrMs.Position = 0;
                using var arrDoc = JsonDocument.Parse(arrMs);
                arrElement = arrDoc.RootElement.Clone();
            }

            var arrSeq = new Sequence(arrElement);
            JsonElement? match = null;
            for (int i = 0; i < elements.Count; i++)
            {
                var result = lambda.Invoke(
                    new[] { new Sequence(elements[i]), new Sequence(FunctionalCompiler.CreateNumberElement(i)), arrSeq },
                    elements[i],
                    env);
                if (FunctionalCompiler.IsTruthy(result))
                {
                    if (match.HasValue)
                    {
                        throw new JsonataException("D3138", "The $single function expected exactly one matching result", 0);
                    }

                    match = elements[i];
                }
            }

            if (!match.HasValue)
            {
                throw new JsonataException("D3139", "The $single function matched zero results", 0);
            }

            return new Sequence(match.Value);
        };
    }

    private static ExpressionEvaluator CompileSift(ExpressionEvaluator[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            throw new JsonataException("T0410", "$sift expects 1-2 arguments", 0);
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
            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartObject();
            var objSeq = new Sequence(obj);
            bool anyMatch = false;
            foreach (var prop in obj.EnumerateObject())
            {
                var valSeq = new Sequence(prop.Value);
                var keySeq = new Sequence(FunctionalCompiler.CreateStringElement(prop.Name));
                var result = lambda.Invoke(new[] { valSeq, keySeq, objSeq }, input, env);
                if (FunctionalCompiler.IsTruthy(result))
                {
                    writer.WritePropertyName(prop.Name);
                    prop.Value.WriteTo(writer);
                    anyMatch = true;
                }
            }

            writer.WriteEndObject();

            if (!anyMatch)
            {
                return Sequence.Undefined;
            }

            writer.Flush();
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            return new Sequence(doc.RootElement.Clone());
        };
    }

    // --- String functions: pad, match, replace ---
    private static ExpressionEvaluator CompilePad(ExpressionEvaluator[] args)
    {
        if (args.Length < 2 || args.Length > 3)
        {
            throw new JsonataException("T0410", "$pad expects 2-3 arguments", 0);
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
                return new Sequence(FunctionalCompiler.CreateStringElement(str));
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
            return new Sequence(FunctionalCompiler.CreateStringElement(result));
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
            throw new JsonataException("T0410", "$match expects 1-3 arguments", 0);
        }

        return (in JsonElement input, Environment env) =>
        {
            var strSeq = strArg(input, env);
            var patSeq = patternArg(input, env);

            if (strSeq.IsUndefined || !patSeq.IsRegex)
            {
                return Sequence.Undefined;
            }

            string? str = strSeq.FirstOrDefault.GetString();
            if (str is null)
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
            MatchCollection matches = regex.Matches(str);

            var results = new List<JsonElement>();
            int count = 0;
            foreach (Match m in matches)
            {
                if (count >= limit)
                {
                    break;
                }

                var groups = new List<string>();
                for (int g = 1; g < m.Groups.Count; g++)
                {
                    groups.Add(m.Groups[g].Value);
                }

                results.Add(FunctionalCompiler.CreateMatchObject(m.Value, m.Index, groups));
                count++;
            }

            if (results.Count == 0)
            {
                return Sequence.Undefined;
            }

            if (results.Count == 1)
            {
                return new Sequence(results[0]);
            }

            return new Sequence(results.ToArray(), results.Count);
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
            throw new JsonataException("T0410", "$replace expects 2-4 arguments", 0);
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
                throw new JsonataException("T0410", "Argument 1 of function $replace is not of the correct type", 0);
            }

            // Validate pattern is a string or regex
            if (!patSeq.IsRegex)
            {
                if (patSeq.IsUndefined || patSeq.IsLambda || patSeq.FirstOrDefault.ValueKind != JsonValueKind.String)
                {
                    throw new JsonataException("T0410", "Argument 2 of function $replace is not of the correct type", 0);
                }
            }

            // Validate replacement is a string or function
            bool isLambdaReplacement = repSeq.IsLambda;
            if (!isLambdaReplacement)
            {
                if (repSeq.IsUndefined || repSeq.FirstOrDefault.ValueKind != JsonValueKind.String)
                {
                    throw new JsonataException("T0410", "Argument 3 of function $replace is not of the correct type", 0);
                }
            }

            string str = FunctionalCompiler.CoerceElementToString(strSeq.FirstOrDefault);
            int limit = int.MaxValue;
            if (limitArg is not null)
            {
                var limSeq = limitArg(input, env);
                if (!limSeq.IsUndefined)
                {
                    if (limSeq.IsLambda || limSeq.FirstOrDefault.ValueKind != JsonValueKind.Number)
                    {
                        throw new JsonataException("T0410", "Argument 4 of function $replace is not of the correct type", 0);
                    }

                    if (FunctionalCompiler.TryCoerceToNumber(limSeq.FirstOrDefault, out double n))
                    {
                        if (n < 0)
                        {
                            throw new JsonataException("D3011", "The fourth argument of the $replace function must be a positive number", 0);
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
                    str = RegexReplaceWithFunction(str, regex, repSeq.Lambda!, limit, input, env);
                }
                else
                {
                    string replacement = FunctionalCompiler.CoerceElementToString(repSeq.FirstOrDefault);
                    str = RegexReplaceWithString(str, regex, replacement, limit);
                }
            }
            else
            {
                string pattern = FunctionalCompiler.CoerceElementToString(patSeq.FirstOrDefault);

                // Empty string pattern is an error
                if (pattern.Length == 0)
                {
                    throw new JsonataException("D3010", "The second argument of the $replace function cannot be an empty string", 0);
                }

                string replacement = FunctionalCompiler.CoerceElementToString(repSeq.FirstOrDefault);

                int count = 0;
                int idx;
                while (count < limit && (idx = str.IndexOf(pattern, StringComparison.Ordinal)) >= 0)
                {
                    str = str.Substring(0, idx) + replacement + str.Substring(idx + pattern.Length);
                    count++;
                }
            }

            return new Sequence(FunctionalCompiler.CreateStringElement(str));
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
                throw new JsonataException("D1004", "Regular expression matches zero length string", 0);
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
                throw new JsonataException("D1004", "Regular expression matches zero length string", 0);
            }

            sb.Append(str, searchStart, m.Index - searchStart);

            var groups = new List<string>();
            for (int g = 1; g < m.Groups.Count; g++)
            {
                groups.Add(m.Groups[g].Value);
            }

            JsonElement matchObj = FunctionalCompiler.CreateMatchObject(m.Value, m.Index, groups);
            Sequence result = lambda.Invoke(new[] { new Sequence(matchObj) }, input, env);

            if (result.IsUndefined || result.FirstOrDefault.ValueKind != JsonValueKind.String)
            {
                throw new JsonataException("D3012", "The replacement function must return a string", 0);
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

    // --- Encoding functions ---
    private static ExpressionEvaluator CompileBase64Encode(ExpressionEvaluator[] args)
    {
        if (args.Length > 1)
        {
            throw new JsonataException("T0410", "$base64encode expects 0-1 arguments", 0);
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
            return new Sequence(FunctionalCompiler.CreateStringElement(
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(str))));
        };
    }

    private static ExpressionEvaluator CompileBase64Decode(ExpressionEvaluator[] args)
    {
        if (args.Length > 1)
        {
            throw new JsonataException("T0410", "$base64decode expects 0-1 arguments", 0);
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
            return new Sequence(FunctionalCompiler.CreateStringElement(
                System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(str))));
        };
    }

    private static ExpressionEvaluator CompileEncodeUrlComponent(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$encodeUrlComponent expects 1 argument", 0);
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
                throw new JsonataException("D3140", "Malformed URL passed to $encodeUrlComponent(): invalid string", 0);
            }

            ValidateNoUnpairedSurrogates(str, "$encodeUrlComponent");
            return new Sequence(FunctionalCompiler.CreateStringElement(Uri.EscapeDataString(str)));
        };
    }

    private static ExpressionEvaluator CompileDecodeUrlComponent(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$decodeUrlComponent expects 1 argument", 0);
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
                throw new JsonataException("D3140", $"Malformed URL passed to $decodeUrlComponent(): \"{str}\"", 0);
            }

            try
            {
                return new Sequence(FunctionalCompiler.CreateStringElement(Uri.UnescapeDataString(str)));
            }
            catch (Exception ex) when (ex is UriFormatException or ArgumentException or FormatException)
            {
                throw new JsonataException("D3140", $"Malformed URL passed to $decodeUrlComponent(): \"{str}\"", 0);
            }
        };
    }

#pragma warning disable SYSLIB0013 // Uri.EscapeUriString is obsolete
    private static ExpressionEvaluator CompileEncodeUrl(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$encodeUrl expects 1 argument", 0);
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
                throw new JsonataException("D3140", "Malformed URL passed to $encodeUrl(): invalid string", 0);
            }

            ValidateNoUnpairedSurrogates(str, "$encodeUrl");
            return new Sequence(FunctionalCompiler.CreateStringElement(Uri.EscapeUriString(str)));
        };
    }
#pragma warning restore SYSLIB0013

    private static ExpressionEvaluator CompileDecodeUrl(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$decodeUrl expects 1 argument", 0);
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
                throw new JsonataException("D3140", $"Malformed URL passed to $decodeUrl(): \"{str}\"", 0);
            }

            try
            {
                return new Sequence(FunctionalCompiler.CreateStringElement(Uri.UnescapeDataString(str)));
            }
            catch (Exception ex) when (ex is UriFormatException or ArgumentException or FormatException)
            {
                throw new JsonataException("D3140", $"Malformed URL passed to $decodeUrl(): \"{str}\"", 0);
            }
        };
    }

    // --- Numeric functions: random, formatNumber, formatBase ---
    private static bool HasInvalidPercentEncoding(string input)
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

    private static void ValidateNoUnpairedSurrogates(string str, string funcName)
    {
        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 >= str.Length || !char.IsLowSurrogate(str[i + 1]))
                {
                    throw new JsonataException("D3140", $"Malformed URL passed to {funcName}(): \"{str}\"", 0);
                }

                i++;
            }
            else if (char.IsLowSurrogate(c))
            {
                throw new JsonataException("D3140", $"Malformed URL passed to {funcName}(): \"{str}\"", 0);
            }
        }
    }

    private static bool IsHexDigit(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private static ExpressionEvaluator CompileRandom(ExpressionEvaluator[] args)
    {
        return static (in JsonElement input, Environment env) =>
        {
            return new Sequence(FunctionalCompiler.CreateNumberElement(ThreadRandom.NextDouble()));
        };
    }

    private static ExpressionEvaluator CompileFormatNumber(ExpressionEvaluator[] args)
    {
        if (args.Length < 2 || args.Length > 3)
        {
            throw new JsonataException("T0410", "$formatNumber expects 2 or 3 arguments", 0);
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

            return new Sequence(FunctionalCompiler.CreateStringElement(
                FormatNumberXPath(num, picture, options)));
        };
    }

    private static string FormatNumberXPath(double value, string picture, JsonElement options)
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
            throw new JsonataException("D3080", "The picture string must not contain more than one pattern separator", 0);
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
                throw new JsonataException("D3081", "The picture string must not contain more than one decimal separator", 0);
            }

            if (pct.Length > 0)
            {
                int firstPct = sub.IndexOf(pct, StringComparison.Ordinal);
                if (firstPct != -1 && sub.IndexOf(pct, firstPct + pct.Length, StringComparison.Ordinal) != -1)
                {
                    throw new JsonataException("D3082", "The picture string must not contain more than one percent character", 0);
                }
            }

            if (perMille.Length > 0)
            {
                int firstPm = sub.IndexOf(perMille, StringComparison.Ordinal);
                if (firstPm != -1 && sub.IndexOf(perMille, firstPm + perMille.Length, StringComparison.Ordinal) != -1)
                {
                    throw new JsonataException("D3083", "The picture string must not contain more than one per-mille character", 0);
                }
            }

            if (pct.Length > 0 && perMille.Length > 0 &&
                sub.IndexOf(pct, StringComparison.Ordinal) != -1 &&
                sub.IndexOf(perMille, StringComparison.Ordinal) != -1)
            {
                throw new JsonataException("D3084", "The picture string must not contain both percent and per-mille characters", 0);
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
                throw new JsonataException("D3085", "The mantissa part of the picture string must contain at least one digit or optional digit character", 0);
            }

            for (int i = 0; i < activeParts[p].Length; i++)
            {
                if (!IsActive(activeParts[p][i]))
                {
                    throw new JsonataException("D3086", "The picture string must not contain a passive character between active characters", 0);
                }
            }

            int decPosInSub = sub.IndexOf(decSep);
            if (decPosInSub != -1)
            {
                if ((decPosInSub > 0 && sub[decPosInSub - 1] == grpSep) ||
                    (decPosInSub < sub.Length - 1 && sub[decPosInSub + 1] == grpSep))
                {
                    throw new JsonataException("D3087", "The picture string must not contain a grouping separator adjacent to the decimal separator", 0);
                }
            }
            else
            {
                if (integerParts[p].Length > 0 && integerParts[p][integerParts[p].Length - 1] == grpSep)
                {
                    throw new JsonataException("D3088", "The integer part of the picture string must not end with a grouping separator", 0);
                }
            }

            string doubleGrp = new string(grpSep, 2);
            if (sub.IndexOf(doubleGrp, StringComparison.Ordinal) != -1)
            {
                throw new JsonataException("D3089", "The picture string must not contain adjacent grouping separators", 0);
            }

            int optPos = integerParts[p].IndexOf(optDigit);
            if (optPos != -1)
            {
                for (int i = 0; i < optPos; i++)
                {
                    if (IsInDigitFamily(integerParts[p][i]))
                    {
                        throw new JsonataException("D3090", "The integer part must not contain a mandatory digit before an optional digit", 0);
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
                        throw new JsonataException("D3091", "The fractional part must not contain a mandatory digit after an optional digit", 0);
                    }
                }
            }

            bool expExists = exponentParts[p] is not null;
            if (expExists && exponentParts[p]!.Length > 0 &&
                (sub.IndexOf(pct, StringComparison.Ordinal) != -1 ||
                 sub.IndexOf(perMille, StringComparison.Ordinal) != -1))
            {
                throw new JsonataException("D3092", "The picture string must not contain an exponent with percent or per-mille", 0);
            }

            if (expExists)
            {
                if (exponentParts[p]!.Length == 0)
                {
                    throw new JsonataException("D3093", "The exponent part of the picture string must contain at least one digit", 0);
                }

                for (int i = 0; i < exponentParts[p]!.Length; i++)
                {
                    if (!IsInDigitFamily(exponentParts[p]![i]))
                    {
                        throw new JsonataException("D3093", "The exponent part of the picture string must contain only digit characters", 0);
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
            throw new JsonataException("T0410", "$formatBase expects 1 or 2 arguments", 0);
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
                throw new JsonataException("D3100", $"The radix of the $formatBase function must be between 2 and 36. It was given {radixInt}", 0);
            }

            long numLong = (long)Math.Round(num, MidpointRounding.ToEven);
            bool negative = numLong < 0;
            string result = ConvertToBase(Math.Abs(numLong), radixInt);
            if (negative)
            {
                result = "-" + result;
            }

            return new Sequence(FunctionalCompiler.CreateStringElement(result));
        };
    }

    private static string ConvertToBase(long value, int radix)
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
            throw new JsonataException("T0410", "$shuffle expects 1 argument", 0);
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

            var elements = new List<JsonElement>();
            elements.AddRange(arr.EnumerateArray());

            // Fisher-Yates shuffle
            for (int i = elements.Count - 1; i > 0; i--)
            {
                int j = ThreadRandom.Next(i + 1);
                (elements[i], elements[j]) = (elements[j], elements[i]);
            }

            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartArray();
            foreach (var elem in elements)
            {
                elem.WriteTo(writer);
            }

            writer.WriteEndArray();
            writer.Flush();
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            return new Sequence(doc.RootElement.Clone());
        };
    }

    private static ExpressionEvaluator CompileZip(ExpressionEvaluator[] args)
    {
        return (in JsonElement input, Environment env) =>
        {
            var arrays = new List<List<JsonElement>>();
            foreach (var arg in args)
            {
                var seq = arg(input, env);
                var list = new List<JsonElement>();
                if (seq.IsSingleton && seq.FirstOrDefault.ValueKind == JsonValueKind.Array)
                {
                    list.AddRange(seq.FirstOrDefault.EnumerateArray());
                }
                else if (!seq.IsUndefined)
                {
                    // Scalar values: treat as single-element array
                    for (int i = 0; i < seq.Count; i++)
                    {
                        list.Add(seq[i]);
                    }
                }

                arrays.Add(list);
            }

            if (arrays.Count == 0)
            {
                return Sequence.Undefined;
            }

            int minLen = int.MaxValue;
            foreach (var a in arrays)
            {
                if (a.Count < minLen)
                {
                    minLen = a.Count;
                }
            }

            if (minLen == int.MaxValue)
            {
                minLen = 0;
            }

            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartArray();
            for (int i = 0; i < minLen; i++)
            {
                writer.WriteStartArray();
                foreach (var a in arrays)
                {
                    a[i].WriteTo(writer);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndArray();
            writer.Flush();
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            return new Sequence(doc.RootElement.Clone());
        };
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
                        throw new JsonataException("T0410", "$error expects a string argument", 0);
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
            throw new JsonataException("T0410", "$assert expects 1-2 arguments", 0);
        }

        var condArg = args[0];
        var msgArg = args.Length > 1 ? args[1] : null;
        return (in JsonElement input, Environment env) =>
        {
            var cond = condArg(input, env);

            // $assert requires a boolean argument
            if (cond.IsUndefined || cond.FirstOrDefault.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new JsonataException("T0410", "Argument 1 of function $assert must be a boolean", 0);
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
            return new Sequence(FunctionalCompiler.CreateStringElement(
                DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)));
        };
    }

    private static ExpressionEvaluator CompileMillis(ExpressionEvaluator[] args)
    {
        return static (in JsonElement input, Environment env) =>
        {
            return new Sequence(FunctionalCompiler.CreateNumberElement(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
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
                    return new Sequence(FunctionalCompiler.CreateStringElement(
                        FormatIso8601WithOffset(dt, offset)));
                }

                return new Sequence(FunctionalCompiler.CreateStringElement(
                    dt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)));
            }

            var picSeq = pictureArg(input, env);
            if (picSeq.IsUndefined)
            {
                // Undefined picture -> use ISO 8601 default with timezone
                if (hasTz)
                {
                    return new Sequence(FunctionalCompiler.CreateStringElement(
                        FormatIso8601WithOffset(dt, offset)));
                }

                return new Sequence(FunctionalCompiler.CreateStringElement(
                    dt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)));
            }

            string picture = FunctionalCompiler.CoerceElementToString(picSeq.FirstOrDefault);
            string result = XPathDateTimeFormatter.FormatDateTime(dt, picture);
            return new Sequence(FunctionalCompiler.CreateStringElement(result));
        };
    }

    private static string FormatIso8601WithOffset(DateTimeOffset dt, TimeSpan offset)
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

    private static ExpressionEvaluator CompileToMillis(ExpressionEvaluator[] args)
    {
        if (args.Length < 1)
        {
            throw new JsonataException("T0410", "$toMillis expects at least 1 argument", 0);
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
                if (DateTimeOffset.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    return new Sequence(FunctionalCompiler.CreateNumberElement(dt.ToUnixTimeMilliseconds()));
                }

                return Sequence.Undefined;
            }

            var picSeq = pictureArg(input, env);
            if (picSeq.IsUndefined)
            {
                if (DateTimeOffset.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    return new Sequence(FunctionalCompiler.CreateNumberElement(dt.ToUnixTimeMilliseconds()));
                }

                return Sequence.Undefined;
            }

            string picture = FunctionalCompiler.CoerceElementToString(picSeq.FirstOrDefault);

            try
            {
                if (XPathDateTimeFormatter.TryParseDateTime(str, picture, out long millis))
                {
                    return new Sequence(FunctionalCompiler.CreateNumberElement(millis));
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
            throw new JsonataException("T0410", "$formatInteger expects at least 2 arguments", 0);
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

            if (!FunctionalCompiler.TryCoerceToNumber(numSeq.FirstOrDefault, out double numVal))
            {
                return Sequence.Undefined;
            }

            var picSeq = picArg(input, env);
            string picture = FunctionalCompiler.CoerceElementToString(picSeq.FirstOrDefault);

            long intVal = (long)numVal;
            string result = XPathDateTimeFormatter.FormatInteger(intVal, picture);
            return new Sequence(FunctionalCompiler.CreateStringElement(result));
        };
    }

    private static ExpressionEvaluator CompileParseInteger(ExpressionEvaluator[] args)
    {
        if (args.Length < 2)
        {
            throw new JsonataException("T0410", "$parseInteger expects at least 2 arguments", 0);
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

            if (XPathDateTimeFormatter.TryParseInteger(str, picture, out long value))
            {
                return new Sequence(FunctionalCompiler.CreateNumberElement(value));
            }

            return Sequence.Undefined;
        };
    }

    private static ExpressionEvaluator CompileEval(ExpressionEvaluator[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            throw new JsonataException("T0410", "$eval expects 1 or 2 arguments", 0);
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
                throw new JsonataException("T0410", "$eval expects a string argument", 0);
            }

            string expr = el.GetString() ?? string.Empty;

            Ast.JsonataNode ast;
            try
            {
                ast = Parser.Parse(expr);
            }
            catch (JsonataException ex)
            {
                throw new JsonataException("D3120", $"Syntax error in expression passed to $eval: {ex.Message}", 0);
            }

            ExpressionEvaluator compiled;
            try
            {
                compiled = FunctionalCompiler.Compile(ast);
            }
            catch (JsonataException ex)
            {
                throw new JsonataException("D3121", $"Dynamic error in expression passed to $eval: {ex.Message}", 0);
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
                    var elements = new List<JsonElement>();
                    foreach (var item in ctxSeq)
                    {
                        elements.Add(item);
                    }

                    evalInput = FunctionalCompiler.CreateArrayElement(elements);
                }
            }

            try
            {
                return compiled(evalInput, env);
            }
            catch (JsonataException ex)
            {
                throw new JsonataException("D3121", $"Dynamic error in expression passed to $eval: {ex.Message}", 0);
            }
        };
    }

    // --- Helpers ---
    private delegate void NumericAccumulator(ref double accumulator, double value);

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
            throw new JsonataException("T0412", "Argument 1 of aggregate function must be an array of numbers", 0);
        }
    }

    private static void WriteSequenceToArray(Sequence seq, Utf8JsonWriter writer)
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
                    item.WriteTo(writer);
                }
            }
            else
            {
                el.WriteTo(writer);
            }
        }
        else
        {
            for (int i = 0; i < seq.Count; i++)
            {
                seq[i].WriteTo(writer);
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
    /// Performs a stable sort on a list using the given comparison function.
    /// .NET's List&lt;T&gt;.Sort is not guaranteed stable; this preserves
    /// the relative order of elements that compare equal.
    /// </summary>
    private static void StableSort(List<JsonElement> list, Comparison<JsonElement> comparison)
    {
        // Pair each element with its original index to break ties
        var indexed = new (JsonElement Element, int Index)[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            indexed[i] = (list[i], i);
        }

        Array.Sort(indexed, (a, b) =>
        {
            int cmp = comparison(a.Element, b.Element);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        });

        for (int i = 0; i < list.Count; i++)
        {
            list[i] = indexed[i].Element;
        }
    }
}