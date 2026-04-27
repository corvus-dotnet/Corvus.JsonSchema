// <copyright file="JsonPathFunctionType.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// The type of a JSONPath function parameter or return value, per RFC 9535 §2.4.
/// </summary>
public enum JsonPathFunctionType : byte
{
    /// <summary>
    /// A single JSON value (string, number, boolean, null, array, or object).
    /// </summary>
    ValueType,

    /// <summary>
    /// A boolean (logical true/false) used in filter expressions.
    /// </summary>
    LogicalType,

    /// <summary>
    /// A node list (zero or more JSON elements) produced by a filter query.
    /// </summary>
    NodesType,
}
