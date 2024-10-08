// <copyright file="CommonPatterns.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Common pattern matches.
/// </summary>
public static partial class CommonPatterns
{
#if NET8_0_OR_GREATER
    /// <summary>
    /// Gets the common anchor pattern.
    /// </summary>
    public static readonly Regex AnchorPattern = GetAnchorPattern();
#else
    /// <summary>
    /// Gets the common anchor pattern.
    /// </summary>
    public static readonly Regex AnchorPattern = new("^[A-Za-z][-A-Za-z0-9.:_]*$", RegexOptions.Compiled, TimeSpan.FromSeconds(3));
#endif

#if NET8_0_OR_GREATER
    [GeneratedRegex("^[A-Za-z][-A-Za-z0-9.:_]*$")]
    private static partial Regex GetAnchorPattern();
#endif
}