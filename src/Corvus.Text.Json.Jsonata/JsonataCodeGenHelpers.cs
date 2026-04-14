// <copyright file="JsonataCodeGenHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Corvus.Runtime.InteropServices;
using Corvus.Text;
using Corvus.Text.Json.Internal;

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
    /// Navigates a chain of property names with JSONata auto-flattening semantics,
    /// using at most one <c>ArrayBuilder</c> for the entire chain regardless of
    /// how many nested arrays are encountered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// At each step, if the current value is an object, the property is looked up directly.
    /// If the current value is an array, all remaining steps are collected into a single
    /// mutable array builder — avoiding the nested-builder overhead of the recursive approach.
    /// </para>
    /// <para>
    /// This matches the runtime compiler's inline name step pattern where a single
    /// <c>SequenceBuilder</c> accumulates results across all path steps.
    /// </para>
    /// </remarks>
    /// <param name="data">The input data element.</param>
    /// <param name="names">The UTF-8 encoded property names to navigate.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>The navigation result, or <c>default</c> if undefined.</returns>
    public static JsonElement NavigatePropertyChain(in JsonElement data, byte[][] names, JsonWorkspace workspace)
    {
        // Walk object steps until we complete the chain or encounter an array.
        JsonElement current = data;

        for (int i = 0; i < names.Length; i++)
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
                // First array encountered — collect via ElementBuffer to avoid builder overhead.
                var buffer = default(ElementBuffer);
                try
                {
                    CollectChainFlatInto(current, names, i, ref buffer);
                    return buffer.Count == 0
                        ? default
                        : buffer.Count == 1
                            ? buffer[0]
                            : buffer.ToResult(workspace);
                }
                finally
                {
                    buffer.Dispose();
                }
            }
            else
            {
                return default;
            }
        }

        return current;
    }

    /// <summary>
    /// Navigates a property chain starting at <paramref name="startIndex"/>, skipping
    /// already-resolved prefix steps. Used by generated code that resolves chain steps
    /// incrementally via nested <c>if</c> checks and falls back to this helper when
    /// an array is encountered partway through, avoiding re-navigation of already-known steps.
    /// </summary>
    /// <param name="data">The element at the start position (already resolved up to <paramref name="startIndex"/>).</param>
    /// <param name="names">The full UTF-8 encoded property names array.</param>
    /// <param name="startIndex">The index into <paramref name="names"/> to start navigating from.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>The navigation result, or <c>default</c> if undefined.</returns>
    public static JsonElement NavigatePropertyChain(in JsonElement data, byte[][] names, int startIndex, JsonWorkspace workspace)
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
                var buffer = default(ElementBuffer);
                try
                {
                    CollectChainFlatInto(current, names, i, ref buffer);
                    return buffer.Count == 0
                        ? default
                        : buffer.Count == 1
                            ? buffer[0]
                            : buffer.ToResult(workspace);
                }
                finally
                {
                    buffer.Dispose();
                }
            }
            else
            {
                return default;
            }
        }

        return current;
    }

    /// <summary>
    /// Fuses a property chain navigation with per-element group-by object construction.
    /// Navigates the chain into an <see cref="ElementBuffer"/> (no intermediate
    /// <see cref="JsonElement.CreateArrayBuilder"/>), then builds <c>{key: value}</c> objects
    /// directly into the result array. This saves one full builder allocation compared to
    /// the separate <see cref="NavigatePropertyChain(in JsonElement, byte[][], JsonWorkspace)"/> + <see cref="SimpleGroupByPerElement"/> path.
    /// </summary>
    /// <param name="data">The input data element.</param>
    /// <param name="chainNames">The UTF-8 encoded property names for the prefix chain.</param>
    /// <param name="keyPropUtf8">UTF-8 encoded key property name (evaluated per element).</param>
    /// <param name="valuePropUtf8">UTF-8 encoded value property name.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>The array of objects, single object, or <c>default</c> if no results.</returns>
    public static JsonElement FusedChainGroupByPerElement(
        in JsonElement data,
        byte[][] chainNames,
        byte[] keyPropUtf8,
        byte[] valuePropUtf8,
        JsonWorkspace workspace)
    {
        var buffer = default(ElementBuffer);
        try
        {
            NavigatePropertyChainInto(data, chainNames, ref buffer);

            if (buffer.Count == 0)
            {
                return default;
            }

            var doc = JsonElement.CreateArrayBuilder(workspace, buffer.Count * 2);
            JsonElement.Mutable root = doc.RootElement;
            int count = 0;

            for (int i = 0; i < buffer.Count; i++)
            {
                JsonElement item = buffer[i];
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!item.TryGetProperty(keyPropUtf8, out JsonElement keyEl))
                {
                    continue;
                }

                if (keyEl.ValueKind == JsonValueKind.Number)
                {
                    throw new JsonataException("T1003", SR.T1003_KeyInObjectStructureMustEvaluateToAStringGotNumber, 0);
                }

                if (keyEl.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                if (!item.TryGetProperty(valuePropUtf8, out JsonElement valEl))
                {
                    continue;
                }

                // Build object directly into the array document via AddItem callback.
                var ctx = new GroupByObjectContext(keyEl, valEl);
                root.AddItem(ctx, GroupByObjectCallback, 1);
                count++;
            }

            return count == 0 ? default : count == 1 ? root[0] : (JsonElement)root;
        }
        finally
        {
            buffer.Dispose();
        }
    }

    private readonly struct GroupByObjectContext
    {
        public readonly JsonElement Key;
        public readonly JsonElement Value;

        public GroupByObjectContext(JsonElement key, JsonElement value)
        {
            Key = key;
            Value = value;
        }
    }

    private static void GroupByObjectCallback(in GroupByObjectContext ctx, ref JsonElement.ObjectBuilder builder)
    {
        using var keyUtf8 = ctx.Key.GetUtf8String();
        builder.AddProperty(keyUtf8.Span, ctx.Value);
    }

    /// <summary>
    /// Navigates a property chain, collecting leaf results into an <see cref="ElementBuffer"/>
    /// instead of creating a <see cref="JsonElement.CreateArrayBuilder"/>-backed array.
    /// This avoids MetadataDb overhead for intermediate results — the caller iterates the
    /// buffer directly and only materializes when the final result is needed.
    /// </summary>
    /// <param name="data">The input data element.</param>
    /// <param name="names">The UTF-8 encoded property names for each step.</param>
    /// <param name="buffer">The buffer to collect results into.</param>
    public static void NavigatePropertyChainInto(
        in JsonElement data,
        byte[][] names,
        ref ElementBuffer buffer)
    {
        NavigatePropertyChainInto(data, names, 0, ref buffer);
    }

    /// <summary>
    /// Navigates a property chain starting at <paramref name="startIndex"/> into an
    /// <see cref="ElementBuffer"/>, skipping already-resolved prefix steps.
    /// </summary>
    /// <param name="data">The element at the start position.</param>
    /// <param name="names">The full UTF-8 encoded property names array.</param>
    /// <param name="startIndex">The index into <paramref name="names"/> to start navigating from.</param>
    /// <param name="buffer">The buffer to collect results into.</param>
    public static void NavigatePropertyChainInto(
        in JsonElement data,
        byte[][] names,
        int startIndex,
        ref ElementBuffer buffer)
    {
        JsonElement current = data;

        for (int i = startIndex; i < names.Length; i++)
        {
            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty((ReadOnlySpan<byte>)names[i], out current))
                {
                    return;
                }
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                CollectChainFlatInto(current, names, i, ref buffer);
                return;
            }
            else
            {
                return;
            }
        }

        // Completed chain without hitting an array — single result (with auto-flatten)
        buffer.AddFlatten(current);
    }

    /// <summary>
    /// Recursively collects property chain results from an array into an <see cref="ElementBuffer"/>.
    /// Mirrors <see cref="CollectChainFlat"/> but avoids MetadataDb-based accumulation.
    /// </summary>
    private static void CollectChainFlatInto(
        in JsonElement array,
        byte[][] names,
        int stepIndex,
        ref ElementBuffer buffer)
    {
        byte[] name = names[stepIndex];
        int nextStep = stepIndex + 1;
        bool isLastStep = nextStep >= names.Length;

        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                if (item.TryGetProperty((ReadOnlySpan<byte>)name, out var val))
                {
                    if (isLastStep)
                    {
                        buffer.AddFlatten(val);
                    }
                    else
                    {
                        ContinueChainFlatInto(val, names, nextStep, ref buffer);
                    }
                }
            }
            else if (item.ValueKind == JsonValueKind.Array)
            {
                CollectChainFlatInto(item, names, stepIndex, ref buffer);
            }
        }
    }

    /// <summary>
    /// Continues navigating remaining chain steps from a single value into an <see cref="ElementBuffer"/>.
    /// </summary>
    private static void ContinueChainFlatInto(
        in JsonElement value,
        byte[][] names,
        int nextIndex,
        ref ElementBuffer buffer)
    {
        JsonElement current = value;

        for (int i = nextIndex; i < names.Length; i++)
        {
            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty((ReadOnlySpan<byte>)names[i], out current))
                {
                    return;
                }
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                CollectChainFlatInto(current, names, i, ref buffer);
                return;
            }
            else
            {
                return;
            }
        }

        buffer.AddFlatten(current);
    }

    /// <summary>
    /// Navigates a property chain with fused constant-index and string-equality predicates.
    /// This is the codegen equivalent of the runtime's EvalPropertyChainWithPredicates —
    /// a single tight loop handles the entire path including predicates, avoiding all
    /// delegate dispatch and intermediate element allocations.
    /// </summary>
    /// <param name="data">The input data element.</param>
    /// <param name="names">The UTF-8 encoded property names for each step.</param>
    /// <param name="constantIndices">Per-step constant index (-1 = none). Null if no indices.</param>
    /// <param name="equalityPredicates">Per-step (propName, expectedValues[]) pairs. Null entries = no predicate.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>The navigation result, or <c>default</c> if undefined.</returns>
    public static JsonElement NavigatePropertyChainWithPredicates(
        in JsonElement data,
        byte[][] names,
        int[]? constantIndices,
        (byte[] PropName, byte[][] ExpectedValues)[]? equalityPredicates,
        JsonWorkspace workspace)
    {
        return FusedEvalFromStep(data, names, constantIndices, equalityPredicates, 0, workspace);
    }

    private static JsonElement FusedEvalFromStep(
        in JsonElement input,
        byte[][] names,
        int[]? constantIndices,
        (byte[] PropName, byte[][] ExpectedValues)[]? equalityPredicates,
        int startStep,
        JsonWorkspace workspace)
    {
        JsonElement current = input;

        for (int step = startStep; step < names.Length; step++)
        {
            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty((ReadOnlySpan<byte>)names[step], out current))
                {
                    return default;
                }

                // Apply constant index if present for this step
                if (constantIndices?[step] >= 0)
                {
                    if (current.ValueKind == JsonValueKind.Array)
                    {
                        int idx = constantIndices[step];
                        if (idx < current.GetArrayLength())
                        {
                            current = current[idx];
                        }
                        else
                        {
                            return default;
                        }
                    }
                    else
                    {
                        // Singleton: index 0 returns the value itself
                        if (constantIndices[step] != 0)
                        {
                            return default;
                        }
                    }
                }

                // Apply equality predicate if present for this step
                else if (equalityPredicates is not null
                         && step < equalityPredicates.Length
                         && equalityPredicates[step].PropName is not null)
                {
                    var pred = equalityPredicates[step];
                    if (current.ValueKind == JsonValueKind.Array)
                    {
                        // Filter array elements matching the predicate, then continue remaining steps
                        return FusedFilterAndContinue(current, names, constantIndices, equalityPredicates, step, pred.PropName, pred.ExpectedValues, workspace);
                    }
                    else if (current.ValueKind == JsonValueKind.Object)
                    {
                        // Singleton object: check if it matches
                        if (!FusedMatchesEqualityPredicate(current, pred.PropName, pred.ExpectedValues))
                        {
                            return default;
                        }
                    }
                    else
                    {
                        return default;
                    }
                }
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                // Array input: collect-then-predicate semantics
                return FusedCollectAndContinue(current, names, constantIndices, equalityPredicates, step, workspace);
            }
            else
            {
                return default;
            }
        }

        return current;
    }

    private static bool FusedMatchesEqualityPredicate(in JsonElement element, byte[] propName, byte[][] expectedValues)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!element.TryGetProperty((ReadOnlySpan<byte>)propName, out var propValue))
        {
            return false;
        }

        if (propValue.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        for (int i = 0; i < expectedValues.Length; i++)
        {
            if (propValue.ValueEquals(expectedValues[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static JsonElement FusedFilterAndContinue(
        in JsonElement array,
        byte[][] names,
        int[]? constantIndices,
        (byte[] PropName, byte[][] ExpectedValues)[]? equalityPredicates,
        int step,
        byte[] propName,
        byte[][] expectedValues,
        JsonWorkspace workspace)
    {
        var buffer = default(ElementBuffer);
        try
        {
            foreach (JsonElement item in array.EnumerateArray())
            {
                if (FusedMatchesEqualityPredicate(item, propName, expectedValues))
                {
                    if (step + 1 < names.Length)
                    {
                        JsonElement result = FusedEvalFromStep(item, names, constantIndices, equalityPredicates, step + 1, workspace);
                        buffer.AddFlatten(result);
                    }
                    else
                    {
                        buffer.Add(item);
                    }
                }
            }

            return buffer.ToResult(workspace);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    private static JsonElement FusedCollectAndContinue(
        in JsonElement array,
        byte[][] names,
        int[]? constantIndices,
        (byte[] PropName, byte[][] ExpectedValues)[]? equalityPredicates,
        int step,
        JsonWorkspace workspace)
    {
        bool hasIndexThisStep = constantIndices?[step] >= 0;
        bool hasEqPredThisStep = equalityPredicates is not null
                                 && step < equalityPredicates.Length
                                 && equalityPredicates[step].PropName is not null;
        bool globalIndex = hasIndexThisStep && step == 0;
        bool perElementIndex = hasIndexThisStep && step > 0;

        var buffer = default(ElementBuffer);
        try
        {
            foreach (JsonElement item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    if (item.TryGetProperty((ReadOnlySpan<byte>)names[step], out var propValue))
                    {
                        if (perElementIndex)
                        {
                            if (propValue.ValueKind == JsonValueKind.Array)
                            {
                                int idx = constantIndices![step];
                                if (idx < propValue.GetArrayLength())
                                {
                                    propValue = propValue[idx];
                                }
                                else
                                {
                                    continue;
                                }
                            }
                            else if (constantIndices![step] != 0)
                            {
                                continue;
                            }

                            if (step + 1 < names.Length)
                            {
                                JsonElement result = FusedEvalFromStep(propValue, names, constantIndices, equalityPredicates, step + 1, workspace);
                                buffer.AddFlatten(result);
                            }
                            else
                            {
                                buffer.AddFlatten(propValue);
                            }
                        }
                        else if (hasEqPredThisStep)
                        {
                            var pred = equalityPredicates![step];
                            if (propValue.ValueKind == JsonValueKind.Array)
                            {
                                foreach (JsonElement subItem in propValue.EnumerateArray())
                                {
                                    if (FusedMatchesEqualityPredicate(subItem, pred.PropName, pred.ExpectedValues))
                                    {
                                        if (step + 1 < names.Length)
                                        {
                                            JsonElement result = FusedEvalFromStep(subItem, names, constantIndices, equalityPredicates, step + 1, workspace);
                                            buffer.AddFlatten(result);
                                        }
                                        else
                                        {
                                            buffer.Add(subItem);
                                        }
                                    }
                                }
                            }
                            else if (FusedMatchesEqualityPredicate(propValue, pred.PropName, pred.ExpectedValues))
                            {
                                if (step + 1 < names.Length)
                                {
                                    JsonElement result = FusedEvalFromStep(propValue, names, constantIndices, equalityPredicates, step + 1, workspace);
                                    buffer.AddFlatten(result);
                                }
                                else
                                {
                                    buffer.Add(propValue);
                                }
                            }
                        }
                        else
                        {
                            buffer.AddFlatten(propValue);
                        }
                    }
                }
                else if (item.ValueKind == JsonValueKind.Array)
                {
                    JsonElement nested = FusedCollectAndContinue(item, names, constantIndices, equalityPredicates, step, workspace);
                    buffer.AddFlatten(nested);
                }
            }

            // Global index (step 0): apply index to collected results
            if (globalIndex)
            {
                int idx = constantIndices![step];
                if (idx >= buffer.Count)
                {
                    return default;
                }

                JsonElement indexed = buffer[idx];

                if (step + 1 >= names.Length)
                {
                    return indexed;
                }

                return FusedEvalFromStep(indexed, names, constantIndices, equalityPredicates, step + 1, workspace);
            }

            // No per-element predicate and more steps: continue per-element on collected results
            if (!perElementIndex && !hasEqPredThisStep && step + 1 < names.Length)
            {
                var resultBuffer = default(ElementBuffer);
                try
                {
                    for (int i = 0; i < buffer.Count; i++)
                    {
                        JsonElement result = FusedEvalFromStep(buffer[i], names, constantIndices, equalityPredicates, step + 1, workspace);
                        resultBuffer.AddFlatten(result);
                    }

                    return resultBuffer.ToResult(workspace);
                }
                finally
                {
                    resultBuffer.Dispose();
                }
            }

            return buffer.ToResult(workspace);
        }
        finally
        {
            buffer.Dispose();
        }
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
    /// Navigates a single property over an array and returns the result as an array.
    /// Builds directly into the CVB array builder (no intermediate <see cref="ElementBuffer"/>).
    /// Used for <c>path[]</c> syntax where the result must always be an array.
    /// </summary>
    /// <param name="data">The input data element (typically an array).</param>
    /// <param name="name">The UTF-8 encoded property name.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>The array result, or <c>default</c> if no matches found.</returns>
    public static JsonElement NavigatePropertyToArray(in JsonElement data, byte[] name, JsonWorkspace workspace)
    {
        if (data.ValueKind == JsonValueKind.Object)
        {
            if (data.TryGetProperty((ReadOnlySpan<byte>)name, out JsonElement result))
            {
                return KeepSingletonArray(result, workspace);
            }

            return default;
        }

        if (data.ValueKind != JsonValueKind.Array)
        {
            return default;
        }

        // Build the flat array directly via CVB — iterate inside the builder callback.
        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            (data, name),
            static (in (JsonElement Array, byte[] Name) ctx, ref JsonElement.ArrayBuilder builder) =>
            {
                CollectPropertyFlatIntoBuilder(ctx.Array, ctx.Name, ref builder);
            },
            estimatedMemberCount: data.GetArrayLength() * 2 + 2);

        // If nothing was collected, return undefined (not an empty array).
        if (doc.RootElement.GetArrayLength() == 0)
        {
            return default;
        }

        return (JsonElement)doc.RootElement;
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
    /// Fused descendant + property navigation. Recursively collects the named property
    /// from all descendant objects in a single pass, avoiding intermediate array materialization.
    /// </summary>
    /// <param name="input">The input element to traverse.</param>
    /// <param name="name">The UTF-8 encoded property name to collect.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>The collected values, or <c>default</c> if none found.</returns>
    public static JsonElement EnumerateDescendantProperty(in JsonElement input, byte[] name, JsonWorkspace workspace)
    {
        var buffer = default(ElementBuffer);
        try
        {
            CollectDescendantProperty(input, name, ref buffer);

            return buffer.Count == 0
                ? default
                : buffer.Count == 1
                    ? buffer[0]
                    : buffer.ToResult(workspace);
        }
        finally
        {
            buffer.Dispose();
        }

        static void CollectDescendantProperty(in JsonElement element, byte[] name, ref ElementBuffer buffer)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty((ReadOnlySpan<byte>)name, out JsonElement val))
                {
                    buffer.AddFlatten(val);
                }

                foreach (var prop in element.EnumerateObject())
                {
                    CollectDescendantProperty(prop.Value, name, ref buffer);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in element.EnumerateArray())
                {
                    CollectDescendantProperty(item, name, ref buffer);
                }
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
            throw new JsonataException("D1002", SR.D1002_CannotNegateANonNumericValue, 0);
        }

        return JsonataHelpers.NumberFromDouble(-value.GetDouble(), workspace);
    }

    // ===== Raw-double arithmetic (matches runtime Sequence.FromDouble pattern) =====
    // These methods keep intermediate arithmetic results as raw doubles,
    // avoiding the double→UTF-8→FixedJsonValueDocument→GetDouble round-trip
    // that occurs with the JsonElement-based helpers above.
    // NaN is used as a sentinel for undefined propagation.

    /// <summary>
    /// Extracts a <see langword="double"/> from a left operand <see cref="JsonElement"/> for arithmetic.
    /// Returns <see cref="double.NaN"/> for undefined (propagation sentinel).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ToArithmeticDoubleLeft(in JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.GetDouble();
        }

        if (element.ValueKind == JsonValueKind.Undefined)
        {
            return double.NaN;
        }

        throw new JsonataException("T2001", SR.T2001_TheLeftSideOfTheArithmeticExpressionIsNotANumber, 0);
    }

    /// <summary>
    /// Extracts a <see langword="double"/> from a right operand <see cref="JsonElement"/> for arithmetic.
    /// Returns <see cref="double.NaN"/> for undefined (propagation sentinel).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ToArithmeticDoubleRight(in JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.GetDouble();
        }

        if (element.ValueKind == JsonValueKind.Undefined)
        {
            return double.NaN;
        }

        throw new JsonataException("T2002", SR.T2002_TheRightSideOfTheArithmeticExpressionIsNotANumber, 0);
    }

    /// <summary>
    /// Raw-double addition. NaN inputs propagate as undefined.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ArithmeticAdd(double left, double right)
    {
        if (double.IsNaN(left) || double.IsNaN(right))
        {
            return double.NaN;
        }

        double result = left + right;
        if (double.IsInfinity(result))
        {
            throw new JsonataException("D1001", SR.D1001_NumberOutOfRange, 0);
        }

        return result;
    }

    /// <summary>
    /// Raw-double subtraction. NaN inputs propagate as undefined.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ArithmeticSubtract(double left, double right)
    {
        if (double.IsNaN(left) || double.IsNaN(right))
        {
            return double.NaN;
        }

        double result = left - right;
        if (double.IsInfinity(result))
        {
            throw new JsonataException("D1001", SR.D1001_NumberOutOfRange, 0);
        }

        return result;
    }

    /// <summary>
    /// Raw-double multiplication. NaN inputs propagate as undefined.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ArithmeticMultiply(double left, double right)
    {
        if (double.IsNaN(left) || double.IsNaN(right))
        {
            return double.NaN;
        }

        double result = left * right;
        if (double.IsInfinity(result))
        {
            throw new JsonataException("D1001", SR.D1001_NumberOutOfRange, 0);
        }

        return result;
    }

    /// <summary>
    /// Raw-double division. NaN inputs propagate as undefined.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ArithmeticDivide(double left, double right)
    {
        if (double.IsNaN(left) || double.IsNaN(right))
        {
            return double.NaN;
        }

        double result = left / right;
        if (double.IsInfinity(result) || double.IsNaN(result))
        {
            throw new JsonataException("D1001", SR.D1001_NumberOutOfRange, 0);
        }

        return result;
    }

    /// <summary>
    /// Raw-double modulo. NaN inputs propagate as undefined.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ArithmeticModulo(double left, double right)
    {
        if (double.IsNaN(left) || double.IsNaN(right))
        {
            return double.NaN;
        }

        double result = left % right;
        if (double.IsNaN(result))
        {
            throw new JsonataException("D1001", SR.D1001_NumberOutOfRange, 0);
        }

        return result;
    }

    /// <summary>
    /// Materializes a raw <see langword="double"/> to a <see cref="JsonElement"/>.
    /// NaN (undefined sentinel) returns <c>default</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement DoubleToElement(double value, JsonWorkspace workspace)
    {
        if (double.IsNaN(value))
        {
            return default;
        }

        return JsonataHelpers.NumberFromDouble(value, workspace);
    }

    /// <summary>
    /// Multi-operand string concatenation. Uses a single <see cref="Utf8ValueStringBuilder"/>
    /// pass instead of creating intermediate string elements.
    /// </summary>
    public static JsonElement StringConcat3(
        in JsonElement a, in JsonElement b, in JsonElement c, JsonWorkspace workspace)
    {
        Utf8ValueStringBuilder sb = new(stackalloc byte[256]);
        try
        {
            AppendCoercedValue(a, ref sb);
            AppendCoercedValue(b, ref sb);
            AppendCoercedValue(c, ref sb);
            return JsonataHelpers.StringFromUnescapedUtf8(sb.AsSpan(), workspace);
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Multi-operand string concatenation (4 operands).
    /// </summary>
    public static JsonElement StringConcat4(
        in JsonElement a, in JsonElement b, in JsonElement c, in JsonElement d,
        JsonWorkspace workspace)
    {
        Utf8ValueStringBuilder sb = new(stackalloc byte[256]);
        try
        {
            AppendCoercedValue(a, ref sb);
            AppendCoercedValue(b, ref sb);
            AppendCoercedValue(c, ref sb);
            AppendCoercedValue(d, ref sb);
            return JsonataHelpers.StringFromUnescapedUtf8(sb.AsSpan(), workspace);
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Multi-operand string concatenation (5 operands).
    /// </summary>
    public static JsonElement StringConcat5(
        in JsonElement a, in JsonElement b, in JsonElement c, in JsonElement d,
        in JsonElement e, JsonWorkspace workspace)
    {
        Utf8ValueStringBuilder sb = new(stackalloc byte[256]);
        try
        {
            AppendCoercedValue(a, ref sb);
            AppendCoercedValue(b, ref sb);
            AppendCoercedValue(c, ref sb);
            AppendCoercedValue(d, ref sb);
            AppendCoercedValue(e, ref sb);
            return JsonataHelpers.StringFromUnescapedUtf8(sb.AsSpan(), workspace);
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Multi-operand string concatenation (6+ operands).
    /// </summary>
    public static JsonElement StringConcatMany(JsonWorkspace workspace, params JsonElement[] elements)
    {
        Utf8ValueStringBuilder sb = new(stackalloc byte[256]);
        try
        {
            for (int i = 0; i < elements.Length; i++)
            {
                AppendCoercedValue(elements[i], ref sb);
            }

            return JsonataHelpers.StringFromUnescapedUtf8(sb.AsSpan(), workspace);
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Creates a <see cref="ConcatBuilder"/> for zero-allocation string concatenation
    /// in generated code. The caller provides a stack-allocated initial buffer.
    /// </summary>
    /// <param name="initialBuffer">A stack-allocated byte buffer (typically 256 bytes).</param>
    /// <returns>A new <see cref="ConcatBuilder"/> that must be disposed after use.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ConcatBuilder BeginConcat(Span<byte> initialBuffer) => new(initialBuffer);

    /// <summary>
    /// Begins a concat operation backed by a rented <see cref="ArrayPool{T}"/> buffer.
    /// Use <see cref="ConcatBuilder.DetachWrittenBytes"/> to take ownership of the buffer
    /// when the result needs to outlive the builder (e.g. to pass through a context tuple).
    /// </summary>
    /// <param name="initialCapacity">The initial buffer size to rent.</param>
    /// <returns>A <see cref="ConcatBuilder"/> backed by a rented buffer.</returns>
    public static ConcatBuilder BeginConcatRented(int initialCapacity = 256)
    {
        byte[] buf = ArrayPool<byte>.Shared.Rent(initialCapacity);
        return new ConcatBuilder(buf);
    }

    /// <summary>
    /// A ref struct for building concatenated strings from a mix of <see cref="JsonElement"/>
    /// values and UTF-8 literal spans. Wraps the internal <see cref="Utf8ValueStringBuilder"/>
    /// to provide a public API for generated code.
    /// </summary>
    public ref struct ConcatBuilder
    {
        private Utf8ValueStringBuilder _sb;
        private byte[]? _initialRentedBuffer;

        internal ConcatBuilder(Span<byte> initialBuffer)
        {
            _sb = new Utf8ValueStringBuilder(initialBuffer);
            _initialRentedBuffer = null;
        }

        internal ConcatBuilder(byte[] rentedBuffer)
        {
            _sb = new Utf8ValueStringBuilder(rentedBuffer);
            _initialRentedBuffer = rentedBuffer;
        }

        /// <summary>
        /// Appends a <see cref="JsonElement"/> value, coerced to its string representation.
        /// Undefined and null values append nothing.
        /// </summary>
        /// <param name="value">The JSON element to append.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendElement(in JsonElement value) => AppendCoercedValue(value, ref _sb);

        /// <summary>
        /// Appends a UTF-8 literal span directly (no coercion, no allocation).
        /// </summary>
        /// <param name="literal">The UTF-8 bytes to append.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendLiteral(ReadOnlySpan<byte> literal) => _sb.Append(literal);

        /// <summary>
        /// Appends the result of auto-mapping a property over a source element directly into
        /// the concat buffer, avoiding intermediate document creation.
        /// When the source is an array, iterates elements and writes <c>[val1,val2,...]</c>
        /// as raw JSON text. When the source is an object, extracts the property and appends
        /// its coerced string representation. When the source is undefined/null, appends nothing.
        /// </summary>
        /// <param name="source">The source element (may be object, array, or undefined).</param>
        /// <param name="propertyName">The UTF-8 property name to extract.</param>
        public void AppendAutoMap(in JsonElement source, byte[] propertyName)
        {
            if (source.ValueKind == JsonValueKind.Array)
            {
                _sb.Append((byte)'[');
                bool first = true;
                foreach (JsonElement el in source.EnumerateArray())
                {
                    if (el.TryGetProperty(propertyName, out JsonElement val) && !val.IsNullOrUndefined())
                    {
                        if (!first)
                        {
                            _sb.Append((byte)',');
                        }

                        using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(val);
                        _sb.Append(raw.Span);
                        first = false;
                    }
                }

                _sb.Append((byte)']');
            }
            else if (source.ValueKind == JsonValueKind.Object
                     && source.TryGetProperty(propertyName, out JsonElement val))
            {
                AppendCoercedValue(val, ref _sb);
            }

            // Undefined/null/other kinds: nothing to append (matches RT's undefined → no-op)
        }

        /// <summary>
        /// Completes the concatenation and returns the result as a string <see cref="JsonElement"/>.
        /// </summary>
        /// <param name="workspace">The workspace for the result element.</param>
        /// <returns>A string <see cref="JsonElement"/> containing the concatenated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsonElement Complete(JsonWorkspace workspace) =>
            JsonataHelpers.StringFromUnescapedUtf8(_sb.AsSpan(), workspace);

        /// <summary>
        /// Detaches the written bytes into a caller-owned <see langword="byte"/>[] from
        /// <see cref="ArrayPool{T}.Shared"/>. Only valid when constructed via
        /// <see cref="BeginConcatRented"/>. Disposes this builder. The caller must return
        /// the result buffer to <see cref="ArrayPool{T}.Shared"/> when done.
        /// </summary>
        /// <returns>A tuple of the buffer and the number of valid bytes written.</returns>
        public (byte[] Buffer, int Length) DetachWrittenBytes()
        {
            int length = _sb.Length;
            (byte[]? grownBuf, _) = _sb.GetRentedBufferAndLengthAndDispose();
            if (grownBuf != null)
            {
                // Builder grew beyond initial buffer; return initial, use grown
                if (_initialRentedBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(_initialRentedBuffer);
                }

                return (grownBuf, length);
            }

            // Data is still in the initial rented buffer (no growth) — zero copy
            System.Diagnostics.Debug.Assert(_initialRentedBuffer != null, "DetachWrittenBytes requires BeginConcatRented");
            return (_initialRentedBuffer!, length);
        }

        /// <summary>
        /// Releases resources used by this builder. If constructed via
        /// <see cref="BeginConcatRented"/>, returns the initial rented buffer.
        /// </summary>
        public void Dispose()
        {
            _sb.Dispose();
            if (_initialRentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(_initialRentedBuffer);
                _initialRentedBuffer = null;
            }
        }
    }

    /// <summary>
    /// JSONata range (..) operator. Builds an array of integers from <paramref name="left"/> to <paramref name="right"/>.
    /// </summary>
    public static JsonElement Range(in JsonElement left, in JsonElement right, JsonWorkspace workspace)
    {
        if (!left.IsUndefined() && left.ValueKind != JsonValueKind.Number)
        {
            throw new JsonataException("T2003", SR.T2003_TheLeftSideOfTheRangeOperatorMustEvaluateToAnInteger, 0);
        }

        if (!right.IsUndefined() && right.ValueKind != JsonValueKind.Number)
        {
            throw new JsonataException("T2004", SR.T2004_TheRightSideOfTheRangeOperatorMustEvaluateToAnInteger, 0);
        }

        if (left.IsUndefined() || right.IsUndefined())
        {
            return default;
        }

        double start = left.GetDouble();
        double end = right.GetDouble();

        if (start != Math.Floor(start))
        {
            throw new JsonataException("T2003", SR.T2003_TheLeftSideOfTheRangeOperatorMustEvaluateToAnInteger, 0);
        }

        if (end != Math.Floor(end))
        {
            throw new JsonataException("T2004", SR.T2004_TheRightSideOfTheRangeOperatorMustEvaluateToAnInteger, 0);
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
            throw new JsonataException("D2014", SR.D2014_RangeExpressionGeneratesTooManyResults, 0);
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

    // ── Fused numeric comparison → bool (avoids NumberFromDouble + BooleanElement + IsTruthy) ──

    /// <summary>
    /// Fused <c>element &gt; constant</c> comparison that returns <see langword="bool"/> directly.
    /// Avoids materializing the constant as a <see cref="JsonElement"/> via <c>NumberFromDouble</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareNumberGT(in JsonElement left, double right)
    {
        if (left.ValueKind == JsonValueKind.Number)
        {
            return left.GetDouble() > right;
        }

        if (left.IsNullOrUndefined())
        {
            return false;
        }

        return ThrowCompareError<bool>(">");
    }

    /// <summary>
    /// Fused <c>element &gt;= constant</c> comparison that returns <see langword="bool"/> directly.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareNumberGTE(in JsonElement left, double right)
    {
        if (left.ValueKind == JsonValueKind.Number)
        {
            return left.GetDouble() >= right;
        }

        if (left.IsNullOrUndefined())
        {
            return false;
        }

        return ThrowCompareError<bool>(">=");
    }

    /// <summary>
    /// Fused <c>element &lt; constant</c> comparison that returns <see langword="bool"/> directly.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareNumberLT(in JsonElement left, double right)
    {
        if (left.ValueKind == JsonValueKind.Number)
        {
            return left.GetDouble() < right;
        }

        if (left.IsNullOrUndefined())
        {
            return false;
        }

        return ThrowCompareError<bool>("<");
    }

    /// <summary>
    /// Fused <c>element &lt;= constant</c> comparison that returns <see langword="bool"/> directly.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareNumberLTE(in JsonElement left, double right)
    {
        if (left.ValueKind == JsonValueKind.Number)
        {
            return left.GetDouble() <= right;
        }

        if (left.IsNullOrUndefined())
        {
            return false;
        }

        return ThrowCompareError<bool>("<=");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T ThrowCompareError<T>(string op)
    {
        throw new JsonataException("T2010", SR.Format(SR.T2010_ExpressionsMustBeNumbersOrStringsWithOp, op), 0);
    }

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
        Utf8ValueStringBuilder sb = new(stackalloc byte[256]);
        try
        {
            AppendCoercedValue(left, ref sb);
            AppendCoercedValue(right, ref sb);
            return JsonataHelpers.StringFromUnescapedUtf8(sb.AsSpan(), workspace);
        }
        finally
        {
            sb.Dispose();
        }
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

        if (value.ValueKind == JsonValueKind.String)
        {
            return value;
        }

        Utf8ValueStringBuilder sb = new(stackalloc byte[256]);
        try
        {
            AppendCoercedValue(value, ref sb);
            return JsonataHelpers.StringFromUnescapedUtf8(sb.AsSpan(), workspace);
        }
        finally
        {
            sb.Dispose();
        }
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
                    throw new JsonataException("T0412", SR.T0412_Argument1OfFunctionSumMustBeAnArrayOfNumbers, 0);
                }
            }
        }
        else if (input.ValueKind == JsonValueKind.Number)
        {
            sum = input.GetDouble();
        }
        else
        {
            throw new JsonataException("T0412", SR.T0412_Argument1OfFunctionSumMustBeAnArrayOfNumbers, 0);
        }

        return JsonataHelpers.NumberFromDouble(sum, workspace);
    }

    /// <summary>
    /// Fused <c>$sum</c> over per-element double-producing step. Evaluates the step function
    /// per element and sums the raw doubles directly, avoiding the intermediate
    /// <see cref="JsonElement"/> array and per-element <c>NumberFromDouble</c> materialisation.
    /// Only one <c>NumberFromDouble</c> call is made for the final result.
    /// </summary>
    /// <param name="data">The input array (or single value) to iterate.</param>
    /// <param name="step">A function that produces a raw <see langword="double"/> per element.
    /// Returns <see cref="double.NaN"/> for undefined/non-numeric inputs.</param>
    /// <param name="workspace">The workspace for the final result document.</param>
    /// <returns>The sum as a <see cref="JsonElement"/> number, or <c>default</c> if undefined.</returns>
    public static JsonElement SumOverElementsDouble(
        in JsonElement data,
        Func<JsonElement, JsonWorkspace, double> step,
        JsonWorkspace workspace)
    {
        double raw = SumOverElementsDoubleRaw(data, step, workspace);
        return double.IsNaN(raw) ? default : JsonataHelpers.NumberFromDouble(raw, workspace);
    }

    /// <summary>
    /// Raw double variant of <see cref="SumOverElementsDouble"/>: returns the
    /// accumulated sum as a <see langword="double"/> (<see cref="double.NaN"/>
    /// when the input is undefined or a single NaN element). Avoids the
    /// <see cref="JsonataHelpers.NumberFromDouble"/> document allocation so the
    /// caller can pass the result directly to
    /// <see cref="JsonElement.ObjectBuilder.AddProperty(ReadOnlySpan{byte}, double, bool, bool)"/>.
    /// </summary>
    public static double SumOverElementsDoubleRaw(
        in JsonElement data,
        Func<JsonElement, JsonWorkspace, double> step,
        JsonWorkspace workspace)
    {
        if (data.ValueKind == JsonValueKind.Undefined)
        {
            return double.NaN;
        }

        double sum = 0;

        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in data.EnumerateArray())
            {
                double val = step(item, workspace);
                if (!double.IsNaN(val))
                {
                    sum += val;
                }
            }
        }
        else
        {
            double val = step(data, workspace);
            if (double.IsNaN(val))
            {
                return double.NaN;
            }

            sum = val;
        }

        return sum;
    }

    /// <summary>
    /// Fused sum-over-property-chain: navigates a property name chain from the input,
    /// auto-flattening arrays at each step, then applies <paramref name="step"/> to each
    /// leaf element and accumulates the sum. Returns <see cref="double.NaN"/> when no
    /// numeric values are found.
    /// </summary>
    /// <remarks>
    /// This avoids the intermediate <see cref="JsonElement"/> array that
    /// <see cref="NavigatePropertyChain(in JsonElement, byte[][], JsonWorkspace)"/>
    /// would create — no document builder is allocated for the chain navigation.
    /// </remarks>
    public static double SumOverChainDoubleRaw(
        in JsonElement input,
        byte[][] names,
        Func<JsonElement, JsonWorkspace, double> step,
        JsonWorkspace workspace)
    {
        double sum = 0;
        bool hasValue = false;
        SumOverChainCore(input, names, 0, step, workspace, ref sum, ref hasValue);
        return hasValue ? sum : double.NaN;
    }

    /// <summary>
    /// <see cref="JsonElement"/>-returning variant of <see cref="SumOverChainDoubleRaw"/>.
    /// </summary>
    public static JsonElement SumOverChainDouble(
        in JsonElement input,
        byte[][] names,
        Func<JsonElement, JsonWorkspace, double> step,
        JsonWorkspace workspace)
    {
        double raw = SumOverChainDoubleRaw(input, names, step, workspace);
        return double.IsNaN(raw) ? default : JsonataHelpers.NumberFromDouble(raw, workspace);
    }

    private static void SumOverChainCore(
        in JsonElement current,
        byte[][] names,
        int index,
        Func<JsonElement, JsonWorkspace, double> step,
        JsonWorkspace workspace,
        ref double sum,
        ref bool hasValue)
    {
        if (index >= names.Length)
        {
            // At the leaf: apply the step function.
            if (current.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement el in current.EnumerateArray())
                {
                    double d = step(el, workspace);
                    if (!double.IsNaN(d))
                    {
                        sum += d;
                        hasValue = true;
                    }
                }
            }
            else
            {
                double d = step(current, workspace);
                if (!double.IsNaN(d))
                {
                    sum += d;
                    hasValue = true;
                }
            }

            return;
        }

        if (current.ValueKind == JsonValueKind.Object)
        {
            if (current.TryGetProperty(names[index], out JsonElement prop))
            {
                SumOverChainCore(prop, names, index + 1, step, workspace, ref sum, ref hasValue);
            }
        }
        else if (current.ValueKind == JsonValueKind.Array)
        {
            // Auto-flatten: iterate array elements and continue chain from same index.
            foreach (JsonElement el in current.EnumerateArray())
            {
                SumOverChainCore(el, names, index, step, workspace, ref sum, ref hasValue);
            }
        }
    }

    /// <summary>
    /// Fused map-over-elements for computed steps that produce <see langword="double"/> results.
    /// Writes doubles directly into the array builder via <c>AddItem(double)</c> (implicit
    /// <see cref="JsonElement.Mutable.Source"/> conversion), avoiding per-element
    /// <c>NumberFromDouble</c> / <c>FixedJsonValueDocument</c> overhead.
    /// </summary>
    /// <param name="data">The input (array or single value).</param>
    /// <param name="step">A function that produces a raw <see langword="double"/> per element.
    /// Returns <see cref="double.NaN"/> for undefined/non-numeric inputs.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>The array of numbers, a single number, or <c>default</c> if undefined.</returns>
    public static JsonElement MapOverElementsDouble(
        in JsonElement data,
        Func<JsonElement, JsonWorkspace, double> step,
        JsonWorkspace workspace)
    {
        if (data.ValueKind == JsonValueKind.Array)
        {
            int count = 0;
            var doc = JsonElement.CreateArrayBuilder(workspace, data.GetArrayLength());
            JsonElement.Mutable root = doc.RootElement;

            foreach (JsonElement item in data.EnumerateArray())
            {
                double result = step(item, workspace);
                if (!double.IsNaN(result))
                {
                    root.AddItem(result);
                    count++;
                }
            }

            return count == 0 ? default : count == 1 ? root[0] : (JsonElement)root;
        }

        if (!data.IsUndefined())
        {
            double result = step(data, workspace);
            return double.IsNaN(result) ? default : JsonataHelpers.NumberFromDouble(result, workspace);
        }

        return default;
    }

    /// <summary>
    /// Fused property-chain navigation + double-space map.
    /// Navigates <paramref name="names"/> into an <see cref="ElementBuffer"/> (ArrayPool-backed),
    /// then maps each element through <paramref name="step"/>, building the final array in one pass.
    /// Eliminates the intermediate <see cref="JsonElement.CreateArrayBuilder"/> that
    /// <see cref="NavigatePropertyChain(in JsonElement, byte[][], JsonWorkspace)"/> would create.
    /// </summary>
    public static JsonElement MapChainDouble(
        in JsonElement data,
        byte[][] names,
        Func<JsonElement, JsonWorkspace, double> step,
        JsonWorkspace workspace)
    {
        var buffer = default(ElementBuffer);
        try
        {
            NavigatePropertyChainInto(data, names, ref buffer);

            if (buffer.Count == 0)
            {
                return default;
            }

            if (buffer.Count == 1)
            {
                double d = step(buffer[0], workspace);
                return double.IsNaN(d) ? default : JsonataHelpers.NumberFromDouble(d, workspace);
            }

            var doc = JsonElement.CreateArrayBuilder(workspace, buffer.Count);
            JsonElement.Mutable root = doc.RootElement;
            int count = 0;

            for (int i = 0; i < buffer.Count; i++)
            {
                double d = step(buffer[i], workspace);
                if (!double.IsNaN(d))
                {
                    root.AddItem(d);
                    count++;
                }
            }

            return count == 0 ? default : count == 1 ? root[0] : (JsonElement)root;
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Fused property-chain navigation + per-element array construction.
    /// Navigates <paramref name="names"/> into an <see cref="ElementBuffer"/>,
    /// creates a single array builder, and calls <paramref name="buildItem"/> for each element.
    /// The callback adds items directly into the array root, enabling
    /// <c>arrRoot.AddItem(ctx, ObjectBuilder.Build, count)</c> to build objects without
    /// intermediate document allocations. This mirrors the RT's
    /// <c>CompileFusedArrayOfObjects</c> pattern.
    /// </summary>
    public static JsonElement FusedChainBuildArray(
        in JsonElement data,
        byte[][] names,
        Action<JsonElement, JsonWorkspace, JsonElement.Mutable> buildItem,
        JsonWorkspace workspace)
    {
        var buffer = default(ElementBuffer);
        try
        {
            NavigatePropertyChainInto(data, names, ref buffer);

            if (buffer.Count == 0)
            {
                var emptyDoc = JsonElement.CreateArrayBuilder(workspace, 0);
                return (JsonElement)emptyDoc.RootElement;
            }

            var doc = JsonElement.CreateArrayBuilder(workspace, buffer.Count * 3);
            JsonElement.Mutable root = doc.RootElement;

            for (int i = 0; i < buffer.Count; i++)
            {
                buildItem(buffer[i], workspace, root);
            }

            return (JsonElement)root;
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Fused property-chain navigation + element map.
    /// Navigates <paramref name="names"/> into an <see cref="ElementBuffer"/>,
    /// then maps each element through <paramref name="transform"/>.
    /// </summary>
    public static JsonElement MapChainElements(
        in JsonElement data,
        byte[][] names,
        Func<JsonElement, JsonWorkspace, JsonElement> transform,
        JsonWorkspace workspace)
    {
        var buffer = default(ElementBuffer);
        try
        {
            NavigatePropertyChainInto(data, names, ref buffer);

            if (buffer.Count == 0)
            {
                return default;
            }

            if (buffer.Count == 1)
            {
                JsonElement single = transform(buffer[0], workspace);
                if (single.ValueKind == JsonValueKind.Undefined)
                {
                    return default;
                }

                var singleDoc = JsonElement.CreateArrayBuilder(workspace, 1);
                singleDoc.RootElement.AddItem(single);
                return (JsonElement)singleDoc.RootElement;
            }

            var doc = JsonElement.CreateArrayBuilder(workspace, buffer.Count);
            JsonElement.Mutable root = doc.RootElement;

            for (int i = 0; i < buffer.Count; i++)
            {
                JsonElement result = transform(buffer[i], workspace);
                if (result.ValueKind != JsonValueKind.Undefined)
                {
                    root.AddItem(result);
                }
            }

            return (JsonElement)root;
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Fused property-chain navigation + element filter.
    /// Navigates <paramref name="names"/> into an <see cref="ElementBuffer"/>,
    /// then filters elements through <paramref name="predicate"/>.
    /// </summary>
    public static JsonElement FilterChainElements(
        in JsonElement data,
        byte[][] names,
        Func<JsonElement, JsonWorkspace, bool> predicate,
        JsonWorkspace workspace)
    {
        var buffer = default(ElementBuffer);
        try
        {
            NavigatePropertyChainInto(data, names, ref buffer);

            if (buffer.Count == 0)
            {
                return default;
            }

            if (buffer.Count == 1)
            {
                return predicate(buffer[0], workspace) ? buffer[0] : default;
            }

            var doc = JsonElement.CreateArrayBuilder(workspace, buffer.Count);
            JsonElement.Mutable root = doc.RootElement;
            int count = 0;

            for (int i = 0; i < buffer.Count; i++)
            {
                if (predicate(buffer[i], workspace))
                {
                    root.AddItem(buffer[i]);
                    count++;
                }
            }

            return count == 0 ? default : count == 1 ? root[0] : (JsonElement)root;
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Fused property-chain navigation + step application (general version).
    /// Same as <see cref="ApplyStepOverElements"/> but avoids the intermediate
    /// <see cref="JsonElement.CreateArrayBuilder"/> for the chain result.
    /// </summary>
    public static JsonElement ApplyChainStep(
        in JsonElement data,
        byte[][] names,
        Func<JsonElement, JsonWorkspace, JsonElement> step,
        JsonWorkspace workspace)
    {
        var buffer = default(ElementBuffer);
        try
        {
            NavigatePropertyChainInto(data, names, ref buffer);

            if (buffer.Count == 0)
            {
                return default;
            }

            if (buffer.Count == 1)
            {
                return step(buffer[0], workspace);
            }

            var doc = JsonElement.CreateArrayBuilder(workspace, buffer.Count);
            JsonElement.Mutable root = doc.RootElement;
            int count = 0;

            for (int i = 0; i < buffer.Count; i++)
            {
                JsonElement result = step(buffer[i], workspace);
                count = AddResultWithFlatten(root, result, count);
            }

            return count == 0 ? default : count == 1 ? root[0] : (JsonElement)root;
        }
        finally
        {
            buffer.Dispose();
        }
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
            throw new JsonataException("T0410", SR.T0410_SecondArgumentOfFunctionJoinMustBeAString, 0);
        }

        if (input.ValueKind == JsonValueKind.Array)
        {
            Utf8ValueStringBuilder sb = new(stackalloc byte[256]);
            try
            {
                bool first = true;
                foreach (JsonElement item in input.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        if (!first && separator.ValueKind == JsonValueKind.String)
                        {
                            using UnescapedUtf8JsonString utf8Sep = separator.GetUtf8String();
                            sb.Append(utf8Sep.Span);
                        }

                        using UnescapedUtf8JsonString utf8Item = item.GetUtf8String();
                        sb.Append(utf8Item.Span);
                        first = false;
                    }
                    else if (!item.IsNullOrUndefined())
                    {
                        throw new JsonataException("T0412", SR.T0412_Argument1OfFunctionJoinMustBeAnArrayOfStrings, 0);
                    }
                }

                return JsonataHelpers.StringFromUnescapedUtf8(sb.AsSpan(), workspace);
            }
            finally
            {
                sb.Dispose();
            }
        }

        if (input.ValueKind == JsonValueKind.String)
        {
            return input;
        }

        throw new JsonataException("T0412", SR.T0412_Argument1OfFunctionJoinMustBeAnArrayOfStrings, 0);
    }

    /// <summary>
    /// Creates a <see cref="JoinBuilder"/> for zero-allocation <c>$join</c> of scalar elements.
    /// </summary>
    /// <param name="initialBuffer">A stack-allocated buffer for the builder (use <c>stackalloc byte[256]</c>).</param>
    /// <param name="separator">The UTF-8 separator bytes (empty for no separator).</param>
    /// <returns>A new <see cref="JoinBuilder"/> that must be disposed after use.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JoinBuilder BeginJoin(Span<byte> initialBuffer, ReadOnlySpan<byte> separator) => new(initialBuffer, separator);

    /// <summary>
    /// A ref struct for building <c>$join</c> results from individual <see cref="JsonElement"/> values
    /// and a UTF-8 separator, without allocating an intermediate array or mutable document.
    /// Follows the same pattern as <see cref="ConcatBuilder"/>.
    /// </summary>
    public ref struct JoinBuilder
    {
        private Utf8ValueStringBuilder _sb;
        private readonly ReadOnlySpan<byte> _separator;
        private bool _first;

        internal JoinBuilder(Span<byte> initialBuffer, ReadOnlySpan<byte> separator)
        {
            _sb = new Utf8ValueStringBuilder(initialBuffer);
            _separator = separator;
            _first = true;
        }

        /// <summary>
        /// Appends a string <see cref="JsonElement"/>. Undefined/null elements are skipped.
        /// Non-string, non-null/undefined elements throw <see cref="JsonataException"/>.
        /// </summary>
        /// <param name="value">The JSON element to append.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendElement(in JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                if (!_first && _separator.Length > 0)
                {
                    _sb.Append(_separator);
                }

                using UnescapedUtf8JsonString utf8 = value.GetUtf8String();
                _sb.Append(utf8.Span);
                _first = false;
            }
            else if (!value.IsNullOrUndefined())
            {
                throw new JsonataException("T0412", SR.T0412_Argument1OfFunctionJoinMustBeAnArrayOfStrings, 0);
            }
        }

        /// <summary>
        /// Completes the join and returns the result as a string <see cref="JsonElement"/>.
        /// </summary>
        /// <param name="workspace">The workspace for the result element.</param>
        /// <returns>A string <see cref="JsonElement"/> containing the joined value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsonElement Complete(JsonWorkspace workspace) =>
            JsonataHelpers.StringFromUnescapedUtf8(_sb.AsSpan(), workspace);

        /// <summary>
        /// Releases resources used by this builder.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => _sb.Dispose();
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
                throw new JsonataException("T0410", SR.T0410_SecondArgumentOfStringMustBeABoolean, 0);
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

        // Strings pass through unchanged
        if (value.ValueKind == JsonValueKind.String)
        {
            return value;
        }

        // Null produces the string "null" (distinct from AppendCoercedValue which appends nothing for concat)
        if (value.ValueKind == JsonValueKind.Null)
        {
            return JsonataHelpers.StringFromUnescapedUtf8("null"u8, workspace);
        }

        // Non-finite numbers cannot be stringified
        if (value.ValueKind == JsonValueKind.Number)
        {
            double d = value.GetDouble();
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                throw new JsonataException("D3001", SR.D3001_AttemptingToInvokeStringFunctionOnInfinityOrNan, 0);
            }

            Utf8ValueStringBuilder sb = new(stackalloc byte[64]);
            try
            {
                AppendFormattedNumber(value, ref sb);
                return JsonataHelpers.StringFromUnescapedUtf8(sb.AsSpan(), workspace);
            }
            finally
            {
                sb.Dispose();
            }
        }

        // For arrays and objects, produce JSON representation
        if (value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
        {
            string json = StringifyElement(value, prettyPrint);
            return JsonataHelpers.StringFromString(json, workspace);
        }

        // true/false
        ReadOnlySpan<byte> literal = value.ValueKind == JsonValueKind.True ? "true"u8 : "false"u8;
        return JsonataHelpers.StringFromUnescapedUtf8(literal, workspace);
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
            throw new JsonataException("T0410", SR.T0410_Argument1OfFunctionLengthMustBeAString, 0);
        }

        using UnescapedUtf8JsonString utf8 = input.GetUtf8String();
        int count = JsonElementHelpers.GetUtf8StringLength(utf8.Span);

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
                    throw new JsonataException("D3030", SR.D3030_UnableToCastValueToANumber, 0);
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
                        throw new JsonataException("D3030", SR.D3030_UnableToCastValueToANumber, 0);
                    }

                    return JsonataHelpers.NumberFromDouble(parsed, workspace);
                }

                throw new JsonataException("D3030", SR.D3030_UnableToCastValueToANumber, 0);
            }

            default:
                throw new JsonataException("T0410", SR.T0410_Argument1OfFunctionNumberIsNotOfTheCorrectType2, 0);
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
                    throw new JsonataException("T0412", SR.T0412_Argument1OfFunctionMaxMustBeAnArrayOfNumbers, 0);
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
            throw new JsonataException("T0412", SR.T0412_Argument1OfFunctionMaxMustBeAnArrayOfNumbers, 0);
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
                    throw new JsonataException("T0412", SR.T0412_Argument1OfFunctionMinMustBeAnArrayOfNumbers, 0);
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
            throw new JsonataException("T0412", SR.T0412_Argument1OfFunctionMinMustBeAnArrayOfNumbers, 0);
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
                    throw new JsonataException("T0412", SR.T0412_Argument1OfFunctionAverageMustBeAnArrayOfNumbers, 0);
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
            throw new JsonataException("T0412", SR.T0412_Argument1OfFunctionAverageMustBeAnArrayOfNumbers, 0);
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
            throw new JsonataException("T0410", SR.T0410_UnableToCastValueToANumber, 0);
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
            throw new JsonataException("T0410", SR.T0410_UnableToCastValueToANumber, 0);
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
            throw new JsonataException("T0410", SR.T0410_UnableToCastValueToANumber, 0);
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
            throw new JsonataException("T0410", SR.T0410_UnableToCastValueToANumber, 0);
        }

        if (num < 0)
        {
            throw new JsonataException("D3060", SR.D3060_TheArgumentOfTheSqrtFunctionMustBeNonNegative, 0);
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
            throw new JsonataException("T0410", SR.T0410_UnableToCastValueToANumber, 0);
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
            throw new JsonataException("D3061", SR.D3061_PowerFunctionResultOutOfRange, 0);
        }

        return JsonataHelpers.NumberFromDouble(result, workspace);
    }

    // ===== Phase 1c: String Transforms =====
    private static readonly Regex WhitespaceCollapseRegex = new(@"\s+", RegexOptions.Compiled);

#if !NET
    [ThreadStatic]
    private static Random? t_random;

    private static Random ThreadLocalRandom => t_random ??= new Random();
#endif

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
            throw new JsonataException("T0410", SR.T0410_StringFunctionArgumentMustBeAString, 0);
        }

        using UnescapedUtf16JsonString utf16 = input.GetUtf16String();
        ReadOnlySpan<char> source = utf16.Span;
        if (source.Length == 0)
        {
            return JsonataHelpers.EmptyString();
        }

        char[]? rentedChars = null;
        Span<char> dest = source.Length <= 128
            ? stackalloc char[128]
            : (rentedChars = ArrayPool<char>.Shared.Rent(source.Length));
        try
        {
#if NET
            int written = source.ToUpperInvariant(dest);
#else
            for (int i = 0; i < source.Length; i++)
            {
                dest[i] = char.ToUpperInvariant(source[i]);
            }

            int written = source.Length;
#endif
            return JsonataHelpers.StringFromChars(dest.Slice(0, written), workspace);
        }
        finally
        {
            if (rentedChars is not null)
            {
                ArrayPool<char>.Shared.Return(rentedChars);
            }
        }
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
            throw new JsonataException("T0410", SR.T0410_StringFunctionArgumentMustBeAString, 0);
        }

        using UnescapedUtf16JsonString utf16 = input.GetUtf16String();
        ReadOnlySpan<char> source = utf16.Span;
        if (source.Length == 0)
        {
            return JsonataHelpers.EmptyString();
        }

        char[]? rentedChars = null;
        Span<char> dest = source.Length <= 128
            ? stackalloc char[128]
            : (rentedChars = ArrayPool<char>.Shared.Rent(source.Length));
        try
        {
#if NET
            int written = source.ToLowerInvariant(dest);
#else
            for (int i = 0; i < source.Length; i++)
            {
                dest[i] = char.ToLowerInvariant(source[i]);
            }

            int written = source.Length;
#endif
            return JsonataHelpers.StringFromChars(dest.Slice(0, written), workspace);
        }
        finally
        {
            if (rentedChars is not null)
            {
                ArrayPool<char>.Shared.Return(rentedChars);
            }
        }
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
            throw new JsonataException("T0410", SR.T0410_StringFunctionArgumentMustBeAString, 0);
        }

        using UnescapedUtf16JsonString utf16 = input.GetUtf16String();
        ReadOnlySpan<char> trimmed = utf16.Span.Trim();
        if (trimmed.Length == 0)
        {
            return JsonataHelpers.EmptyString();
        }

        // Collapse whitespace runs to single spaces without regex or string allocation
        char[]? rentedChars = null;
        Span<char> dest = trimmed.Length <= 128
            ? stackalloc char[128]
            : (rentedChars = ArrayPool<char>.Shared.Rent(trimmed.Length));
        try
        {
            int written = 0;
            bool prevWhitespace = false;
            for (int i = 0; i < trimmed.Length; i++)
            {
                if (char.IsWhiteSpace(trimmed[i]))
                {
                    if (!prevWhitespace)
                    {
                        dest[written++] = ' ';
                        prevWhitespace = true;
                    }
                }
                else
                {
                    dest[written++] = trimmed[i];
                    prevWhitespace = false;
                }
            }

            return JsonataHelpers.StringFromChars(dest.Slice(0, written), workspace);
        }
        finally
        {
            if (rentedChars is not null)
            {
                ArrayPool<char>.Shared.Return(rentedChars);
            }
        }
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
            throw new JsonataException("T0410", SR.T0410_Argument1OfFunctionSubstringIsNotAString, 0);
        }

        if (!FunctionalCompiler.TryCoerceToNumber(startElement, out double startD))
        {
            throw new JsonataException("T0410", SR.T0410_Argument2OfFunctionSubstringIsNotANumber, 0);
        }

        using UnescapedUtf16JsonString utf16 = input.GetUtf16String();
        ReadOnlySpan<char> span = utf16.Span;
        int cpLen = CountCodePoints(span);
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
        int endCharIdx = CodePointToCharIndex(span, start + count);
        ReadOnlySpan<char> result = span.Slice(startCharIdx, endCharIdx - startCharIdx);
        return JsonataHelpers.StringFromChars(result, workspace);
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
            throw new JsonataException("T0410", SR.T0410_Argument1OfStringFunctionIsNotAString, 0);
        }

        if (search.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", SR.T0410_Argument2OfStringFunctionIsNotAString, 0);
        }

        // Use UTF-8 span comparison to avoid string allocations
        using UnescapedUtf8JsonString utf8Str = input.GetUtf8String();
        using UnescapedUtf8JsonString utf8Search = search.GetUtf8String();
        ReadOnlySpan<byte> str = utf8Str.Span;
        ReadOnlySpan<byte> searchSpan = utf8Search.Span;
        int idx = str.IndexOf(searchSpan);
        ReadOnlySpan<byte> result = idx < 0 ? str : str.Slice(0, idx);
        return JsonataHelpers.StringFromUnescapedUtf8(result, workspace);
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
            throw new JsonataException("T0410", SR.T0410_Argument1OfStringFunctionIsNotAString, 0);
        }

        if (search.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", SR.T0410_Argument2OfStringFunctionIsNotAString, 0);
        }

        // Use UTF-8 span comparison to avoid string allocations
        using UnescapedUtf8JsonString utf8Str = input.GetUtf8String();
        using UnescapedUtf8JsonString utf8Search = search.GetUtf8String();
        ReadOnlySpan<byte> str = utf8Str.Span;
        ReadOnlySpan<byte> searchSpan = utf8Search.Span;
        int idx = str.IndexOf(searchSpan);
        ReadOnlySpan<byte> result = idx < 0 ? str : str.Slice(idx + searchSpan.Length);
        return JsonataHelpers.StringFromUnescapedUtf8(result, workspace);
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
            throw new JsonataException("T0410", SR.T0410_Argument1OfFunctionContainsMustBeAString, 0);
        }

        if (search.IsNullOrUndefined() || search.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", SR.T0410_Argument2OfFunctionContainsMustBeAStringOrRegex, 0);
        }

        // Use UTF-8 span comparison to avoid two string allocations
        using UnescapedUtf8JsonString utf8Input = input.GetUtf8String();
        using UnescapedUtf8JsonString utf8Search = search.GetUtf8String();
        return BooleanElement(utf8Input.Span.IndexOf(utf8Search.Span) >= 0);
    }

    /// <summary>
    /// JSONata <c>$split</c> function (string separator only).
    /// </summary>
    public static JsonElement Split(in JsonElement input, in JsonElement separator, in JsonElement limitElement, JsonWorkspace workspace)
    {
        // No undefined/null guard here — runtime $split does not have elem-level undefined check,
        // so undefined/null input falls through to the type check and throws T0410.
        if (input.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", SR.T0410_Argument1OfFunctionSplitIsNotOfTheCorrectType, 0);
        }

        if (separator.IsUndefined() || separator.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", SR.T0410_Argument2OfFunctionSplitIsNotOfTheCorrectType, 0);
        }

        int limit = int.MaxValue;
        if (limitElement.ValueKind != JsonValueKind.Undefined)
        {
            if (limitElement.ValueKind != JsonValueKind.Number)
            {
                throw new JsonataException("T0410", SR.T0410_Argument3OfFunctionSplitIsNotOfTheCorrectType, 0);
            }

            if (FunctionalCompiler.TryCoerceToNumber(limitElement, out double limitD))
            {
                if (limitD < 0)
                {
                    throw new JsonataException("D3020", SR.D3020_ThirdArgumentOfTheSplitFunctionMustEvaluateToAPositiveNumber, 0);
                }

                limit = (int)Math.Floor(limitD);
            }
        }

        using UnescapedUtf8JsonString utf8Str = input.GetUtf8String();
        using UnescapedUtf8JsonString utf8Sep = separator.GetUtf8String();
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
                arrayRoot.AddItem(JsonataHelpers.StringFromUnescapedUtf8(str.Slice(searchStart), workspace));
            }
            else
            {
                int idx = str.Slice(searchStart).IndexOf(sep);
                int partLen = idx >= 0 ? idx : str.Length - searchStart;
                arrayRoot.AddItem(JsonataHelpers.StringFromUnescapedUtf8(str.Slice(searchStart, partLen), workspace));
            }

            return (JsonElement)arrayRoot;
        }
    }

    /// <summary>
    /// JSONata <c>$split</c> function — regex separator form.
    /// Uses a pre-compiled <see cref="Regex"/> passed from the code-generated static field.
    /// On NET, uses <see cref="Regex.EnumerateMatches(ReadOnlySpan{char})"/>
    /// to avoid per-match object allocations.
    /// </summary>
    public static JsonElement SplitRegex(in JsonElement input, Regex regex, in JsonElement limitElement, JsonWorkspace workspace)
    {
        if (input.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", SR.T0410_Argument1OfFunctionSplitIsNotOfTheCorrectType, 0);
        }

        int limit = int.MaxValue;
        if (!limitElement.IsUndefined())
        {
            if (limitElement.ValueKind != JsonValueKind.Number)
            {
                throw new JsonataException("T0410", SR.T0410_Argument3OfFunctionSplitIsNotOfTheCorrectType, 0);
            }

            if (FunctionalCompiler.TryCoerceToNumber(limitElement, out double limitD))
            {
                if (limitD < 0)
                {
                    throw new JsonataException("D3020", SR.D3020_ThirdArgumentOfTheSplitFunctionMustEvaluateToAPositiveNumber, 0);
                }

                limit = (int)Math.Floor(limitD);
            }
        }

        using UnescapedUtf16JsonString utf16Str = input.GetUtf16String();
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

        // Pass 2: emit the split parts
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
        // Fallback: use Regex.Split on a string
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

    /// <summary>
    /// JSONata <c>$pad</c> function — pads a string with cycling pad characters.
    /// </summary>
    public static JsonElement Pad(in JsonElement input, in JsonElement widthElement, in JsonElement padCharElement, JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        if (!FunctionalCompiler.TryCoerceToNumber(widthElement, out double widthNum))
        {
            return default;
        }

        int width = (int)widthNum;

        // Get input as UTF-16 span — use GetUtf16String for strings, CoerceElementToString for other types
        string? coercedStr = null;
        using UnescapedUtf16JsonString utf16Input = input.ValueKind == JsonValueKind.String
            ? input.GetUtf16String()
            : default;
        ReadOnlySpan<char> inputSpan = input.ValueKind == JsonValueKind.String
            ? utf16Input.Span
            : (coercedStr = FunctionalCompiler.CoerceElementToString(input)).AsSpan();

        // Get pad char as UTF-16 span
        string? coercedPad = null;
        using UnescapedUtf16JsonString utf16Pad = padCharElement.ValueKind == JsonValueKind.String
            ? padCharElement.GetUtf16String()
            : default;
        ReadOnlySpan<char> padSpan;
        if (padCharElement.ValueKind == JsonValueKind.Undefined)
        {
            padSpan = " ".AsSpan();
        }
        else if (padCharElement.ValueKind == JsonValueKind.String)
        {
            padSpan = utf16Pad.Span;
            if (padSpan.Length == 0)
            {
                padSpan = " ".AsSpan();
            }
        }
        else
        {
            coercedPad = FunctionalCompiler.CoerceElementToString(padCharElement);
            padSpan = coercedPad.Length > 0 ? coercedPad.AsSpan() : " ".AsSpan();
        }

        int cpLen = CountCodePoints(inputSpan);
        int absWidth = Math.Abs(width);
        int padNeeded = absWidth - cpLen;
        if (padNeeded <= 0)
        {
            return JsonataHelpers.StringFromChars(inputSpan, workspace);
        }

        // Build code-point array for the pad string so cycling works correctly with surrogates.
        // Pad strings are typically 1-2 characters; stackalloc avoids the heap allocation.
        int padCpCount = CountCodePoints(padSpan);
        int[]? rentedCpArray = null;
        Span<int> padCodePoints = padCpCount <= 32
            ? stackalloc int[32]
            : (rentedCpArray = ArrayPool<int>.Shared.Rent(padCpCount));
        int cpIdx = 0;
        for (int ci = 0; ci < padSpan.Length; ci++)
        {
            if (char.IsHighSurrogate(padSpan[ci]) && ci + 1 < padSpan.Length && char.IsLowSurrogate(padSpan[ci + 1]))
            {
                padCodePoints[cpIdx++] = char.ConvertToUtf32(padSpan[ci], padSpan[ci + 1]);
                ci++;
            }
            else
            {
                padCodePoints[cpIdx++] = padSpan[ci];
            }
        }

        // Build result in a rented char buffer: padding can be at most padNeeded * 2 chars (surrogates)
        int maxPadChars = padNeeded * 2;
        int maxResultChars = inputSpan.Length + maxPadChars;
        char[]? rentedChars = null;
        Span<char> dest = maxResultChars <= 128
            ? stackalloc char[128]
            : (rentedChars = ArrayPool<char>.Shared.Rent(maxResultChars));
        try
        {
            int written = 0;
            if (width < 0)
            {
                // Left-pad: padding first
                for (int i = 0; i < padNeeded; i++)
                {
                    written += WriteCodePoint(padCodePoints[i % padCpCount], dest.Slice(written));
                }

                inputSpan.CopyTo(dest.Slice(written));
                written += inputSpan.Length;
            }
            else
            {
                // Right-pad: string first
                inputSpan.CopyTo(dest);
                written = inputSpan.Length;
                for (int i = 0; i < padNeeded; i++)
                {
                    written += WriteCodePoint(padCodePoints[i % padCpCount], dest.Slice(written));
                }
            }

            return JsonataHelpers.StringFromChars(dest.Slice(0, written), workspace);
        }
        finally
        {
            if (rentedChars is not null)
            {
                ArrayPool<char>.Shared.Return(rentedChars);
            }

            if (rentedCpArray is not null)
            {
                ArrayPool<int>.Shared.Return(rentedCpArray);
            }
        }
    }

    private static int WriteCodePoint(int codePoint, Span<char> dest)
    {
        if (codePoint <= 0xFFFF)
        {
            dest[0] = (char)codePoint;
            return 1;
        }

        dest[0] = (char)((codePoint - 0x10000) / 0x400 + 0xD800);
        dest[1] = (char)((codePoint - 0x10000) % 0x400 + 0xDC00);
        return 2;
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
        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            (input, len),
            static (in (JsonElement Input, int Len) ctx, ref JsonElement.ArrayBuilder builder) =>
            {
                for (int i = ctx.Len - 1; i >= 0; i--)
                {
                    builder.AddItem(ctx.Input[i]);
                }
            },
            estimatedMemberCount: len + 2);

        return (JsonElement)doc.RootElement;
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

        int count = input.GetArrayLength();
        if (count <= 1)
        {
            return input;
        }

        var (parentDoc, _) = JsonElementHelpers.GetParentDocumentAndIndex(input);

        Span<int> buckets = count <= UniqueItemsHashSet.StackAllocBucketSize
            ? stackalloc int[UniqueItemsHashSet.StackAllocBucketSize]
            : default;
        int entryBytes = count * 12;
        Span<byte> entries = entryBytes <= UniqueItemsHashSet.StackAllocEntrySize
            ? stackalloc byte[UniqueItemsHashSet.StackAllocEntrySize]
            : default;

        using var hashSet = new UniqueItemsHashSet(parentDoc, count, buckets, entries);

        JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, count);
        JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
        foreach (var item in input.EnumerateArray())
        {
            var (_, idx) = JsonElementHelpers.GetParentDocumentAndIndex(item);
            if (hashSet.AddItemIfNotExists(idx))
            {
                arrayRoot.AddItem(item);
            }
        }

        return (JsonElement)arrayRoot;
    }

    /// <summary>
    /// Fused chain navigation + distinct: navigates a property chain into a lightweight
    /// <see cref="ElementBuffer"/> (no intermediate builder), then deduplicates directly.
    /// </summary>
    /// <param name="data">The input data element.</param>
    /// <param name="names">UTF-8 encoded property name chain.</param>
    /// <param name="workspace">The workspace for pooled memory.</param>
    /// <returns>The deduplicated result.</returns>
    public static JsonElement ChainDistinct(in JsonElement data, byte[][] names, JsonWorkspace workspace)
    {
        var buffer = default(ElementBuffer);
        try
        {
            NavigatePropertyChainInto(data, names, ref buffer);
            return DistinctFromBuffer(ref buffer, workspace);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Fused chain navigation + distinct with a start index for partially-resolved chains.
    /// </summary>
    /// <param name="data">The element at the start position (already resolved up to <paramref name="startIndex"/>).</param>
    /// <param name="names">The full UTF-8 encoded property names array.</param>
    /// <param name="startIndex">The index into <paramref name="names"/> to start navigating from.</param>
    /// <param name="workspace">The workspace for pooled memory.</param>
    /// <returns>The deduplicated result.</returns>
    public static JsonElement ChainDistinct(in JsonElement data, byte[][] names, int startIndex, JsonWorkspace workspace)
    {
        var buffer = default(ElementBuffer);
        try
        {
            NavigatePropertyChainInto(data, names, startIndex, ref buffer);
            return DistinctFromBuffer(ref buffer, workspace);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Fused chain navigation + keep-singleton-array (<c>[]</c> syntax).
    /// Navigates the property chain into an <see cref="ElementBuffer"/> (no intermediate builder),
    /// then builds the result array directly from the buffer.
    /// </summary>
    /// <param name="data">The root element to navigate from.</param>
    /// <param name="names">The UTF-8 encoded property names for the chain.</param>
    /// <param name="workspace">The workspace for pooled memory.</param>
    /// <returns>The flattened array result.</returns>
    public static JsonElement ChainKeepSingletonArray(in JsonElement data, byte[][] names, JsonWorkspace workspace)
    {
        var buffer = default(ElementBuffer);
        try
        {
            NavigatePropertyChainInto(data, names, ref buffer);
            return ArrayFromBuffer(ref buffer, workspace);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Fused chain navigation + keep-singleton-array with a start index for partially-resolved chains.
    /// </summary>
    /// <param name="data">The element at the start position (already resolved up to <paramref name="startIndex"/>).</param>
    /// <param name="names">The full UTF-8 encoded property names array.</param>
    /// <param name="startIndex">The index into <paramref name="names"/> to start navigating from.</param>
    /// <param name="workspace">The workspace for pooled memory.</param>
    /// <returns>The flattened array result.</returns>
    public static JsonElement ChainKeepSingletonArray(in JsonElement data, byte[][] names, int startIndex, JsonWorkspace workspace)
    {
        var buffer = default(ElementBuffer);
        try
        {
            NavigatePropertyChainInto(data, names, startIndex, ref buffer);
            return ArrayFromBuffer(ref buffer, workspace);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Fused chain navigation + merge. Navigates the property chain into an
    /// <see cref="ElementBuffer"/> (no intermediate builder), then merges object
    /// properties from the buffer elements into a single result.
    /// </summary>
    /// <param name="data">The root element to navigate from.</param>
    /// <param name="names">The UTF-8 encoded property names for the chain.</param>
    /// <param name="workspace">The workspace for pooled memory.</param>
    /// <returns>The merged object result.</returns>
    public static JsonElement ChainMerge(in JsonElement data, byte[][] names, JsonWorkspace workspace)
    {
        var buffer = default(ElementBuffer);
        try
        {
            NavigatePropertyChainInto(data, names, ref buffer);
            return MergeFromBuffer(ref buffer, workspace);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Fused chain navigation + merge with a start index for partially-resolved chains.
    /// </summary>
    /// <param name="data">The element at the start position (already resolved up to <paramref name="startIndex"/>).</param>
    /// <param name="names">The full UTF-8 encoded property names array.</param>
    /// <param name="startIndex">The index into <paramref name="names"/> to start navigating from.</param>
    /// <param name="workspace">The workspace for pooled memory.</param>
    /// <returns>The merged object result.</returns>
    public static JsonElement ChainMerge(in JsonElement data, byte[][] names, int startIndex, JsonWorkspace workspace)
    {
        var buffer = default(ElementBuffer);
        try
        {
            NavigatePropertyChainInto(data, names, startIndex, ref buffer);
            return MergeFromBuffer(ref buffer, workspace);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Builds an array from an <see cref="ElementBuffer"/>. If the buffer is empty,
    /// returns undefined. If it has exactly one element, returns that element wrapped
    /// in an array. Otherwise, builds a full array document.
    /// </summary>
    private static JsonElement ArrayFromBuffer(ref ElementBuffer buffer, JsonWorkspace workspace)
    {
        if (buffer.Count == 0)
        {
            return default;
        }

        if (buffer.Count == 1)
        {
            // Single element — wrap in a 1-element array
            var singleDoc = JsonElement.CreateArrayBuilder(workspace, 1);
            singleDoc.RootElement.AddItem(buffer[0]);
            return (JsonElement)singleDoc.RootElement;
        }

        JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, buffer.Count);
        JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
        for (int i = 0; i < buffer.Count; i++)
        {
            arrayRoot.AddItem(buffer[i]);
        }

        return (JsonElement)arrayRoot;
    }

    /// <summary>
    /// Merges object properties from an <see cref="ElementBuffer"/> into a single object
    /// using the CVB pattern. Last-wins for duplicate property names.
    /// </summary>
    private static JsonElement MergeFromBuffer(ref ElementBuffer buffer, JsonWorkspace workspace)
    {
        if (buffer.Count == 0)
        {
            return default;
        }

        if (buffer.Count == 1)
        {
            return buffer[0].ValueKind == JsonValueKind.Object ? buffer[0] : default;
        }

        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            buffer,
            static (in ElementBuffer ctx, ref JsonElement.ObjectBuilder builder) =>
            {
                for (int i = 0; i < ctx.Count; i++)
                {
                    if (ctx[i].ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in ctx[i].EnumerateObject())
                        {
                            using UnescapedUtf8JsonString nameUtf8 = prop.Utf8NameSpan;
                            builder.AddProperty(nameUtf8.Span, prop.Value, escapeName: false, nameRequiresUnescaping: false);
                        }
                    }
                }
            },
            estimatedMemberCount: 16);

        return (JsonElement)doc.RootElement;
    }

    /// <summary>
    /// Deduplicates elements from an <see cref="ElementBuffer"/> using
    /// <see cref="UniqueItemsHashSet"/> and builds the result array in one pass.
    /// </summary>
    private static JsonElement DistinctFromBuffer(ref ElementBuffer buffer, JsonWorkspace workspace)
    {
        if (buffer.Count == 0)
        {
            return default;
        }

        if (buffer.Count == 1)
        {
            return buffer[0];
        }

        var (parentDoc, _) = JsonElementHelpers.GetParentDocumentAndIndex(buffer[0]);

        Span<int> buckets = buffer.Count <= UniqueItemsHashSet.StackAllocBucketSize
            ? stackalloc int[UniqueItemsHashSet.StackAllocBucketSize]
            : default;
        int entryBytes = buffer.Count * 12;
        Span<byte> entries = entryBytes <= UniqueItemsHashSet.StackAllocEntrySize
            ? stackalloc byte[UniqueItemsHashSet.StackAllocEntrySize]
            : default;

        using var hashSet = new UniqueItemsHashSet(parentDoc, buffer.Count, buckets, entries);

        JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, buffer.Count);
        JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
        for (int i = 0; i < buffer.Count; i++)
        {
            var (_, idx) = JsonElementHelpers.GetParentDocumentAndIndex(buffer[i]);
            if (hashSet.AddItemIfNotExists(idx))
            {
                arrayRoot.AddItem(buffer[i]);
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

        if (input.ValueKind == JsonValueKind.Object)
        {
            // Fast path for single object: use CVB array builder with UTF-8 property names
            int propCount = 0;
            foreach (var p in input.EnumerateObject())
            {
                propCount++;
            }

            if (propCount == 0)
            {
                return default;
            }

            JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
                workspace,
                input,
                static (in JsonElement ctx, ref JsonElement.ArrayBuilder builder) =>
                {
                    foreach (var prop in ctx.EnumerateObject())
                    {
                        using UnescapedUtf8JsonString nameUtf8 = prop.Utf8NameSpan;
                        builder.AddItem(nameUtf8.Span);
                    }
                },
                estimatedMemberCount: propCount + 2);

            if (propCount == 1)
            {
                return doc.RootElement[0];
            }

            return (JsonElement)doc.RootElement;
        }

        if (input.ValueKind == JsonValueKind.Array)
        {
            // Array of objects — use Utf8KeyHashSet for allocation-free dedup
            int arrayLen = input.GetArrayLength();
            Span<int> keyBuckets = arrayLen <= Utf8KeyHashSet.StackAllocBucketSize
                ? stackalloc int[Utf8KeyHashSet.StackAllocBucketSize]
                : default;
            Span<byte> keyEntries = arrayLen * 20 <= Utf8KeyHashSet.StackAllocEntrySize
                ? stackalloc byte[Utf8KeyHashSet.StackAllocEntrySize]
                : default;
            Span<byte> keyBuffer = stackalloc byte[Utf8KeyHashSet.StackAllocKeyBufferSize];

            var keySet = new Utf8KeyHashSet(arrayLen, keyBuckets, keyEntries, keyBuffer);
            try
            {
                JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, arrayLen);
                JsonElement.Mutable arrayRoot = arrayDoc.RootElement;

                CollectUniqueKeys(input, ref keySet, ref arrayRoot);

                if (keySet.Count == 0)
                {
                    return default;
                }

                if (keySet.Count == 1)
                {
                    return arrayRoot[0];
                }

                return (JsonElement)arrayRoot;
            }
            finally
            {
                keySet.Dispose();
            }
        }

        return default;
    }

    private static void CollectUniqueKeys(JsonElement el, ref Utf8KeyHashSet keySet, ref JsonElement.Mutable arrayRoot)
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
                CollectUniqueKeys(item, ref keySet, ref arrayRoot);
            }
        }
    }

    /// <summary>
    /// JSONata <c>$values</c> function — returns values from an object.
    /// Uses <see cref="ElementBuffer"/> to avoid
    /// document creation when the object has a single property.
    /// </summary>
    public static JsonElement Values(in JsonElement input, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined() || input.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        var buffer = default(ElementBuffer);
        try
        {
            foreach (var prop in input.EnumerateObject())
            {
                buffer.Add(prop.Value);
            }

            return buffer.ToArrayResult(workspace);
        }
        finally
        {
            buffer.Dispose();
        }
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

        using UnescapedUtf8JsonString utf8Key = keyElement.GetUtf8String();
        var results = new List<JsonElement>();
        LookupCollect(input, utf8Key.Span, results);

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

    private static void LookupCollect(JsonElement el, ReadOnlySpan<byte> utf8Key, List<JsonElement> results)
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                LookupCollect(item, utf8Key, results);
            }
        }
        else if (el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty(utf8Key, out var value))
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

        if (input.ValueKind != JsonValueKind.Array)
        {
            return default;
        }

        // Use CVB ObjectBuilder — AddProperty is forward-only append (no O(n) scan).
        // Last-wins semantics are handled by the fact that the document builder
        // keeps the last value for duplicate property names when serialised.
        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            input,
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

        return (JsonElement)doc.RootElement;
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
            JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, input.GetArrayLength() * 3);
            JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
            foreach (var item in input.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in item.EnumerateObject())
                    {
                        arrayRoot.AddItem(prop, SpreadPropertyCallback, 1);
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
            arrayRoot.AddItem(prop, SpreadPropertyCallback, 1);
        }

        return (JsonElement)arrayRoot;
    }

    private static void SpreadPropertyCallback(in JsonProperty<JsonElement> ctx, ref JsonElement.ObjectBuilder builder)
    {
        using UnescapedUtf8JsonString nameUtf8 = ctx.Utf8NameSpan;
        builder.AddProperty(nameUtf8.Span, ctx.Value, escapeName: false, nameRequiresUnescaping: false);
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
            throw new JsonataException("D3139", SR.D3139_SingleFunctionExpectedOneResultGotZero, 0);
        }

        throw new JsonataException("D3138", SR.Format(SR.D3138_SingleFunctionExpectedOneResult, count), 0);
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
                        throw new JsonataException("D3138", SR.Format(SR.D3138_SingleFunctionExpectedOneResult, matchCount), 0);
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
            throw new JsonataException("D3139", SR.D3139_SingleFunctionExpectedOneResultGotZero, 0);
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
                        throw new JsonataException("D3138", SR.Format(SR.D3138_SingleFunctionExpectedOneResult, matchCount), 0);
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
            throw new JsonataException("D3139", SR.D3139_SingleFunctionExpectedOneResultGotZero, 0);
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

        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            input,
            static (in JsonElement ctx, ref JsonElement.ArrayBuilder builder) =>
            {
                FlattenIntoBuilder(ctx, ref builder);
            },
            estimatedMemberCount: 16);

        return (JsonElement)doc.RootElement;
    }

    private static void FlattenIntoBuilder(JsonElement element, ref JsonElement.ArrayBuilder builder)
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

        // Use ElementBuffer (ArrayPool-backed) instead of heap-allocating new JsonElement[].
        var buf = default(ElementBuffer);
        try
        {
            foreach (var item in input.EnumerateArray())
            {
                buf.Add(item);
            }

            buf.GetContents(out var elements, out var count);

            // Fisher-Yates shuffle directly on the backing array
            for (int i = count - 1; i > 0; i--)
            {
#if NET
                int j = Random.Shared.Next(i + 1);
#else
                int j = ThreadLocalRandom.Next(i + 1);
#endif
                (elements![i], elements[j]) = (elements[j], elements[i]);
            }

            JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, count);
            JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
            for (int k = 0; k < count; k++)
            {
                arrayRoot.AddItem(elements![k]);
            }

            return (JsonElement)arrayRoot;
        }
        finally
        {
            buf.Dispose();
        }
    }

    /// <summary>
    /// Shuffles elements from a pre-populated <see cref="ElementBuffer"/> (e.g., from a
    /// chain navigation) and builds a single result array document. Used by the buffer-fused
    /// CG path to avoid the intermediate chain builder.
    /// </summary>
    public static JsonElement ShuffleFromBuffer(ref ElementBuffer buf, JsonWorkspace workspace)
    {
        buf.GetContents(out var elements, out var count);

        if (count == 0)
        {
            return default;
        }

        // Fisher-Yates shuffle directly on the pooled backing array
        for (int i = count - 1; i > 0; i--)
        {
#if NET
            int j = Random.Shared.Next(i + 1);
#else
            int j = ThreadLocalRandom.Next(i + 1);
#endif
            (elements![i], elements[j]) = (elements[j], elements[i]);
        }

        JsonDocumentBuilder<JsonElement.Mutable> arrayDoc = JsonElement.CreateArrayBuilder(workspace, count);
        JsonElement.Mutable arrayRoot = arrayDoc.RootElement;
        for (int k = 0; k < count; k++)
        {
            arrayRoot.AddItem(elements![k]);
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
                throw new JsonataException("T0410", SR.T0410_ErrorExpectsAStringArgument, 0);
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
            throw new JsonataException("T0410", SR.T0410_Argument1OfFunctionAssertMustBeABoolean, 0);
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
            throw new JsonataException("D3140", SR.D3140_MalformedUrlEncodeUrlComponent, 0);
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
            throw new JsonataException("D3140", SR.Format(SR.D3140_MalformedUrlPassedToDecodeUrlComponent, str), 0);
        }

        try
        {
            return JsonataHelpers.StringFromString(Uri.UnescapeDataString(str), workspace);
        }
        catch (Exception ex) when (ex is UriFormatException or ArgumentException or FormatException)
        {
            throw new JsonataException("D3140", SR.Format(SR.D3140_MalformedUrlPassedToDecodeUrlComponent, str), 0);
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
            throw new JsonataException("D3140", SR.D3140_MalformedUrlEncodeUrl, 0);
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
            throw new JsonataException("D3140", SR.Format(SR.D3140_MalformedUrlPassedToDecodeUrl, str), 0);
        }

        try
        {
            return JsonataHelpers.StringFromString(Uri.UnescapeDataString(str), workspace);
        }
        catch (Exception ex) when (ex is UriFormatException or ArgumentException or FormatException)
        {
            throw new JsonataException("D3140", SR.Format(SR.D3140_MalformedUrlPassedToDecodeUrl, str), 0);
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
            throw new JsonataException("T0410", SR.T0410_Argument1OfFunctionReplaceIsNotOfTheCorrectType, 0);
        }

        if (pattern.IsUndefined() || pattern.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", SR.T0410_Argument2OfFunctionReplaceIsNotOfTheCorrectType, 0);
        }

        if (replacement.IsUndefined() || replacement.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", SR.T0410_Argument3OfFunctionReplaceIsNotOfTheCorrectType, 0);
        }

        using UnescapedUtf8JsonString utf8Str = input.GetUtf8String();
        using UnescapedUtf8JsonString utf8Pat = pattern.GetUtf8String();
        using UnescapedUtf8JsonString utf8Rep = replacement.GetUtf8String();

        ReadOnlySpan<byte> str = utf8Str.Span;
        ReadOnlySpan<byte> pat = utf8Pat.Span;
        ReadOnlySpan<byte> rep = utf8Rep.Span;

        if (pat.Length == 0)
        {
            throw new JsonataException("D3010", SR.D3010_TheSecondArgumentOfTheReplaceFunctionCannotBeAnEmptyString, 0);
        }

        int limit = int.MaxValue;
        if (!limitElement.IsUndefined())
        {
            if (limitElement.ValueKind == JsonValueKind.Null)
            {
                throw new JsonataException("T0410", SR.T0410_Argument4OfFunctionReplaceIsNotOfTheCorrectType, 0);
            }

            if (limitElement.ValueKind != JsonValueKind.Number)
            {
                throw new JsonataException("T0410", SR.T0410_Argument4OfFunctionReplaceIsNotOfTheCorrectType, 0);
            }

            double n = limitElement.GetDouble();
            if (n < 0)
            {
                throw new JsonataException("D3011", SR.D3011_TheFourthArgumentOfTheReplaceFunctionMustBeAPositiveNumber, 0);
            }

            limit = (int)n;
        }

        Utf8ValueStringBuilder sb = new(stackalloc byte[256]);
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

    /// <summary>
    /// JSONata <c>$replace</c> function — regex form.
    /// Uses <see cref="ValueStringBuilder"/> and <see cref="UnescapedUtf16JsonString"/>
    /// to avoid intermediate string allocations. On NET, uses
    /// <see cref="Regex.EnumerateMatches(ReadOnlySpan{char})"/> when the replacement
    /// contains no backreferences.
    /// </summary>
    /// <param name="input">The input string element.</param>
    /// <param name="regex">The pre-compiled <see cref="Regex"/> pattern.</param>
    /// <param name="replacement">The replacement string element.</param>
    /// <param name="limitElement">Optional limit element (number or undefined).</param>
    /// <param name="workspace">The workspace for building the result.</param>
    /// <returns>The replaced string, or <c>default</c> if input is undefined.</returns>
    public static JsonElement ReplaceRegex(
        in JsonElement input,
        Regex regex,
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
            throw new JsonataException("T0410", SR.T0410_Argument1OfFunctionReplaceIsNotOfTheCorrectType, 0);
        }

        if (replacement.IsUndefined() || replacement.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", SR.T0410_Argument3OfFunctionReplaceIsNotOfTheCorrectType, 0);
        }

        int limit = int.MaxValue;
        if (!limitElement.IsUndefined())
        {
            if (limitElement.ValueKind == JsonValueKind.Null || limitElement.ValueKind != JsonValueKind.Number)
            {
                throw new JsonataException("T0410", SR.T0410_Argument4OfFunctionReplaceIsNotOfTheCorrectType, 0);
            }

            double n = limitElement.GetDouble();
            if (n < 0)
            {
                throw new JsonataException("D3011", SR.D3011_TheFourthArgumentOfTheReplaceFunctionMustBeAPositiveNumber, 0);
            }

            limit = (int)n;
        }

        if (limit <= 0)
        {
            return input;
        }

        using var utf16Str = input.GetUtf16String();
        ReadOnlySpan<char> strChars = utf16Str.Span;

        using var utf16Rep = replacement.GetUtf16String();
        ReadOnlySpan<char> repChars = utf16Rep.Span;

        ValueStringBuilder sb = new(stackalloc char[JsonConstants.StackallocCharThreshold]);
        try
        {
#if NET
            bool hasBackrefs = repChars.Contains('$');

            if (!hasBackrefs)
            {
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
                string str = input.GetString()!;
                int count = 0;
                int searchStart = 0;
                while (count < limit && searchStart <= str.Length)
                {
                    System.Text.RegularExpressions.Match m = regex.Match(str, searchStart);
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
            string str = input.GetString()!;
            int count = 0;
            int searchStart = 0;
            while (count < limit && searchStart <= str.Length)
            {
                System.Text.RegularExpressions.Match m = regex.Match(str, searchStart);
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
    /// directly into a <see cref="ValueStringBuilder"/>.
    /// </summary>
    private static void ApplyJsonataBackreferencesSpan(
        ReadOnlySpan<char> replacement, System.Text.RegularExpressions.Match match, ref ValueStringBuilder sb)
    {
        int numGroups = match.Groups.Count - 1;
        int segStart = 0;

        for (int i = 0; i < replacement.Length; i++)
        {
            if (replacement[i] != '$')
            {
                continue;
            }

            if (i > segStart)
            {
                sb.Append(replacement.Slice(segStart, i - segStart));
            }

            if (i + 1 >= replacement.Length)
            {
                sb.Append('$');
                segStart = i + 1;
                continue;
            }

            char next = replacement[i + 1];

            if (next == '$')
            {
                sb.Append('$');
                i++;
                segStart = i + 1;
            }
            else if (next >= '0' && next <= '9')
            {
                int digitStart = i + 1;
                int digitEnd = digitStart;
                while (digitEnd < replacement.Length && replacement[digitEnd] >= '0' && replacement[digitEnd] <= '9')
                {
                    digitEnd++;
                }

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

                        if (digitEnd > digitStart + consumed)
                        {
                            sb.Append(replacement.Slice(digitStart + consumed, digitEnd - digitStart - consumed));
                        }

                        found = true;
                        break;
                    }

                    consumed--;
                }

                if (!found && digitEnd > digitStart + 1)
                {
                    sb.Append(replacement.Slice(digitStart + 1, digitEnd - digitStart - 1));
                }

                i = digitEnd - 1;
                segStart = digitEnd;
            }
            else
            {
                sb.Append('$');
                sb.Append(next);
                i++;
                segStart = i + 1;
            }
        }

        if (segStart < replacement.Length)
        {
            sb.Append(replacement.Slice(segStart));
        }
    }

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
    /// JSONata <c>$match</c> function — matches a string against a compiled <see cref="Regex"/>
    /// and returns an array of match objects <c>{"match","index","groups"}</c>.
    /// Uses <see cref="Regex.EnumerateMatches(ReadOnlySpan{char})"/> for patterns without
    /// capture groups (zero <see cref="Match"/> allocations), and CVB for building results.
    /// </summary>
    /// <param name="input">The input string element.</param>
    /// <param name="regex">The pre-compiled <see cref="Regex"/> pattern.</param>
    /// <param name="limitElement">Optional limit element (number or undefined).</param>
    /// <param name="workspace">The workspace for building the result.</param>
    /// <returns>A match object, array of match objects, or <c>default</c> if no matches.</returns>
    public static JsonElement Match(
        in JsonElement input,
        Regex regex,
        in JsonElement limitElement,
        JsonWorkspace workspace)
    {
        if (input.IsUndefined())
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.String)
        {
            throw new JsonataException("T0410", SR.T0410_Argument1OfFunctionMatchIsNotOfTheCorrectType, 0);
        }

        int limit = int.MaxValue;
        if (limitElement.ValueKind != JsonValueKind.Undefined)
        {
            if (limitElement.ValueKind != JsonValueKind.Number)
            {
                throw new JsonataException("T0410", SR.T0410_Argument3OfFunctionMatchIsNotOfTheCorrectType, 0);
            }

            if (FunctionalCompiler.TryCoerceToNumber(limitElement, out double limitD))
            {
                limit = (int)limitD;
            }
        }

#if NET
        bool hasGroups = regex.GetGroupNumbers().Length > 1;

        if (!hasGroups)
        {
            using UnescapedUtf16JsonString utf16Str = input.GetUtf16String();
            ReadOnlyMemory<char> charMemory = utf16Str.Memory;

            // Fused single-document: outer array with nested object builders per match
            var doc = JsonElement.CreateBuilder(
                workspace,
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
            string regexStr = input.GetString()!;
            ReadOnlyMemory<char> charMemory = regexStr.AsMemory();

            // Fused single-document: outer array with nested object+groups builders per match
            var doc = JsonElement.CreateBuilder(
                workspace,
                (charMemory, regex, regexStr, limit),
                static (in (ReadOnlyMemory<char> Chars, Regex Regex, string RegexStr, int Limit) ctx, ref JsonElement.ArrayBuilder ab) =>
                {
                    int count = 0;
                    System.Text.RegularExpressions.Match m = ctx.Regex.Match(ctx.RegexStr);
                    while (m.Success && count < ctx.Limit)
                    {
                        ab.AddItem(
                            (ctx.Chars, m),
                            static (in (ReadOnlyMemory<char> C, System.Text.RegularExpressions.Match M) mctx, ref JsonElement.ObjectBuilder ob) =>
                            {
                                ob.AddProperty("match"u8, mctx.C.Span.Slice(mctx.M.Index, mctx.M.Length));
                                ob.AddProperty("index"u8, mctx.M.Index);
                                ob.AddProperty("groups"u8, (mctx.C, mctx.M),
                                    static (in (ReadOnlyMemory<char> C, System.Text.RegularExpressions.Match M) gctx, ref JsonElement.ArrayBuilder gab) =>
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
        string? regexStr = input.GetString();
        if (regexStr is null)
        {
            return default;
        }

        ReadOnlyMemory<char> charMemory = regexStr.AsMemory();
        MatchCollection matches = regex.Matches(regexStr);

        // Fused single-document: outer array with nested object+groups builders per match
        var doc = JsonElement.CreateBuilder(
            workspace,
            (charMemory, matches, limit),
            static (in (ReadOnlyMemory<char> Chars, MatchCollection Matches, int Limit) ctx, ref JsonElement.ArrayBuilder ab) =>
            {
                int count = 0;
                foreach (System.Text.RegularExpressions.Match m in ctx.Matches)
                {
                    if (count >= ctx.Limit)
                    {
                        break;
                    }

                    ab.AddItem(
                        (ctx.Chars, m),
                        static (in (ReadOnlyMemory<char> C, System.Text.RegularExpressions.Match M) mctx, ref JsonElement.ObjectBuilder ob) =>
                        {
                            ob.AddProperty("match"u8, mctx.C.Span.Slice(mctx.M.Index, mctx.M.Length));
                            ob.AddProperty("index"u8, mctx.M.Index);
                            ob.AddProperty("groups"u8, (mctx.C, mctx.M),
                                static (in (ReadOnlyMemory<char> C, System.Text.RegularExpressions.Match M) gctx, ref JsonElement.ArrayBuilder gab) =>
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
    }

    /// <summary>
    /// Unwraps a match result array: 0 elements → undefined, 1 element → the single
    /// match object, N elements → the array itself.
    /// </summary>
    private static JsonElement UnwrapMatchArray(JsonElement array)
    {
        int count = array.GetArrayLength();
        if (count == 0)
        {
            return default;
        }

        if (count == 1)
        {
            return array[0];
        }

        return array;
    }

    /// <summary>
    /// JSONata <c>$zip</c> function — transposes a single array.
    /// Each element becomes a single-element inner array.
    /// </summary>
    /// <param name="arg0">The input array element.</param>
    /// <param name="workspace">The workspace for building the result.</param>
    /// <returns>An array of single-element arrays, or <c>default</c> if no valid arrays.</returns>
    public static JsonElement Zip(JsonElement arg0, JsonWorkspace workspace)
    {
        ResolveZipArg(arg0, out bool isArr0, out int len0);

        if (len0 == 0)
        {
            var emptyDoc = JsonElement.CreateArrayBuilder(workspace, 0);
            return (JsonElement)emptyDoc.RootElement;
        }

        var doc = JsonElement.CreateBuilder(
            workspace,
            (arg0, len0, isArr0),
            static (in (JsonElement A0, int Len, bool IsArr0) ctx, ref JsonElement.ArrayBuilder outer) =>
            {
                for (int i = 0; i < ctx.Len; i++)
                {
                    outer.AddItem(
                        (ctx.A0, i, ctx.IsArr0),
                        static (in (JsonElement A0, int I, bool IsArr0) ictx, ref JsonElement.ArrayBuilder inner) =>
                        {
                            inner.AddItem(ictx.IsArr0 ? ictx.A0[ictx.I] : ictx.A0);
                        });
                }
            },
            estimatedMemberCount: (len0 * 2) + 2);

        return (JsonElement)doc.RootElement;
    }

    /// <summary>
    /// JSONata <c>$zip</c> function — transposes two arrays.
    /// Specialized 2-argument overload that avoids intermediate array allocation.
    /// </summary>
    /// <param name="arg0">The first input array element.</param>
    /// <param name="arg1">The second input array element.</param>
    /// <param name="workspace">The workspace for building the result.</param>
    /// <returns>An array of arrays, or <c>default</c> if no valid arrays.</returns>
    public static JsonElement Zip(JsonElement arg0, JsonElement arg1, JsonWorkspace workspace)
    {
        ResolveZipArg(arg0, out bool isArr0, out int len0);
        ResolveZipArg(arg1, out bool isArr1, out int len1);

        int minLen = Math.Min(len0, len1);

        if (minLen == 0)
        {
            var emptyDoc = JsonElement.CreateArrayBuilder(workspace, 0);
            return (JsonElement)emptyDoc.RootElement;
        }

        var doc = JsonElement.CreateBuilder(
            workspace,
            (arg0, arg1, minLen, isArr0, isArr1),
            static (in (JsonElement A0, JsonElement A1, int MinLen, bool IsArr0, bool IsArr1) ctx, ref JsonElement.ArrayBuilder outer) =>
            {
                for (int i = 0; i < ctx.MinLen; i++)
                {
                    outer.AddItem(
                        (ctx.A0, ctx.A1, i, ctx.IsArr0, ctx.IsArr1),
                        static (in (JsonElement A0, JsonElement A1, int I, bool IsArr0, bool IsArr1) ictx, ref JsonElement.ArrayBuilder inner) =>
                        {
                            inner.AddItem(ictx.IsArr0 ? ictx.A0[ictx.I] : ictx.A0);
                            inner.AddItem(ictx.IsArr1 ? ictx.A1[ictx.I] : ictx.A1);
                        });
                }
            },
            estimatedMemberCount: (minLen * 3) + 2);

        return (JsonElement)doc.RootElement;
    }

    /// <summary>
    /// JSONata <c>$zip</c> function — transposes three arrays.
    /// Specialized 3-argument overload that avoids intermediate array allocation.
    /// </summary>
    /// <param name="arg0">The first input array element.</param>
    /// <param name="arg1">The second input array element.</param>
    /// <param name="arg2">The third input array element.</param>
    /// <param name="workspace">The workspace for building the result.</param>
    /// <returns>An array of arrays, or <c>default</c> if no valid arrays.</returns>
    public static JsonElement Zip(JsonElement arg0, JsonElement arg1, JsonElement arg2, JsonWorkspace workspace)
    {
        ResolveZipArg(arg0, out bool isArr0, out int len0);
        ResolveZipArg(arg1, out bool isArr1, out int len1);
        ResolveZipArg(arg2, out bool isArr2, out int len2);

        int minLen = Math.Min(len0, Math.Min(len1, len2));

        if (minLen == 0)
        {
            var emptyDoc = JsonElement.CreateArrayBuilder(workspace, 0);
            return (JsonElement)emptyDoc.RootElement;
        }

        var doc = JsonElement.CreateBuilder(
            workspace,
            (arg0, arg1, arg2, minLen, isArr0, isArr1, isArr2),
            static (in (JsonElement A0, JsonElement A1, JsonElement A2, int MinLen, bool IsArr0, bool IsArr1, bool IsArr2) ctx, ref JsonElement.ArrayBuilder outer) =>
            {
                for (int i = 0; i < ctx.MinLen; i++)
                {
                    outer.AddItem(
                        (ctx.A0, ctx.A1, ctx.A2, i, ctx.IsArr0, ctx.IsArr1, ctx.IsArr2),
                        static (in (JsonElement A0, JsonElement A1, JsonElement A2, int I, bool IsArr0, bool IsArr1, bool IsArr2) ictx, ref JsonElement.ArrayBuilder inner) =>
                        {
                            inner.AddItem(ictx.IsArr0 ? ictx.A0[ictx.I] : ictx.A0);
                            inner.AddItem(ictx.IsArr1 ? ictx.A1[ictx.I] : ictx.A1);
                            inner.AddItem(ictx.IsArr2 ? ictx.A2[ictx.I] : ictx.A2);
                        });
                }
            },
            estimatedMemberCount: (minLen * 4) + 2);

        return (JsonElement)doc.RootElement;
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

        int minLen = int.MaxValue;
        for (int a = 0; a < args.Length; a++)
        {
            ResolveZipArg(args[a], out _, out int len);

            if (len < minLen)
            {
                minLen = len;
            }
        }

        if (minLen == 0 || minLen == int.MaxValue)
        {
            var emptyDoc = JsonElement.CreateArrayBuilder(workspace, 0);
            return (JsonElement)emptyDoc.RootElement;
        }

        var doc = JsonElement.CreateBuilder(
            workspace,
            (args, minLen),
            static (in (JsonElement[] Args, int MinLen) ctx, ref JsonElement.ArrayBuilder outer) =>
            {
                for (int i = 0; i < ctx.MinLen; i++)
                {
                    outer.AddItem(
                        (ctx.Args, i),
                        static (in (JsonElement[] Args, int I) ictx, ref JsonElement.ArrayBuilder inner) =>
                        {
                            for (int a = 0; a < ictx.Args.Length; a++)
                            {
                                if (ictx.Args[a].ValueKind == JsonValueKind.Array)
                                {
                                    inner.AddItem(ictx.Args[a][ictx.I]);
                                }
                                else if (!ictx.Args[a].IsUndefined())
                                {
                                    inner.AddItem(ictx.Args[a]);
                                }
                            }
                        });
                }
            },
            estimatedMemberCount: (minLen * (args.Length + 1)) + 2);

        return (JsonElement)doc.RootElement;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ResolveZipArg(JsonElement arg, out bool isArray, out int length)
    {
        if (arg.ValueKind == JsonValueKind.Array)
        {
            isArray = true;
            length = arg.GetArrayLength();
        }
        else if (arg.IsUndefined())
        {
            isArray = false;
            length = 0;
        }
        else
        {
            isArray = false;
            length = 1;
        }
    }

    /// <summary>
    /// Buffer-fused 2-arg zip: takes raw backing arrays from <see cref="ElementBuffer.GetContents"/>
    /// and produces the zip result with a single builder document (no intermediate array materialization).
    /// </summary>
    public static JsonElement ZipFromBuffers(
        JsonElement[]? arr0, int cnt0,
        JsonElement[]? arr1, int cnt1,
        JsonWorkspace workspace)
    {
        if (cnt0 == 0 || cnt1 == 0)
        {
            var emptyDoc = JsonElement.CreateArrayBuilder(workspace, 0);
            return (JsonElement)emptyDoc.RootElement;
        }

        int minLen = cnt0 < cnt1 ? cnt0 : cnt1;

        var doc = JsonElement.CreateBuilder(
            workspace,
            (arr0!, arr1!, minLen),
            static (in (JsonElement[] A0, JsonElement[] A1, int MinLen) ctx, ref JsonElement.ArrayBuilder outer) =>
            {
                for (int i = 0; i < ctx.MinLen; i++)
                {
                    outer.AddItem(
                        (ctx.A0, ctx.A1, i),
                        static (in (JsonElement[] A0, JsonElement[] A1, int I) ictx, ref JsonElement.ArrayBuilder inner) =>
                        {
                            inner.AddItem(ictx.A0[ictx.I]);
                            inner.AddItem(ictx.A1[ictx.I]);
                        });
                }
            },
            estimatedMemberCount: (minLen * 3) + 2);

        return (JsonElement)doc.RootElement;
    }

    /// <summary>
    /// Buffer-fused 3-arg zip.
    /// </summary>
    public static JsonElement ZipFromBuffers(
        JsonElement[]? arr0, int cnt0,
        JsonElement[]? arr1, int cnt1,
        JsonElement[]? arr2, int cnt2,
        JsonWorkspace workspace)
    {
        int minLen = cnt0;
        if (cnt1 < minLen)
        {
            minLen = cnt1;
        }

        if (cnt2 < minLen)
        {
            minLen = cnt2;
        }

        if (minLen == 0)
        {
            var emptyDoc = JsonElement.CreateArrayBuilder(workspace, 0);
            return (JsonElement)emptyDoc.RootElement;
        }

        var doc = JsonElement.CreateBuilder(
            workspace,
            (arr0!, arr1!, arr2!, minLen),
            static (in (JsonElement[] A0, JsonElement[] A1, JsonElement[] A2, int MinLen) ctx, ref JsonElement.ArrayBuilder outer) =>
            {
                for (int i = 0; i < ctx.MinLen; i++)
                {
                    outer.AddItem(
                        (ctx.A0, ctx.A1, ctx.A2, i),
                        static (in (JsonElement[] A0, JsonElement[] A1, JsonElement[] A2, int I) ictx, ref JsonElement.ArrayBuilder inner) =>
                        {
                            inner.AddItem(ictx.A0[ictx.I]);
                            inner.AddItem(ictx.A1[ictx.I]);
                            inner.AddItem(ictx.A2[ictx.I]);
                        });
                }
            },
            estimatedMemberCount: (minLen * 4) + 2);

        return (JsonElement)doc.RootElement;
    }

    /// <summary>
    /// Buffer-fused mixed zip: one arg is a pre-resolved JsonElement (e.g., a constant),
    /// the other is a raw buffer from property chain navigation.
    /// </summary>
    public static JsonElement ZipElementAndBuffer(
        in JsonElement resolved,
        JsonElement[]? arr1, int cnt1,
        JsonWorkspace workspace)
    {
        ResolveZipArg(resolved, out bool isArray0, out int len0);
        if (len0 == 0 || cnt1 == 0)
        {
            var emptyDoc = JsonElement.CreateArrayBuilder(workspace, 0);
            return (JsonElement)emptyDoc.RootElement;
        }

        int minLen = len0 < cnt1 ? len0 : cnt1;

        var doc = JsonElement.CreateBuilder(
            workspace,
            (resolved, isArray0, arr1!, minLen),
            static (in (JsonElement R, bool IsArr, JsonElement[] A1, int MinLen) ctx, ref JsonElement.ArrayBuilder outer) =>
            {
                for (int i = 0; i < ctx.MinLen; i++)
                {
                    outer.AddItem(
                        (ctx.R, ctx.IsArr, ctx.A1, i),
                        static (in (JsonElement R, bool IsArr, JsonElement[] A1, int I) ictx, ref JsonElement.ArrayBuilder inner) =>
                        {
                            inner.AddItem(ictx.IsArr ? ictx.R[ictx.I] : ictx.R);
                            inner.AddItem(ictx.A1[ictx.I]);
                        });
                }
            },
            estimatedMemberCount: (minLen * 3) + 2);

        return (JsonElement)doc.RootElement;
    }

    /// <summary>
    /// Buffer-fused mixed zip: first arg is a raw buffer, second is a pre-resolved JsonElement.
    /// </summary>
    public static JsonElement ZipBufferAndElement(
        JsonElement[]? arr0, int cnt0,
        in JsonElement resolved,
        JsonWorkspace workspace)
    {
        ResolveZipArg(resolved, out bool isArray1, out int len1);
        if (cnt0 == 0 || len1 == 0)
        {
            var emptyDoc = JsonElement.CreateArrayBuilder(workspace, 0);
            return (JsonElement)emptyDoc.RootElement;
        }

        int minLen = cnt0 < len1 ? cnt0 : len1;

        var doc = JsonElement.CreateBuilder(
            workspace,
            (arr0!, resolved, isArray1, minLen),
            static (in (JsonElement[] A0, JsonElement R, bool IsArr, int MinLen) ctx, ref JsonElement.ArrayBuilder outer) =>
            {
                for (int i = 0; i < ctx.MinLen; i++)
                {
                    outer.AddItem(
                        (ctx.A0, ctx.R, ctx.IsArr, i),
                        static (in (JsonElement[] A0, JsonElement R, bool IsArr, int I) ictx, ref JsonElement.ArrayBuilder inner) =>
                        {
                            inner.AddItem(ictx.A0[ictx.I]);
                            inner.AddItem(ictx.IsArr ? ictx.R[ictx.I] : ictx.R);
                        });
                }
            },
            estimatedMemberCount: (minLen * 3) + 2);

        return (JsonElement)doc.RootElement;
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
            throw new JsonataException("T2008", SR.T2008_OrderByMustBeNumericOrString, 0);
        }

        if (!bIsNum && !bIsStr)
        {
            throw new JsonataException("T2008", SR.T2008_OrderByMustBeNumericOrString, 0);
        }

        if (aIsNum != bIsNum)
        {
            throw new JsonataException("T2007", SR.T2007_TypeMismatchWithinOrderByClauseAllValuesMustBeOfTheSameType, 0);
        }

        if (aIsNum)
        {
            return a.GetDouble().CompareTo(b.GetDouble());
        }

        return FunctionalCompiler.Utf8CompareOrdinal(a, b);
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
                    throw new JsonataException("D3070", SR.D3070_SortSingleArgRequiresStringsOrNumbers, 0);
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

                    // Both are strings (objects/arrays already excluded) — use zero-allocation UTF-8 comparison
                    int strCmp = FunctionalCompiler.Utf8CompareOrdinal(aEl, bEl);
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

        FormatNumberPicture parsedPic = FormatNumberPicture.Parse(picture, options);
        Utf8ValueStringBuilder sb = new(stackalloc byte[JsonConstants.StackallocByteThreshold]);
        try
        {
            parsedPic.Format(num, ref sb);
            return JsonataHelpers.StringFromRawUtf8Content(sb.AsSpan(), workspace);
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Creates a pre-parsed format-number picture for use with <see cref="FormatNumberPreParsed"/>.
    /// Called once from a static field initializer in generated code.
    /// </summary>
    public static CachedFormatNumberPicture CreateFormatNumberPicture(string picture, JsonElement options)
    {
        return new CachedFormatNumberPicture(FormatNumberPicture.Parse(picture, options));
    }

    /// <summary>
    /// Creates a pre-parsed format-number picture with no options for use with <see cref="FormatNumberPreParsed"/>.
    /// Called once from a static field initializer in generated code.
    /// </summary>
    public static CachedFormatNumberPicture CreateFormatNumberPicture(string picture)
    {
        return new CachedFormatNumberPicture(FormatNumberPicture.Parse(picture, default));
    }

    /// <summary>
    /// JSONata <c>$formatNumber</c> using a pre-parsed picture. Zero per-call allocations.
    /// </summary>
    public static JsonElement FormatNumberPreParsed(in JsonElement input, CachedFormatNumberPicture cached, JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        if (!FunctionalCompiler.TryCoerceToNumber(input, out double num))
        {
            return default;
        }

        Utf8ValueStringBuilder sb = new(stackalloc byte[JsonConstants.StackallocByteThreshold]);
        try
        {
            cached.Picture.Format(num, ref sb);
            return JsonataHelpers.StringFromRawUtf8Content(sb.AsSpan(), workspace);
        }
        finally
        {
            sb.Dispose();
        }
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
            throw new JsonataException("D3100", SR.Format(SR.D3100_FormatBaseRadixOutOfRange, radixInt), 0);
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

    private static int CountCodePoints(ReadOnlySpan<char> span)
    {
        int count = 0;
        for (int i = 0; i < span.Length; i++)
        {
            count++;
            if (char.IsHighSurrogate(span[i]) && i + 1 < span.Length && char.IsLowSurrogate(span[i + 1]))
            {
                i++;
            }
        }

        return count;
    }

    private static int CodePointToCharIndex(ReadOnlySpan<char> span, int codePointIndex)
    {
        int charIdx = 0;
        for (int cpCount = 0; charIdx < span.Length && cpCount < codePointIndex; cpCount++)
        {
            if (char.IsHighSurrogate(span[charIdx]) && charIdx + 1 < span.Length && char.IsLowSurrogate(span[charIdx + 1]))
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

        // Collect elements into a rented array
        JsonElement[] elements = ArrayPool<JsonElement>.Shared.Rent(count);
        try
        {
            int idx = 0;
            foreach (JsonElement item in input.EnumerateArray())
            {
                elements[idx++] = item;
            }

            // Stable insertion sort: comparator returns true if a should be placed AFTER b
            for (int i = 1; i < count; i++)
            {
                JsonElement key = elements[i];
                int j = i - 1;
                while (j >= 0 && comparator(elements[j], key, workspace))
                {
                    elements[j + 1] = elements[j];
                    j--;
                }

                elements[j + 1] = key;
            }

            // Build result array
            var doc = JsonElement.CreateArrayBuilder(workspace, count);
            JsonElement.Mutable root = doc.RootElement;
            for (int i = 0; i < count; i++)
            {
                root.AddItem(elements[i]);
            }

            return (JsonElement)root;
        }
        finally
        {
            ArrayPool<JsonElement>.Shared.Return(elements, clearArray: true);
        }
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
            var buffer = default(ElementBuffer);
            try
            {
                foreach (JsonElement item in input.EnumerateArray())
                {
                    JsonElement result = transform(item, workspace);
                    if (result.ValueKind != JsonValueKind.Undefined)
                    {
                        buffer.Add(result);
                    }
                }

                return buffer.ToArrayResult(workspace);
            }
            finally
            {
                buffer.Dispose();
            }
        }

        // Single value — map once, wrap in array
        JsonElement single = transform(input, workspace);
        if (single.ValueKind == JsonValueKind.Undefined)
        {
            return default;
        }

        var singleDoc = JsonElement.CreateArrayBuilder(workspace, 1);
        singleDoc.RootElement.AddItem(single);
        return (JsonElement)singleDoc.RootElement;
    }

    /// <summary>
    /// Specialized map that keeps results as raw <c>double</c> values, avoiding the per-element
    /// <see cref="DoubleToElement"/> and intermediate <c>FixedJsonValueDocument</c> creation.
    /// The result array is built via CVB <see cref="JsonElement.ArrayBuilder.AddItem(double)"/>
    /// which writes doubles directly to the MetadataDb.
    /// </summary>
    public static JsonElement MapElementsDouble(
        in JsonElement input,
        Func<JsonElement, JsonWorkspace, double> transform,
        JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        if (input.ValueKind == JsonValueKind.Array)
        {
            int len = input.GetArrayLength();
            if (len == 0)
            {
                return JsonataHelpers.EmptyArray();
            }

            // Collect doubles into a rented array, then build via CVB in one pass.
            // Always rent (never stackalloc) so we can pass the array directly
            // as the CVB context tuple without a ToArray() heap allocation.
            double[] rented = ArrayPool<double>.Shared.Rent(len);

            try
            {
                int count = 0;
                foreach (JsonElement item in input.EnumerateArray())
                {
                    double d = transform(item, workspace);
                    if (!double.IsNaN(d))
                    {
                        if (count == rented.Length)
                        {
                            // Grow — extremely rare (NaN filtering changed count estimate)
                            double[] bigger = ArrayPool<double>.Shared.Rent(rented.Length * 2);
                            rented.AsSpan(0, count).CopyTo(bigger);
                            ArrayPool<double>.Shared.Return(rented);
                            rented = bigger;
                        }

                        rented[count++] = d;
                    }
                }

                if (count == 0)
                {
                    return JsonataHelpers.EmptyArray();
                }

                JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
                    workspace,
                    (rented, count),
                    static (in (double[] Array, int Count) ctx, ref JsonElement.ArrayBuilder builder) =>
                    {
                        for (int i = 0; i < ctx.Count; i++)
                        {
                            builder.AddItem(ctx.Array[i]);
                        }
                    },
                    estimatedMemberCount: count + 2,
                    initialValueBufferSize: Math.Max(8192, count * 28));

                return (JsonElement)doc.RootElement;
            }
            finally
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }

        // Single value — map once, wrap in array
        double single = transform(input, workspace);
        if (double.IsNaN(single))
        {
            return default;
        }

        JsonDocumentBuilder<JsonElement.Mutable> singleDoc = JsonElement.CreateBuilder(
            workspace,
            single,
            static (in double ctx, ref JsonElement.ArrayBuilder builder) =>
            {
                builder.AddItem(ctx);
            },
            estimatedMemberCount: 3);

        return (JsonElement)singleDoc.RootElement;
    }

    /// <summary>
    /// Per-element object construction with groupby semantics: returns <c>default</c> for
    /// empty or null/undefined input, singularises a 1-element result, and skips undefined
    /// transform results. Used for <c>path.{StringKey: expr, ...}</c> where all keys are
    /// literal string constants.
    /// </summary>
    public static JsonElement GroupByMapElements(
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
            if (len == 0)
            {
                return default;
            }

            var buffer = default(ElementBuffer);
            try
            {
                foreach (JsonElement item in input.EnumerateArray())
                {
                    JsonElement result = transform(item, workspace);
                    if (result.ValueKind != JsonValueKind.Undefined)
                    {
                        buffer.Add(result);
                    }
                }

                return buffer.ToResult(workspace);
            }
            finally
            {
                buffer.Dispose();
            }
        }

        // Single value — transform once
        return transform(input, workspace);
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
            var buffer = default(ElementBuffer);
            try
            {
                int i = 0;
                foreach (JsonElement item in input.EnumerateArray())
                {
                    JsonElement idx = JsonataHelpers.NumberFromDouble(i, workspace);
                    JsonElement result = transform(item, idx, workspace);
                    if (result.ValueKind != JsonValueKind.Undefined)
                    {
                        buffer.Add(result);
                    }

                    i++;
                }

                return buffer.ToArrayResult(workspace);
            }
            finally
            {
                buffer.Dispose();
            }
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
            var buffer = default(ElementBuffer);
            try
            {
                foreach (JsonElement item in input.EnumerateArray())
                {
                    if (predicate(item, workspace))
                    {
                        buffer.Add(item);
                    }
                }

                return buffer.ToResult(workspace);
            }
            finally
            {
                buffer.Dispose();
            }
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

            if (hasInit)
            {
                accumulator = initial;
                foreach (JsonElement item in input.EnumerateArray())
                {
                    accumulator = reducer(accumulator, item, workspace);
                }
            }
            else
            {
                // Skip the first element (use it as the initial accumulator)
                bool first = true;
                accumulator = default;
                foreach (JsonElement item in input.EnumerateArray())
                {
                    if (first)
                    {
                        accumulator = item;
                        first = false;
                    }
                    else
                    {
                        accumulator = reducer(accumulator, item, workspace);
                    }
                }
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

    /// <summary>
    /// Specialized reduce that keeps the accumulator as a raw <c>double</c> throughout the loop,
    /// avoiding the per-iteration <see cref="DoubleToElement"/>/<see cref="ToArithmeticDoubleLeft"/>
    /// roundtrip that creates a <see cref="JsonataHelpers.NumberFromDouble"/> document per iteration.
    /// Only the final result is materialized to <see cref="JsonElement"/>.
    /// </summary>
    public static JsonElement ReduceElementsDouble(
        in JsonElement input,
        double initial,
        Func<double, JsonElement, JsonWorkspace, double> reducer,
        JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        double accumulator;

        if (input.ValueKind == JsonValueKind.Array)
        {
            int len = input.GetArrayLength();
            if (len == 0)
            {
                return DoubleToElement(initial, workspace);
            }

            accumulator = initial;
            foreach (JsonElement item in input.EnumerateArray())
            {
                accumulator = reducer(accumulator, item, workspace);
            }
        }
        else
        {
            // Scalar input treated as single-element sequence
            accumulator = reducer(initial, input, workspace);
        }

        return DoubleToElement(accumulator, workspace);
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

        throw new JsonataException("T1003", SR.Format(SR.T1003_KeyMustBeStringGotValueKind, key.ValueKind), 0);
    }

    /// <summary>
    /// Optimized per-element group-by for path steps where both key and value are simple
    /// property names (<c>.{keyProp: valueProp}</c>). Accesses properties directly via
    /// <see cref="JsonElement.TryGetProperty(ReadOnlySpan{byte}, out JsonElement)"/> with
    /// pre-compiled UTF-8 names — no Dictionary, no List, no lambda dispatch.
    /// </summary>
    /// <param name="input">The input (array or scalar).</param>
    /// <param name="keyPropUtf8">UTF-8 encoded key property name.</param>
    /// <param name="valuePropUtf8">UTF-8 encoded value property name.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>An array of objects (or singleton), or <c>default</c> if input is undefined/empty.</returns>
    public static JsonElement SimpleGroupByPerElement(
        in JsonElement input,
        byte[] keyPropUtf8,
        byte[] valuePropUtf8,
        JsonWorkspace workspace)
    {
        if (input.ValueKind == JsonValueKind.Array)
        {
            int len = input.GetArrayLength();
            if (len == 0)
            {
                return default;
            }

            var doc = JsonElement.CreateArrayBuilder(workspace, len * 2);
            JsonElement.Mutable root = doc.RootElement;
            int count = 0;

            foreach (JsonElement item in input.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!item.TryGetProperty(keyPropUtf8, out JsonElement keyEl))
                {
                    continue;
                }

                if (keyEl.ValueKind == JsonValueKind.Number)
                {
                    throw new JsonataException("T1003", SR.T1003_KeyInObjectStructureMustEvaluateToAStringGotNumber, 0);
                }

                if (keyEl.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                if (!item.TryGetProperty(valuePropUtf8, out JsonElement valEl))
                {
                    continue;
                }

                // Build object directly into the array document via AddItem callback.
                var ctx = new GroupByObjectContext(keyEl, valEl);
                root.AddItem(ctx, GroupByObjectCallback, 1);
                count++;
            }

            return count == 0 ? default : count == 1 ? root[0] : (JsonElement)root;
        }

        if (input.ValueKind != JsonValueKind.Undefined)
        {
            return BuildSingleEntryObject(input, keyPropUtf8, valuePropUtf8, workspace);
        }

        return default;
    }

    /// <summary>
    /// Optimized annotation group-by where both key and value are simple property names.
    /// Groups ALL elements by key property, collecting values per group. Mirrors the
    /// runtime's <c>ApplySimpleNamePairGroupBy</c> using ArrayPool buffers and O(n²)
    /// grouping with stackalloc — no Dictionary, no List, no lambda dispatch.
    /// </summary>
    /// <param name="input">The input (array or scalar).</param>
    /// <param name="keyPropUtf8">UTF-8 encoded key property name.</param>
    /// <param name="valuePropUtf8">UTF-8 encoded value property name.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>A JSON object with grouped values, or <c>default</c> if input is undefined.</returns>
    public static JsonElement SimpleGroupByAnnotation(
        in JsonElement input,
        byte[] keyPropUtf8,
        byte[] valuePropUtf8,
        JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        int maxElements;
        if (input.ValueKind == JsonValueKind.Array)
        {
            maxElements = input.GetArrayLength();
            if (maxElements == 0)
            {
                var emptyDoc = JsonElement.CreateObjectBuilder(workspace, 0);
                return (JsonElement)emptyDoc.RootElement;
            }
        }
        else
        {
            maxElements = 1;
        }

        JsonElement[] keyBuffer = ArrayPool<JsonElement>.Shared.Rent(maxElements);
        JsonElement[] valBuffer = ArrayPool<JsonElement>.Shared.Rent(maxElements);

        try
        {
            int pairCount = 0;

            if (input.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in input.EnumerateArray())
                {
                    CollectSimpleGroupByPair(item, keyPropUtf8, valuePropUtf8, keyBuffer, valBuffer, ref pairCount);
                }
            }
            else
            {
                CollectSimpleGroupByPair(input, keyPropUtf8, valuePropUtf8, keyBuffer, valBuffer, ref pairCount);
            }

            if (pairCount == 0)
            {
                if (input.ValueKind == JsonValueKind.Array)
                {
                    var emptyDoc = JsonElement.CreateObjectBuilder(workspace, 0);
                    return (JsonElement)emptyDoc.RootElement;
                }

                return default;
            }

            // Phase 2: Group by key with O(n²) scan + stackalloc bool[].
            var objDoc = JsonElement.CreateObjectBuilder(workspace, Math.Min(pairCount, 16));
            var objRoot = objDoc.RootElement;

            Span<bool> processed = pairCount <= 256
                ? stackalloc bool[Math.Min(pairCount, 256)]
                : new bool[pairCount];

            for (int i = 0; i < pairCount; i++)
            {
                if (processed[i])
                {
                    continue;
                }

                processed[i] = true;

                using var keyUtf8 = keyBuffer[i].GetUtf8String();
                ReadOnlySpan<byte> keyBytes = keyUtf8.Span;

                bool hasDuplicates = false;
                for (int j = i + 1; j < pairCount; j++)
                {
                    if (!processed[j] && keyBuffer[j].ValueEquals(keyBytes))
                    {
                        hasDuplicates = true;
                        break;
                    }
                }

                if (!hasDuplicates)
                {
                    objRoot.SetProperty(keyBytes, valBuffer[i]);
                }
                else
                {
                    var arrDoc = JsonElement.CreateArrayBuilder(workspace, 4);
                    var arrRoot = arrDoc.RootElement;
                    arrRoot.AddItem(valBuffer[i]);

                    for (int j = i + 1; j < pairCount; j++)
                    {
                        if (!processed[j] && keyBuffer[j].ValueEquals(keyBytes))
                        {
                            arrRoot.AddItem(valBuffer[j]);
                            processed[j] = true;
                        }
                    }

                    objRoot.SetProperty(keyBytes, (JsonElement)arrRoot);
                }
            }

            return objRoot.GetPropertyCount() == 0 ? default : (JsonElement)objRoot;
        }
        finally
        {
            ArrayPool<JsonElement>.Shared.Return(keyBuffer);
            ArrayPool<JsonElement>.Shared.Return(valBuffer);
        }
    }

    private static JsonElement BuildSingleEntryObject(
        in JsonElement element,
        byte[] keyPropUtf8,
        byte[] valuePropUtf8,
        JsonWorkspace workspace)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        if (!element.TryGetProperty(keyPropUtf8, out JsonElement keyEl))
        {
            return default;
        }

        if (keyEl.ValueKind == JsonValueKind.Number)
        {
            throw new JsonataException("T1003", SR.T1003_KeyInObjectStructureMustEvaluateToAStringGotNumber, 0);
        }

        if (keyEl.ValueKind != JsonValueKind.String)
        {
            return default;
        }

        if (!element.TryGetProperty(valuePropUtf8, out JsonElement valEl))
        {
            return default;
        }

        using var keyUtf8 = keyEl.GetUtf8String();
        var doc = JsonElement.CreateObjectBuilder(workspace, 1);
        doc.RootElement.SetProperty(keyUtf8.Span, valEl);
        return (JsonElement)doc.RootElement;
    }

    private static void CollectSimpleGroupByPair(
        in JsonElement element,
        byte[] keyPropUtf8,
        byte[] valuePropUtf8,
        JsonElement[] keyBuffer,
        JsonElement[] valBuffer,
        ref int pairCount)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                TryAddSimpleGroupByPair(item, keyPropUtf8, valuePropUtf8, keyBuffer, valBuffer, ref pairCount);
            }
        }
        else
        {
            TryAddSimpleGroupByPair(element, keyPropUtf8, valuePropUtf8, keyBuffer, valBuffer, ref pairCount);
        }
    }

    private static void TryAddSimpleGroupByPair(
        in JsonElement element,
        byte[] keyPropUtf8,
        byte[] valuePropUtf8,
        JsonElement[] keyBuffer,
        JsonElement[] valBuffer,
        ref int pairCount)
    {
        if (!element.TryGetProperty(keyPropUtf8, out JsonElement keyEl))
        {
            return;
        }

        if (keyEl.ValueKind == JsonValueKind.Number)
        {
            throw new JsonataException("T1003", SR.T1003_KeyInObjectStructureMustEvaluateToAStringGotNumber, 0);
        }

        if (keyEl.ValueKind != JsonValueKind.String)
        {
            return;
        }

        if (!element.TryGetProperty(valuePropUtf8, out JsonElement valEl))
        {
            return;
        }

        keyBuffer[pairCount] = keyEl;
        valBuffer[pairCount] = valEl;
        pairCount++;
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

    /// <summary>
    /// Collects results from an array element by applying the current property step
    /// to each array item, then continuing the chain. All results are added to the
    /// same <paramref name="root"/> builder — no nested builders are created.
    /// </summary>
    private static void CollectChainFlat(
        in JsonElement array,
        byte[][] names,
        int stepIndex,
        JsonElement.Mutable root,
        ref int count)
    {
        byte[] name = names[stepIndex];
        int nextStep = stepIndex + 1;
        bool isLastStep = nextStep >= names.Length;

        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                if (item.TryGetProperty((ReadOnlySpan<byte>)name, out var val))
                {
                    if (isLastStep)
                    {
                        count = AddResultWithFlatten(root, val, count);
                    }
                    else
                    {
                        ContinueChainFlat(val, names, nextStep, root, ref count);
                    }
                }
            }
            else if (item.ValueKind == JsonValueKind.Array)
            {
                // Nested array auto-flatten: apply same step to inner array
                CollectChainFlat(item, names, stepIndex, root, ref count);
            }
        }
    }

    /// <summary>
    /// Continues navigating remaining chain steps from a single value,
    /// collecting all results into the shared <paramref name="root"/> builder.
    /// </summary>
    private static void ContinueChainFlat(
        in JsonElement value,
        byte[][] names,
        int nextIndex,
        JsonElement.Mutable root,
        ref int count)
    {
        // Walk through object steps until we complete the chain or hit an array.
        JsonElement current = value;

        for (int i = nextIndex; i < names.Length; i++)
        {
            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty((ReadOnlySpan<byte>)names[i], out current))
                {
                    return;
                }
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                CollectChainFlat(current, names, i, root, ref count);
                return;
            }
            else
            {
                return;
            }
        }

        count = AddResultWithFlatten(root, current, count);
    }

    private static JsonElement NavigatePropertyOverArray(
        in JsonElement array, byte[] name, JsonWorkspace workspace)
    {
        var buffer = default(ElementBuffer);
        try
        {
            foreach (JsonElement item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    if (item.TryGetProperty((ReadOnlySpan<byte>)name, out var val))
                    {
                        buffer.AddFlatten(val);
                    }
                }
                else if (item.ValueKind == JsonValueKind.Array)
                {
                    JsonElement nested = NavigatePropertyOverArray(item, name, workspace);
                    if (!nested.IsNullOrUndefined())
                    {
                        buffer.AddFlatten(nested);
                    }
                }
            }

            return buffer.Count == 0
                ? default
                : buffer.Count == 1
                    ? buffer[0]
                    : buffer.ToResult(workspace);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Collects property values from array elements directly into an <see cref="JsonElement.ArrayBuilder"/>.
    /// Used by <see cref="NavigatePropertyToArray"/> to avoid intermediate buffers.
    /// </summary>
    private static void CollectPropertyFlatIntoBuilder(
        in JsonElement array, byte[] name, ref JsonElement.ArrayBuilder builder)
    {
        ReadOnlySpan<byte> nameSpan = name;
        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                if (item.TryGetProperty(nameSpan, out var val))
                {
                    if (val.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement child in val.EnumerateArray())
                        {
                            builder.AddItem(child);
                        }
                    }
                    else
                    {
                        builder.AddItem(val);
                    }
                }
            }
            else if (item.ValueKind == JsonValueKind.Array)
            {
                CollectPropertyFlatIntoBuilder(item, name, ref builder);
            }
        }
    }

    /// <summary>
    /// Adds a result element to a mutable array builder with auto-flattening.
    /// If the result is an array, each element is added individually.
    /// </summary>
    /// <param name="root">The mutable array root to add to.</param>
    /// <param name="result">The result element to add.</param>
    /// <param name="count">The current count of elements added.</param>
    /// <returns>The updated count.</returns>
    public static int AddResultWithFlatten(JsonElement.Mutable root, in JsonElement result, int count)
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
            throw new JsonataException("T2001", SR.Format(SR.T2001_LeftSideOfOperatorMustBeNumber, op), 0);
        }

        if (!rhsUndef && right.ValueKind != JsonValueKind.Number)
        {
            throw new JsonataException("T2002", SR.Format(SR.T2002_RightSideOfOperatorMustBeNumber, op), 0);
        }

        if (lhsUndef || rhsUndef)
        {
            return default;
        }

        double result = compute(left.GetDouble(), right.GetDouble());

        if (double.IsInfinity(result) || double.IsNaN(result))
        {
            throw new JsonataException("D1001", SR.Format(SR.D1001_NumberOutOfRangeWithExpr, op, left.GetRawText(), right.GetRawText()), 0);
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
            throw new JsonataException("T2010", SR.Format(SR.T2010_ExpressionsMustBeNumbersOrStringsWithOp, op), 0);
        }

        if (left.ValueKind == JsonValueKind.Null)
        {
            throw new JsonataException("T2010", SR.Format(SR.T2010_ExpressionsMustBeNumbersOrStringsWithOp, op), 0);
        }

        if (!right.IsNullOrUndefined() && right.ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Array or JsonValueKind.Object)
        {
            throw new JsonataException("T2010", SR.Format(SR.T2010_ExpressionsMustBeNumbersOrStringsWithOp, op), 0);
        }

        if (right.ValueKind == JsonValueKind.Null)
        {
            throw new JsonataException("T2010", SR.Format(SR.T2010_ExpressionsMustBeNumbersOrStringsWithOp, op), 0);
        }

        // If either side is undefined (after invalid-type check), return undefined
        if (left.ValueKind == JsonValueKind.Undefined || right.ValueKind == JsonValueKind.Undefined)
        {
            return default;
        }

        // String comparison
        if (left.ValueKind == JsonValueKind.String && right.ValueKind == JsonValueKind.String)
        {
            int result = FunctionalCompiler.Utf8CompareOrdinal(left, right);
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
        throw new JsonataException("T2009", SR.T2009_TheValuesEitherSideOfTheOperatorMustBeOfTheSameDataType, 0);
    }

    private static bool ElementEquals(in JsonElement left, in JsonElement right)
    {
        // Delegate to the runtime's zero-allocation equality (uses raw UTF-8 byte comparison)
        return FunctionalCompiler.JsonElementEquals(left, right);
    }

    /// <summary>
    /// Writes the coerced string representation of a JSON element directly to a UTF-8 builder.
    /// For the &amp; concat operator: undefined and null both produce empty (append nothing).
    /// </summary>
    /// <param name="value">The JSON element to coerce.</param>
    /// <param name="sb">The UTF-8 string builder to append to.</param>
    private static void AppendCoercedValue(in JsonElement value, ref Utf8ValueStringBuilder sb)
    {
        if (value.IsNullOrUndefined())
        {
            return;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                {
                    using UnescapedUtf8JsonString utf8 = value.GetUtf8String();
                    sb.Append(utf8.Span);
                    break;
                }

            case JsonValueKind.Number:
                AppendFormattedNumber(value, ref sb);
                break;

            case JsonValueKind.True:
                sb.Append("true"u8);
                break;

            case JsonValueKind.False:
                sb.Append("false"u8);
                break;

            default:
                // Object/Array — write raw UTF-8 JSON representation directly
                {
                    using RawUtf8JsonString rawUtf8 = JsonMarshal.GetRawUtf8Value(value);
                    sb.Append(rawUtf8.Span);
                    break;
                }
        }
    }

    /// <summary>
    /// Appends a number formatted like JavaScript to a UTF-8 builder.
    /// Integer values with plain literal representation use raw UTF-8 (zero alloc).
    /// Non-integer values use G15 formatting directly to UTF-8 (zero alloc on .NET 8+
    /// for the common non-exponent case; falls back to string allocation for exponent forms).
    /// </summary>
    private static void AppendFormattedNumber(in JsonElement element, ref Utf8ValueStringBuilder sb)
    {
        double value = element.GetDouble();
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            sb.Append("null"u8);
            return;
        }

        // Integer values: use raw UTF-8 bytes directly (zero alloc)
        if (value == Math.Floor(value))
        {
            using RawUtf8JsonString rawUtf8 = JsonMarshal.GetRawUtf8Value(element);
            ReadOnlySpan<byte> raw = rawUtf8.Span;
            if (raw.IndexOf((byte)'.') < 0 && raw.IndexOf((byte)'e') < 0 && raw.IndexOf((byte)'E') < 0)
            {
                sb.Append(raw);
                return;
            }
        }

#if NET8_0_OR_GREATER
        // Non-integer: format G15 directly to UTF-8 bytes (zero alloc for non-exponent case)
        Span<byte> scratch = stackalloc byte[64];
        if (value.TryFormat(scratch, out int written, "G15", System.Globalization.CultureInfo.InvariantCulture))
        {
            ReadOnlySpan<byte> result = scratch.Slice(0, written);
            if (result.IndexOf((byte)'E') < 0)
            {
                sb.Append(result);
                return;
            }
        }
#endif

        // Exponent case or pre-.NET 8: fall back to string-based formatting
        string formatted = FunctionalCompiler.FormatNumberLikeJavaScript(value);
        Span<byte> dest = sb.AppendSpan(formatted.Length);
        for (int i = 0; i < formatted.Length; i++)
        {
            dest[i] = (byte)formatted[i];
        }
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

    /// <summary>
    /// Delegate for <c>$each</c> transform: receives the property value and
    /// the property key as unescaped UTF-8 bytes, avoiding intermediate <see cref="JsonElement"/> creation for the key.
    /// </summary>
    /// <param name="value">The property value.</param>
    /// <param name="keyUtf8">The property name as unescaped UTF-8 bytes.</param>
    /// <param name="workspace">The workspace for memory allocation.</param>
    /// <returns>The transformed element.</returns>
    public delegate JsonElement EachTransform(JsonElement value, ReadOnlySpan<byte> keyUtf8, JsonWorkspace workspace);

    /// <summary>
    /// Delegate for <c>$each</c> transform with original object: receives the property value,
    /// the property key as unescaped UTF-8 bytes, the original object, and the workspace.
    /// </summary>
    /// <param name="value">The property value.</param>
    /// <param name="keyUtf8">The property name as unescaped UTF-8 bytes.</param>
    /// <param name="obj">The original object being iterated.</param>
    /// <param name="workspace">The workspace for memory allocation.</param>
    /// <returns>The transformed element.</returns>
    public delegate JsonElement EachTransformWithObject(JsonElement value, ReadOnlySpan<byte> keyUtf8, JsonElement obj, JsonWorkspace workspace);

    /// <summary>
    /// Delegate for <c>$sift</c> predicate: receives the property value and
    /// the property key as unescaped UTF-8 bytes, avoiding intermediate <see cref="JsonElement"/> creation for the key.
    /// </summary>
    /// <param name="value">The property value.</param>
    /// <param name="keyUtf8">The property name as unescaped UTF-8 bytes.</param>
    /// <param name="workspace">The workspace for memory allocation.</param>
    /// <returns><see langword="true"/> if the property should be included.</returns>
    public delegate bool SiftPredicate(JsonElement value, ReadOnlySpan<byte> keyUtf8, JsonWorkspace workspace);

    /// <summary>
    /// Delegate for <c>$sift</c> predicate with original object: receives the property value,
    /// the property key as unescaped UTF-8 bytes, the original object, and the workspace.
    /// </summary>
    /// <param name="value">The property value.</param>
    /// <param name="keyUtf8">The property name as unescaped UTF-8 bytes.</param>
    /// <param name="obj">The original object being sifted.</param>
    /// <param name="workspace">The workspace for memory allocation.</param>
    /// <returns><see langword="true"/> if the property should be included.</returns>
    public delegate bool SiftPredicateWithObject(JsonElement value, ReadOnlySpan<byte> keyUtf8, JsonElement obj, JsonWorkspace workspace);

    /// <summary>
    /// Creates a JSON string element from unescaped UTF-8 bytes.
    /// Forwarding method so generated code can use the single <c>H.</c> prefix.
    /// </summary>
    /// <param name="unescapedUtf8">The unescaped UTF-8 bytes.</param>
    /// <param name="workspace">The workspace for memory allocation.</param>
    /// <returns>A JSON string element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement StringFromUnescapedUtf8(ReadOnlySpan<byte> unescapedUtf8, JsonWorkspace workspace)
        => JsonataHelpers.StringFromUnescapedUtf8(unescapedUtf8, workspace);

    /// <summary>
    /// JSONata <c>$each</c> — maps a function over each property of an object,
    /// returning an array of the transform results.
    /// </summary>
    /// <param name="input">The object to iterate.</param>
    /// <param name="transform">A delegate <c>(value, keyUtf8, workspace) → result</c>.</param>
    /// <param name="workspace">The workspace for memory allocation.</param>
    /// <returns>An array of transform results, or undefined if input is not an object.</returns>
    public static JsonElement EachProperty(
        in JsonElement input,
        EachTransform transform,
        JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        var buffer = default(ElementBuffer);
        try
        {
            foreach (var prop in input.EnumerateObject())
            {
                using UnescapedUtf8JsonString nameUtf8 = prop.Utf8NameSpan;
                JsonElement result = transform(prop.Value, nameUtf8.Span, workspace);
                if (result.ValueKind != JsonValueKind.Undefined)
                {
                    buffer.Add(result);
                }
            }

            return buffer.ToArrayResult(workspace);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// JSONata <c>$each(obj, fn($v,$k,$o))</c> — iterates properties with access to the original object.
    /// </summary>
    /// <param name="input">The object to iterate.</param>
    /// <param name="transform">A delegate <c>(value, keyUtf8, obj, workspace) → JsonElement</c>.</param>
    /// <param name="workspace">The workspace for memory allocation.</param>
    /// <returns>An array of transform results, or undefined if input is not an object.</returns>
    public static JsonElement EachProperty(
        in JsonElement input,
        EachTransformWithObject transform,
        JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        var buffer = default(ElementBuffer);
        try
        {
            foreach (var prop in input.EnumerateObject())
            {
                using UnescapedUtf8JsonString nameUtf8 = prop.Utf8NameSpan;
                JsonElement result = transform(prop.Value, nameUtf8.Span, input, workspace);
                if (result.ValueKind != JsonValueKind.Undefined)
                {
                    buffer.Add(result);
                }
            }

            return buffer.ToArrayResult(workspace);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// JSONata <c>$sift</c> — filters the properties of an object,
    /// returning a new object containing only properties for which the predicate returns truthy.
    /// Uses the CVB pattern for forward-only object construction.
    /// </summary>
    /// <param name="input">The object to filter.</param>
    /// <param name="predicate">A delegate <c>(value, keyUtf8, workspace) → bool</c>.</param>
    /// <param name="workspace">The workspace for memory allocation.</param>
    /// <returns>A filtered object, or undefined if no properties match or input is not an object.</returns>
    public static JsonElement SiftProperty(
        in JsonElement input,
        SiftPredicate predicate,
        JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            (input, predicate, workspace),
            static (in (JsonElement input, SiftPredicate predicate, JsonWorkspace workspace) ctx, ref JsonElement.ObjectBuilder builder) =>
            {
                foreach (var prop in ctx.input.EnumerateObject())
                {
                    using UnescapedUtf8JsonString nameUtf8 = prop.Utf8NameSpan;
                    if (ctx.predicate(prop.Value, nameUtf8.Span, ctx.workspace))
                    {
                        builder.AddProperty(nameUtf8.Span, prop.Value);
                    }
                }
            },
            estimatedMemberCount: 16);

        JsonElement result = (JsonElement)doc.RootElement;
        var enumerator = result.EnumerateObject();
        return enumerator.MoveNext() ? result : default;
    }

    /// <summary>
    /// JSONata <c>$sift(obj, fn($v,$k,$o))</c> — filters properties with access to the original object.
    /// </summary>
    /// <param name="input">The object to filter.</param>
    /// <param name="predicate">A delegate <c>(value, keyUtf8, obj, workspace) → bool</c>.</param>
    /// <param name="workspace">The workspace for memory allocation.</param>
    /// <returns>A filtered object, or undefined if no properties match or input is not an object.</returns>
    public static JsonElement SiftProperty(
        in JsonElement input,
        SiftPredicateWithObject predicate,
        JsonWorkspace workspace)
    {
        if (input.IsNullOrUndefined())
        {
            return default;
        }

        if (input.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            (input, predicate, workspace),
            static (in (JsonElement input, SiftPredicateWithObject predicate, JsonWorkspace workspace) ctx, ref JsonElement.ObjectBuilder builder) =>
            {
                foreach (var prop in ctx.input.EnumerateObject())
                {
                    using UnescapedUtf8JsonString nameUtf8 = prop.Utf8NameSpan;
                    if (ctx.predicate(prop.Value, nameUtf8.Span, ctx.input, ctx.workspace))
                    {
                        builder.AddProperty(nameUtf8.Span, prop.Value);
                    }
                }
            },
            estimatedMemberCount: 16);

        JsonElement result = (JsonElement)doc.RootElement;
        var enumerator = result.EnumerateObject();
        return enumerator.MoveNext() ? result : default;
    }
}