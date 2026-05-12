// <copyright file="FusedPipePlanner.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JMESPath;

/// <summary>
/// Classifies pipe stages and builds a fused pipeline plan.
/// Used by both the runtime compiler and the code generator to avoid
/// materializing intermediate documents between pipe stages.
/// </summary>
internal static class FusedPipePlanner
{
    /// <summary>
    /// Tries to build a fused pipeline plan from a pipe node.
    /// </summary>
    /// <param name="pipe">The outermost PipeNode.</param>
    /// <param name="source">The source expression (first stage's left input).</param>
    /// <param name="stages">The classified pipeline stages.</param>
    /// <returns><see langword="true"/> if the entire pipe chain is fusible.</returns>
    public static bool TryBuildPlan(
        PipeNode pipe,
        out JMESPathNode source,
        out PipeStage[] stages)
    {
        // 1. Flatten the left-associative pipe chain.
        List<JMESPathNode> flat = FlattenPipeChain(pipe);

        // 2. Classify each stage. If any is non-fusible, bail out.
        List<PipeStage> classified = new(flat.Count);

        // The source is extracted from the first fusible stage.
        // For projections (ListProjection, FilterProjection, FlattenProjection),
        // the source is node.Left and the stage represents the projection itself.
        // For function calls (sort, sort_by, reverse, map), the source is the
        // function's first argument (which must be @).
        source = default!;
        bool sourceSet = false;

        for (int i = 0; i < flat.Count; i++)
        {
            JMESPathNode node = flat[i];

            if (!sourceSet)
            {
                // First stage: extract source and classify the stage itself.
                if (!TryClassifyFirstStage(node, out source!, out PipeStage? firstStage))
                {
                    stages = default!;
                    return false;
                }

                if (firstStage is not null)
                {
                    classified.Add(firstStage);
                }

                sourceSet = true;
            }
            else
            {
                // Subsequent stages: must operate on current (@) input.
                if (!TryClassifySubsequentStage(node, out PipeStage? stage))
                {
                    stages = default!;
                    return false;
                }

                if (stage is not null)
                {
                    classified.Add(stage);
                }
            }
        }

        // Must have at least 2 stages to benefit from fusion.
        if (classified.Count < 2)
        {
            stages = default!;
            return false;
        }

        stages = classified.ToArray();
        return true;
    }

    /// <summary>
    /// Flattens a left-associative pipe chain into a list of stages.
    /// <c>Pipe(Pipe(A, B), C)</c> becomes <c>[A, B, C]</c>.
    /// </summary>
    private static List<JMESPathNode> FlattenPipeChain(PipeNode pipe)
    {
        List<JMESPathNode> stages = new();
        FlattenPipeChainCore(pipe, stages);
        return stages;
    }

    private static void FlattenPipeChainCore(JMESPathNode node, List<JMESPathNode> stages)
    {
        if (node is PipeNode pipe)
        {
            FlattenPipeChainCore(pipe.Left, stages);
            FlattenPipeChainCore(pipe.Right, stages);
        }
        else
        {
            stages.Add(node);
        }
    }

    /// <summary>
    /// Classifies the first stage of the pipeline. The first stage provides
    /// the source array and optionally a streaming operation.
    /// </summary>
    private static bool TryClassifyFirstStage(
        JMESPathNode node,
        out JMESPathNode source,
        out PipeStage? stage)
    {
        switch (node)
        {
            case FilterProjectionNode filter:
                source = filter.Left;
                stage = new PipeStage.Filter(filter.Condition, filter.Right is CurrentNode ? null : filter.Right);
                return true;

            case ListProjectionNode listProj:
                source = listProj.Left;
                JMESPathNode? proj = UnwrapProjection(listProj.Right);
                if (proj is MultiSelectHashNode hash)
                {
                    stage = new PipeStage.HashProject(hash);
                }
                else if (proj is not null)
                {
                    stage = new PipeStage.Project(proj);
                }
                else
                {
                    // [*] identity — no stage needed, source is already the array
                    stage = null;
                }

                return true;

            case FlattenProjectionNode flatten:
                source = flatten.Left;
                JMESPathNode? flatProj = UnwrapProjection(flatten.Right);
                stage = new PipeStage.Flatten(flatProj);
                return true;

            // A bare function call like sort(expr) — the source is the function's argument.
            case FunctionCallNode fn when IsFusibleFunction(fn, out PipeStage? fnStage, out JMESPathNode? fnSource):
                source = fnSource!;
                stage = fnStage;
                return true;

            // A simple expression that just provides the source array (e.g., .people)
            // with no transformation — valid as source with no stage.
            default:
                // Non-projection first stage: treat as source provider.
                // This is valid — the first element just evaluates to the source array.
                source = node;
                stage = null;
                return true;
        }
    }

    /// <summary>
    /// Classifies a subsequent stage (not the first). These must operate on the
    /// current array (the previous stage's output).
    /// </summary>
    private static bool TryClassifySubsequentStage(
        JMESPathNode node,
        out PipeStage? stage)
    {
        switch (node)
        {
            case FilterProjectionNode filter when filter.Left is CurrentNode:
                stage = new PipeStage.Filter(filter.Condition, filter.Right is CurrentNode ? null : filter.Right);
                return true;

            case ListProjectionNode listProj when listProj.Left is CurrentNode:
                JMESPathNode? proj = UnwrapProjection(listProj.Right);
                if (proj is MultiSelectHashNode hash)
                {
                    stage = new PipeStage.HashProject(hash);
                }
                else if (proj is not null)
                {
                    stage = new PipeStage.Project(proj);
                }
                else
                {
                    // [*] identity — no-op in a pipe, skip
                    stage = null;
                }

                return true;

            case FlattenProjectionNode flatten when flatten.Left is CurrentNode:
                JMESPathNode? flatProj = UnwrapProjection(flatten.Right);
                stage = new PipeStage.Flatten(flatProj);
                return true;

            case FunctionCallNode fn when IsFusiblePipeFunction(fn, out PipeStage? fnStage):
                stage = fnStage;
                return true;

            default:
                // Non-fusible stage — bail out.
                stage = null;
                return false;
        }
    }

    /// <summary>
    /// Checks if a function call is fusible as the first stage of a pipe
    /// (where the function's array argument provides the source).
    /// </summary>
    private static bool IsFusibleFunction(
        FunctionCallNode fn,
        out PipeStage? stage,
        out JMESPathNode? source)
    {
        ReadOnlySpan<byte> name = fn.Name.AsSpan();

        if (name.SequenceEqual("sort"u8) && fn.Arguments.Length == 1)
        {
            source = fn.Arguments[0];
            stage = new PipeStage.Sort();
            return true;
        }

        if (name.SequenceEqual("sort_by"u8)
            && fn.Arguments.Length == 2
            && fn.Arguments[1] is ExpressionRefNode sortExpr)
        {
            source = fn.Arguments[0];
            stage = new PipeStage.SortBy(sortExpr.Expression);
            return true;
        }

        if (name.SequenceEqual("reverse"u8) && fn.Arguments.Length == 1)
        {
            source = fn.Arguments[0];
            stage = new PipeStage.Reverse();
            return true;
        }

        if (name.SequenceEqual("map"u8)
            && fn.Arguments.Length == 2
            && fn.Arguments[0] is ExpressionRefNode mapExpr)
        {
            source = fn.Arguments[1];
            stage = new PipeStage.MapExpr(mapExpr.Expression);
            return true;
        }

        stage = null;
        source = null;
        return false;
    }

    /// <summary>
    /// Checks if a function call is fusible as a subsequent pipe stage
    /// (must operate on current node @).
    /// </summary>
    private static bool IsFusiblePipeFunction(
        FunctionCallNode fn,
        out PipeStage? stage)
    {
        ReadOnlySpan<byte> name = fn.Name.AsSpan();

        if (name.SequenceEqual("sort"u8)
            && fn.Arguments.Length == 1
            && fn.Arguments[0] is CurrentNode)
        {
            stage = new PipeStage.Sort();
            return true;
        }

        if (name.SequenceEqual("sort_by"u8)
            && fn.Arguments.Length == 2
            && fn.Arguments[0] is CurrentNode
            && fn.Arguments[1] is ExpressionRefNode sortExpr)
        {
            stage = new PipeStage.SortBy(sortExpr.Expression);
            return true;
        }

        if (name.SequenceEqual("reverse"u8)
            && fn.Arguments.Length == 1
            && fn.Arguments[0] is CurrentNode)
        {
            stage = new PipeStage.Reverse();
            return true;
        }

        if (name.SequenceEqual("map"u8)
            && fn.Arguments.Length == 2
            && fn.Arguments[0] is ExpressionRefNode mapExpr
            && fn.Arguments[1] is CurrentNode)
        {
            stage = new PipeStage.MapExpr(mapExpr.Expression);
            return true;
        }

        stage = null;
        return false;
    }

    /// <summary>
    /// Unwraps the projection expression from a projection node's Right side.
    /// Returns <see langword="null"/> if the projection is identity (CurrentNode).
    /// Unwraps SubExpressionNode(CurrentNode, X) to X.
    /// </summary>
    private static JMESPathNode? UnwrapProjection(JMESPathNode right)
    {
        if (right is CurrentNode)
        {
            return null;
        }

        if (right is SubExpressionNode { Left: CurrentNode, Right: var inner })
        {
            return inner;
        }

        return right;
    }
}

/// <summary>
/// Represents a classified stage in a fused pipe pipeline.
/// </summary>
internal abstract class PipeStage
{
    private PipeStage()
    {
    }

    /// <summary>Gets a value indicating whether this stage is a barrier (needs all elements).</summary>
    public abstract bool IsBarrier { get; }

    /// <summary>
    /// Filter: evaluate condition, keep truthy elements, optionally project. Cardinality 0:1.
    /// </summary>
    internal sealed class Filter(JMESPathNode condition, JMESPathNode? projection) : PipeStage
    {
        public JMESPathNode Condition { get; } = condition;

        public JMESPathNode? Projection { get; } = projection;

        public override bool IsBarrier => false;
    }

    /// <summary>
    /// Project: apply expression to each element, skip null/undefined. Cardinality 0:1.
    /// </summary>
    internal sealed class Project(JMESPathNode expression) : PipeStage
    {
        public JMESPathNode Expression { get; } = expression;

        public override bool IsBarrier => false;
    }

    /// <summary>
    /// Flatten: expand inner arrays one level, optionally project. Cardinality 0:N.
    /// </summary>
    internal sealed class Flatten(JMESPathNode? projection) : PipeStage
    {
        public JMESPathNode? Projection { get; } = projection;

        public override bool IsBarrier => false;
    }

    /// <summary>
    /// Map: apply expression to each element, preserve nulls as JSON null. Cardinality 1:1.
    /// </summary>
    internal sealed class MapExpr(JMESPathNode expression) : PipeStage
    {
        public JMESPathNode Expression { get; } = expression;

        public override bool IsBarrier => false;
    }

    /// <summary>
    /// Sort: sort elements by value (numbers or strings). Barrier.
    /// </summary>
    internal sealed class Sort : PipeStage
    {
        public override bool IsBarrier => true;
    }

    /// <summary>
    /// SortBy: sort elements by key expression. Barrier.
    /// </summary>
    internal sealed class SortBy(JMESPathNode keyExpression) : PipeStage
    {
        public JMESPathNode KeyExpression { get; } = keyExpression;

        public override bool IsBarrier => true;
    }

    /// <summary>
    /// Reverse: reverse array order. Barrier.
    /// </summary>
    internal sealed class Reverse : PipeStage
    {
        public override bool IsBarrier => true;
    }

    /// <summary>
    /// Slice: extract subset by indices. Barrier.
    /// </summary>
    internal sealed class Slice(int? start, int? stop, int step) : PipeStage
    {
        public int? Start { get; } = start;

        public int? Stop { get; } = stop;

        public int Step { get; } = step;

        public override bool IsBarrier => true;
    }

    /// <summary>
    /// HashProject: terminal stage that constructs objects from each element.
    /// Used as the final streaming stage to enable CreateBuilder + ObjectBuilder fusion.
    /// </summary>
    internal sealed class HashProject(MultiSelectHashNode hash) : PipeStage
    {
        public MultiSelectHashNode Hash { get; } = hash;

        public override bool IsBarrier => false;
    }
}
