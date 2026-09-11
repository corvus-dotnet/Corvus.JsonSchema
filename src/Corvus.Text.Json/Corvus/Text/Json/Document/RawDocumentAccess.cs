// <copyright file="RawDocumentAccess.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Direct, non-virtual access to the metadata rows and UTF-8 text of a document whose rows are all local
/// (a <see cref="ParsedJsonDocument{T}"/>). Obtained once per operation from
/// <see cref="JsonDocument.TryGetRawAccess(out RawDocumentAccess)"/>; every accessor here is a couple of loads
/// with no disposal check, so callers must not outlive the document.
/// </summary>
/// <remarks>
/// Indices are the same row indices used throughout <see cref="IJsonDocument"/>. A simple value occupies one
/// row; an object or array occupies <see cref="GetNumberOfRows(int)"/> rows followed by its end row.
/// </remarks>
[CLSCompliant(false)]
public readonly struct RawDocumentAccess
{
    private const int RowSize = 12;
    private const int SizeOrLengthOffset = 4;
    private const int NumberOfRowsOffset = 8;

    private readonly byte[] rows;
    private readonly ReadOnlyMemory<byte> utf8;

    internal RawDocumentAccess(byte[] rows, ReadOnlyMemory<byte> utf8)
    {
        this.rows = rows;
        this.utf8 = utf8;
    }

    /// <summary>
    /// Gets the metadata rows: <see cref="RowSize"/> bytes per row, the layout of <see cref="DbRow"/>.
    /// </summary>
    internal byte[] Rows => this.rows;

    /// <summary>
    /// Gets the UTF-8 text the rows index into.
    /// </summary>
    internal ReadOnlyMemory<byte> Utf8 => this.utf8;

    /// <summary>
    /// Gets the token type of the element at the index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsonTokenType GetTokenType(int index)
    {
        return (JsonTokenType)(this.ReadUInt32(index + NumberOfRowsOffset) >> 28);
    }

    /// <summary>
    /// Gets the byte length of a simple value, or the number of properties/items of a container.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetSizeOrLength(int index)
    {
        return this.ReadInt32(index + SizeOrLengthOffset) & int.MaxValue;
    }

    /// <summary>
    /// Gets a value indicating whether a string or property name contains escape sequences.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEscaped(int index)
    {
        return this.ReadInt32(index + SizeOrLengthOffset) < 0;
    }

    /// <summary>
    /// Gets the number of rows occupied by a container, excluding its end row.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetNumberOfRows(int index)
    {
        return (int)(this.ReadUInt32(index + NumberOfRowsOffset) & 0x0FFFFFFFU);
    }

    /// <summary>
    /// Gets the index of the row following the element (its next sibling, or the parent's end row).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetNextIndex(int index)
    {
        uint union = this.ReadUInt32(index + NumberOfRowsOffset);
        JsonTokenType tokenType = (JsonTokenType)(union >> 28);
        if (tokenType >= JsonTokenType.PropertyName)
        {
            return index + RowSize;
        }

        return index + (RowSize * (int)(union & 0x0FFFFFFFU)) + RowSize;
    }

    /// <summary>
    /// Gets the index of a container's end row.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetEndIndex(int containerIndex)
    {
        return containerIndex + (RowSize * this.GetNumberOfRows(containerIndex));
    }

    /// <summary>
    /// Gets the index of the first property value (for objects) or item (for arrays) of a container.
    /// </summary>
    /// <remarks>For objects the row before each value is its property name row.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetFirstItemIndex(int containerIndex)
    {
        return containerIndex + RowSize;
    }

    /// <summary>
    /// Gets the raw UTF-8 text of a simple value (for strings and property names, without quotes and
    /// without unescaping; check <see cref="IsEscaped(int)"/>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetRawValue(int index)
    {
        int location = this.ReadInt32(index) & 0x0FFFFFFF;
        int length = this.ReadInt32(index + SizeOrLengthOffset) & int.MaxValue;
        return this.utf8.Span.Slice(location, length);
    }

    /// <summary>
    /// Gets the raw UTF-8 text of a simple value as memory (see <see cref="GetRawValue(int)"/>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<byte> GetRawValueMemory(int index)
    {
        int location = this.ReadInt32(index) & 0x0FFFFFFF;
        int length = this.ReadInt32(index + SizeOrLengthOffset) & int.MaxValue;
        return this.utf8.Slice(location, length);
    }

    /// <summary>
    /// Gets the raw property name text for a property value index (the name row precedes the value row).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetPropertyNameRaw(int valueIndex)
    {
        return this.GetRawValue(valueIndex - RowSize);
    }

    /// <summary>
    /// Gets a value indicating whether the property name for a value index contains escape sequences.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool PropertyNameIsEscaped(int valueIndex)
    {
        return this.IsEscaped(valueIndex - RowSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint ReadUInt32(int offset)
    {
        return MemoryMarshal.Read<uint>(this.rows.AsSpan(offset, sizeof(uint)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReadInt32(int offset)
    {
        return MemoryMarshal.Read<int>(this.rows.AsSpan(offset, sizeof(int)));
    }
}