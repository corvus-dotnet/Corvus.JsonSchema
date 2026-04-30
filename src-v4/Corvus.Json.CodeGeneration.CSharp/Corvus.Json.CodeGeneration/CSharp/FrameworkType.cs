// <copyright file="FrameworkType.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Specifies a framework type for conditionaly code generation.
/// </summary>
public enum FrameworkType
{
    /// <summary>
    /// The code is never emitted.
    /// </summary>
    NotEmitted,

    /// <summary>
    /// The code is for all framework types.
    /// </summary>
    All,

    /// <summary>
    /// The code is for anything prior to net80.
    /// </summary>
    PreNet80,

    /// <summary>
    /// The code is specifically for net80.
    /// </summary>
    Net80,

    /// <summary>
    /// The code is for net80 or later.
    /// </summary>
    Net80OrGreater,
}