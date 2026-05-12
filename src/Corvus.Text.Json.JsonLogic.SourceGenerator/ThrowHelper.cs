// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace System;

/// <summary>
/// Minimal throw helpers required by BigNumber when linked into the source generator.
/// </summary>
internal static class ThrowHelper
{
    internal static void ThrowArgumentOutOfRangeException_PrecisionMustBeBetween0And255()
    {
        throw new ArgumentOutOfRangeException("precision", "Precision must be between 0 and 255.");
    }
}