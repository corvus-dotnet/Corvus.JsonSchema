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
                prettyPrint = FunctionalCompiler.IsTruthy(prettySeq);
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
                    writer.WriteNumberValue(d);
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

            if (seq.IsLambda)
            {
                throw new JsonataException("T0410", "Argument 1 of function $number is not of the correct type", 0);
            }

            if (FunctionalCompiler.TryCoerceToNumber(element, out double num))
            {
                // Overflow to Infinity should be an error
                if (double.IsInfinity(num))
                {
                    throw new JsonataException("D3030", "Unable to cast value to a number", 0);
                }

                return new Sequence(FunctionalCompiler.CreateNumberElement(num));
            }

            // String that can't be parsed as a number
            if (element.ValueKind == JsonValueKind.String)
            {
                // Check for overflow case
                string? s = element.GetString();
                if (s is not null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double overflowCheck) && double.IsInfinity(overflowCheck))
                {
                    throw new JsonataException("D3030", "Unable to cast value to a number", 0);
                }

                throw new JsonataException("D3030", "Unable to cast value to a number", 0);
            }

            return Sequence.Undefined;
        };
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

        var arg = args.Length == 1 ? args[0] : ContextArg;
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

            // $length on non-string should throw type error
            throw new JsonataException("T0410", "$length expects a string argument", 0);
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

            string? str = strSeq.FirstOrDefault.GetString();
            if (str is null || !FunctionalCompiler.TryCoerceToNumber(startSeq.FirstOrDefault, out double startD))
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
                    return Sequence.Undefined;
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
        if (args.Length != 2)
        {
            throw new JsonataException("T0410", "$substringBefore expects 2 arguments", 0);
        }

        return CompileStringSearch(args[0], args[1], before: true);
    }

    private static ExpressionEvaluator CompileSubstringAfter(ExpressionEvaluator[] args)
    {
        if (args.Length != 2)
        {
            throw new JsonataException("T0410", "$substringAfter expects 2 arguments", 0);
        }

        return CompileStringSearch(args[0], args[1], before: false);
    }

    private static ExpressionEvaluator CompileStringSearch(ExpressionEvaluator strArg, ExpressionEvaluator searchArg, bool before)
    {
        return (in JsonElement input, Environment env) =>
        {
            var strSeq = strArg(input, env);
            var searchSeq = searchArg(input, env);

            if (strSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            string? str = strSeq.FirstOrDefault.GetString();
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
        return CompileStringTransform(args, s => s.Trim());
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

            string? str = seq.FirstOrDefault.GetString();
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
        if (args.Length < 2 || args.Length > 3)
        {
            throw new JsonataException("T0410", "$split expects 2 or 3 arguments", 0);
        }

        var strArg = args[0];
        var sepArg = args[1];
        var limitArg = args.Length > 2 ? args[2] : null;

        return (in JsonElement input, Environment env) =>
        {
            var strSeq = strArg(input, env);
            var sepSeq = sepArg(input, env);

            if (strSeq.IsUndefined)
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
                var limitSeq = limitArg(input, env);
                if (FunctionalCompiler.TryCoerceToNumber(limitSeq.FirstOrDefault, out double limitD))
                {
                    if (limitD < 0)
                    {
                        throw new JsonataException("D3020", "Third argument of the split function must evaluate to a positive number", 0);
                    }

                    limit = (int)limitD;
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

                parts = str.Split(new[] { sep }, StringSplitOptions.None);
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
        if (args.Length != 2)
        {
            throw new JsonataException("T0410", "$contains expects 2 arguments", 0);
        }

        var strArg = args[0];
        var searchArg = args[1];

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

    private static ExpressionEvaluator CompileSqrt(ExpressionEvaluator[] args) => CompileMathFunc(args, Math.Sqrt);

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
                result = Math.Round(num, Math.Min(precision, 15), MidpointRounding.ToEven);
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

            return new Sequence(FunctionalCompiler.CreateNumberElement(Math.Pow(baseNum, expNum)));
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

            // Collect elements from array, sequence, or wrap singleton
            var elements = new List<JsonElement>();
            var arr = seq.FirstOrDefault;
            if (arr.ValueKind == JsonValueKind.Array)
            {
                elements.AddRange(arr.EnumerateArray());
            }
            else if (seq.Count > 1)
            {
                for (int i = 0; i < seq.Count; i++)
                {
                    elements.Add(seq[i]);
                }
            }
            else
            {
                // Singleton value — wrap in array
                elements.Add(arr);
            }

            if (funcArg is not null)
            {
                var funcSeq = funcArg(input, env);
                if (funcSeq.IsLambda)
                {
                    var lambda = funcSeq.Lambda!;
                    var sortInput = input;
                    elements.Sort((a, b) =>
                    {
                        var aSeq = new Sequence(a);
                        var bSeq = new Sequence(b);
                        var result = lambda.Invoke([aSeq, bSeq], sortInput, env);
                        if (result.IsUndefined)
                        {
                            return 0;
                        }

                        var el = result.FirstOrDefault;

                        // Boolean comparator: true means a < b (a should come before b)
                        if (el.ValueKind == JsonValueKind.True)
                        {
                            return -1;
                        }

                        if (el.ValueKind == JsonValueKind.False)
                        {
                            return 1;
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

                elements.Sort((a, b) =>
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

            var arr = seq.FirstOrDefault;
            if (arr.ValueKind != JsonValueKind.Array)
            {
                return seq;
            }

            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartArray();
            var seen = new HashSet<string>();
            foreach (var item in arr.EnumerateArray())
            {
                var raw = item.GetRawText();
                if (seen.Add(raw))
                {
                    item.WriteTo(writer);
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

            if (seq.IsSingleton && seq.FirstOrDefault.ValueKind == JsonValueKind.Array)
            {
                var arr = seq.FirstOrDefault;
                int len = arr.GetArrayLength();
                for (int i = 0; i < len; i++)
                {
                    var elemSeq = new Sequence(arr[i]);
                    var idxSeq = new Sequence(FunctionalCompiler.CreateNumberElement(i));
                    var result = lambda.Invoke(new[] { elemSeq, idxSeq, seq }, arr[i], env);
                    builder.AddRange(result);
                }
            }
            else
            {
                for (int i = 0; i < seq.Count; i++)
                {
                    var elemSeq = new Sequence(seq[i]);
                    var idxSeq = new Sequence(FunctionalCompiler.CreateNumberElement(i));
                    var result = lambda.Invoke(new[] { elemSeq, idxSeq, seq }, seq[i], env);
                    builder.AddRange(result);
                }
            }

            // Materialize builder as JSON array
            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartArray();
            var built = builder.ToSequence();
            for (int i = 0; i < built.Count; i++)
            {
                built[i].WriteTo(writer);
            }

            writer.WriteEndArray();
            writer.Flush();
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            return new Sequence(doc.RootElement.Clone());
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
            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartArray();

            if (seq.IsSingleton && seq.FirstOrDefault.ValueKind == JsonValueKind.Array)
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

            for (int i = startIdx; i < elements.Count; i++)
            {
                var elemSeq = new Sequence(elements[i]);
                var idxSeq = new Sequence(FunctionalCompiler.CreateNumberElement(i));
                accumulator = lambda.Invoke(new[] { accumulator, elemSeq, idxSeq }, input, env);
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
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            // For lambdas, return empty string
            if (seq.IsLambda)
            {
                return new Sequence(FunctionalCompiler.CreateStringElement(string.Empty));
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

            var obj = objSeq.FirstOrDefault;
            if (obj.ValueKind != JsonValueKind.Object)
            {
                return Sequence.Undefined;
            }

            string? key = keySeq.FirstOrDefault.GetString();
            if (key is not null && obj.TryGetProperty(key, out var value))
            {
                return new Sequence(value);
            }

            return Sequence.Undefined;
        };
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
                if (elements.Count != 1)
                {
                    throw new JsonataException("D3138", "$single: expected single match", 0);
                }

                return new Sequence(elements[0]);
            }

            var funcSeq = funcArg(input, env);
            if (!funcSeq.IsLambda)
            {
                return Sequence.Undefined;
            }

            var lambda = funcSeq.Lambda!;
            JsonElement? match = null;
            for (int i = 0; i < elements.Count; i++)
            {
                var result = lambda.Invoke(
                    new[] { new Sequence(elements[i]), new Sequence(FunctionalCompiler.CreateNumberElement(i)) },
                    elements[i],
                    env);
                if (FunctionalCompiler.IsTruthy(result))
                {
                    if (match.HasValue)
                    {
                        throw new JsonataException("D3138", "$single: expected single match", 0);
                    }

                    match = elements[i];
                }
            }

            return match.HasValue ? new Sequence(match.Value) : Sequence.Undefined;
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
            foreach (var prop in obj.EnumerateObject())
            {
                var valSeq = new Sequence(prop.Value);
                var keySeq = new Sequence(FunctionalCompiler.CreateStringElement(prop.Name));
                var result = lambda.Invoke(new[] { valSeq, keySeq }, input, env);
                if (FunctionalCompiler.IsTruthy(result))
                {
                    writer.WritePropertyName(prop.Name);
                    prop.Value.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
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
        if (args.Length < 2 || args.Length > 3)
        {
            throw new JsonataException("T0410", "$match expects 2 or 3 arguments", 0);
        }

        var strArg = args[0];
        var patternArg = args[1];
        var limitArg = args.Length > 2 ? args[2] : null;

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
        if (args.Length < 3)
        {
            throw new JsonataException("T0410", "$replace expects 3+ arguments", 0);
        }

        var strArg = args[0];
        var patternArg = args[1];
        var replacementArg = args[2];
        var limitArg = args.Length > 3 ? args[3] : null;

        return (in JsonElement input, Environment env) =>
        {
            var strSeq = strArg(input, env);
            var patSeq = patternArg(input, env);
            var repSeq = replacementArg(input, env);
            if (strSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            if (!patSeq.IsRegex && patSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            bool isLambdaReplacement = repSeq.IsLambda;
            if (!isLambdaReplacement && repSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            string str = FunctionalCompiler.CoerceElementToString(strSeq.FirstOrDefault);
            int limit = int.MaxValue;
            if (limitArg is not null)
            {
                var limSeq = limitArg(input, env);
                if (!limSeq.IsUndefined && FunctionalCompiler.TryCoerceToNumber(limSeq.FirstOrDefault, out double n))
                {
                    limit = (int)n;
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
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$base64encode expects 1 argument", 0);
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
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$base64decode expects 1 argument", 0);
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

            string str = FunctionalCompiler.CoerceElementToString(seq.FirstOrDefault);
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
            return new Sequence(FunctionalCompiler.CreateStringElement(Uri.UnescapeDataString(str)));
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

            string str = FunctionalCompiler.CoerceElementToString(seq.FirstOrDefault);
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
            return new Sequence(FunctionalCompiler.CreateStringElement(Uri.UnescapeDataString(str)));
        };
    }

    // --- Numeric functions: random, formatNumber, formatBase ---
    private static ExpressionEvaluator CompileRandom(ExpressionEvaluator[] args)
    {
        return static (in JsonElement input, Environment env) =>
        {
            return new Sequence(FunctionalCompiler.CreateNumberElement(ThreadRandom.NextDouble()));
        };
    }

    private static ExpressionEvaluator CompileFormatNumber(ExpressionEvaluator[] args)
    {
        if (args.Length != 2)
        {
            throw new JsonataException("T0410", "$formatNumber expects 2 arguments", 0);
        }

        var numArg = args[0];
        var picArg = args[1];
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
            return new Sequence(FunctionalCompiler.CreateStringElement(
                num.ToString(picture, CultureInfo.InvariantCulture)));
        };
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
            if (!FunctionalCompiler.IsTruthy(cond))
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
        if (args.Length < 1)
        {
            throw new JsonataException("T0410", "$fromMillis expects at least 1 argument", 0);
        }

        var msArg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var msSeq = msArg(input, env);
            if (msSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            if (!FunctionalCompiler.TryCoerceToNumber(msSeq.FirstOrDefault, out double millisVal))
            {
                return Sequence.Undefined;
            }

            var dt = DateTimeOffset.FromUnixTimeMilliseconds((long)millisVal);
            return new Sequence(FunctionalCompiler.CreateStringElement(
                dt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)));
        };
    }

    private static ExpressionEvaluator CompileToMillis(ExpressionEvaluator[] args)
    {
        if (args.Length < 1)
        {
            throw new JsonataException("T0410", "$toMillis expects at least 1 argument", 0);
        }

        var strArg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var strSeq = strArg(input, env);
            if (strSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            string str = FunctionalCompiler.CoerceElementToString(strSeq.FirstOrDefault);
            if (DateTimeOffset.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return new Sequence(FunctionalCompiler.CreateNumberElement(dt.ToUnixTimeMilliseconds()));
            }

            return Sequence.Undefined;
        };
    }

    private static ExpressionEvaluator CompileEval(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$eval expects 1 argument", 0);
        }

        var exprArg = args[0];
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

            // Parse and compile on the fly
            var ast = Parser.Parse(expr);
            var compiled = FunctionalCompiler.Compile(ast);
            return compiled(input, env);
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
}