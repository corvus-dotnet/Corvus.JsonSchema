// <copyright file="RequiredOrOptional.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Determines whether a property is required or optional.
/// </summary>
public enum RequiredOrOptional
{
    /// <summary>
    /// The property is required.
    /// </summary>
    Required,

    /// <summary>
    /// The property is optional.
    /// </summary>
    Optional,
}