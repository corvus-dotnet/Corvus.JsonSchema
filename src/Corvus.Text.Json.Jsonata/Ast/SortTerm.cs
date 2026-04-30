// <copyright file="SortTerm.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A single term in a <c>^()</c> order-by clause.
/// </summary>
internal sealed class SortTerm
{
    /// <summary>Gets or sets a value indicating whether this term sorts descending.</summary>
    public bool Descending { get; set; }

    /// <summary>Gets or sets the expression to evaluate for each element to determine sort order.</summary>
    public JsonataNode Expression { get; set; } = null!;
}