// <copyright file="EvaluationState.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;

namespace Corvus.Text.Json.RuntimeEvaluator.Evaluation;

/// <summary>
/// Selects between the zero-cost flag evaluation and results collection at JIT time.
/// </summary>
internal interface IEvaluationMode
{
    /// <summary>Gets a value indicating whether results are collected.</summary>
    static abstract bool Collecting { get; }
}

/// <summary>Flag-only evaluation: fail fast, no results.</summary>
internal readonly struct FastMode : IEvaluationMode
{
    public static bool Collecting => false;
}

/// <summary>Exhaustive evaluation reporting to a results collector.</summary>
internal readonly struct CollectingMode : IEvaluationMode
{
    public static bool Collecting => true;
}

/// <summary>
/// How the evaluator reads the instance document. JIT-specialised like <see cref="IEvaluationMode"/>:
/// <see cref="RawAccess"/> reads metadata rows and text directly (parsed documents), <see cref="InterfaceAccess"/>
/// goes through <see cref="IJsonDocument"/> for every other document type.
/// </summary>
internal interface IDocumentAccess
{
    /// <summary>Gets the token type of the element.</summary>
    static abstract JsonTokenType TokenType(ref EvaluationState state, IJsonDocument doc, int index);

    /// <summary>Gets the property count of an object or the length of an array.</summary>
    static abstract int Count(ref EvaluationState state, IJsonDocument doc, int index, JsonTokenType tokenType);

    /// <summary>Gets the raw text of a simple value (strings without quotes, not unescaped).</summary>
    static abstract ReadOnlySpan<byte> RawValue(ref EvaluationState state, IJsonDocument doc, int index);

    /// <summary>Gets the raw text of a simple value as memory.</summary>
    static abstract ReadOnlyMemory<byte> RawValueMemory(ref EvaluationState state, IJsonDocument doc, int index);

    /// <summary>Gets a value indicating whether a string value contains escapes.</summary>
    static abstract bool IsEscaped(ref EvaluationState state, IJsonDocument doc, int index);

    /// <summary>Gets the raw property name for a property value index.</summary>
    static abstract ReadOnlyMemory<byte> PropertyNameRawMemory(ref EvaluationState state, IJsonDocument doc, int valueIndex);

    /// <summary>Gets a value indicating whether the property name for a value index contains escapes.</summary>
    static abstract bool PropertyNameIsEscaped(ref EvaluationState state, IJsonDocument doc, int valueIndex);

    /// <summary>Gets the index of a container's end row.</summary>
    static abstract int EndIndex(ref EvaluationState state, IJsonDocument doc, int containerIndex);

    /// <summary>Gets the index of the row after an element (its next sibling, or the parent's end row).</summary>
    static abstract int NextIndex(ref EvaluationState state, IJsonDocument doc, int index);

    /// <summary>Gets the unescaped text of a string value.</summary>
    static abstract UnescapedUtf8JsonString GetString(ref EvaluationState state, IJsonDocument doc, int index);

    /// <summary>Gets the unescaped property name for a property value index.</summary>
    static abstract UnescapedUtf8JsonString GetPropertyName(ref EvaluationState state, IJsonDocument doc, int valueIndex);
}

/// <summary>Direct row access for parsed documents.</summary>
internal readonly struct RawAccess : IDocumentAccess
{
    public static JsonTokenType TokenType(ref EvaluationState state, IJsonDocument doc, int index) => state.Raw.GetTokenType(index);

    public static int Count(ref EvaluationState state, IJsonDocument doc, int index, JsonTokenType tokenType) => state.Raw.GetSizeOrLength(index);

    public static ReadOnlySpan<byte> RawValue(ref EvaluationState state, IJsonDocument doc, int index) => state.Raw.GetRawValue(index);

    public static ReadOnlyMemory<byte> RawValueMemory(ref EvaluationState state, IJsonDocument doc, int index) => state.Raw.GetRawValueMemory(index);

    public static bool IsEscaped(ref EvaluationState state, IJsonDocument doc, int index) => state.Raw.IsEscaped(index);

    public static ReadOnlyMemory<byte> PropertyNameRawMemory(ref EvaluationState state, IJsonDocument doc, int valueIndex) => state.Raw.GetRawValueMemory(valueIndex - Evaluator.RowSize);

    public static bool PropertyNameIsEscaped(ref EvaluationState state, IJsonDocument doc, int valueIndex) => state.Raw.PropertyNameIsEscaped(valueIndex);

    public static int EndIndex(ref EvaluationState state, IJsonDocument doc, int containerIndex) => state.Raw.GetEndIndex(containerIndex);

    public static int NextIndex(ref EvaluationState state, IJsonDocument doc, int index) => state.Raw.GetNextIndex(index);

    public static UnescapedUtf8JsonString GetString(ref EvaluationState state, IJsonDocument doc, int index)
    {
        // Escaped strings are rare: unescape through the document; otherwise wrap the raw text without renting.
        return state.Raw.IsEscaped(index)
            ? doc.GetUtf8JsonString(index, JsonTokenType.String)
            : new UnescapedUtf8JsonString(state.Raw.GetRawValueMemory(index));
    }

    public static UnescapedUtf8JsonString GetPropertyName(ref EvaluationState state, IJsonDocument doc, int valueIndex)
    {
        return state.Raw.PropertyNameIsEscaped(valueIndex)
            ? doc.GetPropertyNameUnescaped(valueIndex)
            : new UnescapedUtf8JsonString(state.Raw.GetRawValueMemory(valueIndex - Evaluator.RowSize));
    }
}

/// <summary>Access through the <see cref="IJsonDocument"/> interface.</summary>
internal readonly struct InterfaceAccess : IDocumentAccess
{
    public static JsonTokenType TokenType(ref EvaluationState state, IJsonDocument doc, int index) => doc.GetJsonTokenType(index);

    public static int Count(ref EvaluationState state, IJsonDocument doc, int index, JsonTokenType tokenType)
    {
        return tokenType == JsonTokenType.StartObject ? doc.GetPropertyCount(index) : doc.GetArrayLength(index);
    }

    public static ReadOnlySpan<byte> RawValue(ref EvaluationState state, IJsonDocument doc, int index) => doc.GetRawSimpleValue(index).Span;

    public static ReadOnlyMemory<byte> RawValueMemory(ref EvaluationState state, IJsonDocument doc, int index) => doc.GetRawSimpleValue(index);

    public static bool IsEscaped(ref EvaluationState state, IJsonDocument doc, int index) => doc.ValueIsEscaped(index, isPropertyName: false);

    public static ReadOnlyMemory<byte> PropertyNameRawMemory(ref EvaluationState state, IJsonDocument doc, int valueIndex) => doc.GetPropertyNameRaw(valueIndex, includeQuotes: false);

    public static bool PropertyNameIsEscaped(ref EvaluationState state, IJsonDocument doc, int valueIndex) => doc.ValueIsEscaped(valueIndex, isPropertyName: true);

    public static int EndIndex(ref EvaluationState state, IJsonDocument doc, int containerIndex) => containerIndex + doc.GetDbSize(containerIndex, includeEndElement: false);

    public static int NextIndex(ref EvaluationState state, IJsonDocument doc, int index) => index + doc.GetDbSize(index, includeEndElement: true);

    public static UnescapedUtf8JsonString GetString(ref EvaluationState state, IJsonDocument doc, int index) => doc.GetUtf8JsonString(index, JsonTokenType.String);

    public static UnescapedUtf8JsonString GetPropertyName(ref EvaluationState state, IJsonDocument doc, int valueIndex) => doc.GetPropertyNameUnescaped(valueIndex);
}

/// <summary>
/// Per-evaluation mutable state, allocated on the stack.
/// </summary>
internal ref struct EvaluationState
{
    public CompiledSchema Program;
    public SchemaNode[] Nodes;
    public IJsonSchemaResultsCollector? Collector;
    public Span<int> Scope;
    public int ScopeDepth;
    public int[]? RentedScope;
    public int Depth;
    public int MaxDepth;
    public bool UsesDynamicScope;

    /// <summary>Direct row access to the instance document, when <see cref="RawAccess"/> is in use.</summary>
    public RawDocumentAccess Raw;

    /// <summary>A throwaway context so that the shared format helpers can be reused.</summary>
    public JsonSchemaContext Scratch;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PushScope(int resourceId)
    {
        if (this.ScopeDepth == this.Scope.Length)
        {
            this.GrowScope();
        }

        this.Scope[this.ScopeDepth++] = resourceId;
    }

    public void Dispose()
    {
        if (this.RentedScope is not null)
        {
            ArrayPool<int>.Shared.Return(this.RentedScope);
            this.RentedScope = null;
        }
    }

    private void GrowScope()
    {
        int[] bigger = ArrayPool<int>.Shared.Rent(this.Scope.Length * 2);
        this.Scope.CopyTo(bigger);
        if (this.RentedScope is not null)
        {
            ArrayPool<int>.Shared.Return(this.RentedScope);
        }

        this.RentedScope = bigger;
        this.Scope = bigger;
    }
}

/// <summary>
/// Context handed to the results collector's path providers so that no closures are needed.
/// </summary>
internal readonly struct EdgeContext
{
    public EdgeContext(byte[]? evalSegment, byte[] schemaLocation, IJsonDocument? document, int propertyValueIndex, int itemIndex)
    {
        this.EvalSegment = evalSegment;
        this.SchemaLocation = schemaLocation;
        this.Document = document;
        this.PropertyValueIndex = propertyValueIndex;
        this.ItemIndex = itemIndex;
    }

    public byte[]? EvalSegment { get; }

    public byte[] SchemaLocation { get; }

    public IJsonDocument? Document { get; }

    public int PropertyValueIndex { get; }

    public int ItemIndex { get; }
}

/// <summary>
/// Static path/message providers used with the collector's generic overloads.
/// </summary>
internal static class Providers
{
    public static readonly JsonSchemaPathProvider<EdgeContext> EvalPath = static (EdgeContext ctx, Span<byte> buffer, out int written) =>
    {
        byte[]? segment = ctx.EvalSegment;
        if (segment is null)
        {
            written = 0;
            return true;
        }

        if (segment.Length > buffer.Length)
        {
            written = 0;
            return false;
        }

        segment.CopyTo(buffer);
        written = segment.Length;
        return true;
    };

    public static readonly JsonSchemaPathProvider<EdgeContext> SchemaPath = static (EdgeContext ctx, Span<byte> buffer, out int written) =>
    {
        byte[] location = ctx.SchemaLocation;
        if (location.Length > buffer.Length)
        {
            written = 0;
            return false;
        }

        location.CopyTo(buffer);
        written = location.Length;
        return true;
    };

    public static readonly JsonSchemaPathProvider<EdgeContext> DocumentPath = static (EdgeContext ctx, Span<byte> buffer, out int written) =>
    {
        if (ctx.ItemIndex >= 0)
        {
            return System.Buffers.Text.Utf8Formatter.TryFormat(ctx.ItemIndex, buffer, out written);
        }

        using UnescapedUtf8JsonString name = ctx.Document!.GetPropertyNameUnescaped(ctx.PropertyValueIndex);
        return Utf8JsonPointer.TryEncodeSegment(name.Span, buffer, out written);
    };

    public static readonly JsonSchemaMessageProvider<byte[]> RawJson = static (byte[] raw, Span<byte> buffer, out int written) =>
    {
        if (raw.Length > buffer.Length)
        {
            written = 0;
            return false;
        }

        raw.CopyTo(buffer);
        written = raw.Length;
        return true;
    };

    public static readonly JsonSchemaMessageProvider<string> Text = static (string text, Span<byte> buffer, out int written) =>
    {
        return System.Text.Encoding.UTF8.TryGetBytes(text, buffer, out written);
    };
}
