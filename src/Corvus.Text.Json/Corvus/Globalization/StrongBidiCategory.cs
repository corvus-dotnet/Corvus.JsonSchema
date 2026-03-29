// <copyright file="StrongBidiCategory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Globalization;

// Corresponds to the "strong" categories from https:// www.unicode.org/reports/tr44/#Bidi_Class_Values.
// For our purposes, each code point is strongly left-to-right ("L"), strongly right-to-left ("R", "AL"),
// or other (all remaining code points). This is only used internally by IDN processing, and since our
// IDN processing logic only cares about "strong" values we don't carry the rest of the data.
internal enum StrongBidiCategory
{
    Other = 0x00,
    StrongLeftToRight = 0x20,
    StrongRightToLeft = 0x40,
}