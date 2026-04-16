// <copyright file="Compiler.Functions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Corvus.Text.Json.JMESPath;

/// <summary>
/// JMESPath built-in function implementations.
/// </summary>
internal static partial class Compiler
{
    private static readonly JsonElement TrueElement = JsonElement.ParseValue("true"u8);
    private static readonly JsonElement FalseElement = JsonElement.ParseValue("false"u8);
    private static readonly JsonElement EmptyArrayElement = JsonElement.ParseValue("[]"u8);
    private static readonly JsonElement ZeroElement = JsonElement.ParseValue("0"u8);
    private static readonly JsonElement EmptyStringElement = JsonElement.ParseValue("\"\""u8);

    private static readonly JsonElement TypeString = JsonElement.ParseValue("\"string\""u8);
    private static readonly JsonElement TypeNumber = JsonElement.ParseValue("\"number\""u8);
    private static readonly JsonElement TypeBoolean = JsonElement.ParseValue("\"boolean\""u8);
    private static readonly JsonElement TypeArray = JsonElement.ParseValue("\"array\""u8);
    private static readonly JsonElement TypeObject = JsonElement.ParseValue("\"object\""u8);
    private static readonly JsonElement TypeNull = JsonElement.ParseValue("\"null\""u8);

    // ─── NUMERIC FUNCTIONS ──────────────────────────────────────────
    private static JMESPathEval CompileAbs(JMESPathNode[] args)
    {
        ValidateArity("abs", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireNumber("abs", val);
            return DoubleToElement(Math.Abs(val.GetDouble()), ws);
        };
    }

    private static JMESPathEval CompileAvg(JMESPathNode[] args)
    {
        ValidateArity("avg", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireArray("avg", val);
            int len = val.GetArrayLength();
            if (len == 0)
            {
                return NullElement;
            }

            double sum = 0;
            foreach (JsonElement item in val.EnumerateArray())
            {
                RequireNumber("avg", item);
                sum += item.GetDouble();
            }

            return DoubleToElement(sum / len, ws);
        };
    }

    private static JMESPathEval CompileCeil(JMESPathNode[] args)
    {
        ValidateArity("ceil", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireNumber("ceil", val);
            return DoubleToElement(Math.Ceiling(val.GetDouble()), ws);
        };
    }

    private static JMESPathEval CompileFloor(JMESPathNode[] args)
    {
        ValidateArity("floor", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireNumber("floor", val);
            return DoubleToElement(Math.Floor(val.GetDouble()), ws);
        };
    }

    private static JMESPathEval CompileSum(JMESPathNode[] args)
    {
        ValidateArity("sum", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireArray("sum", val);
            double sum = 0;
            foreach (JsonElement item in val.EnumerateArray())
            {
                RequireNumber("sum", item);
                sum += item.GetDouble();
            }

            return DoubleToElement(sum, ws);
        };
    }

    private static JMESPathEval CompileMax(JMESPathNode[] args)
    {
        ValidateArity("max", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireArray("max", val);
            int len = val.GetArrayLength();
            if (len == 0)
            {
                return NullElement;
            }

            return FindExtremum(val, isMax: true);
        };
    }

    private static JMESPathEval CompileMin(JMESPathNode[] args)
    {
        ValidateArity("min", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireArray("min", val);
            int len = val.GetArrayLength();
            if (len == 0)
            {
                return NullElement;
            }

            return FindExtremum(val, isMax: false);
        };
    }

    private static JMESPathEval CompileToNumber(JMESPathNode[] args)
    {
        ValidateArity("to_number", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            if (val.ValueKind == JsonValueKind.Number)
            {
                return val;
            }

            if (val.ValueKind == JsonValueKind.String)
            {
                string? s = val.GetString();
                if (s is not null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                {
                    return DoubleToElement(d, ws);
                }
            }

            return NullElement;
        };
    }

    // ─── STRING FUNCTIONS ───────────────────────────────────────────
    private static JMESPathEval CompileContains(JMESPathNode[] args)
    {
        ValidateArity("contains", args, 2);
        JMESPathEval evalSubject = CompileNode(args[0]);
        JMESPathEval evalSearch = CompileNode(args[1]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement subject = evalSubject(data, ws);
            JsonElement search = evalSearch(data, ws);

            if (subject.ValueKind == JsonValueKind.String)
            {
                RequireString("contains", search);
                bool found = subject.GetString()!.Contains(search.GetString()!);
                return found ? TrueElement : FalseElement;
            }

            if (subject.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in subject.EnumerateArray())
                {
                    if (DeepEquals(item, search))
                    {
                        return TrueElement;
                    }
                }

                return FalseElement;
            }

            throw new JMESPathException("invalid-type: contains() expects a string or array as the first argument.");
        };
    }

    private static JMESPathEval CompileEndsWith(JMESPathNode[] args)
    {
        ValidateArity("ends_with", args, 2);
        JMESPathEval evalStr = CompileNode(args[0]);
        JMESPathEval evalSuffix = CompileNode(args[1]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement str = evalStr(data, ws);
            JsonElement suffix = evalSuffix(data, ws);
            RequireString("ends_with", str);
            RequireString("ends_with", suffix);
            bool found = str.GetString()!.EndsWith(suffix.GetString()!, StringComparison.Ordinal);
            return found ? TrueElement : FalseElement;
        };
    }

    private static JMESPathEval CompileStartsWith(JMESPathNode[] args)
    {
        ValidateArity("starts_with", args, 2);
        JMESPathEval evalStr = CompileNode(args[0]);
        JMESPathEval evalPrefix = CompileNode(args[1]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement str = evalStr(data, ws);
            JsonElement prefix = evalPrefix(data, ws);
            RequireString("starts_with", str);
            RequireString("starts_with", prefix);
            bool found = str.GetString()!.StartsWith(prefix.GetString()!, StringComparison.Ordinal);
            return found ? TrueElement : FalseElement;
        };
    }

    private static JMESPathEval CompileJoin(JMESPathNode[] args)
    {
        ValidateArity("join", args, 2);
        JMESPathEval evalSep = CompileNode(args[0]);
        JMESPathEval evalArr = CompileNode(args[1]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement sep = evalSep(data, ws);
            JsonElement arr = evalArr(data, ws);
            RequireString("join", sep);
            RequireArray("join", arr);

            string separator = sep.GetString()!;
            StringBuilder sb = new();
            bool first = true;
            foreach (JsonElement item in arr.EnumerateArray())
            {
                RequireString("join", item);
                if (!first)
                {
                    sb.Append(separator);
                }

                sb.Append(item.GetString());
                first = false;
            }

            return StringToElement(sb.ToString());
        };
    }

    private static JMESPathEval CompileLength(JMESPathNode[] args)
    {
        ValidateArity("length", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            int len = val.ValueKind switch
            {
                JsonValueKind.String => GetStringInfoLength(val.GetString()!),
                JsonValueKind.Array => val.GetArrayLength(),
                JsonValueKind.Object => val.GetPropertyCount(),
                _ => throw new JMESPathException("invalid-type: length() expects a string, array, or object argument."),
            };

            return DoubleToElement(len, ws);
        };
    }

    private static JMESPathEval CompileReverse(JMESPathNode[] args)
    {
        ValidateArity("reverse", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            if (val.ValueKind == JsonValueKind.String)
            {
                string s = val.GetString()!;
                StringInfo si = new(s);
                int textLen = si.LengthInTextElements;
                StringBuilder sb = new(s.Length);
                for (int i = textLen - 1; i >= 0; i--)
                {
                    sb.Append(si.SubstringByTextElements(i, 1));
                }

                return StringToElement(sb.ToString());
            }

            if (val.ValueKind == JsonValueKind.Array)
            {
                int len = val.GetArrayLength();
                JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(ws, len);
                JsonElement.Mutable root = doc.RootElement;
                for (int i = len - 1; i >= 0; i--)
                {
                    root.AddItem(val[i]);
                }

                return (JsonElement)root;
            }

            throw new JMESPathException("invalid-type: reverse() expects a string or array argument.");
        };
    }

    // ─── TYPE FUNCTIONS ─────────────────────────────────────────────
    private static JMESPathEval CompileType(JMESPathNode[] args)
    {
        ValidateArity("type", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            return val.ValueKind switch
            {
                JsonValueKind.String => TypeString,
                JsonValueKind.Number => TypeNumber,
                JsonValueKind.True or JsonValueKind.False => TypeBoolean,
                JsonValueKind.Array => TypeArray,
                JsonValueKind.Object => TypeObject,
                _ => TypeNull,
            };
        };
    }

    private static JMESPathEval CompileToString(JMESPathNode[] args)
    {
        ValidateArity("to_string", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            if (val.ValueKind == JsonValueKind.String)
            {
                return val;
            }

            if (val.IsNullOrUndefined())
            {
                return StringToElement("null");
            }

            // Serialize compactly (no whitespace) via Utf8JsonWriter
            using System.IO.MemoryStream ms = new();
            using (Utf8JsonWriter writer = new(ms))
            {
                val.WriteTo(writer);
            }

            return StringToElement(Encoding.UTF8.GetString(ms.ToArray()));
        };
    }

    private static JMESPathEval CompileToArray(JMESPathNode[] args)
    {
        ValidateArity("to_array", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            if (val.ValueKind == JsonValueKind.Array)
            {
                return val;
            }

            JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(ws, 1);
            doc.RootElement.AddItem(val);
            return (JsonElement)doc.RootElement;
        };
    }

    // ─── OBJECT / ARRAY FUNCTIONS ───────────────────────────────────
    private static JMESPathEval CompileKeys(JMESPathNode[] args)
    {
        ValidateArity("keys", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireObject("keys", val);

            List<string> keyList = [];
            foreach (JsonProperty<JsonElement> prop in val.EnumerateObject())
            {
                keyList.Add(prop.Name);
            }

            keyList.Sort(StringComparer.Ordinal);

            JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(ws, keyList.Count);
            JsonElement.Mutable root = doc.RootElement;
            foreach (string key in keyList)
            {
                root.AddItem(StringToElement(key));
            }

            return (JsonElement)root;
        };
    }

    private static JMESPathEval CompileValues(JMESPathNode[] args)
    {
        ValidateArity("values", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireObject("values", val);

            int count = val.GetPropertyCount();
            JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(ws, count);
            JsonElement.Mutable root = doc.RootElement;
            foreach (JsonProperty<JsonElement> prop in val.EnumerateObject())
            {
                root.AddItem(prop.Value);
            }

            return (JsonElement)root;
        };
    }

    private static JMESPathEval CompileMerge(JMESPathNode[] args)
    {
        ValidateMinArity("merge", args, 1);
        JMESPathEval[] evalArgs = new JMESPathEval[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            evalArgs[i] = CompileNode(args[i]);
        }

        return (in JsonElement data, JsonWorkspace ws) =>
        {
            // Collect all key-value pairs, later values overwrite earlier ones
            Dictionary<string, JsonElement> merged = [];
            List<string> keyOrder = [];

            foreach (JMESPathEval evalArg in evalArgs)
            {
                JsonElement val = evalArg(data, ws);
                RequireObject("merge", val);
                foreach (JsonProperty<JsonElement> prop in val.EnumerateObject())
                {
                    string key = prop.Name;
                    if (!merged.ContainsKey(key))
                    {
                        keyOrder.Add(key);
                    }

                    merged[key] = prop.Value;
                }
            }

            JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateObjectBuilder(ws, merged.Count);
            JsonElement.Mutable root = doc.RootElement;
            foreach (string key in keyOrder)
            {
                root.SetProperty(Encoding.UTF8.GetBytes(key), merged[key]);
            }

            return (JsonElement)root;
        };
    }

    private static JMESPathEval CompileSort(JMESPathNode[] args)
    {
        ValidateArity("sort", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireArray("sort", val);
            int len = val.GetArrayLength();
            if (len == 0)
            {
                return EmptyArrayElement;
            }

            // Collect elements and determine type
            JsonElement[] elements = new JsonElement[len];
            for (int i = 0; i < len; i++)
            {
                elements[i] = val[i];
            }

            JsonValueKind kind = elements[0].ValueKind;
            if (kind != JsonValueKind.Number && kind != JsonValueKind.String)
            {
                throw new JMESPathException("invalid-type: sort() expects an array of numbers or strings.");
            }

            // Verify homogeneous types
            for (int i = 1; i < len; i++)
            {
                if (elements[i].ValueKind != kind)
                {
                    throw new JMESPathException("invalid-type: sort() requires all elements to be the same type.");
                }
            }

            if (kind == JsonValueKind.Number)
            {
                Array.Sort(elements, (a, b) => a.GetDouble().CompareTo(b.GetDouble()));
            }
            else
            {
                Array.Sort(elements, (a, b) => string.CompareOrdinal(a.GetString(), b.GetString()));
            }

            JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(ws, len);
            JsonElement.Mutable root = doc.RootElement;
            foreach (JsonElement e in elements)
            {
                root.AddItem(e);
            }

            return (JsonElement)root;
        };
    }

    private static JMESPathEval CompileNotNull(JMESPathNode[] args)
    {
        ValidateMinArity("not_null", args, 1);
        JMESPathEval[] evalArgs = new JMESPathEval[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            evalArgs[i] = CompileNode(args[i]);
        }

        return (in JsonElement data, JsonWorkspace ws) =>
        {
            foreach (JMESPathEval evalArg in evalArgs)
            {
                JsonElement val = evalArg(data, ws);
                if (!val.IsNullOrUndefined())
                {
                    return val;
                }
            }

            return NullElement;
        };
    }

    // ─── EXPRESSION ARGUMENT FUNCTIONS ──────────────────────────────
    private static JMESPathEval CompileMap(JMESPathNode[] args)
    {
        ValidateArity("map", args, 2);
        JMESPathEval exprFn = CompileExpressionArg("map", args[0]);
        JMESPathEval evalArr = CompileNode(args[1]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement arr = evalArr(data, ws);
            RequireArray("map", arr);
            int len = arr.GetArrayLength();
            JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(ws, len);
            JsonElement.Mutable root = doc.RootElement;
            foreach (JsonElement item in arr.EnumerateArray())
            {
                JsonElement mapped = exprFn(item, ws);
                root.AddItem(mapped.IsNullOrUndefined() ? NullElement : mapped);
            }

            return (JsonElement)root;
        };
    }

    private static JMESPathEval CompileSortBy(JMESPathNode[] args)
    {
        ValidateArity("sort_by", args, 2);
        JMESPathEval evalArr = CompileNode(args[0]);
        JMESPathEval exprFn = CompileExpressionArg("sort_by", args[1]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement arr = evalArr(data, ws);
            RequireArray("sort_by", arr);
            int len = arr.GetArrayLength();
            if (len == 0)
            {
                return EmptyArrayElement;
            }

            // Build array of (element, key) pairs
            (JsonElement Element, JsonElement Key)[] pairs = new (JsonElement, JsonElement)[len];
            for (int i = 0; i < len; i++)
            {
                JsonElement item = arr[i];
                JsonElement key = exprFn(item, ws);
                pairs[i] = (item, key);
            }

            // Verify homogeneous key types
            JsonValueKind keyKind = pairs[0].Key.ValueKind;
            if (keyKind != JsonValueKind.Number && keyKind != JsonValueKind.String)
            {
                throw new JMESPathException("invalid-type: sort_by() expression must return numbers or strings.");
            }

            for (int i = 1; i < len; i++)
            {
                if (pairs[i].Key.ValueKind != keyKind)
                {
                    throw new JMESPathException("invalid-type: sort_by() expression must return consistent types.");
                }
            }

            // Stable sort using Array.Sort (which is stable since .NET Core)
            if (keyKind == JsonValueKind.Number)
            {
                Array.Sort(pairs, (a, b) => a.Key.GetDouble().CompareTo(b.Key.GetDouble()));
            }
            else
            {
                Array.Sort(pairs, (a, b) => string.CompareOrdinal(a.Key.GetString(), b.Key.GetString()));
            }

            JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(ws, len);
            JsonElement.Mutable root = doc.RootElement;
            foreach ((JsonElement element, _) in pairs)
            {
                root.AddItem(element);
            }

            return (JsonElement)root;
        };
    }

    private static JMESPathEval CompileMaxBy(JMESPathNode[] args)
    {
        ValidateArity("max_by", args, 2);
        JMESPathEval evalArr = CompileNode(args[0]);
        JMESPathEval exprFn = CompileExpressionArg("max_by", args[1]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement arr = evalArr(data, ws);
            RequireArray("max_by", arr);
            return FindExtremumBy(arr, exprFn, ws, isMax: true);
        };
    }

    private static JMESPathEval CompileMinBy(JMESPathNode[] args)
    {
        ValidateArity("min_by", args, 2);
        JMESPathEval evalArr = CompileNode(args[0]);
        JMESPathEval exprFn = CompileExpressionArg("min_by", args[1]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement arr = evalArr(data, ws);
            RequireArray("min_by", arr);
            return FindExtremumBy(arr, exprFn, ws, isMax: false);
        };
    }

    // ─── VALIDATION HELPERS ─────────────────────────────────────────
    private static void ValidateArity(string name, JMESPathNode[] args, int expected)
    {
        if (args.Length != expected)
        {
            throw new JMESPathException($"invalid-arity: {name}() takes exactly {expected} argument(s), got {args.Length}.");
        }
    }

    private static void ValidateMinArity(string name, JMESPathNode[] args, int min)
    {
        if (args.Length < min)
        {
            throw new JMESPathException($"invalid-arity: {name}() takes at least {min} argument(s), got {args.Length}.");
        }
    }

    private static JMESPathEval CompileExpressionArg(string funcName, JMESPathNode arg)
    {
        if (arg is ExpressionRefNode exprRef)
        {
            return CompileNode(exprRef.Expression);
        }

        throw new JMESPathException($"invalid-type: {funcName}() expects an expression argument (&expr).");
    }

    private static void RequireNumber(string funcName, in JsonElement val)
    {
        if (val.ValueKind != JsonValueKind.Number)
        {
            throw new JMESPathException($"invalid-type: {funcName}() expects a number argument.");
        }
    }

    private static void RequireString(string funcName, in JsonElement val)
    {
        if (val.ValueKind != JsonValueKind.String)
        {
            throw new JMESPathException($"invalid-type: {funcName}() expects a string argument.");
        }
    }

    private static void RequireArray(string funcName, in JsonElement val)
    {
        if (val.ValueKind != JsonValueKind.Array)
        {
            throw new JMESPathException($"invalid-type: {funcName}() expects an array argument.");
        }
    }

    private static void RequireObject(string funcName, in JsonElement val)
    {
        if (val.ValueKind != JsonValueKind.Object)
        {
            throw new JMESPathException($"invalid-type: {funcName}() expects an object argument.");
        }
    }

    // ─── ELEMENT CREATION HELPERS ───────────────────────────────────
    private static JsonElement DoubleToElement(double value, JsonWorkspace workspace)
    {
        Span<byte> buffer = stackalloc byte[32];
        if (Utf8Formatter.TryFormat(value, buffer, out int bytesWritten))
        {
            return JsonElement.ParseValue(buffer.Slice(0, bytesWritten));
        }

        return ZeroElement;
    }

    private static JsonElement StringToElement(string value)
    {
        // JSON-escape and wrap in quotes
        StringBuilder sb = new(value.Length + 8);
        sb.Append('"');
        foreach (char ch in value)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < 0x20)
                    {
                        sb.Append($"\\u{(int)ch:X4}");
                    }
                    else
                    {
                        sb.Append(ch);
                    }

                    break;
            }
        }

        sb.Append('"');
        return JsonElement.ParseValue(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    private static JsonElement BoolElement(bool value) => value ? TrueElement : FalseElement;

    // ─── COMPARISON HELPERS ─────────────────────────────────────────
    private static JsonElement FindExtremum(in JsonElement array, bool isMax)
    {
        JsonElement best = array[0];
        JsonValueKind kind = best.ValueKind;

        if (kind != JsonValueKind.Number && kind != JsonValueKind.String)
        {
            throw new JMESPathException("invalid-type: max()/min() expects an array of numbers or strings.");
        }

        int len = array.GetArrayLength();
        for (int i = 1; i < len; i++)
        {
            JsonElement item = array[i];
            if (item.ValueKind != kind)
            {
                throw new JMESPathException("invalid-type: max()/min() requires all elements to be the same type.");
            }

            int cmp = kind == JsonValueKind.Number
                ? item.GetDouble().CompareTo(best.GetDouble())
                : string.CompareOrdinal(item.GetString(), best.GetString());

            if (isMax ? cmp > 0 : cmp < 0)
            {
                best = item;
            }
        }

        return best;
    }

    private static JsonElement FindExtremumBy(
        in JsonElement array,
        JMESPathEval exprFn,
        JsonWorkspace ws,
        bool isMax)
    {
        int len = array.GetArrayLength();
        if (len == 0)
        {
            return NullElement;
        }

        JsonElement bestElement = array[0];
        JsonElement bestKey = exprFn(bestElement, ws);
        JsonValueKind keyKind = bestKey.ValueKind;

        if (keyKind != JsonValueKind.Number && keyKind != JsonValueKind.String)
        {
            throw new JMESPathException("invalid-type: max_by()/min_by() expression must return numbers or strings.");
        }

        for (int i = 1; i < len; i++)
        {
            JsonElement item = array[i];
            JsonElement key = exprFn(item, ws);
            if (key.ValueKind != keyKind)
            {
                throw new JMESPathException("invalid-type: max_by()/min_by() expression must return consistent types.");
            }

            int cmp = keyKind == JsonValueKind.Number
                ? key.GetDouble().CompareTo(bestKey.GetDouble())
                : string.CompareOrdinal(key.GetString(), bestKey.GetString());

            if (isMax ? cmp > 0 : cmp < 0)
            {
                bestElement = item;
                bestKey = key;
            }
        }

        return bestElement;
    }

    /// <summary>
    /// Gets the length of a string in text elements (grapheme clusters),
    /// matching JMESPath's Unicode-aware string length semantics.
    /// </summary>
    private static int GetStringInfoLength(string s)
    {
        StringInfo si = new(s);
        return si.LengthInTextElements;
    }
}
