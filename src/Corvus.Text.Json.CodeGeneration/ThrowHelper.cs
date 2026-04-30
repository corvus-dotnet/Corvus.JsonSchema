// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Provides helper methods for throwing exceptions in a consistent manner for code generation scenarios.
/// </summary>
internal static class ThrowHelper
{
    /// <summary>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> when a JSON number exponent is too large.
    /// </summary>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    public static void ThrowArgumentOutOfRangeException_JsonNumberExponentTooLarge(string paramName)
    {
        throw new ArgumentOutOfRangeException(paramName, "The JSON number exponent was too large.");
    }
}