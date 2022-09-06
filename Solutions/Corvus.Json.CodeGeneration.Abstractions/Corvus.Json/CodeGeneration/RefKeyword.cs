// <copyright file="RefKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Represents a ref-like keyword.
/// </summary>
/// <param name="Name">The name of the keyword.</param>
/// <param name="RefKind">The strategy to use for resolving the referenced value.</param>
public record RefKeyword(string Name, RefKind RefKind);