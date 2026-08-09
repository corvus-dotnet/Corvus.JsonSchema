// <copyright file="SecurityLabelInstruction.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The operations of a compiled <see cref="SecurityLabelQuery"/> program (see
/// <see cref="SecurityLabelQuery.Compile"/>).
/// </summary>
public enum SecurityLabelInstructionKind
{
    /// <summary>Push the set of row ids carrying the security tag (<see cref="SecurityLabelInstruction.Key"/>, <see cref="SecurityLabelInstruction.Value"/>).</summary>
    PushLabel,

    /// <summary>Push the set of row ids carrying the security-tag key <see cref="SecurityLabelInstruction.Key"/> with any value.</summary>
    PushKey,

    /// <summary>Pop <see cref="SecurityLabelInstruction.Count"/> sets and push their union.</summary>
    Union,

    /// <summary>Pop <see cref="SecurityLabelInstruction.Count"/> sets and push their intersection.</summary>
    Intersect,
}

/// <summary>
/// One step of a compiled <see cref="SecurityLabelQuery"/> plan, in the postfix program produced by
/// <see cref="SecurityLabelQuery.Compile"/> — the seam for a backend that executes the §14.4 set algebra
/// natively (server-side set operations) rather than supplying point lookups to
/// <see cref="SecurityLabelQueryResolver"/>.
/// </summary>
/// <param name="Kind">The operation.</param>
/// <param name="Key">The tag key, for <see cref="SecurityLabelInstructionKind.PushLabel"/> and <see cref="SecurityLabelInstructionKind.PushKey"/>.</param>
/// <param name="Value">The tag value, for <see cref="SecurityLabelInstructionKind.PushLabel"/>.</param>
/// <param name="Count">The operand count, for <see cref="SecurityLabelInstructionKind.Union"/> and <see cref="SecurityLabelInstructionKind.Intersect"/> (always at least two).</param>
public readonly record struct SecurityLabelInstruction(SecurityLabelInstructionKind Kind, string? Key, string? Value, int Count);