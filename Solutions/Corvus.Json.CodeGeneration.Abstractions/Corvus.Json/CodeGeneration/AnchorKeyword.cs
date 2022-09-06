// <copyright file="AnchorKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Represents an anchor keyword.
/// </summary>
/// <param name="Name">The name of the keyword.</param>
/// <param name="IsDynamic">Whether the anchor is dynamic.</param>
/// <param name="IsRecursive">Whether the anchor is recursive.</param>
public record AnchorKeyword(string Name, bool IsDynamic, bool IsRecursive);