// <copyright file="JsonataCodeGenHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Public helper methods for code-generated JSONata evaluators.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the runtime operations that generated JSONata evaluation code
/// calls. It bridges between the public <see cref="JsonElement"/> API and the
/// internal JSONata evaluation machinery.
/// </para>
/// <para>
/// Generated code emitted by <c>JsonataCodeGenerator</c> calls these helpers for
/// operations that are too complex to inline (path navigation with auto-flattening,
/// arithmetic with type checking, string coercion, etc.). Simple operations like
/// literal creation and property access are emitted inline.
/// </para>
/// </remarks>
public static class JsonataCodeGenHelpers
{
    private static readonly JsonataEvaluator SharedEvaluator = new();

    /// <summary>
    /// Gets a cached <see cref="JsonElement"/> representing JSON <c>true</c>.
    /// </summary>
    public static JsonElement True
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => JsonataHelpers.True();
    }

    /// <summary>
    /// Gets a cached <see cref="JsonElement"/> representing JSON <c>false</c>.
    /// </summary>
    public static JsonElement False
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => JsonataHelpers.False();
    }

    /// <summary>
    /// Gets a cached <see cref="JsonElement"/> representing JSON <c>null</c>.
    /// </summary>
    public static JsonElement Null
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => JsonataHelpers.Null();
    }

    /// <summary>
    /// Gets a cached <see cref="JsonElement"/> representing JSON <c>0</c>.
    /// </summary>
    public static JsonElement Zero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => JsonataHelpers.Zero();
    }

    /// <summary>
    /// Gets a cached <see cref="JsonElement"/> representing JSON <c>1</c>.
    /// </summary>
    public static JsonElement One
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => JsonataHelpers.One();
    }

    /// <summary>
    /// Gets a cached <see cref="JsonElement"/> representing JSON <c>""</c>.
    /// </summary>
    public static JsonElement EmptyString
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => JsonataHelpers.EmptyString();
    }

    /// <summary>
    /// Converts a string to a UTF-8 byte array.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>The UTF-8 encoded bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Utf8(string value) => System.Text.Encoding.UTF8.GetBytes(value);

    /// <summary>
    /// Creates a <see cref="JsonElement"/> representing the given boolean value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement BooleanElement(bool value) => value ? JsonataHelpers.True() : JsonataHelpers.False();

    /// <summary>
    /// Creates a <see cref="JsonElement"/> number from a double value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement NumberFromDouble(double value, JsonWorkspace workspace) =>
        JsonataHelpers.NumberFromDouble(value, workspace);

    /// <summary>
    /// Parses a JSON number from its raw UTF-8 representation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement ParseNumber(ReadOnlySpan<byte> utf8Json) =>
        JsonElement.ParseValue(utf8Json);

    // ===== Navigation =====

    /// <summary>
    /// Navigates a chain of property names with JSONata auto-flattening semantics.
    /// </summary>
    /// <remarks>
    /// At each step, if the current value is an object, the property is looked up directly.
    /// If the current value is an array, the property is looked up on each element and
    /// results are collected with auto-flattening (nested arrays are expanded).
    /// </remarks>
    /// <param name="data">The input data element.</param>
    /// <param name="names">The UTF-8 encoded property names to navigate.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>The navigation result, or <c>default</c> if undefined.</returns>
    public static JsonElement NavigatePropertyChain(in JsonElement data, byte[][] names, JsonWorkspace workspace)
    {
        return NavigatePropertyChainCore(data, names, 0, workspace);
    }

    /// <summary>
    /// Navigates a single property with auto-flattening over arrays.
    /// </summary>
    /// <param name="data">The input data element.</param>
    /// <param name="name">The UTF-8 encoded property name.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>The navigation result, or <c>default</c> if undefined.</returns>
    public static JsonElement NavigateProperty(in JsonElement data, byte[] name, JsonWorkspace workspace)
    {
        if (data.ValueKind == JsonValueKind.Object)
        {
            return data.TryGetProperty((ReadOnlySpan<byte>)name, out JsonElement result) ? result : default;
        }

        if (data.ValueKind == JsonValueKind.Array)
        {
            return NavigatePropertyOverArray(data, name, workspace);
        }

        return default;
    }

    /// <summary>
    /// Gets an element from an array by index, supporting negative indices (from end).
    /// </summary>
    /// <param name="data">The array element.</param>
    /// <param name="index">The index (negative counts from end).</param>
    /// <returns>The element at the index, or <c>default</c> if out of range.</returns>
    public static JsonElement ArrayIndex(in JsonElement data, int index)
    {
        if (data.ValueKind != JsonValueKind.Array)
        {
            return default;
        }

        int len = data.GetArrayLength();
        if (len == 0)
        {
            return default;
        }

        int actualIndex = index < 0 ? len + index : index;
        if (actualIndex < 0 || actualIndex >= len)
        {
            return default;
        }

        return data[actualIndex];
    }

    /// <summary>
    /// Applies a step function over each element of a multi-valued result,
    /// collecting results with JSONata auto-flattening.
    /// </summary>
    /// <param name="data">The input (array or single value).</param>
    /// <param name="step">The step function to apply to each element.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>The collected results, or <c>default</c> if undefined.</returns>
    public static JsonElement ApplyStepOverElements(
        in JsonElement data,
        Func<JsonElement, JsonWorkspace, JsonElement> step,
        JsonWorkspace workspace)
    {
        if (data.ValueKind == JsonValueKind.Array)
        {
            int count = 0;
            var doc = JsonElement.CreateArrayBuilder(workspace, data.GetArrayLength());
            JsonElement.Mutable root = doc.RootElement;

            foreach (JsonElement item in data.EnumerateArray())
            {
                JsonElement result = step(item, workspace);
                count = AddResultWithFlatten(root, result, count);
            }

            return count == 0 ? default : count == 1 ? root[0] : (JsonElement)root;
        }

        if (data.ValueKind != JsonValueKind.Undefined)
        {
            return step(data, workspace);
        }

        return default;
    }

    // ===== Arithmetic =====

    /// <summary>
    /// JSONata addition (+) operator.
    /// </summary>
    public static JsonElement Add(in JsonElement left, in JsonElement right, JsonWorkspace workspace) =>
        BinaryArithmetic(left, right, workspace, "+", static (a, b) => a + b);

    /// <summary>
    /// JSONata subtraction (-) operator.
    /// </summary>
    public static JsonElement Subtract(in JsonElement left, in JsonElement right, JsonWorkspace workspace) =>
        BinaryArithmetic(left, right, workspace, "-", static (a, b) => a - b);

    /// <summary>
    /// JSONata multiplication (*) operator.
    /// </summary>
    public static JsonElement Multiply(in JsonElement left, in JsonElement right, JsonWorkspace workspace) =>
        BinaryArithmetic(left, right, workspace, "*", static (a, b) => a * b);

    /// <summary>
    /// JSONata division (/) operator.
    /// </summary>
    public static JsonElement Divide(in JsonElement left, in JsonElement right, JsonWorkspace workspace) =>
        BinaryArithmetic(left, right, workspace, "/", static (a, b) => a / b);

    /// <summary>
    /// JSONata modulo (%) operator.
    /// </summary>
    public static JsonElement Modulo(in JsonElement left, in JsonElement right, JsonWorkspace workspace) =>
        BinaryArithmetic(left, right, workspace, "%", static (a, b) => a % b);

    /// <summary>
    /// JSONata unary negation (-) operator.
    /// </summary>
    public static JsonElement Negate(in JsonElement value, JsonWorkspace workspace)
    {
        if (value.IsNullOrUndefined())
        {
            return default;
        }

        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new JsonataException("D3001", "Attempted to negate a non-numeric value", 0);
        }

        return JsonataHelpers.NumberFromDouble(-value.GetDouble(), workspace);
    }

    // ===== Comparison =====

    /// <summary>
    /// JSONata equality (=) operator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreEqual(in JsonElement left, in JsonElement right)
    {
        if (left.IsNullOrUndefined() || right.IsNullOrUndefined())
        {
            return false;
        }

        return ElementEquals(left, right);
    }

    /// <summary>
    /// JSONata inequality (!=) operator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreNotEqual(in JsonElement left, in JsonElement right)
    {
        if (left.IsNullOrUndefined() && right.IsNullOrUndefined())
        {
            return false;
        }

        if (left.IsNullOrUndefined() || right.IsNullOrUndefined())
        {
            return true;
        }

        return !ElementEquals(left, right);
    }

    /// <summary>
    /// JSONata less-than (&lt;) operator.
    /// </summary>
    public static bool LessThan(in JsonElement left, in JsonElement right) =>
        NumericCompare(left, right, "<") < 0;

    /// <summary>
    /// JSONata less-than-or-equal (&lt;=) operator.
    /// </summary>
    public static bool LessThanOrEqual(in JsonElement left, in JsonElement right) =>
        NumericCompare(left, right, "<=") <= 0;

    /// <summary>
    /// JSONata greater-than (&gt;) operator.
    /// </summary>
    public static bool GreaterThan(in JsonElement left, in JsonElement right) =>
        NumericCompare(left, right, ">") > 0;

    /// <summary>
    /// JSONata greater-than-or-equal (&gt;=) operator.
    /// </summary>
    public static bool GreaterThanOrEqual(in JsonElement left, in JsonElement right) =>
        NumericCompare(left, right, ">=") >= 0;

    /// <summary>
    /// JSONata <c>in</c> operator — tests whether the left value is contained in the right array.
    /// </summary>
    public static bool In(in JsonElement left, in JsonElement right)
    {
        if (right.ValueKind != JsonValueKind.Array)
        {
            return AreEqual(left, right);
        }

        foreach (JsonElement item in right.EnumerateArray())
        {
            if (AreEqual(left, item))
            {
                return true;
            }
        }

        return false;
    }

    // ===== String Operations =====

    /// <summary>
    /// JSONata string concatenation (&amp;) operator. Coerces both operands to strings.
    /// </summary>
    public static JsonElement StringConcat(in JsonElement left, in JsonElement right, JsonWorkspace workspace)
    {
        // In JSONata, the & operator coerces both operands to strings and concatenates.
        // undefined → ""
        string leftStr = CoerceToStringValue(left);
        string rightStr = CoerceToStringValue(right);
        string result = string.Concat(leftStr, rightStr);
        return JsonataHelpers.StringFromString(result, workspace);
    }

    /// <summary>
    /// Coerces a <see cref="JsonElement"/> to a string per JSONata semantics.
    /// </summary>
    public static JsonElement CoerceToStringElement(in JsonElement value, JsonWorkspace workspace)
    {
        if (value.IsNullOrUndefined())
        {
            return default;
        }

        string str = CoerceToStringValue(value);
        return JsonataHelpers.StringFromString(str, workspace);
    }

    // ===== Truthiness =====

    /// <summary>
    /// Determines whether a <see cref="JsonElement"/> is truthy per JSONata semantics.
    /// </summary>
    /// <remarks>
    /// Falsy values: undefined, null, false, 0, "", NaN.
    /// All other values (including empty arrays and objects) are truthy.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTruthy(in JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Undefined => false,
            JsonValueKind.Null => false,
            JsonValueKind.False => false,
            JsonValueKind.True => true,
            JsonValueKind.Number => value.GetDouble() != 0.0,
            JsonValueKind.String => !value.ValueEquals(ReadOnlySpan<byte>.Empty),
            JsonValueKind.Array => true,
            JsonValueKind.Object => true,
            _ => false,
        };
    }

    // ===== Built-in Functions =====

    /// <summary>
    /// JSONata <c>$sum</c> function — sums numeric values in an array.
    /// </summary>
    public static JsonElement Sum(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        double sum = 0;

        if (input.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in input.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number)
                {
                    sum += item.GetDouble();
                }
                else if (!item.IsNullOrUndefined())
                {
                    throw new JsonataException("T2001", "The $sum function expects an array of numbers", 0);
                }
            }
        }
        else if (input.ValueKind == JsonValueKind.Number)
        {
            sum = input.GetDouble();
        }
        else
        {
            throw new JsonataException("T2001", "The $sum function expects an array of numbers", 0);
        }

        return JsonataHelpers.NumberFromDouble(sum, workspace);
    }

    /// <summary>
    /// JSONata <c>$count</c> function — counts elements in an array.
    /// </summary>
    public static JsonElement Count(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return JsonataHelpers.Zero();
        }

        if (input.ValueKind == JsonValueKind.Array)
        {
            return JsonataHelpers.NumberFromDouble(input.GetArrayLength(), workspace);
        }

        // Single value counts as 1
        return JsonataHelpers.One();
    }

    /// <summary>
    /// JSONata <c>$join</c> function — joins array elements with a separator.
    /// </summary>
    public static JsonElement Join(in JsonElement input, in JsonElement separator, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        string sep = separator.ValueKind == JsonValueKind.String
            ? separator.GetString() ?? string.Empty
            : string.Empty;

        if (input.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (JsonElement item in input.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    parts.Add(item.GetString() ?? string.Empty);
                }
                else if (!item.IsNullOrUndefined())
                {
                    throw new JsonataException("T2009", "The $join function expects an array of strings", 0);
                }
            }

            return JsonataHelpers.StringFromString(string.Join(sep, parts), workspace);
        }

        if (input.ValueKind == JsonValueKind.String)
        {
            return input;
        }

        throw new JsonataException("T2009", "The $join function expects an array of strings", 0);
    }

    /// <summary>
    /// JSONata <c>$string</c> function — converts a value to its string representation.
    /// </summary>
    public static JsonElement String(in JsonElement value, JsonWorkspace workspace)
    {
        if (value.IsNullOrUndefined())
        {
            return default;
        }

        return CoerceToStringElement(value, workspace);
    }

    /// <summary>
    /// JSONata <c>$sort</c> function — sorts array elements using a comparator.
    /// </summary>
    public static JsonElement Sort(
        in JsonElement input,
        Func<JsonElement, JsonElement, JsonWorkspace, bool> comparator,
        JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.Array)
        {
            return input;
        }

        int count = input.GetArrayLength();
        if (count <= 1)
        {
            return input;
        }

        // Collect elements into an array for sorting
        JsonElement[] elements = new JsonElement[count];
        int idx = 0;
        foreach (JsonElement item in input.EnumerateArray())
        {
            elements[idx++] = item;
        }

        // Sort using the comparator (returns true if a should come before b)
        Array.Sort(elements, (a, b) => comparator(a, b, workspace) ? -1 : comparator(b, a, workspace) ? 1 : 0);

        // Build result array
        var doc = JsonElement.CreateArrayBuilder(workspace, count);
        JsonElement.Mutable root = doc.RootElement;
        for (int i = 0; i < count; i++)
        {
            root.AddItem(elements[i]);
        }

        return (JsonElement)root;
    }

    // ===== HOF Inline Helpers =====

    /// <summary>
    /// Helper for inline <c>$map</c> — maps a function over array elements and
    /// collects results into an array.
    /// </summary>
    public static JsonElement MapElements(
        in JsonElement input,
        Func<JsonElement, JsonWorkspace, JsonElement> transform,
        JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        if (input.ValueKind == JsonValueKind.Array)
        {
            int len = input.GetArrayLength();
            var doc = JsonElement.CreateArrayBuilder(workspace, len);
            JsonElement.Mutable root = doc.RootElement;

            foreach (JsonElement item in input.EnumerateArray())
            {
                JsonElement result = transform(item, workspace);
                if (result.ValueKind != JsonValueKind.Undefined)
                {
                    root.AddItem(result);
                }
            }

            return (JsonElement)root;
        }

        // Single value — map once, wrap in array
        JsonElement single = transform(input, workspace);
        if (single.ValueKind == JsonValueKind.Undefined)
        {
            return default;
        }

        using var singleDoc = JsonElement.CreateArrayBuilder(workspace, 1);
        singleDoc.RootElement.AddItem(single);
        return (JsonElement)singleDoc.RootElement;
    }

    /// <summary>
    /// Helper for inline <c>$filter</c> — filters array elements by a predicate.
    /// </summary>
    public static JsonElement FilterElements(
        in JsonElement input,
        Func<JsonElement, JsonWorkspace, bool> predicate,
        JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        if (input.ValueKind == JsonValueKind.Array)
        {
            int count = 0;
            var doc = JsonElement.CreateArrayBuilder(workspace, input.GetArrayLength());
            JsonElement.Mutable root = doc.RootElement;

            foreach (JsonElement item in input.EnumerateArray())
            {
                if (predicate(item, workspace))
                {
                    root.AddItem(item);
                    count++;
                }
            }

            return count == 0 ? default : count == 1 ? root[0] : (JsonElement)root;
        }

        // Single value — test predicate
        return predicate(input, workspace) ? input : default;
    }

    /// <summary>
    /// Helper for inline <c>$reduce</c> — reduces array elements with an accumulator.
    /// </summary>
    public static JsonElement ReduceElements(
        in JsonElement input,
        in JsonElement initial,
        Func<JsonElement, JsonElement, JsonWorkspace, JsonElement> reducer,
        JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        JsonElement accumulator = initial;

        if (input.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in input.EnumerateArray())
            {
                accumulator = reducer(accumulator, item, workspace);
            }
        }
        else
        {
            accumulator = reducer(accumulator, input, workspace);
        }

        return accumulator;
    }

    // ===== Object Construction =====

    /// <summary>
    /// Helper for group-by object construction: <c>data.{keyExpr: valueExpr}</c>.
    /// Groups elements by the key expression and collects values.
    /// </summary>
    public static JsonElement GroupByObject(
        in JsonElement input,
        Func<JsonElement, JsonWorkspace, string?> keySelector,
        Func<JsonElement, JsonWorkspace, JsonElement> valueSelector,
        JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        var groups = new Dictionary<string, List<JsonElement>>(StringComparer.Ordinal);
        var keyOrder = new List<string>();

        void ProcessElement(JsonElement elem)
        {
            string? key = keySelector(elem, workspace);
            if (key is null)
            {
                return;
            }

            JsonElement value = valueSelector(elem, workspace);
            if (value.ValueKind == JsonValueKind.Undefined)
            {
                return;
            }

            if (!groups.TryGetValue(key, out List<JsonElement>? list))
            {
                list = new List<JsonElement>();
                groups[key] = list;
                keyOrder.Add(key);
            }

            list.Add(value);
        }

        if (input.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in input.EnumerateArray())
            {
                ProcessElement(item);
            }
        }
        else
        {
            ProcessElement(input);
        }

        // Build the result object
        var doc = JsonElement.CreateObjectBuilder(workspace, groups.Count);
        JsonElement.Mutable root = doc.RootElement;

        foreach (string key in keyOrder)
        {
            List<JsonElement> values = groups[key];
            if (values.Count == 1)
            {
                root.SetProperty(key, values[0]);
            }
            else
            {
                using var arrDoc = JsonElement.CreateArrayBuilder(workspace, values.Count);
                JsonElement.Mutable arrRoot = arrDoc.RootElement;
                foreach (JsonElement v in values)
                {
                    arrRoot.AddItem(v);
                }

                root.SetProperty(key, (JsonElement)arrRoot);
            }
        }

        return (JsonElement)root;
    }

    // ===== Fallback Evaluation =====

    /// <summary>
    /// Evaluates a JSONata expression at runtime. Used as a fallback for expressions
    /// that cannot be statically compiled.
    /// </summary>
    /// <param name="expression">The JSONata expression string.</param>
    /// <param name="data">The input data.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>The evaluation result.</returns>
    public static JsonElement EvaluateExpression(string expression, in JsonElement data, JsonWorkspace workspace)
    {
        return SharedEvaluator.Evaluate(expression, data, workspace);
    }

    /// <summary>
    /// Creates a JSON string element from a .NET string value.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>A <see cref="JsonElement"/> of kind <see cref="JsonValueKind.String"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement StringElement(string value, JsonWorkspace workspace) =>
        JsonataHelpers.StringFromString(value, workspace);

    /// <summary>
    /// Creates a JSON object from parallel arrays of keys and values.
    /// </summary>
    /// <param name="keys">The property names.</param>
    /// <param name="values">The property values (undefined values are omitted).</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>A <see cref="JsonElement"/> of kind <see cref="JsonValueKind.Object"/>.</returns>
    public static JsonElement CreateObject(string[] keys, JsonElement[] values, JsonWorkspace workspace)
    {
        var doc = JsonElement.CreateObjectBuilder(workspace, keys.Length);
        JsonElement.Mutable root = doc.RootElement;
        for (int i = 0; i < keys.Length; i++)
        {
            if (!values[i].IsNullOrUndefined())
            {
                root.SetProperty(keys[i], values[i]);
            }
        }

        return root.Clone();
    }

    /// <summary>
    /// Creates a JSON array from an array of elements.
    /// </summary>
    /// <param name="elements">The array elements (undefined elements are omitted).</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>A <see cref="JsonElement"/> of kind <see cref="JsonValueKind.Array"/>.</returns>
    public static JsonElement CreateArray(JsonElement[] elements, JsonWorkspace workspace)
    {
        var doc = JsonElement.CreateArrayBuilder(workspace, elements.Length);
        JsonElement.Mutable root = doc.RootElement;
        foreach (JsonElement el in elements)
        {
            if (!el.IsNullOrUndefined())
            {
                root.AddItem(el);
            }
        }

        return root.Clone();
    }

    private static JsonElement NavigatePropertyChainCore(
        in JsonElement data, byte[][] names, int startIndex, JsonWorkspace workspace)
    {
        JsonElement current = data;

        for (int i = startIndex; i < names.Length; i++)
        {
            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty((ReadOnlySpan<byte>)names[i], out current))
                {
                    return default;
                }
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                return NavigateChainOverArray(current, names, i, workspace);
            }
            else
            {
                return default;
            }
        }

        return current;
    }

    private static JsonElement NavigateChainOverArray(
        in JsonElement array, byte[][] names, int startIndex, JsonWorkspace workspace)
    {
        int count = 0;
        var doc = JsonElement.CreateArrayBuilder(workspace, array.GetArrayLength());
        JsonElement.Mutable root = doc.RootElement;

        foreach (JsonElement item in array.EnumerateArray())
        {
            JsonElement result = NavigatePropertyChainCore(item, names, startIndex, workspace);
            count = AddResultWithFlatten(root, result, count);
        }

        return count == 0 ? default : count == 1 ? root[0] : (JsonElement)root;
    }

    private static JsonElement NavigatePropertyOverArray(
        in JsonElement array, byte[] name, JsonWorkspace workspace)
    {
        int count = 0;
        var doc = JsonElement.CreateArrayBuilder(workspace, array.GetArrayLength());
        JsonElement.Mutable root = doc.RootElement;

        foreach (JsonElement item in array.EnumerateArray())
        {
            JsonElement result = NavigateProperty(item, name, workspace);
            count = AddResultWithFlatten(root, result, count);
        }

        return count == 0 ? default : count == 1 ? root[0] : (JsonElement)root;
    }

    private static int AddResultWithFlatten(JsonElement.Mutable root, in JsonElement result, int count)
    {
        if (result.ValueKind == JsonValueKind.Array)
        {
            // Auto-flatten: add each element individually
            foreach (JsonElement child in result.EnumerateArray())
            {
                root.AddItem(child);
                count++;
            }
        }
        else if (result.ValueKind != JsonValueKind.Undefined)
        {
            root.AddItem(result);
            count++;
        }

        return count;
    }

    private static JsonElement BinaryArithmetic(
        in JsonElement left, in JsonElement right, JsonWorkspace workspace,
        string op, Func<double, double, double> compute)
    {
        bool lhsUndef = left.IsNullOrUndefined();
        bool rhsUndef = right.IsNullOrUndefined();

        if (lhsUndef && rhsUndef)
        {
            return default;
        }

        if (!lhsUndef && left.ValueKind != JsonValueKind.Number)
        {
            throw new JsonataException("T2001", $"The left side of the \"{op}\" operator must evaluate to a number", 0);
        }

        if (!rhsUndef && right.ValueKind != JsonValueKind.Number)
        {
            throw new JsonataException("T2002", $"The right side of the \"{op}\" operator must evaluate to a number", 0);
        }

        if (lhsUndef || rhsUndef)
        {
            return default;
        }

        double result = compute(left.GetDouble(), right.GetDouble());

        if (double.IsInfinity(result) || double.IsNaN(result))
        {
            throw new JsonataException("D1001", $"Number out of range: {op}(\"{left.GetRawText()}\", \"{right.GetRawText()}\")", 0);
        }

        return JsonataHelpers.NumberFromDouble(result, workspace);
    }

    private static int NumericCompare(in JsonElement left, in JsonElement right, string op)
    {
        if (left.IsNullOrUndefined() || right.IsNullOrUndefined())
        {
            return 0; // undefined comparisons return false (caller checks < or > which gives false for 0)
        }

        // String comparison
        if (left.ValueKind == JsonValueKind.String && right.ValueKind == JsonValueKind.String)
        {
            return string.CompareOrdinal(
                left.GetString(),
                right.GetString());
        }

        // Numeric comparison
        if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number)
        {
            double lv = left.GetDouble();
            double rv = right.GetDouble();
            return lv < rv ? -1 : lv > rv ? 1 : 0;
        }

        // Type mismatch
        throw new JsonataException(
            "T2010",
            $"The expressions either side of operator \"{op}\" must be of the same type",
            0);
    }

    private static bool ElementEquals(in JsonElement left, in JsonElement right)
    {
        // Same types compare naturally
        if (left.ValueKind != right.ValueKind)
        {
            // Number vs string with numeric content
            if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.String)
            {
                if (double.TryParse(right.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double rv))
                {
                    return left.GetDouble() == rv;
                }

                return false;
            }

            if (left.ValueKind == JsonValueKind.String && right.ValueKind == JsonValueKind.Number)
            {
                if (double.TryParse(left.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lv))
                {
                    return lv == right.GetDouble();
                }

                return false;
            }

            return false;
        }

        return left.ValueKind switch
        {
            JsonValueKind.Number => left.GetDouble() == right.GetDouble(),
            JsonValueKind.String => string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal),
            JsonValueKind.True => true,
            JsonValueKind.False => true,
            JsonValueKind.Null => true,
            _ => false,
        };
    }

    private static string CoerceToStringValue(in JsonElement value)
    {
        if (value.IsNullOrUndefined())
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => value.GetRawText(),
        };
    }
}