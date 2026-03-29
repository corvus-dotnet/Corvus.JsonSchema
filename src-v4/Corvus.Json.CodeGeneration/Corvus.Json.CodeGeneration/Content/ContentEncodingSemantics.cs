// <copyright file="ContentEncodingSemantics.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Defines the content encoding semantics.
/// </summary>
public enum ContentEncodingSemantics
{
    /// <summary>
    /// Content semantics are those pre-2019-09.
    /// </summary>
    PreDraft201909,

    /// <summary>
    /// Content semantics are those 2019-09 and later.
    /// </summary>
    Draft102909AndLater,
}