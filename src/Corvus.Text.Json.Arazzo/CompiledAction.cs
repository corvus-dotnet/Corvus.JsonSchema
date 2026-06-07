// <copyright file="CompiledAction.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// A compiled Arazzo Success/Failure Action: a control-flow effect (<c>end</c>/<c>goto</c>/<c>retry</c>)
/// gated by a <see cref="CriteriaSet"/>. The first action (in document order) whose criteria all match
/// is the one taken.
/// </summary>
public sealed class CompiledAction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompiledAction"/> class.
    /// </summary>
    /// <param name="name">The action name.</param>
    /// <param name="type">The action type.</param>
    /// <param name="criteria">The criteria gating this action.</param>
    /// <param name="stepId">The target step id (for <c>goto</c>/<c>retry</c> targeting a step).</param>
    /// <param name="workflowId">The target workflow id (for <c>goto</c>/<c>retry</c> targeting a workflow).</param>
    /// <param name="retryAfter">The delay, in seconds, before a retry (failure <c>retry</c> actions).</param>
    /// <param name="retryLimit">The maximum number of retries (failure <c>retry</c> actions).</param>
    public CompiledAction(
        string name,
        WorkflowActionType type,
        CriteriaSet criteria,
        string? stepId = null,
        string? workflowId = null,
        double? retryAfter = null,
        int? retryLimit = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        this.Name = name;
        this.Type = type;
        this.Criteria = criteria;
        this.StepId = stepId;
        this.WorkflowId = workflowId;
        this.RetryAfter = retryAfter;
        this.RetryLimit = retryLimit;
    }

    /// <summary>Gets the action name.</summary>
    public string Name { get; }

    /// <summary>Gets the action type.</summary>
    public WorkflowActionType Type { get; }

    /// <summary>Gets the criteria gating this action.</summary>
    public CriteriaSet Criteria { get; }

    /// <summary>Gets the target step id, if any.</summary>
    public string? StepId { get; }

    /// <summary>Gets the target workflow id, if any.</summary>
    public string? WorkflowId { get; }

    /// <summary>Gets the retry delay in seconds, if specified.</summary>
    public double? RetryAfter { get; }

    /// <summary>Gets the retry limit, if specified.</summary>
    public int? RetryLimit { get; }

    /// <summary>
    /// Determines whether this action's criteria are all satisfied by the context.
    /// </summary>
    /// <param name="context">The workflow execution context.</param>
    /// <returns><see langword="true"/> if the action should be taken.</returns>
    public bool Matches(WorkflowExecutionContext context) => this.Criteria.AllMatch(context);

    /// <summary>
    /// Selects the first action whose criteria all match the context (document order), or
    /// <see langword="null"/> if none match (the caller then applies the default behaviour:
    /// next step on success, break/return on failure).
    /// </summary>
    /// <param name="actions">The ordered candidate actions.</param>
    /// <param name="context">The workflow execution context.</param>
    /// <returns>The selected action, or <see langword="null"/>.</returns>
    public static CompiledAction? SelectFirstMatch(IReadOnlyList<CompiledAction> actions, WorkflowExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(context);

        for (int i = 0; i < actions.Count; i++)
        {
            CompiledAction action = actions[i];
            if (action.Matches(context))
            {
                return action;
            }
        }

        return null;
    }
}