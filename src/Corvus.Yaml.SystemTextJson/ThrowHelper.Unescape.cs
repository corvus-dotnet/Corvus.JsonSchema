// <copyright file="ThrowHelper.Unescape.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Yaml.Internal;

/// <summary>
/// Provides helper methods for throwing exceptions related to JSON unescape processing.
/// </summary>
internal static partial class ThrowHelper
{
    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when incomplete UTF-16 characters are read.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowInvalidOperationException_ReadIncompleteUTF16()
    {
        throw new InvalidOperationException(SR.CannotReadIncompleteUTF16);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when invalid UTF-16 characters are read.
    /// </summary>
    /// <param name="charAsInt">The invalid character as an integer.</param>
    [DoesNotReturn]
    public static void ThrowInvalidOperationException_ReadInvalidUTF16(int charAsInt)
    {
        throw new InvalidOperationException(SR.Format(SR.CannotReadInvalidUTF16, $"0x{charAsInt:X2}"));
    }
}