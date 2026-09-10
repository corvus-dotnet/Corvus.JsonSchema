// <copyright file="CeilingProbe.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Runtime.CompilerServices;
using Corvus.MicroModels;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.RuntimeEvaluator;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks;

/// <summary>
/// Ceiling probe for emitted evaluation code: hand-written, straight-line evaluators for the micro cases, using the
/// same raw document access and the same helpers the engine uses but no node graph, no flags and no generic dispatch.
/// This is what a per-schema code emitter would produce at best; the gap to the engine is the most an emitter can win.
/// </summary>
public static class CeilingProbe
{
    private const int RowSize = 12;
    private static readonly NumberValue Hundred = new("100"u8);
    private static readonly NumberValue Thousand = new("1000"u8);

    public static int Run(string[] args)
    {
        int rounds = args.Length > 0 && int.TryParse(args[0], out int r) ? r : 30;
        string[] only = args.Length > 1 ? args[1..] : [];
        var b = new MicroBenchmarks();
        b.Setup();
        string schemaDir = Path.Combine(AppContext.BaseDirectory, "micro-schemas");
        JsonSchemaEvaluator Compile(string name) => JsonSchemaEvaluator.Compile(File.ReadAllBytes(Path.Combine(schemaDir, name)), new JsonSchemaEvaluatorOptions());

        using JsonSchemaEvaluator objectEvaluator = Compile("object.json");
        using JsonSchemaEvaluator arrayEvaluator = Compile("array.json");
        using JsonSchemaEvaluator unevaluatedEvaluator = Compile("unevaluated.json");
        using JsonSchemaEvaluator dynamicEvaluator = Compile("dynamic.json");
        using JsonSchemaEvaluator oneOfEvaluator = Compile("oneof.json");
        using ParsedJsonDocument<JsonElement> objectDoc = ParsedJsonDocument<JsonElement>.Parse(MicroBenchmarks.ObjectJsonText);
        using ParsedJsonDocument<JsonElement> arrayDoc = ParsedJsonDocument<JsonElement>.Parse(MicroBenchmarks.ArrayJsonText);
        using ParsedJsonDocument<JsonElement> treeDoc = ParsedJsonDocument<JsonElement>.Parse(MicroBenchmarks.TreeJsonText);
        using ParsedJsonDocument<MicroObject> typedObjectDoc = ParsedJsonDocument<MicroObject>.Parse(MicroBenchmarks.ObjectJsonText);
        using ParsedJsonDocument<MicroArray> typedArrayDoc = ParsedJsonDocument<MicroArray>.Parse(MicroBenchmarks.ArrayJsonText);
        using ParsedJsonDocument<MicroUnevaluated> typedUnevaluatedDoc = ParsedJsonDocument<MicroUnevaluated>.Parse(MicroBenchmarks.ObjectJsonText);
        using ParsedJsonDocument<MicroDynamic> typedTreeDoc = ParsedJsonDocument<MicroDynamic>.Parse(MicroBenchmarks.TreeJsonText);
        using ParsedJsonDocument<MicroOneOf> typedOneOfDoc = ParsedJsonDocument<MicroOneOf>.Parse(MicroBenchmarks.ObjectJsonText);

        // Agreement first: every specialised evaluator must return what the engine returns.
        (string Name, bool Engine, bool Specialised)[] checks =
        [
            ("Object", objectEvaluator.Evaluate(objectDoc.RootElement), ObjectSpecialised(objectDoc)),
            ("Array", arrayEvaluator.Evaluate(arrayDoc.RootElement), ArraySpecialised(arrayDoc)),
            ("Unevaluated", unevaluatedEvaluator.Evaluate(objectDoc.RootElement), UnevaluatedSpecialised(objectDoc)),
            ("DynamicRef", dynamicEvaluator.Evaluate(treeDoc.RootElement), DynamicRefSpecialised(treeDoc)),
            ("OneOf", oneOfEvaluator.Evaluate(objectDoc.RootElement), OneOfSpecialised(objectDoc)),
        ];
        int disagreements = 0;
        foreach ((string name, bool engine, bool specialised) in checks)
        {
            if (engine != specialised)
            {
                disagreements++;
                Console.WriteLine($"{name}: engine={engine} specialised={specialised}");
            }
        }

        Console.WriteLine($"{"category",-14} {"typed",12} {"runtime",12} {"specialised",12} {"rt/typed",9} {"spec/rt",9}");
        foreach ((string name, Func<bool> typed, Func<bool> runtime, Func<bool> specialised) in new (string, Func<bool>, Func<bool>, Func<bool>)[]
        {
            ("Object", () => typedObjectDoc.RootElement.EvaluateSchema(), () => objectEvaluator.Evaluate(objectDoc.RootElement), () => ObjectSpecialised(objectDoc)),
            ("Array", () => typedArrayDoc.RootElement.EvaluateSchema(), () => arrayEvaluator.Evaluate(arrayDoc.RootElement), () => ArraySpecialised(arrayDoc)),
            ("Unevaluated", () => typedUnevaluatedDoc.RootElement.EvaluateSchema(), () => unevaluatedEvaluator.Evaluate(objectDoc.RootElement), () => UnevaluatedSpecialised(objectDoc)),
            ("DynamicRef", () => typedTreeDoc.RootElement.EvaluateSchema(), () => dynamicEvaluator.Evaluate(treeDoc.RootElement), () => DynamicRefSpecialised(treeDoc)),
            ("OneOf", () => typedOneOfDoc.RootElement.EvaluateSchema(), () => oneOfEvaluator.Evaluate(objectDoc.RootElement), () => OneOfSpecialised(objectDoc)),
        })
        {
            if (only.Length > 0 && !Array.Exists(only, o => string.Equals(o, name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            (double t, double rt, double sp) = QuickTimer.Run3(rounds, 2000, typed, runtime, specialised);
            Console.WriteLine($"{name,-14} {QuickTimer.Format(t),12} {QuickTimer.Format(rt),12} {QuickTimer.Format(sp),12} {rt / t,9:F2} {sp / rt,9:F2}");
        }

        b.Cleanup();
        return disagreements;
    }

    // ---- object.json ----------------------------------------------------------------------------------------

    private static bool ObjectSpecialised(ParsedJsonDocument<JsonElement> document)
    {
        IJsonDocument doc = document;
        if (!((JsonDocument)document).TryGetRawAccess(out RawDocumentAccess raw))
        {
            return false;
        }

        int index = Elements.Index(document.RootElement);
        if (raw.GetTokenType(index) != JsonTokenType.StartObject)
        {
            return false;
        }

        bool seenId = false;
        bool seenName = false;
        int end = raw.GetEndIndex(index);
        for (int v = index + (2 * RowSize); v - RowSize < end; v = raw.GetNextIndex(v) + RowSize)
        {
            bool ok;
            if (raw.PropertyNameIsEscaped(v))
            {
                using UnescapedUtf8JsonString name = doc.GetPropertyNameUnescaped(v);
                ok = ObjectProperty(in raw, doc, v, name.Span, ref seenId, ref seenName);
            }
            else
            {
                ok = ObjectProperty(in raw, doc, v, raw.GetPropertyNameRaw(v), ref seenId, ref seenName);
            }

            if (!ok)
            {
                return false;
            }
        }

        return seenId && seenName;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ObjectProperty(in RawDocumentAccess raw, IJsonDocument doc, int v, ReadOnlySpan<byte> name, ref bool seenId, ref bool seenName)
    {
        switch (name.Length)
        {
            case 2:
                if (name.SequenceEqual("id"u8))
                {
                    seenId = true;
                    return raw.GetTokenType(v) == JsonTokenType.Number && IsInteger(raw.GetRawValue(v)) && AtLeastZero(raw.GetRawValue(v));
                }

                break;
            case 4:
                if (name.SequenceEqual("name"u8))
                {
                    seenName = true;
                    if (raw.GetTokenType(v) != JsonTokenType.String)
                    {
                        return false;
                    }

                    using UnescapedUtf8JsonString s = StringValue(in raw, doc, v);
                    ReadOnlySpan<byte> value = s.Span;
                    if (value.Length < 1)
                    {
                        return false;
                    }

                    return value.Length <= 100 || JsonElementHelpers.CountRunes(value) <= 100;
                }

                if (name.SequenceEqual("tags"u8))
                {
                    if (raw.GetTokenType(v) != JsonTokenType.StartArray)
                    {
                        return false;
                    }

                    int itemsEnd = raw.GetEndIndex(v);
                    for (int item = v + RowSize; item < itemsEnd; item = raw.GetNextIndex(item))
                    {
                        if (raw.GetTokenType(item) != JsonTokenType.String)
                        {
                            return false;
                        }
                    }

                    return true;
                }

                break;
            case 5:
                if (name.SequenceEqual("email"u8))
                {
                    return raw.GetTokenType(v) == JsonTokenType.String;
                }

                if (name.SequenceEqual("score"u8))
                {
                    return raw.GetTokenType(v) == JsonTokenType.Number && AtMost(raw.GetRawValue(v), 100, Hundred);
                }

                break;
            case 6:
                if (name.SequenceEqual("active"u8))
                {
                    JsonTokenType t = raw.GetTokenType(v);
                    return t is JsonTokenType.True or JsonTokenType.False;
                }

                break;
            case 7:
                if (name.SequenceEqual("address"u8))
                {
                    if (raw.GetTokenType(v) != JsonTokenType.StartObject)
                    {
                        return false;
                    }

                    bool seenCity = false;
                    int addressEnd = raw.GetEndIndex(v);
                    for (int p = v + (2 * RowSize); p - RowSize < addressEnd; p = raw.GetNextIndex(p) + RowSize)
                    {
                        ReadOnlySpan<byte> pn = raw.GetPropertyNameRaw(p);
                        if (!raw.PropertyNameIsEscaped(p) && pn.Length == 4 && pn.SequenceEqual("city"u8))
                        {
                            seenCity = true;
                            if (raw.GetTokenType(p) != JsonTokenType.String)
                            {
                                return false;
                            }
                        }
                        else if (!raw.PropertyNameIsEscaped(p) && pn.Length == 3 && pn.SequenceEqual("zip"u8))
                        {
                            if (raw.GetTokenType(p) != JsonTokenType.String)
                            {
                                return false;
                            }
                        }
                    }

                    return seenCity;
                }

                break;
        }

        // additionalProperties: false
        return false;
    }

    // ---- array.json -----------------------------------------------------------------------------------------

    private static bool ArraySpecialised(ParsedJsonDocument<JsonElement> document)
    {
        IJsonDocument doc = document;
        if (!((JsonDocument)document).TryGetRawAccess(out RawDocumentAccess raw))
        {
            return false;
        }

        int index = Elements.Index(document.RootElement);
        if (raw.GetTokenType(index) != JsonTokenType.StartArray)
        {
            return false;
        }

        int length = raw.GetSizeOrLength(index);
        if (length < 1)
        {
            return false;
        }

        bool pairwise = length <= 8;
        Span<int> buckets = pairwise ? default : stackalloc int[UniqueItemsHashSet.StackAllocBucketSize];
        Span<byte> entries = pairwise ? default : stackalloc byte[UniqueItemsHashSet.StackAllocEntrySize];
        UniqueItemsHashSet set = pairwise ? default : new UniqueItemsHashSet(doc, length, buckets, entries);
        Span<int> seen = pairwise ? stackalloc int[8] : default;
        try
        {
            int end = raw.GetEndIndex(index);
            int i = 0;
            for (int item = index + RowSize; item < end; item = raw.GetNextIndex(item), i++)
            {
                if (raw.GetTokenType(item) != JsonTokenType.Number)
                {
                    return false;
                }

                ReadOnlySpan<byte> value = raw.GetRawValue(item);
                if (!IsInteger(value) || !AtLeastZero(value) || !AtMost(value, 1000, Thousand))
                {
                    return false;
                }

                if (pairwise)
                {
                    for (int j = 0; j < i; j++)
                    {
                        if (raw.GetRawValue(seen[j]).SequenceEqual(value))
                        {
                            return false;
                        }
                    }

                    seen[i] = item;
                }
                else if (!set.AddItemIfNotExists(item))
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            if (!pairwise)
            {
                set.Dispose();
            }
        }
    }

    // ---- unevaluated.json -----------------------------------------------------------------------------------

    private static bool UnevaluatedSpecialised(ParsedJsonDocument<JsonElement> document)
    {
        IJsonDocument doc = document;
        if (!((JsonDocument)document).TryGetRawAccess(out RawDocumentAccess raw))
        {
            return false;
        }

        int index = Elements.Index(document.RootElement);
        if (raw.GetTokenType(index) != JsonTokenType.StartObject)
        {
            // Every branch is object-only; a non-object is valid (no keyword applies) and has no properties.
            return true;
        }

        bool seenActive = false;
        bool seenScore = false;
        int end = raw.GetEndIndex(index);
        for (int v = index + (2 * RowSize); v - RowSize < end; v = raw.GetNextIndex(v) + RowSize)
        {
            if (raw.PropertyNameIsEscaped(v))
            {
                return false; // no escaped names in the schema's property set
            }

            ReadOnlySpan<byte> name = raw.GetPropertyNameRaw(v);
            JsonTokenType t = raw.GetTokenType(v);
            switch (name.Length)
            {
                case 2 when name.SequenceEqual("id"u8):
                    if (t != JsonTokenType.Number || !IsInteger(raw.GetRawValue(v)))
                    {
                        return false;
                    }

                    continue;
                case 4 when name.SequenceEqual("name"u8):
                    if (t != JsonTokenType.String)
                    {
                        return false;
                    }

                    continue;
                case 4 when name.SequenceEqual("tags"u8):
                    if (t != JsonTokenType.StartArray)
                    {
                        return false;
                    }

                    continue;
                case 5 when name.SequenceEqual("email"u8):
                    if (t != JsonTokenType.String)
                    {
                        return false;
                    }

                    continue;
                case 5 when name.SequenceEqual("score"u8):
                    seenScore = true;
                    if (t != JsonTokenType.Number)
                    {
                        return false;
                    }

                    continue;
                case 6 when name.SequenceEqual("active"u8):
                    seenActive = true;
                    if (t is not (JsonTokenType.True or JsonTokenType.False))
                    {
                        return false;
                    }

                    continue;
                case 7 when name.SequenceEqual("address"u8):
                    if (t != JsonTokenType.StartObject)
                    {
                        return false;
                    }

                    continue;
                default:
                    return false; // unevaluatedProperties: false
            }
        }

        // The then-branch (which evaluates active and score) applies only when 'active' is present.
        return !seenScore || seenActive;
    }

    // ---- dynamic.json (strict tree) -------------------------------------------------------------------------

    private static bool DynamicRefSpecialised(ParsedJsonDocument<JsonElement> document)
    {
        IJsonDocument doc = document;
        if (!((JsonDocument)document).TryGetRawAccess(out RawDocumentAccess raw))
        {
            return false;
        }

        return StrictTreeNode(in raw, Elements.Index(document.RootElement));
    }

    private static bool StrictTreeNode(in RawDocumentAccess raw, int index)
    {
        if (raw.GetTokenType(index) != JsonTokenType.StartObject)
        {
            return false;
        }

        int end = raw.GetEndIndex(index);
        for (int v = index + (2 * RowSize); v - RowSize < end; v = raw.GetNextIndex(v) + RowSize)
        {
            if (raw.PropertyNameIsEscaped(v))
            {
                return false;
            }

            ReadOnlySpan<byte> name = raw.GetPropertyNameRaw(v);
            if (name.Length == 4 && name.SequenceEqual("data"u8))
            {
                continue;
            }

            if (name.Length == 8 && name.SequenceEqual("children"u8))
            {
                if (raw.GetTokenType(v) != JsonTokenType.StartArray)
                {
                    return false;
                }

                int itemsEnd = raw.GetEndIndex(v);
                for (int item = v + RowSize; item < itemsEnd; item = raw.GetNextIndex(item))
                {
                    if (!StrictTreeNode(in raw, item))
                    {
                        return false;
                    }
                }

                continue;
            }

            return false; // unevaluatedProperties: false at every level
        }

        return true;
    }

    // ---- oneof.json -----------------------------------------------------------------------------------------

    private static bool OneOfSpecialised(ParsedJsonDocument<JsonElement> document)
    {
        IJsonDocument doc = document;
        if (!((JsonDocument)document).TryGetRawAccess(out RawDocumentAccess raw))
        {
            return false;
        }

        int index = Elements.Index(document.RootElement);
        if (raw.GetTokenType(index) != JsonTokenType.StartObject)
        {
            return false;
        }

        // The branches differ only by the const of 'kind': exactly one matches when kind is one of a..d.
        bool kindMatched = false;
        int end = raw.GetEndIndex(index);
        for (int v = index + (2 * RowSize); v - RowSize < end; v = raw.GetNextIndex(v) + RowSize)
        {
            if (raw.PropertyNameIsEscaped(v))
            {
                continue;
            }

            ReadOnlySpan<byte> name = raw.GetPropertyNameRaw(v);
            if (name.Length == 4 && name.SequenceEqual("kind"u8))
            {
                if (raw.GetTokenType(v) != JsonTokenType.String || raw.IsEscaped(v))
                {
                    return false;
                }

                ReadOnlySpan<byte> kind = raw.GetRawValue(v);
                if (kind.Length != 1 || kind[0] is not ((byte)'a' or (byte)'b' or (byte)'c' or (byte)'d'))
                {
                    return false;
                }

                kindMatched = true;
            }
            else if (name.Length == 2 && name.SequenceEqual("id"u8))
            {
                if (raw.GetTokenType(v) != JsonTokenType.Number || !IsInteger(raw.GetRawValue(v)))
                {
                    return false;
                }
            }
        }

        return kindMatched;
    }

    // ---- shared helpers (the same operations the engine performs) --------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UnescapedUtf8JsonString StringValue(in RawDocumentAccess raw, IJsonDocument doc, int index)
    {
        return raw.IsEscaped(index) ? doc.GetUtf8JsonString(index, JsonTokenType.String) : new UnescapedUtf8JsonString(raw.GetRawValueMemory(index));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInteger(ReadOnlySpan<byte> raw)
    {
        if (raw.IndexOfAny((byte)'.', (byte)'e', (byte)'E') < 0)
        {
            return true;
        }

        JsonElementHelpers.ParseNumber(raw, out _, out _, out _, out int exponent);
        return exponent >= 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AtLeastZero(ReadOnlySpan<byte> raw)
    {
        if (raw.Length <= 18 && Utf8Parser.TryParse(raw, out long value, out int consumed) && consumed == raw.Length)
        {
            return value >= 0;
        }

        JsonElementHelpers.ParseNumber(raw, out bool negative, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out _);
        return !negative || (integral.Length == 0 && fractional.Length == 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AtMost(ReadOnlySpan<byte> raw, long bound, NumberValue boundValue)
    {
        if (raw.Length <= 18 && raw.IndexOfAny((byte)'.', (byte)'e', (byte)'E') < 0 && Utf8Parser.TryParse(raw, out long value, out int consumed) && consumed == raw.Length)
        {
            return value <= bound;
        }

        JsonElementHelpers.ParseNumber(raw, out bool negative, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);
        return boundValue.CompareTo(negative, integral, fractional, exponent) <= 0;
    }
}
