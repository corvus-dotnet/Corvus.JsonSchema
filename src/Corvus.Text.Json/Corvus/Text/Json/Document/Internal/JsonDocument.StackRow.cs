// <copyright file="JsonDocument.StackRow.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Corvus.Text.Json.Internal;

public abstract partial class JsonDocument
{
    // SizeOrLength - offset - 0 - size - 4
    // NumberOfRows - offset - 4 - size - 4
    // StartIndex   - offset - 8 - size - 4

    /// <summary>
    /// Represents a row in the stack containing size/length and row count information for JSON parsing.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct StackRow
    {
        /// <summary>
        /// The size in bytes of a StackRow structure.
        /// </summary>
        internal const int Size = 12;

        /// <summary>
        /// The size or length value for this stack row.
        /// </summary>
        internal readonly int SizeOrLength;

        /// <summary>
        /// The number of rows for this stack row.
        /// </summary>
        internal readonly int NumberOfRows;

        /// <summary>
        /// The MetadataDb index of the corresponding StartObject or StartArray row,
        /// enabling O(1) lookup at EndObject/EndArray instead of a backward scan.
        /// </summary>
        internal readonly int StartIndex;

        /// <summary>
        /// Initializes a new instance of the <see cref="StackRow"/> struct.
        /// </summary>
        /// <param name="sizeOrLength">The size or length value (must be >= 0).</param>
        /// <param name="numberOfRows">The number of rows (must be >= -1).</param>
        /// <param name="startIndex">The MetadataDb index of the matching start container row (must be >= 0).</param>
        internal StackRow(int sizeOrLength = 0, int numberOfRows = -1, int startIndex = 0)
        {
            Debug.Assert(sizeOrLength >= 0);
            Debug.Assert(numberOfRows >= -1);
            Debug.Assert(startIndex >= 0);
            Debug.Assert(Unsafe.SizeOf<StackRow>() == Size);

            SizeOrLength = sizeOrLength;
            NumberOfRows = numberOfRows;
            StartIndex = startIndex;
        }
    }
}