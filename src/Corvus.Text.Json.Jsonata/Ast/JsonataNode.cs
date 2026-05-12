// <copyright file="JsonataNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// Base class for all nodes in a JSONata abstract syntax tree.
/// </summary>
internal abstract class JsonataNode
{
    /// <summary>Gets the kind of node.</summary>
    public abstract NodeType Type { get; }

    /// <summary>Gets or sets the zero-based character position in the source expression.</summary>
    public int Position { get; set; }

    /// <summary>Gets or sets a value indicating whether this node should keep singleton arrays.</summary>
    public bool KeepArray { get; set; }

    /// <summary>Gets or sets the list of parent-seeking slots propagated during AST post-processing.</summary>
    public List<ParentSlot>? SeekingParent { get; set; }

    /// <summary>Gets or sets the step annotations (predicates, group-by, focus/index bindings).</summary>
    public StepAnnotations? Annotations { get; set; }
}