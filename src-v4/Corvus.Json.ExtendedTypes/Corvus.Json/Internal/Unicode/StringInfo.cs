// <copyright file="StringInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// </licensing>

#pragma warning disable

#if !NET8_0_OR_GREATER

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Unicode;
using Corvus.Json;

namespace Corvus.Json
{
    /// <summary>
    /// This class defines behaviours specific to a writing system.
    /// A writing system is the collection of scripts and orthographic rules
    /// required to represent a language as text.
    /// </summary>
    internal static class StringInfo
    {
        /// <summary>
        /// Returns the length of the first text element (extended grapheme cluster) that occurs in the input span.
        /// </summary>
        /// <remarks>
        /// A grapheme cluster is a sequence of one or more Unicode code points that should be treated as a single unit.
        /// </remarks>
        /// <param name="str">The input span to analyze.</param>
        /// <returns>The length (in chars) of the substring corresponding to the first text element within <paramref name="str"/>,
        /// or 0 if <paramref name="str"/> is empty.</returns>
        public static int GetNextTextElementLength(ReadOnlySpan<char> str) => TextSegmentationUtility.GetLengthOfFirstUtf16ExtendedGraphemeCluster(str);

        public static int GetTextLengthInRunes(ReadOnlySpan<char> str)
        {
            ReadOnlySpan<char> remaining = str;
            int length = 0;
            while (!remaining.IsEmpty)
            {
                length++; // a new extended grapheme cluster begins at this offset
                remaining = remaining.Slice(GetNextTextElementLength(remaining)); // consume this cluster
            }

            return length;
        }
    }
}

#endif