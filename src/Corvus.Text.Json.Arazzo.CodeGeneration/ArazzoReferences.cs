// <copyright file="ArazzoReferences.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo11;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// The set of operations and sub-workflows an Arazzo document references — the manifest the
/// generator uses to filter source-description generation (plan §3.1) and to plan executor emission.
/// </summary>
public sealed class ArazzoReferences
{
    private ArazzoReferences(
        IReadOnlyList<StepReference> steps,
        IReadOnlyList<string> operationIds,
        IReadOnlyList<string> operationPaths,
        IReadOnlyList<string> workflowIds)
    {
        this.Steps = steps;
        this.OperationIds = operationIds;
        this.OperationPaths = operationPaths;
        this.WorkflowIds = workflowIds;
    }

    /// <summary>Gets every step reference, in document order.</summary>
    public IReadOnlyList<StepReference> Steps { get; }

    /// <summary>Gets the distinct <c>operationId</c>s referenced, in first-seen order.</summary>
    public IReadOnlyList<string> OperationIds { get; }

    /// <summary>Gets the distinct <c>operationPath</c>s referenced, in first-seen order.</summary>
    public IReadOnlyList<string> OperationPaths { get; }

    /// <summary>Gets the distinct sub-workflow <c>workflowId</c>s referenced by steps, in first-seen order.</summary>
    public IReadOnlyList<string> WorkflowIds { get; }

    /// <summary>
    /// Collects the references made by every step in every workflow of the document.
    /// </summary>
    /// <param name="document">The Arazzo document.</param>
    /// <returns>The collected references.</returns>
    public static ArazzoReferences Collect(in ArazzoDocument document)
    {
        var steps = new List<StepReference>();
        var operationIds = new OrderedSet();
        var operationPaths = new OrderedSet();
        var workflowIds = new OrderedSet();

        foreach (ArazzoDocument.WorkflowObject workflow in document.Workflows.EnumerateArray())
        {
            string workflowId = workflow.WorkflowId.IsNotUndefined() ? workflow.WorkflowId.GetString()! : string.Empty;

            foreach (ArazzoDocument.StepObject step in workflow.Steps.EnumerateArray())
            {
                string stepId = step.StepId.IsNotUndefined() ? step.StepId.GetString()! : string.Empty;
                (StepTargetKind kind, string? target) = Classify(step);

                steps.Add(new StepReference(workflowId, stepId, kind, target));

                if (target is not null)
                {
                    switch (kind)
                    {
                        case StepTargetKind.OperationId:
                            operationIds.Add(target);
                            break;
                        case StepTargetKind.OperationPath:
                            operationPaths.Add(target);
                            break;
                        case StepTargetKind.WorkflowId:
                            workflowIds.Add(target);
                            break;
                    }
                }
            }
        }

        return new ArazzoReferences(steps, operationIds.ToList(), operationPaths.ToList(), workflowIds.ToList());
    }

    private static (StepTargetKind Kind, string? Target) Classify(in ArazzoDocument.StepObject step)
    {
        if (step.OperationId.IsNotUndefined())
        {
            return (StepTargetKind.OperationId, step.OperationId.GetString());
        }

        if (step.OperationPath.IsNotUndefined())
        {
            return (StepTargetKind.OperationPath, step.OperationPath.GetString());
        }

        if (step.WorkflowId.IsNotUndefined())
        {
            return (StepTargetKind.WorkflowId, step.WorkflowId.GetString());
        }

        return (StepTargetKind.None, null);
    }

    private sealed class OrderedSet
    {
        private readonly List<string> ordered = [];
        private readonly HashSet<string> seen = new(StringComparer.Ordinal);

        public void Add(string value)
        {
            if (this.seen.Add(value))
            {
                this.ordered.Add(value);
            }
        }

        public List<string> ToList() => this.ordered;
    }
}