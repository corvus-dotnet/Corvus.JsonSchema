// <copyright file="ValidationPriorities.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Common priorities.
/// </summary>
public static class ValidationPriorities
{
    /// <summary>
    /// Validation must be first.
    /// </summary>
    public const uint First = 0;

    /// <summary>
    /// The priority of core-type contributing keywords.
    /// </summary>
    public const uint CoreType = First + 1000;

    /// <summary>
    /// The default priority.
    /// </summary>
    public const uint Default = uint.MaxValue / 2;

    /// <summary>
    /// The priority for composition applicators.
    /// </summary>
    public const uint Composition = Default + 1000;

    /// <summary>
    /// After composition is completed.
    /// </summary>
    public const uint AfterComposition = Composition + 1000;

    /// <summary>
    /// Validation must be last.
    /// </summary>
    public const uint Last = uint.MaxValue;
}