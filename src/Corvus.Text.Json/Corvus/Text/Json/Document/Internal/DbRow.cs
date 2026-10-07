// <copyright file="DbRow.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
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
    /// Creates a fully-specified local row: an explicit number of rows and complex-children flag, for
    /// copying an already-parsed row run into another database (the source rows carry correct structure;
    /// only the location is rebased by the caller).
    /// </summary>
    /// <param name="jsonTokenType">The <see cref="JsonTokenType"/>.</param>
    /// <param name="location">The (rebased) location of the value in the UTF8 backing.</param>
    /// <param name="sizeOrLength">The size or length of the entity (a property-map index must be normalized to the plain length by the caller).</param>
    /// <param name="numberOfRows">The number of rows the entity occupies.</param>
    /// <param name="hasComplexChildren">Whether the row carries the complex-children/escaped flag.</param>
    internal DbRow(JsonTokenType jsonTokenType, int location, int sizeOrLength, int numberOfRows, bool hasComplexChildren)
    {
        Debug.Assert(jsonTokenType > JsonTokenType.None && jsonTokenType <= JsonTokenType.Null, "The token type is out of the valid range.");
        Debug.Assert(location >= 0, "The location must be >= 0");
        Debug.Assert(sizeOrLength >= 0, "The size or length must be >= 0 (normalize property-map indexes before copying)");
        Debug.Assert(numberOfRows >= 1, "The number of rows must be >= 1");

        _locationAndFromExternalDocumentUnion = (uint)location;
        _sizeLengthOrPropertyMapIndexUnion = hasComplexChildren ? sizeOrLength | int.MinValue : sizeOrLength;
        _numberOfRowsExternalDocumentIndexAndTypeUnion = unchecked((uint)jsonTokenType << 28) | (unchecked((uint)numberOfRows) & 0x0FFFFFFFU);
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

    /// <summary>
    /// The shift of the bits of the location word that say what a local number row's text is. The location itself is
    /// 28 bits and the sign bit marks a row from an external document, which leaves three bits. A row written without
    /// them (by a builder, a mutation, or a copy that rebases the location) says nothing: readers then look at the text.
    /// </summary>
    internal const int NumberShapeShift = 28;

    /// <summary>The number's text is an integer literal: it has no fraction and no exponent.</summary>
    internal const uint NumberIntegerLiteral = 1U << NumberShapeShift;

    /// <summary>The number's text has a fraction or an exponent.</summary>
    internal const uint NumberFractionOrExponent = 2U << NumberShapeShift;

    /// <summary>
    /// For a string or property name row: no byte of its text is above 0x7F, so each byte is one character. Recorded
    /// by the .NET builds of the library, whose string scan finds it. The .NET Standard builds leave it unset, which
    /// is always allowed: a reader of the fact then looks at the text.
    /// </summary>
    internal const uint StringIsAscii = 1U << NumberShapeShift;

    /// <summary>The bits of the location word that carry facts about the row's text.</summary>
    internal const uint TextFactsMask = 0x7000_0000U;

    /// <summary>Gets the facts recorded about the row's text (none for a row from an external document).</summary>
    internal uint TextFacts => FromExternalDocument ? 0U : _locationAndFromExternalDocumentUnion & TextFactsMask;

    /// <summary>Gets a number row's shape: 1 for an integer literal, 2 for a fraction or an exponent, 0 when not recorded.</summary>
    internal int NumberShape => (int)(TextFacts >> NumberShapeShift) & 3;

    /// <summary>Gets a value indicating whether a string or property name row is recorded as all ASCII.</summary>
    internal bool IsAsciiText => (TextFacts & StringIsAscii) != 0;

    /// <summary>
    /// Creates a fully-specified local row that keeps the facts recorded about the source row's text: for copying a
    /// parsed row into another database with its location rebased (the text is the same text).
    /// </summary>
    /// <param name="jsonTokenType">The <see cref="JsonTokenType"/>.</param>
    /// <param name="location">The (rebased) location of the value in the UTF8 backing.</param>
    /// <param name="sizeOrLength">The size or length of the entity.</param>
    /// <param name="numberOfRows">The number of rows the entity occupies.</param>
    /// <param name="hasComplexChildren">Whether the row carries the complex-children/escaped flag.</param>
    /// <param name="textFacts">The source row's <see cref="TextFacts"/>.</param>
    internal DbRow(JsonTokenType jsonTokenType, int location, int sizeOrLength, int numberOfRows, bool hasComplexChildren, uint textFacts)
        : this(jsonTokenType, location, sizeOrLength, numberOfRows, hasComplexChildren)
    {
        Debug.Assert((textFacts & ~TextFactsMask) == 0);
        _locationAndFromExternalDocumentUnion |= textFacts;
    }

    /// <summary>
    /// Creates a local row for a number whose shape the parser recorded.
    /// </summary>
    /// <param name="location">The location of the number in the UTF8 backing.</param>
    /// <param name="length">The length of its text.</param>
    /// <param name="shape">1 for an integer literal, 2 for a number with a fraction or an exponent, 0 for unknown.</param>
    internal DbRow(int location, int length, byte shape)
    {
        Debug.Assert(location >= 0 && location <= 0x0FFFFFFF, "The location must fit 28 bits");
        Debug.Assert(shape <= 2);
        _locationAndFromExternalDocumentUnion = (uint)location | ((uint)shape << NumberShapeShift);
        _sizeLengthOrPropertyMapIndexUnion = length;
        _numberOfRowsExternalDocumentIndexAndTypeUnion = unchecked((uint)JsonTokenType.Number << 28) | 1U;
    }

    internal bool HasPropertyMap => _sizeLengthOrPropertyMapIndexUnion <= 0;
}