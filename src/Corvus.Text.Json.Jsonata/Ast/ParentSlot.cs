// <copyright file="ParentSlot.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// Tracks parent-operator ancestry resolution during AST post-processing.
/// Each <c>%</c> operator creates a slot that is resolved by walking back through
/// path steps to find the ancestor at the requested level.
/// </summary>
internal sealed class ParentSlot
{
    /// <summary>Gets or sets the unique label for this parent reference.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of levels to walk up from the current step.</summary>
    public int Level { get; set; }

    /// <summary>Gets or sets the global index of this slot in the ancestry list.</summary>
    public int Index { get; set; }
}