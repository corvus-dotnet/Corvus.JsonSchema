// <copyright file="EvaluationState.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;

namespace Corvus.Text.Json.RuntimeEvaluator.Evaluation;

/// <summary>
/// Selects between the zero-cost flag evaluation and results collection at JIT time.
/// </summary>
internal interface IEvaluationMode
{
    /// <summary>Gets a value indicating whether results are collected.</summary>
    bool Collecting { get; }
}

/// <summary>Flag-only evaluation: fail fast, no results.</summary>
internal readonly struct FastMode : IEvaluationMode
{
    public bool Collecting => false;
}

/// <summary>Exhaustive evaluation reporting to a results collector.</summary>
internal readonly struct CollectingMode : IEvaluationMode
{
    public bool Collecting => true;
}

/// <summary>
/// How the evaluator reads the instance document. JIT-specialised like <see cref="IEvaluationMode"/>:
/// <see cref="RawAccess"/> reads metadata rows and text directly (parsed documents), <see cref="InterfaceAccess"/>
/// goes through <see cref="IJsonDocument"/> for every other document type.
/// </summary>
internal interface IDocumentAccess
{
    /// <summary>Gets the token type of the element.</summary>
    JsonTokenType TokenType(ref EvaluationState state, IJsonDocument doc, int index);

    /// <summary>Gets the property count of an object or the length of an array.</summary>
    int Count(ref EvaluationState state, IJsonDocument doc, int index, JsonTokenType tokenType);

    /// <summary>Gets the raw text of a simple value (strings without quotes, not unescaped).</summary>
    ReadOnlySpan<byte> RawValue(ref EvaluationState state, IJsonDocument doc, int index);

    /// <summary>Gets the raw text of a simple value as memory.</summary>
    ReadOnlyMemory<byte> RawValueMemory(ref EvaluationState state, IJsonDocument doc, int index);

    /// <summary>Gets a value indicating whether a string value contains escapes.</summary>
    bool IsEscaped(ref EvaluationState state, IJsonDocument doc, int index);

    /// <summary>Gets the raw text of a string value and whether it contains escapes, from one read of its row where the layout allows.</summary>
    ReadOnlySpan<byte> RawValue(ref EvaluationState state, IJsonDocument doc, int index, out bool escaped);

    /// <summary>
    /// Gets the raw (not unescaped) text of the property name for a property value index, and whether it contains
    /// escapes; when it does, the caller must go through <see cref="GetPropertyName"/> instead.
    /// </summary>
    ReadOnlySpan<byte> PropertyNameRaw(ref EvaluationState state, IJsonDocument doc, int valueIndex, out bool escaped);

    /// <summary>Gets the index of a container's end row.</summary>
    int EndIndex(ref EvaluationState state, IJsonDocument doc, int containerIndex);

    /// <summary>Gets the index of the row after an element (its next sibling, or the parent's end row).</summary>
    int NextIndex(ref EvaluationState state, IJsonDocument doc, int index);

    /// <summary>Gets the token type of an element and the index of the row after it, from one read of its header where the layout allows.</summary>
    JsonTokenType TokenTypeAndNext(ref EvaluationState state, IJsonDocument doc, int index, out int nextIndex);

    /// <summary>
    /// Whether the rows up to and including <paramref name="lastIndex"/> exist, so that a loop over a container may use the
    /// unchecked accessors below for every row from the container's index to its end row.
    /// </summary>
    bool RowsAvailable(ref EvaluationState state, IJsonDocument doc, int lastIndex);

    /// <summary><see cref="TokenTypeAndNext"/> without the range check; only after <see cref="RowsAvailable"/> covered the row.</summary>
    JsonTokenType TokenTypeAndNextUnchecked(ref EvaluationState state, IJsonDocument doc, int index, out int nextIndex);

    /// <summary><see cref="PropertyNameRaw"/> with the name row read unchecked; only after <see cref="RowsAvailable"/> covered the row. The slice into the text stays checked.</summary>
    ReadOnlySpan<byte> PropertyNameRawUnchecked(ref EvaluationState state, IJsonDocument doc, int valueIndex, out bool escaped);

    /// <summary><see cref="NextIndex"/> without the range check; only after <see cref="RowsAvailable"/> covered the row.</summary>
    int NextIndexUnchecked(ref EvaluationState state, IJsonDocument doc, int index);

    /// <summary>Gets the unescaped text of a string value.</summary>
    UnescapedUtf8JsonString GetString(ref EvaluationState state, IJsonDocument doc, int index);

    /// <summary>Gets the unescaped property name for a property value index.</summary>
    UnescapedUtf8JsonString GetPropertyName(ref EvaluationState state, IJsonDocument doc, int valueIndex);
}

/// <summary>
/// Direct row access for parsed documents: the metadata rows and the UTF-8 text are read as spans held in the
/// <see cref="EvaluationState"/>, one bounds check per field, no <see cref="ReadOnlyMemory{T}"/> round trips.
/// </summary>
internal readonly struct RawAccess : IDocumentAccess
{
    private const int RowSize = Evaluator.RowSize;
    private const int SizeOrLengthOffset = 4;
    private const int NumberOfRowsOffset = 8;
    private const int LocationMask = 0x0FFFFFFF;
    private const uint NumberOfRowsMask = 0x0FFFFFFFU;

    public JsonTokenType TokenType(ref EvaluationState state, IJsonDocument doc, int index) => (JsonTokenType)(ReadUInt32(state.RawRows, index + NumberOfRowsOffset) >> 28);

    public int Count(ref EvaluationState state, IJsonDocument doc, int index, JsonTokenType tokenType) => ReadInt32(state.RawRows, index + SizeOrLengthOffset) & int.MaxValue;

    public ReadOnlySpan<byte> RawValue(ref EvaluationState state, IJsonDocument doc, int index)
    {
        ReadOnlySpan<byte> rows = state.RawRows;
        int location = ReadInt32(rows, index) & LocationMask;
        int length = ReadInt32(rows, index + SizeOrLengthOffset) & int.MaxValue;
        return state.RawUtf8.Slice(location, length);
    }

    public ReadOnlyMemory<byte> RawValueMemory(ref EvaluationState state, IJsonDocument doc, int index)
    {
        ReadOnlySpan<byte> rows = state.RawRows;
        int location = ReadInt32(rows, index) & LocationMask;
        int length = ReadInt32(rows, index + SizeOrLengthOffset) & int.MaxValue;
        return state.RawUtf8Memory.Slice(location, length);
    }

    public bool IsEscaped(ref EvaluationState state, IJsonDocument doc, int index) => ReadInt32(state.RawRows, index + SizeOrLengthOffset) < 0;

    public ReadOnlySpan<byte> RawValue(ref EvaluationState state, IJsonDocument doc, int index, out bool escaped)
    {
        ulong pair = MemoryMarshal.Read<ulong>(state.RawRows.Slice(index, sizeof(ulong)));
        int location;
        int length;
        if (BitConverter.IsLittleEndian)
        {
            location = (int)pair & LocationMask;
            length = (int)(pair >> 32);
        }
        else
        {
            location = (int)(pair >> 32) & LocationMask;
            length = (int)pair;
        }

        escaped = length < 0;
        return state.RawUtf8.Slice(location, length & int.MaxValue);
    }

    public ReadOnlySpan<byte> PropertyNameRaw(ref EvaluationState state, IJsonDocument doc, int valueIndex, out bool escaped)
    {
        // Location and length are the row's first two ints: one read, one bounds check.
        ReadOnlySpan<byte> rows = state.RawRows;
        int nameIndex = valueIndex - RowSize;
        ulong pair = MemoryMarshal.Read<ulong>(rows.Slice(nameIndex, sizeof(ulong)));
        int location;
        int length;
        if (BitConverter.IsLittleEndian)
        {
            location = (int)pair & LocationMask;
            length = (int)(pair >> 32);
        }
        else
        {
            location = (int)(pair >> 32) & LocationMask;
            length = (int)pair;
        }

        escaped = length < 0;
        return state.RawUtf8.Slice(location, length & int.MaxValue);
    }

    public JsonTokenType TokenTypeAndNext(ref EvaluationState state, IJsonDocument doc, int index, out int nextIndex)
    {
        uint union = ReadUInt32(state.RawRows, index + NumberOfRowsOffset);
        uint tokenType = union >> 28;
        nextIndex = tokenType >= (uint)JsonTokenType.PropertyName
            ? index + RowSize
            : index + (RowSize * (int)(union & NumberOfRowsMask)) + RowSize;
        return (JsonTokenType)tokenType;
    }

    public bool RowsAvailable(ref EvaluationState state, IJsonDocument doc, int lastIndex) => lastIndex >= 0 && (long)lastIndex + RowSize <= state.RawRows.Length;

    public int NextIndexUnchecked(ref EvaluationState state, IJsonDocument doc, int index)
    {
        uint union = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref MemoryMarshal.GetReference(state.RawRows), index + NumberOfRowsOffset));
        return (union >> 28) >= (uint)JsonTokenType.PropertyName
            ? index + RowSize
            : index + (RowSize * (int)(union & NumberOfRowsMask)) + RowSize;
    }

    public JsonTokenType TokenTypeAndNextUnchecked(ref EvaluationState state, IJsonDocument doc, int index, out int nextIndex)
    {
        uint union = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref MemoryMarshal.GetReference(state.RawRows), index + NumberOfRowsOffset));
        uint tokenType = union >> 28;
        nextIndex = tokenType >= (uint)JsonTokenType.PropertyName
            ? index + RowSize
            : index + (RowSize * (int)(union & NumberOfRowsMask)) + RowSize;
        return (JsonTokenType)tokenType;
    }

    public ReadOnlySpan<byte> PropertyNameRawUnchecked(ref EvaluationState state, IJsonDocument doc, int valueIndex, out bool escaped)
    {
        ulong pair = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref MemoryMarshal.GetReference(state.RawRows), valueIndex - RowSize));
        int location;
        int length;
        if (BitConverter.IsLittleEndian)
        {
            location = (int)pair & LocationMask;
            length = (int)(pair >> 32);
        }
        else
        {
            location = (int)(pair >> 32) & LocationMask;
            length = (int)pair;
        }

        escaped = length < 0;
        return state.RawUtf8.Slice(location, length & int.MaxValue);
    }

    public int EndIndex(ref EvaluationState state, IJsonDocument doc, int containerIndex) => containerIndex + (RowSize * (int)(ReadUInt32(state.RawRows, containerIndex + NumberOfRowsOffset) & NumberOfRowsMask));

    public int NextIndex(ref EvaluationState state, IJsonDocument doc, int index)
    {
        uint union = ReadUInt32(state.RawRows, index + NumberOfRowsOffset);
        return (union >> 28) >= (uint)JsonTokenType.PropertyName
            ? index + RowSize
            : index + (RowSize * (int)(union & NumberOfRowsMask)) + RowSize;
    }

    public UnescapedUtf8JsonString GetString(ref EvaluationState state, IJsonDocument doc, int index)
    {
        // Escaped strings are rare: unescape through the document; otherwise wrap the raw text without renting.
        return this.IsEscaped(ref state, doc, index)
            ? doc.GetUtf8JsonString(index, JsonTokenType.String)
            : new UnescapedUtf8JsonString(this.RawValueMemory(ref state, doc, index));
    }

    public UnescapedUtf8JsonString GetPropertyName(ref EvaluationState state, IJsonDocument doc, int valueIndex)
    {
        return this.IsEscaped(ref state, doc, valueIndex - RowSize)
            ? doc.GetPropertyNameUnescaped(valueIndex)
            : new UnescapedUtf8JsonString(this.RawValueMemory(ref state, doc, valueIndex - RowSize));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadInt32(ReadOnlySpan<byte> rows, int offset) => MemoryMarshal.Read<int>(rows.Slice(offset, sizeof(int)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadUInt32(ReadOnlySpan<byte> rows, int offset) => MemoryMarshal.Read<uint>(rows.Slice(offset, sizeof(uint)));
}

/// <summary>Access through the <see cref="IJsonDocument"/> interface.</summary>
internal readonly struct InterfaceAccess : IDocumentAccess
{
    public JsonTokenType TokenType(ref EvaluationState state, IJsonDocument doc, int index) => doc.GetJsonTokenType(index);

    public int Count(ref EvaluationState state, IJsonDocument doc, int index, JsonTokenType tokenType)
    {
        return tokenType == JsonTokenType.StartObject ? doc.GetPropertyCount(index) : doc.GetArrayLength(index);
    }

    // The explicit overload: a fixed-string document's one-argument form keeps the quotes.
    public ReadOnlySpan<byte> RawValue(ref EvaluationState state, IJsonDocument doc, int index) => doc.GetRawSimpleValue(index, includeQuotes: false).Span;

    public ReadOnlyMemory<byte> RawValueMemory(ref EvaluationState state, IJsonDocument doc, int index) => doc.GetRawSimpleValue(index, includeQuotes: false);

    public bool IsEscaped(ref EvaluationState state, IJsonDocument doc, int index) => doc.ValueIsEscaped(index, isPropertyName: false);

    public ReadOnlySpan<byte> RawValue(ref EvaluationState state, IJsonDocument doc, int index, out bool escaped)
    {
        escaped = doc.ValueIsEscaped(index, isPropertyName: false);
        return doc.GetRawSimpleValue(index, includeQuotes: false).Span;
    }

    public ReadOnlySpan<byte> PropertyNameRaw(ref EvaluationState state, IJsonDocument doc, int valueIndex, out bool escaped)
    {
        escaped = doc.ValueIsEscaped(valueIndex, isPropertyName: true);
        return doc.GetPropertyNameRaw(valueIndex, includeQuotes: false).Span;
    }

    public int EndIndex(ref EvaluationState state, IJsonDocument doc, int containerIndex) => containerIndex + doc.GetDbSize(containerIndex, includeEndElement: false);

    public int NextIndex(ref EvaluationState state, IJsonDocument doc, int index) => index + doc.GetDbSize(index, includeEndElement: true);

    public JsonTokenType TokenTypeAndNext(ref EvaluationState state, IJsonDocument doc, int index, out int nextIndex)
    {
        nextIndex = index + doc.GetDbSize(index, includeEndElement: true);
        return doc.GetJsonTokenType(index);
    }

    public bool RowsAvailable(ref EvaluationState state, IJsonDocument doc, int lastIndex) => true;

    public JsonTokenType TokenTypeAndNextUnchecked(ref EvaluationState state, IJsonDocument doc, int index, out int nextIndex) => this.TokenTypeAndNext(ref state, doc, index, out nextIndex);

    public ReadOnlySpan<byte> PropertyNameRawUnchecked(ref EvaluationState state, IJsonDocument doc, int valueIndex, out bool escaped) => this.PropertyNameRaw(ref state, doc, valueIndex, out escaped);

    public int NextIndexUnchecked(ref EvaluationState state, IJsonDocument doc, int index) => this.NextIndex(ref state, doc, index);

    public UnescapedUtf8JsonString GetString(ref EvaluationState state, IJsonDocument doc, int index) => doc.GetUtf8JsonString(index, JsonTokenType.String);

    public UnescapedUtf8JsonString GetPropertyName(ref EvaluationState state, IJsonDocument doc, int valueIndex) => doc.GetPropertyNameUnescaped(valueIndex);
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

    /// <summary>The resource evaluation started in: the outermost dynamic scope on every path.</summary>
    public int EntryResource;

    /// <summary>The UTF-8 text of the instance document as memory, when <see cref="RawAccess"/> is in use: for the values handed on as memory (unescaped strings).</summary>
    public ReadOnlyMemory<byte> RawUtf8Memory;

    /// <summary>The metadata rows of the instance document, when <see cref="RawAccess"/> is in use.</summary>
    public ReadOnlySpan<byte> RawRows;

    /// <summary>The UTF-8 text the rows index into, when <see cref="RawAccess"/> is in use.</summary>
    public ReadOnlySpan<byte> RawUtf8;

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

    public static readonly JsonSchemaMessageProvider<byte[]> RequiredPresent = static (byte[] name, Span<byte> buffer, out int written) =>
        JsonSchemaEvaluation.RequiredPropertyPresent(name, buffer, out written);

    public static readonly JsonSchemaMessageProvider<byte[]> RequiredNotPresent = static (byte[] name, Span<byte> buffer, out int written) =>
        JsonSchemaEvaluation.RequiredPropertyNotPresent(name, buffer, out written);

    /// <summary>Gets the expected-value message provider for a format.</summary>
    public static JsonSchemaMessageProvider? ExpectedFor(FormatKind format)
    {
        return format switch
        {
            FormatKind.Date => JsonSchemaEvaluation.ExpectedDate,
            FormatKind.DateTime => JsonSchemaEvaluation.ExpectedDateTime,
            FormatKind.Time => JsonSchemaEvaluation.ExpectedTime,
            FormatKind.Duration => JsonSchemaEvaluation.ExpectedDuration,
            FormatKind.Email => JsonSchemaEvaluation.ExpectedEmail,
            FormatKind.IdnEmail => JsonSchemaEvaluation.ExpectedIdnEmail,
            FormatKind.Hostname => JsonSchemaEvaluation.ExpectedHostname,
            FormatKind.IdnHostname => JsonSchemaEvaluation.ExpectedIdnHostname,
            FormatKind.Ipv4 => JsonSchemaEvaluation.ExpectedIPV4,
            FormatKind.Ipv6 => JsonSchemaEvaluation.ExpectedIPV6,
            FormatKind.Uri => JsonSchemaEvaluation.ExpectedUri,
            FormatKind.UriReference => JsonSchemaEvaluation.ExpectedUriReference,
            FormatKind.Iri => JsonSchemaEvaluation.ExpectedIri,
            FormatKind.IriReference => JsonSchemaEvaluation.ExpectedIriReference,
            FormatKind.Uuid => JsonSchemaEvaluation.ExpectedUuid,
            FormatKind.UriTemplate => JsonSchemaEvaluation.ExpectedUriTemplate,
            FormatKind.JsonPointer => JsonSchemaEvaluation.ExpectedJsonPointer,
            FormatKind.RelativeJsonPointer => JsonSchemaEvaluation.ExpectedRelativeJsonPointer,
            FormatKind.Regex => JsonSchemaEvaluation.ExpectedRegex,
            FormatKind.Byte => JsonSchemaEvaluation.ExpectedByte,
            FormatKind.UInt16 => JsonSchemaEvaluation.ExpectedUInt16,
            FormatKind.UInt32 => JsonSchemaEvaluation.ExpectedUInt32,
            FormatKind.UInt64 => JsonSchemaEvaluation.ExpectedUInt64,
            FormatKind.UInt128 => JsonSchemaEvaluation.ExpectedUInt128,
            FormatKind.SByte => JsonSchemaEvaluation.ExpectedSByte,
            FormatKind.Int16 => JsonSchemaEvaluation.ExpectedInt16,
            FormatKind.Int32 => JsonSchemaEvaluation.ExpectedInt32,
            FormatKind.Int64 => JsonSchemaEvaluation.ExpectedInt64,
            FormatKind.Int128 => JsonSchemaEvaluation.ExpectedInt128,
            FormatKind.Half => JsonSchemaEvaluation.ExpectedHalf,
            FormatKind.Single => JsonSchemaEvaluation.ExpectedSingle,
            FormatKind.Double => JsonSchemaEvaluation.ExpectedDouble,
            FormatKind.Decimal => JsonSchemaEvaluation.ExpectedDecimal,
            _ => null,
        };
    }

    /// <summary>Gets the warning message provider for a string format that did not conform.</summary>
    public static JsonSchemaMessageProvider? WarningFor(FormatKind format)
    {
        return format switch
        {
            FormatKind.Date => JsonSchemaEvaluation.WarningDate,
            FormatKind.DateTime => JsonSchemaEvaluation.WarningDateTime,
            FormatKind.Time => JsonSchemaEvaluation.WarningTime,
            FormatKind.Duration => JsonSchemaEvaluation.WarningDuration,
            FormatKind.Email => JsonSchemaEvaluation.WarningEmail,
            FormatKind.IdnEmail => JsonSchemaEvaluation.WarningIdnEmail,
            FormatKind.Hostname => JsonSchemaEvaluation.WarningHostname,
            FormatKind.IdnHostname => JsonSchemaEvaluation.WarningIdnHostname,
            FormatKind.Ipv4 => JsonSchemaEvaluation.WarningIPV4,
            FormatKind.Ipv6 => JsonSchemaEvaluation.WarningIPV6,
            FormatKind.Uri => JsonSchemaEvaluation.WarningUri,
            FormatKind.UriReference => JsonSchemaEvaluation.WarningUriReference,
            FormatKind.Iri => JsonSchemaEvaluation.WarningIri,
            FormatKind.IriReference => JsonSchemaEvaluation.WarningIriReference,
            FormatKind.Uuid => JsonSchemaEvaluation.WarningUuid,
            FormatKind.UriTemplate => JsonSchemaEvaluation.WarningUriTemplate,
            FormatKind.JsonPointer => JsonSchemaEvaluation.WarningJsonPointer,
            FormatKind.RelativeJsonPointer => JsonSchemaEvaluation.WarningRelativeJsonPointer,
            FormatKind.Regex => JsonSchemaEvaluation.WarningRegex,
            _ => null,
        };
    }

    public static readonly JsonSchemaMessageProvider<string> Text = static (string text, Span<byte> buffer, out int written) =>
    {
#if NET8_0_OR_GREATER
        return System.Text.Encoding.UTF8.TryGetBytes(text, buffer, out written);
#else
        int required = System.Text.Encoding.UTF8.GetByteCount(text);
        if (required > buffer.Length)
        {
            written = 0;
            return false;
        }

        written = System.Text.Encoding.UTF8.GetBytes(text.AsSpan(), buffer);
        return true;
#endif
    };
}