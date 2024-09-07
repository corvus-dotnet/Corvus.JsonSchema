// <copyright file="HandlerPriorities.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.Markdown;

/// <summary>
/// Well-known handler priorities.
/// </summary>
public static class HandlerPriorities
{
    /// <summary>
    /// Gets the minimum priority. These will be executed first.
    /// </summary>
    public const uint Minimum = uint.MinValue;

    /// <summary>
    /// Gets the priority for header information.
    /// </summary>
    public const uint Header = Minimum + 100;

    /// <summary>
    /// Gets the default priority.
    /// </summary>
    public const uint Default = uint.MaxValue / 2;

    /// <summary>
    /// Gets the priority for footer information.
    /// </summary>
    public const uint Footer = Maximum - 100;

    /// <summary>
    /// Gets the maximum priority. These will be executed last.
    /// </summary>
    public const uint Maximum = uint.MaxValue;
}