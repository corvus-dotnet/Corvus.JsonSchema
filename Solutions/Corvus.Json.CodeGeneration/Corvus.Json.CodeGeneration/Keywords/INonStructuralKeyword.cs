// <copyright file="INonStructuralKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that does not contribute the structure of the schema
/// (e.g. documentation, examples, vocabulary).
/// </summary>
public interface INonStructuralKeyword : IKeyword
{
}