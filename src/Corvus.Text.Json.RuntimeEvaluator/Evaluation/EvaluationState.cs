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
