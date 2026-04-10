// <copyright file="JsonataCodeGenHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

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

    /// <summary>
    /// Implements JSONata's <c>[]</c> (keep-array) operator.
    /// Ensures the result is always a JSON array: undefined → undefined,
    /// already an array → returned as-is, scalar → wrapped in a single-element array.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>The value as a JSON array, or <c>default</c> if undefined.</returns>
    public static JsonElement WrapAsArray(in JsonElement value, JsonWorkspace workspace)
    {
        if (value.IsNullOrUndefined())
        {
            return default;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            return value;
        }

        // Wrap scalar in a single-element array
        var doc = JsonElement.CreateArrayBuilder(workspace, 1);
        doc.RootElement.AddItem(value);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Implements the KeepSingletonArray semantics for path expressions.
    /// When a path has <c>[]</c> on a step, the result must be an array.
    /// For singleton results that are non-arrays, wraps in a single-element array.
    /// </summary>
    /// <param name="value">The path result.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>The wrapped value.</returns>
    public static JsonElement KeepSingletonArray(in JsonElement value, JsonWorkspace workspace)
    {
        if (value.IsNullOrUndefined())
        {
            return value;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            var doc = JsonElement.CreateArrayBuilder(workspace, 1);
            doc.RootElement.AddItem(value);
            return doc.RootElement.Clone();
        }

        return value;
    }

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
    /// Wildcard step: enumerates all values of an object (flattening array values),
    /// or all elements of an array. Returns a collected JSON array.
    /// </summary>
    /// <param name="input">The input element.</param>
    /// <param name="workspace">The workspace for building the result array.</param>
    /// <returns>A JSON array of matched values, or <c>default</c> if input is not an object or array.</returns>
    public static JsonElement EnumerateWildcard(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.ValueKind == JsonValueKind.Array)
        {
            // Array input: return its elements as a collected multi-value
            int len = input.GetArrayLength();
            if (len == 0)
            {
                return default;
            }

            if (len == 1)
            {
                return input[0];
            }

            var doc = JsonElement.CreateArrayBuilder(workspace, len);
            JsonElement.Mutable root = doc.RootElement;
            foreach (JsonElement item in input.EnumerateArray())
            {
                root.AddItem(item);
            }

            return root;
        }

        if (input.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        int propCount = input.GetPropertyCount();
        if (propCount == 0)
        {
            return default;
        }

        var builder = JsonElement.CreateArrayBuilder(workspace, propCount * 2);
        JsonElement.Mutable builderRoot = builder.RootElement;
        int count = 0;

        foreach (var prop in input.EnumerateObject())
        {
            // Flatten array property values into individual elements
            if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in prop.Value.EnumerateArray())
                {
                    builderRoot.AddItem(item);
                    count++;
                }
            }
            else
            {
                builderRoot.AddItem(prop.Value);
                count++;
            }
        }

        return count == 0 ? default : count == 1 ? builderRoot[0] : (JsonElement)builderRoot;
    }

    /// <summary>
    /// Descendant step: recursively collects all descendant values from the input.
    /// Arrays are traversed but not included as descendants themselves.
    /// Objects and primitive values are included, and object properties are recursed.
    /// </summary>
    /// <param name="input">The input element.</param>
    /// <param name="workspace">The workspace for building the result array.</param>
    /// <returns>A JSON array of all descendant values, or <c>default</c> if none found.</returns>
    public static JsonElement EnumerateDescendant(in JsonElement input, JsonWorkspace workspace)
    {
        var doc = JsonElement.CreateArrayBuilder(workspace, 32);
        JsonElement.Mutable root = doc.RootElement;
        int count = 0;

        CollectDescendants(input, root, ref count);

        return count == 0 ? default : count == 1 ? root[0] : (JsonElement)root;

        static void CollectDescendants(in JsonElement element, JsonElement.Mutable builder, ref int count)
        {
            // Non-array values are added as descendants (the element itself).
            // Arrays are never added — only their elements are recursed into.
            if (element.ValueKind != JsonValueKind.Array)
            {
                builder.AddItem(element);
                count++;
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        CollectDescendants(prop.Value, builder, ref count);
                    }

                    break;

                case JsonValueKind.Array:
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        CollectDescendants(item, builder, ref count);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Gets an element from an array by index, supporting negative indices (from end).
    /// Used for global (post-aggregation) indexing.
    /// </summary>
    /// <param name="data">The array element.</param>
    /// <param name="index">The index (negative counts from end).</param>
    /// <returns>The element at the index, or <c>default</c> if out of range.</returns>
    public static JsonElement ArrayIndex(in JsonElement data, int index)
    {
        if (data.ValueKind != JsonValueKind.Array)
        {
            if (data.ValueKind == JsonValueKind.Undefined)
            {
                return default;
            }

            // JSONata autoboxing: scalars are treated as single-element arrays.
            return (index == 0 || index == -1) ? data : default;
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
    /// Per-element array index: matches the runtime's <c>ApplyPerElementFilterStages</c>
    /// behaviour for constant numeric indices within a path step.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the value is a JSON array, returns the element at <paramref name="index"/>.
    /// If the value is a non-array non-undefined scalar and <paramref name="index"/>
    /// effectively resolves to 0, returns the value itself (singleton treatment).
    /// </para>
    /// </remarks>
    /// <param name="data">The per-element navigation result.</param>
    /// <param name="index">The index (negative counts from end).</param>
    /// <returns>The indexed element, or <c>default</c> if out of range.</returns>
    public static JsonElement ArrayIndexPerElement(in JsonElement data, int index)
    {
        if (data.ValueKind == JsonValueKind.Array)
        {
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

        if (data.ValueKind != JsonValueKind.Undefined)
        {
            // Singleton non-array: treat as a sequence of count 1.
            int effectiveIndex = index < 0 ? 1 + index : index;
            return effectiveIndex == 0 ? data : default;
        }

        return default;
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

    /// <summary>
    /// Like <see cref="ApplyStepOverElements"/> but does NOT auto-flatten array results.
    /// Used for array constructor steps in paths (the "cons array" pattern) where
    /// each step result is preserved as an array element.
    /// </summary>
    /// <param name="data">The input (array or single value).</param>
    /// <param name="step">The step function to apply to each element.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>The collected results as an array, or <c>default</c> if undefined.</returns>
    public static JsonElement ApplyStepOverElementsNoFlatten(
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
                if (result.ValueKind != JsonValueKind.Undefined)
                {
                    root.AddItem(result);
                    count++;
                }
            }

            return count == 0 ? default : count == 1 ? root[0] : (JsonElement)root;
        }

        if (data.ValueKind != JsonValueKind.Undefined)
        {
            return step(data, workspace);
        }

        return default;
    }

    /// <summary>
    /// Like <see cref="ApplyStepOverElementsNoFlatten"/> but always produces a collection array.
    /// Used when an array constructor step is followed by <c>[]</c> (KeepSingletonArray) —
    /// the result must always be an outer array wrapping per-element results, even for single-element input.
    /// </summary>
    /// <param name="data">The input (array or single value).</param>
    /// <param name="step">The step function to apply to each element.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>An array of per-element results, or <c>default</c> if no results.</returns>
    public static JsonElement ApplyStepCollectingResults(
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
                if (result.ValueKind != JsonValueKind.Undefined)
                {
                    root.AddItem(result);
                    count++;
                }
            }

            return count == 0 ? default : (JsonElement)root;
        }

        if (data.ValueKind != JsonValueKind.Undefined)
        {
            JsonElement result = step(data, workspace);
            if (result.ValueKind == JsonValueKind.Undefined)
            {
                return default;
            }

            var doc = JsonElement.CreateArrayBuilder(workspace, 1);
            doc.RootElement.AddItem(result);
            return doc.RootElement.Clone();
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
        if (value.ValueKind == JsonValueKind.Undefined)
        {
            return default;
        }

        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new JsonataException("D1002", "Cannot negate a non-numeric value", 0);
        }

        return JsonataHelpers.NumberFromDouble(-value.GetDouble(), workspace);
    }

    /// <summary>
    /// JSONata range (..) operator. Builds an array of integers from <paramref name="left"/> to <paramref name="right"/>.
    /// </summary>
    public static JsonElement Range(in JsonElement left, in JsonElement right, JsonWorkspace workspace)
    {
        if (!left.IsUndefined() && left.ValueKind != JsonValueKind.Number)
        {
            throw new JsonataException("T2003", "The left side of the range operator (..) must evaluate to an integer", 0);
        }

        if (!right.IsUndefined() && right.ValueKind != JsonValueKind.Number)
        {
            throw new JsonataException("T2004", "The right side of the range operator (..) must evaluate to an integer", 0);
        }

        if (left.IsUndefined() || right.IsUndefined())
        {
            return default;
        }

        double start = left.GetDouble();
        double end = right.GetDouble();

        if (start != Math.Floor(start))
        {
            throw new JsonataException("T2003", "The left side of the range operator (..) must evaluate to an integer", 0);
        }

        if (end != Math.Floor(end))
        {
            throw new JsonataException("T2004", "The right side of the range operator (..) must evaluate to an integer", 0);
        }

        int iStart = (int)start;
        int iEnd = (int)end;

        if (iStart > iEnd)
        {
            return default;
        }

        long count = (long)iEnd - (long)iStart + 1;
        if (count > 10_000_000)
        {
            throw new JsonataException("D2014", "Range expression generates too many results", 0);
        }

        JsonElement[] elements = ArrayPool<JsonElement>.Shared.Rent((int)count);
        try
        {
            for (int i = iStart; i <= iEnd; i++)
            {
                elements[i - iStart] = JsonataHelpers.NumberFromDouble(i, workspace);
            }

            return CreateArrayFromPool(elements, (int)count, workspace);
        }
        finally
        {
            ArrayPool<JsonElement>.Shared.Return(elements);
        }
    }

    // ===== Comparison =====

    /// <summary>
    /// JSONata equality (=) operator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreEqual(in JsonElement left, in JsonElement right)
    {
        // undefined = anything → false (undefined is not equal to anything, even itself)
        if (left.ValueKind == JsonValueKind.Undefined || right.ValueKind == JsonValueKind.Undefined)
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
        // undefined != undefined → false
        if (left.ValueKind == JsonValueKind.Undefined && right.ValueKind == JsonValueKind.Undefined)
        {
            return false;
        }

        // undefined != anything → true
        if (left.ValueKind == JsonValueKind.Undefined || right.ValueKind == JsonValueKind.Undefined)
        {
            return true;
        }

        return !ElementEquals(left, right);
    }

    /// <summary>
    /// JSONata less-than (&lt;) operator. Returns a boolean element, or <c>default</c> (undefined)
    /// when one operand is undefined and the other is a valid comparison type.
    /// </summary>
    public static JsonElement LessThan(in JsonElement left, in JsonElement right) =>
        OrderedCompare(left, right, "<", static (cmp) => cmp < 0);

    /// <summary>
    /// JSONata less-than-or-equal (&lt;=) operator. Returns a boolean element, or <c>default</c> (undefined)
    /// when one operand is undefined and the other is a valid comparison type.
    /// </summary>
    public static JsonElement LessThanOrEqual(in JsonElement left, in JsonElement right) =>
        OrderedCompare(left, right, "<=", static (cmp) => cmp <= 0);

    /// <summary>
    /// JSONata greater-than (&gt;) operator. Returns a boolean element, or <c>default</c> (undefined)
    /// when one operand is undefined and the other is a valid comparison type.
    /// </summary>
    public static JsonElement GreaterThan(in JsonElement left, in JsonElement right) =>
        OrderedCompare(left, right, ">", static (cmp) => cmp > 0);

    /// <summary>
    /// JSONata greater-than-or-equal (&gt;=) operator. Returns a boolean element, or <c>default</c> (undefined)
    /// when one operand is undefined and the other is a valid comparison type.
    /// </summary>
    public static JsonElement GreaterThanOrEqual(in JsonElement left, in JsonElement right) =>
        OrderedCompare(left, right, ">=", static (cmp) => cmp >= 0);

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
    /// <para>Falsy values: undefined, null, false, 0, "", NaN, empty object, empty array.</para>
    /// <para>Array truthiness is recursive: an array is truthy if ANY element is truthy.</para>
    /// <para>Object truthiness: an object is truthy if it has at least one property.</para>
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
            JsonValueKind.Number => value.GetDouble() is double d && d != 0.0 && !double.IsNaN(d),
            JsonValueKind.String => !value.ValueEquals(ReadOnlySpan<byte>.Empty),
            JsonValueKind.Array => IsAnyElementTruthy(value),
            JsonValueKind.Object => value.EnumerateObject().MoveNext(),
            _ => false,
        };
    }

    private static bool IsAnyElementTruthy(in JsonElement array)
    {
        foreach (JsonElement child in array.EnumerateArray())
        {
            if (IsTruthy(child))
            {
                return true;
            }
        }

        return false;
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
                    throw new JsonataException("T0412", "Argument 1 of function 'sum' must be an array of numbers", 0);
                }
            }
        }
        else if (input.ValueKind == JsonValueKind.Number)
        {
            sum = input.GetDouble();
        }
        else
        {
            throw new JsonataException("T0412", "Argument 1 of function 'sum' must be an array of numbers", 0);
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

        // Validate separator type — must be a string if provided
        if (!separator.IsNullOrUndefined() && separator.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", "Second argument of function $join must be a string", 0);
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
                    throw new JsonataException("T0412", "Argument 1 of function $join must be an array of strings", 0);
                }
            }

            return JsonataHelpers.StringFromString(string.Join(sep, parts), workspace);
        }

        if (input.ValueKind == JsonValueKind.String)
        {
            return input;
        }

        throw new JsonataException("T0412", "Argument 1 of function $join must be an array of strings", 0);
    }

    /// <summary>
    /// JSONata <c>$string</c> function — converts a value to its string representation.
    /// </summary>
    public static JsonElement String(in JsonElement value, JsonWorkspace workspace)
    {
        return StringCore(value, prettyPrint: false, workspace);
    }

    /// <summary>
    /// JSONata <c>$string</c> function with optional pretty-print parameter.
    /// </summary>
    public static JsonElement String(in JsonElement value, in JsonElement prettyFlag, JsonWorkspace workspace)
    {
        bool prettyPrint = false;
        if (!prettyFlag.IsNullOrUndefined())
        {
            if (prettyFlag.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            {
                throw new JsonataException("T0410", "Second argument of $string must be a boolean", 0);
            }

            prettyPrint = prettyFlag.ValueKind == JsonValueKind.True;
        }

        return StringCore(value, prettyPrint, workspace);
    }

    private static JsonElement StringCore(in JsonElement value, bool prettyPrint, JsonWorkspace workspace)
    {
        // Only undefined propagates; null produces the string "null"
        if (value.ValueKind == JsonValueKind.Undefined)
        {
            return default;
        }

        // Null produces the string "null" (distinct from CoerceToStringValue which returns "" for concat)
        if (value.ValueKind == JsonValueKind.Null)
        {
            return JsonataHelpers.StringFromString("null", workspace);
        }

        // Non-finite numbers cannot be stringified
        if (value.ValueKind == JsonValueKind.Number)
        {
            double d = value.GetDouble();
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                throw new JsonataException("D3001", "Attempting to invoke string function on Infinity or NaN", 0);
            }
        }

        // For arrays and objects, produce JSON representation
        if (value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
        {
            string json = StringifyElement(value, prettyPrint);
            return JsonataHelpers.StringFromString(json, workspace);
        }

        // For scalars (including null), use the same CoerceElementToString logic as the runtime
        string result = CoerceToStringValue(value);
        return JsonataHelpers.StringFromString(result, workspace);
    }

    // ===== Phase 1a: Simple Built-in Functions =====

    /// <summary>
    /// JSONata <c>$exists</c> function — returns true if the value is not undefined.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement Exists(in JsonElement input, JsonWorkspace workspace)
    {
        // In the runtime, lambdas also "exist" but codegen can't represent lambdas as JsonElements.
        // Built-in function names as variable values are caught by FallbackException in codegen.
        return BooleanElement(input.ValueKind != JsonValueKind.Undefined);
    }

    /// <summary>
    /// JSONata <c>$type</c> function — returns a string describing the input type.
    /// </summary>
    public static JsonElement Type(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.ValueKind == JsonValueKind.Undefined)
        {
            return default;
        }

        string typeName = input.ValueKind switch
        {
            JsonValueKind.Null => "null",
            JsonValueKind.Number => "number",
            JsonValueKind.String => "string",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Array => "array",
            JsonValueKind.Object => "object",
            _ => "undefined",
        };

        return StringElement(typeName, workspace);
    }

    /// <summary>
    /// JSONata <c>$length</c> function — counts Unicode code points in a string.
    /// </summary>
    public static JsonElement Length(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.ValueKind == JsonValueKind.Undefined)
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", "Argument 1 of function 'length' must be a string", 0);
        }

        string str = input.GetString()!;
        int count = 0;
        for (int i = 0; i < str.Length; i++)
        {
            count++;
            if (char.IsHighSurrogate(str[i]) && i + 1 < str.Length && char.IsLowSurrogate(str[i + 1]))
            {
                i++;
            }
        }

        return JsonataHelpers.NumberFromDouble(count, workspace);
    }

    /// <summary>
    /// JSONata <c>$number</c> function — coerces the input to a number.
    /// Handles booleans (true→1, false→0), strings (decimal, hex 0x, binary 0b, octal 0o),
    /// and passes through existing numbers. Arrays/objects/null throw T0410.
    /// </summary>
    public static JsonElement Number(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.ValueKind == JsonValueKind.Undefined)
        {
            return default;
        }

        switch (input.ValueKind)
        {
            case JsonValueKind.Number:
            {
                double num = input.GetDouble();
                if (double.IsInfinity(num))
                {
                    throw new JsonataException("D3030", "Unable to cast value to a number", 0);
                }

                return input;
            }

            case JsonValueKind.True:
                return JsonataHelpers.One();

            case JsonValueKind.False:
                return JsonataHelpers.Zero();

            case JsonValueKind.String:
            {
                if (FunctionalCompiler.TryCoerceToNumber(input, out double parsed))
                {
                    if (double.IsInfinity(parsed))
                    {
                        throw new JsonataException("D3030", "Unable to cast value to a number", 0);
                    }

                    return JsonataHelpers.NumberFromDouble(parsed, workspace);
                }

                throw new JsonataException("D3030", "Unable to cast value to a number", 0);
            }

            default:
                throw new JsonataException("T0410", "Argument 1 of function 'number' is not of the correct type", 0);
        }
    }

    /// <summary>
    /// JSONata <c>$max</c> function — returns the maximum numeric value in an array.
    /// </summary>
    public static JsonElement Max(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        double max = double.NegativeInfinity;
        bool found = false;

        if (input.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in input.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number)
                {
                    double val = item.GetDouble();
                    if (val > max)
                    {
                        max = val;
                    }

                    found = true;
                }
                else if (!item.IsNullOrUndefined())
                {
                    throw new JsonataException("T0412", "Argument 1 of function 'max' must be an array of numbers", 0);
                }
            }
        }
        else if (input.ValueKind == JsonValueKind.Number)
        {
            max = input.GetDouble();
            found = true;
        }
        else
        {
            throw new JsonataException("T0412", "Argument 1 of function 'max' must be an array of numbers", 0);
        }

        return found ? JsonataHelpers.NumberFromDouble(max, workspace) : default;
    }

    /// <summary>
    /// JSONata <c>$min</c> function — returns the minimum numeric value in an array.
    /// </summary>
    public static JsonElement Min(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        double min = double.PositiveInfinity;
        bool found = false;

        if (input.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in input.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number)
                {
                    double val = item.GetDouble();
                    if (val < min)
                    {
                        min = val;
                    }

                    found = true;
                }
                else if (!item.IsNullOrUndefined())
                {
                    throw new JsonataException("T0412", "Argument 1 of function 'min' must be an array of numbers", 0);
                }
            }
        }
        else if (input.ValueKind == JsonValueKind.Number)
        {
            min = input.GetDouble();
            found = true;
        }
        else
        {
            throw new JsonataException("T0412", "Argument 1 of function 'min' must be an array of numbers", 0);
        }

        return found ? JsonataHelpers.NumberFromDouble(min, workspace) : default;
    }

    /// <summary>
    /// JSONata <c>$average</c> function — returns the average of numeric values in an array.
    /// </summary>
    public static JsonElement Average(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        double total = 0;
        int count = 0;

        if (input.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in input.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number)
                {
                    total += item.GetDouble();
                    count++;
                }
                else if (!item.IsNullOrUndefined())
                {
                    throw new JsonataException("T0412", "Argument 1 of function 'average' must be an array of numbers", 0);
                }
            }
        }
        else if (input.ValueKind == JsonValueKind.Number)
        {
            total = input.GetDouble();
            count = 1;
        }
        else
        {
            throw new JsonataException("T0412", "Argument 1 of function 'average' must be an array of numbers", 0);
        }

        return count == 0 ? default : JsonataHelpers.NumberFromDouble(total / count, workspace);
    }

    // ===== Phase 1b: Math Functions =====

    /// <summary>
    /// JSONata <c>$abs</c> function.
    /// </summary>
    public static JsonElement Abs(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        if (!FunctionalCompiler.TryCoerceToNumber(input, out double num))
        {
            throw new JsonataException("T0410", "Unable to cast value to a number", 0);
        }

        return JsonataHelpers.NumberFromDouble(Math.Abs(num), workspace);
    }

    /// <summary>
    /// JSONata <c>$floor</c> function.
    /// </summary>
    public static JsonElement Floor(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        if (!FunctionalCompiler.TryCoerceToNumber(input, out double num))
        {
            throw new JsonataException("T0410", "Unable to cast value to a number", 0);
        }

        return JsonataHelpers.NumberFromDouble(Math.Floor(num), workspace);
    }

    /// <summary>
    /// JSONata <c>$ceil</c> function.
    /// </summary>
    public static JsonElement Ceil(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        if (!FunctionalCompiler.TryCoerceToNumber(input, out double num))
        {
            throw new JsonataException("T0410", "Unable to cast value to a number", 0);
        }

        return JsonataHelpers.NumberFromDouble(Math.Ceiling(num), workspace);
    }

    /// <summary>
    /// JSONata <c>$sqrt</c> function. Throws D3060 for negative numbers.
    /// </summary>
    public static JsonElement Sqrt(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        if (!FunctionalCompiler.TryCoerceToNumber(input, out double num))
        {
            throw new JsonataException("T0410", "Unable to cast value to a number", 0);
        }

        if (num < 0)
        {
            throw new JsonataException("D3060", "The argument of the $sqrt function must be non-negative", 0);
        }

        return JsonataHelpers.NumberFromDouble(Math.Sqrt(num), workspace);
    }

    /// <summary>
    /// JSONata <c>$round</c> function with optional precision.
    /// Uses banker's rounding (MidpointRounding.ToEven).
    /// Negative precision rounds to tens, hundreds, etc.
    /// </summary>
    public static JsonElement Round(in JsonElement input, in JsonElement precisionElement, JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        if (!FunctionalCompiler.TryCoerceToNumber(input, out double num))
        {
            throw new JsonataException("T0410", "Unable to cast value to a number", 0);
        }

        int precision = 0;
        if (precisionElement.ValueKind != JsonValueKind.Undefined
            && FunctionalCompiler.TryCoerceToNumber(precisionElement, out double precD))
        {
            precision = (int)precD;
        }

        double result;
        if (precision < 0)
        {
            double factor = Math.Pow(10, -precision);
            result = Math.Round(num / factor, MidpointRounding.ToEven) * factor;
        }
        else
        {
            decimal decValue = (decimal)num;
            decimal rounded = Math.Round(decValue, Math.Min(precision, 15), MidpointRounding.ToEven);
            result = (double)rounded;
        }

        return JsonataHelpers.NumberFromDouble(result, workspace);
    }

    /// <summary>
    /// JSONata <c>$power</c> function. Throws D3061 if result is non-finite.
    /// </summary>
    public static JsonElement Power(in JsonElement baseInput, in JsonElement exponentInput, JsonWorkspace workspace)
    {
        if (!FunctionalCompiler.TryCoerceToNumber(baseInput, out double baseNum)
            || !FunctionalCompiler.TryCoerceToNumber(exponentInput, out double expNum))
        {
            return default;
        }

        double result = Math.Pow(baseNum, expNum);
        if (double.IsInfinity(result) || double.IsNaN(result))
        {
            throw new JsonataException("D3061", "The power function has resulted in a value that cannot be represented as a JSON number", 0);
        }

        return JsonataHelpers.NumberFromDouble(result, workspace);
    }

    // ===== Phase 1c: String Transforms =====
    private static readonly Regex WhitespaceCollapseRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// JSONata <c>$uppercase</c> function.
    /// </summary>
    public static JsonElement Uppercase(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", "String function argument must be a string", 0);
        }

        string str = input.GetString()!;
        return JsonataHelpers.StringFromString(str.ToUpperInvariant(), workspace);
    }

    /// <summary>
    /// JSONata <c>$lowercase</c> function.
    /// </summary>
    public static JsonElement Lowercase(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", "String function argument must be a string", 0);
        }

        string str = input.GetString()!;
        return JsonataHelpers.StringFromString(str.ToLowerInvariant(), workspace);
    }

    /// <summary>
    /// JSONata <c>$trim</c> function — trims and collapses whitespace.
    /// </summary>
    public static JsonElement Trim(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", "String function argument must be a string", 0);
        }

        string str = input.GetString()!;
        return JsonataHelpers.StringFromString(WhitespaceCollapseRegex.Replace(str.Trim(), " "), workspace);
    }

    /// <summary>
    /// JSONata <c>$substring</c> function — code-point-aware substring.
    /// </summary>
    public static JsonElement Substring(in JsonElement input, in JsonElement startElement, in JsonElement lengthElement, JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", "Argument 1 of function $substring is not a string", 0);
        }

        if (!FunctionalCompiler.TryCoerceToNumber(startElement, out double startD))
        {
            throw new JsonataException("T0410", "Argument 2 of function $substring is not a number", 0);
        }

        string str = input.GetString()!;
        int cpLen = CountCodePoints(str);
        int start = (int)startD;
        if (start < 0)
        {
            start = Math.Max(0, cpLen + start);
        }

        start = Math.Min(start, cpLen);

        int count;
        if (lengthElement.ValueKind != JsonValueKind.Undefined)
        {
            if (!FunctionalCompiler.TryCoerceToNumber(lengthElement, out double lenD))
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

        int startCharIdx = CodePointToCharIndex(str, start);
        int endCharIdx = CodePointToCharIndex(str, start + count);
        string result = str.Substring(startCharIdx, endCharIdx - startCharIdx);
        return JsonataHelpers.StringFromString(result, workspace);
    }

    /// <summary>
    /// JSONata <c>$substringBefore</c> function.
    /// </summary>
    public static JsonElement SubstringBefore(in JsonElement input, in JsonElement search, JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", "Argument 1 of string function is not a string", 0);
        }

        if (search.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", "Argument 2 of string function is not a string", 0);
        }

        string str = input.GetString()!;
        string searchStr = search.GetString()!;
        int idx = str.IndexOf(searchStr, StringComparison.Ordinal);
        string result = idx < 0 ? str : str.Substring(0, idx);
        return JsonataHelpers.StringFromString(result, workspace);
    }

    /// <summary>
    /// JSONata <c>$substringAfter</c> function.
    /// </summary>
    public static JsonElement SubstringAfter(in JsonElement input, in JsonElement search, JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", "Argument 1 of string function is not a string", 0);
        }

        if (search.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", "Argument 2 of string function is not a string", 0);
        }

        string str = input.GetString()!;
        string searchStr = search.GetString()!;
        int idx = str.IndexOf(searchStr, StringComparison.Ordinal);
        string result = idx < 0 ? str : str.Substring(idx + searchStr.Length);
        return JsonataHelpers.StringFromString(result, workspace);
    }

    /// <summary>
    /// JSONata <c>$contains</c> function (string-only; regex falls back to runtime).
    /// </summary>
    public static JsonElement Contains(in JsonElement input, in JsonElement search, JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", "Argument 1 of function $contains must be a string", 0);
        }

        if (search.IsNullOrUndefined() || search.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", "Argument 2 of function $contains must be a string or regex", 0);
        }

        string str = input.GetString()!;
        string searchStr = search.GetString()!;
        return BooleanElement(str.Contains(searchStr));
    }

    /// <summary>
    /// JSONata <c>$split</c> function (string separator only; regex falls back to runtime).
    /// </summary>
    public static JsonElement Split(in JsonElement input, in JsonElement separator, in JsonElement limitElement, JsonWorkspace workspace)
    {
        // No undefined/null guard here — runtime $split does not have elem-level undefined check,
        // so undefined/null input falls through to the type check and throws T0410.
        if (input.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", "Argument 1 of function $split is not of the correct type", 0);
        }

        if (separator.IsUndefined() || separator.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", "Argument 2 of function $split is not of the correct type", 0);
        }

        string str = input.GetString()!;
        string sep = separator.GetString()!;

        int limit = int.MaxValue;
        if (limitElement.ValueKind != JsonValueKind.Undefined)
        {
            if (limitElement.ValueKind != JsonValueKind.Number)
            {
                throw new JsonataException("T0410", "Argument 3 of function $split is not of the correct type", 0);
            }

            if (FunctionalCompiler.TryCoerceToNumber(limitElement, out double limitD))
            {
                if (limitD < 0)
                {
                    throw new JsonataException("D3020", "Third argument of the split function must evaluate to a positive number", 0);
                }

                limit = (int)Math.Floor(limitD);
            }
        }

        string[] parts;
        if (sep.Length == 0)
        {
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

        int count = Math.Min(parts.Length, limit);
        JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, count);
        JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
        for (int i = 0; i < count; i++)
        {
            arrayRoot.AddItem(JsonataHelpers.StringFromString(parts[i], workspace));
        }

        return (JsonElement)arrayRoot;
    }

    /// <summary>
    /// JSONata <c>$pad</c> function — pads a string with cycling pad characters.
    /// </summary>
    public static JsonElement Pad(in JsonElement input, in JsonElement widthElement, in JsonElement padCharElement, JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        string str = FunctionalCompiler.CoerceElementToString(input);

        if (!FunctionalCompiler.TryCoerceToNumber(widthElement, out double widthNum))
        {
            return default;
        }

        int width = (int)widthNum;
        string padStr = " ";
        if (padCharElement.ValueKind != JsonValueKind.Undefined)
        {
            string padVal = FunctionalCompiler.CoerceElementToString(padCharElement);
            if (padVal.Length > 0)
            {
                padStr = padVal;
            }
        }

        int cpLen = CountCodePoints(str);
        int absWidth = Math.Abs(width);
        int padNeeded = absWidth - cpLen;
        if (padNeeded <= 0)
        {
            return JsonataHelpers.StringFromString(str, workspace);
        }

        // Build code-point array for the pad string so cycling works correctly with surrogates.
        int padCpCount = CountCodePoints(padStr);
        int[] padCodePoints = new int[padCpCount];
        int cpIdx = 0;
        for (int ci = 0; ci < padStr.Length; ci++)
        {
            if (char.IsHighSurrogate(padStr[ci]) && ci + 1 < padStr.Length && char.IsLowSurrogate(padStr[ci + 1]))
            {
                padCodePoints[cpIdx++] = char.ConvertToUtf32(padStr[ci], padStr[ci + 1]);
                ci++;
            }
            else
            {
                padCodePoints[cpIdx++] = padStr[ci];
            }
        }

        var sbPad = new StringBuilder();
        for (int i = 0; i < padNeeded; i++)
        {
            sbPad.Append(char.ConvertFromUtf32(padCodePoints[i % padCpCount]));
        }

        string padding = sbPad.ToString();
        string result = width > 0 ? str + padding : padding + str;
        return JsonataHelpers.StringFromString(result, workspace);
    }

    // ===== Phase 1d: Array/Object Operations =====

    /// <summary>
    /// JSONata <c>$append</c> function — concatenates two values/arrays.
    /// </summary>
    public static JsonElement Append(in JsonElement a, in JsonElement b, JsonWorkspace workspace)
    {
        bool aUndefined = a.ValueKind == JsonValueKind.Undefined;
        bool bUndefined = b.ValueKind == JsonValueKind.Undefined;

        if (aUndefined && bUndefined)
        {
            return default;
        }

        if (bUndefined)
        {
            return a;
        }

        if (aUndefined)
        {
            return b;
        }

        JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, 16);
        JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
        AddElementToArray(a, ref arrayRoot);
        AddElementToArray(b, ref arrayRoot);
        return (JsonElement)arrayRoot;
    }

    private static void AddElementToArray(JsonElement el, ref JsonElement.Mutable arrayRoot)
    {
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

    /// <summary>
    /// JSONata <c>$reverse</c> function.
    /// </summary>
    public static JsonElement Reverse(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.Array)
        {
            return input;
        }

        int len = input.GetArrayLength();
        JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, len);
        JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
        for (int i = len - 1; i >= 0; i--)
        {
            arrayRoot.AddItem(input[i]);
        }

        return (JsonElement)arrayRoot;
    }

    /// <summary>
    /// JSONata <c>$distinct</c> function — deduplicates by GetRawText.
    /// </summary>
    public static JsonElement Distinct(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.ValueKind == JsonValueKind.Undefined)
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.Array)
        {
            return input;
        }

        JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, input.GetArrayLength());
        JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
        var seen = new HashSet<string>();
        foreach (var item in input.EnumerateArray())
        {
            if (seen.Add(item.GetRawText()))
            {
                arrayRoot.AddItem(item);
            }
        }

        return (JsonElement)arrayRoot;
    }

    /// <summary>
    /// JSONata <c>$keys</c> function — returns unique keys from an object or array of objects.
    /// </summary>
    public static JsonElement Keys(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        var keys = new List<string>();
        var seen = new HashSet<string>();
        CollectKeys(input, keys, seen);

        if (keys.Count == 0)
        {
            return default;
        }

        if (keys.Count == 1)
        {
            return JsonataHelpers.StringFromString(keys[0], workspace);
        }

        JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, keys.Count);
        JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
        foreach (var key in keys)
        {
            arrayRoot.AddItem(JsonataHelpers.StringFromString(key, workspace));
        }

        return (JsonElement)arrayRoot;
    }

    private static void CollectKeys(JsonElement el, List<string> keys, HashSet<string> seen)
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
                CollectKeys(item, keys, seen);
            }
        }
    }

    /// <summary>
    /// JSONata <c>$values</c> function — returns values from an object.
    /// </summary>
    public static JsonElement Values(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined() || input.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        int propCount = 0;
        foreach (var p in input.EnumerateObject())
        {
            propCount++;
        }

        JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, propCount);
        JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
        foreach (var prop in input.EnumerateObject())
        {
            arrayRoot.AddItem(prop.Value);
        }

        return (JsonElement)arrayRoot;
    }

    /// <summary>
    /// JSONata <c>$lookup</c> function — looks up a property recursively across objects/arrays.
    /// </summary>
    public static JsonElement Lookup(in JsonElement input, in JsonElement keyElement, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined() || keyElement.IsNullOrUndefined())
        {
            return default;
        }

        string key = keyElement.GetString()!;

        var results = new List<JsonElement>();
        LookupCollect(input, key, results);

        if (results.Count == 0)
        {
            return default;
        }

        if (results.Count == 1)
        {
            return results[0];
        }

        JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, results.Count);
        JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
        foreach (var item in results)
        {
            arrayRoot.AddItem(item);
        }

        return (JsonElement)arrayRoot;
    }

    private static void LookupCollect(JsonElement el, string key, List<JsonElement> results)
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                LookupCollect(item, key, results);
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

    /// <summary>
    /// JSONata <c>$merge</c> function — merges an array of objects.
    /// </summary>
    public static JsonElement Merge(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        if (input.ValueKind == JsonValueKind.Object)
        {
            return input;
        }

        JsonDocumentBuilder<JsonElement.Mutable> objDoc = JsonElement.CreateObjectBuilder(workspace, 16);
        JsonElement.Mutable objRoot = objDoc.RootElement;

        if (input.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in input.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in item.EnumerateObject())
                    {
                        objRoot.SetProperty(prop.Name, prop.Value);
                    }
                }
            }
        }

        return (JsonElement)objRoot;
    }

    /// <summary>
    /// JSONata <c>$spread</c> function — expands each property into a single-property object.
    /// </summary>
    public static JsonElement Spread(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        if (input.ValueKind == JsonValueKind.Object)
        {
            return SpreadObject(input, workspace);
        }

        if (input.ValueKind == JsonValueKind.Array)
        {
            JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, input.GetArrayLength());
            JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
            foreach (var item in input.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in item.EnumerateObject())
                    {
                        JsonDocumentBuilder<JsonElement.Mutable> propDoc = JsonElement.CreateObjectBuilder(workspace, 1);
                        JsonElement.Mutable propRoot = propDoc.RootElement;
                        propRoot.SetProperty(prop.Name, prop.Value);
                        arrayRoot.AddItem((JsonElement)propRoot);
                    }
                }
            }

            return (JsonElement)arrayRoot;
        }

        return input;
    }

    private static JsonElement SpreadObject(JsonElement obj, JsonWorkspace workspace)
    {
        int propCount = 0;
        foreach (var p in obj.EnumerateObject())
        {
            propCount++;
        }

        if (propCount <= 1)
        {
            return obj;
        }

        JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, propCount);
        JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
        foreach (var prop in obj.EnumerateObject())
        {
            JsonDocumentBuilder<JsonElement.Mutable> propDoc = JsonElement.CreateObjectBuilder(workspace, 1);
            JsonElement.Mutable propRoot = propDoc.RootElement;
            propRoot.SetProperty(prop.Name, prop.Value);
            arrayRoot.AddItem((JsonElement)propRoot);
        }

        return (JsonElement)arrayRoot;
    }

    /// <summary>
    /// JSONata <c>$single</c> function (1-arg form) — returns the single element of an array.
    /// </summary>
    public static JsonElement Single(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.Array)
        {
            return input;
        }

        int count = input.GetArrayLength();
        if (count == 1)
        {
            return input[0];
        }

        if (count == 0)
        {
            throw new JsonataException("D3139", "The $single function expected exactly 1 matching result but got 0", 0);
        }

        throw new JsonataException("D3138", "The $single function expected exactly 1 matching result but got " + count, 0);
    }

    /// <summary>
    /// JSONata <c>$single(array, predicate)</c> — filters array by predicate, expects exactly 1 match.
    /// </summary>
    public static JsonElement SingleWithPredicate(
        in JsonElement input,
        Func<JsonElement, JsonWorkspace, bool> predicate,
        JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        JsonElement found = default;
        int matchCount = 0;

        if (input.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in input.EnumerateArray())
            {
                if (predicate(item, workspace))
                {
                    matchCount++;
                    if (matchCount == 1)
                    {
                        found = item;
                    }
                    else
                    {
                        throw new JsonataException("D3138", "The $single function expected exactly 1 matching result but got " + matchCount, 0);
                    }
                }
            }
        }
        else
        {
            if (predicate(input, workspace))
            {
                return input;
            }
        }

        if (matchCount == 0)
        {
            throw new JsonataException("D3139", "The $single function expected exactly 1 matching result but got 0", 0);
        }

        return found;
    }

    /// <summary>
    /// JSONata <c>$single(array, predicate)</c> with index and array parameters.
    /// </summary>
    public static JsonElement SingleWithPredicateIndexed(
        in JsonElement input,
        Func<JsonElement, JsonElement, JsonElement, JsonWorkspace, bool> predicate,
        JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        JsonElement found = default;
        int matchCount = 0;

        if (input.ValueKind == JsonValueKind.Array)
        {
            int idx = 0;
            foreach (JsonElement item in input.EnumerateArray())
            {
                JsonElement indexEl = NumberFromDouble(idx, workspace);
                if (predicate(item, indexEl, input, workspace))
                {
                    matchCount++;
                    if (matchCount == 1)
                    {
                        found = item;
                    }
                    else
                    {
                        throw new JsonataException("D3138", "The $single function expected exactly 1 matching result but got " + matchCount, 0);
                    }
                }

                idx++;
            }
        }
        else
        {
            JsonElement indexEl = NumberFromDouble(0, workspace);
            if (predicate(input, indexEl, input, workspace))
            {
                return input;
            }
        }

        if (matchCount == 0)
        {
            throw new JsonataException("D3139", "The $single function expected exactly 1 matching result but got 0", 0);
        }

        return found;
    }

    /// <summary>
    /// JSONata <c>$flatten</c> function — flattens nested arrays one level.
    /// </summary>
    public static JsonElement Flatten(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.Array)
        {
            return input;
        }

        JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, input.GetArrayLength());
        JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
        FlattenElement(input, ref arrayRoot);
        return (JsonElement)arrayRoot;
    }

    private static void FlattenElement(JsonElement element, ref JsonElement.Mutable arrayRoot)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                FlattenElement(item, ref arrayRoot);
            }
        }
        else
        {
            arrayRoot.AddItem(element);
        }
    }

    /// <summary>
    /// JSONata <c>$shuffle</c> function — Fisher-Yates shuffle.
    /// </summary>
    public static JsonElement Shuffle(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.Array)
        {
            return input;
        }

        int len = input.GetArrayLength();
        var elements = new JsonElement[len];
        int idx = 0;
        foreach (var item in input.EnumerateArray())
        {
            elements[idx++] = item;
        }

        for (int i = len - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (elements[i], elements[j]) = (elements[j], elements[i]);
        }

        JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, len);
        JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
        for (int k = 0; k < len; k++)
        {
            arrayRoot.AddItem(elements[k]);
        }

        return (JsonElement)arrayRoot;
    }

    // ===== Phase 1e: Error/Utility =====

    /// <summary>
    /// JSONata <c>$error</c> function — throws D3137.
    /// </summary>
    public static JsonElement Error(in JsonElement input, JsonWorkspace workspace)
    {
        string message = "An error occurred";
        if (input.ValueKind != JsonValueKind.Undefined)
        {
            if (input.ValueKind != JsonValueKind.String)
            {
                throw new JsonataException("T0410", "$error expects a string argument", 0);
            }

            message = input.GetString() ?? message;
        }

        throw new JsonataException("D3137", message, 0);
    }

    /// <summary>
    /// JSONata <c>$assert</c> function — throws D3141 if the condition is false.
    /// </summary>
    public static JsonElement Assert(in JsonElement condition, in JsonElement messageElement, JsonWorkspace workspace)
    {
        if (condition.ValueKind == JsonValueKind.Undefined
            || condition.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new JsonataException("T0410", "Argument 1 of function $assert must be a boolean", 0);
        }

        if (condition.ValueKind == JsonValueKind.False)
        {
            string message = "Assertion failed";
            if (messageElement.ValueKind != JsonValueKind.Undefined)
            {
                message = FunctionalCompiler.CoerceElementToString(messageElement);
            }

            throw new JsonataException("D3141", message, 0);
        }

        return default;
    }

    /// <summary>
    /// JSONata <c>$now</c> function — returns current UTC time as ISO 8601 string.
    /// </summary>
    public static JsonElement Now(JsonWorkspace workspace)
    {
        return JsonataHelpers.StringFromString(
            DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture), workspace);
    }

    /// <summary>
    /// JSONata <c>$millis</c> function — returns current UTC time as unix milliseconds.
    /// </summary>
    public static JsonElement Millis(JsonWorkspace workspace)
    {
        return JsonataHelpers.NumberFromDouble(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), workspace);
    }

    // ===== Phase 1f: Encoding =====

    /// <summary>
    /// JSONata <c>$base64encode</c> function.
    /// </summary>
    public static JsonElement Base64Encode(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        string str = FunctionalCompiler.CoerceElementToString(input);
        return JsonataHelpers.StringFromString(
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(str)), workspace);
    }

    /// <summary>
    /// JSONata <c>$base64decode</c> function.
    /// </summary>
    public static JsonElement Base64Decode(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        string str = FunctionalCompiler.CoerceElementToString(input);
        return JsonataHelpers.StringFromString(
            System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(str)), workspace);
    }

    /// <summary>
    /// JSONata <c>$encodeUrlComponent</c> function.
    /// </summary>
    public static JsonElement EncodeUrlComponent(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        string str;
        try
        {
            str = FunctionalCompiler.CoerceElementToString(input);
        }
        catch (InvalidOperationException)
        {
            throw new JsonataException("D3140", "Malformed URL passed to $encodeUrlComponent(): invalid string", 0);
        }

        BuiltInFunctions.ValidateNoUnpairedSurrogates(str, "$encodeUrlComponent");
        return JsonataHelpers.StringFromString(Uri.EscapeDataString(str), workspace);
    }

    /// <summary>
    /// JSONata <c>$decodeUrlComponent</c> function.
    /// </summary>
    public static JsonElement DecodeUrlComponent(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        string str = FunctionalCompiler.CoerceElementToString(input);

        if (BuiltInFunctions.HasInvalidPercentEncoding(str))
        {
            throw new JsonataException("D3140", $"Malformed URL passed to $decodeUrlComponent(): \"{str}\"", 0);
        }

        try
        {
            return JsonataHelpers.StringFromString(Uri.UnescapeDataString(str), workspace);
        }
        catch (Exception ex) when (ex is UriFormatException or ArgumentException or FormatException)
        {
            throw new JsonataException("D3140", $"Malformed URL passed to $decodeUrlComponent(): \"{str}\"", 0);
        }
    }

#pragma warning disable SYSLIB0013 // Uri.EscapeUriString is obsolete
    /// <summary>
    /// JSONata <c>$encodeUrl</c> function.
    /// </summary>
    public static JsonElement EncodeUrl(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        string str;
        try
        {
            str = FunctionalCompiler.CoerceElementToString(input);
        }
        catch (InvalidOperationException)
        {
            throw new JsonataException("D3140", "Malformed URL passed to $encodeUrl(): invalid string", 0);
        }

        BuiltInFunctions.ValidateNoUnpairedSurrogates(str, "$encodeUrl");
        return JsonataHelpers.StringFromString(Uri.EscapeUriString(str), workspace);
    }
#pragma warning restore SYSLIB0013

    /// <summary>
    /// JSONata <c>$decodeUrl</c> function.
    /// </summary>
    public static JsonElement DecodeUrl(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        string str = FunctionalCompiler.CoerceElementToString(input);

        if (BuiltInFunctions.HasInvalidPercentEncoding(str))
        {
            throw new JsonataException("D3140", $"Malformed URL passed to $decodeUrl(): \"{str}\"", 0);
        }

        try
        {
            return JsonataHelpers.StringFromString(Uri.UnescapeDataString(str), workspace);
        }
        catch (Exception ex) when (ex is UriFormatException or ArgumentException or FormatException)
        {
            throw new JsonataException("D3140", $"Malformed URL passed to $decodeUrl(): \"{str}\"", 0);
        }
    }

    // ===== Phase 2: Replace and Zip =====

    /// <summary>
    /// JSONata <c>$replace</c> function — string-only form (no regex).
    /// Replaces occurrences of a pattern string with a replacement string,
    /// with an optional limit on the number of replacements.
    /// </summary>
    /// <param name="input">The input string element.</param>
    /// <param name="pattern">The pattern string to search for.</param>
    /// <param name="replacement">The replacement string.</param>
    /// <param name="limitElement">Optional limit element (number or undefined).</param>
    /// <param name="workspace">The workspace for building the result.</param>
    /// <returns>The replaced string, or <c>default</c> if input is undefined.</returns>
    public static JsonElement Replace(
        in JsonElement input,
        in JsonElement pattern,
        in JsonElement replacement,
        in JsonElement limitElement,
        JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", "Argument 1 of function $replace is not of the correct type", 0);
        }

        if (pattern.IsUndefined() || pattern.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", "Argument 2 of function $replace is not of the correct type", 0);
        }

        if (replacement.IsUndefined() || replacement.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", "Argument 3 of function $replace is not of the correct type", 0);
        }

        string str = input.GetString()!;
        string pat = pattern.GetString()!;
        string rep = replacement.GetString()!;

        if (pat.Length == 0)
        {
            throw new JsonataException("D3010", "The second argument of the $replace function cannot be an empty string", 0);
        }

        int limit = int.MaxValue;
        if (!limitElement.IsUndefined())
        {
            if (limitElement.ValueKind == JsonValueKind.Null)
            {
                throw new JsonataException("T0410", "Argument 4 of function $replace is not of the correct type", 0);
            }

            if (limitElement.ValueKind != JsonValueKind.Number)
            {
                throw new JsonataException("T0410", "Argument 4 of function $replace is not of the correct type", 0);
            }

            double n = limitElement.GetDouble();
            if (n < 0)
            {
                throw new JsonataException("D3011", "The fourth argument of the $replace function must be a positive number", 0);
            }

            limit = (int)n;
        }

        int count = 0;
        int idx;
        while (count < limit && (idx = str.IndexOf(pat, StringComparison.Ordinal)) >= 0)
        {
            str = str.Substring(0, idx) + rep + str.Substring(idx + pat.Length);
            count++;
        }

        return JsonataHelpers.StringFromString(str, workspace);
    }

    /// <summary>
    /// JSONata <c>$zip</c> function — transposes arrays.
    /// Takes multiple arrays and returns an array of arrays where the i-th inner array
    /// contains the i-th element from each input array.
    /// </summary>
    /// <param name="args">The input array elements to zip.</param>
    /// <param name="workspace">The workspace for building the result.</param>
    /// <returns>An array of arrays, or <c>default</c> if no valid arrays.</returns>
    public static JsonElement Zip(JsonElement[] args, JsonWorkspace workspace)
    {
        if (args.Length == 0)
        {
            return default;
        }

        // Collect arrays — scalars become single-element arrays, undefined is empty
        int minLen = int.MaxValue;
        int validCount = 0;
        for (int a = 0; a < args.Length; a++)
        {
            int len;
            if (args[a].ValueKind == JsonValueKind.Array)
            {
                len = args[a].GetArrayLength();
            }
            else if (args[a].IsUndefined())
            {
                len = 0;
            }
            else
            {
                // Scalar → treat as single-element array
                len = 1;
            }

            if (len < minLen)
            {
                minLen = len;
            }

            validCount++;
        }

        if (minLen == 0 || minLen == int.MaxValue)
        {
            // Return empty array (not undefined) — matches runtime behavior
            var emptyDoc = JsonElement.CreateArrayBuilder(workspace, 0);
            return (JsonElement)emptyDoc.RootElement;
        }

        var outerDoc = JsonElement.CreateArrayBuilder(workspace, minLen);
        JsonElement.Mutable outerRoot = outerDoc.RootElement;

        for (int i = 0; i < minLen; i++)
        {
            var innerDoc = JsonElement.CreateArrayBuilder(workspace, args.Length);
            JsonElement.Mutable innerRoot = innerDoc.RootElement;

            for (int a = 0; a < args.Length; a++)
            {
                if (args[a].ValueKind == JsonValueKind.Array)
                {
                    innerRoot.AddItem(args[a][i]);
                }
                else if (!args[a].IsUndefined())
                {
                    // Scalar value
                    innerRoot.AddItem(args[a]);
                }
            }

            outerRoot.AddItem((JsonElement)innerRoot);
        }

        return (JsonElement)outerRoot;
    }

    /// <summary>
    /// Sort step helper: sorts an array by evaluating sort key expressions per element.
    /// Uses a stable index-based sort that preserves relative order of equal elements.
    /// </summary>
    /// <param name="input">The input array (or scalar, treated as single-element array).</param>
    /// <param name="keyExtractors">Functions that extract sort keys from elements.</param>
    /// <param name="descending">Whether each key sorts in descending order.</param>
    /// <param name="workspace">The workspace for building the result array.</param>
    /// <returns>A sorted JSON array.</returns>
    public static JsonElement SortByKeys(
        in JsonElement input,
        Func<JsonElement, JsonWorkspace, JsonElement>[] keyExtractors,
        bool[] descending,
        JsonWorkspace workspace)
    {
        // Collect elements
        int count;
        JsonElement[] elements;
        if (input.ValueKind == JsonValueKind.Array)
        {
            count = input.GetArrayLength();
            if (count <= 1)
            {
                return input;
            }

            elements = ArrayPool<JsonElement>.Shared.Rent(count);
            int idx = 0;
            foreach (JsonElement item in input.EnumerateArray())
            {
                elements[idx++] = item;
            }
        }
        else if (input.IsUndefined())
        {
            return default;
        }
        else
        {
            return input;
        }

        try
        {
            // Build index array for stable sort
            int[] indices = ArrayPool<int>.Shared.Rent(count);
            for (int i = 0; i < count; i++)
            {
                indices[i] = i;
            }

            try
            {
                // Sort indices by comparing sort keys
                Array.Sort(indices, 0, count, Comparer<int>.Create((a, b) =>
                {
                    for (int t = 0; t < keyExtractors.Length; t++)
                    {
                        JsonElement aKey = keyExtractors[t](elements[a], workspace);
                        JsonElement bKey = keyExtractors[t](elements[b], workspace);

                        int cmp = CompareSortKeys(aKey, bKey);
                        if (cmp != 0)
                        {
                            return descending[t] ? -cmp : cmp;
                        }
                    }

                    // Stable sort: preserve original order for equal elements
                    return a.CompareTo(b);
                }));

                // Build sorted result
                var doc = JsonElement.CreateArrayBuilder(workspace, count);
                JsonElement.Mutable root = doc.RootElement;
                for (int i = 0; i < count; i++)
                {
                    root.AddItem(elements[indices[i]]);
                }

                return (JsonElement)root;
            }
            finally
            {
                ArrayPool<int>.Shared.Return(indices);
            }
        }
        finally
        {
            ArrayPool<JsonElement>.Shared.Return(elements);
        }
    }

    /// <summary>
    /// Compares two sort key values. Numbers compare numerically, strings compare
    /// by ordinal UTF-8 bytes. Undefined sorts last. Type mismatches throw T2007.
    /// </summary>
    internal static int CompareSortKeys(in JsonElement a, in JsonElement b)
    {
        bool aUndef = a.IsUndefined();
        bool bUndef = b.IsUndefined();

        if (aUndef && bUndef)
        {
            return 0;
        }

        if (aUndef)
        {
            return 1;
        }

        if (bUndef)
        {
            return -1;
        }

        bool aIsNum = a.ValueKind == JsonValueKind.Number;
        bool aIsStr = a.ValueKind == JsonValueKind.String;
        bool bIsNum = b.ValueKind == JsonValueKind.Number;
        bool bIsStr = b.ValueKind == JsonValueKind.String;

        if (!aIsNum && !aIsStr)
        {
            throw new JsonataException("T2008", "The expressions within an order-by clause must evaluate to numeric or string values", 0);
        }

        if (!bIsNum && !bIsStr)
        {
            throw new JsonataException("T2008", "The expressions within an order-by clause must evaluate to numeric or string values", 0);
        }

        if (aIsNum != bIsNum)
        {
            throw new JsonataException("T2007", "Type mismatch within order-by clause. All values must be of the same type", 0);
        }

        if (aIsNum)
        {
            return a.GetDouble().CompareTo(b.GetDouble());
        }

        return StringComparer.Ordinal.Compare(a.GetString(), b.GetString());
    }

    /// <summary>
    /// JSONata <c>$sort</c> function with 1 argument — default sort.
    /// Flattens arrays, validates no objects/arrays, sorts numbers numerically
    /// and strings by ordinal comparison.
    /// </summary>
    public static JsonElement SortDefault(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        // Collect and flatten elements
        int capacity = input.ValueKind == JsonValueKind.Array ? input.GetArrayLength() : 1;

        JsonElement[] elements = ArrayPool<JsonElement>.Shared.Rent(capacity * 2);
        int count = 0;

        try
        {
            if (input.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement el in input.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement inner in el.EnumerateArray())
                        {
                            if (count >= elements.Length)
                            {
                                JsonElement[] newArr = ArrayPool<JsonElement>.Shared.Rent(elements.Length * 2);
                                Array.Copy(elements, newArr, count);
                                ArrayPool<JsonElement>.Shared.Return(elements);
                                elements = newArr;
                            }

                            elements[count++] = inner;
                        }
                    }
                    else
                    {
                        if (count >= elements.Length)
                        {
                            JsonElement[] newArr = ArrayPool<JsonElement>.Shared.Rent(elements.Length * 2);
                            Array.Copy(elements, newArr, count);
                            ArrayPool<JsonElement>.Shared.Return(elements);
                            elements = newArr;
                        }

                        elements[count++] = el;
                    }
                }
            }
            else
            {
                elements[count++] = input;
            }

            if (count <= 1)
            {
                if (count == 0)
                {
                    return default;
                }

                // Single element — wrap in array
                var singleDoc = JsonElement.CreateArrayBuilder(workspace, 1);
                singleDoc.RootElement.AddItem(elements[0]);
                return (JsonElement)singleDoc.RootElement;
            }

            // Check for objects/arrays — D3070
            for (int i = 0; i < count; i++)
            {
                if (elements[i].ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    throw new JsonataException("D3070", "The single argument form of the $sort function can only be used on an array of strings or an array of numbers", 0);
                }
            }

            // Build index array for stable sort
            int[] indices = ArrayPool<int>.Shared.Rent(count);
            for (int i = 0; i < count; i++)
            {
                indices[i] = i;
            }

            try
            {
                Array.Sort(indices, 0, count, Comparer<int>.Create((a, b) =>
                {
                    JsonElement aEl = elements[a];
                    JsonElement bEl = elements[b];

                    if (aEl.ValueKind == JsonValueKind.Number && bEl.ValueKind == JsonValueKind.Number)
                    {
                        int cmp = aEl.GetDouble().CompareTo(bEl.GetDouble());
                        return cmp != 0 ? cmp : a.CompareTo(b);
                    }

                    int strCmp = StringComparer.Ordinal.Compare(
                        CoerceElementToString(aEl),
                        CoerceElementToString(bEl));
                    return strCmp != 0 ? strCmp : a.CompareTo(b);
                }));

                var doc = JsonElement.CreateArrayBuilder(workspace, count);
                JsonElement.Mutable root = doc.RootElement;
                for (int i = 0; i < count; i++)
                {
                    root.AddItem(elements[indices[i]]);
                }

                return (JsonElement)root;
            }
            finally
            {
                ArrayPool<int>.Shared.Return(indices);
            }
        }
        finally
        {
            ArrayPool<JsonElement>.Shared.Return(elements);
        }
    }

    /// <summary>
    /// Coerces a JSON element to its string representation for sort comparisons.
    /// </summary>
    internal static string CoerceElementToString(in JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => element.GetRawText(),
        };
    }

    // ===== Phase 1g: Date/Time Formatting =====

    /// <summary>
    /// JSONata <c>$fromMillis</c> function — converts milliseconds to a date/time string.
    /// </summary>
    public static JsonElement FromMillis(in JsonElement msElement, in JsonElement pictureElement, in JsonElement tzElement, JsonWorkspace workspace)
    {
        if (msElement.IsNullOrUndefined())
        {
            return default;
        }

        if (!FunctionalCompiler.TryCoerceToNumber(msElement, out double millisVal))
        {
            return default;
        }

        var dt = DateTimeOffset.FromUnixTimeMilliseconds((long)millisVal);

        TimeSpan offset = TimeSpan.Zero;
        bool hasTz = false;
        if (tzElement.ValueKind != JsonValueKind.Undefined)
        {
            string tzStr = FunctionalCompiler.CoerceElementToString(tzElement);
            offset = XPathDateTimeFormatter.ParseTimezoneArgument(tzStr);
            hasTz = true;
            dt = dt.ToOffset(offset);
        }

        if (pictureElement.ValueKind == JsonValueKind.Undefined)
        {
            if (hasTz)
            {
                return JsonataHelpers.StringFromString(
                    BuiltInFunctions.FormatIso8601WithOffset(dt, offset), workspace);
            }

            return JsonataHelpers.StringFromString(
                dt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture), workspace);
        }

        string picture = FunctionalCompiler.CoerceElementToString(pictureElement);
        string result = XPathDateTimeFormatter.FormatDateTime(dt, picture);
        return JsonataHelpers.StringFromString(result, workspace);
    }

    /// <summary>
    /// JSONata <c>$toMillis</c> function — parses a date/time string to milliseconds.
    /// </summary>
    public static JsonElement ToMillis(in JsonElement input, in JsonElement pictureElement, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        string str = FunctionalCompiler.CoerceElementToString(input);

        if (pictureElement.ValueKind == JsonValueKind.Undefined)
        {
            Sequence result = BuiltInFunctions.ParseIso8601ToMillis(str, workspace);
            return result.IsUndefined ? default : result.FirstOrDefault;
        }

        string picture = FunctionalCompiler.CoerceElementToString(pictureElement);

        try
        {
            if (XPathDateTimeFormatter.TryParseDateTime(str, picture, out long millis))
            {
                return JsonataHelpers.NumberFromDouble(millis, workspace);
            }
        }
        catch (JsonataException)
        {
            throw;
        }

        return default;
    }

    /// <summary>
    /// JSONata <c>$formatNumber</c> function.
    /// </summary>
    public static JsonElement FormatNumber(in JsonElement input, in JsonElement pictureElement, in JsonElement optionsElement, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined() || pictureElement.IsNullOrUndefined())
        {
            return default;
        }

        if (!FunctionalCompiler.TryCoerceToNumber(input, out double num))
        {
            return default;
        }

        string picture = FunctionalCompiler.CoerceElementToString(pictureElement);
        JsonElement options = optionsElement.ValueKind != JsonValueKind.Undefined ? optionsElement : default;

        return JsonataHelpers.StringFromString(
            BuiltInFunctions.FormatNumberXPath(num, picture, options), workspace);
    }

    /// <summary>
    /// JSONata <c>$formatBase</c> function.
    /// </summary>
    public static JsonElement FormatBase(in JsonElement input, in JsonElement radixElement, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        if (!FunctionalCompiler.TryCoerceToNumber(input, out double num))
        {
            return default;
        }

        int radixInt = 10;
        if (radixElement.ValueKind != JsonValueKind.Undefined)
        {
            if (!FunctionalCompiler.TryCoerceToNumber(radixElement, out double radix))
            {
                return default;
            }

            radixInt = (int)Math.Truncate(radix);
        }

        if (radixInt < 2 || radixInt > 36)
        {
            throw new JsonataException("D3100", $"The radix of the $formatBase function must be between 2 and 36. It was given {radixInt}", 0);
        }

        long numLong = (long)Math.Round(num, MidpointRounding.ToEven);
        bool negative = numLong < 0;
        string result = BuiltInFunctions.ConvertToBase(Math.Abs(numLong), radixInt);
        if (negative)
        {
            result = "-" + result;
        }

        return JsonataHelpers.StringFromString(result, workspace);
    }

    /// <summary>
    /// JSONata <c>$formatInteger</c> function.
    /// </summary>
    public static JsonElement FormatInteger(in JsonElement input, in JsonElement pictureElement, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        string picture = FunctionalCompiler.CoerceElementToString(pictureElement);
        string result;

        if (input.ValueKind == JsonValueKind.Number && input.TryGetInt64(out long longVal))
        {
            result = XPathDateTimeFormatter.FormatInteger(longVal, picture);
        }
        else if (FunctionalCompiler.TryCoerceToNumber(input, out double numVal))
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
            return default;
        }

        return JsonataHelpers.StringFromString(result, workspace);
    }

    /// <summary>
    /// JSONata <c>$parseInteger</c> function.
    /// </summary>
    public static JsonElement ParseInteger(in JsonElement input, in JsonElement pictureElement, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        string str = FunctionalCompiler.CoerceElementToString(input);
        string picture = FunctionalCompiler.CoerceElementToString(pictureElement);

        if (XPathDateTimeFormatter.TryParseInteger(str, picture, out double dblValue))
        {
            return JsonataHelpers.NumberFromDouble(dblValue, workspace);
        }

        return default;
    }

    // ===== String Utility =====
    private static int CountCodePoints(string str)
    {
        int count = 0;
        for (int i = 0; i < str.Length; i++)
        {
            count++;
            if (char.IsHighSurrogate(str[i]) && i + 1 < str.Length && char.IsLowSurrogate(str[i + 1]))
            {
                i++;
            }
        }

        return count;
    }

    private static int CodePointToCharIndex(string str, int codePointIndex)
    {
        int charIdx = 0;
        for (int cpCount = 0; charIdx < str.Length && cpCount < codePointIndex; cpCount++)
        {
            if (char.IsHighSurrogate(str[charIdx]) && charIdx + 1 < str.Length && char.IsLowSurrogate(str[charIdx + 1]))
            {
                charIdx += 2;
            }
            else
            {
                charIdx++;
            }
        }

        return charIdx;
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

        // Sort: comparator returns true if a should be placed AFTER b (JSONata convention)
        Array.Sort(elements, (a, b) => comparator(a, b, workspace) ? 1 : comparator(b, a, workspace) ? -1 : 0);

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
    /// Helper for inline <c>$map</c> with index parameter — maps each element with its index.
    /// </summary>
    public static JsonElement MapElementsWithIndex(
        in JsonElement input,
        Func<JsonElement, JsonElement, JsonWorkspace, JsonElement> transform,
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

            int i = 0;
            foreach (JsonElement item in input.EnumerateArray())
            {
                JsonElement idx = JsonataHelpers.NumberFromDouble(i, workspace);
                JsonElement result = transform(item, idx, workspace);
                if (result.ValueKind != JsonValueKind.Undefined)
                {
                    root.AddItem(result);
                }

                i++;
            }

            return (JsonElement)root;
        }

        // Single value — map once with index 0
        JsonElement single = transform(input, JsonataHelpers.Zero(), workspace);
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
    /// Applies a stage (predicate or numeric index) to the input, matching the runtime's
    /// <c>ApplyStages</c> semantics. If the stage evaluates to a number, it is used as an
    /// array index. Otherwise, the result is checked for truthiness as a predicate filter.
    /// </summary>
    public static JsonElement ApplyStage(
        in JsonElement input,
        Func<JsonElement, JsonWorkspace, JsonElement> stageEval,
        JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        // Evaluate stage once to check type; for arrays, first element determines behavior
        JsonElement probe = input.ValueKind == JsonValueKind.Array && input.GetArrayLength() > 0
            ? input[0]
            : input;
        JsonElement probeResult = stageEval(probe, workspace);

        // Numeric result → use as global array index
        if (probeResult.ValueKind == JsonValueKind.Number)
        {
            int idx = (int)probeResult.GetDouble();
            return ArrayIndex(input, idx);
        }

        // Array of numbers → select elements whose indices appear in the selector
        if (probeResult.ValueKind == JsonValueKind.Array && input.ValueKind == JsonValueKind.Array)
        {
            // Check if ALL elements are numbers (or undefined)
            bool allNumeric = true;
            foreach (JsonElement el in probeResult.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Number && el.ValueKind != JsonValueKind.Undefined)
                {
                    allNumeric = false;
                    break;
                }
            }

            if (allNumeric)
            {
                int inputLen = input.GetArrayLength();

                // Build a set of selected indices (resolving negatives)
                HashSet<int> selectedIndices = [];
                foreach (JsonElement idxEl in probeResult.EnumerateArray())
                {
                    if (idxEl.ValueKind == JsonValueKind.Undefined)
                    {
                        continue;
                    }

                    int idx = (int)idxEl.GetDouble();
                    if (idx < 0)
                    {
                        idx = inputLen + idx;
                    }

                    selectedIndices.Add(idx);
                }

                // Iterate input in natural order, including elements whose index is selected
                var doc = JsonElement.CreateArrayBuilder(workspace, selectedIndices.Count);
                JsonElement.Mutable root = doc.RootElement;
                int count = 0;
                for (int i = 0; i < inputLen; i++)
                {
                    if (selectedIndices.Contains(i))
                    {
                        root.AddItem(input[i]);
                        count++;
                    }
                }

                return count == 0 ? default : count == 1 ? root[0] : (JsonElement)root;
            }
        }

        // Non-numeric → per-element boolean filter
        if (input.ValueKind == JsonValueKind.Array)
        {
            int count = 0;
            var doc = JsonElement.CreateArrayBuilder(workspace, input.GetArrayLength());
            JsonElement.Mutable root = doc.RootElement;

            foreach (JsonElement item in input.EnumerateArray())
            {
                JsonElement stageResult = stageEval(item, workspace);
                if (IsTruthy(stageResult))
                {
                    root.AddItem(item);
                    count++;
                }
            }

            return count == 0 ? default : count == 1 ? root[0] : (JsonElement)root;
        }

        // Single value — test predicate
        return IsTruthy(probeResult) ? input : default;
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

        JsonElement accumulator;
        bool hasInit = initial.ValueKind != JsonValueKind.Undefined;

        if (input.ValueKind == JsonValueKind.Array)
        {
            int len = input.GetArrayLength();
            if (len == 0)
            {
                return hasInit ? initial : default;
            }

            int startIdx;
            if (hasInit)
            {
                accumulator = initial;
                startIdx = 0;
            }
            else
            {
                accumulator = input[0];
                startIdx = 1;
            }

            for (int i = startIdx; i < len; i++)
            {
                accumulator = reducer(accumulator, input[i], workspace);
            }
        }
        else
        {
            // Scalar input treated as single-element sequence
            if (hasInit)
            {
                accumulator = reducer(initial, input, workspace);
            }
            else
            {
                // Single element, no init → the element itself is the result
                accumulator = input;
            }
        }

        return accumulator;
    }

    // ===== Object Construction =====

    /// <summary>
    /// Validates a group-by key expression result, returning the string key or
    /// <c>null</c> if the key is undefined. Throws T1003 for non-string, non-undefined keys.
    /// </summary>
    /// <param name="key">The evaluated key element.</param>
    /// <returns>The string key, or <c>null</c> if the key is undefined.</returns>
    /// <exception cref="JsonataException">
    /// T1003 when the key evaluates to a non-string type (e.g. number, boolean).
    /// </exception>
    public static string? ValidateGroupByKey(in JsonElement key)
    {
        if (key.ValueKind == JsonValueKind.String)
        {
            return key.GetString();
        }

        if (key.IsNullOrUndefined())
        {
            return null;
        }

        throw new JsonataException("T1003", "Key in object structure must evaluate to a string; got: " + key.ValueKind, 0);
    }

    /// <summary>
    /// Per-element group-by for path steps (<c>.{key: value}</c>).
    /// Each element of the input array is individually passed to <see cref="GroupByObject"/>,
    /// producing one object per element. Results are collected with standard singleton
    /// semantics (single result unwrapped, multiple results in array).
    /// </summary>
    /// <param name="input">The input (array or scalar).</param>
    /// <param name="keySelector">Function to extract the group key from each element.</param>
    /// <param name="valueSelector">Function to extract the value from each element.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>An array of objects (or singleton), or <c>default</c> if input is undefined/empty.</returns>
    public static JsonElement GroupByObjectPerElement(
        in JsonElement input,
        Func<JsonElement, JsonWorkspace, string?> keySelector,
        Func<JsonElement, JsonWorkspace, JsonElement> valueSelector,
        JsonWorkspace workspace)
    {
        if (input.ValueKind == JsonValueKind.Array)
        {
            int len = input.GetArrayLength();
            if (len == 0)
            {
                return default;
            }

            var doc = JsonElement.CreateArrayBuilder(workspace, len);
            JsonElement.Mutable root = doc.RootElement;
            int count = 0;

            foreach (JsonElement item in input.EnumerateArray())
            {
                JsonElement result = GroupByObject(item, keySelector, valueSelector, workspace);
                if (result.ValueKind != JsonValueKind.Undefined)
                {
                    root.AddItem(result);
                    count++;
                }
            }

            return count == 0 ? default : count == 1 ? root[0] : (JsonElement)root;
        }

        if (input.ValueKind != JsonValueKind.Undefined)
        {
            return GroupByObject(input, keySelector, valueSelector, workspace);
        }

        return default;
    }

    /// <summary>
    /// Group-by object construction: <c>data{keyExpr: valueExpr}</c>.
    /// Mirrors the runtime's two-phase approach:
    /// Phase 1: Evaluate KEY per element → group ELEMENTS by key.
    /// Phase 2: For each group, build context (single element or JSON array), then
    /// evaluate VALUE on the group context.
    /// </summary>
    /// <param name="input">The input data (array or scalar).</param>
    /// <param name="keySelector">
    /// Evaluates the key expression for each element. Returns <c>null</c> if the key is undefined.
    /// </param>
    /// <param name="valueSelector">Evaluates the value expression on the group context.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>A JSON object with grouped values, or <c>default</c> if input is undefined.</returns>
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

        // Phase 1: Group ELEMENTS by key (not values — value is evaluated per group).
        var groups = new Dictionary<string, List<JsonElement>>(StringComparer.Ordinal);
        var keyOrder = new List<string>();

        void GroupElement(JsonElement elem)
        {
            string? key = keySelector(elem, workspace);
            if (key is null)
            {
                return;
            }

            if (!groups.TryGetValue(key, out List<JsonElement>? list))
            {
                list = new List<JsonElement>();
                groups[key] = list;
                keyOrder.Add(key);
            }

            list.Add(elem);
        }

        if (input.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in input.EnumerateArray())
            {
                GroupElement(item);
            }
        }
        else
        {
            GroupElement(input);
        }

        // Build the result object — return empty object for empty input arrays,
        // but undefined for non-array inputs that produced no groups.
        if (groups.Count == 0)
        {
            if (input.ValueKind == JsonValueKind.Array)
            {
                // Empty array → empty object {}
                var emptyDoc = JsonElement.CreateObjectBuilder(workspace, 0);
                return (JsonElement)emptyDoc.RootElement;
            }

            return default;
        }

        // Phase 2: For each group, build context and evaluate value expression.
        var doc = JsonElement.CreateObjectBuilder(workspace, groups.Count);
        JsonElement.Mutable root = doc.RootElement;

        foreach (string key in keyOrder)
        {
            List<JsonElement> elements = groups[key];

            // Build context: single element or JSON array of elements.
            JsonElement context;
            if (elements.Count == 1)
            {
                context = elements[0];
            }
            else
            {
                var arrDoc = JsonElement.CreateArrayBuilder(workspace, elements.Count);
                JsonElement.Mutable arrRoot = arrDoc.RootElement;
                foreach (JsonElement e in elements)
                {
                    arrRoot.AddItem(e);
                }

                context = (JsonElement)arrRoot;
            }

            JsonElement value = valueSelector(context, workspace);
            if (value.ValueKind == JsonValueKind.Undefined)
            {
                continue;
            }

            root.SetProperty(key, value);
        }

        return (JsonElement)root;
    }

    // ===== Array Flattening =====

    /// <summary>
    /// Flattens one level of array nesting. Implements the JSONata <c>[]</c> flatten
    /// operator when used as a path step: <c>expr.[]</c>.
    /// For arrays of arrays, inner array elements are expanded into the outer array.
    /// Non-array elements pass through unchanged.
    /// </summary>
    /// <param name="data">The input value.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>The flattened result, or <c>default</c> if undefined.</returns>
    public static JsonElement FlattenArray(in JsonElement data, JsonWorkspace workspace)
    {
        if (data.IsNullOrUndefined())
        {
            return default;
        }

        if (data.ValueKind != JsonValueKind.Array)
        {
            return data;
        }

        // Check if any element is an array — if not, return as-is
        bool hasNestedArrays = false;
        foreach (JsonElement item in data.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Array)
            {
                hasNestedArrays = true;
                break;
            }
        }

        if (!hasNestedArrays)
        {
            return data;
        }

        // Flatten one level: expand inner arrays into the result
        int capacity = data.GetArrayLength();
        var doc = JsonElement.CreateArrayBuilder(workspace, capacity * 2);
        JsonElement.Mutable root = doc.RootElement;

        foreach (JsonElement item in data.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement child in item.EnumerateArray())
                {
                    root.AddItem(child);
                }
            }
            else
            {
                root.AddItem(item);
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
            if (values[i].ValueKind != JsonValueKind.Undefined)
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
            if (el.ValueKind != JsonValueKind.Undefined)
            {
                root.AddItem(el);
            }
        }

        return root.Clone();
    }

    /// <summary>
    /// Creates a JSON array from elements, where some elements may need auto-flattening.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In JSONata, array constructors flatten expression results that are arrays,
    /// unless the expression itself is a nested array constructor. The
    /// <paramref name="isArrayCtor"/> mask indicates which elements should NOT be
    /// flattened (because they came from a nested array constructor).
    /// </para>
    /// </remarks>
    /// <param name="elements">The array elements.</param>
    /// <param name="isArrayCtor">Bitmask: bit <c>i</c> is set if element <c>i</c> is from an array constructor (no flatten).</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>A <see cref="JsonElement"/> of kind <see cref="JsonValueKind.Array"/>.</returns>
    public static JsonElement CreateArrayWithFlatten(JsonElement[] elements, long isArrayCtor, JsonWorkspace workspace)
    {
        var doc = JsonElement.CreateArrayBuilder(workspace, elements.Length * 2);
        JsonElement.Mutable root = doc.RootElement;
        for (int i = 0; i < elements.Length; i++)
        {
            JsonElement el = elements[i];
            if (el.ValueKind == JsonValueKind.Undefined)
            {
                continue;
            }

            bool isCons = (isArrayCtor & (1L << i)) != 0;
            if (!isCons && el.ValueKind == JsonValueKind.Array)
            {
                // Flatten: add each child individually
                foreach (JsonElement child in el.EnumerateArray())
                {
                    root.AddItem(child);
                }
            }
            else
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

    private static JsonElement CreateArrayFromPool(JsonElement[] elements, int count, JsonWorkspace workspace)
    {
        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(workspace, count);
        JsonElement.Mutable root = doc.RootElement;
        for (int i = 0; i < count; i++)
        {
            root.AddItem(elements[i]);
        }

        return root.Clone();
    }

    private static JsonElement BinaryArithmetic(
        in JsonElement left, in JsonElement right, JsonWorkspace workspace,
        string op, Func<double, double, double> compute)
    {
        // Only undefined (not null) propagates as "missing value"
        bool lhsUndef = left.ValueKind == JsonValueKind.Undefined;
        bool rhsUndef = right.ValueKind == JsonValueKind.Undefined;

        if (lhsUndef && rhsUndef)
        {
            return default;
        }

        // Type check: null and non-numeric types are errors
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

    /// <summary>
    /// Implements JSONata ordered comparison (<c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>).
    /// Matches the runtime's <c>CompileNumericComparison</c> behavior:
    /// <list type="number">
    ///   <item>Check for invalid types (bool, null, array, object) → throw T2010</item>
    ///   <item>If either side is undefined → return <c>default</c> (undefined)</item>
    ///   <item>Both strings → ordinal string comparison</item>
    ///   <item>Both numbers → numeric comparison</item>
    ///   <item>Type mismatch (string vs number) → throw T2009</item>
    /// </list>
    /// </summary>
    private static JsonElement OrderedCompare(
        in JsonElement left, in JsonElement right, string op, Func<int, bool> predicate)
    {
        // Check for invalid types BEFORE undefined — bool/null/array/object in comparison is always an error
        if (!left.IsNullOrUndefined() && left.ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Array or JsonValueKind.Object)
        {
            throw new JsonataException("T2010", $"The expressions either side of operator \"{op}\" must be both numbers or both strings", 0);
        }

        if (left.ValueKind == JsonValueKind.Null)
        {
            throw new JsonataException("T2010", $"The expressions either side of operator \"{op}\" must be both numbers or both strings", 0);
        }

        if (!right.IsNullOrUndefined() && right.ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Array or JsonValueKind.Object)
        {
            throw new JsonataException("T2010", $"The expressions either side of operator \"{op}\" must be both numbers or both strings", 0);
        }

        if (right.ValueKind == JsonValueKind.Null)
        {
            throw new JsonataException("T2010", $"The expressions either side of operator \"{op}\" must be both numbers or both strings", 0);
        }

        // If either side is undefined (after invalid-type check), return undefined
        if (left.ValueKind == JsonValueKind.Undefined || right.ValueKind == JsonValueKind.Undefined)
        {
            return default;
        }

        // String comparison
        if (left.ValueKind == JsonValueKind.String && right.ValueKind == JsonValueKind.String)
        {
            int result = string.CompareOrdinal(left.GetString(), right.GetString());
            return BooleanElement(predicate(result));
        }

        // Numeric comparison
        if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number)
        {
            double lv = left.GetDouble();
            double rv = right.GetDouble();
            int result = lv < rv ? -1 : lv > rv ? 1 : 0;
            return BooleanElement(predicate(result));
        }

        // Type mismatch (string vs number)
        throw new JsonataException("T2009", "The values either side of the operator must be of the same data type", 0);
    }

    private static bool ElementEquals(in JsonElement left, in JsonElement right)
    {
        // Different types are never equal in JSONata (no cross-type coercion)
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        return left.ValueKind switch
        {
            JsonValueKind.Number => left.GetDouble() == right.GetDouble(),
            JsonValueKind.String => string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal),
            JsonValueKind.True => true,
            JsonValueKind.False => true,
            JsonValueKind.Null => true,
            JsonValueKind.Array => ArrayDeepEquals(left, right),
            JsonValueKind.Object => ObjectDeepEquals(left, right),
            _ => false,
        };
    }

    private static bool ArrayDeepEquals(in JsonElement a, in JsonElement b)
    {
        int lenA = a.GetArrayLength();
        int lenB = b.GetArrayLength();
        if (lenA != lenB)
        {
            return false;
        }

        var enumA = a.EnumerateArray().GetEnumerator();
        var enumB = b.EnumerateArray().GetEnumerator();
        while (enumA.MoveNext() && enumB.MoveNext())
        {
            if (!ElementEquals(enumA.Current, enumB.Current))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ObjectDeepEquals(in JsonElement a, in JsonElement b)
    {
        int countA = a.GetPropertyCount();
        int countB = b.GetPropertyCount();

        if (countA != countB)
        {
            return false;
        }

        foreach (var prop in a.EnumerateObject())
        {
            if (!b.TryGetProperty(prop.Name, out JsonElement bVal))
            {
                return false;
            }

            if (!ElementEquals(prop.Value, bVal))
            {
                return false;
            }
        }

        return true;
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
            JsonValueKind.Number => FunctionalCompiler.FormatNumberLikeJavaScript(value),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => value.GetRawText(),
        };
    }

    /// <summary>
    /// Serializes a JSON element to a compact JSON string, formatting numbers like
    /// JavaScript to match JSONata semantics.
    /// </summary>
    private static string StringifyElement(in JsonElement element, bool prettyPrint = false)
    {
        using var ms = new System.IO.MemoryStream(256);
        using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions
        {
            Indented = prettyPrint,
            NewLine = "\n",
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        WriteStringifiedElement(element, writer);
        writer.Flush();

        ms.Position = 0;
        using var reader = new System.IO.StreamReader(ms);
        return reader.ReadToEnd();
    }

    private static void WriteStringifiedElement(in JsonElement element, Utf8JsonWriter writer)
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
}