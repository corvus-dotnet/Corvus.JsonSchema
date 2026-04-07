// <copyright file="FunctionalCompiler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.Jsonata.Ast;

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Compiles a JSONata AST into a delegate tree of <see cref="ExpressionEvaluator"/>
/// closures. Each AST node is compiled once; the resulting delegate can then be
/// invoked many times against different input data with zero per-evaluation allocation
/// on the common (singleton) path.
/// </summary>
internal static class FunctionalCompiler
{
    // Pre-cached constant elements via JsonataHelpers
    private static readonly JsonElement TrueElement = JsonataHelpers.True();
    private static readonly JsonElement FalseElement = JsonataHelpers.False();
    private static readonly JsonElement NullElement = JsonataHelpers.Null();

    // Pre-compiled regex for stripping leading zeros from exponents in number formatting
    private static readonly Regex ExponentLeadingZeroRegex = new(@"e([+-])0+(\d)", RegexOptions.Compiled);

    /// <summary>
    /// Compiles an AST node into an evaluator delegate.
    /// </summary>
    public static ExpressionEvaluator Compile(JsonataNode node)
    {
        var evaluator = CompileCore(node);

        // Apply annotations (stages) for non-path nodes.
        // PathNode handles its own stages in CompilePath.
        // Individual step nodes (NameNode, etc.) inside paths are compiled via
        // CompileCore to avoid double-application — CompilePath applies their stages.
        if (node is not PathNode && node.Annotations is not null)
        {
            if (node.Annotations.Stages.Count > 0)
            {
                evaluator = WrapWithStages(evaluator, node.Annotations);
            }

            if (node.Annotations.Group is not null)
            {
                evaluator = WrapWithGroupBy(evaluator, node.Annotations.Group);
            }
        }

        // Handle expr[] — the "keep array" modifier ensures the result is always
        // returned as a JSON array, even for singleton results. PathNode handles
        // this via its own KeepSingletonArray flag in CompilePath.
        if (node is not PathNode && node.KeepArray)
        {
            evaluator = WrapKeepArray(evaluator);
        }

        return evaluator;
    }

    /// <summary>
    /// Core dispatch: compiles a node without applying its annotations.
    /// Used by CompilePath for step compilation (path applies stages itself).
    /// </summary>
    private static ExpressionEvaluator CompileCore(JsonataNode node)
    {
        return node switch
        {
            PathNode path => CompilePath(path),
            BinaryNode binary => CompileBinary(binary),
            UnaryNode unary => CompileUnary(unary),
            NumberNode num => CompileNumber(num),
            StringNode str => CompileString(str),
            ValueNode val => CompileValue(val),
            VariableNode variable => CompileVariable(variable),
            NameNode name => CompileName(name),
            WildcardNode => CompileWildcard(),
            DescendantNode => CompileDescendant(),
            BlockNode block => CompileBlock(block),
            ConditionNode cond => CompileCondition(cond),
            ArrayConstructorNode arr => CompileArrayConstructor(arr),
            ObjectConstructorNode obj => CompileObjectConstructor(obj),
            FunctionCallNode func => CompileFunctionCall(func),
            LambdaNode lambda => CompileLambda(lambda),
            BindNode bind => CompileBind(bind),
            ApplyNode apply => CompileApply(apply),
            FilterNode filter => CompileFilter(filter),
            SortNode sort => CompileSort(sort),
            TransformNode transform => CompileTransform(transform),
            RegexNode regex => CompileRegex(regex),
            ParentNode => CompileParent(),
            PartialNode partial => CompilePartial(partial),
            PlaceholderNode => throw new JsonataException("D1001", "Unexpected placeholder outside partial application", node.Position),
            _ => throw new JsonataException("D1001", $"Unknown node type: {node.Type}", node.Position),
        };
    }

    private static ExpressionEvaluator WrapWithStages(ExpressionEvaluator inner, StepAnnotations annotations)
    {
        var stageEvaluators = new ExpressionEvaluator[annotations.Stages.Count];
        var isSortStage = new bool[annotations.Stages.Count];
        for (int i = 0; i < annotations.Stages.Count; i++)
        {
            var stage = annotations.Stages[i];
            if (stage is FilterNode filterNode)
            {
                stageEvaluators[i] = Compile(filterNode.Expression);
            }
            else if (stage is SortNode sortNode)
            {
                stageEvaluators[i] = CompileSortStage(sortNode);
                isSortStage[i] = true;
            }
            else
            {
                stageEvaluators[i] = Compile(stage);
            }
        }

        return (in JsonElement input, Environment env) =>
        {
            var result = inner(input, env);
            return ApplyStages(result, stageEvaluators, isSortStage, env);
        };
    }

    /// <summary>
    /// Wraps an evaluator to apply a group-by clause for non-path nodes
    /// (e.g., function calls or variable references followed by <c>{key: value}</c>).
    /// </summary>
    private static ExpressionEvaluator WrapWithGroupBy(ExpressionEvaluator inner, GroupBy group)
    {
        var groupByPairs = new (ExpressionEvaluator Key, ExpressionEvaluator Value)[group.Pairs.Count];
        for (int g = 0; g < group.Pairs.Count; g++)
        {
            groupByPairs[g] = (Compile(group.Pairs[g].Key), Compile(group.Pairs[g].Value));
        }

        return (in JsonElement input, Environment env) =>
        {
            var result = inner(input, env);
            return ApplyGroupBy(result, groupByPairs, env);
        };
    }

    /// <summary>
    /// Wraps an evaluator to ensure the result is always a JSON array (the [] operator).
    /// Singletons are wrapped in a one-element array; multi-value sequences are
    /// materialized as arrays; undefined remains undefined.
    /// </summary>
    private static ExpressionEvaluator WrapKeepArray(ExpressionEvaluator inner)
    {
        return (in JsonElement input, Environment env) =>
        {
            var result = inner(input, env);

            if (result.IsUndefined)
            {
                return Sequence.Undefined;
            }

            // Already a multi-value sequence — materialize as JSON array
            if (!result.IsSingleton)
            {
                return MaterializeAsArray(result, env.Workspace);
            }

            // Singleton: if it's already a JSON array, return as-is
            var element = result.FirstOrDefault;
            if (element.ValueKind == JsonValueKind.Array)
            {
                return result;
            }

            // Wrap scalar in a one-element JSON array
            return MaterializeAsArray(result, env.Workspace);
        };
    }

    private static Sequence MaterializeAsArray(Sequence seq, JsonWorkspace workspace)
    {
        return new Sequence(JsonataHelpers.ArrayFromSequence(seq, workspace));
    }

    private static ExpressionEvaluator CompileNumber(NumberNode num)
    {
        double value = num.Value;
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new JsonataException("D3001", "A string cannot be generated from this value", 0);
        }

        return (in JsonElement input, Environment env) => new Sequence(JsonataHelpers.NumberFromDouble(value, env.Workspace));
    }

    private static ExpressionEvaluator CompileString(StringNode str)
    {
        // Strings with unpaired surrogates cannot be stored as JSON elements;
        // defer the error to evaluation time so that URL functions can report D3140.
        if (HasUnpairedSurrogate(str.Value))
        {
            var rawValue = str.Value;
            return (in JsonElement input, Environment env) =>
            {
                throw new JsonataException("D3140", $"String contains unpaired surrogates", 0);
            };
        }

        int maxLen = (str.Value.Length * 6) + 2;
        byte[] buffer = new byte[maxLen];
        buffer[0] = (byte)'"';
        int written = JsonataHelpers.WriteJsonEscapedUtf8(str.Value.AsSpan(), buffer.AsSpan(1));
        buffer[written + 1] = (byte)'"';
        var element = JsonElement.ParseValue(new ReadOnlySpan<byte>(buffer, 0, written + 2));
        return (in JsonElement input, Environment env) => new Sequence(element);
    }

    private static bool HasUnpairedSurrogate(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 >= s.Length || !char.IsLowSurrogate(s[i + 1]))
                {
                    return true;
                }

                i++;
            }
            else if (char.IsLowSurrogate(c))
            {
                return true;
            }
        }

        return false;
    }

    private static ExpressionEvaluator CompileValue(ValueNode val)
    {
        return val.Value switch
        {
            "true" => static (in JsonElement input, Environment env) =>
                new Sequence(TrueElement),
            "false" => static (in JsonElement input, Environment env) =>
                new Sequence(FalseElement),
            "null" => static (in JsonElement input, Environment env) =>
                new Sequence(NullElement),
            _ => throw new JsonataException("D1001", $"Unknown value: {val.Value}", val.Position),
        };
    }

    private static ExpressionEvaluator CompileVariable(VariableNode variable)
    {
        var name = variable.Name;

        // $ (empty name) = current context
        if (name.Length == 0)
        {
            return static (in JsonElement input, Environment env) => new Sequence(input);
        }

        // $$ = root input
        if (name == "$")
        {
            return static (in JsonElement input, Environment env) => new Sequence(env.GetRootInput());
        }

        return (in JsonElement input, Environment env) =>
        {
            if (env.TryLookup(name, out var value))
            {
                return value;
            }

            // Check if this is a built-in function reference (e.g. $uppercase used as a value)
            var builtIn = BuiltInFunctions.TryGetCompiler(name);
            if (builtIn is not null)
            {
                return new Sequence(CreateBuiltInLambda(builtIn));
            }

            return Sequence.Undefined;
        };
    }

    private static ExpressionEvaluator CompileName(NameNode name)
    {
        var fieldName = name.Value;
        return (in JsonElement input, Environment env) =>
        {
            return LookupField(input, fieldName);
        };

        static Sequence LookupField(in JsonElement input, string fieldName)
        {
            if (input.ValueKind == JsonValueKind.Object)
            {
                return input.TryGetProperty(fieldName, out var value)
                    ? new Sequence(value)
                    : Sequence.Undefined;
            }

            if (input.ValueKind == JsonValueKind.Array)
            {
                var builder = default(SequenceBuilder);
                foreach (var item in input.EnumerateArray())
                {
                    var result = LookupField(item, fieldName);
                    if (!result.IsUndefined)
                    {
                        builder.AddRange(result);
                    }
                }

                return builder.ToSequence();
            }

            return Sequence.Undefined;
        }
    }

    private static ExpressionEvaluator CompileWildcard()
    {
        return static (in JsonElement input, Environment env) =>
        {
            if (input.ValueKind == JsonValueKind.Array)
            {
                var arrayBuilder = default(SequenceBuilder);
                foreach (var item in input.EnumerateArray())
                {
                    arrayBuilder.Add(item);
                }

                return arrayBuilder.ToSequence();
            }

            if (input.ValueKind != JsonValueKind.Object)
            {
                return Sequence.Undefined;
            }

            var builder = default(SequenceBuilder);
            foreach (var prop in input.EnumerateObject())
            {
                // Flatten array property values into individual elements
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        builder.Add(item);
                    }
                }
                else
                {
                    builder.Add(prop.Value);
                }
            }

            var result = builder.ToSequence();

            // Note: multi-value sequences hold a reference to the rented array.
            // The caller is responsible for lifetime. In practice, this is
            // managed by the path evaluator's traversal loop.
            return result;
        };
    }

    private static ExpressionEvaluator CompileDescendant()
    {
        return static (in JsonElement input, Environment env) =>
        {
            var builder = default(SequenceBuilder);
            CollectDescendants(input, ref builder);
            return builder.ToSequence();
        };

        static void CollectDescendants(JsonElement element, ref SequenceBuilder builder)
        {
            // Non-array values are added as descendants (the element itself).
            // Arrays are never added — only their elements are recursed into.
            // This matches the jsonata-js recurse_descendant semantics.
            if (element.ValueKind != JsonValueKind.Array)
            {
                builder.Add(element);
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        CollectDescendants(prop.Value, ref builder);
                    }

                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        CollectDescendants(item, ref builder);
                    }

                    break;
            }
        }
    }

    private static ExpressionEvaluator CompilePath(PathNode path)
    {
        if (path.Steps.Count == 0)
        {
            return static (in JsonElement input, Environment env) => Sequence.Undefined;
        }

        // Compile each step and its annotations (stages/filters)
        var steps = new ExpressionEvaluator[path.Steps.Count];
        var stages = new ExpressionEvaluator[]?[path.Steps.Count];
        var stageIsSortFlags = new bool[]?[path.Steps.Count];
        var focusVars = new string?[path.Steps.Count];
        var indexVars = new string?[path.Steps.Count];
        var isPropertyStep = new bool[path.Steps.Count];
        var isConsArrayStep = new bool[path.Steps.Count];
        var isSortStep = new bool[path.Steps.Count];
        var sortTermsPerStep = new (ExpressionEvaluator Expr, bool Descending)[]?[path.Steps.Count];
        (ExpressionEvaluator Key, ExpressionEvaluator Value)[]? groupByPairs = null;

        for (int i = 0; i < path.Steps.Count; i++)
        {
            steps[i] = CompileCore(path.Steps[i]);
            isPropertyStep[i] = path.Steps[i] is NameNode or WildcardNode or DescendantNode;
            isConsArrayStep[i] = path.Steps[i] is ArrayConstructorNode { ConsArray: true };
            isSortStep[i] = path.Steps[i] is SortNode;
            if (path.Steps[i] is SortNode sortNodeStep)
            {
                sortTermsPerStep[i] = sortNodeStep.Terms
                    .Select(t => (Compile(t.Expression), t.Descending)).ToArray();
            }

            var annotations = GetStepAnnotations(path.Steps[i]);
            if (annotations is not null)
            {
                if (annotations.Stages.Count > 0)
                {
                    stages[i] = new ExpressionEvaluator[annotations.Stages.Count];
                    stageIsSortFlags[i] = new bool[annotations.Stages.Count];
                    for (int s = 0; s < annotations.Stages.Count; s++)
                    {
                        var stage = annotations.Stages[s];
                        if (stage is FilterNode filterNode)
                        {
                            // Compile just the predicate expression — ApplyStages handles filtering logic
                            stages[i]![s] = Compile(filterNode.Expression);
                        }
                        else if (stage is SortNode sortNode)
                        {
                            stages[i]![s] = CompileSortStage(sortNode);
                            stageIsSortFlags[i]![s] = true;
                        }
                        else
                        {
                            stages[i]![s] = Compile(stage);
                        }
                    }
                }

                focusVars[i] = annotations.Focus;
                indexVars[i] = annotations.Index;

                if (annotations.Group is not null)
                {
                    var group = annotations.Group;
                    groupByPairs = new (ExpressionEvaluator, ExpressionEvaluator)[group.Pairs.Count];
                    for (int g = 0; g < group.Pairs.Count; g++)
                    {
                        groupByPairs[g] = (Compile(group.Pairs[g].Key), Compile(group.Pairs[g].Value));
                    }
                }
            }
        }

        // Also check the path node's own annotations (group-by may be attached to the path itself)
        var pathAnnotations = GetStepAnnotations(path);
        if (pathAnnotations?.Group is not null && groupByPairs is null)
        {
            var group = pathAnnotations.Group;
            groupByPairs = new (ExpressionEvaluator, ExpressionEvaluator)[group.Pairs.Count];
            for (int g = 0; g < group.Pairs.Count; g++)
            {
                groupByPairs[g] = (Compile(group.Pairs[g].Key), Compile(group.Pairs[g].Value));
            }
        }

        var keepSingleton = path.KeepSingletonArray;
        var lastStepIsConsArray = path.Steps[path.Steps.Count - 1] is ArrayConstructorNode { ConsArray: true };

        // Detect if a sort step follows an index-bound step (tuple semantics).
        // If so, we track per-element group indices through the sort to restore bindings.
        string? tupleIndexVar = null;
        for (int k = 0; k < path.Steps.Count; k++)
        {
            if (isSortStep[k])
            {
                for (int j = 0; j < k; j++)
                {
                    if (indexVars[j] is not null)
                    {
                        tupleIndexVar = indexVars[j];
                    }
                }
            }
        }

        return (in JsonElement input, Environment env) =>
        {
            return EvalPathFrom(new Sequence(input), 0);

            Sequence EvalPathFrom(Sequence initial, int startStep)
            {
                Sequence current = initial;

                // Per-element group indices for tuple-aware sort.
                // When non-null, each element in current has a corresponding group index
                // used to restore index variable bindings after sorting.
                int[]? tupleGroupIndices = null;

                for (int stepIdx = startStep; stepIdx < steps.Length; stepIdx++)
                {
                    if (current.IsUndefined)
                    {
                        return Sequence.Undefined;
                    }

                    var step = steps[stepIdx];
                    var focusVar = focusVars[stepIdx];
                    var indexVar = indexVars[stepIdx];

                    // Focus binding (@$var) — cross-join semantics.
                    // The focus variable captures each element of the step's result.
                    // Subsequent steps evaluate from the PARENT context (input to this step),
                    // not from the step's output. This creates a cross-join when multiple
                    // focus-bound steps are chained (e.g. loans@$l.books@$b).
                    if (focusVar is not null)
                    {
                        return EvalFocusStep(current, stepIdx);
                    }

                    // Index binding (#$var) without focus — per-element propagation.
                    // When a step has #$var AND there are subsequent non-sort steps, we must
                    // evaluate remaining steps per-element so each element carries its correct
                    // index. Skip this when the next step is a sort (sort needs all elements
                    // as a batch). Unlike focus binding, the next step evaluates on each
                    // result element (not the parent context).
                    if (indexVar is not null && stepIdx + 1 < steps.Length
                        && !isSortStep[stepIdx + 1])
                    {
                        return EvalIndexStep(current, stepIdx);
                    }

                    // Sort steps need all elements as a batch — collect, flatten, sort.
                    if (isSortStep[stepIdx])
                    {
                        if (tupleIndexVar is not null && !current.IsSingleton
                            && sortTermsPerStep[stepIdx] is not null)
                        {
                            // Tuple-aware sort: sort elements inline while tracking group indices.
                            var sortElems = new List<JsonElement>();
                            var groups = new List<int>();
                            for (int i = 0; i < current.Count; i++)
                            {
                                var el = current[i];
                                if (el.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var item in el.EnumerateArray())
                                    {
                                        sortElems.Add(item);
                                        groups.Add(tupleGroupIndices is not null && i < tupleGroupIndices.Length
                                            ? tupleGroupIndices[i] : i);
                                    }
                                }
                                else
                                {
                                    sortElems.Add(el);
                                    groups.Add(tupleGroupIndices is not null && i < tupleGroupIndices.Length
                                        ? tupleGroupIndices[i] : i);
                                }
                            }

                            if (sortElems.Count > 1)
                            {
                                // Sort index array using the sort terms directly (avoids
                                // round-tripping through JSON which breaks element identity).
                                var terms = sortTermsPerStep[stepIdx]!;
                                var indices = Enumerable.Range(0, sortElems.Count).ToArray();
                                Array.Sort(indices, (a, b) =>
                                {
                                    for (int t = 0; t < terms.Length; t++)
                                    {
                                        var (expr, desc) = terms[t];
                                        var aVal = expr(sortElems[a], env);
                                        var bVal = expr(sortElems[b], env);
                                        int cmp = CompareSortKeys(aVal, bVal);
                                        if (cmp != 0)
                                        {
                                            return desc ? -cmp : cmp;
                                        }
                                    }

                                    return a.CompareTo(b);
                                });

                                // Reorder elements and groups
                                var sortedElems = new List<JsonElement>(sortElems.Count);
                                var sortedGroupArr = new int[sortElems.Count];
                                for (int i = 0; i < indices.Length; i++)
                                {
                                    sortedElems.Add(sortElems[indices[i]]);
                                    sortedGroupArr[i] = groups[indices[i]];
                                }

                                current = new Sequence(JsonataHelpers.ArrayFromList(sortedElems, env.Workspace));
                                tupleGroupIndices = sortedGroupArr;
                            }
                            else if (sortElems.Count == 1)
                            {
                                current = new Sequence(sortElems[0]);
                                tupleGroupIndices = [groups[0]];
                            }
                        }
                        else
                        {
                            var sortElements = CollectFlatElements(current);
                            if (sortElements.Count <= 1)
                            {
                                if (sortElements.Count == 1)
                                {
                                    current = new Sequence(sortElements[0]);
                                }
                            }
                            else
                            {
                                var arrayInput = JsonataHelpers.ArrayFromList(sortElements, env.Workspace);
                                current = step(arrayInput, env);
                            }

                            tupleGroupIndices = null;
                        }

                        // Apply any stages on the sort step itself (e.g. filters after sort)
                        if (stages[stepIdx] is not null)
                        {
                            current = ApplyStages(current, stages[stepIdx]!, stageIsSortFlags[stepIdx]!, env, indexVars[stepIdx]);
                        }

                        // When tuple tracking is active, expand the sorted JSON array to
                        // multi-value so subsequent steps use the multi-value branch where
                        // per-element tuple bindings are restored.
                        if (tupleGroupIndices is not null && current.IsSingleton
                            && current.FirstOrDefault.ValueKind == JsonValueKind.Array)
                        {
                            var arr = current.FirstOrDefault;
                            var expandBuilder = default(SequenceBuilder);
                            foreach (var item in arr.EnumerateArray())
                            {
                                expandBuilder.Add(item);
                            }

                            current = expandBuilder.ToSequence();
                        }

                        continue;
                    }

                    // Auto-flatten arrays when:
                    // - stepIdx > 0: any step after the first always maps over array contexts
                    // - stepIdx == 0 AND isPropertyStep: property access on root array (e.g. name[0] on root array)
                    bool shouldFlatten = stepIdx > 0 || isPropertyStep[stepIdx];

                    // When a singleton contains a JSON array AND this property step has a
                    // boolean filter stage, expand the array to multi-value so filters
                    // operate on individual elements (not the array as a whole).
                    // Only do this for filter stages — sort and index stages need the original arrays.
                    bool hasFilterStage = false;
                    if (stages[stepIdx] is not null && stageIsSortFlags[stepIdx] is not null)
                    {
                        for (int sf = 0; sf < stageIsSortFlags[stepIdx]!.Length; sf++)
                        {
                            if (!stageIsSortFlags[stepIdx]![sf])
                            {
                                hasFilterStage = true;
                                break;
                            }
                        }
                    }

                    if (current.IsSingleton && isPropertyStep[stepIdx] && shouldFlatten
                        && hasFilterStage && current.FirstOrDefault.ValueKind == JsonValueKind.Array)
                    {
                        var arr = current.FirstOrDefault;
                        var expander = default(SequenceBuilder);
                        foreach (var item in arr.EnumerateArray())
                        {
                            expander.Add(item);
                        }

                        current = expander.ToSequence();
                    }

                    // Per-element filter stages are only applied for steps after the first
                    // (stepIdx > 0). At stepIdx 0, the multi-value input comes from auto-mapping
                    // over an array input, and stages should be applied globally (matching JSONata's
                    // semantics where a[0].b picks the first a globally, but $.a[0].b picks per-element).
                    var perElementStages = stepIdx > 0 ? stages[stepIdx] : null;
                    var perElementSortFlags = stepIdx > 0 ? stageIsSortFlags[stepIdx] : null;

                    if (current.IsSingleton)
                    {
                        // Hot path: singleton input → evaluate step directly
                        var element = current.FirstOrDefault;

                        // Bind index variable (0 for singleton)
                        if (indexVar is not null)
                        {
                            env.Bind(indexVar, new Sequence(JsonataHelpers.Zero()));
                        }

                        if (element.ValueKind == JsonValueKind.Array && shouldFlatten)
                        {
                            current = FlattenArrayStep(element, step, env, isConsArrayStep[stepIdx],
                                perElementStages, perElementSortFlags);
                        }
                        else
                        {
                            current = step(element, env);

                            // Apply per-element filter stages (non-sort) to the single result
                            current = ApplyPerElementFilterStages(current,
                                perElementStages, perElementSortFlags, env);
                        }
                    }
                    else
                    {
                        // Multi-value: map step over all values
                        var builder = default(SequenceBuilder);
                        for (int i = 0; i < current.Count; i++)
                        {
                            var element = current[i];

                            // Restore per-element tuple binding (index variable from a
                            // preceding step, preserved through sort reordering).
                            if (tupleGroupIndices is not null && tupleIndexVar is not null
                                && i < tupleGroupIndices.Length)
                            {
                                env.Bind(tupleIndexVar, new Sequence(JsonataHelpers.NumberFromDouble(tupleGroupIndices[i], env.Workspace)));
                            }

                            if (indexVar is not null)
                            {
                                env.Bind(indexVar, new Sequence(JsonataHelpers.NumberFromDouble(i, env.Workspace)));
                            }

                            Sequence stepResult;

                            if (element.ValueKind == JsonValueKind.Array && shouldFlatten)
                            {
                                stepResult = FlattenArrayStep(element, step, env, isConsArrayStep[stepIdx],
                                    perElementStages, perElementSortFlags);
                            }
                            else
                            {
                                stepResult = step(element, env);

                                // Apply per-element filter stages
                                stepResult = ApplyPerElementFilterStages(stepResult,
                                    perElementStages, perElementSortFlags, env);
                            }

                            // Auto-flatten: when a property step returns a single array element
                            // in a multi-context traversal, expand it into individual elements.
                            // This gives JSONata's one-level-deep flattening semantics.
                            if (isPropertyStep[stepIdx] && stepResult.IsSingleton
                                && stepResult.FirstOrDefault.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in stepResult.FirstOrDefault.EnumerateArray())
                                {
                                    builder.Add(item);
                                }
                            }
                            else
                            {
                                builder.AddRange(stepResult);
                            }
                        }

                        current = builder.ToSequence();
                    }

                    // Apply stages globally:
                    // - For stepIdx == 0: all stages (filter + sort) are applied globally
                    // - For stepIdx > 0: only sort stages (filter stages were applied per-element)
                    if (stages[stepIdx] is not null)
                    {
                        if (stepIdx == 0)
                        {
                            current = ApplyStages(current, stages[stepIdx]!, stageIsSortFlags[stepIdx]!, env, indexVars[stepIdx]);
                        }
                        else
                        {
                            current = ApplySortStagesOnly(current, stages[stepIdx]!, stageIsSortFlags[stepIdx]!, env);
                        }
                    }
                }

                // Apply group-by if present (only at the outermost level;
                // focus-bound paths handle group-by inside EvalFocusStep).
                if (startStep == 0 && groupByPairs is not null)
                {
                    current = ApplyGroupBy(current, groupByPairs, env, tupleIndexVar, tupleGroupIndices);
                }

                // When the path has the KeepSingletonArray flag (from [] modifier),
                // ensure singleton results are wrapped in a JSON array.
                // For cons-array steps (array constructors), even singleton arrays must
                // be wrapped — they are "constructed" arrays that should not be unwrapped.
                if (keepSingleton && !current.IsUndefined && current.IsSingleton
                    && (current.FirstOrDefault.ValueKind != JsonValueKind.Array || lastStepIsConsArray))
                {
                    current = MaterializeAsArray(current, env.Workspace);
                }

                return current;
            }

            // Evaluates a focus-bound step (@$var) with cross-join semantics.
            // The step's result elements are bound to the focus variable,
            // and remaining steps evaluate from the parent context (this step's input).
            Sequence EvalFocusStep(Sequence parentContext, int stepIdx)
            {
                var step = steps[stepIdx];
                var focusVar = focusVars[stepIdx]!;
                var indexVar = indexVars[stepIdx];

                // Evaluate the step on the parent context to get focus elements
                Sequence focusResult;
                if (parentContext.IsSingleton)
                {
                    var el = parentContext.FirstOrDefault;
                    if (el.ValueKind == JsonValueKind.Array && (stepIdx > 0 || isPropertyStep[stepIdx]))
                    {
                        focusResult = FlattenArrayStep(el, step, env, isConsArrayStep[stepIdx]);
                    }
                    else
                    {
                        focusResult = step(el, env);
                    }
                }
                else
                {
                    var builder = default(SequenceBuilder);
                    for (int i = 0; i < parentContext.Count; i++)
                    {
                        var el = parentContext[i];
                        if (el.ValueKind == JsonValueKind.Array && (stepIdx > 0 || isPropertyStep[stepIdx]))
                        {
                            var flat = FlattenArrayStep(el, step, env, isConsArrayStep[stepIdx]);
                            builder.AddRange(flat);
                        }
                        else
                        {
                            builder.AddRange(step(el, env));
                        }
                    }

                    focusResult = builder.ToSequence();
                }

                if (focusResult.IsUndefined)
                {
                    return Sequence.Undefined;
                }

                // Expand singleton array to individual elements for per-element binding
                var elements = new List<JsonElement>();
                if (focusResult.IsSingleton)
                {
                    var el = focusResult.FirstOrDefault;
                    if (el.ValueKind == JsonValueKind.Array)
                    {
                        elements.AddRange(el.EnumerateArray());
                    }
                    else
                    {
                        elements.Add(el);
                    }
                }
                else
                {
                    for (int i = 0; i < focusResult.Count; i++)
                    {
                        elements.Add(focusResult[i]);
                    }
                }

                // Determine group-by handling strategy:
                // - If this is the innermost focus level (no more focus steps after),
                //   evaluate group-by here while all focus variables are correctly bound.
                // - If inner focus steps exist, they handle group-by; we merge their results.
                bool hasGroupBy = groupByPairs is not null;
                bool hasInnerFocus = false;
                if (hasGroupBy)
                {
                    for (int k = stepIdx + 1; k < steps.Length; k++)
                    {
                        if (focusVars[k] is not null)
                        {
                            hasInnerFocus = true;
                            break;
                        }
                    }
                }

                bool applyGroupByHere = hasGroupBy && !hasInnerFocus;
                bool mergeInnerGroupBy = hasGroupBy && hasInnerFocus;

                List<string>? groupKeys = applyGroupByHere ? new() : null;
                Dictionary<string, (List<JsonElement> Elements, int PairIndex)>? groupData =
                    applyGroupByHere ? new() : null;
                var mergeObjects = mergeInnerGroupBy ? new List<JsonElement>() : null;

                // For each focus element: bind, apply stages, then evaluate remaining steps
                var resultBuilder = default(SequenceBuilder);
                for (int i = 0; i < elements.Count; i++)
                {
                    var el = elements[i];

                    // Bind focus variable to this element
                    env.Bind(focusVar, new Sequence(el));

                    // Bind index variable if present
                    if (indexVar is not null)
                    {
                        env.Bind(indexVar, new Sequence(JsonataHelpers.NumberFromDouble(i, env.Workspace)));
                    }

                    // Apply stages (filters) per-element
                    if (stages[stepIdx] is not null)
                    {
                        var filtered = ApplyStages(new Sequence(el), stages[stepIdx]!, stageIsSortFlags[stepIdx]!, env, indexVar);
                        if (filtered.IsUndefined)
                        {
                            continue;
                        }
                    }

                    // Evaluate remaining steps from parent context (cross-join)
                    Sequence subResult;
                    if (stepIdx + 1 < steps.Length)
                    {
                        subResult = EvalPathFrom(parentContext, stepIdx + 1);
                    }
                    else
                    {
                        subResult = new Sequence(el);
                    }

                    if (subResult.IsUndefined)
                    {
                        continue;
                    }

                    if (applyGroupByHere)
                    {
                        // Innermost focus — evaluate group-by keys/values now
                        AccumulateGroupBy(subResult, groupByPairs!, groupKeys!, groupData!, env);
                    }
                    else if (mergeInnerGroupBy)
                    {
                        // Inner focus handled group-by — collect objects for merging
                        for (int j = 0; j < subResult.Count; j++)
                        {
                            mergeObjects!.Add(subResult[j]);
                        }
                    }
                    else
                    {
                        resultBuilder.AddRange(subResult);
                    }
                }

                if (applyGroupByHere && groupKeys!.Count > 0)
                {
                    return BuildGroupByResult(groupKeys!, groupData!, groupByPairs!, env);
                }

                if (mergeInnerGroupBy && mergeObjects!.Count > 0)
                {
                    return MergeGroupedObjects(mergeObjects!, env);
                }

                return resultBuilder.ToSequence();
            }

            // Evaluates a step with index binding (#$var) without focus binding.
            // The step's result elements each get their index bound, then remaining
            // steps are evaluated per-element so each carries the correct index.
            Sequence EvalIndexStep(Sequence inputContext, int stepIdx)
            {
                var step = steps[stepIdx];
                var indexVar = indexVars[stepIdx]!;

                // Evaluate the step to get result elements
                bool shouldFlatten = stepIdx > 0 || isPropertyStep[stepIdx];
                Sequence stepResult;
                if (inputContext.IsSingleton)
                {
                    var el = inputContext.FirstOrDefault;
                    if (el.ValueKind == JsonValueKind.Array && shouldFlatten)
                    {
                        stepResult = FlattenArrayStep(el, step, env, isConsArrayStep[stepIdx]);
                    }
                    else
                    {
                        stepResult = step(el, env);
                    }
                }
                else
                {
                    var builder = default(SequenceBuilder);
                    for (int i = 0; i < inputContext.Count; i++)
                    {
                        var el = inputContext[i];
                        if (el.ValueKind == JsonValueKind.Array && shouldFlatten)
                        {
                            var flat = FlattenArrayStep(el, step, env, isConsArrayStep[stepIdx]);
                            builder.AddRange(flat);
                        }
                        else
                        {
                            builder.AddRange(step(el, env));
                        }
                    }

                    stepResult = builder.ToSequence();
                }

                if (stepResult.IsUndefined)
                {
                    return Sequence.Undefined;
                }

                // Expand singleton array to individual elements
                var elements = new List<JsonElement>();
                if (stepResult.IsSingleton)
                {
                    var el = stepResult.FirstOrDefault;
                    if (el.ValueKind == JsonValueKind.Array)
                    {
                        elements.AddRange(el.EnumerateArray());
                    }
                    else
                    {
                        elements.Add(el);
                    }
                }
                else
                {
                    for (int i = 0; i < stepResult.Count; i++)
                    {
                        elements.Add(stepResult[i]);
                    }
                }

                // Determine group-by handling — same logic as EvalFocusStep
                bool hasGroupBy = groupByPairs is not null;
                bool applyGroupByHere = hasGroupBy;
                List<string>? groupKeys = applyGroupByHere ? new() : null;
                Dictionary<string, (List<JsonElement> Elements, int PairIndex)>? groupData =
                    applyGroupByHere ? new() : null;

                // For each element: bind index, apply stages, evaluate remaining steps
                var resultBuilder = default(SequenceBuilder);
                for (int i = 0; i < elements.Count; i++)
                {
                    var el = elements[i];

                    // Bind index variable
                    env.Bind(indexVar, new Sequence(JsonataHelpers.NumberFromDouble(i, env.Workspace)));

                    // Apply stages (filters) per-element
                    if (stages[stepIdx] is not null)
                    {
                        var filtered = ApplyStages(new Sequence(el), stages[stepIdx]!, stageIsSortFlags[stepIdx]!, env, indexVar);
                        if (filtered.IsUndefined)
                        {
                            continue;
                        }
                    }

                    // Evaluate remaining steps on this element (not parent context — unlike focus)
                    Sequence subResult = EvalPathFrom(new Sequence(el), stepIdx + 1);

                    // Handle group-by accumulation
                    if (applyGroupByHere && !subResult.IsUndefined)
                    {
                        AccumulateGroupBy(subResult, groupByPairs!, groupKeys!, groupData!, env);
                        continue;
                    }

                    resultBuilder.AddRange(subResult);
                }

                if (applyGroupByHere && groupKeys!.Count > 0)
                {
                    return BuildGroupByResult(groupKeys!, groupData!, groupByPairs!, env);
                }

                return resultBuilder.ToSequence();
            }
        };
    }

    /// <summary>
    /// Accumulates pre-evaluated group-by key/value pairs from a sub-result sequence.
    /// Both key and value expressions are evaluated NOW while the environment
    /// has correct focus variable bindings.
    /// </summary>
    private static void AccumulateGroupBy(
        Sequence subResult,
        (ExpressionEvaluator Key, ExpressionEvaluator Value)[] pairs,
        List<string> groupKeys,
        Dictionary<string, (List<JsonElement> Values, int PairIndex)> groupData,
        Environment env)
    {
        // Collect individual elements from the sub-result
        var elements = new List<JsonElement>();
        if (subResult.IsSingleton)
        {
            var el = subResult.FirstOrDefault;
            if (el.ValueKind == JsonValueKind.Array)
            {
                elements.AddRange(el.EnumerateArray());
            }
            else
            {
                elements.Add(el);
            }
        }
        else
        {
            for (int i = 0; i < subResult.Count; i++)
            {
                elements.Add(subResult[i]);
            }
        }

        foreach (var element in elements)
        {
            for (int pairIdx = 0; pairIdx < pairs.Length; pairIdx++)
            {
                var keySeq = pairs[pairIdx].Key(element, env);
                if (keySeq.IsUndefined)
                {
                    continue;
                }

                string keyStr;
                if (keySeq.FirstOrDefault.ValueKind == JsonValueKind.String)
                {
                    keyStr = keySeq.FirstOrDefault.GetString()!;
                }
                else if (keySeq.FirstOrDefault.ValueKind == JsonValueKind.Number)
                {
                    throw new JsonataException("T1003", "Key in object structure must evaluate to a string; got: number", 0);
                }
                else
                {
                    keyStr = CoerceElementToString(keySeq.FirstOrDefault);
                }

                // Evaluate the value expression NOW while focus bindings are correct
                var valueSeq = pairs[pairIdx].Value(element, env);

                if (!groupData.TryGetValue(keyStr, out var entry))
                {
                    entry = ([], pairIdx);
                    groupData[keyStr] = entry;
                    groupKeys.Add(keyStr);
                }

                // Accumulate the evaluated value (not the raw element)
                if (!valueSeq.IsUndefined)
                {
                    if (valueSeq.IsSingleton)
                    {
                        entry.Values.Add(valueSeq.FirstOrDefault);
                    }
                    else
                    {
                        for (int v = 0; v < valueSeq.Count; v++)
                        {
                            entry.Values.Add(valueSeq[v]);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Builds the final group-by object from pre-evaluated key/value data.
    /// </summary>
    private static Sequence BuildGroupByResult(
        List<string> groupKeys,
        Dictionary<string, (List<JsonElement> Values, int PairIndex)> groupData,
        (ExpressionEvaluator Key, ExpressionEvaluator Value)[] pairs,
        Environment env)
    {
        JsonDocumentBuilder<JsonElement.Mutable> objDoc = JsonElement.CreateObjectBuilder(env.Workspace, groupKeys.Count);
        JsonElement.Mutable objRoot = objDoc.RootElement;

        foreach (var key in groupKeys)
        {
            var (values, _) = groupData[key];

            if (values.Count == 1)
            {
                objRoot.SetProperty(key, values[0]);
            }
            else if (values.Count > 1)
            {
                objRoot.SetProperty(key, JsonataHelpers.ArrayFromList(values, env.Workspace));
            }
        }

        return new Sequence((JsonElement)objRoot);
    }

    /// <summary>
    /// Merges multiple grouped objects (from nested focus binding iterations)
    /// into a single object. Same-key values are collected into arrays.
    /// </summary>
    private static Sequence MergeGroupedObjects(List<JsonElement> objects, Environment env)
    {
        var keys = new List<string>();
        var merged = new Dictionary<string, List<JsonElement>>();

        foreach (var obj in objects)
        {
            if (obj.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var prop in obj.EnumerateObject())
            {
                if (!merged.TryGetValue(prop.Name, out var values))
                {
                    values = [];
                    merged[prop.Name] = values;
                    keys.Add(prop.Name);
                }

                values.Add(prop.Value);
            }
        }

        JsonDocumentBuilder<JsonElement.Mutable> objDoc = JsonElement.CreateObjectBuilder(env.Workspace, keys.Count);
        JsonElement.Mutable objRoot = objDoc.RootElement;

        foreach (var key in keys)
        {
            var values = merged[key];
            if (values.Count == 1)
            {
                objRoot.SetProperty(key, values[0]);
            }
            else
            {
                objRoot.SetProperty(key, JsonataHelpers.ArrayFromList(values, env.Workspace));
            }
        }

        return new Sequence((JsonElement)objRoot);
    }

    private static Sequence ApplyGroupBy(
        Sequence current,
        (ExpressionEvaluator Key, ExpressionEvaluator Value)[] pairs,
        Environment env,
        string? tupleIndexVar = null,
        int[]? tupleGroupIndices = null)
    {
        if (current.IsUndefined)
        {
            return Sequence.Undefined;
        }

        // Collect all elements to iterate — flatten arrays in the sequence,
        // preserving tuple group indices when available.
        var elements = new List<JsonElement>();
        int[]? elementGroupIndices = null;

        if (tupleGroupIndices is not null && tupleIndexVar is not null)
        {
            elementGroupIndices = tupleGroupIndices;

            // Elements should already be individual (expanded by sort handler)
            for (int i = 0; i < current.Count; i++)
            {
                elements.Add(current[i]);
            }
        }
        else
        {
            for (int i = 0; i < current.Count; i++)
            {
                var el = current[i];
                if (el.ValueKind == JsonValueKind.Array)
                {
                    elements.AddRange(el.EnumerateArray());
                }
                else
                {
                    elements.Add(el);
                }
            }
        }

        // Phase 1: Evaluate KEY per element to determine grouping.
        // Collect the grouped elements (not values) per key, tracking which
        // pair expression produced each key.
        var groupKeys = new List<string>();
        var groupData = new Dictionary<string, (List<JsonElement> Elements, int PairIndex)>();

        for (int idx = 0; idx < elements.Count; idx++)
        {
            var element = elements[idx];

            // Restore per-element tuple binding before evaluating key expressions
            if (elementGroupIndices is not null && tupleIndexVar is not null
                && idx < elementGroupIndices.Length)
            {
                env.Bind(tupleIndexVar, new Sequence(JsonataHelpers.NumberFromDouble(elementGroupIndices[idx], env.Workspace)));
            }

            for (int pairIdx = 0; pairIdx < pairs.Length; pairIdx++)
            {
                var keySeq = pairs[pairIdx].Key(element, env);

                if (keySeq.IsUndefined)
                {
                    continue;
                }

                string keyStr;
                if (keySeq.FirstOrDefault.ValueKind == JsonValueKind.String)
                {
                    keyStr = keySeq.FirstOrDefault.GetString()!;
                }
                else if (keySeq.FirstOrDefault.ValueKind == JsonValueKind.Number)
                {
                    throw new JsonataException("T1003", "Key in object structure must evaluate to a string; got: number", 0);
                }
                else
                {
                    keyStr = CoerceElementToString(keySeq.FirstOrDefault);
                }

                if (!groupData.TryGetValue(keyStr, out var entry))
                {
                    entry = ([], pairIdx);
                    groupData[keyStr] = entry;
                    groupKeys.Add(keyStr);
                }
                else if (entry.PairIndex != pairIdx)
                {
                    throw new JsonataException(
                        "D1009",
                        $"Multiple key definitions evaluate to same key: \"{keyStr}\"",
                        0);
                }

                entry.Elements.Add(element);
            }
        }

        // Phase 2: For each group, evaluate the VALUE expression once with
        // the collected group elements as context. This matches the reference
        // JSONata behaviour where the value expression sees the group, not
        // individual elements.
        JsonDocumentBuilder<JsonElement.Mutable> objDoc = JsonElement.CreateObjectBuilder(env.Workspace, groupKeys.Count);
        JsonElement.Mutable objRoot = objDoc.RootElement;

        foreach (var key in groupKeys)
        {
            var (groupElements, pairIndex) = groupData[key];
            var valueEval = pairs[pairIndex].Value;

            // Build the context: single element or JSON array of elements
            JsonElement context;
            if (groupElements.Count == 1)
            {
                context = groupElements[0];
            }
            else
            {
                context = JsonataHelpers.ArrayFromList(groupElements, env.Workspace);
            }

            var valSeq = valueEval(context, env);
            if (valSeq.IsUndefined)
            {
                continue;
            }

            if (valSeq.IsSingleton)
            {
                objRoot.SetProperty(key, valSeq.FirstOrDefault);
            }
            else
            {
                objRoot.SetProperty(key, JsonataHelpers.ArrayFromSequence(valSeq, env.Workspace));
            }
        }

        return new Sequence((JsonElement)objRoot);
    }

    private static Sequence ApplyStages(Sequence current, ExpressionEvaluator[] stageEvaluators, bool[] isSortStage, Environment env, string? indexVar = null)
    {
        for (int s = 0; s < stageEvaluators.Length; s++)
        {
            if (current.IsUndefined)
            {
                return Sequence.Undefined;
            }

            var stage = stageEvaluators[s];

            // Collect all elements into a working set
            var elements = new List<JsonElement>();
            if (current.IsSingleton)
            {
                var el = current.FirstOrDefault;
                if (el.ValueKind == JsonValueKind.Array)
                {
                    elements.AddRange(el.EnumerateArray());
                }
                else
                {
                    elements.Add(el);
                }
            }
            else
            {
                for (int i = 0; i < current.Count; i++)
                {
                    elements.Add(current[i]);
                }
            }

            if (isSortStage[s])
            {
                // In path contexts, property steps can produce arrays of arrays
                // (e.g. Account.Order.Product yields [productArray1, productArray2]).
                // Flatten one level so the sort sees individual elements.
                var sortElements = new List<JsonElement>(elements.Count * 2);
                foreach (var el in elements)
                {
                    if (el.ValueKind == JsonValueKind.Array)
                    {
                        sortElements.AddRange(el.EnumerateArray());
                    }
                    else
                    {
                        sortElements.Add(el);
                    }
                }

                // Sort stages need all elements at once — build a JSON array
                // and pass it to the sort evaluator.
                if (sortElements.Count <= 1)
                {
                    if (sortElements.Count == 1)
                    {
                        current = new Sequence(sortElements[0]);
                    }
                }
                else
                {
                    var arrayInput = JsonataHelpers.ArrayFromList(sortElements, env.Workspace);
                    current = stage(arrayInput, env);
                }

                continue;
            }

            // Apply the stage predicate to each element
            var builder = default(SequenceBuilder);
            for (int i = 0; i < elements.Count; i++)
            {
                var el = elements[i];

                // When index binding is active, update the index variable per-element
                // so predicates like [$pos<3] see the correct position.
                if (indexVar is not null)
                {
                    env.Bind(indexVar, new Sequence(JsonataHelpers.NumberFromDouble(i, env.Workspace)));
                }

                var result = stage(el, env);

                if (result.IsUndefined)
                {
                    continue;
                }

                var firstResult = result.FirstOrDefault;

                // Boolean result takes priority — don't coerce to numeric index
                if (result.IsSingleton && firstResult.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    if (firstResult.ValueKind == JsonValueKind.True)
                    {
                        builder.Add(el);
                    }

                    continue;
                }

                // Collect numeric indices from the result (single number, multi-value, or array of numbers)
                bool isNumericIndices = false;
                List<int>? indices = null;

                if (result.IsSingleton && TryCoerceToNumber(firstResult, out double singleIdx))
                {
                    // Single numeric → treat as index array of one element
                    isNumericIndices = true;
                    int idx = (int)Math.Floor(singleIdx);
                    if (idx < 0)
                    {
                        idx = elements.Count + idx;
                    }

                    indices = new List<int> { idx };
                }
                else if (result.IsSingleton && firstResult.ValueKind == JsonValueKind.Array)
                {
                    // Singleton JSON array — check if all elements are numbers
                    bool allNum = true;
                    var tempIndices = new List<int>();
                    foreach (var arrEl in firstResult.EnumerateArray())
                    {
                        // Booleans in a predicate array are NOT numeric indices;
                        // their presence makes this a mixed predicate that falls
                        // through to truthiness evaluation.
                        if (arrEl.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        {
                            allNum = false;
                            break;
                        }

                        if (TryCoerceToNumber(arrEl, out double numVal))
                        {
                            int idx = (int)Math.Floor(numVal);
                            if (idx < 0)
                            {
                                idx = elements.Count + idx;
                            }

                            tempIndices.Add(idx);
                        }
                        else
                        {
                            allNum = false;
                            break;
                        }
                    }

                    if (allNum && tempIndices.Count > 0)
                    {
                        isNumericIndices = true;
                        indices = tempIndices;
                    }
                }
                else if (!result.IsSingleton && result.Count > 0)
                {
                    // Multi-value sequence — check if all are numeric
                    bool allNum = true;
                    var tempIndices = new List<int>();
                    for (int j = 0; j < result.Count; j++)
                    {
                        var rj = result[j];
                        if (rj.ValueKind is JsonValueKind.True or JsonValueKind.False || !TryCoerceToNumber(rj, out double numVal))
                        {
                            allNum = false;
                            break;
                        }

                        int idx = (int)Math.Floor(numVal);
                        if (idx < 0)
                        {
                            idx = elements.Count + idx;
                        }

                        tempIndices.Add(idx);
                    }

                    if (allNum)
                    {
                        isNumericIndices = true;
                        indices = tempIndices;
                    }
                }

                if (isNumericIndices && indices is not null)
                {
                    // JSONata semantics: include the element if the current loop index
                    // matches any of the numeric indices. This naturally deduplicates.
                    foreach (int idx in indices)
                    {
                        if (idx == i)
                        {
                            builder.Add(el);
                            break;
                        }
                    }

                    continue;
                }

                // Boolean filter
                if (IsTruthy(result))
                {
                    builder.Add(el);
                }
            }

            current = builder.ToSequence();
        }

        return current;
    }

    private static StepAnnotations? GetStepAnnotations(JsonataNode node)
    {
        return node.Annotations;
    }

    /// <summary>
    /// Collects all elements from a sequence, flattening one level of nested arrays.
    /// Used before sort operations to gather individual items from multi-value path results.
    /// </summary>
    private static List<JsonElement> CollectFlatElements(Sequence current)
    {
        var result = new List<JsonElement>();
        if (current.IsSingleton)
        {
            var el = current.FirstOrDefault;
            if (el.ValueKind == JsonValueKind.Array)
            {
                result.AddRange(el.EnumerateArray());
            }
            else
            {
                result.Add(el);
            }
        }
        else
        {
            for (int i = 0; i < current.Count; i++)
            {
                var el = current[i];
                if (el.ValueKind == JsonValueKind.Array)
                {
                    result.AddRange(el.EnumerateArray());
                }
                else
                {
                    result.Add(el);
                }
            }
        }

        return result;
    }

    private static Sequence FlattenArrayStep(
        JsonElement array,
        ExpressionEvaluator step,
        Environment env,
        bool isConsArray = false,
        ExpressionEvaluator[]? filterStages = null,
        bool[]? stageIsSortFlags = null)
    {
        var builder = default(SequenceBuilder);
        foreach (var item in array.EnumerateArray())
        {
            var result = step(item, env);

            // Apply per-element filter stages before flattening
            result = ApplyPerElementFilterStages(result, filterStages, stageIsSortFlags, env);

            if (result.IsUndefined)
            {
                continue;
            }

            // Auto-flatten: when a property step returns a JSON array from an element,
            // expand it into individual elements (JSONata's one-level flattening).
            // Constructed-array steps (isConsArray) skip this — their results are
            // "cons" arrays that must be preserved as individual elements.
            if (!isConsArray && result.IsSingleton && result.FirstOrDefault.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in result.FirstOrDefault.EnumerateArray())
                {
                    builder.Add(child);
                }
            }
            else
            {
                builder.AddRange(result);
            }
        }

        return builder.ToSequence();
    }

    /// <summary>
    /// Applies only non-sort (filter) stages to a per-element result.
    /// In JSONata, filter predicates are applied per-input-element inside the
    /// path step evaluation loop, not globally after all elements are collected.
    /// </summary>
    private static Sequence ApplyPerElementFilterStages(
        Sequence result,
        ExpressionEvaluator[]? stageEvaluators,
        bool[]? isSortStage,
        Environment env)
    {
        if (stageEvaluators is null || result.IsUndefined)
        {
            return result;
        }

        for (int s = 0; s < stageEvaluators.Length; s++)
        {
            if (isSortStage?.Length > s && isSortStage[s])
            {
                continue; // Sort stages are applied globally, not per-element
            }

            if (result.IsUndefined)
            {
                return Sequence.Undefined;
            }

            result = ApplyStages(result, [stageEvaluators[s]], [false], env);
        }

        return result;
    }

    /// <summary>
    /// Applies only sort stages from the stage array. Filter stages have already
    /// been applied per-element during path step evaluation.
    /// </summary>
    private static Sequence ApplySortStagesOnly(
        Sequence current,
        ExpressionEvaluator[] stageEvaluators,
        bool[] isSortStage,
        Environment env)
    {
        bool hasSortStage = false;
        for (int s = 0; s < isSortStage.Length; s++)
        {
            if (isSortStage[s])
            {
                hasSortStage = true;
                break;
            }
        }

        if (!hasSortStage)
        {
            return current;
        }

        // Build arrays containing only sort stages
        var sortStages = new List<ExpressionEvaluator>();
        var sortFlags = new List<bool>();
        for (int s = 0; s < stageEvaluators.Length; s++)
        {
            if (isSortStage[s])
            {
                sortStages.Add(stageEvaluators[s]);
                sortFlags.Add(true);
            }
        }

        return ApplyStages(current, sortStages.ToArray(), sortFlags.ToArray(), env);
    }

    private static ExpressionEvaluator CompileBinary(BinaryNode binary)
    {
        var lhs = Compile(binary.Lhs);
        var rhs = Compile(binary.Rhs);
        var op = binary.Operator;

        return op switch
        {
            "+" => CompileArithmetic(lhs, rhs, static (a, b) => a + b),
            "-" => CompileArithmetic(lhs, rhs, static (a, b) => a - b),
            "*" => CompileArithmetic(lhs, rhs, static (a, b) => a * b),
            "/" => CompileArithmetic(lhs, rhs, static (a, b) => a / b),
            "%" => CompileArithmetic(lhs, rhs, static (a, b) => a % b),
            "=" => CompileComparison(lhs, rhs),
            "!=" => CompileNotEqual(lhs, rhs),
            "<" => CompileNumericComparison(lhs, rhs, static (a, b) => a < b),
            "<=" => CompileNumericComparison(lhs, rhs, static (a, b) => a <= b),
            ">" => CompileNumericComparison(lhs, rhs, static (a, b) => a > b),
            ">=" => CompileNumericComparison(lhs, rhs, static (a, b) => a >= b),
            "&" => CompileStringConcat(lhs, rhs),
            "and" => CompileAnd(lhs, rhs),
            "or" => CompileOr(lhs, rhs),
            "in" => CompileIn(lhs, rhs),
            ".." => CompileRange(lhs, rhs),
            _ => throw new JsonataException("D1001", $"Unknown binary operator: {op}", binary.Position),
        };
    }

    private static ExpressionEvaluator CompileArithmetic(
        ExpressionEvaluator lhs,
        ExpressionEvaluator rhs,
        Func<double, double, double> op)
    {
        return (in JsonElement input, Environment env) =>
        {
            var left = lhs(input, env);
            var right = rhs(input, env);

            // Check for non-number types before checking for undefined,
            // so that e.g. `false + $x` throws T2001 rather than returning undefined.
            // Non-finite values (Infinity/NaN) throw D1001 when used as an operand —
            // matching JSONata-js isNumeric() which throws D1001 for !isFinite(n).
            if (!left.IsUndefined)
            {
                if (left.IsNonFinite)
                {
                    throw new JsonataException("D1001", $"Number out of range: {left.NonFiniteValue}", 0);
                }

                if (left.FirstOrDefault.ValueKind != JsonValueKind.Number)
                {
                    throw new JsonataException("T2001", "The left side of the arithmetic expression is not a number", 0);
                }
            }

            if (!right.IsUndefined)
            {
                if (right.IsNonFinite)
                {
                    throw new JsonataException("D1001", $"Number out of range: {right.NonFiniteValue}", 0);
                }

                if (right.FirstOrDefault.ValueKind != JsonValueKind.Number)
                {
                    throw new JsonataException("T2002", "The right side of the arithmetic expression is not a number", 0);
                }
            }

            if (left.IsUndefined || right.IsUndefined)
            {
                return Sequence.Undefined;
            }

            double leftNum = left.FirstOrDefault.GetDouble();
            double rightNum = right.FirstOrDefault.GetDouble();

            double result = op(leftNum, rightNum);
            if (double.IsInfinity(result) || double.IsNaN(result))
            {
                return new Sequence(result);
            }

            return new Sequence(JsonataHelpers.NumberFromDouble(result, env.Workspace));        };
    }

    private static ExpressionEvaluator CompileComparison(ExpressionEvaluator lhs, ExpressionEvaluator rhs)
    {
        return (in JsonElement input, Environment env) =>
        {
            var left = lhs(input, env);
            var right = rhs(input, env);

            // undefined = undefined → false (undefined is not equal to anything, even itself)
            if (left.IsUndefined || right.IsUndefined)
            {
                return new Sequence(JsonataHelpers.BooleanElement(false));
            }

            bool result = JsonElementEquals(left.FirstOrDefault, right.FirstOrDefault);
            return new Sequence(JsonataHelpers.BooleanElement(result));
        };
    }

    private static ExpressionEvaluator CompileNotEqual(ExpressionEvaluator lhs, ExpressionEvaluator rhs)
    {
        var eq = CompileComparison(lhs, rhs);
        return (in JsonElement input, Environment env) =>
        {
            var result = eq(input, env);
            if (result.IsUndefined)
            {
                return Sequence.Undefined;
            }

            bool val = result.FirstOrDefault.ValueKind == JsonValueKind.True;
            return new Sequence(JsonataHelpers.BooleanElement(!val));
        };
    }

    private static ExpressionEvaluator CompileNumericComparison(
        ExpressionEvaluator lhs,
        ExpressionEvaluator rhs,
        Func<double, double, bool> cmp)
    {
        return (in JsonElement input, Environment env) =>
        {
            var left = lhs(input, env);
            var right = rhs(input, env);

            // Check for invalid types BEFORE undefined check — boolean/null in comparison is always an error
            if (!left.IsUndefined)
            {
                var l = left.FirstOrDefault;
                if (l.ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null
                    or JsonValueKind.Array or JsonValueKind.Object)
                {
                    throw new JsonataException("T2010", "The expressions either side of operator must be both numbers or both strings", 0);
                }
            }

            if (!right.IsUndefined)
            {
                var r = right.FirstOrDefault;
                if (r.ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null
                    or JsonValueKind.Array or JsonValueKind.Object)
                {
                    throw new JsonataException("T2010", "The expressions either side of operator must be both numbers or both strings", 0);
                }
            }

            if (left.IsUndefined || right.IsUndefined)
            {
                return Sequence.Undefined;
            }

            var lv = left.FirstOrDefault;
            var rv = right.FirstOrDefault;

            // String comparison
            if (lv.ValueKind == JsonValueKind.String && rv.ValueKind == JsonValueKind.String)
            {
                int result = Utf8CompareOrdinal(lv, rv);
                return new Sequence(JsonataHelpers.BooleanElement(cmp(result, 0)));
            }

            // Numeric comparison — both must be numbers
            if (lv.ValueKind == JsonValueKind.Number && rv.ValueKind == JsonValueKind.Number)
            {
                if (TryCoerceToNumber(lv, out double leftNum) && TryCoerceToNumber(rv, out double rightNum))
                {
                    return new Sequence(JsonataHelpers.BooleanElement(cmp(leftNum, rightNum)));
                }
            }

            // Type mismatch (e.g. string vs number)
            throw new JsonataException("T2009", "The values either side of the operator must be of the same data type", 0);
        };
    }

    private static ExpressionEvaluator CompileStringConcat(ExpressionEvaluator lhs, ExpressionEvaluator rhs)
    {
        return (in JsonElement input, Environment env) =>
        {
            var left = lhs(input, env);
            var right = rhs(input, env);

            // Use a rented buffer to build the quoted UTF-8 string directly,
            // avoiding .NET string allocations entirely (like JsonLogic CompileCat).
            byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
            int pos = 0;
            buffer[pos++] = (byte)'"';

            AppendCoercedToBuffer(left, ref buffer, ref pos);
            AppendCoercedToBuffer(right, ref buffer, ref pos);

            JsonataHelpers.GrowBufferIfNeeded(ref buffer, pos, 1);
            buffer[pos++] = (byte)'"';

            JsonElement result = JsonataHelpers.StringFromQuotedUtf8Span(
                new ReadOnlySpan<byte>(buffer, 0, pos), env.Workspace);
            ArrayPool<byte>.Shared.Return(buffer);
            return new Sequence(result);
        };
    }

    /// <summary>
    /// Appends the UTF-8 coercion of a sequence to a rented byte buffer.
    /// For multi-valued sequences, writes the JSON array representation.
    /// Numbers use JSONata's FormatNumberLikeJavaScript for precision-15 formatting.
    /// </summary>
    private static void AppendCoercedToBuffer(Sequence seq, ref byte[] buffer, ref int pos)
    {
        if (seq.IsUndefined)
        {
            return;
        }

        if (seq.Count > 1)
        {
            // Multi-valued sequences are stringified as JSON arrays: [elem1,elem2,...]
            JsonataHelpers.GrowBufferIfNeeded(ref buffer, pos, 1);
            buffer[pos++] = (byte)'[';
            for (int i = 0; i < seq.Count; i++)
            {
                if (i > 0)
                {
                    JsonataHelpers.GrowBufferIfNeeded(ref buffer, pos, 1);
                    buffer[pos++] = (byte)',';
                }

                AppendElementRawTextToBuffer(seq[i], ref buffer, ref pos);
            }

            JsonataHelpers.GrowBufferIfNeeded(ref buffer, pos, 1);
            buffer[pos++] = (byte)']';
            return;
        }

        AppendCoercedElementToBuffer(seq.FirstOrDefault, ref buffer, ref pos);
    }

    /// <summary>
    /// Appends the coerced UTF-8 representation of a single element.
    /// Numbers go through FormatNumberLikeJavaScript for G15 precision formatting.
    /// Arrays and objects use their raw JSON text representation.
    /// </summary>
    private static void AppendCoercedElementToBuffer(JsonElement elem, ref byte[] buffer, ref int pos)
    {
        if (elem.ValueKind == JsonValueKind.Undefined)
        {
            return;
        }

        if (elem.ValueKind == JsonValueKind.Number)
        {
            // Use FormatNumberLikeJavaScript for proper precision-15 formatting.
            // For integers with clean raw representation, this just copies the raw bytes.
            // For non-integers, it applies G15 to clean up IEEE noise.
            string formatted = FormatNumberLikeJavaScript(elem);
            JsonataHelpers.GrowBufferIfNeeded(ref buffer, pos, formatted.Length);
            for (int i = 0; i < formatted.Length; i++)
            {
                buffer[pos++] = (byte)formatted[i]; // All number chars are ASCII
            }

            return;
        }

        if (elem.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
        {
            // Arrays and objects are serialized as their raw JSON text
            AppendElementRawTextToBuffer(elem, ref buffer, ref pos);
            return;
        }

        // For strings, booleans, null — delegate to JsonataHelpers
        JsonataHelpers.AppendCoercedToBuffer(elem, ref buffer, ref pos);
    }

    /// <summary>
    /// Appends the raw JSON text of an element to a rented byte buffer.
    /// This is used for non-string values within array coercion.
    /// </summary>
    private static void AppendElementRawTextToBuffer(JsonElement element, ref byte[] buffer, ref int pos)
    {
        using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(element);
        ReadOnlySpan<byte> span = raw.Span;
        JsonataHelpers.GrowBufferIfNeeded(ref buffer, pos, span.Length);
        span.CopyTo(buffer.AsSpan(pos));
        pos += span.Length;
    }

    private static ExpressionEvaluator CompileAnd(ExpressionEvaluator lhs, ExpressionEvaluator rhs)
    {
        return (in JsonElement input, Environment env) =>
        {
            var left = lhs(input, env);
            if (!IsTruthy(left))
            {
                return new Sequence(JsonataHelpers.BooleanElement(false));
            }

            var right = rhs(input, env);
            return new Sequence(JsonataHelpers.BooleanElement(IsTruthy(right)));
        };
    }

    private static ExpressionEvaluator CompileOr(ExpressionEvaluator lhs, ExpressionEvaluator rhs)
    {
        return (in JsonElement input, Environment env) =>
        {
            var left = lhs(input, env);
            if (IsTruthy(left))
            {
                return new Sequence(JsonataHelpers.BooleanElement(true));
            }

            var right = rhs(input, env);
            return new Sequence(JsonataHelpers.BooleanElement(IsTruthy(right)));
        };
    }

    private static ExpressionEvaluator CompileIn(ExpressionEvaluator lhs, ExpressionEvaluator rhs)
    {
        return (in JsonElement input, Environment env) =>
        {
            var left = lhs(input, env);
            var right = rhs(input, env);

            if (left.IsUndefined || right.IsUndefined)
            {
                return new Sequence(JsonataHelpers.BooleanElement(false));
            }

            var needle = left.FirstOrDefault;
            var haystack = right.FirstOrDefault;

            if (haystack.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in haystack.EnumerateArray())
                {
                    if (JsonElementEquals(needle, item))
                    {
                        return new Sequence(JsonataHelpers.BooleanElement(true));
                    }
                }

                return new Sequence(JsonataHelpers.BooleanElement(false));
            }

            // String containment: "hello" in "hello world" → true
            if (needle.ValueKind == JsonValueKind.String && haystack.ValueKind == JsonValueKind.String)
            {
                string needleStr = needle.GetString()!;
                string haystackStr = haystack.GetString()!;
                return new Sequence(JsonataHelpers.BooleanElement(haystackStr.Contains(needleStr)));
            }

            // Scalar equality check
            return new Sequence(JsonataHelpers.BooleanElement(JsonElementEquals(needle, haystack)));
        };
    }

    private static ExpressionEvaluator CompileRange(ExpressionEvaluator lhs, ExpressionEvaluator rhs)
    {
        return (in JsonElement input, Environment env) =>
        {
            var left = lhs(input, env);
            var right = rhs(input, env);

            // Type validation first — even if undefined, the other operand's type matters
            if (!left.IsUndefined)
            {
                var leftElem = left.FirstOrDefault;
                if (leftElem.ValueKind != JsonValueKind.Number)
                {
                    throw new JsonataException("T2003", "The left side of the range operator (..) must evaluate to an integer", 0);
                }
            }

            if (!right.IsUndefined)
            {
                var rightElem = right.FirstOrDefault;
                if (rightElem.ValueKind != JsonValueKind.Number)
                {
                    throw new JsonataException("T2004", "The right side of the range operator (..) must evaluate to an integer", 0);
                }
            }

            if (left.IsUndefined || right.IsUndefined)
            {
                return Sequence.Undefined;
            }

            double start = left.FirstOrDefault.GetDouble();
            double end = right.FirstOrDefault.GetDouble();

            // Must be integer values
            if (start != Math.Floor(start))
            {
                throw new JsonataException("T2003", "The left side of the range operator (..) must evaluate to an integer", 0);
            }

            if (end != Math.Floor(end))
            {
                throw new JsonataException("T2004", "The right side of the range operator (..) must evaluate to an integer", 0);
            }

            int iStart = (int)start;
            int iEnd = (int)end;

            if (iStart > iEnd)
            {
                return Sequence.Undefined;
            }

            long count = (long)iEnd - (long)iStart + 1;
            if (count > 10_000_000)
            {
                throw new JsonataException("D2014", "Range expression generates too many results", 0);
            }

            // Build individual number elements for the range
            var elements = new JsonElement[(int)count];
            for (int i = iStart; i <= iEnd; i++)
            {
                elements[i - iStart] = JsonataHelpers.NumberFromDouble(i, env.Workspace);
            }

            return new Sequence(elements, (int)count);
        };
    }

    private static ExpressionEvaluator CompileUnary(UnaryNode unary)
    {
        var expr = Compile(unary.Expression);
        if (unary.Operator == "-")
        {
            return (in JsonElement input, Environment env) =>
            {
                var result = expr(input, env);
                if (result.IsUndefined)
                {
                    return Sequence.Undefined;
                }

                var el = result.FirstOrDefault;
                if (el.ValueKind == JsonValueKind.Number)
                {
                    return new Sequence(JsonataHelpers.NumberFromDouble(-el.GetDouble(), env.Workspace));
                }

                throw new JsonataException("D1002", "Cannot negate a non-numeric value", unary.Position);
            };
        }

        throw new JsonataException("D1001", $"Unknown unary operator: {unary.Operator}", unary.Position);
    }

    private static ExpressionEvaluator CompileBlock(BlockNode block)
    {
        var exprs = block.Expressions.Select(Compile).ToArray();
        return (in JsonElement input, Environment env) =>
        {
            var childEnv = env.CreateChild();
            Sequence result = Sequence.Undefined;
            foreach (var expr in exprs)
            {
                result = expr(input, childEnv);
            }

            return result;
        };
    }

    private static ExpressionEvaluator CompileCondition(ConditionNode cond)
    {
        var condition = Compile(cond.Condition);
        var then = Compile(cond.Then);
        var @else = cond.Else is not null ? Compile(cond.Else) : null;

        return (in JsonElement input, Environment env) =>
        {
            var condResult = condition(input, env);
            if (IsTruthy(condResult))
            {
                return then(input, env);
            }

            return @else is not null ? @else(input, env) : Sequence.Undefined;
        };
    }

    private static ExpressionEvaluator CompileArrayConstructor(ArrayConstructorNode arr)
    {
        // Track which sub-expressions are array constructors (pushed as single elements)
        // vs other expressions (flattened — individual elements appended)
        var items = arr.Expressions.Select(e => (Eval: Compile(e), IsArrayCtor: e is ArrayConstructorNode)).ToArray();
        return (in JsonElement input, Environment env) =>
        {
            JsonDocumentBuilder<JsonElement.Mutable> arrDoc = JsonElement.CreateArrayBuilder(env.Workspace, items.Length);
            JsonElement.Mutable arrRoot = arrDoc.RootElement;

            foreach (var (eval, isArrayCtor) in items)
            {
                var result = eval(input, env);
                if (result.IsUndefined)
                {
                    continue;
                }

                if (isArrayCtor)
                {
                    // Array constructor sub-expression: push result as single element
                    for (int i = 0; i < result.Count; i++)
                    {
                        arrRoot.AddItem(result[i]);
                    }
                }
                else
                {
                    // Other expressions: equivalent to JSONata's append (one-level concat).
                    // Singleton array results are flattened (matching how evaluate() unwraps
                    // singletons and append/concat spreads one level). Multi-value results
                    // have each element written as-is (sub-arrays from nested constructors
                    // are preserved, matching how concat doesn't recurse into elements).
                    if (result.IsSingleton)
                    {
                        var item = result.FirstOrDefault;
                        if (item.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var child in item.EnumerateArray())
                            {
                                arrRoot.AddItem(child);
                            }
                        }
                        else
                        {
                            arrRoot.AddItem(item);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < result.Count; i++)
                        {
                            arrRoot.AddItem(result[i]);
                        }
                    }
                }
            }

            return new Sequence((JsonElement)arrRoot);
        };
    }

    private static ExpressionEvaluator CompileObjectConstructor(ObjectConstructorNode obj)
    {
        var pairs = obj.Pairs.Select(p => (Key: Compile(p.Key), Value: Compile(p.Value))).ToArray();
        bool hasMuliPlePairs = pairs.Length > 1;
        return (in JsonElement input, Environment env) =>
        {
            JsonDocumentBuilder<JsonElement.Mutable> objDoc = JsonElement.CreateObjectBuilder(env.Workspace, pairs.Length);
            JsonElement.Mutable objRoot = objDoc.RootElement;

            // Track which pair index produced each key to detect D1009 duplicates
            Dictionary<string, int>? seenKeys = hasMuliPlePairs ? new() : null;

            for (int pairIdx = 0; pairIdx < pairs.Length; pairIdx++)
            {
                var (key, value) = pairs[pairIdx];
                var keyResult = key(input, env);
                var valueResult = value(input, env);

                if (keyResult.IsUndefined)
                {
                    continue;
                }

                // T1003: key must evaluate to a string
                if (keyResult.Count > 1
                    || keyResult.FirstOrDefault.ValueKind is not JsonValueKind.String)
                {
                    string gotType = keyResult.Count > 1
                        ? "array"
                        : keyResult.FirstOrDefault.ValueKind.ToString().ToLowerInvariant();
                    throw new JsonataException(
                        "T1003",
                        $"Key in object structure must evaluate to a string; got: {gotType}",
                        0);
                }

                string keyStr = keyResult.FirstOrDefault.GetString()!;

                // D1009: duplicate key from different pair expressions
                if (seenKeys is not null)
                {
                    if (seenKeys.TryGetValue(keyStr, out int prevPairIdx))
                    {
                        if (prevPairIdx != pairIdx)
                        {
                            throw new JsonataException(
                                "D1009",
                                $"Multiple key definitions evaluate to same key: \"{keyStr}\"",
                                0);
                        }
                    }
                    else
                    {
                        seenKeys[keyStr] = pairIdx;
                    }
                }

                if (valueResult.IsLambda)
                {
                    // Lambda/function values serialize as empty strings in JSON
                    objRoot.SetProperty(keyStr, JsonataHelpers.EmptyString());
                    continue;
                }

                if (valueResult.IsUndefined)
                {
                    // Skip undefined values — they don't produce a key in the output
                    continue;
                }

                // Non-finite numeric values (Infinity/NaN) cannot be stored in JSON
                if (valueResult.IsNonFinite)
                {
                    throw new JsonataException("D1001", $"Number out of range: {valueResult.NonFiniteValue}", 0);
                }

                if (valueResult.IsSingleton)
                {
                    objRoot.SetProperty(keyStr, valueResult.FirstOrDefault);
                }
                else
                {
                    objRoot.SetProperty(keyStr, JsonataHelpers.ArrayFromSequence(valueResult, env.Workspace));
                }
            }

            return new Sequence((JsonElement)objRoot);
        };
    }

    private static ExpressionEvaluator CompileFunctionCall(FunctionCallNode func)
    {
        var args = func.Arguments.Select(Compile).ToArray();

        // Check for built-in functions by name.
        // Built-in functions are compiled directly and do NOT participate
        // in tail-call optimization — they are always evaluated inline.
        if (func.Procedure is VariableNode varProc)
        {
            var builtIn = BuiltInFunctions.TryGetCompiler(varProc.Name);
            if (builtIn is not null)
            {
                return builtIn(args);
            }
        }

        var procedure = Compile(func.Procedure);
        bool isTailCall = func.IsTailCall;

        // Detect non-$ function names that match built-ins (e.g. sum instead of $sum)
        string? suggestedBuiltIn = null;
        NameNode? nameProc = func.Procedure as NameNode
            ?? (func.Procedure is PathNode pathProc && pathProc.Steps.Count == 1 ? pathProc.Steps[0] as NameNode : null);
        if (nameProc is not null && BuiltInFunctions.TryGetCompiler(nameProc.Value) is not null)
        {
            suggestedBuiltIn = nameProc.Value;
        }

        // Generic function call (user-defined / lambda)
        return (in JsonElement input, Environment env) =>
        {
            var funcResult = procedure(input, env);

            if (funcResult.IsLambda)
            {
                var evaluatedArgs = new Sequence[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    evaluatedArgs[i] = args[i](input, env);
                }

                if (isTailCall)
                {
                    // Return a continuation for the caller's trampoline to handle
                    return new Sequence(new TailCallContinuation(funcResult.Lambda!, evaluatedArgs, input, env));
                }

                return funcResult.Lambda!.Invoke(evaluatedArgs, input, env);
            }

            if (suggestedBuiltIn is not null)
            {
                throw new JsonataException("T1005", $"Attempted to invoke a non-function. Did you mean '${suggestedBuiltIn}'?", func.Position);
            }

            throw new JsonataException("T1006", "Attempted to invoke a non-function", func.Position);
        };
    }

    private static ExpressionEvaluator CompileLambda(LambdaNode lambda)
    {
        var body = Compile(lambda.Body);
        var paramNames = lambda.Parameters.ToArray();
        string? signature = lambda.Signature;
        int contextArgCount = GetSignatureContextArgCount(signature, out int regularArgCount);

        // Validate signature syntax at compile time
        if (signature is not null)
        {
            SignatureValidator.ValidateSyntax(signature, lambda.Position);
        }

        return (in JsonElement input, Environment env) =>
        {
            return new Sequence(new LambdaValue(body, paramNames, env, input, contextArgCount, regularArgCount, signature));
        };
    }

    /// <summary>
    /// Parses a JSONata function signature string to determine how many parameters
    /// are context-bound (before the <c>-</c> separator).
    /// </summary>
    /// <remarks>
    /// In a signature like <c>&lt;n-n:n&gt;</c>, the <c>-</c> separates context params from regular params.
    /// Parameters before <c>-</c> are bound from the path context element when invoked on a path step.
    /// </remarks>
    private static int GetSignatureContextArgCount(string? signature)
    {
        return GetSignatureContextArgCount(signature, out _);
    }

    /// <summary>
    /// Parses a JSONata function signature to determine context and regular param counts.
    /// </summary>
    private static int GetSignatureContextArgCount(string? signature, out int regularArgCount)
    {
        regularArgCount = 0;

        if (signature is null || signature.Length < 3)
        {
            return 0;
        }

        int depth = 0;
        int paramCount = 0;
        bool foundDash = false;
        int postDashCount = 0;

        for (int i = 1; i < signature.Length - 1; i++)
        {
            char c = signature[i];

            if (c == '<' || c == '(')
            {
                if (depth == 0 && c == '(')
                {
                    if (foundDash)
                    {
                        postDashCount++;
                    }
                    else
                    {
                        paramCount++;
                    }
                }

                depth++;
            }
            else if (c == '>' || c == ')')
            {
                depth--;
            }
            else if (depth == 0)
            {
                if (c == '-')
                {
                    foundDash = true;
                    continue;
                }

                if (c == ':')
                {
                    break;
                }

                if (c != '?' && c != '+')
                {
                    if (foundDash)
                    {
                        postDashCount++;
                    }
                    else
                    {
                        paramCount++;
                    }
                }
            }
        }

        if (!foundDash)
        {
            regularArgCount = 0;
            return 0;
        }

        regularArgCount = postDashCount;
        return paramCount;
    }

    private static ExpressionEvaluator CompileBind(BindNode bind)
    {
        var lhsNode = bind.Lhs;
        var rhs = Compile(bind.Rhs);

        if (lhsNode is not VariableNode varNode)
        {
            throw new JsonataException("D1001", "Bind LHS must be a variable", bind.Position);
        }

        var name = varNode.Name;
        return (in JsonElement input, Environment env) =>
        {
            var value = rhs(input, env);
            env.Bind(name, value);
            return value;
        };
    }

    private static ExpressionEvaluator CompileApply(ApplyNode apply)
    {
        var lhsEval = Compile(apply.Lhs);

        // If RHS is a function call, we prepend LHS as the first argument
        if (apply.Rhs is FunctionCallNode funcCall)
        {
            // Check if it's a built-in function — compile directly with LHS prepended
            if (funcCall.Procedure is VariableNode varProc)
            {
                var builtIn = BuiltInFunctions.TryGetCompiler(varProc.Name);
                if (builtIn is not null)
                {
                    var originalArgEvals = funcCall.Arguments.Select(Compile).ToArray();
                    var allArgEvals = new ExpressionEvaluator[originalArgEvals.Length + 1];
                    allArgEvals[0] = lhsEval;
                    Array.Copy(originalArgEvals, 0, allArgEvals, 1, originalArgEvals.Length);
                    return builtIn(allArgEvals);
                }
            }

            // Generic function call: compile procedure and invoke at runtime
            var originalArgs = funcCall.Arguments.Select(Compile).ToArray();
            var procedure = Compile(funcCall.Procedure);

            return (in JsonElement input, Environment env) =>
            {
                var lhsResult = lhsEval(input, env);
                var funcResult = procedure(input, env);

                if (funcResult.IsLambda)
                {
                    var allArgs = new Sequence[originalArgs.Length + 1];
                    allArgs[0] = lhsResult;
                    for (int i = 0; i < originalArgs.Length; i++)
                    {
                        allArgs[i + 1] = originalArgs[i](input, env);
                    }

                    return funcResult.Lambda!.Invoke(allArgs, input, env);
                }

                return Sequence.Undefined;
            };
        }

        // Transform: LHS ~> |pattern|update,delete|
        if (apply.Rhs is TransformNode transformNode)
        {
            var patternEval = Compile(transformNode.Pattern);
            var updateEval = Compile(transformNode.Update);
            ExpressionEvaluator? deleteEval = transformNode.Delete is not null ? Compile(transformNode.Delete) : null;

            return (in JsonElement input, Environment env) =>
            {
                var lhsResult = lhsEval(input, env);
                if (lhsResult.IsUndefined)
                {
                    return Sequence.Undefined;
                }

                return ApplyTransform(lhsResult, patternEval, updateEval, deleteEval, env);
            };
        }

        var rhsEval = Compile(apply.Rhs);

        // Function composition / pipe: LHS ~> RHS where RHS is a variable/lambda
        return (in JsonElement input, Environment env) =>
        {
            var lhsResult = lhsEval(input, env);
            var rhsResult = rhsEval(input, env);

            // If RHS is a lambda...
            if (rhsResult.IsLambda)
            {
                // If LHS is also a lambda, compose: create a new function
                // that applies LHS first, then pipes result through RHS
                if (lhsResult.IsLambda)
                {
                    var lhsLambda = lhsResult.Lambda!;
                    var rhsLambda = rhsResult.Lambda!;
                    var composedLambda = new LambdaValue(
                        (args, compInput, compEnv) =>
                        {
                            var intermediate = lhsLambda.Invoke(args, compInput, compEnv);
                            return rhsLambda.Invoke([intermediate], compInput, compEnv);
                        },
                        lhsLambda.Arity > 0 ? lhsLambda.Arity : 1);
                    return new Sequence(composedLambda);
                }

                // LHS is a value — invoke RHS with LHS as argument
                var args = new[] { lhsResult };
                return rhsResult.Lambda!.Invoke(args, input, env);
            }

            // If RHS is a regex, apply $contains semantics: string ~> /regex/ → boolean
            if (rhsResult.IsRegex)
            {
                if (lhsResult.IsUndefined)
                {
                    return Sequence.Undefined;
                }

                var el = lhsResult.FirstOrDefault;
                if (el.ValueKind == JsonValueKind.String)
                {
                    string str = el.GetString() ?? string.Empty;
                    bool matches = rhsResult.Regex!.IsMatch(str);
                    return new Sequence(JsonataHelpers.BooleanElement(matches));
                }

                return Sequence.Undefined;
            }

            // If RHS is not callable, that's an error
            if (!rhsResult.IsUndefined)
            {
                throw new JsonataException("T2006", "The right side of the '~>' operator must be a function", apply.Position);
            }

            return Sequence.Undefined;
        };
    }

    private static ExpressionEvaluator CompileFilter(FilterNode filter)
    {
        var predicate = Compile(filter.Expression);
        return (in JsonElement input, Environment env) =>
        {
            var result = predicate(input, env);

            if (result.IsUndefined)
            {
                return Sequence.Undefined;
            }

            var first = result.FirstOrDefault;

            // Boolean filter takes priority over numeric coercion
            if (first.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return first.ValueKind == JsonValueKind.True ? new Sequence(input) : Sequence.Undefined;
            }

            // Numeric filter = index access
            if (result.IsSingleton && TryCoerceToNumber(first, out double idx))
            {
                if (input.ValueKind == JsonValueKind.Array)
                {
                    int index = (int)idx;
                    if (index < 0)
                    {
                        index = input.GetArrayLength() + index;
                    }

                    if (index >= 0 && index < input.GetArrayLength())
                    {
                        return new Sequence(input[index]);
                    }
                }

                return Sequence.Undefined;
            }

            // Multi-value result: collect matching indices (array of indices pattern)
            if (result.Count > 1)
            {
                var builder = default(SequenceBuilder);
                for (int i = 0; i < result.Count; i++)
                {
                    var elem = result[i];
                    if (elem.ValueKind is JsonValueKind.True)
                    {
                        builder.Add(input);
                    }
                    else if (TryCoerceToNumber(elem, out double elemIdx) && input.ValueKind == JsonValueKind.Array)
                    {
                        int eIdx = (int)elemIdx;
                        if (eIdx < 0) eIdx = input.GetArrayLength() + eIdx;
                        if (eIdx >= 0 && eIdx < input.GetArrayLength())
                        {
                            builder.Add(input[eIdx]);
                        }
                    }
                }

                return builder.ToSequence();
            }

            // General truthiness
            return IsTruthy(result) ? new Sequence(input) : Sequence.Undefined;
        };
    }

    private static ExpressionEvaluator CompileSort(SortNode sort)
    {
        return CompileSortStage(sort);
    }

    private static ExpressionEvaluator CompileSortStage(SortNode sort)
    {
        var terms = new (ExpressionEvaluator Expr, bool Descending)[sort.Terms.Count];
        for (int i = 0; i < sort.Terms.Count; i++)
        {
            terms[i] = (Compile(sort.Terms[i].Expression), sort.Terms[i].Descending);
        }

        return (in JsonElement input, Environment env) =>
        {
            // Collect elements to sort
            var elements = new List<JsonElement>();
            if (input.ValueKind == JsonValueKind.Array)
            {
                elements.AddRange(input.EnumerateArray());
            }
            else
            {
                elements.Add(input);
            }

            if (elements.Count <= 1)
            {
                return new Sequence(input);
            }

            // Stable sort using index-based ordering to preserve relative
            // order of equal elements (matches JSONata reference semantics).
            var indices = Enumerable.Range(0, elements.Count).ToArray();
            Array.Sort(indices, (a, b) =>
            {
                for (int t = 0; t < terms.Length; t++)
                {
                    var (expr, desc) = terms[t];
                    var aVal = expr(elements[a], env);
                    var bVal = expr(elements[b], env);

                    int cmp = CompareSortKeys(aVal, bVal);
                    if (cmp != 0)
                    {
                        return desc ? -cmp : cmp;
                    }
                }

                return a.CompareTo(b);
            });

            var sorted = new List<JsonElement>(elements.Count);
            for (int i = 0; i < indices.Length; i++)
            {
                sorted.Add(elements[indices[i]]);
            }

            // Build result as a JSON array so subsequent path steps flatten it
            return new Sequence(JsonataHelpers.ArrayFromList(sorted, env.Workspace));
        };
    }

    private static int CompareSortKeys(Sequence a, Sequence b)
    {
        if (a.IsUndefined && b.IsUndefined)
        {
            return 0;
        }

        // Undefined values sort last (after all defined values)
        if (a.IsUndefined)
        {
            return 1;
        }

        if (b.IsUndefined)
        {
            return -1;
        }

        var aEl = a.FirstOrDefault;
        var bEl = b.FirstOrDefault;

        bool aIsNum = aEl.ValueKind == JsonValueKind.Number;
        bool aIsStr = aEl.ValueKind == JsonValueKind.String;
        bool bIsNum = bEl.ValueKind == JsonValueKind.Number;
        bool bIsStr = bEl.ValueKind == JsonValueKind.String;

        // T2008: sort key must evaluate to a number or string
        if (!aIsNum && !aIsStr)
        {
            throw new JsonataException("T2008", "The expressions within an order-by clause must evaluate to numeric or string values", 0);
        }

        if (!bIsNum && !bIsStr)
        {
            throw new JsonataException("T2008", "The expressions within an order-by clause must evaluate to numeric or string values", 0);
        }

        // T2007: types must be consistent (both numbers or both strings)
        if (aIsNum != bIsNum)
        {
            throw new JsonataException("T2007", "Type mismatch within order-by clause. All values must be of the same type", 0);
        }

        if (aIsNum)
        {
            return aEl.GetDouble().CompareTo(bEl.GetDouble());
        }

        return Utf8CompareOrdinal(aEl, bEl);
    }

    private static ExpressionEvaluator CompileTransform(TransformNode transform)
    {
        var patternEval = Compile(transform.Pattern);
        var updateEval = Compile(transform.Update);
        ExpressionEvaluator? deleteEval = transform.Delete is not null ? Compile(transform.Delete) : null;

        return (in JsonElement input, Environment env) =>
        {
            var inputSeq = new Sequence(input);
            return ApplyTransform(inputSeq, patternEval, updateEval, deleteEval, env);
        };
    }

    private static Sequence ApplyTransform(
        Sequence inputSeq,
        ExpressionEvaluator patternEval,
        ExpressionEvaluator updateEval,
        ExpressionEvaluator? deleteEval,
        Environment env)
    {
        // Collect all matched items by evaluating pattern against each input element
        var matchTexts = new HashSet<string>();
        for (int i = 0; i < inputSeq.Count; i++)
        {
            var element = inputSeq[i];
            var matches = patternEval(element, env);
            for (int j = 0; j < matches.Count; j++)
            {
                CollectMatchTexts(matches[j], matchTexts);
            }
        }

        if (inputSeq.IsSingleton)
        {
            var transformed = TransformElement(inputSeq.FirstOrDefault, matchTexts, updateEval, deleteEval, env);
            return new Sequence(transformed);
        }

        var builder = default(SequenceBuilder);
        for (int i = 0; i < inputSeq.Count; i++)
        {
            builder.Add(TransformElement(inputSeq[i], matchTexts, updateEval, deleteEval, env));
        }

        Sequence resultSeq = builder.ToSequence();
        JsonElement arrayResult = JsonataHelpers.ArrayFromSequence(resultSeq, env.Workspace);
        builder.ReturnArray();
        return new Sequence(arrayResult);
    }

    private static void CollectMatchTexts(JsonElement element, HashSet<string> matchTexts)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectMatchTexts(item, matchTexts);
            }
        }
        else
        {
            matchTexts.Add(element.GetRawText());
        }
    }

    private static JsonElement TransformElement(
        JsonElement element,
        HashSet<string> matchTexts,
        ExpressionEvaluator updateEval,
        ExpressionEvaluator? deleteEval,
        Environment env)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            bool isMatch = matchTexts.Contains(element.GetRawText());

            // Count properties for capacity estimate
            int propCount = 0;
            foreach (var unused in element.EnumerateObject())
            {
                propCount++;
            }

            JsonDocumentBuilder<JsonElement.Mutable> objDoc = JsonElement.CreateObjectBuilder(env.Workspace, propCount + 4);
            JsonElement.Mutable objRoot = objDoc.RootElement;

            if (isMatch)
            {
                // Evaluate update expression in the context of the matched object
                var updateResult = updateEval(element, env);
                JsonElement? updateObj = null;
                if (!updateResult.IsUndefined)
                {
                    var first = updateResult.FirstOrDefault;
                    if (first.ValueKind != JsonValueKind.Object)
                    {
                        throw new JsonataException("T2011", "The literal value of the right side of the Transform expression must be an object", 0);
                    }

                    updateObj = first;
                }

                // Evaluate delete expression
                HashSet<string>? deleteProps = null;
                if (deleteEval is not null)
                {
                    deleteProps = new HashSet<string>();
                    var deleteResult = deleteEval(element, env);
                    if (!deleteResult.IsUndefined)
                    {
                        var first = deleteResult.FirstOrDefault;
                        if (first.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var d in first.EnumerateArray())
                            {
                                if (d.ValueKind == JsonValueKind.String)
                                {
                                    deleteProps.Add(d.GetString()!);
                                }
                            }
                        }
                        else if (first.ValueKind == JsonValueKind.String)
                        {
                            deleteProps.Add(first.GetString()!);
                        }
                        else
                        {
                            throw new JsonataException("T2012", "The delete clause of the Transform expression must evaluate to a string or array of strings", 0);
                        }
                    }
                }

                // Build a set of property names that exist in the update
                var updatePropNames = new HashSet<string>();
                if (updateObj.HasValue)
                {
                    foreach (var prop in updateObj.Value.EnumerateObject())
                    {
                        updatePropNames.Add(prop.Name);
                    }
                }

                // Write original properties: skip deleted, override with update values
                foreach (var prop in element.EnumerateObject())
                {
                    if (deleteProps?.Contains(prop.Name) == true)
                    {
                        continue;
                    }

                    if (updatePropNames.Contains(prop.Name))
                    {
                        // Property is overridden by update — use the update value
                        if (updateObj!.Value.TryGetProperty(prop.Name, out var updateValue))
                        {
                            objRoot.SetProperty(prop.Name, updateValue);
                        }
                    }
                    else
                    {
                        // Recursively transform the property value
                        objRoot.SetProperty(prop.Name, TransformElement(prop.Value, matchTexts, updateEval, deleteEval, env));
                    }
                }

                // Add new properties from update that weren't in the original
                if (updateObj.HasValue)
                {
                    foreach (var prop in updateObj.Value.EnumerateObject())
                    {
                        bool alreadyExists = false;
                        foreach (var orig in element.EnumerateObject())
                        {
                            if (orig.Name == prop.Name)
                            {
                                alreadyExists = true;
                                break;
                            }
                        }

                        if (!alreadyExists)
                        {
                            objRoot.SetProperty(prop.Name, prop.Value);
                        }
                    }
                }
            }
            else
            {
                // Not a match — recursively transform children
                foreach (var prop in element.EnumerateObject())
                {
                    objRoot.SetProperty(prop.Name, TransformElement(prop.Value, matchTexts, updateEval, deleteEval, env));
                }
            }

            return (JsonElement)objRoot;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            int arrayLen = element.GetArrayLength();
            JsonDocumentBuilder<JsonElement.Mutable> arrDoc = JsonElement.CreateArrayBuilder(env.Workspace, arrayLen);
            JsonElement.Mutable arrRoot = arrDoc.RootElement;
            foreach (var item in element.EnumerateArray())
            {
                arrRoot.AddItem(TransformElement(item, matchTexts, updateEval, deleteEval, env));
            }

            return (JsonElement)arrRoot;
        }

        return element;
    }

    private static ExpressionEvaluator CompileRegex(RegexNode regex)
    {
        RegexOptions options = RegexOptions.None;
        foreach (char flag in regex.Flags)
        {
            switch (flag)
            {
                case 'i':
                    options |= RegexOptions.IgnoreCase;
                    break;
                case 'm':
                    options |= RegexOptions.Multiline;
                    break;
            }
        }

        var compiledRegex = new Regex(regex.Pattern, options);
        return (in JsonElement input, Environment env) => new Sequence(compiledRegex);
    }

    private static ExpressionEvaluator CompileParent()
    {
        // TODO: Parent context tracking
        return static (in JsonElement input, Environment env) => Sequence.Undefined;
    }

    private static ExpressionEvaluator CompilePartial(PartialNode partial)
    {
        var argCompilers = partial.Arguments
            .Select(a => a is PlaceholderNode ? null : Compile(a))
            .ToArray();

        // Check for built-in function by name
        Func<ExpressionEvaluator[], ExpressionEvaluator>? builtInCompiler = null;
        if (partial.Procedure is VariableNode varProc)
        {
            builtInCompiler = BuiltInFunctions.TryGetCompiler(varProc.Name);
        }

        // Detect non-$ names that match built-ins (e.g. substring instead of $substring) for T1007
        bool matchesBuiltIn = false;
        NameNode? nameProc = partial.Procedure as NameNode
            ?? (partial.Procedure is PathNode pathProc && pathProc.Steps.Count == 1 ? pathProc.Steps[0] as NameNode : null);
        if (nameProc is not null)
        {
            matchesBuiltIn = BuiltInFunctions.TryGetCompiler(nameProc.Value) is not null;
        }

        var procedureEval = Compile(partial.Procedure);

        return (in JsonElement input, Environment env) =>
        {
            // Evaluate non-placeholder arguments eagerly
            var evaluatedArgs = new Sequence?[argCompilers.Length];
            var placeholderCount = 0;
            for (int i = 0; i < argCompilers.Length; i++)
            {
                if (argCompilers[i] is null)
                {
                    placeholderCount++;
                }
                else
                {
                    evaluatedArgs[i] = argCompilers[i]!(input, env);
                }
            }

            var capturedArgs = (Sequence?[])evaluatedArgs.Clone();

            if (builtInCompiler is not null)
            {
                var capturedCompiler = builtInCompiler;

                return new Sequence(new LambdaValue(
                    (runtimeArgs, innerInput, innerEnv) =>
                    {
                        // Fill placeholder positions with runtime arguments
                        var fullArgEvals = new ExpressionEvaluator[capturedArgs.Length];
                        int pIdx = 0;
                        for (int i = 0; i < capturedArgs.Length; i++)
                        {
                            if (capturedArgs[i] is null)
                            {
                                var arg = pIdx < runtimeArgs.Length ? runtimeArgs[pIdx] : Sequence.Undefined;
                                pIdx++;
                                var captured = arg;
                                fullArgEvals[i] = (in JsonElement _, Environment __) => captured;
                            }
                            else
                            {
                                var val = capturedArgs[i]!.Value;
                                fullArgEvals[i] = (in JsonElement _, Environment __) => val;
                            }
                        }

                        var compiled = capturedCompiler(fullArgEvals);
                        return compiled(innerInput, innerEnv);
                    },
                    placeholderCount));
            }
            else
            {
                // User-defined or variable-resolved function
                var funcResult = procedureEval(input, env);
                if (!funcResult.IsLambda)
                {
                    string code = matchesBuiltIn ? "T1007" : "T1008";
                    throw new JsonataException(code, "Attempted to partially apply a non-function", partial.Position);
                }

                var originalLambda = funcResult.Lambda!;

                return new Sequence(new LambdaValue(
                    (runtimeArgs, innerInput, innerEnv) =>
                    {
                        var fullArgs = new Sequence[capturedArgs.Length];
                        int pIdx = 0;
                        for (int i = 0; i < capturedArgs.Length; i++)
                        {
                            if (capturedArgs[i] is null)
                            {
                                fullArgs[i] = pIdx < runtimeArgs.Length ? runtimeArgs[pIdx] : Sequence.Undefined;
                                pIdx++;
                            }
                            else
                            {
                                fullArgs[i] = capturedArgs[i]!.Value;
                            }
                        }

                        return originalLambda.Invoke(fullArgs, innerInput, innerEnv);
                    },
                    placeholderCount));
            }
        };
    }

    #pragma warning disable SA1201 // Elements should appear in the correct order

    // --- Helper methods ---
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryCoerceToNumber(JsonElement element, out double value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                value = element.GetDouble();
                return true;
            case JsonValueKind.True:
                value = 1;
                return true;
            case JsonValueKind.False:
                value = 0;
                return true;
            case JsonValueKind.Null:
                value = 0;
                return true;
            case JsonValueKind.String:
                // Use raw UTF-8 bytes to avoid string allocation for the common decimal path
                using (RawUtf8JsonString rawStr = JsonMarshal.GetRawUtf8Value(element))
                {
                    ReadOnlySpan<byte> span = rawStr.Span;
                    if (span.Length <= 2)
                    {
                        // Empty string
                        value = 0;
                        return false;
                    }

                    // Unquoted content
                    ReadOnlySpan<byte> content = span.Slice(1, span.Length - 2);

                    // Check for hex/binary/octal prefixes (need string fallback)
                    if (content.Length > 2 && content[0] == (byte)'0')
                    {
                        byte prefix = content[1];
                        if (prefix == (byte)'x' || prefix == (byte)'X' ||
                            prefix == (byte)'b' || prefix == (byte)'B' ||
                            prefix == (byte)'o' || prefix == (byte)'O')
                        {
                            // Fall back to string for hex/binary/octal parsing
                            string s = element.GetString()!;
                            return TryParseSpecialRadix(s, out value);
                        }
                    }

                    // Try UTF-8 double parsing (common path — no string allocation)
                    if (Utf8Parser.TryParse(content, out value, out int bytesConsumed)
                        && bytesConsumed == content.Length)
                    {
                        return true;
                    }

                    value = 0;
                    return false;
                }

            default:
                value = 0;
                return false;
        }
    }

    /// <summary>
    /// Parses hex (0x), binary (0b), or octal (0o) prefixed strings to double.
    /// </summary>
    private static bool TryParseSpecialRadix(string s, out double value)
    {
        char prefix = s[1];

        if (prefix is 'x' or 'X')
        {
            if (long.TryParse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hex))
            {
                value = hex;
                return true;
            }

            value = 0;
            return false;
        }

        if (prefix is 'b' or 'B')
        {
            try
            {
                value = Convert.ToInt64(s.Substring(2), 2);
                return true;
            }
            catch
            {
                value = 0;
                return false;
            }
        }

        if (prefix is 'o' or 'O')
        {
            try
            {
                value = Convert.ToInt64(s.Substring(2), 8);
                return true;
            }
            catch
            {
                value = 0;
                return false;
            }
        }

        value = 0;
        return false;
    }

    internal static string CoerceToString(Sequence seq)
    {
        if (seq.IsUndefined)
        {
            return string.Empty;
        }

        // Multi-valued sequences are stringified as JSON arrays
        if (seq.Count > 1)
        {
            return BuildArrayRawText(seq);
        }

        return CoerceElementToString(seq.FirstOrDefault);
    }

    private static string BuildArrayRawText(Sequence seq)
    {
        // Build raw JSON text directly: [elem1,elem2,...]
        var sb = new System.Text.StringBuilder(seq.Count * 16);
        sb.Append('[');
        for (int i = 0; i < seq.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(seq[i].GetRawText());
        }

        sb.Append(']');
        return sb.ToString();
    }

    internal static string CoerceElementToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => FormatNumberLikeJavaScript(element),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            JsonValueKind.Undefined => string.Empty,
            _ => element.GetRawText(),
        };
    }

    /// <summary>
    /// Formats a JSON number element to match JSONata's <c>$string</c> behavior.
    /// JSONata applies <c>Number.toPrecision(15)</c> to non-integer values
    /// (cleaning up IEEE 754 noise), but preserves integers at full precision.
    /// </summary>
    internal static string FormatNumberLikeJavaScript(JsonElement element)
    {
        double value = element.GetDouble();
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return "null";
        }

        // Integer values: use raw text from JSON to preserve full precision
        if (value == Math.Floor(value) && !double.IsInfinity(value))
        {
            string raw = element.GetRawText();

            // Only use raw text if it doesn't contain a decimal point or exponent
            // (i.e., it's a plain integer literal like "5890840712243076")
            if (!raw.Contains('.') && !raw.Contains('e') && !raw.Contains('E'))
            {
                return raw;
            }
        }

        return FormatNumberLikeJavaScript(value);
    }

    /// <summary>
    /// Formats a double to match JSONata's number-to-string behavior for non-integers.
    /// Uses <c>toPrecision(15)</c> (G15 format) to clean up IEEE 754 noise,
    /// then applies JavaScript's scientific notation expansion rules.
    /// </summary>
    internal static string FormatNumberLikeJavaScript(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return "null";
        }

        // Match JSONata's Number(val.toPrecision(15)): 15 significant digits
        string result = value.ToString("G15", CultureInfo.InvariantCulture);

        // Convert uppercase E to lowercase e
        result = result.Replace("E+", "e+").Replace("E-", "e-");

        double abs = Math.Abs(value);

        // JavaScript uses decimal form for [1e-6, 1e+21)
        if (result.Contains('e'))
        {
            if (abs >= 1e-6 && abs < 1e+20)
            {
                // Force decimal format — re-parse G15 result to get the rounded value,
                // then format as decimal
                double rounded = double.Parse(result.Replace("e+", "E+").Replace("e-", "E-"), CultureInfo.InvariantCulture);
                result = rounded.ToString("0.####################", CultureInfo.InvariantCulture);
                return result;
            }

            // For very large integers (like 1e+20), use integer-like representation
            if (abs >= 1e+20 && abs < 1e+21 && value == Math.Floor(value))
            {
                return value.ToString("0", CultureInfo.InvariantCulture);
            }

            // Strip leading zeros from exponent: e-07 → e-7, e+02 → e+2
            result = ExponentLeadingZeroRegex.Replace(result, "e$1$2");
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsTruthy(Sequence seq)
    {
        if (seq.IsUndefined)
        {
            return false;
        }

        return IsTruthyElement(seq.FirstOrDefault);
    }

    internal static bool IsTruthyElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Undefined:
            case JsonValueKind.Null:
            case JsonValueKind.False:
                return false;
            case JsonValueKind.True:
                return true;
            case JsonValueKind.Number:
                double d = element.GetDouble();
                return d != 0 && !double.IsNaN(d);
            case JsonValueKind.String:
            {
                // Check if string is non-empty without allocating a .NET string.
                // Raw UTF-8 includes quotes, so length > 2 means non-empty content.
                using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(element);
                return raw.Span.Length > 2;
            }

            case JsonValueKind.Object:
                // Empty object is falsy
                ObjectEnumerator<JsonElement> enumerator = element.EnumerateObject();
                return enumerator.MoveNext();
            case JsonValueKind.Array:
                // Array: if any element is truthy, return true
                foreach (JsonElement child in element.EnumerateArray())
                {
                    if (IsTruthyElement(child))
                    {
                        return true;
                    }
                }

                return false;
            default:
                return false;
        }
    }

    internal static bool JsonElementEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            return false;
        }

        return a.ValueKind switch
        {
            JsonValueKind.Number => a.GetDouble() == b.GetDouble(),
            JsonValueKind.String => Utf8StringEquals(a, b),
            JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => true,
            JsonValueKind.Array => ArrayDeepEquals(a, b),
            JsonValueKind.Object => ObjectDeepEquals(a, b),
            _ => Utf8RawEquals(a, b),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Utf8StringEquals(JsonElement a, JsonElement b)
    {
        using RawUtf8JsonString rawA = JsonMarshal.GetRawUtf8Value(a);
        using RawUtf8JsonString rawB = JsonMarshal.GetRawUtf8Value(b);
        return rawA.Span.SequenceEqual(rawB.Span);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Utf8RawEquals(JsonElement a, JsonElement b)
    {
        using RawUtf8JsonString rawA = JsonMarshal.GetRawUtf8Value(a);
        using RawUtf8JsonString rawB = JsonMarshal.GetRawUtf8Value(b);
        return rawA.Span.SequenceEqual(rawB.Span);
    }

    /// <summary>
    /// Compares two string JSON elements by ordinal byte order, without allocating .NET strings.
    /// </summary>
    private static int Utf8CompareOrdinal(JsonElement a, JsonElement b)
    {
        using RawUtf8JsonString rawA = JsonMarshal.GetRawUtf8Value(a);
        using RawUtf8JsonString rawB = JsonMarshal.GetRawUtf8Value(b);
        return rawA.Span.SequenceCompareTo(rawB.Span);
    }

    private static bool ArrayDeepEquals(JsonElement a, JsonElement b)
    {
        int lenA = a.GetArrayLength();
        int lenB = b.GetArrayLength();
        if (lenA != lenB)
        {
            return false;
        }

        var enumA = a.EnumerateArray();
        var enumB = b.EnumerateArray();
        using var eA = enumA.GetEnumerator();
        using var eB = enumB.GetEnumerator();
        while (eA.MoveNext() && eB.MoveNext())
        {
            if (!JsonElementEquals(eA.Current, eB.Current))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ObjectDeepEquals(JsonElement a, JsonElement b)
    {
        // Count properties
        int countA = 0;
        foreach (var propA0 in a.EnumerateObject())
        {
            _ = propA0;
            countA++;
        }

        int countB = 0;
        foreach (var propB0 in b.EnumerateObject())
        {
            _ = propB0;
            countB++;
        }

        if (countA != countB)
        {
            return false;
        }

        // For each property in a, find matching property in b (order-independent)
        foreach (var propA in a.EnumerateObject())
        {
            if (!b.TryGetProperty(propA.Name, out JsonElement propB))
            {
                return false;
            }

            if (!JsonElementEquals(propA.Value, propB))
            {
                return false;
            }
        }

        return true;
    }

    internal static JsonElement CreateArrayElement(List<JsonElement> elements, JsonWorkspace workspace)
    {
        return JsonataHelpers.ArrayFromList(elements, workspace);
    }

    internal static JsonElement CreateNumberElement(double value, JsonWorkspace workspace)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new JsonataException("D3001", "A string cannot be generated from this value", 0);
        }

        return JsonataHelpers.NumberFromDouble(value, workspace);
    }

    internal static JsonElement CreateStringElement(string value, JsonWorkspace workspace)
    {
        return JsonataHelpers.StringFromString(value, workspace);
    }

    internal static JsonElement CreateBoolElement(bool value)
    {
        return JsonataHelpers.BooleanElement(value);
    }

    internal static JsonElement CreateNullElement()
    {
        return NullElement;
    }

    /// <summary>
    /// Creates a <see cref="LambdaValue"/> that wraps a built-in function compiler.
    /// When invoked, it compiles the built-in with the provided args as constant evaluators.
    /// Extra args are truncated to match the built-in's expected arity.
    /// </summary>
    internal static LambdaValue CreateBuiltInLambda(Func<ExpressionEvaluator[], ExpressionEvaluator> compiler)
    {
        return new LambdaValue(
            (args, input, env) =>
            {
                // Try invoking with decreasing arg counts to handle HOF over-arity
                // (e.g. $map passes (value, index, array) but $boolean expects 1 arg)
                for (int tryCount = args.Length; tryCount >= 0; tryCount--)
                {
                    var argEvals = new ExpressionEvaluator[tryCount];
                    for (int i = 0; i < tryCount; i++)
                    {
                        var argValue = args[i];
                        argEvals[i] = (in JsonElement _, Environment __) => argValue;
                    }

                    try
                    {
                        var evaluator = compiler(argEvals);
                        return evaluator(input, env);
                    }
                    catch (JsonataException ex) when (ex.Code == "T0410" && tryCount > 0)
                    {
                        // Wrong arity — try with fewer args
                    }
                }

                throw new JsonataException("T0410", "Unable to invoke built-in function", 0);
            },
            paramCount: 0);
    }

    internal static JsonElement CreateJsonArrayElement(IReadOnlyList<JsonElement> items, JsonWorkspace workspace)
    {
        return JsonataHelpers.ArrayFromReadOnlyList(items, workspace);
    }

    internal static JsonElement CreateMatchObject(string match, int index, IReadOnlyList<string> groups, JsonWorkspace workspace)
    {
        return JsonataHelpers.CreateMatchObject(match, index, groups, workspace);
    }
}