// <copyright file="CriteriaSet.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// A set of compiled criteria evaluated together with AND semantics — used for a step's
/// <c>successCriteria</c> and for the <c>criteria</c> gate on a success/failure action. Per the
/// Arazzo spec, all criteria must be satisfied (an empty set is satisfied).
/// </summary>
public readonly struct CriteriaSet
{
    private readonly CompiledCriterion[] criteria;

    /// <summary>
    /// Initializes a new instance of the <see cref="CriteriaSet"/> struct.
    /// </summary>
    /// <param name="criteria">The compiled criteria (an empty or null set always matches).</param>
    public CriteriaSet(CompiledCriterion[]? criteria) => this.criteria = criteria ?? [];

    /// <summary>Gets an empty criteria set (always satisfied).</summary>
    public static CriteriaSet Empty => new([]);

    /// <summary>Gets the number of criteria in the set.</summary>
    public int Count => this.criteria?.Length ?? 0;

    /// <summary>
    /// Evaluates the set against the context: <see langword="true"/> only if every criterion passes
    /// (vacuously true when empty).
    /// </summary>
    /// <param name="context">The workflow execution context.</param>
    /// <returns><see langword="true"/> if all criteria are satisfied.</returns>
    public bool AllMatch(WorkflowExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        CompiledCriterion[] set = this.criteria ?? [];
        foreach (CompiledCriterion criterion in set)
        {
            if (!criterion.Evaluate(context))
            {
                return false;
            }
        }

        return true;
    }
}