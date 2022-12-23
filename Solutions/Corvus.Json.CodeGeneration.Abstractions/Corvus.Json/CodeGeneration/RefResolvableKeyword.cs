// <copyright file="RefResolvableKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Represents a ref resolvable keyword.
/// </summary>
/// <param name="Name">The name of the keyword.</param>
/// <param name="RefResolvablePropertyKind">The strategy to use for inspecting the property for resolvable schema.</param>
public record RefResolvableKeyword(string Name, RefResolvablePropertyKind RefResolvablePropertyKind);