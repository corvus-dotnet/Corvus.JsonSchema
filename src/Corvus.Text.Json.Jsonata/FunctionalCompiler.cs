// <copyright file="FunctionalCompiler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
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

        // Compile each step
        var steps = new ExpressionEvaluator[path.Steps.Count];
        for (int i = 0; i < path.Steps.Count; i++)
        {
            steps[i] = Compile(path.Steps[i]);
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

                if (current.IsSingleton)
                {
                    // Hot path: singleton input → evaluate step directly
                    var element = current.FirstOrDefault;

                    // Auto-flatten arrays for path traversal
                    if (element.ValueKind == JsonValueKind.Array && stepIdx > 0)
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
                        Sequence stepResult;

                        if (element.ValueKind == JsonValueKind.Array && stepIdx > 0)
                        {
                            stepResult = FlattenArrayStep(element, step, env);
                        }
                        else
                        {
                            stepResult = step(element, env);
                        }

                        builder.AddRange(stepResult);
                    }

                    current = builder.ToSequence();
                }
            }

            return current;
        };
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

            if (!TryCoerceToNumber(left.FirstOrDefault, out double leftNum)
                || !TryCoerceToNumber(right.FirstOrDefault, out double rightNum))
            {
                return Sequence.Undefined;
            }

            double result = op(leftNum, rightNum);
            return new Sequence(CreateNumberElement(result));
        };
    }

    private static ExpressionEvaluator CompileComparison(ExpressionEvaluator lhs, ExpressionEvaluator rhs)
    {
        return (in JsonElement input, Environment env) =>
        {
            var left = lhs(input, env);
            var right = rhs(input, env);

            if (left.IsUndefined && right.IsUndefined)
            {
                return new Sequence(CreateBoolElement(true));
            }

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

            if (left.IsUndefined || right.IsUndefined)
            {
                return Sequence.Undefined;
            }

            var l = left.FirstOrDefault;
            var r = right.FirstOrDefault;

            // String comparison
            if (l.ValueKind == JsonValueKind.String && r.ValueKind == JsonValueKind.String)
            {
                int result = string.CompareOrdinal(l.GetString(), r.GetString());
                return new Sequence(CreateBoolElement(cmp(result, 0)));
            }

            // Numeric comparison
            if (TryCoerceToNumber(l, out double leftNum) && TryCoerceToNumber(r, out double rightNum))
            {
                return new Sequence(CreateBoolElement(cmp(leftNum, rightNum)));
            }

            return Sequence.Undefined;
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

            if (left.IsUndefined || right.IsUndefined)
            {
                return Sequence.Undefined;
            }

            if (!TryCoerceToNumber(left.FirstOrDefault, out double start)
                || !TryCoerceToNumber(right.FirstOrDefault, out double end))
            {
                return Sequence.Undefined;
            }

            int iStart = (int)Math.Floor(start);
            int iEnd = (int)Math.Floor(end);

            if (iStart > iEnd)
            {
                return Sequence.Undefined;
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

                if (TryCoerceToNumber(result.FirstOrDefault, out double num))
                {
                    return new Sequence(CreateNumberElement(-num));
                }

                return Sequence.Undefined;
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
                writer.WritePropertyName(keyStr);

                if (valueResult.IsUndefined)
                {
                    writer.WriteNullValue();
                }
                else if (valueResult.IsSingleton)
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
        var procedure = Compile(func.Procedure);
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

        // Generic function call (user-defined / lambda)
        return (in JsonElement input, Environment env) =>
        {
            var funcResult = procedure(input, env);
            if (funcResult.IsUndefined)
            {
                throw new JsonataException("T1005", "Attempted to invoke a non-function", func.Position);
            }

            // Lambda call handling is done through the environment
            if (env.TryLookup("__lambda__" + func.Procedure.Position, out var lambda))
            {
                // TODO: Lambda invocation
            }

            return Sequence.Undefined;
        };
    }

    private static ExpressionEvaluator CompileLambda(LambdaNode lambda)
    {
        var body = Compile(lambda.Body);
        var paramNames = lambda.Parameters.ToArray();

        return (in JsonElement input, Environment env) =>
        {
            // Capture the defining environment for closure
            var definingEnv = env;

            // Return a function reference — represented as a special binding
            // For now, store the lambda information in the environment
            // This will be invoked when used in a function call context
            return Sequence.Undefined; // TODO: Function values
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
        var rhsEval = Compile(apply.Rhs);

        return (in JsonElement input, Environment env) =>
        {
            // ~> chains: evaluate LHS, pass as first arg to RHS
            var lhsResult = lhsEval(input, env);

            // TODO: Full function application semantics
            return rhsEval(input, env);
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
        // TODO: Full sort implementation
        return static (in JsonElement input, Environment env) => new Sequence(input);
    }

    private static ExpressionEvaluator CompileTransform(TransformNode transform)
    {
        // TODO: Full transform implementation
        return static (in JsonElement input, Environment env) => new Sequence(input);
    }

    private static ExpressionEvaluator CompileRegex(RegexNode regex)
    {
        // Regex is a value that's used by $match/$replace etc.
        // Store as a string representation for now
        return static (in JsonElement input, Environment env) => Sequence.Undefined;
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
                return double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
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
            JsonValueKind.Number => element.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            JsonValueKind.Undefined => string.Empty,
            _ => element.GetRawText(),
        };
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsTruthyElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Undefined => false,
            JsonValueKind.Null => false,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.GetDouble() != 0,
            JsonValueKind.String => element.GetString()?.Length > 0,
            JsonValueKind.Array => element.GetArrayLength() > 0,
            JsonValueKind.Object => true,
            JsonValueKind.True => true,
            _ => false,
        };
    }

    internal static bool JsonElementEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            // Cross-type numeric comparison
            if (TryCoerceToNumber(a, out double na) && TryCoerceToNumber(b, out double nb))
            {
                return na == nb;
            }

            return false;
        }

        return a.ValueKind switch
        {
            JsonValueKind.Number => a.GetDouble() == b.GetDouble(),
            JsonValueKind.String => a.GetString() == b.GetString(),
            JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => true,
            _ => a.GetRawText() == b.GetRawText(),
        };
    }

    internal static JsonElement CreateNumberElement(double value)
    {
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
        using var writer = new Utf8JsonWriter(ms);
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
}