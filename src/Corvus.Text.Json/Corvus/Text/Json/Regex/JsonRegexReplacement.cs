// <copyright file="JsonRegexReplacement.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json.Internal;

/// <summary>
/// Provides constants for JSON regular expression replacement operations.
/// </summary>
internal static class JsonRegexReplacement
{
    /// <summary>
    /// Represents the last captured group in the match.
    /// </summary>
    public const int LastGroup = -3;

    /// <summary>
    /// Represents the portion of the input string to the left of the match.
    /// </summary>
    public const int LeftPortion = -1;

    /// <summary>
    /// Represents the portion of the input string to the right of the match.
    /// </summary>
    public const int RightPortion = -2;

    /// <summary>
    /// Represents the entire input string.
    /// </summary>
    public const int WholeString = -4;
}