// <copyright file="WorkflowReachRule.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The per-workflow reach rule the access-request ceiling pins a grant's reach to (ADR 0010): the one definition of
/// its name and expression, shared by the approval service that writes it and the security authoring API that refuses
/// to.
/// </summary>
/// <remarks>
/// <para>The rule is named <c>workflow-access:&lt;workflowId&gt;</c> with the expression
/// <c>sys:workflow == '&lt;workflowId&gt;'</c>, written idempotently by the approval service on the first grant for a
/// workflow, and system-owned (no management tags). A name is only a pin if nobody else can write under it and what
/// it names cannot drift, so the <see cref="NamePrefix"/> namespace is reserved (the authoring API refuses to create
/// or update a rule under it) and the service checks an existing rule's expression before reusing it. Neither control
/// suffices alone: a rule squatted under the name with the right expression would be its squatter's tenant's row to
/// widen later, and a rule written outside the API never meets the namespace check.</para>
/// </remarks>
public static class WorkflowReachRule
{
    /// <summary>The reserved rule-name prefix. A rule under it is the platform ceiling's, never an author's.</summary>
    public const string NamePrefix = "workflow-access:";

    /// <summary>Gets the rule name for a workflow.</summary>
    /// <param name="baseWorkflowId">The base workflow id (validated by the caller against the rule grammar's identifier set).</param>
    /// <returns>The rule name.</returns>
    public static string NameFor(string baseWorkflowId) => NamePrefix + baseWorkflowId;

    /// <summary>Gets the rule expression for a workflow: reach over exactly that workflow's rows.</summary>
    /// <param name="baseWorkflowId">The base workflow id (validated by the caller against the rule grammar's identifier set, since it is woven verbatim into a quoted literal).</param>
    /// <returns>The rule expression.</returns>
    public static string ExpressionFor(string baseWorkflowId) => WorkflowIdentity.WorkflowTagKey + " == '" + baseWorkflowId + "'";

    /// <summary>Gets a value indicating whether a rule name is in the reserved namespace.</summary>
    /// <param name="ruleName">The rule name.</param>
    /// <returns><see langword="true"/> when the name starts with <see cref="NamePrefix"/>.</returns>
    public static bool IsReservedName(string ruleName) => ruleName.StartsWith(NamePrefix, StringComparison.Ordinal);

    /// <summary>Gets a value indicating whether a stored rule is exactly the workflow's reach rule, compared against the
    /// stored expression's UTF-8 without realising it as a string.</summary>
    /// <param name="rule">The stored rule.</param>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <returns><see langword="true"/> when the stored expression equals <see cref="ExpressionFor"/>.</returns>
    public static bool IsExpressionFor(SecurityRuleDocument rule, string baseWorkflowId)
        => ((JsonElement)rule.Expression).EqualsString(ExpressionFor(baseWorkflowId));
}