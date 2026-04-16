// <copyright file="JMESPathCodeGenHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Corvus.Text.Json.JMESPath;

/// <summary>
/// Public helper methods used by JMESPath source-generated evaluation code.
/// </summary>
/// <remarks>
/// <para>
/// These methods are called from code emitted by <c>JMESPathCodeGenerator</c>
/// and <c>JMESPathSourceGenerator</c>. They provide the same semantics as the
/// runtime <see cref="Compiler"/> — both share the same implementations to
/// guarantee identical behaviour.
/// </para>
/// <para>
/// This class is not intended for direct use by application code.
/// </para>
/// </remarks>
public static class JMESPathCodeGenHelpers
{
    /// <summary>
    /// A delegate that evaluates a JMESPath (sub-)expression against a data context.
    /// Used for expression-reference arguments (<c>&amp;expr</c>) in generated code.
    /// </summary>
    /// <param name="data">The current data context.</param>
    /// <param name="workspace">The workspace for intermediate document allocation.</param>
    /// <returns>The resulting JSON element.</returns>
    public delegate JsonElement ExpressionEvaluator(in JsonElement data, JsonWorkspace workspace);

    /// <summary>Gets a pre-parsed <c>null</c> element.</summary>
    public static readonly JsonElement NullElement = JsonElement.ParseValue("null"u8);

    /// <summary>Gets a pre-parsed <c>true</c> element.</summary>
    public static readonly JsonElement TrueElement = JsonElement.ParseValue("true"u8);

    /// <summary>Gets a pre-parsed <c>false</c> element.</summary>
    public static readonly JsonElement FalseElement = JsonElement.ParseValue("false"u8);

    /// <summary>Gets a pre-parsed empty array element.</summary>
    public static readonly JsonElement EmptyArrayElement = JsonElement.ParseValue("[]"u8);

    /// <summary>Gets a pre-parsed zero element.</summary>
    public static readonly JsonElement ZeroElement = JsonElement.ParseValue("0"u8);

    /// <summary>Gets a pre-parsed empty string element.</summary>
    public static readonly JsonElement EmptyStringElement = JsonElement.ParseValue("\"\""u8);

    /// <summary>Gets a pre-parsed <c>"string"</c> type element.</summary>
    public static readonly JsonElement TypeString = JsonElement.ParseValue("\"string\""u8);

    /// <summary>Gets a pre-parsed <c>"number"</c> type element.</summary>
    public static readonly JsonElement TypeNumber = JsonElement.ParseValue("\"number\""u8);

    /// <summary>Gets a pre-parsed <c>"boolean"</c> type element.</summary>
    public static readonly JsonElement TypeBoolean = JsonElement.ParseValue("\"boolean\""u8);

    /// <summary>Gets a pre-parsed <c>"array"</c> type element.</summary>
    public static readonly JsonElement TypeArray = JsonElement.ParseValue("\"array\""u8);

    /// <summary>Gets a pre-parsed <c>"object"</c> type element.</summary>
    public static readonly JsonElement TypeObject = JsonElement.ParseValue("\"object\""u8);

    /// <summary>Gets a pre-parsed <c>"null"</c> type element.</summary>
    public static readonly JsonElement TypeNull = JsonElement.ParseValue("\"null\""u8);

    /// <summary>
    /// Determines whether a JSON element is "truthy" per JMESPath semantics.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns><see langword="true"/> if the value is truthy; otherwise <see langword="false"/>.</returns>
    public static bool IsTruthy(in JsonElement value)
    {
        if (value.IsNullOrUndefined())
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.False => false,
            JsonValueKind.True => true,
            JsonValueKind.Null => false,
            JsonValueKind.Number => true,
            JsonValueKind.String => value.GetString()?.Length > 0,
            JsonValueKind.Array => value.GetArrayLength() > 0,
            JsonValueKind.Object => value.GetPropertyCount() > 0,
            _ => false,
        };
    }

    /// <summary>
    /// Performs a deep-equality comparison of two JSON elements per JMESPath semantics.
    /// </summary>
    /// <param name="left">The left element.</param>
    /// <param name="right">The right element.</param>
    /// <returns><see langword="true"/> if the elements are deeply equal.</returns>
    public static bool DeepEquals(in JsonElement left, in JsonElement right)
    {
        if (left.IsNullOrUndefined() && right.IsNullOrUndefined())
        {
            return true;
        }

        if (left.IsNullOrUndefined() || right.IsNullOrUndefined())
        {
            return false;
        }

        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        return left.ValueKind switch
        {
            JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => true,
            JsonValueKind.Number => left.GetDouble() == right.GetDouble(),
            JsonValueKind.String => string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal),
            JsonValueKind.Array => ArrayEquals(left, right),
            JsonValueKind.Object => ObjectEquals(left, right),
            _ => false,
        };
    }

    /// <summary>
    /// Returns <see cref="TrueElement"/> or <see cref="FalseElement"/>.
    /// </summary>
    /// <param name="value">The boolean value.</param>
    /// <returns>The corresponding JSON element.</returns>
    public static JsonElement BoolElement(bool value) => value ? TrueElement : FalseElement;

    /// <summary>
    /// Converts a <see cref="double"/> to a JSON number element.
    /// </summary>
    /// <param name="value">The numeric value.</param>
    /// <param name="workspace">The workspace (unused, reserved for future use).</param>
    /// <returns>A JSON number element.</returns>
    public static JsonElement DoubleToElement(double value, JsonWorkspace workspace)
    {
        Span<byte> buffer = stackalloc byte[32];
        if (Utf8Formatter.TryFormat(value, buffer, out int bytesWritten))
        {
            return JsonElement.ParseValue(buffer.Slice(0, bytesWritten));
        }

        return ZeroElement;
    }

    /// <summary>
    /// Converts a string to a JSON string element.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <returns>A JSON string element.</returns>
    public static JsonElement StringToElement(string value)
    {
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

    /// <summary>
    /// Normalizes a slice index per JMESPath spec.
    /// </summary>
    /// <param name="index">The raw index value.</param>
    /// <param name="length">The array length.</param>
    /// <param name="isStart">Whether this is the start index.</param>
    /// <param name="positiveStep">Whether the step is positive.</param>
    /// <returns>The normalized index.</returns>
    public static int NormalizeSliceIndex(int index, int length, bool isStart, bool positiveStep)
    {
        if (index < 0)
        {
            index += length;
            if (index < 0)
            {
                index = positiveStep ? 0 : -1;
            }
        }
        else
        {
            if (positiveStep)
            {
                if (index > length)
                {
                    index = length;
                }
            }
            else
            {
                if (isStart && index > length - 1)
                {
                    index = length - 1;
                }
                else if (!isStart && index > length)
                {
                    index = length;
                }
            }
        }

        return index;
    }

    // ─── NUMERIC FUNCTIONS ──────────────────────────────────────────

    /// <summary>Computes <c>abs(value)</c>.</summary>
    public static JsonElement Abs(in JsonElement value, JsonWorkspace workspace)
    {
        RequireNumber("abs", value);
        return DoubleToElement(Math.Abs(value.GetDouble()), workspace);
    }

    /// <summary>Computes <c>ceil(value)</c>.</summary>
    public static JsonElement Ceil(in JsonElement value, JsonWorkspace workspace)
    {
        RequireNumber("ceil", value);
        return DoubleToElement(Math.Ceiling(value.GetDouble()), workspace);
    }

    /// <summary>Computes <c>floor(value)</c>.</summary>
    public static JsonElement Floor(in JsonElement value, JsonWorkspace workspace)
    {
        RequireNumber("floor", value);
        return DoubleToElement(Math.Floor(value.GetDouble()), workspace);
    }

    /// <summary>Computes <c>avg(array)</c>.</summary>
    public static JsonElement Avg(in JsonElement array, JsonWorkspace workspace)
    {
        RequireArray("avg", array);
        int len = array.GetArrayLength();
        if (len == 0)
        {
            return NullElement;
        }

        double sum = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            RequireNumber("avg", item);
            sum += item.GetDouble();
        }

        return DoubleToElement(sum / len, workspace);
    }

    /// <summary>Computes <c>sum(array)</c>.</summary>
    public static JsonElement Sum(in JsonElement array, JsonWorkspace workspace)
    {
        RequireArray("sum", array);
        double sum = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            RequireNumber("sum", item);
            sum += item.GetDouble();
        }

        return DoubleToElement(sum, workspace);
    }

    /// <summary>Computes <c>max(array)</c>.</summary>
    public static JsonElement Max(in JsonElement array)
    {
        RequireArray("max", array);
        if (array.GetArrayLength() == 0)
        {
            return NullElement;
        }

        return FindExtremum(array, isMax: true);
    }

    /// <summary>Computes <c>min(array)</c>.</summary>
    public static JsonElement Min(in JsonElement array)
    {
        RequireArray("min", array);
        if (array.GetArrayLength() == 0)
        {
            return NullElement;
        }

        return FindExtremum(array, isMax: false);
    }

    // ─── STRING FUNCTIONS ───────────────────────────────────────────

    /// <summary>Computes <c>length(value)</c>.</summary>
    public static JsonElement Length(in JsonElement value, JsonWorkspace workspace)
    {
        int len = value.ValueKind switch
        {
            JsonValueKind.String => GetStringInfoLength(value.GetString()!),
            JsonValueKind.Array => value.GetArrayLength(),
            JsonValueKind.Object => value.GetPropertyCount(),
            _ => throw new JMESPathException("invalid-type: length() expects a string, array, or object argument."),
        };

        return DoubleToElement(len, workspace);
    }

    /// <summary>Computes <c>contains(subject, search)</c>.</summary>
    public static JsonElement Contains(in JsonElement subject, in JsonElement search)
    {
        if (subject.ValueKind == JsonValueKind.String)
        {
            RequireString("contains", search);
            return BoolElement(subject.GetString()!.Contains(search.GetString()!));
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
    }

    /// <summary>Computes <c>starts_with(str, prefix)</c>.</summary>
    public static JsonElement StartsWith(in JsonElement str, in JsonElement prefix)
    {
        RequireString("starts_with", str);
        RequireString("starts_with", prefix);
        return BoolElement(str.GetString()!.StartsWith(prefix.GetString()!, StringComparison.Ordinal));
    }

    /// <summary>Computes <c>ends_with(str, suffix)</c>.</summary>
    public static JsonElement EndsWith(in JsonElement str, in JsonElement suffix)
    {
        RequireString("ends_with", str);
        RequireString("ends_with", suffix);
        return BoolElement(str.GetString()!.EndsWith(suffix.GetString()!, StringComparison.Ordinal));
    }

    /// <summary>Computes <c>join(separator, array)</c>.</summary>
    public static JsonElement Join(in JsonElement separator, in JsonElement array)
    {
        RequireString("join", separator);
        RequireArray("join", array);

        string sep = separator.GetString()!;
        StringBuilder sb = new();
        bool first = true;
        foreach (JsonElement item in array.EnumerateArray())
        {
            RequireString("join", item);
            if (!first)
            {
                sb.Append(sep);
            }

            sb.Append(item.GetString());
            first = false;
        }

        return StringToElement(sb.ToString());
    }

    /// <summary>Computes <c>reverse(value)</c> for strings and arrays.</summary>
    public static JsonElement Reverse(in JsonElement value, JsonWorkspace workspace)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            string s = value.GetString()!;
            StringInfo si = new(s);
            int textLen = si.LengthInTextElements;
            StringBuilder sb = new(s.Length);
            for (int i = textLen - 1; i >= 0; i--)
            {
                sb.Append(si.SubstringByTextElements(i, 1));
            }

            return StringToElement(sb.ToString());
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            int len = value.GetArrayLength();
            JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(workspace, len);
            JsonElement.Mutable root = doc.RootElement;
            for (int i = len - 1; i >= 0; i--)
            {
                root.AddItem(value[i]);
            }

            return (JsonElement)root;
        }

        throw new JMESPathException("invalid-type: reverse() expects a string or array argument.");
    }

    // ─── OBJECT / ARRAY FUNCTIONS ───────────────────────────────────

    /// <summary>Computes <c>keys(obj)</c>.</summary>
    public static JsonElement Keys(in JsonElement obj, JsonWorkspace workspace)
    {
        RequireObject("keys", obj);

        List<string> keyList = [];
        foreach (JsonProperty<JsonElement> prop in obj.EnumerateObject())
        {
            keyList.Add(prop.Name);
        }

        keyList.Sort(StringComparer.Ordinal);

        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(workspace, keyList.Count);
        JsonElement.Mutable root = doc.RootElement;
        foreach (string key in keyList)
        {
            root.AddItem(StringToElement(key));
        }

        return (JsonElement)root;
    }

    /// <summary>Computes <c>values(obj)</c>.</summary>
    public static JsonElement Values(in JsonElement obj, JsonWorkspace workspace)
    {
        RequireObject("values", obj);

        int count = obj.GetPropertyCount();
        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(workspace, count);
        JsonElement.Mutable root = doc.RootElement;
        foreach (JsonProperty<JsonElement> prop in obj.EnumerateObject())
        {
            root.AddItem(prop.Value);
        }

        return (JsonElement)root;
    }

    /// <summary>Computes <c>sort(array)</c>.</summary>
    public static JsonElement Sort(in JsonElement array, JsonWorkspace workspace)
    {
        RequireArray("sort", array);
        int len = array.GetArrayLength();
        if (len == 0)
        {
            return EmptyArrayElement;
        }

        JsonElement[] elements = new JsonElement[len];
        for (int i = 0; i < len; i++)
        {
            elements[i] = array[i];
        }

        JsonValueKind kind = elements[0].ValueKind;
        if (kind != JsonValueKind.Number && kind != JsonValueKind.String)
        {
            throw new JMESPathException("invalid-type: sort() expects an array of numbers or strings.");
        }

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

        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(workspace, len);
        JsonElement.Mutable root = doc.RootElement;
        foreach (JsonElement e in elements)
        {
            root.AddItem(e);
        }

        return (JsonElement)root;
    }

    /// <summary>Computes <c>sort_by(array, &amp;expr)</c>.</summary>
    public static JsonElement SortBy(in JsonElement array, ExpressionEvaluator expr, JsonWorkspace workspace)
    {
        RequireArray("sort_by", array);
        int len = array.GetArrayLength();
        if (len == 0)
        {
            return EmptyArrayElement;
        }

        (JsonElement Element, JsonElement Key)[] pairs = new (JsonElement, JsonElement)[len];
        for (int i = 0; i < len; i++)
        {
            JsonElement item = array[i];
            pairs[i] = (item, expr(item, workspace));
        }

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

        if (keyKind == JsonValueKind.Number)
        {
            Array.Sort(pairs, (a, b) => a.Key.GetDouble().CompareTo(b.Key.GetDouble()));
        }
        else
        {
            Array.Sort(pairs, (a, b) => string.CompareOrdinal(a.Key.GetString(), b.Key.GetString()));
        }

        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(workspace, len);
        JsonElement.Mutable root = doc.RootElement;
        foreach ((JsonElement element, _) in pairs)
        {
            root.AddItem(element);
        }

        return (JsonElement)root;
    }

    /// <summary>Computes <c>map(&amp;expr, array)</c>.</summary>
    public static JsonElement Map(ExpressionEvaluator expr, in JsonElement array, JsonWorkspace workspace)
    {
        RequireArray("map", array);
        int len = array.GetArrayLength();
        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(workspace, len);
        JsonElement.Mutable root = doc.RootElement;
        foreach (JsonElement item in array.EnumerateArray())
        {
            JsonElement mapped = expr(item, workspace);
            root.AddItem(mapped.IsNullOrUndefined() ? NullElement : mapped);
        }

        return (JsonElement)root;
    }

    /// <summary>Computes <c>max_by(array, &amp;expr)</c>.</summary>
    public static JsonElement MaxBy(in JsonElement array, ExpressionEvaluator expr, JsonWorkspace workspace)
    {
        RequireArray("max_by", array);
        return FindExtremumBy(array, expr, workspace, isMax: true);
    }

    /// <summary>Computes <c>min_by(array, &amp;expr)</c>.</summary>
    public static JsonElement MinBy(in JsonElement array, ExpressionEvaluator expr, JsonWorkspace workspace)
    {
        RequireArray("min_by", array);
        return FindExtremumBy(array, expr, workspace, isMax: false);
    }

    /// <summary>Computes <c>merge(obj1, obj2, ...)</c>.</summary>
    public static JsonElement Merge(JsonElement[] objects, JsonWorkspace workspace)
    {
        Dictionary<string, JsonElement> merged = [];
        List<string> keyOrder = [];

        foreach (JsonElement val in objects)
        {
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

        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateObjectBuilder(workspace, merged.Count);
        JsonElement.Mutable root = doc.RootElement;
        foreach (string key in keyOrder)
        {
            root.SetProperty(Encoding.UTF8.GetBytes(key), merged[key]);
        }

        return (JsonElement)root;
    }

    /// <summary>Computes <c>not_null(arg1, arg2, ...)</c>.</summary>
    public static JsonElement NotNull(JsonElement[] values)
    {
        foreach (JsonElement val in values)
        {
            if (!val.IsNullOrUndefined())
            {
                return val;
            }
        }

        return NullElement;
    }

    /// <summary>Computes <c>to_array(value)</c>.</summary>
    public static JsonElement ToArray(in JsonElement value, JsonWorkspace workspace)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            return value;
        }

        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(workspace, 1);
        doc.RootElement.AddItem(value);
        return (JsonElement)doc.RootElement;
    }

    /// <summary>Computes <c>to_number(value)</c>.</summary>
    public static JsonElement ToNumber(in JsonElement value, JsonWorkspace workspace)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            return value;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            string? s = value.GetString();
            if (s is not null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            {
                return DoubleToElement(d, workspace);
            }
        }

        return NullElement;
    }

    /// <summary>Computes <c>to_string(value)</c>.</summary>
    public static JsonElement ToString(in JsonElement value, JsonWorkspace workspace)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return value;
        }

        if (value.IsNullOrUndefined())
        {
            return StringToElement("null");
        }

        using System.IO.MemoryStream ms = new();
        using (Utf8JsonWriter writer = new(ms))
        {
            value.WriteTo(writer);
        }

        return StringToElement(Encoding.UTF8.GetString(ms.ToArray()));
    }

    /// <summary>Computes <c>type(value)</c>.</summary>
    public static JsonElement TypeOf(in JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => TypeString,
            JsonValueKind.Number => TypeNumber,
            JsonValueKind.True or JsonValueKind.False => TypeBoolean,
            JsonValueKind.Array => TypeArray,
            JsonValueKind.Object => TypeObject,
            _ => TypeNull,
        };
    }

    // ─── VALIDATION HELPERS ─────────────────────────────────────────

    /// <summary>Asserts that <paramref name="value"/> is a JSON number.</summary>
    public static void RequireNumber(string funcName, in JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new JMESPathException($"invalid-type: {funcName}() expects a number argument.");
        }
    }

    /// <summary>Asserts that <paramref name="value"/> is a JSON string.</summary>
    public static void RequireString(string funcName, in JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new JMESPathException($"invalid-type: {funcName}() expects a string argument.");
        }
    }

    /// <summary>Asserts that <paramref name="value"/> is a JSON array.</summary>
    public static void RequireArray(string funcName, in JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new JMESPathException($"invalid-type: {funcName}() expects an array argument.");
        }
    }

    /// <summary>Asserts that <paramref name="value"/> is a JSON object.</summary>
    public static void RequireObject(string funcName, in JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new JMESPathException($"invalid-type: {funcName}() expects an object argument.");
        }
    }

    // ─── PRIVATE HELPERS ────────────────────────────────────────────
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
        ExpressionEvaluator expr,
        JsonWorkspace workspace,
        bool isMax)
    {
        int len = array.GetArrayLength();
        if (len == 0)
        {
            return NullElement;
        }

        JsonElement bestElement = array[0];
        JsonElement bestKey = expr(bestElement, workspace);
        JsonValueKind keyKind = bestKey.ValueKind;

        if (keyKind != JsonValueKind.Number && keyKind != JsonValueKind.String)
        {
            throw new JMESPathException("invalid-type: max_by()/min_by() expression must return numbers or strings.");
        }

        for (int i = 1; i < len; i++)
        {
            JsonElement item = array[i];
            JsonElement key = expr(item, workspace);
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

    private static bool ArrayEquals(in JsonElement left, in JsonElement right)
    {
        int len = left.GetArrayLength();
        if (len != right.GetArrayLength())
        {
            return false;
        }

        for (int i = 0; i < len; i++)
        {
            if (!DeepEquals(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ObjectEquals(in JsonElement left, in JsonElement right)
    {
        if (left.GetPropertyCount() != right.GetPropertyCount())
        {
            return false;
        }

        foreach (JsonProperty<JsonElement> prop in left.EnumerateObject())
        {
            if (!right.TryGetProperty(prop.Name, out JsonElement rightVal)
                || !DeepEquals(prop.Value, rightVal))
            {
                return false;
            }
        }

        return true;
    }

    private static int GetStringInfoLength(string s)
    {
        StringInfo si = new(s);
        return si.LengthInTextElements;
    }
}
