// <copyright file="JsonPathSequenceBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// An ArrayPool-backed growable collection of <see cref="JsonElement"/> values
/// used to build intermediate node-list results.
/// </summary>
/// <remarks>
/// <para>
/// This is a <c>ref struct</c> to prevent accidental copies that could cause
/// double-return bugs with the pooled backing array.
/// </para>
/// </remarks>
internal ref struct JsonPathSequenceBuilder
{
    private JsonElement[]? array;
    private int count;

    /// <summary>Gets the current count of values added.</summary>
    public readonly int Count => this.count;

    /// <summary>Gets the element at the specified index.</summary>
    public readonly JsonElement this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.array![index];
    }

    /// <summary>
    /// Adds a value to the builder.
    /// </summary>
    /// <param name="value">The value to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(in JsonElement value)
    {
        this.EnsureCapacity();
        this.array![this.count++] = value;
    }

    /// <summary>
    /// Materializes the collected elements into a single <see cref="JsonElement"/>
    /// array using the CVB (ComplexValueBuilder) pattern.
    /// </summary>
    /// <param name="workspace">The workspace for the mutable document.</param>
    /// <returns>A <see cref="JsonElement"/> array containing all collected elements.</returns>
    public readonly JsonElement ToElement(JsonWorkspace workspace)
    {
        if (this.count == 0)
        {
            return EmptyArrayElement;
        }

        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            (this.array!, this.count),
            static (in (JsonElement[] Arr, int Count) ctx, ref JsonElement.ArrayBuilder builder) =>
            {
                for (int i = 0; i < ctx.Count; i++)
                {
                    builder.AddItem(ctx.Arr[i]);
                }
            },
            estimatedMemberCount: this.count + 2);

        return (JsonElement)doc.RootElement;
    }

    /// <summary>
    /// Returns the rented backing array to the pool.
    /// </summary>
    public void ReturnArray()
    {
        if (this.array is not null)
        {
            this.array.AsSpan(0, this.count).Clear();
            ArrayPool<JsonElement>.Shared.Return(this.array);
            this.array = null;
        }

        this.count = 0;
    }

    /// <summary>
    /// Gets a pre-parsed empty JSON array element.
    /// </summary>
    internal static readonly JsonElement EmptyArrayElement = JsonElement.ParseValue("[]"u8);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity()
    {
        if (this.array is null)
        {
            this.array = ArrayPool<JsonElement>.Shared.Rent(8);
        }
        else if (this.count == this.array.Length)
        {
            this.Grow();
        }
    }

    private void Grow()
    {
        int newCapacity = this.array!.Length * 2;
        JsonElement[] newArray = ArrayPool<JsonElement>.Shared.Rent(newCapacity);
        Array.Copy(this.array, newArray, this.count);

        this.array.AsSpan(0, this.count).Clear();
        ArrayPool<JsonElement>.Shared.Return(this.array);
        this.array = newArray;
    }
}
