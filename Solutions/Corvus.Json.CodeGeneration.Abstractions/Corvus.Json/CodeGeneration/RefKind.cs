// <copyright file="RefKind.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Defines a well-known method of reference resolution.
/// </summary>
public enum RefKind
{
    /// <summary>
    /// $ref-style resolution.
    /// </summary>
    Ref,

    /// <summary>
    /// $recursiveRef-style resolution.
    /// </summary>
    RecursiveRef,

    /// <summary>
    /// $dynamicRef-style resolution.
    /// </summary>
    DynamicRef,
}