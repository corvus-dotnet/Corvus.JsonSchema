﻿// <copyright file="ValidationSemantics.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Gets the validation semantic model.
/// </summary>
[Flags]
public enum ValidationSemantics
{
    /// <summary>
    /// Unknown validation semantics.
    /// </summary>
    Unknown = 0b0000,

    /// <summary>
    /// Draft6 semantics.
    /// </summary>
    Draft6 = 0b0001,

    /// <summary>
    /// Draft7 semantics.
    /// </summary>
    Draft7 = 0b0010,

    /// <summary>
    /// Draft 2019-09 semantics.
    /// </summary>
    Draft201909 = 0b0100,

    /// <summary>
    /// Draft 2020-12 semantics.
    /// </summary>
    Draft202012 = 0b1000,

    /// <summary>
    /// OpenAPI 3.0 semantics.
    /// </summary>
    OpenApi30 = 0b0001_0000,

    /// <summary>
    /// Draft 4 semantics.
    /// </summary>
    Draft4 = 0b0010_0000,

    /// <summary>
    /// Semantics prior to draft 2019-09 (i.e. draft6 or draft7).
    /// </summary>
    Pre201909 = Draft4 | Draft6 | Draft7 | OpenApi30,
}