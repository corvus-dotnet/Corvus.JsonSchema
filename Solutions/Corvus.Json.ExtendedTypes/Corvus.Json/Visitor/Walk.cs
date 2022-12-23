﻿// <copyright file="Walk.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Visitor;

/// <summary>
/// Used by <see cref="VisitResult"/> to determine what action should be taken after visiting a node.
/// </summary>
public enum Walk : byte
{
    /// <summary>
    /// Continue to iterate into the children of this node, if present or move to the next available sibling.
    /// </summary>
    Continue,

    /// <summary>
    /// Skip the children of this node, and move to the next sibling.
    /// </summary>
    SkipChildren,

    /// <summary>
    /// Remove this node, and continue.
    /// </summary>
    /// <remarks>
    /// You are expected to set the result to an entity with <see cref="System.Text.Json.JsonValueKind.Undefined"/>
    /// when specifying a remove.
    /// </remarks>
    RemoveAndContinue,

    /// <summary>
    /// Terminate the walk at this node, but keep any changes (including changes made to this node if indicated in the result.)
    /// </summary>
    TerminateAtThisNodeAndKeepChanges,

    /// <summary>
    /// Terminate the walk at this node, and abandon any changes that have been made.
    /// </summary>
    TerminateAtThisNodeAndAbandonAllChanges,
}