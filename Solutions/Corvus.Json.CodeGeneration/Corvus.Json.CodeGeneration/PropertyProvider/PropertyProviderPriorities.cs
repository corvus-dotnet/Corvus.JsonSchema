// <copyright file="PropertyProviderPriorities.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Common priorities.
/// </summary>
public static class PropertyProviderPriorities
{
    /// <summary>
    /// Properties are collected first.
    /// </summary>
    public const uint First = 0;

    /// <summary>
    /// The default priority.
    /// </summary>
    public const uint Default = uint.MaxValue / 2;

    /// <summary>
    /// Properties are collected last.
    /// </summary>
    public const uint Last = uint.MaxValue;
}