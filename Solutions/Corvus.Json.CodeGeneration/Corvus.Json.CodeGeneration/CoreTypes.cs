// <copyright file="CoreTypes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Indicates core types supported by the generator.
/// </summary>
[Flags]
public enum CoreTypes :  byte
{
    /// <summary>
    /// No core type is specified.
    /// </summary>
    None = 0b0000_0000,

    /// <summary>
    /// An object type.
    /// </summary>
    Object = 0b0000_0001,

    /// <summary>
    /// An array type.
    /// </summary>
    Array = 0b0000_0010,

    /// <summary>
    /// A boolean type.
    /// </summary>
    Boolean = 0b0000_0100,

    /// <summary>
    /// A number type.
    /// </summary>
    Number = 0b0000_1000,

    /// <summary>
    /// An integer type.
    /// </summary>
    Integer = 0b0001_0000,

    /// <summary>
    /// A string type.
    /// </summary>
    String = 0b0010_0000,

    /// <summary>
    /// A null type.
    /// </summary>
    Null = 0b0100_0000,

    /// <summary>
    /// Any core type.
    /// </summary>
    Any = Object | Array | Boolean | Number | Integer | String | Null,
}