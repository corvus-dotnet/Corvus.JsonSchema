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
        if (args.Length < 1)
        {
            throw new JsonataException("T0410", "$string expects at least 1 argument", 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            string result = FunctionalCompiler.CoerceElementToString(seq.FirstOrDefault);
            return new Sequence(FunctionalCompiler.CreateStringElement(result));
        };
    }

    private static ExpressionEvaluator CompileToNumber(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$number expects 1 argument", 0);
        }

        var arg = args[0];
        return (in JsonElement input, Environment env) =>
        {
            var seq = arg(input, env);
            if (seq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            var element = seq.FirstOrDefault;

            // Reject types that cannot be converted to number
            if (element.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                throw new JsonataException("T0410", "Argument 1 of function $number is not of the correct type", 0);
            }

            if (seq.IsLambda)
            {
                throw new JsonataException("T0410", "Argument 1 of function $number is not of the correct type", 0);
            }

            if (FunctionalCompiler.TryCoerceToNumber(element, out double num))
            {
                return new Sequence(FunctionalCompiler.CreateNumberElement(num));
            }

            // String that can't be parsed as a number
            if (element.ValueKind == JsonValueKind.String)
            {
                throw new JsonataException("D3030", "Unable to cast value to a number", 0);
            }

            return Sequence.Undefined;
        };
    }

    private static ExpressionEvaluator CompileToBoolean(ExpressionEvaluator[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$boolean expects 1 argument", 0);
        }

        var arg = args[0];
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
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "$length expects 1 argument", 0);
        }

        var arg = args[0];
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
                int len = el.GetString()?.Length ?? 0;
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

            int start = (int)startD;
            if (start < 0)
            {
                start = Math.Max(0, str.Length + start);
            }

            start = Math.Min(start, str.Length);

            string result;
            if (lengthArg is not null)
            {
                var lenSeq = lengthArg(input, env);
                if (!FunctionalCompiler.TryCoerceToNumber(lenSeq.FirstOrDefault, out double lenD))
                {
                    return Sequence.Undefined;
                }

                int len = Math.Max(0, (int)lenD);
                len = Math.Min(len, str.Length - start);
                result = str.Substring(start, len);
            }
            else
            {
                result = str.Substring(start);
            }

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
        if (args.Length != 1)
        {
            throw new JsonataException("T0410", "String function expects 1 argument", 0);
        }

        var arg = args[0];
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
                separator = FunctionalCompiler.CoerceToString(sepSeq);
            }

            var values = new List<string>();
            var arr = arrSeq.FirstOrDefault;
            if (arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    values.Add(FunctionalCompiler.CoerceElementToString(item));
                }
            }
            else
            {
                for (int i = 0; i < arrSeq.Count; i++)
                {
                    values.Add(FunctionalCompiler.CoerceElementToString(arrSeq[i]));
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
            if (seq.IsUndefined || seq.FirstOrDefault.ValueKind != JsonValueKind.Object)
            {
                return Sequence.Undefined;
            }

            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartArray();
            foreach (var prop in seq.FirstOrDefault.EnumerateObject())
            {
                writer.WriteStringValue(prop.Name);
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

            var arr = seq.FirstOrDefault;
            if (arr.ValueKind != JsonValueKind.Array)
            {
                return seq;
            }

            var elements = new List<JsonElement>();
            elements.AddRange(arr.EnumerateArray());

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
                        var result = lambda.Invoke(new[] { aSeq, bSeq }, sortInput, env);
                        if (result.IsSingleton && FunctionalCompiler.TryCoerceToNumber(result.FirstOrDefault, out double num))
                        {
                            return num < 0 ? -1 : (num > 0 ? 1 : 0);
                        }

                        return 0;
                    });
                }
            }
            else
            {
                elements.Sort((a, b) =>
                {
                    if (FunctionalCompiler.TryCoerceToNumber(a, out double na) && FunctionalCompiler.TryCoerceToNumber(b, out double nb))
                    {
                        return na.CompareTo(nb);
                    }

                    return string.CompareOrdinal(
                        FunctionalCompiler.CoerceElementToString(a),
                        FunctionalCompiler.CoerceElementToString(b));
                });
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
        if (args.Length != 2)
        {
            throw new JsonataException("T0410", "$each expects 2 arguments", 0);
        }

        var objArg = args[0];
        var funcArg = args[1];

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

            var obj = seq.FirstOrDefault;
            if (obj.ValueKind != JsonValueKind.Object)
            {
                return Sequence.Undefined;
            }

            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartArray();
            foreach (var prop in obj.EnumerateObject())
            {
                writer.WriteStartObject();
                writer.WritePropertyName(prop.Name);
                prop.Value.WriteTo(writer);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.Flush();
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            return new Sequence(doc.RootElement.Clone());
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
        if (args.Length != 2)
        {
            throw new JsonataException("T0410", "$sift expects 2 arguments", 0);
        }

        var objArg = args[0];
        var funcArg = args[1];

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
            char padChar = ' ';
            if (charArg is not null)
            {
                var charSeq = charArg(input, env);
                if (!charSeq.IsUndefined)
                {
                    string charStr = FunctionalCompiler.CoerceElementToString(charSeq.FirstOrDefault);
                    if (charStr.Length > 0)
                    {
                        padChar = charStr[0];
                    }
                }
            }

            string result = width > 0
                ? str.PadRight(Math.Abs(width), padChar)
                : str.PadLeft(Math.Abs(width), padChar);
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

            return new Sequence(FunctionalCompiler.CreateJsonArrayElement(results));
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
        if (args.Length != 2)
        {
            throw new JsonataException("T0410", "$formatBase expects 2 arguments", 0);
        }

        var numArg = args[0];
        var radixArg = args[1];
        return (in JsonElement input, Environment env) =>
        {
            var numSeq = numArg(input, env);
            var radixSeq = radixArg(input, env);
            if (numSeq.IsUndefined || radixSeq.IsUndefined)
            {
                return Sequence.Undefined;
            }

            if (!FunctionalCompiler.TryCoerceToNumber(numSeq.FirstOrDefault, out double num))
            {
                return Sequence.Undefined;
            }

            if (!FunctionalCompiler.TryCoerceToNumber(radixSeq.FirstOrDefault, out double radix))
            {
                return Sequence.Undefined;
            }

            string result = Convert.ToString((long)num, (int)radix);
            return new Sequence(FunctionalCompiler.CreateStringElement(result));
        };
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
                    if (FunctionalCompiler.TryCoerceToNumber(item, out double num))
                    {
                        action(ref accumulator, num);
                    }
                }
            }
            else if (FunctionalCompiler.TryCoerceToNumber(el, out double num))
            {
                action(ref accumulator, num);
            }
        }
        else
        {
            for (int i = 0; i < seq.Count; i++)
            {
                if (FunctionalCompiler.TryCoerceToNumber(seq[i], out double num))
                {
                    action(ref accumulator, num);
                }
            }
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
}