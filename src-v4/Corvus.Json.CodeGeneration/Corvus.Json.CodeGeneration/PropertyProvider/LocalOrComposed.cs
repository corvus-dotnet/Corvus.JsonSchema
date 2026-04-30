// <copyright file="LocalOrComposed.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Determines whether a property is defined on the local schema,
/// or composed from a subschema.
/// </summary>
public enum LocalOrComposed
{
    /// <summary>
    /// Defined on the local type.
    /// </summary>
    Local,

    /// <summary>
    /// Composed from a subschema.
    /// </summary>
    Composed,
}