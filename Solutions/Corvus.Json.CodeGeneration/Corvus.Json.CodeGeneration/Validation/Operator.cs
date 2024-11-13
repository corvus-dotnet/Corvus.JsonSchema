// <copyright file="Operator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A comparison operator.
/// </summary>
public enum Operator
{
    /// <summary>
    /// No operator is provided.
    /// </summary>
    None,

    /// <summary>
    /// Equality.
    /// </summary>
    Equals,

    /// <summary>
    /// Inequality.
    /// </summary>
    NotEquals,

    /// <summary>
    /// Less than (exclusive).
    /// </summary>
    LessThan,

    /// <summary>
    /// Less than or equal (inclusive).
    /// </summary>
    LessThanOrEquals,

    /// <summary>
    /// Greater than (exclusive).
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Greater than or equal (inclusive).
    /// </summary>
    GreaterThanOrEquals,

    /// <summary>
    /// Multiple of.
    /// </summary>
    MultipleOf,
}