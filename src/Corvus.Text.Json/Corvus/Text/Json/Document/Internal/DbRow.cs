// <copyright file="DbRow.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
#pragma warning disable IDE0032 // We do not want to use autoproperties here.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Represents a database row containing metadata about a JSON token including its type, location, and structural information.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[StructLayout(LayoutKind.Sequential)]
internal readonly struct DbRow
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => $"DbRow: TokenType = {TokenType}, {(FromExternalDocument && (TokenType is not (JsonTokenType.EndObject or JsonTokenType.EndArray)) ? $"WorkspaceDocumentId: {NumberOfRows}" : $"NumberOfRows: {NumberOfRows}")}";

    /// <summary>
    /// The size in bytes of a DbRow structure.
    /// </summary>
    internal const int Size = 12;

    // Sign bit indicates whether this is from an external document.
    private readonly uint _locationAndFromExternalDocumentUnion;

    // Sign bit is used for "HasComplexChildren" (StartArray)
    // And for propertyMap index
    private readonly int _sizeLengthOrPropertyMapIndexUnion;

    // Top nybble is JsonTokenType
    // remaining nybbles are the number of rows to skip to get to the next value
    // This isn't limiting on the number of rows, since Span.MaxLength / sizeof(DbRow) can't
    // exceed that range.
    private readonly uint _numberOfRowsExternalDocumentIndexAndTypeUnion;

    /// <summary>
    /// Index into the payload
    /// </summary>
    internal int LocationOrIndex => (int)(_locationAndFromExternalDocumentUnion & 0x0FFFFFFFU);

    /// <summary>
    /// length of text in JSON payload (or number of elements if its a JSON array)
    /// </summary>
    internal int SizeOrLengthOrPropertyMapIndex => _sizeLengthOrPropertyMapIndexUnion & int.MaxValue;

    internal bool IsUnknownSize => _sizeLengthOrPropertyMapIndexUnion == UnknownSize;

    /// <summary>
    /// The raw size, length or property map index union
    /// </summary>
    internal int RawSizeOrLength => _sizeLengthOrPropertyMapIndexUnion;

    /// <summary>
    /// Gets a value indicating whether this token has complex children (requires unescaping for strings, or contains objects/arrays for arrays).
    /// </summary>
    internal bool HasComplexChildren => _sizeLengthOrPropertyMapIndexUnion < 0;

    /// <summary>
    /// Gets a value indicating whether this row represents data from an external document.
    /// </summary>
    internal bool FromExternalDocument => (unchecked(_locationAndFromExternalDocumentUnion) & 0x8000_0000U) != 0;

    /// <summary>
    /// Gets the number of rows that the current JSON element occupies within the database.
    /// </summary>
    internal int NumberOfRows =>
        (int)(_numberOfRowsExternalDocumentIndexAndTypeUnion & 0x0FFFFFFFU); // Number of rows that the current JSON element occupies within the database

    /// <summary>
    /// Gets the workspace document ID when this simple value is from an external document.
    /// </summary>
    internal int WorkspaceDocumentId =>
        (int)(_numberOfRowsExternalDocumentIndexAndTypeUnion & 0x0FFFFFFFU); // The workspace document ID, if this simple value is from an external document.

    /// <summary>
    /// Gets the JSON token type for this row.
    /// </summary>
    internal JsonTokenType TokenType => (JsonTokenType)(_numberOfRowsExternalDocumentIndexAndTypeUnion >> 28);

    /// <summary>
    /// Constant representing an unknown size value.
    /// </summary>
    internal const int UnknownSize = -1;

    /// <summary>
    /// Creates an instance of a DBRow.
    /// </summary>
    /// <param name="jsonTokenType">The <see cref="JsonTokenType"/>.</param>
    /// <param name="externalIndex">The index of the value in the external document.</param>
    /// <param name="sizeOrLength">The size or length of the entity.</param>
    /// <param name="workspaceDocumentIndex">The index of the parent document in the workspace.</param>
    internal DbRow(JsonTokenType jsonTokenType, int externalIndex, int sizeOrLength, int workspaceDocumentIndex)
    {
        Debug.Assert(jsonTokenType > JsonTokenType.None && jsonTokenType <= JsonTokenType.Null, "The token type is out of the valid range.");
        Debug.Assert((byte)jsonTokenType < 1 << 4, "The token type is out of the valid range");
        Debug.Assert(externalIndex >= 0, "The location must be >= 0");
        Debug.Assert(workspaceDocumentIndex >= 0, "The parent document index must be >= 0");
        Debug.Assert(Unsafe.SizeOf<DbRow>() == Size);

        _locationAndFromExternalDocumentUnion = (uint)externalIndex | 0x8000_0000U; // Add the sign bit to indicate that this is from an external document.
        _sizeLengthOrPropertyMapIndexUnion = sizeOrLength;
        _numberOfRowsExternalDocumentIndexAndTypeUnion = (unchecked((uint)jsonTokenType << 28) + (unchecked((uint)workspaceDocumentIndex) & 0x0FFFFFFFU));
    }

    /// <summary>
    /// Creates an instance of a DBRow.
    /// </summary>
    /// <param name="jsonTokenType">The <see cref="JsonTokenType"/>.</param>
    /// <param name="location">The location of the value in the UTF8 backing.</param>
    /// <param name="sizeOrLength">The size or length of the entity.</param>
    /// <param name="numberOfRows">The number of rows in the entity.</param>"
    internal DbRow(JsonTokenType jsonTokenType, int location, int sizeOrLength)
    {
        Debug.Assert(jsonTokenType > JsonTokenType.None && jsonTokenType <= JsonTokenType.Null, "The token type is out of the valid range.");
        Debug.Assert((byte)jsonTokenType < 1 << 4, "The token type is out of the valid range");
        Debug.Assert(location >= 0, "The location must be >= 0");
        Debug.Assert(sizeOrLength >= UnknownSize, "The size or length must be >= 0, or UnknownSize");

        _locationAndFromExternalDocumentUnion = (uint)location;
        _sizeLengthOrPropertyMapIndexUnion = sizeOrLength;
        _numberOfRowsExternalDocumentIndexAndTypeUnion = unchecked((uint)jsonTokenType << 28) | 1U;
    }

    internal bool IsSimpleValue => TokenType >= JsonTokenType.PropertyName;

    internal bool HasPropertyMap => _sizeLengthOrPropertyMapIndexUnion <= 0;
}