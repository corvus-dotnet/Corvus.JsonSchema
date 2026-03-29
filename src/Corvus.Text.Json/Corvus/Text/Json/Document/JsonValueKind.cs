// <copyright file="JsonValueKind.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json;

/// <summary>
/// Specifies the data type of a JSON value.
/// </summary>
public enum JsonValueKind : byte
{
    /// <summary>
    /// Indicates that there is no value (as distinct from <see cref="Null"/>).
    /// </summary>
    Undefined,

    /// <summary>
    /// Indicates that a value is a JSON object.
    /// </summary>
    Object,

    /// <summary>
    /// Indicates that a value is a JSON array.
    /// </summary>
    Array,

    /// <summary>
    /// Indicates that a value is a JSON string.
    /// </summary>
    String,

    /// <summary>
    /// Indicates that a value is a JSON number.
    /// </summary>
    Number,

    /// <summary>
    /// Indicates that a value is the JSON value <c>true</c>.
    /// </summary>
    True,

    /// <summary>
    /// Indicates that a value is the JSON value <c>false</c>.
    /// </summary>
    False,

    /// <summary>
    /// Indicates that a value is the JSON value <c>null</c>.
    /// </summary>
    Null,
}