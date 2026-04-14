// <copyright file="FunctionalCompiler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
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

    // Thread-local scratch arrays for ApplyPerElementFilterStages to avoid
    // allocating wrapper arrays on every call to ApplyStages.
    [ThreadStatic]
    private static ExpressionEvaluator[]? t_singleStageArray;

    [ThreadStatic]
    private static bool[]? t_singleFlagArray;

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

        // Wrap with per-expression depth tracking for non-leaf nodes.
        // Leaf nodes (numbers, strings, booleans, regex, variables) cannot recurse
        // and never cause stack overflow — skip the overhead for these.
        if (node is not (NumberNode or StringNode or ValueNode or RegexNode or VariableNode))
        {
            evaluator = WrapWithDepthTracking(evaluator);
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
            ParentNode parent => CompileParent(parent),
            PartialNode partial => CompilePartial(partial),
            PlaceholderNode => throw new JsonataException("D1001", SR.D1001_UnexpectedPlaceholderOutsidePartialApplication, node.Position),
            _ => throw new JsonataException("D1001", SR.Format(SR.D1001_UnknownNodeType, node.Type), node.Position),
        };
    }

    private static ExpressionEvaluator WrapWithDepthTracking(ExpressionEvaluator inner)
    {
        return (in JsonElement input, Environment env) =>
        {
            env.EnterEval();
            try
            {
                return inner(in input, env);
            }
            finally
            {
                env.LeaveEval();
            }
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

        // Fast path for single-pair NameNode key + value
        if (group.Pairs is [var pair] && pair.Key is NameNode keyName && pair.Value is NameNode valueName)
        {
            byte[] keyPropUtf8 = System.Text.Encoding.UTF8.GetBytes(keyName.Value);
            byte[] valuePropUtf8 = System.Text.Encoding.UTF8.GetBytes(valueName.Value);

            return (in JsonElement input, Environment env) =>
            {
                var result = inner(input, env);
                return ApplySimpleNamePairGroupBy(result, keyPropUtf8, valuePropUtf8, env);
            };
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
            throw new JsonataException("D3001", SR.D3001_AStringCannotBeGeneratedFromThisValue, 0);
        }

        return (in JsonElement input, Environment env) => Sequence.FromDouble(value, env.Workspace);
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
                throw new JsonataException("D3140", SR.D3140_StringContainsUnpairedSurrogates, 0);
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

    /// <summary>
    /// Determines whether a node is a constant expression that can be evaluated
    /// at compile time (numbers, strings, booleans, null, and compound arrays/objects
    /// composed entirely of constants).
    /// </summary>
    private static bool IsConstantExpression(JsonataNode node)
    {
        if (node.Annotations is not null)
        {
            return false;
        }

        switch (node)
        {
            case NumberNode:
            case ValueNode:
                return true;

            case StringNode s:
                return !HasUnpairedSurrogate(s.Value);

            case UnaryNode { Operator: "-" } u:
                return IsConstantExpression(u.Expression);

            case ArrayConstructorNode arr:
                if (arr.Expressions.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < arr.Expressions.Count; i++)
                {
                    if (!IsConstantExpression(arr.Expressions[i]))
                    {
                        return false;
                    }
                }

                return true;

            case ObjectConstructorNode obj:
                if (obj.Pairs.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < obj.Pairs.Count; i++)
                {
                    if (obj.Pairs[i].Key is not StringNode
                        || !IsConstantExpression(obj.Pairs[i].Value))
                    {
                        return false;
                    }
                }

                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Serializes a constant AST subtree to its JSON representation.
    /// Only call after <see cref="IsConstantExpression"/> returns <see langword="true"/>.
    /// </summary>
    private static string SerializeConstantJson(JsonataNode node)
    {
        switch (node)
        {
            case NumberNode num:
                return num.Value.ToString("R", CultureInfo.InvariantCulture);

            case StringNode str:
                return $"\"{EscapeJsonStringContent(str.Value)}\"";

            case ValueNode val:
                return val.Value; // "true", "false", "null"

            case UnaryNode { Operator: "-" } u:
                if (u.Expression is NumberNode inner)
                {
                    return (-inner.Value).ToString("R", CultureInfo.InvariantCulture);
                }

                return $"-{SerializeConstantJson(u.Expression)}";

            case ArrayConstructorNode arr:
            {
                var sb = new System.Text.StringBuilder("[");
                for (int i = 0; i < arr.Expressions.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append(SerializeConstantJson(arr.Expressions[i]));
                }

                sb.Append(']');
                return sb.ToString();
            }

            case ObjectConstructorNode obj:
            {
                var sb = new System.Text.StringBuilder("{");
                for (int i = 0; i < obj.Pairs.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append('"');
                    sb.Append(EscapeJsonStringContent(((StringNode)obj.Pairs[i].Key).Value));
                    sb.Append("\":");
                    sb.Append(SerializeConstantJson(obj.Pairs[i].Value));
                }

                sb.Append('}');
                return sb.ToString();
            }

            default:
                throw new InvalidOperationException("Not a constant expression");
        }
    }

    /// <summary>
    /// Escapes a string value for embedding inside a JSON string.
    /// </summary>
    private static string EscapeJsonStringContent(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < ' ')
                    {
                        sb.AppendFormat("\\u{0:X4}", (int)c);
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Compiles a constant compound expression (array or object) by pre-evaluating
    /// it at compile time and returning a cached result. The parsed element is
    /// backed by a <see cref="ParsedJsonDocument{T}"/> that owns its own memory
    /// (independent of any workspace).
    /// </summary>
    private static ExpressionEvaluator CompileConstant(JsonataNode node)
    {
        string json = SerializeConstantJson(node);
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(json);
        var element = JsonElement.ParseValue(utf8);
        return (in JsonElement input, Environment env) => new Sequence(element);
    }

    /// <summary>
    /// Evaluates a <c>$zip</c> call with all-constant array arguments at compile time.
    /// Transposes the arrays and returns a cached result element.
    /// </summary>
    private static ExpressionEvaluator CompileConstantZip(FunctionCallNode func)
    {
        var arrays = new List<List<string>>();
        foreach (var arg in func.Arguments)
        {
            var arr = (ArrayConstructorNode)arg;
            var elements = new List<string>(arr.Expressions.Count);
            for (int i = 0; i < arr.Expressions.Count; i++)
            {
                elements.Add(SerializeConstantJson(arr.Expressions[i]));
            }

            arrays.Add(elements);
        }

        int minLen = int.MaxValue;
        for (int i = 0; i < arrays.Count; i++)
        {
            if (arrays[i].Count < minLen)
            {
                minLen = arrays[i].Count;
            }
        }

        if (minLen == 0)
        {
            var emptyArr = JsonElement.ParseValue("[]"u8);
            return (in JsonElement input, Environment env) => new Sequence(emptyArr);
        }

        var json = new System.Text.StringBuilder("[");
        for (int i = 0; i < minLen; i++)
        {
            if (i > 0)
            {
                json.Append(',');
            }

            json.Append('[');
            for (int a = 0; a < arrays.Count; a++)
            {
                if (a > 0)
                {
                    json.Append(',');
                }

                json.Append(arrays[a][i]);
            }

            json.Append(']');
        }

        json.Append(']');

        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(json.ToString());
        var element = JsonElement.ParseValue(utf8);
        return (in JsonElement input, Environment env) => new Sequence(element);
    }

    /// <summary>
    /// Checks whether a node is a simple property chain (PathNode with all NameNode
    /// steps, no annotations or predicates) and extracts the UTF-8 encoded property names.
    /// </summary>
    private static bool TryExtractSimpleChainNames(JsonataNode node, [NotNullWhen(true)] out byte[][]? utf8Names)
    {
        if (node is not PathNode path
            || path.KeepArray
            || path.KeepSingletonArray
            || path.Steps.Count == 0
            || GetStepAnnotations(path) is not null)
        {
            utf8Names = null;
            return false;
        }

        var names = new byte[path.Steps.Count][];
        for (int i = 0; i < path.Steps.Count; i++)
        {
            if (path.Steps[i] is not NameNode nameNode
                || GetStepAnnotations(path.Steps[i]) is not null)
            {
                utf8Names = null;
                return false;
            }

            names[i] = System.Text.Encoding.UTF8.GetBytes(nameNode.Value);
        }

        utf8Names = names;
        return true;
    }

    /// <summary>
    /// Compiles a buffer-fused <c>$zip</c> where all arguments are simple property chains.
    /// Navigates each chain directly into an <see cref="ElementBuffer"/>, extracts the
    /// backing arrays, and builds the result in a single document — eliminating intermediate
    /// Sequence wrappers.
    /// </summary>
    private static ExpressionEvaluator CompileBufferFusedZip2(byte[][] names0, byte[][] names1)
    {
        return (in JsonElement input, Environment env) =>
        {
            var buf0 = default(ElementBuffer);
            var buf1 = default(ElementBuffer);
            try
            {
                JsonataCodeGenHelpers.NavigatePropertyChainInto(input, names0, ref buf0);
                JsonataCodeGenHelpers.NavigatePropertyChainInto(input, names1, ref buf1);
                buf0.GetContents(out var arr0, out var cnt0);
                buf1.GetContents(out var arr1, out var cnt1);
                return new Sequence(JsonataCodeGenHelpers.ZipFromBuffers(arr0, cnt0, arr1, cnt1, env.Workspace));
            }
            finally
            {
                buf0.Dispose();
                buf1.Dispose();
            }
        };
    }

    /// <summary>
    /// Compiles a buffer-fused <c>$zip</c> where one argument is a pre-resolved constant
    /// element and the other is a simple property chain navigated into an <see cref="ElementBuffer"/>.
    /// </summary>
    private static ExpressionEvaluator CompileBufferFusedZipMixed2(
        JsonElement constElement, bool constIsFirst,
        byte[][] chainNames)
    {
        return (in JsonElement input, Environment env) =>
        {
            var buf = default(ElementBuffer);
            try
            {
                JsonataCodeGenHelpers.NavigatePropertyChainInto(input, chainNames, ref buf);
                buf.GetContents(out var arr, out var cnt);

                if (constIsFirst)
                {
                    return new Sequence(JsonataCodeGenHelpers.ZipElementAndBuffer(constElement, arr, cnt, env.Workspace));
                }
                else
                {
                    return new Sequence(JsonataCodeGenHelpers.ZipBufferAndElement(arr, cnt, constElement, env.Workspace));
                }
            }
            finally
            {
                buf.Dispose();
            }
        };
    }

    /// <summary>
    /// Compiles a buffer-fused <c>$zip</c> with 3 arguments that are simple property chains.
    /// </summary>
    private static ExpressionEvaluator CompileBufferFusedZip3(byte[][] names0, byte[][] names1, byte[][] names2)
    {
        return (in JsonElement input, Environment env) =>
        {
            var buf0 = default(ElementBuffer);
            var buf1 = default(ElementBuffer);
            var buf2 = default(ElementBuffer);
            try
            {
                JsonataCodeGenHelpers.NavigatePropertyChainInto(input, names0, ref buf0);
                JsonataCodeGenHelpers.NavigatePropertyChainInto(input, names1, ref buf1);
                JsonataCodeGenHelpers.NavigatePropertyChainInto(input, names2, ref buf2);
                buf0.GetContents(out var arr0, out var cnt0);
                buf1.GetContents(out var arr1, out var cnt1);
                buf2.GetContents(out var arr2, out var cnt2);
                return new Sequence(JsonataCodeGenHelpers.ZipFromBuffers(arr0, cnt0, arr1, cnt1, arr2, cnt2, env.Workspace));
            }
            finally
            {
                buf0.Dispose();
                buf1.Dispose();
                buf2.Dispose();
            }
        };
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
            _ => throw new JsonataException("D1001", SR.Format(SR.D1001_UnknownValue, val.Value), val.Position),
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
                // Environment bindings are shared — return an owned copy so the
                // caller can safely return the backing array after consumption.
                return value.CopyOwned();
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
        byte[] utf8FieldName = System.Text.Encoding.UTF8.GetBytes(name.Value);
        return (in JsonElement input, Environment env) =>
        {
            return LookupField(input, utf8FieldName);
        };

        static Sequence LookupField(in JsonElement input, byte[] utf8FieldName)
        {
            if (input.ValueKind == JsonValueKind.Object)
            {
                return input.TryGetProperty(utf8FieldName, out var value)
                    ? new Sequence(value)
                    : Sequence.Undefined;
            }

            if (input.ValueKind == JsonValueKind.Array)
            {
                var builder = default(SequenceBuilder);
                foreach (var item in input.EnumerateArray())
                {
                    var result = LookupField(item, utf8FieldName);
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

    /// <summary>
    /// Extracts a simple property name from a node that is either a bare NameNode
    /// or a single-step PathNode containing a NameNode (which is how the parser wraps bare names).
    /// </summary>
    private static bool TryGetSimpleNameNode(JsonataNode node, out string name)
    {
        if (node is NameNode nameNode)
        {
            name = nameNode.Value;
            return true;
        }

        if (node is PathNode { Steps: [NameNode innerName] } pathNode
            && !pathNode.KeepArray && !pathNode.KeepSingletonArray
            && GetStepAnnotations(pathNode) is null
            && GetStepAnnotations(innerName) is null)
        {
            name = innerName.Value;
            return true;
        }

        name = default!;
        return false;
    }

    /// <summary>
    /// Extracts a string literal value from a node that is a StringNode.
    /// </summary>
    private static bool TryGetStringNode(JsonataNode node, out string value)
    {
        if (node is StringNode stringNode)
        {
            value = stringNode.Value;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Recursively extracts equality predicate values from an expression tree.
    /// Handles single equality (<c>prop = 'value'</c>) and OR-ed equalities
    /// (<c>prop = 'a' or prop = 'b'</c>) where all branches test the same property.
    /// </summary>
    private static bool TryExtractEqualityPredicateValues(
        JsonataNode expression,
        out string propName,
        out List<string> values)
    {
        // Single equality: prop = 'value'
        if (expression is BinaryNode { Operator: "=" } eq
            && TryGetSimpleNameNode(eq.Lhs, out propName)
            && TryGetStringNode(eq.Rhs, out var singleValue))
        {
            values = [singleValue];
            return true;
        }

        // OR of equalities: lhs or rhs (recursive)
        if (expression is BinaryNode { Operator: "or" } orNode
            && TryExtractEqualityPredicateValues(orNode.Lhs, out var leftProp, out var leftValues)
            && TryExtractEqualityPredicateValues(orNode.Rhs, out var rightProp, out var rightValues)
            && leftProp == rightProp)
        {
            propName = leftProp;
            leftValues.AddRange(rightValues);
            values = leftValues;
            return true;
        }

        propName = default!;
        values = default!;
        return false;
    }

    /// <summary>
    /// Tries to compile a path consisting entirely of NameNode steps (property accesses)
    /// with optional constant-integer index predicates or simple string equality predicates
    /// into a tight evaluator loop that avoids all delegate dispatch and Sequence
    /// intermediaries for the common object-input case.
    /// </summary>
    private static bool TryCompileSimplePropertyChain(PathNode path, out ExpressionEvaluator evaluator)
    {
        // Check all steps are NameNodes with no annotations (or simple constant-integer predicates
        // or simple string equality predicates like [type = 'mobile'])
        if (path.KeepArray || path.KeepSingletonArray || GetStepAnnotations(path) is not null)
        {
            evaluator = default!;
            return false;
        }

        var utf8Names = new byte[path.Steps.Count][];
        int[]? constantIndices = null;

        // String equality predicates: (propertyNameUtf8, expectedValuesUtf8[]) per step
        // Multiple expected values support OR-ed predicates like [type = 'office' or type = 'mobile']
        (byte[] PropName, byte[][] ExpectedValues)[]? equalityPredicates = null;
        bool hasPredicates = false;
        for (int i = 0; i < path.Steps.Count; i++)
        {
            if (path.Steps[i] is not NameNode nameNode)
            {
                evaluator = default!;
                return false;
            }

            var stepAnnotations = GetStepAnnotations(path.Steps[i]);
            if (stepAnnotations is not null)
            {
                bool isSimpleAnnotation =
                    stepAnnotations.Group is null
                    && stepAnnotations.Focus is null
                    && stepAnnotations.Index is null
                    && stepAnnotations.AncestorLabels is null
                    && stepAnnotations.TupleLabels is null
                    && !stepAnnotations.Tuple;

                if (!isSimpleAnnotation || stepAnnotations.Stages.Count != 1)
                {
                    evaluator = default!;
                    return false;
                }

                // Allow a single constant non-negative integer predicate
                if (stepAnnotations.Stages[0] is FilterNode { Expression: NumberNode numNode }
                    && numNode.Value >= 0
                    && numNode.Value <= int.MaxValue
                    && numNode.Value == Math.Floor(numNode.Value))
                {
                    if (constantIndices is null)
                    {
                        constantIndices = new int[path.Steps.Count];
                        constantIndices.AsSpan().Fill(-1);
                    }

                    constantIndices[i] = (int)numNode.Value;
                    hasPredicates = true;
                }

                // Allow a string equality predicate or OR-ed string equality predicates on the same property
                // e.g. [type = 'mobile'] or [type = 'office' or type = 'mobile']
                else if (stepAnnotations.Stages[0] is FilterNode filterNode
                         && TryExtractEqualityPredicateValues(filterNode.Expression, out var filterPropName, out var filterValues))
                {
                    if (equalityPredicates is null)
                    {
                        equalityPredicates = new (byte[], byte[][])[path.Steps.Count];
                    }

                    byte[] propNameUtf8 = System.Text.Encoding.UTF8.GetBytes(filterPropName);
                    byte[][] valuesUtf8 = new byte[filterValues.Count][];
                    for (int v = 0; v < filterValues.Count; v++)
                    {
                        valuesUtf8[v] = System.Text.Encoding.UTF8.GetBytes(filterValues[v]);
                    }

                    equalityPredicates[i] = (propNameUtf8, valuesUtf8);
                    hasPredicates = true;
                }
                else
                {
                    evaluator = default!;
                    return false;
                }
            }
            else if (constantIndices is not null)
            {
                constantIndices[i] = -1;
            }

            utf8Names[i] = System.Text.Encoding.UTF8.GetBytes(nameNode.Value);
        }

        if (hasPredicates)
        {
            // We have at least one predicate (constant index or equality).
            // Ensure constantIndices exists and all non-predicate slots are -1.
            if (constantIndices is null)
            {
                constantIndices = new int[path.Steps.Count];
                constantIndices.AsSpan().Fill(-1);
            }

            evaluator = (in JsonElement input, Environment env) =>
            {
                return EvalPropertyChainWithPredicates(input, utf8Names, constantIndices, equalityPredicates);
            };
        }
        else
        {
            evaluator = (in JsonElement input, Environment env) =>
            {
                return EvalSimplePropertyChain(input, utf8Names);
            };
        }

        return true;

        static Sequence EvalSimplePropertyChain(in JsonElement input, byte[][] utf8Names)
        {
            JsonElement current = input;

            for (int step = 0; step < utf8Names.Length; step++)
            {
                if (current.ValueKind == JsonValueKind.Object)
                {
                    if (!current.TryGetProperty(utf8Names[step], out current))
                    {
                        return Sequence.Undefined;
                    }
                }
                else if (current.ValueKind == JsonValueKind.Array)
                {
                    // Auto-flatten: map remaining steps over array elements
                    return EvalChainOverArray(current, utf8Names, step);
                }
                else
                {
                    return Sequence.Undefined;
                }
            }

            return new Sequence(current);
        }

        static Sequence EvalChainOverArray(in JsonElement array, byte[][] utf8Names, int fromStep)
        {
            var builder = default(SequenceBuilder);
            EvalChainOverArrayInto(array, utf8Names, fromStep, ref builder);
            return builder.ToSequence();
        }

        static void EvalChainOverArrayInto(in JsonElement array, byte[][] utf8Names, int fromStep, ref SequenceBuilder builder)
        {
            foreach (var item in array.EnumerateArray())
            {
                JsonElement current = item;
                bool found = true;
                for (int step = fromStep; step < utf8Names.Length; step++)
                {
                    if (current.ValueKind == JsonValueKind.Object)
                    {
                        if (!current.TryGetProperty(utf8Names[step], out current))
                        {
                            found = false;
                            break;
                        }
                    }
                    else if (current.ValueKind == JsonValueKind.Array)
                    {
                        // Nested array: recurse into the same builder to avoid intermediate allocations
                        EvalChainOverArrayInto(current, utf8Names, step, ref builder);
                        found = false;
                        break;
                    }
                    else
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                {
                    // Auto-flatten: if a property step returns an array in a multi-context,
                    // expand it into individual elements
                    if (current.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var child in current.EnumerateArray())
                        {
                            builder.Add(child);
                        }
                    }
                    else
                    {
                        builder.Add(current);
                    }
                }
            }
        }

        static Sequence EvalPropertyChainWithPredicates(in JsonElement input, byte[][] utf8Names, int[] constantIndices, (byte[] PropName, byte[][] ExpectedValues)[]? equalityPredicates)
        {
            return EvalFromStep(input, utf8Names, constantIndices, equalityPredicates, 0);
        }

        static Sequence EvalFromStep(in JsonElement input, byte[][] utf8Names, int[] constantIndices, (byte[] PropName, byte[][] ExpectedValues)[]? equalityPredicates, int startStep)
        {
            JsonElement current = input;

            for (int step = startStep; step < utf8Names.Length; step++)
            {
                if (current.ValueKind == JsonValueKind.Object)
                {
                    if (!current.TryGetProperty(utf8Names[step], out current))
                    {
                        return Sequence.Undefined;
                    }

                    // Apply constant index if present for this step
                    if (constantIndices[step] >= 0)
                    {
                        if (current.ValueKind == JsonValueKind.Array)
                        {
                            int idx = constantIndices[step];
                            if (idx < current.GetArrayLength())
                            {
                                current = current[idx];
                            }
                            else
                            {
                                return Sequence.Undefined;
                            }
                        }
                        else
                        {
                            // Singleton: index 0 returns the value itself
                            if (constantIndices[step] != 0)
                            {
                                return Sequence.Undefined;
                            }
                        }
                    }

                    // Apply equality predicate if present for this step
                    else if (equalityPredicates?.Length > step && equalityPredicates[step].PropName is not null)
                    {
                        var pred = equalityPredicates[step];
                        if (current.ValueKind == JsonValueKind.Array)
                        {
                            // Filter array elements matching the predicate
                            return FilterAndContinue(current, utf8Names, constantIndices, equalityPredicates, step, pred.PropName, pred.ExpectedValues);
                        }
                        else if (current.ValueKind == JsonValueKind.Object)
                        {
                            // Singleton object: check if it matches
                            if (!MatchesEqualityPredicate(current, pred.PropName, pred.ExpectedValues))
                            {
                                return Sequence.Undefined;
                            }
                        }
                        else
                        {
                            return Sequence.Undefined;
                        }
                    }
                }
                else if (current.ValueKind == JsonValueKind.Array)
                {
                    // Array input: collect-then-predicate semantics
                    return CollectAndContinue(current, utf8Names, constantIndices, equalityPredicates, step);
                }
                else
                {
                    return Sequence.Undefined;
                }
            }

            return new Sequence(current);
        }

        static bool MatchesEqualityPredicate(in JsonElement element, byte[] propName, byte[][] expectedValues)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!element.TryGetProperty(propName, out var propValue))
            {
                return false;
            }

            if (propValue.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            for (int i = 0; i < expectedValues.Length; i++)
            {
                if (propValue.ValueEquals(expectedValues[i]))
                {
                    return true;
                }
            }

            return false;
        }

        static Sequence FilterAndContinue(in JsonElement array, byte[][] utf8Names, int[] constantIndices, (byte[] PropName, byte[][] ExpectedValues)[]? equalityPredicates, int step, byte[] propName, byte[][] expectedValues)
        {
            var builder = default(SequenceBuilder);
            foreach (var item in array.EnumerateArray())
            {
                if (MatchesEqualityPredicate(item, propName, expectedValues))
                {
                    if (step + 1 < utf8Names.Length)
                    {
                        var result = EvalFromStep(item, utf8Names, constantIndices, equalityPredicates, step + 1);
                        if (!result.IsUndefined)
                        {
                            builder.AddRange(result);
                        }
                    }
                    else
                    {
                        builder.Add(item);
                    }
                }
            }

            return builder.ToSequence();
        }

        static Sequence CollectAndContinue(in JsonElement array, byte[][] utf8Names, int[] constantIndices, (byte[] PropName, byte[][] ExpectedValues)[]? equalityPredicates, int step)
        {
            bool hasIndexThisStep = constantIndices[step] >= 0;
            bool hasEqPredThisStep = equalityPredicates?.Length > step && equalityPredicates[step].PropName is not null;
            bool globalIndex = hasIndexThisStep && step == 0;
            bool perElementIndex = hasIndexThisStep && step > 0;

            var builder = default(SequenceBuilder);
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    if (item.TryGetProperty(utf8Names[step], out var propValue))
                    {
                        if (perElementIndex)
                        {
                            // Per-element index (step > 0): apply index inline, then continue
                            if (propValue.ValueKind == JsonValueKind.Array)
                            {
                                int idx = constantIndices[step];
                                if (idx < propValue.GetArrayLength())
                                {
                                    propValue = propValue[idx];
                                }
                                else
                                {
                                    continue;
                                }
                            }
                            else if (constantIndices[step] != 0)
                            {
                                continue;
                            }

                            if (step + 1 < utf8Names.Length)
                            {
                                var result = EvalFromStep(propValue, utf8Names, constantIndices, equalityPredicates, step + 1);
                                if (!result.IsUndefined)
                                {
                                    builder.AddRange(result);
                                }
                            }
                            else
                            {
                                builder.Add(propValue);
                            }
                        }
                        else if (hasEqPredThisStep)
                        {
                            // Per-element equality predicate: filter the property value (which should be an array)
                            var pred = equalityPredicates![step];
                            if (propValue.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var child in propValue.EnumerateArray())
                                {
                                    if (MatchesEqualityPredicate(child, pred.PropName, pred.ExpectedValues))
                                    {
                                        if (step + 1 < utf8Names.Length)
                                        {
                                            var result = EvalFromStep(child, utf8Names, constantIndices, equalityPredicates, step + 1);
                                            if (!result.IsUndefined)
                                            {
                                                builder.AddRange(result);
                                            }
                                        }
                                        else
                                        {
                                            builder.Add(child);
                                        }
                                    }
                                }
                            }
                            else if (propValue.ValueKind == JsonValueKind.Object)
                            {
                                // Singleton: check if it matches
                                if (MatchesEqualityPredicate(propValue, pred.PropName, pred.ExpectedValues))
                                {
                                    if (step + 1 < utf8Names.Length)
                                    {
                                        var result = EvalFromStep(propValue, utf8Names, constantIndices, equalityPredicates, step + 1);
                                        if (!result.IsUndefined)
                                        {
                                            builder.AddRange(result);
                                        }
                                    }
                                    else
                                    {
                                        builder.Add(propValue);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // No per-element index or predicate: collect with auto-flatten
                            if (propValue.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var child in propValue.EnumerateArray())
                                {
                                    builder.Add(child);
                                }
                            }
                            else
                            {
                                builder.Add(propValue);
                            }
                        }
                    }
                }
                else if (item.ValueKind == JsonValueKind.Array)
                {
                    var inner = CollectAndContinue(item, utf8Names, constantIndices, equalityPredicates, step);
                    if (!inner.IsUndefined)
                    {
                        builder.AddRange(inner);
                    }
                }
            }

            // For step 0: apply global index to collected results
            if (globalIndex)
            {
                int idx = constantIndices[step];
                if (idx >= builder.Count)
                {
                    return Sequence.Undefined;
                }

                JsonElement indexed = builder[idx];

                if (step + 1 >= utf8Names.Length)
                {
                    return new Sequence(indexed);
                }

                return EvalFromStep(indexed, utf8Names, constantIndices, equalityPredicates, step + 1);
            }

            // No index, more steps: continue per-element on collected results
            if (!perElementIndex && !hasEqPredThisStep && step + 1 < utf8Names.Length)
            {
                var resultBuilder = default(SequenceBuilder);
                for (int i = 0; i < builder.Count; i++)
                {
                    var result = EvalFromStep(builder[i], utf8Names, constantIndices, equalityPredicates, step + 1);
                    if (!result.IsUndefined)
                    {
                        resultBuilder.AddRange(result);
                    }
                }

                return resultBuilder.ToSequence();
            }

            return builder.ToSequence();
        }
    }

    private static ExpressionEvaluator CompilePath(PathNode path)
    {
        if (path.Steps.Count == 0)
        {
            return static (in JsonElement input, Environment env) => Sequence.Undefined;
        }

        // Fast path: simple property chain (a.b.c.d) with no annotations.
        // Avoids all delegate dispatch, Sequence intermediaries, and stage checks.
        if (TryCompileSimplePropertyChain(path, out var fastEvaluator))
        {
            return fastEvaluator;
        }

        // Compile each step and its annotations (stages/filters)
        var steps = new ExpressionEvaluator[path.Steps.Count];
        var stages = new ExpressionEvaluator[]?[path.Steps.Count];
        var stageIsSortFlags = new bool[]?[path.Steps.Count];
        var stageIndexBindingVars = new string?[]?[path.Steps.Count];
        var constantIntStageValues = new int[]?[path.Steps.Count];
        var focusVars = new string?[path.Steps.Count];
        var indexVars = new string?[path.Steps.Count];
        var ancestorLabels = new string[]?[path.Steps.Count];
        var tupleLabels = new string[]?[path.Steps.Count];
        var isPropertyStep = new bool[path.Steps.Count];
        var isConsArrayStep = new bool[path.Steps.Count];
        var isSortStep = new bool[path.Steps.Count];
        var sortTermsPerStep = new (ExpressionEvaluator Expr, bool Descending)[]?[path.Steps.Count];
        (ExpressionEvaluator Key, ExpressionEvaluator Value)[]? groupByPairs = null;
        GroupBy? groupByAst = null;

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
                    stageIndexBindingVars[i] = new string?[annotations.Stages.Count];
                    constantIntStageValues[i] = new int[annotations.Stages.Count];
                    for (int ci = 0; ci < constantIntStageValues[i]!.Length; ci++)
                    {
                        constantIntStageValues[i]![ci] = -1; // -1 = not a constant integer
                    }

                    for (int s = 0; s < annotations.Stages.Count; s++)
                    {
                        var stage = annotations.Stages[s];
                        if (stage is FilterNode filterNode)
                        {
                            // Compile just the predicate expression — ApplyStages handles filtering logic
                            stages[i]![s] = Compile(filterNode.Expression);

                            // Detect constant integer predicates for fast-path indexing
                            if (filterNode.Expression is NumberNode numNode)
                            {
                                double d = numNode.Value;
                                if (d >= 0 && d == Math.Floor(d) && d <= int.MaxValue)
                                {
                                    constantIntStageValues[i]![s] = (int)d;
                                }
                            }
                        }
                        else if (stage is SortNode sortNode)
                        {
                            // Use focus-aware sort when this step has a focus binding,
                            // so the focus variable is re-bound per comparison element.
                            stages[i]![s] = focusVars[i] is not null
                                ? CompileFocusSortStage(sortNode, focusVars[i]!)
                                : CompileSortStage(sortNode);
                            stageIsSortFlags[i]![s] = true;
                        }
                        else if (stage is VariableNode varStage)
                        {
                            // Post-predicate index binding (e.g. books[$filter]#$ib2).
                            // Treated as a pass-through stage that binds the variable
                            // to each element's position.
                            stageIndexBindingVars[i]![s] = varStage.Name;
                            stages[i]![s] = static (in JsonElement _, Environment __) => Sequence.Undefined;
                        }
                        else
                        {
                            stages[i]![s] = Compile(stage);
                        }
                    }
                }

                focusVars[i] = annotations.Focus;
                indexVars[i] = annotations.Index;
                if (annotations.AncestorLabels is not null)
                {
                    ancestorLabels[i] = annotations.AncestorLabels.ToArray();
                }

                if (annotations.TupleLabels is not null)
                {
                    tupleLabels[i] = annotations.TupleLabels.ToArray();
                }

                if (annotations.Group is not null)
                {
                    var group = annotations.Group;
                    groupByAst = group;
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
            groupByAst = group;
            groupByPairs = new (ExpressionEvaluator, ExpressionEvaluator)[group.Pairs.Count];
            for (int g = 0; g < group.Pairs.Count; g++)
            {
                groupByPairs[g] = (Compile(group.Pairs[g].Key), Compile(group.Pairs[g].Value));
            }
        }

        // Detect single-pair group-by with NameNode key + NameNode value.
        // For this pattern, we can use the CVB object itself as the grouping accumulator,
        // avoiding Dictionary/List allocations, string extraction, and 2-phase evaluation.
        byte[]? simpleGroupByKeyPropUtf8 = null;
        byte[]? simpleGroupByValuePropUtf8 = null;

        if (groupByAst is not null && groupByPairs is { Length: 1 }
            && groupByAst.Pairs[0].Key is NameNode keyNameNode
            && groupByAst.Pairs[0].Value is NameNode valueNameNode)
        {
            simpleGroupByKeyPropUtf8 = System.Text.Encoding.UTF8.GetBytes(keyNameNode.Value);
            simpleGroupByValuePropUtf8 = System.Text.Encoding.UTF8.GetBytes(valueNameNode.Value);
        }

        var keepSingleton = path.KeepSingletonArray;

        // KeepSingletonArray may not have been propagated from step-level KeepArray
        // when the path was created by ProcessSortBinary or ProcessIndexBinary
        // rather than ProcessDot. Check steps directly as a fallback.
        if (!keepSingleton)
        {
            for (int k = 0; k < path.Steps.Count; k++)
            {
                if (path.Steps[k].KeepArray)
                {
                    keepSingleton = true;
                    break;
                }
            }
        }

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

        // Detect steps that are simple name lookups with no annotations.
        // At runtime, these use direct TryGetProperty instead of delegate dispatch,
        // eliminating per-step delegate calls and Sequence intermediaries.
        byte[]?[]? inlineNameUtf8 = null;
        for (int i = 0; i < path.Steps.Count; i++)
        {
            if (path.Steps[i] is NameNode nameNode
                && stages[i] is null
                && focusVars[i] is null
                && indexVars[i] is null
                && ancestorLabels[i] is null
                && tupleLabels[i] is null)
            {
                inlineNameUtf8 ??= new byte[]?[path.Steps.Count];
                inlineNameUtf8[i] = System.Text.Encoding.UTF8.GetBytes(nameNode.Value);
            }
        }

        // Detect DescendantNode followed by NameNode (both with no annotations).
        // At runtime, this pair is fused into a single-pass recursive traversal that
        // collects only matching properties, avoiding the intermediate all-descendants
        // Sequence that the two-step approach would build.
        byte[]?[]? fusedDescendantNameUtf8 = null;
        for (int i = 0; i < path.Steps.Count - 1; i++)
        {
            if (path.Steps[i] is DescendantNode
                && stages[i] is null
                && focusVars[i] is null
                && indexVars[i] is null
                && ancestorLabels[i] is null
                && tupleLabels[i] is null
                && inlineNameUtf8?[i + 1] is byte[] nextNameBytes)
            {
                fusedDescendantNameUtf8 ??= new byte[]?[path.Steps.Count];
                fusedDescendantNameUtf8[i] = nextNameBytes;
            }
        }

        return (in JsonElement input, Environment env) =>
        {
            return EvalPathFrom(new Sequence(input), 0, env);

            Sequence EvalPathFrom(Sequence initial, int startStep, Environment env)
            {
                Sequence current = initial;

                // Tracks whether we own the backing array of `current` and are responsible
                // for returning it to ArrayPool. Set true when the multi-value branch
                // creates `current` via SequenceBuilder.ToSequence(). Cleared before
                // dispatching to sub-evaluators (focus/ancestor/index/tuple) that take
                // ownership of current's elements.
                bool currentArrayOwned = false;

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

                    // Fused descendant+name fast path: single-pass recursive traversal
                    // that collects only properties matching the name, skipping the
                    // intermediate all-descendants Sequence. Handles the DescendantNode
                    // at stepIdx AND the NameNode at stepIdx+1 in one operation.
                    if (fusedDescendantNameUtf8?[stepIdx] is byte[] descNameBytes
                        && tupleGroupIndices is null)
                    {
                        if (currentArrayOwned) { current.ReturnBackingArray(); currentArrayOwned = false; }

                        var descBuilder = default(SequenceBuilder);
                        for (int i = 0; i < current.Count; i++)
                        {
                            CollectDescendantProperty(current[i], descNameBytes, ref descBuilder);
                        }

                        current = descBuilder.ToSequence();
                        currentArrayOwned = current.Count >= 2;
                        stepIdx++; // skip the next step (NameNode) — already handled
                        continue;
                    }

                    // Inline name step fast path: direct UTF-8 property lookup without
                    // delegate dispatch. For simple name steps with no annotations, this
                    // eliminates the CompileName delegate call, Sequence intermediaries,
                    // and all annotation/stage checks (verified null at compile time).
                    // Skipped when tuple tracking is active (preceding sort with index binding).
                    if (inlineNameUtf8?[stepIdx] is byte[] nameBytes
                        && tupleGroupIndices is null)
                    {
                        if (current.IsSingleton)
                        {
                            var element = current.FirstOrDefault;
                            if (element.ValueKind == JsonValueKind.Array)
                            {
                                // Auto-flatten: map name lookup over array elements
                                var nameBuilder = default(SequenceBuilder);
                                InlineNameOverArray(element, nameBytes, ref nameBuilder);
                                current = nameBuilder.ToSequence();
                                currentArrayOwned = current.Count >= 2;
                            }
                            else if (element.ValueKind == JsonValueKind.Object)
                            {
                                current = element.TryGetProperty(nameBytes, out var val)
                                    ? new Sequence(val) : Sequence.Undefined;
                            }
                            else
                            {
                                current = Sequence.Undefined;
                            }
                        }
                        else
                        {
                            // Multi-value: inline name lookup per element with auto-flatten
                            Sequence previousCurrent = currentArrayOwned ? current : default;
                            bool hadOwnedArray = currentArrayOwned;
                            currentArrayOwned = false;

                            var nameBuilder = default(SequenceBuilder);
                            for (int i = 0; i < current.Count; i++)
                            {
                                var element = current[i];
                                if (element.ValueKind == JsonValueKind.Object)
                                {
                                    if (element.TryGetProperty(nameBytes, out var val))
                                    {
                                        if (val.ValueKind == JsonValueKind.Array)
                                        {
                                            foreach (var item in val.EnumerateArray())
                                            {
                                                nameBuilder.Add(item);
                                            }
                                        }
                                        else
                                        {
                                            nameBuilder.Add(val);
                                        }
                                    }
                                }
                                else if (element.ValueKind == JsonValueKind.Array)
                                {
                                    InlineNameOverArray(element, nameBytes, ref nameBuilder);
                                }
                            }

                            if (hadOwnedArray) { previousCurrent.ReturnBackingArray(); }
                            current = nameBuilder.ToSequence();
                            currentArrayOwned = current.Count >= 2;
                        }

                        continue;
                    }

                    var step = steps[stepIdx];
                    var focusVar = focusVars[stepIdx];
                    var indexVar = indexVars[stepIdx];
                    var labels = ancestorLabels[stepIdx];

                    // Ancestor binding (parent operator %) — per-element context binding.
                    // When a step has ancestor labels, the INPUT to this step is the parent
                    // context that % operators in downstream steps need. We must process
                    // per-element so each element carries its correct ancestor binding.
                    if (labels is not null && focusVar is null && indexVar is null)
                    {
                        // Relinquish ownership — EvalAncestorStep iterates current's elements.
                        if (currentArrayOwned) { current.ReturnBackingArray(); currentArrayOwned = false; }
                        return EvalAncestorStep(current, stepIdx, env);
                    }

                    // Tuple block step — when a step is a tuple-mode block (containing
                    // inner ancestor labels), evaluate per-input-element so that inner
                    // __t_ bindings are correctly accumulated across all input elements.
                    // For non-last steps, also continues remaining steps per-result-element
                    // while inner ancestor bindings are still fresh in the env.
                    var tLabels = tupleLabels[stepIdx];
                    if (tLabels is not null)
                    {
                        if (currentArrayOwned) { current.ReturnBackingArray(); currentArrayOwned = false; }
                        return EvalTupleBlockStep(current, stepIdx, tLabels, env);
                    }

                    // Focus binding (@$var) — cross-join semantics.
                    // The focus variable captures each element of the step's result.
                    // Subsequent steps evaluate from the PARENT context (input to this step),
                    // not from the step's output. This creates a cross-join when multiple
                    // focus-bound steps are chained (e.g. loans@$l.books@$b).
                    if (focusVar is not null)
                    {
                        if (currentArrayOwned) { current.ReturnBackingArray(); currentArrayOwned = false; }
                        return EvalFocusStep(current, stepIdx, env);
                    }

                    // Index binding (#$var) without focus — per-element propagation.
                    // When a step has #$var AND there are subsequent non-sort steps, we must
                    // evaluate remaining steps per-element so each element carries its correct
                    // index. Skip this when the next step is a sort (sort needs all elements
                    // as a batch). When a downstream sort exists beyond intermediate steps,
                    // use EvalIndexWithSort to collect all elements with their group indices,
                    // evaluate intermediate steps, sort globally, then continue.
                    if (indexVar is not null && stepIdx + 1 < steps.Length)
                    {
                        int downstreamSortIdx = -1;
                        for (int s = stepIdx + 1; s < steps.Length; s++)
                        {
                            if (isSortStep[s])
                            {
                                downstreamSortIdx = s;
                                break;
                            }
                        }

                        if (downstreamSortIdx < 0)
                        {
                            if (currentArrayOwned) { current.ReturnBackingArray(); currentArrayOwned = false; }
                            return EvalIndexStep(current, stepIdx, env);
                        }
                        else if (downstreamSortIdx == stepIdx + 1)
                        {
                            // Sort is immediately next — skip index step, let sort handle grouping
                        }
                        else
                        {
                            // Sort is downstream but not immediate — evaluate index step
                            // and intermediate steps, then sort globally with index tracking.
                            if (currentArrayOwned) { current.ReturnBackingArray(); currentArrayOwned = false; }
                            return EvalIndexWithSort(current, stepIdx, downstreamSortIdx, env);
                        }
                    }

                    // Sort steps need all elements as a batch — collect, flatten, sort.
                    if (isSortStep[stepIdx])
                    {
                        if (tupleIndexVar is not null && !current.IsSingleton
                            && sortTermsPerStep[stepIdx] is not null)
                        {
                            // Tuple-aware sort: sort elements inline while tracking group indices.
                            var sortElems = default(SequenceBuilder);
                            using var groups = new ValueListBuilder<int>(Math.Max(current.Count, 8));
                            for (int i = 0; i < current.Count; i++)
                            {
                                var el = current[i];
                                if (el.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var item in el.EnumerateArray())
                                    {
                                        sortElems.Add(item);
                                        groups.Append(tupleGroupIndices is not null && i < tupleGroupIndices.Length
                                            ? tupleGroupIndices[i] : i);
                                    }
                                }
                                else
                                {
                                    sortElems.Add(el);
                                    groups.Append(tupleGroupIndices is not null && i < tupleGroupIndices.Length
                                        ? tupleGroupIndices[i] : i);
                                }
                            }

                            // All elements copied out of current — return its backing array.
                            if (currentArrayOwned) { current.ReturnBackingArray(); currentArrayOwned = false; }

                            if (sortElems.Count > 1)
                            {
                                // Sort index array using the sort terms directly (avoids
                                // round-tripping through JSON which breaks element identity).
                                var terms = sortTermsPerStep[stepIdx]!;
                                var indices = RentSortIndices(sortElems.Count);
                                SortByTerms(indices, sortElems, terms, env);

                                // Reorder elements and groups
                                var sortedElems = default(SequenceBuilder);
                                var sortedGroupArr = new int[sortElems.Count];
                                for (int i = 0; i < sortElems.Count; i++)
                                {
                                    sortedElems.Add(sortElems[indices[i]]);
                                    sortedGroupArr[i] = groups[indices[i]];
                                }

                                ReturnSortIndices(indices);
                                sortElems.ReturnArray();
                                current = new Sequence(JsonataHelpers.ArrayFromBuilder(ref sortedElems, env.Workspace));
                                tupleGroupIndices = sortedGroupArr;
                            }
                            else if (sortElems.Count == 1)
                            {
                                current = new Sequence(sortElems[0]);
                                sortElems.ReturnArray();
                                tupleGroupIndices = [groups[0]];
                            }
                            else
                            {
                                sortElems.ReturnArray();
                            }
                        }
                        else
                        {
                            var sortElements = CollectFlatElements(current);

                            // All elements copied out — return current's backing array.
                            if (currentArrayOwned) { current.ReturnBackingArray(); currentArrayOwned = false; }

                            if (sortElements.Count <= 1)
                            {
                                if (sortElements.Count == 1)
                                {
                                    current = new Sequence(sortElements[0]);
                                }

                                sortElements.ReturnArray();
                            }
                            else
                            {
                                var arrayInput = JsonataHelpers.ArrayFromBuilder(ref sortElements, env.Workspace);
                                current = step(arrayInput, env);
                            }

                            tupleGroupIndices = null;
                        }

                        // Apply any stages on the sort step itself (e.g. filters after sort)
                        if (stages[stepIdx] is not null)
                        {
                            current = ApplyStages(current, stages[stepIdx]!, stageIsSortFlags[stepIdx]!, env, indexVars[stepIdx], stageIndexBindingVars[stepIdx], constantIntStageValues[stepIdx]);
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
                            currentArrayOwned = current.Count >= 2;
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
                        currentArrayOwned = current.Count >= 2;
                    }

                    // Per-element filter stages are only applied for steps after the first
                    // (stepIdx > 0). At stepIdx 0, the multi-value input comes from auto-mapping
                    // over an array input, and stages should be applied globally (matching JSONata's
                    // semantics where a[0].b picks the first a globally, but $.a[0].b picks per-element).
                    var perElementStages = stepIdx > 0 ? stages[stepIdx] : null;
                    var perElementSortFlags = stepIdx > 0 ? stageIsSortFlags[stepIdx] : null;
                    var perElementConstantInts = stepIdx > 0 ? constantIntStageValues[stepIdx] : null;

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
                                perElementStages, perElementSortFlags, perElementConstantInts);
                        }
                        else
                        {
                            current = step(element, env);

                            // Apply per-element filter stages (non-sort) to the single result
                            current = ApplyPerElementFilterStages(current,
                                perElementStages, perElementSortFlags, env, perElementConstantInts);
                        }
                    }
                    else
                    {
                        // Multi-value: map step over all values.
                        // We need to return current's backing array after all elements are
                        // copied out. Save the old sequence so we can return after the loop.
                        Sequence previousCurrent = currentArrayOwned ? current : default;
                        bool hadOwnedArray = currentArrayOwned;
                        currentArrayOwned = false;

                        var builder = default(SequenceBuilder);
                        for (int i = 0; i < current.Count; i++)
                        {
                            var element = current[i];

                            // Restore per-element tuple binding (index variable from a
                            // preceding step, preserved through sort reordering).
                            if (tupleGroupIndices is not null && tupleIndexVar is not null
                                && i < tupleGroupIndices.Length)
                            {
                                env.Bind(tupleIndexVar, Sequence.FromDouble(tupleGroupIndices[i], env.Workspace));
                            }

                            if (indexVar is not null)
                            {
                                env.Bind(indexVar, Sequence.FromDouble(i, env.Workspace));
                            }

                            Sequence stepResult;

                            if (element.ValueKind == JsonValueKind.Array && shouldFlatten)
                            {
                                stepResult = FlattenArrayStep(element, step, env, isConsArrayStep[stepIdx],
                                    perElementStages, perElementSortFlags, perElementConstantInts);
                            }
                            else
                            {
                                stepResult = step(element, env);

                                // Apply per-element filter stages
                                stepResult = ApplyPerElementFilterStages(stepResult,
                                    perElementStages, perElementSortFlags, env, perElementConstantInts);
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

                            // All elements from stepResult have been value-copied into builder.
                            stepResult.ReturnBackingArray();
                        }

                        // All elements have been value-copied out of previousCurrent.
                        // Safe to return its backing array now.
                        if (hadOwnedArray) { previousCurrent.ReturnBackingArray(); }

                        current = builder.ToSequence();
                        currentArrayOwned = current.Count >= 2;
                    }

                    // Apply stages globally:
                    // - For stepIdx == 0: all stages (filter + sort) are applied globally
                    // - For stepIdx > 0: only sort stages (filter stages were applied per-element)
                    if (stages[stepIdx] is not null)
                    {
                        // ApplyStages reads from current then produces a new Sequence.
                        // We can't return current's array before the call (it still reads from it).
                        // Instead, save the owned flag and return after stages replace current.
                        bool stagesHadOwnedArray = currentArrayOwned;
                        Sequence stagesPrevious = currentArrayOwned ? current : default;
                        currentArrayOwned = false;

                        if (stepIdx == 0)
                        {
                            current = ApplyStages(current, stages[stepIdx]!, stageIsSortFlags[stepIdx]!, env, indexVars[stepIdx], stageIndexBindingVars[stepIdx], constantIntStageValues[stepIdx]);
                        }
                        else
                        {
                            current = ApplySortStagesOnly(current, stages[stepIdx]!, stageIsSortFlags[stepIdx]!, env);
                        }

                        // Only return the old array if stages produced a genuinely new Sequence.
                        // ApplySortStagesOnly may return the same Sequence unchanged when there
                        // are no sort stages — returning the array in that case would corrupt current.
                        if (stagesHadOwnedArray && !current.SharesBackingArray(stagesPrevious))
                        {
                            stagesPrevious.ReturnBackingArray();
                        }
                        else if (stagesHadOwnedArray)
                        {
                            // Stages returned the same sequence — re-assert ownership.
                            currentArrayOwned = true;
                        }
                    }
                }

                // Apply group-by if present (only at the outermost level;
                // focus-bound paths handle group-by inside EvalFocusStep).
                if (startStep == 0 && groupByPairs is not null)
                {
                    // GroupBy consumes current's elements — return our array.
                    if (currentArrayOwned) { current.ReturnBackingArray(); currentArrayOwned = false; }

                    // Fast path: single-pair group-by with NameNode key + value.
                    // Uses the CVB object directly as the grouping accumulator.
                    if (simpleGroupByKeyPropUtf8 is not null && tupleIndexVar is null)
                    {
                        current = ApplySimpleNamePairGroupBy(current, simpleGroupByKeyPropUtf8, simpleGroupByValuePropUtf8!, env);
                    }
                    else
                    {
                        current = ApplyGroupBy(current, groupByPairs, env, tupleIndexVar, tupleGroupIndices);
                    }
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

                // Don't return current's array here — caller takes ownership.
                return current;
            }

            // Evaluates a tuple-mode block step and remaining steps per-element.
            // The block contains an inner path with ancestor labels. For each input
            // element, we evaluate the block (which stores tuple bindings), then
            // immediately evaluate remaining steps per-result-element while the
            // bindings are still fresh. This avoids the problem where auto-flatten
            // evaluates the block per-element but overwrites tuple bindings each time.
            //
            // For the last step in a path, there are no remaining steps to evaluate.
            // In that case, we accumulate the inner __t_ bindings across all input
            // elements so the outer path can read the correctly accumulated values.
            Sequence EvalTupleBlockStep(Sequence inputContext, int stepIdx, string[] tLabels, Environment env)
            {
                var step = steps[stepIdx];
                bool shouldFlatten = stepIdx > 0 || isPropertyStep[stepIdx];
                bool isLastStep = stepIdx + 1 >= steps.Length;

                // Expand input to individual elements
                var inputElements = default(SequenceBuilder);
                for (int i = 0; i < inputContext.Count; i++)
                {
                    var el = inputContext[i];
                    if (el.ValueKind == JsonValueKind.Array && shouldFlatten)
                    {
                        foreach (var item in el.EnumerateArray())
                        {
                            inputElements.Add(item);
                        }
                    }
                    else
                    {
                        inputElements.Add(el);
                    }
                }

                // For last-step tuple blocks, accumulate inner __t_ bindings
                // across all input elements instead of letting them be overwritten.
                Dictionary<string, List<JsonElement>>? accumulated = null;
                if (isLastStep)
                {
                    accumulated = new();
                    foreach (var label in tLabels)
                    {
                        accumulated[label] = new List<JsonElement>();
                    }
                }

                var resultBuilder = default(SequenceBuilder);
                for (int i = 0; i < inputElements.Count; i++)
                {
                    var el = inputElements[i];

                    // Evaluate the block on this element — the inner path's
                    // EvalAncestorStep will store tuple bindings in the env
                    Sequence blockResult = step(el, env);

                    if (blockResult.IsUndefined)
                    {
                        continue;
                    }

                    if (isLastStep)
                    {
                        // Accumulate whatever inner __t_ bindings were stored
                        foreach (var label in tLabels)
                        {
                            if (env.TryLookup("__t_" + label, out var tupleSeq) && !tupleSeq.IsUndefined)
                            {
                                var arr = tupleSeq.FirstOrDefault;
                                if (arr.ValueKind == JsonValueKind.Array)
                                {
                                    accumulated![label].AddRange(arr.EnumerateArray());
                                }
                            }
                        }

                        // Collect results
                        for (int j = 0; j < blockResult.Count; j++)
                        {
                            var r = blockResult[j];
                            if (r.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var child in r.EnumerateArray())
                                {
                                    resultBuilder.Add(child);
                                }
                            }
                            else
                            {
                                resultBuilder.Add(r);
                            }
                        }
                    }
                    else
                    {
                        // Retrieve fresh tuple bindings for per-element rebinding
                        var bindingArrays = new JsonElement[tLabels.Length];
                        bool hasTupleBindings = true;
                        for (int l = 0; l < tLabels.Length; l++)
                        {
                            if (env.TryLookup("__t_" + tLabels[l], out var tupleSeq) && !tupleSeq.IsUndefined)
                            {
                                bindingArrays[l] = tupleSeq.FirstOrDefault;
                            }
                            else
                            {
                                hasTupleBindings = false;
                                break;
                            }
                        }

                        // Expand block result to individual elements
                        var resultElements = default(SequenceBuilder);
                        for (int j = 0; j < blockResult.Count; j++)
                        {
                            var r = blockResult[j];
                            if (r.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in r.EnumerateArray())
                                {
                                    resultElements.Add(item);
                                }
                            }
                            else
                            {
                                resultElements.Add(r);
                            }
                        }

                        // Process remaining steps per-result-element with restored bindings
                        for (int j = 0; j < resultElements.Count; j++)
                        {
                            if (hasTupleBindings)
                            {
                                for (int l = 0; l < tLabels.Length; l++)
                                {
                                    if (bindingArrays[l].ValueKind == JsonValueKind.Array
                                        && j < bindingArrays[l].GetArrayLength())
                                    {
                                        env.Bind(tLabels[l], new Sequence(bindingArrays[l][j]));
                                    }
                                }
                            }

                            // Apply per-element stages (filter predicates) with rebound ancestors
                            var elementSeq = new Sequence(resultElements[j]);
                            if (stages[stepIdx] is not null)
                            {
                                elementSeq = ApplyStages(elementSeq, stages[stepIdx]!, stageIsSortFlags[stepIdx]!, env, indexVars[stepIdx], null, constantIntStageValues[stepIdx]);
                                if (elementSeq.IsUndefined)
                                {
                                    continue;
                                }
                            }

                            var subResult = EvalPathFrom(elementSeq, stepIdx + 1, env);
                            if (!subResult.IsUndefined)
                            {
                                for (int k = 0; k < subResult.Count; k++)
                                {
                                    resultBuilder.Add(subResult[k]);
                                }
                            }
                        }
                    }
                }

                // Store accumulated inner bindings for the outer path
                if (accumulated is not null)
                {
                    foreach (var kvp in accumulated)
                    {
                        if (kvp.Value.Count > 0)
                        {
                            var arr = JsonataHelpers.ArrayFromList(kvp.Value, env.Workspace);
                            env.Bind("__t_" + kvp.Key, new Sequence(arr));
                        }
                    }
                }

                return resultBuilder.ToSequence();
            }

            // Evaluates a focus-bound step (@$var) with cross-join semantics.
            // The step's result elements are bound to the focus variable,
            // and remaining steps evaluate from the parent context (this step's input).
            Sequence EvalFocusStep(Sequence parentContext, int stepIdx, Environment env)
            {
                var step = steps[stepIdx];
                var focusVar = focusVars[stepIdx]!;
                var indexVar = indexVars[stepIdx];
                var labels = ancestorLabels[stepIdx];

                // Bind ancestor labels to the parent context (the INPUT to this step)
                // so that downstream % operators can look up the parent.
                if (labels is not null && parentContext.IsSingleton)
                {
                    var el = parentContext.FirstOrDefault;
                    for (int l = 0; l < labels.Length; l++)
                    {
                        env.Bind(labels[l], new Sequence(el));
                    }
                }

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
                            flat.ReturnBackingArray();
                        }
                        else
                        {
                            var sr = step(el, env);
                            builder.AddRange(sr);
                            sr.ReturnBackingArray();
                        }
                    }

                    focusResult = builder.ToSequence();
                }

                if (focusResult.IsUndefined)
                {
                    return Sequence.Undefined;
                }

                // Expand singleton array to individual elements for per-element binding
                var elements = default(SequenceBuilder);
                if (focusResult.IsSingleton)
                {
                    var el = focusResult.FirstOrDefault;
                    if (el.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in el.EnumerateArray())
                        {
                            elements.Add(item);
                        }
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

                // All elements copied out — return focusResult's backing array.
                focusResult.ReturnBackingArray();

                // If the next step is a sort, apply it to the focus elements now.
                // The sort modifies the iteration order (e.g. Employee@$e^($e.Surname)
                // sorts employees before the cross-join). We consume the sort step here
                // and advance stepIdx past it for remaining-steps evaluation.
                int sortStepConsumed = 0;
                if (stepIdx + 1 < steps.Length && isSortStep[stepIdx + 1] && elements.Count > 1)
                {
                    int sortIdx = stepIdx + 1;

                    // Use the focus-aware sort: bind the focus variable per comparison
                    // so sort keys like $e.Surname resolve correctly.
                    if (sortTermsPerStep[sortIdx] is not null)
                    {
                        var sortedSeq = FocusSort(
                            elements, sortTermsPerStep[sortIdx]!, env, focusVar);
                        elements.Clear();
                        for (int si = 0; si < sortedSeq.Count; si++)
                        {
                            elements.Add(sortedSeq[si]);
                        }

                        // Note: FocusSort returns a non-pooled array — do NOT call ReturnBackingArray.
                    }
                    else
                    {
                        var sortInput = JsonataHelpers.ArrayFromBuilder(ref elements, env.Workspace);
                        var sortedSeq = steps[sortIdx](sortInput, env);
                        if (sortedSeq.IsSingleton)
                        {
                            var sortedEl = sortedSeq.FirstOrDefault;
                            if (sortedEl.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in sortedEl.EnumerateArray())
                                {
                                    elements.Add(item);
                                }
                            }
                            else
                            {
                                elements.Add(sortedEl);
                            }
                        }
                        else
                        {
                            for (int si = 0; si < sortedSeq.Count; si++)
                            {
                                elements.Add(sortedSeq[si]);
                            }
                        }
                    }

                    sortStepConsumed = 1;
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

                // Phase 1 data for focus-aware group-by:
                // Collect focus elements per group key so we can re-bind the focus
                // variable to a sequence when evaluating the value expression.
                List<string>? focusGroupKeys = applyGroupByHere ? new() : null;
                Dictionary<string, (List<JsonElement> FocusElements, List<JsonElement> ContextElements, int PairIndex)>? focusGroupData =
                    applyGroupByHere ? new() : null;
                var mergeObjects = default(SequenceBuilder);

                // Apply stages as a BATCH to all focus elements at once.
                // This ensures numeric index predicates (e.g. [1]) see the full
                // set of matching elements, not individual singletons.
                // The focus variable is re-bound per-element during evaluation
                // so that filter predicates (e.g. [$l.isbn=$b.isbn]) still work.
                // Track original indices so the index variable (#$var) reflects
                // the element's position in the pre-filter array.
                List<int>? survivingOriginalIndices = indexVar is not null ? new() : null;
                if (stages[stepIdx] is not null)
                {
                    var batchSeq = elements.ToSequence();
                    var filtered = ApplyFocusStages(
                        batchSeq, stages[stepIdx]!, stageIsSortFlags[stepIdx]!, env, focusVar, indexVar, survivingOriginalIndices);
                    if (filtered.IsUndefined)
                    {
                        elements.ReturnArray();
                        return Sequence.Undefined;
                    }

                    // Rebuild elements from the filtered result
                    elements.Clear();
                    if (filtered.IsSingleton)
                    {
                        var el = filtered.FirstOrDefault;
                        if (el.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in el.EnumerateArray())
                            {
                                elements.Add(item);
                            }
                        }
                        else
                        {
                            elements.Add(el);
                        }
                    }
                    else
                    {
                        for (int fi = 0; fi < filtered.Count; fi++)
                        {
                            elements.Add(filtered[fi]);
                        }
                    }

                    // All elements copied out — return filtered's backing array.
                    filtered.ReturnBackingArray();
                }

                // Cross-join mode: when the next step is also a focus step with
                // stages, evaluate the cross-join globally so that post-filter stages
                // (numeric indices [1], index bindings #$var) operate on the full
                // cross-join result, not per-outer-iteration singletons.
                int crossJoinNextStep = stepIdx + 1 + sortStepConsumed;
                bool needsCrossJoin = crossJoinNextStep < steps.Length
                    && focusVars[crossJoinNextStep] is not null
                    && stages[crossJoinNextStep] is not null
                    && !applyGroupByHere && !mergeInnerGroupBy;

                if (needsCrossJoin)
                {
                    return EvalCrossJoinFocus(
                        elements, parentContext, stepIdx, crossJoinNextStep,
                        focusVar, indexVar, survivingOriginalIndices,
                        env);
                }

                // For each surviving focus element: bind, evaluate remaining steps
                var resultBuilder = default(SequenceBuilder);
                for (int i = 0; i < elements.Count; i++)
                {
                    var el = elements[i];

                    // Bind focus variable to this element
                    env.Bind(focusVar, new Sequence(el));

                    // Bind index variable to the element's ORIGINAL array position
                    // (before filtering), not the position in the filtered list.
                    if (indexVar is not null)
                    {
                        int origIdx = survivingOriginalIndices is not null && i < survivingOriginalIndices.Count
                            ? survivingOriginalIndices[i]
                            : i;
                        env.Bind(indexVar, Sequence.FromDouble(origIdx, env.Workspace));
                    }

                    // Evaluate remaining steps from parent context (cross-join).
                    // Skip any consumed sort step.
                    int nextStep = stepIdx + 1 + sortStepConsumed;
                    Sequence subResult;
                    if (nextStep < steps.Length)
                    {
                        subResult = EvalPathFrom(parentContext, nextStep, env);
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
                        // Phase 1: evaluate key, collect focus element per group.
                        // Value expression is deferred to Phase 2 so it sees the
                        // full sequence of focus elements for each group.
                        AccumulateFocusGroupKey(subResult, groupByPairs!, focusGroupKeys!, focusGroupData!, env, el);
                    }
                    else if (mergeInnerGroupBy)
                    {
                        // Inner focus handled group-by — collect objects for merging
                        for (int j = 0; j < subResult.Count; j++)
                        {
                            mergeObjects.Add(subResult[j]);
                        }

                        subResult.ReturnBackingArray();
                    }
                    else
                    {
                        resultBuilder.AddRange(subResult);
                        subResult.ReturnBackingArray();
                    }
                }

                if (applyGroupByHere && focusGroupKeys!.Count > 0)
                {
                    // Phase 2: For each group, re-bind focus variable to the
                    // sequence of all matching elements and evaluate value once.
                    return BuildFocusGroupByResult(
                        focusGroupKeys!, focusGroupData!, groupByPairs!, env, focusVar);
                }

                if (mergeInnerGroupBy && mergeObjects.Count > 0)
                {
                    return MergeGroupedObjects(ref mergeObjects, env);
                }

                return resultBuilder.ToSequence();
            }

            // Evaluates a cross-join between an outer focus step and an inner focus
            // step that has stages requiring global evaluation. Collects ALL cross-join
            // tuples first, then applies inner stages globally, then evaluates
            // remaining steps per surviving tuple.
            Sequence EvalCrossJoinFocus(
                SequenceBuilder outerElements,
                Sequence parentContext,
                int outerStepIdx,
                int innerStepIdx,
                string outerFocusVar,
                string? outerIndexVar,
                List<int>? outerSurvivingIndices,
                Environment env)
            {
                var innerStep = steps[innerStepIdx];
                var innerFocusVar = focusVars[innerStepIdx]!;
                var innerIndexVar = indexVars[innerStepIdx];
                bool shouldFlatten = innerStepIdx > 0 || isPropertyStep[innerStepIdx];

                // Evaluate the inner step once to get ALL focus elements (from parent).
                // The parent context is the same for all outer iterations (cross-join).
                Sequence innerFocusResult;
                if (parentContext.IsSingleton)
                {
                    var parentEl = parentContext.FirstOrDefault;
                    if (parentEl.ValueKind == JsonValueKind.Array && shouldFlatten)
                    {
                        innerFocusResult = FlattenArrayStep(parentEl, innerStep, env, isConsArrayStep[innerStepIdx]);
                    }
                    else
                    {
                        innerFocusResult = innerStep(parentEl, env);
                    }
                }
                else
                {
                    var builder = default(SequenceBuilder);
                    for (int i = 0; i < parentContext.Count; i++)
                    {
                        var parentEl = parentContext[i];
                        if (parentEl.ValueKind == JsonValueKind.Array && shouldFlatten)
                        {
                            var flat = FlattenArrayStep(parentEl, innerStep, env, isConsArrayStep[innerStepIdx]);
                            builder.AddRange(flat);
                            flat.ReturnBackingArray();
                        }
                        else
                        {
                            var sr = innerStep(parentEl, env);
                            builder.AddRange(sr);
                            sr.ReturnBackingArray();
                        }
                    }

                    innerFocusResult = builder.ToSequence();
                }

                if (innerFocusResult.IsUndefined)
                {
                    return Sequence.Undefined;
                }

                // Expand to individual elements
                var innerElements = default(SequenceBuilder);
                if (innerFocusResult.IsSingleton)
                {
                    var el = innerFocusResult.FirstOrDefault;
                    if (el.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in el.EnumerateArray())
                        {
                            innerElements.Add(item);
                        }
                    }
                    else
                    {
                        innerElements.Add(el);
                    }
                }
                else
                {
                    for (int i = 0; i < innerFocusResult.Count; i++)
                    {
                        innerElements.Add(innerFocusResult[i]);
                    }
                }

                // All elements copied out — return innerFocusResult's backing array.
                innerFocusResult.ReturnBackingArray();

                // Phase 1: Build cross-join tuples.
                // For each outer element × each inner element, record the tuple.
                // (outerIdx, outerElement, innerElement, originalInnerIdx)
                int maxTuples = outerElements.Count * innerElements.Count;
                var tuplesArray = maxTuples > 0
                    ? ArrayPool<(int OuterIdx, JsonElement OuterEl, JsonElement InnerEl, int InnerIdx)>.Shared.Rent(maxTuples)
                    : null;
                int tupleCount = 0;
                for (int oi = 0; oi < outerElements.Count; oi++)
                {
                    var outerEl = outerElements[oi];
                    env.Bind(outerFocusVar, new Sequence(outerEl));
                    if (outerIndexVar is not null)
                    {
                        int origIdx = outerSurvivingIndices is not null && oi < outerSurvivingIndices.Count
                            ? outerSurvivingIndices[oi]
                            : oi;
                        env.Bind(outerIndexVar, Sequence.FromDouble(origIdx, env.Workspace));
                    }

                    for (int ii = 0; ii < innerElements.Count; ii++)
                    {
                        tuplesArray![tupleCount++] = (oi, outerEl, innerElements[ii], ii);
                    }
                }

                // Phase 2: Apply inner stages to ALL cross-join tuples globally.
                // Build a combined sequence and apply stages with both outer and
                // inner variable re-binding per element.
                if (stages[innerStepIdx] is not null)
                {
                    var stageEvaluators = stages[innerStepIdx]!;
                    var isSortStage = stageIsSortFlags[innerStepIdx]!;

                    for (int s = 0; s < stageEvaluators.Length; s++)
                    {
                        if (tupleCount == 0)
                        {
                            if (tuplesArray is not null) { ArrayPool<(int, JsonElement, JsonElement, int)>.Shared.Return(tuplesArray); }
                            return Sequence.Undefined;
                        }

                        var stage = stageEvaluators[s];

                        if (isSortStage[s])
                        {
                            // Sort stage — sort all tuples' inner elements
                            var sortBuilder = default(SequenceBuilder);
                            for (int t = 0; t < tupleCount; t++)
                            {
                                sortBuilder.Add(tuplesArray![t].InnerEl);
                            }

                            if (sortBuilder.Count > 1)
                            {
                                var arrayInput = JsonataHelpers.ArrayFromBuilder(ref sortBuilder, env.Workspace);
                                var sorted = stage(arrayInput, env);
                            }
                            else
                            {
                                sortBuilder.ReturnArray();
                            }

                            continue;
                        }

                        // Check if this is an index binding stage (VariableNode added by parser)
                        var stageAnnotations = innerStepIdx < steps.Length ? GetStepAnnotations(path.Steps[innerStepIdx]) : null;
                        bool isIndexBindingStage = stageAnnotations is not null
                            && s < stageAnnotations.Stages.Count
                            && stageAnnotations.Stages[s] is VariableNode;

                        if (isIndexBindingStage)
                        {
                            // Index binding stage: assign position index to each tuple
                            var varNode = (VariableNode)stageAnnotations!.Stages[s];
                            for (int t = 0; t < tupleCount; t++)
                            {
                                env.Bind(varNode.Name, Sequence.FromDouble(t, env.Workspace));
                            }

                            // Keep all tuples (index binding doesn't filter)
                            continue;
                        }

                        // Filter or numeric index stage — filter in-place
                        int writeIdx = 0;
                        for (int t = 0; t < tupleCount; t++)
                        {
                            var tuple = tuplesArray![t];

                            // Re-bind both outer and inner variables
                            env.Bind(outerFocusVar, new Sequence(tuple.OuterEl));
                            if (outerIndexVar is not null)
                            {
                                int origIdx = outerSurvivingIndices is not null && tuple.OuterIdx < outerSurvivingIndices.Count
                                    ? outerSurvivingIndices[tuple.OuterIdx]
                                    : tuple.OuterIdx;
                                env.Bind(outerIndexVar, Sequence.FromDouble(origIdx, env.Workspace));
                            }

                            env.Bind(innerFocusVar, new Sequence(tuple.InnerEl));
                            if (innerIndexVar is not null)
                            {
                                env.Bind(innerIndexVar, Sequence.FromDouble(tuple.InnerIdx, env.Workspace));
                            }

                            var result = stage(tuple.InnerEl, env);

                            if (result.IsUndefined)
                            {
                                continue;
                            }

                            var firstResult = result.FirstOrDefault;

                            if (result.IsSingleton && firstResult.ValueKind is JsonValueKind.True or JsonValueKind.False)
                            {
                                if (firstResult.ValueKind == JsonValueKind.True)
                                {
                                    tuplesArray![writeIdx++] = tuple;
                                }

                                continue;
                            }

                            // Numeric index
                            if (result.IsSingleton && TryCoerceToNumber(firstResult, out double singleIdx))
                            {
                                int idx = (int)Math.Floor(singleIdx);
                                if (idx < 0)
                                {
                                    idx = tupleCount + idx;
                                }

                                if (idx == t && idx >= 0 && idx < tupleCount)
                                {
                                    tuplesArray![writeIdx++] = tuple;
                                }

                                continue;
                            }

                            if (IsTruthy(result))
                            {
                                tuplesArray![writeIdx++] = tuple;
                            }
                        }

                        tupleCount = writeIdx;
                    }
                }

                if (tupleCount == 0)
                {
                    if (tuplesArray is not null) { ArrayPool<(int, JsonElement, JsonElement, int)>.Shared.Return(tuplesArray); }
                    return Sequence.Undefined;
                }

                // Phase 3: For each surviving tuple, re-bind variables and evaluate
                // remaining steps from the parent context.
                var resultBuilder = default(SequenceBuilder);
                int remainingStart = innerStepIdx + 1;

                for (int t = 0; t < tupleCount; t++)
                {
                    var tuple = tuplesArray![t];

                    // Re-bind outer variables
                    env.Bind(outerFocusVar, new Sequence(tuple.OuterEl));
                    if (outerIndexVar is not null)
                    {
                        int origIdx = outerSurvivingIndices is not null && tuple.OuterIdx < outerSurvivingIndices.Count
                            ? outerSurvivingIndices[tuple.OuterIdx]
                            : tuple.OuterIdx;
                        env.Bind(outerIndexVar, Sequence.FromDouble(origIdx, env.Workspace));
                    }

                    // Re-bind inner variables
                    env.Bind(innerFocusVar, new Sequence(tuple.InnerEl));
                    if (innerIndexVar is not null)
                    {
                        env.Bind(innerIndexVar, Sequence.FromDouble(tuple.InnerIdx, env.Workspace));
                    }

                    // Bind the index binding stage variable (e.g. $ib2) to this tuple's
                    // position in the surviving tuples list.
                    // Check for any VariableNode stages (index binding stages)
                    var innerAnnotations = GetStepAnnotations(path.Steps[innerStepIdx]);
                    if (innerAnnotations is not null)
                    {
                        for (int s = 0; s < innerAnnotations.Stages.Count; s++)
                        {
                            if (innerAnnotations.Stages[s] is VariableNode varStage)
                            {
                                env.Bind(varStage.Name, Sequence.FromDouble(t, env.Workspace));
                            }
                        }
                    }

                    Sequence subResult;
                    if (remainingStart < steps.Length)
                    {
                        subResult = EvalPathFrom(parentContext, remainingStart, env);
                    }
                    else
                    {
                        subResult = new Sequence(tuple.InnerEl);
                    }

                    if (!subResult.IsUndefined)
                    {
                        resultBuilder.AddRange(subResult);
                        subResult.ReturnBackingArray();
                    }
                }

                if (tuplesArray is not null) { ArrayPool<(int, JsonElement, JsonElement, int)>.Shared.Return(tuplesArray); }
                return resultBuilder.ToSequence();
            }

            // Evaluates a step that has ancestor labels (parent operator %).
            // The INPUT to this step is bound under the ancestor labels so that
            // downstream % operators can look it up. We process per-element to
            // ensure each element carries its correct parent binding.
            Sequence EvalAncestorStep(Sequence inputContext, int stepIdx, Environment env)
            {
                var step = steps[stepIdx];
                var labels = ancestorLabels[stepIdx]!;
                bool shouldFlatten = stepIdx > 0 || isPropertyStep[stepIdx];

                // Expand input to individual elements for per-element binding
                var inputElements = default(SequenceBuilder);
                for (int i = 0; i < inputContext.Count; i++)
                {
                    var el = inputContext[i];
                    if (el.ValueKind == JsonValueKind.Array && shouldFlatten)
                    {
                        foreach (var item in el.EnumerateArray())
                        {
                            inputElements.Add(item);
                        }
                    }
                    else
                    {
                        inputElements.Add(el);
                    }
                }

                // When a downstream step is a sort, we can't evaluate remaining steps
                // per-element (sort needs all elements as a batch). Instead, collect
                // all (result, ancestor) pairs, then sort with per-element binding.
                int sortStepIndex = -1;
                for (int s = stepIdx + 1; s < steps.Length; s++)
                {
                    if (isSortStep[s])
                    {
                        sortStepIndex = s;
                        break;
                    }
                }

                if (sortStepIndex >= 0)
                {
                    return EvalAncestorWithSort(inputElements, stepIdx, labels, sortStepIndex, env);
                }

                // Determine group-by handling
                bool hasGroupBy = groupByPairs is not null;
                bool applyGroupByHere = hasGroupBy;
                List<string>? groupKeys = applyGroupByHere ? new() : null;
                Dictionary<string, (List<JsonElement> Elements, int PairIndex)>? groupData =
                    applyGroupByHere ? new() : null;

                // Store per-result-element tuple bindings so that outer paths
                // can restore correct ancestor bindings for steps after a tuple block.
                // Always store when not applying group-by, even for non-last steps,
                // because the results may flow through nested blocks where the binding
                // is needed at a higher level (e.g., parent[15] with double nesting).
                bool storeTupleBindings = !applyGroupByHere;
                List<JsonElement>[]? tupleValues = null;
                if (storeTupleBindings)
                {
                    tupleValues = new List<JsonElement>[labels.Length];
                    for (int l = 0; l < labels.Length; l++)
                    {
                        tupleValues[l] = new();
                    }
                }

                var resultBuilder = default(SequenceBuilder);
                for (int i = 0; i < inputElements.Count; i++)
                {
                    var el = inputElements[i];

                    // Bind ancestor labels to the input element (the parent context)
                    for (int l = 0; l < labels.Length; l++)
                    {
                        env.Bind(labels[l], new Sequence(el));
                    }

                    // Evaluate the step on this element
                    Sequence stepResult = step(el, env);

                    // Apply per-element stages if any
                    if (stages[stepIdx] is not null)
                    {
                        stepResult = ApplyStages(stepResult, stages[stepIdx]!, stageIsSortFlags[stepIdx]!, env, indexVars[stepIdx], null, constantIntStageValues[stepIdx]);
                        if (stepResult.IsUndefined)
                        {
                            continue;
                        }
                    }

                    // Continue remaining steps per-element
                    Sequence subResult;
                    if (stepIdx + 1 < steps.Length)
                    {
                        subResult = EvalPathFrom(stepResult, stepIdx + 1, env);

                        // stepResult was consumed by EvalPathFrom — return its array.
                        stepResult.ReturnBackingArray();
                    }
                    else
                    {
                        subResult = stepResult;
                    }

                    if (subResult.IsUndefined)
                    {
                        continue;
                    }

                    // Handle group-by accumulation
                    if (applyGroupByHere)
                    {
                        AccumulateGroupBy(subResult, groupByPairs!, groupKeys!, groupData!, env);
                        continue;
                    }

                    // In tuple stream context, array results are expanded element-by-element
                    // (matching jsonata-js evaluateTupleStep which iterates res[bb] to create
                    // separate tuples). This naturally flattens constructed arrays.
                    for (int j = 0; j < subResult.Count; j++)
                    {
                        var item = subResult[j];
                        if (item.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var child in item.EnumerateArray())
                            {
                                resultBuilder.Add(child);
                                if (storeTupleBindings)
                                {
                                    for (int l = 0; l < labels.Length; l++)
                                    {
                                        tupleValues![l].Add(el);
                                    }
                                }
                            }
                        }
                        else
                        {
                            resultBuilder.Add(item);
                            if (storeTupleBindings)
                            {
                                for (int l = 0; l < labels.Length; l++)
                                {
                                    tupleValues![l].Add(el);
                                }
                            }
                        }
                    }

                    // All elements copied out — return subResult's backing array.
                    subResult.ReturnBackingArray();
                }

                if (applyGroupByHere && groupKeys!.Count > 0)
                {
                    return BuildGroupByResult(groupKeys!, groupData!, groupByPairs!, env);
                }

                // Store per-element tuple bindings as JSON arrays in the env.
                // The outer path can use these to restore correct ancestor bindings
                // per result element after a tuple-mode block returns.
                if (storeTupleBindings && tupleValues is not null)
                {
                    for (int l = 0; l < labels.Length; l++)
                    {
                        if (tupleValues[l].Count > 0)
                        {
                            var arr = JsonataHelpers.ArrayFromList(tupleValues[l], env.Workspace);
                            env.Bind("__t_" + labels[l], new Sequence(arr));
                        }
                    }
                }

                return resultBuilder.ToSequence();
            }

            // Handles the case where an ancestor step has a downstream sort.
            // Collects (element, ancestor) pairs through intermediate steps,
            // sorts with per-element ancestor binding, then continues remaining steps.
            Sequence EvalAncestorWithSort(SequenceBuilder inputElements, int stepIdx, string[] labels, int sortStepIdx, Environment env)
            {
                var step = steps[stepIdx];

                // Collect ALL ancestor labels from this step and intermediate steps
                var allLabels = new List<string>(labels);
                for (int s = stepIdx + 1; s < sortStepIdx; s++)
                {
                    if (ancestorLabels[s] is not null)
                    {
                        allLabels.AddRange(ancestorLabels[s]!);
                    }
                }

                // Per-label per-element ancestor values (parallel to resultElements)
                var labelValues = new Dictionary<string, List<JsonElement>>();
                foreach (var label in allLabels)
                {
                    labelValues[label] = new List<JsonElement>();
                }

                // Phase 1: collect all result elements with their per-label ancestor mappings
                var resultElements = default(SequenceBuilder);

                for (int i = 0; i < inputElements.Count; i++)
                {
                    var el = inputElements[i];

                    // Bind outer ancestor labels
                    for (int l = 0; l < labels.Length; l++)
                    {
                        env.Bind(labels[l], new Sequence(el));
                    }

                    Sequence stepResult = step(el, env);

                    // Apply stages if any
                    if (stages[stepIdx] is not null)
                    {
                        stepResult = ApplyStages(stepResult, stages[stepIdx]!, stageIsSortFlags[stepIdx]!, env, indexVars[stepIdx], null, constantIntStageValues[stepIdx]);
                        if (stepResult.IsUndefined)
                        {
                            continue;
                        }
                    }

                    // Evaluate intermediate steps (between ancestor step and sort step)
                    // Track per-element-per-label ancestor values as we go.
                    // Use a staging list that maps each element to its per-label ancestors.
                    var currentElements = new List<(JsonElement Element, JsonElement OuterAncestor)>();

                    // Expand initial step result
                    if (stepResult.IsSingleton)
                    {
                        var item = stepResult.FirstOrDefault;
                        if (item.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var child in item.EnumerateArray())
                            {
                                currentElements.Add((child, el));
                            }
                        }
                        else
                        {
                            currentElements.Add((item, el));
                        }
                    }
                    else
                    {
                        for (int j = 0; j < stepResult.Count; j++)
                        {
                            currentElements.Add((stepResult[j], el));
                        }
                    }

                    // Process intermediate steps
                    for (int s = stepIdx + 1; s < sortStepIdx; s++)
                    {
                        if (currentElements.Count == 0)
                        {
                            break;
                        }

                        var intLabels = ancestorLabels[s];
                        bool shouldFlattenInt = s > 0 || isPropertyStep[s];
                        var nextElements = new List<(JsonElement Element, JsonElement OuterAncestor, JsonElement? IntAncestor)>();

                        foreach (var (elem, outerAnc) in currentElements)
                        {
                            // Bind intermediate ancestor labels to the input element
                            if (intLabels is not null)
                            {
                                for (int l = 0; l < intLabels.Length; l++)
                                {
                                    env.Bind(intLabels[l], new Sequence(elem));
                                }
                            }

                            Sequence intResult;
                            if (elem.ValueKind == JsonValueKind.Array && shouldFlattenInt)
                            {
                                var sb = default(SequenceBuilder);
                                foreach (var child in elem.EnumerateArray())
                                {
                                    sb.AddRange(steps[s](child, env));
                                }

                                intResult = sb.ToSequence();
                            }
                            else
                            {
                                intResult = steps[s](elem, env);
                            }

                            for (int j = 0; j < intResult.Count; j++)
                            {
                                var r = intResult[j];
                                if (r.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var child in r.EnumerateArray())
                                    {
                                        nextElements.Add((child, outerAnc, intLabels is not null ? elem : null));
                                    }
                                }
                                else
                                {
                                    nextElements.Add((r, outerAnc, intLabels is not null ? elem : null));
                                }
                            }
                        }

                        if (stages[s] is not null)
                        {
                            // Apply stages to the intermediate result
                            var sb = default(SequenceBuilder);
                            foreach (var (elem, _, _) in nextElements)
                            {
                                sb.Add(elem);
                            }

                            var staged = ApplyStages(sb.ToSequence(), stages[s]!, stageIsSortFlags[s]!, env, indexVars[s], null, constantIntStageValues[s]);

                            // Rebuild nextElements preserving only staged elements
                            // (for now, skip complex filtering — stages on intermediate steps are rare)
                            _ = staged;
                        }

                        currentElements = nextElements.ConvertAll(x => (x.Element, x.OuterAncestor));

                        // Record intermediate ancestor values
                        if (intLabels is not null)
                        {
                            foreach (var (_, _, intAnc) in nextElements)
                            {
                                foreach (var label in intLabels)
                                {
                                    if (intAnc.HasValue)
                                    {
                                        labelValues[label].Add(intAnc.Value);
                                    }
                                }
                            }
                        }
                    }

                    // Record outer ancestor values and final result elements
                    foreach (var (elem, _) in currentElements)
                    {
                        resultElements.Add(elem);
                        foreach (var label in labels)
                        {
                            labelValues[label].Add(el);
                        }
                    }

                    // If no intermediate steps were processed, intermediate labels
                    // won't have been recorded yet. Handle the simple case.
                    if (sortStepIdx == stepIdx + 1)
                    {
                        // No intermediate steps — labelValues for intermediate labels
                        // are empty (which is correct — there are none)
                    }
                }

                if (resultElements.Count == 0)
                {
                    resultElements.ReturnArray();
                    return Sequence.Undefined;
                }

                // Phase 2: sort using per-element ancestor binding
                if (resultElements.Count > 1 && sortTermsPerStep[sortStepIdx] is not null)
                {
                    var terms = sortTermsPerStep[sortStepIdx]!;
                    var sortIndices = RentSortIndices(resultElements.Count);
                    SortIndices(sortIndices, resultElements.Count, (a, b) =>
                    {
                        // Bind ALL ancestor labels for element A
                        foreach (var label in allLabels)
                        {
                            if (labelValues.TryGetValue(label, out var vals) && a < vals.Count)
                            {
                                env.Bind(label, new Sequence(vals[a]));
                            }
                        }

                        var aVals = new Sequence[terms.Length];
                        for (int t = 0; t < terms.Length; t++)
                        {
                            aVals[t] = terms[t].Expr(resultElements[a], env);
                        }

                        // Bind ALL ancestor labels for element B
                        foreach (var label in allLabels)
                        {
                            if (labelValues.TryGetValue(label, out var vals) && b < vals.Count)
                            {
                                env.Bind(label, new Sequence(vals[b]));
                            }
                        }

                        for (int t = 0; t < terms.Length; t++)
                        {
                            var bVal = terms[t].Expr(resultElements[b], env);
                            int cmp = CompareSortKeys(aVals[t], bVal);
                            if (cmp != 0)
                            {
                                return terms[t].Descending ? -cmp : cmp;
                            }
                        }

                        return a.CompareTo(b);
                    });

                    var sorted = default(SequenceBuilder);
                    var sortedLabelValues = new Dictionary<string, List<JsonElement>>();
                    foreach (var label in allLabels)
                    {
                        sortedLabelValues[label] = new List<JsonElement>();
                    }

                    for (int i = 0; i < resultElements.Count; i++)
                    {
                        sorted.Add(resultElements[sortIndices[i]]);
                        foreach (var kvp in labelValues)
                        {
                            if (sortIndices[i] < kvp.Value.Count)
                            {
                                sortedLabelValues[kvp.Key].Add(kvp.Value[sortIndices[i]]);
                            }
                        }
                    }

                    ReturnSortIndices(sortIndices);
                    resultElements.ReturnArray();
                    resultElements = sorted;
                    labelValues.Clear();
                    foreach (var kvp in sortedLabelValues)
                    {
                        labelValues[kvp.Key] = kvp.Value;
                    }
                }

                // Apply sort step stages (filters after sort)
                Sequence sortedSeq;
                if (resultElements.Count == 1)
                {
                    sortedSeq = new Sequence(resultElements[0]);
                    resultElements.ReturnArray();
                }
                else
                {
                    sortedSeq = new Sequence(JsonataHelpers.ArrayFromBuilder(ref resultElements, env.Workspace));
                }

                if (stages[sortStepIdx] is not null)
                {
                    sortedSeq = ApplyStages(sortedSeq, stages[sortStepIdx]!, stageIsSortFlags[sortStepIdx]!, env, indexVars[sortStepIdx], null, constantIntStageValues[sortStepIdx]);
                }

                // Phase 3: continue remaining steps per-element with ancestor binding
                if (sortStepIdx + 1 >= steps.Length)
                {
                    return sortedSeq;
                }

                var builder = default(SequenceBuilder);

                // Expand to individual elements for per-element continuation
                var finalElements = CollectFlatElements(sortedSeq);
                for (int i = 0; i < finalElements.Count; i++)
                {
                    // Bind all ancestor labels for this element (sorted order)
                    foreach (var label in allLabels)
                    {
                        if (labelValues.TryGetValue(label, out var vals) && i < vals.Count)
                        {
                            env.Bind(label, new Sequence(vals[i]));
                        }
                    }

                    var subResult = EvalPathFrom(new Sequence(finalElements[i]), sortStepIdx + 1, env);
                    builder.AddRange(subResult);
                }

                finalElements.ReturnArray();
                return builder.ToSequence();
            }

            // Evaluates a step with index binding (#$var) without focus binding.
            // The step's result elements each get their index bound, then remaining
            // steps are evaluated per-element so each carries the correct index.
            Sequence EvalIndexStep(Sequence inputContext, int stepIdx, Environment env)
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
                            flat.ReturnBackingArray();
                        }
                        else
                        {
                            var sr = step(el, env);
                            builder.AddRange(sr);
                            sr.ReturnBackingArray();
                        }
                    }

                    stepResult = builder.ToSequence();
                }

                if (stepResult.IsUndefined)
                {
                    return Sequence.Undefined;
                }

                // Expand singleton array to individual elements
                var elements = default(SequenceBuilder);
                if (stepResult.IsSingleton)
                {
                    var el = stepResult.FirstOrDefault;
                    if (el.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in el.EnumerateArray())
                        {
                            elements.Add(item);
                        }
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

                // All elements copied out — return stepResult's backing array.
                stepResult.ReturnBackingArray();

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
                    env.Bind(indexVar, Sequence.FromDouble(i, env.Workspace));

                    // Apply stages (filters) per-element.
                    // Pass null for indexVar — the index is already bound above.
                    // Passing it here would cause ApplyStages to re-bind it to 0
                    // (singleton input), overwriting the correct binding.
                    if (stages[stepIdx] is not null)
                    {
                        var filtered = ApplyStages(new Sequence(el), stages[stepIdx]!, stageIsSortFlags[stepIdx]!, env, null, null, constantIntStageValues[stepIdx]);
                        if (filtered.IsUndefined)
                        {
                            continue;
                        }
                    }

                    // Evaluate remaining steps on this element (not parent context — unlike focus)
                    Sequence subResult = EvalPathFrom(new Sequence(el), stepIdx + 1, env);

                    // Handle group-by accumulation
                    if (applyGroupByHere && !subResult.IsUndefined)
                    {
                        AccumulateGroupBy(subResult, groupByPairs!, groupKeys!, groupData!, env);
                        continue;
                    }

                    resultBuilder.AddRange(subResult);
                    subResult.ReturnBackingArray();
                }

                if (applyGroupByHere && groupKeys!.Count > 0)
                {
                    return BuildGroupByResult(groupKeys!, groupData!, groupByPairs!, env);
                }

                return resultBuilder.ToSequence();
            }

            // Handles the case where an index-bound step (#$var) has a downstream sort
            // with intermediate steps between them. Evaluates the index step and
            // intermediate steps per-element, collecting all results with their group
            // indices, then sorts globally and continues remaining steps with restored
            // index bindings.
            Sequence EvalIndexWithSort(Sequence inputContext, int stepIdx, int sortStepIdx, Environment env)
            {
                var step = steps[stepIdx];
                var idxVar = indexVars[stepIdx]!;
                bool shouldFlatten = stepIdx > 0 || isPropertyStep[stepIdx];

                // Evaluate the index step
                Sequence stepResult;
                if (inputContext.IsSingleton)
                {
                    var el = inputContext.FirstOrDefault;
                    stepResult = (el.ValueKind == JsonValueKind.Array && shouldFlatten)
                        ? FlattenArrayStep(el, step, env, isConsArrayStep[stepIdx])
                        : step(el, env);
                }
                else
                {
                    var sb = default(SequenceBuilder);
                    for (int i = 0; i < inputContext.Count; i++)
                    {
                        var el = inputContext[i];
                        sb.AddRange((el.ValueKind == JsonValueKind.Array && shouldFlatten)
                            ? FlattenArrayStep(el, step, env, isConsArrayStep[stepIdx])
                            : step(el, env));
                    }

                    stepResult = sb.ToSequence();
                }

                if (stepResult.IsUndefined)
                {
                    return Sequence.Undefined;
                }

                // Expand to individual elements with group (index) tracking
                var elements = CollectFlatElements(stepResult);
                var resultElements = default(SequenceBuilder);
                var groupIndices = new ValueListBuilder<int>(Math.Max(elements.Count, 8));

                for (int i = 0; i < elements.Count; i++)
                {
                    var el = elements[i];

                    // Bind index variable
                    env.Bind(idxVar, Sequence.FromDouble(i, env.Workspace));

                    // Apply stages if any
                    if (stages[stepIdx] is not null)
                    {
                        var filtered = ApplyStages(new Sequence(el), stages[stepIdx]!, stageIsSortFlags[stepIdx]!, env, idxVar, null, constantIntStageValues[stepIdx]);
                        if (filtered.IsUndefined)
                        {
                            continue;
                        }
                    }

                    // Evaluate intermediate steps (between index step and sort step)
                    Sequence current = new Sequence(el);
                    for (int s = stepIdx + 1; s < sortStepIdx; s++)
                    {
                        if (current.IsUndefined)
                        {
                            break;
                        }

                        bool shouldFlattenInt = s > 0 || isPropertyStep[s];
                        var intResult = default(SequenceBuilder);
                        for (int j = 0; j < current.Count; j++)
                        {
                            var item = current[j];
                            if (item.ValueKind == JsonValueKind.Array && shouldFlattenInt)
                            {
                                foreach (var child in item.EnumerateArray())
                                {
                                    intResult.AddRange(steps[s](child, env));
                                }
                            }
                            else
                            {
                                intResult.AddRange(steps[s](item, env));
                            }
                        }

                        current = intResult.ToSequence();

                        if (stages[s] is not null)
                        {
                            current = ApplyStages(current, stages[s]!, stageIsSortFlags[s]!, env, indexVars[s], null, constantIntStageValues[s]);
                        }
                    }

                    if (current.IsUndefined)
                    {
                        continue;
                    }

                    // Collect results with group index
                    var flatResults = CollectFlatElements(current);
                    for (int j = 0; j < flatResults.Count; j++)
                    {
                        resultElements.Add(flatResults[j]);
                        groupIndices.Append(i);
                    }

                    flatResults.ReturnArray();
                }

                if (resultElements.Count == 0)
                {
                    resultElements.ReturnArray();
                    groupIndices.Dispose();
                    return Sequence.Undefined;
                }

                // Sort globally with per-element index rebinding
                if (sortTermsPerStep[sortStepIdx] is not null)
                {
                    var terms = sortTermsPerStep[sortStepIdx]!;
                    var sortIndices = RentSortIndices(resultElements.Count);
                    SortIndices(sortIndices, resultElements.Count, (a, b) =>
                    {
                        for (int t = 0; t < terms.Length; t++)
                        {
                            var aVal = terms[t].Expr(resultElements[a], env);
                            var bVal = terms[t].Expr(resultElements[b], env);
                            int cmp = CompareSortKeys(aVal, bVal);
                            if (cmp != 0)
                            {
                                return terms[t].Descending ? -cmp : cmp;
                            }
                        }

                        return a.CompareTo(b);
                    });

                    var sorted = default(SequenceBuilder);
                    int sortCount = resultElements.Count;
                    int[] sortedGroups = ArrayPool<int>.Shared.Rent(sortCount);
                    for (int i = 0; i < sortCount; i++)
                    {
                        sorted.Add(resultElements[sortIndices[i]]);
                        sortedGroups[i] = groupIndices[sortIndices[i]];
                    }

                    ReturnSortIndices(sortIndices);
                    resultElements.ReturnArray();
                    resultElements = sorted;
                    groupIndices.Length = 0;
                    groupIndices.Append(new ReadOnlySpan<int>(sortedGroups, 0, sortCount));
                    ArrayPool<int>.Shared.Return(sortedGroups);
                }

                // Apply sort step stages
                Sequence sortedSeq;
                if (resultElements.Count == 1)
                {
                    sortedSeq = new Sequence(resultElements[0]);
                    resultElements.ReturnArray();
                }
                else
                {
                    sortedSeq = new Sequence(JsonataHelpers.ArrayFromBuilder(ref resultElements, env.Workspace));
                }

                if (stages[sortStepIdx] is not null)
                {
                    sortedSeq = ApplyStages(sortedSeq, stages[sortStepIdx]!, stageIsSortFlags[sortStepIdx]!, env, indexVars[sortStepIdx], null, constantIntStageValues[sortStepIdx]);
                }

                // Continue remaining steps per-element with restored index bindings
                if (sortStepIdx + 1 >= steps.Length)
                {
                    groupIndices.Dispose();
                    return sortedSeq;
                }

                var builder = default(SequenceBuilder);
                var finalElements = CollectFlatElements(sortedSeq);
                for (int i = 0; i < finalElements.Count; i++)
                {
                    if (i < groupIndices.Length)
                    {
                        env.Bind(idxVar, Sequence.FromDouble(groupIndices[i], env.Workspace));
                    }

                    var subResult = EvalPathFrom(new Sequence(finalElements[i]), sortStepIdx + 1, env);
                    builder.AddRange(subResult);
                }

                groupIndices.Dispose();
                finalElements.ReturnArray();
                return builder.ToSequence();
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
        var elements = default(SequenceBuilder);
        if (subResult.IsSingleton)
        {
            var el = subResult.FirstOrDefault;
            if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in el.EnumerateArray())
                {
                    elements.Add(item);
                }
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

        for (int ei = 0; ei < elements.Count; ei++)
        {
            var element = elements[ei];
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
                    throw new JsonataException("T1003", SR.T1003_KeyInObjectStructureMustEvaluateToAStringGotNumber, 0);
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
    /// Phase 1 of focus-aware group-by: evaluates the KEY expression per-element and
    /// stores both the focus element and the sub-result element under the group key.
    /// The VALUE expression is NOT evaluated here — it is deferred to
    /// <see cref="BuildFocusGroupByResult"/> so that the focus variable can be re-bound
    /// to the full sequence of matching elements for the group.
    /// </summary>
    private static void AccumulateFocusGroupKey(
        Sequence subResult,
        (ExpressionEvaluator Key, ExpressionEvaluator Value)[] pairs,
        List<string> groupKeys,
        Dictionary<string, (List<JsonElement> FocusElements, List<JsonElement> ContextElements, int PairIndex)> groupData,
        Environment env,
        JsonElement focusElement)
    {
        // Extract context elements from subResult for key evaluation
        var contextElements = default(SequenceBuilder);
        if (subResult.IsSingleton)
        {
            var el = subResult.FirstOrDefault;
            if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in el.EnumerateArray())
                {
                    contextElements.Add(item);
                }
            }
            else
            {
                contextElements.Add(el);
            }
        }
        else
        {
            for (int i = 0; i < subResult.Count; i++)
            {
                contextElements.Add(subResult[i]);
            }
        }

        for (int ei = 0; ei < contextElements.Count; ei++)
        {
            var element = contextElements[ei];
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
                    throw new JsonataException("T1003", SR.T1003_KeyInObjectStructureMustEvaluateToAStringGotNumber, 0);
                }
                else
                {
                    keyStr = CoerceElementToString(keySeq.FirstOrDefault);
                }

                if (!groupData.TryGetValue(keyStr, out var entry))
                {
                    entry = ([], [], pairIdx);
                    groupData[keyStr] = entry;
                    groupKeys.Add(keyStr);
                }

                entry.FocusElements.Add(focusElement);
                entry.ContextElements.Add(element);
            }
        }
    }

    /// <summary>
    /// Phase 2 of focus-aware group-by: for each group, re-binds the focus variable
    /// to a <see cref="Sequence"/> of all matching focus elements, builds the context
    /// from all matching sub-result elements, then evaluates the value expression once.
    /// This gives the value expression access to the full set of matching elements
    /// (e.g. <c>$join($c.Phone.number, ', ')</c> sees ALL phone numbers from all
    /// matching contacts, not just one contact at a time).
    /// </summary>
    private static Sequence BuildFocusGroupByResult(
        List<string> groupKeys,
        Dictionary<string, (List<JsonElement> FocusElements, List<JsonElement> ContextElements, int PairIndex)> groupData,
        (ExpressionEvaluator Key, ExpressionEvaluator Value)[] pairs,
        Environment env,
        string focusVar)
    {
        JsonDocumentBuilder<JsonElement.Mutable> objDoc = JsonElement.CreateObjectBuilder(env.Workspace, groupKeys.Count);
        JsonElement.Mutable objRoot = objDoc.RootElement;

        foreach (var key in groupKeys)
        {
            var (focusElements, contextElements, pairIndex) = groupData[key];
            var valueEval = pairs[pairIndex].Value;

            // Re-bind focus variable to the full sequence of matching focus elements
            if (focusElements.Count == 1)
            {
                env.Bind(focusVar, new Sequence(focusElements[0]));
            }
            else
            {
                // Use a pooled array so the env can safely return it on cleanup.
                var focusArr = ArrayPool<JsonElement>.Shared.Rent(focusElements.Count);
                for (int fe = 0; fe < focusElements.Count; fe++)
                {
                    focusArr[fe] = focusElements[fe];
                }

                env.Bind(focusVar, new Sequence(focusArr, focusElements.Count));
            }

            // Build context from sub-result elements for the value expression
            JsonElement context;
            if (contextElements.Count == 1)
            {
                context = contextElements[0];
            }
            else
            {
                context = JsonataHelpers.ArrayFromList(contextElements, env.Workspace);
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

    /// <summary>
    /// Merges multiple grouped objects (from nested focus binding iterations)
    /// into a single object. Same-key values are collected into arrays.
    /// </summary>
    private static Sequence MergeGroupedObjects(ref SequenceBuilder objects, Environment env)
    {
        var keys = new List<string>();
        var merged = new Dictionary<string, List<JsonElement>>();

        for (int o = 0; o < objects.Count; o++)
        {
            var obj = objects[o];
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

        objects.ReturnArray();

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
        var elements = default(SequenceBuilder);
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
                    foreach (var item in el.EnumerateArray())
                    {
                        elements.Add(item);
                    }
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
                env.Bind(tupleIndexVar, Sequence.FromDouble(elementGroupIndices[idx], env.Workspace));
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
                    throw new JsonataException("T1003", SR.T1003_KeyInObjectStructureMustEvaluateToAStringGotNumber, 0);
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
                        SR.Format(SR.D1009_MultipleKeyDefinitionsEvaluateToSameKey, keyStr),
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

    /// <summary>
    /// Fast-path group-by for the common single-pair pattern where both key and value are
    /// simple NameNode property accesses. Collects key-value pairs first, then groups and
    /// builds the CVB object in a single pass with exactly one SetProperty per unique key.
    /// Avoids Dictionary/List allocations, string extraction, and 2-phase evaluation.
    /// </summary>
    private static Sequence ApplySimpleNamePairGroupBy(
        Sequence current,
        byte[] keyPropUtf8,
        byte[] valuePropUtf8,
        Environment env)
    {
        if (current.IsUndefined)
        {
            return Sequence.Undefined;
        }

        // Phase 1: Collect (keyElement, valueElement) pairs from all elements.
        // Use ArrayPool for the pair buffers.
        int maxElements = CountSequenceElements(current);
        if (maxElements == 0)
        {
            return Sequence.Undefined;
        }

        JsonElement[] keyBuffer = ArrayPool<JsonElement>.Shared.Rent(maxElements);
        JsonElement[] valBuffer = ArrayPool<JsonElement>.Shared.Rent(maxElements);

        try
        {
            int pairCount = 0;

            if (current.IsSingleton)
            {
                CollectGroupByPairs(current.FirstOrDefault, keyPropUtf8, valuePropUtf8, keyBuffer, valBuffer, ref pairCount);
            }
            else
            {
                for (int i = 0; i < current.Count; i++)
                {
                    CollectGroupByPairs(current[i], keyPropUtf8, valuePropUtf8, keyBuffer, valBuffer, ref pairCount);
                }
            }

            if (pairCount == 0)
            {
                return Sequence.Undefined;
            }

            // Phase 2: Group by key and build CVB with exactly one SetProperty per group.
            // Use a simple O(n²) scan — optimal for typical small group counts.
            var objDoc = JsonElement.CreateObjectBuilder(env.Workspace, Math.Min(pairCount, 16));
            var objRoot = objDoc.RootElement;

            Span<bool> processed = pairCount <= 256
                ? stackalloc bool[Math.Min(pairCount, 256)]
                : new bool[pairCount];

            for (int i = 0; i < pairCount; i++)
            {
                if (processed[i])
                {
                    continue;
                }

                processed[i] = true;

                using var keyUtf8 = keyBuffer[i].GetUtf8String();
                ReadOnlySpan<byte> keyBytes = keyUtf8.Span;

                // Scan forward for duplicates of this key.
                bool hasDuplicates = false;
                for (int j = i + 1; j < pairCount; j++)
                {
                    if (!processed[j] && keyBuffer[j].ValueEquals(keyBytes))
                    {
                        hasDuplicates = true;
                        break;
                    }
                }

                if (!hasDuplicates)
                {
                    // Singleton group — set scalar value directly.
                    objRoot.SetProperty(keyBytes, valBuffer[i]);
                }
                else
                {
                    // Multi-element group — build array from all matching values.
                    var groupBuilder = default(SequenceBuilder);
                    groupBuilder.Add(valBuffer[i]);

                    for (int j = i + 1; j < pairCount; j++)
                    {
                        if (!processed[j] && keyBuffer[j].ValueEquals(keyBytes))
                        {
                            groupBuilder.Add(valBuffer[j]);
                            processed[j] = true;
                        }
                    }

                    var groupArray = JsonataHelpers.ArrayFromBuilder(ref groupBuilder, env.Workspace);
                    objRoot.SetProperty(keyBytes, groupArray);
                }
            }

            if (objRoot.GetPropertyCount() == 0)
            {
                return Sequence.Undefined;
            }

            return new Sequence((JsonElement)objRoot);
        }
        finally
        {
            ArrayPool<JsonElement>.Shared.Return(keyBuffer);
            ArrayPool<JsonElement>.Shared.Return(valBuffer);
        }

        // Collects key-value pairs from a single element (arrays are flattened).
        static void CollectGroupByPairs(
            JsonElement element,
            byte[] keyPropUtf8,
            byte[] valuePropUtf8,
            JsonElement[] keyBuffer,
            JsonElement[] valBuffer,
            ref int pairCount)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    TryAddGroupByPair(item, keyPropUtf8, valuePropUtf8, keyBuffer, valBuffer, ref pairCount);
                }
            }
            else
            {
                TryAddGroupByPair(element, keyPropUtf8, valuePropUtf8, keyBuffer, valBuffer, ref pairCount);
            }
        }

        static void TryAddGroupByPair(
            JsonElement element,
            byte[] keyPropUtf8,
            byte[] valuePropUtf8,
            JsonElement[] keyBuffer,
            JsonElement[] valBuffer,
            ref int pairCount)
        {
            if (!element.TryGetProperty(keyPropUtf8, out JsonElement keyEl))
            {
                return;
            }

            if (keyEl.ValueKind == JsonValueKind.Number)
            {
                throw new JsonataException("T1003", SR.T1003_KeyInObjectStructureMustEvaluateToAStringGotNumber, 0);
            }

            if (keyEl.ValueKind != JsonValueKind.String)
            {
                return;
            }

            if (!element.TryGetProperty(valuePropUtf8, out JsonElement valueEl))
            {
                return;
            }

            keyBuffer[pairCount] = keyEl;
            valBuffer[pairCount] = valueEl;
            pairCount++;
        }
    }

    /// <summary>
    /// Counts the total number of leaf elements in a sequence, flattening arrays.
    /// </summary>
    private static int CountSequenceElements(Sequence current)
    {
        if (current.IsSingleton)
        {
            var el = current.FirstOrDefault;
            return el.ValueKind == JsonValueKind.Array ? el.GetArrayLength() : 1;
        }

        int count = 0;
        for (int i = 0; i < current.Count; i++)
        {
            var el = current[i];
            count += el.ValueKind == JsonValueKind.Array ? el.GetArrayLength() : 1;
        }

        return count;
    }

    private static Sequence ApplyStages(Sequence current, ExpressionEvaluator[] stageEvaluators, bool[] isSortStage, Environment env, string? indexVar = null, string?[]? indexBindingVars = null, int[]? constantIntValues = null)
    {
        // Track any index binding variable introduced by a VariableNode stage.
        // Subsequent filter/predicate stages bind this variable per-element so
        // expressions like [$pos>=2] reference the correct position.
        string? activeIndexBinding = null;

        // Track whether we own current's backing array (created by a stage within this method).
        // The initial `current` is owned by the caller — we don't return it.
        bool currentArrayOwned = false;

        for (int s = 0; s < stageEvaluators.Length; s++)
        {
            if (current.IsUndefined)
            {
                return Sequence.Undefined;
            }

            // Index binding stage: register the variable for per-element binding
            // in subsequent stages, and expand elements (don't filter).
            if (indexBindingVars is not null && s < indexBindingVars.Length && indexBindingVars[s] is not null)
            {
                activeIndexBinding = indexBindingVars[s]!;

                // Expand singleton array to multi-value so subsequent stages
                // iterate individual elements
                if (current.IsSingleton)
                {
                    var el = current.FirstOrDefault;
                    if (el.ValueKind == JsonValueKind.Array)
                    {
                        var expanded = default(SequenceBuilder);
                        foreach (var item in el.EnumerateArray())
                        {
                            expanded.Add(item);
                        }

                        current = expanded.ToSequence();
                        currentArrayOwned = current.Count >= 2;
                    }
                }

                continue;
            }

            // Fast-path: constant integer index (e.g., [0], [1])
            // Directly index into the current collection without delegate evaluation
            if (constantIntValues is not null && s < constantIntValues.Length && constantIntValues[s] >= 0 && !isSortStage[s])
            {
                int idx = constantIntValues[s];
                if (current.IsSingleton)
                {
                    var el = current.FirstOrDefault;
                    if (el.ValueKind == JsonValueKind.Array)
                    {
                        if (idx < el.GetArrayLength())
                        {
                            if (currentArrayOwned) { current.ReturnBackingArray(); currentArrayOwned = false; }
                            current = new Sequence(el[idx]);
                        }
                        else
                        {
                            if (currentArrayOwned) { current.ReturnBackingArray(); }
                            return Sequence.Undefined;
                        }
                    }
                    else
                    {
                        // Singleton non-array: index 0 returns the value itself
                        if (idx != 0 && currentArrayOwned) { current.ReturnBackingArray(); currentArrayOwned = false; }
                        current = idx == 0 ? current : Sequence.Undefined;
                    }
                }
                else
                {
                    // Multi-value sequence: index directly — element is value-copied
                    if (idx < current.Count)
                    {
                        JsonElement el = current[idx];
                        if (currentArrayOwned) { current.ReturnBackingArray(); currentArrayOwned = false; }
                        current = new Sequence(el);
                    }
                    else
                    {
                        if (currentArrayOwned) { current.ReturnBackingArray(); }
                        return Sequence.Undefined;
                    }
                }

                continue;
            }

            var stage = stageEvaluators[s];

            // Collect all elements into a working set
            var elements = default(SequenceBuilder);
            if (current.IsSingleton)
            {
                var el = current.FirstOrDefault;
                if (el.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in el.EnumerateArray())
                    {
                        elements.Add(item);
                    }
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

            // All elements copied out — return old current's backing array.
            if (currentArrayOwned) { current.ReturnBackingArray(); currentArrayOwned = false; }

            if (isSortStage[s])
            {
                // In path contexts, property steps can produce arrays of arrays
                // (e.g. Account.Order.Product yields [productArray1, productArray2]).
                // Flatten one level so the sort sees individual elements.
                var sortElements = default(SequenceBuilder);
                for (int i = 0; i < elements.Count; i++)
                {
                    var el = elements[i];
                    if (el.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in el.EnumerateArray())
                        {
                            sortElements.Add(item);
                        }
                    }
                    else
                    {
                        sortElements.Add(el);
                    }
                }

                elements.ReturnArray();

                // Sort stages need all elements at once — build a JSON array
                // and pass it to the sort evaluator.
                if (sortElements.Count <= 1)
                {
                    if (sortElements.Count == 1)
                    {
                        current = new Sequence(sortElements[0]);
                    }

                    sortElements.ReturnArray();
                    currentArrayOwned = false;
                }
                else
                {
                    var arrayInput = JsonataHelpers.ArrayFromBuilder(ref sortElements, env.Workspace);
                    current = stage(arrayInput, env);
                    currentArrayOwned = current.Count >= 2;
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
                    env.Bind(indexVar, Sequence.FromDouble(i, env.Workspace));
                }

                // Bind any active index binding variable (from a preceding #$var stage)
                if (activeIndexBinding is not null)
                {
                    env.Bind(activeIndexBinding, Sequence.FromDouble(i, env.Workspace));
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

                // Check if the predicate result is a numeric index (or set of indices).
                // Instead of materializing a List<int>, check each index against
                // the current loop position inline to avoid per-element allocation.
                bool isNumericIndices = false;
                bool matchedCurrentIndex = false;

                if (result.IsSingleton && TryCoerceToNumber(firstResult, out double singleIdx))
                {
                    isNumericIndices = true;
                    int idx = (int)Math.Floor(singleIdx);
                    if (idx < 0)
                    {
                        idx = elements.Count + idx;
                    }

                    matchedCurrentIndex = idx == i;
                }
                else if (result.IsSingleton && firstResult.ValueKind == JsonValueKind.Array)
                {
                    // Singleton JSON array — check if all elements are numbers.
                    // Booleans in a predicate array are NOT numeric indices;
                    // their presence makes this a mixed predicate that falls
                    // through to truthiness evaluation.
                    bool allNum = true;
                    bool hasAnyIndex = false;
                    foreach (var arrEl in firstResult.EnumerateArray())
                    {
                        if (arrEl.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        {
                            allNum = false;
                            break;
                        }

                        if (TryCoerceToNumber(arrEl, out double numVal))
                        {
                            hasAnyIndex = true;
                            int idx = (int)Math.Floor(numVal);
                            if (idx < 0)
                            {
                                idx = elements.Count + idx;
                            }

                            if (idx == i)
                            {
                                matchedCurrentIndex = true;
                            }
                        }
                        else
                        {
                            allNum = false;
                            break;
                        }
                    }

                    if (allNum && hasAnyIndex)
                    {
                        isNumericIndices = true;
                    }
                }
                else if (!result.IsSingleton && result.Count > 0)
                {
                    // Multi-value sequence — check if all are numeric
                    bool allNum = true;
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

                        if (idx == i)
                        {
                            matchedCurrentIndex = true;
                        }
                    }

                    if (allNum)
                    {
                        isNumericIndices = true;
                    }
                }

                if (isNumericIndices)
                {
                    if (matchedCurrentIndex)
                    {
                        builder.Add(el);
                    }

                    continue;
                }

                // Boolean filter
                if (IsTruthy(result))
                {
                    builder.Add(el);
                }
            }

            elements.ReturnArray();
            current = builder.ToSequence();
            currentArrayOwned = current.Count >= 2;
        }

        // Don't return current's array — caller takes ownership.
        return current;
    }

    /// <summary>
    /// Applies stage predicates with focus variable re-binding per element.
    /// Identical to <see cref="ApplyStages"/> except that before evaluating each
    /// enables filter predicates referencing the focus variable (e.g.
    /// <c>[$l.isbn=$b.isbn]</c>) to work correctly, while index predicates
    /// (e.g. <c>[1]</c>) see the full element count.
    /// </summary>
    /// <param name="survivingOriginalIndices">
    /// When non-null, populated with the original (pre-filter) positions of the
    /// elements that survived all stages. Enables the caller to map filtered
    /// results back to their original array positions for index binding (#$var).
    /// </param>
    private static Sequence ApplyFocusStages(
        Sequence current,
        ExpressionEvaluator[] stageEvaluators,
        bool[] isSortStage,
        Environment env,
        string focusVar,
        string? indexVar = null,
        List<int>? survivingOriginalIndices = null)
    {
        // Track original indices through filtering stages.
        // When survivingOriginalIndices is non-null, we maintain a parallel
        // mapping from current element position to original array position.
        int[]? currentIndices = survivingOriginalIndices is not null
            ? new int[current.IsUndefined ? 0 : current.IsSingleton && current.FirstOrDefault.ValueKind == JsonValueKind.Array ? current.FirstOrDefault.GetArrayLength() : current.Count]
            : null;

        if (currentIndices is not null)
        {
            for (int ci = 0; ci < currentIndices.Length; ci++)
            {
                currentIndices[ci] = ci;
            }
        }

        bool currentArrayOwned = false;

        for (int s = 0; s < stageEvaluators.Length; s++)
        {
            if (current.IsUndefined)
            {
                return Sequence.Undefined;
            }

            var stage = stageEvaluators[s];

            var elements = default(SequenceBuilder);
            if (current.IsSingleton)
            {
                var el = current.FirstOrDefault;
                if (el.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in el.EnumerateArray())
                    {
                        elements.Add(item);
                    }
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

            // All elements copied out — return old current's backing array.
            if (currentArrayOwned) { current.ReturnBackingArray(); currentArrayOwned = false; }

            if (isSortStage[s])
            {
                var sortElements = default(SequenceBuilder);
                for (int i = 0; i < elements.Count; i++)
                {
                    var el = elements[i];
                    if (el.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in el.EnumerateArray())
                        {
                            sortElements.Add(item);
                        }
                    }
                    else
                    {
                        sortElements.Add(el);
                    }
                }

                elements.ReturnArray();

                if (sortElements.Count <= 1)
                {
                    if (sortElements.Count == 1)
                    {
                        current = new Sequence(sortElements[0]);
                    }

                    sortElements.ReturnArray();
                    currentArrayOwned = false;
                }
                else
                {
                    var arrayInput = JsonataHelpers.ArrayFromBuilder(ref sortElements, env.Workspace);
                    current = stage(arrayInput, env);
                    currentArrayOwned = current.Count >= 2;
                }

                // Sort stages reorder elements; original index tracking becomes
                // unreliable. Clear the mapping (caller won't use it for sorts).
                currentIndices = null;
                continue;
            }

            var builder = default(SequenceBuilder);
            bool trackIndices = currentIndices is not null;
            var nextIndices = trackIndices ? new ValueListBuilder<int>(elements.Count) : default;
            for (int i = 0; i < elements.Count; i++)
            {
                var el = elements[i];

                // Re-bind focus variable per-element so filter predicates work
                env.Bind(focusVar, new Sequence(el));

                // Bind index to the ORIGINAL array position so predicates like
                // [$ib > 1] reference the pre-filter position.
                if (indexVar is not null)
                {
                    int origIdx = currentIndices is not null ? currentIndices[i] : i;
                    env.Bind(indexVar, Sequence.FromDouble(origIdx, env.Workspace));
                }

                var result = stage(el, env);

                if (result.IsUndefined)
                {
                    continue;
                }

                var firstResult = result.FirstOrDefault;

                if (result.IsSingleton && firstResult.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    if (firstResult.ValueKind == JsonValueKind.True)
                    {
                        builder.Add(el);
                        if (trackIndices) { nextIndices.Append(currentIndices is not null ? currentIndices[i] : i); }
                    }

                    continue;
                }

                // Check if the predicate result is a numeric index (or set of indices).
                bool isNumericIndices = false;
                bool matchedCurrentIndex = false;

                if (result.IsSingleton && TryCoerceToNumber(firstResult, out double singleIdx))
                {
                    isNumericIndices = true;
                    int idx = (int)Math.Floor(singleIdx);
                    if (idx < 0)
                    {
                        idx = elements.Count + idx;
                    }

                    matchedCurrentIndex = idx == i && idx >= 0 && idx < elements.Count;
                }
                else if (result.IsSingleton && firstResult.ValueKind == JsonValueKind.Array)
                {
                    bool allNum = true;
                    bool hasAnyIndex = false;
                    foreach (var arrEl in firstResult.EnumerateArray())
                    {
                        if (arrEl.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        {
                            allNum = false;
                            break;
                        }

                        if (TryCoerceToNumber(arrEl, out double numVal))
                        {
                            hasAnyIndex = true;
                            int idx = (int)Math.Floor(numVal);
                            if (idx < 0)
                            {
                                idx = elements.Count + idx;
                            }

                            if (idx == i && idx >= 0 && idx < elements.Count)
                            {
                                matchedCurrentIndex = true;
                            }
                        }
                        else
                        {
                            allNum = false;
                            break;
                        }
                    }

                    if (allNum && hasAnyIndex)
                    {
                        isNumericIndices = true;
                    }
                }
                else if (!result.IsSingleton && result.Count > 0)
                {
                    bool allNum = true;
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

                        if (idx == i && idx >= 0 && idx < elements.Count)
                        {
                            matchedCurrentIndex = true;
                        }
                    }

                    if (allNum)
                    {
                        isNumericIndices = true;
                    }
                }

                if (isNumericIndices)
                {
                    if (matchedCurrentIndex)
                    {
                        builder.Add(el);
                        if (trackIndices) { nextIndices.Append(currentIndices is not null ? currentIndices[i] : i); }
                    }

                    continue;
                }

                if (IsTruthy(result))
                {
                    builder.Add(el);
                    if (trackIndices) { nextIndices.Append(currentIndices is not null ? currentIndices[i] : i); }
                }
            }

            elements.ReturnArray();
            current = builder.ToSequence();
            currentArrayOwned = current.Count >= 2;
            currentIndices = trackIndices ? nextIndices.AsSpan().ToArray() : null;
            nextIndices.Dispose();
        }

        // Output the surviving original indices for the caller
        if (survivingOriginalIndices is not null && currentIndices is not null)
        {
            survivingOriginalIndices.AddRange(currentIndices);
        }

        // Don't return current's array — caller takes ownership.
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
    private static SequenceBuilder CollectFlatElements(Sequence current)
    {
        var result = default(SequenceBuilder);
        if (current.IsSingleton)
        {
            var el = current.FirstOrDefault;
            if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in el.EnumerateArray())
                {
                    result.Add(item);
                }
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
                    foreach (var item in el.EnumerateArray())
                    {
                        result.Add(item);
                    }
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
        bool[]? stageIsSortFlags = null,
        int[]? constantIntValues = null)
    {
        var builder = default(SequenceBuilder);
        foreach (var item in array.EnumerateArray())
        {
            var result = step(item, env);

            // Apply per-element filter stages before flattening
            result = ApplyPerElementFilterStages(result, filterStages, stageIsSortFlags, env, constantIntValues);

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
    /// Inline name lookup over array elements without delegate dispatch.
    /// Recursively handles nested arrays (JSONata's one-level flattening).
    /// </summary>
    private static void InlineNameOverArray(in JsonElement array, byte[] nameUtf8, ref SequenceBuilder builder)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                if (item.TryGetProperty(nameUtf8, out var val))
                {
                    if (val.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var child in val.EnumerateArray())
                        {
                            builder.Add(child);
                        }
                    }
                    else
                    {
                        builder.Add(val);
                    }
                }
            }
            else if (item.ValueKind == JsonValueKind.Array)
            {
                InlineNameOverArray(item, nameUtf8, ref builder);
            }
        }
    }

    /// <summary>
    /// Fused descendant + property-name traversal. Recursively walks the element tree
    /// and collects only values of properties matching <paramref name="nameUtf8"/>,
    /// with JSONata array auto-flattening. This avoids building a Sequence of all
    /// descendants only to filter most of them in a second pass.
    /// </summary>
    private static void CollectDescendantProperty(in JsonElement element, byte[] nameUtf8, ref SequenceBuilder builder)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty((ReadOnlySpan<byte>)nameUtf8, out JsonElement val))
            {
                if (val.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in val.EnumerateArray())
                    {
                        builder.Add(item);
                    }
                }
                else
                {
                    builder.Add(val);
                }
            }

            foreach (var prop in element.EnumerateObject())
            {
                CollectDescendantProperty(prop.Value, nameUtf8, ref builder);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                CollectDescendantProperty(item, nameUtf8, ref builder);
            }
        }
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
        Environment env,
        int[]? constantIntValues = null)
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

            // Fast-path: constant integer index (e.g., [0], [1])
            // Directly index into the result without going through ApplyStages
            if (constantIntValues is not null && s < constantIntValues.Length && constantIntValues[s] >= 0)
            {
                int idx = constantIntValues[s];
                if (result.IsSingleton)
                {
                    var el = result.FirstOrDefault;
                    if (el.ValueKind == JsonValueKind.Array)
                    {
                        if (idx < el.GetArrayLength())
                        {
                            result = new Sequence(el[idx]);
                        }
                        else
                        {
                            return Sequence.Undefined;
                        }
                    }
                    else
                    {
                        // Singleton non-array: index 0 returns the value itself
                        result = idx == 0 ? result : Sequence.Undefined;
                    }
                }
                else
                {
                    // Multi-value sequence: index directly
                    if (idx < result.Count)
                    {
                        result = new Sequence(result[idx]);
                    }
                    else
                    {
                        return Sequence.Undefined;
                    }
                }

                continue;
            }

            var singleStage = t_singleStageArray ??= new ExpressionEvaluator[1];
            var singleFlag = t_singleFlagArray ??= [false];
            singleStage[0] = stageEvaluators[s];
            result = ApplyStages(result, singleStage, singleFlag, env);
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
        int sortCount = 0;
        for (int s = 0; s < stageEvaluators.Length; s++)
        {
            if (isSortStage[s])
            {
                sortCount++;
            }
        }

        var sortStages = new ExpressionEvaluator[sortCount];
        var sortFlags = new bool[sortCount];
        int si = 0;
        for (int s = 0; s < stageEvaluators.Length; s++)
        {
            if (isSortStage[s])
            {
                sortStages[si] = stageEvaluators[s];
                sortFlags[si] = true;
                si++;
            }
        }

        return ApplyStages(current, sortStages, sortFlags, env);
    }

    private static ExpressionEvaluator CompileBinary(BinaryNode binary)
    {
        var op = binary.Operator;

        // String concat gets special handling to inline $string() calls —
        // avoids intermediate string allocation by coercing directly to UTF-8.
        if (op == "&")
        {
            return CompileStringConcatInlined(binary.Lhs, binary.Rhs);
        }

        var lhs = Compile(binary.Lhs);
        var rhs = Compile(binary.Rhs);

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
            "and" => CompileAnd(lhs, rhs),
            "or" => CompileOr(lhs, rhs),
            "in" => CompileIn(lhs, rhs),
            ".." => CompileRange(lhs, rhs),
            _ => throw new JsonataException("D1001", SR.Format(SR.D1001_UnknownBinaryOperator, op), binary.Position),
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
                    throw new JsonataException("D1001", SR.Format(SR.D1001_NumberOutOfRangeWithValue, left.NonFiniteValue), 0);
                }

                if (left.IsRawDouble)
                {
                    // Raw double — already a number, fast path
                }
                else if (left.FirstOrDefault.ValueKind != JsonValueKind.Number)
                {
                    throw new JsonataException("T2001", SR.T2001_TheLeftSideOfTheArithmeticExpressionIsNotANumber, 0);
                }
            }

            if (!right.IsUndefined)
            {
                if (right.IsNonFinite)
                {
                    throw new JsonataException("D1001", SR.Format(SR.D1001_NumberOutOfRangeWithValue, right.NonFiniteValue), 0);
                }

                if (right.IsRawDouble)
                {
                    // Raw double — already a number, fast path
                }
                else if (right.FirstOrDefault.ValueKind != JsonValueKind.Number)
                {
                    throw new JsonataException("T2002", SR.T2002_TheRightSideOfTheArithmeticExpressionIsNotANumber, 0);
                }
            }

            if (left.IsUndefined || right.IsUndefined)
            {
                return Sequence.Undefined;
            }

            double leftNum;
            if (!left.TryGetDouble(out leftNum))
            {
                leftNum = left.FirstOrDefault.GetDouble();
            }

            double rightNum;
            if (!right.TryGetDouble(out rightNum))
            {
                rightNum = right.FirstOrDefault.GetDouble();
            }

            double result = op(leftNum, rightNum);
            if (double.IsInfinity(result) || double.IsNaN(result))
            {
                return new Sequence(result);
            }

            return Sequence.FromDouble(result, env.Workspace);
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
                return new Sequence(JsonataHelpers.BooleanElement(false));
            }

            // Fast path: both are raw doubles (common in arithmetic chains)
            if (left.TryGetDouble(out double ld) && right.TryGetDouble(out double rd))
            {
                return new Sequence(JsonataHelpers.BooleanElement(ld == rd));
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

            // Fast path: both are raw doubles or numeric elements
            if (left.TryGetDouble(out double ld) && right.TryGetDouble(out double rd))
            {
                return new Sequence(JsonataHelpers.BooleanElement(cmp(ld, rd)));
            }

            // Check for invalid types BEFORE undefined check — boolean/null in comparison is always an error
            if (!left.IsUndefined && !left.IsRawDouble)
            {
                var l = left.FirstOrDefault;
                if (l.ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null
                    or JsonValueKind.Array or JsonValueKind.Object)
                {
                    throw new JsonataException("T2010", SR.T2010_TheExpressionsEitherSideOfOperatorMustBeBothNumbersOrBothStr, 0);
                }
            }

            if (!right.IsUndefined && !right.IsRawDouble)
            {
                var r = right.FirstOrDefault;
                if (r.ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null
                    or JsonValueKind.Array or JsonValueKind.Object)
                {
                    throw new JsonataException("T2010", SR.T2010_TheExpressionsEitherSideOfOperatorMustBeBothNumbersOrBothStr, 0);
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
            throw new JsonataException("T2009", SR.T2009_TheValuesEitherSideOfTheOperatorMustBeOfTheSameDataType, 0);
        };
    }

    /// <summary>
    /// Compiles a string concatenation (<c>&amp;</c>) with $string() inlining.
    /// When an operand is <c>$string(x)</c> with a single argument, compiles just <c>x</c>
    /// and lets the existing <see cref="AppendCoercedToBuffer"/> handle the coercion
    /// directly to UTF-8, avoiding intermediate string allocation.
    /// </summary>
    private static ExpressionEvaluator CompileStringConcatInlined(JsonataNode lhsNode, JsonataNode rhsNode)
    {
        var lhs = IsSimpleStringCall(lhsNode, out var innerLhs) ? Compile(innerLhs) : Compile(lhsNode);
        var rhs = IsSimpleStringCall(rhsNode, out var innerRhs) ? Compile(innerRhs) : Compile(rhsNode);
        return CompileStringConcat(lhs, rhs);
    }

    /// <summary>
    /// Checks whether a node is a simple <c>$string(x)</c> call (1 argument, no pretty-print).
    /// </summary>
    private static bool IsSimpleStringCall(JsonataNode node, [NotNullWhen(true)] out JsonataNode? innerArg)
    {
        if (node is FunctionCallNode { Procedure: VariableNode { Name: "string" }, Arguments: { Count: 1 } args })
        {
            innerArg = args[0];
            return true;
        }

        innerArg = null;
        return false;
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

        // Raw double fast path: format directly without materializing a JsonElement
        if (seq.IsRawDouble && seq.TryGetDouble(out double d))
        {
            AppendNumberToBuffer(d, ref buffer, ref pos);
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
            AppendNumberToBuffer(elem, ref buffer, ref pos);
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
    /// Appends a number element's formatted UTF-8 representation directly to the buffer.
    /// For integers with clean raw text (no decimal/exponent), copies raw UTF-8 bytes
    /// with zero string allocation. For non-integers, falls back to G15 formatting.
    /// </summary>
    private static void AppendNumberToBuffer(JsonElement elem, ref byte[] buffer, ref int pos)
    {
        double value = elem.GetDouble();
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            AppendNullToBuffer(ref buffer, ref pos);
            return;
        }

        // Integer fast path: copy raw UTF-8 bytes directly (zero allocation)
        if (value == Math.Floor(value) && !double.IsInfinity(value))
        {
            using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(elem);
            ReadOnlySpan<byte> rawSpan = raw.Span;

            // Only use raw bytes if the text is a plain integer literal (no '.' or 'e'/'E')
            bool isPlainInteger = true;
            for (int i = 0; i < rawSpan.Length; i++)
            {
                byte b = rawSpan[i];
                if (b == (byte)'.' || b == (byte)'e' || b == (byte)'E')
                {
                    isPlainInteger = false;
                    break;
                }
            }

            if (isPlainInteger)
            {
                JsonataHelpers.GrowBufferIfNeeded(ref buffer, pos, rawSpan.Length);
                rawSpan.CopyTo(buffer.AsSpan(pos));
                pos += rawSpan.Length;
                return;
            }
        }

        // Non-integer path: format via G15 then write as ASCII bytes
        AppendG15FormattedNumber(value, ref buffer, ref pos);
    }

    /// <summary>
    /// Appends a raw double value's formatted UTF-8 representation directly to the buffer.
    /// For integer values, writes the integer form directly. For non-integers, uses G15 formatting.
    /// </summary>
    private static void AppendNumberToBuffer(double value, ref byte[] buffer, ref int pos)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            AppendNullToBuffer(ref buffer, ref pos);
            return;
        }

        // Integer fast path: format as integer directly
        if (value == Math.Floor(value) && !double.IsInfinity(value) && Math.Abs(value) < 1e15)
        {
            long intVal = (long)value;
            Span<byte> intBuffer = stackalloc byte[20]; // max long is 19 digits + sign
#if NET
            if (Utf8Formatter.TryFormat(intVal, intBuffer, out int intBytesWritten))
#else
            if (System.Buffers.Text.Utf8Formatter.TryFormat(intVal, intBuffer, out int intBytesWritten))
#endif
            {
                JsonataHelpers.GrowBufferIfNeeded(ref buffer, pos, intBytesWritten);
                intBuffer.Slice(0, intBytesWritten).CopyTo(buffer.AsSpan(pos));
                pos += intBytesWritten;
                return;
            }
        }

        // Non-integer path: format via G15 then write as ASCII bytes
        AppendG15FormattedNumber(value, ref buffer, ref pos);
    }

    private static void AppendNullToBuffer(ref byte[] buffer, ref int pos)
    {
        JsonataHelpers.GrowBufferIfNeeded(ref buffer, pos, 4);
        buffer[pos++] = (byte)'n';
        buffer[pos++] = (byte)'u';
        buffer[pos++] = (byte)'l';
        buffer[pos++] = (byte)'l';
    }

    private static void AppendG15FormattedNumber(double value, ref byte[] buffer, ref int pos)
    {
#if NET8_0_OR_GREATER
        // Try formatting G15 directly to UTF-8 (zero alloc for non-exponent case)
        Span<byte> scratch = stackalloc byte[64];
        if (value.TryFormat(scratch, out int written, "G15", System.Globalization.CultureInfo.InvariantCulture))
        {
            ReadOnlySpan<byte> result = scratch.Slice(0, written);
            if (result.IndexOf((byte)'E') < 0)
            {
                JsonataHelpers.GrowBufferIfNeeded(ref buffer, pos, written);
                result.CopyTo(buffer.AsSpan(pos));
                pos += written;
                return;
            }
        }
#endif

        // Exponent case or pre-.NET 8: fall back to string-based formatting
        string formatted = FormatNumberLikeJavaScript(value);
        JsonataHelpers.GrowBufferIfNeeded(ref buffer, pos, formatted.Length);
        for (int i = 0; i < formatted.Length; i++)
        {
            buffer[pos++] = (byte)formatted[i];
        }
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
                // Use UTF-8 span comparison to avoid string allocation
                using RawUtf8JsonString needleRaw = JsonMarshal.GetRawUtf8Value(needle);
                using RawUtf8JsonString haystackRaw = JsonMarshal.GetRawUtf8Value(haystack);

                // Raw spans include quotes, so strip them for content comparison
                ReadOnlySpan<byte> needleSpan = needleRaw.Span;
                ReadOnlySpan<byte> haystackSpan = haystackRaw.Span;

                if (needleSpan.Length >= 2 && needleSpan[0] == (byte)'"')
                {
                    needleSpan = needleSpan.Slice(1, needleSpan.Length - 2);
                }

                if (haystackSpan.Length >= 2 && haystackSpan[0] == (byte)'"')
                {
                    haystackSpan = haystackSpan.Slice(1, haystackSpan.Length - 2);
                }

                return new Sequence(JsonataHelpers.BooleanElement(haystackSpan.IndexOf(needleSpan) >= 0));
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
                    throw new JsonataException("T2003", SR.T2003_TheLeftSideOfTheRangeOperatorMustEvaluateToAnInteger, 0);
                }
            }

            if (!right.IsUndefined)
            {
                var rightElem = right.FirstOrDefault;
                if (rightElem.ValueKind != JsonValueKind.Number)
                {
                    throw new JsonataException("T2004", SR.T2004_TheRightSideOfTheRangeOperatorMustEvaluateToAnInteger, 0);
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
                throw new JsonataException("T2003", SR.T2003_TheLeftSideOfTheRangeOperatorMustEvaluateToAnInteger, 0);
            }

            if (end != Math.Floor(end))
            {
                throw new JsonataException("T2004", SR.T2004_TheRightSideOfTheRangeOperatorMustEvaluateToAnInteger, 0);
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
                throw new JsonataException("D2014", SR.D2014_RangeExpressionGeneratesTooManyResults, 0);
            }

            // Use lazy range for large ranges to avoid materializing millions of elements
            if (count > 100_000)
            {
                return new Sequence(new Sequence.LazyRangeInfo(iStart, iEnd, env.Workspace));
            }

            // Build individual number elements for the range
            var elements = ArrayPool<JsonElement>.Shared.Rent((int)count);
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
                if (result.IsRawDouble)
                {
                    result.TryGetDouble(out double d);
                    return Sequence.FromDouble(-d, env.Workspace);
                }

                if (el.ValueKind == JsonValueKind.Number)
                {
                    return Sequence.FromDouble(-el.GetDouble(), env.Workspace);
                }

                throw new JsonataException("D1002", SR.D1002_CannotNegateANonNumericValue, unary.Position);
            };
        }

        throw new JsonataException("D1001", SR.Format(SR.D1001_UnknownUnaryOperator, unary.Operator), unary.Position);
    }

    private static ExpressionEvaluator CompileBlock(BlockNode block)
    {
        var exprs = block.Expressions.Select(Compile).ToArray();
        var annotations = GetStepAnnotations(block);
        bool isTuple = annotations?.Tuple == true;

        return (in JsonElement input, Environment env) =>
        {
            // Tuple-mode blocks (containing ancestry-bound paths) skip child env creation
            // so that ancestor label bindings propagate to the caller's environment.
            // This matches jsonata-js where tuple stream bindings flow through blocks.
            if (isTuple)
            {
                Sequence result = Sequence.Undefined;
                foreach (var expr in exprs)
                {
                    result = expr(input, env);
                }

                return result;
            }
            else
            {
                var evalEnv = Environment.RentChild(env);
                try
                {
                    Sequence result = Sequence.Undefined;
                    foreach (var expr in exprs)
                    {
                        result = expr(input, evalEnv);
                    }

                    return result;
                }
                finally
                {
                    Environment.ReturnChild(evalEnv);
                }
            }
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
        // All-constant array: pre-evaluate at compile time, zero per-call cost.
        if (IsConstantExpression(arr))
        {
            return CompileConstant(arr);
        }

        // Fused array-of-objects: detect [path.to.{"const-key": value, ...}]
        // When the sole expression is a path ending in a constant-key object constructor
        // with no annotations on the last step, we can build each object directly into
        // the array's document using AddItem(ctx, ObjectBuilder.Build) — eliminating
        // one intermediate document allocation and copy per element.
        if (arr.Expressions.Count == 1
            && arr.Expressions[0] is PathNode fusionPath
            && fusionPath.Steps.Count >= 2
            && fusionPath.Steps[^1] is ObjectConstructorNode fusionObj
            && fusionObj.Pairs.Count > 0
            && fusionObj.Pairs.All(p => p.Key is StringNode)
            && GetStepAnnotations(fusionPath.Steps[^1]) is null
            && GetStepAnnotations(fusionPath)?.Group is null)
        {
            return CompileFusedArrayOfObjects(fusionPath, fusionObj);
        }

        // Track which sub-expressions are array constructors (pushed as single elements)
        // vs other expressions (flattened — individual elements appended)
        var items = arr.Expressions.Select(e => (Eval: Compile(e), IsArrayCtor: e is ArrayConstructorNode)).ToArray();
        return (in JsonElement input, Environment env) =>
        {
            // Start optimistically building a JSON array. If a non-JSON value (lambda, regex)
            // is encountered, switch to a tuple Sequence that preserves function references.
            JsonDocumentBuilder<JsonElement.Mutable>? arrDoc = null;
            JsonElement.Mutable arrRoot = default;
            List<Sequence>? tupleItems = null;

            foreach (var (eval, isArrayCtor) in items)
            {
                var result = eval(input, env);
                if (result.IsUndefined)
                {
                    continue;
                }

                // Switch to tuple path on first non-JSON value or lazy range
                if (tupleItems is null && (result.IsLambda || result.IsRegex || result.IsTupleSequence || result.IsLazyRange))
                {
                    tupleItems = new List<Sequence>();

                    // Backfill any items already added to the JSON array
                    if (arrDoc is not null)
                    {
                        foreach (var child in arrRoot.EnumerateArray())
                        {
                            tupleItems.Add(new Sequence(child));
                        }
                    }
                }

                if (tupleItems is not null)
                {
                    // Tuple path: preserve full Sequences including lambdas and lazy ranges
                    if (result.IsLambda || result.IsRegex || result.IsLazyRange)
                    {
                        // Add as a single opaque item — lazy ranges are not materialized
                        tupleItems.Add(result);
                    }
                    else if (result.IsTupleSequence)
                    {
                        for (int i = 0; i < result.Count; i++)
                        {
                            tupleItems.Add(result.GetItemSequence(i));
                        }
                    }
                    else if (isArrayCtor)
                    {
                        for (int i = 0; i < result.Count; i++)
                        {
                            tupleItems.Add(result.GetItemSequence(i));
                        }
                    }
                    else if (result.IsSingleton)
                    {
                        var item = result.FirstOrDefault;
                        if (item.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var child in item.EnumerateArray())
                            {
                                tupleItems.Add(new Sequence(child));
                            }
                        }
                        else
                        {
                            tupleItems.Add(result);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < result.Count; i++)
                        {
                            tupleItems.Add(new Sequence(result[i]));
                        }
                    }
                }
                else
                {
                    // JSON path: build into a JSON array document
                    if (arrDoc is null)
                    {
                        arrDoc = JsonElement.CreateArrayBuilder(env.Workspace, items.Length);
                        arrRoot = arrDoc.RootElement;
                    }

                    if (isArrayCtor)
                    {
                        for (int i = 0; i < result.Count; i++)
                        {
                            arrRoot.AddItem(result[i]);
                        }
                    }
                    else if (result.IsSingleton)
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

            if (tupleItems is not null)
            {
                // If the only item is a lazy range, return it directly (avoid
                // wrapping in a tuple which would lose the range's element count)
                if (tupleItems.Count == 1 && tupleItems[0].IsLazyRange)
                {
                    return tupleItems[0];
                }

                return new Sequence(tupleItems.ToArray());
            }

            if (arrDoc is null)
            {
                arrDoc = JsonElement.CreateArrayBuilder(env.Workspace, 0);
                arrRoot = arrDoc.RootElement;
            }

            return new Sequence((JsonElement)arrRoot);
        };
    }

    /// <summary>
    /// Fused compilation for <c>[path.to.{"const-key": value, ...}]</c>.
    /// Instead of creating an intermediate document per object, each object is built
    /// directly into the array's document via <c>AddItem(ctx, ObjectBuilder.Build)</c>.
    /// </summary>
    private static ExpressionEvaluator CompileFusedArrayOfObjects(PathNode fullPath, ObjectConstructorNode objNode)
    {
        // Compile the prefix path (all steps except the last object constructor)
        var prefixPath = new PathNode();
        for (int i = 0; i < fullPath.Steps.Count - 1; i++)
        {
            prefixPath.Steps.Add(fullPath.Steps[i]);
        }

        var prefixEval = CompilePath(prefixPath);

        // Compile object constructor keys/values
        var keyStrings = new string[objNode.Pairs.Count];
        var keyUtf8 = new byte[objNode.Pairs.Count][];
        var values = new ExpressionEvaluator[objNode.Pairs.Count];

        for (int i = 0; i < objNode.Pairs.Count; i++)
        {
            keyStrings[i] = ((StringNode)objNode.Pairs[i].Key).Value;
            keyUtf8[i] = System.Text.Encoding.UTF8.GetBytes(keyStrings[i]);
            values[i] = Compile(objNode.Pairs[i].Value);
        }

        // Validate no duplicate keys at compile time
        for (int i = 0; i < keyStrings.Length; i++)
        {
            for (int j = i + 1; j < keyStrings.Length; j++)
            {
                if (keyStrings[i] == keyStrings[j])
                {
                    throw new JsonataException(
                        "D1009",
                        SR.Format(SR.D1009_MultipleKeyDefinitionsEvaluateToSameKey, keyStrings[i]),
                        0);
                }
            }
        }

        return (in JsonElement input, Environment env) =>
        {
            // Evaluate prefix path to get input elements
            var prefixResult = prefixEval(input, env);
            if (prefixResult.IsUndefined)
            {
                // Empty array for undefined path result
                var emptyDoc = JsonElement.CreateArrayBuilder(env.Workspace, 0);
                return new Sequence((JsonElement)emptyDoc.RootElement);
            }

            // Create array builder with estimated capacity
            var arrDoc = JsonElement.CreateArrayBuilder(env.Workspace, Math.Max(prefixResult.Count * (values.Length + 1), 4));
            var arrRoot = arrDoc.RootElement;

            if (prefixResult.IsSingleton)
            {
                var element = prefixResult.FirstOrDefault;
                if (element.ValueKind == JsonValueKind.Array)
                {
                    foreach (var child in element.EnumerateArray())
                    {
                        FusedBuildObjectIntoArray(ref arrRoot, child, keyUtf8, keyStrings, values, env);
                    }
                }
                else
                {
                    FusedBuildObjectIntoArray(ref arrRoot, element, keyUtf8, keyStrings, values, env);
                }
            }
            else
            {
                for (int i = 0; i < prefixResult.Count; i++)
                {
                    var element = prefixResult[i];
                    if (element.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var child in element.EnumerateArray())
                        {
                            FusedBuildObjectIntoArray(ref arrRoot, child, keyUtf8, keyStrings, values, env);
                        }
                    }
                    else
                    {
                        FusedBuildObjectIntoArray(ref arrRoot, element, keyUtf8, keyStrings, values, env);
                    }
                }

                prefixResult.ReturnBackingArray();
            }

            return new Sequence((JsonElement)arrRoot);
        };
    }

    /// <summary>
    /// Builds one constant-key object directly into an array's document,
    /// avoiding an intermediate document allocation and copy.
    /// </summary>
    private static void FusedBuildObjectIntoArray(
        ref JsonElement.Mutable arrRoot,
        in JsonElement element,
        byte[][] keyUtf8,
        string[] keyStrings,
        ExpressionEvaluator[] values,
        Environment env)
    {
        if (values.Length >= 2 && element.ValueKind == JsonValueKind.Object)
        {
            element.EnsurePropertyMap();
        }

        var ctx = new ConstKeyObjectBuildContext(keyUtf8, keyStrings, values, element, env);
        arrRoot.AddItem(ctx, ConstKeyObjectBuilderCallback, values.Length);

        // Clean up lambda side-channel (extremely rare — only when a property
        // value expression returns a lambda, which serializes as an empty string).
        env.TempObjectLambdas = null;
    }

    /// <summary>
    /// Shared static callback for building a constant-key object into a
    /// <see cref="JsonElement.ObjectBuilder"/>. Used by both standalone object
    /// construction and fused array-of-objects construction.
    /// </summary>
    private static void ConstKeyObjectBuilderCallback(in ConstKeyObjectBuildContext c, ref JsonElement.ObjectBuilder builder)
    {
        for (int i = 0; i < c.Values.Length; i++)
        {
            var valueResult = c.Values[i](c.Input, c.Env);

            if (valueResult.IsLambda)
            {
                // Lambda values serialize as empty strings in JSON, but we preserve
                // the reference via Environment side-channel so callers can retrieve them.
                c.Env.TempObjectLambdas ??= new Dictionary<string, LambdaValue>();
                c.Env.TempObjectLambdas[c.KeyStrings[i]] = valueResult.Lambda!;
                builder.AddProperty(c.KeyUtf8[i], JsonataHelpers.EmptyString());
                continue;
            }

            if (valueResult.IsUndefined)
            {
                continue;
            }

            if (valueResult.IsNonFinite)
            {
                throw new JsonataException("D1001", SR.Format(SR.D1001_NumberOutOfRangeWithValue, valueResult.NonFiniteValue), 0);
            }

            if (valueResult.IsSingleton)
            {
                // Fast path: raw doubles (from arithmetic) bypass FixedJsonValueDocument.
                if (valueResult.TryGetDouble(out double d))
                {
                    builder.AddProperty(c.KeyUtf8[i], d);
                }
                else
                {
                    builder.AddProperty(c.KeyUtf8[i], valueResult.FirstOrDefault);
                }
            }
            else
            {
                // Multi-value result → build inline array
                builder.AddProperty(
                    c.KeyUtf8[i],
                    valueResult,
                    static (in Sequence seq, ref JsonElement.ArrayBuilder ab) =>
                    {
                        for (int j = 0; j < seq.Count; j++)
                        {
                            ab.AddItem(seq[j]);
                        }
                    });
            }
        }
    }

    private static ExpressionEvaluator CompileObjectConstructor(ObjectConstructorNode obj)
    {
        // All-constant object: pre-evaluate at compile time, zero per-call cost.
        if (IsConstantExpression(obj))
        {
            return CompileConstant(obj);
        }

        // Check if ALL keys are compile-time string constants.
        // When they are, we can skip runtime key evaluation, GetString(),
        // and the duplicate-detection dictionary entirely.
        bool allKeysConstant = obj.Pairs.All(p => p.Key is StringNode);

        if (allKeysConstant && obj.Pairs.Count > 0)
        {
            return CompileObjectConstructorConstantKeys(obj);
        }

        var pairs = obj.Pairs.Select(p => (Key: Compile(p.Key), Value: Compile(p.Value))).ToArray();
        bool hasMultiplePairs = pairs.Length > 1;
        return (in JsonElement input, Environment env) =>
        {
            // Build a hash-based property map for O(1) lookups when multiple key/value
            // expressions will access properties on the same input element.
            if (pairs.Length >= 2 && input.ValueKind == JsonValueKind.Object)
            {
                input.EnsurePropertyMap();
            }

            // For multi-pair objects, rent tracking arrays for duplicate detection.
            // Single-pair objects (common — e.g. group-by) skip this entirely.
            JsonElement[]? seenKeyElements = hasMultiplePairs ? ArrayPool<JsonElement>.Shared.Rent(pairs.Length) : null;
            int[]? seenPairIndices = hasMultiplePairs ? ArrayPool<int>.Shared.Rent(pairs.Length) : null;
            try
            {
                var ctx = new DynKeyObjectBuildContext(pairs, seenKeyElements, seenPairIndices, input, env);
                JsonDocumentBuilder<JsonElement.Mutable> objDoc = JsonElement.CreateBuilder(
                    env.Workspace,
                    in ctx,
                    static (in DynKeyObjectBuildContext c, ref JsonElement.ObjectBuilder builder) =>
                    {
                        int seenCount = 0;

                        for (int pairIdx = 0; pairIdx < c.Pairs.Length; pairIdx++)
                        {
                            var (key, value) = c.Pairs[pairIdx];
                            var keyResult = key(c.Input, c.Env);
                            var valueResult = value(c.Input, c.Env);

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
                                    SR.Format(SR.T1003_KeyMustBeStringGotType, gotType),
                                    0);
                            }

                            JsonElement keyElement = keyResult.FirstOrDefault;
                            using UnescapedUtf8JsonString utf8Key = keyElement.GetUtf8String();
                            ReadOnlySpan<byte> keyBytes = utf8Key.Span;

                            // D1009: duplicate key from different pair expressions (linear scan, pair count is tiny)
                            if (c.SeenKeyElements is not null)
                            {
                                for (int j = 0; j < seenCount; j++)
                                {
                                    if (c.SeenKeyElements[j].ValueEquals(keyBytes) && c.SeenPairIndices![j] != pairIdx)
                                    {
                                        throw new JsonataException(
                                            "D1009",
                                            SR.Format(SR.D1009_MultipleKeyDefinitionsEvaluateToSameKey, keyElement.GetString()),
                                            0);
                                    }
                                }

                                c.SeenKeyElements[seenCount] = keyElement;
                                c.SeenPairIndices![seenCount] = pairIdx;
                                seenCount++;
                            }

                            if (valueResult.IsLambda)
                            {
                                // Lambda values need a string key for the TempObjectLambdas dictionary.
                                // This path is extremely rare — only allocate the string here.
                                c.Env.TempObjectLambdas ??= new Dictionary<string, LambdaValue>();
                                c.Env.TempObjectLambdas[keyElement.GetString()!] = valueResult.Lambda!;
                                builder.AddProperty(keyBytes, JsonataHelpers.EmptyString());
                                continue;
                            }

                            if (valueResult.IsUndefined)
                            {
                                continue;
                            }

                            if (valueResult.IsNonFinite)
                            {
                                throw new JsonataException("D1001", SR.Format(SR.D1001_NumberOutOfRangeWithValue, valueResult.NonFiniteValue), 0);
                            }

                            if (valueResult.IsSingleton)
                            {
                                if (valueResult.TryGetDouble(out double d))
                                {
                                    builder.AddProperty(keyBytes, d);
                                }
                                else
                                {
                                    builder.AddProperty(keyBytes, valueResult.FirstOrDefault);
                                }
                            }
                            else
                            {
                                builder.AddProperty(
                                    keyBytes,
                                    valueResult,
                                    static (in Sequence seq, ref JsonElement.ArrayBuilder ab) =>
                                    {
                                        for (int j = 0; j < seq.Count; j++)
                                        {
                                            ab.AddItem(seq[j]);
                                        }
                                    });
                            }
                        }
                    },
                    pairs.Length);

                var lambdas = env.TempObjectLambdas;
                env.TempObjectLambdas = null;

                if (lambdas is not null)
                {
                    return new Sequence((JsonElement)objDoc.RootElement, lambdas);
                }

                return new Sequence((JsonElement)objDoc.RootElement);
            }
            finally
            {
                if (seenKeyElements is not null)
                {
                    Array.Clear(seenKeyElements, 0, pairs.Length);
                    ArrayPool<JsonElement>.Shared.Return(seenKeyElements);
                }

                if (seenPairIndices is not null)
                {
                    ArrayPool<int>.Shared.Return(seenPairIndices);
                }
            }
        };
    }

    /// <summary>
    /// Fast path for object constructors where all keys are compile-time string constants.
    /// Eliminates per-object key evaluation, GetString(), and duplicate-detection dictionary.
    /// Uses forward-only CVB building via <c>CreateBuilder</c>
    /// instead of the create-empty-then-mutate pattern.
    /// </summary>
    private static ExpressionEvaluator CompileObjectConstructorConstantKeys(ObjectConstructorNode obj)
    {
        var keyStrings = new string[obj.Pairs.Count];
        var keyUtf8 = new byte[obj.Pairs.Count][];
        var values = new ExpressionEvaluator[obj.Pairs.Count];

        for (int i = 0; i < obj.Pairs.Count; i++)
        {
            keyStrings[i] = ((StringNode)obj.Pairs[i].Key).Value;
            keyUtf8[i] = System.Text.Encoding.UTF8.GetBytes(keyStrings[i]);
            values[i] = Compile(obj.Pairs[i].Value);
        }

        // Validate no duplicate keys at compile time
        for (int i = 0; i < keyStrings.Length; i++)
        {
            for (int j = i + 1; j < keyStrings.Length; j++)
            {
                if (keyStrings[i] == keyStrings[j])
                {
                    throw new JsonataException(
                        "D1009",
                        SR.Format(SR.D1009_MultipleKeyDefinitionsEvaluateToSameKey, keyStrings[i]),
                        0);
                }
            }
        }

        return (in JsonElement input, Environment env) =>
        {
            // Build a hash-based property map for O(1) lookups when multiple value
            // expressions will access properties on the same input element.
            // The map is idempotent and persists in the document, so the cost is
            // amortized to zero on subsequent evaluations of the same element.
            if (values.Length >= 2 && input.ValueKind == JsonValueKind.Object)
            {
                input.EnsurePropertyMap();
            }

            var ctx = new ConstKeyObjectBuildContext(keyUtf8, keyStrings, values, input, env);
            JsonDocumentBuilder<JsonElement.Mutable> objDoc = JsonElement.CreateBuilder(
                env.Workspace,
                in ctx,
                ConstKeyObjectBuilderCallback,
                values.Length);

            var lambdas = env.TempObjectLambdas;
            env.TempObjectLambdas = null;

            if (lambdas is not null)
            {
                return new Sequence((JsonElement)objDoc.RootElement, lambdas);
            }

            return new Sequence((JsonElement)objDoc.RootElement);
        };
    }

    /// <summary>
    /// Context struct for the CVB object builder callback in constant-key object construction.
    /// Passed by <c>in</c> reference to avoid heap allocation.
    /// </summary>
    private readonly struct ConstKeyObjectBuildContext(
        byte[][] keyUtf8,
        string[] keyStrings,
        ExpressionEvaluator[] values,
        JsonElement input,
        Environment env)
    {
        public readonly byte[][] KeyUtf8 = keyUtf8;
        public readonly string[] KeyStrings = keyStrings;
        public readonly ExpressionEvaluator[] Values = values;
        public readonly JsonElement Input = input;
        public readonly Environment Env = env;
    }

    /// <summary>
    /// Context struct for the CVB object builder callback in dynamic-key object construction.
    /// Passed by <c>in</c> reference to avoid heap allocation.
    /// </summary>
    private readonly struct DynKeyObjectBuildContext(
        (ExpressionEvaluator Key, ExpressionEvaluator Value)[] pairs,
        JsonElement[]? seenKeyElements,
        int[]? seenPairIndices,
        JsonElement input,
        Environment env)
    {
        public readonly (ExpressionEvaluator Key, ExpressionEvaluator Value)[] Pairs = pairs;
        public readonly JsonElement[]? SeenKeyElements = seenKeyElements;
        public readonly int[]? SeenPairIndices = seenPairIndices;
        public readonly JsonElement Input = input;
        public readonly Environment Env = env;
    }

    private static ExpressionEvaluator CompileFunctionCall(FunctionCallNode func)
    {
        // Fully-constant $zip: evaluate at compile time, zero per-call cost.
        if (func.Procedure is VariableNode zipCheck
            && zipCheck.Name == "zip"
            && func.Arguments.Count > 0
            && func.Arguments.All(a => a is ArrayConstructorNode && IsConstantExpression(a)))
        {
            return CompileConstantZip(func);
        }

        // Buffer-fused $zip: when all arguments are simple property chains (or constants),
        // navigate chains directly into ElementBuffers and build the zip result in a single
        // document — eliminating intermediate Sequence wrappers and rented arrays.
        if (func.Procedure is VariableNode zipFusedCheck
            && zipFusedCheck.Name == "zip"
            && func.Arguments.Count >= 2
            && func.Arguments.Count <= 3)
        {
            // Classify each arg: chain, constant, or other
            bool allFusible = true;
            bool anyChain = false;
            int chainCount = 0;
            int constCount = 0;

            // Use fixed-size arrays to avoid allocation during classification
            byte[][]?[] chainNames = new byte[][]?[func.Arguments.Count];
            JsonElement[] constElements = new JsonElement[func.Arguments.Count];
            bool[] isConst = new bool[func.Arguments.Count];

            for (int i = 0; i < func.Arguments.Count; i++)
            {
                if (TryExtractSimpleChainNames(func.Arguments[i], out var names))
                {
                    chainNames[i] = names;
                    anyChain = true;
                    chainCount++;
                }
                else if (IsConstantExpression(func.Arguments[i]))
                {
                    string json = SerializeConstantJson(func.Arguments[i]);
                    byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(json);
                    constElements[i] = JsonElement.ParseValue(utf8);
                    isConst[i] = true;
                    constCount++;
                }
                else
                {
                    allFusible = false;
                    break;
                }
            }

            if (allFusible && anyChain)
            {
                if (func.Arguments.Count == 2)
                {
                    if (chainCount == 2)
                    {
                        return CompileBufferFusedZip2(chainNames[0]!, chainNames[1]!);
                    }

                    // Mixed: one constant + one chain
                    if (isConst[0])
                    {
                        return CompileBufferFusedZipMixed2(constElements[0], constIsFirst: true, chainNames[1]!);
                    }
                    else
                    {
                        return CompileBufferFusedZipMixed2(constElements[1], constIsFirst: false, chainNames[0]!);
                    }
                }

                if (func.Arguments.Count == 3 && chainCount == 3)
                {
                    return CompileBufferFusedZip3(chainNames[0]!, chainNames[1]!, chainNames[2]!);
                }

                // 3-arg with mixed const/chain falls through to standard path
            }
        }

        var args = func.Arguments.Select(Compile).ToArray();

        // Check for built-in functions by name.
        // Built-in functions are compiled directly and do NOT participate
        // in tail-call optimization — they are always evaluated inline.
        // However, user-defined functions can shadow built-in names (e.g. defining
        // $match inside a closure), so we emit a runtime check that falls back to
        // the user binding when one exists.
        if (func.Procedure is VariableNode varProc)
        {
            var builtIn = BuiltInFunctions.TryGetCompiler(varProc.Name);
            if (builtIn is not null)
            {
                var builtInEval = builtIn(args);
                string varName = varProc.Name;

                return (in JsonElement input, Environment env) =>
                {
                    // Runtime scope check: if the variable is rebound to a lambda,
                    // the user's function shadows the built-in
                    if (env.TryLookup(varName, out Sequence rebound) && rebound.IsLambda)
                    {
                        int argCount = args.Length;
                        var evalArgs = ArrayPool<Sequence>.Shared.Rent(argCount);
                        for (int i = 0; i < argCount; i++)
                        {
                            evalArgs[i] = args[i](input, env);
                        }

                        try
                        {
                            return rebound.Lambda!.Invoke(evalArgs, argCount, input, env);
                        }
                        finally
                        {
                            evalArgs.AsSpan(0, argCount).Clear();
                            ArrayPool<Sequence>.Shared.Return(evalArgs);
                        }
                    }

                    return builtInEval(input, env);
                };
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
                int argCount = args.Length;

                if (isTailCall)
                {
                    // Tail-call: args live in the continuation until the trampoline consumes them
                    var evaluatedArgs = new Sequence[argCount];
                    for (int i = 0; i < argCount; i++)
                    {
                        evaluatedArgs[i] = args[i](input, env);
                    }

                    return new Sequence(new TailCallContinuation(funcResult.Lambda!, evaluatedArgs, argCount, input, env));
                }

                var rentedArgs = ArrayPool<Sequence>.Shared.Rent(argCount);
                for (int i = 0; i < argCount; i++)
                {
                    rentedArgs[i] = args[i](input, env);
                }

                try
                {
                    return funcResult.Lambda!.Invoke(rentedArgs, argCount, input, env);
                }
                finally
                {
                    rentedArgs.AsSpan(0, argCount).Clear();
                    ArrayPool<Sequence>.Shared.Return(rentedArgs);
                }
            }

            if (suggestedBuiltIn is not null)
            {
                throw new JsonataException("T1005", SR.Format(SR.T1005_AttemptedToInvokeNonFunctionSuggestion, suggestedBuiltIn), func.Position);
            }

            throw new JsonataException("T1006", SR.T1006_AttemptedToInvokeANonFunction, func.Position);
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
            throw new JsonataException("D1001", SR.D1001_BindLhsMustBeAVariable, bind.Position);
        }

        var name = varNode.Name;
        return (in JsonElement input, Environment env) =>
        {
            var value = rhs(input, env);
            env.Bind(name, value);

            // The binding and the return share the same backing array.
            // Return an owned copy so the caller can safely return it.
            return value.CopyOwned();
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
                    int allArgCount = originalArgs.Length + 1;
                    var allArgs = ArrayPool<Sequence>.Shared.Rent(allArgCount);
                    allArgs[0] = lhsResult;
                    for (int i = 0; i < originalArgs.Length; i++)
                    {
                        allArgs[i + 1] = originalArgs[i](input, env);
                    }

                    try
                    {
                        return funcResult.Lambda!.Invoke(allArgs, allArgCount, input, env);
                    }
                    finally
                    {
                        allArgs.AsSpan(0, allArgCount).Clear();
                        ArrayPool<Sequence>.Shared.Return(allArgs);
                    }
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
                        (ReadOnlySpan<Sequence> args, in JsonElement compInput, Environment compEnv) =>
                        {
                            var intermediate = lhsLambda.Invoke(args.ToArray(), args.Length, compInput, compEnv);
                            return rhsLambda.Invoke([intermediate], 1, compInput, compEnv);
                        },
                        lhsLambda.Arity > 0 ? lhsLambda.Arity : 1);
                    return new Sequence(composedLambda);
                }

                // LHS is a value — invoke RHS with LHS as argument
                var args = new[] { lhsResult };
                return rhsResult.Lambda!.Invoke(args, 1, input, env);
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
                throw new JsonataException("T2006", SR.T2006_TheRightSideOfTheOperatorMustBeAFunction, apply.Position);
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
            var elements = default(SequenceBuilder);
            if (input.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in input.EnumerateArray())
                {
                    elements.Add(item);
                }
            }
            else
            {
                elements.Add(input);
            }

            if (elements.Count <= 1)
            {
                elements.ReturnArray();
                return new Sequence(input);
            }

            // Stable sort using index-based ordering to preserve relative
            // order of equal elements (matches JSONata reference semantics).
            var indices = RentSortIndices(elements.Count);
            SortIndices(indices, elements.Count, (a, b) =>
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

            var sorted = default(SequenceBuilder);
            for (int i = 0; i < elements.Count; i++)
            {
                sorted.Add(elements[indices[i]]);
            }

            ReturnSortIndices(indices);
            elements.ReturnArray();

            // Build result as a JSON array so subsequent path steps flatten it
            return new Sequence(JsonataHelpers.ArrayFromBuilder(ref sorted, env.Workspace));
        };
    }

    /// <summary>
    /// Compiles a focus-aware sort stage that re-binds the focus variable to each
    /// comparison element before evaluating the sort key. This is necessary for
    /// sort expressions like <c>^($e.Surname)</c> inside a focus step <c>@$e</c>,
    /// where the key references the focus variable rather than the input context.
    /// </summary>
    private static ExpressionEvaluator CompileFocusSortStage(SortNode sort, string focusVar)
    {
        var terms = new (ExpressionEvaluator Expr, bool Descending)[sort.Terms.Count];
        for (int i = 0; i < sort.Terms.Count; i++)
        {
            terms[i] = (Compile(sort.Terms[i].Expression), sort.Terms[i].Descending);
        }

        return (in JsonElement input, Environment env) =>
        {
            var elements = default(SequenceBuilder);
            if (input.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in input.EnumerateArray())
                {
                    elements.Add(item);
                }
            }
            else
            {
                elements.Add(input);
            }

            if (elements.Count <= 1)
            {
                elements.ReturnArray();
                return new Sequence(input);
            }

            var indices = RentSortIndices(elements.Count);
            SortIndices(indices, elements.Count, (a, b) =>
            {
                for (int t = 0; t < terms.Length; t++)
                {
                    var (expr, desc) = terms[t];

                    // Re-bind focus variable for each comparison element
                    env.Bind(focusVar, new Sequence(elements[a]));
                    var aVal = expr(elements[a], env);
                    env.Bind(focusVar, new Sequence(elements[b]));
                    var bVal = expr(elements[b], env);

                    int cmp = CompareSortKeys(aVal, bVal);
                    if (cmp != 0)
                    {
                        return desc ? -cmp : cmp;
                    }
                }

                return a.CompareTo(b);
            });

            var sorted = default(SequenceBuilder);
            for (int i = 0; i < elements.Count; i++)
            {
                sorted.Add(elements[indices[i]]);
            }

            ReturnSortIndices(indices);
            elements.ReturnArray();

            return new Sequence(JsonataHelpers.ArrayFromBuilder(ref sorted, env.Workspace));
        };
    }

    /// <summary>
    /// Sorts a list of elements with focus-variable re-binding per comparison.
    /// Used when a sort step follows a focus step (e.g. <c>Employee@$e^($e.Surname)</c>)
    /// and the sort key expression references the focus variable.
    /// </summary>
    private static Sequence FocusSort(
        SequenceBuilder elements,
        (ExpressionEvaluator Expr, bool Descending)[] sortTerms,
        Environment env,
        string focusVar)
    {
        if (elements.Count <= 1)
        {
            return elements.Count == 1 ? new Sequence(elements[0]) : Sequence.Undefined;
        }

        var indices = RentSortIndices(elements.Count);
        SortIndices(indices, elements.Count, (a, b) =>
        {
            for (int t = 0; t < sortTerms.Length; t++)
            {
                var (expr, desc) = sortTerms[t];
                env.Bind(focusVar, new Sequence(elements[a]));
                var aVal = expr(elements[a], env);
                env.Bind(focusVar, new Sequence(elements[b]));
                var bVal = expr(elements[b], env);

                int cmp = CompareSortKeys(aVal, bVal);
                if (cmp != 0)
                {
                    return desc ? -cmp : cmp;
                }
            }

            return a.CompareTo(b);
        });

        var sorted = ArrayPool<JsonElement>.Shared.Rent(elements.Count);
        for (int i = 0; i < elements.Count; i++)
        {
            sorted[i] = elements[indices[i]];
        }

        ReturnSortIndices(indices);

        return new Sequence(sorted, elements.Count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int[] RentSortIndices(int count)
    {
        int[] indices = ArrayPool<int>.Shared.Rent(count);
        for (int i = 0; i < count; i++)
        {
            indices[i] = i;
        }

        return indices;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReturnSortIndices(int[] indices)
    {
        ArrayPool<int>.Shared.Return(indices);
    }

    /// <summary>
    /// Sorts a rented index array by the given sort terms.
    /// Extracted from inline lambdas to prevent the compiler from hoisting
    /// <paramref name="env"/> into a display-class on the calling method's
    /// entry path — the display-class would allocate on every invocation
    /// even when sorting is not needed.
    /// </summary>
    private static void SortByTerms(
        int[] indices,
        SequenceBuilder elements,
        (ExpressionEvaluator Expr, bool Descending)[] terms,
        Environment env)
    {
        SortIndices(indices, elements.Count, (a, b) =>
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
    }

    /// <summary>
    /// Sorts a rented index array using the given comparison.
    /// Uses Span.Sort on .NET (avoids Comparer wrapper allocation);
    /// falls back to Array.Sort on netstandard.
    /// </summary>
    private static void SortIndices(int[] indices, int count, Comparison<int> comparison)
    {
#if NET
        indices.AsSpan(0, count).Sort(comparison);
#else
        Array.Sort(indices, 0, count, Comparer<int>.Create(comparison));
#endif
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
            throw new JsonataException("T2008", SR.T2008_OrderByMustBeNumericOrString, 0);
        }

        if (!bIsNum && !bIsStr)
        {
            throw new JsonataException("T2008", SR.T2008_OrderByMustBeNumericOrString, 0);
        }

        // T2007: types must be consistent (both numbers or both strings)
        if (aIsNum != bIsNum)
        {
            throw new JsonataException("T2007", SR.T2007_TypeMismatchWithinOrderByClauseAllValuesMustBeOfTheSameType, 0);
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
                        throw new JsonataException("T2011", SR.T2011_TransformRhsMustBeObject, 0);
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
                            throw new JsonataException("T2012", SR.T2012_TransformDeleteClauseMustBeString, 0);
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

    private static ExpressionEvaluator CompileParent(ParentNode parent)
    {
        string label = parent.Slot.Label;
        return (in JsonElement input, Environment env) =>
        {
            if (env.TryLookup(label, out var result))
            {
                return result;
            }

            return Sequence.Undefined;
        };
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
                    (ReadOnlySpan<Sequence> runtimeArgs, in JsonElement innerInput, Environment innerEnv) =>
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
                    throw new JsonataException(code, SR.T1007_AttemptedToPartiallyApplyANonFunction, partial.Position);
                }

                var originalLambda = funcResult.Lambda!;

                return new Sequence(new LambdaValue(
                    (ReadOnlySpan<Sequence> runtimeArgs, in JsonElement innerInput, Environment innerEnv) =>
                    {
                        int fullArgCount = capturedArgs.Length;
                        var fullArgs = ArrayPool<Sequence>.Shared.Rent(fullArgCount);
                        int pIdx = 0;
                        for (int i = 0; i < fullArgCount; i++)
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

                        try
                        {
                            return originalLambda.Invoke(fullArgs, fullArgCount, innerInput, innerEnv);
                        }
                        finally
                        {
                            fullArgs.AsSpan(0, fullArgCount).Clear();
                            ArrayPool<Sequence>.Shared.Return(fullArgs);
                        }
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
#if NET
                            using var utf16 = element.GetUtf16String();
                            return TryParseSpecialRadix(utf16.Span, out value);
#else
                            // Fall back to string for hex/binary/octal parsing
                            string s = element.GetString()!;
                            return TryParseSpecialRadix(s, out value);
#endif
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

#if NET
    /// <summary>
    /// Parses hex (0x), binary (0b), or octal (0o) prefixed spans to double.
    /// </summary>
    private static bool TryParseSpecialRadix(ReadOnlySpan<char> s, out double value)
    {
        char prefix = s[1];
        ReadOnlySpan<char> digits = s.Slice(2);

        if (prefix is 'x' or 'X')
        {
            if (long.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hex))
            {
                value = hex;
                return true;
            }

            value = 0;
            return false;
        }

        if (prefix is 'b' or 'B')
        {
            long result = 0;
            for (int i = 0; i < digits.Length; i++)
            {
                char c = digits[i];
                if (c is not ('0' or '1'))
                {
                    value = 0;
                    return false;
                }

                result = (result << 1) | (long)(uint)(c - '0');
            }

            value = result;
            return digits.Length > 0;
        }

        if (prefix is 'o' or 'O')
        {
            long result = 0;
            for (int i = 0; i < digits.Length; i++)
            {
                char c = digits[i];
                if (c < '0' || c > '7')
                {
                    value = 0;
                    return false;
                }

                result = (result << 3) | (long)(uint)(c - '0');
            }

            value = result;
            return digits.Length > 0;
        }

        value = 0;
        return false;
    }
#endif

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

        if (seq.TryGetDouble(out double d))
        {
            return d != 0 && !double.IsNaN(d);
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
    internal static int Utf8CompareOrdinal(JsonElement a, JsonElement b)
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
            throw new JsonataException("D3001", SR.D3001_AStringCannotBeGeneratedFromThisValue, 0);
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
            (ReadOnlySpan<Sequence> args, in JsonElement input, Environment env) =>
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

                throw new JsonataException("T0410", SR.T0410_UnableToInvokeBuiltInFunction, 0);
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