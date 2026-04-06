// <copyright file="FunctionalCompiler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    // Use relaxed escaping so characters like > and < are not HTML-encoded.
    private static readonly JsonWriterOptions RelaxedWriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // Pre-cached constant elements
    private static readonly JsonElement TrueElement = CreateConstantElement(w => w.WriteBooleanValue(true));
    private static readonly JsonElement FalseElement = CreateConstantElement(w => w.WriteBooleanValue(false));
    private static readonly JsonElement NullElement = CreateConstantElement(w => w.WriteNullValue());

    private static JsonElement CreateConstantElement(Action<Utf8JsonWriter> write)
    {
        using var ms = new MemoryStream(16);
        using var writer = new Utf8JsonWriter(ms);
        write(writer);
        writer.Flush();
        ms.Position = 0;
        using var doc = JsonDocument.Parse(ms);
        return doc.RootElement.Clone();
    }

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
        if (node is not PathNode && node.Annotations?.Stages.Count > 0)
        {
            evaluator = WrapWithStages(evaluator, node.Annotations);
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
            }
            else
            {
                stageEvaluators[i] = Compile(stage);
            }
        }

        return (in JsonElement input, Environment env) =>
        {
            var result = inner(input, env);
            return ApplyStages(result, stageEvaluators, env);
        };
    }

    private static ExpressionEvaluator CompileNumber(NumberNode num)
    {
        var element = CreateNumberElement(num.Value);
        return (in JsonElement input, Environment env) => new Sequence(element);
    }

    private static ExpressionEvaluator CompileString(StringNode str)
    {
        var element = CreateStringElement(str.Value);
        return (in JsonElement input, Environment env) => new Sequence(element);
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
            if (input.ValueKind != JsonValueKind.Object)
            {
                return Sequence.Undefined;
            }

            if (input.TryGetProperty(fieldName, out var value))
            {
                return new Sequence(value);
            }

            return Sequence.Undefined;
        };
    }

    private static ExpressionEvaluator CompileWildcard()
    {
        return static (in JsonElement input, Environment env) =>
        {
            if (input.ValueKind != JsonValueKind.Object)
            {
                return Sequence.Undefined;
            }

            var builder = default(SequenceBuilder);
            foreach (var prop in input.EnumerateObject())
            {
                builder.Add(prop.Value);
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
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        builder.Add(prop.Value);
                        CollectDescendants(prop.Value, ref builder);
                    }

                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        builder.Add(item);
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
        var focusVars = new string?[path.Steps.Count];
        var indexVars = new string?[path.Steps.Count];
        var isPropertyStep = new bool[path.Steps.Count];
        (ExpressionEvaluator Key, ExpressionEvaluator Value)[]? groupByPairs = null;

        for (int i = 0; i < path.Steps.Count; i++)
        {
            steps[i] = CompileCore(path.Steps[i]);
            isPropertyStep[i] = path.Steps[i] is NameNode or WildcardNode or DescendantNode;

            var annotations = GetStepAnnotations(path.Steps[i]);
            if (annotations is not null)
            {
                if (annotations.Stages.Count > 0)
                {
                    stages[i] = new ExpressionEvaluator[annotations.Stages.Count];
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

        return (in JsonElement input, Environment env) =>
        {
            // Start with the input as a singleton sequence
            Sequence current = new(input);

            for (int stepIdx = 0; stepIdx < steps.Length; stepIdx++)
            {
                if (current.IsUndefined)
                {
                    return Sequence.Undefined;
                }

                var step = steps[stepIdx];
                var focusVar = focusVars[stepIdx];
                var indexVar = indexVars[stepIdx];

                // Bind focus variable if present
                if (focusVar is not null)
                {
                    env.Bind(focusVar, current);
                }

                // Auto-flatten arrays when:
                // - stepIdx > 0: any step after the first always maps over array contexts
                // - stepIdx == 0 AND isPropertyStep: property access on root array (e.g. name[0] on root array)
                bool shouldFlatten = stepIdx > 0 || isPropertyStep[stepIdx];

                if (current.IsSingleton)
                {
                    // Hot path: singleton input → evaluate step directly
                    var element = current.FirstOrDefault;

                    // Bind index variable (0 for singleton)
                    if (indexVar is not null)
                    {
                        env.Bind(indexVar, new Sequence(CreateNumberElement(0)));
                    }

                    if (element.ValueKind == JsonValueKind.Array && shouldFlatten)
                    {
                        current = FlattenArrayStep(element, step, env);
                    }
                    else
                    {
                        current = step(element, env);
                    }
                }
                else
                {
                    // Multi-value: map step over all values
                    var builder = default(SequenceBuilder);
                    for (int i = 0; i < current.Count; i++)
                    {
                        var element = current[i];

                        if (indexVar is not null)
                        {
                            env.Bind(indexVar, new Sequence(CreateNumberElement(i)));
                        }

                        Sequence stepResult;

                        if (element.ValueKind == JsonValueKind.Array && shouldFlatten)
                        {
                            stepResult = FlattenArrayStep(element, step, env);
                        }
                        else
                        {
                            stepResult = step(element, env);
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

                // Apply stages (predicates/filters/sorts) after step evaluation
                if (stages[stepIdx] is not null)
                {
                    current = ApplyStages(current, stages[stepIdx]!, env);
                }
            }

            // Apply group-by if present
            if (groupByPairs is not null)
            {
                current = ApplyGroupBy(current, groupByPairs, env);
            }

            return current;
        };
    }

    private static Sequence ApplyGroupBy(
        Sequence current,
        (ExpressionEvaluator Key, ExpressionEvaluator Value)[] pairs,
        Environment env)
    {
        if (current.IsUndefined)
        {
            return Sequence.Undefined;
        }

        // Collect all elements to iterate — flatten arrays in the sequence
        var elements = new List<JsonElement>();
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

        // Build grouped result: ordered dictionary of key → list of values
        var groupKeys = new List<string>();
        var groupValues = new Dictionary<string, List<JsonElement>>();

        foreach (var element in elements)
        {
            foreach (var (keyEval, valueEval) in pairs)
            {
                var keySeq = keyEval(element, env);
                var valSeq = valueEval(element, env);

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

                if (!groupValues.TryGetValue(keyStr, out var valueList))
                {
                    valueList = [];
                    groupValues[keyStr] = valueList;
                    groupKeys.Add(keyStr);
                }

                // Collect values — if value is array, add individual elements
                if (valSeq.IsUndefined)
                {
                    // Skip undefined values
                }
                else if (valSeq.Count > 1)
                {
                    for (int i = 0; i < valSeq.Count; i++)
                    {
                        valueList.Add(valSeq[i]);
                    }
                }
                else
                {
                    var val = valSeq.FirstOrDefault;
                    if (val.ValueKind == JsonValueKind.Array)
                    {
                        valueList.AddRange(val.EnumerateArray());
                    }
                    else
                    {
                        valueList.Add(val);
                    }
                }
            }
        }

        // Build the result object
        using var ms = new MemoryStream(256);
        using var writer = new Utf8JsonWriter(ms);
        writer.WriteStartObject();

        foreach (var key in groupKeys)
        {
            writer.WritePropertyName(key);
            var values = groupValues[key];
            if (values.Count == 1)
            {
                values[0].WriteTo(writer);
            }
            else
            {
                writer.WriteStartArray();
                foreach (var v in values)
                {
                    v.WriteTo(writer);
                }

                writer.WriteEndArray();
            }
        }

        writer.WriteEndObject();
        writer.Flush();
        ms.Position = 0;
        using var doc = JsonDocument.Parse(ms);
        return new Sequence(doc.RootElement.Clone());
    }

    private static Sequence ApplyStages(Sequence current, ExpressionEvaluator[] stageEvaluators, Environment env)
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

            // Apply the stage predicate to each element
            var builder = default(SequenceBuilder);
            for (int i = 0; i < elements.Count; i++)
            {
                var el = elements[i];
                var result = stage(el, env);

                if (result.IsUndefined)
                {
                    continue;
                }

                // Numeric result = index access
                if (result.IsSingleton && TryCoerceToNumber(result.FirstOrDefault, out double idx))
                {
                    int index = (int)idx;
                    if (index < 0)
                    {
                        index = elements.Count + index;
                    }

                    if (index >= 0 && index < elements.Count)
                    {
                        builder.Add(elements[index]);
                    }

                    // Index access returns a single element, stop iterating
                    current = builder.ToSequence();
                    break;
                }

                // Multi-value numeric = multiple index access
                if (!result.IsSingleton && result.Count > 0)
                {
                    bool allNumeric = true;
                    for (int j = 0; j < result.Count; j++)
                    {
                        if (!TryCoerceToNumber(result[j], out _))
                        {
                            allNumeric = false;
                            break;
                        }
                    }

                    if (allNumeric)
                    {
                        for (int j = 0; j < result.Count; j++)
                        {
                            TryCoerceToNumber(result[j], out double idx2);
                            int index2 = (int)idx2;
                            if (index2 < 0)
                            {
                                index2 = elements.Count + index2;
                            }

                            if (index2 >= 0 && index2 < elements.Count)
                            {
                                builder.Add(elements[index2]);
                            }
                        }

                        current = builder.ToSequence();
                        break;
                    }
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

    private static Sequence FlattenArrayStep(JsonElement array, ExpressionEvaluator step, Environment env)
    {
        var builder = default(SequenceBuilder);
        foreach (var item in array.EnumerateArray())
        {
            var result = step(item, env);
            builder.AddRange(result);
        }

        return builder.ToSequence();
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

            if (left.IsUndefined || right.IsUndefined)
            {
                return Sequence.Undefined;
            }

            var leftEl = left.FirstOrDefault;
            var rightEl = right.FirstOrDefault;

            // Strict arithmetic: only Number ValueKind allowed (no string/boolean coercion)
            if (leftEl.ValueKind != JsonValueKind.Number)
            {
                throw new JsonataException("T2001", "The left side of the arithmetic expression is not a number", 0);
            }

            if (rightEl.ValueKind != JsonValueKind.Number)
            {
                throw new JsonataException("T2002", "The right side of the arithmetic expression is not a number", 0);
            }

            double leftNum = leftEl.GetDouble();
            double rightNum = rightEl.GetDouble();

            double result = op(leftNum, rightNum);
            if (double.IsInfinity(result) || double.IsNaN(result))
            {
                throw new JsonataException("D1001", "Number out of range", 0);
            }

            return new Sequence(CreateNumberElement(result));
        };
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
                return new Sequence(CreateBoolElement(false));
            }

            bool result = JsonElementEquals(left.FirstOrDefault, right.FirstOrDefault);
            return new Sequence(CreateBoolElement(result));
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
            return new Sequence(CreateBoolElement(!val));
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
                int result = string.CompareOrdinal(lv.GetString(), rv.GetString());
                return new Sequence(CreateBoolElement(cmp(result, 0)));
            }

            // Numeric comparison — both must be numbers
            if (lv.ValueKind == JsonValueKind.Number && rv.ValueKind == JsonValueKind.Number)
            {
                if (TryCoerceToNumber(lv, out double leftNum) && TryCoerceToNumber(rv, out double rightNum))
                {
                    return new Sequence(CreateBoolElement(cmp(leftNum, rightNum)));
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

            string leftStr = CoerceToString(left);
            string rightStr = CoerceToString(right);

            return new Sequence(CreateStringElement(leftStr + rightStr));
        };
    }

    private static ExpressionEvaluator CompileAnd(ExpressionEvaluator lhs, ExpressionEvaluator rhs)
    {
        return (in JsonElement input, Environment env) =>
        {
            var left = lhs(input, env);
            if (!IsTruthy(left))
            {
                return new Sequence(CreateBoolElement(false));
            }

            var right = rhs(input, env);
            return new Sequence(CreateBoolElement(IsTruthy(right)));
        };
    }

    private static ExpressionEvaluator CompileOr(ExpressionEvaluator lhs, ExpressionEvaluator rhs)
    {
        return (in JsonElement input, Environment env) =>
        {
            var left = lhs(input, env);
            if (IsTruthy(left))
            {
                return new Sequence(CreateBoolElement(true));
            }

            var right = rhs(input, env);
            return new Sequence(CreateBoolElement(IsTruthy(right)));
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
                return Sequence.Undefined;
            }

            var needle = left.FirstOrDefault;
            var haystack = right.FirstOrDefault;

            if (haystack.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in haystack.EnumerateArray())
                {
                    if (JsonElementEquals(needle, item))
                    {
                        return new Sequence(CreateBoolElement(true));
                    }
                }
            }

            return new Sequence(CreateBoolElement(false));
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

            // Guard against excessively large ranges
            if ((long)iEnd - (long)iStart > 10_000_000)
            {
                throw new JsonataException("D2014", "Range expression generates too many results", 0);
            }

            var builder = default(SequenceBuilder);
            for (int i = iStart; i <= iEnd; i++)
            {
                builder.Add(CreateNumberElement(i));
            }

            return builder.ToSequence();
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
                    return new Sequence(CreateNumberElement(-el.GetDouble()));
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
        var exprs = arr.Expressions.Select(Compile).ToArray();
        return (in JsonElement input, Environment env) =>
        {
            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartArray();

            foreach (var expr in exprs)
            {
                var result = expr(input, env);
                for (int i = 0; i < result.Count; i++)
                {
                    result[i].WriteTo(writer);
                }
            }

            writer.WriteEndArray();
            writer.Flush();
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            return new Sequence(doc.RootElement.Clone());
        };
    }

    private static ExpressionEvaluator CompileObjectConstructor(ObjectConstructorNode obj)
    {
        var pairs = obj.Pairs.Select(p => (Key: Compile(p.Key), Value: Compile(p.Value))).ToArray();
        return (in JsonElement input, Environment env) =>
        {
            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartObject();

            foreach (var (key, value) in pairs)
            {
                var keyResult = key(input, env);
                var valueResult = value(input, env);

                if (keyResult.IsUndefined)
                {
                    continue;
                }

                string keyStr = CoerceToString(keyResult);

                if (valueResult.IsUndefined)
                {
                    // Skip undefined values — they don't produce a key in the output
                    continue;
                }

                writer.WritePropertyName(keyStr);

                if (valueResult.IsSingleton)
                {
                    valueResult.FirstOrDefault.WriteTo(writer);
                }
                else
                {
                    writer.WriteStartArray();
                    for (int i = 0; i < valueResult.Count; i++)
                    {
                        valueResult[i].WriteTo(writer);
                    }

                    writer.WriteEndArray();
                }
            }

            writer.WriteEndObject();
            writer.Flush();
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            return new Sequence(doc.RootElement.Clone());
        };
    }

    private static ExpressionEvaluator CompileFunctionCall(FunctionCallNode func)
    {
        var args = func.Arguments.Select(Compile).ToArray();

        // Check for built-in functions by name
        if (func.Procedure is VariableNode varProc)
        {
            var builtIn = BuiltInFunctions.TryGetCompiler(varProc.Name);
            if (builtIn is not null)
            {
                return builtIn(args);
            }
        }

        var procedure = Compile(func.Procedure);

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

                return funcResult.Lambda!.Invoke(evaluatedArgs, input, env);
            }

            throw new JsonataException("T1006", "Attempted to invoke a non-function", func.Position);
        };
    }

    private static ExpressionEvaluator CompileLambda(LambdaNode lambda)
    {
        var body = Compile(lambda.Body);
        var paramNames = lambda.Parameters.ToArray();

        return (in JsonElement input, Environment env) =>
        {
            return new Sequence(new LambdaValue(body, paramNames, env));
        };
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

        // Function composition: LHS ~> RHS where RHS is a variable/lambda
        return (in JsonElement input, Environment env) =>
        {
            var lhsResult = lhsEval(input, env);
            var rhsResult = rhsEval(input, env);

            // If RHS is a lambda, invoke it with LHS as argument
            if (rhsResult.IsLambda)
            {
                var args = new[] { lhsResult };
                return rhsResult.Lambda!.Invoke(args, input, env);
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

            // Numeric filter = index access
            if (result.IsSingleton && TryCoerceToNumber(result.FirstOrDefault, out double idx))
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

            // Boolean filter
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

            // Sort using the compiled terms
            elements.Sort((a, b) =>
            {
                for (int t = 0; t < terms.Length; t++)
                {
                    var (expr, desc) = terms[t];
                    var aVal = expr(a, env);
                    var bVal = expr(b, env);

                    int cmp = CompareSequences(aVal, bVal);
                    if (cmp != 0)
                    {
                        return desc ? -cmp : cmp;
                    }
                }

                return 0;
            });

            // Build result as a multi-value sequence (not a JSON array)
            // so subsequent path steps can iterate over individual elements
            var builder = default(SequenceBuilder);
            for (int i = 0; i < elements.Count; i++)
            {
                builder.Add(elements[i]);
            }

            return builder.ToSequence();
        };
    }

    private static int CompareSequences(Sequence a, Sequence b)
    {
        if (a.IsUndefined && b.IsUndefined)
        {
            return 0;
        }

        if (a.IsUndefined)
        {
            return -1;
        }

        if (b.IsUndefined)
        {
            return 1;
        }

        var aEl = a.FirstOrDefault;
        var bEl = b.FirstOrDefault;

        if (TryCoerceToNumber(aEl, out double aNum) && TryCoerceToNumber(bEl, out double bNum))
        {
            return aNum.CompareTo(bNum);
        }

        if (aEl.ValueKind == JsonValueKind.String && bEl.ValueKind == JsonValueKind.String)
        {
            return string.CompareOrdinal(aEl.GetString(), bEl.GetString());
        }

        return 0;
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

        var results = new List<JsonElement>();
        for (int i = 0; i < inputSeq.Count; i++)
        {
            results.Add(TransformElement(inputSeq[i], matchTexts, updateEval, deleteEval, env));
        }

        return new Sequence(CreateJsonArrayElement(results));
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

            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartObject();

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

                    writer.WritePropertyName(prop.Name);

                    if (updatePropNames.Contains(prop.Name))
                    {
                        // Property is overridden by update — use the update value
                        if (updateObj!.Value.TryGetProperty(prop.Name, out var updateValue))
                        {
                            updateValue.WriteTo(writer);
                        }
                    }
                    else
                    {
                        // Recursively transform the property value
                        TransformElement(prop.Value, matchTexts, updateEval, deleteEval, env).WriteTo(writer);
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
                            writer.WritePropertyName(prop.Name);
                            prop.Value.WriteTo(writer);
                        }
                    }
                }
            }
            else
            {
                // Not a match — recursively transform children
                foreach (var prop in element.EnumerateObject())
                {
                    writer.WritePropertyName(prop.Name);
                    TransformElement(prop.Value, matchTexts, updateEval, deleteEval, env).WriteTo(writer);
                }
            }

            writer.WriteEndObject();
            writer.Flush();
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            return doc.RootElement.Clone();
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            using var ms = new MemoryStream(256);
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray())
            {
                TransformElement(item, matchTexts, updateEval, deleteEval, env).WriteTo(writer);
            }

            writer.WriteEndArray();
            writer.Flush();
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            return doc.RootElement.Clone();
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
        // TODO: Partial application
        return static (in JsonElement input, Environment env) => Sequence.Undefined;
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
                string? s = element.GetString();
                if (s is null)
                {
                    value = 0;
                    return false;
                }

                // Hex prefix: 0x or 0X
                if (s.Length > 2 && s[0] == '0' && (s[1] == 'x' || s[1] == 'X'))
                {
                    if (long.TryParse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hex))
                    {
                        value = hex;
                        return true;
                    }

                    value = 0;
                    return false;
                }

                // Binary prefix: 0b or 0B
                if (s.Length > 2 && s[0] == '0' && (s[1] == 'b' || s[1] == 'B'))
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

                // Octal prefix: 0o or 0O
                if (s.Length > 2 && s[0] == '0' && (s[1] == 'o' || s[1] == 'O'))
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

                return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            default:
                value = 0;
                return false;
        }
    }

    internal static string CoerceToString(Sequence seq)
    {
        if (seq.IsUndefined)
        {
            return string.Empty;
        }

        return CoerceElementToString(seq.FirstOrDefault);
    }

    internal static string CoerceElementToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => FormatNumberLikeJavaScript(element.GetDouble()),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            JsonValueKind.Undefined => string.Empty,
            _ => element.GetRawText(),
        };
    }

    /// <summary>
    /// Formats a double to match JavaScript's Number.toString() behavior:
    /// - Lowercase 'e' in scientific notation
    /// - No leading zero in exponent (e-7, not e-07)
    /// - Decimal form for numbers in range [1e-6, 1e+20)
    /// - At most ~17 significant digits
    /// </summary>
    internal static string FormatNumberLikeJavaScript(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return "null";
        }

        // Use R format for shortest roundtrip representation
        string result = value.ToString("R", CultureInfo.InvariantCulture);

        // Convert uppercase E to lowercase e
        result = result.Replace("E+", "e+").Replace("E-", "e-");

        double abs = Math.Abs(value);

        // JavaScript uses decimal form for [1e-6, 1e+20]
        if (result.Contains('e'))
        {
            if (abs >= 1e-6 && abs < 1e+20)
            {
                // Force decimal format
                result = value.ToString("0.####################", CultureInfo.InvariantCulture);
                return result;
            }

            // For very large integers (like 1e+20), use integer-like representation
            if (abs >= 1e+20 && abs < 1e+21 && value == Math.Floor(value))
            {
                return value.ToString("0", CultureInfo.InvariantCulture);
            }

            // Strip leading zeros from exponent: e-07 → e-7, e+02 → e+2
            result = System.Text.RegularExpressions.Regex.Replace(result, @"e([+-])0+(\d)", "e$1$2");
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
                return element.GetString()?.Length > 0;
            case JsonValueKind.Object:
                // Empty object is falsy
                JsonElement.ObjectEnumerator enumerator = element.EnumerateObject();
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
            JsonValueKind.String => a.GetString() == b.GetString(),
            JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => true,
            JsonValueKind.Array => ArrayDeepEquals(a, b),
            JsonValueKind.Object => ObjectDeepEquals(a, b),
            _ => a.GetRawText() == b.GetRawText(),
        };
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

    internal static JsonElement CreateNumberElement(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new JsonataException("D3001", "A string cannot be generated from this value", 0);
        }

        using var ms = new MemoryStream(32);
        using var writer = new Utf8JsonWriter(ms);
        writer.WriteNumberValue(value);

        writer.Flush();
        ms.Position = 0;
        using var doc = JsonDocument.Parse(ms);
        return doc.RootElement.Clone();
    }

    internal static JsonElement CreateStringElement(string value)
    {
        using var ms = new MemoryStream(value.Length + 32);
        using var writer = new Utf8JsonWriter(ms, RelaxedWriterOptions);
        writer.WriteStringValue(value);
        writer.Flush();
        ms.Position = 0;
        using var doc = JsonDocument.Parse(ms);
        return doc.RootElement.Clone();
    }

    internal static JsonElement CreateBoolElement(bool value)
    {
        return value ? TrueElement : FalseElement;
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

    internal static JsonElement CreateJsonArrayElement(IReadOnlyList<JsonElement> items)
    {
        using var ms = new MemoryStream(256);
        using var writer = new Utf8JsonWriter(ms);
        writer.WriteStartArray();
        foreach (var item in items)
        {
            item.WriteTo(writer);
        }

        writer.WriteEndArray();
        writer.Flush();
        ms.Position = 0;
        using var doc = JsonDocument.Parse(ms);
        return doc.RootElement.Clone();
    }

    internal static JsonElement CreateMatchObject(string match, int index, IReadOnlyList<string> groups)
    {
        using var ms = new MemoryStream(256);
        using var writer = new Utf8JsonWriter(ms, RelaxedWriterOptions);
        writer.WriteStartObject();
        writer.WriteString("match", match);
        writer.WriteNumber("index", index);
        writer.WritePropertyName("groups");
        writer.WriteStartArray();
        foreach (string g in groups)
        {
            writer.WriteStringValue(g);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        ms.Position = 0;
        using var doc = JsonDocument.Parse(ms);
        return doc.RootElement.Clone();
    }
}