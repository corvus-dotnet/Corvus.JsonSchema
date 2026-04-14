// <copyright file="GroupBy.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A group-by clause attached to a path step (<c>expr{key: value, ...}</c>).
/// </summary>
internal sealed class GroupBy
{
    /// <summary>Gets the key-value expression pairs that define the grouping.</summary>
    public List<(JsonataNode Key, JsonataNode Value)> Pairs { get; } = [];

    /// <summary>Gets or sets the position of the group-by clause.</summary>
    public int Position { get; set; }
}